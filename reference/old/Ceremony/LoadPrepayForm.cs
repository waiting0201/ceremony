using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Globalization;

using Ceremony.Models;
using Ceremony.Service;

namespace Ceremony
{
    public partial class LoadPrepayForm : Form
    {
        private CeremonyEntities db;
        private CeremonyCategorysService ceremonycategorysService;
        private SignupsService signupsService;

        private TaiwanCalendar taiwanCalendar;

        public LoadPrepayForm()
        {
            InitializeComponent();

            taiwanCalendar = new TaiwanCalendar();

            db = new CeremonyEntities();
            ceremonycategorysService = new CeremonyCategorysService(db);
            signupsService = new SignupsService(db);
        }

        private void LoadPrepayForm_Load(object sender, EventArgs e)
        {
            LoadBeliever();
            LoadSelectYear();
            LoadSelectCeremony();
            LoadCeremony();
            LoadYear();
        }

        private void btnConfirm_Click(object sender, EventArgs e)
        {
            if(dlYear.SelectedValue == null)
            {
                MessageBox.Show("請選擇年份", Global.AppTitle);
                return;
            }

            if(dlCeremony.SelectedValue == null)
            {
                MessageBox.Show("請選擇法會", Global.AppTitle);
                return;
            }

            DialogResult confirmResult = MessageBox.Show("是否載入" + dlBeliever.Text + dlSelectYear.Text + "年" + dlSelectCeremony.Text + "法會預繳資料？", Global.AppTitle, MessageBoxButtons.YesNo);
            
            if (confirmResult == DialogResult.Yes)
            {
                btnConfirm.Enabled = false;

                //選擇的年份
                int selectyear = Convert.ToInt32(dlYear.SelectedValue);
                //選擇的法會
                CeremonyCategorys selectceremony = ceremonycategorysService.GetByID((Guid)dlCeremony.SelectedValue);

                switch ((int)dlBeliever.SelectedValue)
                {
                    //非員工一般
                    case 1:
                        //取得目前最大編號
                        Signups onelastsignup = signupsService.Get().Where(a => a.Year == selectyear && a.CeremonyCategoryID == selectceremony.CeremonyCategoryID && a.SignupType == 1).OrderByDescending(o => o.Number).FirstOrDefault();

                        //非員工一般有固定編號
                        List<int> onenolist = new List<int>();
                        IQueryable<Signups> onefixedsignups = signupsService.Get().Where(a => a.SignupType == 1 && a.Believers.EmployeeType == 1 && a.Believers.IsFixedNumber == true && a.Year == (int)dlSelectYear.SelectedItem && a.CeremonyCategoryID == (Guid)dlSelectCeremony.SelectedValue && a.PrepayYear != null && ((a.PrepayYear == selectyear && a.CeremonyCategorys1.Sort >= selectceremony.Sort) || (a.PrepayYear > selectyear && a.PrepayCeremonyCategoryID != null))).OrderBy(o => o.Number);

                        int oneno = (onelastsignup == null) ? 1 : (int)onelastsignup.Number + 1;
                        foreach (Signups signup in onefixedsignups)
                        {
                            Signups su = new Signups
                            {
                                SignupID = Guid.NewGuid(),
                                Year = selectyear,
                                CeremonyCategoryID = selectceremony.CeremonyCategoryID,
                                SignupType = signup.SignupType,
                                BelieverID = signup.BelieverID,
                                NumberTitle = signup.NumberTitle,
                                Number = signup.Number,
                                Fee = signup.Fee,
                                LivingNameOne = signup.LivingNameOne,
                                LivingNameTwo = signup.LivingNameTwo,
                                LivingNameThree = signup.LivingNameThree,
                                LivingNameFour = signup.LivingNameFour,
                                LivingNameFive = signup.LivingNameFive,
                                LivingNameSix = signup.LivingNameSix,
                                DeadNameOne = signup.DeadNameOne,
                                DeadNameTwo = signup.DeadNameTwo,
                                DeadNameThree = signup.DeadNameThree,
                                DeadNameFour = signup.DeadNameFour,
                                DeadNameFive = signup.DeadNameFive,
                                DeadNameSix = signup.DeadNameSix,
                                MailZipcodeID = signup.MailZipcodeID,
                                MailZipcode = signup.MailZipcode,
                                MailAddress = signup.MailAddress,
                                TextZipcodeID = signup.TextZipcodeID,
                                TextZipcode = signup.TextZipcode,
                                TextAddress = signup.TextAddress,
                                Remark = signup.Remark,
                                AdminID = Global.AdminID,
                                Createdate = DateTime.Now
                            };

                            if ((signup.PrepayYear == selectyear && signup.CeremonyCategorys1.Sort > selectceremony.Sort) || (signup.PrepayYear > selectyear))
                            {
                                su.PrepayYear = signup.PrepayYear;
                                su.PrepayCeremonyCategoryID = (Guid)signup.PrepayCeremonyCategoryID;
                            }

                            signupsService.Create(su);

                            //固定編號有跳號的
                            if (oneno != signup.Number)
                            {
                                for (int x = oneno; x < signup.Number; x++)
                                {
                                    onenolist.Add(x);
                                }
                                oneno = (int)signup.Number + 1;
                            }
                            else
                            {
                                oneno++;
                            }
                        }

                        //非員工一般無固定編號
                        IQueryable<Signups> onesignups = signupsService.Get().Where(a => a.SignupType == 1 && a.Believers.EmployeeType == 1 && a.Believers.IsFixedNumber == false && a.Year == (int)dlSelectYear.SelectedItem && a.CeremonyCategoryID == (Guid)dlSelectCeremony.SelectedValue && a.PrepayYear != null && ((a.PrepayYear == selectyear && a.CeremonyCategorys1.Sort >= selectceremony.Sort) || (a.PrepayYear > selectyear && a.PrepayCeremonyCategoryID != null))).OrderBy(o => o.Number);

                        int i = 0;
                        foreach (Signups signup in onesignups)
                        {
                            Signups su = new Signups
                            {
                                SignupID = Guid.NewGuid(),
                                Year = selectyear,
                                CeremonyCategoryID = selectceremony.CeremonyCategoryID,
                                SignupType = signup.SignupType,
                                BelieverID = signup.BelieverID,
                                NumberTitle = signup.NumberTitle,
                                Fee = signup.Fee,
                                LivingNameOne = signup.LivingNameOne,
                                LivingNameTwo = signup.LivingNameTwo,
                                LivingNameThree = signup.LivingNameThree,
                                LivingNameFour = signup.LivingNameFour,
                                LivingNameFive = signup.LivingNameFive,
                                LivingNameSix = signup.LivingNameSix,
                                DeadNameOne = signup.DeadNameOne,
                                DeadNameTwo = signup.DeadNameTwo,
                                DeadNameThree = signup.DeadNameThree,
                                DeadNameFour = signup.DeadNameFour,
                                DeadNameFive = signup.DeadNameFive,
                                DeadNameSix = signup.DeadNameSix,
                                MailZipcodeID = signup.MailZipcodeID,
                                MailZipcode = signup.MailZipcode,
                                MailAddress = signup.MailAddress,
                                TextZipcodeID = signup.TextZipcodeID,
                                TextZipcode = signup.TextZipcode,
                                TextAddress = signup.TextAddress,
                                Remark = signup.Remark,
                                AdminID = Global.AdminID,
                                Createdate = DateTime.Now
                            };

                            if ((signup.PrepayYear == selectyear && signup.CeremonyCategorys1.Sort > selectceremony.Sort) || (signup.PrepayYear > selectyear))
                            {
                                su.PrepayYear = signup.PrepayYear;
                                su.PrepayCeremonyCategoryID = (Guid)signup.PrepayCeremonyCategoryID;
                            }

                            if (onenolist.Count() > 0 && onenolist.Count() > i)
                            {
                                su.Number = onenolist[i];
                                i++;
                            }
                            else
                            {
                                su.Number = oneno;
                                oneno++;
                            }

                            signupsService.Create(su);
                        }
                        break;
                    //員工一般
                    case 2:
                        Signups twolastsignup = signupsService.Get().Where(a => a.Year == selectyear && a.CeremonyCategoryID == selectceremony.CeremonyCategoryID && a.SignupType == 1).OrderByDescending(o => o.Number).FirstOrDefault();

                        //地藏殿員工一般有固定編號
                        List<int> xonenolist = new List<int>();
                        IQueryable<Signups> xonefixedsignups = signupsService.Get().Where(a => a.SignupType == 1 && a.Believers.EmployeeType == 3 && a.Believers.IsFixedNumber == true && a.Year == (int)dlSelectYear.SelectedItem && a.CeremonyCategoryID == (Guid)dlSelectCeremony.SelectedValue && a.PrepayYear != null && ((a.PrepayYear == selectyear && a.CeremonyCategorys1.Sort >= selectceremony.Sort) || (a.PrepayYear > selectyear && a.PrepayCeremonyCategoryID != null))).OrderBy(o => o.Number);

                        int xoneno = (twolastsignup == null) ? 1 : (int)twolastsignup.Number + 1;
                        foreach (Signups signup in xonefixedsignups)
                        {
                            Signups su = new Signups
                            {
                                SignupID = Guid.NewGuid(),
                                Year = selectyear,
                                CeremonyCategoryID = selectceremony.CeremonyCategoryID,
                                SignupType = signup.SignupType,
                                BelieverID = signup.BelieverID,
                                NumberTitle = signup.NumberTitle,
                                Number = signup.Number,
                                Fee = signup.Fee,
                                LivingNameOne = signup.LivingNameOne,
                                LivingNameTwo = signup.LivingNameTwo,
                                LivingNameThree = signup.LivingNameThree,
                                LivingNameFour = signup.LivingNameFour,
                                LivingNameFive = signup.LivingNameFive,
                                LivingNameSix = signup.LivingNameSix,
                                DeadNameOne = signup.DeadNameOne,
                                DeadNameTwo = signup.DeadNameTwo,
                                DeadNameThree = signup.DeadNameThree,
                                DeadNameFour = signup.DeadNameFour,
                                DeadNameFive = signup.DeadNameFive,
                                DeadNameSix = signup.DeadNameSix,
                                MailZipcodeID = signup.MailZipcodeID,
                                MailZipcode = signup.MailZipcode,
                                MailAddress = signup.MailAddress,
                                TextZipcodeID = signup.TextZipcodeID,
                                TextZipcode = signup.TextZipcode,
                                TextAddress = signup.TextAddress,
                                Remark = signup.Remark,
                                AdminID = Global.AdminID,
                                Createdate = DateTime.Now
                            };

                            if ((signup.PrepayYear == selectyear && signup.CeremonyCategorys1.Sort > selectceremony.Sort) || (signup.PrepayYear > selectyear))
                            {
                                su.PrepayYear = signup.PrepayYear;
                                su.PrepayCeremonyCategoryID = (Guid)signup.PrepayCeremonyCategoryID;
                            }

                            signupsService.Create(su);

                            if (xoneno != signup.Number)
                            {
                                for (int x = xoneno; x < signup.Number; x++)
                                {
                                    xonenolist.Add(x);
                                }
                                xoneno = (int)signup.Number + 1;
                            }
                            else
                            {
                                xoneno++;
                            }
                        }

                        //地藏殿員工一般無固定編號
                        IQueryable<Signups> xonesignups = signupsService.Get().Where(a => a.SignupType == 1 && a.Believers.EmployeeType == 3 && a.Believers.IsFixedNumber == false && a.Year == (int)dlSelectYear.SelectedItem && a.CeremonyCategoryID == (Guid)dlSelectCeremony.SelectedValue && a.PrepayYear != null && ((a.PrepayYear == selectyear && a.CeremonyCategorys1.Sort >= selectceremony.Sort) || (a.PrepayYear > selectyear && a.PrepayCeremonyCategoryID != null))).OrderBy(o => o.Number);

                        int xi = 0;
                        foreach (Signups signup in xonesignups)
                        {
                            Signups su = new Signups
                            {
                                SignupID = Guid.NewGuid(),
                                Year = selectyear,
                                CeremonyCategoryID = selectceremony.CeremonyCategoryID,
                                SignupType = signup.SignupType,
                                BelieverID = signup.BelieverID,
                                NumberTitle = signup.NumberTitle,
                                Fee = signup.Fee,
                                LivingNameOne = signup.LivingNameOne,
                                LivingNameTwo = signup.LivingNameTwo,
                                LivingNameThree = signup.LivingNameThree,
                                LivingNameFour = signup.LivingNameFour,
                                LivingNameFive = signup.LivingNameFive,
                                LivingNameSix = signup.LivingNameSix,
                                DeadNameOne = signup.DeadNameOne,
                                DeadNameTwo = signup.DeadNameTwo,
                                DeadNameThree = signup.DeadNameThree,
                                DeadNameFour = signup.DeadNameFour,
                                DeadNameFive = signup.DeadNameFive,
                                DeadNameSix = signup.DeadNameSix,
                                MailZipcodeID = signup.MailZipcodeID,
                                MailZipcode = signup.MailZipcode,
                                MailAddress = signup.MailAddress,
                                TextZipcodeID = signup.TextZipcodeID,
                                TextZipcode = signup.TextZipcode,
                                TextAddress = signup.TextAddress,
                                Remark = signup.Remark,
                                AdminID = Global.AdminID,
                                Createdate = DateTime.Now
                            };

                            if ((signup.PrepayYear == selectyear && signup.CeremonyCategorys1.Sort > selectceremony.Sort) || (signup.PrepayYear > selectyear))
                            {
                                su.PrepayYear = signup.PrepayYear;
                                su.PrepayCeremonyCategoryID = (Guid)signup.PrepayCeremonyCategoryID;
                            }

                            if (xonenolist.Count() > 0 && xonenolist.Count() > xi)
                            {
                                su.Number = xonenolist[xi];
                                xi++;
                            }
                            else
                            {
                                su.Number = xoneno;
                                xoneno++;
                            }

                            signupsService.Create(su);
                        }
                        break;
                    //寺方
                    case 3:
                        Signups threelastsignup = signupsService.Get().Where(a => a.Year == selectyear && a.CeremonyCategoryID == selectceremony.CeremonyCategoryID && a.SignupType == 2).OrderByDescending(o => o.Number).FirstOrDefault();

                        //寺方有固定編號
                        List<int> twonolist = new List<int>();
                        IQueryable<Signups> twofixedsignups = signupsService.Get().Where(a => a.SignupType == 2 && a.Believers.IsFixedNumber == true && a.Year == (int)dlSelectYear.SelectedItem && a.CeremonyCategoryID == (Guid)dlSelectCeremony.SelectedValue && a.PrepayYear != null && ((a.PrepayYear == selectyear && a.CeremonyCategorys1.Sort >= selectceremony.Sort) || (a.PrepayYear > selectyear && a.PrepayCeremonyCategoryID != null))).OrderBy(o => o.Number);

                        int twono = (threelastsignup == null) ? 1 : (int)threelastsignup.Number + 1;
                        foreach (Signups signup in twofixedsignups)
                        {
                            Signups su = new Signups
                            {
                                SignupID = Guid.NewGuid(),
                                Year = selectyear,
                                CeremonyCategoryID = selectceremony.CeremonyCategoryID,
                                SignupType = signup.SignupType,
                                BelieverID = signup.BelieverID,
                                NumberTitle = signup.NumberTitle,
                                Number = signup.Number,
                                Fee = signup.Fee,
                                LivingNameOne = signup.LivingNameOne,
                                LivingNameTwo = signup.LivingNameTwo,
                                LivingNameThree = signup.LivingNameThree,
                                LivingNameFour = signup.LivingNameFour,
                                LivingNameFive = signup.LivingNameFive,
                                LivingNameSix = signup.LivingNameSix,
                                DeadNameOne = signup.DeadNameOne,
                                DeadNameTwo = signup.DeadNameTwo,
                                DeadNameThree = signup.DeadNameThree,
                                DeadNameFour = signup.DeadNameFour,
                                DeadNameFive = signup.DeadNameFive,
                                DeadNameSix = signup.DeadNameSix,
                                MailZipcodeID = signup.MailZipcodeID,
                                MailZipcode = signup.MailZipcode,
                                MailAddress = signup.MailAddress,
                                TextZipcodeID = signup.TextZipcodeID,
                                TextZipcode = signup.TextZipcode,
                                TextAddress = signup.TextAddress,
                                Remark = signup.Remark,
                                AdminID = Global.AdminID,
                                Createdate = DateTime.Now
                            };

                            if ((signup.PrepayYear == selectyear && signup.CeremonyCategorys1.Sort > selectceremony.Sort) || (signup.PrepayYear > selectyear))
                            {
                                su.PrepayYear = signup.PrepayYear;
                                su.PrepayCeremonyCategoryID = (Guid)signup.PrepayCeremonyCategoryID;
                            }

                            signupsService.Create(su);

                            if (twono != signup.Number)
                            {
                                for (int x = twono; x < signup.Number; x++)
                                {
                                    twonolist.Add(x);
                                }
                                twono = (int)signup.Number + 1;
                            }
                            else
                            {
                                twono++;
                            }
                        }

                        //寺方無固定編號
                        IQueryable<Signups> twosignups = signupsService.Get().Where(a => a.SignupType == 2 && a.Believers.IsFixedNumber == false && a.Year == (int)dlSelectYear.SelectedItem && a.CeremonyCategoryID == (Guid)dlSelectCeremony.SelectedValue && a.PrepayYear != null && ((a.PrepayYear == selectyear && a.CeremonyCategorys1.Sort >= selectceremony.Sort) || (a.PrepayYear > selectyear && a.PrepayCeremonyCategoryID != null))).OrderBy(o => o.Number);

                        i = 0;
                        foreach (Signups signup in twosignups)
                        {
                            Signups su = new Signups
                            {
                                SignupID = Guid.NewGuid(),
                                Year = selectyear,
                                CeremonyCategoryID = selectceremony.CeremonyCategoryID,
                                SignupType = signup.SignupType,
                                BelieverID = signup.BelieverID,
                                NumberTitle = signup.NumberTitle,
                                Fee = signup.Fee,
                                LivingNameOne = signup.LivingNameOne,
                                LivingNameTwo = signup.LivingNameTwo,
                                LivingNameThree = signup.LivingNameThree,
                                LivingNameFour = signup.LivingNameFour,
                                LivingNameFive = signup.LivingNameFive,
                                LivingNameSix = signup.LivingNameSix,
                                DeadNameOne = signup.DeadNameOne,
                                DeadNameTwo = signup.DeadNameTwo,
                                DeadNameThree = signup.DeadNameThree,
                                DeadNameFour = signup.DeadNameFour,
                                DeadNameFive = signup.DeadNameFive,
                                DeadNameSix = signup.DeadNameSix,
                                MailZipcodeID = signup.MailZipcodeID,
                                MailZipcode = signup.MailZipcode,
                                MailAddress = signup.MailAddress,
                                TextZipcodeID = signup.TextZipcodeID,
                                TextZipcode = signup.TextZipcode,
                                TextAddress = signup.TextAddress,
                                Remark = signup.Remark,
                                AdminID = Global.AdminID,
                                Createdate = DateTime.Now
                            };

                            if ((signup.PrepayYear == selectyear && signup.CeremonyCategorys1.Sort > selectceremony.Sort) || (signup.PrepayYear > selectyear))
                            {
                                su.PrepayYear = signup.PrepayYear;
                                su.PrepayCeremonyCategoryID = (Guid)signup.PrepayCeremonyCategoryID;
                            }

                            if (twonolist.Count() > 0 && twonolist.Count() > i)
                            {
                                su.Number = twonolist[i];
                                i++;
                            }
                            else
                            {
                                su.Number = twono;
                                twono++;
                            }

                            signupsService.Create(su);
                        }
                        break;
                    //觀音會
                    case 4:
                        Signups fourlastsignup = signupsService.Get().Where(a => a.Year == selectyear && a.CeremonyCategoryID == selectceremony.CeremonyCategoryID && a.SignupType == 3).OrderByDescending(o => o.Number).FirstOrDefault();

                        //觀音會有固定編號
                        List<int> threenolist = new List<int>();
                        IQueryable<Signups> threefixedsignups = signupsService.Get().Where(a => a.SignupType == 3 && a.Believers.IsFixedNumber == true && a.Year == (int)dlSelectYear.SelectedItem && a.CeremonyCategoryID == (Guid)dlSelectCeremony.SelectedValue && a.PrepayYear != null && ((a.PrepayYear == selectyear && a.CeremonyCategorys1.Sort >= selectceremony.Sort) || (a.PrepayYear > selectyear && a.PrepayCeremonyCategoryID != null))).OrderBy(o => o.Number);

                        int threeno = (fourlastsignup == null) ? 1 : (int)fourlastsignup.Number + 1;
                        foreach (Signups signup in threefixedsignups)
                        {
                            Signups su = new Signups
                            {
                                SignupID = Guid.NewGuid(),
                                Year = selectyear,
                                CeremonyCategoryID = selectceremony.CeremonyCategoryID,
                                SignupType = signup.SignupType,
                                BelieverID = signup.BelieverID,
                                NumberTitle = signup.NumberTitle,
                                Number = signup.Number,
                                Fee = signup.Fee,
                                LivingNameOne = signup.LivingNameOne,
                                LivingNameTwo = signup.LivingNameTwo,
                                LivingNameThree = signup.LivingNameThree,
                                LivingNameFour = signup.LivingNameFour,
                                LivingNameFive = signup.LivingNameFive,
                                LivingNameSix = signup.LivingNameSix,
                                DeadNameOne = signup.DeadNameOne,
                                DeadNameTwo = signup.DeadNameTwo,
                                DeadNameThree = signup.DeadNameThree,
                                DeadNameFour = signup.DeadNameFour,
                                DeadNameFive = signup.DeadNameFive,
                                DeadNameSix = signup.DeadNameSix,
                                MailZipcodeID = signup.MailZipcodeID,
                                MailZipcode = signup.MailZipcode,
                                MailAddress = signup.MailAddress,
                                TextZipcodeID = signup.TextZipcodeID,
                                TextZipcode = signup.TextZipcode,
                                TextAddress = signup.TextAddress,
                                Remark = signup.Remark,
                                AdminID = Global.AdminID,
                                Createdate = DateTime.Now
                            };

                            if ((signup.PrepayYear == selectyear && signup.CeremonyCategorys1.Sort > selectceremony.Sort) || (signup.PrepayYear > selectyear))
                            {
                                su.PrepayYear = signup.PrepayYear;
                                su.PrepayCeremonyCategoryID = (Guid)signup.PrepayCeremonyCategoryID;
                            }

                            signupsService.Create(su);

                            if (threeno != signup.Number)
                            {
                                for (int x = threeno; x < signup.Number; x++)
                                {
                                    threenolist.Add(x);
                                }
                                threeno = (int)signup.Number + 1;
                            }
                            else
                            {
                                threeno++;
                            }
                        }

                        //觀音會無固定編號
                        IQueryable<Signups> threesignups = signupsService.Get().Where(a => a.SignupType == 3 && a.Believers.IsFixedNumber == false && a.Year == (int)dlSelectYear.SelectedItem && a.CeremonyCategoryID == (Guid)dlSelectCeremony.SelectedValue && a.PrepayYear != null && ((a.PrepayYear == selectyear && a.CeremonyCategorys1.Sort >= selectceremony.Sort) || (a.PrepayYear > selectyear && a.PrepayCeremonyCategoryID != null))).OrderBy(o => o.Number);

                        i = 0;
                        foreach (Signups signup in threesignups)
                        {
                            Signups su = new Signups
                            {
                                SignupID = Guid.NewGuid(),
                                Year = selectyear,
                                CeremonyCategoryID = selectceremony.CeremonyCategoryID,
                                SignupType = signup.SignupType,
                                BelieverID = signup.BelieverID,
                                NumberTitle = signup.NumberTitle,
                                Fee = signup.Fee,
                                LivingNameOne = signup.LivingNameOne,
                                LivingNameTwo = signup.LivingNameTwo,
                                LivingNameThree = signup.LivingNameThree,
                                LivingNameFour = signup.LivingNameFour,
                                LivingNameFive = signup.LivingNameFive,
                                LivingNameSix = signup.LivingNameSix,
                                DeadNameOne = signup.DeadNameOne,
                                DeadNameTwo = signup.DeadNameTwo,
                                DeadNameThree = signup.DeadNameThree,
                                DeadNameFour = signup.DeadNameFour,
                                DeadNameFive = signup.DeadNameFive,
                                DeadNameSix = signup.DeadNameSix,
                                MailZipcodeID = signup.MailZipcodeID,
                                MailZipcode = signup.MailZipcode,
                                MailAddress = signup.MailAddress,
                                TextZipcodeID = signup.TextZipcodeID,
                                TextZipcode = signup.TextZipcode,
                                TextAddress = signup.TextAddress,
                                Remark = signup.Remark,
                                AdminID = Global.AdminID,
                                Createdate = DateTime.Now
                            };

                            if ((signup.PrepayYear == selectyear && signup.CeremonyCategorys1.Sort > selectceremony.Sort) || (signup.PrepayYear > selectyear))
                            {
                                su.PrepayYear = signup.PrepayYear;
                                su.PrepayCeremonyCategoryID = (Guid)signup.PrepayCeremonyCategoryID;
                            }

                            if (threenolist.Count() > 0 && threenolist.Count() > i)
                            {
                                su.Number = threenolist[i];
                                i++;
                            }
                            else
                            {
                                su.Number = threeno;
                                threeno++;
                            }

                            signupsService.Create(su);
                        }
                        break;
                    //員工郵撥
                    case 5:
                        Signups fivelastsignup = signupsService.Get().Where(a => a.Year == selectyear && a.CeremonyCategoryID == selectceremony.CeremonyCategoryID && a.SignupType == 5).OrderByDescending(o => o.Number).FirstOrDefault();

                        //大殿員工郵撥有固定編號
                        List<int> xfournolist = new List<int>();
                        IQueryable<Signups> xfourfixedsignups = signupsService.Get().Where(a => a.SignupType == 5 && a.Believers.EmployeeType == 2 && a.Believers.IsFixedNumber == true && a.Year == (int)dlSelectYear.SelectedItem && a.CeremonyCategoryID == (Guid)dlSelectCeremony.SelectedValue && a.PrepayYear != null && ((a.PrepayYear == selectyear && a.CeremonyCategorys1.Sort >= selectceremony.Sort) || (a.PrepayYear > selectyear && a.PrepayCeremonyCategoryID != null))).OrderBy(o => o.Number);

                        int xfourno = (fivelastsignup == null) ? 1 : (int)fivelastsignup.Number + 1;
                        foreach (Signups signup in xfourfixedsignups)
                        {
                            Signups su = new Signups
                            {
                                SignupID = Guid.NewGuid(),
                                Year = selectyear,
                                CeremonyCategoryID = selectceremony.CeremonyCategoryID,
                                SignupType = signup.SignupType,
                                BelieverID = signup.BelieverID,
                                NumberTitle = signup.NumberTitle,
                                Number = signup.Number,
                                Fee = signup.Fee,
                                LivingNameOne = signup.LivingNameOne,
                                LivingNameTwo = signup.LivingNameTwo,
                                LivingNameThree = signup.LivingNameThree,
                                LivingNameFour = signup.LivingNameFour,
                                LivingNameFive = signup.LivingNameFive,
                                LivingNameSix = signup.LivingNameSix,
                                DeadNameOne = signup.DeadNameOne,
                                DeadNameTwo = signup.DeadNameTwo,
                                DeadNameThree = signup.DeadNameThree,
                                DeadNameFour = signup.DeadNameFour,
                                DeadNameFive = signup.DeadNameFive,
                                DeadNameSix = signup.DeadNameSix,
                                MailZipcodeID = signup.MailZipcodeID,
                                MailZipcode = signup.MailZipcode,
                                MailAddress = signup.MailAddress,
                                TextZipcodeID = signup.TextZipcodeID,
                                TextZipcode = signup.TextZipcode,
                                TextAddress = signup.TextAddress,
                                Remark = signup.Remark,
                                AdminID = Global.AdminID,
                                Createdate = DateTime.Now
                            };

                            if ((signup.PrepayYear == selectyear && signup.CeremonyCategorys1.Sort > selectceremony.Sort) || (signup.PrepayYear > selectyear))
                            {
                                su.PrepayYear = signup.PrepayYear;
                                su.PrepayCeremonyCategoryID = (Guid)signup.PrepayCeremonyCategoryID;
                            }

                            signupsService.Create(su);

                            if (xfourno != signup.Number)
                            {
                                for (int x = xfourno; x < signup.Number; x++)
                                {
                                    xfournolist.Add(x);
                                }
                                xfourno = (int)signup.Number + 1;
                            }
                            else
                            {
                                xfourno++;
                            }
                        }

                        //大殿員工郵播無固定編號
                        IQueryable<Signups> xfoursignups = signupsService.Get().Where(a => a.SignupType == 5 && a.Believers.EmployeeType == 2 && a.Believers.IsFixedNumber == false && a.Year == (int)dlSelectYear.SelectedItem && a.CeremonyCategoryID == (Guid)dlSelectCeremony.SelectedValue && a.PrepayYear != null && ((a.PrepayYear == selectyear && a.CeremonyCategorys1.Sort >= selectceremony.Sort) || (a.PrepayYear > selectyear && a.PrepayCeremonyCategoryID != null))).OrderBy(o => o.Number);

                        i = 0;
                        foreach (Signups signup in xfoursignups)
                        {
                            Signups su = new Signups
                            {
                                SignupID = Guid.NewGuid(),
                                Year = selectyear,
                                CeremonyCategoryID = selectceremony.CeremonyCategoryID,
                                SignupType = signup.SignupType,
                                BelieverID = signup.BelieverID,
                                NumberTitle = signup.NumberTitle,
                                Fee = signup.Fee,
                                LivingNameOne = signup.LivingNameOne,
                                LivingNameTwo = signup.LivingNameTwo,
                                LivingNameThree = signup.LivingNameThree,
                                LivingNameFour = signup.LivingNameFour,
                                LivingNameFive = signup.LivingNameFive,
                                LivingNameSix = signup.LivingNameSix,
                                DeadNameOne = signup.DeadNameOne,
                                DeadNameTwo = signup.DeadNameTwo,
                                DeadNameThree = signup.DeadNameThree,
                                DeadNameFour = signup.DeadNameFour,
                                DeadNameFive = signup.DeadNameFive,
                                DeadNameSix = signup.DeadNameSix,
                                MailZipcodeID = signup.MailZipcodeID,
                                MailZipcode = signup.MailZipcode,
                                MailAddress = signup.MailAddress,
                                TextZipcodeID = signup.TextZipcodeID,
                                TextZipcode = signup.TextZipcode,
                                TextAddress = signup.TextAddress,
                                Remark = signup.Remark,
                                AdminID = Global.AdminID,
                                Createdate = DateTime.Now
                            };

                            if ((signup.PrepayYear == selectyear && signup.CeremonyCategorys1.Sort > selectceremony.Sort) || (signup.PrepayYear > selectyear))
                            {
                                su.PrepayYear = signup.PrepayYear;
                                su.PrepayCeremonyCategoryID = (Guid)signup.PrepayCeremonyCategoryID;
                            }

                            if (xfournolist.Count() > 0 && xfournolist.Count() > i)
                            {
                                su.Number = xfournolist[i];
                                i++;
                            }
                            else
                            {
                                su.Number = xfourno;
                                xfourno++;
                            }

                            signupsService.Create(su);
                        }
                        break;
                    //非員工郵撥
                    case 6:
                        Signups sixlastsignup = signupsService.Get().Where(a => a.Year == selectyear && a.CeremonyCategoryID == selectceremony.CeremonyCategoryID && a.SignupType == 5).OrderByDescending(o => o.Number).FirstOrDefault();

                        //非員工郵撥有固定編號
                        List<int> fournolist = new List<int>();
                        IQueryable<Signups> fourfixedsignups = signupsService.Get().Where(a => a.SignupType == 5 && a.Believers.EmployeeType == 1 && a.Believers.IsFixedNumber == true && a.Year == (int)dlSelectYear.SelectedItem && a.CeremonyCategoryID == (Guid)dlSelectCeremony.SelectedValue && a.PrepayYear != null && ((a.PrepayYear == selectyear && a.CeremonyCategorys1.Sort >= selectceremony.Sort) || (a.PrepayYear > selectyear && a.PrepayCeremonyCategoryID != null))).OrderBy(o => o.Number);

                        int fourno = (sixlastsignup == null) ? 1 : (int)sixlastsignup.Number + 1;
                        foreach (Signups signup in fourfixedsignups)
                        {
                            Signups su = new Signups
                            {
                                SignupID = Guid.NewGuid(),
                                Year = selectyear,
                                CeremonyCategoryID = selectceremony.CeremonyCategoryID,
                                SignupType = signup.SignupType,
                                BelieverID = signup.BelieverID,
                                NumberTitle = signup.NumberTitle,
                                Number = signup.Number,
                                Fee = signup.Fee,
                                LivingNameOne = signup.LivingNameOne,
                                LivingNameTwo = signup.LivingNameTwo,
                                LivingNameThree = signup.LivingNameThree,
                                LivingNameFour = signup.LivingNameFour,
                                LivingNameFive = signup.LivingNameFive,
                                LivingNameSix = signup.LivingNameSix,
                                DeadNameOne = signup.DeadNameOne,
                                DeadNameTwo = signup.DeadNameTwo,
                                DeadNameThree = signup.DeadNameThree,
                                DeadNameFour = signup.DeadNameFour,
                                DeadNameFive = signup.DeadNameFive,
                                DeadNameSix = signup.DeadNameSix,
                                MailZipcodeID = signup.MailZipcodeID,
                                MailZipcode = signup.MailZipcode,
                                MailAddress = signup.MailAddress,
                                TextZipcodeID = signup.TextZipcodeID,
                                TextZipcode = signup.TextZipcode,
                                TextAddress = signup.TextAddress,
                                Remark = signup.Remark,
                                AdminID = Global.AdminID,
                                Createdate = DateTime.Now
                            };

                            if ((signup.PrepayYear == selectyear && signup.CeremonyCategorys1.Sort > selectceremony.Sort) || (signup.PrepayYear > selectyear))
                            {
                                su.PrepayYear = signup.PrepayYear;
                                su.PrepayCeremonyCategoryID = (Guid)signup.PrepayCeremonyCategoryID;
                            }

                            signupsService.Create(su);

                            if (fourno != signup.Number)
                            {
                                for (int x = fourno; x < signup.Number; x++)
                                {
                                    fournolist.Add(x);
                                }
                                fourno = (int)signup.Number + 1;
                            }
                            else
                            {
                                fourno++;
                            }
                        }

                        //非員工郵播無固定編號
                        IQueryable<Signups> foursignups = signupsService.Get().Where(a => a.SignupType == 5 && a.Believers.EmployeeType == 1 && a.Believers.IsFixedNumber == false && a.Year == (int)dlSelectYear.SelectedItem && a.CeremonyCategoryID == (Guid)dlSelectCeremony.SelectedValue && a.PrepayYear != null && ((a.PrepayYear == selectyear && a.CeremonyCategorys1.Sort >= selectceremony.Sort) || (a.PrepayYear > selectyear && a.PrepayCeremonyCategoryID != null))).OrderBy(o => o.Number);

                        i = 0;
                        foreach (Signups signup in foursignups)
                        {
                            Signups su = new Signups
                            {
                                SignupID = Guid.NewGuid(),
                                Year = selectyear,
                                CeremonyCategoryID = selectceremony.CeremonyCategoryID,
                                SignupType = signup.SignupType,
                                BelieverID = signup.BelieverID,
                                NumberTitle = signup.NumberTitle,
                                Fee = signup.Fee,
                                LivingNameOne = signup.LivingNameOne,
                                LivingNameTwo = signup.LivingNameTwo,
                                LivingNameThree = signup.LivingNameThree,
                                LivingNameFour = signup.LivingNameFour,
                                LivingNameFive = signup.LivingNameFive,
                                LivingNameSix = signup.LivingNameSix,
                                DeadNameOne = signup.DeadNameOne,
                                DeadNameTwo = signup.DeadNameTwo,
                                DeadNameThree = signup.DeadNameThree,
                                DeadNameFour = signup.DeadNameFour,
                                DeadNameFive = signup.DeadNameFive,
                                DeadNameSix = signup.DeadNameSix,
                                MailZipcodeID = signup.MailZipcodeID,
                                MailZipcode = signup.MailZipcode,
                                MailAddress = signup.MailAddress,
                                TextZipcodeID = signup.TextZipcodeID,
                                TextZipcode = signup.TextZipcode,
                                TextAddress = signup.TextAddress,
                                Remark = signup.Remark,
                                AdminID = Global.AdminID,
                                Createdate = DateTime.Now
                            };

                            if ((signup.PrepayYear == selectyear && signup.CeremonyCategorys1.Sort > selectceremony.Sort) || (signup.PrepayYear > selectyear))
                            {
                                su.PrepayYear = signup.PrepayYear;
                                su.PrepayCeremonyCategoryID = (Guid)signup.PrepayCeremonyCategoryID;
                            }

                            if (fournolist.Count() > 0 && fournolist.Count() > i)
                            {
                                su.Number = fournolist[i];
                                i++;
                            }
                            else
                            {
                                su.Number = fourno;
                                fourno++;
                            }

                            signupsService.Create(su);
                        }
                        break;
                }

                signupsService.SaveChanges();

                MessageBox.Show("載入預繳成功", Global.AppTitle);
            }
        }

        private void LoadBeliever()
        {
            List<SignupTypeViewModel> signuptypes = new List<SignupTypeViewModel>();
            signuptypes.Add(new SignupTypeViewModel
            {
                ID = 1,
                Title = "一般非員工"
            });
            signuptypes.Add(new SignupTypeViewModel
            {
                ID = 2,
                Title = "一般地藏殿員工"
            });
            signuptypes.Add(new SignupTypeViewModel
            {
                ID = 3,
                Title = "寺方"
            });
            signuptypes.Add(new SignupTypeViewModel
            {
                ID = 4,
                Title = "觀音會"
            });
            signuptypes.Add(new SignupTypeViewModel
            {
                ID = 5,
                Title = "郵撥大殿員工"
            });
            signuptypes.Add(new SignupTypeViewModel
            {
                ID = 6,
                Title = "郵撥非員工"
            });

            BindingSource bsSignupType = new BindingSource { DataSource = signuptypes };

            dlBeliever.DataSource = bsSignupType;
            dlBeliever.DisplayMember = "Title";
            dlBeliever.ValueMember = "ID";
        }

        private void LoadSelectCeremony()
        {
            List<CeremonyCategorys> ceremonycategorys = ceremonycategorysService.Get().Where(a => a.ParentID == null).OrderBy(o => o.Sort).ToList();

            BindingSource bsCeremonyCategory = new BindingSource { DataSource = ceremonycategorys };
            dlSelectCeremony.DataSource = bsCeremonyCategory;
            dlSelectCeremony.DisplayMember = "Title";
            dlSelectCeremony.ValueMember = "CeremonyCategoryID";

            dlSelectCeremony.SelectedIndex = -1;
            dlSelectCeremony.Text = "請選擇法會";
        }

        private void LoadCeremony()
        {
            List<CeremonyCategorys> ceremonycategorys = ceremonycategorysService.Get().Where(a => a.ParentID == null).OrderBy(o => o.Sort).ToList();

            BindingSource bsCeremonyCategory = new BindingSource { DataSource = ceremonycategorys };
            dlCeremony.DataSource = bsCeremonyCategory;
            dlCeremony.DisplayMember = "Title";
            dlCeremony.ValueMember = "CeremonyCategoryID";

            dlCeremony.SelectedIndex = -1;
            dlCeremony.Text = "請選擇法會";
        }

        private void LoadSelectYear()
        {
            int year = taiwanCalendar.GetYear(DateTime.Now);

            List<int> years = new List<int>();
            for(int y = 0; y < 5; y++)
            {
                years.Add(year - y);
            }

            BindingSource bsYear = new BindingSource { DataSource = years };
            dlSelectYear.DataSource = bsYear;

            dlSelectYear.SelectedIndex = -1;
            dlSelectYear.Text = "請選擇年份";
        }

        private void LoadYear()
        {
            int year = taiwanCalendar.GetYear(DateTime.Now);

            List<int> years = new List<int>();
            years.Add(year);
            years.Add(year + 1);

            BindingSource bsYear = new BindingSource { DataSource = years };
            dlYear.DataSource = bsYear;

            dlYear.SelectedIndex = -1;
            dlYear.Text = "請選擇年份";
        }
    }
}
