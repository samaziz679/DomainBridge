# DomainBridge pilot runbook

## Scope

This runbook enables a controlled test of agent inventory to API to PostgreSQL audit. It does not authorize domain migration, domain join, profile remapping, credential handling or arbitrary remote execution.

## Prepare configuration

Copy deployment/pilot/pilot.env.example to a protected location outside the repository. Fill only non-secret values. Store database credentials or managed-identity material in the approved secret manager.

Validate the file:

    powershell.exe -ExecutionPolicy Bypass -File deployment\pilot\Validate-PilotConfiguration.ps1 -ConfigurationPath C:\Secure\pilot.env

## Provision infrastructure

1. Deploy the API with a trusted HTTPS certificate for the configured API FQDN.
2. Apply deployment/postgres/001_audit_events.sql to the pilot PostgreSQL database.
3. Configure the database connection outside source control.
4. Issue device client certificates from the approved mTLS template and deploy the issuing chain to API and pilot devices.
5. Assign the signed production agent only to the approved pilot OU or Intune device group.

## Test sequence

1. Confirm API health over HTTPS.
2. Confirm mTLS rejects a request with no client certificate.
3. Deploy one pilot agent.
4. Run its read-only inventory submission.
5. Confirm the agent inventory audit event exists in PostgreSQL.
6. Validate the console read model.
7. Remove the agent with the documented uninstall script.

## Stop conditions

Stop immediately if the wrong device scope is targeted, certificate validation fails, audit records are absent, an unapproved credential is requested, or any command proposes a domain/profile change.
