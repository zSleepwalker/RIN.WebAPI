using RIN.WebAPI.Models.Operator;

namespace RIN.WebAPI.Models.ClientApi
{
    public class OracleTicket
    {
        public string country           { get; set; } = null!;
        public string datacenter        { get; set; } = null!;
        public string hostname          { get; set; } = null!;
        public string matrix_url        { get; set; } = null!;
        public Hosts  operator_override { get; set; } = null!;
        public string session_id        { get; set; } = null!;
        public string ticket            { get; set; } = null!;
    }
}