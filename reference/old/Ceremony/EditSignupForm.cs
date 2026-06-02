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
using Microsoft.VisualBasic;
using System.Data.Entity;
using System.Text.RegularExpressions;

using Newtonsoft.Json;

using Ceremony.Models;
using Ceremony.Service;

namespace Ceremony
{
    public partial class EditSignupForm : Form
    {
        private CeremonyEntities db;
        private CeremonyCategorysService ceremonycategorysService;
        private SignupsService signupsService;
        private BelieversService believersService;
        private ZipcodesService zipcodesService;
        private BelieverViewService believerviewService;
        private SignupViewService signupviewService;
        private SignupLogsService signuplogsService;

        private TaiwanCalendar taiwanCalendar;
        private int Year;

        private SignupForm signupForm;
        private Guid ParamSignupID;
        private Guid BelieverID;
        private string ParamName;

        public EditSignupForm(SignupForm parent, Guid SignupID, string Name)
        {
            signupForm = parent;
            ParamSignupID = SignupID;
            ParamName = Name;

            InitializeComponent();

            db = new CeremonyEntities();
            ceremonycategorysService = new CeremonyCategorysService(db);
            signupsService = new SignupsService(db);
            believersService = new BelieversService(db);
            zipcodesService = new ZipcodesService(db);
            believerviewService = new BelieverViewService(db);
            signupviewService = new SignupViewService(db);
            signuplogsService = new SignupLogsService(db);

            taiwanCalendar = new TaiwanCalendar();

            Year = taiwanCalendar.GetYear(DateTime.Now);

            LoadEmployeeType();
            LoadCeremony1();
            LoadSignupType();
            LoadCity();

            LoadBeliever();
        }

        private void EditSignupForm_Load(object sender, EventArgs e)
        {
            BelieverSelected(ParamSignupID);
        }

        private void dlBeliever_SelectedIndexChanged(object sender, EventArgs e)
        {
            BelieverID = (Guid)dlBeliever.SelectedValue;

            Believers believer = believersService.GetByID(BelieverID);
            txtHallName.Text = believer.HallName;
            dlEmployeeType.SelectedValue = believer.EmployeeType;
            txtName.Text = believer.Name;
            txtPhone.Text = believer.Phone;
            if (believer.IsFixedNumber) cbIsFixedNumber.Checked = true;
        }

        private void dlMailCity_SelectedIndexChanged(object sender, EventArgs e)
        {
            string city = (string)dlMailCity.SelectedItem;

            List<Zipcodes> zipcodes = zipcodesService.Get().Where(a => a.City == city).OrderBy(o => o.Zipcode).ToList();
            zipcodes.Add(new Zipcodes
            {
                ZipcodeID = -1,
                Zipcode = "00",
                Area = "請選擇區域"
            });

            BindingSource bsMailZone = new BindingSource { DataSource = zipcodes };
            dlMailZone.DataSource = bsMailZone;
            dlMailZone.DisplayMember = "Area";
            dlMailZone.ValueMember = "ZipcodeID";

            dlMailZone.SelectedValue = -1;
            dlMailZone.Text = "請選擇區域";
        }

        private void dlMailZone_SelectedIndexChanged(object sender, EventArgs e)
        {
            if(dlMailZone.SelectedValue != null && (int)dlMailZone.SelectedValue != -1)
            {
                int ZipcodeID = (int)dlMailZone.SelectedValue;
                Zipcodes zipcode = zipcodesService.GetByID(ZipcodeID);

                txtMailZipcode.Text = zipcode.Zipcode;
            }
            else
            {
                txtMailZipcode.Text = "";
            }
        }

        private void dlTextCity_SelectedIndexChanged(object sender, EventArgs e)
        {
            string city = (string)dlTextCity.SelectedItem;

            List<Zipcodes> zipcodes = zipcodesService.Get().Where(a => a.City == city).OrderBy(o => o.Zipcode).ToList();
            zipcodes.Add(new Zipcodes
            {
                ZipcodeID = -1,
                Zipcode = "00",
                Area = "請選擇區域"
            });

            BindingSource bsTextZone = new BindingSource { DataSource = zipcodes };
            dlTextZone.DataSource = bsTextZone;
            dlTextZone.DisplayMember = "Area";
            dlTextZone.ValueMember = "ZipcodeID";

            dlTextZone.SelectedValue = -1;
            dlTextZone.Text = "請選擇區域";
        }

        private void dlTextZone_SelectedIndexChanged(object sender, EventArgs e)
        {
            if(dlTextZone.SelectedValue != null && (int)dlTextZone.SelectedValue != -1)
            {
                int ZipcodeID = (int)dlTextZone.SelectedValue;
                Zipcodes zipcode = zipcodesService.GetByID(ZipcodeID);

                txtTextZipcode.Text = zipcode.Zipcode;
            }
            else
            {
                txtTextZipcode.Text = "";
            }
        }

        private void cbSameMailAddress_CheckedChanged(object sender, EventArgs e)
        {
            if (cbSameMailAddress.Checked)
            {
                if (txtMailAddress.Text.Trim() != "")
                {
                    dlTextCity.SelectedIndex = dlMailCity.SelectedIndex;
                    dlTextZone.SelectedIndex = dlMailZone.SelectedIndex;
                    txtTextZipcode.Text = txtMailZipcode.Text.Trim();
                    txtTextAddress.Text = txtMailAddress.Text.Trim();
                }
                else
                {
                    MessageBox.Show("請先輸入寄件地址", Global.AppTitle);
                    cbSameMailAddress.CheckState = CheckState.Unchecked;
                }
            }
            else
            {
                dlTextCity.SelectedIndex = -1;
                dlTextZone.SelectedIndex = -1;
                dlTextCity.Text = "請選擇城市";
                dlTextZone.Text = "請選擇區域";
                txtTextAddress.Text = string.Empty;
            }
        }

        private void btnConfirm_Click(object sender, EventArgs e)
        {
            btnConfirm.Enabled = false;

            if (txtYear.Text.Trim() == string.Empty)
            {
                MessageBox.Show("請輸入年份", Global.AppTitle);
                txtYear.Focus();
                btnConfirm.Enabled = true;
                return;
            }

            if (dlCeremony1.SelectedValue == null)
            {
                MessageBox.Show("請選擇法會", Global.AppTitle);
                btnConfirm.Enabled = true;
                return;
            }

            if (dlSignupType.SelectedValue == null)
            {
                MessageBox.Show("請選擇類型", Global.AppTitle);
                btnConfirm.Enabled = true;
                return;
            }

            int Year = Convert.ToInt32(txtYear.Text.Trim());
            int number = Convert.ToInt32(txtNumber.Text.Trim());

            IQueryable<Signups> signups = signupsService.Get().Where(a => a.Year == Year && a.Number == number && a.CeremonyCategoryID == (Guid)dlCeremony1.SelectedValue && a.SignupType == (int)dlSignupType.SelectedValue && a.SignupID != ParamSignupID).OrderBy(o => o.Number);

            if (signups.Any())
            {
                MessageBox.Show(Year + "年編號" + number + "重複，請重新確認！", Global.AppTitle);
                txtNumber.Focus();
                btnConfirm.Enabled = true;
                return;
            }

            Believers believer = believersService.GetByID(BelieverID);
            believer.HallName = txtHallName.Text.Trim();
            believer.EmployeeType = (int)dlEmployeeType.SelectedValue;
            //believer.Name = txtName.Text.Trim();
            //believer.Phone = txtPhone.Text.Trim();
            believer.IsFixedNumber = cbIsFixedNumber.Checked;
            believersService.SaveChanges();

            Signups signup = signupsService.GetByID(ParamSignupID);
            signup.Year = Year;
            signup.CeremonyCategoryID = (Guid)dlCeremony1.SelectedValue;
            if (txtFee.Text.Trim() != "") signup.Fee = Convert.ToInt32(Strings.StrConv(txtFee.Text.Trim(), VbStrConv.Narrow));
            signup.SignupType = (int)dlSignupType.SelectedValue;
            signup.BelieverID = BelieverID;
            signup.Number = Convert.ToInt32(Strings.StrConv(txtNumber.Text.Trim(), VbStrConv.Narrow));


            if (dlMailZone.SelectedValue != null && (int)dlMailZone.SelectedValue != -1)
            {
                signup.MailZipcodeID = (int)dlMailZone.SelectedValue;
            }
            else
            {
                signup.MailZipcodeID = null;
            }

            if (dlTextZone.SelectedValue != null && (int)dlTextZone.SelectedValue != -1)
            {
                signup.TextZipcodeID = (int)dlTextZone.SelectedValue;
            }
            else if(txtTextAddress.Text == "" && (int)dlMailZone.SelectedValue != -1)
            {
                signup.TextZipcodeID = (int)dlMailZone.SelectedValue;
            }
            else
            {
                signup.TextZipcodeID = null;
            }

            signup.MailZipcode = txtMailZipcode.Text.Trim();
            signup.MailAddress = txtMailAddress.Text.Trim();
            signup.TextZipcode = (txtTextZipcode.Text != "") ? txtTextZipcode.Text.Trim() : txtMailZipcode.Text.Trim();
            signup.TextAddress = (txtTextAddress.Text != "") ? txtTextAddress.Text.Trim() : txtMailAddress.Text.Trim();

            signup.Name = txtName.Text;
            signup.Phone = txtPhone.Text;
            signup.LivingNameOne = txtLivingNameOne.Text;
            signup.LivingNameTwo = txtLivingNameTwo.Text;
            signup.LivingNameThree = txtLivingNameThree.Text;
            signup.LivingNameFour = txtLivingNameFour.Text;
            signup.LivingNameFive = txtLivingNameFive.Text;
            signup.LivingNameSix = txtLivingNameSix.Text;
            signup.DeadNameOne = txtDeadNameOne.Text;
            signup.DeadNameTwo = txtDeadNameTwo.Text;
            signup.DeadNameThree = txtDeadNameThree.Text;
            signup.DeadNameFour = txtDeadNameFour.Text;
            signup.DeadNameFive = txtDeadNameFive.Text;
            signup.DeadNameSix = txtDeadNameSix.Text;

            switch ((int)dlSignupType.SelectedValue)
            {
                case 1:
                    signup.NumberTitle = "No";
                    break;
                case 2:
                    signup.NumberTitle = "寺";
                    break;
                case 3:
                    signup.NumberTitle = "觀";
                    break;
                case 4:
                    signup.NumberTitle = "普";
                    break;
                case 5:
                    signup.NumberTitle = "郵";
                    break;
            }

            signup.Remark = txtRemark.Text.Trim();

            if (txtPrepayYear.Text.Trim() != "")
            {
                signup.PrepayYear = Convert.ToInt32(Strings.StrConv(txtPrepayYear.Text.Trim(), VbStrConv.Narrow));
                signup.PrepayCeremonyCategoryID = (Guid)dlPrepayCeremony.SelectedValue;
            }
            else
            {
                signup.PrepayYear = null;
                signup.PrepayCeremonyCategoryID = null;
            }

            signup.AdminID = Global.AdminID;
            signup.Createdate = DateTime.Now;

            SignupLogs signuplog = new SignupLogs
            {
                SignupLogID = Guid.NewGuid(),
                SignupID = signup.SignupID,
                Year = signup.Year,
                CeremonyCategoryTitle = dlCeremony1.Text,
                SignupType = (int)dlSignupType.SelectedValue,
                HallName = txtHallName.Text,
                Name = believer.Name,
                Phone = txtPhone.Text.Trim(),
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
                MailCity = dlMailCity.Text,
                MailZone = dlMailZone.Text,
                MailAddress = txtMailAddress.Text,
                TextCity = dlTextCity.Text,
                TextZone = dlTextZone.Text,
                TextAddress = txtTextAddress.Text,
                Remark = txtRemark.Text,
                PrepayYear = signup.PrepayYear,
                PrepayCeremonyCategoryTitle = dlPrepayCeremony.Text,
                Admin = Global.Username,
                Createdate = DateTime.Now
            };

            signupsService.Update(signup);
            signupsService.SaveChanges();

            signuplogsService.Create(signuplog);
            signuplogsService.SaveChanges();

            signupForm.LoadSearchSignups();

            btnConfirm.Enabled = true;

            MessageBox.Show("修改報名成功", Global.AppTitle);
        }

        private void txtYear_Validating(object sender, CancelEventArgs e)
        {
            Regex NumberPattern = new Regex("^1[0-9]{2}$");
            if (txtYear.Text.Trim() != "" && !NumberPattern.IsMatch(txtYear.Text.Trim()))
            {
                MessageBox.Show("年份格式錯誤，請重新確認！", Global.AppTitle);
                e.Cancel = true;
            }

            if (txtYear.Text.Trim() != "" && Convert.ToInt32(txtYear.Text.Trim()) < Year)
            {
                MessageBox.Show("請勿輸入今年以前的年份", Global.AppTitle);
                e.Cancel = true;
            }
        }

        private void txtFee_Validating(object sender, CancelEventArgs e)
        {
            Regex NumberPattern = new Regex("^[0-9]*$");
            if (txtFee.Text.Trim() != "" && !NumberPattern.IsMatch(txtFee.Text.Trim()))
            {
                MessageBox.Show("費用格式錯誤，請重新確認！", Global.AppTitle);
                e.Cancel = true;
            }
        }

        private void txtNumber_Validating(object sender, CancelEventArgs e)
        {
            Regex NumberPattern = new Regex("^[1-9][0-9]*$");
            if (txtNumber.Text.Trim() != string.Empty && !NumberPattern.IsMatch(txtNumber.Text.Trim()))
            {
                MessageBox.Show("編號格式錯誤，請重新確認！", Global.AppTitle);
                e.Cancel = true;
            }

            if (txtNumber.Text.Trim() != string.Empty)
            {
                int number = Convert.ToInt32(txtNumber.Text.Trim());
                int Y = Convert.ToInt32(txtYear.Text.Trim());

                IQueryable<Signups> signups = signupsService.Get().Where(a => a.Year == Y && a.Number == number && a.CeremonyCategoryID == (Guid)dlCeremony1.SelectedValue && a.SignupType == (int)dlSignupType.SelectedValue && a.SignupID != ParamSignupID).OrderBy(o => o.Number);

                if (signups.Any())
                {
                    MessageBox.Show(Y + "年編號" + number + "重複，請重新確認！", Global.AppTitle);
                    e.Cancel = true;
                }
            }
        }

        private void txtPrepayYear_Validating(object sender, CancelEventArgs e)
        {
            Regex NumberPattern = new Regex("^1[0-9]{2}$");
            if (txtPrepayYear.Text.Trim() != "" && !NumberPattern.IsMatch(txtPrepayYear.Text.Trim()))
            {
                dlPrepayCeremony.DataSource = null;
                dlPrepayCeremony.Text = "請選擇預繳法會";

                MessageBox.Show("預繳年份格式錯誤，請重新確認！", Global.AppTitle);
                e.Cancel = true;
            }
            else if (txtPrepayYear.Text.Trim() != "")
            {
                if (Convert.ToInt32(txtPrepayYear.Text.Trim()) >= Year)
                {
                    LoadPrepayCeremony();
                }
                else
                {
                    MessageBox.Show("預繳年份需大於" + Year + "，請重新確認！", Global.AppTitle);
                    e.Cancel = true;
                }
            }
            else
            {
                dlPrepayCeremony.DataSource = null;
                dlPrepayCeremony.Text = "請選擇預繳法會";
            }
        }

        private void LoadBeliever()
        {
            List<Believers> believers = believersService.Get().ToList();

            BindingSource bsBeliever = new BindingSource { DataSource = believers };
            dlBeliever.DataSource = bsBeliever;
            dlBeliever.DisplayMember = "Name";
            dlBeliever.ValueMember = "BelieverID";

            dlBeliever.AutoCompleteSource = AutoCompleteSource.ListItems;
            dlBeliever.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        }

        private void LoadCeremony1()
        {
            List<CeremonyCategorys> ceremonycategorys = ceremonycategorysService.Get().Where(a => a.ParentID == null).OrderBy(o => o.Sort).ToList();

            BindingSource bsCeremonyCategory = new BindingSource { DataSource = ceremonycategorys };
            dlCeremony1.DataSource = bsCeremonyCategory;
            dlCeremony1.DisplayMember = "Title";
            dlCeremony1.ValueMember = "CeremonyCategoryID";
        }

        private void LoadSignupType()
        {
            List<SignupTypeViewModel> signuptypes = new List<SignupTypeViewModel>();
            signuptypes.Add(new SignupTypeViewModel
            {
                ID = 1,
                Title = "一般"
            });
            signuptypes.Add(new SignupTypeViewModel
            {
                ID = 2,
                Title = "寺方"
            });
            signuptypes.Add(new SignupTypeViewModel
            {
                ID = 3,
                Title = "觀音會"
            });
            signuptypes.Add(new SignupTypeViewModel
            {
                ID = 4,
                Title = "普桌"
            });
            signuptypes.Add(new SignupTypeViewModel
            {
                ID = 5,
                Title = "郵撥"
            });

            BindingSource bsSignupType = new BindingSource { DataSource = signuptypes };

            dlSignupType.DataSource = bsSignupType;
            dlSignupType.DisplayMember = "Title";
            dlSignupType.ValueMember = "ID";
        }

        private void LoadCity()
        {
            List<string> citys = zipcodesService.Get().GroupBy(g => g.City).OrderBy(o => o.Key).Select(s => s.Key).ToList();
            citys.Add("請選擇城市");

            BindingSource bsMailCity = new BindingSource { DataSource = citys };
            BindingSource bsTextCity = new BindingSource { DataSource = citys };
            dlMailCity.DataSource = bsMailCity;
            dlTextCity.DataSource = bsTextCity;

            dlMailCity.SelectedIndex = -1;
            dlMailCity.Text = "請選擇城市";

            dlTextCity.SelectedIndex = -1;
            dlTextCity.Text = "請選擇城市";
        }

        private void LoadPrepayCeremony()
        {
            List<CeremonyCategorys> ceremonycategorys = ceremonycategorysService.Get().Where(a => a.ParentID == null).OrderBy(o => o.Sort).ToList();

            BindingSource bsPrepayCeremony = new BindingSource { DataSource = ceremonycategorys };
            dlPrepayCeremony.DataSource = bsPrepayCeremony;
            dlPrepayCeremony.DisplayMember = "Title";
            dlPrepayCeremony.ValueMember = "CeremonyCategoryID";
        }

        private void LoadEmployeeType()
        {
            List<EmployeeTypeViewModel> employeetypes = new List<EmployeeTypeViewModel>();
            employeetypes.Add(new EmployeeTypeViewModel
            {
                ID = 1,
                Title = "非員工"
            });
            employeetypes.Add(new EmployeeTypeViewModel
            {
                ID = 2,
                Title = "大殿"
            });
            employeetypes.Add(new EmployeeTypeViewModel
            {
                ID = 3,
                Title = "地藏殿"
            });

            BindingSource bsEmployeeType = new BindingSource { DataSource = employeetypes };

            dlEmployeeType.DataSource = bsEmployeeType;
            dlEmployeeType.DisplayMember = "Title";
            dlEmployeeType.ValueMember = "ID";
        }

        private void BelieverSelected(Guid SignupID)
        {
            Signups signup = signupsService.GetByID(SignupID);
            BelieverID = (Guid)signup.BelieverID;

            dlBeliever.SelectedValue = BelieverID;

            if (signup.Zipcodes != null) dlMailCity.SelectedItem = signup.Zipcodes.City;
            if (signup.Zipcodes != null) dlMailZone.SelectedValue = signup.MailZipcodeID;
            txtMailZipcode.Text = signup.MailZipcode;
            txtMailAddress.Text = signup.MailAddress;

            if (signup.Zipcodes1 != null) dlTextCity.SelectedItem = signup.Zipcodes1.City;
            if (signup.Zipcodes1 != null) dlTextZone.SelectedValue = signup.TextZipcodeID;
            txtTextZipcode.Text = signup.TextZipcode;
            txtTextAddress.Text = signup.TextAddress;

            txtName.Text = ParamName;
            txtNumber.Text = signup.Number.ToString();
            txtYear.Text = signup.Year.ToString();
            txtFee.Text = signup.Fee.ToString();
            dlCeremony1.SelectedValue = signup.CeremonyCategoryID;
            dlSignupType.SelectedValue = signup.SignupType;

            dlEmployeeType.SelectedValue = signup.Believers.EmployeeType;
            txtHallName.Text = signup.Believers.HallName;
            if(signup.Phone != null)
            {
                txtPhone.Text = signup.Phone;
            }
            else
            {
                txtPhone.Text = signup.Believers.Phone;
            }
            if (signup.Believers.IsFixedNumber) cbIsFixedNumber.Checked = true;

            txtLivingNameOne.Text = signup.LivingNameOne;
            txtLivingNameTwo.Text = signup.LivingNameTwo;
            txtLivingNameThree.Text = signup.LivingNameThree;
            txtLivingNameFour.Text = signup.LivingNameFour;
            txtLivingNameFive.Text = signup.LivingNameFive;
            txtLivingNameSix.Text = signup.LivingNameSix;
            txtDeadNameOne.Text = signup.DeadNameOne;
            txtDeadNameTwo.Text = signup.DeadNameTwo;
            txtDeadNameThree.Text = signup.DeadNameThree;
            txtDeadNameFour.Text = signup.DeadNameFour;
            txtDeadNameFive.Text = signup.DeadNameFive;
            txtDeadNameSix.Text = signup.DeadNameSix;

            txtRemark.Text = signup.Remark;

            if (signup.PrepayYear != null && signup.PrepayYear.ToString() != "")
            {
                LoadPrepayCeremony();
                txtPrepayYear.Text = signup.PrepayYear.ToString();
                dlPrepayCeremony.SelectedValue = signup.PrepayCeremonyCategoryID;
            }

            //年份為之前年份時，不可改預繳
            if (signup.Year < taiwanCalendar.GetYear(DateTime.Now))
            {
                txtPrepayYear.Enabled = false;
                dlPrepayCeremony.Enabled = false;
            }
        }
    }
}
