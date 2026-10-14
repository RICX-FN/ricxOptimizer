using System;
using System.Diagnostics;
using System.ServiceProcess;
using ricxOptimizer.Models;

namespace ricxOptimizer.Core
{
    public static class ServiceHelper
    {
        public static bool SetStartupType(string serviceName, string startType)
        {
            // startType can be: auto, demand, disabled, delayed-auto
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = $"config \"{serviceName}\" start= {startType}",
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

        public static bool StopService(string serviceName)
        {
            try
            {
                using (var sc = new ServiceController(serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Running || sc.Status == ServiceControllerStatus.StartPending)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(5));
                        return true;
                    }
                    return true;
                }
            }
            catch
            {
                // Fallback to sc.exe / net stop
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "net.exe",
                        Arguments = $"stop \"{serviceName}\" /y",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using (var proc = Process.Start(psi))
                    {
                        proc?.WaitForExit(4000);
                        return true;
                    }
                }
                catch
                {
                    return false;
                }
            }
        }

        public static bool StartService(string serviceName)
        {
            try
            {
                using (var sc = new ServiceController(serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Stopped || sc.Status == ServiceControllerStatus.StopPending)
                    {
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(5));
                        return true;
                    }
                    return true;
                }
            }
            catch
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "net.exe",
                        Arguments = $"start \"{serviceName}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using (var proc = Process.Start(psi))
                    {
                        proc?.WaitForExit(4000);
                        return true;
                    }
                }
                catch
                {
                    return false;
                }
            }
        }

        public static ServiceSnapshot GetSnapshot(string serviceName)
        {
            try
            {
                using (var sc = new ServiceController(serviceName))
                {
                    string status = sc.Status.ToString();
                    string displayName = sc.DisplayName;
                    string startMode = "Demand";

                    // Query sc qc
                    var psi = new ProcessStartInfo
                    {
                        FileName = "sc.exe",
                        Arguments = $"qc \"{serviceName}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                    };

                    using (var proc = Process.Start(psi))
                    {
                        if (proc != null)
                        {
                            string output = proc.StandardOutput.ReadToEnd();
                            proc.WaitForExit(2000);

                            if (output.Contains("AUTO_START")) startMode = "auto";
                            else if (output.Contains("DEMAND_START")) startMode = "demand";
                            else if (output.Contains("DISABLED")) startMode = "disabled";
                        }
                    }

                    return new ServiceSnapshot
                    {
                        ServiceName = serviceName,
                        DisplayName = displayName,
                        StartMode = startMode,
                        State = status
                    };
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
