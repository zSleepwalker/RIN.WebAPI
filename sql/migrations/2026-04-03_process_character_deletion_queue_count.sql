DROP FUNCTION IF EXISTS webapi."ProcessCharacterDeletionQueue"();

CREATE OR REPLACE FUNCTION webapi."ProcessCharacterDeletionQueue"() RETURNS integer
    LANGUAGE plpgsql
AS $$
DECLARE
    v_char_guid bigint;
    v_deleted_count integer := 0;
    v_rows_deleted integer;
BEGIN
    FOR v_char_guid IN
        SELECT character_guid
        FROM webapi."DeletionQueue"
        WHERE expires_in <= now()
    LOOP
        UPDATE webapi."Characters"
        SET current_battleframe_guid = NULL
        WHERE character_guid = v_char_guid;

        IF to_regclass('webapi."CharacterBoosts"') IS NOT NULL THEN
            DELETE FROM webapi."CharacterBoosts" WHERE character_guid = v_char_guid;
        END IF;

        IF to_regclass('webapi."CharacterLoadoutItems"') IS NOT NULL THEN
            DELETE FROM webapi."CharacterLoadoutItems" WHERE character_guid = v_char_guid;
        END IF;

        IF to_regclass('webapi."CharacterLoadouts"') IS NOT NULL THEN
            DELETE FROM webapi."CharacterLoadouts" WHERE character_guid = v_char_guid;
        END IF;

        DELETE FROM webapi."ArmyMembers" WHERE character_guid = v_char_guid;

        DELETE FROM webapi."ArmyApplications"
        WHERE character_guid = v_char_guid OR inviter_guid = v_char_guid;

        DELETE FROM webapi."Battleframes" WHERE character_guid = v_char_guid;

        DELETE FROM webapi."Characters" WHERE character_guid = v_char_guid;
        GET DIAGNOSTICS v_rows_deleted = ROW_COUNT;

        DELETE FROM webapi."DeletionQueue" WHERE character_guid = v_char_guid;

        IF v_rows_deleted > 0 THEN
            v_deleted_count := v_deleted_count + v_rows_deleted;
        END IF;
    END LOOP;

    RETURN v_deleted_count;
END;
$$;
