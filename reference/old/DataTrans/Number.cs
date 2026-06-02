using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.VisualBasic;

namespace DataTrans
{
    public class Number
    {
        public Number(string number)
        {
            this.OrginalNumber = number;
            this.ParseByRegex(number);
        }

        public int Num { get; set; }

        public string Others { get; set; }

        public int PrePayYear { get; set; }

        public Guid PrePayCeremonyCategoryID { get; set; }

        public bool IsParseSuccessed { get; set; }

        public string OrginalNumber { get; private set; }

        private void ParseByRegex(string number)
        {
            number = Strings.StrConv(number, VbStrConv.Narrow);

            var pattern = @"(?<number>(^[0-9]*(-1)?))(?<others>.*)";
            Match match = Regex.Match(number.Trim(), pattern);

            if (match.Success)
            {
                this.IsParseSuccessed = true;

                string[] n = match.Groups["number"].ToString().Split('-');

                if(n.Length == 1)
                {
                    this.Num = Convert.ToInt32(n[0]);
                }
                else
                {
                    this.Num = Convert.ToInt32(n[0]) + 1;
                }

                this.Others = match.Groups["others"].ToString();
                if(this.Others != "")
                {
                    this.ParseOtherByRegex(this.Others);
                }
            }
        }

        private void ParseOtherByRegex(string other)
        {
            other = other.Replace("●預繳至", "").Replace("年", "").Replace("止●", "");

            var pattern = @"(?<year>[0-9]{3})(?<ceremony>\D{1}[季元])";
            Match match = Regex.Match(other.Trim(), pattern);

            if (match.Success)
            {
                this.PrePayYear = Convert.ToInt32(match.Groups["year"].ToString());

                string ceremony = match.Groups["ceremony"].ToString();
                switch (ceremony)
                {
                    case "春季":
                        this.PrePayCeremonyCategoryID = new Guid("18927907-dcad-42b2-8f2a-635c2e0fa98d");
                        break;
                    case "中元":
                        this.PrePayCeremonyCategoryID = new Guid("0c478f0e-787c-448e-ba7b-b1579f3f1fce");
                        break;
                    case "秋季":
                        this.PrePayCeremonyCategoryID = new Guid("3864e4dc-24db-4544-acb3-3351592f6dab");
                        break;
                }
            }
        }
    }
}
