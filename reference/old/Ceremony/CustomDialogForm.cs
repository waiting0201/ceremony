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
    public partial class CustomDialogForm : Form
    {
        private string result;

        public CustomDialogForm()
        {
            InitializeComponent();
        }

        public string PrintFormatDialog()
        {
            this.ShowDialog();
            return result;
        }

        private void btnPDF_Click(object sender, EventArgs e)
        {
            result = "PDF";
            Close();
        }

        private void btnPreview_Click(object sender, EventArgs e)
        {
            result = "Preview";
            Close();
        }
    }
}
