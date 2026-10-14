using System;
using System.Diagnostics;
using System.IO;

namespace ricxOptimizer.Core
{
    public static class BcdHelper
    {
        public static bool ExportBcd(string filePath)
        {
            try
            {
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var psi = new ProcessStartInfo
                {
                    FileName = "bcdedit.exe",
                    Arguments = $"/export \"{filePath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var proc = Process.Start(psi))
                {
                    proc?.WaitForExit(5000);
                    return proc?.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool ImportBcd(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return false;

                var psi = new ProcessStartInfo
                {
                    FileName = "bcdedit.exe",
                    Arguments = $"/import \"{filePath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var proc = Process.Start(psi))
                {
                    proc?.WaitForExit(5000);
                    return proc?.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool SetBcdValue(string parameter, string value)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "bcdedit.exe",
                    Arguments = $"/set {parameter} {value}",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var proc = Process.Start(psi))
                {
                    proc?.WaitForExit(3000);
                    return proc?.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool DeleteBcdValue(string parameter)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "bcdedit.exe",
                    Arguments = $"/deletevalue {parameter}",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var proc = Process.Start(psi))
                {
                    proc?.WaitForExit(3000);
                    return proc?.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
