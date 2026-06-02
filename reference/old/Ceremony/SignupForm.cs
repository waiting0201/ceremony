using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Globalization;
using System.Data.Entity;
using Microsoft.VisualBasic;
using Microsoft.Reporting.WinForms;
using System.Drawing.Printing;
using System.Drawing.Imaging;

using LinqKit;

using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;

using Ceremony.Models;
using Ceremony.Service;

namespace Ceremony
{
    public partial class SignupForm : Form
    {
        private CeremonyEntities db;
        private CeremonyCategorysService ceremonycategorysService;
        private SignupsService signupsService;
        private SignupViewService signupviewService;

        private TaiwanCalendar taiwanCalendar;

        private string _Path = AppDomain.CurrentDomain.BaseDirectory;

        private PrintPreviewDialog printPreviewDialog;
        private PrintDialog printDialog;
        private PrintDocument printDocument;
        private System.Drawing.Printing.PaperSize paperSize;
        private Margins margins;
        //private bool isLandscape;

        //列印記數用的頁數紀錄
        private int m_currentPageIndex;
        //列印用串流資料儲存區
        private IList<Stream> m_streams;

        public SignupForm()
        {
            InitializeComponent();

            db = new CeremonyEntities();
            ceremonycategorysService = new CeremonyCategorysService(db);
            signupsService = new SignupsService(db);

            taiwanCalendar = new TaiwanCalendar();

            LoadCeremony();
            LoadPrintType();
            LoadSignupType();
        }

        private void btnSearch_Click(object sender, EventArgs e)
        {
            LoadSearchSignups();
        }

        private void btnNew_Click(object sender, EventArgs e)
        {
            int selectedcount = dgvSignups.SelectedRows.Count;

            NewSignupForm newsignupform = new NewSignupForm(this);
            if (selectedcount > 0) {
                DataGridViewRow dgvRow = dgvSignups.SelectedRows[0];
                Guid SignupID = (Guid)dgvRow.Cells["ColSignupID"].Value;
                string Name = (string)dgvRow.Cells["ColName"].Value;

                newsignupform.ParamSignupID = SignupID;
                newsignupform.ParamName = Name;
            }
            newsignupform.ShowDialog();
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            int selectedcount = dgvSignups.SelectedRows.Count;
            if (selectedcount == 0)
            {
                MessageBox.Show("尚未選擇報名資料", Global.AppTitle);
                return;
            }
            else
            {
                DataGridViewRow dgvRow = dgvSignups.SelectedRows[0];
                Guid SignupID = (Guid)dgvRow.Cells["ColSignupID"].Value;
                string Name = (dgvRow.Cells["ColName"].Value != null) ? (string)dgvRow.Cells["ColName"].Value : "";

                EditSignupForm editsignupform = new EditSignupForm(this, SignupID, Name);

                editsignupform.ShowDialog();
            }
        }

        private void dgvSignups_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                dgvSignups.ClearSelection();
                dgvSignups.Rows[e.RowIndex].Selected = true;
            }
        }

        private void dgvSignups_RowHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                dgvSignups.Rows[e.RowIndex].Selected = true;
                
                if(dgvSignups.SelectedRows.Count == 1)
                {
                    tsmiAdd.Enabled = true;
                    tsmiEdit.Enabled = true;
                }
                else
                {
                    tsmiAdd.Enabled = false;
                    tsmiEdit.Enabled = false;
                }

                if((int)dlSearchSignupType.SelectedValue == 4)
                {
                    tsmiPrintWorship.Enabled = true;
                }
                else
                {
                    tsmiPrintWorship.Enabled = false;
                }

                cmsSignups.Show(dgvSignups, dgvSignups.PointToClient(Cursor.Position));
            }
        }

        private void tsmiAdd_Click(object sender, EventArgs e)
        {
            int selectedcount = dgvSignups.SelectedRows.Count;

            NewSignupForm newsignupform = new NewSignupForm(this);
            if (selectedcount > 0)
            {
                DataGridViewRow dgvRow = dgvSignups.SelectedRows[0];
                Guid SignupID = (Guid)dgvRow.Cells["ColSignupID"].Value;
                string Name = (string)dgvRow.Cells["ColName"].Value;

                newsignupform.ParamSignupID = SignupID;
                newsignupform.ParamName = Name;
            }
            newsignupform.ShowDialog();
        }

        private void tsmiEdit_Click(object sender, EventArgs e)
        {
            int selectedcount = dgvSignups.SelectedRows.Count;
            if (selectedcount == 0)
            {
                MessageBox.Show("尚未選擇報名資料", Global.AppTitle);
                return;
            }
            else
            {
                DataGridViewRow dgvRow = dgvSignups.SelectedRows[0];
                Guid SignupID = (Guid)dgvRow.Cells["ColSignupID"].Value;
                string Name = (dgvRow.Cells["ColName"].Value != null) ? (string)dgvRow.Cells["ColName"].Value : "";

                EditSignupForm editsignupform = new EditSignupForm(this, SignupID, Name);

                editsignupform.ShowDialog();
            }
        }

        private void tsmiPrintDataCard_Click(object sender, EventArgs e)
        {
            string printformat;
            CustomDialogForm customDialogForm = new CustomDialogForm();
            printformat = customDialogForm.PrintFormatDialog();

            List<DataCardViewModel> datacards = new List<DataCardViewModel>();
            foreach (DataGridViewRow dgvRow in dgvSignups.SelectedRows)
            {
                string HallName = dgvRow.Cells["ColHallName"].Value != null ? dgvRow.Cells["ColHallName"].Value.ToString() : "";
                string LivingNameOne = dgvRow.Cells["ColLivingNameOne"].Value != null ? dgvRow.Cells["ColLivingNameOne"].Value.ToString() : "";
                string LivingNameTwo = dgvRow.Cells["ColLivingNameTwo"].Value != null ? dgvRow.Cells["ColLivingNameTwo"].Value.ToString() : "";
                string LivingNameThree = dgvRow.Cells["ColLivingNameThree"].Value != null ? dgvRow.Cells["ColLivingNameThree"].Value.ToString() : "";
                string LivingNameFour = dgvRow.Cells["ColLivingNameFour"].Value != null ? dgvRow.Cells["ColLivingNameFour"].Value.ToString() : "";
                string LivingNameFive = dgvRow.Cells["ColLivingNameFive"].Value != null ? dgvRow.Cells["ColLivingNameFive"].Value.ToString() : "";
                string LivingNameSix = dgvRow.Cells["ColLivingNameSix"].Value != null ? dgvRow.Cells["ColLivingNameSix"].Value.ToString() : "";
                string DeadNameOne = dgvRow.Cells["ColDeadNameOne"].Value != null ? dgvRow.Cells["ColDeadNameOne"].Value.ToString() : "";
                string DeadNameTwo = dgvRow.Cells["ColDeadNameTwo"].Value != null ? dgvRow.Cells["ColDeadNameTwo"].Value.ToString() : "";
                string DeadNameThree = dgvRow.Cells["ColDeadNameThree"].Value != null ? dgvRow.Cells["ColDeadNameThree"].Value.ToString() : "";
                string DeadNameFour = dgvRow.Cells["ColDeadNameFour"].Value != null ? dgvRow.Cells["ColDeadNameFour"].Value.ToString() : "";
                string DeadNameFive = dgvRow.Cells["ColDeadNameFive"].Value != null ? dgvRow.Cells["ColDeadNameFive"].Value.ToString() : "";
                string DeadNameSix = dgvRow.Cells["ColDeadNameSix"].Value != null ? dgvRow.Cells["ColDeadNameSix"].Value.ToString() : "";

                string MailCity = dgvRow.Cells["ColTextCity"].Value != null ? dgvRow.Cells["ColTextCity"].Value.ToString() : "";
                string MailZone = dgvRow.Cells["ColTextZone"].Value != null ? dgvRow.Cells["ColTextZone"].Value.ToString() : "";
                string MailAddress = dgvRow.Cells["ColTextAddress"].Value != null ? dgvRow.Cells["ColTextAddress"].Value.ToString() : "";

                datacards.Add(new DataCardViewModel
                {
                    SignupID = (Guid)dgvRow.Cells["ColSignupID"].Value,
                    HallName = HallName.Trim(),
                    Number = dgvRow.Cells["ColNumberTitle"].Value.ToString() + " " + GetNumberText((int)dgvRow.Cells["ColNumber"].Value),
                    Prepay = dgvRow.Cells["ColPrepayYear"].Value != null ? "預繳至" + dgvRow.Cells["ColPrepayYear"].Value.ToString() + "年" + dgvRow.Cells["ColPrepayCeremonyTitle"].Value.ToString() : "",
                    LivingNameOne = LivingNameOne.Trim(),
                    LivingNameTwo = LivingNameTwo.Trim(),
                    LivingNameThree = LivingNameThree.Trim(),
                    LivingNameFour = LivingNameFour.Trim(),
                    LivingNameFive = LivingNameFive.Trim(),
                    LivingNameSix = LivingNameSix.Trim(),
                    DeadNameOne = DeadNameOne.Trim(),
                    DeadNameTwo = DeadNameTwo.Trim(),
                    DeadNameThree = DeadNameThree.Trim(),
                    DeadNameFour = DeadNameFour.Trim(),
                    DeadNameFive = DeadNameFive.Trim(),
                    DeadNameSix = DeadNameSix.Trim(),
                    Address = MailCity + MailZone + MailAddress,
                    Phone = dgvRow.Cells["ColPhone"].Value != null ? dgvRow.Cells["ColPhone"].Value.ToString() : "",
                    Remark = dgvRow.Cells["ColRemark"].Value != null ? dgvRow.Cells["ColRemark"].Value.ToString() : ""
                });
            }

            PrintDataCard(datacards, printformat);
        }

        private void tsmiPrintReceipt_Click(object sender, EventArgs e)
        {
            string printformat;
            CustomDialogForm customDialogForm = new CustomDialogForm();
            printformat = customDialogForm.PrintFormatDialog();

            List<ReceiptViewModel> receipts = new List<ReceiptViewModel>();
            foreach (DataGridViewRow dgvRow in dgvSignups.SelectedRows)
            {
                string MailCity = dgvRow.Cells["ColMailCity"].Value != null ? dgvRow.Cells["ColMailCity"].Value.ToString() : "";
                string MailZone = dgvRow.Cells["ColMailZone"].Value != null ? dgvRow.Cells["ColMailZone"].Value.ToString() : "";
                string MailAddress = dgvRow.Cells["ColMailAddress"].Value != null ? dgvRow.Cells["ColMailAddress"].Value.ToString() : "";

                receipts.Add(new ReceiptViewModel
                {
                    SignupID = (Guid)dgvRow.Cells["ColSignupID"].Value,
                    Name = dgvRow.Cells["ColName"].Value.ToString(),
                    Zipcode = dgvRow.Cells["ColMailZipcode"].Value != null ? dgvRow.Cells["ColMailZipcode"].Value.ToString() : "",
                    Address = MailCity + MailZone + MailAddress,
                    Fee = dgvRow.Cells["ColFee"].Value != null ? Convert.ToInt32((int)dgvRow.Cells["ColFee"].Value).ToString("N0") : "",
                    Number = GetNumberText((int)dgvRow.Cells["ColNumber"].Value),
                    Year = taiwanCalendar.GetYear(DateTime.Now).ToString(),
                    Month = DateTime.Now.Month.ToString(),
                    Day = DateTime.Now.Day.ToString(),
                    Prepay = dgvRow.Cells["ColPrepayYear"].Value != null ? "預繳至" + dgvRow.Cells["ColPrepayYear"].Value.ToString() + "年" + dgvRow.Cells["ColPrepayCeremonyTitle"].Value.ToString() : ""
                });
            }

            PrintReceipt(receipts, printformat);
        }

        private void tsmiPrintTablet_Click(object sender, EventArgs e)
        {
            string printformat;
            CustomDialogForm customDialogForm = new CustomDialogForm();
            printformat = customDialogForm.PrintFormatDialog();

            List<TabletViewModel> tablets = new List<TabletViewModel>();
            foreach (DataGridViewRow dgvRow in dgvSignups.SelectedRows)
            {
                string HallNameFirst = string.Empty;
                string HallNameSecond = string.Empty;

                if(dgvRow.Cells["ColHallName"].Value != null && dgvRow.Cells["ColHallName"].Value.ToString() != "")
                {
                    if(dgvRow.Cells["ColHallName"].Value.ToString().Length == 2)
                    {
                        HallNameFirst = dgvRow.Cells["ColHallName"].Value.ToString().Substring(0, 1).Replace("-", "").Trim();
                        HallNameSecond = dgvRow.Cells["ColHallName"].Value.ToString().Substring(1, 1).Replace("-", "").Trim();
                    }
                    else if(dgvRow.Cells["ColHallName"].Value.ToString().Length == 4)
                    {
                        HallNameFirst = dgvRow.Cells["ColHallName"].Value.ToString().Substring(0, 2).Replace("-", "").Trim();
                        HallNameSecond = dgvRow.Cells["ColHallName"].Value.ToString().Substring(2, 2).Replace("-", "").Trim();
                    }
                }

                tablets.Add(new TabletViewModel
                {
                    SignupID = (Guid)dgvRow.Cells["ColSignupID"].Value,
                    Number = (dgvRow.Cells["ColSignupType"].Value.ToString() == "2") ? dgvRow.Cells["ColNumberTitle"].Value.ToString() : dgvRow.Cells["ColNumberTitle"].Value.ToString() + GetNumberText((int)dgvRow.Cells["ColNumber"].Value),
                    HallNameFirst = HallNameFirst,
                    HallNameSecond = HallNameSecond,
                    LivingNameOne = dgvRow.Cells["ColLivingNameOne"].Value != null ? dgvRow.Cells["ColLivingNameOne"].Value.ToString() : "",
                    LivingNameTwo = dgvRow.Cells["ColLivingNameTwo"].Value != null ? dgvRow.Cells["ColLivingNameTwo"].Value.ToString() : "",
                    LivingNameThree = dgvRow.Cells["ColLivingNameThree"].Value != null ? dgvRow.Cells["ColLivingNameThree"].Value.ToString() : "",
                    LivingNameFour = dgvRow.Cells["ColLivingNameFour"].Value != null ? dgvRow.Cells["ColLivingNameFour"].Value.ToString() : "",
                    LivingNameFive = dgvRow.Cells["ColLivingNameFive"].Value != null ? dgvRow.Cells["ColLivingNameFive"].Value.ToString() : "",
                    LivingNameSix = dgvRow.Cells["ColLivingNameSix"].Value != null ? dgvRow.Cells["ColLivingNameSix"].Value.ToString() : "",
                    DeadNameOne = dgvRow.Cells["ColDeadNameOne"].Value != null ? dgvRow.Cells["ColDeadNameOne"].Value.ToString() : "",
                    DeadNameTwo = dgvRow.Cells["ColDeadNameTwo"].Value != null ? dgvRow.Cells["ColDeadNameTwo"].Value.ToString() : "",
                    DeadNameThree = dgvRow.Cells["ColDeadNameThree"].Value != null ? dgvRow.Cells["ColDeadNameThree"].Value.ToString() : "",
                    DeadNameFour = dgvRow.Cells["ColDeadNameFour"].Value != null ? dgvRow.Cells["ColDeadNameFour"].Value.ToString() : "",
                    DeadNameFive = dgvRow.Cells["ColDeadNameFive"].Value != null ? dgvRow.Cells["ColDeadNameFive"].Value.ToString() : "",
                    DeadNameSix = dgvRow.Cells["ColDeadNameSix"].Value != null ? dgvRow.Cells["ColDeadNameSix"].Value.ToString() : ""
                });
            }

            PrintTablet(tablets,printformat);
        }

        private void tsmiPrintText_Click(object sender, EventArgs e)
        {
            string printformat;
            CustomDialogForm customDialogForm = new CustomDialogForm();
            printformat = customDialogForm.PrintFormatDialog();

            List<TextViewModel> texts = new List<TextViewModel>();
            foreach (DataGridViewRow dgvRow in dgvSignups.SelectedRows)
            {
                string HallNameFirst = string.Empty;
                string HallNameSecond = string.Empty;

                if (dgvRow.Cells["ColHallName"].Value != null && dgvRow.Cells["ColHallName"].Value.ToString() != "")
                {
                    if (dgvRow.Cells["ColHallName"].Value.ToString().Length == 2)
                    {
                        HallNameFirst = dgvRow.Cells["ColHallName"].Value.ToString().Substring(0, 1).Replace("-", "").Trim();
                        HallNameSecond = dgvRow.Cells["ColHallName"].Value.ToString().Substring(1, 1).Replace("-", "").Trim();
                    }
                    else if (dgvRow.Cells["ColHallName"].Value.ToString().Length == 4)
                    {
                        HallNameFirst = dgvRow.Cells["ColHallName"].Value.ToString().Substring(0, 2).Replace("-", "").Trim();
                        HallNameSecond = dgvRow.Cells["ColHallName"].Value.ToString().Substring(2, 2).Replace("-", "").Trim();
                    }
                }

                string add = string.Empty;
                add = add + (dgvRow.Cells["ColTextCity"].Value != null ? dgvRow.Cells["ColTextCity"].Value.ToString() : "");
                add = add + (dgvRow.Cells["ColTextZone"].Value != null ? dgvRow.Cells["ColTextZone"].Value.ToString() : "");
                add = add + (dgvRow.Cells["ColTextAddress"].Value != null ? dgvRow.Cells["ColTextAddress"].Value.ToString() : "");

                texts.Add(new TextViewModel
                {
                    SignupID = (Guid)dgvRow.Cells["ColSignupID"].Value,
                    HallNameFirst = HallNameFirst,
                    HallNameSecond = HallNameSecond,
                    Number = (dgvRow.Cells["ColSignupType"].Value.ToString() == "2") ? dgvRow.Cells["ColNumberTitle"].Value.ToString() : dgvRow.Cells["ColNumberTitle"].Value.ToString() + GetNumberText((int)dgvRow.Cells["ColNumber"].Value),
                    TextAddress = add,
                    LivingNameOne = dgvRow.Cells["ColLivingNameOne"].Value != null ? dgvRow.Cells["ColLivingNameOne"].Value.ToString() : "",
                    LivingNameTwo = dgvRow.Cells["ColLivingNameTwo"].Value != null ? dgvRow.Cells["ColLivingNameTwo"].Value.ToString() : "",
                    LivingNameThree = dgvRow.Cells["ColLivingNameThree"].Value != null ? dgvRow.Cells["ColLivingNameThree"].Value.ToString() : "",
                    LivingNameFour = dgvRow.Cells["ColLivingNameFour"].Value != null ? dgvRow.Cells["ColLivingNameFour"].Value.ToString() : "",
                    LivingNameFive = dgvRow.Cells["ColLivingNameFive"].Value != null ? dgvRow.Cells["ColLivingNameFive"].Value.ToString() : "",
                    LivingNameSix = dgvRow.Cells["ColLivingNameSix"].Value != null ? dgvRow.Cells["ColLivingNameSix"].Value.ToString() : "",
                    DeadNameOne = dgvRow.Cells["ColDeadNameOne"].Value != null ? dgvRow.Cells["ColDeadNameOne"].Value.ToString() : "",
                    DeadNameTwo = dgvRow.Cells["ColDeadNameTwo"].Value != null ? dgvRow.Cells["ColDeadNameTwo"].Value.ToString() : "",
                    DeadNameThree = dgvRow.Cells["ColDeadNameThree"].Value != null ? dgvRow.Cells["ColDeadNameThree"].Value.ToString() : "",
                    DeadNameFour = dgvRow.Cells["ColDeadNameFour"].Value != null ? dgvRow.Cells["ColDeadNameFour"].Value.ToString() : "",
                    DeadNameFive = dgvRow.Cells["ColDeadNameFive"].Value != null ? dgvRow.Cells["ColDeadNameFive"].Value.ToString() : "",
                    DeadNameSix = dgvRow.Cells["ColDeadNameSix"].Value != null ? dgvRow.Cells["ColDeadNameSix"].Value.ToString() : "",
                    PhotoAddress = Library.DrawText(add)
                });
            }

            PrintText(texts, printformat);
        }

        private void tsmiPrintWorship_Click(object sender, EventArgs e)
        {
            string printformat;
            CustomDialogForm customDialogForm = new CustomDialogForm();
            printformat = customDialogForm.PrintFormatDialog();

            List<WorshipViewModel> worships = new List<WorshipViewModel>();
            foreach (DataGridViewRow dgvRow in dgvSignups.SelectedRows)
            {
                worships.Add(new WorshipViewModel
                {
                    SignupID = (Guid)dgvRow.Cells["ColSignupID"].Value,
                    Number = dgvRow.Cells["ColNumberTitle"].Value.ToString() + GetNumberText((int)dgvRow.Cells["ColNumber"].Value),
                    LivingNameOne = dgvRow.Cells["ColLivingNameOne"].Value != null ? dgvRow.Cells["ColLivingNameOne"].Value.ToString() : "",
                    LivingNameTwo = dgvRow.Cells["ColLivingNameTwo"].Value != null ? dgvRow.Cells["ColLivingNameTwo"].Value.ToString() : "",
                    LivingNameThree = dgvRow.Cells["ColLivingNameThree"].Value != null ? dgvRow.Cells["ColLivingNameThree"].Value.ToString() : "",
                    LivingNameFour = dgvRow.Cells["ColLivingNameFour"].Value != null ? dgvRow.Cells["ColLivingNameFour"].Value.ToString() : "",
                    LivingNameFive = dgvRow.Cells["ColLivingNameFive"].Value != null ? dgvRow.Cells["ColLivingNameFive"].Value.ToString() : "",
                    LivingNameSix = dgvRow.Cells["ColLivingNameSix"].Value != null ? dgvRow.Cells["ColLivingNameSix"].Value.ToString() : ""
                });
            }

            PrintWorship(worships, printformat);
        }

        private void tsmiDelete_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show("確認刪除嗎？", Global.AppTitle, MessageBoxButtons.YesNo);
            if (result == DialogResult.Yes)
            {
                foreach (DataGridViewRow dgvRow in dgvSignups.SelectedRows)
                {
                    Guid SignupID = (Guid)dgvRow.Cells["ColSignupID"].Value;

                    signupsService.Delete(SignupID);
                    signupsService.SaveChanges();
                }

                MessageBox.Show("刪除成功！", Global.AppTitle);

                LoadSearchSignups();
            }
            else
            {
                return;
            }
        }

        private void tsmiLog_Click(object sender, EventArgs e)
        {
            int selectedcount = dgvSignups.SelectedRows.Count;
            if (selectedcount == 0)
            {
                MessageBox.Show("尚未選擇報名資料", Global.AppTitle);
                return;
            }
            else
            {
                DataGridViewRow dgvRow = dgvSignups.SelectedRows[0];
                Guid SignupID = (Guid)dgvRow.Cells["ColSignupID"].Value;

                SignupLogForm signuplogform = new SignupLogForm(this, SignupID);

                signuplogform.ShowDialog();
            }
        }

        private void btnPrint_Click(object sender, EventArgs e)
        {
            int startnumber = (int)nudStart.Value;
            int endnumber = (int)nudEnd.Value;

            if (endnumber < startnumber)
            {
                MessageBox.Show("編號錯誤", Global.AppTitle);
                return;
            }

            string printformat;
            CustomDialogForm customDialogForm = new CustomDialogForm();
            printformat = customDialogForm.PrintFormatDialog();

            IQueryable<SignupView> signups = signupviewService.Get().Where(a => a.Number >= startnumber && a.Number <= endnumber).OrderBy(o => o.Number);
            if (txtSearchYear.Text.Trim() != "" && cbIsScope.Checked)
            {
                int Y = Convert.ToInt32(txtSearchYear.Text.Trim());
                signups = signups.Where(a => a.Year >= Y);
            }
            if (txtSearchYear.Text.Trim() != "" && !cbIsScope.Checked)
            {
                int Y = Convert.ToInt32(txtSearchYear.Text.Trim());
                signups = signups.Where(a => a.Year == Y);
            }
            if (dlSearchCeremony.SelectedValue != null) signups = signups.Where(a => a.CeremonyCategoryID == (Guid)dlSearchCeremony.SelectedValue);
            if (dlSearchSignupType.SelectedValue != null) signups = signups.Where(a => a.SignupType == (int)dlSearchSignupType.SelectedValue);

            int printtype = (int)dlPrintType.SelectedValue;
            switch (printtype)
            {
                //資料卡
                case 1:
                    List<DataCardViewModel> datacards = new List<DataCardViewModel>();

                    foreach (SignupView item in signups)
                    {
                        datacards.Add(new DataCardViewModel {
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
                    }

                    PrintDataCard(datacards, printformat);

                    break;
                //收據
                case 2:
                    List<ReceiptViewModel> receipts = new List<ReceiptViewModel>();

                    foreach (SignupView item in signups)
                    {
                        receipts.Add(new ReceiptViewModel { 
                            SignupID = item.SignupID,
                            Name = item.Name,
                            Zipcode = item.MailZipcode,
                            Address = item.MailCity + item.MailZone + item.MailAddress,
                            Fee = Convert.ToInt32(item.Fee).ToString("N0"),
                            Number = GetNumberText((int)item.Number),
                            Year = taiwanCalendar.GetYear(DateTime.Now).ToString(),
                            Month = DateTime.Now.Month.ToString(),
                            Day = DateTime.Now.Day.ToString(),
                            Prepay = (item.PrepayYear.ToString() != "") ? "預繳至" + item.PrepayYear.ToString() + "年" + item.PrepayCeremonyTitle : ""
                        });
                    }

                    PrintReceipt(receipts, printformat);

                    break;
                //薦牌
                case 3:
                    List<TabletViewModel> tablets = new List<TabletViewModel>();

                    foreach(SignupView item in signups)
                    {
                        string HallNameFirst = string.Empty;
                        string HallNameSecond = string.Empty;

                        if (item.HallName != null && item.HallName != "")
                        {
                            if (item.HallName.Length == 2)
                            {
                                HallNameFirst = item.HallName.Substring(0, 1).Replace("-", "").Trim();
                                HallNameSecond = item.HallName.Substring(1, 1).Replace("-", "").Trim();
                            }
                            else if (item.HallName.Length == 4)
                            {
                                HallNameFirst = item.HallName.Substring(0, 2).Replace("-", "").Trim();
                                HallNameSecond = item.HallName.Substring(2, 2).Replace("-", "").Trim();
                            }
                        }

                        tablets.Add(new TabletViewModel {
                            SignupID = item.SignupID,
                            Number = (item.SignupType == 2) ? item.NumberTitle : item.NumberTitle + GetNumberText((int)item.Number),
                            HallNameFirst = HallNameFirst,
                            HallNameSecond = HallNameSecond,
                            LivingNameOne = item.LivingNameOne,
                            LivingNameTwo = item.LivingNameTwo,
                            LivingNameThree = item.LivingNameThree,
                            LivingNameFour = item.LivingNameFour,
                            LivingNameFive = item.LivingNameFive,
                            LivingNameSix = item.LivingNameSix,
                            DeadNameOne = item.DeadNameOne,
                            DeadNameTwo =item.DeadNameTwo,
                            DeadNameThree = item.DeadNameThree,
                            DeadNameFour = item.DeadNameFour,
                            DeadNameFive = item.DeadNameFive,
                            DeadNameSix = item.DeadNameSix
                        });
                    }

                    PrintTablet(tablets, printformat);

                    break;
                //文牒
                case 4:
                    List<TextViewModel> texts = new List<TextViewModel>();

                    foreach(SignupView item in signups)
                    {
                        string HallNameFirst = string.Empty;
                        string HallNameSecond = string.Empty;

                        if (item.HallName != null && item.HallName != "")
                        {
                            if (item.HallName.Length == 2)
                            {
                                HallNameFirst = item.HallName.Substring(0, 1).Replace("-", "").Trim();
                                HallNameSecond = item.HallName.Substring(1, 1).Replace("-", "").Trim();
                            }
                            else if (item.HallName.Length == 4)
                            {
                                HallNameFirst = item.HallName.Substring(0, 2).Replace("-", "").Trim();
                                HallNameSecond = item.HallName.Substring(2, 2).Replace("-", "").Trim();
                            }
                        }

                        texts.Add(new TextViewModel { 
                            SignupID = item.SignupID,
                            HallNameFirst = HallNameFirst,
                            HallNameSecond = HallNameSecond,
                            Number = (item.SignupType == 2) ? item.NumberTitle : item.NumberTitle + GetNumberText((int)item.Number),
                            TextAddress = item.TextCity + item.TextZone + item.TextAddress,
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
                            DeadNameSix = item.DeadNameSix,
                            PhotoAddress = Library.DrawText(item.TextCity + item.TextZone + item.TextAddress)
                        });
                    }

                    PrintText(texts, printformat);

                    break;
                //普桌
                case 5:
                    List<WorshipViewModel> worships = new List<WorshipViewModel>();

                    foreach (SignupView item in signups)
                    {
                        worships.Add(new WorshipViewModel
                        {
                            SignupID = item.SignupID,
                            Number = item.NumberTitle + GetNumberText((int)item.Number),
                            LivingNameOne = item.LivingNameOne,
                            LivingNameTwo = item.LivingNameTwo,
                            LivingNameThree = item.LivingNameThree,
                            LivingNameFour = item.LivingNameFour,
                            LivingNameFive = item.LivingNameFive,
                            LivingNameSix = item.LivingNameSix
                        });
                    }

                    PrintWorship(worships, printformat);

                    break;
                default:
                    break;
            }
        }

        private void btnExportExcel_Click(object sender, EventArgs e)
        {
            if(dgvSignups.Rows.Count == 0)
            {
                MessageBox.Show("請先搜尋，才可匯出Excel！", Global.AppTitle);
                return;
            }

            IWorkbook wBook = new HSSFWorkbook();

            ISheet wSheet = wBook.CreateSheet("Search");

            int rowIdx = 0;
            foreach (DataGridViewRow dgvRow in dgvSignups.Rows)
            {
                IRow urow = wSheet.CreateRow(rowIdx);

                urow.CreateCell(0).SetCellValue(dgvRow.Cells["ColYear"].Value.ToString());
                urow.CreateCell(1).SetCellValue(dgvRow.Cells["ColCeremonyTitle"].Value.ToString());
                urow.CreateCell(2).SetCellValue(dgvRow.Cells["ColNumberTitle"].Value.ToString());
                urow.CreateCell(3).SetCellValue(dgvRow.Cells["ColNumber"].Value != null ? dgvRow.Cells["ColNumber"].Value.ToString() : "" );
                urow.CreateCell(4).SetCellValue(dgvRow.Cells["ColFee"].Value != null ? dgvRow.Cells["ColFee"].Value.ToString() : "" );
                urow.CreateCell(5).SetCellValue(dgvRow.Cells["ColEmployee"].Value != null ? dgvRow.Cells["ColEmployee"].Value.ToString() : "" );
                urow.CreateCell(6).SetCellValue(dgvRow.Cells["ColName"].Value != null ? dgvRow.Cells["ColName"].Value.ToString() : "");
                urow.CreateCell(7).SetCellValue(dgvRow.Cells["ColRemark"].Value != null ? dgvRow.Cells["ColRemark"].Value.ToString() : "");
                urow.CreateCell(8).SetCellValue(dgvRow.Cells["ColHallName"].Value != null ? dgvRow.Cells["ColHallName"].Value.ToString() : "");
                urow.CreateCell(9).SetCellValue(dgvRow.Cells["ColDeadNameOne"].Value != null ? dgvRow.Cells["ColDeadNameOne"].Value.ToString() : "");
                urow.CreateCell(10).SetCellValue(dgvRow.Cells["ColDeadNameTwo"].Value != null ? dgvRow.Cells["ColDeadNameTwo"].Value.ToString() : "" );
                urow.CreateCell(11).SetCellValue(dgvRow.Cells["ColDeadNameThree"].Value != null ? dgvRow.Cells["ColDeadNameThree"].Value.ToString() : "" );
                urow.CreateCell(12).SetCellValue(dgvRow.Cells["ColDeadNameFour"].Value != null ? dgvRow.Cells["ColDeadNameFour"].Value.ToString() : "" );
                urow.CreateCell(13).SetCellValue(dgvRow.Cells["ColDeadNameFive"].Value != null ? dgvRow.Cells["ColDeadNameFive"].Value.ToString() : "" );
                urow.CreateCell(14).SetCellValue(dgvRow.Cells["ColDeadNameSix"].Value != null ? dgvRow.Cells["ColDeadNameSix"].Value.ToString() : "" );
                urow.CreateCell(15).SetCellValue(dgvRow.Cells["ColLivingNameOne"].Value != null ? dgvRow.Cells["ColLivingNameOne"].Value.ToString() : "" );
                urow.CreateCell(16).SetCellValue(dgvRow.Cells["ColLivingNameTwo"].Value != null ? dgvRow.Cells["ColLivingNameTwo"].Value.ToString() : "" );
                urow.CreateCell(17).SetCellValue(dgvRow.Cells["ColLivingNameThree"].Value != null ? dgvRow.Cells["ColLivingNameThree"].Value.ToString() : "" );
                urow.CreateCell(18).SetCellValue(dgvRow.Cells["ColLivingNameFour"].Value != null ? dgvRow.Cells["ColLivingNameFour"].Value.ToString() : "" );
                urow.CreateCell(19).SetCellValue(dgvRow.Cells["ColLivingNameFive"].Value != null ? dgvRow.Cells["ColLivingNameFive"].Value.ToString() : "" );
                urow.CreateCell(20).SetCellValue(dgvRow.Cells["ColLivingNameSix"].Value != null ? dgvRow.Cells["ColLivingNameSix"].Value.ToString() : "" );
                urow.CreateCell(21).SetCellValue(dgvRow.Cells["ColPrepayYear"].Value != null ? dgvRow.Cells["ColPrepayYear"].Value.ToString() : "" );
                urow.CreateCell(22).SetCellValue(dgvRow.Cells["ColPrepayCeremonyTitle"].Value != null ? dgvRow.Cells["ColPrepayCeremonyTitle"].Value.ToString() : "" );
                urow.CreateCell(23).SetCellValue(dgvRow.Cells["ColPhone"].Value != null ? dgvRow.Cells["ColPhone"].Value.ToString() : "" );
                urow.CreateCell(24).SetCellValue(dgvRow.Cells["ColMailCity"].Value != null ? dgvRow.Cells["ColMailCity"].Value.ToString() : "" );
                urow.CreateCell(25).SetCellValue(dgvRow.Cells["ColMailZone"].Value != null ? dgvRow.Cells["ColMailZone"].Value.ToString() : "" );
                urow.CreateCell(26).SetCellValue(dgvRow.Cells["ColMailAddress"].Value != null ? dgvRow.Cells["ColMailAddress"].Value.ToString() : "" );
                urow.CreateCell(27).SetCellValue(dgvRow.Cells["ColTextCity"].Value != null ? dgvRow.Cells["ColTextCity"].Value.ToString() : "" );
                urow.CreateCell(28).SetCellValue(dgvRow.Cells["ColTextZone"].Value != null ? dgvRow.Cells["ColTextZone"].Value.ToString() : "" );
                urow.CreateCell(29).SetCellValue(dgvRow.Cells["ColTextAddress"].Value != null ? dgvRow.Cells["ColTextAddress"].Value.ToString() : "" );
                urow.CreateCell(30).SetCellValue(dgvRow.Cells["ColAdminName"].Value != null ? dgvRow.Cells["ColAdminName"].Value.ToString() : "");
                urow.CreateCell(31).SetCellValue(dgvRow.Cells["ColCreatedate"].Value != null ? dgvRow.Cells["ColCreatedate"].Value.ToString() : "" );

                rowIdx++;
            }

            MemoryStream stream = new MemoryStream();
            wBook.Write(stream);
            byte[] arr = stream.ToArray();

            Stream myStream;
            SaveFileDialog saveFileDialog1 = new SaveFileDialog();

            saveFileDialog1.Filter = "Excel File(*.xls)|*.xls";
            saveFileDialog1.FilterIndex = 1;
            saveFileDialog1.RestoreDirectory = true;
            saveFileDialog1.FileName = DateTime.Now.ToString("yyyyMMddHHmmss");

            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                if ((myStream = saveFileDialog1.OpenFile()) != null)
                {
                    myStream.Write(arr, 0, arr.Length);
                    myStream.Close();
                }
            }
        }

        private void cbSearchName_CheckedChanged(object sender, EventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            if (cb.Checked)
            {
                EnabledSearchKey(true);
            }
            else if(!cbSearchLivingName.Checked && !cbSearchDeadName.Checked && !cbSearchPhone.Checked)
            {
                EnabledSearchKey(false);
            }
        }

        private void cbSearchLivingName_CheckedChanged(object sender, EventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            if (cb.Checked)
            {
                EnabledSearchKey(true);
            }
            else if(!cbSearchName.Checked && !cbSearchDeadName.Checked && !cbSearchPhone.Checked)
            {
                EnabledSearchKey(false);
            }
        }

        private void cbSearchDeadName_CheckedChanged(object sender, EventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            if (cb.Checked)
            {
                EnabledSearchKey(true);
            }
            else if (!cbSearchName.Checked && !cbSearchLivingName.Checked && !cbSearchPhone.Checked)
            {
                EnabledSearchKey(false);
            }
        }

        private void cbSearchPhone_CheckedChanged(object sender, EventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            if (cb.Checked)
            {
                EnabledSearchKey(true);
            }
            else if (!cbSearchName.Checked && !cbSearchLivingName.Checked && !cbSearchDeadName.Checked)
            {
                EnabledSearchKey(false);
            }
        }

        private void cbShowAll_CheckedChanged(object sender, EventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            if (cb.Checked)
            {
                ShowCompleteColumn(true);
            }
            else
            {
                ShowCompleteColumn(false);
            }
        }

        private void dgvSignups_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            if (cbShowAll.Checked)
            {
                ShowCompleteColumn(true);
            }
            else
            {
                ShowCompleteColumn(false);
            }
        }

        public void LoadSearchSignups()
        {
            tsslStatus.Text = "搜尋中，請稍後...";
            ssStatus.Update();

            PanelSearchSwitch(false);
            PanelPrintSwitch(false);
            PanelControlSwitch(false);

            Expression<Func<SignupView, bool>> predicateand = PredicateBuilder.New<SignupView>(true);
            if (txtSearchYear.Text.Trim() != string.Empty && cbIsScope.Checked)
            {
                int Y = Convert.ToInt32(txtSearchYear.Text.Trim());
                predicateand = predicateand.And(a => a.Year >= Y);
            }
            if (txtSearchYear.Text.Trim() != string.Empty && !cbIsScope.Checked)
            {
                int Y = Convert.ToInt32(txtSearchYear.Text.Trim());
                predicateand = predicateand.And(a => a.Year == Y);
            }
            if (dlSearchCeremony.SelectedValue != null && (Guid)dlSearchCeremony.SelectedValue != Guid.Empty) predicateand = predicateand.And(a => a.CeremonyCategoryID == (Guid)dlSearchCeremony.SelectedValue);
            if (dlSearchSignupType.SelectedValue != null && (int)dlSearchSignupType.SelectedValue != -1) predicateand = predicateand.And(a => a.SignupType == (int)dlSearchSignupType.SelectedValue);
            if (nudSearchNumber.Text != "0" && nudSearchNumber.Text != "") predicateand = predicateand.And(a => a.Number == nudSearchNumber.Value);

            bool isOr = false;
            Expression<Func<SignupView, bool>> predicateor = PredicateBuilder.New<SignupView>(false);
            if (cbSearchName.Checked && txtSearchKey.Text.Trim() != string.Empty) { predicateor = predicateor.Or(a => a.Name != null && a.Name.Contains(txtSearchKey.Text.Trim())); isOr = true; }    
            if (cbSearchLivingName.Checked && txtSearchKey.Text.Trim() != string.Empty) { predicateor = predicateor.Or(a => (a.LivingNameOne != null && a.LivingNameOne.Contains(txtSearchKey.Text.Trim())) || (a.LivingNameTwo != null && a.LivingNameTwo.Contains(txtSearchKey.Text.Trim())) || (a.LivingNameThree != null && a.LivingNameThree.Contains(txtSearchKey.Text.Trim())) || (a.LivingNameFour != null && a.LivingNameFour.Contains(txtSearchKey.Text.Trim())) || (a.LivingNameFive != null && a.LivingNameFive.Contains(txtSearchKey.Text.Trim())) || (a.LivingNameSix != null && a.LivingNameSix.Contains(txtSearchKey.Text.Trim()))); isOr = true; }    
            if (cbSearchDeadName.Checked && txtSearchKey.Text.Trim() != string.Empty) { predicateor = predicateor.Or(a => (a.DeadNameOne != null && a.DeadNameOne.Contains(txtSearchKey.Text.Trim())) || (a.DeadNameTwo != null && a.DeadNameTwo.Contains(txtSearchKey.Text.Trim())) || (a.DeadNameThree != null && a.DeadNameThree.Contains(txtSearchKey.Text.Trim())) || (a.DeadNameFour != null && a.DeadNameFour.Contains(txtSearchKey.Text.Trim())) || (a.DeadNameFive != null && a.DeadNameFive.Contains(txtSearchKey.Text.Trim())) || (a.DeadNameSix != null && a.DeadNameSix.Contains(txtSearchKey.Text.Trim()))); isOr = true; }               
            if (cbSearchPhone.Checked && txtSearchKey.Text.Trim() != string.Empty) { predicateor = predicateor.Or(a => (a.Phone != null && a.Phone.Contains(txtSearchKey.Text.Trim()))); isOr = true; }
            if (cbIsFixedNumber.Checked) { predicateor = predicateor.Or(a => a.IsFixedNumber == true); isOr = true; }

            signupviewService = new SignupViewService();
            IQueryable<SignupView> signupviews = signupviewService.Get().AsExpandable().Where(predicateand);
            if (isOr) signupviews = signupviews.Where(predicateor);

            if (signupviews.Any())
            {              
                BindingSource bindingSource = new BindingSource();
                bindingSource.DataSource = signupviews.OrderBy(o => o.Year).ThenBy(o => o.CeremonySort).ThenBy(o => o.NumberTitle).ThenBy(o => o.Number).ToList();

                dgvSignups.DataSource = bindingSource;
                
                dgvSignups.ClearSelection();
            }
            else
            {
                dgvSignups.Rows.Clear();
                MessageBox.Show("無資料，請重新搜尋！", Global.AppTitle);
            }

            tsslStatus.Text = "待命";
            ssStatus.Update();

            PanelSearchSwitch(true);
            PanelPrintSwitch(true);
            PanelControlSwitch(true);
        }

        private void LoadCeremony()
        {
            List<CeremonyCategorys> ceremonycategorys = ceremonycategorysService.Get().Where(a => a.ParentID == null).ToList();
            ceremonycategorys.Add(new CeremonyCategorys { 
                CeremonyCategoryID = Guid.Empty,
                Title = "全部",
                Sort = 0
            });

            BindingSource bsCeremonyCategory = new BindingSource { DataSource = ceremonycategorys.OrderBy(o => o.Sort) };

            dlSearchCeremony.DataSource = bsCeremonyCategory;
            dlSearchCeremony.DisplayMember = "Title";
            dlSearchCeremony.ValueMember = "CeremonyCategoryID";

            dlSearchCeremony.SelectedValue = Guid.Empty;
            dlSearchCeremony.Text = "全部";
        }

        private void LoadPrintType()
        {
            List<PrintTypeViewModel> printtypes = new List<PrintTypeViewModel>();
            printtypes.Add(new PrintTypeViewModel { 
                ID = 1,
                Title = "資料卡"
            });
            printtypes.Add(new PrintTypeViewModel { 
                ID = 2,
                Title = "收據"
            });
            printtypes.Add(new PrintTypeViewModel {
                ID = 3,
                Title = "薦牌"
            });
            printtypes.Add(new PrintTypeViewModel {
                ID = 4,
                Title = "文牒"
            });
            printtypes.Add(new PrintTypeViewModel {
                ID = 5,
                Title = "普桌"
            });

            BindingSource bsPrintType = new BindingSource { DataSource = printtypes };
            dlPrintType.DataSource = bsPrintType;
            dlPrintType.DisplayMember = "Title";
            dlPrintType.ValueMember = "ID";
        }

        private void LoadSignupType()
        {
            List<SignupTypeViewModel> signuptypes = new List<SignupTypeViewModel>();
            signuptypes.Add(new SignupTypeViewModel {
                ID = -1,
                Title = "全部"
            });
            signuptypes.Add(new SignupTypeViewModel { 
                ID = 1,
                Title = "一般"
            });
            signuptypes.Add(new SignupTypeViewModel { 
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

            BindingSource bsSignupType2 = new BindingSource { DataSource = signuptypes };

            dlSearchSignupType.DataSource = bsSignupType2;
            dlSearchSignupType.DisplayMember = "Title";
            dlSearchSignupType.ValueMember = "ID";

            dlSearchSignupType.SelectedValue = -1;
            dlSearchSignupType.Text = "全部";
        }

        private void PrintDataCard(List<DataCardViewModel> datacards, string printFormat = "Preview")
        {
            m_streams = new List<Stream>();

            LocalReport lr = new LocalReport();
            string path = Path.Combine(_Path, "tmpDataCard.rdlc");
            lr.ReportPath = path;
            lr.EnableExternalImages = true;
            lr.DataSources.Add(new ReportDataSource("DataCardDataSet", datacards));

            string reportType = string.Empty;
            if (printFormat == "Preview")
            {
                reportType = "EMF";
            }
            else
            {
                reportType = "PDF";
            }

            //這裡是決定列印的紙張大小，RDLC只是用來排版的
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

            if(printFormat == "Preview")
            {
                lr.Render("Image", deviceInfo, CreateStream, out warnings);
                foreach (Stream stream in m_streams)
                    stream.Position = 0;

                paperSize = new System.Drawing.Printing.PaperSize("資料卡", 794, 560);
                margins = new Margins(0, 0, 0, 0);
                //isLandscape = true;

                printDocument = new PrintDocument();
                printDocument.DefaultPageSettings.Margins = margins;
                printDocument.DefaultPageSettings.PaperSize = paperSize;

                printDocument.BeginPrint += new PrintEventHandler(printDocument_BeginPrint);
                printDocument.PrintPage += new PrintPageEventHandler(printDocument_PrintPage);

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
            else
            {
                string mimeType, encoding, fileNameExtension;
                string[] streams;
                byte[] renderedBytes;

                renderedBytes = lr.Render(
                    reportType,
                    deviceInfo,
                    out mimeType,
                    out encoding,
                    out fileNameExtension,
                    out streams,
                    out warnings);

                Stream myStream;
                SaveFileDialog saveFileDialog1 = new SaveFileDialog();

                saveFileDialog1.Filter = "PDF file (*.pdf)|*.pdf";
                saveFileDialog1.FilterIndex = 1;
                saveFileDialog1.RestoreDirectory = true;
                saveFileDialog1.FileName = "printdatacard";
                if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    if ((myStream = saveFileDialog1.OpenFile()) != null)
                    {
                        myStream.Write(renderedBytes, 0, renderedBytes.Length);
                        myStream.Close();
                    }
                }
            }
        }

        private void PrintReceipt(List<ReceiptViewModel> receipts, string printFormat = "Preview")
        {
            m_streams = new List<Stream>();

            LocalReport lr = new LocalReport();
            string path = Path.Combine(_Path, "tmpReceipt.rdlc");
            lr.ReportPath = path;
            lr.EnableExternalImages = true;
            lr.DataSources.Add(new ReportDataSource("ReceiptDataSet", receipts));

            string reportType = string.Empty;
            if (printFormat == "Preview")
            {
                reportType = "EMF";
            }
            else
            {
                reportType = "PDF";
            }

            //這裡是決定列印的紙張大小，RDLC只是用來排版的
            string deviceInfo =
            "<DeviceInfo>" +
            "  <OutputFormat>" + reportType + "</OutputFormat>" +
            "  <PageWidth>21cm</PageWidth>" +
            "  <PageHeight>29.7cm</PageHeight>" +
            "  <MarginTop>0cm</MarginTop>" +
            "  <MarginLeft>0cm</MarginLeft>" +
            "  <MarginRight>0cm</MarginRight>" +
            "  <MarginBottom>0cm</MarginBottom>" +
            "</DeviceInfo>";

            Warning[] warnings;

            if(printFormat == "Preview")
            {
                lr.Render("Image", deviceInfo, CreateStream, out warnings);
                foreach (Stream stream in m_streams)
                    stream.Position = 0;

                paperSize = new System.Drawing.Printing.PaperSize("收據", 827, 1170);
                margins = new Margins(0, 0, 0, 0);
                //isLandscape = false;

                printDocument = new PrintDocument();
                printDocument.DefaultPageSettings.Margins = margins;
                printDocument.DefaultPageSettings.PaperSize = paperSize;

                printDocument.BeginPrint += new PrintEventHandler(printDocument_BeginPrint);
                printDocument.PrintPage += new PrintPageEventHandler(printDocument_PrintPage);

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
            else
            {
                string mimeType, encoding, fileNameExtension;
                string[] streams;
                byte[] renderedBytes;

                renderedBytes = lr.Render(
                    reportType,
                    deviceInfo,
                    out mimeType,
                    out encoding,
                    out fileNameExtension,
                    out streams,
                    out warnings);

                Stream myStream;
                SaveFileDialog saveFileDialog1 = new SaveFileDialog();

                saveFileDialog1.Filter = "PDF file (*.pdf)|*.pdf";
                saveFileDialog1.FilterIndex = 1;
                saveFileDialog1.RestoreDirectory = true;
                saveFileDialog1.FileName = "printreceipt";
                if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    if ((myStream = saveFileDialog1.OpenFile()) != null)
                    {
                        myStream.Write(renderedBytes, 0, renderedBytes.Length);
                        myStream.Close();
                    }
                }
            }
        }

        private void PrintTablet(List<TabletViewModel> tablets, string printFormat = "Preview")
        {
            m_streams = new List<Stream>();

            List<byte[]> pdflist = new List<byte[]>();

            foreach (TabletViewModel model in tablets)
            {
                LocalReport lr = new LocalReport();

                string fontsize = string.Empty;
                string filename = "tmpTablet.rdlc";
                List<TabletViewModel> models = new List<TabletViewModel>();

                models.Add(model);

                if (model.DeadNameOne != null && model.DeadNameOne.Trim() != "" && (model.DeadNameTwo == null || model.DeadNameTwo.Trim() == "") && (model.DeadNameThree == null || model.DeadNameThree.Trim() == "") && (model.DeadNameFour == null || model.DeadNameFour.Trim() == "") && (model.DeadNameFive == null || model.DeadNameFive.Trim() == "") && (model.DeadNameSix == null || model.DeadNameSix.Trim() == ""))
                {
                    if (model.LivingNameOne != null && model.LivingNameOne.Trim() != "" && (model.LivingNameTwo == null || model.LivingNameTwo.Trim() == "") && (model.LivingNameThree == null || model.LivingNameThree.Trim() == "") && (model.LivingNameFour == null || model.LivingNameFour.Trim() == "") && (model.LivingNameFive == null || model.LivingNameFive.Trim() == "") && (model.LivingNameSix == null || model.LivingNameSix.Trim() == ""))
                    {
                        filename = "tmpTabletOneOne.rdlc";
                    }
                    else if (model.LivingNameTwo != null && model.LivingNameTwo.Trim() != "" && (model.LivingNameThree == null || model.LivingNameThree.Trim() == "") && (model.LivingNameFour == null || model.LivingNameFour.Trim() == "") && (model.LivingNameFive == null || model.LivingNameFive.Trim() == "") && (model.LivingNameSix == null || model.LivingNameSix.Trim() == ""))
                    {
                        filename = "tmpTabletOneTwo.rdlc";
                    }
                    else
                    {
                        filename = "tmpTabletOne.rdlc";
                    }

                    if (model.DeadNameOne.Trim().Length > 7)
                    {
                        fontsize = "0.6cm";
                    }
                    else
                    {
                        fontsize = "0.8cm";
                    }
                }
                else if (model.DeadNameTwo != null && model.DeadNameTwo.Trim() != "" && (model.DeadNameThree == null || model.DeadNameThree.Trim() == "") && (model.DeadNameFour == null || model.DeadNameFour.Trim() == "") && (model.DeadNameFive == null || model.DeadNameFive.Trim() == "") && (model.DeadNameSix == null || model.DeadNameSix.Trim() == ""))
                {
                    if (model.LivingNameOne != null && model.LivingNameOne.Trim() != "" && (model.LivingNameTwo == null || model.LivingNameTwo.Trim() == "") && (model.LivingNameThree == null || model.LivingNameThree.Trim() == "") && (model.LivingNameFour == null || model.LivingNameFour.Trim() == "") && (model.LivingNameFive == null || model.LivingNameFive.Trim() == "") && (model.LivingNameSix == null || model.LivingNameSix.Trim() == ""))
                    {
                        filename = "tmpTabletTwoOne.rdlc";
                    }
                    else if (model.LivingNameTwo != null && model.LivingNameTwo.Trim() != "" && (model.LivingNameThree == null || model.LivingNameThree.Trim() == "") && (model.LivingNameFour == null || model.LivingNameFour.Trim() == "") && (model.LivingNameFive == null || model.LivingNameFive.Trim() == "") && (model.LivingNameSix == null || model.LivingNameSix.Trim() == ""))
                    {
                        filename = "tmpTabletTwoTwo.rdlc";
                    }
                    else
                    {
                        filename = "tmpTabletTwo.rdlc";
                    }

                    if ((model.DeadNameOne != null && model.DeadNameOne.Trim() != "" && model.DeadNameOne.Trim().Length > 7) || model.DeadNameTwo.Trim().Length > 7)
                    {
                        fontsize = "0.6cm";
                    }
                    else
                    {
                        fontsize = "0.8cm";
                    }
                }
                else
                {
                    if (model.LivingNameOne != null && model.LivingNameOne.Trim() != "" && (model.LivingNameTwo == null || model.LivingNameTwo.Trim() == "") && (model.LivingNameThree == null || model.LivingNameThree.Trim() == "") && (model.LivingNameFour == null || model.LivingNameFour.Trim() == "") && (model.LivingNameFive == null || model.LivingNameFive.Trim() == "") && (model.LivingNameSix == null || model.LivingNameSix.Trim() == ""))
                    {
                        filename = "tmpTablet_One.rdlc";
                    }
                    else if (model.LivingNameTwo != null && model.LivingNameTwo.Trim() != "" && (model.LivingNameThree == null || model.LivingNameThree.Trim() == "") && (model.LivingNameFour == null || model.LivingNameFour.Trim() == "") && (model.LivingNameFive == null || model.LivingNameFive.Trim() == "") && (model.LivingNameSix == null || model.LivingNameSix.Trim() == ""))
                    {
                        filename = "tmpTablet_Two.rdlc";
                    }
                    else
                    {
                        filename = "tmpTablet.rdlc";
                    }

                    fontsize = "0.6cm";
                }

                string path = Path.Combine(_Path, filename);
                lr.DataSources.Clear();
                lr.ReportPath = path;
                lr.EnableExternalImages = true;
                lr.DataSources.Add(new ReportDataSource("TabletDataSet", models));
                lr.SetParameters(new ReportParameter("ParaFontSize", fontsize));

                string reportType = string.Empty;
                if (printFormat == "Preview")
                {
                    reportType = "EMF";
                }
                else
                {
                    reportType = "PDF";
                }

                //這裡是決定列印的紙張大小，RDLC只是用來排版的
                string deviceInfo =
                "<DeviceInfo>" +
                "  <OutputFormat>" + reportType + "</OutputFormat>" +
                "  <PageWidth>11.5cm</PageWidth>" +
                "  <PageHeight>25.4cm</PageHeight>" +
                "  <MarginTop>0cm</MarginTop>" +
                "  <MarginLeft>0cm</MarginLeft>" +
                "  <MarginRight>0cm</MarginRight>" +
                "  <MarginBottom>0cm</MarginBottom>" +
                "</DeviceInfo>";

                Warning[] warnings;

                if (printFormat == "Preview")
                {
                    // 此處就是把 LocalReport 顯示的內容 轉成 Stream 資料
                    lr.Render("Image", deviceInfo, CreateStream, out warnings);
                    foreach (Stream stream in m_streams)
                        stream.Position = 0;
                }
                else
                {
                    string mimeType, encoding, fileNameExtension;
                    string[] streams;
                    byte[] renderedBytes;

                    renderedBytes = lr.Render(
                        reportType,
                        deviceInfo,
                        out mimeType,
                        out encoding,
                        out fileNameExtension,
                        out streams,
                        out warnings);

                    pdflist.Add(renderedBytes);
                }
            }

            if(printFormat == "Preview")
            {
                paperSize = new System.Drawing.Printing.PaperSize("薦牌", 453, 1000);
                margins = new Margins(0, 0, 0, 0);
                //isLandscape = false;

                printDocument = new PrintDocument();
                printDocument.DefaultPageSettings.Margins = margins;
                printDocument.DefaultPageSettings.PaperSize = paperSize;

                printDocument.BeginPrint += new PrintEventHandler(printDocument_BeginPrint);
                printDocument.PrintPage += new PrintPageEventHandler(printDocument_PrintPage);

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
            else
            {
                byte[] arr = CombinePDFs(pdflist);

                Stream myStream;
                SaveFileDialog saveFileDialog1 = new SaveFileDialog();

                saveFileDialog1.Filter = "PDF file (*.pdf)|*.pdf";
                saveFileDialog1.FilterIndex = 1;
                saveFileDialog1.RestoreDirectory = true;
                saveFileDialog1.FileName = "printtablet";

                if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    if ((myStream = saveFileDialog1.OpenFile()) != null)
                    {
                        myStream.Write(arr, 0, arr.Length);
                        myStream.Close();
                    }
                }
            }
        }

        private void PrintText(List<TextViewModel> texts, string printFormat = "Preview")
        {
            m_streams = new List<Stream>();

            List<byte[]> pdflist = new List<byte[]>();

            foreach (TextViewModel model in texts)
            {
                LocalReport lr = new LocalReport();

                string filename = "tmpText.rdlc";
                List<TextViewModel> models = new List<TextViewModel>();

                models.Add(model);

                if (model.DeadNameTwo != null && model.DeadNameTwo.Trim() != "" && (model.DeadNameThree == null || model.DeadNameThree.Trim() == "") && (model.DeadNameFour == null || model.DeadNameFour.Trim() == "") && (model.DeadNameFive == null || model.DeadNameFive.Trim() == "") && (model.DeadNameSix == null || model.DeadNameSix.Trim() == ""))
                {
                    filename = "tmpTextTwo.rdlc";
                }
                else
                {
                    filename = "tmpText.rdlc";
                }

                string path = Path.Combine(_Path, filename);
                lr.ReportPath = path;
                lr.EnableExternalImages = true;
                lr.DataSources.Add(new ReportDataSource("TextDataSet", models));

                string reportType = string.Empty;
                if (printFormat == "Preview")
                {
                    reportType = "EMF";
                }
                else
                {
                    reportType = "PDF";
                }

                //這裡是決定列印的紙張大小，RDLC只是用來排版的
                string deviceInfo =
                "<DeviceInfo>" +
                "  <OutputFormat>" + reportType + "</OutputFormat>" +
                "  <PageWidth>36.5cm</PageWidth>" +
                "  <PageHeight>26.2cm</PageHeight>" +
                "  <MarginTop>0cm</MarginTop>" +
                "  <MarginLeft>0cm</MarginLeft>" +
                "  <MarginRight>0cm</MarginRight>" +
                "  <MarginBottom>0cm</MarginBottom>" +
                "</DeviceInfo>";

                Warning[] warnings;

                if (printFormat == "Preview")
                {
                    // 此處就是把 LocalReport 顯示的內容 轉成 Stream 資料
                    lr.Render("Image", deviceInfo, CreateStream, out warnings);
                    foreach (Stream stream in m_streams)
                        stream.Position = 0;
                }
                else
                {
                    string mimeType, encoding, fileNameExtension;
                    string[] streams;
                    byte[] renderedBytes;

                    renderedBytes = lr.Render(
                        reportType,
                        deviceInfo,
                        out mimeType,
                        out encoding,
                        out fileNameExtension,
                        out streams,
                        out warnings);

                    pdflist.Add(renderedBytes);
                }
            }

            if (printFormat == "Preview")
            {
                paperSize = new System.Drawing.Printing.PaperSize("文牒", 1370, 990);
                margins = new Margins(0, 0, 0, 0);
                //isLandscape = false;

                printDocument = new PrintDocument();
                printDocument.DefaultPageSettings.Margins = margins;
                printDocument.DefaultPageSettings.PaperSize = paperSize;

                printDocument.BeginPrint += new PrintEventHandler(printDocument_BeginPrint);
                printDocument.PrintPage += new PrintPageEventHandler(printDocument_PrintPage);

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
            else
            {
                byte[] arr = CombinePDFs(pdflist);

                Stream myStream;
                SaveFileDialog saveFileDialog1 = new SaveFileDialog();

                saveFileDialog1.Filter = "PDF file (*.pdf)|*.pdf";
                saveFileDialog1.FilterIndex = 1;
                saveFileDialog1.RestoreDirectory = true;
                saveFileDialog1.FileName = "printtext";

                if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    if ((myStream = saveFileDialog1.OpenFile()) != null)
                    {
                        myStream.Write(arr, 0, arr.Length);
                        myStream.Close();
                    }
                }
            }

            //LocalReport lr = new LocalReport();
            //string path = Path.Combine(_Path, "tmpText.rdlc");
            //lr.ReportPath = path;
            //lr.EnableExternalImages = true;
            //lr.DataSources.Add(new ReportDataSource("TextDataSet", texts));

            //string reportType = string.Empty;
            //if (printFormat == "Preview")
            //{
            //    reportType = "EMF";
            //}
            //else
            //{
            //    reportType = "PDF";
            //}

            ////這裡是決定列印的紙張大小，RDLC只是用來排版的
            //string deviceInfo =
            //"<DeviceInfo>" +
            //"  <OutputFormat>" + reportType + "</OutputFormat>" +
            //"  <PageWidth>36.5cm</PageWidth>" +
            //"  <PageHeight>26.2cm</PageHeight>" +
            //"  <MarginTop>0cm</MarginTop>" +
            //"  <MarginLeft>0cm</MarginLeft>" +
            //"  <MarginRight>0cm</MarginRight>" +
            //"  <MarginBottom>0cm</MarginBottom>" +
            //"</DeviceInfo>";

            //Warning[] warnings;

            //if (printFormat == "Preview")
            //{
            //    lr.Render("Image", deviceInfo, CreateStream, out warnings);
            //    foreach (Stream stream in m_streams)
            //        stream.Position = 0;

            //    //paperSize = new System.Drawing.Printing.PaperSize("文牒", 1437, 1031);
            //    paperSize = new System.Drawing.Printing.PaperSize("文牒", 1370, 990);
            //    margins = new Margins(0, 0, 0, 0);
            //    //isLandscape = true;

            //    printDocument = new PrintDocument();
            //    printDocument.DefaultPageSettings.Margins = margins;
            //    printDocument.DefaultPageSettings.PaperSize = paperSize;

            //    printDocument.BeginPrint += new PrintEventHandler(printDocument_BeginPrint);
            //    printDocument.PrintPage += new PrintPageEventHandler(printDocument_PrintPage);

            //    printPreviewDialog = new PrintPreviewDialog();
            //    printPreviewDialog.Document = printDocument;

            //    ToolStripButton b = new ToolStripButton();
            //    b.Image = ((ToolStrip)(printPreviewDialog.Controls[1])).ImageList.Images[0];
            //    b.DisplayStyle = ToolStripItemDisplayStyle.Image;
            //    b.Click += printPreview_PrintClick;
            //    ((ToolStrip)(printPreviewDialog.Controls[1])).Items.RemoveAt(0);
            //    ((ToolStrip)(printPreviewDialog.Controls[1])).Items.Insert(0, b);

            //    printPreviewDialog.ShowDialog();
            //}
            //else
            //{
            //    string mimeType, encoding, fileNameExtension;
            //    string[] streams;
            //    byte[] renderedBytes;
                
            //    renderedBytes = lr.Render(
            //        reportType,
            //        deviceInfo,
            //        out mimeType,
            //        out encoding,
            //        out fileNameExtension,
            //        out streams,
            //        out warnings);

            //    Stream myStream;
            //    SaveFileDialog saveFileDialog1 = new SaveFileDialog();

            //    saveFileDialog1.Filter = "PDF file (*.pdf)|*.pdf";
            //    saveFileDialog1.FilterIndex = 1;
            //    saveFileDialog1.RestoreDirectory = true;
            //    saveFileDialog1.FileName = "printtext";
            //    if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            //    {
            //        if ((myStream = saveFileDialog1.OpenFile()) != null)
            //        {
            //            myStream.Write(renderedBytes, 0, renderedBytes.Length);
            //            myStream.Close();
            //        }
            //    }
            //}
        }

        private void PrintWorship(List<WorshipViewModel> worships, string printFormat = "Preview")
        {
            m_streams = new List<Stream>();

            List<byte[]> pdflist = new List<byte[]>();
            
            foreach (WorshipViewModel model in worships)
            {
                LocalReport lr = new LocalReport();

                string filename = "tmpWorship.rdlc";
                List<WorshipViewModel> models = new List<WorshipViewModel>();

                models.Add(model);

                if (model.LivingNameSix != null && model.LivingNameSix.Trim() != "")
                {
                    filename = "tmpWorship.rdlc";
                }
                else if (model.LivingNameFive != null && model.LivingNameFive.Trim() != "" && (model.LivingNameSix == null || model.LivingNameSix.Trim() == ""))
                {
                    filename = "tmpWorshipFive.rdlc";
                }
                else if (model.LivingNameFour != null && model.LivingNameFour.Trim() != "" && (model.LivingNameFive == null || model.LivingNameFive.Trim() == "") && (model.LivingNameSix == null || model.LivingNameSix.Trim() == ""))
                {
                    filename = "tmpWorshipFour.rdlc";
                }
                else if (model.LivingNameThree != null && model.LivingNameThree.Trim() != "" && (model.LivingNameFour == null || model.LivingNameFour.Trim() == "") && (model.LivingNameFive == null || model.LivingNameFive.Trim() == "") && (model.LivingNameSix == null || model.LivingNameSix.Trim() == ""))
                {
                    filename = "tmpWorshipThree.rdlc";
                }
                else if (model.LivingNameTwo != null && model.LivingNameTwo.Trim() != "" && (model.LivingNameThree == null || model.LivingNameThree.Trim() == "") && (model.LivingNameFour == null || model.LivingNameFour.Trim() == "") && (model.LivingNameFive == null || model.LivingNameFive.Trim() == "") && (model.LivingNameSix == null || model.LivingNameSix.Trim() == ""))
                {
                    filename = "tmpWorshipTwo.rdlc";
                }
                else if (model.LivingNameOne != null && model.LivingNameOne.Trim() != "" && (model.LivingNameTwo == null || model.LivingNameTwo.Trim() == "") && (model.LivingNameThree == null || model.LivingNameThree.Trim() == "") && (model.LivingNameFour == null || model.LivingNameFour.Trim() == "") && (model.LivingNameFive == null || model.LivingNameFive.Trim() == "") && (model.LivingNameSix == null || model.LivingNameSix.Trim() == ""))
                {
                    filename = "tmpWorshipOne.rdlc";
                }

                string path = Path.Combine(_Path, filename);
                lr.DataSources.Clear();
                lr.ReportPath = path;
                lr.EnableExternalImages = true;
                lr.DataSources.Add(new ReportDataSource("WorshipDataSet", models));

                string reportType = string.Empty;
                if (printFormat == "Preview")
                {
                    reportType = "EMF";
                }
                else
                {
                    reportType = "PDF";
                }

                //這裡是決定列印的紙張大小，RDLC只是用來排版的
                string deviceInfo =
                "<DeviceInfo>" +
                "  <OutputFormat>" + reportType + "</OutputFormat>" +
                "  <PageWidth>21cm</PageWidth>" +
                "  <PageHeight>29.6cm</PageHeight>" +
                "  <MarginTop>0cm</MarginTop>" +
                "  <MarginLeft>0cm</MarginLeft>" +
                "  <MarginRight>0cm</MarginRight>" +
                "  <MarginBottom>0cm</MarginBottom>" +
                "</DeviceInfo>";

                Warning[] warnings;

                if (printFormat == "Preview")
                {
                    // 此處就是把 LocalReport 顯示的內容 轉成 Stream 資料
                    lr.Render("Image", deviceInfo, CreateStream, out warnings);
                    foreach (Stream stream in m_streams)
                        stream.Position = 0;
                }
                else
                {
                    string mimeType, encoding, fileNameExtension;
                    string[] streams;
                    byte[] renderedBytes;

                    renderedBytes = lr.Render(
                        reportType,
                        deviceInfo,
                        out mimeType,
                        out encoding,
                        out fileNameExtension,
                        out streams,
                        out warnings);

                    pdflist.Add(renderedBytes);
                }
            }

            if(printFormat == "Preview")
            {
                paperSize = new System.Drawing.Printing.PaperSize("普桌", 827, 1165);
                margins = new Margins(0, 0, 0, 0);
                //isLandscape = false;

                printDocument = new PrintDocument();
                printDocument.DefaultPageSettings.Margins = margins;
                printDocument.DefaultPageSettings.PaperSize = paperSize;

                printDocument.BeginPrint += new PrintEventHandler(printDocument_BeginPrint);
                printDocument.PrintPage += new PrintPageEventHandler(printDocument_PrintPage);

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
            else
            {
                byte[] arr = CombinePDFs(pdflist);

                Stream myStream;
                SaveFileDialog saveFileDialog1 = new SaveFileDialog();

                saveFileDialog1.Filter = "PDF file (*.pdf)|*.pdf";
                saveFileDialog1.FilterIndex = 1;
                saveFileDialog1.RestoreDirectory = true;
                saveFileDialog1.FileName = "printworship";

                if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    if ((myStream = saveFileDialog1.OpenFile()) != null)
                    {
                        myStream.Write(arr, 0, arr.Length);
                        myStream.Close();
                    }
                }
            }
        }

        private byte[] CombinePDFs(List<byte[]> srcPDFs)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (PdfDocument resultPDF = new PdfDocument(ms))
                {
                    foreach (byte[] pdf in srcPDFs)
                    {
                        using (MemoryStream src = new MemoryStream(pdf))
                        {
                            using (PdfDocument srcPDF = PdfReader.Open(src, PdfDocumentOpenMode.Import))
                            {
                                for (var i = 0; i < srcPDF.PageCount; i++)
                                {
                                    resultPDF.AddPage(srcPDF.Pages[i]);
                                }
                            }
                        }
                    }
                    resultPDF.Save(ms);
                    
                    return ms.ToArray();
                }
            }
        }

        // 提供給 the report renderer 使用, 用來建立列印用的 image stream
        private Stream CreateStream(string name, string fileNameExtension, Encoding encoding, string mimeType, bool willSeek)
        {
            Stream stream = new MemoryStream();
            m_streams.Add(stream);
            return stream;
        }

        private void printDocument_BeginPrint(object sender, PrintEventArgs e)
        {
            m_currentPageIndex = 0;
        }

        private void printDocument_PrintPage(object sender, PrintPageEventArgs ev)
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

        private void PanelSearchSwitch(bool isenable)
        {
            foreach (Control ctrl in plSearch.Controls)
            {
                if (ctrl is TextBox)
                {
                    TextBox textbox = (TextBox)ctrl;
                    if(textbox.Name != "txtSearchKey")
                    {
                        textbox.Enabled = isenable;
                    }
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

                if (ctrl is CheckBox)
                {
                    CheckBox checkbox = (CheckBox)ctrl;
                    checkbox.Enabled = isenable;
                }

                if (ctrl is NumericUpDown)
                {
                    NumericUpDown numericupdown = (NumericUpDown)ctrl;
                    numericupdown.Enabled = isenable;
                }
            }
        }

        private void PanelPrintSwitch(bool isenable)
        {
            foreach (Control ctrl in plPrint.Controls)
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

                if (ctrl is ComboBox)
                {
                    ComboBox combobox = (ComboBox)ctrl;
                    combobox.Enabled = isenable;
                }

                if (ctrl is CheckBox)
                {
                    CheckBox checkbox = (CheckBox)ctrl;
                    checkbox.Enabled = isenable;
                }

                if (ctrl is NumericUpDown)
                {
                    NumericUpDown numericupdown = (NumericUpDown)ctrl;
                    numericupdown.Enabled = isenable;
                }
            }
        }

        private void PanelControlSwitch(bool isenable)
        {
            foreach (Control ctrl in plControl.Controls)
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

                if (ctrl is ComboBox)
                {
                    ComboBox combobox = (ComboBox)ctrl;
                    combobox.Enabled = isenable;
                }

                if (ctrl is CheckBox)
                {
                    CheckBox checkbox = (CheckBox)ctrl;
                    checkbox.Enabled = isenable;
                }

                if (ctrl is NumericUpDown)
                {
                    NumericUpDown numericupdown = (NumericUpDown)ctrl;
                    numericupdown.Enabled = isenable;
                }
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

        private void ShowCompleteColumn(bool IsVisible)
        {
            dgvSignups.Columns["ColFee"].Visible = IsVisible;
            dgvSignups.Columns["ColEmployee"].Visible = IsVisible;
            dgvSignups.Columns["ColHallName"].Visible = IsVisible;
            dgvSignups.Columns["ColLivingNameSix"].Visible = IsVisible;
            dgvSignups.Columns["ColDeadNameSix"].Visible = IsVisible;
        }

        private void EnabledSearchKey(bool IsEnabled)
        {
            txtSearchKey.Text = null;
            txtSearchKey.Enabled = IsEnabled;
        }
    }
}
