using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Ceremony.Models;

namespace Ceremony
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            if (!Global.Islogin)
            {
                LoginForm loginform = new LoginForm();
                loginform.FormClosed += LoginFormClosed;
                loginform.ShowDialog();
            }

            InitializeComponent();

            labVersion.Text = Global.Version;
        }

        public void LoginFormClosed(object sender, System.EventArgs e)
        {
            if (!Global.Islogin)
            {
                Environment.Exit(Environment.ExitCode);
            }
        }

        /// <summary>
        /// 管理者維護
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnAdmins_Click(object sender, EventArgs e)
        {
            AdminsForm adminsform = new AdminsForm();
            adminsform.Show();
        }

        /// <summary>
        /// 信眾維護
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnBeliever_Click(object sender, EventArgs e)
        {
            BelieverForm believerform = new BelieverForm();
            believerform.Show();
        }

        /// <summary>
        /// 報名維護
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSignup_Click(object sender, EventArgs e)
        {
            SignupForm signupform = new SignupForm();
            signupform.Show();
        }

        /// <summary>
        /// 新增報名
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnNewSignup_Click(object sender, EventArgs e)
        {
            NewSignupForm newsignupform = new NewSignupForm();
            newsignupform.Show();
        }

        /// <summary>
        /// 載入預繳
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnPreload_Click(object sender, EventArgs e)
        {
            LoadPrepayForm loadprepayform = new LoadPrepayForm();
            loadprepayform.Show();
        }

        private void btnBackup_Click(object sender, EventArgs e)
        {
            using (CeremonyEntities db = new CeremonyEntities())
            {
                string dbname = db.Database.Connection.Database;
                string backup = DateTime.Now.ToString("yyyyMMddHHmmssffffff") + ".bak";
                string sqlCommand = @"BACKUP DATABASE [{0}] TO DISK = N'{1}' WITH NOFORMAT, NOINIT, NAME = N'Ceremony-Full Database Backup', SKIP, NOREWIND, NOUNLOAD, STATS = 10";

                if (!Directory.Exists("D:\\Backup\\"))
                {
                    Directory.CreateDirectory("D:\\Backup\\");
                }

                db.Configuration.EnsureTransactionsForFunctionsAndCommands = false;
                db.Database.ExecuteSqlCommand(System.Data.Entity.TransactionalBehavior.DoNotEnsureTransaction, string.Format(sqlCommand, dbname, "D:\\Backup\\" + backup));
            }

            MessageBox.Show("備份完成！", Global.AppTitle);
        }
    }
}
