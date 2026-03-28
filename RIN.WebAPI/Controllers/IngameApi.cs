using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RIN.Core.DB.SDB;
using RIN.Core;
using RIN.WebAPI.Models.Config;
using RIN.WebAPI.Utils;
using RIN.Core.DB;
using RIN.Core.Utils;

namespace RIN.WebAPI.Controllers
{
    [ApiController]
    [Route("Ingame")]
    public class IngameApi : TmwController
    {
        private readonly ServerDefaultsSettings ServerDefaults;
        private readonly DevServerSettings DevServerSettings;
        private readonly ILogger<OperatorController> Logger;
        private readonly DB Db;
        private readonly SDB Sdb;

        public IngameApi(
            IOptions<ServerDefaultsSettings> serverDefaults,
            IOptions<DevServerSettings> devServerSettings,
            ILogger<OperatorController> logger,
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
        
        [HttpGet("")]
        public OkResult IngameRoot()
        {
            return Ok();
        }
        
        [HttpGet("panelmanager")]
        public object PanelManager()
        {
            return new {};
        }
    }
}