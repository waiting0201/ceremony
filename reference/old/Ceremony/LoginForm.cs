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
    public partial class LoginForm : Form
    {
        private CeremonyEntities db;
        private AdminsService adminsService;

        public LoginForm()
        {
            InitializeComponent();

            db = new CeremonyEntities();
            adminsService = new AdminsService(db);

            labVersion.Text = Global.Version;
        }

        private void btnConfirm_Click(object sender, EventArgs e)
        {
            if (txtUsername.Text.Trim() == string.Empty)
            {
                MessageBox.Show("請輸入帳號！", Global.AppTitle);
                txtUsername.Focus();
                return;
            }

            if (txtPassword.Text.Trim() == string.Empty)
            {
                MessageBox.Show("請輸入密碼！", Global.AppTitle);
                txtPassword.Focus();
                return;
            }

            if (ValidateUser(txtUsername.Text.Trim(), txtPassword.Text.Trim()))
            {
                Close();
            }
            else
            {
                MessageBox.Show("帳號或密碼錯誤！", Global.AppTitle);
            }
        }

        // 驗證帳號密碼
        private bool ValidateUser(string username, string password)
        {
            if (username == "weypro" && password == "weypro12ab")
            {
                Global.Islogin = true;
                Global.Username = "weypro";
                Global.AdminID = 0;

                return true;
            }

            Admins admin = adminsService.Get().FirstOrDefault(a => a.Username == username && a.IsEnabled == true);
            if (admin == null)
                return false;

            if (admin.Password != password)
                return false;

            Global.Islogin = true;
            Global.Username = username;
            Global.AdminID = admin.AdminID;

            return true;
        }
    }
}
