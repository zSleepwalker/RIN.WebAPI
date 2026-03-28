namespace RIN.Core.ClientApi
{
    public class ArmyListItem
    {
        public long   army_guid     { get; set; }
        public string name          { get; set; } = null!;
        public string personality   { get; set; } = null!;
        public bool   is_recruiting { get; set; }
        public string region        { get; set; } = null!;
        public uint   member_count  { get; set; }
    }
}
