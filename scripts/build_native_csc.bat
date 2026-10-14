@echo off
setlocal enabledelayedexpansion
title Compilando ricxOptimizer com Microsoft CSC Nativo...

echo =====================================================================
echo          ricxOptimizer - Compilador Nativo C# (Windows 10/11)
echo =====================================================================
echo.

:: Detectar CSC do .NET Framework 64-bit
set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    set "CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

if not exist "%CSC%" (
    echo [ERRO] Compilador csc.exe nao encontrado em C:\Windows\Microsoft.NET!
    echo Verifique se o .NET Framework esta ativo.
    pause
    exit /b 1
)

echo [OK] Compilador encontrado: %CSC%
echo [INFO] Compilando arquivos C# em ricxOptimizer.exe...

set "ROOT=%~dp0.."
set "OUT=%ROOT%\ricxOptimizer.exe"
set "SRC=%ROOT%\src"
set "MANIFEST=%SRC%\app.manifest"

"%CSC%" /target:winexe /optimize+ /platform:x64 /out:"%OUT%" /win32manifest:"%MANIFEST%" /r:System.Windows.Forms.dll,System.Drawing.dll,System.ServiceProcess.dll,System.Management.dll,System.dll,System.Core.dll /recurse:"%SRC%\*.cs"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo =====================================================================
    echo [SUCESSO] ricxOptimizer.exe compilado com exito!
    echo Local: %OUT%
    echo =====================================================================
) else (
    echo.
    echo [FALHA] Houve um erro durante a compilacao.
)

pause
