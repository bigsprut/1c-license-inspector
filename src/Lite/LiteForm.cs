using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace OneCLicenseInspector.Lite
{
    /// <summary>
    /// Окно краткого приложения: две вкладки — «Лицензии» и «Использование лицензий».
    /// Сканирование — тем же сборщиком, что и в полном приложении, но без аппаратных ключей,
    /// служб и портов (см. Scanner.RunQuick), поэтому старт заметно быстрее.
    /// </summary>
    internal class LiteForm : Form
    {
        private ScanResult _result = new ScanResult();
        private Ui.HeaderPanel _header;
        private Ui.SegmentedTabs _tabs;
        private Panel _content;
        private Ui.StatusBarEx _status;
        private LiteLicencesPage _licencesPage;
        private LiteSessionsPage _sessionsPage;
        private Ui.PageBase _current;
        private bool _scanning;

        public bool AutoScan = true;
        private readonly int _initialTab;

        public LiteForm(int initialTab)
        {
            _initialTab = initialTab;
            SuspendLayout();
            Text = "Лицензии 1С — кратко";
            BackColor = Ui.Theme.Canvas;
            Font = Ui.Theme.Body;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1160, 760);
            MinimumSize = new Size(980, 620);
            KeyPreview = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            _content = new Panel();
            _content.Dock = DockStyle.Fill;
            _content.BackColor = Ui.Theme.Canvas;
            Controls.Add(_content);

            _status = new Ui.StatusBarEx();
            _status.Dock = DockStyle.Bottom;
            _status.LeftText = "Готово";
            // Автор — постоянной подписью справа; щелчок открывает «О программе» (как и F1).
            _status.RightText = Program.AuthorLine;
            _status.MouseClick += delegate { Ui.AboutBox.Show(this, Program.AppName + " — кратко"); };
            Controls.Add(_status);

            _tabs = new Ui.SegmentedTabs();
            _tabs.Dock = DockStyle.Top;
            _tabs.Height = 46;
            _tabs.Items.Add("Лицензии");
            _tabs.Items.Add("Использование лицензий");
            _tabs.SelectedIndex = Math.Max(0, Math.Min(1, initialTab));
            _tabs.SelectedIndexChanged += delegate { ShowTab(); };
            Controls.Add(_tabs);

            _header = new Ui.HeaderPanel();
            _header.Dock = DockStyle.Top;
            _header.Title = "Лицензии 1С — кратко";
            _header.Subtitle = "Только лицензии и их использование";
            _header.RefreshButton.Click += delegate { StartScan(); };
            _header.Export.Click += delegate { ExportReport(); };
            _header.Copy.Click += delegate { CopyCurrent(); };
            Controls.Add(_header);

            _licencesPage = new LiteLicencesPage();
            _sessionsPage = new LiteSessionsPage();
            foreach (Ui.PageBase p in new Ui.PageBase[] { _licencesPage, _sessionsPage })
            {
                p.Dock = DockStyle.Fill;
                p.Visible = false;
                _content.Controls.Add(p);
            }

            KeyDown += delegate (object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.F5) { StartScan(); e.Handled = true; }
                else if (e.KeyCode == Keys.F1) { Ui.AboutBox.Show(this, Program.AppName + " — кратко"); e.Handled = true; }
                else if (e.Control && e.KeyCode == Keys.C) { CopyCurrent(); e.Handled = true; }
            };

            ResumeLayout(true);
            ShowTab();
            // Скан запускаем после появления окна: страницы уже имеют размеры и «растущую» строку.
            Shown += delegate { if (AutoScan) StartScan(); };
        }

        private void ShowTab()
        {
            if (_licencesPage == null) return;
            bool first = _tabs.SelectedIndex == 0;
            _current = first ? (Ui.PageBase)_licencesPage : _sessionsPage;
            _licencesPage.Visible = first;
            _sessionsPage.Visible = !first;
            try
            {
                if (first) _licencesPage.BringToFront(); else _sessionsPage.BringToFront();
            }
            catch { }
        }

        public ScanResult CurrentResult { get { return _result; } }

        /// <summary>Переключить вкладку по номеру — для автотестов и скриншотов.</summary>
        public void SelectTabForTest(int index)
        {
            try
            {
                _tabs.SelectedIndex = Math.Max(0, Math.Min(1, index));
                ShowTab();
            }
            catch { }
        }

        public void StartScan()
        {
            if (_scanning) return;
            _scanning = true;
            _header.SetBusy(true);
            _status.Busy = true;
            _status.LeftText = "Сканирование…";
            _status.Invalidate();

            Thread t = new Thread(delegate ()
            {
                ScanResult r = Scanner.RunQuick(delegate (string text)
                {
                    try
                    {
                        BeginInvoke(new Action(delegate
                        {
                            _status.LeftText = text;
                            _status.Invalidate();
                        }));
                    }
                    catch { }
                });
                try { BeginInvoke(new Action(delegate { ApplyResult(r); })); }
                catch { }
            });
            t.IsBackground = true;
            t.Start();
        }

        /// <summary>Применить результат сканирования (используется и скриншотом, и тестом).</summary>
        public void ApplyResult(ScanResult r)
        {
            if (r == null) return;
            _result = r;
            _licencesPage.SetSnapshot(r);
            _sessionsPage.SetSnapshot(r);
            try { _licencesPage.Bind(r); } catch (Exception ex) { Diag.Log("lite: licences bind: " + ex.Message); }
            try { _sessionsPage.Bind(r); } catch (Exception ex) { Diag.Log("lite: sessions bind: " + ex.Message); }

            _scanning = false;
            _header.SetBusy(false);
            _status.Busy = false;
            _status.LeftText = "Лицензий: " + _licencesPage.Count + " · сеансов: " + _sessionsPage.Count +
                               "  ·  обновлено " + DateTime.Now.ToString("HH:mm") +
                               " (" + Math.Max(1, (int)(r.Finished - r.Started).TotalSeconds) + " с)";
            _status.RightText = Program.AuthorLine;
            _status.Invalidate();
        }

        private void CopyCurrent()
        {
            try
            {
                if (_current == null) return;
                Clipboard.SetText(_current.CopyText());
                _status.LeftText = "Скопировано в буфер обмена";
                _status.Invalidate();
            }
            catch (Exception ex)
            {
                _status.LeftText = "Не удалось скопировать: " + ex.Message;
                _status.Invalidate();
            }
        }

        private void ExportReport()
        {
            try
            {
                using (SaveFileDialog dlg = new SaveFileDialog())
                {
                    dlg.Filter = "HTML-отчёт (*.html)|*.html|Все файлы (*.*)|*.*";
                    dlg.FileName = "licenses-" + Environment.MachineName + "-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".html";
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    File.WriteAllText(dlg.FileName, Report.BuildHtml(_result), new UTF8Encoding(true));
                    _status.LeftText = "Отчёт сохранён: " + dlg.FileName;
                    _status.Invalidate();
                }
            }
            catch (Exception ex)
            {
                _status.LeftText = "Не удалось сохранить отчёт: " + ex.Message;
                _status.Invalidate();
            }
        }
    }
}
