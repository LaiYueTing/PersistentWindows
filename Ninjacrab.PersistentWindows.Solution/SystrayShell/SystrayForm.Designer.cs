using System.Windows.Forms;

namespace PersistentWindows.SystrayShell
{
    static class Globals
    {
        //use Application.ProductVersion instead
        //public const string Version = "";
    }

    partial class SystrayForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        public System.Windows.Forms.NotifyIcon notifyIconMain;
        public System.Windows.Forms.ContextMenuStrip contextMenuStripSysTray;

        private ToolStripMenuItem captureToolStripMenuItem;
        private ToolStripMenuItem restoreToolStripMenuItem;
        private ToolStripMenuItem restoreAllParkedMenuItem;
        private ToolStripMenuItem captureSnapshotMenuItem;
        private ToolStripMenuItem restoreSnapshotMenuItem;
        private ToolStripMenuItem manageSnapshotMenuItem;
        private ToolStripMenuItem pauseResumeToolStripMenuItem;
        public  ToolStripMenuItem toggleIconMenuItem;
        public  ToolStripMenuItem invokeWebCommander;
        public  ToolStripMenuItem upgradeNoticeMenuItem;
        private ToolStripMenuItem helpToolStripMenuItem;
        private ToolStripMenuItem aboutToolStripMenuItem;
        private ToolStripMenuItem exitToolStripMenuItem;
        private ToolStripSeparator[] menuSeparators = new System.Windows.Forms.ToolStripSeparator[5];

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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SystrayForm));
            this.notifyIconMain = new System.Windows.Forms.NotifyIcon(this.components);
            this.contextMenuStripSysTray = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.helpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.captureToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.restoreToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.captureSnapshotMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.restoreSnapshotMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.manageSnapshotMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.restoreAllParkedMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pauseResumeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toggleIconMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.invokeWebCommander = new System.Windows.Forms.ToolStripMenuItem();
            this.upgradeNoticeMenuItem = new System.Windows.Forms.ToolStripMenuItem();

            for (int i = 0; i < menuSeparators.Length; ++i)
            {
                this.menuSeparators[i] = new System.Windows.Forms.ToolStripSeparator();
                this.menuSeparators[i].Name = $"toolStripMenuItem{i}";
            }
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuStripSysTray.SuspendLayout();
            this.SuspendLayout();
            //
            // notifyIconMain
            //
            this.notifyIconMain.ContextMenuStrip = this.contextMenuStripSysTray;
            //this.notifyIconMain.Icon = ((System.Drawing.Icon)(resources.GetObject("notifyIconMain.Icon")));
            this.notifyIconMain.Icon = Program.IdleIcon;
            this.notifyIconMain.Text = $"{Application.ProductName} {Application.ProductVersion}";
            this.notifyIconMain.BalloonTipTitle = "";
            this.notifyIconMain.BalloonTipText = "正在還原視窗，請稍候 ...";
            this.notifyIconMain.BalloonTipIcon = ToolTipIcon.Info;
            if (!Program.Gui)
                this.notifyIconMain.Visible = false;
            this.notifyIconMain.MouseDown += new System.Windows.Forms.MouseEventHandler(this.IconMouseDown);
            this.notifyIconMain.MouseUp += new System.Windows.Forms.MouseEventHandler(this.IconMouseUp);

            this.notifyIconMain.MouseClick += new System.Windows.Forms.MouseEventHandler(this.IconMouseClick);

            this.notifyIconMain.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.IconMouseDoubleClick);

            //
            // contextMenuStripSysTray
            //
            this.contextMenuStripSysTray.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                /*
                this.manageLayoutProfile,
                this.toolStripMenuItem[3],
                */
                this.captureToolStripMenuItem,
                this.restoreToolStripMenuItem,
                this.menuSeparators[0],
                this.captureSnapshotMenuItem,
                this.restoreSnapshotMenuItem,
                this.manageSnapshotMenuItem,
                this.menuSeparators[1],
                this.restoreAllParkedMenuItem,
                this.menuSeparators[2],
                this.pauseResumeToolStripMenuItem,
                this.toggleIconMenuItem,
                this.invokeWebCommander,
                this.menuSeparators[3],
                this.upgradeNoticeMenuItem,
                this.helpToolStripMenuItem,
                this.aboutToolStripMenuItem,
                this.menuSeparators[4],
                this.exitToolStripMenuItem});
            this.contextMenuStripSysTray.Name = "contextMenuStripSysTray";
            this.contextMenuStripSysTray.Font = PersistentWindows.Common.UiFont.Get(9F);
            this.contextMenuStripSysTray.Opened += new System.EventHandler(this.ContextMenuOpened);

            // capture
            //
            this.captureToolStripMenuItem.Name = "capture";
            this.captureToolStripMenuItem.Text = "擷取視窗佈局至硬碟(&C)";
            this.captureToolStripMenuItem.Click += new System.EventHandler(this.CaptureWindowToDisk);

            // restore
            //
            this.restoreToolStripMenuItem.Name = "restore";
            this.restoreToolStripMenuItem.Text = "從硬碟還原視窗佈局(&R)";
            this.restoreToolStripMenuItem.Click += new System.EventHandler(this.RestoreWindowFromDisk);

            // restore all minimized
            //
            this.restoreAllParkedMenuItem.Name = "restoreAllMinimized";
            this.restoreAllParkedMenuItem.Text = "還原所有最小化的視窗(&N)";
            this.restoreAllParkedMenuItem.Click += new System.EventHandler(this.RestoreAllParkedClickHandler);

            // capture snapshot
            //
            this.captureSnapshotMenuItem.Name = "capture snapshot";
            this.captureSnapshotMenuItem.Text = "擷取快照(&P)";
            this.captureSnapshotMenuItem.Click += new System.EventHandler(this.CaptureSnapshot);

            // restore
            //
            this.restoreSnapshotMenuItem.Name = "restore snapshot";
            this.restoreSnapshotMenuItem.Text = "還原快照(&T)";
            this.restoreSnapshotMenuItem.Click += new System.EventHandler(this.RestoreSnapshot);
            this.restoreSnapshotMenuItem.Enabled = false;

            // 快照管理
            //
            this.manageSnapshotMenuItem.Name = "manage snapshot";
            this.manageSnapshotMenuItem.Text = "快照管理(&M) ...";
            this.manageSnapshotMenuItem.Click += new System.EventHandler(this.ManageSnapshot);

            // suspend/resume auto restore
            //
            this.pauseResumeToolStripMenuItem.Name = "suspend/resume";
            this.pauseResumeToolStripMenuItem.Text = "暫停自動還原(&U)";
            this.pauseResumeToolStripMenuItem.Click += new System.EventHandler(this.PauseResumeAutoRestore);

            // toggle icon
            //
            this.toggleIconMenuItem.Name = "toggle icon";
            this.toggleIconMenuItem.Text = "試用自訂圖示(&I)";
            this.toggleIconMenuItem.Click += new System.EventHandler(this.ToggleIcon);

            // web commander
            this.invokeWebCommander.Name = "web commander on/off";
            this.invokeWebCommander.Text = "停用網頁指令視窗(&W)";
            this.invokeWebCommander.Click += new System.EventHandler(this.WebCommander);
            if (!Program.hotkey_window)
                this.invokeWebCommander.Visible = false;
            //
            // aboutToolStripMenuItem
            //
            //
            // helpToolStripMenuItem
            //
            this.helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            this.helpToolStripMenuItem.Text = "說明(&H)";
            this.helpToolStripMenuItem.Click += new System.EventHandler(this.HelpToolStripMenuItemClickHandler);

            //
            // aboutToolStripMenuItem
            //
            this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            this.aboutToolStripMenuItem.Text = "關於(&A) ...";
            this.aboutToolStripMenuItem.Click += new System.EventHandler(this.AboutToolStripMenuItemClickHandler);

            // pause/resume upgrade notice
            //this.upgradeNoticeMenuItem.Text = "Disable upgrade notice";
            this.upgradeNoticeMenuItem.Click += new System.EventHandler(this.PauseResumeUpgradeNotice);

            //
            // exitToolStripMenuItem
            //
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Text = "結束(&X)";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.ExitToolStripMenuItemClickHandler);
            //
            // SystrayForm
            //
            /*
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(284, 261);
            */
            this.Name = "SystrayForm";
            this.ShowInTaskbar = false;
            this.WindowState = FormWindowState.Minimized;
            this.contextMenuStripSysTray.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
    }
}

