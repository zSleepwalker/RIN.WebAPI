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
        [R5SigAuthRequired]
        public async Task<IActionResult> StoreRoot()
        {
            var vipSeconds = await ResolveVipSecondsRemaining();
            var html = BuildSimpleStoreHtml("Store", $"VIP time remaining: {vipSeconds} seconds", "/store/vip", "/store/billing/packages");
            return Content(html, "text/html");
        }

        [HttpGet("products.json")]
        [R5SigAuthRequired]
        public async Task<IActionResult> ProductsJson()
        {
            var path = Path.Combine(_env.ContentRootPath, "StaticData", "products.json");
            if (!System.IO.File.Exists(path))
            {
                return NotFound();
            }

            var json = await System.IO.File.ReadAllTextAsync(path);
            var model = JsonSerializer.Deserialize<ProductsJson>(json);
            if (model == null)
            {
                return NotFound();
            }

            model.vip_time_remaining = await ResolveVipSecondsRemaining();
            return Ok(model);
        }
        
        [HttpGet("billing/packages")]
        [R5SigAuthRequired]
        public IActionResult BillingPackages()
        {
            // Minimal in-game billing page until external payment providers are integrated.
            var html = BuildSimpleStoreHtml(
                "Billing",
                "Billing providers are not integrated in this environment yet. Red Beans can currently be awarded by server tooling/events.",
                "/store/",
                "/store/vip");

            return Content(html, "text/html");
        }
        
        [HttpGet("vip")]
        [R5SigAuthRequired]
        public async Task<IActionResult> Vip()
        {
            var vipSeconds = await ResolveVipSecondsRemaining();
            var status = vipSeconds > 0
                ? $"VIP active. Time remaining: {vipSeconds} seconds."
                : "VIP inactive.";

            var html = BuildSimpleStoreHtml("VIP", status, "/store/", "/store/billing/packages");
            return Content(html, "text/html");
        }

        private async Task<int> ResolveVipSecondsRemaining()
        {
            var uid = GetUid();
            if (string.IsNullOrWhiteSpace(uid))
            {
                return 0;
            }

            var loginData = await Db.GetLoginData(uid);
            if (loginData == null || loginData.vip_expiration <= 0)
            {
                return 0;
            }

            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var remainingMs = Math.Max(0, loginData.vip_expiration - nowMs);
            return (int)(remainingMs / 1000);
        }

        private static string BuildSimpleStoreHtml(string title, string message, string firstLink, string secondLink)
        {
            return $@"<!doctype html>
<html>
  <head>
    <meta charset='utf-8' />
    <title>{title}</title>
    <style>
      body {{ font-family: Segoe UI, Arial, sans-serif; margin: 24px; color: #e6e6e6; background: #121417; }}
      a {{ color: #6dd6ff; text-decoration: none; }}
      a:hover {{ text-decoration: underline; }}
      .card {{ background: #1b1f25; border: 1px solid #2a313b; border-radius: 8px; padding: 16px; max-width: 720px; }}
      .links {{ margin-top: 10px; display: flex; gap: 16px; }}
    </style>
  </head>
  <body>
    <div class='card'>
      <h2>{title}</h2>
      <p>{message}</p>
      <div class='links'>
        <a href='{firstLink}'>Open</a>
        <a href='{secondLink}'>Open</a>
      </div>
    </div>
  </body>
</html>";
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