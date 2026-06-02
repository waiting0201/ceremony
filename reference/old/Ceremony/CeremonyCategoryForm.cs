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
    public partial class CeremonyCategoryForm : Form
    {
        private CeremonyEntities db;
        private CeremonyCategorysService ceremonycategorysService;

        private bool IsCreate = false;
        private Guid CurrentCeremonyCategoryID;

        private TreeNode CurrentNode;

        public CeremonyCategoryForm()
        {
            InitializeComponent();

            db = new CeremonyEntities();
            ceremonycategorysService = new CeremonyCategorysService(db);

            PanelFormSwitch(false);

            LoadCeremonyCategorys();
            tvCeremonyCategorys.ExpandAll();
        }

        private void tvCeremonyCategorys_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            CurrentNode = e.Node;

            //如果不是root
            if (e.Node.Level > 0)
            {
                CurrentCeremonyCategoryID = new Guid(e.Node.Name);
                CeremonyCategorys ceremonycategory = ceremonycategorysService.GetByID(CurrentCeremonyCategoryID);

                txtTitle.Text = e.Node.Text;
                nudSort.Value = (int)ceremonycategory.Sort;

                PanelFormSwitch(true);

                if (e.Node.Level > 1)
                {
                    tsmiCreate.Enabled = false;
                }
                else
                {
                    tsmiCreate.Enabled = true;
                }
                tsmiDelete.Enabled = true;
            }
            else
            {
                CurrentCeremonyCategoryID = Guid.Empty;

                PanelFormEmpty();
                PanelFormSwitch(false);

                tsmiCreate.Enabled = true;
                tsmiDelete.Enabled = false;
            }

            //判斷是否按右鍵
            if (e.Button == MouseButtons.Right)
            {
                cmsCeremonyCategorys.Show(tvCeremonyCategorys, tvCeremonyCategorys.PointToClient(Cursor.Position));
            }
            else if (e.Button == MouseButtons.Left)
            {
                IsCreate = false;            
            }
        }

        private void tsmiCreate_Click(object sender, EventArgs e)
        {
            IsCreate = true;

            PanelFormEmpty();
            PanelFormSwitch(true);
        }

        private void btnConfirm_Click(object sender, EventArgs e)
        {
            if (IsCreate)
            {
                CeremonyCategorys ceremonycategory = new CeremonyCategorys();
                ceremonycategory.CeremonyCategoryID = Guid.NewGuid();
                ceremonycategory.Title = txtTitle.Text.Trim();
                ceremonycategory.Sort = (int)nudSort.Value;
                if (CurrentCeremonyCategoryID != Guid.Empty) ceremonycategory.ParentID = CurrentCeremonyCategoryID;

                ceremonycategorysService.Create(ceremonycategory);
                ceremonycategorysService.SaveChanges();

                TreeNode NewNode = new TreeNode();
                NewNode.Name = ceremonycategory.CeremonyCategoryID.ToString();
                NewNode.Text = ceremonycategory.Title.Trim();
                CurrentNode.Nodes.Add(NewNode);
                CurrentNode.ExpandAll();

                MessageBox.Show("新增法會成功！", Global.AppTitle);
            }
            else
            {
                CeremonyCategorys ceremonycategory = ceremonycategorysService.GetByID(CurrentCeremonyCategoryID);
                ceremonycategory.Title = txtTitle.Text.Trim();
                ceremonycategory.Sort = (int)nudSort.Value;

                ceremonycategorysService.Update(ceremonycategory);
                ceremonycategorysService.SaveChanges();

                CurrentNode.Text = txtTitle.Text.Trim();

                MessageBox.Show("編輯法會成功！", Global.AppTitle);
            }

            IsCreate = false;

            PanelFormEmpty();
            PanelFormSwitch(false);
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            IsCreate = false;

            PanelFormEmpty();
            PanelFormSwitch(false);
        }

        private void tsmiDelete_Click(object sender, EventArgs e)
        {
            IsCreate = false;

            CeremonyCategorys ceremonycategory = ceremonycategorysService.GetByID(CurrentCeremonyCategoryID);
            
            if(!ceremonycategory.Signups.Any() && !ceremonycategory.CeremonyCategorys1.Any())
            {
                ceremonycategorysService.Delete(CurrentCeremonyCategoryID);
                ceremonycategorysService.SaveChanges();

                CurrentNode.Remove();

                MessageBox.Show("刪除法會成功！", Global.AppTitle);
            }
            else
            {
                MessageBox.Show("已有報名或還有下層法會，無法刪除", Global.AppTitle);
            }

            PanelFormEmpty();
            PanelFormSwitch(false);
        }

        private void LoadCeremonyCategorys()
        {
            List<CeremonyCategorys> ceremonycategorys = ceremonycategorysService.Get().Where(a => a.ParentID == null).OrderBy(o => o.Sort).ToList();
            CreateRootNode(tvCeremonyCategorys, ceremonycategorys);
        }

        private void CreateRootNode(TreeView tv, List<CeremonyCategorys> list)
        {
            TreeNode Node = new TreeNode();
            Node.Text = "法會維護";
            Node.Name = "Node0";
            tv.Nodes.Add(Node);

            CreateNode(tv, list, Node);
        }

        private void CreateNode(TreeView tv, List<CeremonyCategorys> list, TreeNode Node)
        {
            TreeNode NewNode;
            foreach(CeremonyCategorys item in list)
            {
                NewNode = new TreeNode();
                NewNode.Name = item.CeremonyCategoryID.ToString();
                NewNode.Text = item.Title;
                Node.Nodes.Add(NewNode);

                CreateNode(tv, item.CeremonyCategorys1.OrderBy(o => o.Sort).ToList(), NewNode);
            }
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

                if (ctrl is NumericUpDown)
                {
                    NumericUpDown numericupdown = (NumericUpDown)ctrl;
                    numericupdown.Enabled = isenable;
                }
            }
        }

        private void PanelFormEmpty()
        {
            txtTitle.Text = string.Empty;
            nudSort.Value = 0;
        }
    }
}
