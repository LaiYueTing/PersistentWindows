using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using PersistentWindows.Common.Models;

namespace PersistentWindows.Common.Diagnostics
{
    /// <summary>
    /// 讀取 PersistentWindows 的純文字記錄檔。
    ///
    /// 記錄檔是唯一來源：不需要任何特殊權限，讀取也幾乎即時。
    /// </summary>
    public static class LogReader
    {
        /// <summary>預設最多回傳的筆數。</summary>
        public const int DefaultMaxRecords = 1000;

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
                EventId = ParseKind(parts[1]),
                // 舊版記錄檔的內容可能仍帶有重複的時間前綴，讀取時一併移除
                Message = Log.StripLeadingTimestamp(parts[2]),
            };
        }

        /// <summary>
        /// 將記錄檔的類型欄位轉回事件識別碼。未知的類型一律當作一般事件。
        /// </summary>
        public static int ParseKind(string kind)
        {
            if (kind == "錯誤")
                return LogRecord.EventIdError;

            if (kind == "資訊")
                return LogRecord.EventIdInfo;

            return LogRecord.EventIdEvent;
        }

        /// <summary>
        /// 由新到舊讀取記錄檔，一併讀入輪替後的舊檔。
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
