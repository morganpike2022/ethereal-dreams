-- =============================================================================
-- Ethereal Dreams MMORPG — PostgreSQL Database Schema
-- =============================================================================

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- =============================================================================
-- ENUMS
-- =============================================================================

CREATE TYPE character_class AS ENUM (
    'warrior', 'mage', 'rogue', 'paladin', 'ranger', 'necromancer'
);

CREATE TYPE item_rarity AS ENUM (
    'common', 'uncommon', 'rare', 'epic', 'legendary', 'mythic'
);

CREATE TYPE item_type AS ENUM (
    'weapon', 'armor', 'accessory', 'consumable', 'material', 'quest', 'mount', 'cosmetic'
);

CREATE TYPE equipment_slot AS ENUM (
    'head', 'neck', 'shoulders', 'chest', 'back', 'wrist',
    'hands', 'waist', 'legs', 'feet',
    'ring_1', 'ring_2', 'trinket_1', 'trinket_2',
    'main_hand', 'off_hand', 'ranged'
);

CREATE TYPE quest_type AS ENUM (
    'kill', 'collect', 'explore', 'escort', 'delivery', 'crafting', 'daily', 'weekly'
);

CREATE TYPE quest_status AS ENUM (
    'available', 'in_progress', 'completed', 'failed', 'abandoned'
);

CREATE TYPE guild_rank AS ENUM (
    'recruit', 'member', 'officer', 'guild_master'
);

CREATE TYPE combat_action AS ENUM (
    'attack', 'skill', 'defend', 'flee', 'item'
);

CREATE TYPE zone_type AS ENUM (
    'open_world', 'dungeon', 'raid', 'pvp_arena', 'city', 'instanced'
);

CREATE TYPE auction_status AS ENUM (
    'active', 'sold', 'expired', 'cancelled'
);

CREATE TYPE chat_channel AS ENUM (
    'global', 'zone', 'party', 'guild', 'whisper', 'trade', 'lfg', 'system'
);

CREATE TYPE ban_type AS ENUM (
    'temporary', 'permanent'
);

-- =============================================================================
-- PLAYERS & ACCOUNTS
-- =============================================================================

CREATE TABLE players (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    username        VARCHAR(32) NOT NULL,
    email           VARCHAR(255) NOT NULL,
    password_hash   TEXT NOT NULL,
    email_verified  BOOLEAN NOT NULL DEFAULT FALSE,
    is_admin        BOOLEAN NOT NULL DEFAULT FALSE,
    last_login_at   TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at      TIMESTAMPTZ,

    CONSTRAINT uq_players_username UNIQUE (username),
    CONSTRAINT uq_players_email UNIQUE (email),
    CONSTRAINT ck_players_username_len CHECK (char_length(username) >= 3)
);

CREATE INDEX idx_players_email ON players (email);
CREATE INDEX idx_players_deleted_at ON players (deleted_at) WHERE deleted_at IS NULL;

-- =============================================================================

CREATE TABLE refresh_tokens (
    id          UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    player_id   UUID NOT NULL REFERENCES players (id) ON DELETE CASCADE,
    token_hash  TEXT NOT NULL,
    expires_at  TIMESTAMPTZ NOT NULL,
    revoked_at  TIMESTAMPTZ,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT uq_refresh_tokens_hash UNIQUE (token_hash)
);

CREATE INDEX idx_refresh_tokens_player_id ON refresh_tokens (player_id);
CREATE INDEX idx_refresh_tokens_expires_at ON refresh_tokens (expires_at);

-- =============================================================================

CREATE TABLE player_bans (
    id          UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    player_id   UUID NOT NULL REFERENCES players (id) ON DELETE CASCADE,
    banned_by   UUID NOT NULL REFERENCES players (id),
    reason      TEXT NOT NULL,
    ban_type    ban_type NOT NULL,
    expires_at  TIMESTAMPTZ,
    lifted_at   TIMESTAMPTZ,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_player_bans_player_id ON player_bans (player_id);

-- =============================================================================
-- WORLD & ZONES
-- =============================================================================

CREATE TABLE continents (
    id          SERIAL PRIMARY KEY,
    name        VARCHAR(100) NOT NULL,
    description TEXT,
    map_image   VARCHAR(500),

    CONSTRAINT uq_continents_name UNIQUE (name)
);

-- =============================================================================

CREATE TABLE zones (
    id                  SERIAL PRIMARY KEY,
    continent_id        INT NOT NULL REFERENCES continents (id),
    name                VARCHAR(100) NOT NULL,
    description         TEXT,
    zone_type           zone_type NOT NULL DEFAULT 'open_world',
    min_level           SMALLINT NOT NULL DEFAULT 1,
    max_level           SMALLINT NOT NULL DEFAULT 60,
    is_pvp              BOOLEAN NOT NULL DEFAULT FALSE,
    is_instanced        BOOLEAN NOT NULL DEFAULT FALSE,
    max_players         INT,
    ambient_music       VARCHAR(200),
    map_image           VARCHAR(500),
    respawn_zone_id     INT REFERENCES zones (id),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT uq_zones_name UNIQUE (name),
    CONSTRAINT ck_zones_levels CHECK (min_level <= max_level)
);

CREATE INDEX idx_zones_continent_id ON zones (continent_id);

-- =============================================================================

CREATE TABLE zone_portals (
    id              SERIAL PRIMARY KEY,
    from_zone_id    INT NOT NULL REFERENCES zones (id),
    to_zone_id      INT NOT NULL REFERENCES zones (id),
    min_level       SMALLINT NOT NULL DEFAULT 1,
    name            VARCHAR(100),

    CONSTRAINT uq_zone_portals UNIQUE (from_zone_id, to_zone_id)
);

-- =============================================================================
-- CHARACTER SYSTEM
-- =============================================================================

CREATE TABLE character_classes (
    id                  SERIAL PRIMARY KEY,
    name                character_class NOT NULL,
    display_name        VARCHAR(50) NOT NULL,
    description         TEXT,
    base_hp             INT NOT NULL DEFAULT 100,
    base_mana           INT NOT NULL DEFAULT 50,
    base_strength       SMALLINT NOT NULL DEFAULT 10,
    base_agility        SMALLINT NOT NULL DEFAULT 10,
    base_intelligence   SMALLINT NOT NULL DEFAULT 10,
    base_endurance      SMALLINT NOT NULL DEFAULT 10,
    base_spirit         SMALLINT NOT NULL DEFAULT 10,
    hp_per_level        INT NOT NULL DEFAULT 15,
    mana_per_level      INT NOT NULL DEFAULT 10,

    CONSTRAINT uq_character_classes_name UNIQUE (name)
);

-- =============================================================================

CREATE TABLE characters (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    player_id       UUID NOT NULL REFERENCES players (id) ON DELETE CASCADE,
    class_id        INT NOT NULL REFERENCES character_classes (id),
    name            VARCHAR(32) NOT NULL,
    level           SMALLINT NOT NULL DEFAULT 1,
    experience      BIGINT NOT NULL DEFAULT 0,
    gold            BIGINT NOT NULL DEFAULT 0,

    -- Current stats
    current_hp      INT NOT NULL,
    max_hp          INT NOT NULL,
    current_mana    INT NOT NULL,
    max_mana        INT NOT NULL,

    -- Base attributes
    strength        SMALLINT NOT NULL,
    agility         SMALLINT NOT NULL,
    intelligence    SMALLINT NOT NULL,
    endurance       SMALLINT NOT NULL,
    spirit          SMALLINT NOT NULL,

    -- Derived combat stats
    attack_power    INT NOT NULL DEFAULT 0,
    spell_power     INT NOT NULL DEFAULT 0,
    armor           INT NOT NULL DEFAULT 0,
    crit_chance     NUMERIC(5,2) NOT NULL DEFAULT 5.00,
    dodge_chance    NUMERIC(5,2) NOT NULL DEFAULT 5.00,
    haste           NUMERIC(5,2) NOT NULL DEFAULT 0.00,

    -- Location
    zone_id         INT REFERENCES zones (id),
    pos_x           NUMERIC(10,2) NOT NULL DEFAULT 0,
    pos_y           NUMERIC(10,2) NOT NULL DEFAULT 0,

    -- Appearance
    appearance_data JSONB NOT NULL DEFAULT '{}',

    -- Flags
    is_online       BOOLEAN NOT NULL DEFAULT FALSE,
    is_deleted      BOOLEAN NOT NULL DEFAULT FALSE,
    delete_at       TIMESTAMPTZ,

    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT uq_characters_name_player UNIQUE (player_id, name),
    CONSTRAINT ck_characters_level CHECK (level BETWEEN 1 AND 50),
    CONSTRAINT ck_characters_gold CHECK (gold >= 0)
);

CREATE INDEX idx_characters_player_id ON characters (player_id);
CREATE INDEX idx_characters_zone_id ON characters (zone_id);
CREATE INDEX idx_characters_is_online ON characters (is_online) WHERE is_online = TRUE;

-- =============================================================================
-- SKILLS & ABILITIES
-- =============================================================================

CREATE TABLE skills (
    id              SERIAL PRIMARY KEY,
    class_id        INT REFERENCES character_classes (id),
    name            VARCHAR(100) NOT NULL,
    description     TEXT,
    skill_type      VARCHAR(50) NOT NULL,
    mana_cost       INT NOT NULL DEFAULT 0,
    cooldown_secs   NUMERIC(6,2) NOT NULL DEFAULT 0,
    min_level       SMALLINT NOT NULL DEFAULT 1,
    max_rank        SMALLINT NOT NULL DEFAULT 1,
    parent_skill_id INT REFERENCES skills (id),
    damage_base     INT,
    damage_scaling  NUMERIC(5,2),
    heal_base       INT,
    effect_data     JSONB NOT NULL DEFAULT '{}'
);

CREATE INDEX idx_skills_class_id ON skills (class_id);

-- =============================================================================

CREATE TABLE character_skills (
    character_id    UUID NOT NULL REFERENCES characters (id) ON DELETE CASCADE,
    skill_id        INT NOT NULL REFERENCES skills (id),
    current_rank    SMALLINT NOT NULL DEFAULT 1,
    is_on_cooldown  BOOLEAN NOT NULL DEFAULT FALSE,
    cooldown_ends   TIMESTAMPTZ,

    PRIMARY KEY (character_id, skill_id)
);

-- =============================================================================
-- ITEMS & INVENTORY
-- =============================================================================

CREATE TABLE items (
    id              SERIAL PRIMARY KEY,
    name            VARCHAR(150) NOT NULL,
    description     TEXT,
    item_type       item_type NOT NULL,
    rarity          item_rarity NOT NULL DEFAULT 'common',
    required_level  SMALLINT NOT NULL DEFAULT 1,
    required_class  character_class,
    is_stackable    BOOLEAN NOT NULL DEFAULT FALSE,
    max_stack       INT NOT NULL DEFAULT 1,
    weight          NUMERIC(6,2) NOT NULL DEFAULT 0,
    sell_price      INT NOT NULL DEFAULT 0,
    buy_price       INT NOT NULL DEFAULT 0,
    icon_url        VARCHAR(500),

    -- Stat bonuses (for equipment)
    bonus_strength      SMALLINT NOT NULL DEFAULT 0,
    bonus_agility       SMALLINT NOT NULL DEFAULT 0,
    bonus_intelligence  SMALLINT NOT NULL DEFAULT 0,
    bonus_endurance     SMALLINT NOT NULL DEFAULT 0,
    bonus_spirit        SMALLINT NOT NULL DEFAULT 0,
    bonus_hp            INT NOT NULL DEFAULT 0,
    bonus_mana          INT NOT NULL DEFAULT 0,
    bonus_attack_power  INT NOT NULL DEFAULT 0,
    bonus_spell_power   INT NOT NULL DEFAULT 0,
    bonus_armor         INT NOT NULL DEFAULT 0,
    bonus_crit          NUMERIC(5,2) NOT NULL DEFAULT 0,

    equipment_slot  equipment_slot,
    extra_data      JSONB NOT NULL DEFAULT '{}'
);

CREATE INDEX idx_items_type ON items (item_type);
CREATE INDEX idx_items_rarity ON items (rarity);

-- =============================================================================

CREATE TABLE inventory (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    character_id    UUID NOT NULL REFERENCES characters (id) ON DELETE CASCADE,
    item_id         INT NOT NULL REFERENCES items (id),
    quantity        INT NOT NULL DEFAULT 1,
    slot_index      SMALLINT NOT NULL,
    acquired_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT uq_inventory_slot UNIQUE (character_id, slot_index),
    CONSTRAINT ck_inventory_quantity CHECK (quantity > 0),
    CONSTRAINT ck_inventory_slot CHECK (slot_index >= 0)
);

CREATE INDEX idx_inventory_character_id ON inventory (character_id);

-- =============================================================================

CREATE TABLE character_equipment (
    character_id    UUID NOT NULL REFERENCES characters (id) ON DELETE CASCADE,
    slot            equipment_slot NOT NULL,
    item_id         INT NOT NULL REFERENCES items (id),
    equipped_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    PRIMARY KEY (character_id, slot)
);

-- =============================================================================
-- QUESTS
-- =============================================================================

CREATE TABLE quests (
    id              SERIAL PRIMARY KEY,
    name            VARCHAR(200) NOT NULL,
    description     TEXT,
    quest_type      quest_type NOT NULL,
    min_level       SMALLINT NOT NULL DEFAULT 1,
    max_level       SMALLINT,
    required_class  character_class,
    prerequisite_quest_id INT REFERENCES quests (id),
    zone_id         INT REFERENCES zones (id),
    is_daily        BOOLEAN NOT NULL DEFAULT FALSE,
    is_weekly       BOOLEAN NOT NULL DEFAULT FALSE,
    is_repeatable   BOOLEAN NOT NULL DEFAULT FALSE,
    xp_reward       INT NOT NULL DEFAULT 0,
    gold_reward     INT NOT NULL DEFAULT 0,
    objectives      JSONB NOT NULL DEFAULT '[]',
    reward_items    JSONB NOT NULL DEFAULT '[]',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_quests_zone_id ON quests (zone_id);
CREATE INDEX idx_quests_min_level ON quests (min_level);

-- =============================================================================

CREATE TABLE character_quests (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    character_id    UUID NOT NULL REFERENCES characters (id) ON DELETE CASCADE,
    quest_id        INT NOT NULL REFERENCES quests (id),
    status          quest_status NOT NULL DEFAULT 'in_progress',
    progress        JSONB NOT NULL DEFAULT '{}',
    accepted_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at    TIMESTAMPTZ,

    CONSTRAINT uq_character_active_quest UNIQUE (character_id, quest_id)
);

CREATE INDEX idx_character_quests_character_id ON character_quests (character_id);
CREATE INDEX idx_character_quests_status ON character_quests (status);

-- =============================================================================
-- NPCS & ENEMIES
-- =============================================================================

CREATE TABLE npc_types (
    id              SERIAL PRIMARY KEY,
    name            VARCHAR(150) NOT NULL,
    description     TEXT,
    npc_role        VARCHAR(50) NOT NULL,
    is_hostile      BOOLEAN NOT NULL DEFAULT FALSE,
    level           SMALLINT NOT NULL DEFAULT 1,
    hp              INT NOT NULL DEFAULT 100,
    mana            INT NOT NULL DEFAULT 0,
    attack_power    INT NOT NULL DEFAULT 10,
    armor           INT NOT NULL DEFAULT 0,
    xp_reward       INT NOT NULL DEFAULT 0,
    gold_min        INT NOT NULL DEFAULT 0,
    gold_max        INT NOT NULL DEFAULT 0,
    aggro_radius    NUMERIC(6,2) NOT NULL DEFAULT 10,
    loot_table      JSONB NOT NULL DEFAULT '[]',
    ai_behavior     JSONB NOT NULL DEFAULT '{"type":"patrol"}'
);

-- =============================================================================

CREATE TABLE npc_spawns (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    npc_type_id     INT NOT NULL REFERENCES npc_types (id),
    zone_id         INT NOT NULL REFERENCES zones (id),
    pos_x           NUMERIC(10,2) NOT NULL,
    pos_y           NUMERIC(10,2) NOT NULL,
    patrol_path     JSONB,
    respawn_secs    INT NOT NULL DEFAULT 300,
    current_hp      INT,
    is_alive        BOOLEAN NOT NULL DEFAULT TRUE,
    died_at         TIMESTAMPTZ
);

CREATE INDEX idx_npc_spawns_zone_id ON npc_spawns (zone_id);
CREATE INDEX idx_npc_spawns_is_alive ON npc_spawns (is_alive);

-- =============================================================================
-- COMBAT
-- =============================================================================

CREATE TABLE combat_sessions (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    zone_id         INT NOT NULL REFERENCES zones (id),
    is_pvp          BOOLEAN NOT NULL DEFAULT FALSE,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    started_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    ended_at        TIMESTAMPTZ
);

CREATE INDEX idx_combat_sessions_active ON combat_sessions (is_active) WHERE is_active = TRUE;

-- =============================================================================

CREATE TABLE combat_participants (
    session_id      UUID NOT NULL REFERENCES combat_sessions (id) ON DELETE CASCADE,
    character_id    UUID REFERENCES characters (id) ON DELETE CASCADE,
    npc_spawn_id    UUID REFERENCES npc_spawns (id) ON DELETE CASCADE,
    team            SMALLINT NOT NULL DEFAULT 1,
    is_alive        BOOLEAN NOT NULL DEFAULT TRUE,
    joined_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT ck_combat_participant_type CHECK (
        (character_id IS NOT NULL AND npc_spawn_id IS NULL) OR
        (character_id IS NULL AND npc_spawn_id IS NOT NULL)
    )
);

CREATE INDEX idx_combat_participants_session ON combat_participants (session_id);

-- =============================================================================

CREATE TABLE combat_logs (
    id              BIGSERIAL PRIMARY KEY,
    session_id      UUID NOT NULL REFERENCES combat_sessions (id) ON DELETE CASCADE,
    round_number    INT NOT NULL,
    action          combat_action NOT NULL,
    actor_type      VARCHAR(20) NOT NULL,
    actor_id        UUID NOT NULL,
    target_type     VARCHAR(20),
    target_id       UUID,
    skill_id        INT REFERENCES skills (id),
    damage          INT,
    healing         INT,
    is_critical     BOOLEAN NOT NULL DEFAULT FALSE,
    effect_applied  VARCHAR(100),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_combat_logs_session_id ON combat_logs (session_id);

-- =============================================================================
-- GUILDS
-- =============================================================================

CREATE TABLE guilds (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name            VARCHAR(64) NOT NULL,
    tag             VARCHAR(6) NOT NULL,
    description     TEXT,
    level           SMALLINT NOT NULL DEFAULT 1,
    experience      BIGINT NOT NULL DEFAULT 0,
    gold            BIGINT NOT NULL DEFAULT 0,
    motd            TEXT,
    emblem_data     JSONB NOT NULL DEFAULT '{}',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT uq_guilds_name UNIQUE (name),
    CONSTRAINT uq_guilds_tag UNIQUE (tag),
    CONSTRAINT ck_guilds_level CHECK (level BETWEEN 1 AND 25)
);

-- =============================================================================

CREATE TABLE guild_members (
    guild_id        UUID NOT NULL REFERENCES guilds (id) ON DELETE CASCADE,
    character_id    UUID NOT NULL REFERENCES characters (id) ON DELETE CASCADE,
    rank            guild_rank NOT NULL DEFAULT 'recruit',
    contribution_xp BIGINT NOT NULL DEFAULT 0,
    joined_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_active_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    PRIMARY KEY (guild_id, character_id)
);

CREATE INDEX idx_guild_members_character_id ON guild_members (character_id);

-- =============================================================================

CREATE TABLE guild_bank_tabs (
    id          SERIAL PRIMARY KEY,
    guild_id    UUID NOT NULL REFERENCES guilds (id) ON DELETE CASCADE,
    tab_index   SMALLINT NOT NULL,
    name        VARCHAR(50) NOT NULL DEFAULT 'Tab',
    icon        VARCHAR(200),

    CONSTRAINT uq_guild_bank_tabs UNIQUE (guild_id, tab_index)
);

-- =============================================================================

CREATE TABLE guild_bank_items (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tab_id          INT NOT NULL REFERENCES guild_bank_tabs (id) ON DELETE CASCADE,
    item_id         INT NOT NULL REFERENCES items (id),
    quantity        INT NOT NULL DEFAULT 1,
    slot_index      SMALLINT NOT NULL,
    deposited_by    UUID REFERENCES characters (id),
    deposited_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT uq_guild_bank_slot UNIQUE (tab_id, slot_index)
);

-- =============================================================================
-- CRAFTING
-- =============================================================================

CREATE TABLE crafting_recipes (
    id              SERIAL PRIMARY KEY,
    name            VARCHAR(150) NOT NULL,
    description     TEXT,
    result_item_id  INT NOT NULL REFERENCES items (id),
    result_quantity INT NOT NULL DEFAULT 1,
    station_type    VARCHAR(50) NOT NULL,
    required_skill  VARCHAR(50),
    required_level  SMALLINT NOT NULL DEFAULT 1,
    ingredients     JSONB NOT NULL DEFAULT '[]',
    xp_reward       INT NOT NULL DEFAULT 0
);

-- =============================================================================
-- MARKETPLACE / AUCTION HOUSE
-- =============================================================================

CREATE TABLE auction_listings (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    seller_id       UUID NOT NULL REFERENCES characters (id),
    item_id         INT NOT NULL REFERENCES items (id),
    quantity        INT NOT NULL DEFAULT 1,
    starting_bid    BIGINT NOT NULL,
    buyout_price    BIGINT,
    current_bid     BIGINT,
    bidder_id       UUID REFERENCES characters (id),
    status          auction_status NOT NULL DEFAULT 'active',
    duration_hours  SMALLINT NOT NULL DEFAULT 48,
    listed_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at      TIMESTAMPTZ NOT NULL,
    sold_at         TIMESTAMPTZ,

    CONSTRAINT ck_auction_bid CHECK (current_bid IS NULL OR current_bid >= starting_bid),
    CONSTRAINT ck_auction_buyout CHECK (buyout_price IS NULL OR buyout_price >= starting_bid)
);

CREATE INDEX idx_auction_listings_status ON auction_listings (status);
CREATE INDEX idx_auction_listings_item_id ON auction_listings (item_id);
CREATE INDEX idx_auction_listings_expires_at ON auction_listings (expires_at);
CREATE INDEX idx_auction_listings_seller_id ON auction_listings (seller_id);

-- =============================================================================

CREATE TABLE gold_transactions (
    id              BIGSERIAL PRIMARY KEY,
    from_character  UUID REFERENCES characters (id),
    to_character    UUID REFERENCES characters (id),
    amount          BIGINT NOT NULL,
    fee             BIGINT NOT NULL DEFAULT 0,
    reason          VARCHAR(100) NOT NULL,
    reference_id    UUID,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT ck_gold_transaction_amount CHECK (amount > 0)
);

CREATE INDEX idx_gold_transactions_from ON gold_transactions (from_character);
CREATE INDEX idx_gold_transactions_to ON gold_transactions (to_character);

-- =============================================================================
-- ACHIEVEMENTS
-- =============================================================================

CREATE TABLE achievements (
    id              SERIAL PRIMARY KEY,
    name            VARCHAR(200) NOT NULL,
    description     TEXT,
    category        VARCHAR(50) NOT NULL,
    points          SMALLINT NOT NULL DEFAULT 10,
    is_account_wide BOOLEAN NOT NULL DEFAULT FALSE,
    icon_url        VARCHAR(500),
    criteria        JSONB NOT NULL DEFAULT '{}',
    reward_title    VARCHAR(100),
    reward_item_id  INT REFERENCES items (id)
);

CREATE INDEX idx_achievements_category ON achievements (category);

-- =============================================================================

CREATE TABLE character_achievements (
    character_id    UUID NOT NULL REFERENCES characters (id) ON DELETE CASCADE,
    achievement_id  INT NOT NULL REFERENCES achievements (id),
    progress        JSONB NOT NULL DEFAULT '{}',
    earned_at       TIMESTAMPTZ,

    PRIMARY KEY (character_id, achievement_id)
);

-- =============================================================================
-- SOCIAL
-- =============================================================================

CREATE TABLE friendships (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    player_id       UUID NOT NULL REFERENCES players (id) ON DELETE CASCADE,
    friend_id       UUID NOT NULL REFERENCES players (id) ON DELETE CASCADE,
    is_accepted     BOOLEAN NOT NULL DEFAULT FALSE,
    is_blocked      BOOLEAN NOT NULL DEFAULT FALSE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT uq_friendships UNIQUE (player_id, friend_id),
    CONSTRAINT ck_friendships_self CHECK (player_id <> friend_id)
);

CREATE INDEX idx_friendships_player_id ON friendships (player_id);
CREATE INDEX idx_friendships_friend_id ON friendships (friend_id);

-- =============================================================================

CREATE TABLE mail_messages (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    from_character  UUID REFERENCES characters (id),
    to_character    UUID NOT NULL REFERENCES characters (id),
    subject         VARCHAR(200) NOT NULL,
    body            TEXT,
    gold_attached   BIGINT NOT NULL DEFAULT 0,
    item_id         INT REFERENCES items (id),
    item_quantity   INT,
    is_read         BOOLEAN NOT NULL DEFAULT FALSE,
    is_system_mail  BOOLEAN NOT NULL DEFAULT FALSE,
    sent_at         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at      TIMESTAMPTZ NOT NULL DEFAULT NOW() + INTERVAL '30 days',
    claimed_at      TIMESTAMPTZ
);

CREATE INDEX idx_mail_messages_to_character ON mail_messages (to_character);
CREATE INDEX idx_mail_messages_expires_at ON mail_messages (expires_at);

-- =============================================================================

CREATE TABLE chat_messages (
    id              BIGSERIAL PRIMARY KEY,
    channel         chat_channel NOT NULL,
    sender_id       UUID NOT NULL REFERENCES characters (id),
    recipient_id    UUID REFERENCES characters (id),
    zone_id         INT REFERENCES zones (id),
    guild_id        UUID REFERENCES guilds (id),
    content         TEXT NOT NULL,
    sent_at         TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_chat_messages_channel ON chat_messages (channel);
CREATE INDEX idx_chat_messages_sender ON chat_messages (sender_id);
CREATE INDEX idx_chat_messages_sent_at ON chat_messages (sent_at);

-- Partition by month for large datasets
-- (Can be converted to range-partitioned table in production)

-- =============================================================================
-- PVP
-- =============================================================================

CREATE TABLE pvp_ratings (
    character_id    UUID PRIMARY KEY REFERENCES characters (id) ON DELETE CASCADE,
    rating          INT NOT NULL DEFAULT 1500,
    wins            INT NOT NULL DEFAULT 0,
    losses          INT NOT NULL DEFAULT 0,
    season          SMALLINT NOT NULL DEFAULT 1,
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- =============================================================================

CREATE TABLE pvp_matches (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    bracket         VARCHAR(10) NOT NULL,
    season          SMALLINT NOT NULL DEFAULT 1,
    team_a          JSONB NOT NULL DEFAULT '[]',
    team_b          JSONB NOT NULL DEFAULT '[]',
    winner_team     SMALLINT,
    duration_secs   INT,
    started_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    ended_at        TIMESTAMPTZ
);

CREATE INDEX idx_pvp_matches_season ON pvp_matches (season);

-- =============================================================================
-- ADMIN & AUDIT
-- =============================================================================

CREATE TABLE admin_audit_log (
    id              BIGSERIAL PRIMARY KEY,
    admin_id        UUID NOT NULL REFERENCES players (id),
    action          VARCHAR(100) NOT NULL,
    target_type     VARCHAR(50),
    target_id       UUID,
    details         JSONB NOT NULL DEFAULT '{}',
    ip_address      INET,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_admin_audit_log_admin_id ON admin_audit_log (admin_id);
CREATE INDEX idx_admin_audit_log_created_at ON admin_audit_log (created_at);

-- =============================================================================
-- UTILITY FUNCTIONS & TRIGGERS
-- =============================================================================

CREATE OR REPLACE FUNCTION update_updated_at()
RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_players_updated_at
    BEFORE UPDATE ON players
    FOR EACH ROW EXECUTE FUNCTION update_updated_at();

CREATE TRIGGER trg_characters_updated_at
    BEFORE UPDATE ON characters
    FOR EACH ROW EXECUTE FUNCTION update_updated_at();

CREATE TRIGGER trg_guilds_updated_at
    BEFORE UPDATE ON guilds
    FOR EACH ROW EXECUTE FUNCTION update_updated_at();

-- =============================================================================
-- REFERENCE DATA SEED
-- =============================================================================

INSERT INTO character_classes (name, display_name, description, base_hp, base_mana, base_strength, base_agility, base_intelligence, base_endurance, base_spirit, hp_per_level, mana_per_level)
VALUES
    ('warrior',     'Warrior',     'A mighty melee fighter clad in plate armor.',                200, 50,  18, 12, 8,  16, 8,  25, 5),
    ('mage',        'Mage',        'A master of arcane magic, fragile but devastating.',          80,  200, 6,  8,  20, 6,  14, 10, 20),
    ('rogue',       'Rogue',       'A nimble assassin who strikes from the shadows.',             120, 80,  12, 20, 10, 10, 10, 15, 8),
    ('paladin',     'Paladin',     'A holy warrior who combines combat skill with divine magic.', 180, 120, 15, 10, 12, 14, 12, 20, 12),
    ('ranger',      'Ranger',      'A skilled archer and tracker at home in the wilderness.',     130, 90,  11, 18, 11, 11, 11, 16, 9),
    ('necromancer', 'Necromancer', 'A dark mage who bends life and death to their will.',         90,  180, 7,  9,  18, 8,  16, 12, 18);

INSERT INTO continents (name, description)
VALUES
    ('Aethoria',    'The central continent, home to humans and the capital city of Solhaven.'),
    ('Frostspire',  'A frozen northern landmass ruled by ancient giants and ice dragons.'),
    ('Verdenmoor',  'Lush tropical islands inhabited by elves and deep jungle creatures.'),
    ('The Ashlands', 'A volcanic wasteland cursed by an ancient cataclysm.'),
    ('The Abyssal Deep', 'Mysterious undersea caverns, accessible only to high-level adventurers.');

-- id = 1, referenced by CharacterService.CreateAsync as the starter item
INSERT INTO items (name, description, item_type, rarity, is_stackable, max_stack, sell_price, buy_price)
VALUES ('Novice Health Potion', 'A basic healing potion for new adventurers.', 'consumable', 'common', TRUE, 99, 2, 5);

-- one level-1 starter skill per class (class IDs follow the character_classes insert order above)
INSERT INTO skills (class_id, name, description, skill_type, mana_cost, min_level, max_rank)
VALUES
    (1, 'Heroic Strike',  'A powerful melee attack.',          'active', 10, 1, 5),
    (2, 'Fireball',       'Launches a ball of fire.',           'active', 20, 1, 5),
    (3, 'Backstab',       'Strike from the shadows.',           'active', 10, 1, 5),
    (4, 'Holy Strike',    'A strike imbued with holy light.',   'active', 15, 1, 5),
    (5, 'Arrow Shot',     'Fire a swift arrow.',                'active',  5, 1, 5),
    (6, 'Shadow Bolt',    'Unleash a bolt of dark energy.',     'active', 20, 1, 5);
