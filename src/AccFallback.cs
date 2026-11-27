using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace OneCLicenseInspector
{
    /// <summary>
    /// Резервный источник данных о ключах Sentinel: веб-интерфейс ACC отдаёт
    /// tab_dev.html / tab_feat.html в виде JSON и работает даже там, где Admin API
    /// отвечает 403. Формат: несколько объектов вида { key: "value" } подряд.
    /// </summary>
    internal static class AccFallback
    {
        public static bool TryLoad(ScanResult r, string baseUrl)
        {
            try
            {
                string devUrl = baseUrl + "/_int_/tab_dev.html?haspid=0&featureid=-1&vendorid=0&productid=0&filterfrom=1&filterto=100&timestamp=" + NowMs();
                string devText;
                int status;
                string error;
                devText = Http.Get(devUrl, 4000, out status, out error);
                if (string.IsNullOrEmpty(devText)) return false;

                List<Dictionary<string, string>> keys = ParseObjects(devText);
                if (keys.Count == 0) return false;

                int loaded = 0;
                foreach (Dictionary<string, string> o in keys)
                {
                    string id = Get(o, "haspid");
                    if (string.IsNullOrEmpty(id) || Get(o, "cnt") == "0") continue;
                    if (Get(o, "typ") == "placeholder") continue;

                    SentinelKey key = new SentinelKey();
                    key.Id = id;
                    key.Name = Get(o, "haspname");
                    key.TypeName = Get(o, "typ");
                    key.VendorName = Get(o, "ven");
                    key.VendorId = Get(o, "vid");
                    key.Configuration = Get(o, "configuration");
                    key.Location = Get(o, "loc");
                    key.Firmware = Get(o, "fw");
                    key.HwVersion = Get(o, "hw_version");
                    key.Sessions = Get(o, "sesc");
                    key.HostName = Get(o, "hostnam");
                    key.OsName = Get(o, "osnam") + " " + Get(o, "osver");
                    key.Disabled = Get(o, "key_disabled");
                    if (string.IsNullOrEmpty(key.Name)) key.Name = key.Id;
                    if (string.IsNullOrEmpty(r.Sentinel.ServerName)) r.Sentinel.ServerName = key.HostName;
                    if (string.IsNullOrEmpty(r.Sentinel.ServerOs)) r.Sentinel.ServerOs = key.OsName;
                    string vmaj = Get(o, "vmaj");
                    string vmin = Get(o, "vmin");
                    if (vmaj.Length > 0) r.Sentinel.Version = vmaj + "." + vmin + " (ACC)";

                    string featUrl = baseUrl + "/_int_/tab_feat.html?haspid=" + Uri.EscapeDataString(id) +
                                     "&featureid=-1&vendorid=0&productid=0&filterfrom=1&filterto=100&timestamp=" + NowMs();
                    string featText = Http.Get(featUrl, 4000, out status, out error);
                    if (!string.IsNullOrEmpty(featText))
                    {
                        foreach (Dictionary<string, string> f in ParseObjects(featText))
                        {
                            string fid = Get(f, "fid");
                            if (fid.Length == 0) continue;
                            SentinelFeature feat = new SentinelFeature();
                            feat.KeyId = id;
                            feat.FeatureId = fid;
                            feat.Name = Get(f, "fn");
                            feat.ProductName = Get(f, "prname");
                            feat.ProductId = Get(f, "prid");
                            feat.Type = Get(f, "typ");
                            feat.Location = Get(f, "loc");
                            feat.Logins = Get(f, "logc");
                            feat.Limit = Get(f, "logl");
                            feat.Sessions = Get(f, "sesc");
                            feat.ExpireText = Get(f, "lic");
                            feat.Disabled = Get(f, "dis");
                            feat.Unusable = Get(f, "unusable");
                            feat.Restriction = CollectSentinel.RuExpire(Get(f, "cnt"));
                            int n;
                            if (int.TryParse(feat.Logins, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) feat.LoginsNum = n;
                            if (int.TryParse(feat.Limit, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) feat.LimitNum = n;
                            feat.IsUnlimited = feat.LimitNum <= 0;
                            if (string.IsNullOrEmpty(feat.Name))
                            {
                                feat.Name = string.IsNullOrEmpty(feat.ProductName) ? ("Компонент " + fid) : feat.ProductName;
                            }
                            key.Features.Add(feat);
                            r.Sentinel.Features.Add(feat);
                        }
                    }

                    r.Sentinel.Keys.Add(key);
                    loaded++;
                }

                if (loaded > 0)
                {
                    r.Sentinel.Available = true;
                    if (string.IsNullOrEmpty(r.Sentinel.Version)) r.Sentinel.Version = "ACC";
                    r.Sentinel.Status = "ACC web (Admin API недоступен)";
                    r.Sentinel.Error = "";
                    r.AddLog(LogLevel.Info, "Sentinel LDK", "Ключей через ACC-интерфейс: " + loaded);
                    return true;
                }
            }
            catch (Exception ex)
            {
                r.AddLog(LogLevel.Warn, "Sentinel LDK", "ACC-интерфейс: " + ex.Message);
            }
            return false;
        }

        private static string NowMs()
        {
            return ((long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds).ToString(CultureInfo.InvariantCulture);
        }

        private static string Get(Dictionary<string, string> o, string key)
        {
            string v;
            return o.TryGetValue(key, out v) ? (v ?? "").Trim() : "";
        }

        /// <summary>Разбирает последовательность плоских объектов { key: "value", n: 12 }.</summary>
        private static List<Dictionary<string, string>> ParseObjects(string text)
        {
            List<Dictionary<string, string>> list = new List<Dictionary<string, string>>();
            if (string.IsNullOrEmpty(text)) return list;

            int depth = 0;
            int start = -1;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '{')
                {
                    if (depth == 0) start = i + 1;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && start >= 0)
                    {
                        string body = text.Substring(start, i - start);
                        Dictionary<string, string> obj = ParseFields(body);
                        if (obj.Count > 0) list.Add(obj);
                        start = -1;
                    }
                }
            }
            return list;
        }

        private static Dictionary<string, string> ParseFields(string body)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in Regex.Matches(body, "\"?(?<k>[A-Za-z_][A-Za-z0-9_]*)\"?\\s*:\\s*(?:\"(?<v>(?:\\\\.|[^\"\\\\])*)\"|(?<v>[-+]?[0-9]+)|(?<v>true|false))"))
            {
                string key = m.Groups["k"].Value;
                string val = m.Groups["v"].Value;
                val = val.Replace("\\\"", "\"").Replace("\\\\", "\\");
                map[key] = val;
            }
            return map;
        }
    }
}
