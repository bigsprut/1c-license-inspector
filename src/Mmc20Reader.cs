using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;

namespace OneCLicenseInspector
{
    /// <summary>
    /// Чтение таблиц консоли 1С 8.2 через официальную COM-модель MMC 2.0
    /// (MMC20.Application): загружаем оснастку, идём по дереву, читаем
    /// View.ListItems + View.CellContents. Окно и буфер обмена не нужны.
    /// </summary>
    internal static class Mmc20Reader
    {
        private const BindingFlags Get = BindingFlags.GetProperty;
        private const BindingFlags Invoke = BindingFlags.InvokeMethod;
        private const BindingFlags SetFlag = BindingFlags.SetProperty;

        public static string ReadTable(string mscPath, string nodeName, out string error)
        {
            error = "";

            // Основной путь: 32-битный помощник (оснастка 1С 32-битная), экспорт списка в Unicode-TSV.
            try
            {
                string helper = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mmc20Dump.exe");
                if (System.IO.File.Exists(helper))
                {
                    string outFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mmc20-" + Guid.NewGuid().ToString("N") + ".txt");
                    System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo(
                        helper, "\"" + mscPath + "\" \"" + nodeName + "\" \"" + outFile + "\"");
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    using (System.Diagnostics.Process proc = System.Diagnostics.Process.Start(psi))
                    {
                        if (!proc.WaitForExit(25000)) { try { proc.Kill(); } catch { } }
                    }
                    if (System.IO.File.Exists(outFile))
                    {
                        byte[] bytes = System.IO.File.ReadAllBytes(outFile);
                        string text = bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE
                            ? Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2)
                            : Encoding.UTF8.GetString(bytes);
                        try { System.IO.File.Delete(outFile); } catch { }
                        text = text.TrimStart('\uFEFF');
                        if (text.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
                        {
                            error = text.Substring(6).Trim();
                            return null;
                        }
                        return text;
                    }
                    error = "помощник Mmc20Dump не создал файл (см. %TEMP%\\mmc20dump.log)";
                    return null;
                }
            }
            catch (Exception ex)
            {
                error = "Mmc20Dump: " + ex.Message;
                return null;
            }

            object app = null;
            try
            {
                Type t = Type.GetTypeFromProgID("MMC20.Application");
                if (t == null) { error = "COM-объект MMC20.Application не зарегистрирован"; return null; }
                app = Activator.CreateInstance(t);
                try { app.GetType().InvokeMember("UserControl", SetFlag, null, app, new object[] { 0 }); } catch { }

                app.GetType().InvokeMember("Load", Invoke, null, app, new object[] { mscPath });
                object doc = Prop(app, "Document");
                object scope = Prop(doc, "ScopeNamespace");
                object root = Prop(doc, "RootNode");

                object found = FindNode(scope, root, nodeName, 0, 6, 0);
                if (found == null)
                {
                    error = "в дереве не найден узел «" + nodeName + "»";
                    return null;
                }

                try { scope.GetType().InvokeMember("Expand", Invoke, null, scope, new object[] { found }); } catch { }

                object view = Prop(doc, "ActiveView");
                try { view.GetType().InvokeMember("ActiveScopeNode", SetFlag, null, view, new object[] { found }); } catch { }
                Thread.Sleep(800);

                string tsv = ReadView(view, out error);
                if (string.IsNullOrEmpty(tsv) && string.IsNullOrEmpty(error))
                    error = "в узле «" + nodeName + "» нет строк";
                return tsv;
            }
            catch (Exception ex)
            {
                error = "MMC20: " + ex.Message;
                return null;
            }
            finally
            {
                try { if (app != null && System.Runtime.InteropServices.Marshal.IsComObject(app)) System.Runtime.InteropServices.Marshal.ReleaseComObject(app); }
                catch { }
            }
        }

        private static object FindNode(object scope, object parent, string needle, int depth, int maxDepth, int budget)
        {
            if (depth > maxDepth || budget > 600) return null;
            object child = null;
            try { child = scope.GetType().InvokeMember("GetChild", Invoke, null, scope, new object[] { parent }); }
            catch { child = null; }
            while (child != null)
            {
                string name = "";
                try { name = Convert.ToString(Prop(child, "Name")); } catch { }
                if (depth <= 4) Diag.Log("mmc node d" + depth + ": [" + name + "]");
                if (!string.IsNullOrEmpty(name) && name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                    return child;

                object deep = FindNode(scope, child, needle, depth + 1, maxDepth, budget + 1);
                if (deep != null) return deep;

                object next = null;
                try { next = scope.GetType().InvokeMember("GetNext", Invoke, null, scope, new object[] { child }); }
                catch { next = null; }
                child = next;
                budget++;
            }
            return null;
        }

        private static string ReadView(object view, out string error)
        {
            error = "";
            try
            {
                object items = Prop(view, "ListItems");
                int rows = Convert.ToInt32(Prop(items, "Count"));
                object colsObj = Prop(view, "Columns");
                int cols = Convert.ToInt32(Prop(colsObj, "Count"));
                if (rows <= 0 || cols <= 0) return "";

                StringBuilder sb = new StringBuilder();
                for (int c = 1; c <= cols; c++)
                {
                    string h = "";
                    try
                    {
                        object col = colsObj.GetType().InvokeMember("Item", Invoke, null, colsObj, new object[] { c });
                        h = Convert.ToString(Prop(col, "Name"));
                    }
                    catch { }
                    if (string.IsNullOrEmpty(h)) h = "col" + c;
                    sb.Append(h);
                    sb.Append(c == cols ? "\n" : "\t");
                }

                for (int r = 1; r <= rows; r++)
                {
                    object item = items.GetType().InvokeMember("Item", Invoke, null, items, new object[] { r });
                    for (int c = 1; c <= cols; c++)
                    {
                        string cell = "";
                        try
                        {
                            object v = view.GetType().InvokeMember("CellContents", Invoke, null, view, new object[] { item, c });
                            cell = Convert.ToString(v);
                        }
                        catch { }
                        sb.Append(cell);
                        sb.Append(c == cols ? "\n" : "\t");
                    }
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                error = "чтение таблицы: " + ex.Message;
                return "";
            }
        }

        private static object Prop(object obj, string name)
        {
            return obj.GetType().InvokeMember(name, Get, null, obj, null);
        }
    }
}
