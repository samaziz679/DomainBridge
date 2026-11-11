public sealed class ConsoleAccessOptions
{
    public const string SectionName = "Console";

    public bool EnableReadApi { get; init; }

    public string? Authority { get; init; }

    public string? ClientId { get; init; }

    public string? Audience { get; init; }

    public string RequiredRole { get; init; } = "DomainBridge.Console.Reader";

    public bool IsReady()
    {
        return EnableReadApi &&
            Uri.TryCreate(Authority, UriKind.Absolute, out var authority) &&
            authority.Scheme == Uri.UriSchemeHttps &&
            !string.IsNullOrWhiteSpace(ClientId) &&
            !string.IsNullOrWhiteSpace(Audience) &&
            !string.IsNullOrWhiteSpace(RequiredRole);
    }
}
