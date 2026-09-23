using System.Runtime.InteropServices;

namespace PersistentWindows.Common.WinApiBridge
{
    // 搭配 GetMonitorInfoW 使用，DeviceName 必須以寬字元配置
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MonitorInfo
    {
        // size of a device name string
        private const int CCHDEVICENAME = 32;

        public int StructureSize;
        public RECT Monitor;
        public RECT WorkArea;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
        public string DeviceName;
    }
}
