--
-- Automated Character Deletion Process
--

--
-- Name: ProcessCharacterDeletionQueue(); Type: FUNCTION; Schema: webapi;
--
CREATE OR REPLACE FUNCTION webapi."ProcessCharacterDeletionQueue"() RETURNS void
    LANGUAGE plpgsql
AS $$
DECLARE
    v_char_guid bigint;
BEGIN
    -- Loop through all expired characters in the deletion queue
    FOR v_char_guid IN 
        SELECT character_guid 
        FROM webapi."DeletionQueue" 
        WHERE expires_in <= now()
    LOOP
        -- 1. Sever the circular reference with Battleframes
        UPDATE webapi."Characters" 
        SET current_battleframe_guid = NULL 
        WHERE character_guid = v_char_guid;

        -- 2. Manual cleanup for tables without ON DELETE CASCADE
        
        -- Remove from ArmyMembers
        DELETE FROM webapi."ArmyMembers" WHERE character_guid = v_char_guid;
        
        -- Remove from ArmyApplications (where character is applicant or inviter)
        DELETE FROM webapi."ArmyApplications" 
        WHERE character_guid = v_char_guid OR inviter_guid = v_char_guid;
        
        -- Remove from Battleframes
        DELETE FROM webapi."Battleframes" WHERE character_guid = v_char_guid;

        -- 3. Final deletion from Characters
        -- This will trigger ON DELETE CASCADE for:
        -- - CharacterItems
        -- - CharacterResources
        -- - MailMessages (and child MailAttachments)
        -- - LeaderboardEntries
        DELETE FROM webapi."Characters" WHERE character_guid = v_char_guid;

        -- 4. Remove from DeletionQueue
        DELETE FROM webapi."DeletionQueue" WHERE character_guid = v_char_guid;
        
        RAISE NOTICE 'Processed deletion for character %', v_char_guid;
    END LOOP;
END;
$$;
