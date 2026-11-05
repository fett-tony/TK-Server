/*
 * TKSipInt_Registra
 * Aufgabe:
 * - Zuständig für die interne SIP-Registrierungslogik lokaler Benutzer.
 * - Prüft Benutzer und interne REGISTER-Berechtigung.
 * - Bleibt vollständig im Bereich SipInt und arbeitet nur für interne REGISTER-Flüsse.
 *
 * Nicht Aufgabe:
 * - Keine Persistenz in RuntimeRegistration/InternalRegistrationState; das macht TKBase_RegistrationManager.
 * - Keine externe Provider-Registrierung.
 * - Keine GSM-Registrierung.
 * - Keine Session-, Leg- oder allgemeine Call-Steuerung.
 * 
 * Diese information darf nicht gelöscht werden!
 */

namespace TKServer.SipInt {
    using System.Data;

    using TKServer.BASE;
    using TKServer.SQL;
    /// TKSipInt_Registra
    /// Aufgabe:
    /// - Zuständig für die interne SIP-Registrierungslogik lokaler Benutzer.
    /// - Prüft Benutzer und interne REGISTER-Berechtigung.
    /// - Bleibt vollständig im Bereich SipInt und arbeitet nur für interne REGISTER-Flüsse.
    /// 
    /// Nicht Aufgabe:
    /// - Keine Persistenz in RuntimeRegistration/InternalRegistrationState; das macht TKBase_RegistrationManager.
    /// - Keine externe Provider-Registrierung.
    /// - Keine GSM-Registrierung.
    /// - Keine Session-, Leg- oder allgemeine Call-Steuerung.
    /// 
    /// Diese information darf nicht gelöscht werden!

    /// <summary>
    /// Defines the <see cref="TKSipInt_Registration" />
    /// </summary>
    public sealed class TKSipInt_Registration {
        /// <summary>
        /// Defines the _sipSql
        /// </summary>
        private readonly TKSQL_Commands _sipSql;

        /// <summary>
        /// Defines the _authentication
        /// </summary>
        private readonly TKSipInt_Authentication _authentication;

        /// <summary>
        /// Defines the _logger
        /// </summary>
        private readonly TKBase_LogManager? _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TKSipInt_Registration"/> class.
        /// </summary>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        public TKSipInt_Registration(TKBase_LogManager? logger = null) {
            _logger = logger;
            _sipSql = new TKSQL_Commands(logger);
            _authentication = new TKSipInt_Authentication(logger);
        }

        /// <summary>
        /// The Register
        /// </summary>
        /// <param name="username">The username<see cref="string"/></param>
        /// <param name="password">The password<see cref="string"/></param>
        /// <param name="contactUri">The contactUri<see cref="string"/></param>
        /// <param name="remoteEndPoint">The remoteEndPoint<see cref="string"/></param>
        /// <param name="transport">The transport<see cref="string?"/></param>
        /// <param name="callId">The callId<see cref="string"/></param>
        /// <param name="userAgent">The userAgent<see cref="string?"/></param>
        /// <param name="expiresSeconds">The expiresSeconds<see cref="int"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public bool Register(
            string username,
            string password,
            string contactUri,
            string remoteEndPoint,
            string? transport,
            string callId,
            string? userAgent,
            int expiresSeconds) {

            if (!_authentication.ValidateUser(username, password)) {
                _logger?.Warn($"SIP Registrierung abgewiesen | Username={username} | Grund=Passwort falsch");
                return false;
            }

            return RegisterTrusted(username, contactUri, remoteEndPoint, transport, callId, userAgent, expiresSeconds);
        }

        /// <summary>
        /// The RegisterTrusted
        /// </summary>
        /// <param name="username">The username<see cref="string"/></param>
        /// <param name="contactUri">The contactUri<see cref="string"/></param>
        /// <param name="remoteEndPoint">The remoteEndPoint<see cref="string"/></param>
        /// <param name="transport">The transport<see cref="string?"/></param>
        /// <param name="callId">The callId<see cref="string"/></param>
        /// <param name="userAgent">The userAgent<see cref="string?"/></param>
        /// <param name="expiresSeconds">The expiresSeconds<see cref="int"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public bool RegisterTrusted(
            string username,
            string contactUri,
            string remoteEndPoint,
            string? transport,
            string callId,
            string? userAgent,
            int expiresSeconds) {

            int? userId = _authentication.GetUserId(username);
            if (!userId.HasValue) {
                _logger?.Warn($"SIP Registrierung fehlgeschlagen | UserID nicht gefunden | Username={username}");
                return false;
            }

            if (string.IsNullOrWhiteSpace(contactUri) || string.IsNullOrWhiteSpace(remoteEndPoint)) {
                _logger?.Warn($"SIP Registrierung fehlgeschlagen | Kontakt unvollständig | Username={username} | Contact={contactUri} | Remote={remoteEndPoint}");
                return false;
            }

            _logger?.Info($"SIP Registrierung geprüft | Username={username} | UserID={userId.Value} | Contact={contactUri} | Expires={expiresSeconds}");
            return true;
        }

        /// <summary>
        /// The GetRegistrationByNumber
        /// </summary>
        /// <param name="number">The number<see cref="string"/></param>
        /// <returns>The <see cref="DataRow?"/></returns>
        public DataRow? GetRegistrationByNumber(string number) {
            return _sipSql.GetActiveRegistrationByNumber(number);
        }

        /// <summary>
        /// The HandleRegister
        /// </summary>
        /// <param name="username">The username<see cref="string"/></param>
        /// <param name="password">The password<see cref="string"/></param>
        /// <param name="contactUri">The contactUri<see cref="string"/></param>
        /// <param name="remoteEndPoint">The remoteEndPoint<see cref="string"/></param>
        /// <param name="transport">The transport<see cref="string?"/></param>
        /// <param name="callId">The callId<see cref="string"/></param>
        /// <param name="userAgent">The userAgent<see cref="string?"/></param>
        /// <param name="expiresSeconds">The expiresSeconds<see cref="int"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public bool HandleRegister(
            string username,
            string password,
            string contactUri,
            string remoteEndPoint,
            string? transport,
            string callId,
            string? userAgent,
            int expiresSeconds) {

            _logger?.Info($"SIPInt REGISTER empfangen | Username={username} | Contact={contactUri}");
            return Register(username, password, contactUri, remoteEndPoint, transport, callId, userAgent, expiresSeconds);
        }

        /// <summary>
        /// The HandleRegisterAuthenticated
        /// </summary>
        /// <param name="username">The username<see cref="string"/></param>
        /// <param name="contactUri">The contactUri<see cref="string"/></param>
        /// <param name="remoteEndPoint">The remoteEndPoint<see cref="string"/></param>
        /// <param name="transport">The transport<see cref="string?"/></param>
        /// <param name="callId">The callId<see cref="string"/></param>
        /// <param name="userAgent">The userAgent<see cref="string?"/></param>
        /// <param name="expiresSeconds">The expiresSeconds<see cref="int"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public bool HandleRegisterAuthenticated(
            string username,
            string contactUri,
            string remoteEndPoint,
            string? transport,
            string callId,
            string? userAgent,
            int expiresSeconds) {

            _logger?.Info($"SIPInt REGISTER authentifiziert | Username={username} | Contact={contactUri}");
            return RegisterTrusted(username, contactUri, remoteEndPoint, transport, callId, userAgent, expiresSeconds);
        }
    }
}
