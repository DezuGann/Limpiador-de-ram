using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LimpiadorRAM.Installer
{
    static class SetupProgram
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm());
        }
    }

    public class SetupForm : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private TextBox _txtPath;
        private Button _btnBrowse;
        private CheckBox _chkDesktop;
        private CheckBox _chkStartMenu;
        private CheckBox _chkStartWithWindows;
        private CheckBox _chkLaunchNow;
        private Button _btnInstall;
        private Button _btnCancel;
        private ProgressBar _progressBar;
        private Label _lblStatus;
        private bool _isCompleted = false;

        private Color _accentColor = Color.FromArgb(16, 185, 129);
        private Image _avatarLogo = null;

        public SetupForm()
        {
            InitializeComponent();
            ApplyDarkMode();
            ExtractHeaderLogo();
        }

        private void ApplyDarkMode()
        {
            try
            {
                int darkMode = 1;
                DwmSetWindowAttribute(this.Handle, 20, ref darkMode, sizeof(int));
                int corner = 2;
                DwmSetWindowAttribute(this.Handle, 33, ref corner, sizeof(int));
            }
            catch { }
        }

        private void ExtractHeaderLogo()
        {
            try
            {
                Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload.panda_clean.png");
                if (stream != null)
                {
                    _avatarLogo = Image.FromStream(stream);
                }
            }
            catch { }
        }

        private void InitializeComponent()
        {
            this.Text = "Instalador - Limpiador de RAM";
            this.ClientSize = new Size(520, 420);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(14, 18, 28);
            this.ForeColor = Color.FromArgb(240, 244, 248);
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            try
            {
                Stream icoStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload.app.ico");
                if (icoStream != null) this.Icon = new Icon(icoStream);
            }
            catch { }

            // 1. Encabezado
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 70;
            header.BackColor = Color.FromArgb(20, 26, 40);
            header.Paint += Header_Paint;

            // 2. Ruta de instalación
            Label lblDest = new Label();
            lblDest.Text = "Carpeta de destino:";
            lblDest.Location = new Point(24, 88);
            lblDest.AutoSize = true;
            lblDest.ForeColor = Color.FromArgb(203, 213, 225);

            string defaultDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\LimpiadorRAM");

            _txtPath = new TextBox();
            _txtPath.Location = new Point(24, 112);
            _txtPath.Size = new Size(370, 25);
            _txtPath.BackColor = Color.FromArgb(26, 32, 50);
            _txtPath.ForeColor = Color.White;
            _txtPath.BorderStyle = BorderStyle.FixedSingle;
            _txtPath.Text = defaultDir;

            _btnBrowse = new Button();
            _btnBrowse.Text = "Examinar...";
            _btnBrowse.Location = new Point(404, 110);
            _btnBrowse.Size = new Size(90, 28);
            _btnBrowse.FlatStyle = FlatStyle.Flat;
            _btnBrowse.FlatAppearance.BorderColor = Color.FromArgb(60, 75, 105);
            _btnBrowse.BackColor = Color.FromArgb(32, 40, 62);
            _btnBrowse.ForeColor = Color.White;
            _btnBrowse.Click += BtnBrowse_Click;

            // 3. Opciones adicionales
            Label lblOptions = new Label();
            lblOptions.Text = "Opciones adicionales:";
            lblOptions.Location = new Point(24, 154);
            lblOptions.AutoSize = true;
            lblOptions.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            lblOptions.ForeColor = _accentColor;

            _chkDesktop = new CheckBox();
            _chkDesktop.Text = "Crear acceso directo en el Escritorio";
            _chkDesktop.Location = new Point(26, 180);
            _chkDesktop.Size = new Size(460, 24);
            _chkDesktop.Checked = true;

            _chkStartMenu = new CheckBox();
            _chkStartMenu.Text = "Crear acceso directo en el Menú Inicio";
            _chkStartMenu.Location = new Point(26, 206);
            _chkStartMenu.Size = new Size(460, 24);
            _chkStartMenu.Checked = true;

            _chkStartWithWindows = new CheckBox();
            _chkStartWithWindows.Text = "Iniciar automáticamente con Windows en segundo plano";
            _chkStartWithWindows.Location = new Point(26, 232);
            _chkStartWithWindows.Size = new Size(460, 24);
            _chkStartWithWindows.Checked = false;

            _chkLaunchNow = new CheckBox();
            _chkLaunchNow.Text = "Ejecutar Limpiador de RAM al finalizar la instalación";
            _chkLaunchNow.Location = new Point(26, 258);
            _chkLaunchNow.Size = new Size(460, 24);
            _chkLaunchNow.Checked = true;

            // 4. Barra de progreso y estado
            _lblStatus = new Label();
            _lblStatus.Text = "Listo para instalar.";
            _lblStatus.Location = new Point(24, 300);
            _lblStatus.Size = new Size(470, 20);
            _lblStatus.ForeColor = Color.FromArgb(148, 163, 184);

            _progressBar = new ProgressBar();
            _progressBar.Location = new Point(24, 324);
            _progressBar.Size = new Size(470, 18);
            _progressBar.Style = ProgressBarStyle.Blocks;
            _progressBar.Value = 0;

            // 5. Botones de acción
            _btnCancel = new Button();
            _btnCancel.Text = "Cancelar";
            _btnCancel.Location = new Point(296, 362);
            _btnCancel.Size = new Size(94, 34);
            _btnCancel.FlatStyle = FlatStyle.Flat;
            _btnCancel.FlatAppearance.BorderColor = Color.FromArgb(60, 75, 105);
            _btnCancel.BackColor = Color.FromArgb(32, 40, 62);
            _btnCancel.ForeColor = Color.White;
            _btnCancel.Click += delegate { this.Close(); };

            _btnInstall = new Button();
            _btnInstall.Text = "Instalar";
            _btnInstall.Location = new Point(400, 362);
            _btnInstall.Size = new Size(94, 34);
            _btnInstall.FlatStyle = FlatStyle.Flat;
            _btnInstall.FlatAppearance.BorderColor = _accentColor;
            _btnInstall.BackColor = _accentColor;
            _btnInstall.ForeColor = Color.FromArgb(10, 14, 22);
            _btnInstall.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            _btnInstall.Click += BtnInstall_Click;

            this.Controls.Add(header);
            this.Controls.Add(lblDest);
            this.Controls.Add(_txtPath);
            this.Controls.Add(_btnBrowse);
            this.Controls.Add(lblOptions);
            this.Controls.Add(_chkDesktop);
            this.Controls.Add(_chkStartMenu);
            this.Controls.Add(_chkStartWithWindows);
            this.Controls.Add(_chkLaunchNow);
            this.Controls.Add(_lblStatus);
            this.Controls.Add(_progressBar);
            this.Controls.Add(_btnCancel);
            this.Controls.Add(_btnInstall);
        }

        private void Header_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int iconSize = 44;
            int iconX = 24;
            int iconY = 13;

            if (_avatarLogo != null)
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.DrawImage(_avatarLogo, new Rectangle(iconX, iconY, iconSize, iconSize));
            }
            else
            {
                using (SolidBrush b = new SolidBrush(_accentColor))
                {
                    g.FillEllipse(b, iconX, iconY, iconSize, iconSize);
                }
            }

            int textX = iconX + iconSize + 14;
            using (Font titleFont = new Font("Segoe UI", 12.5f, FontStyle.Bold))
            using (SolidBrush titleBrush = new SolidBrush(Color.White))
            {
                g.DrawString("Instalador de Limpiador de RAM", titleFont, titleBrush, textX, 14);
            }

            using (Font subFont = new Font("Segoe UI", 8.5f, FontStyle.Regular))
            using (SolidBrush subBrush = new SolidBrush(Color.FromArgb(148, 163, 184)))
            {
                g.DrawString("Versión 1.0.0", subFont, subBrush, textX, 38);
            }

            using (Pen borderPen = new Pen(Color.FromArgb(30, 255, 255, 255), 1))
            {
                g.DrawLine(borderPen, 0, 69, 520, 69);
            }
        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Selecciona la carpeta de instalación:";
                fbd.SelectedPath = _txtPath.Text;
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    _txtPath.Text = fbd.SelectedPath;
                }
            }
        }

        private void BtnInstall_Click(object sender, EventArgs e)
        {
            if (_isCompleted)
            {
                if (_chkLaunchNow.Checked)
                {
                    string targetExe = Path.Combine(_txtPath.Text, "LimpiadorRAM.exe");
                    if (File.Exists(targetExe))
                    {
                        Process.Start(new ProcessStartInfo(targetExe) { UseShellExecute = true });
                    }
                }
                this.Close();
                return;
            }

            string installDir = _txtPath.Text.Trim();
            if (string.IsNullOrEmpty(installDir))
            {
                MessageBox.Show("Por favor especifica una ruta válida.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                installDir = Path.GetFullPath(installDir);
                string root = Path.GetPathRoot(installDir);
                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

                // Seguridad: Validar que no se instale directamente en la raíz de un disco o carpetas críticas del sistema
                if (string.Equals(installDir.TrimEnd('\\', '/'), root.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Por seguridad, no se puede instalar directamente en la raíz de una unidad (ej. C:\\ o D:\\).\nPor favor especifica una subcarpeta.", "Ruta no permitida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!string.IsNullOrEmpty(winDir) && installDir.StartsWith(winDir, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Por seguridad, no se puede instalar dentro del directorio de Windows.", "Ruta no permitida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ruta no válida: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _btnInstall.Enabled = false;
            _btnBrowse.Enabled = false;
            _txtPath.Enabled = false;
            _chkDesktop.Enabled = false;
            _chkStartMenu.Enabled = false;
            _chkStartWithWindows.Enabled = false;

            try
            {
                _lblStatus.Text = "Cerrando instancias previas si están activas...";
                _progressBar.Value = 15;
                Application.DoEvents();

                // 1. Cerrar LimpiadorRAM si está ejecutándose
                foreach (Process p in Process.GetProcessesByName("LimpiadorRAM"))
                {
                    try { p.Kill(); p.WaitForExit(1000); } catch { }
                }

                // 2. Crear carpetas
                _lblStatus.Text = "Creando directorios...";
                _progressBar.Value = 30;
                Application.DoEvents();

                string resDir = Path.Combine(installDir, "resources");
                if (!Directory.Exists(installDir)) Directory.CreateDirectory(installDir);
                if (!Directory.Exists(resDir)) Directory.CreateDirectory(resDir);

                // 3. Extraer archivos embebidos
                _lblStatus.Text = "Extrayendo ejecutables y recursos...";
                _progressBar.Value = 50;
                Application.DoEvents();

                ExtractResource("Payload.LimpiadorRAM.exe", Path.Combine(installDir, "LimpiadorRAM.exe"));
                ExtractResource("Payload.app.ico", Path.Combine(resDir, "app.ico"));
                ExtractResource("Payload.app.ico", Path.Combine(installDir, "app.ico"));
                ExtractResource("Payload.panda_clean.png", Path.Combine(resDir, "panda_clean.png"));
                ExtractResource("Payload.watermark_bg.jpg", Path.Combine(resDir, "watermark_bg.jpg"));

                // 4. Crear desinstalador seguro
                _lblStatus.Text = "Configurando desinstalador...";
                _progressBar.Value = 70;
                Application.DoEvents();

                string uninstallerCmd = Path.Combine(installDir, "desinstalar.cmd");
                string uninstContent =
                    "@echo off\r\n" +
                    "taskkill /f /im LimpiadorRAM.exe >nul 2>&1\r\n" +
                    "schtasks /delete /tn \"LimpiadorRAM\" /f >nul 2>&1\r\n" +
                    "reg delete \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\LimpiadorRAM\" /f >nul 2>&1\r\n" +
                    "reg delete \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run\" /v \"LimpiadorRAM\" /f >nul 2>&1\r\n" +
                    "del /f /q \"" + Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Limpiador de RAM.lnk") + "\" >nul 2>&1\r\n" +
                    "del /f /q \"" + Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Limpiador de RAM.lnk") + "\" >nul 2>&1\r\n" +
                    "del /f /q \"" + Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Limpiador de RAM.lnk") + "\" >nul 2>&1\r\n" +
                    "timeout /t 1 >nul 2>&1\r\n" +
                    "if exist \"%~dp0LimpiadorRAM.exe\" (\r\n" +
                    "    cd /d \"%~dp0..\"\r\n" +
                    "    rmdir /s /q \"%~dp0\" >nul 2>&1\r\n" +
                    ")\r\n" +
                    "echo Desinstalacion completada con exito.\r\n" +
                    "pause\r\n";
                File.WriteAllText(uninstallerCmd, uninstContent);

                // 5. Registrar en Windows (Agregar o quitar programas)
                RegisterInWindows(installDir, uninstallerCmd);

                // 6. Accesos directos
                _lblStatus.Text = "Creando accesos directos...";
                _progressBar.Value = 85;
                Application.DoEvents();

                string targetExe = Path.Combine(installDir, "LimpiadorRAM.exe");
                string iconPath = Path.Combine(resDir, "app.ico");

                if (_chkDesktop.Checked)
                {
                    string desktopLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Limpiador de RAM.lnk");
                    CreateShortcut(desktopLnk, targetExe, installDir, iconPath, "Limpiador de RAM");
                }

                if (_chkStartMenu.Checked)
                {
                    string startMenuDir = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                    string startLnk = Path.Combine(startMenuDir, "Limpiador de RAM.lnk");
                    CreateShortcut(startLnk, targetExe, installDir, iconPath, "Limpiador de RAM");
                }

                if (_chkStartWithWindows.Checked)
                {
                    try
                    {
                        ProcessStartInfo psi = new ProcessStartInfo();
                        psi.FileName = "schtasks.exe";
                        psi.Arguments = string.Format("/create /tn \"LimpiadorRAM\" /tr \"\\\"{0}\\\" /tray\" /sc ONLOGON /rl HIGHEST /f", targetExe);
                        psi.UseShellExecute = false;
                        psi.CreateNoWindow = true;
                        using (Process p = Process.Start(psi)) { if (p != null) p.WaitForExit(3000); }
                    }
                    catch { }

                    try
                    {
                        string startupLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Limpiador de RAM.lnk");
                        CreateShortcut(startupLnk, targetExe, installDir, iconPath, "Limpiador de RAM");
                    }
                    catch { }

                    try
                    {
                        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                        {
                            if (key != null) key.SetValue("LimpiadorRAM", "\"" + targetExe + "\" /tray");
                        }
                    }
                    catch { }
                }

                _progressBar.Value = 100;
                _lblStatus.Text = "¡Instalación completada con éxito!";
                _lblStatus.ForeColor = _accentColor;

                _isCompleted = true;
                _btnInstall.Enabled = true;
                _btnInstall.Text = "Finalizar";
                _btnCancel.Visible = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error durante la instalación: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _btnInstall.Enabled = true;
            }
        }

        private void ExtractResource(string resName, string outPath)
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(resName))
            {
                if (s == null) return;
                using (FileStream fs = new FileStream(outPath, FileMode.Create, FileAccess.Write))
                {
                    byte[] buffer = new byte[65536];
                    int read;
                    while ((read = s.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        fs.Write(buffer, 0, read);
                    }
                }
            }
        }

        private void RegisterInWindows(string installDir, string uninstallerCmd)
        {
            try
            {
                const string uninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\LimpiadorRAM";
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(uninstallKey))
                {
                    if (key != null)
                    {
                        key.SetValue("DisplayName", "Limpiador de RAM");
                        key.SetValue("DisplayVersion", "1.0.0");
                        key.SetValue("Publisher", "LimpiadorRAM");
                        key.SetValue("DisplayIcon", Path.Combine(installDir, @"resources\app.ico"));
                        key.SetValue("InstallLocation", installDir);
                        key.SetValue("UninstallString", "\"" + uninstallerCmd + "\"");
                        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    }
                }
            }
            catch { }
        }

        private void CreateShortcut(string shortcutPath, string targetPath, string workingDir, string iconPath, string description)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = targetPath;
                shortcut.WorkingDirectory = workingDir;
                shortcut.IconLocation = iconPath + ",0";
                shortcut.Description = description;
                shortcut.Save();
            }
            catch { }
        }
    }
}
