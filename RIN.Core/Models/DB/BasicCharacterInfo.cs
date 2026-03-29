using ProtoBuf;

namespace RIN.Core.Models
{
    [ProtoContract]
    public class BasicCharacterInfo
    {
        [ProtoMember(1)]  public string Name                    { get; set; } = null!;
        [ProtoMember(2)]  public byte   Race                    { get; set; }
        [ProtoMember(3)]  public byte   Gender                  { get; set; }
        [ProtoMember(4)]  public int    TitleId                 { get; set; }
        [ProtoMember(5)]  public long   CurrentBattleframeId    { get; set; }
        [ProtoMember(6)]  public uint   CurrentBattleframeSDBId { get; set; }
        [ProtoMember(7)]  public string ArmyTag                 { get; set; } = null!;
        [ProtoMember(8)]  public ulong  ArmyGuid                { get; set; }
        [ProtoMember(9)]  public bool   ArmyIsOfficer           { get; set; }
        [ProtoMember(10)] public uint   LastZoneId              { get; set; }
        [ProtoMember(11)] public uint   LastOutpostId           { get; set; }
        [ProtoMember(12)] public uint   TimePlayed              { get; set; }
        [ProtoMember(13)] public uint   PvPRank                 { get; set; }
        [ProtoMember(14)] public uint   EliteLevel              { get; set; }
        [ProtoMember(15)] public uint   StaffFlags              { get; set; }
        [ProtoMember(16)] public byte   Level                   { get; set; }
        [ProtoMember(17)] public byte   EffectiveLevel          { get; set; }
        [ProtoMember(18)] public uint   VipLevel                { get; set; }

    }
}
