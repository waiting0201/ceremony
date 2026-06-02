using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ceremony.Models
{
    public class BelieverViewModel
    {
        public Guid BelieverID { get; set; }
        public int EmployeeType { get; set; }
        public string EmployeeTypeTitle { get; set; }
        public string HallName { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public int? MailZipcodeID { get; set; }
        public string MailCity { get; set; }
        public string MailZone { get; set; }
        public string MailAddress { get; set; }
        public int? TextZipcodeID { get; set; }
        public string TextCity { get; set; }
        public string TextZone { get; set; }
        public string TextAddress { get; set; }
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
