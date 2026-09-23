using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

using PersistentWindows.Common.Diagnostics;
using PersistentWindows.Common.Models;
using PersistentWindows.Common.WinApiBridge;

namespace PersistentWindows.Common
{
    /// <summary>
    /// 原生 Win32 記錄檢視對話框。
    ///
    /// PersistentWindows 的記錄寫在 Windows 事件記錄中（事件識別碼 9990 與 9999），
    /// 原本只能開啟事件檢視器再自行篩選。這裡直接讀出來並提供搜尋、複製與匯出，
    /// 對應說明文件中回報問題時需要附上事件記錄的流程。
    /// </summary>
    public class LogViewer
    {
        #region 控制項識別碼

        private const int IdcStatusLabel = 1301;
        private const int IdcFilterLabel = 1302;
        private const int IdcFilterEdit = 1303;
        private const int IdcLogList = 1304;
        private const int IdcButtonRefresh = 1401;
        private const int IdcButtonCopy = 1402;
        private const int IdcButtonExport = 1403;
        private const int IdcButtonEventViewer = 1404;
        private const int IdcCheckAutoRefresh = 1405;
        private const int IdcButtonClose = NativeDialog.IDCANCEL;

        /// <summary>背景載入完成時回送的自訂訊息。</summary>
        private const uint WM_LOG_LOADED = NativeDialog.WM_APP + 1;

        /// <summary>自動更新用的計時器識別碼與間隔。</summary>
        private const int AutoRefreshTimerId = 1;
        private const int AutoRefreshIntervalMs = 2000;

        #endregion

        #region 版面配置常數 (對話框單位)

        private const short DialogWidth = 560;
        private const short DialogHeight = 300;
        private const short Margin = 7;
        private const short Gap = 5;
        private const short ButtonBarGap = 7;
        private const short LabelHeight = 9;
        private const short EditHeight = 14;
        private const short ButtonHeight = 16;

        private const short ButtonRefreshWidth = 70;
        private const short ButtonCopyWidth = 92;
        private const short ButtonExportWidth = 92;
        private const short ButtonEventViewerWidth = 96;
        private const short ButtonCloseWidth = 58;
        private const short CheckAutoRefreshWidth = 76;

        private const short DialogFontSize = 9;

        #endregion

        private const string WindowTitle = "記錄檢視";

        private static readonly double[] ColumnWeights = { 0.16, 0.08, 0.76 };

        private static LogViewer activeInstance;
        private static DialogProc dialogProcKeepAlive;

        private readonly string productName;
        private readonly IntPtr iconHandle;

        private IntPtr dialogHandle;
        private NativeListView logList;
        private List<LogRecord> allRecords = new List<LogRecord>();
        private List<LogRecord> shownRecords = new List<LogRecord>();
        private string loadError;
        private volatile bool loading;
        private volatile bool fromFile;

        // 自動更新：只在記錄檔真的變動時才重載，避免無謂地清空清單
        private long lastFileLength = -1;
        private DateTime lastFileWriteTime = DateTime.MinValue;
        private int savedSelection = -1;
        private int baseUnitX = 4;
        private int baseUnitY = 8;

        private static string DialogFontFace
        {
            get { return UiFont.FamilyName; }
        }

        private LogViewer(string productName, IntPtr icon)
        {
            this.productName = productName;
            this.iconHandle = icon;
        }

        /// <summary>
        /// 以強制回應方式顯示記錄檢視對話框。
        /// </summary>
        public static void Show(IntPtr owner, string productName, IntPtr icon = default(IntPtr))
        {
            if (activeInstance != null)
            {
                if (activeInstance.dialogHandle != IntPtr.Zero)
                    User32.SetForegroundWindow(activeInstance.dialogHandle);
                return;
            }

            User32.SetThreadDpiAwarenessContextSafe();
            NativeDialog.EnsureCommonControls();

            var viewer = new LogViewer(productName, icon);
            activeInstance = viewer;

            IntPtr template = IntPtr.Zero;
            try
            {
                byte[] data = viewer.BuildTemplate();
                template = Marshal.AllocHGlobal(data.Length);
                Marshal.Copy(data, 0, template, data.Length);

                dialogProcKeepAlive = new DialogProc(StaticDialogProc);
                NativeDialog.DialogBoxIndirectParamW(NativeDialog.GetCurrentInstance(), template,
                    owner, dialogProcKeepAlive, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }
            finally
            {
                if (template != IntPtr.Zero)
                    Marshal.FreeHGlobal(template);
                activeInstance = null;
            }
        }

        #region 對話框樣板

        private byte[] BuildTemplate()
        {
            uint dialogStyle = NativeDialog.WS_POPUP | NativeDialog.WS_CAPTION | NativeDialog.WS_SYSMENU
                | NativeDialog.WS_THICKFRAME | NativeDialog.WS_MAXIMIZEBOX | NativeDialog.WS_CLIPCHILDREN
                | NativeDialog.DS_MODALFRAME | NativeDialog.DS_CENTER | NativeDialog.DS_NOIDLEMSG;

            var builder = new DialogTemplateBuilder(dialogStyle, NativeDialog.WS_EX_CONTROLPARENT,
                0, 0, DialogWidth, DialogHeight, WindowTitle, DialogFontSize, DialogFontFace);

            uint labelStyle = NativeDialog.WS_CHILD | NativeDialog.WS_VISIBLE | NativeDialog.SS_LEFTNOWORDWRAP;
            uint listStyle = NativeDialog.WS_CHILD | NativeDialog.WS_VISIBLE | NativeDialog.WS_TABSTOP
                | NativeDialog.LVS_REPORT | NativeDialog.LVS_SINGLESEL | NativeDialog.LVS_SHOWSELALWAYS
                | NativeDialog.LVS_NOSORTHEADER;
            uint buttonStyle = NativeDialog.WS_CHILD | NativeDialog.WS_VISIBLE | NativeDialog.WS_TABSTOP
                | NativeDialog.BS_PUSHBUTTON;

            short contentWidth = (short)(DialogWidth - Margin * 2);

            builder.AddControl(DialogTemplateBuilder.AtomStatic, IdcStatusLabel, labelStyle, 0,
                Margin, Margin, contentWidth, LabelHeight, "載入中 ...");

            builder.AddControl(DialogTemplateBuilder.AtomStatic, IdcFilterLabel, labelStyle, 0,
                Margin, 20, 40, LabelHeight, "搜尋：");
            builder.AddControl(DialogTemplateBuilder.AtomEdit, IdcFilterEdit,
                NativeDialog.WS_CHILD | NativeDialog.WS_VISIBLE | NativeDialog.WS_TABSTOP
                | NativeDialog.ES_AUTOHSCROLL, NativeDialog.WS_EX_CLIENTEDGE,
                (short)(Margin + 42), 18,
                (short)(contentWidth - 42 - CheckAutoRefreshWidth - Gap), EditHeight, String.Empty);

            builder.AddControl(DialogTemplateBuilder.AtomButton, IdcCheckAutoRefresh,
                NativeDialog.WS_CHILD | NativeDialog.WS_VISIBLE | NativeDialog.WS_TABSTOP
                | NativeDialog.BS_AUTOCHECKBOX, 0,
                (short)(DialogWidth - Margin - CheckAutoRefreshWidth), 19,
                CheckAutoRefreshWidth, LabelHeight + 2, "自動更新(&A)");

            builder.AddControl("SysListView32", IdcLogList, listStyle, NativeDialog.WS_EX_CLIENTEDGE,
                Margin, 38, contentWidth, 220, String.Empty);

            short buttonTop = (short)(DialogHeight - Margin - ButtonHeight);
            short x = Margin;

            builder.AddControl(DialogTemplateBuilder.AtomButton, IdcButtonRefresh,
                buttonStyle | NativeDialog.BS_DEFPUSHBUTTON, 0,
                x, buttonTop, ButtonRefreshWidth, ButtonHeight, "重新整理(&R)");
            x += ButtonRefreshWidth + Gap;

            builder.AddControl(DialogTemplateBuilder.AtomButton, IdcButtonCopy, buttonStyle, 0,
                x, buttonTop, ButtonCopyWidth, ButtonHeight, "複製到剪貼簿(&C)");
            x += ButtonCopyWidth + Gap;

            builder.AddControl(DialogTemplateBuilder.AtomButton, IdcButtonExport, buttonStyle, 0,
                x, buttonTop, ButtonExportWidth, ButtonHeight, "匯出文字檔(&E) ...");
            x += ButtonExportWidth + Gap;

            builder.AddControl(DialogTemplateBuilder.AtomButton, IdcButtonEventViewer, buttonStyle, 0,
                x, buttonTop, ButtonEventViewerWidth, ButtonHeight, "開啟事件檢視器(&V)");

            builder.AddControl(DialogTemplateBuilder.AtomButton, IdcButtonClose, buttonStyle, 0,
                (short)(DialogWidth - Margin - ButtonCloseWidth), buttonTop,
                ButtonCloseWidth, ButtonHeight, "關閉(&X)");

            return builder.Build();
        }

        #endregion

        #region 訊息處理

        private static IntPtr StaticDialogProc(IntPtr hwndDlg, uint uMsg, IntPtr wParam, IntPtr lParam)
        {
            var instance = activeInstance;
            if (instance == null)
                return IntPtr.Zero;

            try
            {
                return instance.DialogProcCore(hwndDlg, uMsg, wParam, lParam);
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                return IntPtr.Zero;
            }
        }

        private IntPtr DialogProcCore(IntPtr hwndDlg, uint uMsg, IntPtr wParam, IntPtr lParam)
        {
            switch (uMsg)
            {
                case NativeDialog.WM_INITDIALOG:
                    dialogHandle = hwndDlg;
                    OnInitDialog();
                    return new IntPtr(1);

                case NativeDialog.WM_SIZE:
                    if (dialogHandle != IntPtr.Zero)
                        LayoutControls();
                    return IntPtr.Zero;

                case NativeDialog.WM_GETMINMAXINFO:
                    OnGetMinMaxInfo(lParam);
                    return new IntPtr(1);

                case WM_LOG_LOADED:
                    OnLoadFinished();
                    return new IntPtr(1);

                case NativeDialog.WM_TIMER:
                    if (wParam.ToInt32() == AutoRefreshTimerId)
                        OnAutoRefreshTick();
                    return new IntPtr(1);

                case NativeDialog.WM_NOTIFY:
                    return OnNotify(lParam);

                case NativeDialog.WM_COMMAND:
                    return OnCommand(LowWord(wParam), HighWord(wParam));

                case NativeDialog.WM_CLOSE:
                    NativeDialog.KillTimer(hwndDlg, new IntPtr(AutoRefreshTimerId));
                    NativeDialog.EndDialog(hwndDlg, IntPtr.Zero);
                    return new IntPtr(1);
            }

            return IntPtr.Zero;
        }

        private void OnInitDialog()
        {
            NativeDialog.GetDialogBaseUnits(dialogHandle, out baseUnitX, out baseUnitY);

            if (iconHandle != IntPtr.Zero)
            {
                NativeDialog.SendMessageW(dialogHandle, NativeDialog.WM_SETICON,
                    new IntPtr(NativeDialog.ICON_SMALL), iconHandle);
                NativeDialog.SendMessageW(dialogHandle, NativeDialog.WM_SETICON,
                    new IntPtr(NativeDialog.ICON_BIG), iconHandle);
            }

            logList = new NativeListView(NativeDialog.GetDlgItem(dialogHandle, IdcLogList));
            logList.EnableModernStyle();
            logList.InsertColumn(0, "時間", 150, NativeDialog.LVCFMT_LEFT);
            logList.InsertColumn(1, "類型", 70, NativeDialog.LVCFMT_LEFT);
            logList.InsertColumn(2, "內容", 600, NativeDialog.LVCFMT_LEFT);

            NativeDialog.SendMessageW(NativeDialog.GetDlgItem(dialogHandle, IdcCheckAutoRefresh),
                NativeDialog.BM_SETCHECK, new IntPtr(NativeDialog.BST_CHECKED), IntPtr.Zero);

            LayoutControls();
            User32.SetForegroundWindow(dialogHandle);

            StartLoad();
            NativeDialog.SetTimer(dialogHandle, new IntPtr(AutoRefreshTimerId), AutoRefreshIntervalMs, IntPtr.Zero);
        }

        private void OnGetMinMaxInfo(IntPtr lParam)
        {
            if (lParam == IntPtr.Zero)
                return;

            var info = (NativeDialog.MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(NativeDialog.MINMAXINFO));
            info.ptMinTrackSize = new POINT(Dx(DialogWidth), Dy(DialogHeight));
            Marshal.StructureToPtr(info, lParam, false);
        }

        private IntPtr OnNotify(IntPtr lParam)
        {
            if (lParam == IntPtr.Zero)
                return IntPtr.Zero;

            var header = (NativeDialog.NMHDR)Marshal.PtrToStructure(lParam, typeof(NativeDialog.NMHDR));
            if (header.idFrom.ToInt64() == IdcLogList && header.code == NativeDialog.NM_DBLCLK)
                ShowSelectedRecord();

            return IntPtr.Zero;
        }

        /// <summary>
        /// 清單欄寬有限，較長的訊息會被截斷，因此提供連按兩下查看全文。
        /// 訊息方塊的內容可用 Ctrl + C 複製。
        /// </summary>
        private void ShowSelectedRecord()
        {
            if (logList == null)
                return;

            int index = logList.SelectedIndex;
            if (index < 0 || index >= shownRecords.Count)
                return;

            var record = shownRecords[index];
            string text = String.Format("時間：{0}{1}類型：{2}{1}{1}{3}",
                record.TimeText, Environment.NewLine, record.KindText, record.Message);

            NativeDialog.MessageBox(dialogHandle, text, "記錄內容",
                NativeDialog.MB_OK | NativeDialog.MB_ICONINFORMATION);
        }

        private IntPtr OnCommand(int controlId, int notifyCode)
        {
            // 搜尋框內容變動時即時篩選
            if (controlId == IdcFilterEdit && notifyCode == NativeDialog.EN_CHANGE)
            {
                if (!loading)
                    ApplyFilter();
                return IntPtr.Zero;
            }

            switch (controlId)
            {
                case IdcButtonRefresh:
                    StartLoad();
                    return new IntPtr(1);

                case IdcButtonCopy:
                    CopyToClipboard();
                    return new IntPtr(1);

                case IdcButtonExport:
                    ExportToFile();
                    return new IntPtr(1);

                case IdcButtonEventViewer:
                    OpenEventViewer();
                    return new IntPtr(1);

                case IdcCheckAutoRefresh:
                    // 重新勾選時立刻對齊最新內容
                    if (IsAutoRefreshEnabled)
                        OnAutoRefreshTick();
                    return new IntPtr(1);

                case IdcButtonClose:
                    NativeDialog.KillTimer(dialogHandle, new IntPtr(AutoRefreshTimerId));
                    NativeDialog.EndDialog(dialogHandle, IntPtr.Zero);
                    return new IntPtr(1);
            }

            return IntPtr.Zero;
        }

        #endregion

        #region 版面配置

        private int Dx(int dlu)
        {
            return dlu * baseUnitX / 4;
        }

        private int Dy(int dlu)
        {
            return dlu * baseUnitY / 8;
        }

        private void LayoutControls()
        {
            RECT client;
            if (!NativeDialog.GetClientRect(dialogHandle, out client))
                return;

            int width = client.Width;
            int height = client.Height;
            if (width <= 0 || height <= 0)
                return;

            int margin = Dx(Margin);
            int marginY = Dy(Margin);
            int gap = Dx(Gap);
            int gapY = Dy(Gap);
            int labelHeight = Dy(LabelHeight);
            int editHeight = Dy(EditHeight);
            int buttonHeight = Dy(ButtonHeight);
            int filterLabelWidth = Dx(40);

            int contentWidth = width - margin * 2;
            if (contentWidth <= 0)
                return;

            int y = marginY;
            MoveControl(IdcStatusLabel, margin, y, contentWidth, labelHeight);
            y += labelHeight + gapY;

            int autoWidth = Dx(CheckAutoRefreshWidth);
            MoveControl(IdcFilterLabel, margin, y + (editHeight - labelHeight) / 2, filterLabelWidth, labelHeight);
            MoveControl(IdcFilterEdit, margin + filterLabelWidth + Dx(2), y,
                contentWidth - filterLabelWidth - Dx(2) - autoWidth - gap, editHeight);
            MoveControl(IdcCheckAutoRefresh, margin + contentWidth - autoWidth,
                y + (editHeight - labelHeight) / 2, autoWidth, labelHeight + Dy(2));
            y += editHeight + gapY;

            int buttonTop = height - marginY - buttonHeight;
            int listHeight = buttonTop - Dy(ButtonBarGap) - y;
            if (listHeight < Dy(40))
                listHeight = Dy(40);

            MoveControl(IdcLogList, margin, y, contentWidth, listHeight);

            int x = margin;
            x = LayoutButton(IdcButtonRefresh, x, buttonTop, ButtonRefreshWidth, buttonHeight, gap);
            x = LayoutButton(IdcButtonCopy, x, buttonTop, ButtonCopyWidth, buttonHeight, gap);
            x = LayoutButton(IdcButtonExport, x, buttonTop, ButtonExportWidth, buttonHeight, gap);
            LayoutButton(IdcButtonEventViewer, x, buttonTop, ButtonEventViewerWidth, buttonHeight, gap);

            int closeWidth = Dx(ButtonCloseWidth);
            MoveControl(IdcButtonClose, width - margin - closeWidth, buttonTop, closeWidth, buttonHeight);

            DistributeColumns(contentWidth);
        }

        private int LayoutButton(int controlId, int x, int y, short widthDlu, int height, int gap)
        {
            int w = Dx(widthDlu);
            MoveControl(controlId, x, y, w, height);
            return x + w + gap;
        }

        private void MoveControl(int controlId, int x, int y, int width, int height)
        {
            IntPtr control = NativeDialog.GetDlgItem(dialogHandle, controlId);
            if (control == IntPtr.Zero)
                return;

            NativeDialog.SetWindowPos(control, IntPtr.Zero, x, y, width, height,
                NativeDialog.SWP_NOZORDER | NativeDialog.SWP_NOACTIVATE);
        }

        private void DistributeColumns(int totalWidth)
        {
            if (logList == null || logList.Handle == IntPtr.Zero)
                return;

            int usable = totalWidth - 24;
            if (usable <= 0)
                return;

            int assigned = 0;
            for (int i = 0; i < ColumnWeights.Length; ++i)
            {
                int w = i == ColumnWeights.Length - 1 ? usable - assigned : (int)(usable * ColumnWeights[i]);
                if (w < 40)
                    w = 40;
                logList.SetColumnWidth(i, w);
                assigned += w;
            }
        }

        #endregion

        #region 載入與顯示

        /// <summary>
        /// 於背景執行緒讀取事件記錄。掃描上萬筆事件可能耗時數秒，
        /// 不能在對話框執行緒同步進行，否則視窗會整個沒有回應。
        /// </summary>
        private void StartLoad()
        {
            if (loading)
                return;

            loading = true;
            SetStatus("載入中 ...");
            logList.Clear();
            EnableActionButtons(false);

            IntPtr target = dialogHandle;
            var thread = new Thread(() =>
            {
                string error;

                // 記錄檔為主要來源：讀取即時，且不需要任何特殊權限。
                // 只有在記錄檔不存在或沒有內容時，才回頭掃描 Windows 事件記錄，
                // 讓舊版留下的記錄仍然看得到。
                var records = LogReader.ReadFile(Log.LogFilePath, LogReader.DefaultMaxRecords, out error);
                fromFile = records.Count > 0;
                RememberFileState();

                if (records.Count == 0)
                {
                    string eventError;
                    records = LogReader.Read(productName,
                        LogReader.DefaultMaxRecords, LogReader.DefaultMaxScan, out eventError);
                    if (error == null)
                        error = eventError;
                }

                allRecords = records;
                loadError = error;
                loading = false;

                // 回到對話框執行緒再更新介面
                User32.PostMessageW(target, WM_LOG_LOADED, IntPtr.Zero, IntPtr.Zero);
            });
            thread.IsBackground = true;
            thread.Name = "LogViewerLoad";
            thread.Start();
        }

        /// <summary>記下目前記錄檔的大小與修改時間，作為下次比對的基準。</summary>
        private void RememberFileState()
        {
            try
            {
                string path = Log.LogFilePath;
                if (String.IsNullOrEmpty(path))
                    return;

                var info = new FileInfo(path);
                if (!info.Exists)
                    return;

                lastFileLength = info.Length;
                lastFileWriteTime = info.LastWriteTime;
            }
            catch (Exception)
            {
                // 忽略，下次輪詢會再試
            }
        }

        private void OnLoadFinished()
        {
            EnableActionButtons(true);
            ApplyFilter();
            RestoreSelection();
        }

        private bool IsAutoRefreshEnabled
        {
            get
            {
                IntPtr check = NativeDialog.GetDlgItem(dialogHandle, IdcCheckAutoRefresh);
                if (check == IntPtr.Zero)
                    return false;

                return NativeDialog.SendMessageW(check, NativeDialog.BM_GETCHECK,
                    IntPtr.Zero, IntPtr.Zero).ToInt32() == NativeDialog.BST_CHECKED;
            }
        }

        /// <summary>
        /// 定期檢查記錄檔是否變動，只有真的有新內容時才重新載入。
        /// 不論檔案是否改變都定期輪詢，成本僅是一次檔案屬性查詢。
        /// </summary>
        private void OnAutoRefreshTick()
        {
            if (loading || !IsAutoRefreshEnabled)
                return;

            string path = Log.LogFilePath;
            if (String.IsNullOrEmpty(path))
                return;

            try
            {
                var info = new FileInfo(path);
                if (!info.Exists)
                    return;

                if (info.Length == lastFileLength && info.LastWriteTime == lastFileWriteTime)
                    return;

                savedSelection = logList == null ? -1 : logList.SelectedIndex;
                StartLoad();
            }
            catch (Exception)
            {
                // 檔案正在被寫入時查詢可能失敗，下一次再試
            }
        }

        /// <summary>
        /// 重新載入後盡量保留原本選取的那一列。
        /// </summary>
        private void RestoreSelection()
        {
            if (savedSelection < 0 || logList == null)
                return;

            if (savedSelection < shownRecords.Count)
                logList.Select(savedSelection);

            savedSelection = -1;
        }

        private void ApplyFilter()
        {
            string keyword = GetFilterText();
            shownRecords = LogReader.Filter(allRecords, keyword);

            logList.Clear();
            for (int i = 0; i < shownRecords.Count; ++i)
            {
                var record = shownRecords[i];
                var columns = new List<string> { record.TimeText, record.KindText, record.Message };
                logList.InsertRow(i, columns, new IntPtr(i));
            }

            if (!String.IsNullOrEmpty(loadError))
            {
                SetStatus("無法讀取事件記錄：" + loadError);
                return;
            }

            if (allRecords.Count == 0)
            {
                string where = String.IsNullOrEmpty(Log.LogFilePath)
                    ? "尚未設定記錄檔位置" : Log.LogFilePath;
                SetStatus("找不到任何記錄。以 -silent 執行時不會寫入記錄。記錄檔位置：" + where);
                return;
            }

            string source = fromFile ? "記錄檔" : "Windows 事件記錄";
            string status = String.Format("共 {0} 筆記錄，來源：{1}（連按兩下可查看完整內容）",
                allRecords.Count, source);
            if (shownRecords.Count != allRecords.Count)
                status += String.Format("，符合搜尋條件 {0} 筆", shownRecords.Count);
            if (allRecords.Count >= LogReader.DefaultMaxRecords)
                status += String.Format("（已達 {0} 筆上限，較舊的記錄請用事件檢視器查看）", LogReader.DefaultMaxRecords);

            SetStatus(status);
        }

        private string GetFilterText()
        {
            var buffer = new StringBuilder(256);
            NativeDialog.GetDlgItemTextW(dialogHandle, IdcFilterEdit, buffer, buffer.Capacity);
            return buffer.ToString();
        }

        private void SetStatus(string text)
        {
            NativeDialog.SetDlgItemTextW(dialogHandle, IdcStatusLabel, text);
        }

        private void EnableActionButtons(bool enable)
        {
            NativeDialog.EnableWindow(NativeDialog.GetDlgItem(dialogHandle, IdcButtonRefresh), enable);
            NativeDialog.EnableWindow(NativeDialog.GetDlgItem(dialogHandle, IdcButtonCopy), enable);
            NativeDialog.EnableWindow(NativeDialog.GetDlgItem(dialogHandle, IdcButtonExport), enable);
        }

        #endregion

        #region 動作

        private void CopyToClipboard()
        {
            string text = LogReader.ToPlainText(shownRecords);
            if (String.IsNullOrEmpty(text))
            {
                ShowMessage("目前沒有可複製的記錄。");
                return;
            }

            try
            {
                System.Windows.Forms.Clipboard.SetText(text);
                SetStatus(String.Format("已複製 {0} 筆記錄到剪貼簿", shownRecords.Count));
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                ShowMessage("複製到剪貼簿失敗，剪貼簿可能正被其他程式占用。");
            }
        }

        private void ExportToFile()
        {
            string text = LogReader.ToPlainText(shownRecords);
            if (String.IsNullOrEmpty(text))
            {
                ShowMessage("目前沒有可匯出的記錄。");
                return;
            }

            try
            {
                using (var dialog = new System.Windows.Forms.SaveFileDialog())
                {
                    dialog.Title = "匯出記錄";
                    dialog.Filter = "文字檔 (*.txt)|*.txt|所有檔案 (*.*)|*.*";
                    dialog.FileName = String.Format("PersistentWindows_記錄_{0:yyyyMMdd_HHmmss}.txt", DateTime.Now);
                    dialog.RestoreDirectory = true;

                    if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                        return;

                    // 帶 BOM 的 UTF-8，記事本才會正確顯示中文
                    File.WriteAllText(dialog.FileName, text, new UTF8Encoding(true));
                    SetStatus(String.Format("已匯出 {0} 筆記錄至 {1}", shownRecords.Count, dialog.FileName));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                ShowMessage("匯出失敗：" + ex.Message);
            }
        }

        private void OpenEventViewer()
        {
            try
            {
                Shell32.ShellExecuteW(dialogHandle, "open", "eventvwr.msc", null, null,
                    NativeDialog.SW_SHOWNORMAL);
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                ShowMessage("無法開啟事件檢視器。");
            }
        }

        private void ShowMessage(string message)
        {
            NativeDialog.MessageBox(dialogHandle, message, WindowTitle,
                NativeDialog.MB_OK | NativeDialog.MB_ICONINFORMATION);
        }

        #endregion

        private static int LowWord(IntPtr value)
        {
            return (int)(value.ToInt64() & 0xFFFF);
        }

        private static int HighWord(IntPtr value)
        {
            return (int)((value.ToInt64() >> 16) & 0xFFFF);
        }
    }
}
