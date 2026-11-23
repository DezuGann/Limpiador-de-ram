using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace LimpiadorRAM
{
    static class Program
    {
        private const string MutexName = @"Global\LimpiadorRAM_Pro_SingleInstance_Mutex";

        [STAThread]
        static void Main(string[] args)
        {
            // 1. Verificar permisos de Administrador
            if (!IsAdministrator())
            {
                try
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo();
                    startInfo.UseShellExecute = true;
                    startInfo.WorkingDirectory = Environment.CurrentDirectory;
                    startInfo.FileName = Application.ExecutablePath;
                    startInfo.Verb = "runas"; // Solicita elevación UAC en Windows
                    if (args != null && args.Length > 0)
                    {
                        startInfo.Arguments = string.Join(" ", args);
                    }
                    Process.Start(startInfo);
                }
                catch
                {
                    MessageBox.Show(
                        "Esta aplicación requiere permisos de Administrador para liberar la memoria de todos los procesos del sistema y limpiar cachés protegidos.",
                        "Permisos Requeridos",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                }
                return;
            }

            // 2. Control de Instancia Única
            bool createdNew;
            using (Mutex mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    // Si ya se encuentra en ejecución, podemos disparar la limpieza si se pasó /clean
                    bool isCleanArg = false;
                    if (args != null)
                    {
                        foreach (string arg in args)
                        {
                            if (string.Equals(arg, "/clean", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(arg, "-clean", StringComparison.OrdinalIgnoreCase))
                            {
                                isCleanArg = true;
                                break;
                            }
                        }
                    }

                    if (isCleanArg)
                    {
                        // Ejecuta optimización silenciosa directa
                        AppSettings set = AppSettings.Load();
                        MemoryManager.Optimize(set.ToOptimizationOptions(), null);
                    }
                    else
                    {
                        MessageBox.Show(
                            "Limpiador de RAM ya se encuentra en ejecución en la bandeja del sistema (junto al reloj).",
                            "Limpiador de RAM Pro",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information
                        );
                    }
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // Cargar configuración
                AppSettings settings = AppSettings.Load();

                // Analizar argumentos de línea de comandos
                bool startMinimized = settings.StartMinimized;
                bool silentCleanAndExit = false;

                if (args != null)
                {
                    foreach (string a in args)
                    {
                        string lower = a.ToLowerInvariant().Trim('-', '/');
                        if (lower == "tray" || lower == "min" || lower == "minimized")
                        {
                            startMinimized = true;
                        }
                        else if (lower == "silent" || lower == "quiet")
                        {
                            silentCleanAndExit = true;
                        }
                        else if (lower == "clean")
                        {
                            startMinimized = true;
                        }
                    }
                }

                if (silentCleanAndExit)
                {
                    // Modo completamente silencioso (ej. programador de tareas)
                    MemoryManager.Optimize(settings.ToOptimizationOptions(), null);
                    return;
                }

                // Iniciar contexto de la bandeja del sistema
                TrayAppContext context = new TrayAppContext(settings, startMinimized);

                // Si se solicitó /clean, disparar limpieza inmediata
                if (args != null)
                {
                    foreach (string a in args)
                    {
                        if (a.ToLowerInvariant().Trim('-', '/') == "clean")
                        {
                            context.RunQuickOptimization();
                            break;
                        }
                    }
                }

                Application.Run(context);
            }
        }

        private static bool IsAdministrator()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                if (identity == null) return false;
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }
}
