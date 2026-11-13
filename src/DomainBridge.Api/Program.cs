using System.Text.Json;
using DomainBridge.Contracts;
using System.Net.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Options;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var isDevelopmentOrTest = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing");
var agentTransportRequired = !isDevelopmentOrTest;
var operatorAccess = builder.Configuration.GetSection(OperatorAccessOptions.SectionName).Get<OperatorAccessOptions>() ?? new();
var operatorAccessRequired = !isDevelopmentOrTest || operatorAccess.Enforce;
if (operatorAccessRequired && (!operatorAccess.HasAnyGroup() || operatorAccess.InvalidEntries().Count > 0))
{
    throw new InvalidOperationException(
        "OperatorAccess must list at least one group, and every group must be a security identifier (S-1-...).");
}

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
    isDevelopmentOrTest,
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

builder.Services.Configure<OperatorAccessOptions>(builder.Configuration.GetSection(OperatorAccessOptions.SectionName));
if (operatorAccessRequired)
{
    // Operators sign in with their Windows session; roles come from Active Directory groups.
    builder.Services.AddTransient<IClaimsTransformation, OperatorRoleClaimsTransformation>();
    var authentication = builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme);
    if (!builder.Environment.IsEnvironment("Testing"))
    {
        // The in-memory test server cannot carry Windows authentication; tests register
        // a stand-in scheme. With no scheme at all, protected routes fail closed.
        authentication.AddNegotiate();
    }
    builder.Services.AddAuthorizationBuilder()
        .AddPolicy(OperatorPolicies.Read, policy => policy
            .RequireAuthenticatedUser()
            .RequireClaim(OperatorRoles.ClaimType, OperatorRoles.All))
        .AddPolicy(OperatorPolicies.Prepare, policy => policy
            .RequireAuthenticatedUser()
            .RequireClaim(OperatorRoles.ClaimType, OperatorRoles.Preparer))
        .AddPolicy(OperatorPolicies.Approve, policy => policy
            .RequireAuthenticatedUser()
            .RequireClaim(OperatorRoles.ClaimType, OperatorRoles.Approver))
        .AddPolicy(OperatorPolicies.Technician, policy => policy
            .RequireAuthenticatedUser()
            .RequireClaim(OperatorRoles.ClaimType, OperatorRoles.Technician));
}

builder.Services.Configure<AgentIdentityOptions>(builder.Configuration.GetSection(AgentIdentityOptions.SectionName));
builder.Services.AddSingleton<IAgentIssuerChainReader>(_ => new X509AgentIssuerChainReader());

var app = builder.Build();

var agentIdentity = app.Services.GetRequiredService<IOptions<AgentIdentityOptions>>().Value;
if (agentTransportRequired && agentIdentity.NormalizedIssuerThumbprints().Count == 0)
{
    throw new InvalidOperationException(
        "AgentIdentity:TrustedIssuerThumbprints must pin at least one issuing authority outside development and test.");
}

var agentIdentityRequired = agentTransportRequired || agentIdentity.Enforce;

AgentIdentityResult ResolveAgent(HttpRequest request, IAgentIssuerChainReader chainReader) =>
    AgentIdentityResolver.Resolve(
        request.HttpContext.Connection.ClientCertificate,
        agentIdentityRequired,
        agentIdentity,
        chainReader);

IResult RejectIdentityMismatch(IAuditStore auditStore, AgentIdentityResult caller, string claimedDeviceId)
{
    auditStore.Append(new AuditEvent(
        Guid.NewGuid(),
        DateTimeOffset.UtcNow,
        "agent.identity.rejected",
        claimedDeviceId,
        AuditActors.Agent(caller.DeviceId),
        AuditOutcomes.Denied));

    return Results.Json(new { error = "agent_identity_mismatch" }, statusCode: StatusCodes.Status403Forbidden);
}

app.UseDefaultFiles();
app.UseStaticFiles();
if (operatorAccessRequired)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

var console = app.MapGroup("/api/v1/console");
if (operatorAccessRequired)
{
    console.RequireAuthorization(OperatorPolicies.Read);
}

{
    console.MapGet("/me", (HttpContext context) => Results.Ok(new
    {
        name = context.User.Identity?.Name ?? "anonymous",
        roles = context.User.FindAll(OperatorRoles.ClaimType).Select(claim => claim.Value).Distinct().ToArray()
    }));

    console.MapGet("/overview", (IAuditStore auditStore, IInventoryStore inventoryStore) =>
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

    console.MapGet("/agents", (int? limit, IInventoryStore inventoryStore) =>
    {
        if (!ConsoleQuery.IsValidLimit(limit))
        {
            return Results.BadRequest(new { error = "invalid_limit" });
        }

        return Results.Ok(inventoryStore.GetRecent(ConsoleQuery.ResolveLimit(limit)));
    });

    console.MapGet("/audit", (int? limit, IAuditStore auditStore) =>
    {
        if (!ConsoleQuery.IsValidLimit(limit))
        {
            return Results.BadRequest(new { error = "invalid_limit" });
        }

        return Results.Ok(auditStore.GetRecent(ConsoleQuery.ResolveLimit(limit)));
    });

}

app.MapPost("/api/v1/enrollments", async (HttpRequest request, IAuditStore auditStore, IAgentIssuerChainReader chainReader) =>
{
    var caller = ResolveAgent(request, chainReader);
    if (!caller.IsSuccess)
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

        // The caller is whoever the certificate says it is; the body cannot claim otherwise.
        if (!caller.IsAnonymous &&
            (!string.Equals(enrollment.Value!.AgentId, caller.DeviceId, StringComparison.OrdinalIgnoreCase) ||
             !string.Equals(enrollment.Value.CertificateThumbprint, caller.Thumbprint, StringComparison.OrdinalIgnoreCase)))
        {
            return RejectIdentityMismatch(auditStore, caller, enrollment.Value.AgentId);
        }

        auditStore.Append(new AuditEvent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "agent.enrolled",
            enrollment.Value!.AgentId,
            AuditActors.Agent(caller.DeviceId)));

        return Results.Ok(new EnrollmentResponse(
            enrollment.Value!.AgentId,
            enrollment.Value.CertificateThumbprint,
            enrollment.Value.AgentVersion));
    }
});

app.MapPost("/api/v1/inventory", async (HttpRequest request, IAuditStore auditStore, IInventoryStore inventoryStore, IAgentIssuerChainReader chainReader) =>
{
    var caller = ResolveAgent(request, chainReader);
    if (!caller.IsSuccess)
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

        if (!caller.IsAnonymous &&
            !string.Equals(payload.DeviceId.Trim(), caller.DeviceId, StringComparison.OrdinalIgnoreCase))
        {
            return RejectIdentityMismatch(auditStore, caller, payload.DeviceId.Trim());
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
            inventory.DeviceId,
            AuditActors.Agent(caller.DeviceId)));

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
