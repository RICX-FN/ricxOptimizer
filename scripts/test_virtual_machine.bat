@echo off
setlocal
title ricxOptimizer - Diagnostico de Ambiente de Maquina Virtual

echo =====================================================================
echo          ricxOptimizer - Verificacao de Ambiente para Testes
echo =====================================================================
echo.

:: Verificar Administrador
net session >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [AVISO] Execute este diagnostico como Administrador!
    echo.
)

echo [1] Detalhes do Sistema Operacional:
systeminfo | findstr /B /C:"OS Name" /C:"OS Version" /C:"System Manufacturer" /C:"System Model"
echo.

echo [2] Parametros Atuais do BCD (Timers / Hypervisor):
bcdedit /enum {current} | findstr /I "useplatformtick disabledynamictick useplatformclock hypervisorlaunchtype"
echo.

echo [3] Estado do Plano de Energia Ativo:
powercfg /getactivescheme
echo.

echo [4] Status do VBS (Virtualization-Based Security):
reg query "HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard" /v "EnableVirtualizationBasedSecurity" 2>nul
echo.

echo [5] Status do Windows Defender (Monitoramento em Tempo Real):
reg query "HKLM\SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection" /v "DisableRealtimeMonitoring" 2>nul
echo.

echo =====================================================================
echo Diagnostico concluido. Para iniciar a interface execute: run_ricxOptimizer.bat
echo =====================================================================
pause
