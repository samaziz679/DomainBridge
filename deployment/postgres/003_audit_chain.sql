-- Tranche 2.3: complete, chained, append-only audit events.
-- NOT YET EXECUTED against a database: to be validated by the integration tests of tranche 2.4.
-- Existing rows keep NULL hashes; chain verification starts at the first sealed event.

ALTER TABLE audit_events
    ADD COLUMN IF NOT EXISTS sequence BIGINT GENERATED ALWAYS AS IDENTITY,
    ADD COLUMN IF NOT EXISTS actor TEXT NOT NULL DEFAULT 'system' CHECK (length(actor) BETWEEN 1 AND 256),
    ADD COLUMN IF NOT EXISTS outcome TEXT NOT NULL DEFAULT 'success' CHECK (length(outcome) BETWEEN 1 AND 32),
    ADD COLUMN IF NOT EXISTS task_id TEXT NULL CHECK (task_id IS NULL OR length(task_id) BETWEEN 1 AND 128),
    ADD COLUMN IF NOT EXISTS previous_hash TEXT NULL CHECK (previous_hash IS NULL OR length(previous_hash) = 64),
    ADD COLUMN IF NOT EXISTS hash TEXT NULL CHECK (hash IS NULL OR length(hash) = 64);

CREATE UNIQUE INDEX IF NOT EXISTS ux_audit_events_sequence ON audit_events (sequence);

-- Append-only is enforced by the database itself, not by a comment.
CREATE OR REPLACE FUNCTION audit_events_reject_change() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'audit_events is append-only';
END;
$$;

DROP TRIGGER IF EXISTS audit_events_no_update_delete ON audit_events;
CREATE TRIGGER audit_events_no_update_delete
    BEFORE UPDATE OR DELETE ON audit_events
    FOR EACH ROW EXECUTE FUNCTION audit_events_reject_change();

DROP TRIGGER IF EXISTS audit_events_no_truncate ON audit_events;
CREATE TRIGGER audit_events_no_truncate
    BEFORE TRUNCATE ON audit_events
    FOR EACH STATEMENT EXECUTE FUNCTION audit_events_reject_change();

-- The runtime role needs SELECT and INSERT only (the chain reads the last hash):
-- GRANT SELECT, INSERT ON audit_events TO domainbridge_runtime;
