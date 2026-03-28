namespace RIN.Core.Models.ClientApi
{
    public class ZoneSettings
    {
        public uint                       id                             { get; set; }
        public string                     description                    { get; set; } = null!;
        public bool                       queueing_enabled               { get; set; }
        public bool                       challenge_enabled              { get; set; }
        public bool                       skip_matchmaking               { get; set; }
        public uint                       team_count                     { get; set; }
        public uint                       min_players_per_team           { get; set; }
        public uint                       min_players_accept_per_team    { get; set; }
        public uint                       max_players_per_team           { get; set; }
        public uint                       challenge_min_players_per_team { get; set; }
        public uint                       challenge_max_players_per_team { get; set; }
        public string                     gametype                       { get; set; } = null!;
        public string                     displayed_name                 { get; set; } = null!;
        public string                     displayed_gametype             { get; set; } = null!;
        public uint                       rotation_priority              { get; set; }
        public uint                       zone_id                        { get; set; }
        public uint?                      mission_id                     { get; set; }
        public uint?                      sort_order                     { get; set; }
        public float                      xp_bonus                       { get; set; }
        public string                     instance_type_pool             { get; set; } = null!;
        public ZoneReward                 reward_winner                  { get; set; } = new ZoneReward();
        public ZoneReward                 reward_loser                   { get; set; } = new ZoneReward();
        public bool                       cert_required                  { get; set; }
        public bool                       is_preview_zone                { get; set; }
        public string                     displayed_desc                 { get; set; } = null!;
        public ZoneImages                 images                         { get; set; } = null!;
        public List<ZoneCertRequirements> cert_requirements              { get; set; } = new();
        public List<ZoneDifficultyLevels> difficulty_levels              { get; set; } = new();
    }
}
