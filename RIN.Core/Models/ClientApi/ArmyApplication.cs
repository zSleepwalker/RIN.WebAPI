namespace RIN.Core.ClientApi
{
    public class ArmyApplication
    {
        public uint id { get; set; }
        public long army_id { get; set; }
        public long character_guid { get; set; }
        public string message { get; set; }
        public string direction { get; set; }
        public long created_at { get; set; }
        public long updated_at { get; set; }
        public long army_guid { get; set; }
        public uint current_level { get; set; }
        public uint current_frame_sdb_id { get; set; }
        public bool is_online { get; set; }
        public string name { get; set; }
    }
    
    public class CharacterArmyApplication
    {
        public uint id { get; set; }
        public long army_id { get; set; }
        public long character_guid { get; set; }
        public string message { get; set; }
        public string direction { get; set; }
        public long created_at { get; set; }
        public long updated_at { get; set; }
        public long army_guid { get; set; }
        public uint current_level { get; set; }
        public uint current_frame_sdb_id { get; set; }
        public bool is_online { get; set; }
        public string name { get; set; }
    }
}
