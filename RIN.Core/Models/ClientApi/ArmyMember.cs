namespace RIN.Core.ClientApi
{
    public class ArmyMembers
    {
        public string page { get; set; }
        public int total_count { get; set; }
        public List<ArmyMember> results { get; set; }
    }

    public class ArmyMember
    {
        public short id { get; set; }
        public long army_id { get; set; }
        public long army_guid { get; set; }
        public long character_guid { get; set; }
        public short army_rank_id { get; set; }
        public long created_at { get; set; }
        public long updated_at { get; set; }
        public string rank_name { get; set; }
        public short rank_position { get; set; }
        public short last_zone_id { get; set; }
        public long last_seen_at { get; set; }
        public short current_level { get; set; }
        public long current_frame_sdb_id { get; set; }
        public bool is_online { get; set; }
        public string public_note { get; set; }
        public string officer_note { get; set; }
        public string name { get; set; }
    }
}
