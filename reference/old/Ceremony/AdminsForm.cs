using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Ceremony.Models;
using Ceremony.Service;

namespace Ceremony
{
    public partial class AdminsForm : Form
    {
        private CeremonyEntities db;
        private AdminsService adminsService;

        public AdminsForm()
        {
            InitializeComponent();

            db = new CeremonyEntities();
            adminsService = new AdminsService(db);

            PanelFormSwitch(false);

            LoadAdmins();
        }

        /// <summary>
        /// Enter鍵擁有Tab鍵的功能
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="keyData"></param>
        /// <returns></returns>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter)
            {
                SendKeys.Send("{TAB}");
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void btnNew_Click(object sender, EventArgs e)
        {
            dgvAdmins.ClearSelection();

            PanelFormEmpty();
            PanelFormSwitch(true);
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            dgvAdmins.ClearSelection();

            PanelFormEmpty();
            PanelFormSwitch(false);
        }

        private void dgvAdmins_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                dgvAdmins.ClearSelection();
                dgvAdmins.Rows[e.RowIndex].Selected = true;

                DataGridViewRow dgvRow = dgvAdmins.Rows[e.RowIndex];
                int AdminID = Convert.ToInt32(dgvRow.Cells["ColAdminID"].Value);

                Admins admin = adminsService.GetByID(AdminID);
                txtUsername.Text = admin.Username;
                txtPassword.Text = admin.Password;
                txtConfirmPassword.Text = admin.Password;
                txtName.Text = admin.Name;

                PanelFormSwitch(true);

                txtUsername.Enabled = false;
            }
        }

        private void btnConfirm_Click(object sender, EventArgs e)
        {
            int selectedcount = dgvAdmins.SelectedRows.Count;

            if (selectedcount == 0)
            {
                Admins admin = new Admins {
                    Username = txtUsername.Text.Trim(),
                    Password = txtPassword.Text.Trim(),
                    Name = txtName.Text.Trim(),
                    IsEnabled = true
                };

                adminsService.Create(admin);
                adminsService.SaveChanges();

                MessageBox.Show("新增帳號成功！", Global.AppTitle);
            }
            else
            {
                DataGridViewRow dgvRow = dgvAdmins.SelectedRows[0];
                Admins admin = adminsService.GetByID((int)dgvRow.Cells["ColAdminID"].Value);
                admin.Name = txtName.Text.Trim();
                admin.Password = txtPassword.Text.Trim();

                adminsService.Update(admin);
                adminsService.SaveChanges();

                MessageBox.Show("修改帳號成功！", Global.AppTitle);
            }

            PanelFormEmpty();
            PanelFormSwitch(false);
            LoadAdmins();
        }

        private void dgvAdmins_RowHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                dgvAdmins.ClearSelection();
                dgvAdmins.Rows[e.RowIndex].Selected = true;
                cmsAdmins.Show(dgvAdmins, dgvAdmins.PointToClient(Cursor.Position));
            }
        }

        private void tsmiDelete_Click(object sender, EventArgs e)
        {
            DataGridViewRow dgvRow = dgvAdmins.SelectedRows[0];
            int AdminID = Convert.ToInt32(dgvRow.Cells["ColAdminID"].Value);
            string Username = (string)dgvRow.Cells["ColUsername"].Value;

            DialogResult result = MessageBox.Show("確認刪除 " + Username + " 嗎？", Global.AppTitle, MessageBoxButtons.YesNo);
            if (result == DialogResult.Yes)
            {
                Admins admin = adminsService.GetByID(AdminID);
                admin.IsEnabled = false;

                adminsService.SaveChanges();

                MessageBox.Show("刪除成功！", Global.AppTitle);

                PanelFormEmpty();
                PanelFormSwitch(false);
                LoadAdmins();
            }
            else
            {
                return;
            }
        }

        private void txtUsername_Validating(object sender, CancelEventArgs e)
        {
            if (txtUsername.Text.Trim() == string.Empty)
            {
                MessageBox.Show("請輸入帳號", Global.AppTitle);
                e.Cancel = true;
            }

            Admins admin;
            int selectedcount = dgvAdmins.SelectedRows.Count;

            if (selectedcount == 0)
            {
                admin = adminsService.Get().FirstOrDefault(a => a.Username == txtUsername.Text.Trim());
            }
            else
            {
                DataGridViewRow dgvRow = dgvAdmins.SelectedRows[0];

                admin = adminsService.Get().FirstOrDefault(a => a.Username == txtUsername.Text.Trim() && a.AdminID != (int)dgvRow.Cells["ColAdminID"].Value);
            }
                
            if (admin != null)
            {
                MessageBox.Show("帳號重複，請重新確認！", Global.AppTitle);
                e.Cancel = true;
            }
        }

        private void txtPassword_Validating(object sender, CancelEventArgs e)
        {
            if (txtPassword.Text.Trim() == string.Empty)
            {
                MessageBox.Show("請輸入密碼", Global.AppTitle);
                e.Cancel = true;
            }
        }

        private void txtConfirmPassword_Validating(object sender, CancelEventArgs e)
        {
            if (txtConfirmPassword.Text.Trim() != txtPassword.Text.Trim())
            {
                MessageBox.Show("確認密碼輸入錯誤", Global.AppTitle);
                e.Cancel = true;
            }
        }

        private void LoadAdmins()
        {
            List<Admins> admins = adminsService.Get().ToList();

            BindingSource bindingSource = new BindingSource { DataSource = admins };
            dgvAdmins.DataSource = bindingSource;
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
            }
        }

        private void PanelFormEmpty()
        {
            txtUsername.Text = string.Empty;
            txtPassword.Text = string.Empty;
            txtConfirmPassword.Text = string.Empty;
            txtName.Text = string.Empty;
        }
    }
}
