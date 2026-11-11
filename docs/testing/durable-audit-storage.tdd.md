# TDD evidence — durable audit storage

| Stage | Result |
|---|---|
| RED | The API test failed because the audit storage selector did not exist. |
| GREEN | Twenty-one API tests pass, including PostgreSQL selection when configured, memory restriction to Development or Testing, and production startup refusal without durable audit configuration. |

The PostgreSQL write store only inserts event ID, UTC timestamp, action and subject ID. Database provisioning and runtime-role grants are documented separately.
