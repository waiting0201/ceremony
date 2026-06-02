using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Ceremony
{
    public partial class CustomMessageForm : Form
    {
        public CustomMessageForm(string text, string title)
        {
            InitializeComponent();
            labMessage.Text = text;
            this.Text = title;
            btnClose.Focus();
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
