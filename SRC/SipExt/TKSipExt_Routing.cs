/*
 * TKSipExt_Routing
 * Aufgabe:
 * - EnthÃ¤lt ausschlieÃŸlich die Routinglogik des externen SIP-Bereichs.
 * - Ermittelt provider- und externbezogene Ziele, Rufnummernwege und Zielparameter.
 *
 * Nicht Aufgabe:
 * - Keine globale Routingkoordination Ã¼ber alle Bereiche.
 * - Keine Session-, Leg- oder GesprÃ¤chssteuerung.
 * - Keine Fachlogik von SipInt, GSM oder SVC Ã¼bernehmen.
 * 
 * Diese information darf nicht gelÃ¶scht werden!
 */

namespace TKServer.SipExt {
    using sipcryptlib;
    using SIPSorcery.SIP;
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;

    using TKServer.BASE;
    using TKServer.SQL;
    /// TKSipExt_Routing
    /// Aufgabe:
    /// - EnthÃ¤lt ausschlieÃŸlich die Routinglogik des externen SIP-Bereichs.
    /// - Ermittelt provider- und externbezogene Ziele, Rufnummernwege und Zielparameter.
    /// 
    /// Nicht Aufgabe:
    /// - Keine globale Routingkoordination Ã¼ber alle Bereiche.
    /// - Keine Session-, Leg- oder GesprÃ¤chssteuerung.
    /// - Keine Fachlogik von SipInt, GSM oder SVC Ã¼bernehmen.
    /// 
    /// Diese information darf nicht gelÃ¶scht werden!

    /// <summary>
    /// Defines the <see cref="TKSipExt_Routing" />
    /// </summary>
    internal sealed class TKSipExt_Routing {
        /// <summary>
        /// Defines the _sipSql
        /// </summary>
        private readonly TKSQL_Commands _sipSql;

        /// <summary>
        /// Defines the _logger
        /// </summary>
        private readonly TKBase_LogManager? _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TKSipExt_Routing"/> class.
        /// </summary>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        public TKSipExt_Routing(TKBase_LogManager? logger = null) {
            _logger = logger;
            _sipSql = new TKSQL_Commands(logger);
        }

        /// <summary>
        /// The ResolveInboundTargets
        /// </summary>
        /// <param name="calledNumber">The calledNumber<see cref="string"/></param>
        /// <returns>The <see cref="List{SIPIntResolvedTarget}"/></returns>
        public List<SIPIntResolvedTarget> ResolveInboundTargets(string calledNumber) {
            string normalizedCalled = NormalizeExternalNumber(calledNumber);
            List<SIPIntResolvedTarget> results = new();
            if (string.IsNullOrWhiteSpace(normalizedCalled)) {
                return results;
            }

            string matchedCalled = normalizedCalled;
            DataRow? externalNumber = null;
            foreach (string candidate in BuildInboundNumberCandidates(normalizedCalled)) {
                externalNumber = _sipSql.GetExternalNumberByValue(candidate);
                if (externalNumber != null) {
                    matchedCalled = candidate;
                    break;
                }
            }

            if (externalNumber == null) {
                _logger?.Warn($"Externes Inbound Routing fehlgeschlagen | DID unbekannt | An={normalizedCalled}");
                return results;
            }

            bool isActive = externalNumber.Table.Columns.Contains("Active")
                && externalNumber["Active"] != DBNull.Value
                && Convert.ToBoolean(externalNumber["Active"]);
            if (!isActive) {
                _logger?.Warn($"Externes Inbound Routing fehlgeschlagen | DID inaktiv | An={normalizedCalled}");
                return results;
            }

            List<DataRow> routes = _sipSql.GetInboundExternalRoutesByCalledNumber(matchedCalled);
            if (routes.Count == 0) {
                _logger?.Warn($"Externes Inbound Routing fehlgeschlagen | Keine Inbound-Route | An={normalizedCalled}");
                return results;
            }

            HashSet<int> addedUserIds = new();
            foreach (DataRow route in routes) {
                int userId = route.Table.Columns.Contains("TargetUserID") && route["TargetUserID"] != DBNull.Value
                    ? Convert.ToInt32(route["TargetUserID"])
                    : (route.Table.Columns.Contains("InternalNumberID") && route["InternalNumberID"] != DBNull.Value ? Convert.ToInt32(route["InternalNumberID"]) : 0);
                if (userId <= 0) {
                    continue;
                }

                if (addedUserIds.Contains(userId)) {
                    continue;
                }

                bool targetUserActive = route.Table.Columns.Contains("TargetUserActive") && route["TargetUserActive"] != DBNull.Value && Convert.ToBoolean(route["TargetUserActive"]);
                bool canReceiveExternal = route.Table.Columns.Contains("CanReceiveExternal") && route["CanReceiveExternal"] != DBNull.Value && Convert.ToBoolean(route["CanReceiveExternal"]);
                if (!targetUserActive || !canReceiveExternal) {
                    _logger?.Info($"Externes Inbound Routing Kandidat Ã¼bersprungen | DID={matchedCalled} | UserID={userId} | Grund=UserInaktivOderKeinReceiveExternal");
                    continue;
                }

                string contactUri = route.Table.Columns.Contains("ContactURI") && route["ContactURI"] != DBNull.Value
                    ? Convert.ToString(route["ContactURI"]) ?? string.Empty
                    : string.Empty;
                string remoteEndPoint = route.Table.Columns.Contains("RemoteEndPoint") && route["RemoteEndPoint"] != DBNull.Value
                    ? Convert.ToString(route["RemoteEndPoint"]) ?? string.Empty
                    : string.Empty;
                string? transport = route.Table.Columns.Contains("Transport") && route["Transport"] != DBNull.Value
                    ? Convert.ToString(route["Transport"])
                    : null;
                string targetNumber = route.Table.Columns.Contains("TargetTelefonnummer") && route["TargetTelefonnummer"] != DBNull.Value
                    ? Convert.ToString(route["TargetTelefonnummer"]) ?? normalizedCalled
                    : normalizedCalled;
                string targetUsername = route.Table.Columns.Contains("TargetUsername") && route["TargetUsername"] != DBNull.Value
                    ? Convert.ToString(route["TargetUsername"]) ?? string.Empty
                    : string.Empty;
                if (string.IsNullOrWhiteSpace(contactUri) || string.IsNullOrWhiteSpace(remoteEndPoint)) {
                    _logger?.Info($"Externes Inbound Routing Kandidat offline | DID={matchedCalled} | UserID={userId} | Username={targetUsername}");
                    continue;
                }

                _logger?.Info($"Externes Inbound Routing Kandidat OK | DID={matchedCalled} | UserID={userId} | Username={targetUsername} | Contact={contactUri} | EndPoint={remoteEndPoint}");
                results.Add(SIPIntResolvedTarget.OkUser(targetNumber, userId, contactUri, remoteEndPoint, transport, targetUsername));
                addedUserIds.Add(userId);
            }

            return results;
        }

        /// <summary>
        /// Prueft, ob die gerufene Nummer als aktive externe DID mit Inbound-Route bekannt ist.
        /// </summary>
        /// <param name="calledNumber">The calledNumber<see cref="string"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public bool HasActiveInboundRoute(string calledNumber) {
            string normalizedCalled = NormalizeExternalNumber(calledNumber);
            if (string.IsNullOrWhiteSpace(normalizedCalled)) {
                return false;
            }

            foreach (string candidate in BuildInboundNumberCandidates(normalizedCalled)) {
                DataRow? externalNumber = _sipSql.GetExternalNumberByValue(candidate);
                if (externalNumber == null) {
                    continue;
                }

                bool isActive = externalNumber.Table.Columns.Contains("Active")
                    && externalNumber["Active"] != DBNull.Value
                    && Convert.ToBoolean(externalNumber["Active"]);
                if (!isActive) {
                    continue;
                }

                if (_sipSql.GetInboundExternalRoutesByCalledNumber(candidate).Count > 0) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The ResolveInboundTarget
        /// </summary>
        /// <param name="calledNumber">The calledNumber<see cref="string"/></param>
        /// <returns>The <see cref="SIPIntResolvedTarget"/></returns>
        public SIPIntResolvedTarget ResolveInboundTarget(string calledNumber) {
            string normalizedCalled = NormalizeExternalNumber(calledNumber);
            if (string.IsNullOrWhiteSpace(normalizedCalled)) {
                return SIPIntResolvedTarget.Fail(calledNumber, "Keine eingehende Zielrufnummer.");
            }

            string matchedCalled = normalizedCalled;
            DataRow? externalNumber = null;
            foreach (string candidate in BuildInboundNumberCandidates(normalizedCalled)) {
                externalNumber = _sipSql.GetExternalNumberByValue(candidate);
                if (externalNumber != null) {
                    matchedCalled = candidate;
                    break;
                }
            }

            if (externalNumber == null) {
                _logger?.Warn($"Externes Inbound Routing fehlgeschlagen | DID unbekannt | An={normalizedCalled}");
                return SIPIntResolvedTarget.Fail(normalizedCalled, "Zielrufnummer unbekannt.");
            }

            bool isActive = externalNumber.Table.Columns.Contains("Active")
                && externalNumber["Active"] != DBNull.Value
                && Convert.ToBoolean(externalNumber["Active"]);
            if (!isActive) {
                _logger?.Warn($"Externes Inbound Routing fehlgeschlagen | DID inaktiv | An={normalizedCalled}");
                return SIPIntResolvedTarget.Fail(normalizedCalled, "Zielrufnummer inaktiv.");
            }

            List<SIPIntResolvedTarget> targets = ResolveInboundTargets(matchedCalled);
            SIPIntResolvedTarget? firstOnline = targets.FirstOrDefault();
            if (firstOnline != null) {
                return firstOnline;
            }

            List<DataRow> routes = _sipSql.GetInboundExternalRoutesByCalledNumber(matchedCalled);
            if (routes.Count == 0) {
                _logger?.Warn($"Externes Inbound Routing fehlgeschlagen | Keine Inbound-Route | An={normalizedCalled}");
                return SIPIntResolvedTarget.Fail(normalizedCalled, "Keine Inbound-Route gefunden.");
            }

            _logger?.Warn($"Externes Inbound Routing fehlgeschlagen | Kein online registriertes internes Ziel | An={normalizedCalled}");
            return SIPIntResolvedTarget.Fail(normalizedCalled, "Kein online registriertes internes Ziel.");
        }



        /// <summary>
        /// The BuildInboundNumberCandidates
        /// </summary>
        /// <param name="normalizedCalled">The normalizedCalled<see cref="string"/></param>
        /// <returns>The <see cref="IEnumerable{string}"/></returns>
        private static IEnumerable<string> BuildInboundNumberCandidates(string normalizedCalled) {
            string value = (normalizedCalled ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value)) yield break;

            HashSet<string> emitted = new(StringComparer.OrdinalIgnoreCase);
            List<string> candidates = new();
            void Add(string candidate) {
                candidate = (candidate ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(candidate) && emitted.Add(candidate)) candidates.Add(candidate);
            }

            Add(value);
            string digits = new string(value.Where(char.IsDigit).ToArray());
            Add(digits);
            if (value.StartsWith("+49", StringComparison.Ordinal) && value.Length > 3) Add("0" + value.Substring(3));
            if (digits.StartsWith("49", StringComparison.Ordinal) && digits.Length > 2) Add("0" + digits.Substring(2));
            if (digits.StartsWith("0", StringComparison.Ordinal) && digits.Length > 1) {
                Add("+49" + digits.Substring(1));
                Add("49" + digits.Substring(1));
            }

            foreach (string candidate in candidates) yield return candidate;
        }

        /// <summary>
        /// The NormalizeExternalNumber
        /// </summary>
        /// <param name="calledNumber">The calledNumber<see cref="string?"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string NormalizeExternalNumber(string? calledNumber) {
            if (string.IsNullOrWhiteSpace(calledNumber)) {
                return string.Empty;
            }

            string value = calledNumber.Trim();
            try { value = Uri.UnescapeDataString(value); } catch { }

            value = value.Replace(" ", string.Empty).Replace("-", string.Empty).Replace("(", string.Empty).Replace(")", string.Empty).Replace("/", string.Empty);
            if (value.StartsWith("00", StringComparison.Ordinal)) {
                value = "+" + value.Substring(2);
            }

            int start = value.StartsWith("+", StringComparison.Ordinal) ? 1 : 0;
            if (value.Length <= start) {
                return string.Empty;
            }

            for (int i = start; i < value.Length; i++) {
                if (!char.IsDigit(value[i])) {
                    return string.Empty;
                }
            }

            if (value.StartsWith("#", StringComparison.Ordinal)) {
                return string.Empty;
            }

            return value.Length >= 3 ? value : string.Empty;
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

            string normalized = NormalizeExternalNumber(fromNumber);
            if (!string.IsNullOrWhiteSpace(normalized)) {
                caller = _sipSql.GetSipUserByNumber(normalized);
                if (caller != null) {
                    return caller;
                }
            }

            return null;
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
            string externeNr = FirstNotEmpty(Convert.ToString(route["ExterneNr"]), Convert.ToString(route["NumberValue"]));
            string sourceType = (Convert.ToString(route["SourceType"]) ?? string.Empty).Trim();
            int sourceId = route["SourceID"] == DBNull.Value ? 0 : Convert.ToInt32(route["SourceID"]);
            bool routeActive = route["Active"] != DBNull.Value && Convert.ToBoolean(route["Active"]);
            bool externalActive = route.Table.Columns.Contains("ExternalNumberActive") && route["ExternalNumberActive"] != DBNull.Value && Convert.ToBoolean(route["ExternalNumberActive"]);
            bool isOnline = route.Table.Columns.Contains("IsOnline") && route["IsOnline"] != DBNull.Value && Convert.ToBoolean(route["IsOnline"]);
            bool isSelectable = route.Table.Columns.Contains("IsSelectable") && route["IsSelectable"] != DBNull.Value && Convert.ToBoolean(route["IsSelectable"]);

            if (!routeActive || !externalActive) {
                _logger?.Warn($"Externes Routing Kandidat Ã¼bersprungen | RouteID={routingId} | Priority={priority} | Grund=Route/Inaktiv");
                return null;
            }

            if (!string.IsNullOrWhiteSpace(routeTargetType) &&
                !string.Equals(routeTargetType, callerType, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(routeTargetType, "Alle", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(routeTargetType, "Any", StringComparison.OrdinalIgnoreCase)) {
                _logger?.Warn($"Externes Routing Kandidat Ã¼bersprungen | RouteID={routingId} | Priority={priority} | Grund=TargetTypeMismatch | RouteType={routeTargetType} | CallerType={callerType}");
                return null;
            }

            if (!string.Equals(sourceType, "ProviderAccount", StringComparison.OrdinalIgnoreCase) || sourceId <= 0) {
                _logger?.Warn($"Externes Routing Kandidat Ã¼bersprungen | RouteID={routingId} | Priority={priority} | Grund=KeinProviderAccount | SourceType={sourceType} | SourceID={sourceId}");
                return null;
            }

            if (!isSelectable) {
                _logger?.Warn($"Externes Routing Kandidat Ã¼bersprungen | RouteID={routingId} | Priority={priority} | Grund=NummerNichtSelektierbar | ExternalNumberID={externalNumberId}");
                return null;
            }

            if (!isOnline) {
                _logger?.Warn($"Externes Routing Kandidat Ã¼bersprungen | RouteID={routingId} | Priority={priority} | Grund=NummerOffline | ExternalNumberID={externalNumberId} | ExternNr={externeNr}");
                return null;
            }

            DataRow? provider = _sipSql.GetProviderAccountById(sourceId);
            if (provider == null) {
                _logger?.Warn($"Externes Routing Kandidat Ã¼bersprungen | RouteID={routingId} | Priority={priority} | Grund=ProviderNichtGefunden | ProviderID={sourceId}");
                return null;
            }

            string protocolRaw = (Convert.ToString(provider["SIPProtokoll"]) ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(protocolRaw)) {
                _logger?.Warn($"Externes Routing Kandidat Ã¼bersprungen | RouteID={routingId} | Priority={priority} | Grund=SIPProtokollLeer | ProviderID={sourceId}");
                return null;
            }

            DataRow? registrationState = _sipSql.GetProviderRegistrationStateByProviderAccountId(sourceId);
            if (registrationState == null) {
                _logger?.Warn($"Externes Routing Kandidat Ã¼bersprungen | RouteID={routingId} | Priority={priority} | Grund=RegistrationStateFehlt | ProviderID={sourceId}");
                return null;
            }

            bool isRegistered = registrationState.Table.Columns.Contains("IsRegistered")
                && registrationState["IsRegistered"] != DBNull.Value
                && Convert.ToBoolean(registrationState["IsRegistered"]);
            if (!isRegistered) {
                _logger?.Warn($"Externes Routing Kandidat Ã¼bersprungen | RouteID={routingId} | Priority={priority} | Grund=ProviderNichtRegistriert | ProviderID={sourceId}");
                return null;
            }

            string protocol = NormalizeProtocolText(protocolRaw);
            string outboundProxy = (Convert.ToString(provider["SIPOutboundProxy"]) ?? string.Empty).Trim();
            string proxy = (Convert.ToString(provider["SIPProxy"]) ?? string.Empty).Trim();
            string registrar = (Convert.ToString(provider["SIPRegistrar"]) ?? string.Empty).Trim();
            string server = (Convert.ToString(provider["SIPServer"]) ?? string.Empty).Trim();
            string authUser = (Convert.ToString(provider["SipAuthUser"]) ?? string.Empty).Trim();
            string sipUser = (Convert.ToString(provider["SIPUser"]) ?? string.Empty).Trim();
            string providerNumber = FirstNotEmpty(Convert.ToString(provider["SIPNr"]), sipUser, externeNr);
            string encryptedPassword = Convert.ToString(provider["SIPPasswort"]) ?? string.Empty;

            string stateRemoteEndPoint = (Convert.ToString(registrationState["LastRemoteEndPoint"]) ?? string.Empty).Trim();
            string stateRequestUri = (Convert.ToString(registrationState["LastRequestUri"]) ?? string.Empty).Trim();
            string stateServiceRoute = (Convert.ToString(registrationState["ServiceRoute"]) ?? string.Empty).Trim();
            string stateAssociatedUri = (Convert.ToString(registrationState["AssociatedUri"]) ?? string.Empty).Trim();
            string stateTransportRaw = (Convert.ToString(registrationState["LastTransport"]) ?? string.Empty).Trim();
            string stateResolvedIp = (Convert.ToString(registrationState["LastResolvedIP"]) ?? string.Empty).Trim();
            string stateTransport = string.IsNullOrWhiteSpace(stateTransportRaw) ? protocol : NormalizeProtocolText(stateTransportRaw);

            if (string.IsNullOrWhiteSpace(authUser)) {
                authUser = sipUser;
            }

            int sipPort = provider["SIPPort"] == DBNull.Value ? 0 : Convert.ToInt32(provider["SIPPort"]);
            if (sipPort <= 0) {
                _logger?.Warn($"Externes Routing Kandidat Ã¼bersprungen | RouteID={routingId} | Priority={priority} | Grund=SIPPortLeer | ProviderID={sourceId}");
                return null;
            }

            string targetSeed = FirstNotEmpty(outboundProxy, proxy, registrar, server);
            HostPort target = ParseHostPort(targetSeed, sipPort, protocol);
            string password = DecryptPassword(encryptedPassword, Convert.ToString(provider["ProviderName"]) ?? target.Host, authUser);

            if (string.IsNullOrWhiteSpace(password)) {
                _logger?.Warn($"Externes Routing Kandidat Ã¼bersprungen | RouteID={routingId} | Priority={priority} | Grund=PasswortLeer | ProviderID={sourceId}");
                return null;
            }

            string inviteHost = target.Host;
            int invitePort = target.Port;
            string inviteProtocol = protocol;

            if (!string.IsNullOrWhiteSpace(stateRequestUri)) {
                try {
                    SIPURI requestUri = SIPURI.ParseSIPURIRelaxed(stateRequestUri);
                    inviteHost = string.IsNullOrWhiteSpace(requestUri.Host) ? inviteHost : requestUri.Host;
                    int requestUriPort = requestUri.ToSIPEndPoint()?.Port ?? 0;
                    if (requestUriPort > 0) {
                        invitePort = requestUriPort;
                    }
                    inviteProtocol = NormalizeProtocolText(requestUri.Protocol.ToString());
                } catch {
                }
            }

            string destinationUri = BuildSipUriString(normalizedCalled, inviteHost, invitePort, inviteProtocol);

            string remoteEndPoint = stateRemoteEndPoint;
            if (string.IsNullOrWhiteSpace(remoteEndPoint) && !string.IsNullOrWhiteSpace(stateServiceRoute)) {
                try {
                    SIPRouteSet serviceRoutes = SIPRouteSet.ParseSIPRouteSet(stateServiceRoute);
                    if (serviceRoutes.Length > 0) {
                        SIPEndPoint routeEndPoint = serviceRoutes.GetAt(0).ToSIPEndPoint();
                        remoteEndPoint = $"{routeEndPoint.Protocol.ToString().ToLowerInvariant()}:{routeEndPoint.Address}:{routeEndPoint.Port}";
                    }
                } catch {
                }
            }

            if (string.IsNullOrWhiteSpace(remoteEndPoint)) {
                remoteEndPoint = $"{stateTransport.ToLowerInvariant()}:{target.Host}:{target.Port}";
            }

            _logger?.Info($"Externes Routing OK | UserID={callerId} | UserType={callerType} | Von={fromNumber} | An={normalizedCalled} | RouteID={routingId} | Priority={priority} | ExternalNumberID={externalNumberId} | ExternNr={externeNr} | Provider={Convert.ToString(provider["ProviderName"])} | Remote={remoteEndPoint} | Uri={destinationUri} | ServiceRoute={stateServiceRoute} | AssociatedUri={stateAssociatedUri} | AuthUser={authUser} | SIPUser={sipUser}");

            return SIPIntResolvedTarget.OkExternal(
                normalizedCalled,
                destinationUri,
                remoteEndPoint,
                stateTransport,
                provider["ID"] == DBNull.Value ? (int?)null : Convert.ToInt32(provider["ID"]),
                Convert.ToString(provider["ProviderName"]),
                authUser,
                sipUser,
                password,
                providerNumber,
                string.IsNullOrWhiteSpace(stateServiceRoute) ? null : stateServiceRoute,
                string.IsNullOrWhiteSpace(stateAssociatedUri) ? null : stateAssociatedUri,
                string.IsNullOrWhiteSpace(stateRequestUri) ? null : stateRequestUri,
                string.IsNullOrWhiteSpace(stateResolvedIp) ? null : stateResolvedIp,
                callerUsername
            );
        }

        /// <summary>
        /// The DecryptPassword
        /// </summary>
        /// <param name="encryptedPassword">The encryptedPassword<see cref="string"/></param>
        /// <param name="providerName">The providerName<see cref="string"/></param>
        /// <param name="authUser">The authUser<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private string DecryptPassword(string encryptedPassword, string providerName, string authUser) {
            try {
                return SipCryptor.DecryptFixed(encryptedPassword);
            } catch (Exception ex) {
                _logger?.Error($"Provider Passwort konnte nicht entschlÃ¼sselt werden | Provider={providerName} | AuthUser={authUser} | Fehler={ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// The FirstNotEmpty
        /// </summary>
        /// <param name="values">The values<see cref="string?[]"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string FirstNotEmpty(params string?[] values) {
            foreach (string? value in values) {
                if (!string.IsNullOrWhiteSpace(value)) {
                    return value.Trim();
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// The NormalizeProtocolText
        /// </summary>
        /// <param name="value">The value<see cref="string?"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string NormalizeProtocolText(string? value) {
            string protocol = (value ?? string.Empty).Trim().ToUpperInvariant();
            return protocol switch {
                "TLS" => "TLS",
                "TCP" => "TCP",
                "UDP" => "UDP",
                _ => throw new InvalidOperationException($"UngÃ¼ltiges SIPProtokoll: {value}")
            };
        }

        /// <summary>
        /// The ParseHostPort
        /// </summary>
        /// <param name="input">The input<see cref="string"/></param>
        /// <param name="defaultPort">The defaultPort<see cref="int"/></param>
        /// <param name="protocol">The protocol<see cref="string"/></param>
        /// <returns>The <see cref="HostPort"/></returns>
        private static HostPort ParseHostPort(string input, int defaultPort, string protocol) {
            string value = (input ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value)) {
                throw new InvalidOperationException("Host/Port ist leer.");
            }

            try {
                SIPURI uri = SIPURI.ParseSIPURIRelaxed(value);
                string hostOnly = SanitizeHost(uri.Host);
                int port = uri.ToSIPEndPoint()?.Port ?? defaultPort;
                return new HostPort(hostOnly, port);
            } catch {
            }

            value = SanitizeHost(value);
            int resultPort = defaultPort;
            if (resultPort <= 0) {
                throw new InvalidOperationException("SIPPort ist leer oder ungÃ¼ltig.");
            }

            if (value.Count(c => c == ':') == 1) {
                int colon = value.LastIndexOf(':');
                if (colon > 0 && int.TryParse(value.Substring(colon + 1), out int parsedPort)) {
                    resultPort = parsedPort;
                    value = value.Substring(0, colon);
                }
            }

            return new HostPort(value, resultPort);
        }

        /// <summary>
        /// The BuildSipUriString
        /// </summary>
        /// <param name="user">The user<see cref="string"/></param>
        /// <param name="host">The host<see cref="string"/></param>
        /// <param name="port">The port<see cref="int"/></param>
        /// <param name="protocol">The protocol<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string BuildSipUriString(string user, string host, int port, string protocol) {
            string normalizedProtocol = NormalizeProtocolText(protocol);
            string scheme = normalizedProtocol == "TLS" ? "sips" : "sip";
            string transportPart = normalizedProtocol == "UDP" ? string.Empty : $";transport={normalizedProtocol.ToLowerInvariant()}";
            return $"{scheme}:{user}@{host}:{port}{transportPart}";
        }

        /// <summary>
        /// The SanitizeHost
        /// </summary>
        /// <param name="value">The value<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string SanitizeHost(string value) {
            value = value.Trim();

            if (value.StartsWith("sip:", StringComparison.OrdinalIgnoreCase)) {
                value = value.Substring(4);
            } else if (value.StartsWith("sips:", StringComparison.OrdinalIgnoreCase)) {
                value = value.Substring(5);
            }

            int semi = value.IndexOf(';');
            if (semi >= 0) {
                value = value.Substring(0, semi);
            }

            int slash = value.IndexOf('/');
            if (slash >= 0) {
                value = value.Substring(0, slash);
            }

            int at = value.LastIndexOf('@');
            if (at >= 0) {
                value = value.Substring(at + 1);
            }

            return value.Trim();
        }

        private readonly record struct HostPort(string Host, int Port);
    }
}
