using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace OneCLicenseInspector
{
    internal static class CollectSystem
    {
        [StructLayout(LayoutKind.Sequential)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;

            public MEMORYSTATUSEX()
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        public static void Run(ScanResult r)
        {
            SystemFacts s = r.System;
            s.ComputerName = Environment.MachineName;
            s.UserName = Environment.UserName;
            s.Domain = Environment.UserDomainName;
            s.OsCaption = Os.OsCaption();
            s.OsVersion = Environment.OSVersion.Version.ToString();
            s.OsBuild = Os.OsBuildText();
            s.OsArch = Os.ArchName();
            s.DotNet = Os.DotNetText();
            s.Locale = CultureInfo.CurrentCulture.DisplayName;
            s.IsAdmin = Os.IsAdmin();
            s.TimeZone = TimeZone.CurrentTimeZone.StandardName;
            s.CpuCount = Environment.ProcessorCount;

            try
            {
                MEMORYSTATUSEX m = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(m))
                {
                    s.RamBytes = (long)m.ullTotalPhys;
                }
            }
            catch { }

            using (RegistryKey k = Reg.Open(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", false))
            {
                long install = 0;
                string raw = Reg.Str(k, "InstallDate");
                if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out install) && install > 0)
                {
                    try
                    {
                        s.OsInstallDate = Fmt.Date(DateTime.FromFileTimeUtc(install * 10000000L).ToLocalTime());
                    }
                    catch { }
                }
            }

            using (RegistryKey k = Reg.Open(@"HARDWARE\DESCRIPTION\System\BIOS", false))
            {
                s.Manufacturer = Reg.Str(k, "SystemManufacturer");
                s.Model = Reg.Str(k, "SystemProductName");
            }

            using (RegistryKey k = Reg.Open(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", false))
            {
                s.Processor = Reg.Str(k, "ProcessorNameString").Trim();
            }

            r.AddLog(LogLevel.Info, "Система", s.OsCaption + " " + s.OsArch + ", ПК " + s.ComputerName);
            if (!s.IsAdmin)
            {
                r.AddWarning(LogLevel.Warn, "Ограниченные права",
                    "Программа запущена без прав администратора. Часть данных (службы, HASP, лицензии в защищённых каталогах) может быть недоступна.");
            }
        }
    }
}
