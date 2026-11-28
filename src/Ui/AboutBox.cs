using System;
using System.Drawing;
using System.Windows.Forms;

namespace OneCLicenseInspector.Ui
{
    /// <summary>
    /// «О программе»: название, версия, автор и контакты. Открывается щелчком по подписи
    /// автора в статусной строке или клавишей F1 — в обоих приложениях (полном и кратком).
    /// </summary>
    internal static class AboutBox
    {
        public static void Show(IWin32Window owner) { Show(owner, Program.AppName, 0); }

        public static void Show(IWin32Window owner, string appTitle) { Show(owner, appTitle, 0); }

        /// <summary>autoCloseMs &gt; 0 — закрыть окно само (нужно для автопроверки флагом /about).</summary>
        public static void Show(IWin32Window owner, string appTitle, int autoCloseMs)
        {
            try
            {
                using (Form f = Create(appTitle, autoCloseMs))
                {
                    if (owner != null) f.ShowDialog(owner);
                    else f.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                Diag.Log("about: не удалось показать окно: " + ex.Message);
            }
        }

        /// <summary>Создать окно «О программе» (без показа — нужно для скриншота).</summary>
        public static Form Create(string appTitle, int autoCloseMs)
        {
            Form f = new Form();
            f.Text = "О программе";
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.MaximizeBox = false;
            f.MinimizeBox = false;
            f.ShowInTaskbar = false;
            f.StartPosition = FormStartPosition.CenterParent;
            f.ClientSize = new Size(460, 250);
            f.BackColor = Color.White;
            f.Font = Theme.Body;
            f.AutoScaleMode = AutoScaleMode.None;
            try { f.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Label name = new Label();
            name.Text = appTitle;
            name.Font = Theme.Font(13f, FontStyle.Bold);
            name.ForeColor = Theme.Ink;
            name.AutoSize = true;
            name.Location = new Point(18, 16);
            f.Controls.Add(name);

            Label version = new Label();
            version.Text = "Версия " + Program.Version;
            version.ForeColor = Theme.Muted;
            version.AutoSize = true;
            version.Location = new Point(20, 46);
            f.Controls.Add(version);

            Label author = new Label();
            author.Text = "Автор: " + Program.Author;
            author.ForeColor = Theme.Ink;
            author.AutoSize = true;
            author.Location = new Point(20, 78);
            f.Controls.Add(author);

            Label mail = new Label();
            mail.Text = "E-mail: " + Program.AuthorEmail;
            mail.ForeColor = Theme.Ink;
            mail.AutoSize = true;
            mail.Location = new Point(20, 100);
            f.Controls.Add(mail);

            LinkLabel site = new LinkLabel();
            site.Text = "github.com/bigsprut/1c-license-inspector";
            site.AutoSize = true;
            site.Location = new Point(20, 122);
            site.LinkColor = Theme.AccentText;
            site.ActiveLinkColor = Theme.Accent;
            site.LinkClicked += delegate
            {
                try { System.Diagnostics.Process.Start("https://github.com/bigsprut/1c-license-inspector"); }
                catch { }
            };
            f.Controls.Add(site);

            Label note = new Label();
            note.Text = "Неофициальная утилита для аудита лицензий 1С:Предприятие.\r\n" +
                        "Не связана с фирмой «1С»; названия «1С», «HASP», «Sentinel»\r\n" +
                        "принадлежат их правообладателям. Распространяется как есть.";
            note.ForeColor = Theme.Muted;
            note.Font = Theme.Small;
            note.Location = new Point(20, 148);
            note.Size = new Size(420, 58);
            f.Controls.Add(note);

            FlatButton close = new FlatButton();
            close.Text = "Закрыть";
            close.Kind = ButtonKind.Primary;
            close.Size = new Size(110, 30);
            close.Location = new Point(332, 208);
            close.Click += delegate { f.Close(); };
            f.Controls.Add(close);

            f.KeyPreview = true;
            f.KeyDown += delegate (object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) f.Close(); };

            if (autoCloseMs > 0)
            {
                Timer t = new Timer();
                t.Interval = autoCloseMs;
                t.Tick += delegate { try { t.Stop(); f.Close(); } catch { } };
                t.Start();
            }
            return f;
        }
    }
}
