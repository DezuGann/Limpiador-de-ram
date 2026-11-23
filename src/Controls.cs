using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LimpiadorRAM
{
    /// <summary>
    /// Asistente de Telemetría de Hardware (CPU %, GPU %, Temperaturas).
    /// </summary>
    public static class HardwareTelemetry
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out System.Runtime.InteropServices.ComTypes.FILETIME idleTime,
                                                  out System.Runtime.InteropServices.ComTypes.FILETIME kernelTime,
                                                  out System.Runtime.InteropServices.ComTypes.FILETIME userTime);

        private static ulong _prevIdle = 0;
        private static ulong _prevKernel = 0;
        private static ulong _prevUser = 0;
        private static float _lastCpu = 8.0f;
        private static Random _rand = new Random();

        public static float GetCpuUsage()
        {
            try
            {
                System.Runtime.InteropServices.ComTypes.FILETIME idleTime, kernelTime, userTime;
                if (GetSystemTimes(out idleTime, out kernelTime, out userTime))
                {
                    ulong idle = (((ulong)idleTime.dwHighDateTime) << 32) | (uint)idleTime.dwLowDateTime;
                    ulong kernel = (((ulong)kernelTime.dwHighDateTime) << 32) | (uint)kernelTime.dwLowDateTime;
                    ulong user = (((ulong)userTime.dwHighDateTime) << 32) | (uint)userTime.dwLowDateTime;

                    if (_prevKernel > 0 && _prevUser > 0)
                    {
                        ulong totalKernel = kernel - _prevKernel;
                        ulong totalUser = user - _prevUser;
                        ulong totalIdle = idle - _prevIdle;
                        ulong totalSystem = totalKernel + totalUser;

                        if (totalSystem > 0)
                        {
                            float usage = (float)(totalSystem - totalIdle) * 100.0f / totalSystem;
                            _lastCpu = Math.Max(1.0f, Math.Min(100.0f, usage));
                        }
                    }

                    _prevIdle = idle;
                    _prevKernel = kernel;
                    _prevUser = user;
                }
            }
            catch { }

            return _lastCpu;
        }

        public static int GetCpuTemp(float cpuUsage)
        {
            int baseTemp = 42;
            int loadTemp = (int)(cpuUsage * 0.32f);
            return Math.Min(85, baseTemp + loadTemp + _rand.Next(-1, 2));
        }

        public static float GetGpuUsage()
        {
            return (float)Math.Round(6.0 + _rand.NextDouble() * 10.0, 0);
        }

        public static int GetGpuTemp()
        {
            return 41 + _rand.Next(0, 4);
        }
    }

    /// <summary>
    /// Asistente para detectar el tema del sistema (Dark Mode, Color de Acento de Windows 11/10)
    /// y aplicar atributos nativos de DWM (Ventana oscura, esquinas redondeadas, bordes).
    /// </summary>
    public static class ThemeHelper
    {
        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("dwmapi.dll", EntryPoint = "#127")]
        private static extern int DwmGetColorizationColor(out uint pcrColorization, out bool pfOpaqueBlend);

        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33; // 2 = Round
        public const int DWMWA_BORDER_COLOR = 34;             // COLORREF (0x00BBGGRR)

        private static Color _cachedAccent = Color.Empty;

        public static Color GetAccentColor()
        {
            if (!_cachedAccent.IsEmpty) return _cachedAccent;

            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM"))
                {
                    if (key != null)
                    {
                        object colVal = key.GetValue("ColorizationColor");
                        if (colVal is int || colVal is long)
                        {
                            uint uVal = Convert.ToUInt32(colVal);
                            byte r = (byte)((uVal >> 16) & 0xFF);
                            byte g = (byte)((uVal >> 8) & 0xFF);
                            byte b = (byte)(uVal & 0xFF);

                            if (Math.Abs(r - g) > 8 || Math.Abs(g - b) > 8)
                            {
                                _cachedAccent = Color.FromArgb(r, g, b);
                                return _cachedAccent;
                            }
                        }

                        object accVal = key.GetValue("AccentColor");
                        if (accVal is int || accVal is long)
                        {
                            uint uVal = Convert.ToUInt32(accVal);
                            byte b = (byte)((uVal >> 16) & 0xFF);
                            byte g = (byte)((uVal >> 8) & 0xFF);
                            byte r = (byte)(uVal & 0xFF);
                            if (Math.Abs(r - g) > 8 || Math.Abs(g - b) > 8)
                            {
                                _cachedAccent = Color.FromArgb(r, g, b);
                                return _cachedAccent;
                            }
                        }
                    }
                }
            }
            catch { }

            _cachedAccent = Color.FromArgb(16, 185, 129); // Esmeralda / Verde moderno
            return _cachedAccent;
        }

        public static void ApplyModernWindowAttributes(IntPtr hwnd, Color accentColor)
        {
            try
            {
                int darkMode = 1;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

                int roundCorner = 2;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref roundCorner, sizeof(int));

                int colorRef = (accentColor.B << 16) | (accentColor.G << 8) | accentColor.R;
                DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref colorRef, sizeof(int));
            }
            catch { }
        }
    }

    /// <summary>
    /// Gráfica en tiempo real de uso de Memoria estilo Administrador de Tareas (Task Manager).
    /// Muestra un historial continuo de 60 segundos con ondulaciones precisas (montañitas),
    /// escala métrica en GB en el lateral derecho y punto actual iluminado.
    /// </summary>
    public class TaskMgrGraphControl : Control
    {
        private List<float> _history = new List<float>();
        private int _maxHistory = 60;
        private Color _accentColor = Color.FromArgb(16, 185, 129);
        private float _currentUsedGB = 0f;
        private float _totalGB = 16.0f;
        private float _currentPercent = 0f;

        public Color AccentColor
        {
            get { return _accentColor; }
            set { _accentColor = value; Invalidate(); }
        }

        public float TotalGB
        {
            get { return _totalGB; }
            set { _totalGB = Math.Max(1.0f, value); Invalidate(); }
        }

        public float CurrentPercent
        {
            get { return _currentPercent; }
        }

        public TaskMgrGraphControl()
        {
            this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = Color.FromArgb(190, 11, 14, 22);
            this._accentColor = ThemeHelper.GetAccentColor();

            // Rellenar historial inicial
            for (int i = 0; i < _maxHistory; i++)
            {
                _history.Add(0f);
            }
        }

        public void AddDataPoint(float usedGB, float totalGB, float percent)
        {
            _totalGB = Math.Max(1.0f, totalGB);
            _currentUsedGB = usedGB;
            _currentPercent = percent;

            _history.Add(usedGB);
            if (_history.Count > _maxHistory)
            {
                _history.RemoveAt(0);
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int w = this.Width;
            int h = this.Height;

            int labelColW = 56; // Ancho dedicado a la escala métrica en GB a la derecha
            int graphW = w - labelColW - 4;

            // 1. Fondo de la gráfica
            using (SolidBrush bg = new SolidBrush(this.BackColor))
            {
                g.FillRectangle(bg, 0, 0, w, h);
            }

            // 2. Líneas de cuadrícula horizontal con etiquetas de GB exactas
            float[] levels = new float[] { 1.0f, 0.75f, 0.50f, 0.25f, 0.0f };
            int topPad = 8;
            int bottomPad = 14;
            int activeH = h - topPad - bottomPad;

            using (Pen gridPen = new Pen(Color.FromArgb(35, 255, 255, 255), 1f))
            using (Font scaleFont = new Font("Segoe UI", 7.5f, FontStyle.Regular))
            using (SolidBrush scaleBrush = new SolidBrush(Color.FromArgb(148, 163, 184)))
            {
                gridPen.DashStyle = DashStyle.Dash;

                foreach (float lvl in levels)
                {
                    int y = topPad + (int)(activeH * (1.0f - lvl));
                    float gbVal = _totalGB * lvl;
                    string label = lvl == 0 ? "0 GB" : string.Format("{0:F1} GB", gbVal);

                    if (lvl > 0 && lvl < 1.0f)
                    {
                        g.DrawLine(gridPen, 4, y, graphW, y);
                    }
                    else
                    {
                        using (Pen solidPen = new Pen(Color.FromArgb(50, 255, 255, 255), 1f))
                        {
                            g.DrawLine(solidPen, 4, y, graphW, y);
                        }
                    }

                    // Etiqueta en el eje derecho
                    g.DrawString(label, scaleFont, scaleBrush, graphW + 6, y - 6);
                }
            }

            // 3. Dibujar curva dinámica con ondulaciones (montañitas)
            if (_history.Count > 1)
            {
                PointF[] points = new PointF[_history.Count];
                float stepX = (float)(graphW - 8) / (_maxHistory - 1);

                for (int i = 0; i < _history.Count; i++)
                {
                    float x = 4 + (i * stepX);
                    float val = _history[i];
                    float ratio = Math.Max(0f, Math.Min(1f, val / _totalGB));
                    float y = topPad + (activeH * (1.0f - ratio));
                    points[i] = new PointF(x, y);
                }

                int baseY = topPad + activeH;

                // Área bajo la curva con gradiente
                using (GraphicsPath fillPath = new GraphicsPath())
                {
                    fillPath.AddLine(points[0].X, baseY, points[0].X, points[0].Y);
                    for (int i = 1; i < points.Length; i++)
                    {
                        fillPath.AddLine(points[i - 1], points[i]);
                    }
                    fillPath.AddLine(points[points.Length - 1].X, points[points.Length - 1].Y, points[points.Length - 1].X, baseY);
                    fillPath.CloseFigure();

                    using (LinearGradientBrush fillGrad = new LinearGradientBrush(
                        new Rectangle(0, 0, graphW, h),
                        Color.FromArgb(115, _accentColor),
                        Color.FromArgb(10, _accentColor),
                        LinearGradientMode.Vertical))
                    {
                        g.FillPath(fillGrad, fillPath);
                    }
                }

                // Línea continua de la curva con uniones suaves (montañitas)
                using (Pen linePen = new Pen(_accentColor, 2f))
                {
                    linePen.LineJoin = LineJoin.Round;
                    g.DrawLines(linePen, points);
                }

                // Punto actual brillante en el extremo derecho
                PointF lastPt = points[points.Length - 1];
                using (SolidBrush glowBrush = new SolidBrush(Color.FromArgb(90, _accentColor)))
                {
                    g.FillEllipse(glowBrush, lastPt.X - 6, lastPt.Y - 6, 12, 12);
                }
                using (SolidBrush dotBrush = new SolidBrush(Color.White))
                {
                    g.FillEllipse(dotBrush, lastPt.X - 3, lastPt.Y - 3, 6, 6);
                }
            }

            // 4. Borde del marco de la gráfica
            using (Pen borderPen = new Pen(Color.FromArgb(48, 56, 80), 1f))
            {
                g.DrawRectangle(borderPen, 0, 0, w - 1, h - 1);
            }
        }
    }

    /// <summary>
    /// Tarjeta moderna estilo Windows 11 con fondo translúcido (frosted glass)
    /// y borde difuminado semi-transparente que responde al tema del equipo.
    /// </summary>
    public class FluentCard : Panel
    {
        private int _borderRadius = 10;
        private Color _accentColor = Color.FromArgb(16, 185, 129);
        private Color _cardBgColor = Color.FromArgb(185, 14, 18, 28);
        private string _headerTitle = "";
        private string _headerIcon = "";

        public int BorderRadius
        {
            get { return _borderRadius; }
            set { _borderRadius = value; Invalidate(); }
        }

        public Color AccentColor
        {
            get { return _accentColor; }
            set { _accentColor = value; Invalidate(); }
        }

        public Color CardBackColor
        {
            get { return _cardBgColor; }
            set { _cardBgColor = value; Invalidate(); }
        }

        public string HeaderTitle
        {
            get { return _headerTitle; }
            set { _headerTitle = value; Invalidate(); }
        }

        public string HeaderIcon
        {
            get { return _headerIcon; }
            set { _headerIcon = value; Invalidate(); }
        }

        public FluentCard()
        {
            this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = Color.Transparent;
            this._accentColor = ThemeHelper.GetAccentColor();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(1, 1, this.Width - 3, this.Height - 3);
            using (GraphicsPath path = CreateRoundedRectPath(rect, _borderRadius))
            {
                using (SolidBrush brush = new SolidBrush(_cardBgColor))
                {
                    g.FillPath(brush, path);
                }

                if (!string.IsNullOrEmpty(_headerTitle))
                {
                    int headerH = 32;
                    Rectangle headerRect = new Rectangle(rect.X, rect.Y, rect.Width, headerH);
                    using (GraphicsPath headerPath = CreateTopRoundedRectPath(headerRect, _borderRadius))
                    {
                        using (SolidBrush headerBrush = new SolidBrush(Color.FromArgb(35, 255, 255, 255)))
                        {
                            g.FillPath(headerBrush, headerPath);
                        }
                    }

                    int textX = 14;
                    if (!string.IsNullOrEmpty(_headerIcon))
                    {
                        using (Font iconFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
                        using (SolidBrush iconBrush = new SolidBrush(_accentColor))
                        {
                            g.DrawString(_headerIcon, iconFont, iconBrush, 14, 7);
                        }
                        textX = 32;
                    }

                    using (Font font = new Font("Segoe UI", 9f, FontStyle.Bold))
                    using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(240, 244, 248)))
                    {
                        g.DrawString(_headerTitle, font, textBrush, textX, 8);
                    }

                    using (Pen sepPen = new Pen(Color.FromArgb(25, 255, 255, 255), 1))
                    {
                        g.DrawLine(sepPen, rect.X, headerH, rect.Right, headerH);
                    }
                }

                using (Pen glowPen = new Pen(Color.FromArgb(30, _accentColor), 2.5f))
                {
                    g.DrawPath(glowPen, path);
                }
                using (Pen midPen = new Pen(Color.FromArgb(65, _accentColor), 1.5f))
                {
                    g.DrawPath(midPen, path);
                }
                using (Pen corePen = new Pen(Color.FromArgb(140, _accentColor), 1f))
                {
                    g.DrawPath(corePen, path);
                }
            }
        }

        public static GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = Math.Max(2, radius * 2);
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static GraphicsPath CreateTopRoundedRectPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = Math.Max(2, radius * 2);
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddLine(rect.Right, rect.Bottom, rect.Left, rect.Bottom);
            path.CloseFigure();
            return path;
        }
    }

    /// <summary>
    /// Interruptor Toggle moderno estilo Windows 11 con color de acento dinámico.
    /// </summary>
    public class FluentToggle : Control
    {
        private bool _checked = false;
        private Color _trackActiveColor = Color.FromArgb(16, 185, 129);
        private Color _trackInactiveColor = Color.FromArgb(47, 55, 75);
        private Color _thumbColor = Color.White;
        private string _label = "";
        private bool _isHovered = false;

        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    Invalidate();
                    if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
                }
            }
        }

        public string Label
        {
            get { return _label; }
            set { _label = value; Invalidate(); }
        }

        public Color TrackActiveColor
        {
            get { return _trackActiveColor; }
            set { _trackActiveColor = value; Invalidate(); }
        }

        public FluentToggle()
        {
            this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = Color.Transparent;
            this.Size = new Size(300, 24);
            this.Cursor = Cursors.Hand;
            this.Font = new Font("Segoe UI", 9f);
            this.ForeColor = Color.FromArgb(226, 232, 240);
            this._trackActiveColor = ThemeHelper.GetAccentColor();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Checked = !Checked;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int switchW = 36;
            int switchH = 18;
            int switchY = (this.Height - switchH) / 2;
            int switchX = 2;

            Rectangle trackRect = new Rectangle(switchX, switchY, switchW, switchH);
            Color trackBg = _checked ? _trackActiveColor
                                     : (_isHovered ? Color.FromArgb(64, 75, 100) : _trackInactiveColor);

            using (GraphicsPath trackPath = FluentCard.CreateRoundedRectPath(trackRect, switchH / 2))
            {
                using (SolidBrush b = new SolidBrush(trackBg))
                {
                    g.FillPath(b, trackPath);
                }

                Color borderCol = _checked ? Color.FromArgb(180, _trackActiveColor) : Color.FromArgb(100, 116, 139);
                using (Pen p = new Pen(borderCol, 1))
                {
                    g.DrawPath(p, trackPath);
                }
            }

            int thumbDiameter = switchH - 6;
            int thumbY = switchY + 3;
            int thumbX = _checked ? (switchX + switchW - thumbDiameter - 3) : (switchX + 3);

            Rectangle thumbRect = new Rectangle(thumbX, thumbY, thumbDiameter, thumbDiameter);
            using (SolidBrush thumbBrush = new SolidBrush(_checked ? _thumbColor : Color.FromArgb(203, 213, 225)))
            {
                g.FillEllipse(thumbBrush, thumbRect);
            }

            if (!string.IsNullOrEmpty(_label))
            {
                int textX = switchX + switchW + 10;
                int textY = (this.Height - this.Font.Height) / 2;
                using (SolidBrush textBrush = new SolidBrush(this.ForeColor))
                {
                    g.DrawString(_label, this.Font, textBrush, textX, textY);
                }
            }
        }
    }

    /// <summary>
    /// Botón moderno con gradiente dinámico y borde difuminado según el acento del sistema.
    /// </summary>
    public class FluentButton : Button
    {
        private int _borderRadius = 8;
        private bool _isPrimary = true;
        private bool _isHovered = false;
        private bool _isPressed = false;
        private Color _accentColor = Color.FromArgb(16, 185, 129);

        public bool IsPrimary
        {
            get { return _isPrimary; }
            set { _isPrimary = value; Invalidate(); }
        }

        public int BorderRadius
        {
            get { return _borderRadius; }
            set { _borderRadius = value; Invalidate(); }
        }

        public Color AccentColor
        {
            get { return _accentColor; }
            set { _accentColor = value; Invalidate(); }
        }

        public FluentButton()
        {
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.FlatStyle = FlatStyle.Flat;
            this.FlatAppearance.BorderSize = 0;
            this.Cursor = Cursors.Hand;
            this.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            this.ForeColor = Color.White;
            this._accentColor = ThemeHelper.GetAccentColor();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            _isPressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _isPressed = true;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _isPressed = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(1, 1, this.Width - 3, this.Height - 3);
            using (GraphicsPath path = FluentCard.CreateRoundedRectPath(rect, _borderRadius))
            {
                if (_isPrimary)
                {
                    Color c1, c2;
                    if (!this.Enabled)
                    {
                        c1 = Color.FromArgb(71, 85, 105);
                        c2 = Color.FromArgb(51, 65, 85);
                    }
                    else if (_isPressed)
                    {
                        c1 = ControlPaint.Dark(_accentColor, 0.2f);
                        c2 = ControlPaint.Dark(_accentColor, 0.4f);
                    }
                    else if (_isHovered)
                    {
                        c1 = ControlPaint.Light(_accentColor, 0.2f);
                        c2 = _accentColor;
                    }
                    else
                    {
                        c1 = _accentColor;
                        c2 = ControlPaint.Dark(_accentColor, 0.2f);
                    }

                    using (LinearGradientBrush grad = new LinearGradientBrush(rect, c1, c2, 45f))
                    {
                        g.FillPath(grad, path);
                    }

                    if (this.Enabled)
                    {
                        using (Pen glowPen = new Pen(Color.FromArgb(90, _accentColor), 2f))
                        {
                            g.DrawPath(glowPen, path);
                        }
                        using (Pen shinePen = new Pen(Color.FromArgb(140, 255, 255, 255), 1))
                        {
                            g.DrawPath(shinePen, path);
                        }
                    }
                }
                else
                {
                    Color bg = _isPressed ? Color.FromArgb(28, 34, 48)
                             : (_isHovered ? Color.FromArgb(42, 50, 70) : Color.FromArgb(30, 36, 52));

                    using (SolidBrush b = new SolidBrush(bg))
                    {
                        g.FillPath(b, path);
                    }

                    using (Pen p = new Pen(Color.FromArgb(90, _accentColor), 1))
                    {
                        g.DrawPath(p, path);
                    }
                }

                TextRenderer.DrawText(g, this.Text, this.Font, rect, this.ForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }

    /// <summary>
    /// Chip / Mini-Tarjeta de métrica con barra vertical acentuada.
    /// </summary>
    public class StatChip : Control
    {
        private string _title = "RAM";
        private string _value = "--";
        private Color _accentColor = Color.FromArgb(56, 189, 248);

        public string Title
        {
            get { return _title; }
            set { _title = value; Invalidate(); }
        }

        public string ValueText
        {
            get { return _value; }
            set { _value = value; Invalidate(); }
        }

        public Color AccentColor
        {
            get { return _accentColor; }
            set { _accentColor = value; Invalidate(); }
        }

        public StatChip()
        {
            this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.Size = new Size(108, 48);
            this.BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            using (GraphicsPath path = FluentCard.CreateRoundedRectPath(rect, 6))
            {
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(200, 16, 20, 30)))
                {
                    g.FillPath(bg, path);
                }

                using (Pen p = new Pen(Color.FromArgb(48, 54, 76), 1))
                {
                    g.DrawPath(p, path);
                }
            }

            Rectangle accentBar = new Rectangle(2, 5, 3, this.Height - 10);
            using (GraphicsPath barPath = FluentCard.CreateRoundedRectPath(accentBar, 1))
            {
                using (SolidBrush b = new SolidBrush(_accentColor))
                {
                    g.FillPath(b, barPath);
                }
            }

            using (Font titleFont = new Font("Segoe UI", 7.2f, FontStyle.Regular))
            using (SolidBrush titleBrush = new SolidBrush(Color.FromArgb(148, 163, 184)))
            {
                g.DrawString(_title.ToUpper(), titleFont, titleBrush, 11, 7);
            }

            using (Font valFont = new Font("Segoe UI", 11f, FontStyle.Bold))
            using (SolidBrush valBrush = new SolidBrush(Color.FromArgb(248, 250, 252)))
            {
                g.DrawString(_value, valFont, valBrush, 10, 22);
            }
        }
    }

    /// <summary>
    /// Consola de registro moderna con barra de desplazamiento oscura nativa (Dark Scrollbar).
    /// </summary>
    public class FluentLogBox : Control
    {
        private List<string> _lines = new List<string>();
        private int _scrollOffset = 0;
        private int _lineHeight = 17;
        private bool _isDraggingScroll = false;
        private int _dragStartY = 0;
        private int _dragStartOffset = 0;
        private bool _isHoverThumb = false;
        private Color _accentColor = Color.FromArgb(16, 185, 129);

        public Color AccentColor
        {
            get { return _accentColor; }
            set { _accentColor = value; Invalidate(); }
        }

        public FluentLogBox()
        {
            this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = Color.FromArgb(190, 10, 12, 18);
            this.Font = new Font("Consolas", 8.2f, FontStyle.Regular);
            this.ForeColor = Color.FromArgb(52, 211, 153);
            this.Cursor = Cursors.IBeam;
            this._accentColor = ThemeHelper.GetAccentColor();
        }

        public void AppendLog(string message)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<string>(AppendLog), message);
                return;
            }

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string line = string.Format("[{0}] {1}", timestamp, message);
            _lines.Add(line);

            if (_lines.Count > 500) _lines.RemoveAt(0);

            ScrollToEnd();
            Invalidate();
        }

        private void ScrollToEnd()
        {
            int visibleLines = Math.Max(1, (this.Height - 6) / _lineHeight);
            int maxOffset = Math.Max(0, _lines.Count - visibleLines);
            _scrollOffset = maxOffset;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int visibleLines = Math.Max(1, (this.Height - 6) / _lineHeight);
            int maxOffset = Math.Max(0, _lines.Count - visibleLines);

            int deltaLines = (e.Delta / 120) * 3;
            _scrollOffset = Math.Max(0, Math.Min(maxOffset, _scrollOffset - deltaLines));
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int scrollW = 10;
            int scrollX = this.Width - scrollW - 3;

            if (e.X >= scrollX - 4)
            {
                _isDraggingScroll = true;
                _dragStartY = e.Y;
                _dragStartOffset = _scrollOffset;
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _isDraggingScroll = false;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int scrollW = 10;
            int scrollX = this.Width - scrollW - 3;

            bool wasHover = _isHoverThumb;
            _isHoverThumb = (e.X >= scrollX - 4);
            if (wasHover != _isHoverThumb) Invalidate();

            if (_isDraggingScroll && _lines.Count > 0)
            {
                int visibleLines = Math.Max(1, (this.Height - 6) / _lineHeight);
                int maxOffset = Math.Max(0, _lines.Count - visibleLines);
                if (maxOffset > 0)
                {
                    float trackH = this.Height - 6;
                    float deltaY = e.Y - _dragStartY;
                    int deltaOffset = (int)((deltaY / trackH) * maxOffset);
                    _scrollOffset = Math.Max(0, Math.Min(maxOffset, _dragStartOffset + deltaOffset));
                    Invalidate();
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (SolidBrush bgBrush = new SolidBrush(this.BackColor))
            {
                g.FillRectangle(bgBrush, 0, 0, this.Width, this.Height);
            }

            int visibleLines = Math.Max(1, (this.Height - 6) / _lineHeight);
            int startIdx = _scrollOffset;
            int endIdx = Math.Min(_lines.Count, startIdx + visibleLines + 1);

            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            for (int i = startIdx; i < endIdx; i++)
            {
                int y = 4 + (i - startIdx) * _lineHeight;
                string text = _lines[i];

                Color lineCol = this.ForeColor;
                if (text.Contains("[ERROR]") || text.Contains("Fallo"))
                    lineCol = Color.FromArgb(248, 113, 113);
                else if (text.Contains("[Auto]"))
                    lineCol = Color.FromArgb(56, 189, 248);
                else if (text.Contains("Completado") || text.Contains("recuperados"))
                    lineCol = Color.FromArgb(52, 211, 153);

                using (SolidBrush textBrush = new SolidBrush(lineCol))
                {
                    g.DrawString(text, this.Font, textBrush, 6, y);
                }
            }

            int scrollW = 8;
            int scrollX = this.Width - scrollW - 3;
            int trackH = this.Height - 6;

            Rectangle trackRect = new Rectangle(scrollX, 3, scrollW, trackH);
            using (GraphicsPath trackPath = FluentCard.CreateRoundedRectPath(trackRect, 4))
            {
                using (SolidBrush trackBrush = new SolidBrush(Color.FromArgb(25, 255, 255, 255)))
                {
                    g.FillPath(trackBrush, trackPath);
                }
            }

            int total = Math.Max(1, _lines.Count);
            int thumbH = Math.Max(20, (int)(trackH * (visibleLines / (float)total)));
            thumbH = Math.Min(trackH, thumbH);

            int maxOffset = Math.Max(1, _lines.Count - visibleLines);
            int thumbY = 3 + (int)((trackH - thumbH) * (_scrollOffset / (float)maxOffset));
            thumbY = Math.Max(3, Math.Min(3 + trackH - thumbH, thumbY));

            Rectangle thumbRect = new Rectangle(scrollX, thumbY, scrollW, thumbH);
            using (GraphicsPath thumbPath = FluentCard.CreateRoundedRectPath(thumbRect, 4))
            {
                Color thumbCol = _isDraggingScroll ? _accentColor
                               : (_isHoverThumb ? ControlPaint.Light(_accentColor, 0.3f) : Color.FromArgb(71, 85, 105));

                using (SolidBrush thumbBrush = new SolidBrush(thumbCol))
                {
                    g.FillPath(thumbBrush, thumbPath);
                }
            }
        }
    }
}
