using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

namespace OneCLicenseInspector
{
    /// <summary>
    /// 32-битный помощник: читает таблицу из консоли 1С 8.2 через MMC 2.0
    /// и вызывает View.ExportList (формат 3 — Unicode TSV).
    /// Аргументы: msc, node, outFile.
    /// </summary>
    internal static class Mmc20Dump
    {
        private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "mmc20dump.log");

        private static void Log(string text)
        {
            try { File.AppendAllText(LogPath, DateTime.Now.ToString("HH:mm:ss.fff") + " " + text + "\r\n", Encoding.UTF8); }
            catch { }
        }

        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                Log("start args=" + args.Length);
                for (int i = 0; i < args.Length; i++) Log("  arg" + i + "=[" + args[i] + "]");

                if (args.Length < 3) return 2;
                string msc = args[0];
                string node = args[1];
                string outFile = args[2];

                Type t = Type.GetTypeFromProgID("MMC20.Application");
                if (t == null) return 3;
                object app = Activator.CreateInstance(t);
                try { app.GetType().InvokeMember("UserControl", BindingFlags.SetProperty, null, app, new object[] { 0 }); } catch { }
                app.GetType().InvokeMember("Load", BindingFlags.InvokeMethod, null, app, new object[] { msc });
                Log("loaded " + msc);

                object doc = P(app, "Document");
                object scope = P(doc, "ScopeNamespace");
                object root = P(doc, "RootNode");
                Log("root=" + Convert.ToString(P(root, "Name")));

                object found = Find(scope, root, node, 0, 7);
                Log("found=" + (found != null));
                if (found == null) return 4;

                try { C(scope, "Expand", found); } catch { }
                object view = P(doc, "ActiveView");
                try { view.GetType().InvokeMember("ActiveScopeNode", BindingFlags.SetProperty, null, view, new object[] { found }); } catch { }
                Thread.Sleep(900);

                try { if (File.Exists(outFile)) File.Delete(outFile); } catch { }
                view.GetType().InvokeMember("ExportList", BindingFlags.InvokeMethod, null, view, new object[] { outFile, 3 });
                Thread.Sleep(300);
                Log("export exists=" + File.Exists(outFile));
                return 0;
            }
            catch (Exception ex)
            {
                Log("EX " + ex.GetType().Name + ": " + ex.Message);
                return 5;
            }
        }

        private static object Find(object scope, object parent, string needle, int depth, int maxDepth)
        {
            if (depth > maxDepth) return null;
            try { C(scope, "Expand", parent); } catch { }
            Thread.Sleep(120);

            object child = null;
            try { child = C(scope, "GetChild", parent); } catch { child = null; }
            while (child != null)
            {
                string name = "";
                try { name = Convert.ToString(P(child, "Name")); } catch { }
                if (depth <= 2) Log("  d" + depth + " [" + name + "]");
                if (!string.IsNullOrEmpty(name) && name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) return child;
                object deep = Find(scope, child, needle, depth + 1, maxDepth);
                if (deep != null) return deep;
                object next = null;
                try { next = C(scope, "GetNext", child); } catch { next = null; }
                child = next;
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
