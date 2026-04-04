using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using RIN.WebAPI.Utils;
using System.Linq;
using System;

namespace RIN.WebAPI.Controllers
{
    public partial class ClientApiV2
    {
        [HttpGet("zone_settings")]
        [R5SigAuthRequired]
        public async Task<IActionResult> ZoneSettings()
        {
            var response = await Db.GetZoneSettings();

            var options = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            };

            return new JsonResult(response, options);
        }

        [HttpGet("zone_settings/zone/{zoneId}")]
        [R5SigAuthRequired]
        public async Task<IActionResult> ZoneSettingsByZone(string zoneId)
        {
            return await FilterZoneSettings("zone_id", zoneId);
        }

        [HttpGet("zone_settings/context/{context}")]
        [R5SigAuthRequired]
        public async Task<IActionResult> ZoneSettingsByContext(string context)
        {
            return await FilterZoneSettings("context", context);
        }

        [HttpGet("zone_settings/gametype/{gametype}")]
        [R5SigAuthRequired]
        public async Task<IActionResult> ZoneSettingsByGametype(string gametype)
        {
            return await FilterZoneSettings("gametype", gametype);
        }

        private async Task<IActionResult> FilterZoneSettings(string key, string expected)
        {
            var response = await Db.GetZoneSettings();
            var root = JsonSerializer.SerializeToElement(response);

            if (root.ValueKind != JsonValueKind.Array)
            {
                return new JsonResult(Array.Empty<object>());
            }

            var filtered = root.EnumerateArray()
                .Where(item => Matches(item, key, expected))
                .ToList();

            return new JsonResult(filtered);
        }

        private static bool Matches(JsonElement item, string key, string expected)
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var property in item.EnumerateObject())
            {
                if (!property.Name.Equals(key, StringComparison.OrdinalIgnoreCase) &&
                    !property.Name.Replace("_", string.Empty).Equals(key.Replace("_", string.Empty), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number => property.Value.GetRawText(),
                    _ => null,
                };

                return !string.IsNullOrEmpty(value) &&
                       value.Equals(expected, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }
    }
}
