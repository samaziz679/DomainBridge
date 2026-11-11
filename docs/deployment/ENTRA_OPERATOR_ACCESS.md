# Microsoft Entra ID operator access

DomainBridge treats on-premises Active Directory as the source of domain and device data. Microsoft Entra ID authenticates console operators.

Before enabling `Console:EnableReadApi` in production, register the DomainBridge API in Entra ID and configure its HTTPS authority, application client ID and API audience. Assign the `DomainBridge.Console.Reader` app role only to approved operators or a dedicated Entra group.

The production API remains fail-closed: read endpoints are not exposed unless all Entra settings are present and use an HTTPS authority. Do not place client secrets in `appsettings`; supply them through the approved secret manager when the OIDC gateway is configured.
