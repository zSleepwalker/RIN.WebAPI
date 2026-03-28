namespace RIN.WebAPI.Models.ClientApi
{
    public class CaisStatus
    {
        public string state      { get; set; } = null!;
        public long   duration   { get; set; }
        public long   expires_at { get; set; }
    }
}