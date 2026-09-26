using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using PersistentWindows.Common.Diagnostics;
using PersistentWindows.Common.Models;
using PersistentWindows.Common.WinApiBridge;

namespace PersistentWindows.Common
{
    /// <summary>
    /// 從執行中的視窗勾選要加入快照的視窗。原生 Win32 對話框，由快照管理開啟。
    /// </summary>
    public class WindowPicker
    {
        #region 控制項識別碼

        private const int IdcHintLabel = 1501;
        private const int IdcWindowList = 1502;
        private const int IdcStatusLabel = 1503;
        private const int IdcButtonSameProcess = 1601;
        private const int IdcButtonUncheckAll = 1602;
        private const int IdcButtonAdd = NativeDialog.IDOK;
        private const int IdcButtonCancel = NativeDialog.IDCANCEL;

        #endregion

        #region 版面配置常數 (對話框單位)

        private const short DialogWidth = 500;
        private const short DialogHeight = 260;
        private const short Margin = 7;
        private const short Gap = 5;
        private const short ButtonBarGap = 7;
        private const short LabelHeight = 9;
        private const short ButtonHeight = 16;

        private const short ButtonSameProcessWidth = 84;
        private const short ButtonUncheckAllWidth = 84;
        private const short ButtonAddWidth = 58;
        private const short ButtonCancelWidth = 58;

        private const short DialogFontSize = 9;

        #endregion

        private const string WindowTitle = "加入視窗到快照";

        private static readonly double[] ColumnWeights = { 0.15, 0.30, 0.08, 0.06, 0.06, 0.06, 0.06, 0.08, 0.15 };

        private static WindowPicker activeInstance;
        private static DialogProc dialogProcKeepAlive;

        private readonly SnapshotEntry target;
        private readonly List<SnapshotWindowInfo> windows;
        private List<IntPtr> result;

        private IntPtr dialogHandle;
        private NativeListView windowList;
        private int baseUnitX = 4;
        private int baseUnitY = 8;

        private static string DialogFontFace
        {
            get { return UiFont.FamilyName; }
        }

        private WindowPicker(SnapshotEntry target, List<SnapshotWindowInfo> windows)
        {
            this.target = target;
            this.windows = windows ?? new List<SnapshotWindowInfo>();
        }

        /// <summary>
        /// 顯示挑選對話框。按「加入」時回傳勾選的視窗代碼，取消時回傳 null。
        /// </summary>
        public static List<IntPtr> Show(IntPtr owner, SnapshotEntry target, List<SnapshotWindowInfo> windows)
        {
            if (activeInstance != null)
                return null;

            NativeDialog.EnsureCommonControls();

            var picker = new WindowPicker(target, windows);
            activeInstance = picker;

            IntPtr template = IntPtr.Zero;
            try
            {
                byte[] data = picker.BuildTemplate();
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

            return picker.result;
        }

        #region 對話框樣板

        private byte[] BuildTemplate()
        {
            // 由快照管理擁有，沒有工作列按鈕，因此不提供最小化；
            // 只給最大化會讓 Windows 畫出一顆反灰的最小化鈕，所以兩者都不加，只保留可調整大小
            uint dialogStyle = NativeDialog.WS_POPUP | NativeDialog.WS_CAPTION | NativeDialog.WS_SYSMENU
                | NativeDialog.WS_THICKFRAME | NativeDialog.WS_CLIPCHILDREN
                | NativeDialog.DS_MODALFRAME | NativeDialog.DS_CENTER;

            var builder = new DialogTemplateBuilder(dialogStyle, NativeDialog.WS_EX_CONTROLPARENT,
                0, 0, DialogWidth, DialogHeight, WindowTitle, DialogFontSize, DialogFontFace);

            uint labelStyle = NativeDialog.WS_CHILD | NativeDialog.WS_VISIBLE | NativeDialog.SS_LEFTNOWORDWRAP;
            uint listStyle = NativeDialog.WS_CHILD | NativeDialog.WS_VISIBLE | NativeDialog.WS_TABSTOP
                | NativeDialog.LVS_REPORT | NativeDialog.LVS_SINGLESEL | NativeDialog.LVS_SHOWSELALWAYS
                | NativeDialog.LVS_NOSORTHEADER;
            uint buttonStyle = NativeDialog.WS_CHILD | NativeDialog.WS_VISIBLE | NativeDialog.WS_TABSTOP
                | NativeDialog.BS_PUSHBUTTON;

            short contentWidth = (short)(DialogWidth - Margin * 2);

            builder.AddControl(DialogTemplateBuilder.AtomStatic, IdcHintLabel, labelStyle, 0,
                Margin, Margin, contentWidth, LabelHeight,
                String.Format("勾選要加入快照「{0}」的視窗，會以目前的位置加入：", target == null ? "" : target.Name));

            builder.AddControl("SysListView32", IdcWindowList, listStyle, NativeDialog.WS_EX_CLIENTEDGE,
                Margin, 20, contentWidth, 190, String.Empty);

            builder.AddControl(DialogTemplateBuilder.AtomStatic, IdcStatusLabel, labelStyle, 0,
                Margin, 214, contentWidth, LabelHeight, String.Empty);

            short buttonTop = (short)(DialogHeight - Margin - ButtonHeight);
            short x = Margin;

            builder.AddControl(DialogTemplateBuilder.AtomButton, IdcButtonSameProcess, buttonStyle, 0,
                x, buttonTop, ButtonSameProcessWidth, ButtonHeight, "勾選同行程(&P)");
            x += ButtonSameProcessWidth + Gap;

            builder.AddControl(DialogTemplateBuilder.AtomButton, IdcButtonUncheckAll, buttonStyle, 0,
                x, buttonTop, ButtonUncheckAllWidth, ButtonHeight, "全部取消勾選(&U)");

            builder.AddControl(DialogTemplateBuilder.AtomButton, IdcButtonAdd,
                buttonStyle | NativeDialog.BS_DEFPUSHBUTTON, 0,
                (short)(DialogWidth - Margin - ButtonCancelWidth - Gap - ButtonAddWidth), buttonTop,
                ButtonAddWidth, ButtonHeight, "加入(&A)");

            builder.AddControl(DialogTemplateBuilder.AtomButton, IdcButtonCancel, buttonStyle, 0,
                (short)(DialogWidth - Margin - ButtonCancelWidth), buttonTop,
                ButtonCancelWidth, ButtonHeight, "取消");

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

            windowList = new NativeListView(NativeDialog.GetDlgItem(dialogHandle, IdcWindowList));
            windowList.EnableModernStyle();
            windowList.EnableCheckBoxes();

            string[] headers = { "行程名稱", "視窗標題", "顯示器", "X", "Y", "寬度", "高度", "狀態", "快照中" };
            for (int i = 0; i < headers.Length; ++i)
            {
                int format = (i >= 3 && i <= 6) ? NativeDialog.LVCFMT_RIGHT : NativeDialog.LVCFMT_LEFT;
                windowList.InsertColumn(i, headers[i], 80, format);
            }

            for (int i = 0; i < windows.Count; ++i)
            {
                var info = windows[i];
                var columns = new List<string>
                {
                    info.ProcessName,
                    info.Title,
                    info.MonitorText,
                    info.X.ToString(),
                    info.Y.ToString(),
                    info.Width.ToString(),
                    info.Height.ToString(),
                    info.State,
                    info.AlreadyInSnapshot ? "已在快照中" : String.Empty,
                };
                windowList.InsertRow(i, columns, new IntPtr(i));
            }

            if (windows.Count > 0)
                windowList.Select(0);

            LayoutControls();
            UpdateState();
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

            // 勾選方塊或選取列有變動時更新狀態與按鈕
            var header = (NativeDialog.NMHDR)Marshal.PtrToStructure(lParam, typeof(NativeDialog.NMHDR));
            if (header.idFrom.ToInt64() == IdcWindowList && header.code == NativeDialog.LVN_ITEMCHANGED)
                UpdateState();

            return IntPtr.Zero;
        }

        private IntPtr OnCommand(int controlId)
        {
            switch (controlId)
            {
                case IdcButtonSameProcess:
                    CheckSameProcess();
                    return new IntPtr(1);

                case IdcButtonUncheckAll:
                    for (int i = 0; i < windows.Count; ++i)
                        windowList.SetChecked(i, false);
                    UpdateState();
                    return new IntPtr(1);

                case IdcButtonAdd:
                    var checkedHandles = GetCheckedHandles();
                    if (checkedHandles.Count == 0)
                    {
                        NativeDialog.MessageBox(dialogHandle, "請先勾選要加入的視窗。", WindowTitle,
                            NativeDialog.MB_OK | NativeDialog.MB_ICONINFORMATION);
                        return new IntPtr(1);
                    }

                    result = checkedHandles;
                    NativeDialog.EndDialog(dialogHandle, new IntPtr(IdcButtonAdd));
                    return new IntPtr(1);

                case IdcButtonCancel:
                    NativeDialog.EndDialog(dialogHandle, IntPtr.Zero);
                    return new IntPtr(1);
            }

            return IntPtr.Zero;
        }

        #endregion

        #region 勾選

        /// <summary>
        /// 勾選與目前選取列同一個行程的所有視窗，例如一次勾選 chrome 的全部視窗。
        /// </summary>
        private void CheckSameProcess()
        {
            int index = windowList.SelectedIndex;
            if (index < 0 || index >= windows.Count)
                return;

            string processName = windows[index].ProcessName;
            for (int i = 0; i < windows.Count; ++i)
            {
                if (String.Equals(windows[i].ProcessName, processName, StringComparison.OrdinalIgnoreCase))
                    windowList.SetChecked(i, true);
            }

            UpdateState();
        }

        private List<IntPtr> GetCheckedHandles()
        {
            var handles = new List<IntPtr>();
            for (int i = 0; i < windows.Count; ++i)
            {
                if (windowList.IsChecked(i))
                    handles.Add(windows[i].WindowHandle);
            }

            return handles;
        }

        private void UpdateState()
        {
            if (windowList == null)
                return;

            int checkedCount = 0;
            int alreadyCount = 0;
            for (int i = 0; i < windows.Count; ++i)
            {
                if (!windowList.IsChecked(i))
                    continue;

                ++checkedCount;
                if (windows[i].AlreadyInSnapshot)
                    ++alreadyCount;
            }

            string status;
            if (windows.Count == 0)
                status = "目前沒有可加入的視窗。";
            else if (checkedCount == 0)
                status = String.Format("共 {0} 個視窗，尚未勾選。", windows.Count);
            else if (alreadyCount == 0)
                status = String.Format("已勾選 {0} 個視窗。", checkedCount);
            else
                status = String.Format("已勾選 {0} 個視窗，其中 {1} 個已在快照中，加入後會更新為目前的位置。",
                    checkedCount, alreadyCount);

            NativeDialog.SetDlgItemTextW(dialogHandle, IdcStatusLabel, status);

            int selected = windowList.SelectedIndex;
            NativeDialog.EnableWindow(NativeDialog.GetDlgItem(dialogHandle, IdcButtonSameProcess),
                selected >= 0 && selected < windows.Count);
            NativeDialog.EnableWindow(NativeDialog.GetDlgItem(dialogHandle, IdcButtonUncheckAll), checkedCount > 0);
            NativeDialog.EnableWindow(NativeDialog.GetDlgItem(dialogHandle, IdcButtonAdd), checkedCount > 0);
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

            int y = marginY;
            MoveControl(IdcHintLabel, margin, y, contentWidth, labelHeight);
            y += labelHeight + gapY;

            int buttonTop = height - marginY - buttonHeight;
            int statusTop = buttonTop - Dy(ButtonBarGap) - labelHeight;
            int listHeight = statusTop - gapY - y;
            if (listHeight < Dy(40))
                listHeight = Dy(40);

            MoveControl(IdcWindowList, margin, y, contentWidth, listHeight);
            MoveControl(IdcStatusLabel, margin, statusTop, contentWidth, labelHeight);

            int x = margin;
            int w = Dx(ButtonSameProcessWidth);
            MoveControl(IdcButtonSameProcess, x, buttonTop, w, buttonHeight);
            x += w + gap;
            MoveControl(IdcButtonUncheckAll, x, buttonTop, Dx(ButtonUncheckAllWidth), buttonHeight);

            int cancelWidth = Dx(ButtonCancelWidth);
            int addWidth = Dx(ButtonAddWidth);
            MoveControl(IdcButtonCancel, width - margin - cancelWidth, buttonTop, cancelWidth, buttonHeight);
            MoveControl(IdcButtonAdd, width - margin - cancelWidth - gap - addWidth, buttonTop, addWidth, buttonHeight);

            DistributeColumns(contentWidth);
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
            if (windowList == null || windowList.Handle == IntPtr.Zero)
                return;

            int usable = totalWidth - 24;
            if (usable <= 0)
                return;

            int assigned = 0;
            for (int i = 0; i < ColumnWeights.Length; ++i)
            {
                int w = i == ColumnWeights.Length - 1 ? usable - assigned : (int)(usable * ColumnWeights[i]);
                if (w < 36)
                    w = 36;
                windowList.SetColumnWidth(i, w);
                assigned += w;
            }
        }

        #endregion

        private static int LowWord(IntPtr value)
        {
            return (int)(value.ToInt64() & 0xFFFF);
        }
    }
}
