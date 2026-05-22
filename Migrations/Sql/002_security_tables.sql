CREATE TABLE IF NOT EXISTS login_attempts (
    username TEXT PRIMARY KEY,
    failed_count INT NOT NULL DEFAULT 0,
    lockout_until TIMESTAMPTZ,
    last_failed_at TIMESTAMPTZ
);

CREATE TABLE IF NOT EXISTS audit_events (
    id BIGSERIAL PRIMARY KEY,
    event_type TEXT NOT NULL,
    actor_subject TEXT,
    target TEXT,
    details JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_audit_events_created_at ON audit_events (created_at DESC);
CREATE INDEX IF NOT EXISTS ix_audit_events_event_type ON audit_events (event_type);
