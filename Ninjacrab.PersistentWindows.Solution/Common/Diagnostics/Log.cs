using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace PersistentWindows.Common.Diagnostics
{
    public class Log
    {
        public static bool silent = false;

        // 記錄檔
        //
        // 上游把記錄寫進 Windows 事件記錄，但註冊事件來源需要系統管理員權限，
        // 而 EventLog.SourceExists() 在一般權限下甚至會直接擲出 SecurityException
        // （無法搜尋 Security 與 State 記錄檔）。使用者因此常常什麼記錄都看不到，
        // 就算看得到也得自己開事件檢視器再篩選。
        // 現在只寫這份純文字記錄檔，不需要任何特殊權限，記錄檢視也讀得即時。

        private const long MaxLogFileBytes = 2 * 1024 * 1024;
        private const int RotatedLogCount = 2;

        private static readonly object fileLock = new object();
        private static string logFilePath;
        private static List<string> pendingLines = new List<string>();

        // 可忽略的雜訊錯誤
        //
        // 這兩種失敗在正常運作中會頻繁出現（建立既有檔案、缺少系統管理員權限
        // 而無法移動別人的視窗），不是使用者需要處理的問題，記成錯誤只會蓋掉真正的失敗。
        // 但訊息文字是作業系統產生的，在繁體中文系統上是「存取被拒。」之類的中文，
        // 原本寫死的英文比對永遠不會成立，過濾等於失效。
        // 這裡改為在啟動時依錯誤碼取得當地語言訊息，英文字串則保留以相容其他語系。
        private const int ErrorAccessDenied = 5;
        private const int ErrorFileExists = 80;
        private static readonly string[] IgnorableErrors = BuildIgnorableErrors();

        private static string[] BuildIgnorableErrors()
        {
            var list = new List<string>
            {
                "Cannot create a file when that file already exists",
                "Access is denied",
            };

            foreach (int code in new[] { ErrorFileExists, ErrorAccessDenied })
            {
                try
                {
                    string localized = new System.ComponentModel.Win32Exception(code).Message;
                    if (!String.IsNullOrEmpty(localized) && !list.Contains(localized))
                        list.Add(localized);
                }
                catch (Exception)
                {
                    // 取不到當地語言訊息時沿用英文比對
                }
            }

            return list.ToArray();
        }

        /// <summary>訊息是否屬於可忽略的雜訊錯誤。</summary>
        public static bool IsIgnorableError(string message)
        {
            if (String.IsNullOrEmpty(message))
                return false;

            foreach (var known in IgnorableErrors)
            {
                if (message.IndexOf(known, StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }

        /// <summary>記錄檔完整路徑，尚未設定時為 null。</summary>
        public static string LogFilePath
        {
            get { return logFilePath; }
        }

        /// <summary>
        /// 設定記錄檔位置並寫出在此之前暫存的內容。
        ///
        /// 程式啟動最早期就可能寫入記錄，那時還沒解析命令列、不知道資料夾在哪，
        /// 因此先把訊息留在記憶體，等這個方法被呼叫後再一次寫出。
        /// </summary>
        public static void SetLogFolder(string folder)
        {
            if (String.IsNullOrEmpty(folder))
                return;

            try
            {
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                string path = Path.Combine(folder,
                    System.Windows.Forms.Application.ProductName + ".log");

                List<string> pending;
                lock (fileLock)
                {
                    logFilePath = path;
                    pending = pendingLines;
                    pendingLines = new List<string>();
                }

                foreach (var line in pending)
                    AppendToFile(line);
            }
            catch (Exception)
            {
                // 無法建立記錄檔時安靜略過，記錄不能反過來把程式弄壞
            }
        }

        /// <summary>
        /// 移除訊息開頭的「時間 :: 」前綴。
        ///
        /// 舊版會在每則訊息前面加上時間戳（為了事件記錄），記錄檔本身已經有
        /// 獨立的時間欄位，不移除就會重複顯示。現在不再產生這種前綴，
        /// 但讀取舊記錄檔時仍需要處理。
        /// 只有在前綴確實能解析成日期時才移除，避免誤傷內容中的 "::"。
        /// </summary>
        public static string StripLeadingTimestamp(string message)
        {
            if (String.IsNullOrEmpty(message))
                return String.Empty;

            const string separator = " :: ";
            int index = message.IndexOf(separator, StringComparison.Ordinal);
            if (index <= 0 || index > 40)
                return message;

            DateTime parsed;
            if (!DateTime.TryParse(message.Substring(0, index), out parsed))
                return message;

            return message.Substring(index + separator.Length);
        }

        /// <summary>
        /// 將一行訊息寫入記錄檔；尚未設定路徑時先暫存於記憶體。
        /// </summary>
        private static void WriteToFile(string kind, string message)
        {
            string body = StripLeadingTimestamp(message ?? String.Empty)
                .Replace("\r\n", " ").Replace("\n", " ").TrimEnd();

            // 上游用空字串當主控台的分隔行，寫進記錄檔只會變成空白列
            if (body.Length == 0)
                return;

            string line = String.Format("{0:yyyy-MM-dd HH:mm:ss.fff}\t{1}\t{2}",
                DateTime.Now, kind, body);

            lock (fileLock)
            {
                if (logFilePath == null)
                {
                    // 避免在極端情況下無限增長
                    if (pendingLines.Count < 512)
                        pendingLines.Add(line);
                    return;
                }
            }

            AppendToFile(line);
        }

        private static void AppendToFile(string line)
        {
            try
            {
                lock (fileLock)
                {
                    if (logFilePath == null)
                        return;

                    RotateIfNeeded();

                    // 帶 BOM 的 UTF-8，記事本才能正確顯示中文
                    bool needBom = !File.Exists(logFilePath) || new FileInfo(logFilePath).Length == 0;
                    using (var writer = new StreamWriter(logFilePath, true, new UTF8Encoding(needBom)))
                    {
                        writer.WriteLine(line);
                    }
                }
            }
            catch (Exception)
            {
                // 記錄失敗絕不能影響主功能
            }
        }

        /// <summary>
        /// 清空記錄檔並刪除輪替後的舊檔。成功時回傳 null，否則回傳失敗原因。
        ///
        /// 目前的記錄檔是截斷而不是刪除：記錄檢視可能正以共用刪除模式開著它，
        /// 此時刪除只會讓檔案進入「等待刪除」狀態，接下來的寫入反而會失敗。
        /// </summary>
        public static string ClearLogFiles()
        {
            lock (fileLock)
            {
                if (logFilePath == null)
                    return "尚未設定記錄檔位置";

                try
                {
                    for (int i = RotatedLogCount; i >= 1; --i)
                    {
                        string rotated = logFilePath + "." + i;
                        if (File.Exists(rotated))
                            File.Delete(rotated);
                    }

                    if (File.Exists(logFilePath))
                    {
                        using (new FileStream(logFilePath, FileMode.Truncate, FileAccess.Write,
                            FileShare.ReadWrite | FileShare.Delete))
                        {
                        }
                    }
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }
            }

            return null;
        }

        /// <summary>
        /// 記錄檔超過上限時輪替，保留數份舊檔。
        /// </summary>
        private static void RotateIfNeeded()
        {
            try
            {
                if (!File.Exists(logFilePath))
                    return;

                var info = new FileInfo(logFilePath);
                if (info.Length < MaxLogFileBytes)
                    return;

                string oldest = logFilePath + "." + RotatedLogCount;
                if (File.Exists(oldest))
                    File.Delete(oldest);

                for (int i = RotatedLogCount - 1; i >= 1; --i)
                {
                    string from = logFilePath + "." + i;
                    if (File.Exists(from))
                        File.Move(from, logFilePath + "." + (i + 1));
                }

                File.Move(logFilePath, logFilePath + ".1");
            }
            catch (Exception)
            {
                // 輪替失敗就繼續往原檔追加
            }
        }

        public static void Trace(string format, params object[] args)
        {
            if (silent)
                return;
#if DEBUG
            var message = Format(format, args);
            Console.Write(message);
#endif
        }

        /// <summary>
        /// 記錄例行的內部動作。
        ///
        /// 上游把這類訊息全走 Log.Error，因為 Trace 與 Info 只在 DEBUG 版輸出，
        /// Release 版等於看不到；結果記錄檢視裡幾乎每一行都標成「錯誤」。
        /// 這裡讓資訊等級真的會輸出，在記錄檢視中可以單獨篩掉。
        /// </summary>
        public static void Info(string format, params object[] args)
        {
            if (silent)
                return;

            var message = Format(format, args);
            WriteToFile("資訊", message);
#if DEBUG
            Console.Write(message);
#endif
        }

        public static void Error(string format, params object[] args)
        {
            if (silent)
                return;

            var message = Format(format, args);

            // 可忽略的雜訊（檔案已存在、缺少系統管理員權限而無法移動視窗）降為資訊，
            // 內容仍然留著，但不會佔住「錯誤」這個等級
            WriteToFile(IsIgnorableError(message) ? "資訊" : "錯誤", message);
#if DEBUG
            Console.Write(message);
#endif
        }

        /// <summary>
        /// 記錄例外。
        ///
        /// 直接寫 ex.ToString() 會得到一整段英文堆疊，看不出是哪個動作出錯。
        /// 這裡先寫一行中文摘要（例外訊息本身由 .NET 依系統語言產生，
        /// 在繁體中文系統上就是中文），再單獨寫一行堆疊供追查。
        /// 呼叫端的成員名稱由編譯器填入，屬於程式識別碼，不做在地化。
        /// </summary>
        public static void Error(Exception ex, [CallerMemberName] string member = null)
        {
            if (ex == null)
                return;

            Error("{0} 發生例外（{1}）：{2}",
                String.IsNullOrEmpty(member) ? "未知位置" : member,
                ex.GetType().Name,
                ex.Message);

            // 換行交給 WriteToFile 壓成單行，這裡不重複處理
            string stack = ex.StackTrace;
            if (!String.IsNullOrEmpty(stack))
                Error("例外堆疊：{0}", stack.Trim());
        }

        public static void Event(string format, params object[] args)
        {
            if (silent)
                return;

            var message = Format(format, args);
            WriteToFile("事件", message);
#if DEBUG
            Console.Write(message);
#endif
        }

        /// <summary>
        /// Since string.Format doesn't like args being null or having no entries.
        /// </summary>
        /// <param name="format">The format.</param>
        /// <param name="args">The args.</param>
        /// <returns></returns>
        private static string Format(string format, params object[] args)
        {
            if (string.IsNullOrEmpty(format))
            {
                return "\n";
            }

            // 不再補上時間前綴：那是為了事件記錄而加的，記錄檔本身就有獨立的時間欄位
            bool arg_null = args.Length == 0;
            return arg_null ? format + "\n" : string.Format(format, args) + "\n";
        }

    }
}
