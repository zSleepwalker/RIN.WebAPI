using Dapper;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RIN.Core.DB
{
    public partial class DB
    {
                public async Task<long?> GetBattleframeId(long characterId, int battleframeSdbId)
                {
                        const string SELECT_SQL = @"SELECT id
                                                                                FROM webapi.""Battleframes""
                                                                                WHERE character_guid = @characterId
                                                                                    AND battleframe_sdb_id = @battleframeSdbId
                                                                                ORDER BY id
                                                                                LIMIT 1;";

                        return await DBCall(conn => conn.QueryFirstOrDefaultAsync<long?>(SELECT_SQL, new { characterId, battleframeSdbId }));
                }

        public async ValueTask<long?> CreateBattleframeLoadout(long characterId, int battleframeSdId, PlayerBattleframeVisuals visuals)
        {
            const string INSERT_SQL = @"INSERT INTO webapi.""Battleframes""(
	                        character_guid, battleframe_sdb_id, visuals, hidden, level, xp, id)
	                        VALUES (@characterId, @battleframeSdId, @visuals, false, 1, 0, webapi.create_entity_guid(253))
                            RETURNING id;";

            byte[] visualsData = Utils.MiscUtils.ToProtoBuffByteArray(visuals);
            var result = await DBCall(conn => conn.QueryAsync<long>(INSERT_SQL, new { characterId, battleframeSdId, visuals = visualsData }),
                exception =>
                {
                    Serilog.Log.Error(exception, "Error creating a battleframe loadout ({battleframeSdId}) for {characterId} due to: {exception}", characterId, battleframeSdId, exception);
                    throw exception;
                });

            return result?.Single() ?? -1;
        }

        public async Task<long?> EnsureBattleframeRecord(long characterId, int battleframeSdbId, PlayerBattleframeVisuals? visuals = null)
        {
            var existingId = await GetBattleframeId(characterId, battleframeSdbId);
            if (existingId.HasValue && existingId.Value > 0)
            {
                return existingId.Value;
            }

            return await CreateBattleframeLoadout(characterId, battleframeSdbId, visuals ?? PlayerBattleframeVisuals.CreateDefault());
        }

        public async Task<bool> EnsureBattleframeRecords(long characterId, IEnumerable<int> battleframeSdbIds)
        {
            bool changed = false;

            foreach (var battleframeSdbId in battleframeSdbIds.Where(id => id > 0).Distinct())
            {
                var existingId = await GetBattleframeId(characterId, battleframeSdbId);
                if (existingId.HasValue && existingId.Value > 0)
                {
                    continue;
                }

                var createdId = await CreateBattleframeLoadout(characterId, battleframeSdbId, PlayerBattleframeVisuals.CreateDefault());
                if (createdId.HasValue && createdId.Value > 0)
                {
                    changed = true;
                }
            }

            return changed;
        }

        public async Task<bool> SetCharacterCurrentBattleframeBySdbId(long charId, int battleframeSdbId)
        {
            var battleframeId = await EnsureBattleframeRecord(charId, battleframeSdbId);
            if (!battleframeId.HasValue || battleframeId.Value <= 0)
            {
                return false;
            }

            return await SetCharacterCurrentBattleframe(charId, battleframeId.Value);
        }

        public async Task<bool> UpdateBattleframeVisuals(long battleframeId, PlayerBattleframeVisuals visuals)
        {
            const string UPDATE_SQL = @"UPDATE webapi.""Battleframes""
	                            SET visuals = @visualsBlob
	                            WHERE id = @battleframeId;";

            var visualsBlob = Utils.MiscUtils.ToProtoBuffByteArray(visuals);
            var result = await DBCall(conn => conn.ExecuteAsync(UPDATE_SQL, new { battleframeId, visualsBlob }));

            return result > 0;
        }

        public async Task<PlayerBattleframeVisuals?> GetCurrentBattleframeVisuals(long characterId)
        {
            const string SELECT_SQL = @"SELECT bf.visuals
                                        FROM webapi.""Characters"" c
                                        LEFT JOIN webapi.""Battleframes"" bf ON bf.id = c.current_battleframe_guid
                                        WHERE c.character_guid = @characterId";

            var visualsBlob = await DBCall(conn => conn.QueryFirstOrDefaultAsync<byte[]>(SELECT_SQL, new { characterId }));
            if (visualsBlob == null || visualsBlob.Length == 0)
            {
                return null;
            }

            try
            {
                return Utils.MiscUtils.FromProtoBuffByteArray<PlayerBattleframeVisuals>(visualsBlob.AsSpan());
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to deserialize battleframe visuals for character {characterId}", characterId);
                return null;
            }
        }

    }
}
