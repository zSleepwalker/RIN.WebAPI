using Microsoft.AspNetCore.Mvc;
using RIN.Core;
using RIN.WebAPI.Utils;

namespace RIN.WebAPI.Controllers
{
    public partial class ClientAPiV3
    {
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
            Logger.LogInformation("SetGarageSlotPerks: characterGuid={characterGuid}, garageSlotId={garageSlotId}, body={@body}", characterGuid, garageSlotId, body);
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
    }
}
