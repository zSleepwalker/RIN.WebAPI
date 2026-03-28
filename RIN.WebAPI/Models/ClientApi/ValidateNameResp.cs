using System.Collections.Generic;

namespace RIN.WebAPI.Models.ClientApi
{
    public class ValidateNameResp
    {
        public string       code    { get; set; } = null!;
        public string       message { get; set; } = null!;
        public string       name    { get; set; } = null!;
        public List<string> reason  { get; set; } = new List<string>();
        public bool         valid   { get; set; }
    }
}