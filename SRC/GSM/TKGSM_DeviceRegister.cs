/*
 * TKGSM_DeviceRegister
 * Aufgabe:
 * - Übernimmt die von TKGSM_DeviceMapping erkannten GSM-Geräte.
 * - Schreibt Modem-, SIM- und Statusdaten in die zentrale Datenbank.
 * - Aktualisiert daraus die GSM-Statussicht und die externe Nummernsicht.
 *
 * Nicht Aufgabe:
 * - Keine eigene Geräteerkennung.
 * - Keine Session-Steuerung.
 * - Kein Routing.
 * - Keine Call-Steuerung.
 * - Keine Media-Steuerung.
 *
 * Diese Datei ist die Persistenz- und Registerschicht für erkannte GSM-Geräte.
 */

namespace TKServer.GSM {
    using System;
    using System.Collections.Generic;
    using System.Threading;

    using TKServer.BASE;
    using TKServer.SQL;

    /// TKGSM_DeviceRegister
    /// Aufgabe:
    /// - Übernimmt die von TKGSM_DeviceMapping erkannten GSM-Geräte.
    /// - Schreibt Modem-, SIM- und Statusdaten in die zentrale Datenbank.
    /// - Aktualisiert daraus die GSM-Statussicht und die externe Nummernsicht.
    /// 
    /// Nicht Aufgabe:
    /// - Keine eigene Geräteerkennung.
    /// - Keine Session-Steuerung.
    /// - Kein Routing.
    /// - Keine Call-Steuerung.
    /// - Keine Media-Steuerung.
    /// 
    /// Diese Datei ist die Persistenz- und Registerschicht für erkannte GSM-Geräte.

    /// <summary>
    /// Defines the <see cref="TKGSM_DeviceRegister" />
    /// </summary>
    public class TKGSM_DeviceRegister {
        /// <summary>
        /// Defines the _logger
        /// </summary>
        private readonly TKBase_LogManager? _logger;

        /// <summary>
        /// Defines the _commands
        /// </summary>
        private readonly TKSQL_Commands _commands;

        /// <summary>
        /// Initializes a new instance of the <see cref="TKGSM_DeviceRegister"/> class.
        /// </summary>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        public TKGSM_DeviceRegister(TKBase_LogManager? logger) {
            _logger = logger;
            _commands = new TKSQL_Commands(logger);
        }

        /// <summary>
        /// The Execute
        /// </summary>
        /// <param name="cancellationToken">The cancellationToken<see cref="CancellationToken"/></param>
        public void Execute(CancellationToken cancellationToken = default) {
            try {
                _logger?.Info("GSM_Device_Register gestartet.");

                cancellationToken.ThrowIfCancellationRequested();

                TKGSM_DeviceMapping mapper = new TKGSM_DeviceMapping(_logger, cancellationToken);
                List<GsmDeviceMap> devices = mapper.DiscoverDevices();

                if (devices == null || devices.Count == 0) {
                    _logger?.Warn("Keine GSM-Geräte gefunden.");
                    return;
                }

                foreach (GsmDeviceMap device in devices) {
                    cancellationToken.ThrowIfCancellationRequested();
                    SaveDevice(device);
                }

                _logger?.Info("GSM_Device_Register abgeschlossen.");
            } catch (OperationCanceledException) {
                _logger?.Info("GSM_Device_Register abgebrochen.");
                throw;
            } catch (Exception ex) {
                _logger?.Error($"Fehler in GSM_Device_Register.Execute: {ex.Message}");
            }
        }

        /// <summary>
        /// The SaveDevice
        /// </summary>
        /// <param name="device">The device<see cref="GsmDeviceMap"/></param>
        private void SaveDevice(GsmDeviceMap device) {
            try {
                if (device == null) {
                    _logger?.Warn("Leeres Gerät übersprungen.");
                    return;
                }

                string? pnpDeviceId = Normalize(device.PNPDeviceID);
                string? imei = Normalize(device.IMEI);
                string? modemName = Normalize(device.ModemName);
                string? manufacturer = Normalize(device.Manufacturer);
                string? model = Normalize(device.Model);

                string? iccid = Normalize(device.ICCID);
                string? imsi = Normalize(device.IMSI);
                string? telefonnummer = Normalize(device.Telefonnummer);
                string? providerName = Normalize(device.ProviderName);

                string modemKeyColumn;
                object? modemKeyValue;

                if (!string.IsNullOrWhiteSpace(pnpDeviceId)) {
                    modemKeyColumn = "PNPDeviceID";
                    modemKeyValue = pnpDeviceId;
                } else if (!string.IsNullOrWhiteSpace(imei)) {
                    modemKeyColumn = "IMEI";
                    modemKeyValue = imei;
                } else {
                    _logger?.Warn($"Gerät ohne PNPDeviceID und ohne IMEI übersprungen | Modem={modemName}");
                    return;
                }

                int modemId = _commands.UpsertDynamic(
                    "TK_GSM_Modems",
                    modemKeyColumn,
                    modemKeyValue,
                    new SqlFieldValue("ModemName", modemName),
                    new SqlFieldValue("Manufacturer", manufacturer),
                    new SqlFieldValue("Model", model),
                    new SqlFieldValue("IMEI", imei),
                    new SqlFieldValue("PNPDeviceID", pnpDeviceId),
                    new SqlFieldValue("Active", device.Active)
                );

                Guid modemGuid = _commands.GetGsmGuidById("TK_GSM_Modems", modemId);

                int? simId = null;
                Guid? simGuid = null;

                if (!string.IsNullOrWhiteSpace(iccid) ||
                    !string.IsNullOrWhiteSpace(imsi) ||
                    !string.IsNullOrWhiteSpace(telefonnummer)) {

                    string simKeyColumn;
                    object? simKeyValue;

                    if (!string.IsNullOrWhiteSpace(iccid)) {
                        simKeyColumn = "ICCID";
                        simKeyValue = iccid;
                    } else if (!string.IsNullOrWhiteSpace(imsi)) {
                        simKeyColumn = "IMSI";
                        simKeyValue = imsi;
                    } else {
                        simKeyColumn = "Telefonnummer";
                        simKeyValue = telefonnummer;
                    }

                    simId = _commands.UpsertDynamic(
                        "TK_GSM_Sim",
                        simKeyColumn,
                        simKeyValue,
                        new SqlFieldValue("Telefonnummer", telefonnummer),
                        new SqlFieldValue("ProviderName", providerName),
                        new SqlFieldValue("ICCID", iccid),
                        new SqlFieldValue("IMSI", imsi),
                        new SqlFieldValue("Active", device.IsSimInserted)
                    );

                    simGuid = _commands.GetGsmGuidById("TK_GSM_Sim", simId.Value);
                }

                Guid modemSimStatusGuid = _commands.BuildGsmModemSimStatusGuid(modemId, simId);

                int statusId = _commands.UpsertDynamic(
                    "TK_GSM_ModemSimStatus",
                    new[] {
                        new SqlFieldValue("ModemID", modemId),
                        new SqlFieldValue("SimID", simId)
                    },
                    new SqlFieldValue("GUID", modemSimStatusGuid),
                    new SqlFieldValue("IsPresent", device.IsPresent),
                    new SqlFieldValue("IsSimInserted", device.IsSimInserted),
                    new SqlFieldValue("IsRegistered", device.IsRegistered),
                    new SqlFieldValue("SignalStrength", device.SignalStrength),
                    new SqlFieldValue("ComPortAT", Normalize(device.ComPortAT)),
                    new SqlFieldValue("ComPortAudio", Normalize(device.ComPortAudio)),
                    new SqlFieldValue("ComPortDiag", Normalize(device.ComPortDiag)),
                    new SqlFieldValue("ComPortNMEA", Normalize(device.ComPortNMEA)),
                    new SqlFieldValue("LastSeen", device.LastSeen),
                    new SqlFieldValue("LastError", Normalize(device.LastError))
                );

                _commands.UpsertExternalNumberFromGsmStatus(statusId, device.IsPresent && device.IsRegistered, true);

                _logger?.Info(
                    $"GSM Gerät gespeichert | Modem={modemName} | IMEI={imei} | ModemID={modemId} | ModemGUID={modemGuid} | SimID={(simId.HasValue ? simId.Value.ToString() : "NULL")} | SimGUID={(simGuid.HasValue ? simGuid.Value.ToString() : "NULL")} | StatusID={statusId} | StatusGUID={modemSimStatusGuid}"
                );
            } catch (OperationCanceledException) {
                _logger?.Info("GSM_Device_Register abgebrochen.");
                throw;
            } catch (Exception ex) {
                _logger?.Error($"Fehler beim Speichern des Geräts | Modem={device?.ModemName} | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The Normalize
        /// </summary>
        /// <param name="value">The value<see cref="string?"/></param>
        /// <returns>The <see cref="string?"/></returns>
        private static string? Normalize(string? value) {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
