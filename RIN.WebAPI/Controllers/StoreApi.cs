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

        public StoreApi(
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
        public string StoreRoot()
        {
            return "StoreRoot";
        }

        [HttpGet("products.json")]
        public ProductsJson ProductsJson()
        {
            string jsonFile = @"E:\RIN.WebAPI\RIN.WebAPI\StaticData\products.json";
            string jsonString = System.IO.File.ReadAllText(jsonFile);
            
            if (String.IsNullOrEmpty(jsonString))
            {
                return new ProductsJson {};
            }
            else
            {
                return JsonSerializer.Deserialize<ProductsJson>(jsonString) ?? new ProductsJson();
            }
            
            // return LoadJSON<ProductsJson>("./StaticData/products.json");
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
    }
}