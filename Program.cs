using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;

// ============================================================
//  SYSLOG ALERT - v2.1
//  Descripcion: Recibe syslog de MikroTik y reenvía a Telegram/WhatsApp
//  Soporta formato BSD-syslog y default
//  Incluye rate limiter anti-flood y filtros de palabras clave
// ============================================================
// ============================================================
//  Desarrollado por DMZ Sistemas — Brian Tierno
//  https://dmz.ar | https://wa.me/541178285893
// ============================================================

string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SyslogAlerter.cfg");

if (!File.Exists(configPath))
{
    Log("ERROR", $"No se encontro el archivo de configuracion: {configPath}");
    Console.WriteLine("Presiona cualquier tecla para salir...");
    Console.ReadKey();
    return;
}

var config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
foreach (string linea in File.ReadAllLines(configPath))
{
    string l = linea.Trim();
    if (string.IsNullOrEmpty(l) || l.StartsWith("#")) continue;
    int eq = l.IndexOf('=');
    if (eq > 0)
        config[l[..eq].Trim()] = l[(eq + 1)..].Trim();
}

int puerto = int.TryParse(config.GetValueOrDefault("Puerto", "514"), out int p) ? p : 514;
string[] severidadesAlertar = config.GetValueOrDefault("Severidades", "warning,error,critical,alert,emergency").Split(',');
string[] topicsIgnorar = config.GetValueOrDefault("TopicsIgnorar", "").Split(',');
string[] mensajesIgnorar = config.GetValueOrDefault("MensajesIgnorar", "").Split(',');

bool telegramEnabled = config.GetValueOrDefault("TelegramEnabled", "false").ToLower() == "true";
string telegramToken = config.GetValueOrDefault("TelegramToken", "");
string telegramChatId = config.GetValueOrDefault("TelegramChatId", "");

bool whatsappEnabled = config.GetValueOrDefault("WhatsAppEnabled", "false").ToLower() == "true";
string alertPhone = config.GetValueOrDefault("WhatsAppPhone", "");
string alertApiKey = config.GetValueOrDefault("WhatsAppApiKey", "");

var rateLimiter = new ConcurrentDictionary<string, EventState>();

const int UMBRAL_URGENTE  = 10;
const int MINUTOS_RESET   = 5;
const int MINUTOS_URGENTE = 15;

using var http = new HttpClient();
using var udp = new UdpClient(puerto);
var endpoint = new IPEndPoint(IPAddress.Any, puerto);

Log("INFO", $"Escuchando syslog en UDP:{puerto}");
Log("INFO", $"Telegram: {(telegramEnabled ? "activo" : "inactivo")} | WhatsApp: {(whatsappEnabled ? "activo" : "inactivo")}");
Log("INFO", $"Severidades: {string.Join(",", severidadesAlertar)}");
Log("INFO", $"Topics ignorados: {string.Join(",", topicsIgnorar)}");
Log("INFO", $"Mensajes ignorados: {string.Join(" | ", mensajesIgnorar.Where(m => !string.IsNullOrEmpty(m.Trim())))}");
Console.WriteLine(new string('-', 60));

while (true)
{
    byte[] data = udp.Receive(ref endpoint);
    string rawCapturado = Encoding.UTF8.GetString(data).Trim();

    _ = Task.Run(async () =>
    {
        string raw = rawCapturado;
        Log("DEBUG", $"RAW: {raw}");

        string severidad = "";
        string hostname = "";
        string mensaje = raw;
        bool parseWarning = false;

        try
        {
            if (raw.StartsWith("<"))
            {
                // --- Formato BSD-syslog ---
                int inicio = raw.IndexOf('<');
                int fin = raw.IndexOf('>');
                if (inicio >= 0 && fin > inicio)
                {
                    int prioridad = int.Parse(raw[(inicio + 1)..fin]);
                    int sevNum = prioridad % 8;
                    severidad = sevNum switch
                    {
                        0 => "emergency",
                        1 => "alert",
                        2 => "critical",
                        3 => "error",
                        4 => "warning",
                        5 => "notice",
                        6 => "info",
                        7 => "debug",
                        _ => "unknown"
                    };
                    string resto = raw[(fin + 1)..].Trim();
                    string[] partes = resto.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (partes.Length >= 4)
                    {
                        hostname = partes[3];
                        mensaje = string.Join(" ", partes[4..]);
                    }
                    else
                    {
                        hostname = "unknown";
                        parseWarning = true;
                    }
                }
                else
                {
                    severidad = "unknown";
                    hostname = "unknown";
                    parseWarning = true;
                }
            }
            else
            {
                // --- Formato default ---
                int espacio = raw.IndexOf(' ');
                if (espacio > 0)
                {
                    string topics = raw[..espacio].ToLower();
                    string resto = raw[(espacio + 1)..].Trim();

                    foreach (string s in new[] { "emergency", "alert", "critical", "error", "warning", "notice", "info", "debug" })
                        if (topics.Contains(s)) { severidad = s; break; }

                    int dospuntos = resto.IndexOf(':');
                    if (dospuntos > 0)
                    {
                        hostname = resto[..dospuntos].Trim();
                        mensaje = resto[(dospuntos + 1)..].Trim();
                    }
                    else
                    {
                        hostname = "unknown";
                        parseWarning = true;
                    }
                }
                else
                {
                    severidad = "unknown";
                    hostname = "unknown";
                    parseWarning = true;
                }
            }
        }
        catch (Exception ex)
        {
            Log("ERROR", $"Excepcion en parser: {ex.Message}");
            severidad = "unknown";
            hostname = "unknown";
            parseWarning = true;
        }

        // Valores por defecto si quedaron vacíos
        if (string.IsNullOrEmpty(severidad)) severidad = "unknown";
        if (string.IsNullOrEmpty(hostname)) hostname = "unknown";

        if (parseWarning)
            Log("DEBUG", $"PARSE WARNING — hostname={hostname} severidad={severidad} (valores por defecto)");
        else
            Log("DEBUG", $"PARSE OK — hostname={hostname} severidad={severidad} mensaje={mensaje.Trim()}");

        // --- Filtro topics ignorados ---
        bool ignorar = false;
        foreach (string t in topicsIgnorar)
            if (!string.IsNullOrEmpty(t) && mensaje.ToLower().Contains(t.Trim())) { ignorar = true; break; }

        if (ignorar)
        {
            Log("DEBUG", $"FILTRADO — topic ignorado: {mensaje.Trim()}");
            return;
        }

        // --- Filtro palabras clave en el mensaje ---
        bool palabraEncontrada = false;
        foreach (string palabra in mensajesIgnorar)
        {
            if (!string.IsNullOrEmpty(palabra))
            {
                string palabraLimpia = palabra.Trim().ToLower();
                if (mensaje.ToLower().Contains(palabraLimpia))
                {
                    Log("DEBUG", $"COINCIDENCIA — '{palabraLimpia}' encontrado en mensaje");
                    palabraEncontrada = true;
                    ignorar = true;
                    break;
                }
            }
        }

        if (ignorar)
        {
            Log("DEBUG", $"FILTRADO — mensaje contiene palabra clave ignorada: {mensaje.Trim()}");
            return;
        }

        // --- Filtro severidad ---
        bool alertar = false;
        foreach (string sev in severidadesAlertar)
            if (severidad == sev.Trim().ToLower()) { alertar = true; break; }

        if (!alertar)
        {
            Log("DEBUG", $"FILTRADO — severidad '{severidad}' no esta en la lista de alertas");
            return;
        }

        string clave = $"{hostname}|{severidad}|{mensaje[..Math.Min(40, mensaje.Length)]}";
        var ahora = DateTime.Now;
        var estado = rateLimiter.GetOrAdd(clave, _ => new EventState());

        lock (estado)
        {
            if (estado.UltimaVez != DateTime.MinValue && (ahora - estado.UltimaVez).TotalMinutes >= MINUTOS_RESET)
            {
                Log("DEBUG", $"RATE RESET — sin actividad por {MINUTOS_RESET} min, reiniciando contador para: {clave}");
                estado.Contador = 0;
                estado.ModoUrgente = false;
                estado.UltimaAlertaUrgente = DateTime.MinValue;
            }

            estado.Contador++;
            estado.UltimaVez = ahora;

            Log("DEBUG", $"RATE contador={estado.Contador}/{UMBRAL_URGENTE} modoUrgente={estado.ModoUrgente} clave={clave}");

            if (estado.ModoUrgente)
            {
                double minutosRestantes = MINUTOS_URGENTE - (ahora - estado.UltimaAlertaUrgente).TotalMinutes;
                if ((ahora - estado.UltimaAlertaUrgente).TotalMinutes >= MINUTOS_URGENTE)
                {
                    estado.UltimaAlertaUrgente = ahora;
                    Log("INFO", $"ATAQUE EN CURSO — reenviando alerta urgente: {hostname}");
                    string textoUrgente = Uri.EscapeDataString(
                        $"🚨 [{hostname}] INTERVENCIÓN URGENTE\n" +
                        $"Ataque en curso detectado — actividad maliciosa sostenida.\n" +
                        $"Evento: {mensaje.Trim()}\n" +
                        $"El ataque continúa. Se notificará cada {MINUTOS_URGENTE} minutos.");
                    _ = EnviarAlertas(http, telegramEnabled, telegramToken, telegramChatId,
                        whatsappEnabled, alertPhone, alertApiKey, textoUrgente);
                }
                else
                {
                    Log("INFO", $"SUPRIMIDO — ataque en curso, proxima alerta en {minutosRestantes:F1} min: [{hostname}] {mensaje.Trim()}");
                }
                return;
            }

            if (estado.Contador == 1)
            {
                Log("INFO", $"ALERTA ENVIADA — [{hostname}] [{severidad.ToUpper()}] {mensaje.Trim()}");
                string texto = Uri.EscapeDataString($"[{hostname}] [{severidad.ToUpper()}] {mensaje.Trim()}");
                _ = EnviarAlertas(http, telegramEnabled, telegramToken, telegramChatId,
                    whatsappEnabled, alertPhone, alertApiKey, texto);
            }
            else if (estado.Contador == UMBRAL_URGENTE)
            {
                estado.ModoUrgente = true;
                estado.UltimaAlertaUrgente = ahora;
                Log("INFO", $"UMBRAL ALCANZADO — activando modo urgente para: {hostname}");
                string textoUrgente = Uri.EscapeDataString(
                    $"🚨 [{hostname}] INTERVENCIÓN URGENTE\n" +
                    $"Ataque en curso detectado — {UMBRAL_URGENTE} eventos en menos de {MINUTOS_RESET} minutos.\n" +
                    $"Evento: {mensaje.Trim()}\n" +
                    $"Se suprimirán alertas repetidas por {MINUTOS_URGENTE} minutos.");
                _ = EnviarAlertas(http, telegramEnabled, telegramToken, telegramChatId,
                    whatsappEnabled, alertPhone, alertApiKey, textoUrgente);
            }
            else
            {
                Log("INFO", $"SUPRIMIDO {estado.Contador}/{UMBRAL_URGENTE} — [{hostname}] [{severidad.ToUpper()}] {mensaje.Trim()}");
            }
        }
    });
}

static void Log(string nivel, string mensaje)
{
    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    Console.WriteLine($"[{timestamp}] [{nivel,-5}] {mensaje}");
}

static async Task EnviarAlertas(
    HttpClient http,
    bool telegramEnabled, string telegramToken, string telegramChatId,
    bool whatsappEnabled, string alertPhone, string alertApiKey,
    string textoEncoded)
{
    if (telegramEnabled)
    {
        try
        {
            await http.GetAsync($"https://api.telegram.org/bot{telegramToken}/sendMessage?chat_id={telegramChatId}&text={textoEncoded}");
            Log("INFO", "Telegram enviado");
        }
        catch (Exception ex) { Log("ERROR", $"Telegram: {ex.Message}"); }
    }

    if (whatsappEnabled)
    {
        try
        {
            await http.GetAsync($"http://api.textmebot.com/send.php?recipient={alertPhone}&apikey={alertApiKey}&text={textoEncoded}");
            Log("INFO", "WhatsApp enviado");
        }
        catch (Exception ex) { Log("ERROR", $"WhatsApp: {ex.Message}"); }
    }
}

class EventState
{
    public int Contador { get; set; } = 0;
    public bool ModoUrgente { get; set; } = false;
    public DateTime UltimaVez { get; set; } = DateTime.MinValue;
    public DateTime UltimaAlertaUrgente { get; set; } = DateTime.MinValue;
}
