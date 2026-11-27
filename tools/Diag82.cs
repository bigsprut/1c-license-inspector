using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

internal static class Diag82
{
    private static readonly StringBuilder LogText = new StringBuilder();

    private static void Log(string s)
    {
        LogText.AppendLine(DateTime.Now.ToString("HH:mm:ss.fff") + "  " + s);
    }

    private static object P(object o, string n) { return o.GetType().InvokeMember(n, BindingFlags.GetProperty, null, o, null); }
    private static object C(object o, string n, params object[] a) { return o.GetType().InvokeMember(n, BindingFlags.InvokeMethod, null, o, a); }

    private static object Find(object scope, object parent, string needle, int depth, int maxDepth)
    {
        if (depth > maxDepth) return null;
        try { C(scope, "Expand", parent); } catch { }
        Thread.Sleep(150);
        object child = null;
        try { child = C(scope, "GetChild", parent); } catch { child = null; }
        while (child != null)
        {
            string name = "";
            try { name = Convert.ToString(P(child, "Name")); } catch { }
            if (!string.IsNullOrEmpty(name) && name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) return child;
            object deep = Find(scope, child, needle, depth + 1, maxDepth);
            if (deep != null) return deep;
            object next = null;
            try { next = C(scope, "GetNext", child); } catch { next = null; }
            child = next;
        }
        return null;
    }

    [STAThread]
    private static void Main(string[] args)
    {
        string msc = args.Length > 0 ? args[0] : @"C:\Program Files\1cv82\common\1CV8 Servers (x86-64).msc";
        string outFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "diag82.txt");

        Log("bitness=" + (Environment.Is64BitProcess ? "x64" : "x86") + ", OS=" + Environment.OSVersion);
        Log("msc=" + msc + " exists=" + File.Exists(msc));

        try
        {
            Type t82 = Type.GetTypeFromProgID("V82.ServerAdminScope");
            Log("V82.ServerAdminScope: " + (t82 == null ? "не зарегистрирован" : "зарегистрирован"));
            if (t82 != null)
            {
                try
                {
                    object o82 = Activator.CreateInstance(t82);
                    Log("V82 создан: " + o82.GetType().FullName);
                }
                catch (Exception ex) { Log("V82 создать не удалось: " + ex.Message); }
            }
        }
        catch (Exception ex) { Log("V82 проверка: " + ex.Message); }

        try
        {
            Type t = Type.GetTypeFromProgID("MMC20.Application");
            Log("MMC20.Application: " + (t == null ? "нет" : "есть"));
            if (t == null) { Finish(outFile); return; }

            object app = Activator.CreateInstance(t);
            C(app, "Load", msc);
            object doc = P(app, "Document");
            object scope = P(doc, "ScopeNamespace");
            object root = P(doc, "RootNode");
            Log("root=" + Convert.ToString(P(root, "Name")));

            foreach (string nodeName in new string[] { "Рабочие процессы", "Сеансы" })
            {
                object found = Find(scope, root, nodeName, 0, 7);
                Log("узел «" + nodeName + "»: " + (found != null ? "найден" : "НЕ найден"));
                if (found == null) continue;

                try { C(scope, "Expand", found); } catch { }
                object view = P(doc, "ActiveView");
                try { view.GetType().InvokeMember("ActiveScopeNode", BindingFlags.SetProperty, null, view, new object[] { found }); }
                catch (Exception ex) { Log("ActiveScopeNode: " + ex.Message); }
                Thread.Sleep(2500);

                view = P(doc, "ActiveView");
                try
                {
                    object items = P(view, "ListItems");
                    int rows = Convert.ToInt32(P(items, "Count"));
                    int cols = Convert.ToInt32(P(P(view, "Columns"), "Count"));
                    Log("  ListItems: rows=" + rows + " cols=" + cols);
                }
                catch (Exception ex) { Log("  ListItems ошибка: " + ex.Message); }

                string export = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "diag82-" + (nodeName == "Сеансы" ? "sessions" : "processes") + ".txt");
                try
                {
                    if (File.Exists(export)) File.Delete(export);
                    C(view, "ExportList", export, 3);
                    Thread.Sleep(500);
                    if (File.Exists(export))
                    {
                        long len = new FileInfo(export).Length;
                        string first = File.ReadAllLines(export, Encoding.Unicode)[0];
                        Log("  ExportList: " + len + " байт, заголовок: " + first);
                    }
                    else Log("  ExportList: файл не создан");
                }
                catch (Exception ex) { Log("  ExportList ошибка: " + ex.Message); }
            }
        }
        catch (Exception ex)
        {
            Log("EX " + ex.GetType().Name + ": " + ex.Message);
        }

        Finish(outFile);
    }

    private static void Finish(string outFile)
    {
        try { File.WriteAllText(outFile, LogText.ToString(), Encoding.UTF8); } catch { }
    }
}
