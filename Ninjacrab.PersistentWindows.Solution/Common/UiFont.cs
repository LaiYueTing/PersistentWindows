using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;

namespace PersistentWindows.Common
{
    /// <summary>
    /// 統一解析介面字型。
    ///
    /// 原本各對話框寫死 Microsoft Sans Serif，該字型不含中文字符，
    /// 中文得靠系統字型替代機制顯示，字重與字距都會和周圍的英數字不一致。
    /// 這裡改為在執行期挑選第一個實際安裝的繁體中文介面字型。
    ///
    /// 若想改用其他字型，只要調整 Candidates 的順序即可，
    /// 例如把 "Microsoft JhengHei UI" 移到第一個改用微軟正黑體。
    /// </summary>
    public static class UiFont
    {
        /// <summary>依偏好順序排列的候選字型，最後一項為保底值。</summary>
        public static readonly string[] Candidates =
        {
            "Noto Sans TC",             // 思源黑體繁體中文版
            "Microsoft JhengHei UI",    // 微軟正黑體 UI，Windows 8 以後的繁體中文介面字型
            "Microsoft JhengHei",       // 微軟正黑體，Windows 7 亦有
            "Segoe UI",
            "Microsoft Sans Serif",
        };

        private static string familyName;
        private static readonly object resolveLock = new object();

        /// <summary>實際採用的字型家族名稱。</summary>
        public static string FamilyName
        {
            get
            {
                if (familyName != null)
                    return familyName;

                lock (resolveLock)
                {
                    if (familyName == null)
                        familyName = Resolve();
                }

                return familyName;
            }
        }

        private static string Resolve()
        {
            var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var collection = new InstalledFontCollection())
                {
                    foreach (var family in collection.Families)
                        installed.Add(family.Name);
                }
            }
            catch (Exception)
            {
                return "Microsoft Sans Serif";
            }

            foreach (var candidate in Candidates)
            {
                if (installed.Contains(candidate))
                    return candidate;
            }

            return "Microsoft Sans Serif";
        }

        /// <summary>取得指定大小的介面字型。</summary>
        public static Font Get(float pointSize)
        {
            return Get(pointSize, FontStyle.Regular);
        }

        /// <summary>取得指定大小與樣式的介面字型。</summary>
        public static Font Get(float pointSize, FontStyle style)
        {
            try
            {
                return new Font(FamilyName, pointSize, style, GraphicsUnit.Point);
            }
            catch (Exception)
            {
                return new Font("Microsoft Sans Serif", pointSize, style, GraphicsUnit.Point);
            }
        }
    }
}
