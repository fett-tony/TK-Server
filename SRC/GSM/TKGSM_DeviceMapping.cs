/*
 * TKGSM_DeviceMapping
 * Aufgabe:
 * - Erkennt GSM-bezogene Hardware und zugehörige Ports.
 * - Ordnet AT-, Audio-, Diagnose- und NMEA-Ports zu.
 * - Liest Geräte-, SIM- und Netzbasisdaten per AT aus.
 * - Liefert nur bestätigte GSM-Geräte mit verwertbarer Zuordnung zurück.
 *
 * Nicht Aufgabe:
 * - Keine Session-Steuerung.
 * - Kein Routing.
 * - Keine Call-Steuerung.
 * - Keine Registration-Verwaltung.
 * - Keine Media-Steuerung.
 *
 * Diese Datei ist der Hardware- und Zuordnungsbereich für GSM-Geräte.
 */

namespace TKServer.GSM {
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.IO.Ports;
    using System.Linq;
    using System.Management;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;

    using TKServer.BASE;
    using TKServer.SQL;
    /// TKGSM_DeviceMapping
    /// Aufgabe:
    /// - Erkennt GSM-bezogene Hardware und zugehörige Ports.
    /// - Ordnet AT-, Audio-, Diagnose- und NMEA-Ports zu.
    /// - Liest Geräte-, SIM- und Netzbasisdaten per AT aus.
    /// - Liefert nur bestätigte GSM-Geräte mit verwertbarer Zuordnung zurück.
    /// 
    /// Nicht Aufgabe:
    /// - Keine Session-Steuerung.
    /// - Kein Routing.
    /// - Keine Call-Steuerung.
    /// - Keine Registration-Verwaltung.
    /// - Keine Media-Steuerung.
    /// 
    /// Diese Datei ist der Hardware- und Zuordnungsbereich für GSM-Geräte.

    /// <summary>
    /// Ermittelt GSM/USB-Modems unter Windows, ordnet deren COM-Ports zu
    /// und liest – sofern möglich – Basisinformationen per AT-Befehl aus.
    ///
    /// Ziel:
    /// - TK_GSM_Modems
    /// - TK_GSM_Sim
    /// - TK_GSM_ModemSimStatus
    ///
    /// Hinweis:
    /// Für System.Management muss im Projekt ggf. zusätzlich referenziert werden
    /// </summary>
    public sealed class TKGSM_DeviceMapping {
        /// <summary>
        /// Defines the DefaultBaudRate
        /// </summary>
        private const int DefaultBaudRate = 115200;

        /// <summary>
        /// Defines the DefaultReadTimeoutMs
        /// </summary>
        private const int DefaultReadTimeoutMs = 1500;

        /// <summary>
        /// Defines the DefaultWriteTimeoutMs
        /// </summary>
        private const int DefaultWriteTimeoutMs = 1500;

        /// <summary>
        /// Defines the DefaultOpenDelayMs
        /// </summary>
        private const int DefaultOpenDelayMs = 250;

        /// <summary>
        /// Defines the _logger
        /// </summary>
        private readonly TKBase_LogManager? _logger;

        /// <summary>
        /// Defines the _commands
        /// </summary>
        private readonly TKSQL_Commands _commands;

        /// <summary>
        /// Defines the _cancellationToken
        /// </summary>
        private readonly CancellationToken _cancellationToken;

        /// <summary>
        /// Initializes a new instance of the <see cref="TKGSM_DeviceMapping"/> class.
        /// </summary>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        /// <param name="cancellationToken">The cancellationToken<see cref="CancellationToken"/></param>
        public TKGSM_DeviceMapping(TKBase_LogManager? logger = null, CancellationToken cancellationToken = default) {
            _logger = logger;
            _commands = new TKSQL_Commands(logger);
            _cancellationToken = cancellationToken;
        }

        /// <summary>
        /// The DiscoverDevices
        /// </summary>
        /// <returns>The <see cref="List{GsmDeviceMap}"/></returns>
        public List<GsmDeviceMap> DiscoverDevices() {
            _logger?.Info("GSM_Device_Mapping.DiscoverDevices gestartet.");

            var pnpDevices = ReadPnPDevices();
            _logger?.Info($"PnP Geräte gelesen: {pnpDevices.Count}");

            var modemCandidates = BuildCandidates(pnpDevices);
            _logger?.Info($"GSM Kandidaten gefunden: {modemCandidates.Count}");

            foreach (var candidate in modemCandidates) {
                _cancellationToken.ThrowIfCancellationRequested();

                _logger?.Info($"Bearbeite Kandidat: {candidate.ModemName} | PNP={candidate.PNPDeviceID}");

                AssignPorts(candidate);
                ApplyPersistedPorts(candidate);

                _logger?.Info(
                    $"Ports zugeordnet | Modem={candidate.ModemName} | AT={candidate.ComPortAT} | Audio={candidate.ComPortAudio} | Diag={candidate.ComPortDiag} | NMEA={candidate.ComPortNMEA}");

                ProbeDevice(candidate);
            }

            var confirmedDevices = new List<GsmDeviceMap>();

            foreach (var device in modemCandidates) {
                if (IsConfirmedModem(device)) {
                    confirmedDevices.Add(device);
                    _logger?.Info($"Bestätigtes GSM-Modem | Modem={device.ModemName} | IMEI={device.IMEI} | ICCID={device.ICCID} | IMSI={device.IMSI}");
                } else {
                    _logger?.Warn($"Gerät ausgeschlossen | Modem={device.ModemName} | PNP={device.PNPDeviceID}");
                }
            }

            _logger?.Info($"Bestätigte GSM-Modems: {confirmedDevices.Count}");
            _logger?.Info("GSM_Device_Mapping.DiscoverDevices abgeschlossen.");

            return confirmedDevices;
        }

        /// <summary>
        /// The IsConfirmedModem
        /// </summary>
        /// <param name="device">The device<see cref="GsmDeviceMap"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private bool IsConfirmedModem(GsmDeviceMap device) {
            if (device == null)
                return false;

            string text = $"{device.ModemName} {device.Manufacturer} {device.Model} {device.PNPDeviceID}".ToLowerInvariant();

            // Harte Ausschlüsse zur Sicherheit auch nach dem Mapping
            if (text.Contains("intel(r) serial io gpio"))
                return false;

            if (text.Contains("intel(r) serial io i2c"))
                return false;

            if (text.Contains("acpi-schalter"))
                return false;

            if (text.Contains("acpi switch"))
                return false;

            if (text.Contains("windows.networking.networkoperators.mobilebroadbandaccount"))
                return false;

            if (text.Contains("mobilebroadbandaccount"))
                return false;

            bool hasPort =
                !string.IsNullOrWhiteSpace(device.ComPortAT) ||
                !string.IsNullOrWhiteSpace(device.ComPortAudio) ||
                !string.IsNullOrWhiteSpace(device.ComPortDiag) ||
                !string.IsNullOrWhiteSpace(device.ComPortNMEA);

            bool hasMobileIdentity =
                !string.IsNullOrWhiteSpace(device.IMEI) ||
                !string.IsNullOrWhiteSpace(device.ICCID) ||
                !string.IsNullOrWhiteSpace(device.IMSI);

            return hasPort && hasMobileIdentity;
        }

        /// <summary>
        /// The ReadPnPDevices
        /// </summary>
        /// <returns>The <see cref="List{PnpDeviceInfo}"/></returns>
        private List<PnpDeviceInfo> ReadPnPDevices() {
            var result = new List<PnpDeviceInfo>();

            const string query = @"SELECT Name,DeviceID,PNPDeviceID,Manufacturer,Description,Service,ClassGuid,Present,Status FROM Win32_PnPEntity";

            using var searcher = new ManagementObjectSearcher("root\\CIMV2", query);
            using var collection = searcher.Get();

            foreach (ManagementObject mo in collection) {
                string name = GetString(mo, "Name");
                string deviceId = GetString(mo, "DeviceID");
                string pnpDeviceId = GetString(mo, "PNPDeviceID");
                string manufacturer = GetString(mo, "Manufacturer");
                string description = GetString(mo, "Description");
                string service = GetString(mo, "Service");
                string classGuid = GetString(mo, "ClassGuid");
                string status = GetString(mo, "Status");
                bool present = GetBool(mo, "Present");

                result.Add(new PnpDeviceInfo {
                    Name = name,
                    DeviceID = deviceId,
                    PNPDeviceID = pnpDeviceId,
                    Manufacturer = manufacturer,
                    Description = description,
                    Service = service,
                    ClassGuid = classGuid,
                    Present = present,
                    Status = status,
                    ComPort = ExtractComPort(name),
                    MatchKey = BuildMatchKey(pnpDeviceId, name)
                });
            }

            return result;
        }

        /// <summary>
        /// The BuildCandidates
        /// </summary>
        /// <param name="devices">The devices<see cref="List{PnpDeviceInfo}"/></param>
        /// <returns>The <see cref="List{GsmDeviceMap}"/></returns>
        private List<GsmDeviceMap> BuildCandidates(List<PnpDeviceInfo> devices) {
            var filtered = devices
                .Where(IsRelevantGsmDevice)
                .ToList();

            var groups = filtered
                .GroupBy(d => d.MatchKey, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var result = new List<GsmDeviceMap>();

            foreach (var group in groups) {
                var primary = group
                    .OrderByDescending(IsStrongModemHint)
                    .ThenByDescending(x => x.Present)
                    .ThenBy(x => x.Name)
                    .First();

                var map = new GsmDeviceMap {
                    ModemName = Clean(primary.Name),
                    Manufacturer = string.Empty,
                    Model = string.Empty,
                    PNPDeviceID = primary.PNPDeviceID,
                    IsPresent = group.Any(x => x.Present),
                    Active = group.Any(x => x.Present),
                    RawPnPDevices = group.ToList()
                };

                result.Add(map);
            }

            return result;
        }

        /// <summary>
        /// The AssignPorts
        /// </summary>
        /// <param name="device">The device<see cref="GsmDeviceMap"/></param>
        private void AssignPorts(GsmDeviceMap device) {
            var ports = device.RawPnPDevices
                .Where(x => !string.IsNullOrWhiteSpace(x.ComPort))
                .ToList();

            foreach (var port in ports) {
                string text = $"{port.Name} {port.Description}".ToLowerInvariant();

                if (device.ComPortAT == null && ContainsAny(text, "at port", "hs-usb at", "pc ui", "wwan", "application")) {
                    device.ComPortAT = port.ComPort;
                    continue;
                }

                if (device.ComPortAudio == null && ContainsAny(text, "audio")) {
                    device.ComPortAudio = port.ComPort;
                    continue;
                }

                if (device.ComPortDiag == null && ContainsAny(text, "diag", "diagnostic", "diagnostics", "dm")) {
                    device.ComPortDiag = port.ComPort;
                    continue;
                }

                if (device.ComPortNMEA == null && ContainsAny(text, "nmea", "gps")) {
                    device.ComPortNMEA = port.ComPort;
                    continue;
                }

            }

            var remaining = ports
                .Select(x => x.ComPort)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var port in remaining) {
                if (device.ComPortAT == null) {
                    device.ComPortAT = port;
                    continue;
                }

                if (device.ComPortAudio == null && !SamePort(device.ComPortAT, port)) {
                    device.ComPortAudio = port;
                    continue;
                }

                if (device.ComPortDiag == null && !SamePort(device.ComPortAT, port) && !SamePort(device.ComPortAudio, port)) {
                    device.ComPortDiag = port;
                    continue;
                }

                if (device.ComPortNMEA == null &&
                    !SamePort(device.ComPortAT, port) &&
                    !SamePort(device.ComPortAudio, port) &&
                    !SamePort(device.ComPortDiag, port)) {
                    device.ComPortNMEA = port;
                }
            }
        }

        /// <summary>
        /// The ApplyPersistedPorts
        /// </summary>
        /// <param name="device">The device<see cref="GsmDeviceMap"/></param>
        private void ApplyPersistedPorts(GsmDeviceMap device) {
            if (device == null)
                return;

            try {
                string? pnpDeviceId = Normalize(device.PNPDeviceID);
                if (string.IsNullOrWhiteSpace(pnpDeviceId))
                    return;

                DataRow? row = _commands.GetLatestGsmPersistedPortsByPnpDeviceId(pnpDeviceId);
                if (row == null)
                    return;

                device.ComPortAT = ReadDbString(row, "ComPortAT") ?? device.ComPortAT;
                device.ComPortAudio = ReadDbString(row, "ComPortAudio") ?? device.ComPortAudio;
                device.ComPortDiag = ReadDbString(row, "ComPortDiag") ?? device.ComPortDiag;
                device.ComPortNMEA = ReadDbString(row, "ComPortNMEA") ?? device.ComPortNMEA;

                _logger?.Info($"Persistierte Portzuordnung übernommen | Modem={device.ModemName} | AT={device.ComPortAT} | Audio={device.ComPortAudio} | Diag={device.ComPortDiag} | NMEA={device.ComPortNMEA}");
            } catch (Exception ex) {
                _logger?.Warn($"Persistierte Portzuordnung konnte nicht geladen werden | Modem={device.ModemName} | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The ReadDbString
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="columnName">The columnName<see cref="string"/></param>
        /// <returns>The <see cref="string?"/></returns>
        private static string? ReadDbString(DataRow row, string columnName) {
            if (row.Table == null || !row.Table.Columns.Contains(columnName))
                return null;

            object? value = row[columnName];
            if (value == null || value == DBNull.Value)
                return null;

            string text = value.ToString()?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        /// <summary>
        /// The ProbeDevice
        /// </summary>
        /// <param name="device">The device<see cref="GsmDeviceMap"/></param>
        private void ProbeDevice(GsmDeviceMap device) {
            List<string> probePorts;

            if (!string.IsNullOrWhiteSpace(device.ComPortAT)) {
                probePorts = new List<string> { device.ComPortAT! };
            } else {
                probePorts = new[] {
                    device.ComPortAT,
                    device.ComPortAudio,
                    device.ComPortNMEA,
                    device.ComPortDiag
                }
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()!;
            }

            _logger?.Info($"ProbeDevice gestartet | Modem={device.ModemName} | Ports={string.Join(", ", probePorts)}");

            foreach (var port in probePorts) {
                _cancellationToken.ThrowIfCancellationRequested();

                _logger?.Info($"Prüfe Port {port} für Modem {device.ModemName}");

                var probe = ProbeAtPort(port!);
                device.Probes.Add(probe);

                if (!probe.Success) {
                    _logger?.Warn($"AT Probe fehlgeschlagen | Port={port} | Fehler={probe.ErrorMessage}");
                    continue;
                }

                _logger?.Info(
                    $"AT Probe OK | Port={port} | IMEI={probe.IMEI} | ICCID={probe.ICCID} | IMSI={probe.IMSI} | TEL={probe.Telefonnummer} | Provider={probe.ProviderName}");

                device.ComPortAT = port;

                if (!string.IsNullOrWhiteSpace(probe.Model))
                    device.Model = probe.Model;

                if (!string.IsNullOrWhiteSpace(probe.Manufacturer))
                    device.Manufacturer = probe.Manufacturer;

                if (!string.IsNullOrWhiteSpace(probe.IMEI))
                    device.IMEI = probe.IMEI;

                if (!string.IsNullOrWhiteSpace(probe.ICCID))
                    device.ICCID = probe.ICCID;

                if (!string.IsNullOrWhiteSpace(probe.IMSI))
                    device.IMSI = probe.IMSI;

                if (!string.IsNullOrWhiteSpace(probe.Telefonnummer))
                    device.Telefonnummer = probe.Telefonnummer;

                if (!string.IsNullOrWhiteSpace(probe.ProviderName))
                    device.ProviderName = probe.ProviderName;

                device.IsSimInserted = !string.IsNullOrWhiteSpace(probe.ICCID) ||
                                       !string.IsNullOrWhiteSpace(probe.IMSI) ||
                                       !string.IsNullOrWhiteSpace(probe.Telefonnummer);

                device.IsRegistered = probe.IsRegistered;
                device.SignalStrength = probe.SignalStrength;
                device.LastSeen = DateTime.Now;
                device.LastError = null;

                if (string.IsNullOrWhiteSpace(device.Model) && !string.IsNullOrWhiteSpace(probe.ATI))
                    device.Model = probe.ATI;

                break;
            }

            if (device.LastSeen == null && probePorts.Count > 0) {
                device.LastError = device.Probes.LastOrDefault(x => !string.IsNullOrWhiteSpace(x.ErrorMessage))?.ErrorMessage;
            }

            _logger?.Info(
                $"ProbeDevice abgeschlossen | Modem={device.ModemName} | IMEI={device.IMEI} | ICCID={device.ICCID} | IMSI={device.IMSI} | TEL={device.Telefonnummer} | Provider={device.ProviderName}");
        }

        /// <summary>
        /// The ProbeAtPort
        /// </summary>
        /// <param name="comPort">The comPort<see cref="string"/></param>
        /// <returns>The <see cref="GsmAtProbeResult"/></returns>
        private GsmAtProbeResult ProbeAtPort(string comPort) {
            _cancellationToken.ThrowIfCancellationRequested();

            var result = new GsmAtProbeResult {
                PortName = comPort
            };

            try {
                _logger?.Info($"Öffne Port {comPort}");

                using var port = new SerialPort(comPort, DefaultBaudRate, Parity.None, 8, StopBits.One) {
                    Handshake = Handshake.None,
                    DtrEnable = true,
                    RtsEnable = true,
                    NewLine = "\r\n",
                    ReadTimeout = DefaultReadTimeoutMs,
                    WriteTimeout = DefaultWriteTimeoutMs,
                    Encoding = Encoding.ASCII
                };

                port.Open();
                System.Threading.Thread.Sleep(DefaultOpenDelayMs);

                result.Success = SendBasicAT(port);
                if (!result.Success) {
                    result.ErrorMessage = "Keine gültige AT-Antwort erhalten.";
                    return result;
                }

                result.ATI = Query(port, "ATI");
                result.Manufacturer = CleanAtValue(Query(port, "AT+CGMI"));
                result.Model = CleanAtValue(Query(port, "AT+CGMM"));
                result.IMEI = ExtractDigits(CleanAtValue(QueryAny(port, "AT+CGSN", "AT+GSN")));
                result.ICCID = ExtractDigits(CleanAtValue(QueryWithRetry(port, "AT+CCID")));
                result.IMSI = ExtractDigits(CleanAtValue(Query(port, "AT+CIMI")));

                string cnumResponse = QueryWithRetry(port, "AT+CNUM", 3, 1200);
                _logger?.Info($"AT+CNUM RAW >>> [{cnumResponse}]");
                result.Telefonnummer = ParseCNUM(cnumResponse);
                _logger?.Info($"AT+CNUM PARSED >>> [{result.Telefonnummer}]");

                result.ProviderName = ParseProvider(Query(port, "AT+COPS?"));
                result.SignalStrength = ParseSignalStrength(Query(port, "AT+CSQ"));
                result.IsRegistered = ParseRegistered(QueryAny(port, "AT+CREG?", "AT+CGREG?", "AT+CEREG?"));

                return result;
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                _logger?.Error($"ProbeAtPort Fehler | Port={comPort} | Fehler={ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// The SendBasicAT
        /// </summary>
        /// <param name="port">The port<see cref="SerialPort"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private bool SendBasicAT(SerialPort port) {
            foreach (int baud in new[] { 115200, 9600, 57600, 19200, 38400 }) {
                _cancellationToken.ThrowIfCancellationRequested();
                try {
                    if (port.IsOpen) {
                        port.Close();
                    }

                    port.BaudRate = baud;
                    port.Open();
                    System.Threading.Thread.Sleep(DefaultOpenDelayMs);

                    _logger?.Info($"Teste Baudrate {baud} auf Port {port.PortName}");

                    string response = Query(port, "AT");
                    if (ContainsOk(response)) {
                        _logger?.Info($"AT erfolgreich | Port={port.PortName} | Baud={baud}");
                        return true;
                    }

                    response = Query(port, "ATE0");
                    if (ContainsOk(response)) {
                        _logger?.Info($"ATE0 erfolgreich | Port={port.PortName} | Baud={baud}");
                        return true;
                    }

                    port.Close();
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception ex) {
                    _logger?.Warn($"SendBasicAT Fehler | Port={port.PortName} | Baud={baud} | Fehler={ex.Message}");
                    try {
                        if (port.IsOpen)
                            port.Close();
                    } catch {
                        // ignorieren
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// The QueryAny
        /// </summary>
        /// <param name="port">The port<see cref="SerialPort"/></param>
        /// <param name="commands">The commands<see cref="string[]"/></param>
        /// <returns>The <see cref="string"/></returns>
        private string QueryAny(SerialPort port, params string[] commands) {
            foreach (var cmd in commands) {
                var response = Query(port, cmd);
                if (!string.IsNullOrWhiteSpace(CleanAtValue(response)))
                    return response;

                if (ContainsOk(response))
                    return response;
            }

            return string.Empty;
        }

        /// <summary>
        /// The Query
        /// </summary>
        /// <param name="port">The port<see cref="SerialPort"/></param>
        /// <param name="command">The command<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private string Query(SerialPort port, string command) {
            try {
                _cancellationToken.ThrowIfCancellationRequested();

                port.DiscardInBuffer();
                port.DiscardOutBuffer();

                _logger?.Info($"AT SEND | Port={port.PortName} | CMD={command}");

                port.Write(command + "\r");

                var sb = new StringBuilder();
                var until = DateTime.Now.AddMilliseconds(DefaultReadTimeoutMs);

                while (DateTime.Now < until) {
                    _cancellationToken.ThrowIfCancellationRequested();
                    try {
                        string chunk = port.ReadExisting();
                        if (!string.IsNullOrEmpty(chunk)) {
                            sb.Append(chunk);

                            string txt = sb.ToString();
                            if (txt.Contains("\r\nOK\r\n", StringComparison.OrdinalIgnoreCase) ||
                                txt.Contains("\nOK\n", StringComparison.OrdinalIgnoreCase) ||
                                txt.Contains("ERROR", StringComparison.OrdinalIgnoreCase)) {
                                break;
                            }
                        }
                    } catch {
                        // ignorieren
                    }

                    System.Threading.Thread.Sleep(50);
                }

                string response = sb.ToString();
                _logger?.Debug($"AT RECV | Port={port.PortName} | CMD={command} | RAW=[{response}]");

                return response;
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) {
                _logger?.Error($"Query Fehler | Port={port.PortName} | CMD={command} | Fehler={ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// The ParseRegistered
        /// </summary>
        /// <param name="response">The response<see cref="string"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private bool ParseRegistered(string response) {
            var match = Regex.Match(response ?? string.Empty, @"\+C(?:G|E)?REG:\s*\d,\s*(\d)");
            if (!match.Success)
                return false;

            string stat = match.Groups[1].Value;
            return stat == "1" || stat == "5";
        }

        /// <summary>
        /// The ParseSignalStrength
        /// </summary>
        /// <param name="response">The response<see cref="string"/></param>
        /// <returns>The <see cref="int?"/></returns>
        private int? ParseSignalStrength(string response) {
            var match = Regex.Match(response ?? string.Empty, @"\+CSQ:\s*(\d{1,2}),");
            if (!match.Success)
                return null;

            if (!int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rssi))
                return null;

            if (rssi == 99)
                return null;

            return rssi;
        }

        /// <summary>
        /// The ParseProvider
        /// </summary>
        /// <param name="response">The response<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private string ParseProvider(string response) {
            var match = Regex.Match(response ?? string.Empty, "\"([^\"]+)\"");
            return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
        }

        /// <summary>
        /// The ParseCNUM
        /// </summary>
        /// <param name="response">The response<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private string ParseCNUM(string response) {
            if (string.IsNullOrWhiteSpace(response))
                return string.Empty;

            var lines = response.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var rawLine in lines) {
                string line = rawLine.Trim();

                if (!line.StartsWith("+CNUM:", StringComparison.OrdinalIgnoreCase))
                    continue;

                var match = Regex.Match(line, "\"([^\"]*)\"\\s*,\\s*\"([^\"]+)\"\\s*,\\s*(\\d+)");
                if (match.Success)
                    return match.Groups[2].Value.Trim();
            }

            return string.Empty;
        }

        /// <summary>
        /// The ContainsOk
        /// </summary>
        /// <param name="value">The value<see cref="string"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool ContainsOk(string value) {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return value.IndexOf("OK", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// The IsRelevantGsmDevice
        /// </summary>
        /// <param name="dev">The dev<see cref="PnpDeviceInfo"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool IsRelevantGsmDevice(PnpDeviceInfo dev) {
            string text = $"{dev.Name} {dev.Description} {dev.Manufacturer} {dev.Service}".ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(text))
                return false;

            // Harte Ausschlüsse
            if (text.Contains("intel(r) serial io gpio"))
                return false;

            if (text.Contains("intel(r) serial io i2c"))
                return false;

            if (text.Contains("acpi-schalter"))
                return false;

            if (text.Contains("acpi switch"))
                return false;

            if (text.Contains("windows.networking.networkoperators.mobilebroadbandaccount"))
                return false;

            if (text.Contains("mobilebroadbandaccount"))
                return false;

            // Nur Geräte berücksichtigen, die modemtypisch wirken
            return ContainsAny(text,
                "modem",
                "gsm",
                "gprs",
                "edge",
                "umts",
                "hsupa",
                "hspa",
                "lte",
                "5g",
                "wwan",
                "simcom",
                "quectel",
                "fibocom",
                "sierra",
                "telit",
                "zte",
                "huawei",
                "u-blox",
                "ublox",
                "qualcomm hs-usb",
                "mobile broadband",
                "at port");
        }

        /// <summary>
        /// The IsStrongModemHint
        /// </summary>
        /// <param name="dev">The dev<see cref="PnpDeviceInfo"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int IsStrongModemHint(PnpDeviceInfo dev) {
            string text = $"{dev.Name} {dev.Description}".ToLowerInvariant();
            int score = 0;

            if (ContainsAny(text, "modem", "mobile", "gsm", "lte", "wwan"))
                score += 10;

            if (!string.IsNullOrWhiteSpace(dev.ComPort))
                score += 5;

            if (ContainsAny(text, "at", "application", "pc ui"))
                score += 3;

            return score;
        }

        /// <summary>
        /// The GuessModel
        /// </summary>
        /// <param name="name">The name<see cref="string"/></param>
        /// <param name="description">The description<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string GuessModel(string name, string description) {
            string combined = $"{name} {description}".Trim();
            return combined.Length > 150 ? combined.Substring(0, 150) : combined;
        }

        /// <summary>
        /// The BuildMatchKey
        /// </summary>
        /// <param name="pnpDeviceId">The pnpDeviceId<see cref="string"/></param>
        /// <param name="name">The name<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string BuildMatchKey(string pnpDeviceId, string name) {
            string source = !string.IsNullOrWhiteSpace(pnpDeviceId) ? pnpDeviceId : name;

            if (string.IsNullOrWhiteSpace(source))
                return Guid.NewGuid().ToString("N");

            var vidPid = Regex.Match(source, @"VID_([0-9A-F]{4}).*PID_([0-9A-F]{4})", RegexOptions.IgnoreCase);
            var serial = Regex.Match(source, @"\\([^\\&]{6,})$", RegexOptions.IgnoreCase);
            var portBase = Regex.Match(name ?? string.Empty, @"^(.*?)(\s*\(COM\d+\))?$", RegexOptions.IgnoreCase);

            if (vidPid.Success) {
                string key = $"VID_{vidPid.Groups[1].Value}_PID_{vidPid.Groups[2].Value}";
                if (serial.Success)
                    key += $"_{serial.Groups[1].Value}";
                return key.ToUpperInvariant();
            }

            if (portBase.Success && !string.IsNullOrWhiteSpace(portBase.Groups[1].Value))
                return portBase.Groups[1].Value.Trim().ToUpperInvariant();

            return source.Trim().ToUpperInvariant();
        }

        /// <summary>
        /// The ExtractComPort
        /// </summary>
        /// <param name="text">The text<see cref="string"/></param>
        /// <returns>The <see cref="string?"/></returns>
        private static string? ExtractComPort(string text) {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var match = Regex.Match(text, @"\((COM\d+)\)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
        }

        /// <summary>
        /// The CleanAtValue
        /// </summary>
        /// <param name="response">The response<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string CleanAtValue(string response) {
            if (string.IsNullOrWhiteSpace(response))
                return string.Empty;

            var lines = response
                .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x) &&
                    !x.Equals("OK", StringComparison.OrdinalIgnoreCase) &&
                    !x.Equals("ERROR", StringComparison.OrdinalIgnoreCase) &&
                    !x.StartsWith("AT", StringComparison.OrdinalIgnoreCase))
                .ToList();

            return string.Join(" ", lines).Trim();
        }

        /// <summary>
        /// The ExtractDigits
        /// </summary>
        /// <param name="value">The value<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string ExtractDigits(string value) {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return new string(value.Where(char.IsDigit).ToArray());
        }

        /// <summary>
        /// The ContainsAny
        /// </summary>
        /// <param name="text">The text<see cref="string"/></param>
        /// <param name="needles">The needles<see cref="string[]"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool ContainsAny(string text, params string[] needles) {
            foreach (var needle in needles) {
                if (text.Contains(needle, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The SamePort
        /// </summary>
        /// <param name="a">The a<see cref="string?"/></param>
        /// <param name="b">The b<see cref="string?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool SamePort(string? a, string? b) {
            return string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The GetString
        /// </summary>
        /// <param name="mo">The mo<see cref="ManagementObject"/></param>
        /// <param name="propertyName">The propertyName<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string GetString(ManagementObject mo, string propertyName) {
            try {
                return mo[propertyName]?.ToString()?.Trim() ?? string.Empty;
            } catch {
                return string.Empty;
            }
        }

        /// <summary>
        /// The GetBool
        /// </summary>
        /// <param name="mo">The mo<see cref="ManagementObject"/></param>
        /// <param name="propertyName">The propertyName<see cref="string"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool GetBool(ManagementObject mo, string propertyName) {
            try {
                return mo[propertyName] != null && Convert.ToBoolean(mo[propertyName], CultureInfo.InvariantCulture);
            } catch {
                return false;
            }
        }

        /// <summary>
        /// The Clean
        /// </summary>
        /// <param name="value">The value<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string Clean(string value) {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        /// <summary>
        /// The Normalize
        /// </summary>
        /// <param name="value">The value<see cref="string?"/></param>
        /// <returns>The <see cref="string?"/></returns>
        private static string? Normalize(string? value) {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        /// <summary>
        /// The QueryWithRetry
        /// </summary>
        /// <param name="port">The port<see cref="SerialPort"/></param>
        /// <param name="command">The command<see cref="string"/></param>
        /// <param name="attempts">The attempts<see cref="int"/></param>
        /// <param name="delayMs">The delayMs<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        private string QueryWithRetry(SerialPort port, string command, int attempts = 5, int delayMs = 800) {
            for (int i = 0; i < attempts; i++) {
                _cancellationToken.ThrowIfCancellationRequested();
                _logger?.Info($"Retry {i + 1}/{attempts} | Port={port.PortName} | CMD={command}");

                string response = Query(port, command);
                string clean = CleanAtValue(response);

                if (!string.IsNullOrWhiteSpace(clean) &&
                    !clean.Equals("ERROR", StringComparison.OrdinalIgnoreCase)) {
                    return response;
                }

                System.Threading.Thread.Sleep(delayMs);
            }

            return string.Empty;
        }
    }

    /// <summary>
    /// Defines the <see cref="GsmDeviceMap" />
    /// </summary>
    public sealed class GsmDeviceMap {
        // TK_GSM_Modems

        /// <summary>
        /// Gets or sets the ModemName
        /// </summary>
        public string ModemName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Manufacturer
        /// </summary>
        public string Manufacturer { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Model
        /// </summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the IMEI
        /// </summary>
        public string IMEI { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the PNPDeviceID
        /// </summary>
        public string PNPDeviceID { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether Active
        /// </summary>
        public bool Active { get; set; }

        // TK_GSM_Sim

        /// <summary>
        /// Gets or sets the Telefonnummer
        /// </summary>
        public string Telefonnummer { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ProviderName
        /// </summary>
        public string ProviderName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ICCID
        /// </summary>
        public string ICCID { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the IMSI
        /// </summary>
        public string IMSI { get; set; } = string.Empty;

        /// <summary>
        /// Gets a value indicating whether SimActive
        /// </summary>
        public bool SimActive => IsSimInserted;

        // TK_GSM_ModemSimStatus

        /// <summary>
        /// Gets or sets a value indicating whether IsPresent
        /// </summary>
        public bool IsPresent { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether IsSimInserted
        /// </summary>
        public bool IsSimInserted { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether IsRegistered
        /// </summary>
        public bool IsRegistered { get; set; }

        /// <summary>
        /// Gets or sets the SignalStrength
        /// </summary>
        public int? SignalStrength { get; set; }

        /// <summary>
        /// Gets or sets the ComPortAT
        /// </summary>
        public string? ComPortAT { get; set; }

        /// <summary>
        /// Gets or sets the ComPortAudio
        /// </summary>
        public string? ComPortAudio { get; set; }

        /// <summary>
        /// Gets or sets the ComPortDiag
        /// </summary>
        public string? ComPortDiag { get; set; }

        /// <summary>
        /// Gets or sets the ComPortNMEA
        /// </summary>
        public string? ComPortNMEA { get; set; }

        /// <summary>
        /// Gets or sets the LastSeen
        /// </summary>
        public DateTime? LastSeen { get; set; }

        /// <summary>
        /// Gets or sets the LastError
        /// </summary>
        public string? LastError { get; set; }

        // intern

        /// <summary>
        /// Gets or sets the RawPnPDevices
        /// </summary>
        public List<PnpDeviceInfo> RawPnPDevices { get; set; } = new();

        /// <summary>
        /// Gets or sets the Probes
        /// </summary>
        public List<GsmAtProbeResult> Probes { get; set; } = new();
    }

    /// <summary>
    /// Defines the <see cref="GsmAtProbeResult" />
    /// </summary>
    public sealed class GsmAtProbeResult {
        /// <summary>
        /// Gets or sets the PortName
        /// </summary>
        public string PortName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether Success
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the ErrorMessage
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the ATI
        /// </summary>
        public string ATI { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Manufacturer
        /// </summary>
        public string Manufacturer { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Model
        /// </summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the IMEI
        /// </summary>
        public string IMEI { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ICCID
        /// </summary>
        public string ICCID { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the IMSI
        /// </summary>
        public string IMSI { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Telefonnummer
        /// </summary>
        public string Telefonnummer { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ProviderName
        /// </summary>
        public string ProviderName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the SignalStrength
        /// </summary>
        public int? SignalStrength { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether IsRegistered
        /// </summary>
        public bool IsRegistered { get; set; }
    }

    /// <summary>
    /// Defines the <see cref="PnpDeviceInfo" />
    /// </summary>
    public sealed class PnpDeviceInfo {
        /// <summary>
        /// Gets or sets the Name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the DeviceID
        /// </summary>
        public string DeviceID { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the PNPDeviceID
        /// </summary>
        public string PNPDeviceID { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Manufacturer
        /// </summary>
        public string Manufacturer { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Service
        /// </summary>
        public string Service { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ClassGuid
        /// </summary>
        public string ClassGuid { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether Present
        /// </summary>
        public bool Present { get; set; }

        /// <summary>
        /// Gets or sets the Status
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ComPort
        /// </summary>
        public string? ComPort { get; set; }

        /// <summary>
        /// Gets or sets the MatchKey
        /// </summary>
        public string MatchKey { get; set; } = string.Empty;
    }
}
