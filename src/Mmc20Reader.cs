using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

namespace OneCLicenseInspector
{
    /// <summary>
    /// Чтение таблицы консоли 1С 8.2 через официальную COM-модель MMC 2.0
    /// в том же процессе, что и программа (64-битная MMC для 64-битной ОС —
    /// как у пользователя, когда он открывает консоль руками).
    /// </summary>
    internal static class Mmc20Reader
    {
        public static string ReadTable(string mscPath, string nodeName, out string error)
        {
            error = "";
            string outFile = Path.Combine(Path.GetTempPath(), "mmc20-" + Guid.NewGuid().ToString("N") + ".txt");
            object app = null;
            try
            {
                Type t = Type.GetTypeFromProgID("MMC20.Application");
                if (t == null) { error = "MMC20.Application не зарегистрирован"; return null; }

                app = Activator.CreateInstance(t);
                try { app.GetType().InvokeMember("UserControl", BindingFlags.SetProperty, null, app, new object[] { 0 }); } catch { }
                app.GetType().InvokeMember("Load", BindingFlags.InvokeMethod, null, app, new object[] { mscPath });

                object doc = Prop(app, "Document");
                object scope = Prop(doc, "ScopeNamespace");
                object root = Prop(doc, "RootNode");

                object found = Find(scope, root, nodeName, 0, 7);
                if (found == null)
                {
                    error = "в дереве не найден узел «" + nodeName + "»";
                    return null;
                }

                try { Call(scope, "Expand", found); } catch { }
                object view = Prop(doc, "ActiveView");
                try { view.GetType().InvokeMember("ActiveScopeNode", BindingFlags.SetProperty, null, view, new object[] { found }); } catch { }
                Thread.Sleep(900);

                try { if (File.Exists(outFile)) File.Delete(outFile); } catch { }
                Call(view, "ExportList", outFile, 3);
                Thread.Sleep(300);

                if (!File.Exists(outFile))
                {
                    error = "ExportList не создал файл";
                    return null;
                }

                byte[] bytes = File.ReadAllBytes(outFile);
                string text = bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE
                    ? Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2)
                    : Encoding.UTF8.GetString(bytes);
                return text.TrimStart('\uFEFF');
            }
            catch (Exception ex)
            {
                error = "MMC20: " + ex.Message;
                return null;
            }
            finally
            {
                try { if (File.Exists(outFile)) File.Delete(outFile); } catch { }
                try { if (app != null && System.Runtime.InteropServices.Marshal.IsComObject(app)) System.Runtime.InteropServices.Marshal.ReleaseComObject(app); }
                catch { }
            }
        }

        private static object Find(object scope, object parent, string needle, int depth, int maxDepth)
        {
            if (depth > maxDepth) return null;
            try { Call(scope, "Expand", parent); } catch { }
            Thread.Sleep(150);

            object child = null;
            try { child = Call(scope, "GetChild", parent); } catch { child = null; }
            while (child != null)
            {
                string name = "";
                try { name = Convert.ToString(Prop(child, "Name")); } catch { }
                if (!string.IsNullOrEmpty(name) && name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                    return child;

                object deep = Find(scope, child, needle, depth + 1, maxDepth);
                if (deep != null) return deep;

                object next = null;
                try { next = Call(scope, "GetNext", child); } catch { next = null; }
                child = next;
            }
            return null;
        }

        private static object Prop(object obj, string name)
        {
            return obj.GetType().InvokeMember(name, BindingFlags.GetProperty, null, obj, null);
        }

        private static object Call(object obj, string name, params object[] args)
        {
            return obj.GetType().InvokeMember(name, BindingFlags.InvokeMethod, null, obj, args);
        }
    }
}
