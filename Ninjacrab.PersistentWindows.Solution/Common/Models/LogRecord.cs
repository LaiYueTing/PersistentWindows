using System;

namespace PersistentWindows.Common.Models
{
    /// <summary>
    /// 從記錄檔讀回的一筆 PersistentWindows 記錄。
    /// </summary>
    public class LogRecord
    {
        /// <summary>事件識別碼：9980 為資訊，9990 為一般事件，9999 為錯誤。</summary>
        public const int EventIdInfo = 9980;
        public const int EventIdEvent = 9990;
        public const int EventIdError = 9999;

        public DateTime Time { get; set; }
        public int EventId { get; set; }
        public string Message { get; set; }

        public LogRecord()
        {
            Time = DateTime.MinValue;
            Message = String.Empty;
        }

        /// <summary>類型的顯示字串。</summary>
        public string KindText
        {
            get
            {
                if (EventId == EventIdError)
                    return "錯誤";

                if (EventId == EventIdInfo)
                    return "資訊";

                return "事件";
            }
        }

        public string TimeText
        {
            get { return Time.ToString("yyyy-MM-dd HH:mm:ss"); }
        }

        /// <summary>供複製與匯出使用的單行文字。</summary>
        public override string ToString()
        {
            return String.Format("{0}\t{1}\t{2}", TimeText, KindText, Message);
        }
    }
}
