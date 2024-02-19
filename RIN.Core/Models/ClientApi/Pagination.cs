using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RIN.Core.ClientApi
{
    public class Pagination
    {
        public string? q { get; set; }
        public int page { get; set; } = 1;
        public int per_page { get; set; } = 100;
    }
}
