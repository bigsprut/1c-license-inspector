using System;
using System.Collections.Generic;

namespace OneCLicenseInspector.Ui
{
    /// <summary>
    /// Карточка выбранного сеанса — текст под таблицей. Общая для полного приложения
    /// (вкладка «Использование») и краткого (вкладка «Использование лицензий»),
    /// чтобы описание сеанса не расходилось между окнами.
    /// </summary>
    internal static class SessionCard
    {
        public static string Build(SessionEntry s)
        {
            if (s == null) return "";
            List<string> lines = new List<string>();
            lines.Add("Пользователь : " + (string.IsNullOrEmpty(s.User) ? "(не определён)" : s.User));
            string clientIp = CollectCluster.NormalizeIp(s.ClientIp);
            lines.Add("Компьютер    : " + (string.IsNullOrEmpty(s.Host)
                ? (clientIp.Length > 0 ? "не определён — 1С не отдаёт имя у спящего сеанса" : "")
                : s.Host));
            if (clientIp.Length > 0)
                lines.Add("Адрес клиента: " + clientIp +
                          (string.Equals(s.HostSource, "dns", StringComparison.OrdinalIgnoreCase)
                              ? " · имя компьютера получено обратным DNS"
                              : ""));
            lines.Add("Приложение   : " + Fmt.AppName(s.AppId));
            lines.Add("База         : " + s.Infobase +
                      (!string.IsNullOrEmpty(s.InfobaseId) && s.InfobaseId != s.Infobase ? " (" + s.InfobaseId + ")" : ""));
            lines.Add("Начало       : " + UsageData.ShortDate(s.StartedAt));
            // Последняя активность — единственный признак «давно ли пользователь работал»:
            // у спящего сеанса она заметно отличается от времени начала.
            if (!string.IsNullOrEmpty(s.LastActivity) &&
                !string.Equals(s.LastActivity, s.StartedAt, StringComparison.Ordinal))
                lines.Add("Активность   : " + UsageData.ShortDate(s.LastActivity));
            bool sleeping = string.Equals(s.Hibernate, "yes", StringComparison.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(s.Hibernate))
                lines.Add("Состояние    : " + (sleeping ? "спящий — 1С освободила клиентскую лицензию" : "активный"));
            if (!string.IsNullOrEmpty(s.LicenseType)) lines.Add("Тип лицензии : " + s.LicenseType);
            if (!string.IsNullOrEmpty(s.LicenseSeries)) lines.Add("Серия        : " + s.LicenseSeries);
            if (!string.IsNullOrEmpty(s.LicenseFullName)) lines.Add("Продукт      : " + s.LicenseFullName);
            if (!string.IsNullOrEmpty(s.IssuedByServer)) lines.Add("Выдана сервером : " + s.IssuedByServer);
            if (!string.IsNullOrEmpty(s.MaxUsersCur)) lines.Add("Мест занято/всего : " + s.MaxUsersCur + " / " + s.MaxUsersAll);
            if (!string.IsNullOrEmpty(s.LicenseFull))
            {
                lines.Add("");
                lines.Add(s.LicenseFull);
            }
            return string.Join("\r\n", lines.ToArray());
        }
    }
}
