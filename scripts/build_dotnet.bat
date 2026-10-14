@echo off
setlocal
title Compilando ricxOptimizer (.NET SDK)...

echo =====================================================================
echo          ricxOptimizer - Build com .NET SDK 8.0+
echo =====================================================================
echo.

where dotnet >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo [ERRO] .NET SDK (dotnet) nao encontrado no PATH do sistema.
    echo Utilize scripts\build_native_csc.bat para compilar sem o SDK.
    pause
    exit /b 1
)

set "ROOT=%~dp0.."
set "PROJ=%ROOT%\src\ricxOptimizer.csproj"

echo [INFO] Publicando executavel unico auto-contido x64...
dotnet publish "%PROJ%" -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o "%ROOT%\publish"

if %ERRORLEVEL% EQU 0 (
    copy /y "%ROOT%\publish\ricxOptimizer.exe" "%ROOT%\ricxOptimizer.exe"
    echo.
    echo [SUCESSO] Binario publicado em %ROOT%\ricxOptimizer.exe
) else (
    echo.
    echo [ERRO] Falha no comando dotnet publish.
)

pause
