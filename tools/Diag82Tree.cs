using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

internal static class Diag82
{
    private static readonly StringBuilder Tree = new StringBuilder();
    private static int _nodes;
    private static string _outDir;

    private static object P(object o, string n) { return o.GetType().InvokeMember(n, BindingFlags.GetProperty, null, o, null); }
    private static object C(object o, string n, params object[] a) { return o.GetType().InvokeMember(n, BindingFlags.InvokeMethod, null, o, a); }

    private static void Log(string s) { Tree.AppendLine(s); }

    [STAThread]
    private static void Main(string[] args)
    {
        string msc = args.Length > 0 ? args[0] : @"C:\Program Files\1cv82\common\1CV8 Servers (x86-64).msc";
        _outDir = AppDomain.CurrentDomain.BaseDirectory;

        Log("bitness=" + (Environment.Is64BitProcess ? "x64" : "x86"));
        Log("OS=" + Environment.OSVersion);
        Log("msc=" + msc + " exists=" + File.Exists(msc));

        try
        {
            Type t82 = Type.GetTypeFromProgID("V82.ServerAdminScope");
            Log("V82.ServerAdminScope: " + (t82 == null ? "нет" : "зарегистрирован"));
            if (t82 != null)
            {
                try { object o82 = Activator.CreateInstance(t82); Log("V82 создан: " + o82.GetType().FullName); }
                catch (Exception ex) { Log("V82 создать не удалось: " + ex.Message); }
            }
        }
        catch (Exception ex) { Log("V82: " + ex.Message); }

        try
        {
            Type t = Type.GetTypeFromProgID("MMC20.Application");
            Log("MMC20.Application: " + (t == null ? "нет" : "есть"));
            if (t == null) { SaveTree(); return; }

            object app = Activator.CreateInstance(t);
            C(app, "Load", msc);
            object doc = P(app, "Document");
            object scope = P(doc, "ScopeNamespace");
            object root = P(doc, "RootNode");
            Log("=== дерево (path | rows | cols) ===");
            Walk(scope, doc, root, Convert.ToString(P(root, "Name")), 0, 8);
            Log("=== узлов: " + _nodes + " ===");
        }
        catch (Exception ex)
        {
            Log("EX " + ex.GetType().Name + ": " + ex.Message);
        }

        SaveTree();
    }

    private static void Walk(object scope, object doc, object node, string path, int depth, int maxDepth)
    {
        if (depth > maxDepth || _nodes > 250) return;
        _nodes++;

        string name = "";
        try { name = Convert.ToString(P(node, "Name")); } catch { }
        string full = path + (depth == 0 ? "" : "\\" + name);

        int rows = -1, cols = -1;
        try
        {
            C(scope, "Expand", node);
            object view = P(doc, "ActiveView");
            try { view.GetType().InvokeMember("ActiveScopeNode", BindingFlags.SetProperty, null, view, new object[] { node }); } catch { }
            Thread.Sleep(350);
            view = P(doc, "ActiveView");
            rows = Convert.ToInt32(P(P(view, "ListItems"), "Count"));
            cols = Convert.ToInt32(P(P(view, "Columns"), "Count"));

            if (rows > 0 && cols > 0 && cols <= 40)
            {
                string export = Path.Combine(_outDir, "diag82-node-" + _nodes + ".txt");
                try
                {
                    if (File.Exists(export)) File.Delete(export);
                    C(view, "ExportList", export, 3);
                }
                catch { export = ""; }
                Log(full + " | rows=" + rows + " | cols=" + cols + " | " + Path.GetFileName(export));
            }
            else
            {
                Log(full + " | rows=" + rows + " | cols=" + cols);
            }
        }
        catch (Exception ex)
        {
            Log(full + " | ОШИБКА: " + ex.Message);
        }

        object child = null;
        try { child = C(scope, "GetChild", node); } catch { child = null; }
        while (child != null)
        {
            Walk(scope, doc, child, full, depth + 1, maxDepth);
            object next = null;
            try { next = C(scope, "GetNext", child); } catch { next = null; }
            child = next;
        }
    }

    private static void SaveTree()
    {
        try { File.WriteAllText(Path.Combine(_outDir, "diag82-tree.txt"), Tree.ToString(), Encoding.UTF8); }
        catch { }
    }
}
