DomainBridge Test Lab

This MSI is for an isolated lab or controlled pilot only.

It installs the DomainBridge inventory agent in:
  %ProgramFiles%DomainBridge Test Lab

To test it, run:
  Run-DomainBridgeTestLab.cmd

The agent reports read-only technical inventory. It does not join a
domain, change a user profile, store credentials, or migrate secrets.

This package is intentionally unsigned for laboratory validation.
Do not deploy it to production. Production deployment requires code signing,
PKI mTLS configuration, operator groups (Windows session and AD groups),
and the documented Intune or GPO deployment process.
