using Microsoft.AspNetCore.Mvc;
using RIN.Core;
using RIN.WebAPI.Utils;
using System.Text.Json;

namespace RIN.WebAPI.Controllers
{
    public partial class ClientAPiV3
    {
        [HttpPost("characters/{characterGuid}/garage_slots/purchase_battleframe")]
        [R5SigAuthRequired]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<object> PurchaseBattleframe(long characterGuid, [FromBody] JsonElement? body = null)
        {
            if (characterGuid != GetCid())
            {
                return ReturnError(Error.Codes.ERR_UNKNOWN, "Unauthorized", StatusCodes.Status403Forbidden);
            }

            var frameSdbId = ParsePositiveInt(body, "sdb_id", "frame_id", "battleframe_sdb_id");
            if (frameSdbId <= 0)
            {
                return ReturnError(Error.Codes.ERR_UNKNOWN, "Invalid battleframe id", StatusCodes.Status400BadRequest);
            }

            var loadouts = (await Db.GetCharacterLoadouts(characterGuid)).ToList();
            if (loadouts.Any(l => l.ChassisSdbId == frameSdbId))
            {
                await Db.EnsureBattleframeRecord(characterGuid, frameSdbId);
                return ReturnError(Error.Codes.ERR_UNKNOWN, "Battleframe already owned", StatusCodes.Status400BadRequest);
            }

            int nextLoadoutId = loadouts.Count == 0 ? 1 : loadouts.Max(l => l.LoadoutId) + 1;
            bool saved = await BattleframeLoadoutBuilder.CreateLoadoutWithDefaults(Db, SDB, characterGuid, nextLoadoutId, frameSdbId);
            if (!saved)
            {
                return ReturnError(Error.Codes.ERR_UNKNOWN, "Failed to create battleframe loadout", StatusCodes.Status400BadRequest);
            }

            await Db.SetCharacterCurrentBattleframeBySdbId(characterGuid, frameSdbId);

            return Content("{}", "application/json");
        }

        [HttpPost("characters/{characterGuid}/garage_slots/unlock_slot")]
        [R5SigAuthRequired]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public object UnlockGarageSlot(long characterGuid, [FromBody] JsonElement? body = null)
        {
            if (characterGuid != GetCid())
            {
                return ReturnError(Error.Codes.ERR_UNKNOWN, "Unauthorized", StatusCodes.Status403Forbidden);
            }

            Serilog.Log.Information("UnlockGarageSlot: characterGuid={characterGuid}, payload={payload}", characterGuid, body?.GetRawText() ?? "{}");
            return Content("{}", "application/json");
        }

        private static int ParsePositiveInt(JsonElement? body, params string[] keys)
        {
            if (body == null || body.Value.ValueKind != JsonValueKind.Object)
            {
                return 0;
            }

            foreach (var key in keys)
            {
                if (!body.Value.TryGetProperty(key, out var value))
                {
                    continue;
                }

                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numeric) && numeric > 0)
                {
                    return numeric;
                }

                if (value.ValueKind == JsonValueKind.String
                    && int.TryParse(value.GetString(), out var textNumeric)
                    && textNumeric > 0)
                {
                    return textNumeric;
                }
            }

            return 0;
        }

        [HttpGet("characters/{characterGuid}/army_applications")]
        [R5SigAuthRequired]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<object> GetPersonalArmyApplications(long characterGuid)
        {
            if (characterGuid != GetCid())
            {
                return ReturnError(
                    Error.Codes.ERR_UNKNOWN,
                    "You can only view your own army applications.",
                    StatusCodes.Status403Forbidden
                );
            }

            return await Db.GetPersonalArmyApplications(characterGuid);
        }

        [HttpGet("characters/{characterGuid}/army_invites")]
        [R5SigAuthRequired]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<object> GetPersonalArmyInvites(long characterGuid)
        {
            if (characterGuid != GetCid())
            {
                return ReturnError(
                    Error.Codes.ERR_UNKNOWN,
                    "You can only view your own army invites.",
                    StatusCodes.Status403Forbidden
                );
            }

            return await Db.GetPersonalArmyInvites(characterGuid);
        }

        [HttpPost("characters/{characterGuid}/garage_slots/{garageSlotId}/perks")]
        [R5SigAuthRequired]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public object SetGarageSlotPerks(long characterGuid, int garageSlotId, [FromBody] object body)
        {
            // TODO: persist perk selections to the database
            Serilog.Log.Information("SetGarageSlotPerks: characterGuid={characterGuid}, garageSlotId={garageSlotId}, body={@body}", characterGuid, garageSlotId, body);
            return Content("", "application/json");
        }

        [HttpPost("characters/{characterGuid}/leaderboards/{leaderboardId}")]
        [R5SigAuthRequired]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<object> GetLeaderboardResult(
            long characterGuid,
            int leaderboardId,
            [FromQuery] bool army_list = false,
            [FromQuery] bool friends_list = false
        )
        {
            return await Db.GetLeaderboardResult(leaderboardId, GetCid());
        }

        [HttpPost("characters/{characterGuid}/consume/{sdbId}")]
        [R5SigAuthRequired]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<object> ConsumeItem(long characterGuid, int sdbId, [FromQuery] int count = 1)
        {
            if (characterGuid != GetCid())
            {
                return ReturnError(Error.Codes.ERR_UNKNOWN, "Unauthorized", StatusCodes.Status403Forbidden);
            }

            // 1. Resolve effect via SDB
            var boostInfo = await SdbRepo.ResolveBoostFromItem(sdbId);
            
            if (boostInfo == null)
            {
                // Fallback for known unmapped VIPs if SDB lookup fails
                if (sdbId == 5262) boostInfo = new RIN.Core.Models.DB.BoostInfo { IsVip = true, DurationSecs = 3600 };
                else if (sdbId == 77196) boostInfo = new RIN.Core.Models.DB.BoostInfo { IsVip = true, DurationSecs = 604800 };
                else if (sdbId == 77197) boostInfo = new RIN.Core.Models.DB.BoostInfo { IsVip = true, DurationSecs = 2592000 };
                else return ReturnError(Error.Codes.ERR_UNKNOWN, "Item effect not implemented or found", StatusCodes.Status400BadRequest);
            }

            // 2. Apply effect
            if (boostInfo.IsVip)
            {
                var accountId = await GetAid();
                await Db.AddOrExtendVip(accountId, boostInfo.DurationSecs);
            }
            else if (!string.IsNullOrEmpty(boostInfo.BoostType))
            {
                await Db.AddOrExtendBoost(characterGuid, boostInfo.BoostType, boostInfo.Modifier, boostInfo.DurationSecs);
            }
            else
            {
                return ReturnError(Error.Codes.ERR_UNKNOWN, "Item has no valid boost effect", StatusCodes.Status400BadRequest);
            }

            // 3. Consume item
            var success = await Db.ConsumeCharacterItem(characterGuid, sdbId, count);
            if (!success)
            {
                return ReturnError(Error.Codes.ERR_UNKNOWN, "Failed to consume item (insufficient quantity?)", StatusCodes.Status400BadRequest);
            }

            return Ok();
        }

        // TODO: Implement
        [HttpGet("characters/{characterGuid}/garage_slots/{loadoutId}/perks")]
        [R5SigAuthRequired]
        public Task<object> GetPerkRespecs(long characterGuid, int loadoutId)
        {
            var data = "{ respecs: 0 }";

            return Task.FromResult<object>(Content(data, "application/json"));
        }
    }
}
