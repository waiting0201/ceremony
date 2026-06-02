
namespace Ceremony
{
    partial class LoadPrepayForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.plForm = new System.Windows.Forms.Panel();
            this.dlBeliever = new System.Windows.Forms.ComboBox();
            this.dlYear = new System.Windows.Forms.ComboBox();
            this.dlCeremony = new System.Windows.Forms.ComboBox();
            this.label5 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.btnConfirm = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.dlSelectYear = new System.Windows.Forms.ComboBox();
            this.dlSelectCeremony = new System.Windows.Forms.ComboBox();
            this.plForm.SuspendLayout();
            this.SuspendLayout();
            // 
            // plForm
            // 
            this.plForm.Controls.Add(this.dlSelectCeremony);
            this.plForm.Controls.Add(this.dlSelectYear);
            this.plForm.Controls.Add(this.dlBeliever);
            this.plForm.Controls.Add(this.dlYear);
            this.plForm.Controls.Add(this.dlCeremony);
            this.plForm.Controls.Add(this.label5);
            this.plForm.Controls.Add(this.label4);
            this.plForm.Controls.Add(this.btnConfirm);
            this.plForm.Controls.Add(this.label3);
            this.plForm.Controls.Add(this.label2);
            this.plForm.Controls.Add(this.label1);
            this.plForm.Font = new System.Drawing.Font("微軟正黑體", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.plForm.Location = new System.Drawing.Point(12, 12);
            this.plForm.Name = "plForm";
            this.plForm.Size = new System.Drawing.Size(314, 240);
            this.plForm.TabIndex = 0;
            // 
            // dlBeliever
            // 
            this.dlBeliever.FormattingEnabled = true;
            this.dlBeliever.Location = new System.Drawing.Point(153, 41);
            this.dlBeliever.Name = "dlBeliever";
            this.dlBeliever.Size = new System.Drawing.Size(121, 28);
            this.dlBeliever.TabIndex = 11;
            // 
            // dlYear
            // 
            this.dlYear.FormattingEnabled = true;
            this.dlYear.Location = new System.Drawing.Point(26, 141);
            this.dlYear.Name = "dlYear";
            this.dlYear.Size = new System.Drawing.Size(109, 28);
            this.dlYear.TabIndex = 10;
            // 
            // dlCeremony
            // 
            this.dlCeremony.FormattingEnabled = true;
            this.dlCeremony.Location = new System.Drawing.Point(170, 141);
            this.dlCeremony.Name = "dlCeremony";
            this.dlCeremony.Size = new System.Drawing.Size(110, 28);
            this.dlCeremony.TabIndex = 9;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(138, 145);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(25, 20);
            this.label5.TabIndex = 8;
            this.label5.Text = "年";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("微軟正黑體", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.label4.ForeColor = System.Drawing.Color.Red;
            this.label4.Location = new System.Drawing.Point(140, 106);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(26, 21);
            this.label4.TabIndex = 6;
            this.label4.Text = "至";
            // 
            // btnConfirm
            // 
            this.btnConfirm.Location = new System.Drawing.Point(99, 182);
            this.btnConfirm.Name = "btnConfirm";
            this.btnConfirm.Size = new System.Drawing.Size(113, 41);
            this.btnConfirm.TabIndex = 5;
            this.btnConfirm.Text = "確認";
            this.btnConfirm.UseVisualStyleBackColor = true;
            this.btnConfirm.Click += new System.EventHandler(this.btnConfirm_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(153, 80);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(41, 20);
            this.label3.TabIndex = 4;
            this.label3.Text = "法會";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(122, 46);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(25, 20);
            this.label2.TabIndex = 2;
            this.label2.Text = "年";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(58, 16);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(201, 20);
            this.label1.TabIndex = 0;
            this.label1.Text = "即將載入以下法會預繳報名";
            // 
            // dlSelectYear
            // 
            this.dlSelectYear.FormattingEnabled = true;
            this.dlSelectYear.Location = new System.Drawing.Point(42, 41);
            this.dlSelectYear.Name = "dlSelectYear";
            this.dlSelectYear.Size = new System.Drawing.Size(74, 28);
            this.dlSelectYear.TabIndex = 12;
            // 
            // dlSelectCeremony
            // 
            this.dlSelectCeremony.FormattingEnabled = true;
            this.dlSelectCeremony.Location = new System.Drawing.Point(67, 76);
            this.dlSelectCeremony.Name = "dlSelectCeremony";
            this.dlSelectCeremony.Size = new System.Drawing.Size(82, 28);
            this.dlSelectCeremony.TabIndex = 13;
            // 
            // LoadPrepayForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(337, 259);
            this.Controls.Add(this.plForm);
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(353, 298);
            this.MinimumSize = new System.Drawing.Size(353, 298);
            this.Name = "LoadPrepayForm";
            this.ShowIcon = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "載入預繳 | 寶覺寺";
            this.Load += new System.EventHandler(this.LoadPrepayForm_Load);
            this.plForm.ResumeLayout(false);
            this.plForm.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel plForm;
        private System.Windows.Forms.Button btnConfirm;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox dlCeremony;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ComboBox dlYear;
        private System.Windows.Forms.ComboBox dlBeliever;
        private System.Windows.Forms.ComboBox dlSelectCeremony;
        private System.Windows.Forms.ComboBox dlSelectYear;
    }
}