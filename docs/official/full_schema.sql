-- ==============================================================================
-- THE LAST SIGNAL (FA26SE / GFA26SE54)
-- Authoritative Complete Relational Database Schema (PostgreSQL 16)
-- Conforms 100% to Software Design Document (SDD) Section IV.2 Table Descriptions
-- ==============================================================================

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ------------------------------------------------------------------------------
-- 1. USERS
-- Stores player credentials, account role, activation state (enabled: true = ACTIVE,
-- false = DEACTIVE), and registration metadata.
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    username VARCHAR(64) NOT NULL UNIQUE,
    email VARCHAR(255) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    role VARCHAR(32) NOT NULL,
    enabled BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS ix_users_username ON users (username);
CREATE INDEX IF NOT EXISTS ix_users_email ON users (email);

-- ------------------------------------------------------------------------------
-- 2. PLAYER_STATS
-- Tracks player career progression, victory metrics, escapes, repairs, revives,
-- and incapacitations (1:1 with users).
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS player_stats (
    user_id UUID PRIMARY KEY REFERENCES users (id) ON DELETE CASCADE,
    matches_played INTEGER NOT NULL DEFAULT 0,
    wins INTEGER NOT NULL DEFAULT 0,
    losses INTEGER NOT NULL DEFAULT 0,
    aborted_matches INTEGER NOT NULL DEFAULT 0,
    escapes INTEGER NOT NULL DEFAULT 0,
    machines_repaired INTEGER NOT NULL DEFAULT 0,
    teammates_revived INTEGER NOT NULL DEFAULT 0,
    times_incapacitated INTEGER NOT NULL DEFAULT 0,
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- ------------------------------------------------------------------------------
-- 3. GAME_SESSIONS
-- Records dedicated game server match sessions, lifecycle states, container ID,
-- duration, idempotency key, server secret hash, and authoritative match outcomes.
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS game_sessions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    room_code VARCHAR(16) NOT NULL,
    status VARCHAR(32) NOT NULL,
    server_host VARCHAR(64),
    server_port INTEGER,
    container_id VARCHAR(128),
    last_heartbeat_at TIMESTAMP WITH TIME ZONE,
    started_at TIMESTAMP WITH TIME ZONE,
    ended_at TIMESTAMP WITH TIME ZONE,
    outcome VARCHAR(32),
    ended_reason VARCHAR(255),
    server_secret_hash VARCHAR(255),
    result_idempotency_key VARCHAR(128),
    result_reported_at TIMESTAMP WITH TIME ZONE,
    duration_seconds INTEGER,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_game_sessions_result_idempotency_key
    ON game_sessions (result_idempotency_key)
    WHERE result_idempotency_key IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_game_sessions_room_code ON game_sessions (room_code);
CREATE INDEX IF NOT EXISTS ix_game_sessions_status ON game_sessions (status);

-- ------------------------------------------------------------------------------
-- 4. MATCH_PLAYERS
-- Junction entity storing per-player participation details, slot index, character
-- role, join tokens, per-match contributions, and final outcome status.
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS match_players (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    match_id UUID NOT NULL REFERENCES game_sessions (id) ON DELETE CASCADE,
    user_id UUID REFERENCES users (id) ON DELETE SET NULL,
    display_name VARCHAR(32),
    role VARCHAR(32) NOT NULL,
    slot_index INTEGER NOT NULL,
    character_role VARCHAR(30),
    join_token_hash VARCHAR(255) NOT NULL,
    token_expires_at TIMESTAMP WITH TIME ZONE NOT NULL,
    joined_at TIMESTAMP WITH TIME ZONE,
    machines_repaired INTEGER NOT NULL DEFAULT 0,
    teammates_revived INTEGER NOT NULL DEFAULT 0,
    incapacitated BOOLEAN,
    final_status VARCHAR(32),
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT ux_match_players_match_user UNIQUE (match_id, user_id)
);

CREATE INDEX IF NOT EXISTS ix_match_players_match_id ON match_players (match_id);
CREATE INDEX IF NOT EXISTS ix_match_players_user_id ON match_players (user_id);

-- ------------------------------------------------------------------------------
-- 5. SKINS
-- Cosmetic skin catalog defining available character skins, pricing, and visual assets.
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS skins (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(50) NOT NULL UNIQUE,
    name VARCHAR(100) NOT NULL,
    character_role VARCHAR(30) NOT NULL,
    price NUMERIC(12, 2) NOT NULL DEFAULT 0.00,
    image_url VARCHAR(255),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS ix_skins_character_role ON skins (character_role);
CREATE INDEX IF NOT EXISTS ix_skins_is_active ON skins (is_active);

-- ------------------------------------------------------------------------------
-- 6. USER_SKINS
-- Player inventory maintaining owned cosmetic skins and active equipped status.
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS user_skins (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    skin_id UUID NOT NULL REFERENCES skins (id) ON DELETE CASCADE,
    is_equipped BOOLEAN NOT NULL DEFAULT FALSE,
    acquired_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT ux_user_skins_user_skin UNIQUE (user_id, skin_id)
);

CREATE INDEX IF NOT EXISTS ix_user_skins_user_id ON user_skins (user_id);
CREATE INDEX IF NOT EXISTS ix_user_skins_skin_id ON user_skins (skin_id);

-- ------------------------------------------------------------------------------
-- 7. PAYMENT_TRANSACTIONS
-- Financial transaction ledger for cosmetic purchases and payment gateway webhooks.
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS payment_transactions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    transaction_code VARCHAR(64) NOT NULL UNIQUE,
    user_id UUID NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    skin_id UUID NOT NULL REFERENCES skins (id) ON DELETE RESTRICT,
    amount NUMERIC(12, 2) NOT NULL,
    currency VARCHAR(10) NOT NULL DEFAULT 'VND',
    payment_provider VARCHAR(50) NOT NULL DEFAULT 'VNPAY',
    status VARCHAR(20) NOT NULL DEFAULT 'PENDING',
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    paid_at TIMESTAMP WITH TIME ZONE
);

CREATE INDEX IF NOT EXISTS ix_payment_transactions_user_id ON payment_transactions (user_id);
CREATE INDEX IF NOT EXISTS ix_payment_transactions_status ON payment_transactions (status);

-- ------------------------------------------------------------------------------
-- 8. BUG_REPORTS
-- Player-submitted bug and gameplay issue reports triaged via the Web Admin Portal.
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS bug_reports (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    reporter_id UUID NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    category VARCHAR(50) NOT NULL,
    title VARCHAR(200) NOT NULL,
    description TEXT NOT NULL,
    client_log TEXT,
    status VARCHAR(20) NOT NULL DEFAULT 'OPEN',
    admin_note TEXT,
    resolved_by UUID REFERENCES users (id) ON DELETE SET NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    resolved_at TIMESTAMP WITH TIME ZONE
);

CREATE INDEX IF NOT EXISTS ix_bug_reports_reporter_id ON bug_reports (reporter_id);
CREATE INDEX IF NOT EXISTS ix_bug_reports_status ON bug_reports (status);

-- ------------------------------------------------------------------------------
-- 9. BALANCE_CONFIGS
-- Versioned gameplay balance parameters managed dynamically by administrators.
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS balance_configs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    version_code VARCHAR(50) NOT NULL UNIQUE,
    required_repairs INTEGER NOT NULL DEFAULT 3,
    sanity_drain_rate NUMERIC(5, 2) NOT NULL DEFAULT 1.00,
    sanity_low_threshold NUMERIC(5, 2) NOT NULL DEFAULT 30.00,
    survivor_base_stamina NUMERIC(5, 2) NOT NULL DEFAULT 100.00,
    shade_light_slow_multiplier NUMERIC(5, 2) NOT NULL DEFAULT 0.50,
    downed_crawl_speed_ratio NUMERIC(5, 2) NOT NULL DEFAULT 0.20,
    revive_health_ratio NUMERIC(5, 2) NOT NULL DEFAULT 0.50,
    is_active BOOLEAN NOT NULL DEFAULT FALSE,
    updated_by UUID REFERENCES users (id) ON DELETE SET NULL,
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS ix_balance_configs_is_active ON balance_configs (is_active);
