using System;
using System.Collections.Generic;

namespace OneCLicenseInspector
{
    public enum LogLevel { Info, Warn, Error }

    public class LogEntry
    {
        public DateTime Time;
        public LogLevel Level;
        public string Source;
        public string Text;

        public LogEntry(LogLevel level, string source, string text)
        {
            Time = DateTime.Now;
            Level = level;
            Source = source;
            Text = text;
        }
    }

    public class SystemFacts
    {
        public string ComputerName = "";
        public string UserName = "";
        public string Domain = "";
        public string OsCaption = "";
        public string OsVersion = "";
        public string OsBuild = "";
        public string OsArch = "";
        public string OsInstallDate = "";
        public string Manufacturer = "";
        public string Model = "";
        public string Processor = "";
        public int CpuCount;
        public long RamBytes;
        public string TimeZone = "";
        public bool IsAdmin;
        public string DotNet = "";
        public string Locale = "";
    }

    public class PlatformInfo
    {
        public string Family = "";          // 1С:Предприятие 8 / 1С:Предприятие 7.7
        public string Version = "";
        public string Bitness = "";
        public string Path = "";
        public string BinPath = "";
        public string Components = "";
        public string Delivery = "";        // состав поставки
        public string InstallDate = "";
        public string Source = "";
        public string Comment = "";
        public bool HasConfigurator;
        public bool HasThinClient;
        public bool HasThickClient;
        public bool HasServer;
        public bool HasCluster;
    }

    public class LicenseFileInfo
    {
        public string FileName = "";
        public string Directory = "";
        public string FullPath = "";
        public long Size;
        public DateTime Modified;
        public string Sha1 = "";
        public string Activated = "";
        public string Note = "";
        public string Format = "";
        public string Preview = "";
        public string Pin = "";
        public string Product = "";
        public string LicenseType = "";
        public string Users = "";
        public int UsersNum;
        /// <summary>Сколько сеансов держат эту лицензию сейчас (-1 — кластер не опрошен).</summary>
        public int UsedBySessions = -1;
        public string Expires = "";
        public string BoundTo = "";
    }

    public class SentinelFeature
    {
        public string KeyId = "";
        public string FeatureId = "";
        public string Name = "";
        public string ProductName = "";
        public string ProductId = "";
        public string Type = "";
        public string Location = "";
        public string Logins = "";
        public string Limit = "";
        public string Sessions = "";
        public string ExpireText = "";
        public string Disabled = "";
        public string Unusable = "";
        public string Restriction = "";
        public int LoginsNum;
        public int LimitNum;
        public bool IsUnlimited;
        public int FreeSeats { get { if (IsUnlimited) return -2; return Math.Max(0, LimitNum - LoginsNum); } }
    }

    public class SentinelKey
    {
        public string Id = "";
        public string Name = "";
        public string TypeName = "";
        public string VendorName = "";
        public string VendorId = "";
        public string Configuration = "";
        public string Location = "";
        public string Firmware = "";
        public string HwVersion = "";
        public string Sessions = "";
        public string Expiry = "";
        public string Disabled = "";
        public string HostName = "";
        public string OsName = "";
        public string Comment = "";
        public List<SentinelFeature> Features = new List<SentinelFeature>();
    }

    public class SentinelSession
    {
        public string KeyId = "";
        public string Feature = "";
        public string User = "";
        public string Machine = "";
        public string ProcessId = "";
        public string LoginTime = "";
        public string Remaining = "";
    }

    public class SentinelInfo
    {
        public bool Available;
        public string Endpoint = "";
        public string Status = "";
        public string Version = "";
        public string ServerName = "";
        public string ServerOs = "";
        public string Addresses = "";
        public string Uptime = "";
        public string LoginCount = "";
        public string SessionCount = "";
        public string DriverSummary = "";
        public bool ServiceInstalled;
        public bool ServiceRunning;
        public string ServiceState = "";
        public List<SentinelKey> Keys = new List<SentinelKey>();
        public List<SentinelFeature> Features = new List<SentinelFeature>();
        public List<SentinelSession> Sessions = new List<SentinelSession>();
        public string Error = "";
    }

    public class Hasp4Info
    {
        public bool ServiceInstalled;
        public bool ServiceRunning;
        public string ServiceName = "";
        public string ServicePath = "";
        public string Version = "";
        public string InstallDate = "";
        public string Port = "475";
        public bool PortListening;
        public string IniSummary = "";
        public string LogPath = "";
        public string AddressFile = "";
        public string NethaspIni = "";
        public string Comment = "";
        public string ToolPath = "";
        public string ToolKind = "";
        public string ToolOutput = "";
        public string ReportedKeyId = "";
        public string ReportedKeyType = "";
        public List<string> UsbKeys = new List<string>();
        public int Hasp4Devices;
        public int HlDevices;
        public int HlErrorDevices;
    }

    public class ServiceInfo
    {
        public string Name = "";
        public string DisplayName = "";
        public string State = "";
        public string StartMode = "";
        public string Path = "";
        public bool IsOneC;
        public bool IsHasp;
    }

    public class ProcessInfo
    {
        public string Name = "";
        public string Path = "";
        public int Pid;
        public long WorkingSet;
        public string Started = "";
        public bool IsHasp;
    }

    public class PortInfo
    {
        public int Port;
        public string Protocol = "TCP";
        public string Address = "";
        public int Pid;
        public string Owner = "";
        public string Role = "";
    }

    public class Warning
    {
        public string Title;
        public string Text;
        public LogLevel Level;

        public Warning(LogLevel level, string title, string text)
        {
            Level = level;
            Title = title;
            Text = text;
        }
    }

    public class ScanResult
    {
        public DateTime Started = DateTime.Now;
        public DateTime Finished = DateTime.Now;
        public string MachineInfo = "";
        public SystemFacts System = new SystemFacts();
        public List<PlatformInfo> Platforms = new List<PlatformInfo>();
        public List<LicenseFileInfo> LicenseFiles = new List<LicenseFileInfo>();
        public List<string> LicenseStorages = new List<string>();
        public string HostId = "";
        public string LicenseRing = "";
        public string JavaPath = "";
        public string LicenseRingOutput = "";
        public SentinelInfo Sentinel = new SentinelInfo();
        public Hasp4Info Hasp4 = new Hasp4Info();
        public ClusterInfo Cluster = new ClusterInfo();
        public List<ServiceInfo> Services = new List<ServiceInfo>();
        public List<ProcessInfo> Processes = new List<ProcessInfo>();
        public List<PortInfo> Ports = new List<PortInfo>();
        public List<LogEntry> Log = new List<LogEntry>();
        public List<Warning> Warnings = new List<Warning>();

        public void AddLog(LogLevel level, string source, string text)
        {
            lock (Log)
            {
                Log.Add(new LogEntry(level, source, text));
            }
        }

        public void AddWarning(LogLevel level, string title, string text)
        {
            lock (Warnings)
            {
                Warnings.Add(new Warning(level, title, text));
            }
        }

        public int TotalFreeSeats()
        {
            int n = 0;
            foreach (SentinelFeature f in Sentinel.Features)
            {
                if (f.IsUnlimited) continue;
                n += f.FreeSeats;
            }
            return n;
        }

        public int TotalSeats()
        {
            int n = 0;
            foreach (SentinelFeature f in Sentinel.Features)
            {
                if (f.IsUnlimited) continue;
                n += f.LimitNum;
            }
            return n;
        }

        public int TotalBusySeats()
        {
            int n = 0;
            foreach (SentinelFeature f in Sentinel.Features)
            {
                if (f.IsUnlimited) continue;
                n += f.LoginsNum;
            }
            return n;
        }
    }

    public class ClusterEntry
    {
        public string Id = "";
        public string Name = "";
        public string Host = "";
        public string Port = "";
        public string Expiration = "";
    }

    public class SessionEntry
    {
        public string SessionId = "";
        public string SessionNum = "";
        public string Hibernate = "";
        public string User = "";
        public string Host = "";
        public string AppId = "";
        public string Infobase = "";
        public string InfobaseId = "";
        public string StartedAt = "";
        public string LicenseType = "";
        public string LicenseSeries = "";
        public string LicenseFullName = "";
        public string IssuedByServer = "";
        public string MaxUsersAll = "";
        public string MaxUsersCur = "";
        public string LicenseShort = "";
        public string LicenseFull = "";
        public string ClusterName = "";
        public string Usage = "";
        public string Enabled = "";
        public string Active = "";
        public string Memory = "";
        public string LicenseFile = "";
    }

    public class ClusterInfo
    {
        public bool Available;
        public string Endpoint = "localhost:1545";
        public string RacPath = "";
        public string RasPath = "";
        public bool RasStartedByUs;
        public int ImportedClientUsed = -1;
        public int ImportedClientTotal = -1;
        public string ImportedClientPin = "";
        public int ImportedServerUsed = -1;
        public int ImportedServerTotal = -1;
        public string ImportedServerPin = "";
        public string Error = "";
        public string Note = "";
        public List<ClusterEntry> Clusters = new List<ClusterEntry>();
        public List<SessionEntry> Sessions = new List<SessionEntry>();
        public List<SessionEntry> Processes = new List<SessionEntry>();
        public string SessionRaw = "";
        public string Raw = "";
    }
}
