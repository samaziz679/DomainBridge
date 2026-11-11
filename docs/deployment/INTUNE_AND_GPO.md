# Intune and GPO deployment

## Shared principle

Both channels deploy the same signed agent payload. No account password, domain join credential, private key or tenant secret is embedded in the package.

## Intune Win32

1. Sign the published DomainBridge.Agent.exe with the organisation code-signing certificate.
2. Run deployment/intune/Build-IntunePackage.ps1 with the Microsoft Win32 Content Prep Tool.
3. Install command: powershell.exe -ExecutionPolicy Bypass -File Install-DomainBridgeAgent.ps1 -PackageRoot .
4. Uninstall command: powershell.exe -ExecutionPolicy Bypass -File Uninstall-DomainBridgeAgent.ps1.
5. Detection command: powershell.exe -ExecutionPolicy Bypass -File Test-DomainBridgeAgent.ps1.

The install script rejects an unsigned payload unless LabMode is passed. Do not use LabMode in production assignments.

## GPO startup deployment

1. Place the signed payload and deployment scripts in protected SYSVOL or DFS storage accessible to computer accounts.
2. Assign Deploy-AgentStartup.ps1 as a computer startup script.
3. Pass only the protected package path and omit LabMode.
4. Pilot on a synthetic lab OU before broader assignment.

GPO deployment must not include interactive administrator passwords. The script copies the signed payload and records its local installation state.

## Validation and rollback

- Intune detection is Test-DomainBridgeAgent.ps1.
- GPO validation checks the same executable and installation state file.
- Rollback uses Uninstall-DomainBridgeAgent.ps1.
- Verify Authenticode signing and the publishing certificate chain before production deployment.
