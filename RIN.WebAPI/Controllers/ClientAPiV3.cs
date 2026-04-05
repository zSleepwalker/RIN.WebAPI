using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;
using RIN.Core;
using RIN.Core.Common;
using RIN.Core.DB;
using RIN.Core.DB.SDB;
using RIN.Core.Models.ClientApi;
using RIN.WebAPI.Models.ClientApi;
using RIN.WebAPI.Models.Config;
using RIN.WebAPI.Utils;

namespace RIN.WebAPI.Controllers
{
    [ApiController]
    [Route("Clientapi/api/v3")]
    [Produces("application/json")]
    [ProducesErrorResponseType(typeof(Error))]
    public partial class ClientAPiV3 : TmwController
    {
        internal static readonly int[] StarterBattleframeIds =
        [
            76164, // Assault
            75774, // Biotech
            75772, // Dreadnaught
            75775, // Engineer
            75773, // Recon
        ];

        // Keep the terminal sale list constrained to known playable chassis IDs.
        // The Lua terminal crashes when fed unknown/unsupported frame archetypes.
        private static readonly HashSet<int> KnownBattleframeSaleIds =
        [
            ..StarterBattleframeIds,

            // Advanced
            76133, // Firecat
            76132, // Tigerclaw
            76335, // Dragonfly
            76336, // Recluse
            76331, // Mammoth
            76332, // Rhino
            76337, // Electron
            76338, // Bastion
            76333, // Nighthawk
            76334, // Raptor

            // Advanced 2
            82359, // Graviton
            82360, // Arsenal
            82394, // Archangel
        ];

        private readonly ServerDefaultsSettings ServerDefaults;
        private readonly ILogger<OperatorController> Logger;
        private readonly DB Db;
        private readonly SDB SDB;
        private readonly SdbRepository SdbRepo;

        public ClientAPiV3(IOptions<ServerDefaultsSettings> serverDefaults, ILogger<OperatorController> logger, DB db, SDB sdb, SdbRepository sdbRepo)
        {
            ServerDefaults = serverDefaults.Value;
            Logger = logger;
            Db = db;
            SDB = sdb;
            SdbRepo = sdbRepo;
        }

        // TODO: log to db?
        [HttpPost("ui_actions")]
        public string ClientEvent([FromBody] JsonElement? action = null)
        {
            if (action is null || action.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            {
                Serilog.Log.Information("UiAction: empty payload");
                return "";
            }

            if (TryParseUiAction(action.Value, out var uiAction))
            {
                Serilog.Log.Information("UiAction: screen={screen}, action={actionName}, screen_reference_id={screenReferenceId}",
                    uiAction!.screen,
                    uiAction.action,
                    uiAction.screen_reference_id);
                return "";
            }

            Serilog.Log.Information("UiAction: unrecognized payload {action}", action.Value.GetRawText());
            return "";
        }

        private static bool TryParseUiAction(JsonElement payload, out UiActions? uiAction)
        {
            uiAction = null;

            if (payload.ValueKind == JsonValueKind.Object)
            {
                uiAction = payload.Deserialize<UiActions>();
                return uiAction != null;
            }

            if (payload.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in payload.EnumerateArray())
                {
                    if (element.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    uiAction = element.Deserialize<UiActions>();
                    return uiAction != null;
                }
            }

            return false;
        }

        [HttpGet("characters/{characterGuid}/garage_slots")]
        [R5SigAuthRequired]
        public List<GarageSlot> GarageSlots(long characterGuid)
        {
            var slots = new List<GarageSlot>()
            {
                new GarageSlot()
                {
                    id                = 0,
                    name              = "Crafting Station",
                    character_guid    = characterGuid,
                    garage_type       =  "crafting_station",
                    item_guid         = 0,
                    equipped_slots    = [],
                    limits            = new SlotLimits() { abilities = 4 },
                    decals            = new List<Decal>(),
                    visual_loadout_id = 0,
                    warpaint_id       = 0,
                    warpaintpatterns  = new List<WarpaintPattern>(),
                    visual_overrides  = new List<VisualOverride>(),
                    unlocked          = true,
                    expires_in_secs   = 0
                }
            };

            return slots;
        }

        [HttpPost("trade/products")]
        [R5SigAuthRequired]
        public async Task<List<TradeItem>> TradeProducts([FromBody] JsonElement? requestBody = null)
        {
            var items = new List<TradeItem>();
            var requestedTypes = ParseRequestedTypes(requestBody);
            if (requestedTypes.Count == 0)
            {
                requestedTypes.Add("ornaments");
            }

            if (requestedTypes.Contains("ornaments"))
            {
                var cosmeticsInfos = await SDB.GetOrnamentsInfoList();
                foreach (var info in cosmeticsInfos)
                {
                    items.Add(CreateFreeTradeItem(info.id, "ornaments", info.lang_name ?? string.Empty));
                }
            }

            if (requestedTypes.Contains("head"))
            {
                foreach (var id in await SDB.GetHeadIds())
                {
                    items.Add(CreateFreeTradeItem(id, "head"));
                }
            }

            if (requestedTypes.Contains("head_accessory"))
            {
                foreach (var id in await SDB.GetHeadAccessoryIds())
                {
                    items.Add(CreateFreeTradeItem(id, "head_accessory"));
                }
            }

            if (requestedTypes.Contains("warpaints"))
            {
                var equippedBattleframe = ParseEquippedBattleframe(requestBody);
                var warpaintInfos = await SDB.GetWarpaintPaletteInfoList(equippedBattleframe);
                foreach (var info in warpaintInfos)
                {
                    items.Add(CreateFreeTradeItem(info.id, "warpaints", info.lang_name ?? string.Empty));
                }
            }

            if (requestedTypes.Contains("czi_patterns"))
            {
                var patternInfos = await SDB.GetCziPatternInfoList();
                foreach (var info in patternInfos)
                {
                    items.Add(CreateFreeTradeItem(info.id, "czi_patterns", info.lang_name ?? string.Empty));
                }
            }

            if (requestedTypes.Contains("decals"))
            {
                var decalInfos = await SDB.GetDecalInfoList();
                foreach (var info in decalInfos)
                {
                    items.Add(CreateFreeTradeItem(info.id, "decals", info.lang_name ?? string.Empty));
                }
            }

            if (requestedTypes.Contains("visual_overrides"))
            {
                foreach (var id in await SDB.GetVisualOverrideIds())
                {
                    items.Add(CreateFreeTradeItem(id, "visual_overrides"));
                }
            }

            return items;
        }

        [HttpGet("garage_slots/battleframes_for_sale")]
        [R5SigAuthRequired]
        public async Task<Dictionary<string, BattleframeSaleEntry>> BattleframesForSale()
        {
            var frameIds = await SDB.GetBattleframesForSaleIds();
            return frameIds
                .Where(id => id > 0)
                .Where(id => KnownBattleframeSaleIds.Contains(id))
                .Distinct()
                .ToDictionary(
                    id => id.ToString(),
                    id => CreateBattleframeSaleEntry(id));
        }

        private static BattleframeSaleEntry CreateBattleframeSaleEntry(int frameId)
        {
            bool isStarter = StarterBattleframeIds.Contains(frameId);
            return new BattleframeSaleEntry
            {
                unlocked = isStarter,
                redbean = isStarter ? 0 : 100,
                battleframe_token = isStarter ? 0 : 10,
            };
        }

        private static HashSet<string> ParseRequestedTypes(JsonElement? body)
        {
            var requested = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            if (body == null || body.Value.ValueKind != JsonValueKind.Object)
            {
                return requested;
            }

            if (!body.Value.TryGetProperty("types", out var typesElement))
            {
                return requested;
            }

            if (typesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in typesElement.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var value = item.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            requested.Add(value);
                        }
                    }
                }
            }
            else if (typesElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in typesElement.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.True)
                    {
                        requested.Add(prop.Name);
                    }
                }
            }

            return requested;
        }

        private static string? ParseEquippedBattleframe(JsonElement? body)
        {
            if (body == null || body.Value.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            static string? GetStringProperty(JsonElement element, params string[] propertyNames)
            {
                foreach (var propertyName in propertyNames)
                {
                    if (element.TryGetProperty(propertyName, out var propertyValue)
                        && propertyValue.ValueKind == JsonValueKind.String)
                    {
                        var value = propertyValue.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            return value;
                        }
                    }
                }

                return null;
            }

            var rootValue = GetStringProperty(
                body.Value,
                "battleframe",
                "battleframe_name",
                "frame",
                "frame_name",
                "chassis",
                "chassis_name",
                "equipped_battleframe");
            if (!string.IsNullOrWhiteSpace(rootValue))
            {
                return rootValue;
            }

            if (body.Value.TryGetProperty("garage_slot", out var garageSlot)
                && garageSlot.ValueKind == JsonValueKind.Object)
            {
                return GetStringProperty(
                    garageSlot,
                    "battleframe",
                    "battleframe_name",
                    "frame",
                    "frame_name",
                    "chassis",
                    "chassis_name");
            }

            return null;
        }

        private static TradeItem CreateFreeTradeItem(int id, string remoteType, string? name = null)
        {
            return new TradeItem
            {
                duration = 0,
                id = id,
                remote_id = id,
                name = name ?? string.Empty,
                quanity = 1,
                remote_type = remoteType,
                prices = new[]
                {
                    new TradePrice
                    {
                        amount = 0,
                        currency_remote_id = 0,
                        currency_type = "redbean",
                        id = 171201,
                    },
                },
                unlock_context = "account",
            };
        }

        public sealed class BattleframeSaleEntry
        {
            public bool unlocked { get; set; } = true;
            public int redbean { get; set; } = 0;
            public int battleframe_token { get; set; } = 0;
        }

        [HttpGet("trade/products/garage_slot_perk_respec")]
        [R5SigAuthRequired]
        public object GarageSlotPerkRespec()
        {
            var data = "";

            return Content(data, "application/json");
        }

        [HttpGet("leaderboards/{leaderboardId}")]
        [R5SigAuthRequired]
        public async Task<Leaderboard?> GetLeaderboard(int leaderboardId, [FromQuery] int page = 1)
        {
            return await Db.GetLeaderboard(leaderboardId, page);
        }

        // TODO: Implement
        [HttpGet("trade/products/inventory_expansion")]
        [R5SigAuthRequired]
        public Task<object> InventoryExpansion()
        {
            var data = "[]";

            return Task.FromResult<object>(Content(data, "application/json"));
        }

        // TODO: Implement
        [HttpGet("squad_builder/lfp")]
        [R5SigAuthRequired]
        public Task<object> LookingForPeople()
        {
            var data = "{ total_count: 0, results: [] }";

            return Task.FromResult<object>(Content(data, "application/json"));
        }

        protected async Task<long> GetAid()
        {
            var uid = GetUid();
            var loginResult = await Db.GetLoginData(uid);
            return loginResult?.account_id ?? 0;
        }
    }
}
