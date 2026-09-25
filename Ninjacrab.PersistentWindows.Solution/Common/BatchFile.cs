using System;
using System.IO;
using System.Text;

using PersistentWindows.Common.Diagnostics;
using PersistentWindows.Common.WinApiBridge;

namespace PersistentWindows.Common
{
    /// <summary>
    /// 批次檔 (.bat) 寫檔輔助類別。
    ///
    /// cmd.exe 是以主控台的 OEM 字碼頁 (繁體中文 Windows 為 950 / Big5) 逐行解讀批次檔，
    /// 而 .NET 的 File.WriteAllText 預設寫出「不帶 BOM 的 UTF-8」，
    /// 因此只要安裝路徑或資料夾名稱含有中文字，批次檔中的路徑就會變成亂碼而執行失敗。
    /// 這裡統一以 OEM 字碼頁寫出，並改用寬字元 API 取得的路徑，
    /// 徹底避開 Big5 衝碼字元 (如「許」、「功」、「蓋」等第二位元組為 0x5C 的字元) 造成的路徑截斷。
    /// </summary>
    public static class BatchFile
    {
        private static Encoding oemEncoding;

        /// <summary>
        /// 取得 cmd.exe 解讀批次檔所使用的 OEM 字碼頁編碼。
        /// </summary>
        public static Encoding OemEncoding
        {
            get
            {
                if (oemEncoding != null)
                    return oemEncoding;

                try
                {
                    int codepage = Kernel32.GetOEMCP();
                    oemEncoding = Encoding.GetEncoding(codepage);
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                    oemEncoding = Encoding.Default;
                }

                return oemEncoding;
            }
        }

        /// <summary>
        /// 以 cmd.exe 可正確解讀的編碼寫出批次檔內容。
        /// </summary>
        /// <param name="path">批次檔完整路徑。</param>
        /// <param name="content">批次檔內容。</param>
        public static void Write(string path, string content)
        {
            File.WriteAllText(path, content, OemEncoding);
        }
    }
}
