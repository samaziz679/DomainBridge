namespace DomainBridge.Contracts;

/// <summary>
/// Non-secret device metadata used to begin mutual-certificate enrollment.
/// </summary>
public sealed record EnrollmentRequest(
    string AgentId,
    string CertificateThumbprint,
    string AgentVersion)
{
    public static EnrollmentResult Create(
        string? agentId,
        string? certificateThumbprint,
        string? agentVersion)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            return EnrollmentResult.Failure(EnrollmentError.MissingAgentId);
        }

        if (string.IsNullOrWhiteSpace(certificateThumbprint))
        {
            return EnrollmentResult.Failure(EnrollmentError.MissingCertificateThumbprint);
        }

        if (string.IsNullOrWhiteSpace(agentVersion))
        {
            return EnrollmentResult.Failure(EnrollmentError.MissingAgentVersion);
        }

        return EnrollmentResult.Success(new EnrollmentRequest(
            agentId.Trim(),
            certificateThumbprint.Trim(),
            agentVersion.Trim()));
    }
}

public enum EnrollmentError
{
    None = 0,
    MissingAgentId,
    MissingCertificateThumbprint,
    MissingAgentVersion
}

public sealed record EnrollmentResult(
    EnrollmentRequest? Value,
    EnrollmentError Error)
{
    public bool IsSuccess => Error == EnrollmentError.None;

    public bool IsFailure => !IsSuccess;

    public static EnrollmentResult Success(EnrollmentRequest value) =>
        new(value, EnrollmentError.None);

    public static EnrollmentResult Failure(EnrollmentError error) =>
        new(null, error);
}
