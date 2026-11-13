using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace DomainBridge.Api.Tests;

public sealed class OperatorAccessTests : IClassFixture<WebApplicationFactory<Program>>
{
    // Synthetic security identifiers: source-domain and target-domain groups.
    private const string ReadersInA = "S-1-5-21-1111111111-1111111111-1111111111-2001";
    private const string ReadersInB = "S-1-5-21-2222222222-2222222222-2222222222-2001";
    private const string Preparers = "S-1-5-21-1111111111-1111111111-1111111111-2002";
    private const string Approvers = "S-1-5-21-1111111111-1111111111-1111111111-2003";
    private const string Unrelated = "S-1-5-21-1111111111-1111111111-1111111111-9999";

    private readonly WebApplicationFactory<Program> _factory;

    public OperatorAccessTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(host =>
        {
            host.UseEnvironment("Testing");
            host.UseSetting("OperatorAccess:Enforce", "true");
            host.UseSetting("OperatorAccess:ReaderGroupSids:0", ReadersInA);
            host.UseSetting("OperatorAccess:ReaderGroupSids:1", ReadersInB);
            host.UseSetting("OperatorAccess:PreparerGroupSids:0", Preparers);
            host.UseSetting("OperatorAccess:ApproverGroupSids:0", Approvers);
            host.ConfigureServices(services =>
            {
                services.AddAuthentication(TestOperatorHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestOperatorHandler>(TestOperatorHandler.SchemeName, _ => { });
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultScheme = TestOperatorHandler.SchemeName;
                    options.DefaultAuthenticateScheme = TestOperatorHandler.SchemeName;
                    options.DefaultChallengeScheme = TestOperatorHandler.SchemeName;
                });
            });
        });
    }

    private HttpClient ClientFor(string? user, params string[] groupSids)
    {
        var client = _factory.CreateClient();
        if (user is not null)
        {
            client.DefaultRequestHeaders.Add(TestOperatorHandler.UserHeader, user);
            client.DefaultRequestHeaders.Add(TestOperatorHandler.GroupsHeader, string.Join(',', groupSids));
        }

        return client;
    }

    [Theory]
    [InlineData("/api/v1/console/overview")]
    [InlineData("/api/v1/console/agents")]
    [InlineData("/api/v1/console/audit")]
    [InlineData("/api/v1/console/me")]
    public async Task Console_routes_reject_callers_without_identity(string path)
    {
        var response = await ClientFor(null).GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_account_in_no_configured_group_is_forbidden()
    {
        var response = await ClientFor(@"LAB-A\visitor", Unrelated).GetAsync("/api/v1/console/overview");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(ReadersInA)]
    [InlineData(ReadersInB)]
    public async Task Reader_group_of_either_domain_can_read(string groupSid)
    {
        var response = await ClientFor(@"LAB-A\reader.synth", groupSid).GetAsync("/api/v1/console/overview");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Any_operator_role_can_read()
    {
        var response = await ClientFor(@"LAB-A\prep.synth", Preparers).GetAsync("/api/v1/console/audit");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Me_reports_the_account_and_exactly_its_roles()
    {
        var me = await ClientFor(@"LAB-A\both.synth", Preparers, Approvers, Unrelated)
            .GetFromJsonAsync<OperatorIdentity>("/api/v1/console/me");

        Assert.NotNull(me);
        Assert.Equal(@"LAB-A\both.synth", me.Name);
        Assert.Equal([OperatorRoles.Approver, OperatorRoles.Preparer], me.Roles.Order());
    }

    [Fact]
    public async Task A_role_claim_supplied_by_the_caller_is_ignored()
    {
        var client = ClientFor(@"LAB-A\visitor", Unrelated);
        client.DefaultRequestHeaders.Add(TestOperatorHandler.ForgedRoleHeader, OperatorRoles.Approver);

        var response = await client.GetAsync("/api/v1/console/overview");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Agent_routes_do_not_depend_on_an_operator_identity()
    {
        var response = await ClientFor(null).PostAsJsonAsync("/api/v1/inventory", new
        {
            deviceId = "SPARE01",
            operatingSystem = "Windows 11",
            architecture = "X64",
            agentVersion = "agent/1.0",
            capturedAtUtc = "2026-11-13T00:00:00+00:00"
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Health_stays_public()
    {
        var response = await ClientFor(null).GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void Production_host_refuses_to_start_without_operator_groups()
    {
        using var production = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment("Production");
            host.UseSetting("Storage:DatabasePath", Path.Combine(Path.GetTempPath(), "domainbridge-start-" + Guid.NewGuid().ToString("N"), "db.sqlite"));
            host.UseSetting("AgentIdentity:TrustedIssuerThumbprints:0", new string('A', 40));
        });

        var failure = Assert.ThrowsAny<Exception>(() => production.CreateClient());

        Assert.Contains("OperatorAccess", failure.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Group_names_are_rejected_in_favour_of_security_identifiers()
    {
        var options = new OperatorAccessOptions
        {
            ReaderGroupSids = [ReadersInA, @"LAB-A\DomainBridge Readers", "", "S-1-5-32-545"]
        };

        Assert.Equal([@"LAB-A\DomainBridge Readers", ""], options.InvalidEntries());
    }

    [Fact]
    public void Roles_follow_group_membership_only()
    {
        var options = new OperatorAccessOptions
        {
            ReaderGroupSids = [ReadersInA],
            ApproverGroupSids = [Approvers]
        };

        Assert.Empty(options.RolesFor([Unrelated]));
        Assert.Equal([OperatorRoles.Approver], options.RolesFor([Approvers.ToLowerInvariant(), Unrelated]));
    }

    private sealed record OperatorIdentity(string Name, string[] Roles);

    /// <summary>
    /// Stands in for Windows authentication: the test states which account is
    /// signed in and which groups it belongs to, as Negotiate would report them.
    /// </summary>
    private sealed class TestOperatorHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "TestOperator";
        public const string UserHeader = "X-Test-User";
        public const string GroupsHeader = "X-Test-Groups";
        public const string ForgedRoleHeader = "X-Test-Forged-Role";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(UserHeader, out var user))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim> { new(ClaimTypes.Name, user.ToString()) };
            claims.AddRange(Request.Headers[GroupsHeader].ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(sid => new Claim(ClaimTypes.GroupSid, sid)));
            if (Request.Headers.TryGetValue(ForgedRoleHeader, out var forged))
            {
                claims.Add(new Claim(OperatorRoles.ClaimType, forged.ToString()));
            }

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }
}
