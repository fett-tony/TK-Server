/*
 * TKGSM_Routing
 * Aufgabe:
 * - Enthält ausschließlich die Routinglogik des GSM-Bereichs.
 * - Ermittelt GSM-bezogene Ziele, Geräte- oder Leitungszuordnungen innerhalb des Bereichs.
 * - Stellt die bereichsspezifische Zielauflösung für GSM bereit.
 *
 * Nicht Aufgabe:
 * - Keine globale Routingkoordination über alle Bereiche.
 * - Keine Session-, Leg- oder Gesprächssteuerung.
 * - Keine Fachlogik von SipInt, SipExt oder SVC übernehmen.
 * 
 * Diese information darf nicht gelöscht werden!
 */

namespace TKServer.GSM {
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;

    using TKServer.BASE;
    using TKServer.SipExt;
    using TKServer.SQL;
    /// TKGSM_Routing
    /// Aufgabe:
    /// - Enthält ausschließlich die Routinglogik des GSM-Bereichs.
    /// - Ermittelt GSM-bezogene Ziele, Geräte- oder Leitungszuordnungen innerhalb des Bereichs.
    /// - Stellt die bereichsspezifische Zielauflösung für GSM bereit.
    /// 
    /// Nicht Aufgabe:
    /// - Keine globale Routingkoordination über alle Bereiche.
    /// - Keine Session-, Leg- oder Gesprächssteuerung.
    /// - Keine Fachlogik von SipInt, SipExt oder SVC übernehmen.
    /// 
    /// Diese information darf nicht gelöscht werden!

    /// <summary>
    /// Defines the <see cref="TKGSM_Routing" />
    /// </summary>
    internal sealed class TKGSM_Routing {
        /// <summary>
        /// Defines the _sipSql
        /// </summary>
        private readonly TKSQL_Commands _sipSql;

        /// <summary>
        /// Defines the _logger
        /// </summary>
        private readonly TKBase_LogManager? _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TKGSM_Routing"/> class.
        /// </summary>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        public TKGSM_Routing(TKBase_LogManager? logger = null) {
            _logger = logger;
            _sipSql = new TKSQL_Commands(logger);
        }

        /// <summary>
        /// The ResolveExternalTarget
        /// </summary>
        /// <param name="fromNumber">The fromNumber<see cref="string"/></param>
        /// <param name="calledNumber">The calledNumber<see cref="string"/></param>
        /// <returns>The <see cref="SIPIntResolvedTarget"/></returns>
        public SIPIntResolvedTarget ResolveExternalTarget(string fromNumber, string calledNumber) {
            string normalizedCalled = TKSipExt_Routing.NormalizeExternalNumber(calledNumber);
            if (string.IsNullOrWhiteSpace(normalizedCalled)) {
                return SIPIntResolvedTarget.Fail(calledNumber, "Keine externe Rufnummer.");
            }

            DataRow? caller = ResolveCaller(fromNumber);
            if (caller == null) {
                _logger?.Warn($"GSM Routing abgewiesen | Caller unbekannt | Von={fromNumber} | An={normalizedCalled}");
                return SIPIntResolvedTarget.Fail(normalizedCalled, "Anrufer nicht gefunden.");
            }

            if (!(caller["CanDialExternal"] != DBNull.Value && Convert.ToBoolean(caller["CanDialExternal"]))) {
                _logger?.Warn($"GSM Routing abgewiesen | Keine Berechtigung | Von={fromNumber} | An={normalizedCalled}");
                return SIPIntResolvedTarget.Fail(normalizedCalled, "Externe Gespräche nicht erlaubt.");
            }

            int callerId = Convert.ToInt32(caller["ID"]);
            string callerType = (Convert.ToString(caller["UserType"]) ?? string.Empty).Trim();
            string callerUsername = (Convert.ToString(caller["Username"]) ?? string.Empty).Trim();

            List<DataRow> routes = _sipSql.GetExternalOutboundRoutesForUser(callerId, callerType);
            if (routes.Count == 0 && !string.IsNullOrWhiteSpace(callerType)) {
                routes = _sipSql.GetExternalOutboundRoutesForUser(callerId, null);
            }

            if (routes.Count == 0) {
                return SIPIntResolvedTarget.Fail(normalizedCalled, "Keine externe Nummer zugewiesen.");
            }

            foreach (DataRow route in routes.OrderBy(r => r["Priority"] == DBNull.Value ? int.MaxValue : Convert.ToInt32(r["Priority"]))) {
                SIPIntResolvedTarget? candidate = TryResolveCandidate(route, normalizedCalled, fromNumber, callerId, callerType, callerUsername);
                if (candidate != null) {
                    return candidate;
                }
            }

            return SIPIntResolvedTarget.Fail(normalizedCalled, "Keine nutzbare GSM-Route verfügbar.");
        }

        /// <summary>
        /// The TryResolveCandidate
        /// </summary>
        /// <param name="route">The route<see cref="DataRow"/></param>
        /// <param name="normalizedCalled">The normalizedCalled<see cref="string"/></param>
        /// <param name="fromNumber">The fromNumber<see cref="string"/></param>
        /// <param name="callerId">The callerId<see cref="int"/></param>
        /// <param name="callerType">The callerType<see cref="string"/></param>
        /// <param name="callerUsername">The callerUsername<see cref="string"/></param>
        /// <returns>The <see cref="SIPIntResolvedTarget?"/></returns>
        internal SIPIntResolvedTarget? TryResolveCandidate(DataRow route, string normalizedCalled, string fromNumber, int callerId, string callerType, string callerUsername) {
            int routingId = route["ID"] == DBNull.Value ? 0 : Convert.ToInt32(route["ID"]);
            int priority = route["Priority"] == DBNull.Value ? int.MaxValue : Convert.ToInt32(route["Priority"]);
            int externalNumberId = route["ExternalNumberID"] == DBNull.Value ? 0 : Convert.ToInt32(route["ExternalNumberID"]);
            string routeTargetType = (Convert.ToString(route["TargetType"]) ?? string.Empty).Trim();
            string sourceType = (Convert.ToString(route["SourceType"]) ?? string.Empty).Trim();
            int sourceId = route["SourceID"] == DBNull.Value ? 0 : Convert.ToInt32(route["SourceID"]);
            bool routeActive = route["Active"] != DBNull.Value && Convert.ToBoolean(route["Active"]);
            bool externalActive = route.Table.Columns.Contains("ExternalNumberActive") && route["ExternalNumberActive"] != DBNull.Value && Convert.ToBoolean(route["ExternalNumberActive"]);
            bool isOnline = route.Table.Columns.Contains("IsOnline") && route["IsOnline"] != DBNull.Value && Convert.ToBoolean(route["IsOnline"]);
            bool isSelectable = route.Table.Columns.Contains("IsSelectable") && route["IsSelectable"] != DBNull.Value && Convert.ToBoolean(route["IsSelectable"]);

            if (!routeActive || !externalActive) {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(routeTargetType) &&
                !string.Equals(routeTargetType, callerType, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(routeTargetType, "Alle", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(routeTargetType, "Any", StringComparison.OrdinalIgnoreCase)) {
                return null;
            }

            if (!string.Equals(sourceType, "GSMStatus", StringComparison.OrdinalIgnoreCase) || sourceId <= 0) {
                return null;
            }

            if (!isSelectable || !isOnline) {
                _logger?.Warn($"GSM Routing Kandidat übersprungen | RouteID={routingId} | Priority={priority} | ExternalNumberID={externalNumberId} | Grund={(isSelectable ? "Offline" : "NichtSelektierbar")}");
                return null;
            }

            DataRow? gsm = _sipSql.GetActiveGsmRuntimeDeviceByStatusId(sourceId);
            if (gsm == null) {
                _logger?.Warn($"GSM Routing Kandidat übersprungen | RouteID={routingId} | Priority={priority} | StatusID={sourceId} | Grund=GSMRuntimeNichtGefunden");
                return null;
            }

            bool isRegistered = gsm.Table.Columns.Contains("IsRegistered") && gsm["IsRegistered"] != DBNull.Value && Convert.ToBoolean(gsm["IsRegistered"]);
            string comPortAT = ReadString(gsm, "ComPortAT");
            string comPortAudio = ReadString(gsm, "ComPortAudio");
            string comPortNMEA = ReadString(gsm, "ComPortNMEA");
            string simNumber = ReadString(gsm, "Telefonnummer");
            string imei = ReadString(gsm, "IMEI");
            string providerName = ReadString(gsm, "ProviderName");
            int? modemId = ReadNullableInt(gsm, "ModemID");
            int? simId = ReadNullableInt(gsm, "SimID");

            if (!isRegistered) {
                _logger?.Warn($"GSM Routing Kandidat übersprungen | RouteID={routingId} | Priority={priority} | StatusID={sourceId} | Grund=NichtImMobilfunknetzRegistriert");
                return null;
            }

            if (string.IsNullOrWhiteSpace(comPortAT)) {
                _logger?.Warn($"GSM Routing Kandidat übersprungen | RouteID={routingId} | Priority={priority} | StatusID={sourceId} | Grund=KeinATPort");
                return null;
            }

            if (string.IsNullOrWhiteSpace(comPortAudio)) {
                _logger?.Warn($"GSM Routing Kandidat übersprungen | RouteID={routingId} | Priority={priority} | StatusID={sourceId} | Grund=KeinAudioPort");
                return null;
            }

            _logger?.Info($"GSM Routing OK | UserID={callerId} | UserType={callerType} | Von={fromNumber} | An={normalizedCalled} | RouteID={routingId} | Priority={priority} | StatusID={sourceId} | ModemID={modemId} | SimID={simId} | AT={comPortAT} | Audio={comPortAudio} | NMEA={comPortNMEA} | SIM={simNumber} | IMEI={imei}");
            return SIPIntResolvedTarget.OkGsm(
                normalizedCalled,
                sourceId,
                modemId,
                simId,
                comPortAT,
                comPortAudio,
                simNumber,
                imei,
                providerName,
                $"GSM RouteID={routingId}; Priority={priority}; StatusID={sourceId}",
                callerUsername);
        }

        /// <summary>
        /// The ResolveCaller
        /// </summary>
        /// <param name="fromNumber">The fromNumber<see cref="string"/></param>
        /// <returns>The <see cref="DataRow?"/></returns>
        private DataRow? ResolveCaller(string fromNumber) {
            DataRow? caller = _sipSql.GetSipUserByUsername(fromNumber);
            if (caller != null) {
                return caller;
            }

            caller = _sipSql.GetSipUserByNumber(fromNumber);
            if (caller != null) {
                return caller;
            }

            string normalized = TKSipExt_Routing.NormalizeExternalNumber(fromNumber);
            if (!string.IsNullOrWhiteSpace(normalized)) {
                caller = _sipSql.GetSipUserByNumber(normalized);
            }

            return caller;
        }

        /// <summary>
        /// The ReadString
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="columnName">The columnName<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string ReadString(DataRow row, string columnName) {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value) {
                return string.Empty;
            }

            return Convert.ToString(row[columnName])?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// The ReadNullableInt
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="columnName">The columnName<see cref="string"/></param>
        /// <returns>The <see cref="int?"/></returns>
        private static int? ReadNullableInt(DataRow row, string columnName) {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value) {
                return null;
            }

            return Convert.ToInt32(row[columnName]);
        }
    }
}
