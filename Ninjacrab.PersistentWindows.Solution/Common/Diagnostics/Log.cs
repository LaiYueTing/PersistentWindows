using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Diagnostics;

namespace PersistentWindows.Common.Diagnostics
{
    public class Log
    {
        static EventLog eventLog;
        public static bool silent = false;
        static bool registered = false;

        // 檔案記錄
        //
        // 只寫 Windows 事件記錄並不可靠：註冊事件來源需要系統管理員權限，
        // 而 EventLog.SourceExists() 在一般權限下甚至會直接擲出 SecurityException
        // （無法搜尋 Security 與 State 記錄檔）。使用者因此常常什麼記錄都看不到。
        // 這裡另外寫一份純文字記錄檔，不需要任何特殊權限。
        private const long MaxLogFileBytes = 2 * 1024 * 1024;
        private const int RotatedLogCount = 2;

        private static readonly object fileLock = new object();
        private static string logFilePath;
        private static List<string> pendingLines = new List<string>();

        /// <summary>記錄檔完整路徑，尚未設定時為 null。</summary>
        public static string LogFilePath
        {
            get { return logFilePath; }
        }

        /// <summary>
        /// 設定記錄檔位置並寫出在此之前暫存的內容。
        ///
        /// Init() 在程式啟動最早期就被呼叫，那時還沒解析命令列、不知道資料夾在哪，
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
                // 無法建立記錄檔時安靜略過，事件記錄仍然可用
            }
        }

        /// <summary>
        /// 移除訊息開頭由 Format() 加上的「時間 :: 」前綴。
        ///
        /// 事件來源未註冊時（一般權限下的常態），Format() 會在每則訊息前面
        /// 加上時間戳。記錄檔本身已經有獨立的時間欄位，若不移除就會重複顯示。
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

        public static void Init()
        {
            eventLog = new EventLog();
            string app_name = System.Windows.Forms.Application.ProductName;
            try
            {
                if (!EventLog.SourceExists(app_name))
                {
                    // CreateEventSource requires administrative privileges
                    EventLog.CreateEventSource(app_name, "Application");
                    Console.WriteLine($"Created Event Source '{app_name}'. Please restart the application for changes to take full effect.");
                    // Note: If you create a new source for a custom log, you might need to restart the computer for changes to take full effect in the Event Viewer.
                }
                registered = true;
                eventLog.Source = app_name;
            }
            catch (Exception)
            {
                eventLog.Source = "Application";
            }
        }

        public static void Exit()
        {
            if (eventLog != null)
                eventLog.Close();
        }

        /// <summary>
        /// 寫入 Windows 事件記錄。
        ///
        /// 記錄本身絕不能讓程式中斷：Init() 未被呼叫、事件來源未註冊或事件記錄已滿時，
        /// 這裡一律安靜略過，否則 catch 區塊中的記錄呼叫會再拋一次例外，
        /// 反而蓋掉原始錯誤並導致行程結束。
        /// </summary>
        private static void WriteEntrySafe(string message, int eventId)
        {
            var log = eventLog;
            if (log == null)
                return;

            try
            {
                if (!registered)
                {
                    int index = message.IndexOf("::");
                    if (index >= 0)
                        message = message.Substring(index + 3);
                    message = System.Windows.Forms.Application.ProductName + ": " + message;
                }

                log.WriteEntry(message, EventLogEntryType.Information, eventId, 0);
            }
            catch (Exception)
            {
                // 無法寫入事件記錄時安靜略過
            }
        }

        /// <summary>
        /// Occurs when something is logged. STATIC EVENT!
        /// </summary>

        public static void Trace(string format, params object[] args)
        {
            if (silent)
                return;
#if DEBUG
            var message = Format(format, args);
            Console.Write(message);
#endif
        }

        public static void Info(string format, params object[] args)
        {
            if (silent)
                return;
#if DEBUG
            var message = Format(format, args);
            Console.Write(message);
#endif
        }

        public static void Error(string format, params object[] args)
        {
            if (silent)
                return;

            var message = Format(format, args);
            WriteToFile("錯誤", message);

            if (message.Contains("Cannot create a file when that file already exists"))
            {
                // ignore trivial error
                return;
            }

            if (message.Contains("Access is denied"))
            {
                // ignore window move failure due to lack of admin privilege
                return;
            }

#if DEBUG
            Console.Write(message);
#endif
            WriteEntrySafe(message, 9999);
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
            WriteEntrySafe(message, 9990);
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

            bool arg_null = args.Length == 0;
            if (!registered)
            return arg_null ? $"{DateTime.Now} :: " + format + "\n":
                $"{DateTime.Now} :: " + string.Format(format, args) + "\n";

            return arg_null ? format + "\n":
                string.Format(format, args) + "\n";
        }

    }
}
