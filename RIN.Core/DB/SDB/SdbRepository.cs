using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RIN.Core.Config;
using RIN.Core.Models.DB;
using RIN.Core.Models.SDB;

namespace RIN.Core.DB.SDB
{
    public class SdbRepository : DbBase
    {
        public SdbRepository(IOptions<DbConnectionSettings> config, ILogger<SdbRepository> logger) : base(config, logger)
        {
            ConnStr    = config.Value.SDBConnStr;
            LogDbTimes = config.Value.LogDbCallTimes;
        }

        public async Task<SdbItem?> GetItemWithAbilities(int sdbId)
        {
            const string SELECT_ITEM_SQL = @"
                SELECT sdb_id AS SdbId, name_id AS NameId, description_id AS DescriptionId, quality, tier_id AS TierId
                FROM sdb.""dbitems::RootItem""
                WHERE sdb_id = @sdbId;";

            const string SELECT_ABILITIES_SQL = @"
                SELECT ability_chain_id
                FROM sdb.""dbitems::AbilityModule""
                WHERE id = @sdbId;";

            return await DBCall(async conn =>
            {
                var item = await conn.QueryFirstOrDefaultAsync<SdbItem>(SELECT_ITEM_SQL, new { sdbId });
                if (item == null) return null;

                var abilities = await conn.QueryAsync<int>(SELECT_ABILITIES_SQL, new { sdbId });
                item.AbilityIds = abilities.ToList();

                return item;
            });
        }

        public async Task<SdbAbility?> GetAbilityChain(int abilityId)
        {
            const string SELECT_ABILITY_SQL = @"
                SELECT id, chain
                FROM sdb.""apt::AbilityData""
                WHERE id = @abilityId;";

            return await DBCall(async conn =>
            {
                var abilityData = await conn.QueryFirstOrDefaultAsync<dynamic>(SELECT_ABILITY_SQL, new { abilityId });
                if (abilityData == null) return null;

                var ability = new SdbAbility { Id = (int)abilityData.id };
                int currentChainId = (int)abilityData.chain;

                // Simple traversal of the chain
                while (currentChainId != 0)
                {
                    var command = await GetCommandChain(conn, currentChainId);
                    if (command == null) break;
                    
                    // In this simple model, we just add commands to a single phase
                    // Firefall is more complex, but this covers the basic request.
                    if (ability.Phases.Count == 0) ability.Phases.Add(new SdbPhase { Id = 1, Ordinal = 1 });
                    ability.Phases[0].Commands.Add(command);

                    // Move to next in chain is handled within GetCommandChain if needed, 
                    // but here we follow apt::BaseCommandDef.next
                    var baseCmd = await conn.QueryFirstOrDefaultAsync<dynamic>(
                        @"SELECT next FROM sdb.""apt::BaseCommandDef"" WHERE id = @id", new { id = currentChainId });
                    
                    currentChainId = baseCmd?.next ?? 0;
                }

                return ability;
            });
        }

        private async Task<SdbCommand?> GetCommandChain(Npgsql.NpgsqlConnection conn, int commandId)
        {
            const string SELECT_BASE_COMMAND = @"
                SELECT id, subtype
                FROM sdb.""apt::BaseCommandDef""
                WHERE id = @commandId;";

            var baseCmd = await conn.QueryFirstOrDefaultAsync<dynamic>(SELECT_BASE_COMMAND, new { commandId });
            if (baseCmd == null) return null;

            var cmd = new SdbCommand { Id = (int)baseCmd.id, CommandType = (string)baseCmd.subtype };
            
            // Try to fetch arguments from a subtype-specific table if it exists
            // Usually aptfs::[Subtype]CommandDef
            string subtypeTable = $"aptfs::{cmd.CommandType}CommandDef";
            try {
                var args = await conn.QueryFirstOrDefaultAsync<dynamic>(
                    $@"SELECT * FROM sdb.""{subtypeTable}"" WHERE id = @id", new { id = commandId });
                
                if (args != null) {
                    var argDict = (IDictionary<string, object>)args;
                    foreach (var kvp in argDict) {
                        if (kvp.Key != "id") cmd.Arguments[kvp.Key] = kvp.Value;
                    }
                }
            } catch {
                // If table doesn't exist, just continue
            }

            return cmd;
        }

        // Resolves the boost info by traversing the chains fully
        public async Task<BoostInfo?> ResolveBoostFromItem(int sdbId)
        {
            var item = await GetItemWithAbilities(sdbId);
            if (item == null || item.AbilityIds.Count == 0) return null;

            foreach (var abilityId in item.AbilityIds)
            {
                var abilityChain = await GetAbilityChain(abilityId);
                if (abilityChain == null) continue;

                // Search for ApplyStatusEffect or StatModifier commands
                foreach (var phase in abilityChain.Phases)
                {
                    foreach (var command in phase.Commands)
                    {
                        if (command.CommandType == "ApplyClientStatusEffect")
                        {
                            // Resolve the status effect
                            if (command.Arguments.TryGetValue("status_effect_id", out var seId))
                            {
                                return await ResolveBoostFromStatusEffect((int)seId);
                            }
                        }
                    }
                }
            }

            return null;
        }

        public async Task<SdbBlueprint?> GetBlueprint(int blueprintId)
        {
            const string SELECT_BLUEPRINT_SQL = @"
                SELECT id AS Id, main_output_item_id AS MainOutputItemId, build_time_secs AS BuildTimeSecs
                FROM sdb.""dbitems::Blueprints""
                WHERE id = @blueprintId;";

            const string SELECT_BLUEPRINT_ITEMS_SQL = @"
                SELECT item_type AS ItemSdbId, rsrc_quantity AS Quantity, is_output AS IsOutput
                FROM sdb.""dbitems::Blueprint_Items""
                WHERE blueprint_id = @blueprintId;";

            return await DBCall(async conn =>
            {
                var bp = await conn.QueryFirstOrDefaultAsync<SdbBlueprint>(SELECT_BLUEPRINT_SQL, new { blueprintId });
                if (bp == null) return null;

                var items = await conn.QueryAsync<SdbBlueprintItem>(SELECT_BLUEPRINT_ITEMS_SQL, new { blueprintId });
                bp.Items = items.ToList();

                return bp;
            });
        }

        private async Task<BoostInfo?> ResolveBoostFromStatusEffect(int statusEffectId)
        {
            return await DBCall(async conn => {
                var se = await conn.QueryFirstOrDefaultAsync<dynamic>(
                    @"SELECT * FROM sdb.""apt::StatusEffectData"" WHERE id = @id", new { id = statusEffectId });
                
                if (se == null) return null;

                var boost = new BoostInfo();
                
                // Traverse the apply_chain to find modifiers
                if (se.apply_chain != null) {
                    int chainId = (int)se.apply_chain;
                    while (chainId != 0) {
                        var cmd = await GetCommandChain(conn, chainId);
                        if (cmd != null && cmd.Id == chainId && cmd.CommandType == "StatModifier") {
                            if (cmd.Arguments.TryGetValue("modifier", out var mod)) boost.Modifier = Convert.ToSingle(mod);
                            if (cmd.Arguments.TryGetValue("attribute_id", out var attrId)) {
                                int aid = Convert.ToInt32(attrId);
                                if (aid == 400) boost.BoostType = "xp_boost";
                                else if (aid == 401) boost.BoostType = "resource_boost";
                            }
                        }
                        
                        var baseCmd = await conn.QueryFirstOrDefaultAsync<dynamic>(
                            @"SELECT next FROM sdb.""apt::BaseCommandDef"" WHERE id = @id", new { id = chainId });
                        chainId = baseCmd?.next ?? 0;
                    }
                }

                return boost; 
            });
        }
    }
}
