@echo off
cd /d "%~dp0"

:: Si no se ha compilado aun, compilarlo primero
if not exist "LimpiadorRAM.exe" (
    echo LimpiadorRAM.exe no encontrado. Compilando automaticamente...
    call compilar.bat
)

if exist "LimpiadorRAM.exe" (
    start "" "LimpiadorRAM.exe"
) else (
    echo [ERROR] No se pudo encontrar ni compilar LimpiadorRAM.exe.
    pause
)
