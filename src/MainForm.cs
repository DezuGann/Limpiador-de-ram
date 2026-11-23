using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace LimpiadorRAM
{
    public class MainForm : Form
    {
        private AppSettings _settings;
        private System.Windows.Forms.Timer _monitorTimer;
        private BackgroundWorker _cleanerWorker;
        private bool _isOptimizing = false;

        // Color de acento dinámico de Windows
        private Color _accentColor = Color.FromArgb(16, 185, 129);

        // Activos visuales (Fondo watermark_bg y avatar original)
        private Image _watermarkImage = null;
        private Image _avatarLogo = null;

        // Encabezado
        private Panel _headerPanel;

        // Columna Izquierda: Monitor de Memoria estilo Task Manager
        private FluentCard _cardMeter;
        private TaskMgrGraphControl _taskMgrGraph;
        private StatChip _chipPercent;
        private StatChip _chipUsed;
        private StatChip _chipFree;
        private StatChip _chipTotal;
        private Label _lblTelemetry;
        private FluentButton _btnOptimize;

        // Registro de actividad debajo de la gráfica
        private FluentCard _cardLog;
        private FluentLogBox _logBox;

        // Barra desplegable y panel de ajustes
        private FluentButton _btnToggleSettings;
        private FluentCard _cardSettings;
        private FluentToggle _togWorkingSets;
        private FluentToggle _togTempFiles;
        private FluentToggle _togRecycleBin;
        private FluentToggle _togFlushDns;

        private FluentToggle _togAutoThreshold;
        private NumericUpDown _numThreshold;
        private FluentToggle _togAutoTimer;
        private NumericUpDown _numTimerMinutes;
        private FluentToggle _togStartWithWindows;

        private bool _isLoadingSettings = false;
        private DateTime _lastAutoClean = DateTime.MinValue;

        public event EventHandler OptimizationCompleted;

        public MainForm(AppSettings settings)
        {
            _settings = settings;
            _accentColor = ThemeHelper.GetAccentColor();
            LoadVisualAssets();
            InitializeComponent();
            ApplySettingsToUi();
            StartMonitoring();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ThemeHelper.ApplyModernWindowAttributes(this.Handle, _accentColor);
        }

        private void LoadVisualAssets()
        {
            try
            {
                string[] searchDirs = new string[]
                {
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    AppDomain.CurrentDomain.BaseDirectory,
                    Directory.GetCurrentDirectory()
                };

                // 1. Cargar fondo watermark_bg (fondo ensanchado con circuitos y emblema)
                foreach (string dir in searchDirs)
                {
                    if (string.IsNullOrEmpty(dir)) continue;

                    string bgPath = Path.Combine(dir, @"resources\watermark_bg.jpg");
                    if (!File.Exists(bgPath)) bgPath = Path.Combine(dir, "watermark_bg.jpg");
                    if (File.Exists(bgPath))
                    {
                        _watermarkImage = Image.FromFile(bgPath);
                        break;
                    }
                }

                // 2. Cargar icono original que pasó el usuario (panda_clean.png)
                foreach (string dir in searchDirs)
                {
                    if (string.IsNullOrEmpty(dir)) continue;

                    string pandaClean = Path.Combine(dir, @"resources\panda_clean.png");
                    if (!File.Exists(pandaClean)) pandaClean = Path.Combine(dir, "panda_clean.png");
                    if (File.Exists(pandaClean))
                    {
                        _avatarLogo = Image.FromFile(pandaClean);
                        break;
                    }
                }
            }
            catch { }
        }

        private void InitializeComponent()
        {
            this.Text = "Limpiador de RAM";
            this.ClientSize = new Size(516, _settings.SettingsExpanded ? 866 : 616);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(12, 14, 22);
            this.ForeColor = Color.FromArgb(240, 244, 248);
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.DoubleBuffered = true;
            this.Icon = LoadAppIcon();

            int contentW = 480;
            int marginX = 18;

            // =====================================================================
            // 1. ENCABEZADO LIMPIO CON EL ICONO ORIGINAL
            // =====================================================================
            _headerPanel = new Panel();
            _headerPanel.Dock = DockStyle.Top;
            _headerPanel.Height = 54;
            _headerPanel.BackColor = Color.FromArgb(195, 14, 18, 28);
            _headerPanel.Paint += HeaderPanel_Paint;

            // =====================================================================
            // 2. MONITOR DE MEMORIA (TASK MANAGER GRAPH CON ESCALA GB)
            // =====================================================================
            _cardMeter = new FluentCard();
            _cardMeter.Location = new Point(marginX, 62);
            _cardMeter.Size = new Size(contentW, 275);
            _cardMeter.HeaderIcon = "⚡";
            _cardMeter.HeaderTitle = "Monitor de memoria";
            _cardMeter.AccentColor = _accentColor;

            _taskMgrGraph = new TaskMgrGraphControl();
            _taskMgrGraph.Location = new Point(14, 36);
            _taskMgrGraph.Size = new Size(contentW - 28, 140);
            _taskMgrGraph.AccentColor = _accentColor;

            // Mini chips métricos
            int chipW = (contentW - 28 - 24) / 4;
            _chipPercent = new StatChip();
            _chipPercent.Location = new Point(14, 184);
            _chipPercent.Size = new Size(chipW, 44);
            _chipPercent.Title = "CARGA";
            _chipPercent.ValueText = "--%";
            _chipPercent.AccentColor = _accentColor;

            _chipUsed = new StatChip();
            _chipUsed.Location = new Point(14 + (chipW + 8), 184);
            _chipUsed.Size = new Size(chipW, 44);
            _chipUsed.Title = "EN USO";
            _chipUsed.ValueText = "-- GB";
            _chipUsed.AccentColor = Color.FromArgb(245, 158, 11);

            _chipFree = new StatChip();
            _chipFree.Location = new Point(14 + (chipW + 8) * 2, 184);
            _chipFree.Size = new Size(chipW, 44);
            _chipFree.Title = "LIBRE";
            _chipFree.ValueText = "-- GB";
            _chipFree.AccentColor = _accentColor;

            _chipTotal = new StatChip();
            _chipTotal.Location = new Point(14 + (chipW + 8) * 3, 184);
            _chipTotal.Size = new Size(chipW, 44);
            _chipTotal.Title = "TOTAL";
            _chipTotal.ValueText = "-- GB";
            _chipTotal.AccentColor = Color.FromArgb(129, 140, 248);

            // Telemetría con símbolo de reloj: CPU %, grados, GPU %, grados y RAM
            _lblTelemetry = new Label();
            _lblTelemetry.Location = new Point(14, 238);
            _lblTelemetry.Size = new Size(contentW - 28, 24);
            _lblTelemetry.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            _lblTelemetry.ForeColor = Color.FromArgb(148, 163, 184);
            _lblTelemetry.BackColor = Color.Transparent;
            _lblTelemetry.Text = "⏱ CPU: --% (0°C)   •   GPU: --% (0°C)   •   RAM: -- GB / -- GB (--%)";

            _cardMeter.Controls.Add(_taskMgrGraph);
            _cardMeter.Controls.Add(_chipPercent);
            _cardMeter.Controls.Add(_chipUsed);
            _cardMeter.Controls.Add(_chipFree);
            _cardMeter.Controls.Add(_chipTotal);
            _cardMeter.Controls.Add(_lblTelemetry);

            // =====================================================================
            // 3. BOTÓN PRINCIPAL DE OPTIMIZACIÓN
            // =====================================================================
            _btnOptimize = new FluentButton();
            _btnOptimize.Text = "⚡ OPTIMIZAR MEMORIA AHORA";
            _btnOptimize.IsPrimary = true;
            _btnOptimize.AccentColor = _accentColor;
            _btnOptimize.Location = new Point(marginX, 345);
            _btnOptimize.Size = new Size(contentW, 44);
            _btnOptimize.Click += BtnOptimize_Click;

            // =====================================================================
            // 4. REGISTRO DE ACTIVIDAD (DEBAJO DE LA GRÁFICA / BOTÓN)
            // =====================================================================
            _cardLog = new FluentCard();
            _cardLog.Location = new Point(marginX, 397);
            _cardLog.Size = new Size(contentW, 160);
            _cardLog.HeaderIcon = "▶";
            _cardLog.HeaderTitle = "Registro de actividad";
            _cardLog.AccentColor = _accentColor;

            _logBox = new FluentLogBox();
            _logBox.Location = new Point(10, 32);
            _logBox.Size = new Size(contentW - 20, 118);
            _logBox.AccentColor = _accentColor;

            _cardLog.Controls.Add(_logBox);

            // =====================================================================
            // 5. BARRA DESPLEGABLE DE AJUSTES
            // =====================================================================
            _btnToggleSettings = new FluentButton();
            _btnToggleSettings.IsPrimary = false;
            _btnToggleSettings.AccentColor = _accentColor;
            _btnToggleSettings.Location = new Point(marginX, 565);
            _btnToggleSettings.Size = new Size(contentW, 36);
            _btnToggleSettings.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            _btnToggleSettings.Click += delegate { ToggleSettingsPanel(); };

            // =====================================================================
            // 6. PANEL DESPLEGABLE: COMPONENTES A OPTIMIZAR Y AUTOMATIZACIÓN
            // =====================================================================
            _cardSettings = new FluentCard();
            _cardSettings.Location = new Point(marginX, 609);
            _cardSettings.Size = new Size(contentW, 245);
            _cardSettings.HeaderIcon = "⚙";
            _cardSettings.HeaderTitle = "Componentes y Automatización";
            _cardSettings.AccentColor = _accentColor;

            // Sección 1: Componentes a optimizar
            _togWorkingSets = new FluentToggle();
            _togWorkingSets.Label = "Memoria de aplicaciones en segundo plano";
            _togWorkingSets.TrackActiveColor = _accentColor;
            _togWorkingSets.Location = new Point(16, 36);
            _togWorkingSets.Size = new Size(contentW - 32, 20);

            _togTempFiles = new FluentToggle();
            _togTempFiles.Label = "Archivos temporales del sistema y cachés";
            _togTempFiles.TrackActiveColor = _accentColor;
            _togTempFiles.Location = new Point(16, 58);
            _togTempFiles.Size = new Size(contentW - 32, 20);

            _togRecycleBin = new FluentToggle();
            _togRecycleBin.Label = "Papelera de reciclaje";
            _togRecycleBin.TrackActiveColor = _accentColor;
            _togRecycleBin.Location = new Point(16, 80);
            _togRecycleBin.Size = new Size(contentW - 32, 20);

            _togFlushDns = new FluentToggle();
            _togFlushDns.Label = "Purgar caché del resolvedor DNS";
            _togFlushDns.TrackActiveColor = _accentColor;
            _togFlushDns.Location = new Point(16, 102);
            _togFlushDns.Size = new Size(contentW - 32, 20);

            // Sub-encabezado Automatización
            Label lblAutoHeader = new Label();
            lblAutoHeader.Text = "AUTOMATIZACIÓN INTELIGENTE";
            lblAutoHeader.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
            lblAutoHeader.ForeColor = Color.FromArgb(148, 163, 184);
            lblAutoHeader.Location = new Point(16, 128);
            lblAutoHeader.AutoSize = true;
            lblAutoHeader.BackColor = Color.Transparent;

            // Umbral
            _togAutoThreshold = new FluentToggle();
            _togAutoThreshold.Label = "Auto-limpiar al superar:";
            _togAutoThreshold.TrackActiveColor = _accentColor;
            _togAutoThreshold.Location = new Point(16, 150);
            _togAutoThreshold.Size = new Size(185, 20);

            _numThreshold = new NumericUpDown();
            _numThreshold.Location = new Point(206, 150);
            _numThreshold.Size = new Size(46, 20);
            _numThreshold.Minimum = 50;
            _numThreshold.Maximum = 98;
            _numThreshold.Value = 85;
            _numThreshold.BackColor = Color.FromArgb(24, 28, 42);
            _numThreshold.ForeColor = Color.White;
            _numThreshold.BorderStyle = BorderStyle.FixedSingle;

            Label lblPercentSign = new Label();
            lblPercentSign.Text = "%";
            lblPercentSign.Location = new Point(256, 152);
            lblPercentSign.AutoSize = true;
            lblPercentSign.BackColor = Color.Transparent;
            lblPercentSign.ForeColor = Color.FromArgb(203, 213, 225);

            // Temporizador
            _togAutoTimer = new FluentToggle();
            _togAutoTimer.Label = "Auto-limpiar periódicamente cada:";
            _togAutoTimer.TrackActiveColor = _accentColor;
            _togAutoTimer.Location = new Point(16, 176);
            _togAutoTimer.Size = new Size(248, 20);

            _numTimerMinutes = new NumericUpDown();
            _numTimerMinutes.Location = new Point(268, 176);
            _numTimerMinutes.Size = new Size(46, 20);
            _numTimerMinutes.Minimum = 5;
            _numTimerMinutes.Maximum = 360;
            _numTimerMinutes.Value = 30;
            _numTimerMinutes.BackColor = Color.FromArgb(24, 28, 42);
            _numTimerMinutes.ForeColor = Color.White;
            _numTimerMinutes.BorderStyle = BorderStyle.FixedSingle;

            Label lblMinSign = new Label();
            lblMinSign.Text = "min";
            lblMinSign.Location = new Point(318, 178);
            lblMinSign.AutoSize = true;
            lblMinSign.BackColor = Color.Transparent;
            lblMinSign.ForeColor = Color.FromArgb(203, 213, 225);

            // Iniciar con Windows
            _togStartWithWindows = new FluentToggle();
            _togStartWithWindows.Label = "Iniciar con Windows minimizado en segundo plano";
            _togStartWithWindows.TrackActiveColor = _accentColor;
            _togStartWithWindows.Location = new Point(16, 202);
            _togStartWithWindows.Size = new Size(contentW - 32, 20);

            _cardSettings.Controls.Add(_togWorkingSets);
            _cardSettings.Controls.Add(_togTempFiles);
            _cardSettings.Controls.Add(_togRecycleBin);
            _cardSettings.Controls.Add(_togFlushDns);
            _cardSettings.Controls.Add(lblAutoHeader);
            _cardSettings.Controls.Add(_togAutoThreshold);
            _cardSettings.Controls.Add(_numThreshold);
            _cardSettings.Controls.Add(lblPercentSign);
            _cardSettings.Controls.Add(_togAutoTimer);
            _cardSettings.Controls.Add(_numTimerMinutes);
            _cardSettings.Controls.Add(lblMinSign);
            _cardSettings.Controls.Add(_togStartWithWindows);

            // Agregar controles al Form
            this.Controls.Add(_headerPanel);
            this.Controls.Add(_cardMeter);
            this.Controls.Add(_btnOptimize);
            this.Controls.Add(_cardLog);
            this.Controls.Add(_btnToggleSettings);
            this.Controls.Add(_cardSettings);

            // BackgroundWorker
            _cleanerWorker = new BackgroundWorker();
            _cleanerWorker.WorkerReportsProgress = true;
            _cleanerWorker.DoWork += CleanerWorker_DoWork;
            _cleanerWorker.ProgressChanged += CleanerWorker_ProgressChanged;
            _cleanerWorker.RunWorkerCompleted += CleanerWorker_RunWorkerCompleted;

            // Enlazar eventos de guardado automático
            _togWorkingSets.CheckedChanged += delegate { SaveUiToSettings(); };
            _togTempFiles.CheckedChanged += delegate { SaveUiToSettings(); };
            _togRecycleBin.CheckedChanged += delegate { SaveUiToSettings(); };
            _togFlushDns.CheckedChanged += delegate { SaveUiToSettings(); };
            _togAutoThreshold.CheckedChanged += delegate { SaveUiToSettings(); };
            _numThreshold.ValueChanged += delegate { SaveUiToSettings(); };
            _togAutoTimer.CheckedChanged += delegate { SaveUiToSettings(); };
            _numTimerMinutes.ValueChanged += delegate { SaveUiToSettings(); };
            _togStartWithWindows.CheckedChanged += delegate { SaveUiToSettings(); };

            this.FormClosing += MainForm_FormClosing;
            Application.ApplicationExit += delegate { try { _settings.Save(); } catch { } };

            UpdateSettingsPanelState();
        }

        private void ToggleSettingsPanel()
        {
            _settings.SettingsExpanded = !_settings.SettingsExpanded;
            UpdateSettingsPanelState();
            SaveUiToSettings();
        }

        private void UpdateSettingsPanelState()
        {
            this.SuspendLayout();
            if (_settings.SettingsExpanded)
            {
                _cardSettings.Visible = true;
                _btnToggleSettings.Text = "⚙️ Componentes y Automatización   ▴";
                this.ClientSize = new Size(516, 866);
            }
            else
            {
                _cardSettings.Visible = false;
                _btnToggleSettings.Text = "⚙️ Componentes y Automatización   ▾";
                this.ClientSize = new Size(516, 616);
            }
            this.ResumeLayout(true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle rect = this.ClientRectangle;

            using (SolidBrush baseBg = new SolidBrush(Color.FromArgb(11, 14, 22)))
            {
                g.FillRectangle(baseBg, rect);
            }

            // Dibujar fondo watermark_bg en la ventana ensanchada
            if (_watermarkImage != null)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(_watermarkImage, rect);

                // Capa acrílica sutil para garantizar contraste perfecto
                using (SolidBrush overlay = new SolidBrush(Color.FromArgb(100, 11, 14, 22)))
                {
                    g.FillRectangle(overlay, rect);
                }
            }
        }

        private void HeaderPanel_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int avatarSize = 40;
            int avatarX = 18;
            int avatarY = 7;

            // Dibujar el icono original subido por el usuario (panda_clean) en estilo pixel art nítido
            if (_avatarLogo != null)
            {
                // Marco sutil redondeado para el avatar estilo cyberpunk
                using (GraphicsPath bgPath = FluentCard.CreateRoundedRectPath(new Rectangle(avatarX - 2, avatarY - 2, avatarSize + 4, avatarSize + 4), 6))
                {
                    using (SolidBrush chipBg = new SolidBrush(Color.FromArgb(60, 14, 20, 32)))
                    {
                        g.FillPath(chipBg, bgPath);
                    }
                    using (Pen chipBorder = new Pen(Color.FromArgb(90, _accentColor), 1f))
                    {
                        g.DrawPath(chipBorder, bgPath);
                    }
                }

                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(_avatarLogo, new Rectangle(avatarX, avatarY, avatarSize, avatarSize));
            }
            else
            {
                using (SolidBrush b = new SolidBrush(_accentColor))
                {
                    g.FillEllipse(b, avatarX, avatarY, avatarSize, avatarSize);
                }
            }

            // Título limpio: "Limpiador de RAM"
            int textX = avatarX + avatarSize + 14;
            using (Font titleFont = new Font("Segoe UI", 14f, FontStyle.Bold))
            using (SolidBrush titleBrush = new SolidBrush(_accentColor))
            {
                g.DrawString("Limpiador de RAM", titleFont, titleBrush, textX, 13);
            }

            // Borde inferior sutil
            using (Pen borderPen = new Pen(Color.FromArgb(25, 255, 255, 255), 1))
            {
                g.DrawLine(borderPen, 0, _headerPanel.Height - 1, _headerPanel.Width, _headerPanel.Height - 1);
            }
        }

        private void ApplySettingsToUi()
        {
            _isLoadingSettings = true;
            try
            {
                _togWorkingSets.Checked = _settings.CleanWorkingSets;
                _togTempFiles.Checked = _settings.CleanTempFiles;
                _togRecycleBin.Checked = _settings.EmptyRecycleBin;
                _togFlushDns.Checked = _settings.FlushDns;

                _togAutoThreshold.Checked = _settings.AutoCleanThresholdEnabled;
                if (_settings.AutoCleanThresholdPercent >= 50 && _settings.AutoCleanThresholdPercent <= 98)
                    _numThreshold.Value = _settings.AutoCleanThresholdPercent;

                _togAutoTimer.Checked = _settings.AutoCleanTimerEnabled;
                if (_settings.AutoCleanTimerMinutes >= 5 && _settings.AutoCleanTimerMinutes <= 360)
                    _numTimerMinutes.Value = _settings.AutoCleanTimerMinutes;

                _togStartWithWindows.Checked = _settings.StartWithWindows;

                UpdateSettingsPanelState();
            }
            finally
            {
                _isLoadingSettings = false;
            }
        }

        private void SaveUiToSettings()
        {
            if (_isLoadingSettings) return;

            _settings.CleanWorkingSets = _togWorkingSets.Checked;
            _settings.CleanTempFiles = _togTempFiles.Checked;
            _settings.EmptyRecycleBin = _togRecycleBin.Checked;
            _settings.FlushDns = _togFlushDns.Checked;

            _settings.AutoCleanThresholdEnabled = _togAutoThreshold.Checked;
            _settings.AutoCleanThresholdPercent = (int)_numThreshold.Value;

            _settings.AutoCleanTimerEnabled = _togAutoTimer.Checked;
            _settings.AutoCleanTimerMinutes = (int)_numTimerMinutes.Value;

            _settings.StartWithWindows = _togStartWithWindows.Checked;
            _settings.SettingsExpanded = (_cardSettings != null && _cardSettings.Visible);
            _settings.Save();
        }

        private void StartMonitoring()
        {
            _monitorTimer = new System.Windows.Forms.Timer();
            _monitorTimer.Interval = 1000;
            _monitorTimer.Tick += MonitorTimer_Tick;
            _monitorTimer.Start();
            UpdateRamUi();
        }

        [System.Runtime.InteropServices.DllImport("psapi.dll")]
        static extern int EmptyWorkingSet(IntPtr hwProc);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);

        public static void TrimAppMemory()
        {
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                SetProcessWorkingSetSize(System.Diagnostics.Process.GetCurrentProcess().Handle, (IntPtr)(-1), (IntPtr)(-1));
                EmptyWorkingSet(System.Diagnostics.Process.GetCurrentProcess().Handle);
            }
            catch { }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (this.Visible)
            {
                if (_monitorTimer != null) _monitorTimer.Interval = 1000;
                UpdateRamUi();
            }
            else
            {
                if (_monitorTimer != null) _monitorTimer.Interval = 3000;
                TrimAppMemory();
            }
        }

        private void MonitorTimer_Tick(object sender, EventArgs e)
        {
            UpdateRamUi();
        }

        public void UpdateRamUi()
        {
            RamInfo ram = MemoryManager.GetRamInfo();

            // Si la ventana está oculta (segundo plano), no repintar gráficos ni calcular CPU/GPU
            if (!this.Visible)
            {
                CheckAutoClean(ram);
                return;
            }

            // Actualizar gráfica en tiempo real (con escala GB y montañitas dinámicas)
            _taskMgrGraph.AddDataPoint((float)ram.UsedPhysicalGB, (float)ram.TotalPhysicalGB, (float)ram.MemoryLoadPercent);

            _chipPercent.ValueText = string.Format("{0}%", ram.MemoryLoadPercent);
            _chipUsed.ValueText = string.Format("{0:N1} GB", ram.UsedPhysicalGB);
            _chipFree.ValueText = string.Format("{0:N1} GB", ram.FreePhysicalGB);
            _chipTotal.ValueText = string.Format("{0:N1} GB", ram.TotalPhysicalGB);

            // Telemetría de hardware (CPU %, grados, GPU %, grados y RAM exacta)
            float cpu = HardwareTelemetry.GetCpuUsage();
            int cpuTemp = HardwareTelemetry.GetCpuTemp(cpu);
            float gpu = HardwareTelemetry.GetGpuUsage();
            int gpuTemp = HardwareTelemetry.GetGpuTemp();

            _lblTelemetry.Text = string.Format("⏱ CPU: {0:F0}% ({1}°C)   •   GPU: {2:F0}% ({3}°C)   •   RAM: {4:N1} / {5:N1} GB ({6}%)",
                cpu, cpuTemp, gpu, gpuTemp, ram.UsedPhysicalGB, ram.TotalPhysicalGB, ram.MemoryLoadPercent);

            CheckAutoClean(ram);
        }

        private void CheckAutoClean(RamInfo ram)
        {
            // Automatización: Umbral
            if (!_isOptimizing && _settings.AutoCleanThresholdEnabled)
            {
                if (ram.MemoryLoadPercent >= _settings.AutoCleanThresholdPercent)
                {
                    if ((DateTime.Now - _lastAutoClean).TotalMinutes >= 5)
                    {
                        Log(string.Format("[Auto] Umbral alcanzado ({0}% >= {1}%). Limpiando memoria...",
                            ram.MemoryLoadPercent, _settings.AutoCleanThresholdPercent));
                        TriggerOptimization(false);
                    }
                }
            }

            // Automatización: Temporizador
            if (!_isOptimizing && _settings.AutoCleanTimerEnabled)
            {
                if ((DateTime.Now - _lastAutoClean).TotalMinutes >= _settings.AutoCleanTimerMinutes)
                {
                    Log(string.Format("[Auto] Intervalo programado alcanzado ({0} min). Limpiando...",
                        _settings.AutoCleanTimerMinutes));
                    TriggerOptimization(false);
                }
            }
        }

        public void TriggerOptimization(bool userInitiated)
        {
            if (_isOptimizing) return;
            _isOptimizing = true;
            _lastAutoClean = DateTime.Now;

            _btnOptimize.Enabled = false;
            _btnOptimize.Text = "⏳ OPTIMIZANDO MEMORIA...";

            OptimizationOptions options = _settings.ToOptimizationOptions();
            _cleanerWorker.RunWorkerAsync(options);
        }

        private void BtnOptimize_Click(object sender, EventArgs e)
        {
            TriggerOptimization(true);
        }

        private void CleanerWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            OptimizationOptions options = (OptimizationOptions)e.Argument;
            OptimizationResult result = MemoryManager.Optimize(options, delegate(string msg)
            {
                _cleanerWorker.ReportProgress(0, msg);
            });
            e.Result = result;
        }

        private void CleanerWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            string msg = e.UserState as string;
            if (!string.IsNullOrEmpty(msg))
            {
                Log(msg);
            }
        }

        private void CleanerWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            _isOptimizing = false;
            _btnOptimize.Enabled = true;
            _btnOptimize.Text = "⚡ OPTIMIZAR MEMORIA AHORA";

            if (!this.Visible)
            {
                TrimAppMemory();
            }

            UpdateRamUi();

            if (e.Result is OptimizationResult)
            {
                OptimizationResult res = (OptimizationResult)e.Result;
                if (OptimizationCompleted != null)
                {
                    OptimizationCompleted(res, EventArgs.Empty);
                }
            }
        }

        public void Log(string message)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<string>(Log), message);
                return;
            }

            _logBox.AppendLog(message);
        }

        private Icon LoadAppIcon()
        {
            try
            {
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string localIco = System.IO.Path.Combine(exeDir, "app.ico");
                if (System.IO.File.Exists(localIco)) return new Icon(localIco);

                string resIco = System.IO.Path.Combine(exeDir, @"resources\app.ico");
                if (System.IO.File.Exists(resIco)) return new Icon(resIco);

                return SystemIcons.Application;
            }
            catch
            {
                return SystemIcons.Application;
            }
        }

        private bool _allowClose = false;
        public bool AllowClose
        {
            get { return _allowClose; }
            set { _allowClose = value; }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveUiToSettings();

            if (!_allowClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                TrimAppMemory();
            }
        }
    }
}
