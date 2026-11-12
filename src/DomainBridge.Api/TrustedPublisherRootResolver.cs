using System.Security.Cryptography.X509Certificates;

public sealed class TrustedPublisherPkiOptions
{
    public const string SectionName = "TrustedPublisherPki";

    public string[] TrustedRootThumbprints { get; init; } = [];

    public bool IsReady() => TrustedRootThumbprints
        .Select(TrustedPublisherRootResolver.Normalize)
        .Any(value => value.Length is 40 or 64 && value.All(Uri.IsHexDigit));
}

public interface IPublisherRootCertificateSource
{
    X509Certificate2Collection ReadRoots();
}

public sealed class LocalMachinePublisherRootCertificateSource : IPublisherRootCertificateSource
{
    public X509Certificate2Collection ReadRoots()
    {
        using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

        return new X509Certificate2Collection(
            store.Certificates.Select(certificate =>
                new X509Certificate2(certificate.Export(X509ContentType.Cert))).ToArray());
    }
}

public static class TrustedPublisherRootResolver
{
    public static TrustedPublisherRootResolutionResult Resolve(
        TrustedPublisherPkiOptions options,
        IPublisherRootCertificateSource source)
    {
        var configuredThumbprints = options.TrustedRootThumbprints
            .Select(Normalize)
            .Where(value => value.Length is 40 or 64 && value.All(Uri.IsHexDigit))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (configuredThumbprints.Count == 0)
        {
            return TrustedPublisherRootResolutionResult.Failure(TrustedPublisherRootError.TrustRootRequired);
        }

        var roots = source.ReadRoots()
            .Where(certificate => configuredThumbprints.Contains(Normalize(certificate.Thumbprint)))
            .ToArray();

        if (roots.Length != configuredThumbprints.Count)
        {
            return TrustedPublisherRootResolutionResult.Failure(TrustedPublisherRootError.ConfiguredRootNotFound);
        }

        return TrustedPublisherRootResolutionResult.Success(new X509Certificate2Collection(roots));
    }

    public static string Normalize(string? value) =>
        value?.Replace(" ", string.Empty, StringComparison.Ordinal).Trim() ?? string.Empty;
}

public enum TrustedPublisherRootError
{
    None = 0,
    TrustRootRequired,
    ConfiguredRootNotFound
}

public sealed record TrustedPublisherRootResolutionResult(
    TrustedPublisherRootError Error,
    X509Certificate2Collection Roots)
{
    public bool IsSuccess => Error == TrustedPublisherRootError.None;

    public static TrustedPublisherRootResolutionResult Success(X509Certificate2Collection roots) =>
        new(TrustedPublisherRootError.None, roots);

    public static TrustedPublisherRootResolutionResult Failure(TrustedPublisherRootError error) =>
        new(error, new X509Certificate2Collection());
}
