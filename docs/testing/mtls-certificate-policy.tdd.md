# TDD evidence — mTLS certificate policy

| Stage | Command | Result |
|---|---|---|
| RED | dotnet test tests\\DomainBridge.Api.Tests\\DomainBridge.Api.Tests.csproj --no-restore | Expected compile failure: AgentCertificatePolicy did not exist. |
| GREEN | dotnet test tests\\DomainBridge.Api.Tests\\DomainBridge.Api.Tests.csproj --no-restore --collect XPlat Code Coverage | 14 of 14 API tests passing; 100 percent line and 81.25 percent branch coverage. |

The suite covers accepted current client-auth certificates and rejections for missing certificate, expiry, missing client-auth EKU and mismatched thumbprint.
