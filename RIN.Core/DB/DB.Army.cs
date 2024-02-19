using System.ComponentModel.DataAnnotations.Schema;
using Dapper;
using Microsoft.Extensions.Logging;
using RIN.Core.ClientApi;

namespace RIN.Core.DB
{
    public partial class DB
    {
        public async Task<CreateArmyResp> ArmyCreate(
            long accountId, long dateTime, short armyMinSize, short armyMaxSize, string name, string uniqueName, string description, 
            string playstyle, string personality, bool isRecruiting, long characterGuid, string region, string website = "")
        {
            // Create Army
            const string INSERT_SQL_A = @"INSERT INTO webapi.""Army"" (
                    account_id, army_guid, character_guid, name, unique_name, description, playstyle, personality, motd, 
                    is_recruiting, created_at, updated_at, commander_guid, tag_position, min_size, max_size, disbanded, 
                    website, mass_email, region, login_message, timezone, established_at, tag, language)
	            VALUES (@account_id, webapi.create_entity_guid(252), @characterGuid, @name, @uniqueName, @description, @playstyle, @personality, '', @isRecruiting, @dateTime, 
	                    @dateTime, @characterGuid, 0, @armyMinSize, @armyMaxSize, false, '', false, @region, '', '+0000', @dateTime, '', 'EN')
                RETURNING id AS army_id, army_guid;";
            
            var resultArmy = await DBCall(async conn => conn.QuerySingle<CreateArmyResp>(INSERT_SQL_A, new { account_id = accountId,
                    characterGuid, name, uniqueName, description, playstyle, personality, isRecruiting, dateTime, armyMinSize, armyMaxSize, region }),
                exception =>
                {
                    Logger.LogError("Error creating a army for {characterGuid} due to: {exception}", characterGuid, exception);
                    
                });

            return resultArmy;
        }
        
        public async Task<List<Army>> ArmyList(int page, int perPage)
        {
            const string SELECT_SQL = @"SELECT 
                    MIN(c.name) AS commander, 
                    CONCAT('/armies/', a.army_guid) AS link, 
                    a.name, 
                    personality, 
                    playstyle, 
                    CASE
                        WHEN is_recruiting=true THEN 'Yes'
                        ELSE 'No'
                    END as is_recruiting_str, 
                    COUNT(am.army_id) AS member_count,  
                    is_recruiting,  
                    a.army_guid, 
                    a.disbanded, 
                    tag, 
                    region 
                FROM webapi.""Army"" a 
                LEFT JOIN webapi.""ArmyMembers"" am 
                    ON (a.army_guid = am.army_guid) 
                LEFT JOIN webapi.""Characters"" c 
                    ON (a.character_guid = c.character_guid) 
                WHERE disbanded = false 
                GROUP BY a.disbanded, a.army_guid, a.name, a.personality, a.playstyle, a.is_recruiting, a.tag, a.region, c.name 
                ORDER BY a.army_guid ASC  
                LIMIT @per_page OFFSET @offset";

            int offset = (page - 1) * perPage;

            var armyList = await DBCall(async conn => conn.Query<Army>(SELECT_SQL, new { per_page = perPage, offset }),
                exception =>
                {
                    Logger.LogError("Error collecting army list due to: {exception}", exception);
                });

            return armyList.ToList();
        }

        public async Task<ArmyInfo> ArmyInfo(long armyGuid)
        {
            const string SELECT_SQL = @"SELECT a.id, account_id, a.army_guid, a.character_guid, a.name, description, playstyle, personality, motd, 
                    is_recruiting, a.created_at, a.updated_at, commander_guid, tag_position, min_size, max_size, disbanded, website, mass_email, 
                    region, login_message, timezone, a.established_at, tag, a.language, COUNT(am.army_id) AS member_count, 
                    (SELECT id FROM webapi.""ArmyRanks"" WHERE ""ArmyRanks"".army_guid = @armyGuid AND ""ArmyRanks"".is_default = true) as default_rank_id 
                FROM webapi.""Army"" a 
                LEFT JOIN webapi.""ArmyMembers"" am 
                    ON (a.army_guid = am.army_guid) 
                LEFT JOIN webapi.""ArmyRanks"" ar 
                    ON (a.army_guid = ar.army_guid) 
                WHERE a.army_guid = @armyGuid AND disbanded = false 
                GROUP BY a.id, a.name, am.id";

            var armyInfo = await DBCall(async conn => conn.QuerySingle<ArmyInfo>(SELECT_SQL, new { armyGuid }),
                exception =>
                {
                    Logger.LogError("Error collecting army ({armyGuid}) info  due to: {exception}", armyGuid, exception);
                });

            return armyInfo;
        }

        public async Task<bool> ArmySetInfo(long armyGuid)
        {
            return false;
        }
        
        public async Task<Error> ArmyEstablish(long armyGuid, long characterGuid, string tag)
        {
            var rankId = ArmyMemberRank(armyGuid, characterGuid).Result.id;
            if (await ArmyHasAccess(characterGuid, armyGuid, rankId, "can_edit"))
            {
                Logger.LogError("Access denied establishing army ({armyGuid}) for character ({currentCharacterGuid})", armyGuid, characterGuid);
                return new Error() { code = Error.Codes.ERR_UNKNOWN };
            }
            
            const string UPDATE_SQL = @"UPDATE webapi.""Army"" SET  tag=@tag WHERE army_guid=@armyGuid;";
            
            var dispandArmy = await DBCall(conn => conn.ExecuteAsync(UPDATE_SQL, new { tag, armyGuid }),
                exception =>
                {
                    Logger.LogError("Error updating army tag for army ({armyGuid}) due to: {exception}", armyGuid, exception);
                });
            
            if (dispandArmy <= 0)
                return new Error() { code = Error.Codes.ERR_UNKNOWN };
            
            return new Error() { code = Error.Codes.SUCCESS };
        }
        
        public async Task<bool> ArmyLeave(long armyGuid, long characterGuid)
        {
            return false;
        }
        
        public async Task<Error> ArmyStepDown(long armyGuid, long currentCharacterGuid, long newCharacterGuid)
        {
            var rankId = ArmyMemberRank(armyGuid, currentCharacterGuid).Result.id;
            if (await ArmyHasAccess(currentCharacterGuid, armyGuid, rankId, "is_commander"))
            {
                Logger.LogError("Access denied dispanding army ({armyGuid}) for character ({currentCharacterGuid})", armyGuid, currentCharacterGuid);
                return new Error() { code = Error.Codes.ERR_UNKNOWN };
            }
            
            // TODO: Change newCharacterGuid rank to commander rank
            // TODO: Change currentCharacterGuid rank to default rank
            
            return new Error() { code = Error.Codes.SUCCESS };
        }
        
        // TODO: Change so army is marked as dispanded before deleteing so recovery can happen
        public async Task<Error> ArmyDisband(long armyGuid, long characterGuid)
        {
            var rankId = ArmyMemberRank(armyGuid, characterGuid).Result.id;
            if (await ArmyHasAccess(characterGuid, armyGuid, rankId, "is_commander"))
            {
                Logger.LogError("Access denied dispanding army ({armyGuid}) for character ({characterGuid})", armyGuid, characterGuid);
                return new Error() { code = Error.Codes.ERR_UNKNOWN };
            }
            
            const string DELETE_SQL = @"DELETE FROM webapi.""ArmyMembers"" WHERE army_guid = @armyGuid;
                DELETE FROM webapi.""ArmyApplications"" WHERE army_guid = @armyGuid;
                DELETE FROM webapi.""ArmyRanks"" WHERE army_guid = @armyGuid;
                DELETE FROM webapi.""Army"" WHERE army_guid = @armyGuid;";
            
            var dispandArmy = await DBCall(conn => conn.ExecuteAsync(DELETE_SQL, new { armyGuid }),
                exception =>
                {
                    Logger.LogError("Error dispanding army ({armyGuid}) due to: {exception}", armyGuid, exception);
                });
            
            if (dispandArmy <= 0)
                return new Error() { code = Error.Codes.ERR_UNKNOWN };
            
            return new Error() { code = Error.Codes.SUCCESS };
        }
        
        public async Task<List<ArmyRank>> ArmyRankList(long armyGuid)
        {
            const string SELECT_SQL = @"SELECT id, army_id, army_guid, name, is_commander, can_invite, can_kick, created_at, 
                    updated_at, can_edit, can_promote, position, is_officer, can_edit_motd, can_mass_email, is_default
                FROM webapi.""ArmyRanks""
                WHERE army_guid = @armyGuid";

            var rankResults = await DBCall(async conn => conn.Query<ArmyRank>(SELECT_SQL, new { armyGuid }));

            return rankResults.ToList();
        }

        public async Task<long> ArmyCreateOwnerRank(long armyId, long armyGuid)
        {
            var dateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            
            const string INSERT_SQL = @"INSERT INTO webapi.""ArmyRanks""
                    (army_id, army_guid, name, is_commander, can_invite, can_kick, created_at, updated_at, can_edit, can_promote, position, is_officer, can_edit_motd, can_mass_email, is_default)
                VALUES(@armyId, @armyGuid, @armyName, true, true, true, @dateTime, @dateTime, true, true, 1, true, true, true, false)
                RETURNING id AS owner_rank_id";
            
            var rankId = await DBCall(async conn => conn.QuerySingle<long>(INSERT_SQL, new { armyId, armyGuid, armyName = "Owner", dateTime }),
                exception =>
                {
                    Logger.LogError("Error creating rank for army ({armyGuid}) due to: {exception}", armyGuid, exception);
                });

            return rankId;
        }

        public async Task<Error> ArmyAddRank(long armyId, long armyGuid, string name = "", bool isDefault = false, bool isCommander = false, bool isOfficer = false, 
            bool canInvite = false, bool canKick = false, bool canEdit = false, bool canPromote = false, bool canEditMotd = false, bool canMassEmail = false, short position = 2)
        {
            var dateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            
            const string INSERT_SQL = @"INSERT INTO webapi.""ArmyRanks""
                    (army_id, army_guid, ""name"", is_commander, can_invite, can_kick, created_at, updated_at, can_edit, can_promote, ""position"", is_officer, can_edit_motd, can_mass_email, is_default)
                VALUES(@armyId, @armyGuid, @name, @isCommander, @canInvite, @canKick, @dateTime, @dateTime, @canEdit, @canPromote, @position, @isOfficer, @canEditMotd, @canMassEmail, @isDefault)
                RETURNING id";
            
            var rankId = await DBCall(async conn => conn.QuerySingle<long>(INSERT_SQL, new
                {
                    armyId, armyGuid, name, isCommander, canInvite, canKick, dateTime, canEdit, canPromote, position, isOfficer, canEditMotd, canMassEmail, isDefault
                }),
                exception =>
                {
                    Logger.LogError("Error creating rank for army ({armyGuid}) due to: {exception}", armyGuid, exception);
                });

            if (rankId <= 0)
            {
                return new Error() { code = Error.Codes.ERR_UNKNOWN };
            }
            
            return new Error() { code = Error.Codes.SUCCESS };
        }
        
        public async Task<bool> ArmyUpdateRank(long armyGuid)
        {
            return false;
        }
        
        public async Task<bool> ArmyDeleteRank(long armyGuid)
        {
            return false;
        }
        
        public async Task<bool> ArmyBatchUpdateRanks(long armyGuid)
        {
            return false;
        }
        
        public async Task<bool> ArmyBatchUpdateRankOrder(long armyGuid)
        {
            return false;
        }
        
        public async Task<ArmyRank> ArmyMemberRank(long armyGuid, long characterGuid)
        {
            const string SELECT_SQL = @"SELECT id, army_id, army_guid, name, is_commander, can_invite, can_kick, created_at, 
                    updated_at, can_edit, can_promote, position, is_officer, can_edit_motd, can_mass_email, is_default
                FROM webapi.""ArmyRanks""
                WHERE army_guid = @armyGuid
                AND id = @armyRankId";

            var rankResults = await DBCall(async conn => conn.QuerySingle<ArmyRank>(SELECT_SQL, new { armyGuid, characterGuid }));

            return rankResults;
        }
        
        public async Task<List<ArmyMember>> ArmyMemberList(long armyGuid, int page, int perPage)
        {
            const string SELECT_SQL = @"SELECT am.id, am.army_id, am.army_guid, am.character_guid, army_rank_id, am.created_at, am.updated_at, 
                    ar.name AS rank_name, ar.position AS rank_position, DATE_PART('EPOCH', c.last_seen_at) AS last_seen_at, 
                    MAX(b.level) AS current_level, b.battleframe_sdb_id AS current_frame_sdb_id, 
                    c.is_active AS is_online, public_note, officer_note, c.name AS name 
                FROM webapi.""ArmyMembers"" am
                LEFT JOIN webapi.""ArmyRanks"" ar 
                    ON (army_rank_id = ar.id) 
                LEFT JOIN webapi.""Characters"" c 
                    ON (am.character_guid = c.character_guid) 
                LEFT JOIN webapi.""Battleframes"" b 
                    ON (c.character_guid = b.character_guid) 
                WHERE am.army_guid = @armyGuid 
                GROUP BY am.id, ar.name, ar.position, c.last_seen_at, b.battleframe_sdb_id, c.is_active, c.name 
                ORDER BY army_id ASC 
                LIMIT @per_page OFFSET @offset";

            int offset = (page - 1) * perPage;

            var memberList = await DBCall(async conn => conn.Query<ArmyMember>(SELECT_SQL, new { armyGuid, per_page = perPage, offset }),
                exception =>
                {
                    Logger.LogError("Error collecting army members for armyId({armyGuid}) due to: {exception}", armyGuid, exception);
                    throw exception;
                });

            return memberList.ToList();
        }

        public async Task<Error> ArmyAddMember(long armyId, long armyGuid, long characterGuid, long armyRankId)
        {
            var dateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            
            const string INSERT_SQL = @"INSERT INTO webapi.""ArmyMembers"" 
                    (army_id, army_guid, character_guid, army_rank_id, created_at, updated_at, public_note, officer_note)
                VALUES (@armyId, @armyGuid, @characterGuid, @armyRankId, @dateTime, @dateTime, '', '')
                RETURNING id";

            var resultMember = await DBCall(async conn => conn.QuerySingle<uint>(INSERT_SQL, new { armyId, armyGuid, characterGuid, armyRankId, dateTime }),
                exception =>
                {
                    Logger.LogError("Error creating a army ({armyGuid}) owner for {characterGuid} due to: {exception}", armyGuid, characterGuid, exception);
                });

            if (resultMember <= 0)
            {
                return new Error() { code = Error.Codes.ERR_UNKNOWN, message = "Failed to dispand army."};
            }

            return new Error() { code = Error.Codes.SUCCESS };
        }

        public async Task<List<ArmyOfficers>> ArmyOfficers(long armyGuid)
        {
            const string SELECT_SQL = @"SELECT ar.name AS rank_name, c.is_active, c.name 
                FROM webapi.""ArmyMembers"" am 
                LEFT JOIN webapi.""ArmyRanks"" ar 
                    ON (army_rank_id = ar.id) 
                LEFT JOIN webapi.""Characters"" c 
                    ON (am.character_guid = c.character_guid)
                WHERE am.army_guid = @armyGuid AND ar.is_officer = true";

            var officersList = await DBCall(async conn => conn.Query<ArmyOfficers>(SELECT_SQL, new { armyGuid }),
                exception =>
                {
                    Logger.LogError("Error collecting army officers for armyId({armyId}) due to: {exception}", armyGuid, exception);
                    throw exception;
                });

            return officersList.ToList();
        }

        public async Task<List<ArmyApplication>> ArmyApplicationList(long armyGuid)
        {
            const string SELECT_SQL = @"SELECT am.id, am.army_id, am.character_guid, message, direction, am.created_at, 
                    am.updated_at, am.army_guid, MAX(b.level), b.battleframe_sdb_id, c.is_active, c.name 
                FROM webapi.""ArmyApplications"" am 
                LEFT JOIN webapi.""Characters"" c 
                    ON (am.character_guid = c.character_guid) 
                LEFT JOIN webapi.""Battleframes"" b 
                    ON (c.character_guid = b.character_guid) 
                WHERE am.army_guid = @armyGuid 
                GROUP BY am.army_guid, b.battleframe_sdb_id, c.is_active, c.name, am.id";

            var results = await DBCall(async conn => conn.Query<ArmyApplication>(SELECT_SQL, new { armyGuid }),
                exception =>
                {
                    Logger.LogError("Error collecting army applications for armyId({armyGuid}) due to: {exception}", armyGuid, exception);
                    throw exception;
                });

            return results.ToList();
        }
        
        public async Task<List<CharacterArmyApplication>> ArmyCharacterApplications(long characterGuid, bool invite)
        {
            const string SELECT_SQL = @"SELECT id, army_id, character_guid, message, direction, created_at, updated_at, army_guid, invite
                FROM webapi.""ArmyApplications""
                WHERE character_guid = @characterGuid
                    AND invite=@invite";

            var characterApplications = await DBCall(async conn => conn.Query<CharacterArmyApplication>(SELECT_SQL, new { characterGuid, invite }));

            return characterApplications.ToList();
        }

        /// <summary>
        /// Checks army name if it exists before creating a new one
        /// </summary>
        public async Task<bool> ArmyExists(string name, string uniqueName)
        {
            const string SELECT_SQL = @"SELECT id FROM webapi.""Army"" WHERE name = @name OR unique_name = @uniqueName";

            var selectResults = await DBCall(async conn => conn.Query<int>(SELECT_SQL, new { name, uniqueName }));

            if (selectResults.Count() == 0) { return false; }
            
            return true;
        }
        
        /// <summary>
        /// Checks rank access within the army, so checks can be done before performing an action
        /// Options: is_commander, can_invite, can_kick, can_edit, can_promote, can_edit_motd, can_mass_email
        /// </summary>
        public async Task<bool> ArmyHasAccess(long characterGuid, long armyGuid, long armyRankId, string access)
        {
            const string SELECT_SQL_M = @"SELECT id FROM webapi.""ArmyMembers"" WHERE id = @armyRankId AND character_guid = @characterGuid AND army_guid = @armyGuid";
            
            var rankResults = await DBCall(async conn => conn.Query<long>(SELECT_SQL_M, new { armyRankId, characterGuid, armyGuid }));

            if (rankResults.Count() > 0)
            {
                const string SELECT_SQL_R =
                    @"SELECT is_commander, can_invite, can_kick, can_edit, can_promote, can_edit_motd, can_mass_email
                                FROM webapi.""ArmyRanks""
                                WHERE army_guid = @armyGuid
                                AND id = @armyRankId";
                
                var accessResults = await DBCall(async conn =>
                    conn.QuerySingle<ArmyUserAccess>(SELECT_SQL_R, new { armyGuid, armyRankId }));

                var customAccessCheck = accessResults.GetType().GetProperty($"{access}");
                if (accessResults.is_commander || (bool)customAccessCheck.GetValue(accessResults, null))
                    return true;
            }

            return false;
        }
        
        /// <summary>
        /// Verifies so the user is not already in an Army
        /// </summary>
        public async Task<bool> ArmyIsMember(long characterGuid)
        {
            const string SELECT_SQL = @"SELECT id FROM webapi.""ArmyMembers"" WHERE character_guid = @characterGuid";

            var countResults = await DBCall(async conn => conn.Query<long>(SELECT_SQL, new { characterGuid }));

            if (countResults.Count() == 0) { return false; }
            
            return true;
        }
    }
}