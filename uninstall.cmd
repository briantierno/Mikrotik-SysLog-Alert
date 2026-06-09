@echo off
setlocal EnableDelayedExpansion
title Syslog Alert - Desinstalador
:: ============================================================
::  Syslog Alert - Desinstalador v2.0
::  Desarrollado por DMZ Sistemas — Brian Tierno
::  https://dmz.ar | https://wa.me/541178285893
:: ============================================================

:: --- Verificar que corre como administrador ---
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [!] Se requieren permisos de Administrador.
    pause
    exit /b 1
)

:: --- Variables ---
set INSTALL_DIR=%ProgramFiles%\SyslogAlert
set SERVICE_NAME=SyslogAlert
set TEMP_DIR=%TEMP%\SyslogAlertUninstall

echo.
echo  Desinstalando Syslog Alert...
echo  -----------------------------------------------

echo [1/5] Deteniendo el servicio...
"%INSTALL_DIR%\nssm.exe" stop %SERVICE_NAME% >nul 2>&1
timeout /t 3 /nobreak >nul

echo [2/5] Eliminando el servicio...
"%INSTALL_DIR%\nssm.exe" remove %SERVICE_NAME% confirm >nul 2>&1

echo [3/5] Eliminando regla de firewall...
netsh advfirewall firewall delete rule name="%SERVICE_NAME%" >nul 2>&1

echo [4/5] Moviendo archivos a carpeta temporal...
if exist "%TEMP_DIR%" rd /s /q "%TEMP_DIR%" >nul 2>&1
move "%INSTALL_DIR%" "%TEMP_DIR%" >nul 2>&1

echo [5/5] Eliminando archivos...
if !errorLevel!==0 (
    rd /s /q "%TEMP_DIR%" >nul 2>&1
    if exist "%TEMP_DIR%" (
        echo  [!] No se pudo eliminar %TEMP_DIR%
        echo      Podes borrarla manualmente, no afecta el funcionamiento.
    ) else (
        echo  Archivos eliminados correctamente.
    )
) else (
    echo  Intentando borrado directo...
    rd /s /q "%INSTALL_DIR%" >nul 2>&1
    if exist "%INSTALL_DIR%" (
        echo  [!] No se pudo eliminar automaticamente.
        echo      Borra manualmente: %INSTALL_DIR%
    ) else (
        echo  Archivos eliminados correctamente.
    )
)

echo.
echo  ============================================
echo   Syslog Alert desinstalado correctamente.
echo  ============================================
echo.
pause
exit /b 0
