using System;
using System.Text;

// Заглушка rac.exe для автотестов «Инспектора лицензий 1С» (живой кластер не нужен).
//
// Режимы (переменная среды RACSTUB_MODE):
//   open   — как на сервере ALL 27.11.2026: администраторов нет, вход не нужен,
//            а `session list --licenses` возвращает УСПЕШНЫЙ пустой ответ.
//            Проверяет запасной запрос без --licenses и честный вывод «сеансов нет».
//   admins — администраторы есть: `cluster admin list` читается анонимно, сеансы
//            отдаются только логином rac_admin. Проверяет автоподбор логина.
//   sleep  — как на сервере ALL 28.11.2026: единственный сеанс спящий, имени компьютера
//            нет, connection и process — нулевые UUID, зато есть client-ip. Проверяет,
//            что адрес клиента подставляется вместо пустого «Компьютера».
//
// Сборка: csc /out:build\racstub.exe /codepage:65001 tools\racstub.cs
// Запуск приложения: /rac:build\racstub.exe /clusteruser:… /clusterpwd:…
internal static class RacStub
{
    private static void Write(string text)
    {
        // RACSTUB_DROP=<имя поля> убирает строки поля из вывода — так проверяются пустые
        // столбцы в таблицах (например, RACSTUB_DROP=infobase делает пустой первую колонку).
        text = DropField(text);
        byte[] bytes = Encoding.GetEncoding(866).GetBytes(text.Replace("\n", "\r\n"));
        Console.OpenStandardOutput().Write(bytes, 0, bytes.Length);
        Console.OpenStandardOutput().Flush();
    }

    private static string DropField(string text)
    {
        string drop = Environment.GetEnvironmentVariable("RACSTUB_DROP");
        if (string.IsNullOrEmpty(drop)) return text;
        string[] names = drop.Split(',');
        StringBuilder sb = new StringBuilder();
        foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
        {
            string trimmed = line.TrimStart();
            bool cut = false;
            foreach (string raw in names)
            {
                string nm = raw.Trim();
                if (nm.Length == 0) continue;
                if (trimmed.StartsWith(nm + " ", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith(nm + ":", StringComparison.OrdinalIgnoreCase)) { cut = true; break; }
            }
            if (cut) continue;
            sb.Append(line).Append('\n');
        }
        return sb.ToString();
    }

    private static void Fail(string text)
    {
        byte[] bytes = Encoding.GetEncoding(866).GetBytes(text + "\r\n");
        Console.OpenStandardError().Write(bytes, 0, bytes.Length);
        Console.OpenStandardError().Flush();
        Environment.Exit(1);
    }

    private const string Cluster =
        "cluster                                   : 365a0d3e-0672-4ec8-b45f-2d5c8c5609b7\n" +
        "host                                      : ALL\n" +
        "port                                      : 1541\n" +
        "name                                      : \"Локальный кластер\"\n" +
        "expiration-timeout                        : 60\n" +
        "load-balancing-mode                       : performance\n";

    private const string SessionNoLic =
        "session        : aaaaaaaa-0000-0000-0000-000000000001\n" +
        "session-id     : 3\nuser-name      : Сергей\nhost           : WORKPC\n" +
        "app-id         : 1CV8C\ninfobase       : 11111111-1111-1111-1111-111111111111\n" +
        "started-at     : 2026-11-27T18:40:12\nhibernate      : yes\n\n";

    // Как на живом кластере 8.3.27: ответ с --licenses содержит только лицензионную часть,
    // без информационной базы и времени начала. Список без --licenses — полный.
    private const string SessionLicenseOnly =
        "session            : aaaaaaaa-0000-0000-0000-000000000001\n" +
        "user-name          : Сергей\napp-id             : 1CV8C\n" +
        "license-type       : soft\nseries             : \"123456789\"\n" +
        "issued-by-server   : yes\nmax-users-all      : 5\nmax-users-cur      : 5\n" +
        "short-presentation : \"Сервер, 123456789 5 5\"\n" +
        "full-presentation  : \"Сервер, 8732, HOST, 1541, 123456789 5 5, file://C:/ProgramData/1C/licenses/license.lic\"\n\n";

    private const string SessionLic =
        "session        : aaaaaaaa-0000-0000-0000-000000000001\n" +
        "session-id     : 3\nuser-name      : Сергей\nhost           : WORKPC\n" +
        "app-id         : 1CV8C\ninfobase       : 11111111-1111-1111-1111-111111111111\n" +
        "started-at     : 2026-11-27T18:40:12\nlicense-type   : software\n" +
        "series         : 123456789\n" +
        "full-name      : 1С Предприятие 8. Лицензия на 5 рабочих мест (программная защита)\n" +
        "issued-by-server : ALL\nmax-users-all  : 5\nmax-users-cur  : 1\n" +
        "short-presentation : \"1С:Предприятие 8 (5)\"\n\n";

    // Спящий сеанс, как на живом кластере 28.11.2026: имени компьютера rac не отдаёт,
    // connection и process — нулевые UUID (поэтому connection list не поможет), но client-ip есть.
    // Адрес по умолчанию — 192.0.2.34 (TEST-NET-1, зарезервирован для документации);
    // RACSTUB_IP=<адрес> подставляет свой, например внутренний, чтобы проверить обратный DNS.
    private static string SleepySession()
    {
        string ip = (Environment.GetEnvironmentVariable("RACSTUB_IP") ?? "").Trim();
        if (ip.Length == 0) ip = "192.0.2.34";
        return "session        : cccccccc-0000-0000-0000-000000000003\n" +
               "session-id     : 7\nuser-name      : Пользователь 2\nhost           : \n" +
               "app-id         : 1CV8C\ninfobase       : 11111111-1111-1111-1111-111111111111\n" +
               "connection     : 00000000-0000-0000-0000-000000000000\n" +
               "process        : 00000000-0000-0000-0000-000000000000\n" +
               "started-at     : 2026-11-27T12:20:03\nlast-active-at : 2026-11-28T03:06:08\n" +
               "hibernate      : yes\nclient-ip      : " + ip + "\n\n";
    }

    private static string _sessionBody = SessionNoLic;

    private static int Main(string[] args)
    {
        string a = string.Join(" ", args);
        string mode = Environment.GetEnvironmentVariable("RACSTUB_MODE");
        if (string.IsNullOrEmpty(mode)) mode = "open";
        bool sleep = string.Equals(mode, "sleep", StringComparison.OrdinalIgnoreCase);
        bool open = string.Equals(mode, "open", StringComparison.OrdinalIgnoreCase) || sleep;
        _sessionBody = sleep ? SleepySession() : (open ? SessionNoLic : SessionLic);

        bool asAdmin = a.IndexOf("--cluster-user=\"rac_admin\"", StringComparison.Ordinal) >= 0 ||
                       a.IndexOf("--cluster-user=rac_admin", StringComparison.Ordinal) >= 0;
        bool needAdmin = !open && !asAdmin;

        if (a.IndexOf("--version", StringComparison.Ordinal) >= 0) { Write("8.3.27.2342\n"); return 0; }

        if (a.IndexOf("cluster admin list", StringComparison.Ordinal) >= 0)
        {
            if (open) return 0; // администраторов нет — пустой список, код 0
            Write("name : rac_admin\nauth : pwd\ndescr : тестовый администратор\n\n" +
                  "name : ALL\\Admin2\nauth : pwd,os\nos-user : ALL\\Admin2\ndescr : \n\n");
            return 0;
        }

        if (a.IndexOf("cluster list", StringComparison.Ordinal) >= 0) { Write(Cluster); return 0; }

        if (a.IndexOf("infobase list", StringComparison.Ordinal) >= 0 ||
            a.IndexOf("infobase summary list", StringComparison.Ordinal) >= 0)
        {
            if (needAdmin) Fail("Администратор кластера не аутентифицирован");
            Write("infobase : 11111111-1111-1111-1111-111111111111\nname     : Бухгалтерия\n\n" +
                  "infobase : 22222222-2222-2222-2222-222222222222\nname     : Зарплата\n\n");
            return 0;
        }

        if (a.IndexOf("session info", StringComparison.Ordinal) >= 0)
        {
            // Режим open: в списке лицензии не отдаются, а по одному сеансу — да.
            Write(SessionLic);
            return 0;
        }

        if (a.IndexOf("session list", StringComparison.Ordinal) >= 0)
        {
            if (a.IndexOf("--infobase=", StringComparison.Ordinal) >= 0)
            {
                // сеанс принадлежит только «Бухгалтерии»
                if (a.IndexOf("11111111-1111", StringComparison.Ordinal) >= 0)
                {
                    if (a.IndexOf("--licenses", StringComparison.Ordinal) >= 0 && open) { Write(SessionLicenseOnly); return 0; }
                    Write(_sessionBody);
                }
                else Write("");
                return 0;
            }
            if (needAdmin) Fail("Администратор кластера не аутентифицирован");
            // Главная особенность живого сервера: с --licenses успешный, но пустой ответ.
            if (open && a.IndexOf("--licenses", StringComparison.Ordinal) >= 0) { Write(SessionLicenseOnly); return 0; }
            Write(_sessionBody);
            return 0;
        }

        if (a.IndexOf("process list", StringComparison.Ordinal) >= 0)
        {
            if (needAdmin) Fail("Администратор кластера не аутентифицирован");
            Write("process : bbbbbbbb-0000-0000-0000-000000000001\npid     : 4280\nrunning : yes\n" +
                  "started-at : 2026-11-27T18:00:00\nexpired-timeout : 60\n" +
                  "capacity : 37\navailable-perfomance : 88\nmemory-size : 524288000\n" +
                  "host : ALL\nport : 1560\nconnections : 3\n");
            return 0;
        }

        if (a.IndexOf("server list", StringComparison.Ordinal) >= 0)
        {
            if (needAdmin) Fail("Администратор кластера не аутентифицирован");
            Write("server : bbbbbbbb-0000-0000-0000-0000000000ff\nhost   : ALL\nport   : 1541\n\n");
            return 0;
        }

        if (a.IndexOf("connection list", StringComparison.Ordinal) >= 0) { Write(""); return 0; }

        if (a.IndexOf("cluster info", StringComparison.Ordinal) >= 0)
        {
            if (needAdmin) Fail("Администратор кластера не аутентифицирован");
            Write("cluster : 365a0d3e-0672-4ec8-b45f-2d5c8c5609b7\nname : \"Локальный кластер\"\n");
            return 0;
        }

        Fail("Ошибка разбора параметра: " + a);
        return 3;
    }
}
