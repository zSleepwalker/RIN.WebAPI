using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RIN.Core.DB.SDB;
using RIN.Core;
using RIN.WebAPI.Models.ClientApi;
using RIN.WebAPI.Models.Config;
using RIN.WebAPI.Utils;
using RIN.Core.DB;
using RIN.Core.ClientApi;
using RIN.Core.Utils;

namespace RIN.WebAPI.Controllers
{
    [ApiController]
    [Route("Clientapi/api/v1")]
    public class ClientApiV1 : TmwController
    {
        private readonly ServerDefaultsSettings         ServerDefaults;
        private readonly DevServerSettings              DevServerSettings;
        private readonly ILogger<OperatorController>    Logger;
        private readonly DB                          Db;
        private readonly SDB                            Sdb;

        public ClientApiV1(
                IOptions<ServerDefaultsSettings> serverDefaults,
                IOptions<DevServerSettings> devServerSettings,
                ILogger<OperatorController> logger,
                DB db,
                SDB sdb
            )
        {
            ServerDefaults    = serverDefaults.Value;
            DevServerSettings = devServerSettings.Value;
            Logger            = logger;
            Db                = db;
            Sdb               = sdb;
        }

        // Todo: log to db?
        [HttpPost("client_event")]
        public async Task<string> ClientEvent(ClientEvent evnt)
        {
            Serilog.Log.Information("ClientEvent: {@evnt}", evnt);
            await Db.LogClientEvent(evnt);
            return "";
        }

        // Todo: read from DB
        [HttpGet("login_alerts")]
        public List<LoginAlert> LoginAlerts()
        {
            var envStr = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            var alerts = new List<LoginAlert>
            {
                new LoginAlert {message = $"RIN WebAPI {envStr}"},
                new LoginAlert {message = "Test login alert :D"}
            };

            return alerts;
        }

        [HttpPost("characters/validate_name")]
        [R5SigAuthRequired]
        public async Task<ValidateNameResp> ValidateName(ValidateNameReq nameData)
        {
            var data = new ValidateNameResp
            {
                name   = nameData.name,
                valid  = true,
                code   = "",
                reason = new List<string>()
            };

            if (nameData.name.Length > ServerDefaults.CharacterNameMaxLength) data.reason.Add(Error.Codes.ERR_NAME_TOO_LONG);
            if (nameData.name.Length < ServerDefaults.CharacterNameMinLength) data.reason.Add(Error.Codes.ERR_NAME_TOO_SHORT);

            if (data.reason.Count == 0 && Char.IsDigit(nameData.name[0]))
            {
                data.reason.Add(Error.Codes.ERR_NAME_STARTS_WITH_NUMBER);
            }

            if (data.reason.Count == 0 && CharacterUtil.IsInvalidCharactersInName(nameData.name))
            {
                data.reason.Add(Error.Codes.ERR_INVALID_CHARACTER);
            }

            if (data.reason.Count == 0 && CharacterUtil.IsReservedName(nameData.name))
            {
                data.reason.Add(Error.Codes.ERR_NAME_RESERVED);
            }

            if (data.reason.Count == 0 && CharacterUtil.IsNameProfane(nameData.name))
            {
                data.reason.Add(Error.Codes.ERR_NAME_PROFANITY);
            }

            var isfree_result = await Db.CheckIfNameIsFree(nameData.name);

            if (isfree_result == false)
            {
                data.reason.Add(Error.Codes.ERR_NAME_IN_USE);
            }

            if (data.reason.Count > 0)
            {
                data.valid = false;
                data.code  = Error.Codes.ERR_NAME_INVALID;
                return data;
            }

            return data;
        }

        // TODO: Setup a database script/system to automatically delete any characters with expire_in date reached
        [HttpPost("characters/{characterGuid}/delete")]
        [R5SigAuthRequired]
        public async Task<object> Delete(long characterGuid)
        {
            var loginResult = await Db.GetLoginData(GetUid());
            if (loginResult == null) return ReturnError(new Error(Error.Codes.ERR_INCORRECT_USERPASS), 401);

            var delete_result = await Db.SetPendingDeleteCharacterById(loginResult.account_id, characterGuid);

            if (delete_result.code == Error.Codes.SUCCESS)
            {
                return true;
            }
            else
            {
                return ReturnError(delete_result, 404);
            }
        }

        // TODO: Game is pushing Key and Namespace through QueryStrings and then returning it, why?
        // TODO: What is Value and where does it come from?
        [HttpGet("characters/{characterGuid}/data")]
        [R5SigAuthRequired]
        public CharacterDataResp Data(long characterGuid, [FromQuery] string key = "76336_0", [FromQuery] string @namespace = "bfAbiMap")
        {
            var characterData = new CharacterDataResp
            {
                Key = key,
                Namespace = @namespace,
                Value = "0,1,2,3"
            };

            return characterData;
        }

        [HttpPost("characters")]
        [R5SigAuthRequired]
        public async Task<object> Characters(CreateCharacterReq reqData)
        {
            // Validate character name
            var nameValidation = await ValidateName(new ValidateNameReq { name = reqData.name });
            if (!nameValidation.valid)
            {
                return ReturnError(new Error(Error.Codes.ERR_NAME_INVALID, string.Join(", ", nameValidation.reason)), 400);
            }

            const byte DEFAULT_RACE = 0;

            var colors      = await Sdb.GetNewCharactersColors(reqData.eye_color_id, reqData.skin_color_id, reqData.hair_color_id);
            if (colors == null) return ReturnError(new Error(Error.Codes.ERR_INVALID_CHARACTER), 400);

            var genderInt   = CharacterUtil.GenderStrToNum(reqData.gender);
            var assetsValid = await Sdb.ValidateNewCharacterAssets(reqData.head, reqData.voice_set, genderInt);
            if (!assetsValid) return ReturnError(new Error(Error.Codes.ERR_INVALID_CHARACTER), 400);

            var loginResult = await Db.GetLoginData(GetUid()); // temp
            if (loginResult == null) return ReturnError(new Error(Error.Codes.ERR_INCORRECT_USERPASS), 401);

            var visuals    = CharacterUtil.CreateVisualsObj(colors, DEFAULT_RACE, genderInt, reqData.eye_color_id, reqData.skin_color_id, reqData.hair_color_id, reqData.voice_set, reqData.head, reqData.head_accessory_a);
            var visualBlob = MiscUtils.ToProtoBuffByteArray(visuals);

            var charId    = await Db.CreateNewCharacter(loginResult.account_id, reqData.name, reqData.is_dev, reqData.voice_set, genderInt, visualBlob);

            foreach (var itemSdbId in StarterInventory.FallbackInventoryItems)
            {
                await Db.AddCharacterItem(charId, (int)itemSdbId);
            }

            foreach (var (resourceSdbId, quantity) in StarterInventory.FallbackInventoryResources)
            {
                await Db.AddOrUpdateCharacterResource(charId, (int)resourceSdbId, (int)quantity);
            }

            var starterFrames = ClientAPiV3.StarterBattleframeIds
                .Prepend(reqData.start_class_id)
                .Distinct()
                .ToArray();

            for (int index = 0; index < starterFrames.Length; index++)
            {
                bool loadoutSaved = await BattleframeLoadoutBuilder.CreateLoadoutWithDefaults(Db, Sdb, charId, index + 1, starterFrames[index]);
                if (!loadoutSaved)
                {
                    return ReturnError(new Error(Error.Codes.ERR_UNKNOWN, $"Failed to persist starter loadout for battleframe {starterFrames[index]}"), 500);
                }
            }

            bool currentFrameSet = await Db.SetCharacterCurrentBattleframeBySdbId(charId, reqData.start_class_id);
            if (!currentFrameSet)
            {
                return ReturnError(new Error(Error.Codes.ERR_UNKNOWN, "Failed to set current battleframe"), 500);
            }

            var createData = new CreateCharacterResp
            {
                created_at        = DateTime.Now,
                updated_at        = DateTime.Now,
                name              = reqData.name,
                head_accAId       = reqData.head_accessory_a,
                head_accBId       = reqData.head_accessory_b,
                head_mainId       = reqData.head,
                is_active         = true,
                is_dev            = reqData.is_dev,
                max_frame_level   = 0,
                needs_name_change = false,
                pool_id           = 0,
                race              = 0, // always human here?
                time_played_secs  = 0,
                title_id          = 0,
                unique_name       = reqData.name.ToUpper(),
                voice_setId       = reqData.voice_set,
                gender            = reqData.gender
            };
            
            return createData;
        }

        [HttpPost("oracle/ticket")]
        [R5SigAuthRequired]
        public OracleTicket? OracleTicket(OracleTicketReq req)
        {
            if (DevServerSettings.EnableLocalDev) {
                var ticket = new OracleTicket
                {
                    country           = "JP",
                    datacenter        = DevServerSettings.DevGameServerURL.Split(":").Last(),
                    hostname          = DevServerSettings.DevGameServerURL.Split(":").Last(),
                    matrix_url        = DevServerSettings.DevGameServerURL,
                    session_id        = DevServerSettings.DevSessionId,
                    ticket            = DevServerSettings.DevTicket,
                    operator_override = null!
                };

                return ticket;
            }

            // TODO: server lookup, instances, last zone and all that
            return null;
        }

        // Zone List for devs
        [HttpPost("server/list")]
        [R5SigAuthRequired]
        public object ServerList(ServerListReq req)
        {
            var zone_list = new ZoneList();
            var zone = new Zones()
            {
                match = "0",
                matrix_url = "https://localhost/test_matrix",
                zone_name = "test_zone",
                revision = "0",
                protocol_version = "0",
                owner = "root",
                players = 0
            };
            zone_list.zone_list.Add(zone);

            return zone_list;
        }

        [HttpGet("zones/queue_ids")]
        public object ZoneQueueIds()
        {
            return new { };
        }
    }
}