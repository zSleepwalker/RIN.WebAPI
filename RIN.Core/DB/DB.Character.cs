using System.Data;
using Microsoft.Extensions.Logging;
using Dapper;
using System.Text.Json;
using ProtoBuf;
using RIN.Core.ClientApi;
using RIN.Core.Common;
using RIN.Core.Models;
using RIN.Core.Models.ClientApi;
using RIN.Core.Utils;

namespace RIN.Core.DB
{
    public partial class DB
    {
        public async Task<long> CreateNewCharacter(long accountId, string name, bool isDev, int voiceSetId, int gender, byte[] visualsBlob)
        {
            var result = await DBCall(async conn =>
            {
                var p = new DynamicParameters();
                p.Add("@account_id", accountId);
                p.Add("@name", name);
                p.Add("@is_dev", isDev);
                p.Add("@voice_setid", voiceSetId);
                p.Add("@gender", gender);
                p.Add("@visuals", visualsBlob);

                p.Add("@error_text", dbType: DbType.String, direction: ParameterDirection.Output);
                p.Add("@new_character_id", dbType: DbType.Int64, direction: ParameterDirection.Output);

                var r = await conn.ExecuteAsync("webapi.\"CreateNewCharacter\"", p, commandType: CommandType.StoredProcedure);

                return (p.Get<long>("@new_character_id"), p.Get<string>("@error_text"));
            });

            if (result.Item1 == -1) {
                throw new TmwException(result.Item2, result.Item2);
            }

            await SendWelcomeMail(result.Item1);

            return result.Item1;
        }

        public async Task<List<Character>> GetCharactersForAccount(long accountId)
        {
                    const string SELECT_SQL = @"SELECT 
		                        c.character_guid,
		                        c.name,
		                        c.unique_name,
		                        c.is_dev,
		                        c.is_active,
		                        c.created_at,
		                        c.title_id,
		                        c.time_played_secs,
		                        c.needs_name_change,
		                        COALESCE((SELECT MAX(level) FROM webapi.""Battleframes"" WHERE character_guid = c.character_guid), 1) AS max_frame_level,
		                        Battleframes.battleframe_sdb_id AS frame_sdb_id,
		                        COALESCE(Battleframes.level, 1) AS current_level,
		                        c.gender,
		                        c.elite_rank,
								c.pvp_rank,
		                        c.last_seen_at,
		                        c.visuals,
		                        c.race,
		                        dq.deleted_at,
		                        dq.expires_in,
								a.tag as army_tag,
								a.army_guid as army_guid
		                        FROM webapi.""Characters"" c
				                        LEFT JOIN
					                        webapi.""DeletionQueue"" dq
						                        ON dq.character_guid = c.character_guid
				                        LEFT JOIN
					                        webapi.""Battleframes"" as Battleframes
						                        ON Battleframes.id = c.current_battleframe_guid
										LEFT JOIN webapi.""ArmyMembers"" as am 
												ON c.character_guid = am.character_guid
										LEFT JOIN webapi.""Armies"" as a 
												ON am.army_guid = a.army_guid
		                        WHERE c.account_id = @accountId";

            var results = await DBCall(conn => conn.QueryAsync<dynamic>(SELECT_SQL, new {accountId}));

            var chars = new List<Character>(results?.Count() ?? 0);
            if (results != null)
            {
                foreach (var result in results)
                {
                    long? deleted_at = result.deleted_at == null ? null : ((DateTimeOffset)result.deleted_at).ToUnixTimeSeconds();
                    long? expires_in = result.expires_in == null ? null : ((DateTimeOffset)result.expires_in).ToUnixTimeSeconds() - DateTimeOffset.Now.ToUnixTimeSeconds();

                    var character = new Character
                    {
                        character_guid    = result.character_guid,
                        name              = result.name,
                        unique_name       = result.unique_name,
                        is_dev            = result.is_dev,
                        is_active         = result.is_active,
                        created_at        = result.created_at,
                        title_id          = result.title_id,
                        time_played_secs  = result.time_played_secs ?? 0,
                        needs_name_change = result.needs_name_change,
                        max_frame_level   = result.max_frame_level,
                        frame_sdb_id      = result.frame_sdb_id,
                        current_level     = result.current_level,
                        gender            = result.gender,
                        current_gender    = CharacterUtil.GenderNumToString(result.gender),
                        elite_rank        = result.elite_rank,
                        pvp_rank          = result.pvp_rank,
                        army_tag          = result.army_tag,
                        army_guid         = result.army_guid,
                        last_seen_at      = result.last_seen_at,
                        gear              = new List<GearSlot>(),
                        expires_in        = expires_in,
                        deleted_at        = deleted_at,
                        race              = CharacterUtil.RaceIdToString(result.race),
                        migrations        = new List<int>()
                    };

                    if (result.visuals is byte[] visuals && visuals.Length > 0)
                    {
                        var charaterVisuals = Serializer.Deserialize<CharacterVisuals>(visuals.AsSpan());

                        charaterVisuals.ornaments ??= new List<WebId>();

                        character.visuals = new CharacterBattleframeCombinedVisuals();
                        charaterVisuals.ApplyToCharacterVisuals(character.visuals);

                        var defaultBattleframeVisuals = PlayerBattleframeVisuals.CreateDefault();
                        defaultBattleframeVisuals.ApplyToCharacterVisuals(character.visuals);
                    }

                    chars.Add(character);
                }
            }

            return chars;
        }

        public async Task<(BasicCharacterInfo info, CharacterVisuals visuals)> GetBasicCharacterAndVisualData(long charId)
        {
            const string SELECT_SQL = @"SELECT c.name, title_id, gender, race, current_battleframe_guid, bf.battleframe_sdb_id AS CurrentBattleframeSDBId, 
                                 a.tag as ArmyTag, a.army_guid as ArmyGUID, ar.is_officer as ArmyIsOfficer, 
                                 last_zone_id as LastZoneId, last_outpost_id as LastOutpostId, c.time_played_secs as TimePlayed,
                                 c.pvp_rank as PvPRank, c.elite_rank as EliteLevel, acc.staff_flags as StaffFlags,
                                 bf.level AS Level, bf.level AS EffectiveLevel, bf.xp AS Xp,
                                 c.visuals,
                                 (SELECT modifier FROM webapi.""CharacterBoosts"" WHERE character_guid = c.character_guid AND boost_type = 'xp_boost' AND expiration_date > NOW()) as XpBoostModifier,
                                 (SELECT EXTRACT(EPOCH FROM expiration_date)::bigint FROM webapi.""CharacterBoosts"" WHERE character_guid = c.character_guid AND boost_type = 'xp_boost' AND expiration_date > NOW()) as XpBoostExpiration,
                                 (SELECT modifier FROM webapi.""CharacterBoosts"" WHERE character_guid = c.character_guid AND boost_type = 'resource_boost' AND expiration_date > NOW()) as ResourceBoostModifier,
                                 (SELECT EXTRACT(EPOCH FROM expiration_date)::bigint FROM webapi.""CharacterBoosts"" WHERE character_guid = c.character_guid AND boost_type = 'resource_boost' AND expiration_date > NOW()) as ResourceBoostExpiration,
                                 (SELECT modifier FROM webapi.""CharacterBoosts"" WHERE character_guid = c.character_guid AND boost_type = 'reputation_boost' AND expiration_date > NOW()) as ReputationBoostModifier,
                                 (SELECT EXTRACT(EPOCH FROM expiration_date)::bigint FROM webapi.""CharacterBoosts"" WHERE character_guid = c.character_guid AND boost_type = 'reputation_boost' AND expiration_date > NOW()) as ReputationBoostExpiration
  	                        FROM webapi.""Characters"" as c
                                 LEFT JOIN
  					                webapi.""Battleframes"" as bf
  						                ON bf.id = c.current_battleframe_guid
  	                            LEFT JOIN webapi.""ArmyMembers"" as am 
  	                                    ON c.character_guid = am.character_guid
  	                            LEFT JOIN webapi.""Armies"" as a 
  	                                    ON am.army_guid = a.army_guid
  	                            LEFT JOIN webapi.""ArmyRanks"" as ar
  	                                    ON am.army_rank_id = ar.army_rank_id
                                 LEFT JOIN webapi.""Accounts"" as acc
                                         ON acc.account_id = c.account_id
                                 LEFT JOIN webapi.""VipData"" as vd
                                         ON vd.account_id = c.account_id
  	                        WHERE c.character_guid = @charId";


            var result = await DBCall(conn => conn.QueryAsync<BasicCharacterInfo, byte[], (BasicCharacterInfo, CharacterVisuals)>(
                SELECT_SQL,
                map: (charinfo, visuals) =>
                {
                    var parsedVisuals = Utils.MiscUtils.FromProtoBuffByteArray<CharacterVisuals>(visuals.AsSpan()) ?? new CharacterVisuals();
                    return (charinfo, parsedVisuals);
                },
                splitOn: "visuals",
                param: new { charId })
            );

            return result?.Single() ?? default;
        }

        public async Task<bool> UpdateCharacterVisuals(long charId, CharacterVisuals visuals)
        {
            const string UPDATE_SQL = @"UPDATE webapi.""Characters""
	                            SET gender = @gender, race = @race, visuals = @visualsBlob
	                            WHERE character_guid = @charId;";

            var visualsBlob = Utils.MiscUtils.ToProtoBuffByteArray(visuals);
            var result = await DBCall(conn => conn.ExecuteAsync(UPDATE_SQL, new { charId, visuals.gender, visuals.race, visualsBlob }));

            return result > 0;
        }

        // Set this charaters currently equiped battleframe
        public async Task<bool> SetCharacterCurrentBattleframe(long charId, long bfId)
        {
            const string UPDATE_SQL = @"UPDATE webapi.""Characters""
	                            SET current_battleframe_guid = @bfId
	                            WHERE character_guid = @charId;";

            var result = await DBCall(conn => conn.ExecuteAsync(UPDATE_SQL, new { charId, bfId }));

            return result > 0;
        }
        
        public async Task<bool> UpdateCharacterAfterGameSession(long charId, int zoneId, int outpostId, int timePlayed)
        {
            const string UPDATE_SQL = @"UPDATE webapi.""Characters""
                                SET last_zone_id = @zoneId, 
                                    last_outpost_id = @outpostId, 
                                    time_played_secs = time_played_secs + @timePlayed
                                WHERE character_guid = @charId;";

            var result = await DBCall(conn => conn.ExecuteAsync(UPDATE_SQL, new { charId, zoneId, outpostId, timePlayed }));

            return result > 0;
        }

        public async Task<bool> SaveLgvRaceFinish(long charId, int leaderboardId, long timeMs)
        {
            const string UPSERT_SQL = @"INSERT INTO webapi.""LeaderboardEntries"" AS le (
                                leaderboard_id, character_guid, value)
                                VALUES (@leaderboardId, @charId, @timeMs)
                                ON CONFLICT (leaderboard_id, character_guid)
                                DO UPDATE SET value = EXCLUDED.value
                                WHERE EXCLUDED.value < le.value
                                ";

            var result = await DBCall(conn => conn.ExecuteAsync(UPSERT_SQL, new { charId, leaderboardId, timeMs }));

            return result > 0;
        }

        public async Task<Error> SetPendingDeleteCharacterById(long accountId, long characterGuid)
        {
            // TODO: Move calls to be inside the database to make a single DB call

            // Check if character exists and is owned by the account
            const string CHAR_SELECT_SQL = @"SELECT 
                            character_guid
                            FROM webapi.""Characters""
                            WHERE account_id = @accountId
                            AND character_guid = @characterGuid";

            var char_select_results = await DBCall(conn => conn.QueryAsync<dynamic>(CHAR_SELECT_SQL, new { accountId, characterGuid }));

            if ((char_select_results?.Count() ?? 0) == 0)
            {
                return new Error() { code = Error.Codes.ERR_CHAR_NOT_FOUND, message = "Can't find a character with that GUID" };
            }

            // Check if character is an army commander and prevent delete process if true
            const string IS_COMMANDER_SQL = @"SELECT 
                            character_guid
                            FROM webapi.""ArmyMembers"" am
                            INNER JOIN webapi.""ArmyRanks"" ar on ar.army_rank_id = am.army_rank_id
                            WHERE am.character_guid = @characterGuid AND ar.is_commander = true";

            var isCommanderResults = await DBCall(
                conn => conn.QueryAsync<dynamic>(IS_COMMANDER_SQL, new { characterGuid })
            );

            if (isCommanderResults?.Any() ?? false)
            {
                return new Error() { code = Error.Codes.ERR_CANNOT_DELETE_COMMANDER, message = "Character is an army commander" };
            }

            // Check if character is already marked for deletion
            const string SELECT_SQL = @"SELECT 
                            character_guid,
                            deleted_at
                            FROM webapi.""DeletionQueue""
                            WHERE account_id = @accountId
                            AND character_guid = @characterGuid";

            var select_results = await DBCall(conn => conn.QueryAsync<dynamic>(SELECT_SQL, new { accountId, characterGuid }));

            if ((select_results?.Count() ?? 0) == 1)
            {
                return new Error() { code = Error.Codes.ERR_CHAR_DELETED, message = "Character is already marked as deleted" };
            }

            DateTime deleted_at = DateTime.Now;
            DateTime expires_in = deleted_at.AddMonths(1);

            const string INSERT_SQL = @"INSERT
                        INTO webapi.""DeletionQueue""
                        (character_guid, account_id, deleted_at, expires_in)
                        VALUES (@characterGuid, @accountId, @deleted_at, @expires_in)";

            var insert_results = await DBCall(conn => conn.QueryAsync<dynamic>(INSERT_SQL, new { characterGuid, accountId, deleted_at, expires_in }));

            // Uncomment to instantly delete character instead of waiting (for dev-use only)
            /*
            const string DELETE_SQL = @"DELETE 
                        FROM webapi.""Characters"" 
                        WHERE account_id = @accountId
                        AND character_guid = @characterGuid";

            var delete_results = await DBCall(async conn => conn.Query<dynamic>(DELETE_SQL, new { accountId, characterGuid }));
            */

            return new Error() { code = Error.Codes.SUCCESS };
        }

        public async Task<Error> UndeleteCharacterById(long accountId, long characterGuid)
        {
            // TODO: Potentially move this function into the database to reduce DB calls

            // Check if character exists and is owned by the account
            const string SELECT_SQL = @"SELECT 
                            webapi.""Characters"".character_guid,
                            DeletionQueue.deleted_at,
                            DeletionQueue.expires_in
                            FROM webapi.""Characters""
                            LEFT JOIN
                            webapi.""DeletionQueue"" as DeletionQueue
                            ON DeletionQueue.character_guid = webapi.""Characters"".character_guid
                            WHERE webapi.""Characters"".account_id = @accountId
                            AND webapi.""Characters"".character_guid = @characterGuid";

            var select_results = await DBCall(conn => conn.QueryAsync<dynamic>(SELECT_SQL, new { accountId, characterGuid }));

            if ((select_results?.Count() ?? 0) == 0)
            {
                return new Error() { code = Error.Codes.ERR_CHAR_NOT_FOUND, message = "Can't find a character with that GUID" };
            }

            const string DELETE_SQL = @"DELETE
                            FROM webapi.""DeletionQueue""
                            WHERE account_id = @accountId
                            AND character_guid = @characterGuid";

            var delete_results = await DBCall(conn => conn.QueryAsync<dynamic>(DELETE_SQL, new { accountId, characterGuid }));

            return new Error() { code = Error.Codes.SUCCESS };
        }

        public async Task<bool> CheckIfNameIsFree(string name)
        {
            string unique_name = name.ToUpper();
            const string SELECT_SQL = @"SELECT 
                            name
                            FROM webapi.""Characters""
                            WHERE name = @name
                            OR unique_name = @unique_name";

            var select_results = await DBCall(conn => conn.QueryAsync<dynamic>(SELECT_SQL, new { name, unique_name }));

            if ((select_results?.Count() ?? 0) == 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        
        public async Task<bool> IsMemberOfArmy(long characterGuid, long? armyGuid = null)
        { 
            string selectSql = @"
                SELECT character_guid
                FROM webapi.""ArmyMembers""
                WHERE character_guid = @characterGuid";

            if (armyGuid != null)
            {
                selectSql += " AND army_guid = @armyGuid";
            }

            var results = await DBCall(conn => conn.QueryAsync<dynamic>(selectSql, new { characterGuid, armyGuid }));

            return results?.Any() ?? false;
        }

        public async Task<IEnumerable<ArmyApplication>> GetPersonalArmyApplications(long characterGuid)
        {
            const string SELECT_SQL = @"
                SELECT id, a.name AS army_name, army_guid, character_guid, message, 'apply' as direction 
                FROM webapi.""ArmyApplications"" aa
                INNER JOIN webapi.""Armies"" a USING(army_guid)
                WHERE character_guid = @characterGuid AND inviter_guid IS NULL";

            return await DBCall(conn => conn.QueryAsync<ArmyApplication>(SELECT_SQL, new { characterGuid })) ?? Enumerable.Empty<ArmyApplication>();
        }

        public async Task<IEnumerable<ArmyApplication>> GetPersonalArmyInvites(long characterGuid)
        {
            const string SELECT_SQL = @"
                SELECT id, a.name AS army_name, army_guid, character_guid, message, 'invite' AS direction 
                FROM webapi.""ArmyApplications"" aa
                INNER JOIN webapi.""Armies"" a USING(army_guid)
                WHERE character_guid = @characterGuid AND inviter_guid IS NOT NULL";

            var results = await DBCall(conn => conn.QueryAsync<ArmyApplication>(SELECT_SQL, new { characterGuid }));

            return results ?? Enumerable.Empty<ArmyApplication>();
        }
        public async Task<long> AddCharacterItem(long characterGuid, int sdbId)
        {
            const string INSERT_SQL = @"INSERT INTO webapi.""CharacterItems"" (item_guid, character_guid, sdb_id)
                                        VALUES (webapi.create_entity_guid(253), @characterGuid, @sdbId) RETURNING item_guid;";

            var result = await DBCall(conn => conn.QuerySingleAsync<long>(INSERT_SQL, new { characterGuid, sdbId }),
                exception =>
                {
                    Logger.LogError(exception, "Error adding item {sdbId} for character {characterGuid}", sdbId, characterGuid);
                    throw exception;
                });

            await NotifyInventoryUpdate(characterGuid);
            return result;
        }

        public async Task<bool> AddOrUpdateCharacterResource(long characterGuid, int sdbId, int quantity)
        {
            const string UPSERT_SQL = @"INSERT INTO webapi.""CharacterResources"" (character_guid, sdb_id, quantity)
                                        VALUES (@characterGuid, @sdbId, @quantity)
                                        ON CONFLICT (character_guid, sdb_id)
                                        DO UPDATE SET quantity = webapi.""CharacterResources"".quantity + EXCLUDED.quantity;";

            var result = await DBCall(conn => conn.ExecuteAsync(UPSERT_SQL, new { characterGuid, sdbId, quantity }),
                exception =>
                {
                    Logger.LogError(exception, "Error adding/updating resource {sdbId} for character {characterGuid}", sdbId, characterGuid);
                    throw exception;
                });

            if (result > 0)
            {
                await NotifyInventoryUpdate(characterGuid);
            }
            return result > 0;
        }

        public async Task<bool> ConsumeCharacterResource(long characterGuid, int sdbId, int quantity)
        {
            // 1. Try consuming from CharacterResources (stackables)
            const string UPDATE_SQL = @"UPDATE webapi.""CharacterResources""
                                        SET quantity = quantity - @quantity
                                        WHERE character_guid = @characterGuid AND sdb_id = @sdbId AND quantity >= @quantity;";

            var resourceAffected = await DBCall(conn => conn.ExecuteAsync(UPDATE_SQL, new { characterGuid, sdbId, quantity }),
                exception =>
                {
                    Logger.LogError(exception, "Error consuming resource {sdbId} for character {characterGuid}", sdbId, characterGuid);
                    throw exception;
                });

            if (resourceAffected > 0)
            {
                // Cleanup empty stacks
                const string DELETE_SQL = @"DELETE FROM webapi.""CharacterResources"" WHERE character_guid = @characterGuid AND sdb_id = @sdbId AND quantity <= 0;";
                await DBCall(conn => conn.ExecuteAsync(DELETE_SQL, new { characterGuid, sdbId }),
                    exception =>
                    {
                        Logger.LogError(exception, "Error cleaning up resource {sdbId} for character {characterGuid}", sdbId, characterGuid);
                        throw exception;
                    });

                await NotifyInventoryUpdate(characterGuid);
                return true;
            }

            // 2. Try consuming from CharacterItems (individual items)
            // Delete 'quantity' number of items with matching SdbId
            const string DELETE_ITEMS_SQL = @"DELETE FROM webapi.""CharacterItems""
                                               WHERE item_guid IN (
                                                   SELECT item_guid FROM webapi.""CharacterItems""
                                                   WHERE character_guid = @characterGuid AND sdb_id = @sdbId
                                                   LIMIT @quantity
                                               );";

            var itemsAffected = await DBCall(conn => conn.ExecuteAsync(DELETE_ITEMS_SQL, new { characterGuid, sdbId, quantity }),
                exception =>
                {
                    Logger.LogError(exception, "Error consuming items with sdbId {sdbId} for character {characterGuid}", sdbId, characterGuid);
                    throw exception;
                });

            if (itemsAffected >= quantity)
            {
                await NotifyInventoryUpdate(characterGuid);
                return true;
            }

            return false;
        }

        public async Task<(IEnumerable<(long item_guid, int sdb_id)> items, IEnumerable<(int sdb_id, int quantity)> resources)> GetCharacterInventory(long characterGuid)
        {
            const string ITEMS_SQL = @"SELECT item_guid, sdb_id FROM webapi.""CharacterItems"" WHERE character_guid = @characterGuid;";
            const string RES_SQL = @"SELECT sdb_id, quantity FROM webapi.""CharacterResources"" WHERE character_guid = @characterGuid;";

            var items = await DBCall(conn => conn.QueryAsync<(long item_guid, int sdb_id)>(ITEMS_SQL, new { characterGuid }));
            var resources = await DBCall(conn => conn.QueryAsync<(int sdb_id, int quantity)>(RES_SQL, new { characterGuid }));

            return (items!, resources!);
        }

        public async Task ProcessCharacterDeletionQueue()
        {
            await DBCall(conn => conn.ExecuteAsync(@"SELECT webapi.""ProcessCharacterDeletionQueue""()"),
                exception =>
                {
                    Logger.LogError(exception, "Error processing character deletion queue");
                    throw exception;
                });
        }
        public async Task<IEnumerable<CharacterLoadout>> GetCharacterLoadouts(long characterGuid)
        {
            const string SELECT_SQL = @"SELECT loadout_id as LoadoutId, battleframe_sdb_id as ChassisSdbId, visuals as Visuals, slotted_items as SlottedItems 
                                        FROM webapi.""CharacterLoadouts"" 
                                        WHERE character_guid = @characterGuid;";
            
            return await DBCall(conn => conn.QueryAsync<CharacterLoadout>(SELECT_SQL, new { characterGuid })) ?? Enumerable.Empty<CharacterLoadout>();
        }

        public async Task<bool> SaveCharacterLoadout(long characterGuid, int loadoutId, int chassisSdbId, string visualsJson, string slottedItemsJson)
        {
            const string UPSERT_SQL = @"INSERT INTO webapi.""CharacterLoadouts"" (character_guid, loadout_id, battleframe_sdb_id, visuals, slotted_items)
                                        VALUES (@characterGuid, @loadoutId, @chassisSdbId, @visualsJson::jsonb, @slottedItemsJson::jsonb)
                                        ON CONFLICT (character_guid, loadout_id) 
                                        DO UPDATE SET 
                                            battleframe_sdb_id = EXCLUDED.battleframe_sdb_id,
                                            visuals = EXCLUDED.visuals,
                                            slotted_items = EXCLUDED.slotted_items;";

            var result = await DBCall(conn => conn.ExecuteAsync(UPSERT_SQL, new { characterGuid, loadoutId, chassisSdbId, visualsJson, slottedItemsJson }));
            return result > 0;
        }

        public async Task<bool> ConsumeCharacterItem(long characterGuid, int sdbId, int quantity)
        {
            // Similar to ConsumeCharacterResource but for the CharacterItems table
            const string DELETE_ITEMS_SQL = @"DELETE FROM webapi.""CharacterItems""
                                                WHERE item_guid IN (
                                                    SELECT item_guid FROM webapi.""CharacterItems""
                                                    WHERE character_guid = @characterGuid AND sdb_id = @sdbId
                                                    LIMIT @quantity
                                                );";

            var itemsAffected = await DBCall(conn => conn.ExecuteAsync(DELETE_ITEMS_SQL, new { characterGuid, sdbId, quantity }),
                exception =>
                {
                    Logger.LogError(exception, "Error consuming items with sdbId {sdbId} for character {characterGuid}", sdbId, characterGuid);
                    throw exception;
                });

            if (itemsAffected >= quantity)
            {
                await NotifyInventoryUpdate(characterGuid);
                return true;
            }

            return false;
        }

        public async Task NotifyInventoryUpdate(long characterGuid)
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(new { CharacterGuid = (ulong)characterGuid });
            var notifySql = $@"NOTIFY events, 'InventoryUpdated->{payload}'";
            await DBCall(conn => conn.ExecuteAsync(notifySql));
        }

        public async Task<bool> AddOrExtendBoost(long charId, string type, float modifier, int durationSecs)
        {
            const string UPSERT_SQL = @"
                INSERT INTO webapi.""CharacterBoosts"" (character_guid, boost_type, modifier, expiration_date)
                VALUES (@charId, @type, @modifier, NOW() + (@durationSecs || ' seconds')::interval)
                ON CONFLICT (character_guid, boost_type)
                DO UPDATE SET 
                    modifier = EXCLUDED.modifier,
                    expiration_date = GREATEST(webapi.""CharacterBoosts"".expiration_date, NOW()) + (@durationSecs || ' seconds')::interval;";

            var result = await DBCall(conn => conn.ExecuteAsync(UPSERT_SQL, new { charId, type, modifier, durationSecs }));
            return result > 0;
        }

        public async Task<IEnumerable<Models.DB.CharacterBoost>> GetActiveBoosts(long charId)
        {
            const string SELECT_SQL = @"SELECT character_guid, boost_type, modifier, expiration_date 
                                        FROM webapi.""CharacterBoosts"" 
                                        WHERE character_guid = @charId AND expiration_date > NOW();";

            return await DBCall(conn => conn.QueryAsync<Models.DB.CharacterBoost>(SELECT_SQL, new { charId })) ?? Enumerable.Empty<Models.DB.CharacterBoost>();
        }
    }
}
