using ProtoBuf;

namespace RIN.InternalAPI.Models
{
    [ProtoContract]
    [ProtoInclude(1, typeof(SaveGameSessionData))]
    [ProtoInclude(2, typeof(SaveLgvRaceFinish))]
    [ProtoInclude(3, typeof(SaveCharacterLoadout))]
    [ProtoInclude(4, typeof(SaveCharacterUnlock))]
    [ProtoInclude(5, typeof(SaveCurrentBattleframe))]
    public abstract class Command;

    [ProtoContract]
    public class SaveGameSessionData : Command
    {
        [ProtoMember(1)] public ulong CharacterId { get; set; }
        [ProtoMember(2)] public uint  ZoneId      { get; set; }
        [ProtoMember(3)] public uint  OutpostId   { get; set; }
        [ProtoMember(4)] public uint  TimePlayed  { get; set; }
    }

    [ProtoContract]
    public class SaveLgvRaceFinish : Command
    {
        [ProtoMember(1)] public ulong CharacterGuid { get; set; }
        [ProtoMember(2)] public uint  LeaderboardId { get; set; }
        [ProtoMember(3)] public ulong TimeMs        { get; set; }
    }

    [ProtoContract]
    public class SaveCharacterLoadout : Command
    {
        [ProtoMember(1)] public ulong  CharacterGuid    { get; set; }
        [ProtoMember(2)] public int    LoadoutId        { get; set; }
        [ProtoMember(3)] public int    ChassisSdbId     { get; set; }
        [ProtoMember(4)] public string VisualsJson      { get; set; } = string.Empty;
        [ProtoMember(5)] public string SlottedItemsJson { get; set; } = string.Empty;
    }

    [ProtoContract]
    public class SaveCharacterUnlock : Command
    {
        [ProtoMember(1)] public ulong CharacterGuid { get; set; }
        [ProtoMember(2)] public string UnlockType { get; set; } = string.Empty;
        [ProtoMember(3)] public uint UnlockId { get; set; }
        [ProtoMember(4)] public uint FrameId { get; set; }
    }

    [ProtoContract]
    public class SaveCurrentBattleframe : Command
    {
        [ProtoMember(1)] public ulong CharacterGuid { get; set; }
        [ProtoMember(2)] public int ChassisSdbId { get; set; }
    }
}
