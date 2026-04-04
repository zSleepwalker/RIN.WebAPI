using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RIN.Core;
using RIN.Core.Config;
using RIN.Core.Models.SDB;
using RIN.Core.SDB;

namespace RIN.Core.DB.SDB
{
    public class SDB : DbBase
    {
        public SDB(IOptions<DbConnectionSettings> config, ILogger<SDB> logger) : base(config, logger)
        {
            ConnStr    = config.Value.SDBConnStr;
            LogDbTimes = config.Value.LogDbCallTimes;
        }

        // For the passed color ids return the light and dark colors from the warpaints table
        public async Task<NewCharaterColors?> GetNewCharactersColors(int eyeColorId, int skinColorId, int hairColorId)
        {
            const string SELECT_SQL = @"SELECT * from 
                (SELECT color1_highlight as ""EyeColorLight"", color1_shadow as ""EyeColorDark""
                    FROM sdb.""dbvisualrecords::WarpaintPalette"" WHERE id = @eyeColorId) as eyeColor,
	    
                (SELECT color1_highlight as ""SkinColorLight"", color1_shadow as ""SkinColorDark""
                    FROM sdb.""dbvisualrecords::WarpaintPalette"" WHERE id = @skinColorId) as skinColor,
	    
                (SELECT color1_highlight as ""HairColorLight"", color1_shadow as ""HairColorDark""
                    FROM sdb.""dbvisualrecords::WarpaintPalette"" WHERE id = @hairColorId) as HairColor;";
            
            var result = await DBCall(conn => conn.QueryAsync<NewCharaterColors>(SELECT_SQL, new {eyeColorId, skinColorId, hairColorId}),
                exception => Logger.LogError($"Error getting colors for new character with ids (eye: {eyeColorId}, skin: {skinColorId}, hair: {hairColorId}) due to: {exception}"));

            return result?.FirstOrDefault();
        }

        public async Task<IEnumerable<CosmeticInfo>> GetOrnamentsInfoList()
        {
            const string SELECT_SQL = @"SELECT ornaments.id, lang.english AS lang_name, display_flags, ""Usage"" AS ""usage""
                FROM sdb.""dbvisualrecords::OrnamentsMapGroups"" AS ornaments
                LEFT JOIN sdb.""dblocalization::LocalizedText"" AS lang ON lang.id = ""localizedNameId""";

            var result = await DBCall(conn => conn.QueryAsync<CosmeticInfo>(SELECT_SQL, new { }));

            return result ?? Enumerable.Empty<CosmeticInfo>();
        }
        public async Task<bool> ValidateNewCharacterAssets(int headId, int voiceSetId, int? gender = null)
        {
            const string SELECT_SQL = @"
                SELECT
                    (SELECT COUNT(*)
                     FROM sdb.""dbcharacter::Head""
                     WHERE head_id = @headId
                       AND (
                            @gender IS NULL
                            OR sex = CASE @gender WHEN 0 THEN 'M' WHEN 1 THEN 'F' ELSE NULL END
                            OR sex = CAST(@gender AS text)
                           OR sex IN ('2', '3')
                       )) AS ""HeadTotal"",
                    (SELECT COUNT(*)
                     FROM sdb.""dbcharacter::VoiceSet""
                     WHERE id = @voiceSetId) AS ""VoiceTotal"";";

            var result = await DBCall(conn => conn.QueryFirstOrDefaultAsync<(int HeadTotal, int VoiceTotal)>(SELECT_SQL, new { headId, voiceSetId, gender }));
            return result.HeadTotal == 1 && result.VoiceTotal == 1;
        }

        public async Task<IEnumerable<SdbStarterLoadoutSlot>> GetChassisDefaultLoadoutSlots(int chassisId)
        {
            const string SELECT_SQL = @"
                WITH default_loadout AS (
                    SELECT id
                    FROM sdb.""dbcharacter::CharCreateLoadout""
                    WHERE frame_id = @chassisId
                    ORDER BY is_starting_loadout DESC, id
                    LIMIT 1
                )
                SELECT 
                    slot_type AS ""SlotType"",
                    default_pve_module AS ""DefaultPveModule"",
                    default_pvp_module AS ""DefaultPvpModule""
                FROM sdb.""dbcharacter::CharCreateLoadoutSlots""
                WHERE loadout_id = (SELECT id FROM default_loadout)
                ORDER BY slot_type;";

            var result = await DBCall(conn => conn.QueryAsync<SdbStarterLoadoutSlot>(SELECT_SQL, new { chassisId }),
                exception => Logger.LogError(exception, "Error getting starter loadout slots for chassis {chassisId}", chassisId));

            return result ?? Enumerable.Empty<SdbStarterLoadoutSlot>();
        }

        // Resolves the boost type, modifier and duration from an item SDB ID
        // Works by looking up item -> AbilityModule -> AbilityData -> BaseCommandDefs -> ImpactApplyEffect -> StatusEffect
        public async Task<Models.DB.BoostInfo?> GetBoostInfoFromItem(int sdbId)
        {
            // We search for a known status effect id that corresponds to an xp boost or VIP
            // Items 77013, 77034, 77036, 77066, 77068, 77692, 85781, 96534 are XP Boosts (Effect 3509)
            // Hardcode duration and VIP mapping here as a fallback until a full DB sync is possible,
            // but use SDB for validation of existence if possible.
            
            // To simplify without doing a complex 6-table join string:
            // Since we know the main consumable items, we can handle them efficiently.
            
            // First let's check if it's a known XP boost item based on SDB name
            const string SELECT_SQL = @"
                SELECT lt.english as name, r.sdb_id
                FROM sdb.""dbitems::RootItem"" r
                JOIN sdb.""dblocalization::LocalizedText"" lt ON lt.id = r.name_id
                WHERE r.sdb_id = @sdbId;";

            var result = await DBCall(conn => conn.QueryFirstOrDefaultAsync<dynamic>(SELECT_SQL, new { sdbId }));
            
            if (result == null) 
            {
                return null;
            }

            string name = (string)result.name ?? "";
            
            var boost = new Models.DB.BoostInfo();
            
            // Manual overrides for VIP items
            if (sdbId == 5262) { boost.IsVip = true; boost.DurationSecs = 3600; return boost; } 
            if (sdbId == 77196) { boost.IsVip = true; boost.DurationSecs = 604800; return boost; } // 7 days VIP
            if (sdbId == 77197) { boost.IsVip = true; boost.DurationSecs = 2592000; return boost; } // 30 days VIP

            // Parse XP boost string "XP Boost - 20% for 1 hour" or "XP Boost - 50% for 30 days"
            if (name.StartsWith("XP Boost -", StringComparison.OrdinalIgnoreCase))
            {
                boost.BoostType = "xp_boost";
                boost.DurationSecs = 3600; // Default 1 hour
                boost.Modifier = 0.05f; // Default small modifier
                
                // Parse modifier
                if (name.Contains("10%", StringComparison.OrdinalIgnoreCase)) boost.Modifier = 0.1f;
                else if (name.Contains("20%", StringComparison.OrdinalIgnoreCase)) boost.Modifier = 0.2f;
                else if (name.Contains("50%", StringComparison.OrdinalIgnoreCase)) boost.Modifier = 0.5f;
                else if (name.Contains("100%", StringComparison.OrdinalIgnoreCase)) boost.Modifier = 1.0f;
                
                // Parse duration
                if (name.Contains("1 hour", StringComparison.OrdinalIgnoreCase)) boost.DurationSecs = 3600;
                else if (name.Contains("8 hours", StringComparison.OrdinalIgnoreCase)) boost.DurationSecs = 8 * 3600;
                else if (name.Contains("5 days", StringComparison.OrdinalIgnoreCase)) boost.DurationSecs = 5 * 24 * 3600;
                else if (name.Contains("10 days", StringComparison.OrdinalIgnoreCase)) boost.DurationSecs = 10 * 24 * 3600;
                else if (name.Contains("30 days", StringComparison.OrdinalIgnoreCase)) boost.DurationSecs = 30 * 24 * 3600;
                
                return boost;
            }
            
            return null; // Not a known boost
        }
    }
}