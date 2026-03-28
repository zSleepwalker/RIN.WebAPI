namespace RIN.WebAPI.Models.ClientApi
{
    public class LoginEvent
    {
        public long   id          { get; set; }
        public string name        { get; set; } = null!;
        public string description { get; set; } = null!;
        public string color       { get; set; } = null!;
        public bool   is_active   { get; set; }
        public string created_at  { get; set; } = null!;
        public string updated_at  { get; set; } = null!;
    }
}
