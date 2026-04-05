using System.Text.Json;
using RIN.Core.DB;
using RIN.Core.DB.SDB;

namespace RIN.WebAPI.Utils;

internal static class BattleframeLoadoutBuilder
{
    private const uint DefaultVehicleSdbId = 77087;
    private const uint DefaultGliderSdbId = 81423;
    private const byte VehicleSlotType = 157;
    private const byte GliderSlotType = 158;

    public static async Task<bool> CreateLoadoutWithDefaults(DB db, SDB sdb, long characterGuid, int loadoutId, int chassisId)
    {
        var battleframeId = await db.EnsureBattleframeRecord(characterGuid, chassisId);
        if (!battleframeId.HasValue || battleframeId.Value <= 0)
        {
            return false;
        }

        var starterItemGuids = new Dictionary<int, Queue<ulong>>();
        var slottedItems = new Dictionary<byte, ulong>();
        var starterSlots = (await sdb.GetChassisDefaultLoadoutSlots(chassisId)).ToArray();

        foreach (var slot in starterSlots)
        {
            if (slot.DefaultPveModule == 0)
            {
                continue;
            }

            if (!starterItemGuids.TryGetValue(slot.DefaultPveModule, out var availableGuids) || availableGuids.Count == 0)
            {
                var itemGuid = await db.AddCharacterItem(characterGuid, slot.DefaultPveModule);
                if (itemGuid <= 0)
                {
                    return false;
                }

                availableGuids = new Queue<ulong>();
                availableGuids.Enqueue((ulong)itemGuid);
                starterItemGuids[slot.DefaultPveModule] = availableGuids;
            }

            slottedItems[(byte)slot.SlotType] = availableGuids.Dequeue();
        }

        if (!slottedItems.ContainsKey(VehicleSlotType))
        {
            if (!starterItemGuids.TryGetValue((int)DefaultVehicleSdbId, out var vehicleGuids) || vehicleGuids.Count == 0)
            {
                var itemGuid = await db.AddCharacterItem(characterGuid, (int)DefaultVehicleSdbId);
                if (itemGuid <= 0)
                {
                    return false;
                }

                vehicleGuids = new Queue<ulong>();
                vehicleGuids.Enqueue((ulong)itemGuid);
                starterItemGuids[(int)DefaultVehicleSdbId] = vehicleGuids;
            }

            slottedItems[VehicleSlotType] = vehicleGuids.Dequeue();
        }

        if (!slottedItems.ContainsKey(GliderSlotType))
        {
            if (!starterItemGuids.TryGetValue((int)DefaultGliderSdbId, out var gliderGuids) || gliderGuids.Count == 0)
            {
                var itemGuid = await db.AddCharacterItem(characterGuid, (int)DefaultGliderSdbId);
                if (itemGuid <= 0)
                {
                    return false;
                }

                gliderGuids = new Queue<ulong>();
                gliderGuids.Enqueue((ulong)itemGuid);
                starterItemGuids[(int)DefaultGliderSdbId] = gliderGuids;
            }

            slottedItems[GliderSlotType] = gliderGuids.Dequeue();
        }

        return await db.SaveCharacterLoadout(
            characterGuid,
            loadoutId,
            chassisId,
            JsonSerializer.Serialize(Array.Empty<object>()),
            JsonSerializer.Serialize(slottedItems));
    }
}