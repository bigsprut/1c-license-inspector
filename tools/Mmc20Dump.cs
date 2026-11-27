using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

namespace OneCLicenseInspector
{
    /// <summary>
    /// 32-битный помощник (оснастка 1С 8.2 — 32-битная): открывает консоль MMC,
    /// находит узел и вызывает View.ExportList в Unicode-TSV (формат 3).
    /// Аргументы: msc, node, outFile.
    /// </summary>
    internal static class Mmc20Dump
    {
        [STAThread]
        private static int Main(string[] args)
        {
            string logPath = Path.Combine(Path.GetTempPath(), "mmc20dump.log");
            try
            {
                if (args.Length < 3)
                {
                    File.WriteAllText(logPath, "usage: Mmc20Dump <msc> <node> <out>", Encoding.UTF8);
                    return 2;
                }
                string msc = args[0];
                string node = args[1];
                string outFile = args[2];

                Type t = Type.GetTypeFromProgID("MMC20.Application");
                if (t == null)
                {
                    File.WriteAllText(outFile, "ERROR: MMC20.Application не зарегистрирован", Encoding.UTF8);
                    return 3;
                }
                object app = Activator.CreateInstance(t);
                app.GetType().InvokeMember("UserControl", BindingFlags.SetProperty, null, app, new object[] { 0 });
                app.GetType().InvokeMember("Load", BindingFlags.InvokeMethod, null, app, new object[] { msc });
                object doc = P(app, "Document");
                object scope = P(doc, "ScopeNamespace");
                object root = P(doc, "RootNode");

                object found = Find(scope, root, node, 0, 7, 0);
                if (found == null)
                {
                    File.WriteAllText(outFile, "ERROR: узел не найден: " + node, Encoding.UTF8);
                    return 4;
                }

                try { C(scope, "Expand", found); } catch { }
                object view = P(doc, "ActiveView");
                try { view.GetType().InvokeMember("ActiveScopeNode", BindingFlags.SetProperty, null, view, new object[] { found }); } catch { }
                Thread.Sleep(900);

                try { if (File.Exists(outFile)) File.Delete(outFile); } catch { }
                view.GetType().InvokeMember("ExportList", BindingFlags.InvokeMethod, null, view, new object[] { outFile, 3 });
                Thread.Sleep(300);

                if (!File.Exists(outFile))
                {
                    File.WriteAllText(logPath, "export не создал файл: " + outFile, Encoding.UTF8);
                    return 6;
                }
                File.WriteAllText(logPath, "ok " + new FileInfo(outFile).Length, Encoding.UTF8);
                return 0;
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(logPath, "EX " + ex.GetType().Name + ": " + ex.Message); } catch { }
                return 5;
            }
        }

        private static object Find(object scope, object parent, string needle, int depth, int maxDepth, int budget)
        {
            if (depth > maxDepth || budget > 800) return null;
            try { C(scope, "Expand", parent); } catch { }
            Thread.Sleep(120);

            object child = null;
            try { child = C(scope, "GetChild", parent); } catch { child = null; }
            while (child != null)
            {
                string name = "";
                try { name = Convert.ToString(P(child, "Name")); } catch { }
                if (!string.IsNullOrEmpty(name) && name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) return child;
                object deep = Find(scope, child, needle, depth + 1, maxDepth, budget + 1);
                if (deep != null) return deep;
                object next = null;
                try { next = C(scope, "GetNext", child); } catch { next = null; }
                child = next;
                budget++;
            }
            return null;
        }

        private static object P(object o, string n)
        {
            return o.GetType().InvokeMember(n, BindingFlags.GetProperty, null, o, null);
        }

        private static object C(object o, string n, params object[] a)
        {
            return o.GetType().InvokeMember(n, BindingFlags.InvokeMethod, null, o, a);
        }
    }
}
