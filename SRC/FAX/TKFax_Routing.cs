/*
 * TKFax_Routing
 * Aufgabe:
 * - Enthält ausschließlich die Fax-Routing- und Fax-SDP-Erkennung des Fax-Bereichs.
 * - Ermittelt, ob Quelle/Ziel als FAXUser geführt werden müssen.
 * - Erkennt T.38-/UDPTL-Hinweise im SDP für den TKFax_CallController.
 * - Erzeugt keine Session und entscheidet keine globale Route.
 *
 * Nicht Aufgabe:
 * - Keine globale Routingkoordination über alle Bereiche.
 * - Keine Session-, Leg- oder Gesprächssteuerung.
 * - Keine Fachlogik von SipInt, SipExt, GSM oder SVC übernehmen.
 *
 * Diese information darf nicht gelöscht werden!
 */

namespace TKServer.FAX {
    using System;
    using System.Data;
    using System.Linq;

    using TKServer.BASE;
    using TKServer.SipExt;
    using TKServer.SQL;
    /// <summary>
    /// T.38 media endpoint read from one SDP leg.
    /// </summary>
    internal sealed class TKFaxT38Media {
        public bool HasT38 { get; init; }
        public string Address { get; init; } = string.Empty;
        public int Port { get; init; }
    }

    /// <summary>
    /// Defines the <see cref="TKFax_Routing" />
    /// </summary>
    internal sealed class TKFax_Routing(TKBase_LogManager? logger = null) {
        /// <summary>
        /// Defines the FaxUserType
        /// </summary>
        public const string FaxUserType = "FAXUser";

        /// <summary>
        /// Defines the FaxRouteKind
        /// </summary>
        public const string FaxRouteKind = "FaxUser";

        /// <summary>
        /// Defines the _logger
        /// </summary>
        private readonly TKBase_LogManager? _logger = logger;

        /// <summary>
        /// Defines the _sipSql
        /// </summary>
        private readonly TKSQL_Commands _sipSql = new(logger);

        /// <summary>
        /// The IsFaxUserType
        /// </summary>
        /// <param name="userType">The userType<see cref="string?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public static bool IsFaxUserType(string? userType) {
            string value = (userType ?? string.Empty).Trim();
            return string.Equals(value, FaxUserType, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, FaxRouteKind, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Fax", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The IsFaxTargetType
        /// </summary>
        /// <param name="targetType">The targetType<see cref="string?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public static bool IsFaxTargetType(string? targetType) => IsFaxUserType(targetType);


        /// <summary>
        /// The IsFaxUserRow
        /// </summary>
        /// <param name="row">The row<see cref="DataRow?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public static bool IsFaxUserRow(DataRow? row) {
            if (row == null || !row.Table.Columns.Contains("UserType") || row["UserType"] == DBNull.Value) {
                return false;
            }

            return IsFaxUserType(Convert.ToString(row["UserType"]));
        }

        /// <summary>
        /// The ResolveFaxTarget
        /// </summary>
        /// <param name="calledNumber">The calledNumber<see cref="string"/></param>
        /// <returns>The <see cref="SIPIntResolvedTarget"/></returns>
        public SIPIntResolvedTarget ResolveFaxTarget(string calledNumber) {
            string normalized = (calledNumber ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized)) {
                return SIPIntResolvedTarget.Fail(calledNumber, "Fax-Zielrufnummer leer.");
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
                SIPIntResolvedTarget didRoute = ResolveFaxDidTarget(normalized);
                if (didRoute.Success) {
                    return didRoute;
                }

                _logger?.Warn($"FAX Routing fehlgeschlagen | Zielrufnummer unbekannt | Ziel={calledNumber}");
                return SIPIntResolvedTarget.Fail(calledNumber, "Fax-Ziel nicht gefunden.");
            }

            if (!IsFaxUserRow(targetUser)) {
                _logger?.Warn($"FAX Routing fehlgeschlagen | Ziel ist kein FAXUser | Ziel={calledNumber}");
                return SIPIntResolvedTarget.Fail(calledNumber, "Ziel ist kein Fax-User.");
            }

            int userId = Convert.ToInt32(targetUser["ID"]);
            string targetUsername = ReadString(targetUser, "Username");
            string targetNumber = ReadString(targetUser, "Telefonnummer");
            DataRow? registration = _sipSql.GetActiveRegistrationByUserId(userId);
            string contactUri = registration != null ? ReadString(registration, "ContactURI") : string.Empty;
            string remoteEndPoint = registration != null ? ReadString(registration, "RemoteEndPoint") : string.Empty;
            string transport = registration != null ? ReadString(registration, "Transport") : string.Empty;
            _logger?.Info($"FAX Routing OK | Ziel={normalized} | UserID={userId} | Username={targetUsername} | Nummer={targetNumber} | Registriert={!string.IsNullOrWhiteSpace(contactUri)}");
            return SIPIntResolvedTarget.OkFaxUser(normalized, userId, targetUsername, targetNumber, contactUri, remoteEndPoint, transport);
        }

        /// <summary>
        /// Löst eine Fax-DID auf den zugeordneten FaxUser auf.
        /// </summary>
        /// <param name="calledNumber">The calledNumber<see cref="string"/></param>
        /// <returns>The <see cref="SIPIntResolvedTarget"/></returns>
        private SIPIntResolvedTarget ResolveFaxDidTarget(string calledNumber) {
            string normalizedCalled = TKSipExt_Routing.NormalizeExternalNumber(calledNumber);
            if (string.IsNullOrWhiteSpace(normalizedCalled)) {
                return SIPIntResolvedTarget.Fail(calledNumber, "Keine Fax-DID.");
            }

            foreach (string candidate in BuildInboundNumberCandidates(normalizedCalled)) {
                DataRow? externalNumber = _sipSql.GetExternalNumberByValue(candidate);
                if (externalNumber == null) continue;

                bool isActive = !externalNumber.Table.Columns.Contains("Active")
                    || externalNumber["Active"] == DBNull.Value
                    || Convert.ToBoolean(externalNumber["Active"]);
                if (!isActive) {
                    _logger?.Warn($"FAX DID Routing fehlgeschlagen | DID inaktiv | An={candidate}");
                    continue;
                }

                foreach (DataRow route in _sipSql.GetInboundExternalRoutesByCalledNumber(candidate)) {
                    string targetType = ReadString(route, "TargetType");
                    string targetUserType = ReadString(route, "TargetUserType");
                    if (!IsFaxTargetType(targetType) && !IsFaxUserType(targetUserType)) {
                        continue;
                    }

                    int userId = ReadInt(route, "TargetUserID");
                    if (userId <= 0) userId = ReadInt(route, "InternalNumberID");
                    if (userId <= 0) continue;

                    bool targetUserActive = !route.Table.Columns.Contains("TargetUserActive")
                        || route["TargetUserActive"] == DBNull.Value
                        || Convert.ToBoolean(route["TargetUserActive"]);
                    bool canReceiveExternal = !route.Table.Columns.Contains("CanReceiveExternal")
                        || route["CanReceiveExternal"] == DBNull.Value
                        || Convert.ToBoolean(route["CanReceiveExternal"]);
                    if (!targetUserActive || !canReceiveExternal) {
                        _logger?.Info($"FAX DID Routing Kandidat übersprungen | DID={candidate} | UserID={userId} | Grund=UserInaktivOderKeinReceiveExternal");
                        continue;
                    }

                    string targetNumber = ReadString(route, "TargetTelefonnummer");
                    if (string.IsNullOrWhiteSpace(targetNumber)) targetNumber = candidate;
                    string targetUsername = ReadString(route, "TargetUsername");
                    string contactUri = ReadString(route, "ContactURI");
                    string remoteEndPoint = ReadString(route, "RemoteEndPoint");
                    string transport = ReadString(route, "Transport");

                    _logger?.Info($"FAX DID Routing OK | DID={candidate} | Eingang={normalizedCalled} | UserID={userId} | Username={targetUsername} | Ziel={targetNumber} | Registriert={!string.IsNullOrWhiteSpace(contactUri)}");
                    return SIPIntResolvedTarget.OkFaxUser(targetNumber, userId, targetUsername, targetNumber, contactUri, remoteEndPoint, transport);
                }
            }

            return SIPIntResolvedTarget.Fail(calledNumber, "Fax-DID nicht gefunden.");
        }

        /// <summary>
        /// The IsFaxRoute
        /// </summary>
        /// <param name="route">The route<see cref="SIPIntResolvedTarget?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public static bool IsFaxRoute(SIPIntResolvedTarget? route) => route != null
            && route.Success
            && string.Equals(route.TargetKind.ToString(), "FaxUser", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// The ContainsT38Sdp
        /// </summary>
        /// <param name="sdp">The sdp<see cref="string?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public static bool ContainsT38Sdp(string? sdp) {
            if (string.IsNullOrWhiteSpace(sdp)) return false;

            return sdp.Contains("m=image", StringComparison.OrdinalIgnoreCase)
                || sdp.Contains("udptl", StringComparison.OrdinalIgnoreCase)
                || sdp.Contains("T38FaxVersion", StringComparison.OrdinalIgnoreCase)
                || sdp.Contains("T38FaxMaxBuffer", StringComparison.OrdinalIgnoreCase)
                || sdp.Contains("T38FaxMaxDatagram", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The TryParseT38Media
        /// </summary>
        /// <param name="sdp">The sdp<see cref="string?"/></param>
        /// <param name="media">The media<see cref="TKFaxT38Media?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public bool TryParseT38Media(string? sdp, out TKFaxT38Media? media) {
            try {
                return TryReadT38Media(sdp, out media);
            }
            catch (Exception ex) {
                media = null;
                _logger?.Warn($"FAX T.38 SDP konnte nicht gelesen werden | Fehler={ex.Message}");
                return false;
            }
        }

        internal static bool TryReadT38Media(string? sdp, out TKFaxT38Media? media) {
            media = null;
            if (string.IsNullOrWhiteSpace(sdp)) return false;

            string? sessionAddress = null;
            string? imageAddress = null;
            int imagePort = 0;
            bool inImageSection = false;
            bool hasT38Attribute = false;

            string[] lines = sdp.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
            foreach (string rawLine in lines) {
                string line = rawLine.Trim();
                if (line.StartsWith("m=", StringComparison.OrdinalIgnoreCase)) {
                    string[] parts = line[2..].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    inImageSection = parts.Length >= 2 && string.Equals(parts[0], "image", StringComparison.OrdinalIgnoreCase);
                    if (inImageSection) {
                        imagePort = int.TryParse(parts[1], out int parsedImagePort) ? parsedImagePort : 0;
                    }
                    continue;
                }

                if (line.StartsWith("c=", StringComparison.OrdinalIgnoreCase)) {
                    string? connectionAddress = ReadConnectionAddress(line);
                    if (inImageSection) imageAddress = connectionAddress;
                    else sessionAddress ??= connectionAddress;
                    continue;
                }

                if (line.StartsWith("a=T38Fax", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("udptl", StringComparison.OrdinalIgnoreCase)) {
                    hasT38Attribute = true;
                }
            }

            string mediaAddress = imageAddress ?? sessionAddress ?? string.Empty;
            bool hasT38 = imagePort > 0 && (hasT38Attribute || sdp.Contains("m=image", StringComparison.OrdinalIgnoreCase));
            media = new TKFaxT38Media { HasT38 = hasT38, Address = mediaAddress, Port = imagePort };
            return hasT38 && !string.IsNullOrWhiteSpace(mediaAddress);
        }

        private static string? ReadConnectionAddress(string line) {
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length >= 3 ? parts[^1] : null;
        }

        /// <summary>
        /// The DetermineFaxMode
        /// </summary>
        /// <param name="sdp">The sdp<see cref="string?"/></param>
        /// <returns>The <see cref="TKFaxMediaMode"/></returns>
        public static TKFaxMediaMode DetermineFaxMode(string? sdp) => TryReadT38Media(sdp, out _) ? TKFaxMediaMode.T38 : TKFaxMediaMode.AudioPassthrough;

        /// <summary>
        /// The LogFaxRouteDetected
        /// </summary>
        /// <param name="sessionId">The sessionId<see cref="string"/></param>
        /// <param name="source">The source<see cref="string"/></param>
        /// <param name="target">The target<see cref="string"/></param>
        public void LogFaxRouteDetected(string sessionId, string source, string target) {
            _logger?.Debug($"FAX Route erkannt | SessionID={sessionId} | Von={source} | An={target}");
        }

        /// <summary>
        /// Liest einen Integerwert aus einer DataRow.
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="columnName">The columnName<see cref="string"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int ReadInt(DataRow row, string columnName) {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value) {
                return 0;
            }

            return int.TryParse(Convert.ToString(row[columnName]), out int value) ? value : 0;
        }

        /// <summary>
        /// Bildet DID-Varianten für +49/49/0-Formate.
        /// </summary>
        /// <param name="normalizedCalled">The normalizedCalled<see cref="string"/></param>
        /// <returns>The <see cref="System.Collections.Generic.IEnumerable{string}"/></returns>
        private static System.Collections.Generic.IEnumerable<string> BuildInboundNumberCandidates(string normalizedCalled) {
            string value = (normalizedCalled ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value)) yield break;

            System.Collections.Generic.HashSet<string> emitted = new(StringComparer.OrdinalIgnoreCase);
            System.Collections.Generic.List<string> candidates = [];
            void Add(string candidate) {
                candidate = (candidate ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(candidate) && emitted.Add(candidate)) candidates.Add(candidate);
            }

            Add(value);
            string digits = NormalizeDigits(value);
            Add(digits);
            if (value.StartsWith("+49", StringComparison.Ordinal) && value.Length > 3) Add(string.Concat("0", value.AsSpan(3)));
            if (digits.StartsWith("49", StringComparison.Ordinal) && digits.Length > 2) Add(string.Concat("0", digits.AsSpan(2)));
            if (digits.StartsWith('0') && digits.Length > 1) {
                Add(string.Concat("+49", digits.AsSpan(1)));
                Add(string.Concat("49", digits.AsSpan(1)));
            }

            foreach (string candidate in candidates) yield return candidate;
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
        /// The NormalizeDigits
        /// </summary>
        /// <param name="value">The value<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string NormalizeDigits(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return string.Empty;
            }

            return string.Concat(value.Where(char.IsDigit));
        }

    }
}
