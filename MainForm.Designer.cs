namespace Fat32Formator
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.cmbDrives = new System.Windows.Forms.ComboBox();
            this.lblDrives = new System.Windows.Forms.Label();
            this.lblFileSystem = new System.Windows.Forms.Label();
            this.cmbFileSystem = new System.Windows.Forms.ComboBox();
            this.lblVolumeLabel = new System.Windows.Forms.Label();
            this.txtVolumeLabel = new System.Windows.Forms.TextBox();
            this.chkQuickFormat = new System.Windows.Forms.CheckBox();
            this.btnStart = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.grpOptions = new System.Windows.Forms.GroupBox();
            this.pbProgress = new System.Windows.Forms.ProgressBar();
            this.grpOptions.SuspendLayout();
            this.SuspendLayout();
            
            // lblDrives
            this.lblDrives.AutoSize = true;
            this.lblDrives.Location = new System.Drawing.Point(12, 15);
            this.lblDrives.Name = "lblDrives";
            this.lblDrives.Size = new System.Drawing.Size(65, 15);
            this.lblDrives.TabIndex = 0;
            this.lblDrives.Text = "Urządzenie:";
            
            // cmbDrives
            this.cmbDrives.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbDrives.FormattingEnabled = true;
            this.cmbDrives.Location = new System.Drawing.Point(15, 33);
            this.cmbDrives.Name = "cmbDrives";
            this.cmbDrives.Size = new System.Drawing.Size(260, 23);
            this.cmbDrives.TabIndex = 1;
            
            // lblFileSystem
            this.lblFileSystem.AutoSize = true;
            this.lblFileSystem.Location = new System.Drawing.Point(12, 65);
            this.lblFileSystem.Name = "lblFileSystem";
            this.lblFileSystem.Size = new System.Drawing.Size(86, 15);
            this.lblFileSystem.TabIndex = 2;
            this.lblFileSystem.Text = "System plików:";
            
            // cmbFileSystem
            this.cmbFileSystem.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbFileSystem.Enabled = false;
            this.cmbFileSystem.FormattingEnabled = true;
            this.cmbFileSystem.Items.AddRange(new object[] { "FAT32 (Dla CDJ 2000)" });
            this.cmbFileSystem.Location = new System.Drawing.Point(15, 83);
            this.cmbFileSystem.Name = "cmbFileSystem";
            this.cmbFileSystem.Size = new System.Drawing.Size(260, 23);
            this.cmbFileSystem.TabIndex = 3;
            
            // lblVolumeLabel
            this.lblVolumeLabel.AutoSize = true;
            this.lblVolumeLabel.Location = new System.Drawing.Point(12, 115);
            this.lblVolumeLabel.Name = "lblVolumeLabel";
            this.lblVolumeLabel.Size = new System.Drawing.Size(107, 15);
            this.lblVolumeLabel.TabIndex = 4;
            this.lblVolumeLabel.Text = "Etykieta woluminu:";
            
            // txtVolumeLabel
            this.txtVolumeLabel.Location = new System.Drawing.Point(15, 133);
            this.txtVolumeLabel.Name = "txtVolumeLabel";
            this.txtVolumeLabel.Size = new System.Drawing.Size(260, 23);
            this.txtVolumeLabel.TabIndex = 5;
            this.txtVolumeLabel.Text = "PENDRIVE";
            
            // grpOptions
            this.grpOptions.Controls.Add(this.chkQuickFormat);
            this.grpOptions.Location = new System.Drawing.Point(15, 170);
            this.grpOptions.Name = "grpOptions";
            this.grpOptions.Size = new System.Drawing.Size(260, 55);
            this.grpOptions.TabIndex = 6;
            this.grpOptions.TabStop = false;
            this.grpOptions.Text = "Opcje formatowania";
            
            // chkQuickFormat
            this.chkQuickFormat.AutoSize = true;
            this.chkQuickFormat.Checked = true;
            this.chkQuickFormat.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkQuickFormat.Location = new System.Drawing.Point(15, 22);
            this.chkQuickFormat.Name = "chkQuickFormat";
            this.chkQuickFormat.Size = new System.Drawing.Size(142, 19);
            this.chkQuickFormat.TabIndex = 0;
            this.chkQuickFormat.Text = "Szybkie formatowanie";
            this.chkQuickFormat.UseVisualStyleBackColor = true;

            // pbProgress
            this.pbProgress.Location = new System.Drawing.Point(15, 235);
            this.pbProgress.Name = "pbProgress";
            this.pbProgress.Size = new System.Drawing.Size(260, 15);
            this.pbProgress.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            this.pbProgress.TabIndex = 9;
            
            // btnStart
            this.btnStart.Location = new System.Drawing.Point(119, 260);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(75, 25);
            this.btnStart.TabIndex = 7;
            this.btnStart.Text = "Start";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.BtnStart_Click);
            
            // btnClose
            this.btnClose.Location = new System.Drawing.Point(200, 260);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(75, 25);
            this.btnClose.TabIndex = 8;
            this.btnClose.Text = "Zamknij";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.BtnClose_Click);
            
            // MainForm
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(287, 295);
            this.Controls.Add(this.pbProgress);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.btnStart);
            this.Controls.Add(this.grpOptions);
            this.Controls.Add(this.txtVolumeLabel);
            this.Controls.Add(this.lblVolumeLabel);
            this.Controls.Add(this.cmbFileSystem);
            this.Controls.Add(this.lblFileSystem);
            this.Controls.Add(this.cmbDrives);
            this.Controls.Add(this.lblDrives);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Format (FAT32)";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.grpOptions.ResumeLayout(false);
            this.grpOptions.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.ComboBox cmbDrives;
        private System.Windows.Forms.Label lblDrives;
        private System.Windows.Forms.Label lblFileSystem;
        private System.Windows.Forms.ComboBox cmbFileSystem;
        private System.Windows.Forms.Label lblVolumeLabel;
        private System.Windows.Forms.TextBox txtVolumeLabel;
        private System.Windows.Forms.GroupBox grpOptions;
        private System.Windows.Forms.CheckBox chkQuickFormat;
        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.ProgressBar pbProgress;
    }
}
