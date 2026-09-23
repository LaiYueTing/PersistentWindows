using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace PersistentWindows.Common.WinApiBridge
{
    /// <summary>
    /// 原生 Win32 對話框的委派原型 (DLGPROC)。
    /// </summary>
    public delegate IntPtr DialogProc(IntPtr hwndDlg, uint uMsg, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// 以純 Win32 API 建立對話框所需的宣告與輔助程式。
    ///
    /// 由於 .NET 組件無法內嵌 RT_DIALOG 資源，這裡改用 DialogBoxParamW 的記憶體版本
    /// DialogBoxIndirectParamW，於執行期組出 DLGTEMPLATE，語意與行為完全相同，
    /// 且維持零額外相依性。所有字串 API 一律採用寬字元 (*W) 版本。
    /// </summary>
    public static class NativeDialog
    {
        #region 視窗樣式常數

        public const uint WS_OVERLAPPED = 0x00000000;
        public const uint WS_POPUP = 0x80000000;
        public const uint WS_CHILD = 0x40000000;
        public const uint WS_VISIBLE = 0x10000000;
        public const uint WS_DISABLED = 0x08000000;
        public const uint WS_CLIPSIBLINGS = 0x04000000;
        public const uint WS_CLIPCHILDREN = 0x02000000;
        public const uint WS_CAPTION = 0x00C00000;
        public const uint WS_BORDER = 0x00800000;
        public const uint WS_DLGFRAME = 0x00400000;
        public const uint WS_VSCROLL = 0x00200000;
        public const uint WS_SYSMENU = 0x00080000;
        public const uint WS_THICKFRAME = 0x00040000;
        public const uint WS_GROUP = 0x00020000;
        public const uint WS_TABSTOP = 0x00010000;
        public const uint WS_MINIMIZEBOX = 0x00020000;
        public const uint WS_MAXIMIZEBOX = 0x00010000;

        public const uint WS_EX_CLIENTEDGE = 0x00000200;
        public const uint WS_EX_CONTROLPARENT = 0x00010000;
        public const uint WS_EX_APPWINDOW = 0x00040000;

        public const uint DS_SETFONT = 0x0040;
        public const uint DS_MODALFRAME = 0x0080;
        public const uint DS_FIXEDSYS = 0x0008;
        public const uint DS_CENTER = 0x0800;
        public const uint DS_NOIDLEMSG = 0x0100;

        public const uint BS_PUSHBUTTON = 0x00000000;
        public const uint BS_DEFPUSHBUTTON = 0x00000001;

        public const uint SS_LEFT = 0x00000000;
        public const uint SS_LEFTNOWORDWRAP = 0x0000000C;

        public const uint ES_LEFT = 0x00000000;
        public const uint ES_AUTOHSCROLL = 0x00000080;

        public const uint LVS_ICON = 0x0000;
        public const uint LVS_REPORT = 0x0001;
        public const uint LVS_SINGLESEL = 0x0004;
        public const uint LVS_SHOWSELALWAYS = 0x0008;
        public const uint LVS_NOSORTHEADER = 0x8000;

        #endregion

        #region 視窗訊息常數

        public const uint WM_INITDIALOG = 0x0110;
        public const uint WM_COMMAND = 0x0111;
        public const uint WM_CLOSE = 0x0010;
        public const uint WM_DESTROY = 0x0002;
        public const uint WM_SIZE = 0x0005;
        public const uint WM_GETMINMAXINFO = 0x0024;
        public const uint WM_NOTIFY = 0x004E;
        public const uint WM_SETFONT = 0x0030;
        public const uint WM_SETICON = 0x0080;
        public const uint WM_APP = 0x8000;

        /// <summary>編輯方塊內容變更的通知碼，位於 WM_COMMAND 的高位字組。</summary>
        public const int EN_CHANGE = 0x0300;

        public const int IDOK = 1;
        public const int IDCANCEL = 2;
        public const int IDYES = 6;
        public const int IDNO = 7;

        public const uint MB_OK = 0x00000000;
        public const uint MB_OKCANCEL = 0x00000001;
        public const uint MB_YESNO = 0x00000004;
        public const uint MB_ICONERROR = 0x00000010;
        public const uint MB_ICONQUESTION = 0x00000020;
        public const uint MB_ICONWARNING = 0x00000030;
        public const uint MB_ICONINFORMATION = 0x00000040;
        public const uint MB_DEFBUTTON2 = 0x00000100;

        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;

        public const int SW_SHOWNORMAL = 1;

        public const int ICON_SMALL = 0;
        public const int ICON_BIG = 1;

        #endregion

        #region SysListView32 常數

        public const uint LVM_FIRST = 0x1000;
        public const uint LVM_DELETEALLITEMS = LVM_FIRST + 9;
        public const uint LVM_GETITEMCOUNT = LVM_FIRST + 4;
        public const uint LVM_GETNEXTITEM = LVM_FIRST + 12;
        public const uint LVM_SETITEMSTATE = LVM_FIRST + 43;
        public const uint LVM_ENSUREVISIBLE = LVM_FIRST + 19;
        public const uint LVM_SETCOLUMNWIDTH = LVM_FIRST + 30;
        public const uint LVM_SETEXTENDEDLISTVIEWSTYLE = LVM_FIRST + 54;
        public const uint LVM_INSERTITEMW = LVM_FIRST + 77;
        public const uint LVM_SETITEMTEXTW = LVM_FIRST + 116;
        public const uint LVM_INSERTCOLUMNW = LVM_FIRST + 97;

        public const uint LVS_EX_GRIDLINES = 0x00000001;
        public const uint LVS_EX_FULLROWSELECT = 0x00000020;
        public const uint LVS_EX_DOUBLEBUFFER = 0x00010000;

        public const uint LVCF_FMT = 0x0001;
        public const uint LVCF_WIDTH = 0x0002;
        public const uint LVCF_TEXT = 0x0004;
        public const uint LVCF_SUBITEM = 0x0008;

        public const int LVCFMT_LEFT = 0x0000;
        public const int LVCFMT_RIGHT = 0x0001;

        public const uint LVIF_TEXT = 0x0001;
        public const uint LVIF_PARAM = 0x0004;
        public const uint LVIF_STATE = 0x0008;

        public const uint LVIS_FOCUSED = 0x0001;
        public const uint LVIS_SELECTED = 0x0002;

        public const uint LVNI_SELECTED = 0x0002;

        public const int LVN_FIRST = -100;
        public const int LVN_ITEMCHANGED = LVN_FIRST - 1;
        public const int NM_DBLCLK = -3;

        public const uint ICC_LISTVIEW_CLASSES = 0x00000001;
        public const uint ICC_STANDARD_CLASSES = 0x00004000;

        #endregion

        #region 結構

        [StructLayout(LayoutKind.Sequential)]
        public struct INITCOMMONCONTROLSEX
        {
            public int dwSize;
            public uint dwICC;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct LVCOLUMNW
        {
            public uint mask;
            public int fmt;
            public int cx;
            public IntPtr pszText;
            public int cchTextMax;
            public int iSubItem;
            public int iImage;
            public int iOrder;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct LVITEMW
        {
            public uint mask;
            public int iItem;
            public int iSubItem;
            public uint state;
            public uint stateMask;
            public IntPtr pszText;
            public int cchTextMax;
            public int iImage;
            public IntPtr lParam;
            public int iIndent;
            public int iGroupId;
            public uint cColumns;
            public IntPtr puColumns;
            public IntPtr piColFmt;
            public int iGroup;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NMHDR
        {
            public IntPtr hwndFrom;
            public IntPtr idFrom;
            public int code;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NMLISTVIEW
        {
            public NMHDR hdr;
            public int iItem;
            public int iSubItem;
            public uint uNewState;
            public uint uOldState;
            public uint uChanged;
            public POINT ptAction;
            public IntPtr lParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        #endregion

        #region P/Invoke

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "DialogBoxIndirectParamW")]
        public static extern IntPtr DialogBoxIndirectParamW(IntPtr hInstance, IntPtr lpTemplate,
            IntPtr hWndParent, DialogProc lpDialogFunc, IntPtr dwInitParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool EndDialog(IntPtr hDlg, IntPtr nResult);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetDlgItem(IntPtr hDlg, int nIDDlgItem);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SetDlgItemTextW")]
        public static extern bool SetDlgItemTextW(IntPtr hDlg, int nIDDlgItem, string lpString);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetDlgItemTextW")]
        public static extern uint GetDlgItemTextW(IntPtr hDlg, int nIDDlgItem, StringBuilder lpString, int cchMax);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SetWindowTextW")]
        public static extern bool SetWindowTextW(IntPtr hWnd, string lpString);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")]
        public static extern IntPtr SendMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")]
        public static extern IntPtr SendMessageW(IntPtr hWnd, uint msg, IntPtr wParam, ref LVCOLUMNW lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")]
        public static extern IntPtr SendMessageW(IntPtr hWnd, uint msg, IntPtr wParam, ref LVITEMW lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
        public static extern int MessageBoxW(IntPtr hWnd, string lpText, string lpCaption, uint uType);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool MapDialogRect(IntPtr hDlg, ref RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y,
            int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        public static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool EnableWindow(IntPtr hWnd, bool bEnable);

        [DllImport("user32.dll")]
        public static extern bool UpdateWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "LoadCursorW")]
        public static extern IntPtr LoadCursorW(IntPtr hInstance, IntPtr lpCursorName);

        [DllImport("user32.dll")]
        public static extern IntPtr SetCursor(IntPtr hCursor);

        public static readonly IntPtr IDC_ARROW = new IntPtr(32512);
        public static readonly IntPtr IDC_WAIT = new IntPtr(32514);

        [DllImport("comctl32.dll", SetLastError = true)]
        public static extern bool InitCommonControlsEx(ref INITCOMMONCONTROLSEX icce);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetModuleHandleW")]
        private static extern IntPtr GetModuleHandleW(string lpModuleName);

        #endregion

        /// <summary>
        /// 註冊 SysListView32 等通用控制項類別，必須在建立對話框前呼叫。
        /// </summary>
        public static void EnsureCommonControls()
        {
            var icce = new INITCOMMONCONTROLSEX();
            icce.dwSize = Marshal.SizeOf(typeof(INITCOMMONCONTROLSEX));
            icce.dwICC = ICC_LISTVIEW_CLASSES | ICC_STANDARD_CLASSES;
            try
            {
                InitCommonControlsEx(ref icce);
            }
            catch (Exception)
            {
                // 舊版 comctl32 沒有此進入點，可忽略
            }
        }

        /// <summary>
        /// 取得目前行程的模組控制代碼，作為 DialogBoxIndirectParamW 的 hInstance。
        /// </summary>
        public static IntPtr GetCurrentInstance()
        {
            return GetModuleHandleW(null);
        }

        /// <summary>
        /// 以對話框單位 (DLU) 為基準取得像素換算比例。
        /// </summary>
        public static void GetDialogBaseUnits(IntPtr hDlg, out int baseUnitX, out int baseUnitY)
        {
            var rect = new RECT { Left = 0, Top = 0, Right = 4, Bottom = 8 };
            MapDialogRect(hDlg, ref rect);
            baseUnitX = rect.Right > 0 ? rect.Right : 4;
            baseUnitY = rect.Bottom > 0 ? rect.Bottom : 8;
        }

        /// <summary>
        /// 顯示原生訊息方塊，一律使用寬字元版本。
        /// </summary>
        public static int MessageBox(IntPtr owner, string text, string caption, uint type)
        {
            return MessageBoxW(owner, text, caption, type);
        }
    }

    /// <summary>
    /// 於執行期組出 DLGTEMPLATE / DLGITEMTEMPLATE 位元組資料。
    /// 所有字串皆以 UTF-16 寫入，對話框因此天生為 Unicode 對話框。
    /// </summary>
    public class DialogTemplateBuilder
    {
        // 系統預先定義的控制項類別原子值
        public const ushort AtomButton = 0x0080;
        public const ushort AtomEdit = 0x0081;
        public const ushort AtomStatic = 0x0082;
        public const ushort AtomListBox = 0x0083;
        public const ushort AtomScrollBar = 0x0084;
        public const ushort AtomComboBox = 0x0085;

        private readonly MemoryStream stream = new MemoryStream();
        private readonly BinaryWriter writer;
        private ushort itemCount;
        private readonly long itemCountOffset;

        public DialogTemplateBuilder(uint style, uint exStyle, short x, short y, short cx, short cy,
            string title, short fontPointSize, string fontFace)
        {
            writer = new BinaryWriter(stream, Encoding.Unicode);

            writer.Write(style | NativeDialog.DS_SETFONT);
            writer.Write(exStyle);
            itemCountOffset = stream.Position;
            writer.Write((ushort)0); // cdit，於 Build() 時回填
            writer.Write(x);
            writer.Write(y);
            writer.Write(cx);
            writer.Write(cy);

            writer.Write((ushort)0);            // 無功能表
            writer.Write((ushort)0);            // 使用預設對話框視窗類別
            WriteWideString(title);             // 標題

            writer.Write(fontPointSize);
            WriteWideString(fontFace);
        }

        /// <summary>
        /// 加入使用系統預先定義類別 (按鈕、靜態文字、編輯方塊 ...) 的控制項。
        /// </summary>
        public void AddControl(ushort classAtom, int id, uint style, uint exStyle,
            short x, short y, short cx, short cy, string text)
        {
            BeginItem(id, style, exStyle, x, y, cx, cy);
            writer.Write((ushort)0xFFFF);
            writer.Write(classAtom);
            WriteWideString(text);
            writer.Write((ushort)0); // 無額外建立資料
        }

        /// <summary>
        /// 加入以類別名稱指定的控制項，例如 SysListView32。
        /// </summary>
        public void AddControl(string className, int id, uint style, uint exStyle,
            short x, short y, short cx, short cy, string text)
        {
            BeginItem(id, style, exStyle, x, y, cx, cy);
            WriteWideString(className);
            WriteWideString(text);
            writer.Write((ushort)0); // 無額外建立資料
        }

        private void BeginItem(int id, uint style, uint exStyle,
            short x, short y, short cx, short cy)
        {
            AlignToDword();
            writer.Write(style);
            writer.Write(exStyle);
            writer.Write(x);
            writer.Write(y);
            writer.Write(cx);
            writer.Write(cy);
            writer.Write((ushort)id);
            ++itemCount;
        }

        private void AlignToDword()
        {
            while (stream.Position % 4 != 0)
                writer.Write((byte)0);
        }

        private void WriteWideString(string text)
        {
            if (!String.IsNullOrEmpty(text))
            {
                foreach (char c in text)
                    writer.Write((ushort)c);
            }
            writer.Write((ushort)0);
        }

        /// <summary>
        /// 產生樣板位元組資料，呼叫端需自行以 Marshal.AllocHGlobal 複製到非受管記憶體。
        /// </summary>
        public byte[] Build()
        {
            writer.Flush();
            byte[] data = stream.ToArray();
            data[itemCountOffset] = (byte)(itemCount & 0xFF);
            data[itemCountOffset + 1] = (byte)((itemCount >> 8) & 0xFF);
            return data;
        }
    }

    /// <summary>
    /// SysListView32 的輕量包裝，僅提供快照管理介面所需的操作。
    /// </summary>
    public class NativeListView
    {
        private readonly IntPtr handle;

        public NativeListView(IntPtr hwnd)
        {
            handle = hwnd;
        }

        public IntPtr Handle
        {
            get { return handle; }
        }

        public void EnableModernStyle()
        {
            // 不啟用 LVS_EX_GRIDLINES：格線只會畫在有資料的列上，
            // 清單下半部的空白區便只剩直線而沒有橫線，看起來並不一致
            uint exStyle = NativeDialog.LVS_EX_FULLROWSELECT | NativeDialog.LVS_EX_DOUBLEBUFFER;
            NativeDialog.SendMessageW(handle, NativeDialog.LVM_SETEXTENDEDLISTVIEWSTYLE,
                new IntPtr((int)exStyle), new IntPtr((int)exStyle));
        }

        public void InsertColumn(int index, string text, int width, int format)
        {
            IntPtr textPtr = Marshal.StringToHGlobalUni(text);
            try
            {
                var column = new LVCOLUMNWHolder();
                column.Value.mask = NativeDialog.LVCF_FMT | NativeDialog.LVCF_WIDTH
                    | NativeDialog.LVCF_TEXT | NativeDialog.LVCF_SUBITEM;
                column.Value.fmt = format;
                column.Value.cx = width;
                column.Value.pszText = textPtr;
                column.Value.iSubItem = index;
                NativeDialog.SendMessageW(handle, NativeDialog.LVM_INSERTCOLUMNW,
                    new IntPtr(index), ref column.Value);
            }
            finally
            {
                Marshal.FreeHGlobal(textPtr);
            }
        }

        public void SetColumnWidth(int index, int width)
        {
            NativeDialog.SendMessageW(handle, NativeDialog.LVM_SETCOLUMNWIDTH,
                new IntPtr(index), new IntPtr(width));
        }

        public void Clear()
        {
            NativeDialog.SendMessageW(handle, NativeDialog.LVM_DELETEALLITEMS, IntPtr.Zero, IntPtr.Zero);
        }

        public int ItemCount
        {
            get
            {
                return NativeDialog.SendMessageW(handle, NativeDialog.LVM_GETITEMCOUNT,
                    IntPtr.Zero, IntPtr.Zero).ToInt32();
            }
        }

        public int InsertRow(int index, IList<string> columns, IntPtr tag)
        {
            int inserted;
            IntPtr textPtr = Marshal.StringToHGlobalUni(columns.Count > 0 ? columns[0] : String.Empty);
            try
            {
                var item = new LVITEMWHolder();
                item.Value.mask = NativeDialog.LVIF_TEXT | NativeDialog.LVIF_PARAM;
                item.Value.iItem = index;
                item.Value.iSubItem = 0;
                item.Value.pszText = textPtr;
                item.Value.lParam = tag;
                inserted = NativeDialog.SendMessageW(handle, NativeDialog.LVM_INSERTITEMW,
                    IntPtr.Zero, ref item.Value).ToInt32();
            }
            finally
            {
                Marshal.FreeHGlobal(textPtr);
            }

            if (inserted < 0)
                return inserted;

            for (int i = 1; i < columns.Count; ++i)
                SetSubItemText(inserted, i, columns[i]);

            return inserted;
        }

        public void SetSubItemText(int row, int column, string text)
        {
            IntPtr textPtr = Marshal.StringToHGlobalUni(text ?? String.Empty);
            try
            {
                var item = new LVITEMWHolder();
                item.Value.mask = NativeDialog.LVIF_TEXT;
                item.Value.iItem = row;
                item.Value.iSubItem = column;
                item.Value.pszText = textPtr;
                NativeDialog.SendMessageW(handle, NativeDialog.LVM_SETITEMTEXTW,
                    new IntPtr(row), ref item.Value);
            }
            finally
            {
                Marshal.FreeHGlobal(textPtr);
            }
        }

        public int SelectedIndex
        {
            get
            {
                return NativeDialog.SendMessageW(handle, NativeDialog.LVM_GETNEXTITEM,
                    new IntPtr(-1), new IntPtr((int)NativeDialog.LVNI_SELECTED)).ToInt32();
            }
        }

        public void Select(int index)
        {
            if (index < 0)
                return;

            var item = new LVITEMWHolder();
            item.Value.mask = NativeDialog.LVIF_STATE;
            item.Value.state = NativeDialog.LVIS_SELECTED | NativeDialog.LVIS_FOCUSED;
            item.Value.stateMask = NativeDialog.LVIS_SELECTED | NativeDialog.LVIS_FOCUSED;
            NativeDialog.SendMessageW(handle, NativeDialog.LVM_SETITEMSTATE,
                new IntPtr(index), ref item.Value);
            NativeDialog.SendMessageW(handle, NativeDialog.LVM_ENSUREVISIBLE,
                new IntPtr(index), IntPtr.Zero);
        }

        // ref 參數必須指向可定址的欄位，因此以持有者類別包裝結構
        private class LVCOLUMNWHolder
        {
            public NativeDialog.LVCOLUMNW Value;
        }

        private class LVITEMWHolder
        {
            public NativeDialog.LVITEMW Value;
        }
    }
}
