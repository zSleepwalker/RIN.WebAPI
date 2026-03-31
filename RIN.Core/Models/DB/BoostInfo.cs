namespace RIN.Core.Models.DB
{
    public class BoostInfo
    {
        public string BoostType { get; set; } = string.Empty;
        public float Modifier { get; set; }
        public int DurationSecs { get; set; }
        public bool IsVip { get; set; }
    }
}
