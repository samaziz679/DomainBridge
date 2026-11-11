using System.Security.Cryptography.X509Certificates;

public static class AgentTransportSecurity
{
    public static AgentCertificateValidationResult Validate(
        X509Certificate2? certificate,
        bool requireClientCertificate)
    {
        if (!requireClientCertificate)
        {
            return AgentCertificateValidationResult.Success();
        }

        return AgentCertificatePolicy.Validate(
            certificate,
            certificate?.Thumbprint);
    }
}
