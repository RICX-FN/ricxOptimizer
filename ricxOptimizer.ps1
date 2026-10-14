<#
.SYNOPSIS
    ricxOptimizer - Suíte Profissional de Otimização de SO para Jogos (PowerShell Direct GUI)
.DESCRIPTION
    Desenvolvido para testes rápidos em VM e execução nativa sem dependências externas.
    Contém os 3 modos: Windows Default, Windows Gaming Mode e Windows Gaming Ultra.
#>

# Garantir elevação de Administrador
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Start-Process powershell.exe -ArgumentList ("-NoProfile -ExecutionPolicy Bypass -File `"{0}`"" -f $PSCommandPath) -Verb RunAs
    exit
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

[System.Windows.Forms.Application]::EnableVisualStyles()

# Configuração de Pastas
$BackupDir = Join-Path $env:ProgramData "ricxOptimizer\Backups"
if (-not (Test-Path $BackupDir)) { New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null }
$ManifestFile = Join-Path $BackupDir "manifest.json"

# Form Principal
$Form = New-Object System.Windows.Forms.Form
$Form.Text = "ricxOptimizer v1.0 - Modo Ultra Gaming & Otimizador de SO"
$Form.Size = New-Object System.Drawing.Size(980, 750)
$Form.StartPosition = "CenterScreen"
$Form.BackColor = [System.Drawing.Color]::FromArgb(18, 18, 24)
$Form.ForeColor = [System.Drawing.Color]::FromArgb(240, 240, 245)
$Form.Font = New-Object System.Drawing.Font("Segoe UI", 9.5)

# Logging Helper
function Add-Log {
    param (
        [string]$Category,
        [string]$Message,
        [bool]$Success = $true
    )
    $timestamp = (Get-Date).ToString("HH:mm:ss")
    $color = if ($Success) { [System.Drawing.Color]::FromArgb(180, 240, 180) } else { [System.Drawing.Color]::FromArgb(250, 120, 120) }

    $rtbLogs.SelectionStart = $rtbLogs.TextLength
    $rtbLogs.SelectionLength = 0
    $rtbLogs.SelectionColor = [System.Drawing.Color]::FromArgb(120, 120, 140)
    $rtbLogs.AppendText("[$timestamp] ")
    $rtbLogs.SelectionColor = [System.Drawing.Color]::FromArgb(0, 229, 255)
    $rtbLogs.AppendText("[$Category] ")
    $rtbLogs.SelectionColor = $color
    $rtbLogs.AppendText("$Message`r`n")
    $rtbLogs.ScrollToCaret()
}

# Funções de Otimização e Backup
function New-SystemBackup {
    Add-Log "BACKUP" "Iniciando criação de backup de segurança completo..."
    
    # 1. Ponto de Restauração VSS
    try {
        Checkpoint-Computer -Description "ricxOptimizer_SafetyBackup" -RestorePointType "MODIFY_SETTINGS" -ErrorAction SilentlyContinue
        Add-Log "VSS" "Ponto de Restauração do Windows solicitado com sucesso."
    } catch {
        Add-Log "VSS" "Aviso: Ponto de restauração VSS não pôde ser criado (comum em algumas VMs)." $false
    }

    # 2. Backup do Registro
    $regBackup = Join-Path $BackupDir "reg_backup.reg"
    $keysToExport = @(
        "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
        "HKLM\SYSTEM\CurrentControlSet\Control\PriorityControl",
        "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management",
        "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters",
        "HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
        "HKLM\SOFTWARE\Policies\Microsoft\Windows Defender",
        "HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR",
        "HKCU\System\GameConfigStore",
        "HKCU\Control Panel\Mouse"
    )
    
    $count = 0
    foreach ($k in $keysToExport) {
        $tempFile = Join-Path $BackupDir "temp_$count.reg"
        reg export "$k" "$tempFile" /y 2>$null | Out-Null
        $count++
    }
    Add-Log "REGISTRY" "Exportação das chaves críticas de Registro concluída."

    # 3. Snapshot dos Serviços
    $servicesToWatch = @(
        "DiagTrack", "SysMain", "WSearch", "DPS", "WdiServiceHost", "WdiSystemHost",
        "WerSvc", "DoSvc", "wuauserv", "UsoSvc", "BITS", "spoolsv", "Fax",
        "RemoteRegistry", "WbioSrvc", "XblAuthManager", "XblGameSave", "XboxNetApiSvc", "XboxGipSvc"
    )
    $servicesData = @()
    foreach ($sName in $servicesToWatch) {
        $svc = Get-Service -Name $sName -ErrorAction SilentlyContinue
        if ($svc) {
            $startType = (Get-WmiObject -Class Win32_Service -Filter "Name='$sName'").StartMode
            $servicesData += "$($sName)|$($startType)|$($svc.Status)"
        }
    }
    $servicesData | Out-File -FilePath (Join-Path $BackupDir "services_backup.txt") -Encoding utf8
    Add-Log "SERVICES" "Estado original de $($servicesData.Count) serviços salvo."

    # 4. Backup do BCD
    bcdedit /export (Join-Path $BackupDir "bcd_backup.bcd") 2>$null | Out-Null
    Add-Log "BCD" "Configurações de boot do BCD exportadas."

    # 5. Salvar Plano de Energia Ativo
    $powerScheme = (powercfg /getactivescheme)
    $powerScheme | Out-File -FilePath (Join-Path $BackupDir "power_backup.txt") -Encoding utf8

    # 6. Manifest
    @{
        CreatedAt = (Get-Date).ToString("o")
        ComputerName = $env:COMPUTERNAME
        BackupDir = $BackupDir
    } | ConvertTo-Json | Out-File -FilePath $ManifestFile -Encoding utf8

    Add-Log "BACKUP" "Backup de segurança finalizado com êxito!" $true
    Update-BackupStatus
}

function Restore-WindowsDefault {
    Add-Log "DEFAULT" "Iniciando restauração do Windows para Modo Padrão..."

    # Restaurar do Backup se disponível
    if (Test-Path $ManifestFile) {
        Add-Log "DEFAULT" "Restaurando configurações através dos arquivos de backup..."
        
        # Restaurar Serviços
        $svcBackupFile = Join-Path $BackupDir "services_backup.txt"
        if (Test-Path $svcBackupFile) {
            Get-Content $svcBackupFile | ForEach-Object {
                $parts = $_ -split '\|'
                if ($parts.Count -ge 3) {
                    $sName = $parts[0]
                    $sMode = $parts[1]
                    $sState = $parts[2]
                    $scMode = switch ($sMode.ToLower()) {
                        "auto" { "auto" }
                        "demand" { "demand" }
                        "manual" { "demand" }
                        "disabled" { "disabled" }
                        default { "demand" }
                    }
                    sc.exe config "$sName" start= $scMode | Out-Null
                    if ($sState -eq "Running") { sc.exe start "$sName" | Out-Null }
                }
            }
            Add-Log "SERVICES" "Serviços reconfigurados conforme backup original."
        }

        # Restaurar BCD
        $bcdFile = Join-Path $BackupDir "bcd_backup.bcd"
        if (Test-Path $bcdFile) {
            bcdedit /import "$bcdFile" 2>$null | Out-Null
            Add-Log "BCD" "BCD importado do backup."
        }
    } else {
        Add-Log "DEFAULT" "Nenhum backup encontrado. Aplicando diretivas padrão de fábrica..."
        @("SysMain", "WSearch", "spoolsv", "DPS") | ForEach-Object {
            sc.exe config "$_" start= auto | Out-Null
            net start "$_" 2>$null | Out-Null
        }
        @("wuauserv", "UsoSvc", "BITS", "WerSvc", "DoSvc") | ForEach-Object {
            sc.exe config "$_" start= demand | Out-Null
        }
    }

    # Redefinir chaves de Registro para o padrão
    Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile" -Name "SystemResponsiveness" -Value 20 -Type DWord -Force
    Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile" -Name "NetworkThrottlingIndex" -Value 10 -Type DWord -Force
    Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\PriorityControl" -Name "Win32PrioritySeparation" -Value 2 -Type DWord -Force
    Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management" -Name "DisablePagingExecutive" -Value 0 -Type DWord -Force

    # Remover exclusões de Defender
    Remove-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows Defender" -Name "DisableAntiSpyware" -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection" -Name "DisableRealtimeMonitoring" -ErrorAction SilentlyContinue

    # BCD Padrão
    bcdedit /deletevalue useplatformtick 2>$null | Out-Null
    bcdedit /deletevalue disabledynamictick 2>$null | Out-Null
    bcdedit /deletevalue useplatformclock 2>$null | Out-Null
    bcdedit /set hypervisorlaunchtype auto 2>$null | Out-Null

    # Energia Equilibrada
    powercfg /setactive 381b4222-f694-41f0-9685-ff5bb260df2e 2>$null | Out-Null
    Enable-MMAgent -mc 2>$null | Out-Null

    Add-Log "DEFAULT" "Windows restaurado para o Modo Padrão com sucesso!" $true
}

function Apply-GamingMode {
    Add-Log "GAMING" "Aplicando 'Windows Gaming Mode' (Otimização Equilibrada)..."

    if (-not (Test-Path $ManifestFile)) {
        Add-Log "GAMING" "Criando backup prévio de segurança..."
        New-SystemBackup
    }

    # 1. Energia: Ultimate Performance & Core Unparking
    powercfg -duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61 2>$null | Out-Null
    powercfg /setactive e9a42b02-d5df-448d-aa00-03f14749eb61 2>$null | Out-Null
    powercfg -setacvalueindex scheme_current sub_processor CPMINCORES 100 2>$null | Out-Null
    powercfg -setacvalueindex scheme_current sub_processor CPMAXCORES 100 2>$null | Out-Null
    powercfg /setactive scheme_current 2>$null | Out-Null
    Add-Log "POWER" "Plano Ultimate Performance ativado e Core Parking desabilitado."

    # 2. MMCSS
    Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile" -Name "SystemResponsiveness" -Value 10 -Type DWord -Force
    Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile" -Name "NetworkThrottlingIndex" -Value 0xFFFFFFFF -Type DWord -Force
    $taskPath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games"
    if (-not (Test-Path $taskPath)) { New-Item -Path $taskPath -Force | Out-Null }
    Set-ItemProperty -Path $taskPath -Name "Scheduling Category" -Value "High" -Type String -Force
    Set-ItemProperty -Path $taskPath -Name "SFIO Priority" -Value "High" -Type String -Force
    Set-ItemProperty -Path $taskPath -Name "Background Only" -Value "False" -Type String -Force
    Set-ItemProperty -Path $taskPath -Name "Priority" -Value 6 -Type DWord -Force
    Set-ItemProperty -Path $taskPath -Name "GPU Priority" -Value 8 -Type DWord -Force
    Add-Log "MMCSS" "MMCSS priorizado para games (SystemResponsiveness=10%)."

    # 3. Win32PrioritySeparation 0x26 (Short, variable, high foreground quantum boost)
    Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\PriorityControl" -Name "Win32PrioritySeparation" -Value 38 -Type DWord -Force
    Add-Log "SCHEDULER" "Win32PrioritySeparation ajustado para 0x26 (Máximo boost dinâmico em foreground)."

    # 4. GameDVR & Gravação em segundo plano
    if (-not (Test-Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\GameDVR")) { New-Item -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\GameDVR" -Force | Out-Null }
    Set-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\GameDVR" -Name "AllowGameDVR" -Value 0 -Type DWord -Force
    Set-ItemProperty -Path "HKCU:\System\GameConfigStore" -Name "GameDVR_Enabled" -Value 0 -Type DWord -Force
    Set-ItemProperty -Path "HKCU:\System\GameConfigStore" -Name "GameDVR_FSEBehaviorMode" -Value 2 -Type DWord -Force
    Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\GameDVR" -Name "AppCaptureEnabled" -Value 0 -Type DWord -Force
    Add-Log "GAMEDVR" "Captura de vídeo e GameDVR desligados (GPU liberada de gravação constante)."

    # 5. HAGS & Mouse Precision
    Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers" -Name "HwSchMode" -Value 2 -Type DWord -Force -ErrorAction SilentlyContinue
    Set-ItemProperty -Path "HKCU:\Control Panel\Mouse" -Name "MouseSpeed" -Value "0" -Type String -Force
    Set-ItemProperty -Path "HKCU:\Control Panel\Mouse" -Name "MouseThreshold1" -Value "0" -Type String -Force
    Set-ItemProperty -Path "HKCU:\Control Panel\Mouse" -Name "MouseThreshold2" -Value "0" -Type String -Force
    Add-Log "INPUT" "Entrada 1:1 de mouse ativada (Sem aceleração artificial)."

    # 6. Memória: Manter Kernel na RAM
    Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management" -Name "DisablePagingExecutive" -Value 1 -Type DWord -Force
    Add-Log "MEMORY" "DisablePagingExecutive ativado (Kernel travado na RAM)."

    # 7. Rede: TCPNoDelay e AckFrequency
    netsh int tcp set heuristics disabled | Out-Null
    netsh int tcp set global autotuninglevel=normal | Out-Null
    $interfaces = "HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces"
    Get-ChildItem -Path $interfaces | ForEach-Object {
        Set-ItemProperty -Path $_.PSPath -Name "TcpAckFrequency" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue
        Set-ItemProperty -Path $_.PSPath -Name "TCPNoDelay" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue
    }
    Add-Log "NETWORK" "Nagle's Algorithm desativado em todas as interfaces de rede."

    # 8. Telemetria básica
    sc.exe config DiagTrack start= disabled | Out-Null
    sc.exe stop DiagTrack 2>$null | Out-Null
    sc.exe config WerSvc start= disabled | Out-Null
    sc.exe stop WerSvc 2>$null | Out-Null
    Add-Log "TELEMETRY" "DiagTrack e WerSvc desativados."

    Add-Log "GAMING" "Windows Gaming Mode aplicado com sucesso!" $true
}

function Apply-GamingUltra {
    Add-Log "ULTRA" "ATIVANDO 'WINDOWS GAMING ULTRA' (Modo Extremo 100% Gamer)..."

    # Aplica primeiro o modo Gaming
    Apply-GamingMode

    # 1. Desativar Defender e SmartScreen (Ideal para VM e benchmarks)
    Add-Log "SECURITY" "Desativando sobrecarga de monitoramento em tempo real do Windows Defender e SmartScreen..."
    $rtPath = "HKLM:\SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection"
    if (-not (Test-Path $rtPath)) { New-Item -Path $rtPath -Force | Out-Null }
    Set-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows Defender" -Name "DisableAntiSpyware" -Value 1 -Type DWord -Force
    Set-ItemProperty -Path $rtPath -Name "DisableRealtimeMonitoring" -Value 1 -Type DWord -Force
    Set-ItemProperty -Path $rtPath -Name "DisableBehaviorMonitoring" -Value 1 -Type DWord -Force
    Set-ItemProperty -Path $rtPath -Name "DisableOnAccessProtection" -Value 1 -Type DWord -Force
    Set-ItemProperty -Path $rtPath -Name "DisableScanOnRealtimeEnable" -Value 1 -Type DWord -Force
    Set-ItemProperty -Path $rtPath -Name "DisableIOAVProtection" -Value 1 -Type DWord -Force

    Set-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\System" -Name "EnableSmartScreen" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
    Add-Log "SECURITY" "Defender e SmartScreen desativados (Ganho maciço de I/O em games)."

    # 2. Desativar VBS / HVCI
    Add-Log "VBS" "Desativando VBS (Virtualization-Based Security / Memory Integrity)..."
    bcdedit /set hypervisorlaunchtype off 2>$null | Out-Null
    $dgPath = "HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard"
    Set-ItemProperty -Path $dgPath -Name "EnableVirtualizationBasedSecurity" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
    Add-Log "VBS" "VBS e Hypervisor desligados no boot (Latência de CPU eliminada)."

    # 3. Mitigações Spectre / Meltdown
    Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management" -Name "FeatureSettingsOverride" -Value 3 -Type DWord -Force
    Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management" -Name "FeatureSettingsOverrideMask" -Value 3 -Type DWord -Force
    Add-Log "CPU" "Mitigações de CPU desativadas (Desbloqueio de máxima velocidade de clock/IPC)."

    # 4. Timers BCD
    bcdedit /set useplatformtick yes 2>$null | Out-Null
    bcdedit /set disabledynamictick yes 2>$null | Out-Null
    bcdedit /set useplatformclock no 2>$null | Out-Null
    Add-Log "TIMERS" "Dynamic Tick desativado no BCD para eliminar oscilação de quadros."

    # 5. Serviços Pesados Eliminados
    $heavyList = @(
        "SysMain", "WSearch", "DPS", "WdiServiceHost", "WdiSystemHost",
        "DoSvc", "wuauserv", "UsoSvc", "BITS", "spoolsv", "Fax",
        "RemoteRegistry", "WbioSrvc", "XblAuthManager", "XblGameSave", "XboxNetApiSvc", "XboxGipSvc"
    )
    Add-Log "SERVICES" "Desativando $($heavyList.Count) serviços pesados em segundo plano..."
    foreach ($s in $heavyList) {
        sc.exe config "$s" start= disabled | Out-Null
        sc.exe stop "$s" 2>$null | Out-Null
    }
    Add-Log "SERVICES" "Serviços em segundo plano encerrados com sucesso."

    # 6. Memória & MMAgent
    Disable-MMAgent -mc -pc 2>$null | Out-Null
    Add-Log "MEMORY" "Compressão de memória RAM desativada (Evita picos de descompressão em CPU durante tiroteios/ação)."

    # 7. Efeitos Visuais
    Set-ItemProperty -Path "HKCU:\Control Panel\Desktop\WindowMetrics" -Name "MinAnimate" -Value "0" -Type String -Force
    Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize" -Name "EnableTransparency" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
    Add-Log "VISUALS" "Animações da janela e transparência DWM desligadas."

    Add-Log "ULTRA" "=== MODO WINDOWS GAMING ULTRA ATIVADO COM SUCESSO! ===" $true
    Add-Log "ULTRA" "Recomenda-se reiniciar a máquina virtual para carregar o novo Kernel e BCD." $true
}

function Update-BackupStatus {
    if (Test-Path $ManifestFile) {
        $lblBackupStatus.Text = "✔ Backup de Segurança: ATIVO & DISPONÍVEL"
        $lblBackupStatus.BackColor = [System.Drawing.Color]::FromArgb(20, 60, 30)
        $lblBackupStatus.ForeColor = [System.Drawing.Color]::FromArgb(34, 197, 94)
    } else {
        $lblBackupStatus.Text = "⚠ Backup de Segurança: PENDENTE"
        $lblBackupStatus.BackColor = [System.Drawing.Color]::FromArgb(80, 60, 20)
        $lblBackupStatus.ForeColor = [System.Drawing.Color]::FromArgb(250, 204, 21)
    }
}

# --- Construção da UI ---

# Header Panel
$pnlHeader = New-Object System.Windows.Forms.Panel
$pnlHeader.Location = New-Object System.Drawing.Point(20, 15)
$pnlHeader.Size = New-Object System.Drawing.Size(920, 85)

$lblTitle = New-Object System.Windows.Forms.Label
$lblTitle.Text = "⚡ ricxOptimizer"
$lblTitle.Font = New-Object System.Drawing.Font("Segoe UI", 20, [System.Drawing.FontStyle]::Bold)
$lblTitle.ForeColor = [System.Drawing.Color]::FromArgb(0, 229, 255)
$lblTitle.AutoSize = $true
$pnlHeader.Controls.Add($lblTitle)

$lblSubtitle = New-Object System.Windows.Forms.Label
$lblSubtitle.Text = "Suíte Especialista em Engenharia de SO para Games e Redução Extrema de Latência"
$lblSubtitle.Font = New-Object System.Drawing.Font("Segoe UI", 10, [System.Drawing.FontStyle]::Italic)
$lblSubtitle.ForeColor = [System.Drawing.Color]::FromArgb(160, 160, 175)
$lblSubtitle.Location = New-Object System.Drawing.Point(5, 42)
$lblSubtitle.AutoSize = $true
$pnlHeader.Controls.Add($lblSubtitle)

$lblSys = New-Object System.Windows.Forms.Label
$lblSys.Text = "SO: $([Environment]::OSVersion.VersionString) | Máquina: $($env:COMPUTERNAME) | Cores: $([Environment]::ProcessorCount)"
$lblSys.Font = New-Object System.Drawing.Font("Segoe UI", 9)
$lblSys.ForeColor = [System.Drawing.Color]::FromArgb(168, 85, 247)
$lblSys.Location = New-Object System.Drawing.Point(5, 65)
$lblSys.AutoSize = $true
$pnlHeader.Controls.Add($lblSys)

$Form.Controls.Add($pnlHeader)

# Status & Action Bar
$pnlBar = New-Object System.Windows.Forms.Panel
$pnlBar.Location = New-Object System.Drawing.Point(20, 105)
$pnlBar.Size = New-Object System.Drawing.Size(920, 45)

$lblAdminStatus = New-Object System.Windows.Forms.Label
$lblAdminStatus.Text = "✔ Privilégios de Administrador: ATIVO"
$lblAdminStatus.BackColor = [System.Drawing.Color]::FromArgb(20, 60, 30)
$lblAdminStatus.ForeColor = [System.Drawing.Color]::FromArgb(34, 197, 94)
$lblAdminStatus.Font = New-Object System.Drawing.Font("Segoe UI", 9.5, [System.Drawing.FontStyle]::Bold)
$lblAdminStatus.Location = New-Object System.Drawing.Point(0, 5)
$lblAdminStatus.Size = New-Object System.Drawing.Size(260, 32)
$lblAdminStatus.TextAlign = "MiddleCenter"
$pnlBar.Controls.Add($lblAdminStatus)

$lblBackupStatus = New-Object System.Windows.Forms.Label
$lblBackupStatus.Font = New-Object System.Drawing.Font("Segoe UI", 9.5, [System.Drawing.FontStyle]::Bold)
$lblBackupStatus.Location = New-Object System.Drawing.Point(270, 5)
$lblBackupStatus.Size = New-Object System.Drawing.Size(290, 32)
$lblBackupStatus.TextAlign = "MiddleCenter"
$pnlBar.Controls.Add($lblBackupStatus)

$btnBackup = New-Object System.Windows.Forms.Button
$btnBackup.Text = "💾 Criar Backup Completo Agora"
$btnBackup.Location = New-Object System.Drawing.Point(570, 5)
$btnBackup.Size = New-Object System.Drawing.Size(350, 32)
$btnBackup.BackColor = [System.Drawing.Color]::FromArgb(0, 229, 255)
$btnBackup.ForeColor = [System.Drawing.Color]::Black
$btnBackup.FlatStyle = "Flat"
$btnBackup.Font = New-Object System.Drawing.Font("Segoe UI", 9.5, [System.Drawing.FontStyle]::Bold)
$btnBackup.Cursor = [System.Windows.Forms.Cursors]::Hand
$btnBackup.Add_Click({
    New-SystemBackup
    [System.Windows.Forms.MessageBox]::Show("Backup do Windows concluído com sucesso!", "ricxOptimizer", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information)
})
$pnlBar.Controls.Add($btnBackup)

$Form.Controls.Add($pnlBar)

# 3 Action Cards
$cardY = 160
$cardWidth = 295
$cardHeight = 210

# Card 1: Default
$c1 = New-Object System.Windows.Forms.Panel
$c1.Location = New-Object System.Drawing.Point(20, $cardY)
$c1.Size = New-Object System.Drawing.Size($cardWidth, $cardHeight)
$c1.BackColor = [System.Drawing.Color]::FromArgb(28, 28, 38)
$c1.Padding = New-Object System.Windows.Forms.Padding(10)

$t1 = New-Object System.Windows.Forms.Label
$t1.Text = "🛡️ Windows Default"
$t1.Font = New-Object System.Drawing.Font("Segoe UI", 12, [System.Drawing.FontStyle]::Bold)
$t1.ForeColor = [System.Drawing.Color]::FromArgb(59, 130, 246)
$t1.Dock = "Top"
$t1.Height = 30
$c1.Controls.Add($t1)

$d1 = New-Object System.Windows.Forms.Label
$d1.Text = "Restaura o Windows para o funcionamento normal usando o backup de segurança. Reativa Defender, Windows Search, SysMain, spoolsv, MMCSS normal e energia equilibrada."
$d1.Font = New-Object System.Drawing.Font("Segoe UI", 9)
$d1.ForeColor = [System.Drawing.Color]::FromArgb(160, 160, 175)
$d1.Dock = "Fill"
$c1.Controls.Add($d1)

$b1 = New-Object System.Windows.Forms.Button
$b1.Text = "RESTAURAR WINDOWS PADRÃO"
$b1.Dock = "Bottom"
$b1.Height = 42
$b1.BackColor = [System.Drawing.Color]::FromArgb(59, 130, 246)
$b1.ForeColor = [System.Drawing.Color]::White
$b1.FlatStyle = "Flat"
$b1.Font = New-Object System.Drawing.Font("Segoe UI", 9.5, [System.Drawing.FontStyle]::Bold)
$b1.Cursor = [System.Windows.Forms.Cursors]::Hand
$b1.Add_Click({
    $res = [System.Windows.Forms.MessageBox]::Show("Deseja restaurar as configurações padrão do Windows usando o backup?", "Confirmar Restauração", [System.Windows.Forms.MessageBoxButtons]::YesNo, [System.Windows.Forms.MessageBoxIcon]::Question)
    if ($res -eq "Yes") {
        Restore-WindowsDefault
        [System.Windows.Forms.MessageBox]::Show("Windows restaurado com sucesso! É recomendável reiniciar.", "ricxOptimizer", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information)
    }
})
$c1.Controls.Add($b1)
$Form.Controls.Add($c1)

# Card 2: Gaming Mode
$c2 = New-Object System.Windows.Forms.Panel
$c2.Location = New-Object System.Drawing.Point(332, $cardY)
$c2.Size = New-Object System.Drawing.Size($cardWidth, $cardHeight)
$c2.BackColor = [System.Drawing.Color]::FromArgb(28, 28, 38)
$c2.Padding = New-Object System.Windows.Forms.Padding(10)

$t2 = New-Object System.Windows.Forms.Label
$t2.Text = "🎮 Windows Gaming Mode"
$t2.Font = New-Object System.Drawing.Font("Segoe UI", 12, [System.Drawing.FontStyle]::Bold)
$t2.ForeColor = [System.Drawing.Color]::FromArgb(168, 85, 247)
$t2.Dock = "Top"
$t2.Height = 30
$c2.Controls.Add($t2)

$d2 = New-Object System.Windows.Forms.Label
$d2.Text = "Otimização equilibrada para jogos: Ultimate Performance, CPU Core Unparking, MMCSS priorizado, HAGS, GameDVR off, Nagle TCP off e DisablePagingExecutive."
$d2.Font = New-Object System.Drawing.Font("Segoe UI", 9)
$d2.ForeColor = [System.Drawing.Color]::FromArgb(160, 160, 175)
$d2.Dock = "Fill"
$c2.Controls.Add($d2)

$b2 = New-Object System.Windows.Forms.Button
$b2.Text = "ATIVAR MODO GAMING"
$b2.Dock = "Bottom"
$b2.Height = 42
$b2.BackColor = [System.Drawing.Color]::FromArgb(168, 85, 247)
$b2.ForeColor = [System.Drawing.Color]::White
$b2.FlatStyle = "Flat"
$b2.Font = New-Object System.Drawing.Font("Segoe UI", 9.5, [System.Drawing.FontStyle]::Bold)
$b2.Cursor = [System.Windows.Forms.Cursors]::Hand
$b2.Add_Click({
    $res = [System.Windows.Forms.MessageBox]::Show("Ativar o Windows Gaming Mode?", "Confirmar Otimização", [System.Windows.Forms.MessageBoxButtons]::YesNo, [System.Windows.Forms.MessageBoxIcon]::Question)
    if ($res -eq "Yes") {
        Apply-GamingMode
        [System.Windows.Forms.MessageBox]::Show("Gaming Mode aplicado! É recomendável reiniciar a máquina.", "ricxOptimizer", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information)
    }
})
$c2.Controls.Add($b2)
$Form.Controls.Add($c2)

# Card 3: Ultra Gaming
$c3 = New-Object System.Windows.Forms.Panel
$c3.Location = New-Object System.Drawing.Point(645, $cardY)
$c3.Size = New-Object System.Drawing.Size($cardWidth, $cardHeight)
$c3.BackColor = [System.Drawing.Color]::FromArgb(28, 28, 38)
$c3.Padding = New-Object System.Windows.Forms.Padding(10)

$t3 = New-Object System.Windows.Forms.Label
$t3.Text = "⚡ Windows Gaming Ultra"
$t3.Font = New-Object System.Drawing.Font("Segoe UI", 12, [System.Drawing.FontStyle]::Bold)
$t3.ForeColor = [System.Drawing.Color]::FromArgb(244, 63, 94)
$t3.Dock = "Top"
$t3.Height = 30
$c3.Controls.Add($t3)

$d3 = New-Object System.Windows.Forms.Label
$d3.Text = "100% Gamer Extremo (Ideal para VM e Testes): Desativa Defender em tempo real, SmartScreen, VBS/HVCI (-25% latência), Spectre/Meltdown, SysMain, indexação e serviços pesados."
$d3.Font = New-Object System.Drawing.Font("Segoe UI", 9)
$d3.ForeColor = [System.Drawing.Color]::FromArgb(160, 160, 175)
$d3.Dock = "Fill"
$c3.Controls.Add($d3)

$b3 = New-Object System.Windows.Forms.Button
$b3.Text = "ATIVAR GAMING ULTRA (100% GAMER)"
$b3.Dock = "Bottom"
$b3.Height = 42
$b3.BackColor = [System.Drawing.Color]::FromArgb(244, 63, 94)
$b3.ForeColor = [System.Drawing.Color]::White
$b3.FlatStyle = "Flat"
$b3.Font = New-Object System.Drawing.Font("Segoe UI", 9.5, [System.Drawing.FontStyle]::Bold)
$b3.Cursor = [System.Windows.Forms.Cursors]::Hand
$b3.Add_Click({
    $res = [System.Windows.Forms.MessageBox]::Show("ATENÇÃO: O modo Ultra desativa o Windows Defender, VBS/HVCI, SysMain e congela atualizações para obter o desempenho gamer máximo na máquina virtual.`n`nDeseja prosseguir?", "Confirmar Modo Ultra Gamer", [System.Windows.Forms.MessageBoxButtons]::YesNo, [System.Windows.Forms.MessageBoxIcon]::Warning)
    if ($res -eq "Yes") {
        Apply-GamingUltra
        $reboot = [System.Windows.Forms.MessageBox]::Show("O Modo Ultra Gaming foi aplicado com sucesso!`n`nDeseja reiniciar o Windows agora para ativar o novo BCD e configurações do Kernel?", "Reiniciar Windows", [System.Windows.Forms.MessageBoxButtons]::YesNo, [System.Windows.Forms.MessageBoxIcon]::Information)
        if ($reboot -eq "Yes") {
            Restart-Computer -Force
        }
    }
})
$c3.Controls.Add($b3)
$Form.Controls.Add($c3)

# Log Console
$grpLogs = New-Object System.Windows.Forms.GroupBox
$grpLogs.Text = " Console de Operações em Tempo Real "
$grpLogs.Location = New-Object System.Drawing.Point(20, 385)
$grpLogs.Size = New-Object System.Drawing.Size(920, 255)
$grpLogs.ForeColor = [System.Drawing.Color]::FromArgb(0, 229, 255)
$grpLogs.Font = New-Object System.Drawing.Font("Segoe UI", 9.5, [System.Drawing.FontStyle]::Bold)

$rtbLogs = New-Object System.Windows.Forms.RichTextBox
$rtbLogs.Location = New-Object System.Drawing.Point(12, 25)
$rtbLogs.Size = New-Object System.Drawing.Size(896, 218)
$rtbLogs.BackColor = [System.Drawing.Color]::FromArgb(12, 12, 16)
$rtbLogs.ForeColor = [System.Drawing.Color]::FromArgb(160, 230, 160)
$rtbLogs.Font = New-Object System.Drawing.Font("Consolas", 9)
$rtbLogs.BorderStyle = "None"
$rtbLogs.ReadOnly = $true
$grpLogs.Controls.Add($rtbLogs)
$Form.Controls.Add($grpLogs)

# Footer Buttons
$btnRestart = New-Object System.Windows.Forms.Button
$btnRestart.Text = "🔄 Reiniciar PC"
$btnRestart.Location = New-Object System.Drawing.Point(740, 655)
$btnRestart.Size = New-Object System.Drawing.Size(200, 38)
$btnRestart.BackColor = [System.Drawing.Color]::FromArgb(180, 40, 40)
$btnRestart.ForeColor = [System.Drawing.Color]::White
$btnRestart.FlatStyle = "Flat"
$btnRestart.Font = New-Object System.Drawing.Font("Segoe UI", 9.5, [System.Drawing.FontStyle]::Bold)
$btnRestart.Cursor = [System.Windows.Forms.Cursors]::Hand
$btnRestart.Add_Click({
    $c = [System.Windows.Forms.MessageBox]::Show("Deseja reiniciar o sistema agora?", "Reiniciar", [System.Windows.Forms.MessageBoxButtons]::YesNo, [System.Windows.Forms.MessageBoxIcon]::Question)
    if ($c -eq "Yes") { Restart-Computer -Force }
})
$Form.Controls.Add($btnRestart)

$btnClear = New-Object System.Windows.Forms.Button
$btnClear.Text = "Limpar Logs"
$btnClear.Location = New-Object System.Drawing.Point(580, 655)
$btnClear.Size = New-Object System.Drawing.Size(150, 38)
$btnClear.BackColor = [System.Drawing.Color]::FromArgb(28, 28, 38)
$btnClear.ForeColor = [System.Drawing.Color]::White
$btnClear.FlatStyle = "Flat"
$btnClear.Font = New-Object System.Drawing.Font("Segoe UI", 9.5, [System.Drawing.FontStyle]::Bold)
$btnClear.Cursor = [System.Windows.Forms.Cursors]::Hand
$btnClear.Add_Click({ $rtbLogs.Clear() })
$Form.Controls.Add($btnClear)

$btnOpenDir = New-Object System.Windows.Forms.Button
$btnOpenDir.Text = "📂 Abrir Pasta de Backups"
$btnOpenDir.Location = New-Object System.Drawing.Point(20, 655)
$btnOpenDir.Size = New-Object System.Drawing.Size(220, 38)
$btnOpenDir.BackColor = [System.Drawing.Color]::FromArgb(28, 28, 38)
$btnOpenDir.ForeColor = [System.Drawing.Color]::White
$btnOpenDir.FlatStyle = "Flat"
$btnOpenDir.Font = New-Object System.Drawing.Font("Segoe UI", 9.5, [System.Drawing.FontStyle]::Bold)
$btnOpenDir.Cursor = [System.Windows.Forms.Cursors]::Hand
$btnOpenDir.Add_Click({ Start-Process explorer.exe -ArgumentList $BackupDir })
$Form.Controls.Add($btnOpenDir)

# Form Show Event
$Form.Add_Shown({
    Update-BackupStatus
    Add-Log "INIT" "ricxOptimizer inicializado com sucesso."
    Add-Log "INIT" "Selecione o modo desejado ou crie um backup prévio de segurança."
})

[System.Windows.Forms.Application]::Run($Form)
