# Test Lab MSI

Run the Build-TestLabMsi PowerShell script from the installer TestLab folder.

The output is artifacts/test-lab-installer/DomainBridge-TestLab-0.3.0.msi.

Install the MSI only in an isolated laboratory. After installation, open
Run-DomainBridgeTestLab.cmd from the install folder to print the read-only
inventory JSON, or Run-DomainBridgeConsole.cmd to open the local web console.

The MSI is deliberately unsigned and must not be used for production. It is
not an Intune production package. Use the signed-agent Intune/GPO flow
documented separately for production.
