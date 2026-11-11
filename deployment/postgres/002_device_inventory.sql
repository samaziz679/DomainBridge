CREATE TABLE IF NOT EXISTS device_inventory (
    device_id TEXT PRIMARY KEY CHECK (length(device_id) BETWEEN 1 AND 256),
    operating_system TEXT NOT NULL CHECK (length(operating_system) BETWEEN 1 AND 256),
    architecture TEXT NOT NULL CHECK (length(architecture) BETWEEN 1 AND 64),
    agent_version TEXT NOT NULL CHECK (length(agent_version) BETWEEN 1 AND 128),
    captured_at_utc TIMESTAMPTZ NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_device_inventory_captured_at_utc
    ON device_inventory (captured_at_utc DESC);

-- The runtime account needs only the minimum data-plane permissions.
-- GRANT SELECT, INSERT, UPDATE ON device_inventory TO domainbridge_runtime;
-- REVOKE DELETE, TRUNCATE ON device_inventory FROM domainbridge_runtime;
