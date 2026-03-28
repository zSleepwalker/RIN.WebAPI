namespace RIN.WebAPI.Models.Operator
{
    public class CheckReq
    {
        public string environment { get; set; } = null!;
        public int    build       { get; set; }
    }
}