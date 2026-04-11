using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using RIN.Core.ClientApi;
using RIN.Core.Common;
using RIN.Core.Models.ClientApi;

namespace RIN.Core.DB
{
    [ProtoContract]
    public class PlayerBattleframeVisuals
    {
        [ProtoMember(1)] public List<WebDecal>  decals            { get; set; } = new();
        [ProtoMember(2)] public int             warpaint_id       { get; set; }
        [ProtoMember(3)] public List<uint>      warpaint          { get; set; } = new();
        [ProtoMember(4)] public List<int>       decalgradients    { get; set; } = new();
        [ProtoMember(5)] public List<int>       warpaint_patterns { get; set; } = new();
        [ProtoMember(6)] public List<int>       visual_overrides  { get; set; } = new();
        [ProtoMember(7)] public List<WebWarpaintPattern> warpaint_pattern_data { get; set; } = new();

        public List<WebWarpaintPattern> GetEffectiveWarpaintPatterns()
        {
            if (warpaint_pattern_data != null && warpaint_pattern_data.Count > 0)
            {
                return warpaint_pattern_data
                    .Where(pattern => pattern != null && pattern.sdb_id > 0)
                    .Select(pattern => new WebWarpaintPattern
                    {
                        sdb_id = pattern.sdb_id,
                        usage = pattern.usage,
                        transform = pattern.transform ?? System.Array.Empty<float>()
                    })
                    .ToList();
            }

            return (warpaint_patterns ?? new List<int>())
                .Where(id => id > 0)
                .Select((id, index) => new WebWarpaintPattern
                {
                    sdb_id = id,
                    usage = index,
                    transform = System.Array.Empty<float>()
                })
                .ToList();
        }

        public void SetWarpaintPatterns(IEnumerable<WebWarpaintPattern> patterns)
        {
            var normalized = (patterns ?? Enumerable.Empty<WebWarpaintPattern>())
                .Where(pattern => pattern != null && pattern.sdb_id > 0)
                .Select(pattern => new WebWarpaintPattern
                {
                    sdb_id = pattern.sdb_id,
                    usage = pattern.usage,
                    transform = pattern.transform ?? System.Array.Empty<float>()
                })
                .ToList();

            warpaint_pattern_data = normalized;
            warpaint_patterns = normalized.Select(pattern => pattern.sdb_id).ToList();
        }
        
        public void ApplyToCharacterVisuals(CharacterBattleframeCombinedVisuals cVisuals)
        {
            cVisuals.decals            = decals;
            cVisuals.warpaint_id       = warpaint_id;
            cVisuals.warpaint          = warpaint;
            cVisuals.decalgradients    = decalgradients;
            cVisuals.warpaint_patterns = GetEffectiveWarpaintPatterns();
            cVisuals.visual_overrides  = visual_overrides;
        }
        
        public static PlayerBattleframeVisuals CreateDefault()
        {
            var visuals = new PlayerBattleframeVisuals
            {
                decals            = new List<WebDecal>(),
                warpaint_id       = 1033,
                warpaint          = new List<uint> {4294910212, 2631073792, 830865408, 1246298112, 2494725038, 3430953281, 3430953281},
                decalgradients    = new List<int>(),
                warpaint_patterns = new List<int>(),
                visual_overrides  = new List<int>(),
                warpaint_pattern_data = new List<WebWarpaintPattern>()
            };

            return visuals;
        }
    }
}