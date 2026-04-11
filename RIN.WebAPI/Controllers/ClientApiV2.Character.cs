using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.AspNetCore.Mvc;
using RIN.WebAPI.Models;
using RIN.WebAPI.Models.ClientApi;
using RIN.Core.Common;
using RIN.WebAPI.Utils;
using RIN.Core.ClientApi;
using RIN.Core;
using RIN.Core.DB;
using Microsoft.Net.Http.Headers;
using RIN.Core.Models;
using System.Data;
using static RIN.Core.ClientApi.ClientEvent;
using RIN.Core.DB.SDB;
using RIN.Core.Utils;
using RIN.Core.Models.ClientApi;
using RIN.WebAPI.Models.StoreApi;
using System.Linq;
using System.Text.Json;

namespace RIN.WebAPI.Controllers
{
    public partial class ClientApiV2
    {
        [HttpGet("characters/list")]
        [R5SigAuthRequired]
        public async Task<ActionResult<CharacterListResp>> ListCharacters()
        {
            var resp = new CharacterListResp
            {
                is_dev           = false,
                rb_balance       = 0,
                name_change_cost = 0,
            };

            var loginResult = await Db.GetLoginData(GetUid());
            if (loginResult == null) return Unauthorized();

            resp.is_dev        = loginResult.is_dev;
            resp.is_vip        = loginResult.is_vip;
            resp.vip_expiration = loginResult.vip_expiration;
            resp.characters    = await Db.GetCharactersForAccount(loginResult.account_id);

            var accountMTX        = await Db.GetAccountMTXData(loginResult.account_id);
            if (accountMTX != null) {
                resp.rb_balance       = accountMTX.rb_balance;
                resp.name_change_cost = accountMTX.name_change_cost;
            }

            return resp;
        }

        [HttpPost("characters/{characterGuid}/undelete")]
        [R5SigAuthRequired]
        public async Task<object> Undelete(long characterGuid)
        {
            var loginResult = await Db.GetLoginData(GetUid());
            if (loginResult == null) return Unauthorized();

            var restore_result = await Db.UndeleteCharacterById(loginResult.account_id, characterGuid);

            if (restore_result.code == Error.Codes.SUCCESS)
            {
                return true;
            }
            else
            {
                return ReturnError(restore_result, 404);
            }
        }

        [HttpGet("characters/{characterGuid}/visual_loadouts")]
        [R5SigAuthRequired]
        public async Task<ActionResult<List<PlayerVisualLoadout>>> VisualLoadouts(long characterGuid)
        {
            var playerLoadout = await Db.GetBasicCharacterAndVisualData(characterGuid);
            if (playerLoadout.info == null) return NotFound();

            var dbLoadouts = (await Db.GetCharacterLoadouts(characterGuid)).OrderBy(l => l.LoadoutId).ToList();
            var visualsByChassis = await Db.GetBattleframeVisualsByChassis(characterGuid);

            if (dbLoadouts.Count == 0)
            {
                var fallback = playerLoadout.visuals.AsPlayerVisualLoadout(characterGuid);
                fallback.id = 1;
                return new List<PlayerVisualLoadout> { fallback };
            }

            var result = new List<PlayerVisualLoadout>(dbLoadouts.Count);
            foreach (var dbLoadout in dbLoadouts)
            {
                var loadout = playerLoadout.visuals.AsPlayerVisualLoadout(characterGuid);
                loadout.id = dbLoadout.LoadoutId;

                var battleframeVisuals = visualsByChassis.TryGetValue(dbLoadout.ChassisSdbId, out var byChassis)
                    ? byChassis.Visuals
                    : PlayerBattleframeVisuals.CreateDefault();

                loadout.decals = (battleframeVisuals.decals ?? new List<WebDecal>())
                    .Where(decal => decal != null && decal.sdb_id > 0)
                    .Select(decal => new Decal
                    {
                        sdb_id = decal.sdb_id,
                        color = unchecked((uint)decal.color),
                        transform = decal.transform ?? System.Array.Empty<float>()
                    })
                    .ToList();
                loadout.warpaint_id = battleframeVisuals.warpaint_id;
                loadout.warpaintpatterns = battleframeVisuals.GetEffectiveWarpaintPatterns()
                    .Select(pattern => new WarpaintPattern
                    {
                        sdb_id = pattern.sdb_id,
                        transform = pattern.transform ?? System.Array.Empty<float>(),
                        usage = pattern.usage
                    })
                    .ToList();
                loadout.visual_overrides = (battleframeVisuals.visual_overrides ?? new List<int>())
                    .Where(id => id > 0)
                    .Select(id => new VisualOverride
                    {
                        slot_type_id = 0,
                        visual_id = id
                    })
                    .ToList();

                Serilog.Log.Debug(
                    "VisualLoadouts(v2): char={CharGuid}, loadout={LoadoutId}, chassis={ChassisSdbId}, warpaintId={WarpaintId}, patternsCount={PatternsCount}, decalsCount={DecalsCount}, overridesCount={OverridesCount}",
                    characterGuid,
                    dbLoadout.LoadoutId,
                    dbLoadout.ChassisSdbId,
                    loadout.warpaint_id,
                    loadout.warpaintpatterns?.Count ?? 0,
                    loadout.decals?.Count ?? 0,
                    loadout.visual_overrides?.Count ?? 0);

                result.Add(loadout);
            }

            return result;
        }

        [HttpPost("characters/{characterGuid}/visual_loadouts/{loadoutIdx}/purchase_and_update")]
        [R5SigAuthRequired]
        public async Task<object> PurchaseAndUpdateVisualLoadout(long characterGuid, int loadoutIdx, [FromBody] PlayerVisualLoadout updates)
        {
            var loginResult = await Db.GetLoginData(GetUid());
            if (loginResult == null) return Unauthorized();

            var playerLoadout   = await Db.GetBasicCharacterAndVisualData(characterGuid);
            if (playerLoadout.info == null) return NotFound();

            Serilog.Log.Debug(
                "PurchaseAndUpdateVisualLoadout(v2) incoming payload: char={CharGuid}, loadoutIdx={LoadoutIdx}, warpaintId={WarpaintId}, patternCount={PatternCount}, decalCount={DecalCount}, patternPayload={PatternPayload}, decalPayload={DecalPayload}",
                characterGuid,
                loadoutIdx,
                updates.warpaint_id,
                updates.warpaintpatterns?.Count ?? 0,
                updates.decals?.Count ?? 0,
                SummarizePatternPayload(updates.warpaintpatterns),
                SummarizeDecalPayload(updates.decals));

            var assetsValid = await SDB.ValidateNewCharacterAssets(updates.head_id, updates.voice_set_id, updates.gender);
            if (!assetsValid)
            {
                return ReturnError(new Error(Error.Codes.ERR_INVALID_CHARACTER), 400);
            }

            var colors          = await SDB.GetNewCharactersColors(updates.eye_color_id, updates.skin_color_id, updates.hair_color_id);
            if (colors == null) return BadRequest();

            var ornamentUsageById = (await SDB.GetOrnamentsInfoList())
                .GroupBy(item => item.id)
                .ToDictionary(group => group.Key, group => group.Last().usage);

            var purchaseCost = await CalculateVisualPurchaseCost(characterGuid, updates);
            if (purchaseCost > 0)
            {
                var spendResult = await Db.SpendRedBeans(loginResult.account_id, purchaseCost);
                if (!spendResult.success)
                {
                    return ReturnError(Error.Codes.TMW_MSG, spendResult.error, 400);
                }

                await PersistVisualUnlocks(characterGuid, updates);
            }

            playerLoadout.visuals = CharacterUtil.UpdateCharacterVisualsFromGarage(playerLoadout.visuals, updates, colors, ornamentUsageById);

            var dbLoadouts = (await Db.GetCharacterLoadouts(characterGuid)).ToList();
            var targetLoadout = ResolveTargetLoadout(dbLoadouts, loadoutIdx);
            if (targetLoadout == null)
            {
                Serilog.Log.Debug(
                    "PurchaseAndUpdateVisualLoadout(v2): unable to resolve target loadout for char={CharGuid}, loadoutIdx={LoadoutIdx}",
                    characterGuid, loadoutIdx);
                return ReturnError(Error.Codes.ERR_UNKNOWN, "Loadout not found", 400);
            }

            var visualsByChassis = await Db.GetBattleframeVisualsByChassis(characterGuid);
            var battleframeVisuals = visualsByChassis.TryGetValue(targetLoadout.ChassisSdbId, out var existingByChassis)
                ? existingByChassis.Visuals
                : PlayerBattleframeVisuals.CreateDefault();
            ApplyBattleframeVisualUpdates(battleframeVisuals, updates);

            await Db.UpdateCharacterVisuals(characterGuid, playerLoadout.visuals);
            var targetBattleframeId = visualsByChassis.TryGetValue(targetLoadout.ChassisSdbId, out var targetByChassis)
                ? targetByChassis.BattleframeGuid
                : await Db.EnsureBattleframeRecord(characterGuid, targetLoadout.ChassisSdbId);

            if (targetBattleframeId.HasValue && targetBattleframeId.Value > 0)
            {
                await Db.UpdateBattleframeVisuals(targetBattleframeId.Value, battleframeVisuals);
            }

            Serilog.Log.Debug(
                "PurchaseAndUpdateVisualLoadout(v2): updated char={CharGuid}, loadoutIdx={LoadoutIdx}, resolvedLoadout={LoadoutId}, chassis={ChassisSdbId}, battleframeId={BattleframeId}, warpaintId={WarpaintId}",
                characterGuid,
                loadoutIdx,
                targetLoadout.LoadoutId,
                targetLoadout.ChassisSdbId,
                targetBattleframeId,
                battleframeVisuals.warpaint_id);

            return Content("{}", "application/json");
        }

        private static CharacterLoadout? ResolveTargetLoadout(IEnumerable<CharacterLoadout> loadouts, int loadoutIdx)
        {
            var list = loadouts?.ToList() ?? new List<CharacterLoadout>();
            if (list.Count == 0)
            {
                return null;
            }

            // Some clients pass a 1-based loadout id, others pass a zero-based index.
            return list.FirstOrDefault(l => l.LoadoutId == loadoutIdx)
                ?? list.FirstOrDefault(l => l.LoadoutId == loadoutIdx + 1)
                ?? (loadoutIdx >= 0 && loadoutIdx < list.Count
                    ? list.OrderBy(l => l.LoadoutId).ElementAt(loadoutIdx)
                    : null);
        }

        private static void ApplyBattleframeVisualUpdates(PlayerBattleframeVisuals visuals, PlayerVisualLoadout updates)
        {
            visuals.decals = updates.decals
                .Where(decal => decal != null && decal.sdb_id > 0)
                .Select(decal => new WebDecal
                {
                    sdb_id = decal.sdb_id,
                    color = unchecked((int)decal.color),
                    transform = decal.transform ?? System.Array.Empty<float>()
                })
                .ToList();

            if (updates.warpaint_id > 0)
            {
                visuals.warpaint_id = updates.warpaint_id;
            }

            var warpaintPatterns = updates.warpaintpatterns
                .Where(pattern => pattern != null && pattern.sdb_id > 0)
                .Select(pattern => new WebWarpaintPattern
                {
                    sdb_id = pattern.sdb_id,
                    usage = pattern.usage,
                    transform = pattern.transform ?? System.Array.Empty<float>()
                })
                .ToList();
            visuals.SetWarpaintPatterns(warpaintPatterns);

            visuals.visual_overrides = updates.visual_overrides
                .Where(visualOverride => visualOverride != null && visualOverride.visual_id > 0)
                .Select(visualOverride => visualOverride.visual_id)
                .ToList();
        }

        private async Task<int> CalculateVisualPurchaseCost(long characterGuid, PlayerVisualLoadout updates)
        {
            var ownedUnlocks = (await Db.GetCharacterInventory(characterGuid)).unlocks
                .ToHashSet();

            int totalCost = 0;
            foreach (var request in EnumerateRequestedVisualUnlocks(updates))
            {
                if (ownedUnlocks.Contains((request.unlockType, request.unlockId, 0)))
                {
                    continue;
                }

                if (TryGetVisualProductPrice(request.unlockType, request.unlockId, out var price))
                {
                    totalCost += price;
                }
            }

            return totalCost;
        }

        private async Task PersistVisualUnlocks(long characterGuid, PlayerVisualLoadout updates)
        {
            var ownedUnlocks = (await Db.GetCharacterInventory(characterGuid)).unlocks
                .ToHashSet();

            foreach (var request in EnumerateRequestedVisualUnlocks(updates))
            {
                if (ownedUnlocks.Contains((request.unlockType, request.unlockId, 0)))
                {
                    continue;
                }

                if (TryGetVisualProductPrice(request.unlockType, request.unlockId, out _))
                {
                    await Db.UpsertCharacterUnlock(characterGuid, request.unlockType, request.unlockId, 0);
                }
            }
        }

        private static IEnumerable<(string unlockType, int unlockId)> EnumerateRequestedVisualUnlocks(PlayerVisualLoadout updates)
        {
            foreach (var ornament in updates.ornaments.Where(item => item?.remote_id > 0))
            {
                yield return ("ornaments", ornament.remote_id);
            }

            foreach (var decal in updates.decals.Where(item => item != null && item.sdb_id > 0))
            {
                yield return ("decals", decal.sdb_id);
            }

            if (updates.warpaint_id > 0)
            {
                yield return ("warpaints", updates.warpaint_id);
            }

            foreach (var pattern in updates.warpaintpatterns.Where(item => item != null && item.sdb_id > 0))
            {
                yield return ("czi_patterns", pattern.sdb_id);
            }

            foreach (var visualOverride in updates.visual_overrides.Where(item => item != null && item.visual_id > 0))
            {
                yield return ("visual_overrides", visualOverride.visual_id);
            }
        }

        private static bool TryGetVisualProductPrice(string unlockType, int unlockId, out int price)
        {
            price = 0;
            var catalog = LoadProductCatalog();
            return catalog.TryGetValue((unlockType, unlockId), out price);
        }

        private static string SummarizePatternPayload(IEnumerable<WarpaintPattern>? patterns)
        {
            var payload = (patterns ?? Enumerable.Empty<WarpaintPattern>())
                .Where(pattern => pattern != null)
                .Select(pattern => new
                {
                    pattern.sdb_id,
                    pattern.usage,
                    transform = SummarizeTransform(pattern.transform, 8)
                })
                .ToList();

            return JsonSerializer.Serialize(payload);
        }

        private static string SummarizeDecalPayload(IEnumerable<Decal>? decals)
        {
            var payload = (decals ?? Enumerable.Empty<Decal>())
                .Where(decal => decal != null)
                .Select(decal => new
                {
                    decal.sdb_id,
                    decal.color,
                    transform = SummarizeTransform(decal.transform, 12)
                })
                .ToList();

            return JsonSerializer.Serialize(payload);
        }

        private static object SummarizeTransform(float[]? transform, int maxEntries)
        {
            var safe = transform ?? Array.Empty<float>();
            return new
            {
                len = safe.Length,
                values = safe.Take(maxEntries).ToArray()
            };
        }

        private static Dictionary<(string unlockType, int unlockId), int> LoadProductCatalog()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "StaticData", "products.json");
            if (!System.IO.File.Exists(path))
            {
                return new Dictionary<(string unlockType, int unlockId), int>();
            }

            var json = System.IO.File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<ProductsJson>(json);
            return data?.products?
                .Where(product => product.active && product.approved)
                .GroupBy(product => (product.sdb_type, (int)product.sdb_id))
                .ToDictionary(
                    group => group.Key,
                    group => (int)(group.Last().bundles.FirstOrDefault()?.current_price?.amount ?? group.Last().lowest_price))
                ?? new Dictionary<(string unlockType, int unlockId), int>();
        }

        // TOOD: Implement
        [HttpGet("characters/{characterGuid}/market/listings")]
        [R5SigAuthRequired]
        public Task<object> GetMarketListings(long characterGuid)
        {
            var data = "[]";

            return Task.FromResult<object>(Content(data, "application/json"));
        }

        private Character CreateDefaultChar(string name = "Aero")
        {
            var character = new Character
            {
                character_guid    = 42,
                name              = name,
                unique_name       = name,
                is_dev            = true,
                is_active         = true,
                created_at        = DateTime.Now.AddMonths(-2),
                title_id          = 0,
                time_played_secs  = 0,
                needs_name_change = false,
                max_frame_level   = 20,
                frame_sdb_id      = 76335,
                current_level     = 10,
                gender            = 1,
                current_gender    = "female",
                elite_rank        = 95487,
                last_seen_at      = DateTime.Now,
                visuals = new CharacterBattleframeCombinedVisuals()
                {
                    id     = 0,
                    race   = 0,
                    gender = 1,
                    skin_color = new WebIdValueColor()
                    {
                        id = 118969,
                        value = new WebColor()
                        {
                            color = 4294930822
                        }
                    },
                    voice_set = new WebId()
                    {
                        id = 1033
                    },
                    head = new WebId()
                    {
                        id = 10026
                    },
                    eye_color = new WebIdValueColor()
                    {
                        id = 118980,
                        value = new WebColor()
                        {
                            color = 1633685600
                        }
                    },
                    lip_color = new WebIdValueColor()
                    {
                        id = 1,
                        value = new WebColor()
                        {
                            color = 1
                        }
                    },
                    hair_color = new WebIdValueColor()
                    {
                        id = 77193,
                        value = new WebColor()
                        {
                            color = 1917780001
                        }
                    },
                    facial_hair_color = new WebIdValueColor()
                    {
                        id = 77193,
                        value = new WebColor()
                        {
                            color = 1917780001
                        }
                    },
                    head_accessories = new List<WebIdValueColor>()
                    {
                        new WebIdValueColor()
                        {
                            id = 10117,
                            value = new WebColor()
                            {
                                color = 1211031763
                            }
                        }
                    },
                    ornaments = new List<WebId>(),
                    eyes = new WebId()
                    {
                        id = 10001
                    },
                    hair = new WebIdValueColorId()
                    {
                        id = 10113,
                        color = new WebColorId()
                        {
                            id    = 77193,
                            value = 1917780001
                        }
                    },
                    facial_hair = new WebIdValueColorId()
                    {
                        id = 0,
                        color = new WebColorId()
                        {
                            id    = 77187,
                            value = 1518862368
                        }
                    },
                    glider = new WebId()
                    {
                        id = 0
                    },
                    vehicle = new WebId()
                    {
                        id = 0
                    },
                    //decals      = new List<WebId>(),
                    warpaint_id = 143225,
                    warpaint = new List<uint>()
                        {4216738474, 0, 4216717312, 418250752, 1525350400, 4162844703, 4162844703},
                    decalgradients    = new List<int>(),
                    warpaint_patterns = new List<WebWarpaintPattern>(),
                    visual_overrides  = new List<int>()
                },
                gear = new List<GearSlot>()
                {
                },
                expires_in = 0,
                race       = "human",
                migrations = new List<int>()
            };

            return character;
        }
    }
}