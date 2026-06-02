
namespace Ceremony
{
    partial class MainForm
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
            this.btnBeliever = new System.Windows.Forms.Button();
            this.btnNewSignup = new System.Windows.Forms.Button();
            this.btnSignup = new System.Windows.Forms.Button();
            this.btnAdmins = new System.Windows.Forms.Button();
            this.btnPreload = new System.Windows.Forms.Button();
            this.btnBackup = new System.Windows.Forms.Button();
            this.labVersion = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // btnBeliever
            // 
            this.btnBeliever.Font = new System.Drawing.Font("微軟正黑體", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.btnBeliever.Location = new System.Drawing.Point(12, 12);
            this.btnBeliever.Name = "btnBeliever";
            this.btnBeliever.Size = new System.Drawing.Size(210, 45);
            this.btnBeliever.TabIndex = 1;
            this.btnBeliever.Text = "信眾維護";
            this.btnBeliever.UseVisualStyleBackColor = true;
            this.btnBeliever.Click += new System.EventHandler(this.btnBeliever_Click);
            // 
            // btnNewSignup
            // 
            this.btnNewSignup.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.btnNewSignup.Font = new System.Drawing.Font("微軟正黑體", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.btnNewSignup.Location = new System.Drawing.Point(12, 63);
            this.btnNewSignup.Name = "btnNewSignup";
            this.btnNewSignup.Size = new System.Drawing.Size(210, 45);
            this.btnNewSignup.TabIndex = 2;
            this.btnNewSignup.Text = "新增報名";
            this.btnNewSignup.UseVisualStyleBackColor = false;
            this.btnNewSignup.Click += new System.EventHandler(this.btnNewSignup_Click);
            // 
            // btnSignup
            // 
            this.btnSignup.Font = new System.Drawing.Font("微軟正黑體", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.btnSignup.Location = new System.Drawing.Point(12, 114);
            this.btnSignup.Name = "btnSignup";
            this.btnSignup.Size = new System.Drawing.Size(210, 45);
            this.btnSignup.TabIndex = 3;
            this.btnSignup.Text = "報名維護";
            this.btnSignup.UseVisualStyleBackColor = true;
            this.btnSignup.Click += new System.EventHandler(this.btnSignup_Click);
            // 
            // btnAdmins
            // 
            this.btnAdmins.Font = new System.Drawing.Font("微軟正黑體", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.btnAdmins.Location = new System.Drawing.Point(12, 267);
            this.btnAdmins.Name = "btnAdmins";
            this.btnAdmins.Size = new System.Drawing.Size(210, 45);
            this.btnAdmins.TabIndex = 4;
            this.btnAdmins.Text = "管理者維護";
            this.btnAdmins.UseVisualStyleBackColor = true;
            this.btnAdmins.Click += new System.EventHandler(this.btnAdmins_Click);
            // 
            // btnPreload
            // 
            this.btnPreload.Font = new System.Drawing.Font("微軟正黑體", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.btnPreload.Location = new System.Drawing.Point(12, 165);
            this.btnPreload.Name = "btnPreload";
            this.btnPreload.Size = new System.Drawing.Size(210, 45);
            this.btnPreload.TabIndex = 5;
            this.btnPreload.Text = "載入預繳";
            this.btnPreload.UseVisualStyleBackColor = true;
            this.btnPreload.Click += new System.EventHandler(this.btnPreload_Click);
            // 
            // btnBackup
            // 
            this.btnBackup.Font = new System.Drawing.Font("微軟正黑體", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.btnBackup.Location = new System.Drawing.Point(12, 216);
            this.btnBackup.Name = "btnBackup";
            this.btnBackup.Size = new System.Drawing.Size(210, 45);
            this.btnBackup.TabIndex = 6;
            this.btnBackup.Text = "資料備份";
            this.btnBackup.UseVisualStyleBackColor = true;
            this.btnBackup.Click += new System.EventHandler(this.btnBackup_Click);
            // 
            // labVersion
            // 
            this.labVersion.Location = new System.Drawing.Point(12, 319);
            this.labVersion.Name = "labVersion";
            this.labVersion.Size = new System.Drawing.Size(210, 16);
            this.labVersion.TabIndex = 7;
            this.labVersion.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(235, 344);
            this.Controls.Add(this.labVersion);
            this.Controls.Add(this.btnBackup);
            this.Controls.Add(this.btnPreload);
            this.Controls.Add(this.btnAdmins);
            this.Controls.Add(this.btnSignup);
            this.Controls.Add(this.btnNewSignup);
            this.Controls.Add(this.btnBeliever);
            this.Font = new System.Drawing.Font("微軟正黑體", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.ShowIcon = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "主選單 | 寶覺寺";
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Button btnBeliever;
        private System.Windows.Forms.Button btnNewSignup;
        private System.Windows.Forms.Button btnSignup;
        private System.Windows.Forms.Button btnAdmins;
        private System.Windows.Forms.Button btnPreload;
        private System.Windows.Forms.Button btnBackup;
        private System.Windows.Forms.Label labVersion;
    }
}