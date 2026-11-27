using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace OneCLicenseInspector
{
    /// <summary>
    /// Мини-автоматизация консоли администрирования 1С 8.2: находим окно MMC,
    /// поднимаем его и сами нажимаем Ctrl+C, чтобы забрать таблицу из буфера.
    /// </summary>
    internal static class Console82Automation
    {
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder text, int count);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int cmd);
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);

        public static IntPtr FindConsole()
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows(delegate (IntPtr hWnd, IntPtr lParam)
            {
                StringBuilder cls = new StringBuilder(256);
                GetClassName(hWnd, cls, cls.Capacity);
                if (cls.ToString() != "MMCMainFrame") return true;
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

                keybd_event(0x11, 0, 0, UIntPtr.Zero);   // Ctrl down
                keybd_event(0x43, 0, 0, UIntPtr.Zero);   // C down
                keybd_event(0x43, 0, 2, UIntPtr.Zero);   // C up
                keybd_event(0x11, 0, 2, UIntPtr.Zero);   // Ctrl up
                Thread.Sleep(700);

                if (!Clipboard.ContainsText())
                {
                    error = "в буфере нет текста: щёлкните по таблице в консоли (сеансы или рабочие процессы) и повторите";
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
    }
}
