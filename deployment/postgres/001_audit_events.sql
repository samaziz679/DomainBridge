CREATE TABLE IF NOT EXISTS audit_events (
    event_id UUID PRIMARY KEY,
    occurred_at_utc TIMESTAMPTZ NOT NULL,
    action TEXT NOT NULL CHECK (length(action) BETWEEN 1 AND 128),
    subject_id TEXT NOT NULL CHECK (length(subject_id) BETWEEN 1 AND 256)
);

CREATE INDEX IF NOT EXISTS ix_audit_events_occurred_at_utc
    ON audit_events (occurred_at_utc DESC);

-- Apply the following grants to a dedicated runtime role after replacing the placeholder:
-- GRANT INSERT ON audit_events TO domainbridge_runtime;
-- REVOKE UPDATE, DELETE, TRUNCATE ON audit_events FROM domainbridge_runtime;
