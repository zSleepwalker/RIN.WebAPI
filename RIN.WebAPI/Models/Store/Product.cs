namespace RIN.WebAPI.Models.Store
{
    public class Product
    {
        public bool active           { get; set; }
        public bool approved         { get; set; }
        public bool featured         { get; set; }
        public int best_discount     { get; set; }
        public string created_at     { get; set; } = string.Empty;
        public string overview_image { get; set; } = string.Empty;
        public string name           { get; set; } = string.Empty;
        public int lowest_price      { get; set; }
        public int sdb_id            { get; set; }
        public string sdb_type       { get; set; } = string.Empty;
    }
}