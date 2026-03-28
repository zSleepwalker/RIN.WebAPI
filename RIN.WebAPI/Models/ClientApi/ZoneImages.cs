namespace RIN.WebAPI.Models.ClientApi
{
    public class ZoneImages
    {
        public string thumbnail { get; set; } = null!;
        public string[] screenshot { get; set; } = System.Array.Empty<string>();
        public string lfg { get; set; } = null!;
    }
}
