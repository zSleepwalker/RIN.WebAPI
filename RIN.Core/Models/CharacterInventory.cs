using ProtoBuf;
using System.Collections.Generic;

namespace RIN.Core.Models
{
    [ProtoContract]
    public class CharacterItem
    {
        [ProtoMember(1)] public ulong Guid { get; set; }
        [ProtoMember(2)] public uint SdbId { get; set; }
    }

    [ProtoContract]
    public class CharacterResource
    {
        [ProtoMember(1)] public uint SdbId { get; set; }
        [ProtoMember(2)] public uint Quantity { get; set; }
    }

    [ProtoContract]
    public class CharacterLoadout
    {
        [ProtoMember(1)] public int LoadoutId { get; set; }
        [ProtoMember(2)] public int ChassisSdbId { get; set; }
        [ProtoMember(3)] public string Visuals { get; set; } = "{}"; // JSON
        [ProtoMember(4)] public string SlottedItems { get; set; } = "{}"; // JSON (Map<Slot, ItemGuid>)
    }

    [ProtoContract]
    public class CharacterInventoryResponse
    {
        [ProtoMember(1)] public List<CharacterItem> Items { get; set; } = new List<CharacterItem>();
        [ProtoMember(2)] public List<CharacterResource> Resources { get; set; } = new List<CharacterResource>();
        [ProtoMember(3)] public List<CharacterLoadout> Loadouts { get; set; } = new List<CharacterLoadout>();
        [ProtoMember(4)] public List<CharacterUnlockEntry> Unlocks { get; set; } = new List<CharacterUnlockEntry>();
    }

    [ProtoContract]
    public class CharacterUnlockEntry
    {
        [ProtoMember(1)] public string UnlockType { get; set; } = string.Empty;
        [ProtoMember(2)] public uint UnlockId { get; set; }
        [ProtoMember(3)] public uint FrameId { get; set; }
    }

    [ProtoContract]
    public class ConsumeResourceReq
    {
        [ProtoMember(1)] public ulong CharacterId { get; set; }
        [ProtoMember(2)] public uint SdbId { get; set; }
        [ProtoMember(3)] public uint Quantity { get; set; }
    }

    [ProtoContract]
    public class ConsumeResourceResp
    {
        [ProtoMember(1)] public bool Success { get; set; }
    }

    [ProtoContract]
    public class ConsumeItemReq
    {
        [ProtoMember(1)] public ulong CharacterId { get; set; }
        [ProtoMember(2)] public uint SdbId { get; set; } // Item to consume (by SDB ID for quantity check)
        [ProtoMember(3)] public uint Quantity { get; set; }
    }

    [ProtoContract]
    public class ConsumeItemResp
    {
        [ProtoMember(1)] public bool Success { get; set; }
    }

    [ProtoContract]
    public class AddCharacterItemReq
    {
        [ProtoMember(1)] public ulong CharacterId { get; set; }
        [ProtoMember(2)] public uint SdbId { get; set; }
    }

    [ProtoContract]
    public class AddCharacterItemResp
    {
        [ProtoMember(1)] public bool Success { get; set; }
        [ProtoMember(2)] public ulong Guid { get; set; }
    }

    [ProtoContract]
    public class ApplyCharacterBoostReq
    {
        [ProtoMember(1)] public ulong CharacterId { get; set; }
        [ProtoMember(2)] public string BoostType { get; set; } = string.Empty;
        [ProtoMember(3)] public float Modifier { get; set; }
        [ProtoMember(4)] public uint DurationSeconds { get; set; }
    }

    [ProtoContract]
    public class ApplyCharacterBoostResp
    {
        [ProtoMember(1)] public bool Success { get; set; }
    }
}
