namespace RIN.WebAPI.Models.Operator
{
    public class BuildInfo
    {
        public string build       { get; set; } = null!;
        public string environment { get; set; } = null!;
        public string region      { get; set; } = null!;
        public int    patch_level { get; set; }
    }
}