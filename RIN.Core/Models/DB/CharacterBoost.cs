using System;

namespace RIN.Core.Models.DB
{
    public class CharacterBoost
    {
        public long character_guid { get; set; }
        public string boost_type { get; set; } = null!;
        public float modifier { get; set; }
        public DateTime expiration_date { get; set; }
    }
}
