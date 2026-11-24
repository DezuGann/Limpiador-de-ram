@echo off
setlocal
cd /d "%~dp0"

echo =====================================================================
echo    COMPILADOR DE LIMPIADOR DE RAM PRO - NATIVO PARA WINDOWS
echo =====================================================================
echo.

set CSC="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist %CSC% (
    set CSC="C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

if not exist %CSC% (
    echo [ERROR] No se encontro el compilador de C# de .NET Framework.
    echo Asegurate de tener .NET Framework 4.5 o superior instalado.
    if "%1" neq "/nopause" pause
    exit /b 1
)

echo [1/3] Verificando icono de la aplicacion...
if not exist "resources\app.ico" (
    powershell -ExecutionPolicy Bypass -File "resources\generate_icon.ps1"
)
copy /y "resources\app.ico" "app.ico" >nul 2>&1

echo [2/3] Compilando codigo fuente C# con manifiesto de administrador...
if exist "LimpiadorRAM.exe" (
    del /f /q "LimpiadorRAM.exe" >nul 2>&1
)
if exist "LimpiadorRAM.old.exe" (
    del /f /q "LimpiadorRAM.old.exe" >nul 2>&1
)
%CSC% /nologo /target:winexe /optimize+ /platform:anycpu ^
    /win32manifest:src\app.manifest ^
    /win32icon:resources\app.ico ^
    /out:LimpiadorRAM.exe ^
    /r:System.dll,System.Core.dll,System.Drawing.dll,System.Windows.Forms.dll,System.Xml.dll ^
    src\AssemblyInfo.cs src\Program.cs src\MemoryManager.cs src\Settings.cs src\Controls.cs src\MainForm.cs src\TrayAppContext.cs

if %ERRORLEVEL% equ 0 (
    echo.
    echo =====================================================================
    echo    [EXITO] Compilacion completada: LimpiadorRAM.exe generado!
    echo =====================================================================
    echo.
    echo Tamano del ejecutable:
    dir LimpiadorRAM.exe | findstr LimpiadorRAM.exe
    echo.
) else (
    echo.
    echo [ERROR] Ocurrio un fallo durante la compilacion. Revisa los mensajes anteriores.
    echo.
)

if "%1" neq "/nopause" pause
