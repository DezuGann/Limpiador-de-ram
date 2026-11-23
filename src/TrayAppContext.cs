using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace LimpiadorRAM
{
    public class TrayAppContext : ApplicationContext
    {
        private NotifyIcon _notifyIcon;
        private ContextMenuStrip _contextMenu;
        private MainForm _mainForm;
        private AppSettings _settings;
        private System.Windows.Forms.Timer _trayUpdateTimer;

        // Elementos del menú
        private ToolStripMenuItem _menuOptimizeNow;
        private ToolStripMenuItem _menuOpen;
        private ToolStripMenuItem _menuAutoThreshold;
        private ToolStripMenuItem _menuAutoTimer;
        private ToolStripMenuItem _menuStartWithWindows;
        private ToolStripMenuItem _menuExit;

        public TrayAppContext(AppSettings settings, bool startMinimized)
        {
            _settings = settings;

            // Inicializar MainForm
            _mainForm = new MainForm(_settings);
            _mainForm.OptimizationCompleted += MainForm_OptimizationCompleted;

            // Inicializar Icono de la Bandeja
            InitializeTray();

            // Timer para actualizar tooltip con % de RAM en tiempo real
            _trayUpdateTimer = new System.Windows.Forms.Timer();
            _trayUpdateTimer.Interval = 3000;
            _trayUpdateTimer.Tick += delegate { UpdateTrayTooltip(); };
            _trayUpdateTimer.Start();
            UpdateTrayTooltip();

            if (!startMinimized)
            {
                _mainForm.Show();
            }
        }

        private void InitializeTray()
        {
            _contextMenu = new ContextMenuStrip();
            _contextMenu.Font = new Font("Segoe UI", 9f);

            _menuOptimizeNow = new ToolStripMenuItem("⚡ Optimizar RAM Ahora (1 Clic)");
            _menuOptimizeNow.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _menuOptimizeNow.Click += delegate { RunQuickOptimization(); };

            _menuOpen = new ToolStripMenuItem("📊 Abrir Monitor Principal");
            _menuOpen.Click += delegate { ShowMainForm(); };

            _menuAutoThreshold = new ToolStripMenuItem(string.Format("⚡ Auto-limpiar al superar {0}% RAM", _settings.AutoCleanThresholdPercent));
            _menuAutoThreshold.CheckOnClick = true;
            _menuAutoThreshold.Checked = _settings.AutoCleanThresholdEnabled;
            _menuAutoThreshold.Click += delegate
            {
                _settings.AutoCleanThresholdEnabled = _menuAutoThreshold.Checked;
                _settings.Save();
            };

            _menuAutoTimer = new ToolStripMenuItem(string.Format("⏱️ Auto-limpiar cada {0} min", _settings.AutoCleanTimerMinutes));
            _menuAutoTimer.CheckOnClick = true;
            _menuAutoTimer.Checked = _settings.AutoCleanTimerEnabled;
            _menuAutoTimer.Click += delegate
            {
                _settings.AutoCleanTimerEnabled = _menuAutoTimer.Checked;
                _settings.Save();
            };

            _menuStartWithWindows = new ToolStripMenuItem("🚀 Iniciar con Windows");
            _menuStartWithWindows.CheckOnClick = true;
            _menuStartWithWindows.Checked = _settings.StartWithWindows;
            _menuStartWithWindows.Click += delegate
            {
                _settings.StartWithWindows = _menuStartWithWindows.Checked;
                _settings.Save();
            };

            _menuExit = new ToolStripMenuItem("❌ Salir");
            _menuExit.Click += delegate { ExitApplication(); };

            _contextMenu.Items.Add(_menuOptimizeNow);
            _contextMenu.Items.Add(_menuOpen);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(_menuAutoThreshold);
            _contextMenu.Items.Add(_menuAutoTimer);
            _contextMenu.Items.Add(_menuStartWithWindows);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(_menuExit);

            _notifyIcon = new NotifyIcon();
            _notifyIcon.ContextMenuStrip = _contextMenu;
            _notifyIcon.Visible = true;
            _notifyIcon.Icon = LoadAppIcon();
            _notifyIcon.Text = "Limpiador de RAM";

            // Click en el icono: Clic primario -> Optimizar de inmediato, Doble clic -> Abrir ventana
            _notifyIcon.MouseClick += NotifyIcon_MouseClick;
            _notifyIcon.DoubleClick += delegate { ShowMainForm(); };
        }

        private Icon LoadAppIcon()
        {
            try
            {
                // Buscar app.ico en el directorio de la aplicación o recursos
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string localIco = Path.Combine(exeDir, "app.ico");
                if (File.Exists(localIco))
                {
                    return new Icon(localIco);
                }

                string resIco = Path.Combine(exeDir, @"resources\app.ico");
                if (File.Exists(resIco))
                {
                    return new Icon(resIco);
                }

                // Fallback: icono de sistema
                return SystemIcons.Application;
            }
            catch
            {
                return SystemIcons.Application;
            }
        }

        private void NotifyIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // 1 Clic izquierdo en la bandeja: Optimización inmediata
                RunQuickOptimization();
            }
        }

        public void RunQuickOptimization()
        {
            _notifyIcon.ShowBalloonTip(1500, "Limpiador de RAM", "Optimizando memoria en segundo plano...", ToolTipIcon.Info);
            _mainForm.TriggerOptimization(true);
        }

        private void MainForm_OptimizationCompleted(object sender, EventArgs e)
        {
            OptimizationResult res = sender as OptimizationResult;
            if (res != null)
            {
                string title = "⚡ ¡Memoria RAM Optimizada!";
                string msg;
                if (res.FreedMB > 0)
                {
                    msg = string.Format("Se liberaron {0:N0} MB de RAM (+{1:N2} GB).\nRAM Libre Actual: {2:N1} GB ({3}% en uso).",
                        res.FreedMB, res.FreedMB / 1024.0, res.After.FreePhysicalGB, res.After.MemoryLoadPercent);
                }
                else
                {
                    msg = string.Format("La memoria ya estaba en estado óptimo.\nRAM Libre: {0:N1} GB ({1}% en uso).",
                        res.After.FreePhysicalGB, res.After.MemoryLoadPercent);
                }

                _notifyIcon.ShowBalloonTip(4000, title, msg, ToolTipIcon.Info);
            }
            UpdateTrayTooltip();
        }

        private void UpdateTrayTooltip()
        {
            try
            {
                RamInfo ram = MemoryManager.GetRamInfo();
                string text = string.Format("Limpiador de RAM Pro\nRAM: {0}% en uso | {1:N1} GB libres",
                    ram.MemoryLoadPercent, ram.FreePhysicalGB);

                if (text.Length >= 64) // El límite de NotifyIcon.Text en Win32 es 63 chars en versiones antiguas
                {
                    text = string.Format("RAM: {0}% ({1:N1}GB libres)", ram.MemoryLoadPercent, ram.FreePhysicalGB);
                }

                _notifyIcon.Text = text;
            }
            catch { }
        }

        public void ShowMainForm()
        {
            if (_mainForm == null || _mainForm.IsDisposed)
            {
                _mainForm = new MainForm(_settings);
                _mainForm.OptimizationCompleted += MainForm_OptimizationCompleted;
            }

            _mainForm.Show();
            _mainForm.WindowState = FormWindowState.Normal;
            _mainForm.BringToFront();
            _mainForm.Activate();
        }

        private void ExitApplication()
        {
            if (_trayUpdateTimer != null)
            {
                _trayUpdateTimer.Stop();
                _trayUpdateTimer.Dispose();
            }

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }

            if (_mainForm != null && !_mainForm.IsDisposed)
            {
                _mainForm.AllowClose = true;
                _mainForm.Close();
                _mainForm.Dispose();
            }

            try { _settings.Save(); } catch { }

            ExitThread();
            Environment.Exit(0);
        }
    }
}
