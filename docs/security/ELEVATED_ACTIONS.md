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
