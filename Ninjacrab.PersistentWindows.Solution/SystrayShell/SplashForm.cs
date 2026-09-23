using System;
using System.Windows.Forms;

namespace PersistentWindows.SystrayShell
{
    public partial class SplashForm : Form
    {
        private readonly bool aboutMode;

        public SplashForm() : this(false)
        {
        }

        /// <summary>
        /// 建立啟動畫面或「關於」對話框。
        /// </summary>
        /// <param name="aboutMode">true 表示由選單開啟的「關於」對話框，不自動關閉。</param>
        public SplashForm(bool aboutMode)
        {
            this.aboutMode = aboutMode;
            InitializeComponent();

            if (aboutMode)
            {
                // 「關於」對話框由使用者自行關閉，不顯示倒數進度列
                this.Text = "關於 PersistentWindows";
                this.timer1.Enabled = false;
                this.progressBar1.Visible = false;
                this.closeButton.Visible = true;
                this.ControlBox = true;
            }
        }

        private void label1_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(Program.ProjectUrl);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            progressBar1.PerformStep();
            if (progressBar1.Value == progressBar1.Maximum)
            {
                this.Close();
            }
        }

        private void SplashForm_Load(object sender, EventArgs e)
        {
            this.label1.Text =
    $@"Persistent Windows
版本 {Application.ProductVersion}

作者：Min Yong Kim
貢獻者：Kang Yu、Sean Aitken
中文化：LaiYueTing";
        }

        private void closeButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void label2_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(Program.Contributors);
        }
    }
}
