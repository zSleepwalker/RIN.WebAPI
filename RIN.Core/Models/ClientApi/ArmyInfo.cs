using System.Reflection;

namespace RIN.Core.ClientApi
{
    public class ArmyInfo
    {
        public short id { get; set; }
        public long account_id { get; set; }
        public long army_guid { get; set; }
        public long character_guid { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string playstyle { get; set; }
        public string personality { get; set; }
        public string motd { get; set; }
        public bool is_recruiting { get; set; }
        public long created_at { get; set; }
        public long updated_at { get; set; }
        public long commander_guid { get; set; }
        public short tag_position { get; set; }
        public short min_size { get; set; }
        public short max_size { get; set; }
        public bool disbanded { get; set; }
        public string website { get; set; }
        public bool mass_email { get; set; }
        public string region { get; set; }
        public string login_message { get; set; }
        public string timezone { get; set; }
        public long established_at { get; set; }
        public string tag { get; set; }
        public string language { get; set; }
        public short default_rank_id { get; set; }
        public short member_count { get; set; }
        public List<ArmyOfficers> officers { get; set; }

        public bool TryUpdate(List<ArmyOfficers> prop)
        {
            try {
                this.officers = prop;
                return true;
            }
            catch (Exception) { }

            return false;
        }
    }
}
