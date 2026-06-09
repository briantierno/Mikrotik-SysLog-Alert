@echo off
setlocal EnableDelayedExpansion
title Syslog Alert - Instalador
:: ============================================================
::  Syslog Alert - Instalador v2.0
::  Desarrollado por DMZ Sistemas — Brian Tierno
::  https://dmz.ar | https://wa.me/541178285893
:: ============================================================

:: --- Verificar que corre como administrador ---
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [!] Ejecuta este instalador como Administrador.
    pause
    exit /b 1
)

:: --- Variables ---
set INSTALL_DIR=%ProgramFiles%\SyslogAlert
set EXE_NAME=SyslogAlert.exe
set CFG_NAME=SyslogAlerter.cfg
set SERVICE_NAME=SyslogAlert

echo.
echo  Instalando Syslog Alert...
echo  Desarrollado por DMZ Sistemas - Brian Tierno
echo  -----------------------------------------------

:: --- Verificar NSSM ---
if not exist "%~dp0nssm.exe" (
    color 4F
    echo.
    echo  [ERROR] No se encontro nssm.exe en la carpeta del instalador.
    echo  Asegurate de que nssm.exe este en la misma carpeta que install.cmd.
    echo  -----------------------------------------------
    pause
    exit /b 1
)

echo [1/5] Creando carpeta de instalacion...
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"

echo [2/5] Copiando archivos...
copy /Y "%~dp0%EXE_NAME%"    "%INSTALL_DIR%\%EXE_NAME%"    >nul
copy /Y "%~dp0%CFG_NAME%"    "%INSTALL_DIR%\%CFG_NAME%"    >nul
copy /Y "%~dp0nssm.exe"      "%INSTALL_DIR%\nssm.exe"      >nul
copy /Y "%~dp0README.html"   "%INSTALL_DIR%\README.html"   >nul
copy /Y "%~dp0README.txt"    "%INSTALL_DIR%\README.txt"    >nul
echo  [OK] Archivos copiados a %INSTALL_DIR%
set NSSM="%INSTALL_DIR%\nssm.exe"

echo [3/5] Creando regla de firewall...
netsh advfirewall firewall delete rule name="%SERVICE_NAME%" >nul 2>&1
netsh advfirewall firewall add rule name="%SERVICE_NAME%" dir=in action=allow protocol=UDP localport=514 >nul
echo  [OK] Regla de firewall UDP 514 creada

echo [4/5] Registrando servicio...
%NSSM% stop %SERVICE_NAME% >nul 2>&1
%NSSM% remove %SERVICE_NAME% confirm >nul 2>&1
%NSSM% install %SERVICE_NAME% "%INSTALL_DIR%\%EXE_NAME%"
%NSSM% set %SERVICE_NAME% AppDirectory "%INSTALL_DIR%"
%NSSM% set %SERVICE_NAME% DisplayName "Syslog Alert"
%NSSM% set %SERVICE_NAME% Description "Recibe syslog de MikroTik y reenvía alertas por Telegram y WhatsApp"
%NSSM% set %SERVICE_NAME% Start SERVICE_AUTO_START

echo [5/5] Iniciando servicio...
%NSSM% start %SERVICE_NAME%

echo.
echo  ============================================
echo   Syslog Alert instalado correctamente.
echo   Servicio corriendo en segundo plano.
echo  ============================================
echo.

:: --- Abrir README ---
set /p OPEN_README= Deseas abrir la documentacion (README.html)? [S/N]: 
if /i "!OPEN_README!"=="S" (
    start "" "%INSTALL_DIR%\README.html"
)

:: --- Abrir CFG ---
set /p OPEN_CFG= Deseas abrir la configuracion (SyslogAlerter.cfg)? [S/N]: 
if /i "!OPEN_CFG!"=="S" (
    notepad "%INSTALL_DIR%\%CFG_NAME%"
)

echo.
echo  Recordá editar el .cfg y reiniciar el servicio para aplicar los cambios.
echo  -----------------------------------------------
pause
exit /b 0
