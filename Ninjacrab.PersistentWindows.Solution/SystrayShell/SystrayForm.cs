using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using System.Net;
using System.Timers;
using System.IO;
using System.IO.Compression;
using System.Drawing;
using System.Reflection;

using PersistentWindows.Common;
using PersistentWindows.Common.Diagnostics;
using PersistentWindows.Common.WinApiBridge;

namespace PersistentWindows.SystrayShell
{
    public partial class SystrayForm : Form
    {
        private const int MaxSnapshots = 38; // 0-9, a-z, ` and final one for undo

        private bool pauseAutoRestore = false;
        public bool toggleIcon = false;

        // 選單文字已在地化，因此另以旗標記錄狀態，避免比對顯示字串
        private bool upgradeNoticeEnabled = true;
        private bool upgradeAvailable = false;
        private bool webCommanderEnabled = true;

        private int skipUpgradeCounter = 0;
        private bool initialCheckUpgrade = true;
        private bool pauseUpgradeCounter = false;

        public bool autoUpgrade = false;

        private int ctrlKeyPressed = 0;
        private int shiftKeyPressed = 0;
        private int altKeyPressed = 0;
        private int clickCount = 0;
        private bool firstClick = false;
        private bool doubleClick = false;

        private DateTime clickTime;

        private System.Timers.Timer clickDelayTimer;

        // 系統匣選單的自動關閉看門狗
        //
        // 只靠 SetForegroundWindow 讓選單成為前景視窗並不可靠：
        // 一旦先開過強制回應對話框（例如快照管理），行程的前景狀態會改變，
        // SetForegroundWindow 可能靜默失敗，選單就再也收不到停用通知而卡在畫面上。
        // 這裡改為在選單開啟期間輪詢前景視窗，只要前景不是選單本身就主動關閉，
        // 不依賴 WM_ACTIVATEAPP 是否送達。
        private System.Windows.Forms.Timer menuAutoCloseTimer;
        private int menuAutoCloseGrace;

        private Dictionary<string, bool> upgradeDownloaded = new Dictionary<string, bool>();

        public SystrayForm(bool enable_upgrade_notice)
        {
            InitializeComponent();

            if (File.Exists(Program.DisableUpgradeNotice))
            {
                upgradeNoticeEnabled = false;
                upgradeNoticeMenuItem.Text = "啟用升級通知(&G)";
            }
            else if (!enable_upgrade_notice)
            {
                File.Create(Program.DisableUpgradeNotice);
                upgradeNoticeEnabled = false;
                upgradeNoticeMenuItem.Text = "啟用升級通知(&G)";
            }
            else
            {
                upgradeNoticeEnabled = true;
                upgradeNoticeMenuItem.Text = "停用升級通知(&G)";
            }

            if (File.Exists(Program.DisableWebpageCommander))
            {
                webCommanderEnabled = false;
                invokeWebCommander.Text = "啟用網頁指令視窗(&W)";
            }

            menuAutoCloseTimer = new System.Windows.Forms.Timer();
            menuAutoCloseTimer.Interval = 200;
            menuAutoCloseTimer.Tick += MenuAutoCloseTick;

            contextMenuStripSysTray.Closed += ContextMenuClosed;

            clickDelayTimer = new System.Timers.Timer(1000);
            clickDelayTimer.Elapsed += ClickTimerCallBack;
            clickDelayTimer.SynchronizingObject = this.contextMenuStripSysTray;
            clickDelayTimer.AutoReset = false;
            clickDelayTimer.Enabled = false;
        }

        public void StartTimer(int milliseconds)
        {
            clickDelayTimer.Interval = milliseconds;
            clickDelayTimer.AutoReset = false;
            clickDelayTimer.Enabled = true;
        }

        private void ClickTimerCallBack(Object source, ElapsedEventArgs e)
        {
            if (clickCount == 0)
            {
                // fix context menu position
                //contextMenuStripSysTray.Show(Cursor.Position);
                return;
            }

            pauseUpgradeCounter = true;

            Keys keyPressed = Keys.None;
            //check 0-9 key pressed
            for (Keys i = Keys.D0; i <= Keys.D9; ++i)
            {
                if (User32.GetAsyncKeyState((int)i) != 0)
                {
                    keyPressed = i;
                    break;
                }
            }

            //check a-z pressed
            if (keyPressed == Keys.None)
            for (Keys i = Keys.A; i <= Keys.Z; ++i)
            {
                if (User32.GetAsyncKeyState((int)i) != 0)
                {
                    keyPressed = i;
                    break;
                }
            }

            if (keyPressed == Keys.None)
            {
                if (User32.GetAsyncKeyState((int)Keys.Oem3) != 0)
                {
                    keyPressed = Keys.Oem3;
                }
            }

            int totalSpecialKeyPressed = shiftKeyPressed + altKeyPressed;

            if (clickCount > 2)
            {
            }
            else if (totalSpecialKeyPressed > clickCount)
            {
                //no more than one key can be pressed
            }
            else if (altKeyPressed == clickCount && altKeyPressed != 0 && ctrlKeyPressed == 0)
            {
                //restore previous workspace (not necessarily a snapshot)
                Program.RestoreSnapshot(MaxSnapshots - 1);
            }
            else
            {
                if (keyPressed == Keys.None)
                {
                    if (clickCount == 1 && firstClick && !doubleClick)
                    {
                        if (ctrlKeyPressed > 0 && altKeyPressed > 0 && shiftKeyPressed == 0)
                            Program.FgWindowToBottom();
                        else if (ctrlKeyPressed > 0 && altKeyPressed == 0 && shiftKeyPressed == 0)
                            Program.RecallLastKilledPosition();
                        else if (ctrlKeyPressed == 0 && altKeyPressed == 0 && shiftKeyPressed > 0)
                            Program.CenterWindow();
                        else if (ctrlKeyPressed == 0 && altKeyPressed == 0 && shiftKeyPressed == 0)
                            //restore unnamed(default) snapshot
                            Program.RestoreSnapshot(0);
                    }
                    else if (clickCount == 2 && firstClick && doubleClick)
                        Program.CaptureSnapshot(0, delayCapture: shiftKeyPressed > 0);
                }
                else
                {
                    int snapshot = -1;
                    if (keyPressed == Keys.Oem3)
                        snapshot = MaxSnapshots - 2;
                    else if (keyPressed >= Keys.D0 && keyPressed <= Keys.D9)
                        snapshot = keyPressed - Keys.D0;
                    else if (keyPressed >= Keys.A && keyPressed <= Keys.Z)
                        snapshot = keyPressed - Keys.A + 10;

                    if (snapshot < 0)
                    {
                        //invalid key pressed
                    }
                    else if (clickCount == 1 && firstClick && !doubleClick)
                    {
                        Program.RestoreSnapshot(snapshot);
                    }
                    else if (clickCount == 2 && firstClick && doubleClick)
                    {
                        Program.CaptureSnapshot(snapshot, delayCapture: shiftKeyPressed > 0);
                    }
                }
            }

            clickCount = 0;
            doubleClick = false;
            firstClick = false;
            ctrlKeyPressed = 0;
            shiftKeyPressed = 0;
            altKeyPressed = 0;
        }

        //private void TimerEventProcessor(Object myObject, EventArgs myEventArgs)
        public void UpdateMenuEnable(bool enableRestoreFromDB, bool checkUpgrade)
        {
            if (enableRestoreFromDB)
                restoreToolStripMenuItem.Image = null;
            else
                restoreToolStripMenuItem.Image = Properties.Resources.question;

            if (checkUpgrade && upgradeNoticeEnabled && !upgradeAvailable)
            {
                if (pauseUpgradeCounter)
                {
                    pauseUpgradeCounter = false;
                }
                else
                {
                    if (skipUpgradeCounter == 0)
                    {
                        if (initialCheckUpgrade)
                        {
                            initialCheckUpgrade = false;
                            var dst_dir = Path.Combine($"{Program.AppdataFolder}", "upgrade");
                            var upgrade_exe = Path.Combine(dst_dir, $"{Application.ProductName}.exe");
                            if (Directory.Exists(dst_dir) && File.Exists(upgrade_exe))
                            {
                                var version = AssemblyName.GetAssemblyName(upgrade_exe).Version;
                                string[] latest = version.ToString().Split('.');
                                int latest_major = Int32.Parse(latest[0]);
                                int latest_minor = Int32.Parse(latest[1]);

                                string[] current = Application.ProductVersion.Split('.');
                                int current_major = Int32.Parse(current[0]);
                                int current_minor = Int32.Parse(current[1]);

                                if (latest_major > current_major ||
                                    latest_major == current_major && latest_minor > current_minor)
                                {
                                    //upgrade version already downloaded, skip the initial notice to give user more time to make decision
                                    skipUpgradeCounter++;
                                    return;
                                }
                            }
                        }
                        CheckUpgradeSafe();
                    }

                    skipUpgradeCounter = (skipUpgradeCounter + 1) % 31;
                }
            }
        }

        public void EnableSnapshotRestore(bool enable)
        {
            restoreSnapshotMenuItem.Enabled = enable;
        }

        private void CheckUpgradeSafe()
        {
            try
            {
                CheckUpgrade();
            }
            catch (Exception ex)
            {
                Program.LogError(ex.ToString());
            }
        }

        private void CheckUpgrade()
        {
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var cli = new WebClient();
            string data = cli.DownloadString($"{Program.ProjectUrl}/releases");

            string latest_pattern = "releases/latest";
            int index = data.IndexOf(latest_pattern);
            index -= 256;
            data = data.Substring(index, 256);
            string pattern = "releases/tag/";
            index = data.IndexOf(pattern);
            string latestVersion = data.Substring(index + pattern.Length, data.Substring(index + pattern.Length, 6).LastIndexOf('"'));

            string[] latest = latestVersion.Split('.');
            int latest_major = Int32.Parse(latest[0]);
            int latest_minor = Int32.Parse(latest[1]);

            string[] current = Application.ProductVersion.Split('.');
            int current_major = Int32.Parse(current[0]);
            int current_minor = Int32.Parse(current[1]);

            if (current_major < latest_major
                || current_major == latest_major && current_minor < latest_minor)
            {
                notifyIconMain.ShowBalloonTip(5000, $"{Application.ProductName} {latestVersion} 版本可供升級", "可在選單中停用升級通知", ToolTipIcon.Info);
                upgradeAvailable = true;
                upgradeNoticeMenuItem.Text = $"升級至 {latestVersion}(&G)";

                if (!upgradeDownloaded.ContainsKey(latestVersion))
                {
                    string url = Program.ProjectUrl + "/releases";
                    var os_version = Environment.OSVersion;
                    if (os_version.Version.Major < 10)
                        Process.Start(url);
                    else if (os_version.Version.Build < 22000)
                        Process.Start(url);
                    /* windows 11
                    else
                        Process.Start(new ProcessStartInfo(url));
                    */

                    var src_file = $"{Program.ProjectUrl}/releases/download/{latestVersion}/{System.Windows.Forms.Application.ProductName}{latestVersion}.zip";
                    var dst_file = $"{Program.AppdataFolder}/upgrade.zip";
                    var dst_dir = Path.Combine($"{Program.AppdataFolder}", "upgrade");
                    var install_dir = Application.StartupPath;

                    {
                        cli.DownloadFile(src_file, dst_file);
                        if (Directory.Exists(dst_dir))
                            Directory.Delete(dst_dir, true);
                        ZipFile.ExtractToDirectory(dst_file, dst_dir);
                        upgradeDownloaded[latestVersion] = true;

                        string batFile = Path.Combine(Program.AppdataFolder, $"pw_upgrade.bat");
                        string content = Program.WaitPwFinish;
                        content += $"\ncopy /Y \"{dst_dir}\\*.*\" \"{install_dir}\"";
                        content += "\nstart \"\" /B \"" + Path.Combine(install_dir, Application.ProductName) + ".exe\" " + Program.CmdArgs;
                        BatchFile.Write(batFile, content);

                        if (autoUpgrade)
                            Upgrade();
                        else
                            notifyIconMain.Icon = Program.UpdateIcon;
                    }
                }
            }
        }

        private void Exit()
        {
            var process = Process.GetCurrentProcess();
            process.PriorityClass = ProcessPriorityClass.High;

            Program.WriteDataDump();
            Log.Event("工作階段結束");

            this.notifyIconMain.Visible = false;
            //this.notifyIconMain.Icon = null;

            Log.Exit();
            Program.Stop();
            Application.Exit();
        }

        private void Upgrade()
        {
            Program.WriteDataDump();

            string batFile = Path.Combine(Program.AppdataFolder, "pw_upgrade.bat");
            Process.Start(batFile);
            Exit();
        }

        private void CaptureWindowToDisk(object sender, EventArgs e)
        {
            Program.CaptureToDisk();
            restoreToolStripMenuItem.Image = null;
        }

        private void RestoreWindowFromDisk(object sender, EventArgs e)
        {
            Program.RestoreFromDisk(restoreToolStripMenuItem.Image != null);
        }

        private void CaptureSnapshot(object sender, EventArgs e)
        {
            bool shift_key_pressed = (User32.GetKeyState(0x10) & 0x8000) != 0;
            char snapshot_char = Program.EnterSnapshotName();
            int id = Program.SnapshotCharToId(snapshot_char);
            if (id != -1)
                Program.CaptureSnapshot(id, prompt : false, delayCapture: shift_key_pressed);
        }

        private void RestoreSnapshot(object sender, EventArgs e)
        {
            char snapshot_char = Program.EnterSnapshotName();
            int id = Program.SnapshotCharToId(snapshot_char);
            if (id != -1)
            {
                // for debug issue #109 only
                //Program.ChangeZorderMethod();

                Program.RestoreSnapshot(id);
            }
        }


        private void ManageSnapshot(object sender, EventArgs e)
        {
            Program.ShowSnapshotManager();
        }

        private void PauseResumeAutoRestore(object sender, EventArgs e)
        {
            if (pauseAutoRestore)
            {
                Program.ResumeAutoRestore();
                pauseAutoRestore = false;
                pauseResumeToolStripMenuItem.Text = "暫停自動還原(&U)";
            }
            else
            {
                pauseAutoRestore = true;
                Program.PauseAutoRestore();
                pauseResumeToolStripMenuItem.Text = "恢復自動還原(&U)";
            }
        }

        private void WebCommander(object sender, EventArgs e)
        {
            if ((User32.GetKeyState(0x11) & 0x8000) != 0)
                HotKeyForm.InvokeFromMenu();
            else if (webCommanderEnabled)
            {
                File.Create(Program.DisableWebpageCommander);
                webCommanderEnabled = false;
                this.invokeWebCommander.Text = "啟用網頁指令視窗(&W)";
                HotKeyForm.Stop();
            }
            else
            {
                try
                {
                    File.Delete(Program.DisableWebpageCommander);
                }
                catch (Exception ex)
                {
                    Log.Error(ex.ToString());
                }

                webCommanderEnabled = true;
                this.invokeWebCommander.Text = "停用網頁指令視窗(&W)";
                HotKeyForm.Start(Program.hotkey);
            }
        }

        private void ToggleIcon(object sender, EventArgs e)
        {
            if (toggleIcon)
            {
                notifyIconMain.Icon = Program.IdleIcon;
                toggleIcon = !toggleIcon;
                toggleIconMenuItem.Text = "試用自訂圖示(&I)";
            }
            else
            {
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    //openFileDialog.InitialDirectory = "c:\\";
                    openFileDialog.Filter = "*.ico, *.png, *.jpg, *.bmp, *.gif | *.ico;*.png;*.jpg;*.bmp;*.gif | *.ico | *.ico";
                    openFileDialog.FilterIndex = 1;
                    openFileDialog.RestoreDirectory = true;

                    if (openFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        //Get the path of specified file
                        string filePath = openFileDialog.FileName;
                        if (String.IsNullOrEmpty(filePath))
                            return;
                        if (filePath.EndsWith(".ico"))
                            notifyIconMain.Icon = new Icon(filePath);
                        else
                        {
                            using (var bitmap = new Bitmap(filePath))
                            {
                                IntPtr hIcon = bitmap.GetHicon();
                                notifyIconMain.Icon = Icon.FromHandle(hIcon).Clone() as Icon;
                                User32.DestroyIcon(hIcon);
                            }
                        }
                        toggleIcon = !toggleIcon;
                        toggleIconMenuItem.Text = "停用自訂圖示(&I)";
                    }
                }
            }
        }

        private void PauseResumeUpgradeNotice(Object sender, EventArgs e)
        {
            if (upgradeAvailable)
            {
                Upgrade();
            }
            else if (!upgradeNoticeEnabled)
            {
                upgradeNoticeEnabled = true;
                upgradeNoticeMenuItem.Text = "停用升級通知(&G)";
                CheckUpgradeSafe();
                try
                {
                    File.Delete(Program.DisableUpgradeNotice);
                }
                catch (Exception ex)
                {
                    Log.Error(ex.ToString());
                }
            }
            else //選單目前為「停用升級通知」
            {
                File.Create(Program.DisableUpgradeNotice);
                upgradeNoticeEnabled = false;
                upgradeNoticeMenuItem.Text = "啟用升級通知(&G)";
            }
        }

        private void ViewLogMenuItemClickHandler(object sender, EventArgs e)
        {
            Program.ShowLogViewer();
        }

        private void HelpToolStripMenuItemClickHandler(object sender, EventArgs e)
        {
            Process.Start(Program.ProjectUrl + "/blob/master/Help.md");
        }

        private void AboutToolStripMenuItemClickHandler(object sender, EventArgs e)
        {
            Program.ShowAboutBox();
        }

        /// <summary>
        /// 系統匣選單彈出時把自己提升為前景視窗。
        /// 若不這麼做，使用者點選桌面或其他視窗時選單不會自動收起。
        /// </summary>
        private void ContextMenuOpened(object sender, EventArgs e)
        {
            // 系統匣圖示彈出的選單，其擁有者視窗並非前景視窗，
            // 因此 ToolStripDropDown 收不到停用通知，點選別處時不會收起。
            // 先把選單本身提升為前景視窗，再依 MSDN 的建議補送一則 WM_NULL，
            // 讓工作列的訊息佇列重新評估前景狀態。
            IntPtr handle = contextMenuStripSysTray.Handle;
            User32.SetForegroundWindow(handle);
            User32.PostMessageW(handle, User32.WM_NULL, IntPtr.Zero, IntPtr.Zero);

            // 前兩次跳過，讓選單有時間取得前景
            menuAutoCloseGrace = 2;
            menuAutoCloseTimer.Start();
        }

        private void ContextMenuClosed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            menuAutoCloseTimer.Stop();
        }

        private void MenuAutoCloseTick(object sender, EventArgs e)
        {
            if (!contextMenuStripSysTray.Visible)
            {
                menuAutoCloseTimer.Stop();
                return;
            }

            if (menuAutoCloseGrace > 0)
            {
                --menuAutoCloseGrace;
                return;
            }

            IntPtr foreground = User32.GetForegroundWindow();
            if (foreground == contextMenuStripSysTray.Handle)
                return;

            // 前景已不是選單，使用者已點往別處
            menuAutoCloseTimer.Stop();
            contextMenuStripSysTray.Close(ToolStripDropDownCloseReason.AppFocusChange);
        }

        protected override void SetVisibleCore(bool value)
        {
            // Never allow the form to become visible — it's a systray-only app
            base.SetVisibleCore(false);
        }

        private void RestoreAllParkedClickHandler(object sender, EventArgs e)
        {
            Program.pwp.RestoreAllParked();
        }

        private void ExitToolStripMenuItemClickHandler(object sender, EventArgs e)
        {
            bool ctrl_key_pressed = (User32.GetKeyState(0x11) & 0x8000) != 0;
            if (ctrl_key_pressed)
                Program.Restart(2, hidden:false);
            Exit();
        }

        private void IconMouseClick(object sender, MouseEventArgs e)
        {
            if (!doubleClick && e.Button == MouseButtons.Left)
            {
                firstClick = true;
                clickTime = DateTime.Now;
                Console.WriteLine("MouseClick");

                // clear memory of keyboard input
                for (Keys i = Keys.D0; i <= Keys.D9; ++i)
                {
                    User32.GetAsyncKeyState((int)i);
                }

                for (Keys i = Keys.A; i <= Keys.Z; ++i)
                {
                    User32.GetAsyncKeyState((int)i);
                }

                User32.GetAsyncKeyState((int)Keys.Oem3);
            }
        }

        private void IconMouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                DateTime now = DateTime.Now;
                var ms = now.Subtract(clickTime).TotalMilliseconds;
                Console.WriteLine("{0}", ms);
                if (ms < 30 || ms > SystemInformation.DoubleClickTime / 2)
                {
                    Program.LogError($"忽略異常的連按兩下 {ms} 毫秒");
                    return;
                }

                doubleClick = true;
                Console.WriteLine("MouseDoubleClick");
            }
        }

        private void IconMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Console.WriteLine("Down");

                if ((User32.GetKeyState(0x11) & 0x8000) != 0)
                    ctrlKeyPressed++;

                if ((User32.GetKeyState(0x10) & 0x8000) != 0)
                    shiftKeyPressed++;

                if ((User32.GetKeyState(0x12) & 0x8000) != 0)
                    altKeyPressed++;
            }
        }

        private void IconMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Console.WriteLine("Up");

                clickCount++;
                StartTimer(SystemInformation.DoubleClickTime);
            }
            else if (e.Button == MouseButtons.Middle)
            {
                notifyIconMain.Icon = Program.IdleIcon;
            }
        }
    }
}
