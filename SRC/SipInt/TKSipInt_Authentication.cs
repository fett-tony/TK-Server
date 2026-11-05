/*
 * TKSipInt_Authentication
 * Aufgabe:
 * - Prüft interne SIP-Benutzeranmeldung.
 * - Validiert Benutzername/Passwort.
 * - Validiert SIP-Digest für interne SIP-Authentifizierung.
 * - Liefert dafür die notwendigen internen Benutzerdaten.
 *
 * Nicht Aufgabe:
 * - Kein Routing.
 * - Keine Session-Steuerung.
 * - Keine Call-Steuerung.
 * - Keine Media-Steuerung.
 *
 * Diese Datei ist die Authentifizierungsschicht für interne SIP-Benutzer.
 */

namespace TKServer.SipInt {
    using sipcryptlib;
    using SIPSorcery.SIP;
    using System;
    using System.Data;

    using TKServer.BASE;
    using TKServer.SQL;
    /// TKSipInt_Authentication
    /// Aufgabe:
    /// - Prüft interne SIP-Benutzeranmeldung.
    /// - Validiert Benutzername/Passwort.
    /// - Validiert SIP-Digest für interne SIP-Authentifizierung.
    /// - Liefert dafür die notwendigen internen Benutzerdaten.
    /// 
    /// Nicht Aufgabe:
    /// - Kein Routing.
    /// - Keine Session-Steuerung.
    /// - Keine Call-Steuerung.
    /// - Keine Media-Steuerung.
    /// 
    /// Diese Datei ist die Authentifizierungsschicht für interne SIP-Benutzer.

    /// <summary>
    /// Defines the <see cref="TKSipInt_Authentication" />
    /// </summary>
    public sealed class TKSipInt_Authentication {
        /// <summary>
        /// Defines the _sipSql
        /// </summary>
        private readonly TKSQL_Commands _sipSql;

        /// <summary>
        /// Defines the _logger
        /// </summary>
        private readonly TKBase_LogManager? _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TKSipInt_Authentication"/> class.
        /// </summary>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        public TKSipInt_Authentication(TKBase_LogManager? logger = null) {
            _logger = logger;
            _sipSql = new TKSQL_Commands(logger);
        }

        /// <summary>
        /// The ValidateUser
        /// </summary>
        /// <param name="username">The username<see cref="string"/></param>
        /// <param name="password">The password<see cref="string"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public bool ValidateUser(string username, string password) {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)) {
                _logger?.Warn("SIP Auth fehlgeschlagen | Benutzername oder Passwort leer.");
                return false;
            }

            DataRow? user = _sipSql.GetSipUserByUsername(username);
            if (user == null) {
                _logger?.Warn($"SIP Auth fehlgeschlagen | Benutzer nicht gefunden | Username={username}");
                return false;
            }

            string encryptedPassword = Convert.ToString(user["Password"]) ?? string.Empty;
            string dbPassword = DecryptPassword(encryptedPassword, username);

            if (string.IsNullOrWhiteSpace(dbPassword)) {
                _logger?.Warn($"SIP Auth fehlgeschlagen | Entschlüsseltes Passwort leer | Username={username}");
                return false;
            }

            bool ok = string.Equals(dbPassword, password, StringComparison.Ordinal);

            if (ok) {
                _logger?.Info($"SIP Auth erfolgreich | Username={username}");
            } else {
                _logger?.Warn($"SIP Auth fehlgeschlagen | Passwort ungültig | Username={username}");
            }

            return ok;
        }

        /// <summary>
        /// The ValidateDigest
        /// </summary>
        /// <param name="username">The username<see cref="string"/></param>
        /// <param name="digest">The digest<see cref="SIPAuthorisationDigest?"/></param>
        /// <param name="method">The method<see cref="string"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public bool ValidateDigest(string username, SIPAuthorisationDigest? digest, string method) {
            if (string.IsNullOrWhiteSpace(username) || digest == null) {
                _logger?.Warn("SIP Digest Auth fehlgeschlagen | Benutzername oder Digest leer.");
                return false;
            }

            DataRow? user = _sipSql.GetSipUserByUsername(username);
            if (user == null) {
                _logger?.Warn($"SIP Digest Auth fehlgeschlagen | Benutzer nicht gefunden | Username={username}");
                return false;
            }

            string encryptedPassword = Convert.ToString(user["Password"]) ?? string.Empty;
            string dbPassword = DecryptPassword(encryptedPassword, username);

            if (string.IsNullOrWhiteSpace(dbPassword)) {
                _logger?.Warn($"SIP Digest Auth fehlgeschlagen | Entschlüsseltes Passwort leer | Username={username}");
                return false;
            }

            SIPAuthorisationDigest expected = digest.CopyOf();
            string uri = !string.IsNullOrWhiteSpace(digest.URI) ? digest.URI : string.Empty;
            expected.SetCredentials(username, dbPassword, uri, method);

            bool ok = string.Equals(expected.DigestAlgorithm.ToString(), digest.DigestAlgorithm.ToString(), StringComparison.OrdinalIgnoreCase)
                && string.Equals(expected.GetDigest(), digest.Response, StringComparison.OrdinalIgnoreCase);

            if (ok) {
                _logger?.Info($"SIP Digest Auth erfolgreich | Username={username}");
            } else {
                _logger?.Warn($"SIP Digest Auth fehlgeschlagen | Digest ungültig | Username={username}");
            }

            return ok;
        }

        /// <summary>
        /// The GetUser
        /// </summary>
        /// <param name="username">The username<see cref="string"/></param>
        /// <returns>The <see cref="DataRow?"/></returns>
        public DataRow? GetUser(string username) {
            if (string.IsNullOrWhiteSpace(username)) {
                return null;
            }

            return _sipSql.GetSipUserByUsername(username);
        }

        /// <summary>
        /// The GetUserId
        /// </summary>
        /// <param name="username">The username<see cref="string"/></param>
        /// <returns>The <see cref="int?"/></returns>
        public int? GetUserId(string username) {
            DataRow? user = _sipSql.GetSipUserByUsername(username);
            return user == null ? null : Convert.ToInt32(user["ID"]);
        }

        /// <summary>
        /// The GetNumber
        /// </summary>
        /// <param name="username">The username<see cref="string"/></param>
        /// <returns>The <see cref="string?"/></returns>
        public string? GetNumber(string username) {
            DataRow? user = _sipSql.GetSipUserByUsername(username);
            return user == null ? null : Convert.ToString(user["Telefonnummer"]);
        }

        /// <summary>
        /// The DecryptPassword
        /// </summary>
        /// <param name="encryptedPassword">The encryptedPassword<see cref="string"/></param>
        /// <param name="username">The username<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private string DecryptPassword(string encryptedPassword, string username) {
            if (string.IsNullOrWhiteSpace(encryptedPassword)) {
                return string.Empty;
            }

            try {
                return SipCryptor.DecryptFixed(encryptedPassword);
            } catch (Exception ex) {
                _logger?.Error($"SIP Auth | Passwort konnte nicht entschlüsselt werden | Username={username} | Fehler={ex.Message}");
                return string.Empty;
            }
        }
    }
}
