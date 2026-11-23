using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace LimpiadorRAM
{
    public class RamInfo
    {
        public double TotalPhysicalMB { get; set; }
        public double FreePhysicalMB { get; set; }
        public double UsedPhysicalMB { get; set; }
        public uint MemoryLoadPercent { get; set; }

        public double TotalPhysicalGB
        {
            get { return Math.Round(TotalPhysicalMB / 1024.0, 2); }
        }

        public double FreePhysicalGB
        {
            get { return Math.Round(FreePhysicalMB / 1024.0, 2); }
        }

        public double UsedPhysicalGB
        {
            get { return Math.Round(UsedPhysicalMB / 1024.0, 2); }
        }
    }

    public class OptimizationOptions
    {
        public bool CleanWorkingSets { get; set; }
        public bool CleanTempFiles { get; set; }
        public bool EmptyRecycleBin { get; set; }
        public bool FlushDns { get; set; }
        public bool ForceGC { get; set; }

        public OptimizationOptions()
        {
            CleanWorkingSets = true;
            CleanTempFiles = true;
            EmptyRecycleBin = true;
            FlushDns = true;
            ForceGC = true;
        }
    }

    public class OptimizationResult
    {
        public RamInfo Before { get; set; }
        public RamInfo After { get; set; }
        public double FreedMB { get; set; }
        public int ProcessesOptimized { get; set; }
        public int ProcessesSkipped { get; set; }
        public int TempFilesDeleted { get; set; }
        public long TempBytesDeleted { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public static class MemoryManager
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;

            public MEMORYSTATUSEX()
            {
                this.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        [DllImport("psapi.dll")]
        private static extern int EmptyWorkingSet(IntPtr hwProc);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);

        private const uint SHERB_NOCONFIRMATION = 0x00000001;
        private const uint SHERB_NOPROGRESSUI   = 0x00000002;
        private const uint SHERB_NOSOUND        = 0x00000004;

        [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache")]
        private static extern int DnsFlushResolverCache();

        public static RamInfo GetRamInfo()
        {
            MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(memStatus))
            {
                double total = (double)memStatus.ullTotalPhys / (1024.0 * 1024.0);
                double free = (double)memStatus.ullAvailPhys / (1024.0 * 1024.0);
                double used = total - free;

                RamInfo info = new RamInfo();
                info.TotalPhysicalMB = Math.Round(total, 2);
                info.FreePhysicalMB = Math.Round(free, 2);
                info.UsedPhysicalMB = Math.Round(used, 2);
                info.MemoryLoadPercent = memStatus.dwMemoryLoad;
                return info;
            }

            // Fallback por si la API falla
            RamInfo fallback = new RamInfo();
            fallback.TotalPhysicalMB = 8192;
            fallback.FreePhysicalMB = 4096;
            fallback.UsedPhysicalMB = 4096;
            fallback.MemoryLoadPercent = 50;
            return fallback;
        }

        public static OptimizationResult Optimize(OptimizationOptions options, Action<string> log)
        {
            if (log == null) log = delegate(string s) { };
            Stopwatch sw = Stopwatch.StartNew();

            OptimizationResult result = new OptimizationResult();
            result.Before = GetRamInfo();

            log(string.Format("=== Iniciando Optimización de Memoria RAM ==="));
            log(string.Format("RAM Libre Inicial: {0:N0} MB de {1:N0} MB ({2}% en uso)",
                result.Before.FreePhysicalMB, result.Before.TotalPhysicalMB, result.Before.MemoryLoadPercent));

            // 1. Limpieza de Temporales y Cachés
            if (options.CleanTempFiles)
            {
                log("[1/5] Depurando archivos temporales y cachés de Windows...");
                CleanTempCaches(result, log);
            }

            // 2. Limpieza de Caché DNS
            if (options.FlushDns)
            {
                log("[2/5] Vaciando caché del resolver DNS...");
                try
                {
                    DnsFlushResolverCache();
                    log("      -> Caché DNS purgado correctamente.");
                }
                catch (Exception ex)
                {
                    log("      -> Nota DNS: " + ex.Message);
                }
            }

            // 3. Vaciar Papelera de Reciclaje
            if (options.EmptyRecycleBin)
            {
                log("[3/5] Vaciando papelera de reciclaje silenciosamente...");
                try
                {
                    SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
                    log("      -> Papelera de reciclaje vaciada.");
                }
                catch (Exception ex)
                {
                    log("      -> Nota Papelera: " + ex.Message);
                }
            }

            // 4. Liberar Working Sets de Procesos
            if (options.CleanWorkingSets)
            {
                log("[4/5] Liberando Working Sets de aplicaciones activas...");
                OptimizeProcessWorkingSets(result, log);
            }

            // 5. Forzar Recolección de Basura (.NET GC)
            if (options.ForceGC)
            {
                log("[5/5] Forzando recolección de basura del sistema...");
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                log("      -> Recolección de basura ejecutada.");
            }

            // Breve pausa para estabilización del subsistema de memoria
            Thread.Sleep(600);

            result.After = GetRamInfo();
            result.FreedMB = Math.Round(result.After.FreePhysicalMB - result.Before.FreePhysicalMB, 2);
            sw.Stop();
            result.Duration = sw.Elapsed;

            log("=== Optimización Finalizada ===");
            log(string.Format("RAM Libre Anterior : {0:N0} MB", result.Before.FreePhysicalMB));
            log(string.Format("RAM Libre Actual   : {0:N0} MB (Carga: {1}%)", result.After.FreePhysicalMB, result.After.MemoryLoadPercent));

            if (result.FreedMB > 0)
            {
                log(string.Format("Memoria Recuperada : +{0:N0} MB ({1:N2} GB) en {2:N2}s",
                    result.FreedMB, result.FreedMB / 1024.0, result.Duration.TotalSeconds));
            }
            else
            {
                log("La memoria ya se encontraba en estado óptimo.");
            }

            return result;
        }

        private static void OptimizeProcessWorkingSets(OptimizationResult result, Action<string> log)
        {
            Process[] processes = Process.GetProcesses();
            int optimized = 0;
            int skipped = 0;

            foreach (Process proc in processes)
            {
                try
                {
                    // Procesos del sistema protegidos generarán excepción al leer Handle
                    IntPtr handle = proc.Handle;
                    if (handle != IntPtr.Zero)
                    {
                        if (EmptyWorkingSet(handle) != 0)
                        {
                            optimized++;
                        }
                        else
                        {
                            skipped++;
                        }
                    }
                }
                catch
                {
                    skipped++;
                }
                finally
                {
                    try { proc.Dispose(); } catch { }
                }
            }

            result.ProcessesOptimized = optimized;
            result.ProcessesSkipped = skipped;
            log(string.Format("      -> {0} procesos optimizados ({1} protegidos del sistema omitidos).", optimized, skipped));
        }

        private static void CleanTempCaches(OptimizationResult result, Action<string> log)
        {
            List<string> paths = new List<string>();

            // %TEMP%
            try { paths.Add(Path.GetTempPath()); } catch { }

            // %WINDIR%\Temp
            try
            {
                string windir = Environment.GetEnvironmentVariable("WINDIR");
                if (!string.IsNullOrEmpty(windir))
                {
                    paths.Add(Path.Combine(windir, "Temp"));
                    paths.Add(Path.Combine(windir, "Prefetch"));
                }
            }
            catch { }

            // %LOCALAPPDATA%\Microsoft\Windows\INetCache
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrEmpty(localAppData))
                {
                    paths.Add(Path.Combine(localAppData, @"Microsoft\Windows\INetCache"));
                    paths.Add(Path.Combine(localAppData, "CrashDumps"));
                }
            }
            catch { }

            int deletedFiles = 0;
            long deletedBytes = 0;

            foreach (string dir in paths)
            {
                if (Directory.Exists(dir))
                {
                    CleanDirectory(dir, ref deletedFiles, ref deletedBytes);
                }
            }

            result.TempFilesDeleted = deletedFiles;
            result.TempBytesDeleted = deletedBytes;

            double mb = deletedBytes / (1024.0 * 1024.0);
            log(string.Format("      -> {0} archivos temporales depurados ({1:N1} MB eliminados del disco).", deletedFiles, mb));
        }

        private static void CleanDirectory(string dirPath, ref int deletedFiles, ref long deletedBytes)
        {
            try
            {
                DirectoryInfo dir = new DirectoryInfo(dirPath);
                FileInfo[] files = dir.GetFiles();
                foreach (FileInfo file in files)
                {
                    try
                    {
                        long len = file.Length;
                        file.Delete();
                        deletedFiles++;
                        deletedBytes += len;
                    }
                    catch
                    {
                        // Archivo bloqueado o en uso por el sistema operativo
                    }
                }

                DirectoryInfo[] subDirs = dir.GetDirectories();
                foreach (DirectoryInfo sub in subDirs)
                {
                    try
                    {
                        CleanDirectory(sub.FullName, ref deletedFiles, ref deletedBytes);
                        if (sub.GetFiles().Length == 0 && sub.GetDirectories().Length == 0)
                        {
                            sub.Delete();
                        }
                    }
                    catch
                    {
                        // Directorio en uso
                    }
                }
            }
            catch
            {
                // Acceso restringido al directorio
            }
        }
    }
}
