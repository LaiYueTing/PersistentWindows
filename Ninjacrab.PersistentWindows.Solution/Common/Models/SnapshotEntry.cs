using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PersistentWindows.Common.Models
{
    /// <summary>
    /// 快照的來源類型。
    /// </summary>
    public enum SnapshotSource
    {
        /// <summary>儲存於硬碟資料庫的具名快照。</summary>
        Disk = 0,

        /// <summary>僅存在於記憶體中的快照 (以 0-9、a-z 命名)。</summary>
        Memory = 1,
    }

    /// <summary>
    /// 快照管理介面中「快照清單」的一列資料。
    /// </summary>
    public class SnapshotEntry
    {
        /// <summary>快照來源：硬碟或記憶體。</summary>
        public SnapshotSource Source { get; set; }

        /// <summary>顯示於清單的快照名稱。</summary>
        public string Name { get; set; }

        /// <summary>硬碟快照於資料庫中的完整鍵值 (顯示器設定字串加上使用者命名)。</summary>
        public string DbKey { get; set; }

        /// <summary>記憶體快照所屬的顯示器設定字串。</summary>
        public string DisplayKey { get; set; }

        /// <summary>記憶體快照的編號，硬碟快照為 -1。</summary>
        public int SnapshotId { get; set; }

        /// <summary>可讀的顯示器配置，例如 1920x1080 + 2560x1440。</summary>
        public string DisplayLayout { get; set; }

        /// <summary>快照儲存時間，無法判定時為 DateTime.MinValue。</summary>
        public DateTime SaveTime { get; set; }

        /// <summary>快照中記錄的視窗總數。</summary>
        public int WindowCount { get; set; }

        /// <summary>此快照的顯示器設定是否與目前環境相符。</summary>
        public bool MatchesCurrentDisplay { get; set; }

        public SnapshotEntry()
        {
            Source = SnapshotSource.Disk;
            Name = String.Empty;
            DbKey = String.Empty;
            DisplayKey = String.Empty;
            SnapshotId = -1;
            DisplayLayout = String.Empty;
            SaveTime = DateTime.MinValue;
            WindowCount = 0;
            MatchesCurrentDisplay = false;
        }

        /// <summary>儲存時間的顯示字串。</summary>
        public string SaveTimeText
        {
            get
            {
                if (SaveTime == DateTime.MinValue)
                    return "未知";

                return SaveTime.ToString("yyyy-MM-dd HH:mm:ss");
            }
        }

        /// <summary>來源的顯示字串。</summary>
        public string SourceText
        {
            get
            {
                return Source == SnapshotSource.Disk ? "硬碟" : "記憶體";
            }
        }

        /// <summary>
        /// 清單欄位用的精簡文字，符合目前環境時會加上標記。
        /// 完整的排列順序與座標改在下方的詳情列顯示，避免每一列都太長。
        /// </summary>
        public string DisplayLayoutText
        {
            get
            {
                string compact = DisplayKeyParser.ToCompactLayout(DisplayKey);
                return MatchesCurrentDisplay ? compact + "　← 目前" : compact;
            }
        }
    }

    /// <summary>
    /// 快照管理介面中「快照內部詳情」的一列資料。
    /// </summary>
    public class SnapshotWindowInfo
    {
        /// <summary>行程名稱，例如 chrome.exe。</summary>
        public string ProcessName { get; set; }

        /// <summary>視窗標題。</summary>
        public string Title { get; set; }

        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        /// <summary>視窗狀態：最大化 / 最小化 / 一般。</summary>
        public string State { get; set; }

        /// <summary>視窗所在的顯示器，例如 #2；無法判定時為「螢幕外」。</summary>
        public string MonitorText { get; set; }

        /// <summary>快照中的原始記錄，供單一視窗的還原與移除使用。</summary>
        public ApplicationDisplayMetrics Record { get; set; }

        /// <summary>視窗類別名稱，用於比對執行中的視窗。</summary>
        public string ClassName { get; set; }

        /// <summary>執行中視窗的代碼；只有「加入視窗」清單會填入。</summary>
        public IntPtr WindowHandle { get; set; }

        /// <summary>這個執行中的視窗是否已在目標快照中；只有「加入視窗」清單會填入。</summary>
        public bool AlreadyInSnapshot { get; set; }

        public SnapshotWindowInfo()
        {
            ProcessName = String.Empty;
            Title = String.Empty;
            State = String.Empty;
            MonitorText = String.Empty;
            ClassName = String.Empty;
        }
    }

    /// <summary>
    /// 從資料庫鍵值解析出來的單一顯示器資訊。
    /// </summary>
    public class MonitorInfoText
    {
        /// <summary>由左至右的排列序號，從 1 開始。</summary>
        public int Index { get; set; }

        public int Left { get; set; }
        public int Top { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        /// <summary>桌面座標原點 (0,0) 的顯示器即為主要顯示器。</summary>
        public bool IsPrimary
        {
            get { return Left == 0 && Top == 0; }
        }

        /// <summary>此顯示器與指定矩形的重疊面積。</summary>
        public long IntersectionArea(int left, int top, int width, int height)
        {
            long overlapWidth = Math.Min((long)Left + Width, (long)left + width) - Math.Max(Left, left);
            long overlapHeight = Math.Min((long)Top + Height, (long)top + height) - Math.Max(Top, top);
            if (overlapWidth <= 0 || overlapHeight <= 0)
                return 0;

            return overlapWidth * overlapHeight;
        }

        public override string ToString()
        {
            return String.Format("#{0} {1}x{2} @({3},{4}){5}",
                Index, Width, Height, Left, Top, IsPrimary ? " 主顯示器" : String.Empty);
        }
    }

    /// <summary>
    /// 顯示器設定字串 (資料庫鍵值) 的解析工具。
    ///
    /// DesktopDisplayMetrics 產生的鍵值格式為
    /// Display_Loc{Left}x{Top}_Res{Width}x{Height}，多個顯示器以 __ 串接，
    /// 負號一律以 M 取代。硬碟快照的鍵值會在其後直接接上使用者輸入的名稱。
    /// </summary>
    public static class DisplayKeyParser
    {
        private const string SegmentPattern = @"[^_]+_LocM?\d+xM?\d+_ResM?\d+xM?\d+";

        private static readonly Regex PrefixRegex = new Regex(
            "^(?<prefix>" + SegmentPattern + "(?:__" + SegmentPattern + ")*)(?<name>.*)$",
            RegexOptions.Compiled);

        private static readonly Regex MonitorRegex = new Regex(
            @"_Loc(?<left>M?\d+)x(?<top>M?\d+)_Res(?<width>M?\d+)x(?<height>M?\d+)",
            RegexOptions.Compiled);

        /// <summary>
        /// 將資料庫鍵值拆成「顯示器設定字串」與「使用者命名」兩部分。
        /// </summary>
        public static void Split(string dbKey, out string displayKey, out string name)
        {
            displayKey = dbKey ?? String.Empty;
            name = String.Empty;

            if (String.IsNullOrEmpty(dbKey))
                return;

            var match = PrefixRegex.Match(dbKey);
            if (!match.Success)
                return;

            displayKey = match.Groups["prefix"].Value;
            name = match.Groups["name"].Value;
        }

        /// <summary>
        /// 解析顯示器設定字串中每一台顯示器的解析度與桌面座標。
        /// 鍵值本身是以左至右、上至下排序後組出的，因此清單順序即為顯示器由左至右的排列順序。
        /// </summary>
        public static List<MonitorInfoText> ParseMonitors(string displayKey)
        {
            var monitors = new List<MonitorInfoText>();
            if (String.IsNullOrEmpty(displayKey))
                return monitors;

            int index = 1;
            foreach (Match match in MonitorRegex.Matches(displayKey))
            {
                var monitor = new MonitorInfoText();
                monitor.Index = index++;
                monitor.Left = ParseCoordinate(match.Groups["left"].Value);
                monitor.Top = ParseCoordinate(match.Groups["top"].Value);
                monitor.Width = ParseCoordinate(match.Groups["width"].Value);
                monitor.Height = ParseCoordinate(match.Groups["height"].Value);
                monitors.Add(monitor);
            }

            return monitors;
        }

        /// <summary>
        /// 判斷一個視窗矩形落在哪一台顯示器上。
        /// 與 Windows 的 MonitorFromRect 一致，取重疊面積最大的那一台。
        /// </summary>
        public static string ResolveMonitor(List<MonitorInfoText> monitors, int x, int y, int width, int height)
        {
            if (monitors == null || monitors.Count == 0)
                return String.Empty;

            MonitorInfoText best = null;
            long bestArea = 0;
            foreach (var monitor in monitors)
            {
                long area = monitor.IntersectionArea(x, y, width, height);
                if (area > bestArea)
                {
                    bestArea = area;
                    best = monitor;
                }
            }

            if (best == null)
                return "螢幕外";

            return "#" + best.Index;
        }

        /// <summary>
        /// 將顯示器設定字串轉成可讀的描述，包含排列順序、解析度、桌面座標與主要顯示器標記。
        /// 例如：#1 2560x1440 @(-2560,0)　#2 1920x1080 @(0,0) 主
        /// </summary>
        public static string ToDisplayLayout(string displayKey)
        {
            var monitors = ParseMonitors(displayKey);
            if (monitors.Count == 0)
                return "未知";

            var parts = new List<string>();
            foreach (var monitor in monitors)
                parts.Add(monitor.ToString());

            return String.Join("   ", parts.ToArray());
        }

        /// <summary>
        /// 只回傳解析度的簡短描述，例如 1920x1080 + 2560x1440。
        /// </summary>
        public static string ToResolutionSummary(string displayKey)
        {
            var monitors = ParseMonitors(displayKey);
            if (monitors.Count == 0)
                return "未知";

            var parts = new List<string>();
            foreach (var monitor in monitors)
                parts.Add(monitor.Width + "x" + monitor.Height);

            return String.Join(" + ", parts.ToArray());
        }

        /// <summary>
        /// 清單欄位用的精簡描述，相同解析度會合併計數。
        /// 例如 3 台　1920x1080 ×3，或 2 台　2560x1440 + 1920x1080。
        /// 完整的排列順序與桌面座標請用 ToDisplayLayout()。
        /// </summary>
        public static string ToCompactLayout(string displayKey)
        {
            var monitors = ParseMonitors(displayKey);
            if (monitors.Count == 0)
                return "未知";

            var order = new List<string>();
            var counts = new Dictionary<string, int>();
            foreach (var monitor in monitors)
            {
                string resolution = monitor.Width + "x" + monitor.Height;
                if (!counts.ContainsKey(resolution))
                {
                    counts[resolution] = 0;
                    order.Add(resolution);
                }
                ++counts[resolution];
            }

            var parts = new List<string>();
            foreach (var resolution in order)
            {
                int count = counts[resolution];
                parts.Add(count > 1 ? resolution + " ×" + count : resolution);
            }

            return String.Format("{0} 台　{1}", monitors.Count, String.Join(" + ", parts.ToArray()));
        }

        private static int ParseCoordinate(string value)
        {
            if (String.IsNullOrEmpty(value))
                return 0;

            // 鍵值中的負號一律以 M 取代
            bool negative = value.StartsWith("M");
            int result;
            if (!Int32.TryParse(negative ? value.Substring(1) : value, out result))
                return 0;

            return negative ? -result : result;
        }
    }
}
