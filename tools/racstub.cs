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
//
// Сборка: csc /out:build\racstub.exe /codepage:65001 tools\racstub.cs
// Запуск приложения: /rac:build\racstub.exe /clusteruser:… /clusterpwd:…
internal static class RacStub
{
    private static void Write(string text)
    {
        byte[] bytes = Encoding.GetEncoding(866).GetBytes(text.Replace("\n", "\r\n"));
        Console.OpenStandardOutput().Write(bytes, 0, bytes.Length);
        Console.OpenStandardOutput().Flush();
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
        "started-at     : 2026-11-27T18:40:12\n\n";

    private const string SessionLic =
        "session        : aaaaaaaa-0000-0000-0000-000000000001\n" +
        "session-id     : 3\nuser-name      : Сергей\nhost           : WORKPC\n" +
        "app-id         : 1CV8C\ninfobase       : 11111111-1111-1111-1111-111111111111\n" +
        "started-at     : 2026-11-27T18:40:12\nlicense-type   : software\n" +
        "series         : 123456789\n" +
        "full-name      : 1С Предприятие 8. Лицензия на 5 рабочих мест (программная защита)\n" +
        "issued-by-server : ALL\nmax-users-all  : 5\nmax-users-cur  : 1\n" +
        "short-presentation : \"1С:Предприятие 8 (5)\"\n\n";

    private static int Main(string[] args)
    {
        string a = string.Join(" ", args);
        string mode = Environment.GetEnvironmentVariable("RACSTUB_MODE");
        if (string.IsNullOrEmpty(mode)) mode = "open";
        bool open = string.Equals(mode, "open", StringComparison.OrdinalIgnoreCase);

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
                    if (a.IndexOf("--licenses", StringComparison.Ordinal) >= 0 && open) { Write(""); return 0; }
                    Write(open ? SessionNoLic : SessionLic);
                }
                else Write("");
                return 0;
            }
            if (needAdmin) Fail("Администратор кластера не аутентифицирован");
            // Главная особенность живого сервера: с --licenses успешный, но пустой ответ.
            if (open && a.IndexOf("--licenses", StringComparison.Ordinal) >= 0) { Write(""); return 0; }
            Write(open ? SessionNoLic : SessionLic);
            return 0;
        }

        if (a.IndexOf("process list", StringComparison.Ordinal) >= 0)
        {
            if (needAdmin) Fail("Администратор кластера не аутентифицирован");
            Write("process : bbbbbbbb-0000-0000-0000-000000000001\npid     : 4280\nrunning : yes\n" +
                  "started-at : 2026-11-27T18:00:00\nexpired-timeout : 60\n");
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
