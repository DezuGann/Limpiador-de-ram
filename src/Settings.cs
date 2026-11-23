using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace LimpiadorRAM
{
    public class AppSettings
    {
        public bool CleanWorkingSets { get; set; }
        public bool CleanTempFiles { get; set; }
        public bool EmptyRecycleBin { get; set; }
        public bool FlushDns { get; set; }
        public bool ForceGC { get; set; }

        public bool AutoCleanThresholdEnabled { get; set; }
        public int AutoCleanThresholdPercent { get; set; }

        public bool AutoCleanTimerEnabled { get; set; }
        public int AutoCleanTimerMinutes { get; set; }

        public bool StartWithWindows { get; set; }
        public bool MinimizeToTrayOnClose { get; set; }
        public bool StartMinimized { get; set; }
        public bool SettingsExpanded { get; set; }

        public AppSettings()
        {
            CleanWorkingSets = true;
            CleanTempFiles = true;
            EmptyRecycleBin = true;
            FlushDns = true;
            ForceGC = true;

            AutoCleanThresholdEnabled = false;
            AutoCleanThresholdPercent = 85;

            AutoCleanTimerEnabled = false;
            AutoCleanTimerMinutes = 30;

            StartWithWindows = false;
            MinimizeToTrayOnClose = true;
            StartMinimized = false;
            SettingsExpanded = false;
        }

        private static string GetConfigPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "LimpiadorRAM");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            return Path.Combine(folder, "settings.xml");
        }

        public static AppSettings Load()
        {
            try
            {
                string path = GetConfigPath();
                if (File.Exists(path))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(AppSettings));
                    using (FileStream fs = new FileStream(path, FileMode.Open))
                    {
                        AppSettings settings = (AppSettings)serializer.Deserialize(fs);
                        if (settings != null) return settings;
                    }
                }
            }
            catch
            {
                // En caso de error de lectura, retornar valores por defecto
            }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                string path = GetConfigPath();
                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                XmlSerializer serializer = new XmlSerializer(typeof(AppSettings));
                using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
                    serializer.Serialize(fs, this);
                }
            }
            catch
            {
                // Ignorar error al serializar
            }

            try
            {
                UpdateStartupRegistry(this.StartWithWindows);
            }
            catch
            {
                // Ignorar error de inicio
            }
        }

        public static void UpdateStartupRegistry(bool enable)
        {
            string exePath = Application.ExecutablePath;
            const string taskName = "LimpiadorRAM";
            const string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
            const string appName = "LimpiadorRAM";

            // 1. Tarea programada en Windows Task Scheduler (Permite inicio con máximos privilegios sin bloqueo de UAC)
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "schtasks.exe";
                if (enable)
                {
                    psi.Arguments = string.Format("/create /tn \"{0}\" /tr \"\\\"{1}\\\" /tray\" /sc ONLOGON /rl HIGHEST /f", taskName, exePath);
                }
                else
                {
                    psi.Arguments = string.Format("/delete /tn \"{0}\" /f", taskName);
                }
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                using (Process p = Process.Start(psi))
                {
                    if (p != null) p.WaitForExit(3000);
                }
            }
            catch { }

            // 2. Acceso directo en la carpeta de Inicio de Windows (Startup folder)
            try
            {
                string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string lnkPath = Path.Combine(startupFolder, "Limpiador de RAM.lnk");
                if (enable)
                {
                    Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                    if (shellType != null)
                    {
                        dynamic shell = Activator.CreateInstance(shellType);
                        dynamic shortcut = shell.CreateShortcut(lnkPath);
                        shortcut.TargetPath = exePath;
                        shortcut.Arguments = "/tray";
                        shortcut.WorkingDirectory = Path.GetDirectoryName(exePath);
                        string resIco = Path.Combine(Path.GetDirectoryName(exePath), @"resources\app.ico");
                        if (File.Exists(resIco)) shortcut.IconLocation = resIco + ",0";
                        shortcut.Description = "Limpiador de RAM";
                        shortcut.Save();
                    }
                }
                else
                {
                    if (File.Exists(lnkPath)) File.Delete(lnkPath);
                }
            }
            catch { }

            // 3. Clave Run del Registro de usuario
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(runKey, true))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            key.SetValue(appName, "\"" + exePath + "\" /tray");
                        }
                        else
                        {
                            if (key.GetValue(appName) != null)
                            {
                                key.DeleteValue(appName, false);
                            }
                        }
                    }
                }
            }
            catch { }
        }

        public OptimizationOptions ToOptimizationOptions()
        {
            OptimizationOptions opt = new OptimizationOptions();
            opt.CleanWorkingSets = this.CleanWorkingSets;
            opt.CleanTempFiles = this.CleanTempFiles;
            opt.EmptyRecycleBin = this.EmptyRecycleBin;
            opt.FlushDns = this.FlushDns;
            opt.ForceGC = this.ForceGC;
            return opt;
        }
    }
}
