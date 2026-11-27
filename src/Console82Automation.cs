using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace OneCLicenseInspector
{
    /// <summary>
    /// Чтение таблиц из консоли администрирования 1С 8.2 (MMC) напрямую:
    /// находим узел дерева («Сеансы» / «Рабочие процессы»), выделяем его и
    /// вычитываем строки из списка результатов через Win32-сообщения.
    /// </summary>
    internal static class Console82Automation
    {
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder text, int count);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int cmd);
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        // TreeView
        private const uint TVM_GETNEXTITEM = 0x110A;
        private const uint TVM_GETITEMW = 0x113E;
        private const uint TVM_EXPAND = 0x1102;
        private const uint TVM_SELECTITEM = 0x110B;
        private const uint TVM_GETITEMRECT = 0x1104;
        private const int TVGN_ROOT = 0;
        private const int TVGN_NEXT = 1;
        private const int TVGN_CHILD = 4;
        private const int TVGN_CARET = 9;
        private const int TVE_EXPAND = 2;
        private const uint TVIF_TEXT = 0x1;
        private const uint TVIF_HANDLE = 0x10;

        // ListView
        private const uint LVM_GETITEMCOUNT = 0x1004;
        private const uint LVM_GETITEMTEXTW = 0x1073;
        private const uint LVM_GETCOLUMNW = 0x105F;
        private const uint LVM_GETHEADER = 0x101F;
        private const uint HDM_GETITEMCOUNT = 0x1200;
        private const uint LVIF_TEXT = 0x1;
        private const uint LVCF_TEXT = 0x4;
        private const uint LVCF_SUBITEM = 0x2;

        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct TVITEMW
        {
            public uint mask;
            public IntPtr hItem;
            public uint state;
            public uint stateMask;
            public IntPtr pszText;
            public int cchTextMax;
            public int iImage;
            public int iSelectedImage;
            public int cChildren;
            public IntPtr lParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left, top, right, bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct LVCOLUMNW
        {
            public uint mask;
            public int fmt;
            public int cx;
            public IntPtr pszText;
            public int cchTextMax;
            public int iSubItem;
            public int iImage;
            public int iOrder;
            public int cxMin;
            public int cxDefault;
            public int cxIdeal;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct LVITEMW
        {
            public uint mask;
            public int iItem;
            public int iSubItem;
            public uint state;
            public uint stateMask;
            public IntPtr pszText;
            public int cchTextMax;
            public int iImage;
            public IntPtr lParam;
            public int iIndent;
            public int iGroupId;
            public uint cColumns;
            public IntPtr puColumns;
            public IntPtr piColFmt;
            public int iGroup;
        }

        public static IntPtr FindConsole()
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows(delegate (IntPtr hWnd, IntPtr lParam)
            {
                StringBuilder cls = new StringBuilder(256);
                GetClassName(hWnd, cls, cls.Capacity);
                if (cls.ToString() != "MMCMainFrame") return true;
                if (!IsWindowVisible(hWnd)) return true;
                StringBuilder title = new StringBuilder(512);
                GetWindowText(hWnd, title, title.Capacity);
                string t = title.ToString();
                if (t.IndexOf("1CV8", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.IndexOf("1С", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.IndexOf("Администра", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    found = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        public static void BringToFront()
        {
            IntPtr wnd = FindConsole();
            if (wnd == IntPtr.Zero) return;
            ShowWindow(wnd, 9);
            SetForegroundWindow(wnd);
            Thread.Sleep(500);
        }

        /// <summary>Активирует консоль и нажимает Ctrl+C. Возвращает текст из буфера или null.</summary>
        public static string CopyTable(out string error)
        {
            error = "";
            IntPtr wnd = FindConsole();
            if (wnd == IntPtr.Zero)
            {
                error = "консоль администрирования 1С 8.2 не найдена — откройте её кнопкой «Открыть консоль»";
                return null;
            }
            try
            {
                ShowWindow(wnd, 9);
                SetForegroundWindow(wnd);
                Thread.Sleep(500);

                keybd_event(0x11, 0, 0, UIntPtr.Zero);
                keybd_event(0x43, 0, 0, UIntPtr.Zero);
                keybd_event(0x43, 0, 2, UIntPtr.Zero);
                keybd_event(0x11, 0, 2, UIntPtr.Zero);
                Thread.Sleep(700);

                if (!Clipboard.ContainsText())
                {
                    error = "в буфере нет текста: щёлкните по таблице в консоли и повторите";
                    return null;
                }
                return Clipboard.GetText();
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null;
            }
        }

        /// <summary>
        /// Находит узел дерева по названию, выделяет его и вычитывает таблицу списка.
        /// </summary>
        public static string ReadTable(string nodeText, out string error)
        {
            error = "";
            IntPtr wnd = FindConsole();
            if (wnd == IntPtr.Zero)
            {
                error = "консоль не найдена";
                return null;
            }
            BringToFront();

            IntPtr tree = IntPtr.Zero;
            EnumChildWindows(wnd, delegate (IntPtr h, IntPtr l)
            {
                StringBuilder cls = new StringBuilder(256);
                GetClassName(h, cls, cls.Capacity);
                if (cls.ToString() == "SysTreeView32") { tree = h; return false; }
                return true;
            }, IntPtr.Zero);
            if (tree == IntPtr.Zero)
            {
                error = "в консоли не найдено дерево";
                return null;
            }

            IntPtr node = FindNode(tree, nodeText, IntPtr.Zero, 0, 6);
            Diag.Log("ui82: window=" + wnd + " tree=" + tree + " node=" + node);
            if (node == IntPtr.Zero)
            {
                error = "не найден узел «" + nodeText + "»";
                return null;
            }

            SelectNode(tree, node);
            Thread.Sleep(1200);

            IntPtr list = FindBestList(wnd);
            if (list == IntPtr.Zero)
            {
                error = "список результатов не найден";
                return null;
            }
            int rows = (int)SendMessage(list, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
            if (rows <= 0)
            {
                error = "список пуст";
                return null;
            }

            string tsv = ReadListTsv(list);
            if (string.IsNullOrEmpty(tsv))
            {
                error = "не удалось прочитать строки списка";
                return null;
            }
            return tsv;
        }

        private static IntPtr FindNode(IntPtr tree, string needle, IntPtr parent, int depth, int maxDepth)
        {
            if (depth > maxDepth) return IntPtr.Zero;
            IntPtr child = (IntPtr)SendMessage(tree, TVM_GETNEXTITEM, (IntPtr)TVGN_CHILD, parent);
            while (child != IntPtr.Zero)
            {
                string text = GetNodeText(tree, child);
                if (!string.IsNullOrEmpty(text) &&
                    text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return child;
                }
                SendMessage(tree, TVM_EXPAND, (IntPtr)TVE_EXPAND, child);
                IntPtr deep = FindNode(tree, needle, child, depth + 1, maxDepth);
                if (deep != IntPtr.Zero) return deep;
                child = (IntPtr)SendMessage(tree, TVM_GETNEXTITEM, (IntPtr)TVGN_NEXT, child);
            }
            return IntPtr.Zero;
        }

        private static string GetNodeText(IntPtr tree, IntPtr item)
        {
            IntPtr buffer = Marshal.AllocHGlobal(1024);
            try
            {
                TVITEMW tv = new TVITEMW();
                tv.mask = TVIF_TEXT | TVIF_HANDLE;
                tv.hItem = item;
                tv.pszText = buffer;
                tv.cchTextMax = 512;
                IntPtr p = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TVITEMW)));
                try
                {
                    Marshal.StructureToPtr((object)tv, p, false);
                    SendMessage(tree, TVM_GETITEMW, IntPtr.Zero, p);
                    return Marshal.PtrToStringUni(buffer) ?? "";
                }
                finally { Marshal.FreeHGlobal(p); }
            }
            catch { return ""; }
            finally { Marshal.FreeHGlobal(buffer); }
        }

        private static void SelectNode(IntPtr tree, IntPtr node)
        {
            SendMessage(tree, TVM_SELECTITEM, (IntPtr)TVGN_CARET, node);
            IntPtr rectPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(RECT)));
            try
            {
                Marshal.StructureToPtr((object)new RECT(), rectPtr, false);
                SendMessage(tree, TVM_GETITEMRECT, node, rectPtr);
                RECT r = (RECT)Marshal.PtrToStructure(rectPtr, typeof(RECT));
                int x = Math.Max(2, (r.right - r.left) / 2);
                int y = Math.Max(2, (r.bottom - r.top) / 2);
                IntPtr lp = (IntPtr)((y << 16) | (x & 0xFFFF));
                PostMessage(tree, WM_LBUTTONDOWN, (IntPtr)1, lp);
                PostMessage(tree, WM_LBUTTONUP, IntPtr.Zero, lp);
            }
            catch { }
            finally { Marshal.FreeHGlobal(rectPtr); }
        }

        private static IntPtr FindBestList(IntPtr wnd)
        {
            IntPtr best = IntPtr.Zero;
            int bestRows = -1;
            EnumChildWindows(wnd, delegate (IntPtr h, IntPtr l)
            {
                StringBuilder cls = new StringBuilder(256);
                GetClassName(h, cls, cls.Capacity);
                if (cls.ToString() != "SysListView32") return true;
                if (!IsWindowVisible(h)) return true;
                int rows = (int)SendMessage(h, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
                string headers = ReadHeaders(h);
                bool looksLikeOurTable = headers.IndexOf("Лицензия", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                         headers.IndexOf("Номер сеанса", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                         headers.IndexOf("PID", StringComparison.OrdinalIgnoreCase) >= 0;
                int score = rows + (looksLikeOurTable ? 1000 : 0);
                if (score > bestRows) { bestRows = score; best = h; }
                return true;
            }, IntPtr.Zero);
            return best;
        }

        private static string ReadHeaders(IntPtr list)
        {
            IntPtr header = SendMessage(list, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);
            if (header == IntPtr.Zero) return "";
            int count = (int)SendMessage(header, HDM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
            StringBuilder all = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                all.Append(ReadColumn(list, i)).Append('\t');
            }
            return all.ToString();
        }

        private static string ReadColumn(IntPtr list, int index)
        {
            IntPtr buffer = Marshal.AllocHGlobal(1024);
            IntPtr p = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(LVCOLUMNW)));
            try
            {
                LVCOLUMNW c = new LVCOLUMNW();
                c.mask = LVCF_TEXT | LVCF_SUBITEM;
                c.pszText = buffer;
                c.cchTextMax = 512;
                c.iSubItem = index;
                Marshal.StructureToPtr((object)c, p, false);
                SendMessage(list, LVM_GETCOLUMNW, (IntPtr)index, p);
                Marshal.PtrToStructure(p, typeof(LVCOLUMNW));
                return Marshal.PtrToStringUni(buffer) ?? "";
            }
            catch { return ""; }
            finally { Marshal.FreeHGlobal(buffer); Marshal.FreeHGlobal(p); }
        }

        private static string ReadListTsv(IntPtr list)
        {
            IntPtr header = SendMessage(list, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);
            int cols = header != IntPtr.Zero
                ? (int)SendMessage(header, HDM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero)
                : 1;
            int rows = (int)SendMessage(list, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
            if (cols <= 0 || rows <= 0) return null;

            StringBuilder sb = new StringBuilder();
            for (int c = 0; c < cols; c++) sb.Append(ReadColumn(list, c)).Append(c == cols - 1 ? "\n" : "\t");

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    string cell = ReadCell(list, r, c);
                    sb.Append(cell).Append(c == cols - 1 ? "\n" : "\t");
                }
            }
            return sb.ToString();
        }

        private static string ReadCell(IntPtr list, int row, int col)
        {
            IntPtr buffer = Marshal.AllocHGlobal(2048);
            IntPtr p = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(LVITEMW)));
            try
            {
                LVITEMW item = new LVITEMW();
                item.mask = LVIF_TEXT;
                item.iItem = row;
                item.iSubItem = col;
                item.pszText = buffer;
                item.cchTextMax = 1000;
                Marshal.StructureToPtr((object)item, p, false);
                SendMessage(list, LVM_GETITEMTEXTW, (IntPtr)row, p);
                return Marshal.PtrToStringUni(buffer) ?? "";
            }
            catch { return ""; }
            finally { Marshal.FreeHGlobal(buffer); Marshal.FreeHGlobal(p); }
        }
    }
}
