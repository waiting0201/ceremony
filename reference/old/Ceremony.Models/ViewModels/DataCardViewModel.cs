using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ceremony.Models
{
    public class DataCardViewModel
    {
        public Guid SignupID { get; set; }
        public string HallName { get; set; }
        public string Number { get; set; }
        public string Prepay { get; set; }
        public string LivingNameOne { get; set; }
        public string LivingNameTwo { get; set; }
        public string LivingNameThree { get; set; }
        public string LivingNameFour { get; set; }
        public string LivingNameFive { get; set; }
        public string LivingNameSix { get; set; }
        public string DeadNameOne { get; set; }
        public string DeadNameTwo { get; set; }
        public string DeadNameThree { get; set; }
        public string DeadNameFour { get; set; }
        public string DeadNameFive { get; set; }
        public string DeadNameSix { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public string Remark { get; set; }
    }
}
