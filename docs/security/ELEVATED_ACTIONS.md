# Elevated administrator actions

## Default posture

Elevated execution is disabled by default. Setting `EnableElevatedExecution` to `true` is insufficient on its own: DomainBridge requires every field below before the configuration gate is ready.

```json
"AdminActions": {
  "EnableElevatedExecution": true,
  "RequiredOperatorRole": "DomainBridge.Admin",
  "MinimumApprovals": 2,
  "TrustedPublisherThumbprints": ["<trusted-publisher-certificate-thumbprint>"]
}
```

## Required runtime controls

The configuration gate does not itself authenticate an operator or validate a signature. A production implementation must additionally:

1. Validate the authenticated Entra ID/AD operator role against `RequiredOperatorRole`.
2. Validate the approval record, including distinct approvers when required by policy.
3. Validate the action package/manifest signature and certificate chain against the configured publisher thumbprints.
4. Bind the approved manifest version, target set and idempotency key into the agent job.
5. Write durable audit evidence before and after execution.

Do not enable elevated execution until all runtime controls are implemented and tested with a laboratory PKI and synthetic devices.

## Current certificate control

`PublisherCertificatePolicy` now performs a local, deterministic check of the configured publisher allow-list: the presented certificate must be current, explicitly trusted by thumbprint and carry the `Code Signing` EKU (`1.3.6.1.5.5.7.3.3`).

`SignedManifestPolicy` now verifies a detached CMS signature against the exact manifest bytes and then applies `PublisherCertificatePolicy` to the embedded signer certificate. It rejects altered content, attached CMS packages and unknown publishers.

`PublisherCertificateChainPolicy` now builds the signer chain only against the explicit custom root set supplied by the host. It disables certificate downloads and does not silently use the local Windows trust store.

This chain policy is not yet connected to the signed-manifest validator or an execution endpoint. Its laboratory mode does not perform revocation or timestamp checks. A production integration must supply approved roots, define the CRL/OCSP and timestamp policy, and bind the successful chain result to the accepted manifest before an elevated action can become executable.
