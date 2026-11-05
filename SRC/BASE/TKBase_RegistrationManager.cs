/*
 * TKBase_RegistrationManager
 * Aufgabe:
 * - Verarbeitet ausschließlich interne SIP-REGISTER-Anfragen unterhalb von TKBase_Server.
 * - Führt Digest-/Legacy-Auth über den SipInt-Bereich aus.
 * - Speichert interne SIP-Registrierungen in InternalRegistrationState und RuntimeRegistration.
 *
 * Nicht Aufgabe:
 * - Keine Provider-Registrierung. Externe Provider-REGISTER gehören in TKSipExt_Registration.
 * - Keine GSM-Registrierung.
 * - Keine Runtime-Start-/Stop-Steuerung.
 *
 * Ablaufvorgabe:
 * TKBase_Server -> TKBase_RegistrationManager -> TKSipInt_Registration/TKSipInt_Authentication
 */

namespace TKServer.BASE {
    using SIPSorcery.SIP;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;

    using TKServer.SipInt;
    using TKServer.SQL;
    /// <summary>
    /// TKBase_RegistrationManager
    /// Aufgabe:
    /// - Verarbeitet ausschließlich interne SIP-REGISTER-Anfragen unterhalb von TKBase_Server.
    /// - Führt Digest-/Legacy-Auth über den SipInt-Bereich aus.
    /// - Speichert interne SIP-Registrierungen in InternalRegistrationState und RuntimeRegistration.
    ///
    /// Nicht Aufgabe:
    /// - Keine Provider-Registrierung. Externe Provider-REGISTER gehören in TKSipExt_Registration.
    /// - Keine GSM-Registrierung.
    /// - Keine Runtime-Start-/Stop-Steuerung.
    /// </summary>
    internal sealed class TKBase_RegistrationManager {
        /// <summary>
        /// Defines the _sipSql
        /// </summary>
        private readonly TKSQL_Commands _sipSql;

        /// <summary>
        /// Defines the _logger
        /// </summary>
        private readonly TKBase_LogManager? _logger;

        /// <summary>
        /// Defines the _settings
        /// </summary>
        private readonly TKBase_ServerSettings? _settings;

        /// <summary>
        /// Defines the _sipIntRegistration
        /// </summary>
        private readonly TKSipInt_Registration _sipIntRegistration;

        /// <summary>
        /// Defines the _sipAuth
        /// </summary>
        private readonly TKSipInt_Authentication _sipAuth;

        /// <summary>
        /// Defines whether the SIPInt runtime area currently accepts internal registrations.
        /// </summary>
        private volatile bool _sipIntRuntimeEnabled = true;

        /// <summary>
        /// Defines the _issuedNonces
        /// </summary>
        private readonly ConcurrentDictionary<string, DateTime> _issuedNonces = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of the <see cref="TKBase_RegistrationManager"/> class.
        /// </summary>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        /// <param name="settings">The settings<see cref="TKBase_ServerSettings?"/></param>
        public TKBase_RegistrationManager(TKBase_LogManager? logger = null, TKBase_ServerSettings? settings = null) {
            _logger = logger;
            _settings = settings;
            _sipSql = new TKSQL_Commands(logger);
            _sipIntRegistration = new TKSipInt_Registration(logger);
            _sipAuth = new TKSipInt_Authentication(logger);
        }

        /// <summary>
        /// Applies runtime area availability from TKBase_ServiceRunTime.
        /// </summary>
        /// <param name="sipIntEnabled">Whether SipInt is enabled.</param>
        public void ConfigureRuntimeAreas(bool sipIntEnabled) {
            _sipIntRuntimeEnabled = sipIntEnabled;
            _logger?.Info($"SipInt REGISTER Runtimebereich gesetzt | SIPInt={sipIntEnabled}");
        }

        /// <summary>
        /// The HandleSipIntRegisterAsync
        /// </summary>
        /// <param name="transport">The transport<see cref="SIPTransport"/></param>
        /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="remoteEndPoint">The remoteEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="request">The request<see cref="SIPRequest"/></param>
        /// <returns>The <see cref="Task"/></returns>
        public async Task HandleSipIntRegisterAsync(SIPTransport transport, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIPRequest request) {
            string callIdForLog = request?.Header?.CallId ?? "-";
            string remoteForLog = remoteEndPoint?.ToString() ?? "-";

            try {
            string username = ExtractUsername(request);
            string contactUri = request.Header?.Contact?.FirstOrDefault()?.ContactURI?.ToString() ?? string.Empty;
            string remote = remoteEndPoint.ToString();
            string? transportName = localEndPoint?.Protocol.ToString();
            string callId = request.Header?.CallId ?? CallProperties.CreateNewCallId();
            string? userAgent = request.Header?.UserAgent;
            int expires = ExtractExpires(request);

            if (string.IsNullOrWhiteSpace(username)) {
                await SendSimpleResponseAsync(transport, request, SIPResponseStatusCodesEnum.BadRequest, "Username missing");
                return;
            }

            if (!_sipIntRuntimeEnabled && expires > 0) {
                _logger?.Info($"SIPInt REGISTER abgewiesen | SIPInt RuntimeAreaControl deaktiviert | Username={username} | Remote={remote}");
                await SendSimpleResponseAsync(transport, request, SIPResponseStatusCodesEnum.TemporarilyUnavailable, "SIPInt RuntimeAreaControl deaktiviert");
                return;
            }

            if (expires <= 0) {
                CleanupExpiredInternalRegistrations();
                DeactivateInternalRegistration(callId);
                await SendRegisterOkAsync(transport, request, expires);
                return;
            }

            SIPAuthenticationHeader? authHeader = request.Header?.AuthenticationHeaders?
                .FirstOrDefault(x => x?.SIPDigest != null &&
                    (x.AuthorisationType == SIPAuthorisationHeadersEnum.Authorize ||
                     x.AuthorisationType == SIPAuthorisationHeadersEnum.ProxyAuthorization));

            if (authHeader?.SIPDigest == null) {
                string? legacyPassword = ExtractInternalPassword(request);
                if (!string.IsNullOrWhiteSpace(legacyPassword) &&
                    _sipIntRegistration.HandleRegister(username, legacyPassword, contactUri, remote, transportName, callId, userAgent, expires)) {
                    int? userId = _sipAuth.GetUserId(username);
                    if (!userId.HasValue) {
                        await SendSimpleResponseAsync(transport, request, SIPResponseStatusCodesEnum.Forbidden, "Registration failed");
                        return;
                    }

                    SaveInternalRegistration(userId.Value, username, contactUri, remote, transportName, callId, userAgent, DateTime.Now.AddSeconds(expires), true);
                    await SendRegisterOkAsync(transport, request, expires);
                    return;
                }

                await SendDigestChallengeAsync(transport, request);
                return;
            }

            SIPAuthorisationDigest digest = authHeader.SIPDigest;
            string digestUsername = !string.IsNullOrWhiteSpace(digest.Username) ? digest.Username.Trim() : username;

            if (!IsNonceValid(digest.Nonce)) {
                await SendDigestChallengeAsync(transport, request, stale: true);
                return;
            }

            string expectedRealm = GetAuthRealm();
            if (!string.IsNullOrWhiteSpace(digest.Realm) && !string.Equals(digest.Realm, expectedRealm, StringComparison.OrdinalIgnoreCase)) {
                await SendDigestChallengeAsync(transport, request);
                return;
            }

            if (!_sipAuth.ValidateDigest(digestUsername, digest, request.Method.ToString())) {
                await SendSimpleResponseAsync(transport, request, SIPResponseStatusCodesEnum.Forbidden, "Authentication failed");
                return;
            }

            if (!_sipIntRegistration.HandleRegisterAuthenticated(digestUsername, contactUri, remote, transportName, callId, userAgent, expires)) {
                await SendSimpleResponseAsync(transport, request, SIPResponseStatusCodesEnum.Forbidden, "Registration failed");
                return;
            }

            int? authenticatedUserId = _sipAuth.GetUserId(digestUsername);
            if (!authenticatedUserId.HasValue) {
                await SendSimpleResponseAsync(transport, request, SIPResponseStatusCodesEnum.Forbidden, "Registration failed");
                return;
            }

            SaveInternalRegistration(authenticatedUserId.Value, digestUsername, contactUri, remote, transportName, callId, userAgent, DateTime.Now.AddSeconds(expires), true);
            await SendRegisterOkAsync(transport, request, expires);
                    } catch (Exception ex) {
                _logger?.Error($"SipInt REGISTER Verarbeitung fehlgeschlagen | CallID={callIdForLog} | Remote={remoteForLog} | Fehler={ex}");
                await SendSimpleResponseAsync(transport, request, SIPResponseStatusCodesEnum.InternalServerError, "REGISTER processing failed");
            }
        }

        /// <summary>
        /// The SaveInternalRegistration
        /// </summary>
        /// <param name="userId">The userId<see cref="int"/></param>
        /// <param name="username">The username<see cref="string"/></param>
        /// <param name="contactUri">The contactUri<see cref="string"/></param>
        /// <param name="remoteEndPoint">The remoteEndPoint<see cref="string"/></param>
        /// <param name="transport">The transport<see cref="string?"/></param>
        /// <param name="callId">The callId<see cref="string"/></param>
        /// <param name="userAgent">The userAgent<see cref="string?"/></param>
        /// <param name="expiresAt">The expiresAt<see cref="DateTime"/></param>
        /// <param name="isRegistered">The isRegistered<see cref="bool"/></param>
        /// <param name="errorText">The errorText<see cref="string?"/></param>
        public void SaveInternalRegistration(
            int userId,
            string username,
            string contactUri,
            string remoteEndPoint,
            string? transport,
            string callId,
            string? userAgent,
            DateTime expiresAt,
            bool isRegistered,
            string? errorText = null) {

            Guid registrationGuid = BuildInternalRegistrationGuid(userId, transport, remoteEndPoint);

            int internalStateId = _sipSql.SaveOrUpdateInternalRegistrationState(
                registrationGuid,
                userId,
                username,
                contactUri,
                remoteEndPoint,
                transport,
                callId,
                userAgent,
                expiresAt,
                isRegistered,
                errorText);

            _sipSql.SaveOrUpdateRuntimeRegistrationInternal(
                registrationGuid,
                userId,
                internalStateId,
                username,
                contactUri,
                remoteEndPoint,
                transport,
                callId,
                userAgent,
                expiresAt,
                isRegistered,
                errorText);

            _logger?.Info($"SipInt RuntimeRegistration gespeichert | UserID={userId} | Username={username} | CallID={callId} | Registered={isRegistered} | StateID={internalStateId}");
        }

        /// <summary>
        /// The SaveExternalRegistration
        /// </summary>
        /// <param name="providerAccountId">The providerAccountId<see cref="int"/></param>
        /// <param name="providerName">The providerName<see cref="string"/></param>
        /// <param name="aorUri">The aorUri<see cref="string"/></param>
        /// <param name="contactUri">The contactUri<see cref="string?"/></param>
        /// <param name="remoteEndPoint">The remoteEndPoint<see cref="string?"/></param>
        /// <param name="transport">The transport<see cref="string?"/></param>
        /// <param name="requestUri">The requestUri<see cref="string?"/></param>
        /// <param name="callId">The callId<see cref="string?"/></param>
        /// <param name="serviceRoute">The serviceRoute<see cref="string?"/></param>
        /// <param name="associatedUri">The associatedUri<see cref="string?"/></param>
        /// <param name="lastResolvedIp">The lastResolvedIp<see cref="string?"/></param>
        /// <param name="statusCode">The statusCode<see cref="int?"/></param>
        /// <param name="statusText">The statusText<see cref="string?"/></param>
        /// <param name="expiresAt">The expiresAt<see cref="DateTime?"/></param>
        /// <param name="isRegistered">The isRegistered<see cref="bool"/></param>
        /// <param name="errorText">The errorText<see cref="string?"/></param>
        public void SaveExternalRegistration(
            int providerAccountId,
            string providerName,
            string aorUri,
            string? contactUri,
            string? remoteEndPoint,
            string? transport,
            string? requestUri,
            string? callId,
            string? serviceRoute,
            string? associatedUri,
            string? lastResolvedIp,
            int? statusCode,
            string? statusText,
            DateTime? expiresAt,
            bool isRegistered,
            string? errorText = null) {

            _sipSql.SaveOrUpdateRuntimeRegistrationExternal(
                providerAccountId,
                providerName,
                aorUri,
                contactUri,
                remoteEndPoint,
                transport,
                requestUri,
                callId,
                serviceRoute,
                associatedUri,
                lastResolvedIp,
                statusCode,
                statusText,
                expiresAt,
                isRegistered,
                errorText);

            _logger?.Info($"RuntimeRegistration extern gespeichert | ProviderAccountID={providerAccountId} | CallID={callId ?? "-"} | Registered={isRegistered}");
        }

        /// <summary>
        /// The DeactivateInternalRegistration
        /// </summary>
        /// <param name="callId">The callId<see cref="string"/></param>
        public void DeactivateInternalRegistration(string callId) {
            if (string.IsNullOrWhiteSpace(callId)) {
                return;
            }

            _sipSql.DeactivateInternalRegistrationStateByCallId(callId);
            _sipSql.DeactivateRuntimeRegistrationInternalByCallId(callId);
            _logger?.Info($"SipInt Registrierung deaktiviert | CallID={callId}");
        }

        /// <summary>
        /// The CleanupExpiredInternalRegistrations
        /// </summary>
        /// <returns>The <see cref="int"/></returns>
        public int CleanupExpiredInternalRegistrations() {
            int internalRows = _sipSql.CleanupExpiredInternalRegistrationStates();
            int runtimeRows = _sipSql.CleanupExpiredRuntimeRegistrations("Internal");
            int total = internalRows + runtimeRows;
            _logger?.Info($"SipInt Registrierung Cleanup | InternalState={internalRows} | Runtime={runtimeRows} | Gesamt={total}");
            return total;
        }

        /// <summary>
        /// The SendSimpleResponseAsync
        /// </summary>
        /// <param name="transport">The transport<see cref="SIPTransport"/></param>
        /// <param name="request">The request<see cref="SIPRequest"/></param>
        /// <param name="status">The status<see cref="SIPResponseStatusCodesEnum"/></param>
        /// <param name="reason">The reason<see cref="string?"/></param>
        /// <returns>The <see cref="Task"/></returns>
        private async Task SendSimpleResponseAsync(SIPTransport transport, SIPRequest request, SIPResponseStatusCodesEnum status, string? reason) {
            try {
                SIPResponse response = SIPResponse.GetResponse(request, status, reason);
                await transport.SendResponseAsync(response);
            } catch (Exception ex) {
                _logger?.Warn($"SipInt REGISTER Antwort konnte nicht gesendet werden | Status={(int)status} {status} | CallID={request?.Header?.CallId ?? "-"} | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The SendRegisterOkAsync
        /// </summary>
        /// <param name="transport">The transport<see cref="SIPTransport"/></param>
        /// <param name="request">The request<see cref="SIPRequest"/></param>
        /// <param name="expires">The expires<see cref="int"/></param>
        /// <returns>The <see cref="Task"/></returns>
        private async Task SendRegisterOkAsync(SIPTransport transport, SIPRequest request, int expires) {
            try {
                SIPResponse response = SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.Ok, null);
                if (request.Header?.Contact != null) {
                    response.Header.Contact = request.Header.Contact;
                }
                response.Header.Expires = expires;
                await transport.SendResponseAsync(response);
            } catch (Exception ex) {
                _logger?.Warn($"SipInt REGISTER OK konnte nicht gesendet werden | CallID={request?.Header?.CallId ?? "-"} | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The SendDigestChallengeAsync
        /// </summary>
        /// <param name="transport">The transport<see cref="SIPTransport"/></param>
        /// <param name="request">The request<see cref="SIPRequest"/></param>
        /// <param name="stale">The stale<see cref="bool"/></param>
        /// <returns>The <see cref="Task"/></returns>
        private async Task SendDigestChallengeAsync(SIPTransport transport, SIPRequest request, bool stale = false) {
            try {
                string realm = GetAuthRealm();
                string nonce = GenerateNonce();
                _issuedNonces[nonce] = DateTime.UtcNow.AddMinutes(5);
                CleanupExpiredNonces();

                SIPResponse response = SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.Unauthorised, null);
                SIPAuthenticationHeader authHeader = new SIPAuthenticationHeader(SIPAuthorisationHeadersEnum.WWWAuthenticate, realm, nonce);
                authHeader.SIPDigest.Qop = SIPAuthorisationDigest.QOP_AUTHENTICATION_VALUE;
                authHeader.SIPDigest.DigestAlgorithm = DigestAlgorithmsEnum.MD5;
                authHeader.SIPDigest.Opaque = ComputeOpaque(realm, nonce);
                response.Header.AuthenticationHeaders ??= new List<SIPAuthenticationHeader>();
                response.Header.AuthenticationHeaders.Add(authHeader);
                await transport.SendResponseAsync(response);
            } catch (Exception ex) {
                _logger?.Warn($"SipInt REGISTER Digest-Challenge konnte nicht gesendet werden | CallID={request?.Header?.CallId ?? "-"} | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The GetAuthRealm
        /// </summary>
        /// <returns>The <see cref="string"/></returns>
        private string GetAuthRealm() {
            if (_settings is not TKBase_ServerSettings settings || string.IsNullOrWhiteSpace(settings.Realm)) {
                throw new InvalidOperationException("TK_Properties.Realm ist nicht geladen oder leer.");
            }

            return settings.Realm.Trim();
        }

        /// <summary>
        /// The IsNonceValid
        /// </summary>
        /// <param name="nonce">The nonce<see cref="string?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private bool IsNonceValid(string? nonce) {
            if (string.IsNullOrWhiteSpace(nonce)) {
                return false;
            }
            if (!_issuedNonces.TryGetValue(nonce, out DateTime validUntil)) {
                return false;
            }
            if (validUntil < DateTime.UtcNow) {
                _issuedNonces.TryRemove(nonce, out _);
                return false;
            }
            return true;
        }

        /// <summary>
        /// The CleanupExpiredNonces
        /// </summary>
        private void CleanupExpiredNonces() {
            DateTime utcNow = DateTime.UtcNow;
            foreach (KeyValuePair<string, DateTime> entry in _issuedNonces.ToArray()) {
                if (entry.Value < utcNow) {
                    _issuedNonces.TryRemove(entry.Key, out _);
                }
            }
        }

        /// <summary>
        /// The GenerateNonce
        /// </summary>
        /// <returns>The <see cref="string"/></returns>
        private static string GenerateNonce() {
            byte[] nonceBytes = RandomNumberGenerator.GetBytes(16);
            return Convert.ToHexString(nonceBytes).ToLowerInvariant();
        }

        /// <summary>
        /// The ComputeOpaque
        /// </summary>
        /// <param name="realm">The realm<see cref="string"/></param>
        /// <param name="nonce">The nonce<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string ComputeOpaque(string realm, string nonce) {
            byte[] opaqueBytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{realm}:{nonce}"));
            return Convert.ToHexString(opaqueBytes).ToLowerInvariant();
        }

        /// <summary>
        /// The ExtractUsername
        /// </summary>
        /// <param name="request">The request<see cref="SIPRequest"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string ExtractUsername(SIPRequest request) {
            string? user = request.Header?.From?.FromURI?.User;
            if (!string.IsNullOrWhiteSpace(user)) {
                return DecodeSipUserPart(user);
            }
            return DecodeSipUserPart(request.Header?.To?.ToURI?.User);
        }

        /// <summary>
        /// The DecodeSipUserPart
        /// </summary>
        /// <param name="value">The value<see cref="string?"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string DecodeSipUserPart(string? value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return string.Empty;
            }
            try {
                return Uri.UnescapeDataString(value.Trim());
            } catch {
                return value.Trim();
            }
        }

        /// <summary>
        /// The ExtractExpires
        /// </summary>
        /// <param name="request">The request<see cref="SIPRequest"/></param>
        /// <returns>The <see cref="int"/></returns>
        private int ExtractExpires(SIPRequest request) {
            if (_settings is not TKBase_ServerSettings settings) {
                throw new InvalidOperationException("TK_Properties.RegisterExpire ist nicht geladen.");
            }

            if (request.Header?.Expires == 0) {
                return 0;
            }

            SIPContactHeader? contact = request.Header?.Contact?.FirstOrDefault();
            if (contact?.Expires == 0) {
                return 0;
            }

            return settings.RegisterExpire;
        }

        /// <summary>
        /// The ExtractInternalPassword
        /// </summary>
        /// <param name="request">The request<see cref="SIPRequest"/></param>
        /// <returns>The <see cref="string?"/></returns>
        private static string? ExtractInternalPassword(SIPRequest request) {
            string? password = request.Header?.UnknownHeaders?
                .FirstOrDefault(x => x.StartsWith("X-TK-Password:", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(password)) {
                return null;
            }
            int idx = password.IndexOf(':');
            if (idx < 0 || idx == password.Length - 1) {
                return null;
            }
            return password[(idx + 1)..].Trim();
        }

        /// <summary>
        /// The BuildInternalRegistrationGuid
        /// </summary>
        /// <param name="userId">The userId<see cref="int"/></param>
        /// <param name="transport">The transport<see cref="string?"/></param>
        /// <param name="remoteEndPoint">The remoteEndPoint<see cref="string"/></param>
        /// <returns>The <see cref="Guid"/></returns>
        private static Guid BuildInternalRegistrationGuid(int userId, string? transport, string remoteEndPoint) {
            string normalizedTransport = string.IsNullOrWhiteSpace(transport)
                ? string.Empty
                : transport.Trim().ToLowerInvariant();
            string normalizedRemote = string.IsNullOrWhiteSpace(remoteEndPoint)
                ? string.Empty
                : remoteEndPoint.Trim().ToLowerInvariant();
            string key = $"INT|{userId}|{normalizedTransport}|{normalizedRemote}";

            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
            byte[] guidBytes = new byte[16];
            Array.Copy(hash, guidBytes, guidBytes.Length);
            return new Guid(guidBytes);
        }
    }
}
