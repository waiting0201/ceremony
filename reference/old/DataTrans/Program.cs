using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Entity;
using System.Reflection;

using Z.EntityFramework.Extensions;

using Ceremony.Models;
using Ceremony.Service;

namespace DataTrans
{
    class Program
    {
        static CeremonyEntities _entity;
        static CeremonyNOEntities _entity_no;
        static CeremonyONEntities _entity_on;

        static ZipcodesService zipcodesService;
        static List<Zipcodes> listzipcodes;

        //static string _Path = AppDomain.CurrentDomain.BaseDirectory;
        //static string _RsDir = "Rs";
        //static string _RsfilePath = _Path + _RsDir;

        static void Main(string[] args)
        {
            zipcodesService = new ZipcodesService();
            listzipcodes = zipcodesService.Get().ToList();

            //SyncNOs();
            //SyncONs();
            //SyncAddress();
            SyncBeliever();
        }

        //現場信眾資料同步
        static void SyncNOs()
        {
            Console.WriteLine("現場信眾資料同步開始");            
            int i = 1;
            int batchSize = 1000;

            //List<ceremonyfee> no_ceremonyfees = _entity_no.ceremonyfee.Where(a => a.address != "" && a.address.Contains("中市") && !a.address.Contains("台中市") && !a.address.Contains("臺中市")).AsNoTracking().ToList();
            //for (int b = 0; b <= no_ceremonyfees.Count() / batchSize; b++)
            //{
            //    IEnumerable<ceremonyfee> no_ceremonyfeesbatch = no_ceremonyfees.Skip(batchSize * b).Take(batchSize);
            //    foreach (ceremonyfee item in no_ceremonyfeesbatch)
            //    {
            //        item.address = item.address.Replace("中市", "台中市");
            //        _entity_no.ceremonyfee.Attach(item);
            //        _entity_no.Entry(item).State = EntityState.Modified;

            //        Console.WriteLine(i + " " + item.serialid + " " + item.address + " Update");
            //        i++;
            //    }

            //    Console.WriteLine("現場信眾資料更新中");
            //    _entity_no.SaveChanges();
            //}

            using(_entity_no = new CeremonyNOEntities())
            {
                int[] arrceremonys = { 168, 172, 174, 175, 176, 177, 178, 179, 180 };
                IQueryable<ceremonyfee> no_ceremonyfees = _entity_no.ceremonyfee.Where(a => arrceremonys.Contains((int)a.trial_CEREMONYID_2) && a.family != null && !a.family.StartsWith("*")).OrderBy(o => o.family.Trim());

                for (int b = 0; b <= no_ceremonyfees.Count() / batchSize; b++)
                {
                    IQueryable<ceremonyfee> no_ceremonyfeesbatch = no_ceremonyfees.Skip(batchSize * b).Take(batchSize);

                    List<Believers> ListBelievers = new List<Believers>();
                    List<Signups> ListSignups = new List<Signups>();

                    Believers believer;
                    Guid BelieverID = Guid.Empty;
                    string family = string.Empty;

                    //取得每個地址
                    foreach (ceremonyfee item in no_ceremonyfeesbatch)
                    {
                        if (item.ps != null && item.ps.StartsWith("*")) continue;
                        Address address = new Address(item.address);

                        Signups signup = new Signups();
                        signup.SignupID = Guid.NewGuid();
                        signup.Fee = item.feemoney;
                        signup.Year = 110;

                        if (item.ps != null)
                        {
                            Number number = new Number(item.ps);
                            signup.Number = number.Num;

                            if (number.PrePayYear != 0 && number.PrePayCeremonyCategoryID != Guid.Empty)
                            {
                                signup.PrepayYear = number.PrePayYear;
                                signup.PrepayCeremonyCategoryID = number.PrePayCeremonyCategoryID;
                            }
                        }

                        signup.AdminID = 1;
                        signup.Createdate = DateTime.Now;

                        switch (item.trial_CEREMONYID_2)
                        {
                            case 172:
                                //春季
                                signup.CeremonyCategoryID = new Guid("18927907-dcad-42b2-8f2a-635c2e0fa98d");
                                //一般
                                signup.SignupType = 1;
                                signup.NumberTitle = "No";
                                break;
                            case 174:
                                //春季
                                signup.CeremonyCategoryID = new Guid("18927907-dcad-42b2-8f2a-635c2e0fa98d");
                                //普桌
                                signup.SignupType = 4;
                                signup.NumberTitle = "普";
                                break;
                            case 175:
                                //中元
                                signup.CeremonyCategoryID = new Guid("0c478f0e-787c-448e-ba7b-b1579f3f1fce");
                                //一般
                                signup.SignupType = 1;
                                signup.NumberTitle = "No";
                                break;
                            case 176:
                                //中元
                                signup.CeremonyCategoryID = new Guid("0c478f0e-787c-448e-ba7b-b1579f3f1fce");
                                //觀音會
                                signup.SignupType = 3;
                                signup.NumberTitle = "觀";
                                break;
                            case 177:
                                //中元
                                signup.CeremonyCategoryID = new Guid("0c478f0e-787c-448e-ba7b-b1579f3f1fce");
                                //普桌
                                signup.SignupType = 4;
                                signup.NumberTitle = "普";
                                break;
                            case 178:
                                //秋季
                                signup.CeremonyCategoryID = new Guid("3864e4dc-24db-4544-acb3-3351592f6dab");
                                //一般
                                signup.SignupType = 1;
                                signup.NumberTitle = "No";
                                break;
                            case 179:
                                //秋季
                                signup.CeremonyCategoryID = new Guid("3864e4dc-24db-4544-acb3-3351592f6dab");
                                //觀音會
                                signup.SignupType = 3;
                                signup.NumberTitle = "觀";
                                break;
                            case 180:
                                //秋季
                                signup.CeremonyCategoryID = new Guid("3864e4dc-24db-4544-acb3-3351592f6dab");
                                //普桌
                                signup.SignupType = 4;
                                signup.NumberTitle = "普";
                                break;
                        }

                        #region 信眾資料處理
                        char[] sep = new char[] { ' ', '　' };

                        string livingname = string.Empty;

                        if (item.trial_LIVE1_10 != null)
                        {
                            string live1 = item.trial_LIVE1_10.Trim();
                            if (!live1.StartsWith("*") && live1 != "")
                            {
                                int count = 0;
                                string d = string.Empty;

                                string[] arrlive1 = live1.Split(sep, StringSplitOptions.RemoveEmptyEntries);
                                foreach (string s in arrlive1)
                                {
                                    if (s.Length == 1)
                                    {
                                        if (count < 2)
                                        {
                                            d = d + s;
                                            count++;

                                            if (count == 1)
                                            {
                                                continue;
                                            }
                                            else
                                            {
                                                count = 0;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        d = s;
                                    }

                                    if (livingname == string.Empty)
                                    {
                                        livingname = d;
                                    }
                                    else
                                    {
                                        livingname = livingname + "," + d;
                                    }

                                    d = string.Empty;
                                }
                            }
                        }

                        if (item.trial_LIVE2_11 != null)
                        {
                            string live2 = item.trial_LIVE2_11.Trim();
                            if (!live2.StartsWith("*") && live2 != "")
                            {
                                int count = 0;
                                string d = string.Empty;

                                string[] arrlive2 = live2.Split(sep, StringSplitOptions.RemoveEmptyEntries);
                                foreach (string s in arrlive2)
                                {
                                    if (s.Length == 1)
                                    {
                                        if (count < 2)
                                        {
                                            d = d + s;
                                            count++;

                                            if (count == 1)
                                            {
                                                continue;
                                            }
                                            else
                                            {
                                                count = 0;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        d = s;
                                    }

                                    if (livingname == string.Empty)
                                    {
                                        livingname = d;
                                    }
                                    else
                                    {
                                        livingname = livingname + "," + d;
                                    }

                                    d = string.Empty;
                                }
                            }
                        }

                        if (item.trial_LIVE3_12 != null)
                        {
                            string live3 = item.trial_LIVE3_12.Trim();
                            if (!live3.StartsWith("*") && live3 != "")
                            {
                                int count = 0;
                                string d = string.Empty;

                                string[] arrlive3 = live3.Split(sep, StringSplitOptions.RemoveEmptyEntries);
                                foreach (string s in arrlive3)
                                {
                                    if (s.Length == 1)
                                    {
                                        if (count < 2)
                                        {
                                            d = d + s;
                                            count++;

                                            if (count == 1)
                                            {
                                                continue;
                                            }
                                            else
                                            {
                                                count = 0;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        d = s;
                                    }

                                    if (livingname == string.Empty)
                                    {
                                        livingname = d;
                                    }
                                    else
                                    {
                                        livingname = livingname + "," + d;
                                    }

                                    d = string.Empty;
                                }
                            }
                        }

                        string deadname = string.Empty;

                        if (item.die1 != null)
                        {
                            string dead1 = item.die1.Trim();
                            if (!dead1.StartsWith("*") && dead1 != "")
                            {
                                int count = 0;
                                string d = string.Empty;

                                string[] arrdead1 = dead1.Split(sep, StringSplitOptions.RemoveEmptyEntries);
                                foreach (string s in arrdead1)
                                {
                                    if (s.Length == 1)
                                    {
                                        if (count < 2)
                                        {
                                            d = d + s;
                                            count++;

                                            if (count == 1)
                                            {
                                                continue;
                                            }
                                            else
                                            {
                                                count = 0;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        d = s;
                                    }

                                    if (deadname == string.Empty)
                                    {
                                        deadname = d;
                                    }
                                    else
                                    {
                                        deadname = deadname + "," + d;
                                    }

                                    d = string.Empty;
                                }
                            }
                        }

                        if (item.trial_DIE2_8 != null)
                        {
                            string dead2 = item.trial_DIE2_8.Trim();
                            if (!dead2.StartsWith("*") && dead2 != "")
                            {
                                int count = 0;
                                string d = string.Empty;

                                string[] arrdead2 = dead2.Split(sep, StringSplitOptions.RemoveEmptyEntries);
                                foreach (string s in arrdead2)
                                {
                                    if (s.Length == 1)
                                    {
                                        if (count < 2)
                                        {
                                            d = d + s;
                                            count++;

                                            if (count == 1)
                                            {
                                                continue;
                                            }
                                            else
                                            {
                                                count = 0;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        d = s;
                                    }

                                    if (deadname == string.Empty)
                                    {
                                        deadname = d;
                                    }
                                    else
                                    {
                                        deadname = deadname + "," + d;
                                    }

                                    d = string.Empty;
                                }
                            }
                        }

                        if (item.die3 != null)
                        {
                            string dead3 = item.die3.Trim();
                            if (!dead3.StartsWith("*") && dead3 != "")
                            {
                                int count = 0;
                                string d = string.Empty;

                                string[] arrdead3 = dead3.Split(sep, StringSplitOptions.RemoveEmptyEntries);
                                foreach (string s in arrdead3)
                                {
                                    if (s.Length == 1)
                                    {
                                        if (count < 2)
                                        {
                                            d = d + s;
                                            count++;

                                            if (count == 1)
                                            {
                                                continue;
                                            }
                                            else
                                            {
                                                count = 0;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        d = s;
                                    }

                                    if (deadname == string.Empty)
                                    {
                                        deadname = d;
                                    }
                                    else
                                    {
                                        deadname = deadname + "," + d;
                                    }

                                    d = string.Empty;
                                }
                            }
                        }
                        #endregion

                        if (family != item.family.Trim())
                        {
                            BelieverID = Guid.NewGuid();
                            family = item.family.Trim();

                            IEnumerable<Zipcodes> zipcodes = null;
                            Zipcodes zipcode = null;
                            if (address.IsParseSuccessed)
                            {
                                string city = address.City.Substring(address.City.Length - 3, 2).Replace('臺', '台');
                                string district = address.District.Substring(0, 2);

                                zipcodes = listzipcodes.Where(a => a.City.StartsWith(city)).OrderBy(o => o.Zipcode);

                                zipcode = zipcodes.FirstOrDefault(a => a.Area.StartsWith(district));
                            }


                            believer = new Believers();
                            believer.BelieverID = BelieverID;
                            believer.EmployeeType = item.trial_CEREMONYID_2 == 168 ? 3 : 1;
                            believer.HallName = "-";
                            believer.Name = (item.family != null && item.family != "") ? item.family.Trim() : "-";
                            if (item.phone != null) believer.Phone = CleanString(item.phone.Trim());
                            if (address.IsParseSuccessed && zipcodes.Any()) believer.MailZipcodeID = (zipcode != null) ? zipcode.ZipcodeID : zipcodes.FirstOrDefault().ZipcodeID;
                            if (address.IsParseSuccessed && zipcodes.Any()) believer.TextZipcodeID = (zipcode != null) ? zipcode.ZipcodeID : zipcodes.FirstOrDefault().ZipcodeID;
                            if (address.IsParseSuccessed)
                            {
                                believer.MailAddress = (zipcodes.Any()) ? address.Others.Trim() : CleanString(address.OrginalAddress.Trim());
                                believer.TextAddress = (zipcodes.Any()) ? address.Others.Trim() : CleanString(address.OrginalAddress.Trim());
                            }
                            else
                            {
                                believer.MailAddress = address.OrginalAddress != null ? CleanString(address.OrginalAddress.Trim()) : "";
                                believer.TextAddress = address.OrginalAddress != null ? CleanString(address.OrginalAddress.Trim()) : "";
                            }
                            believer.IsFixedNumber = false;

                            if (livingname != string.Empty)
                            {
                                string[] arrliving = livingname.Split(',');
                                int dest = arrliving.Length > 6 ? 6 : arrliving.Length;
                                for (int x = 0; x < dest; x++)
                                {
                                    string property = string.Empty;
                                    switch (x)
                                    {
                                        case 0:
                                            property = "LivingNameOne";
                                            break;
                                        case 1:
                                            property = "LivingNameTwo";
                                            break;
                                        case 2:
                                            property = "LivingNameThree";
                                            break;
                                        case 3:
                                            property = "LivingNameFour";
                                            break;
                                        case 4:
                                            property = "LivingNameFive";
                                            break;
                                        case 5:
                                            property = "LivingNameSix";
                                            break;
                                    }

                                    PropertyInfo propinfo = believer.GetType().GetProperty(property);
                                    propinfo.SetValue(believer, arrliving[x]);

                                    PropertyInfo pi = signup.GetType().GetProperty(property);
                                    pi.SetValue(signup, arrliving[x]);
                                }
                            }

                            if (deadname != string.Empty)
                            {
                                string[] arrdead = deadname.Split(',');
                                int dest = arrdead.Length > 6 ? 6 : arrdead.Length;
                                for (int x = 0; x < dest; x++)
                                {
                                    string property = string.Empty;
                                    switch (x)
                                    {
                                        case 0:
                                            property = "DeadNameOne";
                                            break;
                                        case 1:
                                            property = "DeadNameTwo";
                                            break;
                                        case 2:
                                            property = "DeadNameThree";
                                            break;
                                        case 3:
                                            property = "DeadNameFour";
                                            break;
                                        case 4:
                                            property = "DeadNameFive";
                                            break;
                                        case 5:
                                            property = "DeadNameSix";
                                            break;
                                    }

                                    PropertyInfo propinfo = believer.GetType().GetProperty(property);
                                    propinfo.SetValue(believer, arrdead[x]);

                                    PropertyInfo pi = signup.GetType().GetProperty(property);
                                    pi.SetValue(signup, arrdead[x]);
                                }
                            }

                            signup.BelieverID = BelieverID;
                            //員工不加入報名
                            if (item.trial_CEREMONYID_2 != 168) ListSignups.Add(signup);

                            ListBelievers.Add(believer);

                            Console.WriteLine(i + " " + item.serialid + " " + believer.Name + " " + livingname + " " + deadname);
                        }
                        else
                        {
                            if (livingname != string.Empty)
                            {
                                string[] arrliving = livingname.Split(',');
                                int dest = arrliving.Length > 6 ? 6 : arrliving.Length;
                                for (int x = 0; x < dest; x++)
                                {
                                    string property = string.Empty;
                                    switch (x)
                                    {
                                        case 0:
                                            property = "LivingNameOne";
                                            break;
                                        case 1:
                                            property = "LivingNameTwo";
                                            break;
                                        case 2:
                                            property = "LivingNameThree";
                                            break;
                                        case 3:
                                            property = "LivingNameFour";
                                            break;
                                        case 4:
                                            property = "LivingNameFive";
                                            break;
                                        case 5:
                                            property = "LivingNameSix";
                                            break;
                                    }

                                    PropertyInfo pi = signup.GetType().GetProperty(property);
                                    pi.SetValue(signup, arrliving[x]);
                                }
                            }

                            if (deadname != string.Empty)
                            {
                                string[] arrdead = deadname.Split(',');
                                int dest = arrdead.Length > 6 ? 6 : arrdead.Length;
                                for (int x = 0; x < dest; x++)
                                {
                                    string property = string.Empty;
                                    switch (x)
                                    {
                                        case 0:
                                            property = "DeadNameOne";
                                            break;
                                        case 1:
                                            property = "DeadNameTwo";
                                            break;
                                        case 2:
                                            property = "DeadNameThree";
                                            break;
                                        case 3:
                                            property = "DeadNameFour";
                                            break;
                                        case 4:
                                            property = "DeadNameFive";
                                            break;
                                        case 5:
                                            property = "DeadNameSix";
                                            break;
                                    }

                                    PropertyInfo pi = signup.GetType().GetProperty(property);
                                    pi.SetValue(signup, arrdead[x]);
                                }
                            }

                            signup.BelieverID = BelieverID;

                            if (item.trial_CEREMONYID_2 != 168) ListSignups.Add(signup);

                            Console.WriteLine(i + " " + item.serialid);
                        }

                        i++;
                    }

                    using(_entity = new CeremonyEntities())
                    {
                        Console.WriteLine("現場信眾資料更新中...");
                        _entity.BulkInsert(ListBelievers);
                        _entity.BulkInsert(ListSignups);
                        _entity.BulkSaveChanges();
                    }
                }
            }   
        }

        //郵撥信眾資料同步
        static void SyncONs()
        {
            Console.WriteLine("郵撥信眾資料同步開始");
            int i = 1;
            int batchSize = 1000;

            //List<ceremonyfee> on_ceremonyfees = _entity_on.ceremonyfee.Where(a => a.address != "" && a.address.Contains("新北氏")).AsNoTracking().ToList();
            //for (int b = 0; b <= on_ceremonyfees.Count() / batchSize; b++)
            //{
            //    IEnumerable<ceremonyfee> on_ceremonyfeesbatch = on_ceremonyfees.Skip(batchSize * b).Take(batchSize);
            //    foreach (ceremonyfee item in on_ceremonyfeesbatch)
            //    {
            //        item.address = item.address.Replace("新北氏", "新北市");
            //        _entity_on.ceremonyfee.Attach(item);
            //        _entity_on.Entry(item).State = EntityState.Modified;

            //        Console.WriteLine(i + " " + item.serialid + " " + item.address + " Update");
            //        i++;
            //    }

            //    Console.WriteLine("郵播信眾資料更新中");
            //    _entity_on.SaveChanges();
            //}

            using(_entity_on = new CeremonyONEntities())
            {
                int[] arrceremonys = { 89, 91, 92 };
                IQueryable<ceremonyfee> on_ceremonyfees = _entity_on.ceremonyfee.Where(a => arrceremonys.Contains((int)a.trial_CEREMONYID_2) && a.family != null && !a.family.StartsWith("*")).OrderBy(o => o.family.Trim());

                for (int b = 0; b <= on_ceremonyfees.Count() / batchSize; b++)
                {
                    IQueryable<ceremonyfee> on_ceremonyfeesbatch = on_ceremonyfees.Skip(batchSize * b).Take(batchSize);

                    List<Believers> ListBelievers = new List<Believers>();
                    List<Signups> ListSignups = new List<Signups>();

                    Believers believer;
                    Guid BelieverID = Guid.Empty;
                    string family = string.Empty;

                    foreach (ceremonyfee item in on_ceremonyfeesbatch)
                    {
                        if (item.ps != null && item.ps.StartsWith("*")) continue;
                        Address address = new Address(item.address);

                        Signups signup = new Signups();
                        signup.SignupID = Guid.NewGuid();
                        signup.Fee = item.feemoney;
                        signup.Year = 119;

                        if (item.ps != null)
                        {
                            Number number = new Number(item.ps);
                            signup.Number = number.Num;
                        }

                        //郵撥
                        signup.SignupType = 5;
                        signup.NumberTitle = "郵";

                        signup.AdminID = 1;
                        signup.Createdate = DateTime.Now;

                        switch (item.trial_CEREMONYID_2)
                        {
                            case 89:
                                //春季
                                signup.CeremonyCategoryID = new Guid("18927907-dcad-42b2-8f2a-635c2e0fa98d");
                                break;
                            case 91:
                                //中元
                                signup.CeremonyCategoryID = new Guid("0c478f0e-787c-448e-ba7b-b1579f3f1fce");
                                break;
                            case 92:
                                //秋季
                                signup.CeremonyCategoryID = new Guid("3864e4dc-24db-4544-acb3-3351592f6dab");
                                break;
                        }

                        #region 信眾資料處理
                        char[] sep = new char[] { ' ', '　' };

                        string livingname = string.Empty;

                        if (item.trial_LIVE1_10 != null)
                        {
                            string live1 = item.trial_LIVE1_10.Trim();
                            if (!live1.StartsWith("*") && live1 != "")
                            {
                                int count = 0;
                                string d = string.Empty;

                                string[] arrlive1 = live1.Split(sep, StringSplitOptions.RemoveEmptyEntries);
                                foreach (string s in arrlive1)
                                {
                                    if (s.Length == 1)
                                    {
                                        if (count < 2)
                                        {
                                            d = d + s;
                                            count++;

                                            if (count == 1)
                                            {
                                                continue;
                                            }
                                            else
                                            {
                                                count = 0;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        d = s;
                                    }

                                    if (livingname == string.Empty)
                                    {
                                        livingname = d;
                                    }
                                    else
                                    {
                                        livingname = livingname + "," + d;
                                    }

                                    d = string.Empty;
                                }
                            }
                        }

                        if (item.trial_LIVE2_11 != null)
                        {
                            string live2 = item.trial_LIVE2_11.Trim();
                            if (!live2.StartsWith("*") && live2 != "")
                            {
                                int count = 0;
                                string d = string.Empty;

                                string[] arrlive2 = live2.Split(sep, StringSplitOptions.RemoveEmptyEntries);
                                foreach (string s in arrlive2)
                                {
                                    if (s.Length == 1)
                                    {
                                        if (count < 2)
                                        {
                                            d = d + s;
                                            count++;

                                            if (count == 1)
                                            {
                                                continue;
                                            }
                                            else
                                            {
                                                count = 0;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        d = s;
                                    }

                                    if (livingname == string.Empty)
                                    {
                                        livingname = d;
                                    }
                                    else
                                    {
                                        livingname = livingname + "," + d;
                                    }

                                    d = string.Empty;
                                }
                            }
                        }

                        if (item.trial_LIVE3_12 != null)
                        {
                            string live3 = item.trial_LIVE3_12.Trim();
                            if (!live3.StartsWith("*") && live3 != "")
                            {
                                int count = 0;
                                string d = string.Empty;

                                string[] arrlive3 = live3.Split(sep, StringSplitOptions.RemoveEmptyEntries);
                                foreach (string s in arrlive3)
                                {
                                    if (s.Length == 1)
                                    {
                                        if (count < 2)
                                        {
                                            d = d + s;
                                            count++;

                                            if (count == 1)
                                            {
                                                continue;
                                            }
                                            else
                                            {
                                                count = 0;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        d = s;
                                    }

                                    if (livingname == string.Empty)
                                    {
                                        livingname = d;
                                    }
                                    else
                                    {
                                        livingname = livingname + "," + d;
                                    }

                                    d = string.Empty;
                                }
                            }
                        }

                        string deadname = string.Empty;

                        if (item.die1 != null)
                        {
                            string dead1 = item.die1.Trim();
                            if (!dead1.StartsWith("*") && dead1 != "")
                            {
                                int count = 0;
                                string d = string.Empty;

                                string[] arrdead1 = dead1.Split(sep, StringSplitOptions.RemoveEmptyEntries);
                                foreach (string s in arrdead1)
                                {
                                    if (s.Length == 1)
                                    {
                                        if (count < 2)
                                        {
                                            d = d + s;
                                            count++;

                                            if (count == 1)
                                            {
                                                continue;
                                            }
                                            else
                                            {
                                                count = 0;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        d = s;
                                    }

                                    if (deadname == string.Empty)
                                    {
                                        deadname = d;
                                    }
                                    else
                                    {
                                        deadname = deadname + "," + d;
                                    }

                                    d = string.Empty;
                                }
                            }
                        }

                        if (item.trial_DIE2_8 != null)
                        {
                            string dead2 = item.trial_DIE2_8.Trim();
                            if (!dead2.StartsWith("*") && dead2 != "")
                            {
                                int count = 0;
                                string d = string.Empty;

                                string[] arrdead2 = dead2.Split(sep, StringSplitOptions.RemoveEmptyEntries);
                                foreach (string s in arrdead2)
                                {
                                    if (s.Length == 1)
                                    {
                                        if (count < 2)
                                        {
                                            d = d + s;
                                            count++;

                                            if (count == 1)
                                            {
                                                continue;
                                            }
                                            else
                                            {
                                                count = 0;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        d = s;
                                    }

                                    if (deadname == string.Empty)
                                    {
                                        deadname = d;
                                    }
                                    else
                                    {
                                        deadname = deadname + "," + d;
                                    }

                                    d = string.Empty;
                                }
                            }
                        }

                        if (item.die3 != null)
                        {
                            string dead3 = item.die3.Trim();
                            if (!dead3.StartsWith("*") && dead3 != "")
                            {
                                int count = 0;
                                string d = string.Empty;

                                string[] arrdead3 = dead3.Split(sep, StringSplitOptions.RemoveEmptyEntries);
                                foreach (string s in arrdead3)
                                {
                                    if (s.Length == 1)
                                    {
                                        if (count < 2)
                                        {
                                            d = d + s;
                                            count++;

                                            if (count == 1)
                                            {
                                                continue;
                                            }
                                            else
                                            {
                                                count = 0;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        d = s;
                                    }

                                    if (deadname == string.Empty)
                                    {
                                        deadname = d;
                                    }
                                    else
                                    {
                                        deadname = deadname + "," + d;
                                    }

                                    d = string.Empty;
                                }
                            }
                        }
                        #endregion

                        if (family != item.family.Trim())
                        {
                            BelieverID = Guid.NewGuid();
                            family = item.family.Trim();

                            IEnumerable<Zipcodes> zipcodes = null;
                            Zipcodes zipcode = null;
                            if (address.IsParseSuccessed)
                            {
                                string city = address.City.Substring(address.City.Length - 3, 2).Replace('臺', '台');
                                string district = address.District.Substring(0, 2);

                                zipcodes = listzipcodes.Where(a => a.City.StartsWith(city)).OrderBy(o => o.Zipcode);

                                zipcode = zipcodes.FirstOrDefault(a => a.Area.StartsWith(district));
                            }

                            believer = new Believers();
                            believer.BelieverID = BelieverID;
                            believer.EmployeeType = item.trial_CEREMONYID_2 == 87 ? 2 : 1;
                            believer.HallName = "-";
                            believer.Name = (item.family != null && item.family != "") ? item.family.Trim() : "-";
                            if (item.phone != null) believer.Phone = CleanString(item.phone.Trim());
                            if (address.IsParseSuccessed && zipcodes.Any()) believer.MailZipcodeID = (zipcode != null) ? zipcode.ZipcodeID : zipcodes.FirstOrDefault().ZipcodeID;
                            if (address.IsParseSuccessed && zipcodes.Any()) believer.TextZipcodeID = (zipcode != null) ? zipcode.ZipcodeID : zipcodes.FirstOrDefault().ZipcodeID;
                            if (address.IsParseSuccessed)
                            {
                                believer.MailAddress = (zipcodes.Any()) ? address.Others.Trim() : CleanString(address.OrginalAddress.Trim());
                                believer.TextAddress = (zipcodes.Any()) ? address.Others.Trim() : CleanString(address.OrginalAddress.Trim());
                            }
                            else
                            {
                                believer.MailAddress = address.OrginalAddress != null ? CleanString(address.OrginalAddress.Trim()) : "";
                                believer.TextAddress = address.OrginalAddress != null ? CleanString(address.OrginalAddress.Trim()) : "";
                            }
                            believer.IsFixedNumber = false;

                            if (livingname != string.Empty)
                            {
                                string[] arrliving = livingname.Split(',');
                                int dest = arrliving.Length > 6 ? 6 : arrliving.Length;
                                for (int x = 0; x < dest; x++)
                                {
                                    string property = string.Empty;
                                    switch (x)
                                    {
                                        case 0:
                                            property = "LivingNameOne";
                                            break;
                                        case 1:
                                            property = "LivingNameTwo";
                                            break;
                                        case 2:
                                            property = "LivingNameThree";
                                            break;
                                        case 3:
                                            property = "LivingNameFour";
                                            break;
                                        case 4:
                                            property = "LivingNameFive";
                                            break;
                                        case 5:
                                            property = "LivingNameSix";
                                            break;
                                    }

                                    PropertyInfo propinfo = believer.GetType().GetProperty(property);
                                    propinfo.SetValue(believer, arrliving[x]);

                                    PropertyInfo pi = signup.GetType().GetProperty(property);
                                    pi.SetValue(signup, arrliving[x]);
                                }
                            }

                            if (deadname != string.Empty)
                            {
                                string[] arrdead = deadname.Split(',');
                                int dest = arrdead.Length > 6 ? 6 : arrdead.Length;
                                for (int x = 0; x < dest; x++)
                                {
                                    string property = string.Empty;
                                    switch (x)
                                    {
                                        case 0:
                                            property = "DeadNameOne";
                                            break;
                                        case 1:
                                            property = "DeadNameTwo";
                                            break;
                                        case 2:
                                            property = "DeadNameThree";
                                            break;
                                        case 3:
                                            property = "DeadNameFour";
                                            break;
                                        case 4:
                                            property = "DeadNameFive";
                                            break;
                                        case 5:
                                            property = "DeadNameSix";
                                            break;
                                    }

                                    PropertyInfo propinfo = believer.GetType().GetProperty(property);
                                    propinfo.SetValue(believer, arrdead[x]);

                                    PropertyInfo pi = signup.GetType().GetProperty(property);
                                    pi.SetValue(signup, arrdead[x]);
                                }
                            }

                            signup.BelieverID = BelieverID;
                            //員工不加入報名
                            if (item.trial_CEREMONYID_2 != 87) ListSignups.Add(signup);

                            ListBelievers.Add(believer);

                            Console.WriteLine(i + " " + item.serialid + " " + believer.Name + " " + livingname + " " + deadname);
                        }
                        else
                        {
                            if (livingname != string.Empty)
                            {
                                string[] arrliving = livingname.Split(',');
                                int dest = arrliving.Length > 6 ? 6 : arrliving.Length;
                                for (int x = 0; x < dest; x++)
                                {
                                    string property = string.Empty;
                                    switch (x)
                                    {
                                        case 0:
                                            property = "LivingNameOne";
                                            break;
                                        case 1:
                                            property = "LivingNameTwo";
                                            break;
                                        case 2:
                                            property = "LivingNameThree";
                                            break;
                                        case 3:
                                            property = "LivingNameFour";
                                            break;
                                        case 4:
                                            property = "LivingNameFive";
                                            break;
                                        case 5:
                                            property = "LivingNameSix";
                                            break;
                                    }

                                    PropertyInfo pi = signup.GetType().GetProperty(property);
                                    pi.SetValue(signup, arrliving[x]);
                                }
                            }

                            if (deadname != string.Empty)
                            {
                                string[] arrdead = deadname.Split(',');
                                int dest = arrdead.Length > 6 ? 6 : arrdead.Length;
                                for (int x = 0; x < dest; x++)
                                {
                                    string property = string.Empty;
                                    switch (x)
                                    {
                                        case 0:
                                            property = "DeadNameOne";
                                            break;
                                        case 1:
                                            property = "DeadNameTwo";
                                            break;
                                        case 2:
                                            property = "DeadNameThree";
                                            break;
                                        case 3:
                                            property = "DeadNameFour";
                                            break;
                                        case 4:
                                            property = "DeadNameFive";
                                            break;
                                        case 5:
                                            property = "DeadNameSix";
                                            break;
                                    }

                                    PropertyInfo pi = signup.GetType().GetProperty(property);
                                    pi.SetValue(signup, arrdead[x]);
                                }
                            }

                            signup.BelieverID = BelieverID;

                            if (item.trial_CEREMONYID_2 != 87) ListSignups.Add(signup);

                            Console.WriteLine(i + " " + item.serialid);
                        }

                        i++;
                    }

                    //List<Signups> ssss = ListSignups.Where(a => a.CeremonyCategoryID != new Guid("18927907-dcad-42b2-8f2a-635c2e0fa98d") && a.CeremonyCategoryID != new Guid("0c478f0e-787c-448e-ba7b-b1579f3f1fce") && a.CeremonyCategoryID != new Guid("3864e4dc-24db-4544-acb3-3351592f6dab")).ToList();

                    using (_entity = new CeremonyEntities())
                    {
                        Console.WriteLine("郵播信眾資料更新中...");
                        _entity.BulkInsert(ListBelievers);
                        _entity.BulkInsert(ListSignups);
                        _entity.BulkSaveChanges();
                    }
                }
            }
        }

        static void SyncAddress()
        {
            Console.WriteLine("報名地址資料同步開始");
            int batchSize = 1000;

            using (_entity = new CeremonyEntities())
            {
                IQueryable<Signups> signups = _entity.Signups.Include(a => a.Zipcodes).Include(a => a.Zipcodes1).OrderBy(o => o.Year).ThenBy(o => o.SignupType).ThenBy(o => o.Number);

                for (int b = 0; b <= signups.Count() / batchSize; b++)
                {
                    IQueryable<Signups> signupsbatch = signups.Skip(batchSize * b).Take(batchSize);
                    foreach (Signups item in signupsbatch)
                    {
                        Console.WriteLine(item.Year + " " + item.NumberTitle + item.Number + "同步中");

                        Believers beliver = _entity.Believers.Find(item.BelieverID);
                        if(beliver.Zipcodes != null) beliver.MailZipcode = beliver.Zipcodes.Zipcode;
                        if(beliver.Zipcodes1 != null) beliver.TextZipcode = beliver.Zipcodes1.Zipcode;

                        if (beliver.Zipcodes != null) item.MailZipcodeID = beliver.MailZipcodeID;
                        if (beliver.Zipcodes != null) item.MailZipcode = beliver.Zipcodes.Zipcode;
                        item.MailAddress = beliver.MailAddress;
                        if (beliver.Zipcodes1 != null) item.TextZipcodeID = beliver.TextZipcodeID;
                        if (beliver.Zipcodes1 != null) item.TextZipcode = beliver.Zipcodes1.Zipcode;
                        item.TextAddress = beliver.TextAddress;
                    }

                    Console.WriteLine("資料更新中...");
                    _entity.SaveChanges();
                }
            }
        }

        static void SyncBeliever()
        {
            using (CeremonyEntities db = new CeremonyEntities())
            {
                IQueryable<Signups> signups = db.Signups.Where(a => a.BelieverID == null);
                foreach(Signups signup in signups)
                {
                    Believers believer = db.Believers.FirstOrDefault(a => a.LivingNameOne == signup.LivingNameOne);
                    if(believer != null)
                    {
                        signup.BelieverID = believer.BelieverID;
                    }
                }
                db.SaveChanges();
            }
        }

        static string CleanString(string s)
        {
            if(s.StartsWith("*"))
            {
                return string.Empty;
            }
            else
            {
                return s.Trim();
            }
        }
    }
}
