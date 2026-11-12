public sealed class AdminActionSecurityOptions
{
    public const string SectionName = "AdminActions";

    public bool EnableElevatedExecution { get; init; }

    public string RequiredOperatorRole { get; init; } = string.Empty;

    public int MinimumApprovals { get; init; }

    public IReadOnlyCollection<string> TrustedPublisherThumbprints { get; init; } = [];

    /// <summary>
    /// Configuration gate only. A true result permits wiring real identity and
    /// signature validators; it never substitutes for their runtime validation.
    /// </summary>
    public bool IsReady() =>
        EnableElevatedExecution &&
        !string.IsNullOrWhiteSpace(RequiredOperatorRole) &&
        MinimumApprovals >= 1 &&
        TrustedPublisherThumbprints.Count > 0 &&
        TrustedPublisherThumbprints.All(IsCertificateThumbprint);

    private static bool IsCertificateThumbprint(string? thumbprint)
    {
        if (string.IsNullOrWhiteSpace(thumbprint))
        {
            return false;
        }

        var normalized = thumbprint.Replace(" ", string.Empty, StringComparison.Ordinal);
        return (normalized.Length == 40 || normalized.Length == 64) && normalized.All(Uri.IsHexDigit);
    }
}
