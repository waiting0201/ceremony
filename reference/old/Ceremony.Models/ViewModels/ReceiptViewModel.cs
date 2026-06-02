using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ceremony.Models
{
    public class ReceiptViewModel
    {
        public Guid SignupID { get; set; }
        public string Name { get; set; }
        public string Zipcode { get; set; }
        public string Address { get; set; }
        public string Fee { get; set; }
        public string Number { get; set; }
        public string Year { get; set; }
        public string Month { get; set; }
        public string Day { get; set; }
        public string Prepay { get; set; }
    }
}
