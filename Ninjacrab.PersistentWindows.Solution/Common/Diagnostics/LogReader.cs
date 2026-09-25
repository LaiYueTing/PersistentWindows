using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

using PersistentWindows.Common.Models;

namespace PersistentWindows.Common.Diagnostics
{
    /// <summary>
    /// 從 Windows 事件記錄讀回 PersistentWindows 自己寫入的記錄。
    ///
    /// Log 會依事件來源是否註冊成功而有兩種寫法：
    /// 註冊成功時來源為產品名稱，直接寫入訊息；
    /// 註冊失敗時（CreateEventSource 需要系統管理員權限）來源為 Application，
    /// 訊息前面會加上「產品名稱: 」。這裡兩種都要認得。
    /// </summary>
    public static class LogReader
    {
        /// <summary>事件記錄檔名稱，屬於系統介面，不做在地化。</summary>
        private const string LogName = "Application";

        /// <summary>預設最多回傳的筆數。</summary>
        public const int DefaultMaxRecords = 1000;

        /// <summary>預設最多往回掃描的事件筆數，避免在龐大的記錄檔上耗時過久。</summary>
        public const int DefaultMaxScan = 20000;

        /// <summary>
        /// 判斷一筆事件記錄是否為 PersistentWindows 寫入的。
        /// 抽成純函式以便單獨測試，不需要真的事件記錄。
        /// </summary>
        public static bool Matches(string source, long instanceId, string message, string productName)
        {
            if (!IsOwnEventId(instanceId))
                return false;

            if (!String.IsNullOrEmpty(source) && String.Equals(source, productName, StringComparison.OrdinalIgnoreCase))
                return true;

            return !String.IsNullOrEmpty(message)
                && message.StartsWith(productName + ": ", StringComparison.Ordinal);
        }

        /// <summary>
        /// 事件識別碼是否為 PersistentWindows 使用的 9990 或 9999。
        /// InstanceId 高位可能帶有其他旗標，因此也比對低 16 位。
        /// </summary>
        public static bool IsOwnEventId(long instanceId)
        {
            long low = instanceId & 0xFFFF;
            return instanceId == LogRecord.EventIdEvent || instanceId == LogRecord.EventIdError
                || low == LogRecord.EventIdEvent || low == LogRecord.EventIdError;
        }

        /// <summary>
        /// 移除訊息開頭的產品名稱前綴，讓清單只顯示訊息本身。
        /// </summary>
        public static string StripPrefix(string message, string productName)
        {
            if (String.IsNullOrEmpty(message))
                return String.Empty;

            string prefix = productName + ": ";
            if (message.StartsWith(prefix, StringComparison.Ordinal))
                message = message.Substring(prefix.Length);

            return message.Trim();
        }

        /// <summary>
        /// 解析記錄檔的一行。格式為「時間<TAB>類型<TAB>內容」。
        /// 無法解析時回傳 null。
        /// </summary>
        public static LogRecord ParseFileLine(string line)
        {
            if (String.IsNullOrEmpty(line))
                return null;

            string[] parts = line.Split(new[] { '\t' }, 3);
            if (parts.Length < 3)
                return null;

            DateTime time;
            if (!DateTime.TryParse(parts[0], System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out time))
                return null;

            return new LogRecord
            {
                Time = time,
                EventId = parts[1] == "錯誤" ? LogRecord.EventIdError : LogRecord.EventIdEvent,
                // 舊版記錄檔的內容可能仍帶有重複的時間前綴，讀取時一併移除
                Message = Log.StripLeadingTimestamp(parts[2]),
            };
        }

        /// <summary>
        /// 由新到舊讀取記錄檔。記錄檔是主要來源：不需要任何特殊權限，
        /// 也不必掃描整個 Windows 事件記錄。
        /// </summary>
        public static List<LogRecord> ReadFile(string path, int maxRecords, out string error)
        {
            error = null;
            var records = new List<LogRecord>();

            if (String.IsNullOrEmpty(path))
                return records;

            try
            {
                var paths = new List<string>();
                paths.Add(path);
                // 一併讀入輪替後的舊檔，較新的在前
                for (int i = 1; i <= 2; ++i)
                {
                    string rotated = path + "." + i;
                    if (File.Exists(rotated))
                        paths.Add(rotated);
                }

                foreach (var current in paths)
                {
                    if (!File.Exists(current))
                        continue;

                    var lines = new List<string>();
                    // 以共用讀取開啟，程式仍在寫入時也能讀
                    using (var stream = new FileStream(current, FileMode.Open, FileAccess.Read,
                            FileShare.ReadWrite | FileShare.Delete))
                    using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                            lines.Add(line);
                    }

                    for (int i = lines.Count - 1; i >= 0 && records.Count < maxRecords; --i)
                    {
                        var record = ParseFileLine(lines[i]);
                        if (record != null)
                            records.Add(record);
                    }

                    if (records.Count >= maxRecords)
                        break;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            return records;
        }

        /// <summary>
        /// 由新到舊讀取 PersistentWindows 的記錄。
        /// </summary>
        /// <param name="productName">產品名稱，用於比對事件來源與訊息前綴。</param>
        /// <param name="maxRecords">最多回傳筆數。</param>
        /// <param name="maxScan">最多往回掃描的事件筆數。</param>
        /// <param name="error">讀取失敗時的錯誤訊息，成功時為 null。</param>
        public static List<LogRecord> Read(string productName, int maxRecords, int maxScan, out string error)
        {
            error = null;
            var records = new List<LogRecord>();

            try
            {
                using (var log = new EventLog(LogName))
                {
                    int total = log.Entries.Count;
                    int scanned = 0;

                    for (int i = total - 1; i >= 0 && scanned < maxScan && records.Count < maxRecords; --i)
                    {
                        ++scanned;

                        EventLogEntry entry;
                        try
                        {
                            entry = log.Entries[i];
                        }
                        catch (Exception)
                        {
                            // 記錄檔在掃描期間可能被輪替或截斷，跳過即可
                            continue;
                        }

                        if (!Matches(entry.Source, entry.InstanceId, entry.Message, productName))
                            continue;

                        records.Add(new LogRecord
                        {
                            Time = entry.TimeGenerated,
                            EventId = (int)(entry.InstanceId & 0xFFFF),
                            Message = Log.StripLeadingTimestamp(StripPrefix(entry.Message, productName)),
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Log.Error(ex);
            }

            return records;
        }

        /// <summary>
        /// 依關鍵字篩選記錄，空字串表示不篩選。比對不分大小寫。
        /// </summary>
        public static List<LogRecord> Filter(List<LogRecord> records, string keyword)
        {
            if (records == null)
                return new List<LogRecord>();

            if (String.IsNullOrEmpty(keyword) || keyword.Trim().Length == 0)
                return new List<LogRecord>(records);

            string needle = keyword.Trim();
            var result = new List<LogRecord>();
            foreach (var record in records)
            {
                if (record.Message != null
                    && record.Message.IndexOf(needle, StringComparison.CurrentCultureIgnoreCase) >= 0)
                {
                    result.Add(record);
                    continue;
                }

                if (record.KindText.IndexOf(needle, StringComparison.CurrentCultureIgnoreCase) >= 0
                    || record.TimeText.IndexOf(needle, StringComparison.Ordinal) >= 0)
                {
                    result.Add(record);
                }
            }

            return result;
        }

        /// <summary>
        /// 將記錄組成可貼上或存檔的純文字。
        /// </summary>
        public static string ToPlainText(List<LogRecord> records)
        {
            if (records == null || records.Count == 0)
                return String.Empty;

            var builder = new System.Text.StringBuilder();
            builder.AppendLine("時間\t類型\t內容");
            foreach (var record in records)
                builder.AppendLine(record.ToString());

            return builder.ToString();
        }
    }
}
