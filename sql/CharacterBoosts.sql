CREATE TABLE IF NOT EXISTS webapi."CharacterBoosts" (
    character_guid bigint NOT NULL,
    boost_type text NOT NULL, -- 'xp_boost', 'resource_boost', 'reputation_boost'
    modifier float NOT NULL,
    expiration_date timestamp with time zone NOT NULL,
    PRIMARY KEY (character_guid, boost_type)
);

CREATE INDEX IF NOT EXISTS idx_character_boosts_expiration ON webapi."CharacterBoosts" (expiration_date);
