CREATE TABLE IF NOT EXISTS webapi."CharacterLoadoutItems" (
    character_guid bigint NOT NULL,
    loadout_id integer NOT NULL,
    slot_type integer NOT NULL,
    item_guid bigint NOT NULL,
    item_sdb_id integer NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (character_guid, loadout_id, slot_type),
    UNIQUE (item_guid),
    CONSTRAINT fk_character_loadout_items_character FOREIGN KEY (character_guid)
        REFERENCES webapi."Characters"(character_guid) ON DELETE CASCADE,
    CONSTRAINT fk_character_loadout_items_loadout FOREIGN KEY (character_guid, loadout_id)
        REFERENCES webapi."CharacterLoadouts"(character_guid, loadout_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_character_loadout_items_character
    ON webapi."CharacterLoadoutItems"(character_guid);

CREATE INDEX IF NOT EXISTS idx_character_loadout_items_loadout
    ON webapi."CharacterLoadoutItems"(character_guid, loadout_id);

INSERT INTO webapi."CharacterLoadoutItems" (character_guid, loadout_id, slot_type, item_guid, item_sdb_id)
SELECT
    cl.character_guid,
    cl.loadout_id,
    (kv.key)::integer AS slot_type,
    (kv.value)::bigint AS item_guid,
    ci.sdb_id AS item_sdb_id
FROM webapi."CharacterLoadouts" cl
CROSS JOIN LATERAL jsonb_each_text(COALESCE(cl.slotted_items, '{}'::jsonb)) kv
JOIN webapi."CharacterItems" ci
    ON ci.character_guid = cl.character_guid
    AND ci.item_guid = (kv.value)::bigint
ON CONFLICT (item_guid) DO NOTHING;

DELETE FROM webapi."CharacterItems" ci
USING webapi."CharacterLoadoutItems" cli
WHERE ci.character_guid = cli.character_guid
    AND ci.item_guid = cli.item_guid;
