using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace ricxOptimizer.Core
{
    public static class RegistryHelper
    {
        public static bool SetDWord(RegistryHive hive, string subKey, string valueName, int value)
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (var key = baseKey.CreateSubKey(subKey, RegistryKeyPermissionCheck.ReadWriteSubTree))
                {
                    if (key != null)
                    {
                        key.SetValue(valueName, value, RegistryValueKind.DWord);
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                // Fallback to command line reg.exe if permissions require special handling
                return RunRegAdd(hive, subKey, valueName, "REG_DWORD", value.ToString());
            }
            return false;
        }

        public static bool SetString(RegistryHive hive, string subKey, string valueName, string value)
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (var key = baseKey.CreateSubKey(subKey, RegistryKeyPermissionCheck.ReadWriteSubTree))
                {
                    if (key != null)
                    {
                        key.SetValue(valueName, value, RegistryValueKind.String);
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                return RunRegAdd(hive, subKey, valueName, "REG_SZ", value);
            }
            return false;
        }

        public static bool DeleteValue(RegistryHive hive, string subKey, string valueName)
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (var key = baseKey.OpenSubKey(subKey, true))
                {
                    if (key != null && key.GetValue(valueName) != null)
                    {
                        key.DeleteValue(valueName, false);
                        return true;
                    }
                }
            }
            catch
            {
                return RunRegDelete(hive, subKey, valueName);
            }
            return false;
        }

        public static object GetValue(RegistryHive hive, string subKey, string valueName)
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (var key = baseKey.OpenSubKey(subKey, false))
                {
                    return key != null ? key.GetValue(valueName) : null;
                }
            }
            catch
            {
                return null;
            }
        }

        public static bool ExportKey(string registryPath, string targetFile)
        {
            try
            {
                var dir = Path.GetDirectoryName(targetFile);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var psi = new ProcessStartInfo
                {
                    FileName = "reg.exe",
                    Arguments = $"export \"{registryPath}\" \"{targetFile}\" /y",
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

        public static bool ImportFile(string regFilePath)
        {
            try
            {
                if (!File.Exists(regFilePath)) return false;

                var psi = new ProcessStartInfo
                {
                    FileName = "reg.exe",
                    Arguments = $"import \"{regFilePath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var proc = Process.Start(psi))
                {
                    proc?.WaitForExit(10000);
                    return proc?.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool RunRegAdd(RegistryHive hive, string subKey, string valueName, string type, string data)
        {
            try
            {
                string hiveStr = hive == RegistryHive.LocalMachine ? "HKLM" : "HKCU";
                string fullKey = $"{hiveStr}\\{subKey}";
                var psi = new ProcessStartInfo
                {
                    FileName = "reg.exe",
                    Arguments = $"add \"{fullKey}\" /v \"{valueName}\" /t {type} /d \"{data}\" /f /reg:64",
                    CreateNoWindow = true,
                    UseShellExecute = false
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

        private static bool RunRegDelete(RegistryHive hive, string subKey, string valueName)
        {
            try
            {
                string hiveStr = hive == RegistryHive.LocalMachine ? "HKLM" : "HKCU";
                string fullKey = $"{hiveStr}\\{subKey}";
                var psi = new ProcessStartInfo
                {
                    FileName = "reg.exe",
                    Arguments = $"delete \"{fullKey}\" /v \"{valueName}\" /f /reg:64",
                    CreateNoWindow = true,
                    UseShellExecute = false
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
