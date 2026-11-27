using System;
using System.Collections.Generic;
using System.Text;

namespace OneCLicenseInspector
{
    internal static class Report
    {
        public static string BuildHtml(ScanResult r)
        {
            StringBuilder b = new StringBuilder(64 * 1024);
            b.AppendLine("<!DOCTYPE html>");
            b.AppendLine("<html lang=\"ru\"><head><meta charset=\"utf-8\">");
            b.AppendLine("<title>Лицензии 1С — " + H(r.System.ComputerName) + "</title>");
            b.AppendLine("<style>");
            b.AppendLine("body{font-family:'Segoe UI',Tahoma,Arial,sans-serif;background:#f4f6f8;color:#1e2a36;margin:0;padding:28px 36px 60px;font-size:14px}");
            b.AppendLine(".head{background:#1b2733;color:#fff;border-radius:10px;padding:22px 26px;margin-bottom:22px}");
            b.AppendLine(".head h1{margin:0 0 6px;font-size:22px;font-weight:600}");
            b.AppendLine(".head .sub{color:#a9b7c6;font-size:13px}");
            b.AppendLine(".cards{display:flex;gap:14px;margin:18px 0 26px;flex-wrap:wrap}");
            b.AppendLine(".card{flex:1 1 180px;background:#fff;border:1px solid #e1e6eb;border-radius:8px;padding:14px 16px;border-left:4px solid #e3a33c}");
            b.AppendLine(".card .n{font-size:26px;font-weight:600;color:#1b2733}");
            b.AppendLine(".card .l{color:#6b7a8a;font-size:12px;margin-top:2px}");
            b.AppendLine("h2{font-size:17px;margin:28px 0 10px;color:#1b2733;font-weight:600}");
            b.AppendLine("table{border-collapse:collapse;width:100%;background:#fff;border:1px solid #e1e6eb;font-size:13px}");
            b.AppendLine("th{background:#eef1f4;color:#46586a;text-align:left;padding:8px 10px;font-weight:600;border-bottom:1px solid #e1e6eb;white-space:nowrap}");
            b.AppendLine("td{padding:7px 10px;border-bottom:1px solid #eef1f4;vertical-align:top}");
            b.AppendLine("tr:last-child td{border-bottom:none}");
            b.AppendLine("tr:nth-child(even) td{background:#fafbfc}");
            b.AppendLine(".muted{color:#6b7a8a}");
            b.AppendLine(".pill{display:inline-block;padding:2px 9px;border-radius:10px;font-size:11px;font-weight:600}");
            b.AppendLine(".ok{background:#e6f3ec;color:#2e7d5b}.warn{background:#fdf0dc;color:#a5701f}.bad{background:#fbe9e7;color:#b33a2b}.info{background:#e9eef5;color:#3d5a80}");
            b.AppendLine(".warnbox{background:#fff;border:1px solid #f0d9b5;border-left:4px solid #e3a33c;border-radius:6px;padding:12px 14px;margin:10px 0}");
            b.AppendLine(".note{color:#6b7a8a;font-size:12px;margin:8px 0 0}");
            b.AppendLine("pre{background:#fff;border:1px solid #e1e6eb;border-radius:6px;padding:12px;white-space:pre-wrap;font-size:12px;color:#33465a}");
            b.AppendLine(".foot{margin-top:34px;color:#8a99a8;font-size:12px}");
            b.AppendLine("</style></head><body>");

            b.AppendLine("<div class=\"head\">");
            b.AppendLine("<h1>Инспектор лицензий 1С</h1>");
            b.AppendLine("<div class=\"sub\">" + H(r.System.ComputerName) + " · " + H(r.System.OsCaption) + " " + H(r.System.OsArch) +
                        " · отчёт от " + H(Fmt.Date(r.Finished)) + "</div>");
            b.AppendLine("</div>");

            b.AppendLine("<div class=\"cards\">");
            Card(b, r.Platforms.Count.ToString(), "платформ 1С");
            Card(b, r.Sentinel.Keys.Count.ToString(), "аппаратных ключей");
            Card(b, r.Sentinel.Features.Count.ToString(), "компонентов лицензий");
            Card(b, r.LicenseFiles.Count.ToString(), "файлов лицензий");
            Card(b, r.TotalSeats() > 0 ? r.TotalFreeSeats() + " из " + r.TotalSeats() : "—", "свободно мест");
            b.AppendLine("</div>");

            if (r.Warnings.Count > 0)
            {
                b.AppendLine("<h2>Предупреждения</h2>");
                foreach (Warning w in r.Warnings)
                {
                    string cls = w.Level == LogLevel.Error ? "bad" : (w.Level == LogLevel.Warn ? "warn" : "info");
                    b.AppendLine("<div class=\"warnbox\"><b>" + H(w.Title) + "</b><br>" + H(w.Text) + "</div>");
                }
            }

            b.AppendLine("<h2>Компьютер</h2>");
            b.AppendLine("<table>");
            Row(b, "Имя компьютера", r.System.ComputerName);
            Row(b, "Операционная система", r.System.OsCaption + " " + r.System.OsArch);
            Row(b, "Версия / сборка", r.System.OsBuild);
            Row(b, "Установлена", r.System.OsInstallDate);
            Row(b, "Процессор", r.System.Processor + " (" + r.System.CpuCount + " ядер)");
            Row(b, "Память", Fmt.Bytes(r.System.RamBytes));
            Row(b, "Платформа .NET", r.System.DotNet);
            Row(b, "Пользователь", r.System.Domain + "\\" + r.System.UserName + (r.System.IsAdmin ? " (администратор)" : ""));
            b.AppendLine("</table>");

            b.AppendLine("<h2>Платформы 1С</h2>");
            if (r.Platforms.Count == 0)
            {
                b.AppendLine("<p class=\"muted\">Не обнаружены.</p>");
            }
            else
            {
                b.AppendLine("<table><tr><th>Семейство</th><th>Версия</th><th>Разрядность</th><th>Поставка</th><th>Компоненты</th><th>Путь</th></tr>");
                foreach (PlatformInfo p in r.Platforms)
                {
                    b.AppendLine("<tr><td>" + H(p.Family) + "</td><td><b>" + H(p.Version) + "</b></td><td>" + H(p.Bitness) +
                                "</td><td>" + H(p.Delivery) + "</td><td>" + H(p.Components) + "</td><td class=\"muted\">" + H(p.Path) + "</td></tr>");
                }
                b.AppendLine("</table>");
            }

            b.AppendLine("<h2>Аппаратные ключи Sentinel LDK / HASP HL</h2>");
            if (!r.Sentinel.Available)
            {
                b.AppendLine("<p class=\"muted\">Менеджер лицензий Sentinel LDK не отвечает: " + H(r.Sentinel.Error) + "</p>");
                if (r.Sentinel.ServiceInstalled)
                {
                    Row(b, "Служба Sentinel LDK", r.Sentinel.ServiceRunning ? "запущена, но не отвечает" : "остановлена");
                }
                else
                {
                    Row(b, "Служба Sentinel LDK", "не установлена");
                }
                if (r.Hasp4.UsbKeys.Count > 0)
                {
                    Row(b, "Физический ключ", "вставлен, но состав не прочитать без службы Sentinel LDK");
                }
            }
            else if (r.Sentinel.Keys.Count == 0)
            {
                b.AppendLine("<p class=\"muted\">Ключей не найдено. Менеджер лицензий работает: " + H(r.Sentinel.Version) + "</p>");
            }
            else
            {
                b.AppendLine("<table><tr><th>Ключ (ID)</th><th>Название</th><th>Тип</th><th>Расположение</th><th>Прошивка</th><th>Сеансов</th></tr>");
                foreach (SentinelKey k in r.Sentinel.Keys)
                {
                    b.AppendLine("<tr><td><b>" + H(k.Id) + "</b></td><td>" + H(k.Name) + "</td><td>" + H(k.TypeName) +
                                "</td><td>" + H(k.Location) + "</td><td>" + H(k.Firmware) + "</td><td>" + H(k.Sessions) + "</td></tr>");
                }
                b.AppendLine("</table>");

                b.AppendLine("<h2>Компоненты лицензий (места)</h2>");
                b.AppendLine("<table><tr><th>Ключ</th><th>ID компонента</th><th>Название</th><th>Продукт</th><th>Тип</th><th>Мест</th><th>Свободно</th><th>Свободно</th><th>Ограничение</th></tr>");
                foreach (SentinelFeature f in r.Sentinel.Features)
                {
                    string free = f.IsUnlimited ? "не ограничено" : f.FreeSeats.ToString();
                    b.AppendLine("<tr><td>" + H(f.KeyId) + "</td><td>" + H(f.FeatureId) + "</td><td>" + H(f.Name) + "</td><td>" + H(f.ProductName) +
                                "</td><td>" + H(f.Type) + "</td><td>" + H(f.Logins) + "</td><td>" + H(f.IsUnlimited ? "∞" : f.Limit) +
                                "</td><td>" + H(free) + "</td><td>" + H(f.ExpireText) + "</td></tr>");
                }
                b.AppendLine("</table>");
            }

            if (r.Hasp4.UsbKeys.Count > 0)
            {
                Row(b, "USB-ключ", string.Join("; ", r.Hasp4.UsbKeys.ToArray()));
                Row(b, "Устройства HASP", "HASP4: " + r.Hasp4.Hasp4Devices + ", Sentinel HL: " + r.Hasp4.HlDevices + (r.Hasp4.HlErrorDevices > 0 ? " (с ошибкой драйвера: " + r.Hasp4.HlErrorDevices + ")" : ""));
            }
            if (r.Hasp4.PortListening && r.Sentinel.Keys.Count == 0 && r.Hasp4.UsbKeys.Count > 0)
            {
                Row(b, "Ключ обслуживает", "HASP4 Net (порт 475); количество мест показывает только HASP Monitor");
            }
            if (r.Sentinel.Status.IndexOf("ACC", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                b.AppendLine("<p class=\"note\">Admin API менеджера лицензий закрыт (403) — данные ключей прочитаны через веб-интерфейс ACC: " +
                              "tab_dev.html / tab_feat.html.</p>");
            }

            b.AppendLine("<h2>Устаревший менеджер HASP4 Net (1С 7.7 / 8.2)</h2>");
            b.AppendLine("<table>");
            Row(b, "Служба", r.Hasp4.ServiceInstalled ? (r.Hasp4.ServiceName + (r.Hasp4.ServiceRunning ? " — работает" : " — остановлена")) : "не установлена");
            Row(b, "Версия менеджера", string.IsNullOrEmpty(r.Hasp4.Version) ? "—" : r.Hasp4.Version);
            Row(b, "Порт 475", r.Hasp4.PortListening ? "прослушивается" : "закрыт");
            Row(b, "Параметры", r.Hasp4.IniSummary);
            Row(b, "Файл настроек 1С", r.Hasp4.NethaspIni);
            b.AppendLine("</table>");
            if (!string.IsNullOrEmpty(r.Hasp4.Comment)) b.AppendLine("<p class=\"note\">" + H(r.Hasp4.Comment) + "</p>");

            if (!string.IsNullOrEmpty(r.Hasp4.ToolOutput))
            {
                b.AppendLine("<p class=\"note\">Отчёт утилиты HASP (" + H(r.Hasp4.ToolKind) + "): <code>" + H(r.Hasp4.ToolPath) + "</code></p>");
                b.AppendLine("<pre>" + H(r.Hasp4.ToolOutput) + "</pre>");
            }
            else if (!string.IsNullOrEmpty(r.Hasp4.ToolPath))
            {
                b.AppendLine("<p class=\"note\">Найдена утилита HASP: <code>" + H(r.Hasp4.ToolPath) + "</code>. " +
                             "Она графическая — запустите её из программы, чтобы увидеть состав ключа.</p>");
            }
            else
            {
                b.AppendLine("<p class=\"note\">HASPInfo / HASP Monitor не найдены. Состав лицензий в ключах HASP4 " +
                             "показывает только HASP Monitor (входит в HASP4 SDK) либо сохранённый отчёт HASPInfo (*.haspinfo).</p>");
            }
            b.AppendLine("<h2>Программные лицензии</h2>");
            if (r.LicenseStorages.Count > 0)
            {
                b.AppendLine("<p class=\"muted\">Каталоги поиска: " + H(string.Join("; ", r.LicenseStorages.ToArray())) + "</p>");
            }
            int seatSumR = 0; bool seatUnlimR = false;
            foreach (SentinelFeature f in r.Sentinel.Features) { if (f.IsUnlimited) seatUnlimR = true; else seatSumR += f.LimitNum > 0 ? f.LimitNum : 0; }
            int usersSumR = 0; foreach (LicenseFileInfo lf in r.LicenseFiles) usersSumR += lf.UsersNum;
            int clientTotalR = 0, serverTotalR = 0, otherTotalR = 0;
            foreach (LicenseFileInfo lf in r.LicenseFiles)
            {
                string kind = ((lf.LicenseType ?? "") + " " + (lf.Product ?? "")).ToLowerInvariant();
                if (kind.IndexOf("сервер") >= 0) serverTotalR++;
                else if (kind.IndexOf("клиент") >= 0) clientTotalR += Math.Max(1, lf.UsersNum);
                else otherTotalR++;
            }
            int usedClientR = -1;
            if (r.Cluster != null && r.Cluster.Available)
            {
                usedClientR = 0;
                foreach (SessionEntry se in r.Cluster.Sessions)
                {
                    string sl = ((se.LicenseShort ?? "") + " " + (se.LicenseFullName ?? "") + " " + (se.LicenseType ?? "")).ToLowerInvariant();
                    if ((sl.IndexOf("клиент") >= 0 || se.AppId == "1CV8C" || se.AppId == "Designer" || se.AppId == "WebClient") && sl.IndexOf("сервер") < 0)
                        usedClientR++;
                }
            }
            Row(b, "Лицензий всего", "Лицензий: " + r.LicenseFiles.Count +
                " (клиентских мест: " + clientTotalR + ", серверных: " + serverTotalR +
                (otherTotalR > 0 ? ", прочих: " + otherTotalR : "") + ") · пользователей: " + usersSumR);
            Row(b, "Занято / свободно (клиентские)", usedClientR < 0
                ? "нет данных: кластер недоступен (нужны агент 8.3 и ras)"
                : usedClientR + " из " + clientTotalR + ", свободно " + Math.Max(0, clientTotalR - usedClientR));
            Row(b, "Серверная лицензия", serverTotalR > 0 ? "есть (" + serverTotalR + ")" : "не найдена");
            Row(b, "Места в аппаратных ключах", seatUnlimR ? "∞" : (seatSumR > 0 ? seatSumR.ToString() : "нет данных"));
            if (r.LicenseFiles.Count == 0)
            {
                b.AppendLine("<p class=\"muted\">Файлы *.lic не обнаружены.</p>");
            }
            else
            {
                b.AppendLine("<table><tr><th>Файл</th><th>Рег. номер (PIN)</th><th>Продукт</th><th>Тип</th><th>Пользователей</th><th>Срок действия</th><th>Привязка</th><th>Каталог</th></tr>");
                foreach (LicenseFileInfo f in r.LicenseFiles)
                {
                    b.AppendLine("<tr><td><b>" + H(f.FileName) + "</b></td><td>" + H(f.Pin) + "</td><td>" + H(f.Product) +
                                "</td><td>" + H(f.LicenseType) + "</td><td>" + H(f.Users) + "</td><td>" + H(f.Expires) +
                                "</td><td class=\"muted\">" + H(f.BoundTo) + "</td><td class=\"muted\">" + H(f.Directory) + "</td></tr>");
                }
                b.AppendLine("</table>");
            }
            if (!string.IsNullOrEmpty(r.HostId))
            {
                b.AppendLine("<p class=\"note\">Идентификатор компьютера для привязки лицензий: <code>" + H(r.HostId) + "</code></p>");
            }

            if (r.LicenseFiles.Count > 0)
            {
                bool anyPin = false;
                foreach (LicenseFileInfo lf in r.LicenseFiles) { if (!string.IsNullOrEmpty(lf.Pin)) { anyPin = true; break; } }
                if (!anyPin)
                {
                    b.AppendLine("<p class=\"note\">Файлы лицензий 8.2 зашифрованы: PIN, редакцию и места снаружи не прочитать — " +
                                 "их показывает консоль администрирования 8.2 (или сама 1С). Импортируйте таблицу сеансов/процессов " +
                                 "кнопкой «Из консоли 8.2», и данные попадут в этот отчёт.</p>");
                }
            }
            if (!string.IsNullOrEmpty(r.LicenseRing))
            {
                b.AppendLine("<p class=\"note\">Утилита лицензирования: <code>" + H(r.LicenseRing) + "</code></p>");
                if (!string.IsNullOrEmpty(r.LicenseRingOutput))
                {
                    b.AppendLine("<pre>" + H(r.LicenseRingOutput) + "</pre>");
                }
            }
            else
            {
                b.AppendLine("<p class=\"note\">Утилита лицензирования 1С (ring) не найдена. PIN, редакцию и число мест по файлам *.lic показывает только она (или диалог «Справка → О программе» в самой 1С).</p>");
            }
            b.AppendLine("<h2>Службы</h2>");
            b.AppendLine("<table><tr><th>Служба</th><th>Отображаемое имя</th><th>Состояние</th><th>Запуск</th></tr>");
            foreach (ServiceInfo s in r.Services)
            {
                b.AppendLine("<tr><td>" + H(s.Name) + "</td><td>" + H(s.DisplayName) + "</td><td>" + H(s.State) + "</td><td>" + H(s.StartMode) + "</td></tr>");
            }
            b.AppendLine("</table>");

            b.AppendLine("<h2>Процессы 1С и HASP</h2>");
            b.AppendLine("<table><tr><th>Процесс</th><th>PID</th><th>Память</th><th>Запущен</th><th>Путь</th></tr>");
            foreach (ProcessInfo p in r.Processes)
            {
                b.AppendLine("<tr><td>" + H(p.Name) + "</td><td>" + p.Pid + "</td><td>" + H(Fmt.Bytes(p.WorkingSet)) +
                            "</td><td>" + H(p.Started) + "</td><td class=\"muted\">" + H(p.Path) + "</td></tr>");
            }
            b.AppendLine("</table>");

            b.AppendLine("<h2>Сеансы 1С и выданные лицензии</h2>");
            if (r.Cluster != null && r.Cluster.Processes.Count > 0)
            {
                b.AppendLine("<h3>Рабочие процессы — использование серверной лицензии</h3>");
                b.AppendLine("<table><tr><th>Компьютер</th><th>Порт</th><th>PID</th><th>Лицензия</th><th>Рег. номер</th><th>Мест</th><th>Свободно</th></tr>");
                foreach (SessionEntry ps in r.Cluster.Processes)
                {
                    string pk, pp; int pu, pt;
                    Console82Import.ParseLicense(ps.LicenseShort, out pk, out pp, out pu, out pt);
                    b.AppendLine("<tr><td><b>" + H(ps.User) + "</b></td><td>" + H(ps.Host) + "</td><td>" + H(ps.SessionId) +
                                "</td><td>" + H(pk) + "</td><td>" + H(pp) + "</td><td>" + H(pu >= 0 ? pu.ToString() : "") +
                                "</td><td>" + H(pt >= 0 ? pt.ToString() : "") + "</td></tr>");
                }
                b.AppendLine("</table>");
            }
            if (!r.Cluster.Available)
            {
                b.AppendLine("<p class=\"muted\">" + H(r.Cluster.Error) + "</p>");
                if (!string.IsNullOrEmpty(r.Cluster.Note)) b.AppendLine("<p class=\"note\">" + H(r.Cluster.Note) + "</p>");
            }
            else if (r.Cluster.Sessions.Count == 0)
            {
                b.AppendLine("<p class=\"muted\">Активных сеансов нет (" + H(r.Cluster.Endpoint) + ").</p>");
                if (!string.IsNullOrEmpty(r.Cluster.Error)) b.AppendLine("<p class=\"note\">" + H(r.Cluster.Error) + "</p>");
            }
            else
            {
                bool imported = r.Cluster.Endpoint != null &&
                                r.Cluster.Endpoint.IndexOf("консоль 8.2", StringComparison.OrdinalIgnoreCase) >= 0;
                if (imported)
                {
                    b.AppendLine("<p class=\"note\">Источник: импорт таблицы из консоли администрирования 1С 8.2.</p>");
                    b.AppendLine("<table><tr><th>Пользователь</th><th>Компьютер</th><th>Приложение</th><th>База</th><th>Начало</th><th>Лицензия</th><th>Рег. номер</th><th>Мест</th><th>Свободно</th></tr>");
                    foreach (SessionEntry s in r.Cluster.Sessions)
                    {
                        string kind, pin; int used, total;
                        Console82Import.ParseLicense(s.LicenseShort, out kind, out pin, out used, out total);
                        b.AppendLine("<tr><td><b>" + H(s.User) + "</b></td><td>" + H(s.Host) +
                                    "</td><td>" + H(s.AppId) + "</td><td class=\"muted\">" + H(ShortId(s.Infobase)) + "</td><td>" + H(s.StartedAt) +
                                    "</td><td>" + H(kind) + "</td><td>" + H(pin) + "</td><td>" + H(used >= 0 ? used.ToString() : "") +
                                    "</td><td>" + H(total >= 0 ? total.ToString() : "") + "</td></tr>");
                    }
                    b.AppendLine("</table>");
                    if (r.Cluster.Sessions.Count > 0)
                    {
                        string k0, p0; int u0, t0;
                        Console82Import.ParseLicense(r.Cluster.Sessions[0].LicenseShort, out k0, out p0, out u0, out t0);
                        if (u0 >= 0 && t0 >= 0)
                            b.AppendLine("<p class=\"note\">Лицензия " + H(k0) + ", PIN " + H(p0) + ": занято " + u0 + " из " + t0 + ".</p>");
                    }
                }
                else
                {
                b.AppendLine("<table><tr><th>Пользователь</th><th>Компьютер</th><th>Приложение</th><th>База</th><th>Начало</th><th>Лицензия</th><th>Мест</th></tr>");
                foreach (SessionEntry s in r.Cluster.Sessions)
                {
                    string lic = s.LicenseShort;
                    if (string.IsNullOrEmpty(lic))
                    {
                        lic = (s.LicenseType + " " + s.LicenseSeries + " " + s.LicenseFullName).Trim();
                    }
                    string seats = s.MaxUsersCur;
                    if (!string.IsNullOrEmpty(s.MaxUsersAll)) seats = seats + " / " + s.MaxUsersAll;
                    b.AppendLine("<tr><td><b>" + H(s.User.Length == 0 ? "(не определён)" : s.User) + "</b></td><td>" + H(s.Host) +
                                "</td><td>" + H(s.AppId) + "</td><td class=\"muted\">" + H(ShortId(s.Infobase)) + "</td><td>" + H(s.StartedAt) +
                                "</td><td>" + H(lic) + "</td><td>" + H(seats) + "</td></tr>");
                }
                b.AppendLine("</table>");
                }
                if (!string.IsNullOrEmpty(r.Cluster.Error)) b.AppendLine("<p class=\"note\">" + H(r.Cluster.Error) + "</p>");
            }
            b.AppendLine("<h2>Сетевые порты</h2>");
            b.AppendLine("<table><tr><th>Порт</th><th>Протокол</th><th>Процесс</th><th>PID</th><th>Назначение</th></tr>");
            foreach (PortInfo p in r.Ports)
            {
                if (p.Role.Length == 0 && p.Port < 1024) continue;
                b.AppendLine("<tr><td><b>" + p.Port + "</b></td><td>" + H(p.Protocol) + "</td><td>" + H(p.Owner) + "</td><td>" + p.Pid + "</td><td class=\"muted\">" + H(p.Role) + "</td></tr>");
            }
            b.AppendLine("</table>");

            b.AppendLine("<h2>Журнал сбора данных</h2>");
            b.AppendLine("<pre>");
            foreach (LogEntry e in r.Log)
            {
                b.AppendLine(H(e.Time.ToString("HH:mm:ss")) + "  " + LevelText(e.Level) + "  [" + H(e.Source) + "] " + H(e.Text));
            }
            b.AppendLine("</pre>");

            b.AppendLine("<div class=\"foot\">Отчёт сформирован программой «Инспектор лицензий 1С» " + H(Program.Version) + " · " + H(Fmt.Date(r.Finished)) + "</div>");
            b.AppendLine("</body></html>");
            return b.ToString();
        }

        public static string BuildCsv(ScanResult r)
        {
            StringBuilder b = new StringBuilder();
            b.AppendLine("Раздел;Параметр;Значение");
            Csv(b, "Компьютер", "Имя", r.System.ComputerName);
            Csv(b, "Компьютер", "ОС", r.System.OsCaption + " " + r.System.OsArch);
            Csv(b, "Компьютер", "Сборка", r.System.OsBuild);
            Csv(b, "Компьютер", "Процессор", r.System.Processor);
            Csv(b, "Компьютер", "Память", Fmt.Bytes(r.System.RamBytes));
            Csv(b, "Компьютер", "Пользователь", r.System.Domain + "\\" + r.System.UserName);
            foreach (PlatformInfo p in r.Platforms)
            {
                Csv(b, "Платформы", p.Family + " " + p.Version, p.Delivery + " | " + p.Components + " | " + p.Path);
            }
            foreach (SentinelKey k in r.Sentinel.Keys)
            {
                Csv(b, "Ключи", k.Id, k.Name + " | " + k.TypeName + " | " + k.Location);
            }
            foreach (SentinelFeature f in r.Sentinel.Features)
            {
                Csv(b, "Компоненты", f.KeyId + "/" + f.FeatureId, f.Name + " | занято " + f.Logins + " из " + (f.IsUnlimited ? "∞" : f.Limit));
            }
            foreach (LicenseFileInfo f in r.LicenseFiles)
            {
                Csv(b, "Лицензии", f.FileName, f.Directory + " | " + Fmt.Bytes(f.Size) + " | " + f.Format);
            }
            foreach (Warning w in r.Warnings)
            {
                Csv(b, "Предупреждения", w.Title, w.Text);
            }
            return b.ToString();
        }

        private static string ShortId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            return id.Length > 8 ? id.Substring(0, 8) + "…" : id;
        }
        private static void Card(StringBuilder b, string number, string label)
        {
            b.AppendLine("<div class=\"card\"><div class=\"n\">" + H(number) + "</div><div class=\"l\">" + H(label) + "</div></div>");
        }

        private static void Row(StringBuilder b, string name, string value)
        {
            b.AppendLine("<tr><td class=\"muted\" style=\"width:220px\">" + H(name) + "</td><td>" + H(value) + "</td></tr>");
        }

        private static void Csv(StringBuilder b, string section, string name, string value)
        {
            b.AppendLine(Q(section) + ";" + Q(name) + ";" + Q(value));
        }

        private static string Q(string s)
        {
            if (s == null) s = "";
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }

        private static string LevelText(LogLevel l)
        {
            if (l == LogLevel.Error) return "ОШИБКА";
            if (l == LogLevel.Warn) return "ВНИМАНИЕ";
            return "ИНФО";
        }

        public static string H(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }
    }
}
