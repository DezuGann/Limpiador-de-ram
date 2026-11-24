@echo off
setlocal
cd /d "%~dp0"

echo =====================================================================
echo    COMPILADOR DEL INSTALADOR DE LIMPIADOR DE RAM
echo =====================================================================
echo.

set CSC="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist %CSC% (
    set CSC="C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

if not exist %CSC% (
    echo [ERROR] No se encontro csc.exe
    if "%1" neq "/nopause" pause
    exit /b 1
)

echo [1/2] Verificando ejecutable principal...
if not exist "LimpiadorRAM.exe" (
    call compilar.bat /nopause
)

echo [2/2] Compilando Setup_LimpiadorRAM.exe con recursos integrados...
if exist "Setup_LimpiadorRAM.exe" (
    del /f /q "Setup_LimpiadorRAM.exe" >nul 2>&1
)
if exist "Setup_LimpiadorRAM.old.exe" (
    del /f /q "Setup_LimpiadorRAM.old.exe" >nul 2>&1
)

%CSC% /nologo /target:winexe /optimize+ /platform:anycpu ^
    /win32manifest:src\Installer\setup.manifest ^
    /win32icon:resources\app.ico ^
    /out:Setup_LimpiadorRAM.exe ^
    /resource:LimpiadorRAM.exe,Payload.LimpiadorRAM.exe ^
    /resource:resources\app.ico,Payload.app.ico ^
    /resource:resources\panda_clean.png,Payload.panda_clean.png ^
    /resource:resources\watermark_bg.jpg,Payload.watermark_bg.jpg ^
    /r:System.dll,System.Core.dll,System.Drawing.dll,System.Windows.Forms.dll ^
    src\Installer\SetupProgram.cs

if %ERRORLEVEL% equ 0 (
    echo.
    echo =====================================================================
    echo    [EXITO] Instalador generado: Setup_LimpiadorRAM.exe
    echo =====================================================================
    echo.
    dir Setup_LimpiadorRAM.exe | findstr Setup_LimpiadorRAM.exe
    echo.
) else (
    echo.
    echo [ERROR] Fallo al compilar el instalador.
    echo.
)

if "%1" neq "/nopause" pause
