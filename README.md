# Syslog Alert

**Windows Service** para recibir logs críticos de MikroTik RouterOS y reenviarlos en tiempo real a Telegram y WhatsApp.

```
MikroTik → UDP Syslog → Syslog Alert (Windows Service) → Telegram / WhatsApp
```

## Features

- ✅ **Recibe UDP syslog** de múltiples MikroTik simultáneamente
- ✅ **Filtra por severidad** (warning, error, critical, alert, emergency)
- ✅ **Filtra por topic** (system, firewall, dhcp, ipsec, etc.)
- ✅ **Filtra por palabras clave** — ignorar SYN flooding, port scans, etc.
- ✅ **Rate limiter anti-flood** — suprime alertas repetidas
- ✅ **Modo urgente** — detecta ataques sostenidos y alerta cada 15 min
- ✅ **Soporta Telegram y WhatsApp** simultáneamente
- ✅ **Debug detallado** en consola — útil para troubleshooting
- ✅ **Instalador automático** — configura firewall y registra el servicio

## Requisitos

- Windows 10/11 o Windows Server 2016+
- Puerto UDP 514 disponible
- MikroTik RouterOS 6.x o 7.x
- Tokens de Telegram y/o ApiKey de WhatsApp (opcional)

## Quick Start

1. **Descomprimí** el paquete
2. **Editá** `SyslogAlerter.cfg` con tus credenciales
3. **Ejecutá** `install.cmd` como Administrador
4. **Configurá** MikroTik para enviar syslog

```routeros
/system identity set name=tu-router-name

/system/logging/action
add name=syslogAlert target=remote remote=TU_IP_O_DOMINIO remote-port=514 remote-log-format=default

/system/logging set [find action=syslogAlert] prefix=[/system identity get name]

/system/logging
add topics=system,warning  action=syslogAlert
add topics=system,error    action=syslogAlert
add topics=firewall        action=syslogAlert
```

## Documentación

Lee **README.html** para instrucciones completas de instalación, configuración y troubleshooting.

## Estructura

```
Mikrotik-SysLog-Alert/
├── Program.cs               C# .NET source code (v2.2)
├── SyslogAlerter.cfg        Configuration file (example values)
├── install.cmd              Windows installer script
├── uninstall.cmd            Windows uninstaller script
├── README.html              Full documentation with styling
├── README.txt               Plain text documentation
├── README.md                This file
└── LICENSE                  MIT License
```

## Instalación

### Opción 1: Instalador automático (recomendado)

```cmd
install.cmd
```

El instalador:
- Verifica permisos de admin
- Copia los archivos a `C:\Program Files\SyslogAlert`
- Configura regla de firewall UDP 514
- Registra el servicio de Windows
- Abre el README y .cfg para editar

### Opción 2: Manual (dev/testing)

```cmd
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Luego renombrá el exe a `SyslogAlert.exe` y copialo junto con `SyslogAlerter.cfg`.

## Configuración

Editá **SyslogAlerter.cfg**:

```ini
Puerto=514

# true = activado | false = desactivado
TelegramEnabled=true
TelegramToken=BOT_TOKEN
TelegramChatId=CHAT_ID

WhatsAppEnabled=false
WhatsAppPhone=+549XXXXXXXXXX
WhatsAppApiKey=API_KEY

# Palabras clave a ignorar (separadas por coma)
MensajesIgnorar=possible SYN flooding,possible port scan,possible IP spoofing
```

Reiniciá el servicio para aplicar cambios:

```powershell
Restart-Service SyslogAlert
```

## Filtros de Palabras Clave

Ignorá mensajes que contengan ciertas palabras sin enviar alertas:

```ini
MensajesIgnorar=possible SYN flooding,port scan attempt,brute force
```

Caso insensible. Si el mensaje contiene cualquiera de estas palabras, se filtra automáticamente.

**Ejemplos útiles:**
```
possible SYN flooding
possible port scan
possible IP spoofing
brute force attempt
invalid SSL certificate
```

## Rate Limiter (Anti-Flood)

Cuando el mismo tipo de evento se repite:

- **Alerta 1**: Enviada normalmente
- **Alertas 2–9**: Suprimidas
- **Alerta 10**: Se activa "modo urgente"
  - Siguiente alerta urgente en 15 minutos
  - Las demás alertas entre medio quedan suprimidas
- **Sin actividad 5 min**: Contador resetea

Ejemplo: Si un bot intenta 50 logins fallidos, recibís 1 alerta normal + 1 urgente (a los 10 intentos) + 1 cada 15 min si continúa.

## MikroTik Syslog Format

El servicio soporta:

- **default** ← Recomendado. Formato: `topics HOSTNAME: mensaje`
- **bsd-syslog** ← Alternativo. Formato: `<PRIORIDAD>... HOSTNAME ...`

Configurá en MikroTik:

```
/system/logging/action
add ... remote-log-format=default
```

## Parse Logic v2.2

Si MikroTik no envia el `prefix` (identity del equipo):

- **v2.1 y anteriores**: Enviaba alert de PARSE-ERROR sin filtrar
- **v2.2**: Usa `hostname=unknown` y aplica filtros normales (silencioso)

**Ejemplo:**
```
Raw: system,error: login failure     ← sin hostname
Parse: hostname=unknown, severidad=error, mensaje=login failure
Aplica: filtros, rate limiter, etc.
```

## Troubleshooting

### No recibe syslog

1. Verificá que el puerto 514 UDP está abierto:
   ```powershell
   netstat -ano | findstr 514
   ```

2. Confirmá que el firewall no bloquea UDP 514:
   ```powershell
   netsh advfirewall firewall show rule name=SyslogAlert
   ```

3. Probá enviar un mensaje de prueba desde PowerShell:
   ```powershell
   $udp = New-Object System.Net.Sockets.UdpClient
   $bytes = [System.Text.Encoding]::UTF8.GetBytes("system,error tuRouter: test message")
   $udp.Send($bytes, $bytes.Length, "127.0.0.1", 514)
   $udp.Close()
   ```

### Servicios no se inicia

Verificá los logs de Windows:
```powershell
Get-EventLog -LogName Application -Source "Syslog Alert" -Newest 10
```

O ejecutá el exe directamente para ver el debug en consola.

### Alertas no llegan a Telegram/WhatsApp

1. Verificá que `TelegramEnabled=true` (o `WhatsAppEnabled=true`)
2. Confirmá tokens/ApiKey en el .cfg
3. Reiniciá el servicio: `Restart-Service SyslogAlert`
4. Revisa la consola del debug (errores HTTP)

### Los filtros no funcionan

1. Verificá que estás usando **v2.2** o superior
2. Edita el .cfg y reinicia: `Restart-Service SyslogAlert`
3. Revisa en consola: `[DEBUG] COINCIDENCIA — 'palabra' encontrado en mensaje`
4. Confirmá que la palabra está exactamente como aparece en el mensaje (case-insensitive)

## Changelog

### v2.2
- **Fix crítico**: Parse logic - usa defaults en lugar de enviar errors
- Rate limiter funciona incluso sin hostname
- Filtros se aplican siempre (incluso con parse parcial)
- Better debug para MensajesIgnorar

### v2.1
- Filtro de palabras clave (MensajesIgnorar)
- Debug detallado para filtros

### v2.0
- Rate limiter anti-flood con modo urgente
- Parser dual BSD-syslog y default
- Debug detallado en consola
- Instalador automático con firewall
- Documentación completa HTML

### v1.9
- Rate limiter básico

### v1.0–v1.8
- Implementación inicial y mejoras progresivas

## Licencia

MIT License — Uso libre para desarrollo, testing y producción.

## Autor

**Brian Tierno** @ [DMZ Sistemas](https://dmz.ar)  
📧 [WhatsApp](https://wa.me/541178285893)

---

**Estado**: ✅ Estable | **Última actualización**: Junio 2026
