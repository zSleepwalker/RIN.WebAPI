-- New table for character loadouts
CREATE TABLE IF NOT EXISTS webapi."CharacterLoadouts" (
    character_guid bigint NOT NULL,
    loadout_id integer NOT NULL,
    battleframe_sdb_id integer NOT NULL,
    visuals jsonb NOT NULL DEFAULT '{}',
    slotted_items jsonb NOT NULL DEFAULT '{}',
    PRIMARY KEY (character_guid, loadout_id),
    CONSTRAINT character_loadouts_character_guid_fkey 
        FOREIGN KEY (character_guid) 
        REFERENCES webapi."Characters"(character_guid) 
        ON DELETE CASCADE
);

ALTER TABLE webapi."CharacterLoadouts" OWNER TO tmwadmin;

-- Add comment explaining the table
COMMENT ON TABLE webapi."CharacterLoadouts" IS 'Persistent storage for user loadouts, including gear and visual overrides.';