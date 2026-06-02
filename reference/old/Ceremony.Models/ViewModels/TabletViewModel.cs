using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ceremony.Models
{
    public class TabletViewModel
    {
        public Guid SignupID { get; set; }
        public string Number { get; set; }
        public string HallNameFirst { get; set; }
        public string HallNameSecond { get; set; }
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
    }
}
