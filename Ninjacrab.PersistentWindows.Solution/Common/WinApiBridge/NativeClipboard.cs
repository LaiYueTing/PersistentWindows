using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace PersistentWindows.Common.WinApiBridge
{
    /// <summary>
    /// 以 Win32 API 直接把文字放進剪貼簿。
    ///
    /// System.Windows.Forms.Clipboard.SetText 走 OLE 剪貼簿，寫入時要開兩次剪貼簿
    /// （OleSetClipboard 與 OleFlushClipboard），只重試 10 次、每次 100 毫秒。
    /// 剪貼簿同一時間只能有一個程式開啟，而遠端桌面的剪貼簿同步（例如 RustDesk）、
    /// Windows 剪貼簿歷程記錄這類程式會在內容一變就跑來讀取，
    /// 剛好撞上就會以 CLIPBRD_E_CANT_OPEN (0x800401D0) 失敗。
    ///
    /// 這裡只開一次剪貼簿、持有時間盡量短，重試時間拉長，
    /// 真的失敗時也回報是哪個程式占住剪貼簿。
    /// </summary>
    public static class NativeClipboard
    {
        private const uint CF_UNICODETEXT = 13;
        private const uint GMEM_MOVEABLE = 0x0002;

        /// <summary>預設重試次數與間隔：合計約 2 秒。</summary>
        public const int DefaultRetryCount = 40;
        public const int DefaultRetryDelayMs = 50;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("user32.dll")]
        private static extern IntPtr GetOpenClipboardWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalFree(IntPtr hMem);

        /// <summary>
        /// 把文字放進剪貼簿。成功時回傳 null；失敗時回傳給使用者看的原因。
        /// </summary>
        /// <param name="owner">剪貼簿擁有者視窗，可為 IntPtr.Zero。</param>
        public static string SetText(IntPtr owner, string text,
            int retryCount = DefaultRetryCount, int retryDelayMs = DefaultRetryDelayMs)
        {
            if (text == null)
                text = String.Empty;

            // 先把資料準備好，開啟剪貼簿之後只做最少的事，縮短占用時間
            IntPtr memory = AllocUnicode(text);
            if (memory == IntPtr.Zero)
                return "記憶體不足，無法配置剪貼簿資料";

            bool opened = false;
            for (int attempt = 0; attempt < retryCount; ++attempt)
            {
                if (OpenClipboard(owner))
                {
                    opened = true;
                    break;
                }

                Thread.Sleep(retryDelayMs);
            }

            if (!opened)
            {
                GlobalFree(memory);
                string holder = DescribeHolder();
                return holder == null
                    ? "剪貼簿正被其他程式占用"
                    : String.Format("剪貼簿正被「{0}」占用", holder);
            }

            try
            {
                if (!EmptyClipboard())
                {
                    GlobalFree(memory);
                    return "無法清空剪貼簿";
                }

                // 成功後記憶體歸系統所有，不能再釋放
                if (SetClipboardData(CF_UNICODETEXT, memory) == IntPtr.Zero)
                {
                    GlobalFree(memory);
                    return "無法寫入剪貼簿";
                }

                return null;
            }
            finally
            {
                CloseClipboard();
            }
        }

        private static IntPtr AllocUnicode(string text)
        {
            // UTF-16 字元加上結尾的 null
            int bytes = (text.Length + 1) * 2;
            IntPtr memory = GlobalAlloc(GMEM_MOVEABLE, new UIntPtr((uint)bytes));
            if (memory == IntPtr.Zero)
                return IntPtr.Zero;

            IntPtr target = GlobalLock(memory);
            if (target == IntPtr.Zero)
            {
                GlobalFree(memory);
                return IntPtr.Zero;
            }

            try
            {
                Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
                Marshal.WriteInt16(target, text.Length * 2, 0);
            }
            finally
            {
                GlobalUnlock(memory);
            }

            return memory;
        }

        /// <summary>目前開著剪貼簿的程式名稱，查不到時回傳 null。</summary>
        public static string DescribeHolder()
        {
            try
            {
                IntPtr window = GetOpenClipboardWindow();
                if (window == IntPtr.Zero)
                    return null;

                uint processId;
                GetWindowThreadProcessId(window, out processId);
                if (processId == 0)
                    return null;

                using (var process = Process.GetProcessById((int)processId))
                {
                    return process.ProcessName;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
