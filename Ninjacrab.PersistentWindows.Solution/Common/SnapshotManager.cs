using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

using PersistentWindows.Common.Diagnostics;
using PersistentWindows.Common.Models;
using PersistentWindows.Common.WinApiBridge;

namespace PersistentWindows.Common
{
    /// <summary>
    /// 原生 Win32 快照管理對話框。
    ///
    /// 以純 Win32 API (DialogBoxIndirectParamW、SysListView32、按鈕控制項) 實作，
    /// 不引入任何額外相依性。上方列表列出所有硬碟與記憶體快照，
    /// 下方列表即時解析所選快照內記錄的每個視窗細節，
    /// 讓使用者清楚知道每份快照存了什麼，並可直接刪除過期或無效的快照。
    /// </summary>
    public class SnapshotManager
    {
        #region 控制項識別碼

        private const int IdcSnapshotLabel = 1001;
        private const int IdcSnapshotList = 1002;
        private const int IdcWindowLabel = 1003;
        private const int IdcWindowList = 1004;
        private const int IdcButtonApply = 1101;
        private const int IdcButtonDelete = 1102;
        private const int IdcButtonRename = 1103;
        private const int IdcButtonFolder = 1104;
        private const int IdcButtonSave = 1105;
        private const int IdcButtonRestoreWindow = 1106;
        private const int IdcButtonRemoveWindow = 1107;
        private const int IdcButtonAddWindows = 1108;
        private const int IdcButtonClose = NativeDialog.IDCANCEL;

        private const int IdcInputLabel = 1201;
        private const int IdcInputEdit = 1202;

        #endregion

        #region 版面配置常數 (對話框單位)

        private const short DialogWidth = 540;
        private const short DialogHeight = 296;
        private const short Margin = 7;
        private const short Gap = 5;

        // 下方清單底部與按鈕列頂部之間的實際間距，採 Windows 對話框標準的 7 DLU
        private const short ButtonBarGap = 7;
        private const short LabelHeight = 9;
        private const short ButtonHeight = 16;

        private const short ButtonApplyWidth = 70;
        private const short ButtonDeleteWidth = 70;
        private const short ButtonRenameWidth = 70;
        private const short ButtonFolderWidth = 94;
        private const short ButtonSaveWidth = 132;
        private const short ButtonCloseWidth = 58;
        private const short ButtonRestoreWindowWidth = 84;
        private const short ButtonRemoveWindowWidth = 96;
        private const short ButtonAddWindowsWidth = 84;

        private static string DialogFontFace { get { return UiFont.FamilyName; } }
        private const short DialogFontSize = 9;

        #endregion

        private const string WindowTitle = "快照管理";

        // 上下兩份列表的欄位寬度比重
        private static readonly double[] SnapshotColumnWeights = { 0.27, 0.09, 0.27, 0.24, 0.13 };
        private static readonly double[] WindowColumnWeights = { 0.16, 0.32, 0.07, 0.07, 0.07, 0.07, 0.11, 0.13 };

        // 對話框為互斥視窗，以靜態欄位保存目前作用中的實例供 DLGPROC 取用
        private static SnapshotManager activeInstance;
        private static DialogProc dialogProcKeepAlive;
        private static DialogProc inputProcKeepAlive;

        private readonly PersistentWindowProcessor pwp;
        private readonly Action prepareCapture;
        private readonly IntPtr iconHandle;

        private IntPtr dialogHandle;
        private NativeListView snapshotList;
        private NativeListView windowList;
        private List<SnapshotEntry> catalog = new List<SnapshotEntry>();
        private List<SnapshotWindowInfo> shownWindows = new List<SnapshotWindowInfo>();
        private int baseUnitX = 4;
        private int baseUnitY = 8;

        private SnapshotManager(PersistentWindowProcessor processor, Action prepare, IntPtr icon)
        {
            pwp = processor;
            prepareCapture = prepare;
            iconHandle = icon;
        }

        /// <summary>
        /// 以強制回應方式顯示快照管理對話框。
        /// </summary>
        /// <param name="owner">擁有者視窗控制代碼，可為 IntPtr.Zero。</param>
        /// <param name="processor">視窗佈局處理器。</param>
        /// <param name="prepareCapture">擷取前的準備工作，例如收集行程命令列，可為 null。</param>
        /// <param name="icon">對話框圖示控制代碼，可為 IntPtr.Zero。</param>
        public static void Show(IntPtr owner, PersistentWindowProcessor processor,
            Action prepareCapture = null, IntPtr icon = default(IntPtr))
        {
            if (processor == null)
                return;

            if (activeInstance != null)
            {
                // 已有一個快照管理視窗，直接帶到前景
                if (activeInstance.dialogHandle != IntPtr.Zero)
                    User32.SetForegroundWindow(activeInstance.dialogHandle);
                return;
            }

            User32.SetThreadDpiAwarenessContextSafe();
            NativeDialog.EnsureCommonControls();

            var manager = new SnapshotManager(processor, prepareCapture, icon);
            activeInstance = manager;

            IntPtr template = IntPtr.Zero;
            try
            {
                byte[] data = manager.BuildTemplate();
                template = Marshal.AllocHGlobal(data.Length);
                Marshal.Copy(data, 0, template, data.Length);

                dialogProcKeepAlive = new DialogProc(StaticDialogProc);
                NativeDialog.DialogBoxIndirectParamW(NativeDialog.GetCurrentInstance(), template,
                    owner, dialogProcKeepAlive, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
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
                | NativeDialog.WS_THICKFRAME | NativeDialog.WS_MAXIMIZEBOX | NativeDialog.WS_MINIMIZEBOX
                | NativeDialog.WS_CLIPCHILDREN
                | NativeDialog.DS_MODALFRAME | NativeDialog.DS_CENTER | NativeDialog.DS_NOIDLEMSG;

            var builder = new DialogTemplateBuilder(dialogStyle, NativeDialog.WS_EX_CONTROLPARENT,
                0, 0, DialogWidth, DialogHeight, WindowTitle, DialogFontSize, DialogFontFace);

            uint labelStyle = NativeDialog.WS_CHILD | NativeDialog.WS_VISIBLE | NativeDialog.SS_LEFTNOWORDWRAP;
            uint listStyle = NativeDialog.WS_CHILD | NativeDialog.WS_VISIBLE
                | NativeDialog.WS_TABSTOP | NativeDialog.LVS_REPORT | NativeDialog.LVS_SINGLESEL
                | NativeDialog.LVS_SHOWSELALWAYS | NativeDialog.LVS_NOSORTHEADER;
            uint buttonStyle = NativeDialog.WS_CHILD | NativeDialog.WS_VISIBLE | NativeDialog.WS_TABSTOP
                | NativeDialog.BS_PUSHBUTTON;

            short listWidth = (short)(DialogWidth - Margin * 2);

            builder.AddControl(DialogTemplateBuilder.AtomStatic, IdcSnapshotLabel, labelStyle, 0,
                Margin, Margin, listWidth, LabelHeight, "快照檔案與組態清單：");
            builder.AddControl("SysListView32", IdcSnapshotList, listStyle, NativeDialog.WS_EX_CLIENTEDGE,
                Margin, 18, listWidth, 90, String.Empty);

            builder.AddControl(DialogTemplateBuilder.AtomStatic, IdcWindowLabel, labelStyle, 0,
                Margin, 114, listWidth, LabelHeight, "快照內部詳情：");
            builder.AddControl(DialogTemplateBuilder.AtomButton, IdcButtonAddWindows, buttonStyle, 0,
                (short)(DialogWidth - Margin - ButtonRestoreWindowWidth - Gap - ButtonRemoveWindowWidth
                    - Gap - ButtonAddWindowsWidth), 111,
                ButtonAddWindowsWidth, ButtonHeight, "加入視窗(&I) ...");
            builder.AddControl(DialogTemplateBuilder.AtomButton, IdcButtonRestoreWindow, buttonStyle, 0,
                (short)(DialogWidth - Margin - ButtonRestoreWindowWidth - Gap - ButtonRemoveWindowWidth), 111,
                ButtonRestoreWindowWidth, ButtonHeight, "還原此視窗(&W)");
            builder.AddControl(DialogTemplateBuilder.AtomButton, IdcButtonRemoveWindow, buttonStyle, 0,
                (short)(DialogWidth - Margin - ButtonRemoveWindowWidth), 111,
                ButtonRemoveWindowWidth, ButtonHeight, "從快照移除(&X)");

            builder.AddControl("SysListView32", IdcWindowList, listStyle, NativeDialog.WS_EX_CLIENTEDGE,
                Margin, 131, listWidth, 123, String.Empty);

            short buttonTop = (short)(DialogHeight - Margin - ButtonHeight);
            short x = Margin;

            builder.AddControl(DialogTemplateBuilder.AtomButton, IdcButtonApply,
                buttonStyle | NativeDialog.BS_DEFPUSHBUTTON, 0,
                x, buttonTop, ButtonApplyWidth, ButtonHeight, "套用還原(&A)");
            x += ButtonApplyWidth + Gap;

            builder.AddControl(DialogTemplateBuilder.AtomButton, IdcButtonDelete, buttonStyle, 0,
                x, buttonTop, ButtonDeleteWidth, ButtonHeight, "刪除快照(&D)");
            x += ButtonDeleteWidth + Gap;

            builder.AddControl(DialogTemplateBuilder.AtomButton, IdcButtonRename, buttonStyle, 0,
                x, buttonTop, ButtonRenameWidth, ButtonHeight, "重新命名(&R)");
            x += ButtonRenameWidth + Gap;

            builder.AddControl(DialogTemplateBuilder.AtomButton, IdcButtonFolder, buttonStyle, 0,
                x, buttonTop, ButtonFolderWidth, ButtonHeight, "開啟快照資料夾(&F)");
            x += ButtonFolderWidth + Gap;

            builder.AddControl(DialogTemplateBuilder.AtomButton, IdcButtonSave, buttonStyle, 0,
                x, buttonTop, ButtonSaveWidth, ButtonHeight, "儲存目前佈局為新快照(&S) ...");

            builder.AddControl(DialogTemplateBuilder.AtomButton, IdcButtonClose, buttonStyle, 0,
                (short)(DialogWidth - Margin - ButtonCloseWidth), buttonTop,
                ButtonCloseWidth, ButtonHeight, "關閉(&C)");

            return builder.Build();
        }

        #endregion

        #region 對話框訊息處理

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
                Log.Error(ex);
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
                    // 必須回傳 TRUE，否則 DefDlgProc 會覆寫剛設定的最小尺寸
                    OnGetMinMaxInfo(lParam);
                    return new IntPtr(1);

                case NativeDialog.WM_NOTIFY:
                    return OnNotify(lParam);

                case NativeDialog.WM_COMMAND:
                    return OnCommand(LowWord(wParam));

                case NativeDialog.WM_CLOSE:
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

            snapshotList = new NativeListView(NativeDialog.GetDlgItem(dialogHandle, IdcSnapshotList));
            windowList = new NativeListView(NativeDialog.GetDlgItem(dialogHandle, IdcWindowList));

            snapshotList.EnableModernStyle();
            windowList.EnableModernStyle();

            snapshotList.InsertColumn(0, "快照名稱", 160, NativeDialog.LVCFMT_LEFT);
            snapshotList.InsertColumn(1, "來源", 80, NativeDialog.LVCFMT_LEFT);
            snapshotList.InsertColumn(2, "顯示器配置", 200, NativeDialog.LVCFMT_LEFT);
            snapshotList.InsertColumn(3, "儲存時間", 150, NativeDialog.LVCFMT_LEFT);
            snapshotList.InsertColumn(4, "視窗總數", 80, NativeDialog.LVCFMT_RIGHT);

            windowList.InsertColumn(0, "行程名稱", 130, NativeDialog.LVCFMT_LEFT);
            windowList.InsertColumn(1, "視窗標題", 280, NativeDialog.LVCFMT_LEFT);
            windowList.InsertColumn(2, "X", 60, NativeDialog.LVCFMT_RIGHT);
            windowList.InsertColumn(3, "Y", 60, NativeDialog.LVCFMT_RIGHT);
            windowList.InsertColumn(4, "寬度", 60, NativeDialog.LVCFMT_RIGHT);
            windowList.InsertColumn(5, "高度", 60, NativeDialog.LVCFMT_RIGHT);
            windowList.InsertColumn(6, "顯示器", 80, NativeDialog.LVCFMT_LEFT);
            windowList.InsertColumn(7, "狀態", 90, NativeDialog.LVCFMT_LEFT);

            NativeDialog.SetDlgItemTextW(dialogHandle, IdcSnapshotLabel,
                "快照檔案與組態清單：　目前顯示器 " + pwp.GetCurrentDisplayLayoutText());

            ReloadCatalog(String.Empty);
            LayoutControls();

            User32.SetForegroundWindow(dialogHandle);
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
            if (header.idFrom.ToInt64() == IdcWindowList)
            {
                if (header.code == NativeDialog.LVN_ITEMCHANGED)
                    UpdateWindowButtonState();
                else if (header.code == NativeDialog.NM_DBLCLK)
                    RestoreSelectedWindow();
                return IntPtr.Zero;
            }

            if (header.idFrom.ToInt64() != IdcSnapshotList)
                return IntPtr.Zero;

            if (header.code == NativeDialog.LVN_ITEMCHANGED)
            {
                var change = (NativeDialog.NMLISTVIEW)Marshal.PtrToStructure(lParam, typeof(NativeDialog.NMLISTVIEW));

                // 僅在選取狀態真正改變時重新解析詳情，避免焦點變動造成不必要的重載
                bool selected = (change.uNewState & NativeDialog.LVIS_SELECTED) != 0;
                bool wasSelected = (change.uOldState & NativeDialog.LVIS_SELECTED) != 0;
                if (selected != wasSelected)
                {
                    RefreshWindowDetails();
                    UpdateButtonState();
                }
            }
            else if (header.code == NativeDialog.NM_DBLCLK)
            {
                ApplySelectedSnapshot();
            }

            return IntPtr.Zero;
        }

        private IntPtr OnCommand(int controlId)
        {
            switch (controlId)
            {
                case IdcButtonApply:
                    ApplySelectedSnapshot();
                    return new IntPtr(1);

                case IdcButtonDelete:
                    DeleteSelectedSnapshot();
                    return new IntPtr(1);

                case IdcButtonRename:
                    RenameSelectedSnapshot();
                    return new IntPtr(1);

                case IdcButtonFolder:
                    OpenSnapshotFolder();
                    return new IntPtr(1);

                case IdcButtonSave:
                    SaveCurrentLayout();
                    return new IntPtr(1);

                case IdcButtonRestoreWindow:
                    RestoreSelectedWindow();
                    return new IntPtr(1);

                case IdcButtonAddWindows:
                    AddWindows();
                    return new IntPtr(1);

                case IdcButtonRemoveWindow:
                    RemoveSelectedWindow();
                    return new IntPtr(1);

                case IdcButtonClose:
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
            int buttonHeight = Dy(ButtonHeight);

            int contentWidth = width - margin * 2;
            if (contentWidth <= 0)
                return;

            int buttonBarGap = Dy(ButtonBarGap);
            int buttonTop = height - marginY - buttonHeight;
            int listsBottom = buttonTop - buttonBarGap;

            // 兩組「標籤 + 間距」，外加最後一個清單與按鈕列之間不屬於清單的那段間距，
            // 扣掉之後 ButtonBarGap 才等於實際看到的間距
            int listsTotal = listsBottom - marginY - (labelHeight + gapY) * 2 - gapY;
            if (listsTotal < Dy(40))
                listsTotal = Dy(40);

            int snapshotHeight = listsTotal * 2 / 5;
            int windowHeight = listsTotal - snapshotHeight;

            int y = marginY;
            MoveControl(IdcSnapshotLabel, margin, y, contentWidth, labelHeight);
            y += labelHeight + gapY;
            MoveControl(IdcSnapshotList, margin, y, contentWidth, snapshotHeight);
            y += snapshotHeight + gapY;
            int addWinWidth = Dx(ButtonAddWindowsWidth);
            int restoreWinWidth = Dx(ButtonRestoreWindowWidth);
            int removeWinWidth = Dx(ButtonRemoveWindowWidth);
            int winButtonsWidth = addWinWidth + gap + restoreWinWidth + gap + removeWinWidth;
            int labelRowHeight = Math.Max(labelHeight, buttonHeight);

            int labelWidth = contentWidth - winButtonsWidth - gap;
            if (labelWidth < Dx(40))
                labelWidth = Dx(40);

            MoveControl(IdcWindowLabel, margin, y + (labelRowHeight - labelHeight) / 2, labelWidth, labelHeight);
            MoveControl(IdcButtonAddWindows, margin + contentWidth - winButtonsWidth, y,
                addWinWidth, buttonHeight);
            MoveControl(IdcButtonRestoreWindow, margin + contentWidth - restoreWinWidth - gap - removeWinWidth, y,
                restoreWinWidth, buttonHeight);
            MoveControl(IdcButtonRemoveWindow, margin + contentWidth - removeWinWidth, y,
                removeWinWidth, buttonHeight);

            y += labelRowHeight + gapY;

            int listHeight = windowHeight - (labelRowHeight - labelHeight);
            if (listHeight < Dy(30))
                listHeight = Dy(30);
            MoveControl(IdcWindowList, margin, y, contentWidth, listHeight);

            int x = margin;
            x = LayoutButton(IdcButtonApply, x, buttonTop, ButtonApplyWidth, buttonHeight, gap);
            x = LayoutButton(IdcButtonDelete, x, buttonTop, ButtonDeleteWidth, buttonHeight, gap);
            x = LayoutButton(IdcButtonRename, x, buttonTop, ButtonRenameWidth, buttonHeight, gap);
            x = LayoutButton(IdcButtonFolder, x, buttonTop, ButtonFolderWidth, buttonHeight, gap);
            LayoutButton(IdcButtonSave, x, buttonTop, ButtonSaveWidth, buttonHeight, gap);

            int closeWidth = Dx(ButtonCloseWidth);
            MoveControl(IdcButtonClose, width - margin - closeWidth, buttonTop, closeWidth, buttonHeight);

            DistributeColumns(snapshotList, SnapshotColumnWeights, contentWidth);
            DistributeColumns(windowList, WindowColumnWeights, contentWidth);
        }

        private int LayoutButton(int controlId, int x, int y, short widthDlu, int height, int gap)
        {
            int width = Dx(widthDlu);
            MoveControl(controlId, x, y, width, height);
            return x + width + gap;
        }

        private void MoveControl(int controlId, int x, int y, int width, int height)
        {
            IntPtr control = NativeDialog.GetDlgItem(dialogHandle, controlId);
            if (control == IntPtr.Zero)
                return;

            NativeDialog.SetWindowPos(control, IntPtr.Zero, x, y, width, height,
                NativeDialog.SWP_NOZORDER | NativeDialog.SWP_NOACTIVATE);
        }

        private static void DistributeColumns(NativeListView list, double[] weights, int totalWidth)
        {
            if (list == null || list.Handle == IntPtr.Zero)
                return;

            // 保留垂直捲軸與框線的寬度，避免最後一欄被截斷
            int usable = totalWidth - 24;
            if (usable <= 0)
                return;

            int assigned = 0;
            for (int i = 0; i < weights.Length; ++i)
            {
                int columnWidth = i == weights.Length - 1
                    ? usable - assigned
                    : (int)(usable * weights[i]);

                if (columnWidth < 24)
                    columnWidth = 24;

                list.SetColumnWidth(i, columnWidth);
                assigned += columnWidth;
            }
        }

        #endregion

        #region 清單內容

        /// <summary>
        /// 重新載入後選回同一份快照。硬碟快照以資料庫鍵值比對，
        /// 記憶體快照以顯示設定加編號比對；只傳鍵值時記憶體快照會跳回第一列。
        /// </summary>
        private void ReloadCatalog(SnapshotEntry entryToSelect)
        {
            if (entryToSelect == null || entryToSelect.Source == SnapshotSource.Disk)
            {
                ReloadCatalog(entryToSelect == null ? String.Empty : entryToSelect.DbKey);
                return;
            }

            ReloadCatalog(String.Empty);

            for (int i = 0; i < catalog.Count; ++i)
            {
                if (catalog[i].Source == SnapshotSource.Memory
                    && catalog[i].SnapshotId == entryToSelect.SnapshotId
                    && catalog[i].DisplayKey == entryToSelect.DisplayKey)
                {
                    snapshotList.Select(i);
                    RefreshWindowDetails();
                    UpdateButtonState();
                    break;
                }
            }
        }

        private void ReloadCatalog(string dbKeyToSelect)
        {
            catalog = pwp.GetSnapshotCatalog();

            snapshotList.Clear();
            for (int i = 0; i < catalog.Count; ++i)
            {
                var entry = catalog[i];
                var columns = new List<string>
                {
                    entry.Name,
                    entry.SourceText,
                    entry.DisplayLayoutText,
                    entry.SaveTimeText,
                    entry.WindowCount.ToString(),
                };
                snapshotList.InsertRow(i, columns, new IntPtr(i));
            }

            int selection = 0;
            if (!String.IsNullOrEmpty(dbKeyToSelect))
            {
                for (int i = 0; i < catalog.Count; ++i)
                {
                    if (catalog[i].DbKey == dbKeyToSelect)
                    {
                        selection = i;
                        break;
                    }
                }
            }

            if (catalog.Count > 0)
                snapshotList.Select(selection);

            RefreshWindowDetails();
            UpdateButtonState();
        }

        private SnapshotEntry SelectedEntry
        {
            get
            {
                if (snapshotList == null)
                    return null;

                int index = snapshotList.SelectedIndex;
                if (index < 0 || index >= catalog.Count)
                    return null;

                return catalog[index];
            }
        }

        private void RefreshWindowDetails()
        {
            if (windowList == null)
                return;

            windowList.Clear();
            shownWindows = new List<SnapshotWindowInfo>();

            var entry = SelectedEntry;
            if (entry == null)
            {
                NativeDialog.SetDlgItemTextW(dialogHandle, IdcWindowLabel, "快照內部詳情：");
                UpdateWindowButtonState();
                return;
            }

            var windows = pwp.GetSnapshotWindows(entry);
            shownWindows = windows;
            for (int i = 0; i < windows.Count; ++i)
            {
                var info = windows[i];
                var columns = new List<string>
                {
                    info.ProcessName,
                    info.Title,
                    info.X.ToString(),
                    info.Y.ToString(),
                    info.Width.ToString(),
                    info.Height.ToString(),
                    info.MonitorText,
                    info.State,
                };
                windowList.InsertRow(i, columns, new IntPtr(i));
            }

            // 清單欄位只放精簡描述，完整的排列順序與桌面座標放在這裡
            NativeDialog.SetDlgItemTextW(dialogHandle, IdcWindowLabel,
                String.Format("快照內部詳情：{0} — 共 {1} 個視窗　│　{2}",
                    entry.Name, windows.Count, entry.DisplayLayout));
        }

        private void UpdateButtonState()
        {
            var entry = SelectedEntry;
            bool hasSelection = entry != null;
            bool isDiskSnapshot = hasSelection && entry.Source == SnapshotSource.Disk;

            NativeDialog.EnableWindow(NativeDialog.GetDlgItem(dialogHandle, IdcButtonApply), hasSelection);
            NativeDialog.EnableWindow(NativeDialog.GetDlgItem(dialogHandle, IdcButtonDelete), hasSelection);
            NativeDialog.EnableWindow(NativeDialog.GetDlgItem(dialogHandle, IdcButtonRename), isDiskSnapshot);
            NativeDialog.EnableWindow(NativeDialog.GetDlgItem(dialogHandle, IdcButtonAddWindows), hasSelection);

            UpdateWindowButtonState();
        }

        /// <summary>
        /// 依下方清單是否有選取列，決定單一視窗操作按鈕是否可用。
        /// </summary>
        private void UpdateWindowButtonState()
        {
            bool hasWindow = windowList != null
                && windowList.SelectedIndex >= 0
                && windowList.SelectedIndex < shownWindows.Count;

            NativeDialog.EnableWindow(NativeDialog.GetDlgItem(dialogHandle, IdcButtonRestoreWindow), hasWindow);
            NativeDialog.EnableWindow(NativeDialog.GetDlgItem(dialogHandle, IdcButtonRemoveWindow), hasWindow);
        }

        #endregion

        #region 管理動作

        private void ApplySelectedSnapshot()
        {
            var entry = SelectedEntry;
            if (entry == null)
                return;

            try
            {
                if (entry.Source == SnapshotSource.Disk)
                {
                    pwp.dbDisplayKey = entry.DbKey;
                    pwp.restoringFromDB = true;
                    // 等待滑鼠靜止，工作列還原才會穩定
                    pwp.StartRestoreTimer(milliSecond: 1000);
                }
                else
                {
                    pwp.RestoreSnapshot(entry.SnapshotId);
                }

                NativeDialog.EndDialog(dialogHandle, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                ShowError("還原快照時發生錯誤，詳情請見系統匣選單的「檢視記錄(&L) ...」。");
            }
        }

        private void DeleteSelectedSnapshot()
        {
            var entry = SelectedEntry;
            if (entry == null)
                return;

            string message = String.Format(
                "確定要刪除快照「{0}」嗎？\n\n來源：{1}\n顯示器配置：{2}\n儲存時間：{3}\n視窗總數：{4}\n\n此動作無法復原。",
                entry.Name, entry.SourceText, entry.DisplayLayout, entry.SaveTimeText, entry.WindowCount);

            int answer = NativeDialog.MessageBox(dialogHandle, message, "刪除快照",
                NativeDialog.MB_YESNO | NativeDialog.MB_ICONWARNING | NativeDialog.MB_DEFBUTTON2);
            if (answer != NativeDialog.IDYES)
                return;

            bool deleted = entry.Source == SnapshotSource.Disk
                ? pwp.DeleteDiskSnapshot(entry.DbKey)
                : pwp.DeleteMemorySnapshot(entry.DisplayKey, entry.SnapshotId);

            if (!deleted)
            {
                ShowError("刪除快照失敗，該快照可能已經不存在。");
            }

            ReloadCatalog(String.Empty);
        }

        private void RenameSelectedSnapshot()
        {
            var entry = SelectedEntry;
            if (entry == null || entry.Source != SnapshotSource.Disk)
                return;

            string displayKey;
            string currentName;
            DisplayKeyParser.Split(entry.DbKey, out displayKey, out currentName);

            string newName;
            if (!PromptForText(dialogHandle, "重新命名快照", "請輸入新的快照名稱：", currentName, out newName))
                return;

            newName = PersistentWindowProcessor.SanitizeSnapshotName(newName);
            if (String.IsNullOrEmpty(newName))
            {
                ShowError("快照名稱不可為空白。");
                return;
            }

            string newDbKey = pwp.RenameDiskSnapshot(entry.DbKey, newName);
            if (String.IsNullOrEmpty(newDbKey))
            {
                ShowError("重新命名失敗，可能已有同名的快照存在。");
                return;
            }

            ReloadCatalog(newDbKey);
        }

        private void OpenSnapshotFolder()
        {
            string folder = pwp.SnapshotFolder;
            try
            {
                if (String.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                {
                    ShowError("找不到快照儲存目錄。");
                    return;
                }

                // 以寬字元 API 開啟，含中文字的路徑才不會被 ANSI 轉碼破壞
                IntPtr result = Shell32.ShellExecuteW(dialogHandle, "open", folder, null, null,
                    NativeDialog.SW_SHOWNORMAL);

                // ShellExecuteW 失敗時不會擲出例外，而是回傳小於等於 32 的錯誤碼
                long code = result.ToInt64();
                if (code <= 32)
                {
                    Log.Error("開啟快照儲存目錄失敗，ShellExecuteW 回傳 {0}：{1}", code, folder);
                    ShowError(String.Format("無法開啟快照儲存目錄（錯誤碼 {0}）。", code));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                ShowError("無法開啟快照儲存目錄。");
            }
        }

        private void SaveCurrentLayout()
        {
            string name;
            if (!PromptForText(dialogHandle, "儲存目前佈局", "請輸入新快照的名稱：", String.Empty, out name))
                return;

            name = PersistentWindowProcessor.SanitizeSnapshotName(name);
            if (String.IsNullOrEmpty(name))
            {
                ShowError("快照名稱不可為空白。");
                return;
            }

            // 收集行程資訊需要啟動 powershell，可能耗時數秒，先給使用者明確回饋
            BeginWait("儲存中 ...");
            try
            {
                if (prepareCapture != null)
                    prepareCapture();

                if (!pwp.SaveCurrentLayoutAsSnapshot(name))
                {
                    ShowError("儲存快照失敗，詳情請見系統匣選單的「檢視記錄(&L) ...」。");
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                ShowError("儲存快照失敗，詳情請見系統匣選單的「檢視記錄(&L) ...」。");
                return;
            }
            finally
            {
                EndWait();
            }

            ReloadCatalog(pwp.dbDisplayKey);
        }

        /// <summary>
        /// 進入等待狀態：停用互動、切換為等待游標，並立即重繪提示文字。
        /// </summary>
        private void BeginWait(string message)
        {
            NativeDialog.EnableWindow(dialogHandle, false);
            NativeDialog.SetCursor(NativeDialog.LoadCursorW(IntPtr.Zero, NativeDialog.IDC_WAIT));
            NativeDialog.SetDlgItemTextW(dialogHandle, IdcWindowLabel, message);
            NativeDialog.UpdateWindow(dialogHandle);
        }

        private void EndWait()
        {
            NativeDialog.SetCursor(NativeDialog.LoadCursorW(IntPtr.Zero, NativeDialog.IDC_ARROW));
            NativeDialog.EnableWindow(dialogHandle, true);
            User32.SetForegroundWindow(dialogHandle);
        }

        private SnapshotWindowInfo SelectedWindow
        {
            get
            {
                if (windowList == null)
                    return null;

                int index = windowList.SelectedIndex;
                if (index < 0 || index >= shownWindows.Count)
                    return null;

                return shownWindows[index];
            }
        }

        private void RestoreSelectedWindow()
        {
            var info = SelectedWindow;
            if (info == null)
                return;

            string error;
            if (!pwp.RestoreSingleWindow(info, out error))
            {
                ShowError(error);
                return;
            }

            NativeDialog.SetDlgItemTextW(dialogHandle, IdcWindowLabel,
                String.Format("已還原視窗：{0}「{1}」", info.ProcessName, info.Title));
        }

        private void RemoveSelectedWindow()
        {
            var entry = SelectedEntry;
            var info = SelectedWindow;
            if (entry == null || info == null)
                return;

            string message = String.Format(
                "確定要把這個視窗從快照「{0}」中移除嗎？{3}{3}行程：{1}{3}標題：{2}{3}{3}"
                + "只會移除快照中的這筆記錄，不會影響實際執行中的視窗。此動作無法復原。",
                entry.Name, info.ProcessName, info.Title, Environment.NewLine);

            int answer = NativeDialog.MessageBox(dialogHandle, message, "從快照移除視窗",
                NativeDialog.MB_YESNO | NativeDialog.MB_ICONWARNING | NativeDialog.MB_DEFBUTTON2);
            if (answer != NativeDialog.IDYES)
                return;

            string error;
            if (!pwp.RemoveWindowFromSnapshot(entry, info, out error))
            {
                ShowError(error);
                return;
            }

            ReloadCatalog(entry);
        }

        /// <summary>
        /// 從執行中的視窗勾選，以目前的位置加入選取的快照。
        /// </summary>
        private void AddWindows()
        {
            var entry = SelectedEntry;
            if (entry == null)
                return;

            string error;
            var live = pwp.GetLiveWindows(entry, out error);
            if (error != null)
            {
                ShowError(error);
                return;
            }

            if (live.Count == 0)
            {
                ShowError("目前沒有可加入的視窗。");
                return;
            }

            var handles = WindowPicker.Show(dialogHandle, entry, live);
            if (handles == null || handles.Count == 0)
                return;

            int added, updated;
            if (!pwp.AddWindowsToSnapshot(entry, handles, out added, out updated, out error))
            {
                ShowError(error);
                return;
            }

            ReloadCatalog(entry);
        }

        private void ShowError(string message)
        {
            NativeDialog.MessageBox(dialogHandle, message, WindowTitle,
                NativeDialog.MB_OK | NativeDialog.MB_ICONWARNING);
        }

        #endregion

        #region 簡易輸入對話框

        private const short InputDialogWidth = 260;
        private const short InputDialogHeight = 76;

        private static string inputPrompt;
        private static string inputInitialValue;
        private static string inputResult;
        private static string inputTitle;

        /// <summary>
        /// 以原生對話框要求使用者輸入一段文字。
        /// </summary>
        private static bool PromptForText(IntPtr owner, string title, string prompt,
            string initialValue, out string result)
        {
            inputTitle = title;
            inputPrompt = prompt;
            inputInitialValue = initialValue ?? String.Empty;
            inputResult = null;
            result = String.Empty;

            uint dialogStyle = NativeDialog.WS_POPUP | NativeDialog.WS_CAPTION | NativeDialog.WS_SYSMENU
                | NativeDialog.DS_MODALFRAME | NativeDialog.DS_CENTER;

            var builder = new DialogTemplateBuilder(dialogStyle, 0, 0, 0,
                InputDialogWidth, InputDialogHeight, title, DialogFontSize, DialogFontFace);

            builder.AddControl(DialogTemplateBuilder.AtomStatic, IdcInputLabel,
                NativeDialog.WS_CHILD | NativeDialog.WS_VISIBLE | NativeDialog.SS_LEFTNOWORDWRAP, 0,
                7, 8, 246, 9, prompt);

            builder.AddControl(DialogTemplateBuilder.AtomEdit, IdcInputEdit,
                NativeDialog.WS_CHILD | NativeDialog.WS_VISIBLE | NativeDialog.WS_TABSTOP
                | NativeDialog.ES_AUTOHSCROLL, NativeDialog.WS_EX_CLIENTEDGE,
                7, 22, 246, 14, String.Empty);

            builder.AddControl(DialogTemplateBuilder.AtomButton, NativeDialog.IDOK,
                NativeDialog.WS_CHILD | NativeDialog.WS_VISIBLE | NativeDialog.WS_TABSTOP
                | NativeDialog.BS_DEFPUSHBUTTON, 0,
                139, 52, 54, 16, "確定(&O)");

            builder.AddControl(DialogTemplateBuilder.AtomButton, NativeDialog.IDCANCEL,
                NativeDialog.WS_CHILD | NativeDialog.WS_VISIBLE | NativeDialog.WS_TABSTOP
                | NativeDialog.BS_PUSHBUTTON, 0,
                199, 52, 54, 16, "取消(&C)");

            IntPtr template = IntPtr.Zero;
            try
            {
                byte[] data = builder.Build();
                template = Marshal.AllocHGlobal(data.Length);
                Marshal.Copy(data, 0, template, data.Length);

                inputProcKeepAlive = new DialogProc(InputDialogProc);
                IntPtr answer = NativeDialog.DialogBoxIndirectParamW(NativeDialog.GetCurrentInstance(),
                    template, owner, inputProcKeepAlive, IntPtr.Zero);

                if (answer.ToInt64() != NativeDialog.IDOK || inputResult == null)
                    return false;

                result = inputResult;
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return false;
            }
            finally
            {
                if (template != IntPtr.Zero)
                    Marshal.FreeHGlobal(template);
            }
        }

        private static IntPtr InputDialogProc(IntPtr hwndDlg, uint uMsg, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                switch (uMsg)
                {
                    case NativeDialog.WM_INITDIALOG:
                        NativeDialog.SetWindowTextW(hwndDlg, inputTitle);
                        NativeDialog.SetDlgItemTextW(hwndDlg, IdcInputLabel, inputPrompt);
                        NativeDialog.SetDlgItemTextW(hwndDlg, IdcInputEdit, inputInitialValue);
                        NativeDialog.SetFocus(NativeDialog.GetDlgItem(hwndDlg, IdcInputEdit));
                        return IntPtr.Zero;

                    case NativeDialog.WM_COMMAND:
                        {
                            int controlId = LowWord(wParam);
                            if (controlId == NativeDialog.IDOK)
                            {
                                var buffer = new StringBuilder(512);
                                NativeDialog.GetDlgItemTextW(hwndDlg, IdcInputEdit, buffer, buffer.Capacity);
                                inputResult = buffer.ToString();
                                NativeDialog.EndDialog(hwndDlg, new IntPtr(NativeDialog.IDOK));
                                return new IntPtr(1);
                            }

                            if (controlId == NativeDialog.IDCANCEL)
                            {
                                inputResult = null;
                                NativeDialog.EndDialog(hwndDlg, new IntPtr(NativeDialog.IDCANCEL));
                                return new IntPtr(1);
                            }
                        }
                        return IntPtr.Zero;

                    case NativeDialog.WM_CLOSE:
                        inputResult = null;
                        NativeDialog.EndDialog(hwndDlg, new IntPtr(NativeDialog.IDCANCEL));
                        return new IntPtr(1);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }

            return IntPtr.Zero;
        }

        #endregion

        private static int LowWord(IntPtr value)
        {
            return (int)(value.ToInt64() & 0xFFFF);
        }
    }
}
