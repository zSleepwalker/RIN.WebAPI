using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RIN.Core.DB.SDB;
using RIN.Core;
using RIN.WebAPI.Models.Config;
using RIN.WebAPI.Models.StoreApi;
using RIN.WebAPI.Utils;
using RIN.Core.DB;
using RIN.Core.Utils;

namespace RIN.WebAPI.Controllers
{
    [ApiController]
    [Route("Store")]
    public class StoreApi : TmwController
    {
        private readonly ServerDefaultsSettings ServerDefaults;
        private readonly DevServerSettings DevServerSettings;
        private readonly ILogger<OperatorController> Logger;
        private readonly DB Db;
        private readonly SDB Sdb;
        private readonly IWebHostEnvironment _env;

        public StoreApi(
            IOptions<ServerDefaultsSettings> serverDefaults,
            IOptions<DevServerSettings> devServerSettings,
            ILogger<OperatorController> logger,
            DB db,
            SDB sdb,
            IWebHostEnvironment env
        )
        {
            ServerDefaults = serverDefaults.Value;
            DevServerSettings = devServerSettings.Value;
            Logger = logger;
            Db = db;
            Sdb = sdb;
            _env = env;
        }
        
        [HttpGet("")]
        public string StoreRoot()
        {
            return "StoreRoot";
        }

        [HttpGet("products.json")]
        public IActionResult ProductsJson()
        {
            return ReturnJsonFile("products.json");
        }
        
        [HttpGet("billing/packages")]
        public object BillingPackages()
        {
            return new {};
        }
        
        [HttpGet("vip")]
        public object Vip()
        {
            return new {};
        }

        private IActionResult ReturnJsonFile(string fileName)
        {
            var path = Path.Combine(_env.ContentRootPath, "StaticData", fileName);
            if (!System.IO.File.Exists(path))
            {
                return NotFound();
            }

            var json = System.IO.File.ReadAllText(path);

            using var _ = JsonDocument.Parse(json);
            return Content(json, "application/json");
        }
    }
}