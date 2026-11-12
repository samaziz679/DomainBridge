using System.Text.Json;
using DomainBridge.Contracts;
using System.Net.Security;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var consoleAccess = builder.Configuration.GetSection(ConsoleAccessOptions.SectionName).Get<ConsoleAccessOptions>() ?? new();
var adminActionSecurity = builder.Configuration.GetSection(AdminActionSecurityOptions.SectionName).Get<AdminActionSecurityOptions>() ?? new();
var trustedPublisherPki = builder.Configuration.GetSection(TrustedPublisherPkiOptions.SectionName).Get<TrustedPublisherPkiOptions>() ?? new();
var entraAuthentication = builder.Configuration.GetSection(EntraAuthenticationOptions.SectionName).Get<EntraAuthenticationOptions>() ?? new();
var agentTransportRequired =
    !(builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"));
var consoleReadApiEnabled =
    builder.Environment.IsDevelopment() ||
    builder.Environment.IsEnvironment("Testing") ||
    consoleAccess.IsReady();
var campaignApiEnabled =
    builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing");

if (agentTransportRequired)
{
    builder.WebHost.ConfigureKestrel(options =>
        options.ConfigureHttpsDefaults(https =>
        {
            https.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
            https.ClientCertificateValidation = (_, _, policyErrors) =>
                policyErrors == SslPolicyErrors.None;
        }));
}

var auditConnectionString = builder.Configuration.GetConnectionString("Audit");
var auditStorageMode = AuditStorageSelector.Select(
    builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"),
    auditConnectionString);

if (auditStorageMode == AuditStorageMode.Postgres)
{
    builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(auditConnectionString!));
    builder.Services.AddSingleton<IAuditStore, PostgresAuditStore>();
    builder.Services.AddSingleton<IInventoryStore, PostgresInventoryStore>();
}
else
{
    builder.Services.AddSingleton<IAuditStore, InMemoryAuditStore>();
    builder.Services.AddSingleton<IInventoryStore, InMemoryInventoryStore>();
}

builder.Services.AddSingleton<IMaintenanceCampaignStore, InMemoryMaintenanceCampaignStore>();
builder.Services.AddSingleton<IMaintenanceJobResultStore, InMemoryMaintenanceJobResultStore>();
builder.Services.AddSingleton(adminActionSecurity);
builder.Services.AddSingleton(trustedPublisherPki);
builder.Services.AddSingleton<IPublisherRootCertificateSource, LocalMachinePublisherRootCertificateSource>();
builder.Services.AddSingleton(entraAuthentication);

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

if (campaignApiEnabled)
{
    app.MapPost("/api/v1/maintenance/campaigns", (
        MaintenanceCampaignRequest? request,
        IMaintenanceCampaignStore campaignStore,
        IAuditStore auditStore) =>
    {
        var remediation = ApprovedRemediationCatalog.Find(request?.RemediationId);
        var targetDeviceIds = request?.TargetDeviceIds?
            .Where(deviceId => !string.IsNullOrWhiteSpace(deviceId))
            .Select(deviceId => deviceId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        if (remediation is null || targetDeviceIds.Length == 0 || targetDeviceIds.Length > 1_000)
        {
            return Results.BadRequest(new { error = "invalid_maintenance_campaign" });
        }

        var campaign = campaignStore.Create(remediation.Id, targetDeviceIds);
        auditStore.Append(new AuditEvent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "maintenance.campaign.created",
            campaign.CampaignId.ToString()));

        return Results.Created(
            $"/api/v1/maintenance/campaigns/{campaign.CampaignId}",
            MaintenanceCampaignResponse.From(campaign, remediation.RequiresApproval));
    });

    app.MapPost("/api/v1/maintenance/campaigns/{campaignId:guid}/approve", (
        Guid campaignId,
        IMaintenanceCampaignStore campaignStore,
        IAuditStore auditStore) =>
    {
        var campaign = campaignStore.Approve(campaignId);
        if (campaign is null)
        {
            return Results.NotFound(new { error = "maintenance_campaign_not_found" });
        }

        var remediation = ApprovedRemediationCatalog.Find(campaign.RemediationId)!;
        auditStore.Append(new AuditEvent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "maintenance.campaign.approved",
            campaign.CampaignId.ToString()));

        return Results.Ok(MaintenanceCampaignResponse.From(campaign, remediation.RequiresApproval));
    });

    app.MapGet("/api/v1/agents/{deviceId}/maintenance-jobs", (
        string deviceId,
        IMaintenanceCampaignStore campaignStore) =>
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return Results.BadRequest(new { error = "invalid_device_id" });
        }

        var jobs = campaignStore.GetApprovedForDevice(deviceId.Trim())
            .Select(campaign =>
            {
                var remediation = ApprovedRemediationCatalog.Find(campaign.RemediationId)!;
                return new MaintenanceJob(
                    campaign.CampaignId,
                    remediation.Id,
                    remediation.RequiresApproval,
                    remediation.MayRequireRestart);
            });

        return Results.Ok(jobs);
    });

    app.MapPost("/api/v1/maintenance/campaigns/{campaignId:guid}/results", (
        Guid campaignId,
        MaintenanceJobResultPayload? payload,
        IMaintenanceCampaignStore campaignStore,
        IMaintenanceJobResultStore resultStore,
        IAuditStore auditStore) =>
    {
        var campaign = campaignStore.Find(campaignId);
        if (campaign is null)
        {
            return Results.NotFound(new { error = "maintenance_campaign_not_found" });
        }

        if (campaign.Status != MaintenanceCampaignStatus.Approved)
        {
            return Results.Conflict(new { error = "maintenance_campaign_not_approved" });
        }

        if (string.IsNullOrWhiteSpace(payload?.DeviceId) ||
            !campaign.TargetDeviceIds.Contains(payload.DeviceId.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (!Enum.TryParse<MaintenanceJobOutcome>(payload.Outcome, ignoreCase: true, out var outcome) ||
            !Enum.IsDefined(outcome))
        {
            return Results.BadRequest(new { error = "invalid_maintenance_job_outcome" });
        }

        var result = new MaintenanceJobResult(
            campaign.CampaignId,
            payload.DeviceId.Trim(),
            outcome,
            DateTimeOffset.UtcNow);
        resultStore.Append(result);
        auditStore.Append(new AuditEvent(
            Guid.NewGuid(),
            result.ReportedAtUtc,
            $"maintenance.job.{outcome.ToString().ToLowerInvariant()}",
            result.DeviceId));

        return Results.Accepted($"/api/v1/maintenance/campaigns/{campaign.CampaignId}/results");
    });
}

if (consoleReadApiEnabled)
{
    app.MapGet("/api/v1/console/overview", (IAuditStore auditStore, IInventoryStore inventoryStore) =>
    {
        var recentAudit = auditStore.GetRecent(1).FirstOrDefault();
        return Results.Ok(new
        {
            apiStatus = "healthy",
            enrolledAgents = inventoryStore.GetRecent(1000).Count,
            auditEvents = auditStore.Count,
            lastAuditAction = recentAudit?.Action ?? "none"
        });
    });

    app.MapGet("/api/v1/console/agents", (int? limit, IInventoryStore inventoryStore) =>
    {
        if (!ConsoleQuery.IsValidLimit(limit))
        {
            return Results.BadRequest(new { error = "invalid_limit" });
        }

        return Results.Ok(inventoryStore.GetRecent(ConsoleQuery.ResolveLimit(limit)));
    });

    app.MapGet("/api/v1/console/audit", (int? limit, IAuditStore auditStore) =>
    {
        if (!ConsoleQuery.IsValidLimit(limit))
        {
            return Results.BadRequest(new { error = "invalid_limit" });
        }

        return Results.Ok(auditStore.GetRecent(ConsoleQuery.ResolveLimit(limit)));
    });

    app.MapGet("/api/v1/console/remediations", () => Results.Ok(ApprovedRemediationCatalog.All));
}

app.MapPost("/api/v1/enrollments", async (HttpRequest request, IAuditStore auditStore) =>
{
    var transportValidation = AgentTransportSecurity.Validate(
        request.HttpContext.Connection.ClientCertificate,
        agentTransportRequired);
    if (!transportValidation.IsSuccess)
    {
        return Results.Unauthorized();
    }

    JsonDocument document;

    try
    {
        document = await JsonDocument.ParseAsync(request.Body);
    }
    catch (JsonException)
    {
        return Results.BadRequest(new { error = "invalid_json" });
    }

    using (document)
    {
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            document.RootElement.EnumerateObject().Any(property => !EnrollmentPayload.AllowedFields.Contains(property.Name)))
        {
            return Results.BadRequest(new { error = "unsupported_enrollment_field" });
        }

        var payload = document.RootElement.Deserialize<EnrollmentPayload>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        var enrollment = EnrollmentRequest.Create(
            payload?.AgentId,
            payload?.CertificateThumbprint,
            payload?.AgentVersion);

        if (enrollment.IsFailure)
        {
            return Results.BadRequest(new { error = enrollment.Error.ToString() });
        }

        auditStore.Append(new AuditEvent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "agent.enrolled",
            enrollment.Value!.AgentId));

        return Results.Ok(new EnrollmentResponse(
            enrollment.Value!.AgentId,
            enrollment.Value.CertificateThumbprint,
            enrollment.Value.AgentVersion));
    }
});

app.MapPost("/api/v1/inventory", async (HttpRequest request, IAuditStore auditStore, IInventoryStore inventoryStore) =>
{
    var transportValidation = AgentTransportSecurity.Validate(
        request.HttpContext.Connection.ClientCertificate,
        agentTransportRequired);
    if (!transportValidation.IsSuccess)
    {
        return Results.Unauthorized();
    }

    JsonDocument document;

    try
    {
        document = await JsonDocument.ParseAsync(request.Body);
    }
    catch (JsonException)
    {
        return Results.BadRequest(new { error = "invalid_json" });
    }

    using (document)
    {
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            document.RootElement.EnumerateObject().Any(property => !InventoryPayload.AllowedFields.Contains(property.Name)))
        {
            return Results.BadRequest(new { error = "unsupported_inventory_field" });
        }

        var payload = document.RootElement.Deserialize<InventoryPayload>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (payload is null ||
            string.IsNullOrWhiteSpace(payload.DeviceId) ||
            string.IsNullOrWhiteSpace(payload.OperatingSystem) ||
            string.IsNullOrWhiteSpace(payload.Architecture) ||
            string.IsNullOrWhiteSpace(payload.AgentVersion) ||
            payload.CapturedAtUtc == default)
        {
            return Results.BadRequest(new { error = "invalid_inventory_metadata" });
        }

        var inventory = new DeviceInventoryReport(
            payload.DeviceId.Trim(),
            payload.OperatingSystem.Trim(),
            payload.Architecture.Trim(),
            payload.AgentVersion.Trim(),
            payload.CapturedAtUtc);

        inventoryStore.Upsert(inventory);

        auditStore.Append(new AuditEvent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "agent.inventory.reported",
            inventory.DeviceId));

        return Results.Accepted($"/api/v1/inventory/{inventory.DeviceId}");
    }
});

app.Run();

public sealed record EnrollmentPayload(
    string? AgentId,
    string? CertificateThumbprint,
    string? AgentVersion)
{
    public static readonly ISet<string> AllowedFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "agentId",
        "certificateThumbprint",
        "agentVersion"
    };
}

public sealed record EnrollmentResponse(
    string AgentId,
    string CertificateThumbprint,
    string AgentVersion);

public sealed record InventoryPayload(
    string? DeviceId,
    string? OperatingSystem,
    string? Architecture,
    string? AgentVersion,
    DateTimeOffset CapturedAtUtc)
{
    public static readonly ISet<string> AllowedFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "deviceId",
        "operatingSystem",
        "architecture",
        "agentVersion",
        "capturedAtUtc"
    };
}

public sealed record MaintenanceCampaignRequest(
    string? RemediationId,
    IReadOnlyCollection<string>? TargetDeviceIds);

public sealed record MaintenanceCampaignResponse(
    Guid CampaignId,
    string Status,
    bool RequiresApproval)
{
    public static MaintenanceCampaignResponse From(MaintenanceCampaign campaign, bool requiresApproval) =>
        new(
            campaign.CampaignId,
            campaign.Status.ToString().ToLowerInvariant(),
            requiresApproval);
}

public sealed record MaintenanceJobResultPayload(string? DeviceId, string? Outcome);

public static class ConsoleQuery
{
    private const int DefaultLimit = 25;
    private const int MaximumLimit = 100;

    public static bool IsValidLimit(int? limit) => !limit.HasValue || (limit.Value >= 1 && limit.Value <= MaximumLimit);

    public static int ResolveLimit(int? limit) => limit ?? DefaultLimit;
}

public partial class Program;
