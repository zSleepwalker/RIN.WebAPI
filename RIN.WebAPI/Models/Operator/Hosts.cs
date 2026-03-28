namespace RIN.WebAPI.Models.Operator
{
    public class Hosts
    {
        public string frontend_host        { get; set; } = null!;
        public string store_host           { get; set; } = null!;
        public string chat_server          { get; set; } = null!;
        public string replay_host          { get; set; } = null!;
        public string web_host             { get; set; } = null!;
        public string market_host          { get; set; } = null!;
        public string ingame_host          { get; set; } = null!;
        public string clientapi_host       { get; set; } = null!;
        public string web_asset_host       { get; set; } = null!;
        public string web_accounts_host    { get; set; } = null!;
        public string rhsigscan_host       { get; set; } = null!;
        public string password_change_path { get; set; } = null!;
        public string bug_report_path      { get; set; } = null!;
    }
}