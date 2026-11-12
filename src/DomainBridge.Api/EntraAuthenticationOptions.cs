public sealed class EntraAuthenticationOptions
{
    public const string SectionName = "EntraAuthentication";

    public string TenantId { get; init; } = string.Empty;
    public string ApiAudience { get; init; } = string.Empty;

    public bool IsReady() =>
        Guid.TryParse(TenantId, out _) &&
        !string.IsNullOrWhiteSpace(ApiAudience);
}
