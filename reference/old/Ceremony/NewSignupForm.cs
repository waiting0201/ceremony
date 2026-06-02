using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Drawing.Printing;
using System.Drawing.Imaging;
using System.Data.Entity;
using Microsoft.VisualBasic;
using Microsoft.Reporting.WinForms;

using Newtonsoft.Json;

using Ceremony.Models;
using Ceremony.Service;

namespace Ceremony
{
    public partial class NewSignupForm : Form
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
        private Guid CurrentSignupID;

        private SignupForm signupForm;

        public Guid ParamSignupID;
        public string ParamName;

        private string _Path = AppDomain.CurrentDomain.BaseDirectory;

        private PrintPreviewDialog printPreviewDialog;
        private PrintDialog printDialog;
        private PrintDocument printDocument;
        private System.Drawing.Printing.PaperSize paperSize;
        private Margins margins;

        //列印記數用的頁數紀錄
        private int m_currentPageIndex;
        private IList<Stream> m_streams;

        public NewSignupForm(SignupForm parent = null)
        {
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
            if(parent != null) signupForm = parent;

            LoadCeremony1();
            LoadSignupType();

            PanelFilterSwitch(true);
            PanelFormSwitch(false);

            Year = taiwanCalendar.GetYear(DateTime.Now);

            txtYear.Text = Year.ToString();
        }

        private void btnNextStep_Click(object sender, EventArgs e)
        {
            PanelFilterSwitch(false);
            PanelFormSwitch(true);
            PanelFormEmpty();

            LoadCity();
            LoadEmployeeType();

            txtNumber.Enabled = false;
            btnPrintDataCard.Enabled = false;

            //代入新增
            if(ParamName != null && ParamName != string.Empty)
            {
                txtQ.Text = ParamName;
                LoadBelievers();
                foreach (DataGridViewRow dgvRow in dgvBelievers.Rows)
                {
                    if (dgvRow.Cells["ColSignupID"].Value != null && (Guid)dgvRow.Cells["ColSignupID"].Value == ParamSignupID)
                    {
                        dgvRow.Selected = true;
                        BelieverSelected(dgvRow);
                        break;
                    }
                }
            }
        }

        private void btnBelieverSearch_Click(object sender, EventArgs e)
        {
            if (txtQ.Text.Trim() == "")
            {
                MessageBox.Show("請輸入信眾姓名或聯絡電話", Global.AppTitle);
                txtQ.Focus();
                return;
            }

            LoadBelievers();
        }

        private void dgvBelievers_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                dgvBelievers.ClearSelection();
                dgvBelievers.Rows[e.RowIndex].Selected = true;

                DataGridViewRow dgvRow = dgvBelievers.Rows[e.RowIndex];

                BelieverSelected(dgvRow);
            }
        }

        private void cbKeepNumber_CheckedChanged(object sender, EventArgs e)
        {
            if (cbKeepNumber.Checked)
            {
                txtNumber.Enabled = true;
            }
            else
            {
                txtNumber.Enabled = false;
            }
        }

        private void btnConfirm_Click(object sender, EventArgs e)
        {
            btnConfirm.Enabled = false;

            if (cbKeepNumber.Checked)
            {
                if (txtNumber.Text.Trim() == string.Empty)
                {
                    MessageBox.Show("請輸入編號", Global.AppTitle);
                    txtNumber.Focus();
                    btnConfirm.Enabled = true;
                    return;
                }

                int number = Convert.ToInt32(Strings.StrConv(txtNumber.Text.Trim(), VbStrConv.Narrow));
                int Y = Convert.ToInt32(txtYear.Text.Trim());
                IQueryable<Signups> signups = signupsService.Get().Where(a => a.Number == number && a.Year == Y && a.CeremonyCategoryID == (Guid)dlCeremony1.SelectedValue && a.SignupType == (int)dlSignupType.SelectedValue);

                if (signups.Any())
                {
                    MessageBox.Show(txtYear.Text.Trim() + " " + dlCeremony1.Text + " " + dlSignupType.Text + " 編號重複，請重新確認！", Global.AppTitle);
                    txtNumber.Focus();
                    btnConfirm.Enabled = true;
                    return;
                }
            }

            if (txtName.Text.Trim() == string.Empty)
            {
                MessageBox.Show("請輸入姓名", Global.AppTitle);
                txtName.Focus();
                btnConfirm.Enabled = true;
                return;
            }

            int selectedcount = dgvBelievers.SelectedRows.Count;
            Guid BelieverID;

            if (selectedcount == 0)
            {
                BelieverID = Guid.NewGuid();

                Believers believer = new Believers
                {
                    BelieverID = BelieverID,
                    EmployeeType = (int)dlEmployeeType.SelectedValue,
                    HallName = txtHallName.Text.Trim(),
                    Name = txtName.Text,
                    IsFixedNumber = cbIsFixedNumber.Checked,
                    Phone = Strings.StrConv(txtPhone.Text.Trim(), VbStrConv.Narrow),
                    MailAddress = txtMailAddress.Text.Trim(),
                    TextAddress = txtTextAddress.Text.Trim(),

                    LivingNameOne = txtLivingNameOne.Text,
                    LivingNameTwo = txtLivingNameTwo.Text,
                    LivingNameThree = txtLivingNameThree.Text,
                    LivingNameFour = txtLivingNameFour.Text,
                    LivingNameFive = txtLivingNameFive.Text,
                    LivingNameSix = txtLivingNameSix.Text,
                    DeadNameOne = txtDeadNameOne.Text,
                    DeadNameTwo = txtDeadNameTwo.Text,
                    DeadNameThree = txtDeadNameThree.Text,
                    DeadNameFour = txtDeadNameFour.Text,
                    DeadNameFive = txtDeadNameFive.Text,
                    DeadNameSix = txtDeadNameSix.Text
                };

                if (dlMailZone.SelectedValue != null && (int)dlMailZone.SelectedValue != -1) believer.MailZipcodeID = (int)dlMailZone.SelectedValue;
                if (dlTextZone.SelectedValue != null && (int)dlTextZone.SelectedValue != -1) believer.TextZipcodeID = (int)dlTextZone.SelectedValue;

                believersService.Create(believer);
                believersService.SaveChanges();
            }
            else
            {
                DataGridViewSelectedRowCollection dgvRows = dgvBelievers.SelectedRows;
                BelieverID = (Guid)dgvRows[0].Cells["ColBelieverID"].Value;
            }

            CurrentSignupID = Guid.NewGuid();

            Signups signup = new Signups();
            signup.SignupID = CurrentSignupID;
            signup.Year = Convert.ToInt32(txtYear.Text.Trim());
            signup.CeremonyCategoryID = (Guid)dlCeremony1.SelectedValue;
            if (txtFee.Text.Trim() != "") signup.Fee = Convert.ToInt32(Strings.StrConv(txtFee.Text.Trim(), VbStrConv.Narrow));
            signup.SignupType = (int)dlSignupType.SelectedValue;

            if (dlMailZone.SelectedValue != null && (int)dlMailZone.SelectedValue != -1) signup.MailZipcodeID = (int)dlMailZone.SelectedValue;
            if (dlTextZone.SelectedValue != null && (int)dlTextZone.SelectedValue != -1)
            {
                signup.TextZipcodeID = (int)dlTextZone.SelectedValue;
            }
            else if (txtTextAddress.Text == "" && (int)dlMailZone.SelectedValue != -1)
            {
                signup.TextZipcodeID = (int)dlMailZone.SelectedValue;
            }
            signup.MailZipcode = txtMailZipcode.Text.Trim();
            signup.MailAddress = txtMailAddress.Text.Trim();
            signup.TextZipcode = (txtTextZipcode.Text != "") ? txtTextZipcode.Text.Trim() : txtMailZipcode.Text.Trim();
            signup.TextAddress = (txtTextAddress.Text != "") ? txtTextAddress.Text.Trim() : txtMailAddress.Text.Trim();

            signup.Name = txtName.Text.Trim();
            signup.Phone = Strings.StrConv(txtPhone.Text.Trim(), VbStrConv.Narrow);
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

            if (cbKeepNumber.Checked)
            {
                signup.Number = Convert.ToInt32(Strings.StrConv(txtNumber.Text.Trim(), VbStrConv.Narrow));
                signup.BelieverID = BelieverID;
            }
            else
            {
                signup.Number = Library.GetSignupNumber(Convert.ToInt32(txtYear.Text.Trim()), (Guid)dlCeremony1.SelectedValue, (int)dlSignupType.SelectedValue);
                signup.BelieverID = BelieverID;
            }

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

            if (txtRemark.Text.Trim() != "") signup.Remark = txtRemark.Text.Trim();

            if (txtPrepayYear.Text.Trim() != "")
            {
                signup.PrepayYear = Convert.ToInt32(Strings.StrConv(txtPrepayYear.Text.Trim(), VbStrConv.Narrow));
                signup.PrepayCeremonyCategoryID = (Guid)dlPrepayCeremony.SelectedValue;
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
                Name = txtName.Text,
                Phone = Strings.StrConv(txtPhone.Text.Trim(), VbStrConv.Narrow),
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

            signupsService.Create(signup);
            signupsService.SaveChanges();

            signuplogsService.Create(signuplog);
            signuplogsService.SaveChanges();

            if (signupForm != null) signupForm.LoadSearchSignups();

            string resultTxt = "編號" + signup.Number + "，新增報名成功";
            CustomMessageForm message = new CustomMessageForm(resultTxt, Global.AppTitle);
            message.ShowDialog();
            //MessageBox.Show("編號" + signup.Number + "，新增報名成功", Global.AppTitle);

            btnConfirm.Enabled = true;
            btnPrintDataCard.Enabled = true;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            PanelFilterSwitch(true);
            PanelFormSwitch(false);
            PanelFormEmpty();
        }

        private void btnPrintDataCard_Click(object sender, EventArgs e)
        {
            List<DataCardViewModel> datacards = new List<DataCardViewModel>();
            SignupView item = signupviewService.Get().FirstOrDefault(a => a.SignupID == CurrentSignupID);

            datacards.Add(new DataCardViewModel
            {
                SignupID = item.SignupID,
                HallName = item.HallName.Trim(),
                Number = item.NumberTitle + "." + GetNumberText((int)item.Number),
                Prepay = (item.PrepayYear.ToString() != "") ? "預繳至" + item.PrepayYear.ToString() + "年" + item.PrepayCeremonyTitle : "",
                LivingNameOne = item.LivingNameOne.Trim(),
                LivingNameTwo = item.LivingNameTwo.Trim(),
                LivingNameThree = item.LivingNameThree.Trim(),
                LivingNameFour = item.LivingNameFour.Trim(),
                LivingNameFive = item.LivingNameFive.Trim(),
                LivingNameSix = item.LivingNameSix.Trim(),
                DeadNameOne = item.DeadNameOne.Trim(),
                DeadNameTwo = item.DeadNameTwo.Trim(),
                DeadNameThree = item.DeadNameThree.Trim(),
                DeadNameFour = item.DeadNameFour.Trim(),
                DeadNameFive = item.DeadNameFive.Trim(),
                DeadNameSix = item.DeadNameSix.Trim(),
                Address = item.TextCity + item.TextZone + item.TextAddress,
                Phone = item.Phone,
                Remark = item.Remark
            });

            PrintDataCard(datacards);

            PanelFilterSwitch(true);
            PanelFormSwitch(false);
            PanelFormEmpty();
        }

        private void dlMailCity_SelectedIndexChanged(object sender, EventArgs e)
        {
            string city = (string)dlMailCity.SelectedItem;

            List<Zipcodes> zipcodes = zipcodesService.Get().Where(a => a.City == city).OrderBy(o => o.Zipcode).ToList();
            zipcodes.Add(new Zipcodes { 
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

        private void txtPhone_Validating(object sender, CancelEventArgs e)
        {
            Regex NumberPattern = new Regex("^0[0-9]*$");
            if (txtPhone.Text.Trim() != string.Empty && !NumberPattern.IsMatch(txtPhone.Text.Trim()))
            {
                MessageBox.Show("聯絡電話格式錯誤，請重新確認！", Global.AppTitle);
                e.Cancel = true;
            }

            //if (txtPhone.Text.Trim() != string.Empty)
            //{
            //    Believers believer;
            //    int selectedcount = dgvBelievers.SelectedRows.Count;

            //    if (selectedcount == 0)
            //    {
            //        believer = believersService.Get().FirstOrDefault(a => a.Phone == txtPhone.Text.Trim());
            //    }
            //    else
            //    {
            //        DataGridViewRow dgvRow = dgvBelievers.SelectedRows[0];
            //        Guid BelieverID = (Guid)dgvRow.Cells["ColBelieverID"].Value;
            //        believer = believersService.Get().FirstOrDefault(a => a.Phone == txtPhone.Text.Trim() && a.BelieverID != BelieverID);
            //    }

            //    if (believer != null)
            //    {
            //        MessageBox.Show("聯絡電話重複，請重新確認！", Global.AppTitle);
            //        e.Cancel = true;
            //    }
            //}
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
                int number = Convert.ToInt32(Strings.StrConv(txtNumber.Text.Trim(), VbStrConv.Narrow));
                int Y = Convert.ToInt32(txtYear.Text.Trim());
                IQueryable<Signups> signups = signupsService.Get().Where(a => a.Number == number && a.Year == Y && a.CeremonyCategoryID == (Guid)dlCeremony1.SelectedValue && a.SignupType == (int)dlSignupType.SelectedValue);

                if (signups.Any())
                {
                    MessageBox.Show(txtYear.Text.Trim() + " " + dlCeremony1.Text + " " + dlSignupType.Text + " 編號重複，請重新確認！", Global.AppTitle);
                    e.Cancel = true;
                }
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

        private void LoadPrepayCeremony()
        {
            List<CeremonyCategorys> ceremonycategorys = ceremonycategorysService.Get().Where(a => a.ParentID == null).OrderBy(o => o.Sort).ToList();

            BindingSource bsPrepayCeremony = new BindingSource { DataSource = ceremonycategorys };
            dlPrepayCeremony.DataSource = bsPrepayCeremony;
            dlPrepayCeremony.DisplayMember = "Title";
            dlPrepayCeremony.ValueMember = "CeremonyCategoryID";
        }

        private void LoadBelievers()
        {
            //如果沒有報名過就查不到
            //List<SignupView> believerlist = signupviewService.Get().AsNoTracking().OrderByDescending(o => o.Year).ThenBy(o => o.CeremonySort).ThenBy(o => o.NumberTitle).ThenByDescending(o => o.Number).ToList();
            
            List<BelieverView> believerlist = believerviewService.Get().AsNoTracking().OrderByDescending(o => o.Year).ThenByDescending(o => o.CeremonySort).ThenBy(o => o.NumberTitle).ThenByDescending(o => o.Number).ToList();
            if (txtQ.Text.Trim() != "") believerlist = believerlist.Where(a => (a.Name != null && a.Name.Contains(txtQ.Text.Trim())) || (a.Phone != null && a.Phone.Contains(txtQ.Text.Trim())) || (a.LivingNameOne != null && a.LivingNameOne.Contains(txtQ.Text.Trim())) || (a.LivingNameTwo != null && a.LivingNameTwo.Contains(txtQ.Text.Trim())) || (a.LivingNameThree != null && a.LivingNameThree.Contains(txtQ.Text.Trim())) || (a.LivingNameFour != null && a.LivingNameFour.Contains(txtQ.Text.Trim())) || (a.LivingNameFive != null && a.LivingNameFive.Contains(txtQ.Text.Trim())) || (a.LivingNameSix != null && a.LivingNameSix.Contains(txtQ.Text.Trim())) || (a.DeadNameOne != null && a.DeadNameOne.Contains(txtQ.Text.Trim())) || (a.DeadNameTwo != null && a.DeadNameTwo.Contains(txtQ.Text.Trim())) || (a.DeadNameThree != null && a.DeadNameThree.Contains(txtQ.Text.Trim())) || (a.DeadNameFour != null && a.DeadNameFour.Contains(txtQ.Text.Trim())) || (a.DeadNameFive != null && a.DeadNameFive.Contains(txtQ.Text.Trim())) || (a.DeadNameSix != null && a.DeadNameSix.Contains(txtQ.Text.Trim()))).ToList();

            if (believerlist.Any())
            {
                BindingSource bindingSource = new BindingSource { DataSource = believerlist };
                dgvBelievers.DataSource = bindingSource;

                dgvBelievers.ClearSelection();
            }
            else
            {
                MessageBox.Show("無資料，請重新搜尋！", Global.AppTitle);
            }
        }

        private string GetNumberText(int Number)
        {
            string right = string.Empty;
            string left = Number.ToString().Substring(0, Number.ToString().Length - 1);
            int q = Number % 10;
            if (q == 4)
            {
                right = "3-1";
            }
            else
            {
                right = q.ToString();
            }

            return left + right;
        }

        private void PanelFormSwitch(bool isenable)
        {
            foreach (Control ctrl in plStep2.Controls)
            {
                if (ctrl is DateTimePicker)
                {
                    DateTimePicker datetimepicker = (DateTimePicker)ctrl;
                    datetimepicker.Enabled = isenable;
                }

                if (ctrl is TextBox)
                {
                    TextBox textbox = (TextBox)ctrl;
                    textbox.Enabled = isenable;
                }

                if (ctrl is Button)
                {
                    Button button = (Button)ctrl;
                    button.Enabled = isenable;
                }

                if (ctrl is CheckBox)
                {
                    CheckBox checkbox = (CheckBox)ctrl;
                    checkbox.Enabled = isenable;
                }

                if (ctrl is ComboBox)
                {
                    ComboBox combobox = (ComboBox)ctrl;
                    combobox.Enabled = isenable;
                }

                if (ctrl is DataGridView)
                {
                    DataGridView datagridview = (DataGridView)ctrl;
                    datagridview.Enabled = isenable;
                }
            }
        }

        private void PanelFilterSwitch(bool isenable)
        {
            foreach (Control ctrl in plStep1.Controls)
            {
                if(ctrl is TextBox)
                {
                    TextBox textbox = (TextBox)ctrl;
                    textbox.Enabled = isenable;
                }

                if (ctrl is Button)
                {
                    Button button = (Button)ctrl;
                    button.Enabled = isenable;
                }

                if (ctrl is ComboBox)
                {
                    ComboBox combobox = (ComboBox)ctrl;
                    combobox.Enabled = isenable;
                }
            }
        }

        private void PanelFormEmpty()
        {
            txtQ.Text = string.Empty;
            dgvBelievers.Rows.Clear();

            dlEmployeeType.DataSource = null;
            dlEmployeeType.Text = "請選擇員工";
            txtHallName.Text = string.Empty;
            txtName.Text = string.Empty;
            txtPhone.Text = string.Empty;
            dlMailCity.DataSource = null;
            dlMailCity.Text = "請選擇城市";
            dlMailZone.DataSource = null;
            dlMailZone.Text = "請選擇區域";
            txtMailAddress.Text = string.Empty;
            dlTextCity.DataSource = null;
            dlTextCity.Text = "請選擇城市";
            dlTextZone.DataSource = null;
            dlTextZone.Text = "請選擇區域";
            txtTextAddress.Text = string.Empty;
            cbSameMailAddress.Checked = false;
            txtLivingNameOne.Text = string.Empty;
            txtLivingNameTwo.Text = string.Empty;
            txtLivingNameThree.Text = string.Empty;
            txtLivingNameFour.Text = string.Empty;
            txtLivingNameFive.Text = string.Empty;
            txtLivingNameSix.Text = string.Empty;
            txtDeadNameOne.Text = string.Empty;
            txtDeadNameTwo.Text = string.Empty;
            txtDeadNameThree.Text = string.Empty;
            txtDeadNameFour.Text = string.Empty;
            txtDeadNameFive.Text = string.Empty;
            txtDeadNameSix.Text = string.Empty;

            cbKeepNumber.Checked = false;
            txtNumber.Text = string.Empty;
            txtRemark.Text = string.Empty;
            txtPrepayYear.Text = string.Empty;
            dlPrepayCeremony.DataSource = null;
            dlPrepayCeremony.Text = "請選擇預繳法會";
        }

        private void PrintDataCard(List<DataCardViewModel> datacards)
        {
            LocalReport lr = new LocalReport();
            string path = Path.Combine(_Path, "tmpDataCard.rdlc");
            lr.ReportPath = path;
            lr.EnableExternalImages = true;
            lr.DataSources.Add(new ReportDataSource("DataCardDataSet", datacards));

            string reportType = "EMF";

            string deviceInfo =
            "<DeviceInfo>" +
            "  <OutputFormat>" + reportType + "</OutputFormat>" +
            "  <PageWidth>21cm</PageWidth>" +
            "  <PageHeight>14.8cm</PageHeight>" +
            "  <MarginTop>0cm</MarginTop>" +
            "  <MarginLeft>0cm</MarginLeft>" +
            "  <MarginRight>0cm</MarginRight>" +
            "  <MarginBottom>0cm</MarginBottom>" +
            "</DeviceInfo>";
            
            Warning[] warnings;

            m_streams = new List<Stream>();

            lr.Render("Image", deviceInfo, CreateStream, out warnings);
            foreach (Stream stream in m_streams)
                stream.Position = 0;

            paperSize = new System.Drawing.Printing.PaperSize("資料卡", 794, 560);
            margins = new Margins(0, 0, 0, 0);

            printDocument = new PrintDocument();
            printDocument.DefaultPageSettings.Margins = margins;
            printDocument.DefaultPageSettings.PaperSize = paperSize;

            printDocument.BeginPrint += new PrintEventHandler(BeginPrint);
            printDocument.PrintPage += new PrintPageEventHandler(PrintPage);

            printPreviewDialog = new PrintPreviewDialog();
            printPreviewDialog.Document = printDocument;

            ToolStripButton b = new ToolStripButton();
            b.Image = ((ToolStrip)(printPreviewDialog.Controls[1])).ImageList.Images[0];
            b.DisplayStyle = ToolStripItemDisplayStyle.Image;
            b.Click += printPreview_PrintClick;
            ((ToolStrip)(printPreviewDialog.Controls[1])).Items.RemoveAt(0);
            ((ToolStrip)(printPreviewDialog.Controls[1])).Items.Insert(0, b);

            printPreviewDialog.ShowDialog();
        }

        // 提供給 the report renderer 使用, 用來建立列印用的 image stream
        private Stream CreateStream(string name, string fileNameExtension, Encoding encoding, string mimeType, bool willSeek)
        {
            Stream stream = new MemoryStream();
            m_streams.Add(stream);
            return stream;
        }

        private void BeginPrint(object sender, PrintEventArgs e)
        {
            m_currentPageIndex = 0;
        }

        // 提供給 PrintDocument 作業用的 PrintPageEvents
        private void PrintPage(object sender, PrintPageEventArgs ev)
        {
            Stream stream = m_streams[m_currentPageIndex];
            stream.Position = 0;

            Image pageImage = Image.FromStream(stream);

            // 調整繪製方塊大小同等印表機可列印空間
            Rectangle adjustedRect = new Rectangle(
                0,
                0,
                ev.PageBounds.Width,
                ev.PageBounds.Height
            );

            // 報表背景以白色塗刷
            ev.Graphics.FillRectangle(Brushes.White, adjustedRect);

            //ev.PageSettings.PaperSize = new PaperSize("Custom", ev.PageBounds.Width, ev.PageBounds.Height);
            // 繪製報表內容
            ev.Graphics.DrawImage(pageImage, adjustedRect);

            // 準備到下一頁繪製. 確保將資料繪製完畢
            m_currentPageIndex++;
            ev.HasMorePages = (m_currentPageIndex < m_streams.Count);
        }

        private void printPreview_PrintClick(object sender, EventArgs e)
        {
            try
            {
                printDialog = new PrintDialog();

                System.Drawing.Printing.PaperSize pss = null;
                //取得印表機尺寸設定
                foreach (System.Drawing.Printing.PaperSize ps in printDialog.PrinterSettings.PaperSizes)
                {
                    if (ps.PaperName == paperSize.PaperName)
                    {
                        pss = ps;
                        break;
                    }
                }

                printDialog.Document = printDocument;
                printDialog.Document.DefaultPageSettings.Margins = margins;
                printDialog.Document.DefaultPageSettings.PaperSize = pss != null ? pss : paperSize;
                //printDialog.Document.DefaultPageSettings.Landscape = isLandscape;

                printDialog.PrinterSettings.DefaultPageSettings.Margins = margins;
                printDialog.PrinterSettings.DefaultPageSettings.PaperSize = pss != null ? pss : paperSize;
                //printDialog.PrinterSettings.DefaultPageSettings.Landscape = isLandscape;

                if (printDialog.ShowDialog() == DialogResult.OK)
                {
                    printDocument.Print();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, ToString());
            }
        }

        private void BelieverSelected(DataGridViewRow dgvRow)
        {
            Guid BelieverID = (Guid)dgvRow.Cells["ColBelieverID"].Value;
            Guid SignupID = (Guid)dgvRow.Cells["ColSignupID"].Value;

            Believers believer = believersService.GetByID(BelieverID);
            Signups signup = null;
            if (ParamSignupID != Guid.Empty)
            {
                signup = signupsService.GetByID(ParamSignupID);
            }
            else if (SignupID != Guid.Empty)
            {
                signup = signupsService.GetByID(SignupID);
            }

            dlEmployeeType.SelectedValue = believer.EmployeeType;
            cbIsFixedNumber.Checked = believer.IsFixedNumber;
            txtHallName.Text = believer.HallName;

            if(signup != null && signup.Name != null)
            {
                txtName.Text = signup.Name;
            }
            else
            {
                if (dgvRow.Cells["ColName"].Value != null)
                {
                    txtName.Text = (string)dgvRow.Cells["ColName"].Value;
                }
                else
                {
                    txtName.Text = believer.Name;
                }
            }

            if(signup != null && signup.Phone != null)
            {
                txtPhone.Text = signup.Phone;
            }
            else
            {
                if (dgvRow.Cells["ColPhone"].Value != null)
                {
                    txtPhone.Text = (string)dgvRow.Cells["ColPhone"].Value;
                }
                else
                {
                    txtPhone.Text = believer.Phone;
                }
            }

            if(signup != null && signup.Zipcodes != null)
            {
                dlMailCity.SelectedItem = signup.Zipcodes.City;
                dlMailZone.SelectedValue = signup.MailZipcodeID;
                txtMailZipcode.Text = signup.MailZipcode;
                txtMailAddress.Text = signup.MailAddress;
            }
            else
            {
                if (believer.Zipcodes != null) dlMailCity.SelectedItem = believer.Zipcodes.City;
                if (believer.Zipcodes != null) dlMailZone.SelectedValue = believer.MailZipcodeID;
                if (dgvRow.Cells["ColMailZipcode"].Value != null)
                {
                    txtMailZipcode.Text = (string)dgvRow.Cells["ColMailZipcode"].Value;
                }
                else if (dgvRow.Cells["ColMailZipcode"].Value == null && believer.Zipcodes != null)
                {
                    txtMailZipcode.Text = believer.Zipcodes.Zipcode;
                }
                txtMailAddress.Text = believer.MailAddress;
            }

            if (signup != null && signup.Zipcodes1 != null)
            {
                dlTextCity.SelectedItem = signup.Zipcodes1.City;
                dlTextZone.SelectedValue = signup.TextZipcodeID;
                txtTextZipcode.Text = signup.TextZipcode;
                txtTextAddress.Text = signup.TextAddress;
            }
            else
            {
                if (believer.Zipcodes1 != null) dlTextCity.SelectedItem = believer.Zipcodes1.City;
                if (believer.Zipcodes1 != null) dlTextZone.SelectedValue = believer.TextZipcodeID;
                if (dgvRow.Cells["ColTextZipcode"].Value != null)
                {
                    txtTextZipcode.Text = (string)dgvRow.Cells["ColTextZipcode"].Value;
                }
                else if (dgvRow.Cells["ColTextZipcode"].Value == null && believer.Zipcodes1 != null)
                {
                    txtTextZipcode.Text = believer.Zipcodes1.Zipcode;
                }
                txtTextAddress.Text = believer.TextAddress;
            }
                
            txtLivingNameOne.Text = (string)dgvRow.Cells["ColLivingNameOne"].Value;
            txtLivingNameTwo.Text = (string)dgvRow.Cells["ColLivingNameTwo"].Value;
            txtLivingNameThree.Text = (string)dgvRow.Cells["ColLivingNameThree"].Value;
            txtLivingNameFour.Text = (string)dgvRow.Cells["ColLivingNameFour"].Value;
            txtLivingNameFive.Text = (string)dgvRow.Cells["ColLivingNameFive"].Value;
            txtLivingNameSix.Text = (string)dgvRow.Cells["ColLivingNameSix"].Value;
            txtDeadNameOne.Text = (string)dgvRow.Cells["ColDeadNameOne"].Value;
            txtDeadNameTwo.Text = (string)dgvRow.Cells["ColDeadNameTwo"].Value;
            txtDeadNameThree.Text = (string)dgvRow.Cells["ColDeadNameThree"].Value;
            txtDeadNameFour.Text = (string)dgvRow.Cells["ColDeadNameFour"].Value;
            txtDeadNameFive.Text = (string)dgvRow.Cells["ColDeadNameFive"].Value;
            txtDeadNameSix.Text = (string)dgvRow.Cells["ColDeadNameSix"].Value;

            txtRemark.Text = (string)dgvRow.Cells["ColRemark"].Value;

            //取得此信眾今年以前最新的報名
            int Y = Convert.ToInt32(txtYear.Text.Trim());
            IQueryable<Signups> signups = signupsService.Get().Where(a => a.BelieverID == BelieverID && a.Year <= Y).OrderByDescending(o => o.Year).ThenByDescending(o => o.CeremonyCategorys.Sort);
            if (signups.Any())
            {
                Signups latestsignup = signups.FirstOrDefault();
                //取得預繳資料
                if (latestsignup.PrepayYear != null)
                {
                    LoadPrepayCeremony();
                    txtPrepayYear.Text = latestsignup.PrepayYear.ToString();
                    dlPrepayCeremony.SelectedValue = latestsignup.PrepayCeremonyCategoryID;
                }
            }
        }
    }
}
