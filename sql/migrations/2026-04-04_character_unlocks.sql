CREATE TABLE IF NOT EXISTS webapi."CharacterUnlocks" (
    character_guid BIGINT NOT NULL REFERENCES webapi."Characters"(character_guid) ON DELETE CASCADE,
    unlock_type TEXT NOT NULL,
    unlock_id INTEGER NOT NULL,
    frame_id INTEGER NOT NULL DEFAULT 0,
    unlocked_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (character_guid, unlock_type, unlock_id, frame_id)
);

CREATE INDEX IF NOT EXISTS idx_character_unlocks_character
    ON webapi."CharacterUnlocks" (character_guid);
