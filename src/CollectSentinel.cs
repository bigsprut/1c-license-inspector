using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;

namespace OneCLicenseInspector
{
    internal static class CollectSentinel
    {
        private const string Endpoint = "http://127.0.0.1:1947/_int_/adminapi/get";

        public static void Run(ScanResult r)
        {
            SentinelInfo s = r.Sentinel;
            s.Endpoint = Endpoint;
            // Состояние службы менеджера лицензий — даже если он не отвечает.
            try
            {
                foreach (System.ServiceProcess.ServiceController sc in System.ServiceProcess.ServiceController.GetServices())
                {
                    if (string.Equals(sc.ServiceName, "hasplms", StringComparison.OrdinalIgnoreCase))
                    {
                        r.Sentinel.ServiceInstalled = true;
                        r.Sentinel.ServiceRunning = sc.Status == System.ServiceProcess.ServiceControllerStatus.Running;
                        r.Sentinel.ServiceState = sc.Status.ToString();
                        break;
                    }
                }
            }
            catch { }
            // Наличие физического ключа видно и без менеджера лицензий.
            try
            {
                using (System.Management.ManagementObjectSearcher searcher =
                    new System.Management.ManagementObjectSearcher("SELECT Name FROM Win32_PnPEntity WHERE Name LIKE '%HASP%' OR Name LIKE '%Sentinel%'"))
                {
                    bool usbHasp = false;
                    foreach (System.Management.ManagementBaseObject o in searcher.Get())
                    {
                        string name = Convert.ToString(o["Name"]);
                        if (!string.IsNullOrEmpty(name) &&
                            (name.IndexOf("HASP", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             name.IndexOf("Sentinel", StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            usbHasp = true;
                        }
                    }
                    if (usbHasp) r.Hasp4.UsbKeys.Add("Sentinel HASP Key (USB)");
                }
            }
            catch { }
            foreach (string k in r.Hasp4.UsbKeys) r.AddLog(LogLevel.Info, "HASP", "USB-ключ: " + k);


            int status;
            string error;
            string lm = Http.Post(Endpoint, "<admin><license_manager><element name=\"*\"/></license_manager></admin>", 3000, out status, out error);
            if (string.IsNullOrEmpty(lm))
            {
                if (AccFallback.TryLoad(r, "http://127.0.0.1:1947")) return;
                s.Available = false;
                s.Status = "нет ответа";
                s.Error = FriendlyError(error, status);
                r.AddLog(LogLevel.Warn, "Sentinel LDK", "Менеджер лицензий не отвечает: " + s.Error);
                return;
            }

            XDocument lmDoc = XmlUtil.Parse(lm);
            if (lmDoc == null)
            {
                s.Available = false;
                s.Error = "некорректный ответ";
                return;
            }
            s.Available = true;
            s.Status = XmlUtil.StatusText(lmDoc);

            XElement lmr = lmDoc.Root.Element("license_manager");
            if (lmr != null)
            {
                s.Version = XmlUtil.Value(lmr, "version");
                s.ServerName = XmlUtil.Value(lmr, "servername");
                s.ServerOs = XmlUtil.Value(lmr, "osname") + " " + XmlUtil.Value(lmr, "osversion");
                s.Addresses = XmlUtil.Value(lmr, "localips");
                s.Uptime = XmlUtil.Value(lmr, "serverruntime");
                s.LoginCount = XmlUtil.Value(lmr, "logincount");
                s.SessionCount = XmlUtil.Value(lmr, "sessioncount");
                List<string> drv = new List<string>();
                foreach (XElement d in lmr.Elements("driver_info"))
                {
                    string n = XmlUtil.Value(d, "name");
                    string v = XmlUtil.Value(d, "version");
                    if (!string.IsNullOrEmpty(n)) drv.Add(n + " " + v);
                }
                s.DriverSummary = string.Join("; ", drv.ToArray());
            }

            r.AddLog(LogLevel.Info, "Sentinel LDK", "Менеджер лицензий: " + (string.IsNullOrEmpty(s.Version) ? "версия неизвестна" : "v" + s.Version));

            LoadKeys(r);
            LoadFeatures(r);
            LoadSessions(r);

            if (s.Keys.Count == 0 && AccFallback.TryLoad(r, "http://127.0.0.1:1947")) return;

            if (s.Keys.Count == 0)
            {
                r.AddWarning(LogLevel.Info, "Аппаратные ключи Sentinel LDK не найдены",
                    "Менеджер лицензий Sentinel LDK работает, но подключённых ключей HASP HL/Sentinel HL не видно. " +
                    "Если 1С работает, лицензии могут быть программными или обслуживаться устаревшим менеджером HASP4 (порт 475).");
            }
            else
            {
                r.AddLog(LogLevel.Info, "Sentinel LDK", "Ключей: " + s.Keys.Count + ", компонентов: " + s.Features.Count + ", сеансов: " + s.Sessions.Count);
            }

            if (s.Features.Count > 0)
            {
                int busy = r.TotalBusySeats();
                int total = r.TotalSeats();
                if (total > 0 && busy >= total)
                {
                    r.AddWarning(LogLevel.Warn, "Все места заняты",
                        "Свободных лицензий в аппаратных ключах не осталось (" + busy + " из " + total + "). Новые подключения могут получать отказ.");
                }
            }
        }

        private static string FriendlyError(string error, int status)
        {
            string e = (error ?? "").ToLowerInvariant();
            if (e.Contains("удaлен") || e.Contains("удален") || e.Contains("refused") || e.Contains("отверг") || e.Contains("активно отказано"))
                return "порт 1947 закрыт: служба Sentinel LDK остановлена или не установлена";
            if (e.Contains("unable to connect") || e.Contains("actively refused") || e.Contains("could not be established"))
                return "порт 1947 закрыт: служба Sentinel LDK остановлена или не установлена";
            if (e.Contains("timed out") || e.Contains("время ожидания"))
                return "менеджер лицензий не ответил за отведённое время";
            if (!string.IsNullOrEmpty(error)) return error;
            return "HTTP " + status.ToString(CultureInfo.InvariantCulture);
        }

        private static void LoadKeys(ScanResult r)
        {
            int status;
            string error;
            string xml = Http.Post(Endpoint, "<admin><hasp><element name=\"*\"/></hasp></admin>", 3000, out status, out error);
            XDocument doc = XmlUtil.Parse(xml);
            if (doc == null) return;
            foreach (XElement k in doc.Root.Elements("hasp"))
            {
                SentinelKey key = new SentinelKey();
                key.Id = XmlUtil.Value(k, "haspid");
                key.Name = XmlUtil.Value(k, "haspname");
                key.TypeName = XmlUtil.Value(k, "typename");
                key.VendorName = XmlUtil.Value(k, "vendorname");
                key.VendorId = XmlUtil.Value(k, "vendorid");
                key.Configuration = XmlUtil.Value(k, "configuration");
                key.Location = XmlUtil.Value(k, "local");
                key.Firmware = XmlUtil.Value(k, "firmware");
                key.HwVersion = XmlUtil.Value(k, "hw_version");
                key.Sessions = XmlUtil.Value(k, "sessioncount");
                key.Disabled = XmlUtil.Value(k, "disabled");
                key.HostName = XmlUtil.Value(k, "hostname");
                key.OsName = XmlUtil.Value(k, "osname");
                if (string.IsNullOrEmpty(key.Name)) key.Name = key.Id;
                r.Sentinel.Keys.Add(key);
            }
        }

        private static void LoadFeatures(ScanResult r)
        {
            int status;
            string error;
            string xml = Http.Post(Endpoint, "<admin><feature><element name=\"*\"/></feature></admin>", 4000, out status, out error);
            XDocument doc = XmlUtil.Parse(xml);
            if (doc == null) return;

            foreach (XElement f in doc.Root.Elements("feature"))
            {
                SentinelFeature feat = new SentinelFeature();
                feat.KeyId = XmlUtil.Value(f, "haspid");
                feat.FeatureId = XmlUtil.Value(f, "featureid");
                feat.Name = XmlUtil.Value(f, "featurename");
                feat.ProductName = XmlUtil.Value(f, "productname");
                feat.ProductId = XmlUtil.Value(f, "productid");
                feat.Type = XmlUtil.Value(f, "typename");
                feat.Location = XmlUtil.Value(f, "local");
                feat.Logins = XmlUtil.Value(f, "logincount");
                feat.Limit = XmlUtil.Value(f, "loginlimit");
                feat.Sessions = XmlUtil.Value(f, "sessioncount");
                feat.ExpireText = XmlUtil.Value(f, "licenserestrictions_text");
                feat.Disabled = XmlUtil.Value(f, "disabled");
                feat.Unusable = XmlUtil.Value(f, "unusable");
                feat.Restriction = XmlUtil.Value(f, "countingtext");

                int n;
                if (int.TryParse(feat.Logins, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) feat.LoginsNum = n;
                if (int.TryParse(feat.Limit, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) feat.LimitNum = n;
                feat.IsUnlimited = feat.LimitNum <= 0;

                if (string.IsNullOrEmpty(feat.Name))
                {
                    feat.Name = string.IsNullOrEmpty(feat.ProductName) ? ("Компонент " + feat.FeatureId) : feat.ProductName;
                }

                r.Sentinel.Features.Add(feat);
                foreach (SentinelKey key in r.Sentinel.Keys)
                {
                    if (string.Equals(key.Id, feat.KeyId, StringComparison.OrdinalIgnoreCase))
                    {
                        key.Features.Add(feat);
                        break;
                    }
                }
            }
        }

        private static void LoadSessions(ScanResult r)
        {
            int status;
            string error;
            string xml = Http.Post(Endpoint, "<admin><session><element name=\"*\"/></session></admin>", 3000, out status, out error);
            XDocument doc = XmlUtil.Parse(xml);
            if (doc == null) return;
            foreach (XElement e in doc.Root.Elements("session"))
            {
                SentinelSession s = new SentinelSession();
                s.KeyId = XmlUtil.Value(e, "haspid");
                s.Feature = XmlUtil.Value(e, "featurename");
                s.User = XmlUtil.Value(e, "user");
                s.Machine = XmlUtil.Value(e, "machine");
                s.ProcessId = XmlUtil.Value(e, "processid");
                s.LoginTime = XmlUtil.Value(e, "logintime");
                s.Remaining = XmlUtil.Value(e, "remainingtimetext");
                r.Sentinel.Sessions.Add(s);
            }
        }
    }
}
