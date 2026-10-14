@echo off
setlocal enabledelayedexpansion
title Iniciando ricxOptimizer...

:: 1. Verificar e solicitar privilegios de Administrador
net session >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [INFO] Solicitando privilegios de Administrador...
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

cd /d "%~dp0"

:: 2. Se o executavel compilado ja existir, executar diretamente
if exist "ricxOptimizer.exe" (
    echo [INFO] Iniciando ricxOptimizer.exe...
    start "" "ricxOptimizer.exe"
    exit /b
)

:: 3. Se nao existir, tentar compilar via csc.exe nativo do Windows
echo [INFO] ricxOptimizer.exe nao encontrado. Compilando automaticamente com CSC Nativo...
call "scripts\build_native_csc.bat"

if exist "ricxOptimizer.exe" (
    start "" "ricxOptimizer.exe"
    exit /b
)

:: 4. Fallback: Executar diretamente via PowerShell caso csc nao tenha gerado o exe
echo [INFO] Iniciando versao direta do ricxOptimizer via PowerShell...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "ricxOptimizer.ps1"
