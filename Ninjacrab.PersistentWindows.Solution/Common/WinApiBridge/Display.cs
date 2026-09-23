using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PersistentWindows.Common.WinApiBridge
{
    public class Display
    {
        public const uint MONITORINFOF_PRIMARY = 0x00000001;

        public RECT Position;
        public uint Flags { get; internal set; }

        /// <summary>寫入資料庫鍵值用的名稱，固定為 "Display"，不可變更。</summary>
        public String DeviceName { get; internal set; }

        /// <summary>作業系統指派的顯示器裝置名稱，例如 \.\DISPLAY1，僅供介面顯示。</summary>
        public String SystemDeviceName { get; internal set; }

        /// <summary>是否為主要顯示器。</summary>
        public bool IsPrimary { get; internal set; }

        public static List<Display> GetDisplays()
        {
            List<Display> displays = new List<Display>();

            User32.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                delegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
                {
                    MonitorInfo monitorInfo = new MonitorInfo();
                    monitorInfo.StructureSize = Marshal.SizeOf(monitorInfo);
                    bool success = User32.GetMonitorInfo(hMonitor, ref monitorInfo);
                    if (success)
                    {
                        Display display = new Display();
                        display.Position = monitorInfo.Monitor;
                        display.Flags = monitorInfo.Flags;

                        // 注意：DeviceName 會被寫進資料庫鍵值，一旦改變就無法比對既有的快照，
                        // 因此這裡固定為 "Display"。需要真實裝置名稱請改用 GetDisplaysDetailed()。
                        //int pos = monitorInfo.DeviceName.LastIndexOf("\\") + 1;
                        //display.DeviceName = monitorInfo.DeviceName.Substring(pos, monitorInfo.DeviceName.Length - pos);
                        display.SystemDeviceName = monitorInfo.DeviceName;
                        display.IsPrimary = (monitorInfo.Flags & MONITORINFOF_PRIMARY) != 0;
                        display.DeviceName = "Display";

                        displays.Add(display);
                    }
                    return true;
                }, IntPtr.Zero);
            return displays;
        }

        /// <summary>
        /// 以左至右、上至下的順序取得目前所有顯示器，並保留作業系統的裝置名稱。
        /// 僅供介面顯示，不影響資料庫鍵值。
        /// </summary>
        public static List<Display> GetDisplaysDetailed()
        {
            var displays = GetDisplays();
            displays.Sort(delegate (Display a, Display b)
            {
                if (a.Position.Left != b.Position.Left)
                    return a.Position.Left.CompareTo(b.Position.Left);
                return a.Position.Top.CompareTo(b.Position.Top);
            });
            return displays;
        }
    }
}
