# TDD evidence — mTLS transport enforcement

| Stage | Result |
|---|---|
| RED | The API test failed because AgentTransportSecurity did not exist. |
| GREEN | Twenty-four API tests pass. Transport security allows the test/development mode, rejects a missing production client certificate, and accepts a current client-authentication certificate. |

Production Kestrel configuration requires a client certificate and rejects TLS chains with policy errors before agent endpoint processing.
