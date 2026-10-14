using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using ricxOptimizer.Models;

namespace ricxOptimizer.Core
{
    public class OptimizationEngine
    {
        private readonly BackupEngine _backupEngine;

        public OptimizationEngine(BackupEngine backupEngine)
        {
            _backupEngine = backupEngine;
        }

        public void ApplyMode(OptimizationMode mode, Action<OptimizationStepLog> log)
        {
            switch (mode)
            {
                case OptimizationMode.WindowsDefault:
                    ApplyWindowsDefault(log);
                    break;
                case OptimizationMode.WindowsGamingMode:
                    ApplyGamingMode(log);
                    break;
                case OptimizationMode.WindowsGamingUltra:
                    ApplyGamingUltra(log);
                    break;
            }
        }

        #region Mode 1: Windows Default
        public void ApplyWindowsDefault(Action<OptimizationStepLog> log)
        {
            log(new OptimizationStepLog { Category = "DEFAULT", Message = "Iniciando restauração para Windows Padrão (Default)..." });

            // 1. Tenta restaurar a partir do backup físico existente
            if (_backupEngine.HasExistingBackup())
            {
                log(new OptimizationStepLog { Category = "DEFAULT", Message = "Backup prévio detectado. Restaurando configurações salvas..." });
                _backupEngine.RestoreBackup(log);
            }
            else
            {
                log(new OptimizationStepLog { Category = "DEFAULT", Message = "Nenhum backup encontrado. Aplicando perfil padrão limpo de fábrica..." });
            }

            // 2. Garantir que os parâmetros do registro padrão estejam definidos
            try
            {
                // Multimedia / Scheduler
                RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", 20);
                RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", 10);

                // Priority control
                RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", 2);

                // Memory
                RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "DisablePagingExecutive", 0);
                RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "LargeSystemCache", 0);

                // CPU Mitigations (Default)
                RegistryHelper.DeleteValue(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "FeatureSettingsOverride");
                RegistryHelper.DeleteValue(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "FeatureSettingsOverrideMask");

                // Defender Policies (Re-enable)
                RegistryHelper.DeleteValue(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows Defender", "DisableAntiSpyware");
                RegistryHelper.DeleteValue(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection", "DisableRealtimeMonitoring");
                RegistryHelper.DeleteValue(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection", "DisableBehaviorMonitoring");
                RegistryHelper.DeleteValue(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection", "DisableOnAccessProtection");
                RegistryHelper.DeleteValue(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection", "DisableScanOnRealtimeEnable");
                RegistryHelper.DeleteValue(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection", "DisableIOAVProtection");

                // SmartScreen
                RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "EnableSmartScreen", 1);

                // GameDVR
                RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", 1);
                RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 1);

                // Mouse Precision (Standard Windows default with acceleration enabled)
                RegistryHelper.SetString(RegistryHive.CurrentUser, @"Control Panel\Mouse", "MouseSpeed", "1");
                RegistryHelper.SetString(RegistryHive.CurrentUser, @"Control Panel\Mouse", "MouseThreshold1", "6");
                RegistryHelper.SetString(RegistryHive.CurrentUser, @"Control Panel\Mouse", "MouseThreshold2", "10");

                // Telemetry
                RegistryHelper.DeleteValue(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry");

                log(new OptimizationStepLog { Category = "REGISTRY", Message = "Valores padrão do registro do Windows redefinidos com sucesso." });
            }
            catch (Exception ex)
            {
                log(new OptimizationStepLog { Category = "REGISTRY", Message = $"Falha ao restaurar chaves padrão: {ex.Message}", Success = false });
            }

            // 3. Re-enable default services
            string[] defaultAutoServices = new string[] { "SysMain", "WSearch", "spoolsv", "DPS" };
            foreach (var s in defaultAutoServices)
            {
                ServiceHelper.SetStartupType(s, "auto");
                ServiceHelper.StartService(s);
            }

            string[] defaultDemandServices = new string[] { "wuauserv", "UsoSvc", "BITS", "WerSvc", "DoSvc" };
            foreach (var s in defaultDemandServices)
            {
                ServiceHelper.SetStartupType(s, "demand");
            }

            // 4. BCD Reset
            BcdHelper.DeleteBcdValue("useplatformtick");
            BcdHelper.DeleteBcdValue("disabledynamictick");
            BcdHelper.DeleteBcdValue("useplatformclock");
            BcdHelper.SetBcdValue("hypervisorlaunchtype", "auto");
            log(new OptimizationStepLog { Category = "BCD", Message = "Configurações de boot e hypervisor retornadas ao padrão." });

            // 5. Set Balanced Power Plan
            RunCommand("powercfg.exe", "/setactive 381b4222-f694-41f0-9685-ff5bb260df2e");

            // 6. Enable Memory Compression
            RunPowerShell("Enable-MMAgent -mc");

            log(new OptimizationStepLog { Category = "DEFAULT", Message = "Windows restaurado para o Modo Padrão com sucesso!", Success = true });
        }
        #endregion

        #region Mode 2: Windows Gaming Mode
        public void ApplyGamingMode(Action<OptimizationStepLog> log)
        {
            log(new OptimizationStepLog { Category = "GAMING", Message = "Aplicando 'Windows Gaming Mode' (Otimização Equilibrada)..." });

            // 1. Criar backup automático prévio de segurança caso não exista
            if (!_backupEngine.HasExistingBackup())
            {
                log(new OptimizationStepLog { Category = "GAMING", Message = "Nenhum backup existente encontrado. Gerando backup automático de segurança..." });
                _backupEngine.CreateFullBackup(log);
            }

            // 2. Power Plan: Ultimate Performance + Core Unparking
            ConfigureUltimatePerformance(log);

            // 3. Multimedia Class Scheduler Service (MMCSS)
            try
            {
                // SystemResponsiveness: 10% (permite 90% dos recursos prioritários para jogos)
                RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", 10);
                // NetworkThrottlingIndex: 0xFFFFFFFF (desativa limitação de rede durante jogos)
                RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF));

                // Tasks\Games
                string gamesTask = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games";
                RegistryHelper.SetString(RegistryHive.LocalMachine, gamesTask, "Scheduling Category", "High");
                RegistryHelper.SetString(RegistryHive.LocalMachine, gamesTask, "SFIO Priority", "High");
                RegistryHelper.SetString(RegistryHive.LocalMachine, gamesTask, "Background Only", "False");
                RegistryHelper.SetDWord(RegistryHive.LocalMachine, gamesTask, "Priority", 6);
                RegistryHelper.SetDWord(RegistryHive.LocalMachine, gamesTask, "GPU Priority", 8);

                log(new OptimizationStepLog { Category = "MMCSS", Message = "MMCSS priorizado para jogos (SystemResponsiveness=10%, NetworkThrottling=Desativado)." });
            }
            catch (Exception ex)
            {
                log(new OptimizationStepLog { Category = "MMCSS", Message = $"Erro ao configurar MMCSS: {ex.Message}", Success = false });
            }

            // 4. Process Scheduling: Short Quantum foreground boost (0x26 = 38 dec)
            RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", 0x26);
            log(new OptimizationStepLog { Category = "SCHEDULER", Message = "Win32PrioritySeparation ajustado para 0x26 (Prioridade dinâmica máxima para processo em primeiro plano)." });

            // 5. Game Bar & Game DVR (Remove gravação em segundo plano que rouba FPS e VRAM)
            RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", 0);
            RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", 0);
            RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"System\GameConfigStore", "GameDVR_FSEBehaviorMode", 2);
            RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0);
            log(new OptimizationStepLog { Category = "GAMEDVR", Message = "GameDVR e captura de vídeo em segundo plano desativados." });

            // 6. Graphics & Display: HAGS (Hardware Accelerated GPU Scheduling)
            RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2);
            // Windows Game Mode
            RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1);
            log(new OptimizationStepLog { Category = "GPU", Message = "HAGS (Agendamento de GPU acelerado por hardware) e Modo de Jogo ativados." });

            // 7. Mouse Raw Input: Disable pointer precision acceleration (1:1 muscle memory)
            RegistryHelper.SetString(RegistryHive.CurrentUser, @"Control Panel\Mouse", "MouseSpeed", "0");
            RegistryHelper.SetString(RegistryHive.CurrentUser, @"Control Panel\Mouse", "MouseThreshold1", "0");
            RegistryHelper.SetString(RegistryHive.CurrentUser, @"Control Panel\Mouse", "MouseThreshold2", "0");
            log(new OptimizationStepLog { Category = "INPUT", Message = "Aceleração do mouse desativada (Entrada direta 1:1)." });

            // 8. Memory: Keep Kernel in Physical RAM
            RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "DisablePagingExecutive", 1);
            log(new OptimizationStepLog { Category = "MEMORY", Message = "DisablePagingExecutive ativado (Kernel e drivers mantidos 100% na memória RAM)." });

            // 9. Network Latency: TCPNoDelay & TcpAckFrequency
            ApplyNetworkLatencyTweaks(log);

            // 10. Telemetry & Background Distractions
            ServiceHelper.SetStartupType("DiagTrack", "disabled");
            ServiceHelper.StopService("DiagTrack");
            ServiceHelper.SetStartupType("WerSvc", "disabled");
            ServiceHelper.StopService("WerSvc");
            RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0);
            log(new OptimizationStepLog { Category = "TELEMETRY", Message = "Serviços de telemetria básica (DiagTrack, WerSvc) desativados." });

            log(new OptimizationStepLog { Category = "GAMING", Message = "Windows Gaming Mode aplicado com sucesso! (Recomendado reiniciar)", Success = true });
        }
        #endregion

        #region Mode 3: Windows Gaming Ultra
        public void ApplyGamingUltra(Action<OptimizationStepLog> log)
        {
            log(new OptimizationStepLog { Category = "ULTRA", Message = "ATIVANDO 'WINDOWS GAMING ULTRA' (Modo Extremo 100% Gamer)..." });

            // 1. Criar backup automático prévio de segurança caso não exista
            if (!_backupEngine.HasExistingBackup())
            {
                log(new OptimizationStepLog { Category = "ULTRA", Message = "Gerando backup de segurança automático antes da otimização profunda..." });
                _backupEngine.CreateFullBackup(log);
            }

            // 2. Executa toda a base do Gaming Mode primeiro
            ApplyGamingMode(log);

            // 3. SEGURANÇA & ANTIVÍRUS: Desativação do Windows Defender e SmartScreen (Ideal para VM e benchmarks)
            log(new OptimizationStepLog { Category = "SECURITY", Message = "Desativando sobrecarga de monitoramento em tempo real do Windows Defender e SmartScreen..." });
            try
            {
                string defKey = @"SOFTWARE\Policies\Microsoft\Windows Defender";
                RegistryHelper.SetDWord(RegistryHive.LocalMachine, defKey, "DisableAntiSpyware", 1);

                string rtKey = @"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection";
                RegistryHelper.SetDWord(RegistryHive.LocalMachine, rtKey, "DisableRealtimeMonitoring", 1);
                RegistryHelper.SetDWord(RegistryHive.LocalMachine, rtKey, "DisableBehaviorMonitoring", 1);
                RegistryHelper.SetDWord(RegistryHive.LocalMachine, rtKey, "DisableOnAccessProtection", 1);
                RegistryHelper.SetDWord(RegistryHive.LocalMachine, rtKey, "DisableScanOnRealtimeEnable", 1);
                RegistryHelper.SetDWord(RegistryHive.LocalMachine, rtKey, "DisableIOAVProtection", 1);

                string spKey = @"SOFTWARE\Policies\Microsoft\Windows Defender\Spynet";
                RegistryHelper.SetDWord(RegistryHive.LocalMachine, spKey, "MAPSReporting", 0);
                RegistryHelper.SetDWord(RegistryHive.LocalMachine, spKey, "SubmitSamplesConsent", 2);

                // SmartScreen
                RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "EnableSmartScreen", 0);
                RegistryHelper.SetString(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "SmartScreenEnabled", "Off");
                RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\AppHost", "EnableWebContentEvaluation", 0);

                log(new OptimizationStepLog { Category = "SECURITY", Message = "Defender Real-Time Protection e SmartScreen desativados (Ganho substancial de I/O em jogos)." });
            }
            catch (Exception ex)
            {
                log(new OptimizationStepLog { Category = "SECURITY", Message = $"Aviso ao alterar Defender: {ex.Message}", Success = false });
            }

            // 4. VBS (Virtualization-Based Security / HVCI Memory Integrity)
            // Causa de perda de 10-25% de FPS no Windows 11
            log(new OptimizationStepLog { Category = "VBS", Message = "Desativando VBS (Virtualization-Based Security) e HVCI..." });
            BcdHelper.SetBcdValue("hypervisorlaunchtype", "off");
            RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\DeviceGuard", "EnableVirtualizationBasedSecurity", 0);
            RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled", 0);
            log(new OptimizationStepLog { Category = "VBS", Message = "VBS e Hypervisor desligados no boot (Latência de CPU eliminada)." });

            // 5. Mitigações de CPU (Spectre & Meltdown) - Desativa patch de perda de IPC em VM de testes
            RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "FeatureSettingsOverride", 3);
            RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "FeatureSettingsOverrideMask", 3);
            log(new OptimizationStepLog { Category = "CPU", Message = "Mitigações Spectre/Meltdown desativadas (Desbloqueio de máxima velocidade IPC)." });

            // 6. Timers de Alta Precisão & Eliminação de Micro-Stuttering
            log(new OptimizationStepLog { Category = "TIMERS", Message = "Configurando relógios de precisão estrita (BCD Dynamic Tick Off)..." });
            BcdHelper.SetBcdValue("useplatformtick", "yes");
            BcdHelper.SetBcdValue("disabledynamictick", "yes");
            BcdHelper.SetBcdValue("useplatformclock", "no");
            Win32Native.SetHighPrecisionTimer(5000); // Força 0.5ms timer resolution
            log(new OptimizationStepLog { Category = "TIMERS", Message = "Dynamic Tick desativado e Timer Resolution configurado para 0.5ms." });

            // 7. Desativação Profunda de Serviços Pesados (Debloat Completo)
            string[] heavyServices = new string[]
            {
                "SysMain",          // Superfetch (Disk thrashing e micro-stutters)
                "WSearch",          // Windows Search Indexer (CPU e I/O de disco)
                "DPS",              // Diagnostic Policy Service
                "WdiServiceHost",   // Diagnostic Service Host
                "WdiSystemHost",    // Diagnostic System Host
                "DoSvc",            // Delivery Optimization (P2P Windows update)
                "wuauserv",         // Windows Update (congela atualizações durante jogos)
                "UsoSvc",           // Update Orchestrator
                "BITS",             // Background Intelligent Transfer
                "spoolsv",          // Print Spooler (inútil em VM gamer)
                "Fax",              // Fax
                "RemoteRegistry",   // Registro remoto
                "WbioSrvc",         // Biometria
                "XblAuthManager",   // Xbox Live Auth
                "XblGameSave",      // Xbox Save
                "XboxNetApiSvc",    // Xbox Network
                "XboxGipSvc"        // Xbox Accessory
            };

            log(new OptimizationStepLog { Category = "SERVICES", Message = $"Interrompendo e desativando {heavyServices.Length} serviços pesados em segundo plano..." });
            foreach (var s in heavyServices)
            {
                ServiceHelper.SetStartupType(s, "disabled");
                ServiceHelper.StopService(s);
            }
            log(new OptimizationStepLog { Category = "SERVICES", Message = "Serviços em segundo plano eliminados com sucesso." });

            // 8. Otimização de Memória & Standby List
            log(new OptimizationStepLog { Category = "MEMORY", Message = "Desativando Memory Compression e limpando lista Standby de memória RAM..." });
            RunPowerShell("Disable-MMAgent -mc -pc");
            Win32Native.PurgeStandbyList();
            RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters", "EnableSuperfetch", 0);
            RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters", "EnablePrefetcher", 0);
            log(new OptimizationStepLog { Category = "MEMORY", Message = "Compressão de RAM e Superfetch desativados. Standby List esvaziada." });

            // 9. Visual Effects & DWM (Reduzir overhead de composicão)
            RegistryHelper.SetString(RegistryHive.CurrentUser, @"Control Panel\Desktop\WindowMetrics", "MinAnimate", "0");
            RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0);
            RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 2);
            log(new OptimizationStepLog { Category = "VISUALS", Message = "Efeitos visuais desnecessários e transparência da DWM desativados." });

            // 10. Bloatware & Tarefas Agendadas de Telemetria (CEIP)
            DisableScheduledBloatTasks(log);

            log(new OptimizationStepLog { Category = "ULTRA", Message = "=== MODO WINDOWS GAMING ULTRA ATIVADO COM SUCESSO! ===", Success = true });
            log(new OptimizationStepLog { Category = "ULTRA", Message = "Reinicie a máquina virtual/computador para aplicar todas as diretivas do Kernel e BCD.", Success = true });
        }
        #endregion

        #region Helpers
        private void ConfigureUltimatePerformance(Action<OptimizationStepLog> log)
        {
            try
            {
                // Duplica o esquema Ultimate Performance caso não exista
                RunCommand("powercfg.exe", "-duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61");
                RunCommand("powercfg.exe", "/setactive e9a42b02-d5df-448d-aa00-03f14749eb61");

                // Core Unparking (Min cores = 100%, Max cores = 100%)
                RunCommand("powercfg.exe", "-setacvalueindex scheme_current sub_processor CPMINCORES 100");
                RunCommand("powercfg.exe", "-setacvalueindex scheme_current sub_processor CPMAXCORES 100");
                RunCommand("powercfg.exe", "/setactive scheme_current");

                log(new OptimizationStepLog { Category = "POWER", Message = "Plano 'Desempenho Máximo' (Ultimate Performance) ativado e Core Parking desabilitado." });
            }
            catch (Exception ex)
            {
                log(new OptimizationStepLog { Category = "POWER", Message = $"Falha ao configurar plano de energia: {ex.Message}", Success = false });
            }
        }

        private void ApplyNetworkLatencyTweaks(Action<OptimizationStepLog> log)
        {
            try
            {
                // Desativa heurísticas de escala e otimiza autotuning
                RunCommand("netsh.exe", "int tcp set heuristics disabled");
                RunCommand("netsh.exe", "int tcp set global autotuninglevel=normal");
                RunCommand("netsh.exe", "int tcp set global ecncapability=disabled");
                RunCommand("netsh.exe", "int tcp set global rss=enabled");

                // Configura TcpAckFrequency e TCPNoDelay em todas as interfaces de rede
                string interfacesPath = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var intKey = baseKey.OpenSubKey(interfacesPath, true))
                {
                    if (intKey != null)
                    {
                        foreach (var subName in intKey.GetSubKeyNames())
                        {
                            using (var sub = intKey.OpenSubKey(subName, true))
                            {
                                if (sub != null)
                                {
                                    sub.SetValue("TcpAckFrequency", 1, RegistryValueKind.DWord);
                                    sub.SetValue("TCPNoDelay", 1, RegistryValueKind.DWord);
                                }
                            }
                        }
                    }
                }
                log(new OptimizationStepLog { Category = "NETWORK", Message = "Algoritmo de Nagle desativado (TcpAckFrequency=1, TCPNoDelay=1 nas interfaces)." });
            }
            catch (Exception ex)
            {
                log(new OptimizationStepLog { Category = "NETWORK", Message = $"Aviso nos ajustes de rede: {ex.Message}", Success = false });
            }
        }

        private void DisableScheduledBloatTasks(Action<OptimizationStepLog> log)
        {
            string[] tasks = new string[]
            {
                @"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator",
                @"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip",
                @"\Microsoft\Windows\Customer Experience Improvement Program\KernelCeipTask",
                @"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
                @"\Microsoft\Windows\Application Experience\ProgramDataUpdater",
                @"\Microsoft\Windows\Autochk\Proxy"
            };

            foreach (var t in tasks)
            {
                RunCommand("schtasks.exe", $"/change /tn \"{t}\" /disable");
            }
            log(new OptimizationStepLog { Category = "TASKS", Message = "Tarefas agendadas de diagnóstico e telemetria (CEIP) desativadas." });
        }

        private void RunCommand(string file, string args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = file,
                    Arguments = args,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using (var p = Process.Start(psi))
                {
                    p?.WaitForExit(3000);
                }
            }
            catch { }
        }

        private void RunPowerShell(string script)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using (var p = Process.Start(psi))
                {
                    p?.WaitForExit(5000);
                }
            }
            catch { }
        }
        #endregion
    }
}
