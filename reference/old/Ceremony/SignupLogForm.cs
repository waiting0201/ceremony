using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Newtonsoft.Json;

using Ceremony.Models;
using Ceremony.Service;

namespace Ceremony
{
    public partial class SignupLogForm : Form
    {
        private CeremonyEntities db;
        private SignupLogsService signuplogsService;

        private SignupForm signupForm;
        private Guid ParamSignupID;

        public SignupLogForm(SignupForm parent, Guid SignupID)
        {
            signupForm = parent;
            ParamSignupID = SignupID;

            InitializeComponent();

            db = new CeremonyEntities();
            signuplogsService = new SignupLogsService(db);

            LoadSignupLog();
        }

        private void LoadSignupLog()
        {
            List<SignupLogs> signuploglist = signuplogsService.Get().Where(a => a.SignupID == ParamSignupID).OrderByDescending(o => o.Createdate).ToList();

            BindingSource bindingSource = new BindingSource { DataSource = signuploglist };
            dgvSignup.DataSource = bindingSource;
        }
    }
}
