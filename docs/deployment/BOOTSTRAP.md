# Bootstrap and connector readiness

## Local prerequisites

The bootstrap script detects the .NET SDK, Node.js and the local WiX tool. It performs no installation unless the operator passes InstallMissing. WiX is restored from the repository tool manifest.

Run:

    powershell.exe -ExecutionPolicy Bypass -File deployment\bootstrap\Bootstrap-DomainBridge.ps1

To allow installation of a missing local prerequisite:

    powershell.exe -ExecutionPolicy Bypass -File deployment\bootstrap\Bootstrap-DomainBridge.ps1 -InstallMissing

## External systems

PKI, PostgreSQL, Intune and Active Directory are never auto-installed or auto-configured by DomainBridge. They represent organisation-owned security boundaries and require explicit infrastructure configuration.

Validate a completed non-secret pilot configuration:

    powershell.exe -ExecutionPolicy Bypass -File deployment\bootstrap\Test-ExternalConnectors.ps1 -PilotConfigurationPath C:\Secure\pilot.env

This validates configuration structure only. It does not contact the listed systems or make a change.
