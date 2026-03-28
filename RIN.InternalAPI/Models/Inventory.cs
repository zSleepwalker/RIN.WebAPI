using System.Collections.Generic;
using ProtoBuf;

namespace RIN.InternalAPI.Models
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
    public class CharacterInventoryResponse
    {
        [ProtoMember(1)] public List<CharacterItem> Items { get; set; } = new List<CharacterItem>();
        [ProtoMember(2)] public List<CharacterResource> Resources { get; set; } = new List<CharacterResource>();
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
}
