============================================================
  Syslog Alert v2.0
  Desarrollado por DMZ Sistemas - Brian Tierno
  https://dmz.ar | https://wa.me/541178285893
============================================================

COMO FUNCIONA
-------------
MikroTik envia eventos via UDP (syslog) a este servidor.
Syslog Alert filtra por severidad y topic, y reenvía las
alertas por Telegram y/o WhatsApp.

Un solo servidor puede recibir alertas de multiples routers.
Cada mensaje incluye el identity del equipo que lo genero.

El sistema incluye un rate limiter anti-flood que suprime
alertas repetidas y notifica al administrador cuando detecta
un ataque sostenido.


REQUISITOS
----------
- Windows 10/11 o Windows Server 2016+
- NSSM incluido en el paquete
- Puerto UDP 514 (el instalador lo configura)
- MikroTik RouterOS 6.x o 7.x


INSTALACION
-----------
1. Descomprimí el paquete en cualquier carpeta
2. Ejecutá install.cmd como Administrador
3. El instalador copia archivos, crea la regla de firewall
   y registra el servicio de Windows
4. Al finalizar se ofrece abrir la documentacion y el .cfg
5. Edita SyslogAlerter.cfg con tus datos
6. Reinicia el servicio para aplicar los cambios


CONFIGURACION (SyslogAlerter.cfg)
----------------------------------
Puerto=514
Severidades=warning,error,critical,alert,emergency
TopicsIgnorar=bfd,ospf,rip,meter

# true = activado | false = desactivado
TelegramEnabled=true
TelegramToken=TOKEN_DEL_BOT
TelegramChatId=ID_DEL_CHAT

# true = activado | false = desactivado
WhatsAppEnabled=false
WhatsAppPhone=+549XXXXXXXXXX
WhatsAppApiKey=API_KEY


CONFIGURACION EN MIKROTIK
--------------------------
Ejecuta los comandos en este orden:

1. Configurar el identity del equipo:
   /system identity set name=tuIdentity

2. Crear la logging action:
   /system/logging/action
   add name=syslogAlert target=remote remote=IP.O.DOMINIO remote-port=514 remote-log-format=default

3. Configurar el prefix con el identity:
   /system/logging set [find action=syslogAlert] prefix=[/system identity get name]

4. Crear las logging rules:
   /system/logging
   add topics=system,warning  action=syslogAlert
   add topics=system,error    action=syslogAlert
   add topics=firewall        action=syslogAlert

5. Verificar: intenta conectarte al router con usuario/clave
   incorrectos via Winbox. Deberias recibir una alerta:
   [tuIdentity] [CRITICAL] login failure for user...

NOTA: El formato recomendado es "default". El formato
"bsd-syslog" tambien funciona pero es menos preciso.
Usa bsd-syslog solo para debug con mensajes RAW.


ADMINISTRAR EL SERVICIO (PowerShell como Admin)
------------------------------------------------
Get-Service SyslogAlert        <- ver estado
Stop-Service SyslogAlert       <- detener
Start-Service SyslogAlert      <- iniciar
Restart-Service SyslogAlert    <- reiniciar (aplicar .cfg)

Reinicia el servicio despues de editar el .cfg.


DESINSTALACION
--------------
Ejecuta uninstall.cmd como Administrador.
Detiene el servicio, elimina la regla de firewall
y borra los archivos de C:\Program Files\SyslogAlert


ARCHIVOS DEL PAQUETE
---------------------
SyslogAlert.exe      Aplicacion principal
SyslogAlerter.cfg    Configuracion editable
nssm.exe             Gestor de servicios (incluido)
install.cmd          Instalador
uninstall.cmd        Desinstalador
README.html          Documentacion completa con formato
README.txt           Este archivo

============================================================
