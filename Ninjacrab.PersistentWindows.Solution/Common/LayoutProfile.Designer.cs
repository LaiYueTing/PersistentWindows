using System.Windows.Forms;
using System.Drawing;

namespace PersistentWindows.Common
{
    partial class LayoutProfile
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
            this.SnapshotName = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            //
            // SnapshotName
            //
            this.SnapshotName.Font = UiFont.Get(12F);
            this.SnapshotName.Location = new System.Drawing.Point(230, 130);
            this.SnapshotName.MaxLength = 1;
            this.SnapshotName.Name = "SnapshotName";
            this.SnapshotName.Size = new System.Drawing.Size(45, 30);
            this.SnapshotName.TabIndex = 1;
            this.SnapshotName.TextChanged += new System.EventHandler(this.ProfileName_TextChanged);
            //
            // label1
            //
            this.label1.Font = UiFont.Get(12F);
            this.label1.Location = new System.Drawing.Point(48, 79);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(413, 30);
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.label1.TabIndex = 9;
            this.label1.Text = "請輸入一個數字或字母作為快照名稱";
            //
            // LayoutProfile
            //
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Inherit;
            this.ClientSize = new System.Drawing.Size(510, 225);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.SnapshotName);
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "LayoutProfile";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "輸入快照名稱";
            this.Load += new System.EventHandler(this.LayoutProfile_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private TextBox SnapshotName;
        private Label label1;
    }
}