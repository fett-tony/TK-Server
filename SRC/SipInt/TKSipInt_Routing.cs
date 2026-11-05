/*
 * TKSipInt_Routing
 * Aufgabe:
 * - Enthält ausschließlich die Routinglogik des internen SIP-Bereichs.
 * - Ermittelt interne Benutzer-, Nebenstellen- und interne Zielauflösungen.
 * - Stellt die bereichsspezifische Zielauflösung für SipInt bereit.
 *
 * Nicht Aufgabe:
 * - Keine globale Routingkoordination über alle Bereiche.
 * - Keine Session-, Leg- oder Gesprächssteuerung.
 * - Keine Fachlogik von SipExt, GSM oder SVC übernehmen.
 * 
 * Diese information darf nicht gelöscht werden!
 */

namespace TKServer.SipInt {
    using System;
    using System.Data;
    using System.Linq;

    using TKServer.BASE;
    using TKServer.SQL;
    /// TKSipInt_Routing
    /// Aufgabe:
    /// - Enthält ausschließlich die Routinglogik des internen SIP-Bereichs.
    /// - Ermittelt interne Benutzer-, Nebenstellen- und interne Zielauflösungen.
    /// - Stellt die bereichsspezifische Zielauflösung für SipInt bereit.
    /// 
    /// Nicht Aufgabe:
    /// - Keine globale Routingkoordination über alle Bereiche.
    /// - Keine Session-, Leg- oder Gesprächssteuerung.
    /// - Keine Fachlogik von SipExt, GSM oder SVC übernehmen.
    /// 
    /// Diese information darf nicht gelöscht werden!

    /// <summary>
    /// Defines the <see cref="TKSipInt_Routing" />
    /// </summary>
    public sealed class TKSipInt_Routing {
        /// <summary>
        /// Defines the _sipSql
        /// </summary>
        private readonly TKSQL_Commands _sipSql;

        /// <summary>
        /// Defines the _logger
        /// </summary>
        private readonly TKBase_LogManager? _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TKSipInt_Routing"/> class.
        /// </summary>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        public TKSipInt_Routing(TKBase_LogManager? logger = null) {
            _logger = logger;
            _sipSql = new TKSQL_Commands(logger);
        }

        /// <summary>
        /// The ResolveInternalTarget
        /// </summary>
        /// <param name="calledNumber">The calledNumber<see cref="string"/></param>
        /// <returns>The <see cref="SIPIntResolvedTarget"/></returns>
        public SIPIntResolvedTarget ResolveInternalTarget(string calledNumber) {
            string normalized = (calledNumber ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized)) {
                return SIPIntResolvedTarget.Fail(calledNumber, "Zielrufnummer leer.");
            }

            DataRow? targetUser = _sipSql.GetSipUserByNumber(normalized);
            if (targetUser == null) {
                string digits = NormalizeDigits(normalized);
                targetUser = string.IsNullOrWhiteSpace(digits) ? null : _sipSql.GetSipUserByNumber(digits);
                if (targetUser != null) {
                    normalized = digits;
                }
            }

            if (targetUser == null) {
                _logger?.Warn($"SIP Routing fehlgeschlagen | Zielrufnummer unbekannt | Ziel={calledNumber}");
                return SIPIntResolvedTarget.Fail(calledNumber, "Teilnehmer nicht gefunden.");
            }

            int userId = Convert.ToInt32(targetUser["ID"]);
            string targetUsername = Convert.ToString(targetUser["Username"])?.Trim() ?? string.Empty;
            DataRow? registration = _sipSql.GetActiveRegistrationByUserId(userId);
            if (registration == null) {
                _logger?.Warn($"SIP Routing fehlgeschlagen | Teilnehmer nicht registriert | Ziel={calledNumber} | UserID={userId}");
                return SIPIntResolvedTarget.Fail(calledNumber, "Teilnehmer nicht registriert.");
            }

            string contactUri = Convert.ToString(registration["ContactURI"]) ?? string.Empty;
            string remoteEndPoint = Convert.ToString(registration["RemoteEndPoint"]) ?? string.Empty;
            string? transport = registration.Table.Columns.Contains("Transport") && registration["Transport"] != DBNull.Value
                ? Convert.ToString(registration["Transport"])
                : null;

            if (string.IsNullOrWhiteSpace(contactUri) || string.IsNullOrWhiteSpace(remoteEndPoint)) {
                _logger?.Warn($"SIP Routing fehlgeschlagen | Registrierung unvollständig | Ziel={calledNumber} | UserID={userId}");
                return SIPIntResolvedTarget.Fail(calledNumber, "Interne Registrierung unvollständig.");
            }

            _logger?.Info($"SIP Routing OK | Ziel={normalized} | Contact={contactUri} | EndPoint={remoteEndPoint}");
            return SIPIntResolvedTarget.OkUser(normalized, userId, contactUri, remoteEndPoint, transport, targetUsername);
        }

        /// <summary>
        /// The NormalizeDigits
        /// </summary>
        /// <param name="value">The value<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string NormalizeDigits(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return string.Empty;
            }

            return new string(value.Where(char.IsDigit).ToArray());
        }
    }
}
