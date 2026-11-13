using System.Text.Json;
using DomainBridge.Contracts;
using System.Net.Security;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var consoleAccess = builder.Configuration.GetSection(ConsoleAccessOptions.SectionName).Get<ConsoleAccessOptions>() ?? new();
var entraAuthentication = builder.Configuration.GetSection(EntraAuthenticationOptions.SectionName).Get<EntraAuthenticationOptions>() ?? new();
var agentTransportRequired =
    !(builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"));
var consoleReadApiEnabled =
    builder.Environment.IsDevelopment() ||
    builder.Environment.IsEnvironment("Testing") ||
    consoleAccess.IsReady();

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

builder.Services.AddSingleton(entraAuthentication);

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

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

public static class ConsoleQuery
{
    private const int DefaultLimit = 25;
    private const int MaximumLimit = 100;

    public static bool IsValidLimit(int? limit) => !limit.HasValue || (limit.Value >= 1 && limit.Value <= MaximumLimit);

    public static int ResolveLimit(int? limit) => limit ?? DefaultLimit;
}

public partial class Program;
