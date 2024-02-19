using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using static RIN.Core.Error;

namespace RIN.Core.ClientApi
{
    public class Armies
    {
        public int page { get; set; }
        public int total_count { get; set; }
        // public List<Army> results { get; set; }
        public List<Army> results { get; set; }
    }

    public class Army
    {
        public bool disbanded { get; set; }
        public long army_guid { get; set; }
        public string commander { get; set; }
        public string link { get; set; }
        public string name { get; set; }
        public string personality { get; set; }
        public string playstyle { get; set; }
        public string is_recruiting_str { get; set; }
        public short member_count { get; set; }
        public short is_recruiting { get; set; }
        public string tag { get; set; }
        public string region { get; set; }
    }

    public class CreateArmy
    {
        public string name { get; set; }
        public string description { get; set; }
        public string playstyle { get; set; }
        public string personality { get; set; }
        public bool is_recruiting { get; set; }
        public string website { get; set; } = String.Empty;
        public string region { get; set; }
    }

    public class CreateArmyResp
    {
        public short army_id { get; set; }
        public long army_guid { get; set; }
    }
    
    public class UpdateArmy
    {
        public string? name { get; set; }
        public string? description { get; set; }
        public string playstyle { get; set; }
        public string region { get; set; }
        public bool is_recruiting { get; set; }
        public string personality { get; set; }
        public string website { get; set; } = String.Empty;
    }
    
    public class ArmyUserAccess
    {
        public bool is_commander { get; set; }
        public bool is_officer { get; set; }
        public bool can_invite { get; set; }
        public bool can_kick { get; set; }
        public bool can_edit { get; set; }
        public bool can_promote { get; set; }
        public bool can_edit_motd { get; set; }
        public bool can_mass_email { get; set; }
    }
}