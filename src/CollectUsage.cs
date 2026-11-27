using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using Microsoft.Win32;

namespace OneCLicenseInspector
{
    internal static class CollectUsage
    {
        // ------- перечисление TCP/UDP с PID -------

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int pdwSize, bool bOrder, int ulAf, int tableClass, uint reserved);

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedUdpTable(IntPtr pUdpTable, ref int pdwSize, bool bOrder, int ulAf, int tableClass, uint reserved);

        private const int AF_INET = 2;
        private const int TCP_TABLE_OWNER_PID_ALL = 5;
        private const int UDP_TABLE_OWNER_PID = 1;
        private const int MIB_TCP_STATE_LISTEN = 2;

        public static void Run(ScanResult r)
        {
            CollectProcesses(r);
            CollectServices(r);
            CollectPorts(r);

            int oneCProcs = 0;
            foreach (ProcessInfo p in r.Processes) if (!p.IsHasp) oneCProcs++;
            r.AddLog(LogLevel.Info, "Использование", "Процессов 1С: " + oneCProcs + ", служб: " + r.Services.Count + ", портов: " + r.Ports.Count);
        }

        private static void CollectProcesses(ScanResult r)
        {
            try
            {
                foreach (Process p in Process.GetProcesses())
                {
                    string n = "";
                    try { n = p.ProcessName; } catch { }
                    if (!IsInterestingProcess(n)) continue;

                    ProcessInfo info = new ProcessInfo();
                    info.Name = n;
                    info.Pid = p.Id;
                    try { info.WorkingSet = p.WorkingSet64; } catch { }
                    try { info.Started = Fmt.Date(p.StartTime); } catch { }
                    try
                    {
                        string path = p.MainModule.FileName;
                        info.Path = path;
                    }
                    catch
                    {
                        try { info.Path = p.MainModule.ModuleName; } catch { info.Path = ""; }
                    }
                    info.IsHasp = IsHaspName(n);
                    r.Processes.Add(info);
                }
            }
            catch (Exception ex)
            {
                r.AddLog(LogLevel.Warn, "Использование", "Не удалось получить список процессов: " + ex.Message);
            }

            r.Processes.Sort(delegate (ProcessInfo a, ProcessInfo b)
            {
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
        }

        private static bool IsInterestingProcess(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.ToLowerInvariant();
            if (n.StartsWith("1cv")) return true;
            if (n == "ragent" || n == "rmngr" || n == "rphost" || n == "ras" || n == "ibcmd") return true;
            if (n == "nhsrvice" || n == "hasplms" || n == "hardlock" || n == "haspds") return true;
            if (n.StartsWith("hasp")) return true;
            return false;
        }

        private static bool IsHaspName(string name)
        {
            string n = (name ?? "").ToLowerInvariant();
            return n.StartsWith("hasp") || n == "nhsrvice" || n == "hardlock" || n == "aks";
        }

        private static void CollectServices(ScanResult r)
        {
            try
            {
                foreach (ServiceController sc in ServiceController.GetServices())
                {
                    string name = sc.ServiceName ?? "";
                    string display = sc.DisplayName ?? "";
                    if (!IsInterestingService(name, display)) continue;

                    ServiceInfo s = new ServiceInfo();
                    s.Name = name;
                    s.DisplayName = display;
                    try { s.State = StateText(sc.Status); } catch { s.State = "?"; }
                    s.StartMode = StartModeText(name);
                    s.Path = ReadServicePath(name);

                    string low = (name + " " + display).ToLowerInvariant();
                    s.IsOneC = low.IndexOf("1c", StringComparison.Ordinal) >= 0 || low.IndexOf("1с", StringComparison.Ordinal) >= 0;
                    s.IsHasp = low.IndexOf("hasp", StringComparison.Ordinal) >= 0 ||
                               low.IndexOf("sentinel", StringComparison.Ordinal) >= 0 ||
                               low.IndexOf("aladdin", StringComparison.Ordinal) >= 0 ||
                               low.IndexOf("hardlock", StringComparison.Ordinal) >= 0;
                    r.Services.Add(s);
                }
            }
            catch (Exception ex)
            {
                r.AddLog(LogLevel.Warn, "Использование", "Не удалось перечислить службы: " + ex.Message);
            }

            r.Services.Sort(delegate (ServiceInfo a, ServiceInfo b)
            {
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
        }

        private static bool IsInterestingService(string name, string display)
        {
            string low = (name + " " + display).ToLowerInvariant();
            if (low.IndexOf("hasp", StringComparison.Ordinal) >= 0) return true;
            if (low.IndexOf("sentinel", StringComparison.Ordinal) >= 0) return true;
            if (low.IndexOf("aladdin", StringComparison.Ordinal) >= 0) return true;
            if (low.IndexOf("hardlock", StringComparison.Ordinal) >= 0) return true;
            if (low.IndexOf("1cv8", StringComparison.Ordinal) >= 0) return true;
            if (low.IndexOf("1c:", StringComparison.Ordinal) >= 0) return true;
            if (low.IndexOf("1с:", StringComparison.Ordinal) >= 0) return true;
            if (low.IndexOf("1c enterprise", StringComparison.Ordinal) >= 0) return true;
            if (low.IndexOf("postgres", StringComparison.Ordinal) >= 0) return true;
            if (low.IndexOf("sql server", StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        private static string StartModeText(string serviceName)
        {
            try
            {
                using (RegistryKey k = Reg.Open(@"SYSTEM\CurrentControlSet\Services\" + serviceName, false))
                {
                    int start = Reg.Dword(k, "Start", -1);
                    switch (start)
                    {
                        case 0: return "загрузка";
                        case 1: return "система";
                        case 2: return "авто";
                        case 3: return "вручную";
                        case 4: return "отключена";
                        default: return "";
                    }
                }
            }
            catch { return ""; }
        }

        private static string StateText(ServiceControllerStatus st)
        {
            switch (st)
            {
                case ServiceControllerStatus.Running: return "работает";
                case ServiceControllerStatus.Stopped: return "остановлена";
                case ServiceControllerStatus.Paused: return "пауза";
                case ServiceControllerStatus.StartPending: return "запускается";
                case ServiceControllerStatus.StopPending: return "останавливается";
                case ServiceControllerStatus.PausePending: return "пауза…";
                case ServiceControllerStatus.ContinuePending: return "продолжение…";
                default: return st.ToString();
            }
        }

        private static string ReadServicePath(string name)
        {
            try
            {
                using (RegistryKey k = Reg.Open(@"SYSTEM\CurrentControlSet\Services\" + name, false))
                {
                    return Reg.Str(k, "ImagePath");
                }
            }
            catch { return ""; }
        }

        private static void CollectPorts(ScanResult r)
        {
            Dictionary<int, string> names = new Dictionary<int, string>();
            try
            {
                foreach (Process p in Process.GetProcesses())
                {
                    try
                    {
                        if (!names.ContainsKey(p.Id)) names[p.Id] = p.ProcessName;
                    }
                    catch { }
                }
            }
            catch { }

            HashSet<string> added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                int size = 0;
                GetExtendedTcpTable(IntPtr.Zero, ref size, true, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
                IntPtr buf = Marshal.AllocHGlobal(size);
                try
                {
                    if (GetExtendedTcpTable(buf, ref size, true, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0) == 0)
                    {
                        int count = Marshal.ReadInt32(buf);
                        IntPtr row = new IntPtr(buf.ToInt64() + 4);
                        int rowSize = Marshal.SizeOf(typeof(MIB_TCPROW_OWNER_PID));
                        for (int i = 0; i < count; i++)
                        {
                            MIB_TCPROW_OWNER_PID e = (MIB_TCPROW_OWNER_PID)Marshal.PtrToStructure(row, typeof(MIB_TCPROW_OWNER_PID));
                            row = new IntPtr(row.ToInt64() + rowSize);
                            if (e.dwState != MIB_TCP_STATE_LISTEN) continue;
                            int port = Ntohs(e.dwLocalPort);
                            PortInfo pi = MakePort(port, "TCP", e.dwOwningPid, names);
                            string key = "T" + port.ToString(CultureInfo.InvariantCulture) + "-" + e.dwOwningPid;
                            if (added.Add(key)) r.Ports.Add(pi);
                        }
                    }
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
            catch { }

            try
            {
                int size = 0;
                GetExtendedUdpTable(IntPtr.Zero, ref size, true, AF_INET, UDP_TABLE_OWNER_PID, 0);
                IntPtr buf = Marshal.AllocHGlobal(size);
                try
                {
                    if (GetExtendedUdpTable(buf, ref size, true, AF_INET, UDP_TABLE_OWNER_PID, 0) == 0)
                    {
                        int count = Marshal.ReadInt32(buf);
                        IntPtr row = new IntPtr(buf.ToInt64() + 4);
                        int rowSize = Marshal.SizeOf(typeof(MIB_UDPROW_OWNER_PID));
                        for (int i = 0; i < count; i++)
                        {
                            MIB_UDPROW_OWNER_PID e = (MIB_UDPROW_OWNER_PID)Marshal.PtrToStructure(row, typeof(MIB_UDPROW_OWNER_PID));
                            row = new IntPtr(row.ToInt64() + rowSize);
                            int port = Ntohs(e.dwLocalPort);
                            if (port <= 0) continue;
                            PortInfo pi = MakePort(port, "UDP", e.dwOwningPid, names);
                            string key = "U" + port.ToString(CultureInfo.InvariantCulture) + "-" + e.dwOwningPid;
                            if (added.Add(key)) r.Ports.Add(pi);
                        }
                    }
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
            catch { }

            r.Ports.Sort(delegate (PortInfo a, PortInfo b)
            {
                int ra = RoleRank(a.Role), rb = RoleRank(b.Role);
                if (ra != rb) return ra.CompareTo(rb);
                return a.Port.CompareTo(b.Port);
            });
        }

        private static int RoleRank(string role)
        {
            if (role.StartsWith("важн", StringComparison.Ordinal)) return 0;
            if (role.Length > 0) return 1;
            return 2;
        }

        private static PortInfo MakePort(int port, string proto, int pid, Dictionary<int, string> names)
        {
            PortInfo p = new PortInfo();
            p.Port = port;
            p.Protocol = proto;
            p.Pid = pid;
            string owner;
            if (names.TryGetValue(pid, out owner)) p.Owner = owner;
            p.Role = RoleFor(port, proto);
            return p;
        }

        private static string RoleFor(int port, string proto)
        {
            if (port == 475) return "важно: HASP4 Net (nhsrvice)";
            if (port == 1947) return "важно: Sentinel LDK (hasplms)";
            if (port >= 1540 && port <= 1541) return "кластер 1С (ragent/rmngr)";
            if (port >= 1545 && port <= 1591) return "кластер 1С (rmngr/rphost)";
            if (port == 1433) return "MS SQL Server";
            if (port == 5432) return "PostgreSQL";
            return "";
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_TCPROW_OWNER_PID
        {
            public int dwState;
            public int dwLocalAddr;
            public int dwLocalPort;
            public int dwRemoteAddr;
            public int dwRemotePort;
            public int dwOwningPid;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_UDPROW_OWNER_PID
        {
            public int dwLocalAddr;
            public int dwLocalPort;
            public int dwOwningPid;
        }

        private static int Ntohs(int value)
        {
            int p = value & 0xFFFF;
            return ((p & 0xFF) << 8) | ((p >> 8) & 0xFF);
        }
    }
}
