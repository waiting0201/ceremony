using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualBasic;

using Ceremony.Models;
using Ceremony.Service;

namespace Ceremony
{
    public partial class BelieverForm : Form
    {
        private CeremonyEntities db;
        private BelieversService believersService;
        private ZipcodesService zipcodesService;

        public BelieverForm()
        {
            InitializeComponent();

            db = new CeremonyEntities();
            believersService = new BelieversService(db);
            zipcodesService = new ZipcodesService(db);

            PanelFormSwitch(false); 
        }

        private void btnSearch_Click(object sender, EventArgs e)
        {
            if(txtSearchName.Text == "" && txtSearchHallName.Text == "" && txtSearchPhone.Text == "" && txtSearchLivingName.Text == "" && txtSearchDeadName.Text == "")
            {
                MessageBox.Show("請輸入搜尋條件", Global.AppTitle);
                return;
            }

            LoadBelievers();
        }

        private void btnNew_Click(object sender, EventArgs e)
        {
            dgvBelievers.ClearSelection();

            PanelFormEmpty();
            PanelFormSwitch(true);

            LoadCity();
            LoadEmployeeType();
        }

        private void dgvBelievers_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                dgvBelievers.ClearSelection();
                dgvBelievers.Rows[e.RowIndex].Selected = true;

                DataGridViewRow dgvRow = dgvBelievers.Rows[e.RowIndex];
                Guid BelieverID = (Guid)dgvRow.Cells["ColBelieverID"].Value;

                LoadCity();
                LoadEmployeeType();

                Believers believer = believersService.GetByID(BelieverID);
                dlEmployeeType.SelectedValue = believer.EmployeeType;
                txtHallName.Text = believer.HallName;
                txtName.Text = believer.Name;
                cbIsFixedNumber.Checked = believer.IsFixedNumber;
                txtPhone.Text = believer.Phone;
                if (believer.Zipcodes != null) dlMailCity.SelectedItem = believer.Zipcodes.City;
                if (believer.Zipcodes != null) dlMailZone.SelectedValue = believer.MailZipcodeID;
                txtMailAddress.Text = believer.MailAddress;
                if (believer.Zipcodes1 != null) dlTextCity.SelectedItem = believer.Zipcodes1.City;
                if (believer.Zipcodes1 != null) dlTextZone.SelectedValue = believer.TextZipcodeID;
                txtTextAddress.Text = believer.TextAddress;
                txtLivingNameOne.Text = believer.LivingNameOne;
                txtLivingNameTwo.Text = believer.LivingNameTwo;
                txtLivingNameThree.Text = believer.LivingNameThree;
                txtLivingNameFour.Text = believer.LivingNameFour;
                txtLivingNameFive.Text = believer.LivingNameFive;
                txtLivingNameSix.Text = believer.LivingNameSix;
                txtDeadNameOne.Text = believer.DeadNameOne;
                txtDeadNameTwo.Text = believer.DeadNameTwo;
                txtDeadNameThree.Text = believer.DeadNameThree;
                txtDeadNameFour.Text = believer.DeadNameFour;
                txtDeadNameFive.Text = believer.DeadNameFive;
                txtDeadNameSix.Text = believer.DeadNameSix;

                btnNew.Enabled = false;

                PanelFormSwitch(true);
            }
        }

        private void btnConfirm_Click(object sender, EventArgs e)
        {
            if (txtName.Text.Trim() == string.Empty)
            {
                MessageBox.Show("請輸入姓名", Global.AppTitle);
                txtName.Focus();
                return;
            }

            if (txtMailAddress.Text.Trim() == string.Empty)
            {
                MessageBox.Show("請輸入寄件地址", Global.AppTitle);
                txtMailAddress.Focus();
                return;
            }

            int selectedcount = dgvBelievers.SelectedRows.Count;

            if (selectedcount == 0)
            {
                Believers believer = new Believers
                {
                    BelieverID = Guid.NewGuid(),
                    EmployeeType = (int)dlEmployeeType.SelectedValue,
                    HallName = txtHallName.Text.Trim(),
                    Name = txtName.Text,
                    IsFixedNumber = cbIsFixedNumber.Checked,
                    Phone = Strings.StrConv(txtPhone.Text.Trim(), VbStrConv.Narrow),
                    MailAddress = txtMailAddress.Text.Trim(),
                    TextAddress = txtTextAddress.Text.Trim(),

                    LivingNameOne = txtLivingNameOne.Text.Trim(),
                    LivingNameTwo = txtLivingNameTwo.Text.Trim(),
                    LivingNameThree = txtLivingNameThree.Text.Trim(),
                    LivingNameFour = txtLivingNameFour.Text.Trim(),
                    LivingNameFive = txtLivingNameFive.Text.Trim(),
                    LivingNameSix = txtLivingNameSix.Text.Trim(),
                    DeadNameOne = txtDeadNameOne.Text.Trim(),
                    DeadNameTwo = txtDeadNameTwo.Text.Trim(),
                    DeadNameThree =txtDeadNameThree.Text.Trim(),
                    DeadNameFour = txtDeadNameFour.Text.Trim(),
                    DeadNameFive = txtDeadNameFive.Text.Trim(),
                    DeadNameSix = txtDeadNameSix.Text.Trim()
                };

                if (dlMailZone.SelectedValue != null && (int)dlMailZone.SelectedValue != -1) believer.MailZipcodeID = (int)dlMailZone.SelectedValue;
                if (dlTextZone.SelectedValue != null && (int)dlTextZone.SelectedValue != -1) believer.TextZipcodeID = (int)dlTextZone.SelectedValue;

                believersService.Create(believer);
                believersService.SaveChanges();

                MessageBox.Show("新增信眾成功！", Global.AppTitle);
            }
            else
            {
                DataGridViewRow dgvRow = dgvBelievers.SelectedRows[0];
                Believers believer = believersService.GetByID((Guid)dgvRow.Cells["ColBelieverID"].Value);
                believer.EmployeeType = (int)dlEmployeeType.SelectedValue;
                believer.HallName = txtHallName.Text.Trim();
                believer.Name = txtName.Text;
                believer.IsFixedNumber = cbIsFixedNumber.Checked;
                believer.Phone = Strings.StrConv(txtPhone.Text.Trim(), VbStrConv.Narrow);
                if (dlMailZone.SelectedValue != null && (int)dlMailZone.SelectedValue != -1) believer.MailZipcodeID = (int)dlMailZone.SelectedValue;
                believer.MailAddress = txtMailAddress.Text.Trim();
                if (dlTextZone.SelectedValue != null && (int)dlTextZone.SelectedValue != -1) believer.TextZipcodeID = (int)dlTextZone.SelectedValue;
                believer.TextAddress = txtTextAddress.Text.Trim();

                believer.LivingNameOne = txtLivingNameOne.Text.Trim();
                believer.LivingNameTwo = txtLivingNameTwo.Text.Trim();
                believer.LivingNameThree = txtLivingNameThree.Text.Trim();
                believer.LivingNameFour = txtLivingNameFour.Text.Trim();
                believer.LivingNameFive = txtLivingNameFive.Text.Trim();
                believer.LivingNameSix = txtLivingNameSix.Text.Trim();
                believer.DeadNameOne = txtDeadNameOne.Text.Trim();
                believer.DeadNameTwo = txtDeadNameTwo.Text.Trim();
                believer.DeadNameThree = txtDeadNameThree.Text.Trim();
                believer.DeadNameFour = txtDeadNameFour.Text.Trim();
                believer.DeadNameFive = txtDeadNameFive.Text.Trim();
                believer.DeadNameSix = txtDeadNameSix.Text.Trim();

                believersService.Update(believer);
                believersService.SaveChanges();

                MessageBox.Show("修改信眾成功！", Global.AppTitle);
            }

            PanelFormEmpty();
            PanelFormSwitch(false);

            LoadBelievers();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            dgvBelievers.ClearSelection();
            btnNew.Enabled = true;

            PanelFormEmpty();
            PanelFormSwitch(false);
        }

        private void dgvBelievers_RowHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                dgvBelievers.Rows[e.RowIndex].Selected = true;
                cmsBelievers.Show(dgvBelievers, dgvBelievers.PointToClient(Cursor.Position));
            }
        }

        private void tsmiDelete_Click(object sender, EventArgs e)
        {
            List<Guid> deletes = new List<Guid>();

            foreach(DataGridViewRow dgvRow in dgvBelievers.SelectedRows)
            {
                Guid BelieverID = (Guid)dgvRow.Cells["ColBelieverID"].Value;

                Believers believer = believersService.GetByID(BelieverID);
                if (believer.Signups.Any())
                {
                    MessageBox.Show(believer.Name + " 已有報名資料，不能刪除！", Global.AppTitle);
                    return;
                }
                else
                {
                    deletes.Add(BelieverID);
                }
            }

            DialogResult result = MessageBox.Show("確認刪除嗎？", Global.AppTitle, MessageBoxButtons.YesNo);
            if (result == DialogResult.Yes)
            {
                foreach(Guid BelieverID in deletes)
                {
                    believersService.Delete(BelieverID);
                    believersService.SaveChanges();
                }

                MessageBox.Show("刪除成功！", Global.AppTitle);

                PanelFormEmpty();
                PanelFormSwitch(false);
                LoadBelievers();
            }
            else
            {
                return;
            }
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

        private void cbSameMailAddress_CheckedChanged(object sender, EventArgs e)
        {
            if (cbSameMailAddress.Checked)
            {
                if (txtMailAddress.Text.Trim() != "")
                {
                    dlTextCity.SelectedIndex = dlMailCity.SelectedIndex;
                    dlTextZone.SelectedIndex = dlMailZone.SelectedIndex;
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

        private void txtPhone_Validating(object sender, CancelEventArgs e)
        {
            Regex NumberPattern = new Regex("^0[0-9]*$");
            if (txtPhone.Text.Trim() != string.Empty && !NumberPattern.IsMatch(txtPhone.Text.Trim()))
            {
                MessageBox.Show("聯絡電話格式錯誤，請重新確認！", Global.AppTitle);
                e.Cancel = true;
            }

            //if(txtPhone.Text.Trim() != string.Empty)
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

        private void LoadBelievers()
        {
            IQueryable<Believers> believers = believersService.Get();
            if (txtSearchName.Text.Trim() != "") believers = believers.Where(a => (a.Name != null && a.Name.Contains(txtSearchName.Text.Trim())));
            if (txtSearchPhone.Text.Trim() != "") believers = believers.Where(a => (a.Phone != null && a.Phone.Contains(txtSearchPhone.Text.Trim())));
            if (txtSearchHallName.Text.Trim() != "") believers = believers.Where(a => (a.HallName != null && a.HallName.Contains(txtSearchHallName.Text.Trim())));
            if (txtSearchLivingName.Text.Trim() != "") believers = believers.Where(a => (a.LivingNameOne != null && a.LivingNameOne.Contains(txtSearchLivingName.Text.Trim())) || (a.LivingNameTwo != null && a.LivingNameTwo.Contains(txtSearchLivingName.Text.Trim())) || (a.LivingNameThree != null && a.LivingNameThree.Contains(txtSearchLivingName.Text.Trim())) || (a.LivingNameFour != null && a.LivingNameFour.Contains(txtSearchLivingName.Text.Trim())) || (a.LivingNameFive != null && a.LivingNameFive.Contains(txtSearchLivingName.Text.Trim())) || (a.LivingNameSix != null && a.LivingNameSix.Contains(txtSearchLivingName.Text.Trim())));
            if (txtSearchDeadName.Text.Trim() != "") believers = believers.Where(a => (a.DeadNameOne != null && a.DeadNameOne.Contains(txtSearchDeadName.Text.Trim())) || (a.DeadNameTwo != null && a.DeadNameTwo.Contains(txtSearchDeadName.Text.Trim())) || (a.DeadNameThree != null && a.DeadNameThree.Contains(txtSearchDeadName.Text.Trim())) || (a.DeadNameFour != null && a.DeadNameFour.Contains(txtSearchDeadName.Text.Trim())) || (a.DeadNameFive != null && a.DeadNameFive.Contains(txtSearchDeadName.Text.Trim())) || (a.DeadNameSix != null && a.DeadNameSix.Contains(txtSearchDeadName.Text.Trim())));

            if (believers.Any())
            {
                List<BelieverViewModel> believerviewmodels = new List<BelieverViewModel>();

                foreach (Believers item in believers)
                {
                    believerviewmodels.Add(new BelieverViewModel
                    {
                        BelieverID = item.BelieverID,
                        EmployeeType = item.EmployeeType,
                        EmployeeTypeTitle = item.EmployeeType == 1 ? "非員工" : (item.EmployeeType == 2 ? "大殿" : "地藏殿"),
                        HallName = item.HallName,
                        Name = item.Name,
                        Phone = item.Phone,
                        MailZipcodeID = item.MailZipcodeID,
                        MailCity = item.Zipcodes != null ? item.Zipcodes.City : string.Empty,
                        MailZone = item.Zipcodes != null ? item.Zipcodes.Area : string.Empty,
                        MailAddress = item.MailAddress,
                        TextZipcodeID = item.TextZipcodeID,
                        TextCity = item.Zipcodes1 != null ? item.Zipcodes1.City : string.Empty,
                        TextZone = item.Zipcodes1 != null ? item.Zipcodes1.Area : string.Empty,
                        TextAddress = item.TextAddress,
                        LivingNameOne = item.LivingNameOne,
                        LivingNameTwo = item.LivingNameTwo,
                        LivingNameThree = item.LivingNameThree,
                        LivingNameFour = item.LivingNameFour,
                        LivingNameFive = item.LivingNameFive,
                        LivingNameSix = item.LivingNameSix,
                        DeadNameOne = item.DeadNameOne,
                        DeadNameTwo = item.DeadNameTwo,
                        DeadNameThree = item.DeadNameThree,
                        DeadNameFour = item.DeadNameFour,
                        DeadNameFive = item.DeadNameFive,
                        DeadNameSix = item.DeadNameSix
                    });
                }


                BindingSource bindingSource = new BindingSource { DataSource = believerviewmodels };
                dgvBelievers.DataSource = bindingSource;

                dgvBelievers.ClearSelection();
            }
            else
            {
                MessageBox.Show("無資料，請重新搜尋！", Global.AppTitle);
            }
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

        private void PanelFormSwitch(bool isenable)
        {
            foreach (Control ctrl in plForm.Controls)
            {
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
            }
        }

        private void PanelFormEmpty()
        {
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
        }
    }
}
