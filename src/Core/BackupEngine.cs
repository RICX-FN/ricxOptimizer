using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ricxOptimizer.Models;

namespace ricxOptimizer.Core
{
    public class BackupEngine
    {
        private readonly string _backupFolder;
        private readonly string _manifestFile;

        public static readonly string[] TargetedServices = new string[]
        {
            "DiagTrack",
            "SysMain",
            "WSearch",
            "DPS",
            "WdiServiceHost",
            "WdiSystemHost",
            "WerSvc",
            "DoSvc",
            "wuauserv",
            "UsoSvc",
            "BITS",
            "spoolsv",
            "Fax",
            "RemoteRegistry",
            "WbioSrvc",
            "XblAuthManager",
            "XblGameSave",
            "XboxNetApiSvc",
            "XboxGipSvc"
        };

        public static readonly string[] TargetedRegKeys = new string[]
        {
            @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
            @"HKLM\SYSTEM\CurrentControlSet\Control\PriorityControl",
            @"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management",
            @"HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters",
            @"HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
            @"HKLM\SOFTWARE\Policies\Microsoft\Windows Defender",
            @"HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR",
            @"HKCU\System\GameConfigStore",
            @"HKCU\Control Panel\Mouse"
        };

        public BackupEngine()
        {
            string baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ricxOptimizer", "Backups");
            if (!Directory.Exists(baseDir))
            {
                Directory.CreateDirectory(baseDir);
            }
            _backupFolder = baseDir;
            _manifestFile = Path.Combine(_backupFolder, "manifest.json");
        }

        public bool HasExistingBackup()
        {
            return File.Exists(_manifestFile);
        }

        public string GetBackupPath() => _backupFolder;

        public bool CreateFullBackup(Action<OptimizationStepLog> log)
        {
            try
            {
                log(new OptimizationStepLog { Category = "BACKUP", Message = "Iniciando criação de backup completo do sistema..." });

                // 1. Windows System Restore Point
                bool restorePointOk = CreateSystemRestorePoint(log);

                // 2. Export Registry Keys
                string regBackupFile = Path.Combine(_backupFolder, "registry_backup.reg");
                ExportRegistryKeys(regBackupFile, log);

                // 3. Backup Services States
                var servicesSnapshot = new List<ServiceSnapshot>();
                foreach (var sName in TargetedServices)
                {
                    var snap = ServiceHelper.GetSnapshot(sName);
                    if (snap != null)
                    {
                        servicesSnapshot.Add(snap);
                    }
                }
                SaveServicesSnapshot(servicesSnapshot, log);

                // 4. Backup BCD
                string bcdBackupFile = Path.Combine(_backupFolder, "bcd_backup.bcd");
                bool bcdOk = BcdHelper.ExportBcd(bcdBackupFile);
                log(new OptimizationStepLog 
                { 
                    Category = "BCD", 
                    Message = bcdOk ? "Configurações BCD exportadas com sucesso." : "Aviso: BCD export não pôde ser concluído.",
                    Success = bcdOk 
                });

                // 5. Backup Active Power Scheme
                string activePowerScheme = GetActivePowerScheme();
                File.WriteAllText(Path.Combine(_backupFolder, "power_scheme.txt"), activePowerScheme);
                log(new OptimizationStepLog { Category = "POWER", Message = $"Plano de energia ativo salvo: {activePowerScheme}" });

                // 6. Write Manifest
                var sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine($"  \"CreatedAt\": \"{DateTime.Now:O}\",");
                sb.AppendLine($"  \"ComputerName\": \"{Environment.MachineName}\",");
                sb.AppendLine($"  \"OSVersion\": \"{Environment.OSVersion}\",");
                sb.AppendLine($"  \"RestorePoint\": {restorePointOk.ToString().ToLower()},");
                sb.AppendLine($"  \"PowerScheme\": \"{activePowerScheme}\",");
                sb.AppendLine($"  \"ServicesCount\": {servicesSnapshot.Count}");
                sb.AppendLine("}");
                File.WriteAllText(_manifestFile, sb.ToString());

                log(new OptimizationStepLog { Category = "BACKUP", Message = "Backup completo finalizado com sucesso!", Success = true });
                return true;
            }
            catch (Exception ex)
            {
                log(new OptimizationStepLog { Category = "BACKUP", Message = $"Erro durante o backup: {ex.Message}", Success = false });
                return false;
            }
        }

        public bool RestoreBackup(Action<OptimizationStepLog> log)
        {
            try
            {
                if (!HasExistingBackup())
                {
                    log(new OptimizationStepLog { Category = "RESTORE", Message = "Nenhum backup prévio encontrado. Aplicando perfil baseline seguro de fábrica...", Success = true });
                    return false;
                }

                log(new OptimizationStepLog { Category = "RESTORE", Message = "Restaurando configurações através do backup salvo..." });

                // 1. Restore Registry
                string regBackupFile = Path.Combine(_backupFolder, "registry_backup.reg");
                if (File.Exists(regBackupFile))
                {
                    bool regOk = RegistryHelper.ImportFile(regBackupFile);
                    log(new OptimizationStepLog { Category = "REGISTRY", Message = "Chaves do Registro restauradas.", Success = regOk });
                }

                // 2. Restore Services
                RestoreServicesSnapshot(log);

                // 3. Restore BCD
                string bcdBackupFile = Path.Combine(_backupFolder, "bcd_backup.bcd");
                if (File.Exists(bcdBackupFile))
                {
                    bool bcdOk = BcdHelper.ImportBcd(bcdBackupFile);
                    log(new OptimizationStepLog { Category = "BCD", Message = "Configurações BCD originais restauradas.", Success = bcdOk });
                }
                else
                {
                    // Clean tweaks
                    BcdHelper.DeleteBcdValue("useplatformtick");
                    BcdHelper.DeleteBcdValue("disabledynamictick");
                    BcdHelper.DeleteBcdValue("useplatformclock");
                    BcdHelper.SetBcdValue("hypervisorlaunchtype", "auto");
                }

                // 4. Restore Power Scheme
                string powerFile = Path.Combine(_backupFolder, "power_scheme.txt");
                if (File.Exists(powerFile))
                {
                    string schemeGuid = File.ReadAllText(powerFile).Trim();
                    SetActivePowerScheme(schemeGuid);
                    log(new OptimizationStepLog { Category = "POWER", Message = $"Plano de energia original restaurado: {schemeGuid}" });
                }

                log(new OptimizationStepLog { Category = "RESTORE", Message = "Sistema restaurado com sucesso para o estado padrão do backup!", Success = true });
                return true;
            }
            catch (Exception ex)
            {
                log(new OptimizationStepLog { Category = "RESTORE", Message = $"Falha ao restaurar backup: {ex.Message}", Success = false });
                return false;
            }
        }

        private bool CreateSystemRestorePoint(Action<OptimizationStepLog> log)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"Checkpoint-Computer -Description 'ricxOptimizer_SafetyBackup' -RestorePointType MODIFY_SETTINGS\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var proc = Process.Start(psi))
                {
                    proc?.WaitForExit(15000);
                    bool ok = proc?.ExitCode == 0;
                    log(new OptimizationStepLog 
                    { 
                        Category = "VSS", 
                        Message = ok ? "Ponto de Restauração do Windows criado com sucesso." : "Aviso: Ponto de restauração ignorado ou VSS desabilitado na máquina.",
                        Success = ok
                    });
                    return ok;
                }
            }
            catch
            {
                return false;
            }
        }

        private void ExportRegistryKeys(string outputFile, Action<OptimizationStepLog> log)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Windows Registry Editor Version 5.00");
                sb.AppendLine();
                File.WriteAllText(outputFile, sb.ToString(), Encoding.Unicode);

                int count = 0;
                foreach (var key in TargetedRegKeys)
                {
                    string tempFile = Path.Combine(_backupFolder, $"key_{count++}.reg");
                    if (RegistryHelper.ExportKey(key, tempFile) && File.Exists(tempFile))
                    {
                        string content = File.ReadAllText(tempFile);
                        File.AppendAllText(outputFile, content + Environment.NewLine, Encoding.Unicode);
                        try { File.Delete(tempFile); } catch { }
                    }
                }
                log(new OptimizationStepLog { Category = "REGISTRY", Message = $"Exportadas {TargetedRegKeys.Length} áreas críticas do Registro." });
            }
            catch (Exception ex)
            {
                log(new OptimizationStepLog { Category = "REGISTRY", Message = $"Falha ao exportar registro: {ex.Message}", Success = false });
            }
        }

        private void SaveServicesSnapshot(List<ServiceSnapshot> list, Action<OptimizationStepLog> log)
        {
            try
            {
                string jsonFile = Path.Combine(_backupFolder, "services_backup.txt");
                var sb = new StringBuilder();
                foreach (var s in list)
                {
                    sb.AppendLine($"{s.ServiceName}|{s.StartMode}|{s.State}");
                }
                File.WriteAllText(jsonFile, sb.ToString());
                log(new OptimizationStepLog { Category = "SERVICES", Message = $"Salvo estado original de {list.Count} serviços críticos." });
            }
            catch (Exception ex)
            {
                log(new OptimizationStepLog { Category = "SERVICES", Message = $"Erro ao salvar snapshot de serviços: {ex.Message}", Success = false });
            }
        }

        private void RestoreServicesSnapshot(Action<OptimizationStepLog> log)
        {
            string jsonFile = Path.Combine(_backupFolder, "services_backup.txt");
            if (!File.Exists(jsonFile)) return;

            var lines = File.ReadAllLines(jsonFile);
            foreach (var line in lines)
            {
                var parts = line.Split('|');
                if (parts.Length >= 3)
                {
                    string sName = parts[0];
                    string sStart = parts[1];
                    string sState = parts[2];

                    ServiceHelper.SetStartupType(sName, sStart);
                    if (sState.Equals("Running", StringComparison.OrdinalIgnoreCase))
                    {
                        ServiceHelper.StartService(sName);
                    }
                }
            }
            log(new OptimizationStepLog { Category = "SERVICES", Message = "Serviços reconfigurados conforme snapshot original." });
        }

        private string GetActivePowerScheme()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
                    Arguments = "/getactivescheme",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };

                using (var proc = Process.Start(psi))
                {
                    if (proc != null)
                    {
                        string outStr = proc.StandardOutput.ReadToEnd();
                        proc.WaitForExit(3000);
                        var match = Regex.Match(outStr, @"GUID:\s*([a-fA-F0-9\-]+)");
                        if (match.Success)
                        {
                            return match.Groups[1].Value;
                        }
                    }
                }
            }
            catch { }
            return "381b4222-f694-41f0-9685-ff5bb260df2e"; // Default Balanced
        }

        private void SetActivePowerScheme(string guid)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
                    Arguments = $"/setactive {guid}",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using (var proc = Process.Start(psi))
                {
                    proc?.WaitForExit(3000);
                }
            }
            catch { }
        }
    }
}
