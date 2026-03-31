using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RIN.Core.DB.SDB;
using RIN.Core;
using RIN.WebAPI.Models.Config;
using RIN.WebAPI.Utils;
using RIN.Core.DB;
using RIN.Core.Utils;
using System.Threading.Tasks;

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
        [R5SigAuthRequired]
        public async Task<object> CharacterData()
        {
            var cid = GetCid();
            var result = await Db.GetBasicCharacterAndVisualData(cid);
            if (result.info == null) return NotFound();

            var mtx = await Db.GetAccountMTXData(long.Parse(GetUid()));

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

        [HttpGet("api/v1/character_sheet.json")]
        [R5SigAuthRequired]
        public async Task<object> CharacterSheet()
        {
            var cid = GetCid();
            var result = await Db.GetBasicCharacterAndVisualData(cid);
            if (result.info == null) return NotFound();

            return new
            {
                Battleframe = new
                {
                    ItemSdbId = result.info.CurrentBattleframeSDBId,
                    Name = "Battleframe", // TODO: Get name from SDB
                    WebIcon = "frame",
                    Constraints = new
                    {
                        Mass = new { Level = new { Total = 10, Current = result.info.Level }, Value = new { Total = 1000, Current = 0 } },
                        Power = new { Level = new { Total = 10, Current = result.info.Level }, Value = new { Total = 1000, Current = 0 } },
                        Cpu = new { Level = new { Total = 10, Current = result.info.Level }, Value = new { Total = 1000, Current = 0 } }
                    },
                    Xp = new
                    {
                        CurrentXp = result.info.Xp,
                        LifetimeXp = result.info.Xp
                    }
                }
            };
        }

        [HttpGet("panelmanager")]
        public object PanelManager()
        {
            return new { };
        }
    }
}