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

Outside Development and Testing, Kestrel requires a client certificate and accepts it only when the operating-system chain validation has no policy error. AgentCertificatePolicy additionally validates presence, validity period, EKU and thumbprint binding. Chain trust and revocation are supplied by the enterprise PKI configuration; they are not bypassed or marked trusted by this MVP.

## Agent identity (tranche 2.2)

- The identity of an agent is the common name of its client certificate. Nothing in the URL or the body can change it: a request naming another device is refused with 403 and recorded as `agent.identity.rejected`.
- The certificate must be issued by an authority whose thumbprint is listed in `AgentIdentity:TrustedIssuerThumbprints`. A self-signed certificate is never accepted, even if its own thumbprint is listed.
- Outside development and test, the API refuses to start when no authority is pinned.
- Enrollment must name the certificate actually presented; a different thumbprint is refused.
- `AgentIdentity:Enforce` can only tighten the default: it turns certificate identity on in a development host and cannot turn it off in production.

Still open: how a device obtains its certificate in the first place (threat T2), and revocation checking.

## Deployment requirement

Kestrel or the reverse proxy must require client certificates on agent endpoints. Do not terminate TLS upstream and forward an unverified thumbprint header. Certificate enrollment and renewal should use Intune SCEP or PKCS, or an equivalent enterprise PKI workflow.
