# mTLS agent-to-API contract

## Purpose

DomainBridge agents initiate outbound HTTPS connections to the central API. A production endpoint must require a client certificate at the TLS layer before enrollment is accepted.

## Certificate requirements

- Client certificate authentication EKU: 1.3.6.1.5.5.7.3.2.
- A current validity window at the time of use.
- The private key stays in the Windows certificate store and is never exported by the agent.
- The claimed enrollment thumbprint must match the TLS client certificate.
- The API validates the chain against the configured enterprise issuing CA, revocation policy and permitted issuer set.

## Current implementation boundary

AgentCertificatePolicy validates presence, validity period, EKU and thumbprint binding. Chain trust and revocation require the enterprise PKI configuration; they are not bypassed or marked trusted by this MVP.

## Deployment requirement

Kestrel or the reverse proxy must require client certificates on agent endpoints. Do not terminate TLS upstream and forward an unverified thumbprint header. Certificate enrollment and renewal should use Intune SCEP or PKCS, or an equivalent enterprise PKI workflow.
