using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RIN.Core.DB.SDB;
using RIN.Core;
using RIN.WebAPI.Models.Config;
using RIN.WebAPI.Utils;
using RIN.Core.DB;
using RIN.Core.Utils;
using System.Threading.Tasks;
using System;
using Microsoft.Extensions.Logging;

namespace RIN.WebAPI.Controllers
{
    [ApiController]
    public class IngameApi : TmwController
    {
        private readonly ServerDefaultsSettings ServerDefaults;
        private readonly DevServerSettings DevServerSettings;
        private readonly ILogger<IngameApi> Logger;
        private readonly DB Db;
        private readonly SDB Sdb;

        public IngameApi(
            IOptions<ServerDefaultsSettings> serverDefaults,
            IOptions<DevServerSettings> devServerSettings,
            ILogger<IngameApi> logger,
            DB db,
            SDB sdb
        )
        {
            ServerDefaults = serverDefaults.Value;
            DevServerSettings = devServerSettings.Value;
            Logger = logger;
            Db = db;
            Sdb = sdb;
        }

        [HttpGet("character/data")]
        [HttpGet("api/v1/character/data")]
        [HttpGet("ingame/character/data")]
        [HttpGet("ingame/api/v1/character/data")]
        [R5SigAuthRequired]
        public async Task<object> CharacterData()
        {
            var cid = GetCid();
            var result = await Db.GetBasicCharacterAndVisualData(cid);
            if (result.info == null) return NotFound();

            var uid = GetUid();
            var login = await Db.GetLoginData(uid);
            var mtx = login == null ? null : await Db.GetAccountMTXData(login.account_id);

            return new
            {
                CharacterGuid = (ulong)cid,
                Name = result.info.Name,
                Redbux = mtx?.rb_balance ?? 0,
                Crystite = 0, // TODO
                Gender = result.info.Gender,
                UniqueName = result.info.Name.ToUpper(),
                Race = result.info.Race,
                level = result.info.Level,
                effective_level = result.info.EffectiveLevel,
                elite_level = result.info.EliteLevel
            };
        }

        [HttpGet("panelmanager")]
        [HttpGet("ingame/panelmanager")]
        public IActionResult PanelManager()
        {
            const string html = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>InGame</title></head><body></body></html>";
            return Content(html, "text/html");
        }

        [HttpPost("api/v1/abuse_reports")]
        [HttpPost("ingame/api/v1/abuse_reports")]
        [R5SigAuthRequired]
        public IActionResult AbuseReports()
        {
            return Ok(new { status = "queued" });
        }

        [HttpGet("api/v1/social/friend_list")]
        [HttpGet("ingame/api/v1/social/friend_list")]
        [R5SigAuthRequired]
        public IActionResult FriendList()
        {
            return Ok(Array.Empty<object>());
        }
    }
}