/*
 * TKBase_SessionManager
 * Aufgabe:
 * - Verwaltet neutrale Laufzeitsessions.
 * - Erzeugt SIP-, Provider-, GSM-, SVC- und Fax-Sessions zentral.
 * - Hält Leg A und Leg B getrennt.
 * - Enthält keine Routingentscheidung, keine Transportausführung und keine Command-/Event-Logik.
 */

namespace TKServer.BASE {
    using SIPSorcery.SIP;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Security.Cryptography;
    using System.Text;

    using TKServer.GSM;
    using TKServer.SipExt;
    using TKServer.SVC;
    /// TKBase_SessionManager
    /// Aufgabe:
    /// - Verwaltet neutrale Laufzeitsessions.
    /// - Erzeugt SIP-, Provider-, GSM-, SVC- und Fax-Sessions zentral.
    /// - Hält Leg A und Leg B getrennt.
    /// - Enthält keine Routingentscheidung, keine Transportausführung und keine Command-/Event-Logik.

    /// <summary>
    /// Defines the <see cref="TKBase_SessionManager" />
    /// </summary>
    public sealed class TKBase_SessionManager {
        /// <summary>
        /// Defines the _sessions
        /// </summary>
        private readonly ConcurrentDictionary<string, SIP_ServerSession> _sessions = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Defines the _sessionAliases
        /// </summary>
        private readonly ConcurrentDictionary<string, string> _sessionAliases = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Defines the _logger
        /// </summary>
        private readonly TKBase_LogManager? _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TKBase_SessionManager"/> class.
        /// </summary>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        public TKBase_SessionManager(TKBase_LogManager? logger = null) {
            _logger = logger;
        }

        /// <summary>
        /// The AddOrUpdate
        /// </summary>
        /// <param name="sessionId">The sessionId<see cref="string"/></param>
        /// <param name="fromNumber">The fromNumber<see cref="string"/></param>
        /// <param name="toNumber">The toNumber<see cref="string"/></param>
        /// <param name="status">The status<see cref="string"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public bool AddOrUpdate(string sessionId, string fromNumber, string toNumber, string status) {
            if (string.IsNullOrWhiteSpace(sessionId)) return false;
            SIP_ServerSession session = _sessions.AddOrUpdate(
                sessionId,
                _ => new SIP_ServerSession { SessionId = sessionId, FromNumber = fromNumber, ToNumber = toNumber, Status = status, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now },
                (_, existing) => { existing.FromNumber = fromNumber; existing.ToNumber = toNumber; existing.Status = status; existing.UpdatedAt = DateTime.Now; return existing; });
            RegisterAlias(session.SessionId, session.SessionId);
            return true;
        }

        /// <summary>
        /// The CreateInviteSession
        /// </summary>
        /// <param name="legACallId">The legACallId<see cref="string"/></param>
        /// <param name="fromNumber">The fromNumber<see cref="string"/></param>
        /// <param name="toNumber">The toNumber<see cref="string"/></param>
        /// <param name="originalInvite">The originalInvite<see cref="SIPRequest"/></param>
        /// <param name="callerEndPoint">The callerEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="callerLocalEndPoint">The callerLocalEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="targetEndPoint">The targetEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="targetContactUri">The targetContactUri<see cref="string?"/></param>
        /// <param name="route">The route<see cref="SIPIntResolvedTarget?"/></param>
        /// <param name="registerLegAAlias">The registerLegAAlias<see cref="bool"/></param>
        /// <param name="parentSessionId">The parentSessionId<see cref="string?"/></param>
        /// <returns>The <see cref="SIP_ServerSession"/></returns>
        public SIP_ServerSession CreateInviteSession(
            string legACallId,
            string fromNumber,
            string toNumber,
            SIPRequest originalInvite,
            SIPEndPoint callerEndPoint,
            SIPEndPoint callerLocalEndPoint,
            SIPEndPoint targetEndPoint,
            string? targetContactUri,
            SIPIntResolvedTarget? route = null,
            bool registerLegAAlias = true,
            string? parentSessionId = null) {

            string normalizedLegACallId = string.IsNullOrWhiteSpace(legACallId) ? originalInvite.Header?.CallId ?? CallProperties.CreateNewCallId() : legACallId.Trim();
            string legBCallId = CallProperties.CreateNewCallId();
            string sessionId = CreateSessionId(normalizedLegACallId, legBCallId);

            SIP_ServerSession session = new SIP_ServerSession {
                SessionId = sessionId,
                LegACallId = normalizedLegACallId,
                BridgeId = CreateDeterministicGuid($"BRIDGE|{sessionId}"),
                FromNumber = fromNumber,
                ToNumber = toNumber,
                FromUsername = fromNumber,
                ToUsername = route?.TargetUsername ?? toNumber,
                Status = "Trying",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                ParentSessionId = parentSessionId
            };

            session.LegA.CallId = normalizedLegACallId;
            session.LegA.InviteReceived = originalInvite.Copy();
            session.LegA.InviteReceivedRaw = originalInvite.ToString();
            session.LegA.LastRequestReceived = session.LegA.InviteReceived;
            session.LegA.LastRequestReceivedRaw = session.LegA.InviteReceivedRaw;
            session.LegA.SipEndPoint = callerEndPoint;
            session.LegA.LocalSipEndPoint = callerLocalEndPoint;
            session.LegA.RequestUri = originalInvite.URI;
            session.LegA.ContactUri = originalInvite.Header?.Contact?.FirstOrDefault()?.ContactURI;
            session.LegA.FromHeader = originalInvite.Header?.From?.ToString();
            session.LegA.ToHeader = originalInvite.Header?.To?.ToString();
            session.LegA.FromTag = originalInvite.Header?.From?.FromTag;
            session.LegA.ToTag = originalInvite.Header?.To?.ToTag;
            session.LegA.DialogCSeq = originalInvite.Header?.CSeq ?? 0;
            session.LegA.InviteSeen = true;

            session.LegB.CallId = legBCallId;
            session.LegB.SipEndPoint = targetEndPoint;
            session.LegB.TargetContactUri = targetContactUri;
            session.LegB.RemoteTargetUri = !string.IsNullOrWhiteSpace(targetContactUri) ? SIPURI.ParseSIPURIRelaxed(targetContactUri) : null;
            session.LegB.RequestUri = session.LegB.RemoteTargetUri;

            ApplyRoute(session, route);
            StoreAndAlias(session, registerLegAAlias);
            _logger?.Info($"SIP Session erstellt | SessionID={sessionId} | LegA={normalizedLegACallId} | LegB={legBCallId} | Von={fromNumber} | An={toNumber}");
            return session;
        }

        /// <summary>
        /// The CreateGsmSession
        /// </summary>
        /// <param name="legACallId">The legACallId<see cref="string"/></param>
        /// <param name="fromNumber">The fromNumber<see cref="string"/></param>
        /// <param name="toNumber">The toNumber<see cref="string"/></param>
        /// <param name="originalInvite">The originalInvite<see cref="SIPRequest"/></param>
        /// <param name="callerEndPoint">The callerEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="callerLocalEndPoint">The callerLocalEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="route">The route<see cref="SIPIntResolvedTarget"/></param>
        /// <returns>The <see cref="SIP_ServerSession"/></returns>
        public SIP_ServerSession CreateGsmSession(string legACallId, string fromNumber, string toNumber, SIPRequest originalInvite, SIPEndPoint callerEndPoint, SIPEndPoint callerLocalEndPoint, SIPIntResolvedTarget route) {
            string normalizedLegACallId = string.IsNullOrWhiteSpace(legACallId) ? originalInvite.Header?.CallId ?? CallProperties.CreateNewCallId() : legACallId.Trim();
            string legBCallId = CallProperties.CreateNewCallId();
            string sessionId = CreateSessionId(normalizedLegACallId, legBCallId);

            SIP_ServerSession session = new SIP_ServerSession {
                SessionId = sessionId,
                LegACallId = normalizedLegACallId,
                BridgeId = CreateDeterministicGuid($"BRIDGE|{sessionId}"),
                FromNumber = fromNumber,
                ToNumber = toNumber,
                Status = "Trying",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                IsGsmSession = true,
                GsmDirection = "Outbound",
                GsmStatusId = route.GsmStatusId,
                GsmModemId = route.GsmModemId,
                GsmSimId = route.GsmSimId,
                ProviderName = route.GsmProviderName,
                GsmComPortAT = route.GsmComPortAT,
                GsmComPortAudio = route.GsmComPortAudio,
                GsmImei = route.GsmImei,
                GsmRouteReason = route.GsmRouteReason,
                GsmRouteKind = route.TargetKind
            };

            session.LegA.CallId = normalizedLegACallId;
            session.LegA.SipEndPoint = callerEndPoint;
            session.LegA.LocalSipEndPoint = callerLocalEndPoint;
            session.LegA.RequestUri = originalInvite.URI;
            session.LegA.ContactUri = originalInvite.Header?.Contact?.FirstOrDefault()?.ContactURI;
            session.LegA.InviteReceived = originalInvite.Copy();
            session.LegA.InviteReceivedRaw = originalInvite.ToString();
            session.LegA.LastRequestReceived = session.LegA.InviteReceived;
            session.LegA.LastRequestReceivedRaw = session.LegA.InviteReceivedRaw;
            session.LegA.FromHeader = originalInvite.Header?.From?.ToString();
            session.LegA.ToHeader = originalInvite.Header?.To?.ToString();
            session.LegA.FromTag = originalInvite.Header?.From?.FromTag;
            session.LegA.ToTag = originalInvite.Header?.To?.ToTag;
            session.LegA.DialogCSeq = originalInvite.Header?.CSeq ?? 0;
            session.LegA.InviteSeen = true;

            session.LegB.CallId = legBCallId;
            session.LegB.TargetContactUri = route.GsmNumber;
            session.LegB.UpdatedAt = DateTime.Now;

            StoreAndAlias(session, true);
            _logger?.Info($"GSM Session erstellt | SessionID={session.SessionId} | Direction=Outbound | LegA={session.LegA.CallId} | LegB={session.LegB.CallId} | StatusID={session.GsmStatusId}");
            return session;
        }

        /// <summary>
        /// The CreateServiceSession
        /// </summary>
        /// <param name="legACallId">The legACallId<see cref="string"/></param>
        /// <param name="fromNumber">The fromNumber<see cref="string"/></param>
        /// <param name="toNumber">The toNumber<see cref="string"/></param>
        /// <param name="originalInvite">The originalInvite<see cref="SIPRequest"/></param>
        /// <param name="callerEndPoint">The callerEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="callerLocalEndPoint">The callerLocalEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="serviceLeg">The serviceLeg<see cref="TKSVC_Session"/></param>
        /// <returns>The <see cref="SIP_ServerSession"/></returns>
        public SIP_ServerSession CreateServiceSession(
            string legACallId,
            string fromNumber,
            string toNumber,
            SIPRequest originalInvite,
            SIPEndPoint callerEndPoint,
            SIPEndPoint callerLocalEndPoint,
            TKSVC_Session serviceLeg) {

            if (serviceLeg == null) throw new ArgumentNullException(nameof(serviceLeg));

            string normalizedLegACallId = string.IsNullOrWhiteSpace(legACallId)
                ? originalInvite.Header?.CallId ?? CallProperties.CreateNewCallId()
                : legACallId.Trim();
            string legDCallId = CallProperties.CreateNewCallId();
            string sessionId = CreateSessionId(normalizedLegACallId, legDCallId);

            SIP_ServerSession session = new SIP_ServerSession {
                SessionId = sessionId,
                LegACallId = normalizedLegACallId,
                BridgeId = CreateDeterministicGuid($"BRIDGE|{sessionId}"),
                FromNumber = fromNumber,
                ToNumber = toNumber,
                Status = "Service",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                IsServiceSession = true,
                RouteTargetKind = SIPIntRouteTargetKind.Service
            };

            session.LegA.CallId = normalizedLegACallId;
            session.LegA.SipEndPoint = callerEndPoint;
            session.LegA.LocalSipEndPoint = callerLocalEndPoint;
            session.LegA.RequestUri = originalInvite.URI;
            session.LegA.ContactUri = originalInvite.Header?.Contact?.FirstOrDefault()?.ContactURI;
            session.LegA.InviteReceived = originalInvite.Copy();
            session.LegA.InviteReceivedRaw = originalInvite.ToString();
            session.LegA.LastRequestReceived = session.LegA.InviteReceived;
            session.LegA.LastRequestReceivedRaw = session.LegA.InviteReceivedRaw;
            session.LegA.FromHeader = originalInvite.Header?.From?.ToString();
            session.LegA.ToHeader = originalInvite.Header?.To?.ToString();
            session.LegA.FromTag = originalInvite.Header?.From?.FromTag;
            session.LegA.ToTag = originalInvite.Header?.To?.ToTag;
            session.LegA.DialogCSeq = originalInvite.Header?.CSeq ?? 0;
            session.LegA.InviteSeen = true;
            session.LegA.UpdatedAt = DateTime.Now;

            session.LegD.CallId = legDCallId;
            session.LegD.ServiceLeg = serviceLeg;
            session.LegD.ServiceType = serviceLeg.ServiceType;
            session.LegD.ServiceCode = serviceLeg.ServiceCode;
            session.LegD.InternalNumber = serviceLeg.InternalNumber;
            session.LegD.DisplayName = serviceLeg.DisplayName;
            session.LegD.AutoAnswer = serviceLeg.AutoAnswer;
            session.LegD.KeepSessionUntilPeerEnded = serviceLeg.KeepSessionUntilPeerEnded;
            session.LegD.UpdatedAt = DateTime.Now;

            StoreAndAlias(session, true);
            RegisterAlias(session.LegD.CallId, session.SessionId);
            _logger?.Info($"Service Session erstellt | SessionID={session.SessionId} | LegA={session.LegA.CallId} | LegD={session.LegD.CallId} | Service={serviceLeg.InternalNumber}");
            return session;
        }



        /// <summary>
        /// The CreateFaxSession
        /// </summary>
        /// <param name="legACallId">The legACallId<see cref="string"/></param>
        /// <param name="fromNumber">The fromNumber<see cref="string"/></param>
        /// <param name="toNumber">The toNumber<see cref="string"/></param>
        /// <param name="originalInvite">The originalInvite<see cref="SIPRequest"/></param>
        /// <param name="callerEndPoint">The callerEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="callerLocalEndPoint">The callerLocalEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="route">The route<see cref="SIPIntResolvedTarget"/></param>
        /// <param name="direction">The direction<see cref="string?"/></param>
        /// <param name="registerLegAAlias">The registerLegAAlias<see cref="bool"/></param>
        /// <param name="parentSessionId">The parentSessionId<see cref="string?"/></param>
        /// <returns>The <see cref="SIP_ServerSession"/></returns>
        public SIP_ServerSession CreateFaxSession(
            string legACallId,
            string fromNumber,
            string toNumber,
            SIPRequest originalInvite,
            SIPEndPoint callerEndPoint,
            SIPEndPoint callerLocalEndPoint,
            SIPIntResolvedTarget route,
            string? direction = null,
            bool registerLegAAlias = true,
            string? parentSessionId = null) {

            if (route == null) throw new ArgumentNullException(nameof(route));

            string normalizedLegACallId = string.IsNullOrWhiteSpace(legACallId) ? originalInvite.Header?.CallId ?? CallProperties.CreateNewCallId() : legACallId.Trim();
            string legBCallId = CallProperties.CreateNewCallId();
            string sessionId = CreateSessionId(normalizedLegACallId, legBCallId);
            string faxDirection = string.IsNullOrWhiteSpace(direction) ? (route.IsExternalTarget ? "Outbound" : "Inbound") : direction.Trim();
            bool isOutboundFax = string.Equals(faxDirection, "Outbound", StringComparison.OrdinalIgnoreCase);
            string faxUserNumber = isOutboundFax ? fromNumber : (route.CalledNumber ?? toNumber);
            string faxUserName = isOutboundFax ? fromNumber : route.TargetUsername;

            SIP_ServerSession session = new SIP_ServerSession {
                SessionId = sessionId,
                LegACallId = normalizedLegACallId,
                BridgeId = CreateDeterministicGuid($"BRIDGE|{sessionId}"),
                FromNumber = fromNumber,
                ToNumber = toNumber,
                FromUsername = fromNumber,
                ToUsername = route.TargetUsername ?? toNumber,
                Status = "Trying",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                ParentSessionId = parentSessionId,
                IsFaxSession = true,
                FaxDirection = faxDirection,
                FaxTargetUserId = route.TargetUserId,
                FaxUserNumber = faxUserNumber,
                FaxUserName = faxUserName,
                FaxMode = "Unknown",
                FaxStatus = "Trying",
                FaxJobId = string.Empty,
                RouteTargetKind = route.TargetKind == SIPIntRouteTargetKind.None ? SIPIntRouteTargetKind.FaxUser : route.TargetKind,
                RouteReason = route.Reason
            };

            session.LegA.CallId = normalizedLegACallId;
            session.LegA.InviteReceived = originalInvite.Copy();
            session.LegA.InviteReceivedRaw = originalInvite.ToString();
            session.LegA.LastRequestReceived = session.LegA.InviteReceived;
            session.LegA.LastRequestReceivedRaw = session.LegA.InviteReceivedRaw;
            session.LegA.SipEndPoint = callerEndPoint;
            session.LegA.LocalSipEndPoint = callerLocalEndPoint;
            session.LegA.RequestUri = originalInvite.URI;
            session.LegA.ContactUri = originalInvite.Header?.Contact?.FirstOrDefault()?.ContactURI;
            session.LegA.FromHeader = originalInvite.Header?.From?.ToString();
            session.LegA.ToHeader = originalInvite.Header?.To?.ToString();
            session.LegA.FromTag = originalInvite.Header?.From?.FromTag;
            session.LegA.ToTag = originalInvite.Header?.To?.ToTag;
            session.LegA.DialogCSeq = originalInvite.Header?.CSeq ?? 0;
            session.LegA.InviteSeen = true;
            session.LegA.UpdatedAt = DateTime.Now;

            session.LegB.CallId = legBCallId;
            session.LegB.TargetContactUri = route.ContactUri ?? route.CalledNumber ?? toNumber;
            session.LegB.UpdatedAt = DateTime.Now;

            ApplyRoute(session, route);
            session.IsFaxSession = true;
            session.FaxDirection = faxDirection;
            session.FaxUserNumber = faxUserNumber;
            session.FaxUserName = faxUserName;
            StoreAndAlias(session, registerLegAAlias);
            _logger?.Info($"FAX Session erstellt | SessionID={sessionId} | Direction={faxDirection} | LegA={normalizedLegACallId} | LegB={legBCallId} | Von={fromNumber} | An={toNumber} | FaxUser={session.FaxUserNumber}");
            return session;
        }

        /// <summary>
        /// The AttachServiceLegD
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="serviceLeg">The serviceLeg<see cref="TKSVC_Session"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public bool AttachServiceLegD(SIP_ServerSession session, TKSVC_Session serviceLeg) {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (serviceLeg == null) throw new ArgumentNullException(nameof(serviceLeg));

            lock (session.SyncRoot) {
                if (session.IsTerminated) {
                    _logger?.Warn($"Service LegD konnte nicht angehaengt werden | SessionID={session.SessionId} | Grund=Session beendet");
                    return false;
                }

                if (session.LegD.ServiceLeg != null && !session.LegD.IsEnded) {
                    _logger?.Warn($"Service LegD konnte nicht angehaengt werden | SessionID={session.SessionId} | Grund=LegD bereits aktiv | Service={session.LegD.InternalNumber ?? "-"}");
                    return false;
                }

                session.LegD.CallId = CallProperties.CreateNewCallId();
                session.LegD.ServiceLeg = serviceLeg;
                session.LegD.ServiceType = serviceLeg.ServiceType;
                session.LegD.ServiceCode = serviceLeg.ServiceCode;
                session.LegD.InternalNumber = serviceLeg.InternalNumber;
                session.LegD.DisplayName = serviceLeg.DisplayName;
                session.LegD.AutoAnswer = serviceLeg.AutoAnswer;
                session.LegD.KeepSessionUntilPeerEnded = serviceLeg.KeepSessionUntilPeerEnded;
                session.LegD.IsEnded = false;
                session.LegD.UpdatedAt = DateTime.Now;
                session.HasAttachedServiceLegD = true;
                session.UpdatedAt = DateTime.Now;
            }

            RegisterAlias(session.LegD.CallId, session.SessionId);
            _logger?.Info($"Service LegD an bestehende Session angehaengt | SessionID={session.SessionId} | LegD={session.LegD.CallId ?? "-"} | Service={serviceLeg.InternalNumber} | Type={serviceLeg.ServiceType}");
            return true;
        }

        /// <summary>
        /// The EnsureInboundGsmSession
        /// </summary>
        /// <param name="currentSessionId">The currentSessionId<see cref="string?"/></param>
        /// <param name="statusId">The statusId<see cref="int"/></param>
        /// <param name="fromNumber">The fromNumber<see cref="string"/></param>
        /// <param name="toNumber">The toNumber<see cref="string"/></param>
        /// <param name="modemId">The modemId<see cref="int?"/></param>
        /// <param name="simId">The simId<see cref="int?"/></param>
        /// <param name="providerName">The providerName<see cref="string?"/></param>
        /// <param name="imei">The imei<see cref="string?"/></param>
        /// <param name="comPortAT">The comPortAT<see cref="string?"/></param>
        /// <param name="comPortAudio">The comPortAudio<see cref="string?"/></param>
        /// <param name="lastSignal">The lastSignal<see cref="string?"/></param>
        /// <param name="lastClip">The lastClip<see cref="string?"/></param>
        /// <param name="inboundRoute">The inboundRoute<see cref="SIPIntResolvedTarget?"/></param>
        /// <returns>The <see cref="SIP_ServerSession"/></returns>
        public SIP_ServerSession EnsureInboundGsmSession(string? currentSessionId, int statusId, string fromNumber, string toNumber, int? modemId, int? simId, string? providerName, string? imei, string? comPortAT, string? comPortAudio, string? lastSignal, string? lastClip, SIPIntResolvedTarget? inboundRoute = null) {
            if (!string.IsNullOrWhiteSpace(currentSessionId) && TryGet(currentSessionId, out SIP_ServerSession? existing) && existing != null && existing.IsGsmSession && existing.GsmStatusId == statusId && !existing.IsTerminated && !IsFinalGsmState(existing.Status)) {
                return UpsertGsmMonitorSession(existing.SessionId, "Inbound", fromNumber, toNumber, statusId, modemId, simId, providerName, imei, comPortAT, comPortAudio, lastSignal, lastClip, inboundRoute);
            }

            string legACallId = CallProperties.CreateNewCallId();
            string legBCallId = CallProperties.CreateNewCallId();
            string sessionId = CreateSessionId(legACallId, legBCallId);

            return UpsertGsmMonitorSession(sessionId, "Inbound", fromNumber, toNumber, statusId, modemId, simId, providerName, imei, comPortAT, comPortAudio, lastSignal, lastClip, inboundRoute, legACallId, legBCallId);
        }

        /// <summary>
        /// The CreateGsmInboundForkBranch
        /// </summary>
        /// <param name="rootSession">The rootSession<see cref="SIP_ServerSession"/></param>
        /// <param name="target">The target<see cref="SIPIntResolvedTarget"/></param>
        /// <returns>The <see cref="SIP_ServerSession"/></returns>
        public SIP_ServerSession CreateGsmInboundForkBranch(SIP_ServerSession rootSession, SIPIntResolvedTarget target) {
            if (rootSession == null) throw new ArgumentNullException(nameof(rootSession));
            if (target == null || !target.Success || !target.IsUserTarget) throw new ArgumentException("GSM-Fork-Ziel ist kein gültiges SIP-User-Ziel.", nameof(target));

            string legACallId = !string.IsNullOrWhiteSpace(rootSession.LegA.CallId)
                ? rootSession.LegA.CallId.Trim()
                : (!string.IsNullOrWhiteSpace(rootSession.LegACallId) ? rootSession.LegACallId.Trim() : CallProperties.CreateNewCallId());
            string legBCallId = CallProperties.CreateNewCallId();
            string sessionId = CreateSessionId(legACallId, legBCallId);

            SIP_ServerSession session = new SIP_ServerSession {
                SessionId = sessionId,
                LegACallId = legACallId,
                BridgeId = CreateDeterministicGuid($"BRIDGE|{sessionId}"),
                FromNumber = rootSession.FromNumber,
                ToNumber = rootSession.ToNumber,
                FromUsername = rootSession.FromUsername,
                ToUsername = target.TargetUsername ?? rootSession.ToUsername,
                Status = "Ringing",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                IsGsmSession = true,
                GsmDirection = "Inbound",
                GsmStatusId = rootSession.GsmStatusId,
                GsmModemId = rootSession.GsmModemId,
                GsmSimId = rootSession.GsmSimId,
                ProviderName = rootSession.ProviderName,
                GsmComPortAT = rootSession.GsmComPortAT,
                GsmComPortAudio = rootSession.GsmComPortAudio,
                GsmImei = rootSession.GsmImei,
                GsmLastSignal = rootSession.GsmLastSignal,
                GsmLastClip = rootSession.GsmLastClip,
                GsmLastEventAt = rootSession.GsmLastEventAt,
                GsmRouteKind = target.TargetKind,
                GsmRouteReason = target.Reason ?? rootSession.GsmRouteReason,
                GsmMappedAt = DateTime.Now,
                GsmTargetUserId = target.TargetUserId,
                TargetUserId = target.TargetUserId,
                RouteTargetKind = target.TargetKind,
                ParentSessionId = rootSession.SessionId
            };

            session.LegA.CallId = legACallId;
            session.LegA.UpdatedAt = DateTime.Now;
            session.LegB.CallId = legBCallId;
            session.LegB.TargetContactUri = target.ContactUri;
            session.LegB.UpdatedAt = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(target.ContactUri)) {
                try { session.LegB.RequestUri = SIPURI.ParseSIPURIRelaxed(target.ContactUri); } catch { session.LegB.RequestUri = null; }
            }
            if (!string.IsNullOrWhiteSpace(target.RemoteEndPoint)) {
                session.LegB.SipEndPoint = SIPEndPoint.TryParse(target.RemoteEndPoint);
            }

            StoreAndAlias(session, false);
            _logger?.Info($"GSM Fork Branch erstellt | RootSessionID={rootSession.SessionId} | BranchSessionID={session.SessionId} | LegA={session.LegA.CallId} | LegB={session.LegB.CallId} | UserID={target.TargetUserId} | Contact={target.ContactUri} | EndPoint={target.RemoteEndPoint}");
            return session;
        }

        /// <summary>
        /// The UpsertInboundMonitorSession
        /// </summary>
        /// <param name="monitor">The monitor<see cref="TKBase_GsmGateway.ModemRuntimeMonitor"/></param>
        /// <param name="fromNumber">The fromNumber<see cref="string"/></param>
        /// <param name="toNumber">The toNumber<see cref="string"/></param>
        /// <param name="signalLine">The signalLine<see cref="string"/></param>
        /// <returns>The <see cref="TKGSM_Session"/></returns>
        internal TKGSM_Session UpsertInboundMonitorSession(TKBase_GsmGateway.ModemRuntimeMonitor monitor, string fromNumber, string toNumber, string signalLine) {
            TKSipExt_Routing inboundRouting = new TKSipExt_Routing(_logger);
            SIPIntResolvedTarget route = inboundRouting.ResolveInboundTarget(toNumber);
            SIP_ServerSession session = EnsureInboundGsmSession(monitor.ActiveSessionId, monitor.StatusId, fromNumber, toNumber, monitor.ModemId, monitor.SimId, monitor.ProviderName, monitor.IMEI, monitor.ComPortAT, monitor.ComPortAudio, signalLine, fromNumber, route);
            monitor.ActiveSessionId = session.SessionId;
            return TKGSM_Session.FromServerSession(session);
        }

        /// <summary>
        /// The UpsertGsmMonitorSession
        /// </summary>
        /// <param name="sessionId">The sessionId<see cref="string"/></param>
        /// <param name="direction">The direction<see cref="string"/></param>
        /// <param name="fromNumber">The fromNumber<see cref="string"/></param>
        /// <param name="toNumber">The toNumber<see cref="string"/></param>
        /// <param name="statusId">The statusId<see cref="int"/></param>
        /// <param name="modemId">The modemId<see cref="int?"/></param>
        /// <param name="simId">The simId<see cref="int?"/></param>
        /// <param name="providerName">The providerName<see cref="string?"/></param>
        /// <param name="imei">The imei<see cref="string?"/></param>
        /// <param name="comPortAT">The comPortAT<see cref="string?"/></param>
        /// <param name="comPortAudio">The comPortAudio<see cref="string?"/></param>
        /// <param name="lastSignal">The lastSignal<see cref="string?"/></param>
        /// <param name="lastClip">The lastClip<see cref="string?"/></param>
        /// <param name="inboundRoute">The inboundRoute<see cref="SIPIntResolvedTarget?"/></param>
        /// <param name="legACallId">The legACallId<see cref="string?"/></param>
        /// <param name="legBCallId">The legBCallId<see cref="string?"/></param>
        /// <returns>The <see cref="SIP_ServerSession"/></returns>
        public SIP_ServerSession UpsertGsmMonitorSession(string sessionId, string direction, string fromNumber, string toNumber, int statusId, int? modemId, int? simId, string? providerName, string? imei, string? comPortAT, string? comPortAudio, string? lastSignal, string? lastClip, SIPIntResolvedTarget? inboundRoute = null, string? legACallId = null, string? legBCallId = null) {
            string normalizedLegACallId = string.IsNullOrWhiteSpace(legACallId) ? CallProperties.CreateNewCallId() : legACallId.Trim();
            string normalizedLegBCallId = string.IsNullOrWhiteSpace(legBCallId) ? CallProperties.CreateNewCallId() : legBCallId.Trim();
            string effectiveSessionId = string.IsNullOrWhiteSpace(sessionId) ? CreateSessionId(normalizedLegACallId, normalizedLegBCallId) : sessionId.Trim();

            SIP_ServerSession session = _sessions.AddOrUpdate(
                effectiveSessionId,
                _ => new SIP_ServerSession { SessionId = effectiveSessionId, CallId = effectiveSessionId, LegACallId = normalizedLegACallId, FromNumber = fromNumber ?? string.Empty, ToNumber = toNumber ?? string.Empty, FromUsername = fromNumber ?? string.Empty, ToUsername = inboundRoute?.TargetUsername ?? toNumber ?? string.Empty, Status = "Ringing", CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now, IsGsmSession = true },
                (_, existing) => existing);

            lock (session.SyncRoot) {
                session.IsGsmSession = true;
                session.GsmDirection = direction ?? string.Empty;
                session.FromNumber = fromNumber ?? string.Empty;
                session.ToNumber = toNumber ?? string.Empty;
                session.FromUsername = string.IsNullOrWhiteSpace(session.FromUsername) ? fromNumber ?? string.Empty : session.FromUsername;
                session.ToUsername = string.IsNullOrWhiteSpace(session.ToUsername) ? inboundRoute?.TargetUsername ?? toNumber ?? string.Empty : session.ToUsername;
                session.GsmStatusId = statusId;
                session.GsmModemId = modemId;
                session.GsmSimId = simId;
                session.ProviderName = providerName ?? string.Empty;
                session.GsmImei = imei ?? string.Empty;
                session.GsmComPortAT = comPortAT ?? string.Empty;
                session.GsmComPortAudio = comPortAudio ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(lastSignal)) session.GsmLastSignal = lastSignal;
                if (!string.IsNullOrWhiteSpace(lastClip)) session.GsmLastClip = lastClip;
                session.GsmLastEventAt = DateTime.Now;
                session.UpdatedAt = DateTime.Now;

                if (string.IsNullOrWhiteSpace(session.LegACallId)) session.LegACallId = normalizedLegACallId;
                if (string.IsNullOrWhiteSpace(session.LegA.CallId)) session.LegA.CallId = normalizedLegACallId;
                if (string.IsNullOrWhiteSpace(session.LegB.CallId)) session.LegB.CallId = normalizedLegBCallId;
                session.LegA.UpdatedAt = DateTime.Now;
                session.LegB.UpdatedAt = DateTime.Now;

                if (inboundRoute != null) {
                    session.GsmRouteKind = inboundRoute.TargetKind;
                    session.GsmRouteReason = inboundRoute.Success ? string.Empty : inboundRoute.Reason ?? string.Empty;
                    session.GsmMappedAt = DateTime.Now;
                    session.GsmTargetUserId = inboundRoute.TargetUserId;
                    session.ToUsername = string.IsNullOrWhiteSpace(session.ToUsername) ? inboundRoute.TargetUsername ?? session.ToUsername : session.ToUsername;
                    session.LegB.TargetContactUri = inboundRoute.ContactUri;
                    if (!string.IsNullOrWhiteSpace(inboundRoute.ContactUri)) {
                        try { session.LegB.RequestUri = SIPURI.ParseSIPURIRelaxed(inboundRoute.ContactUri); } catch { session.LegB.RequestUri = null; }
                    }
                    if (!string.IsNullOrWhiteSpace(inboundRoute.RemoteEndPoint)) {
                        SIPEndPoint? ep = SIPEndPoint.TryParse(inboundRoute.RemoteEndPoint);
                        session.LegB.SipEndPoint = ep;
                    }
                }
            }

            StoreAndAlias(session, true);
            return session;
        }

        /// <summary>
        /// The TryGet
        /// </summary>
        /// <param name="callIdOrSessionId">The callIdOrSessionId<see cref="string"/></param>
        /// <param name="session">The session<see cref="SIP_ServerSession?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public bool TryGet(string callIdOrSessionId, out SIP_ServerSession? session) {
            session = null;
            if (string.IsNullOrWhiteSpace(callIdOrSessionId)) return false;
            if (_sessions.TryGetValue(callIdOrSessionId, out SIP_ServerSession? direct) && direct != null) { session = direct; return true; }
            if (_sessionAliases.TryGetValue(callIdOrSessionId, out string? rootId) && !string.IsNullOrWhiteSpace(rootId) && _sessions.TryGetValue(rootId, out SIP_ServerSession? aliased) && aliased != null) { session = aliased; return true; }
            return false;
        }

        /// <summary>
        /// The RegisterAlias
        /// </summary>
        /// <param name="alias">The alias<see cref="string?"/></param>
        /// <param name="sessionId">The sessionId<see cref="string"/></param>
        public void RegisterAlias(string? alias, string sessionId) {
            if (!string.IsNullOrWhiteSpace(alias) && !string.IsNullOrWhiteSpace(sessionId)) _sessionAliases[alias.Trim()] = sessionId;
        }

        /// <summary>
        /// The RemapAlias
        /// </summary>
        /// <param name="alias">The alias<see cref="string?"/></param>
        /// <param name="sessionId">The sessionId<see cref="string"/></param>
        public void RemapAlias(string? alias, string sessionId) {
            RegisterAlias(alias, sessionId);
        }

        /// <summary>
        /// The RegisterLegBCallId
        /// </summary>
        /// <param name="sessionId">The sessionId<see cref="string"/></param>
        /// <param name="legBCallId">The legBCallId<see cref="string?"/></param>
        public void RegisterLegBCallId(string sessionId, string? legBCallId) {
            if (string.IsNullOrWhiteSpace(legBCallId) || !TryGet(sessionId, out SIP_ServerSession? session) || session == null) return;
            lock (session.SyncRoot) { session.LegB.CallId = legBCallId; session.UpdatedAt = DateTime.Now; }
            RegisterAlias(legBCallId, session.SessionId);
        }

        /// <summary>
        /// The MarkStatus
        /// </summary>
        /// <param name="callIdOrSessionId">The callIdOrSessionId<see cref="string"/></param>
        /// <param name="status">The status<see cref="string"/></param>
        public void MarkStatus(string callIdOrSessionId, string status) {
            if (TryGet(callIdOrSessionId, out SIP_ServerSession? session) && session != null) lock (session.SyncRoot) { session.Status = status; session.UpdatedAt = DateTime.Now; }
        }

        /// <summary>
        /// The MarkAnswered
        /// </summary>
        /// <param name="callIdOrSessionId">The callIdOrSessionId<see cref="string"/></param>
        public void MarkAnswered(string callIdOrSessionId) {
            if (TryGet(callIdOrSessionId, out SIP_ServerSession? session) && session != null) lock (session.SyncRoot) { session.IsAnswered = true; session.Status = "Answered"; session.UpdatedAt = DateTime.Now; }
        }

        /// <summary>
        /// The MarkLegEnded
        /// </summary>
        /// <param name="callIdOrSessionId">The callIdOrSessionId<see cref="string"/></param>
        /// <param name="legA">The legA<see cref="bool"/></param>
        public void MarkLegEnded(string callIdOrSessionId, bool legA) {
            if (!TryGet(callIdOrSessionId, out SIP_ServerSession? session) || session == null) return;
            lock (session.SyncRoot) { if (legA) session.LegA.IsEnded = true; else session.LegB.IsEnded = true; session.UpdatedAt = DateTime.Now; }
        }

        /// <summary>
        /// The MarkAllLegsEnded
        /// </summary>
        /// <param name="callIdOrSessionId">The callIdOrSessionId<see cref="string"/></param>
        /// <param name="status">The status<see cref="string?"/></param>
        public void MarkAllLegsEnded(string callIdOrSessionId, string? status = null) {
            if (!TryGet(callIdOrSessionId, out SIP_ServerSession? session) || session == null) return;
            lock (session.SyncRoot) { session.LegA.IsEnded = true; session.LegB.IsEnded = true; session.LegD.IsEnded = true; session.IsTerminated = true; if (!string.IsNullOrWhiteSpace(status)) session.Status = status; session.UpdatedAt = DateTime.Now; }
        }

        /// <summary>
        /// The MarkGsmLegConnected
        /// </summary>
        /// <param name="callIdOrSessionId">The callIdOrSessionId<see cref="string"/></param>
        public void MarkGsmLegConnected(string callIdOrSessionId) {
            if (!TryGet(callIdOrSessionId, out SIP_ServerSession? session) || session == null) return;
            lock (session.SyncRoot) { session.Status = "Answered"; session.IsAnswered = true; session.GsmLastEventAt = DateTime.Now; session.LegA.IsEnded = false; session.LegB.IsEnded = false; session.UpdatedAt = DateTime.Now; }
        }

        /// <summary>
        /// The MarkGsmRinging
        /// </summary>
        /// <param name="callIdOrSessionId">The callIdOrSessionId<see cref="string"/></param>
        /// <param name="signalLine">The signalLine<see cref="string?"/></param>
        public void MarkGsmRinging(string callIdOrSessionId, string? signalLine = null) {
            if (!TryGet(callIdOrSessionId, out SIP_ServerSession? session) || session == null) return;
            lock (session.SyncRoot) { session.Status = "Ringing"; if (!string.IsNullOrWhiteSpace(signalLine)) session.GsmLastSignal = signalLine; session.GsmLastEventAt = DateTime.Now; session.UpdatedAt = DateTime.Now; }
        }

        /// <summary>
        /// The SetGsmEnded
        /// </summary>
        /// <param name="callIdOrSessionId">The callIdOrSessionId<see cref="string"/></param>
        /// <param name="status">The status<see cref="string?"/></param>
        /// <param name="signalLine">The signalLine<see cref="string?"/></param>
        public void SetGsmEnded(string callIdOrSessionId, string? status, string? signalLine = null) {
            if (!TryGet(callIdOrSessionId, out SIP_ServerSession? session) || session == null) return;
            lock (session.SyncRoot) { session.Status = string.IsNullOrWhiteSpace(status) ? "Ended" : status.Trim(); if (!string.IsNullOrWhiteSpace(signalLine)) session.GsmLastSignal = signalLine; session.GsmLastEventAt = DateTime.Now; session.LegA.IsEnded = true; session.LegB.IsEnded = true; session.UpdatedAt = DateTime.Now; }
        }

        /// <summary>
        /// The ApplySignalStateToExistingSession
        /// </summary>
        /// <param name="sessionId">The sessionId<see cref="string"/></param>
        /// <param name="coreState">The coreState<see cref="string"/></param>
        /// <param name="signalLine">The signalLine<see cref="string?"/></param>
        public void ApplySignalStateToExistingSession(string sessionId, string coreState, string? signalLine) {
            if (TryGet(sessionId, out SIP_ServerSession? session) && session != null) ApplySignalStateToExistingSession(session, coreState, signalLine);
        }

        /// <summary>
        /// The ApplySignalStateToExistingSession
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="coreState">The coreState<see cref="string"/></param>
        /// <param name="signalLine">The signalLine<see cref="string?"/></param>
        public void ApplySignalStateToExistingSession(SIP_ServerSession session, string coreState, string? signalLine) {
            string normalizedState = (coreState ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedState)) return;
            lock (session.SyncRoot) {
                session.Status = normalizedState;
                if (!string.IsNullOrWhiteSpace(signalLine)) session.GsmLastSignal = signalLine.Trim();
                session.GsmLastEventAt = DateTime.Now;
                bool final = IsFinalGsmState(normalizedState);
                session.LegA.IsEnded = final;
                session.LegB.IsEnded = final;
                if (string.Equals(normalizedState, "Answered", StringComparison.OrdinalIgnoreCase) || string.Equals(normalizedState, "Connected", StringComparison.OrdinalIgnoreCase)) session.IsAnswered = true;
                session.UpdatedAt = DateTime.Now;
            }
        }

        /// <summary>
        /// The UpdateExistingSessionCallerInfo
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="callerNumber">The callerNumber<see cref="string"/></param>
        /// <param name="signalLine">The signalLine<see cref="string?"/></param>
        public void UpdateExistingSessionCallerInfo(SIP_ServerSession session, string callerNumber, string? signalLine) {
            if (string.IsNullOrWhiteSpace(callerNumber)) return;
            lock (session.SyncRoot) { session.FromNumber = callerNumber.Trim(); session.GsmLastClip = callerNumber.Trim(); if (!string.IsNullOrWhiteSpace(signalLine)) session.GsmLastSignal = signalLine.Trim(); session.GsmLastEventAt = DateTime.Now; session.UpdatedAt = DateTime.Now; }
        }

        /// <summary>
        /// The UpdateConnectedPartyInfo
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="connectedNumber">The connectedNumber<see cref="string"/></param>
        /// <param name="signalLine">The signalLine<see cref="string?"/></param>
        public void UpdateConnectedPartyInfo(SIP_ServerSession session, string connectedNumber, string? signalLine) {
            if (string.IsNullOrWhiteSpace(connectedNumber)) return;
            lock (session.SyncRoot) { if (string.Equals(session.GsmDirection, "Outbound", StringComparison.OrdinalIgnoreCase)) session.ToNumber = connectedNumber.Trim(); else session.FromNumber = connectedNumber.Trim(); session.GsmLastClip = connectedNumber.Trim(); if (!string.IsNullOrWhiteSpace(signalLine)) session.GsmLastSignal = signalLine.Trim(); session.GsmLastEventAt = DateTime.Now; session.UpdatedAt = DateTime.Now; }
        }

        /// <summary>
        /// The Remove
        /// </summary>
        /// <param name="callIdOrSessionId">The callIdOrSessionId<see cref="string"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public bool Remove(string callIdOrSessionId) {
            if (!TryGet(callIdOrSessionId, out SIP_ServerSession? session) || session == null) return false;
            bool removed = _sessions.TryRemove(session.SessionId, out SIP_ServerSession? removedSession);
            if (removedSession != null) {
                removedSession.Relay?.Dispose();
                removedSession.Relay = null;
                removedSession.GsmAudioBridge?.Dispose();
                removedSession.GsmAudioBridge = null;
                removedSession.GsmAudioBridgeStarting = false;
                removedSession.GsmAudioPrepared = false;
                if (removedSession.GsmStatusId.HasValue) TKBase_GsmGateway.TryReleaseAudioRoute(removedSession.GsmStatusId.Value);
            }
            foreach (string alias in _sessionAliases.Where(x => string.Equals(x.Value, session.SessionId, StringComparison.OrdinalIgnoreCase)).Select(x => x.Key).ToList()) _sessionAliases.TryRemove(alias, out _);
            _logger?.Info($"Session entfernt | SessionID={session.SessionId} | Removed={removed}");
            return removed;
        }

        /// <summary>
        /// The GetSnapshot
        /// </summary>
        /// <returns>The <see cref="List{SIP_ServerSession}"/></returns>
        public List<SIP_ServerSession> GetSnapshot() => _sessions.Values.Where(x => x != null).OrderByDescending(x => x.UpdatedAt).ToList();

        /// <summary>
        /// The StoreAndAlias
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="registerLegAAlias">The registerLegAAlias<see cref="bool"/></param>
        private void StoreAndAlias(SIP_ServerSession session, bool registerLegAAlias) {
            _sessions[session.SessionId] = session;
            RegisterAlias(session.SessionId, session.SessionId);
            RegisterAlias(session.LegB.CallId, session.SessionId);
            RegisterAlias(session.LegD.CallId, session.SessionId);
            if (registerLegAAlias) {
                RegisterAlias(session.LegACallId, session.SessionId);
                RegisterAlias(session.LegA.CallId, session.SessionId);
            }
        }

        /// <summary>
        /// The ApplyRoute
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="route">The route<see cref="SIPIntResolvedTarget?"/></param>
        private static void ApplyRoute(SIP_ServerSession session, SIPIntResolvedTarget? route) {
            if (route == null) return;
            session.RouteTargetKind = route.TargetKind;
            session.TargetUserId = route.TargetUserId;
            session.ToUsername = string.IsNullOrWhiteSpace(session.ToUsername) ? route.TargetUsername ?? session.ToUsername : session.ToUsername;
            session.RouteReason = route.Reason;
            session.ProviderAccountId = route.ProviderAccountId;
            session.ProviderName = route.ProviderName;
            session.ProviderAuthUser = route.ProviderAuthUser;
            session.ProviderSipUser = route.ProviderSipUser;
            session.ProviderPassword = route.ProviderPassword;
            session.ProviderNumber = route.ProviderNumber;
            session.ProviderServiceRoute = route.ProviderServiceRoute;
            session.ProviderAssociatedUri = route.ProviderAssociatedUri;
            session.ProviderRequestUri = route.ProviderRequestUri;
        }

        /// <summary>
        /// The IsFinalGsmState
        /// </summary>
        /// <param name="status">The status<see cref="string?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool IsFinalGsmState(string? status) {
            string normalized = (status ?? string.Empty).Trim();
            return string.Equals(normalized, "Ended", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Hangup", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Cancelled", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Busy", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "NoAnswer", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "NO CARRIER", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "BUSY", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "NO ANSWER", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("Failed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "DialFailed", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The CreateSessionId
        /// </summary>
        /// <param name="legACallId">The legACallId<see cref="string"/></param>
        /// <param name="legBOrDCallId">The legBOrDCallId<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string CreateSessionId(string legACallId, string legBOrDCallId) => CreateDeterministicGuid($"{legACallId?.Trim() ?? string.Empty}|{legBOrDCallId?.Trim() ?? string.Empty}").ToString("N");

        /// <summary>
        /// The CreateDeterministicGuid
        /// </summary>
        /// <param name="value">The value<see cref="string"/></param>
        /// <returns>The <see cref="Guid"/></returns>
        private static Guid CreateDeterministicGuid(string value) {
            using MD5 md5 = MD5.Create(); return new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty)));
        }
    }

    /// <summary>
    /// Defines the <see cref="SIP_ServerSession" />
    /// </summary>
    public sealed class SIP_ServerSession {
        /// <summary>
        /// Gets the SyncRoot
        /// </summary>
        internal object SyncRoot { get; } = new();

        /// <summary>
        /// Gets or sets the SessionId
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the CallId
        /// </summary>
        public string CallId { get => SessionId; set => SessionId = value; }

        /// <summary>
        /// Gets or sets the BridgeId
        /// </summary>
        public Guid? BridgeId { get; set; }

        /// <summary>
        /// Gets or sets the LegACallId
        /// </summary>
        public string LegACallId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the FromNumber
        /// </summary>
        public string FromNumber { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ToNumber
        /// </summary>
        public string ToNumber { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the FromUsername
        /// </summary>
        public string? FromUsername { get; set; }

        /// <summary>
        /// Gets or sets the ToUsername
        /// </summary>
        public string? ToUsername { get; set; }

        /// <summary>
        /// Gets or sets the Status
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the CreatedAt
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the UpdatedAt
        /// </summary>
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Gets or sets the LegA
        /// </summary>
        public SIP_ServerLegA LegA { get; set; } = new();

        /// <summary>
        /// Gets or sets the LegB
        /// </summary>
        public SIP_ServerLegB LegB { get; set; } = new();

        /// <summary>
        /// Gets or sets the LegD
        /// </summary>
        public SIP_ServerLegD LegD { get; set; } = new();

        /// <summary>
        /// Gets or sets the RouteTargetKind
        /// </summary>
        public SIPIntRouteTargetKind? RouteTargetKind { get; set; }

        /// <summary>
        /// Gets or sets the RouteReason
        /// </summary>
        public string? RouteReason { get; set; }

        /// <summary>
        /// Gets or sets the TargetUserId
        /// </summary>
        public int? TargetUserId { get; set; }

        /// <summary>
        /// Gets or sets the ProviderAccountId
        /// </summary>
        public int? ProviderAccountId { get; set; }

        /// <summary>
        /// Gets or sets the ProviderName
        /// </summary>
        public string? ProviderName { get; set; }

        /// <summary>
        /// Gets or sets the ProviderAuthUser
        /// </summary>
        public string? ProviderAuthUser { get; set; }

        /// <summary>
        /// Gets or sets the ProviderSipUser
        /// </summary>
        public string? ProviderSipUser { get; set; }

        /// <summary>
        /// Gets or sets the ProviderPassword
        /// </summary>
        public string? ProviderPassword { get; set; }

        /// <summary>
        /// Gets or sets the ProviderNumber
        /// </summary>
        public string? ProviderNumber { get; set; }

        /// <summary>
        /// Gets or sets the ProviderServiceRoute
        /// </summary>
        public string? ProviderServiceRoute { get; set; }

        /// <summary>
        /// Gets or sets the ProviderAssociatedUri
        /// </summary>
        public string? ProviderAssociatedUri { get; set; }

        /// <summary>
        /// Gets or sets the ProviderRequestUri
        /// </summary>
        public string? ProviderRequestUri { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether IsGsmSession
        /// </summary>
        public bool IsGsmSession { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether IsServiceSession
        /// </summary>
        public bool IsServiceSession { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether IsFaxSession
        /// </summary>
        public bool IsFaxSession { get; set; }

        /// <summary>
        /// Gets or sets the FaxDirection
        /// </summary>
        public string? FaxDirection { get; set; }

        /// <summary>
        /// Gets or sets the FaxTargetUserId
        /// </summary>
        public int? FaxTargetUserId { get; set; }

        /// <summary>
        /// Gets or sets the FaxUserNumber
        /// </summary>
        public string? FaxUserNumber { get; set; }

        /// <summary>
        /// Gets or sets the FaxUserName
        /// </summary>
        public string? FaxUserName { get; set; }

        /// <summary>
        /// Gets or sets the FaxMode
        /// </summary>
        public string? FaxMode { get; set; }

        /// <summary>
        /// Gets or sets the FaxStatus
        /// </summary>
        public string? FaxStatus { get; set; }

        /// <summary>
        /// Gets or sets the FaxLastError
        /// </summary>
        public string? FaxLastError { get; set; }

        /// <summary>
        /// Gets or sets the FaxJobId
        /// </summary>
        public string? FaxJobId { get; set; }

        /// <summary>
        /// Gets or sets the FaxLocalT38Address
        /// </summary>
        public string? FaxLocalT38Address { get; set; }

        /// <summary>
        /// Gets or sets the FaxLocalT38Port
        /// </summary>
        public int? FaxLocalT38Port { get; set; }

        /// <summary>
        /// Gets or sets the FaxRemoteT38Address
        /// </summary>
        public string? FaxRemoteT38Address { get; set; }

        /// <summary>
        /// Gets or sets the FaxRemoteT38Port
        /// </summary>
        public int? FaxRemoteT38Port { get; set; }

        /// <summary>
        /// Gets or sets the FaxLocalAudioAddress
        /// </summary>
        public string? FaxLocalAudioAddress { get; set; }

        /// <summary>
        /// Gets or sets the FaxLocalAudioPort
        /// </summary>
        public int? FaxLocalAudioPort { get; set; }

        /// <summary>
        /// Gets or sets the FaxRemoteAudioAddress
        /// </summary>
        public string? FaxRemoteAudioAddress { get; set; }

        /// <summary>
        /// Gets or sets the FaxRemoteAudioPort
        /// </summary>
        public int? FaxRemoteAudioPort { get; set; }

        /// <summary>
        /// Gets or sets the FaxAudioPayloadType
        /// </summary>
        public int? FaxAudioPayloadType { get; set; }

        /// <summary>
        /// Gets or sets the FaxAudioCodec
        /// </summary>
        public string? FaxAudioCodec { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether HasAttachedServiceLegD
        /// </summary>
        public bool HasAttachedServiceLegD { get; set; }

        /// <summary>
        /// Gets or sets the GsmStatusId
        /// </summary>
        public int? GsmStatusId { get; set; }

        /// <summary>
        /// Gets or sets the GsmModemId
        /// </summary>
        public int? GsmModemId { get; set; }

        /// <summary>
        /// Gets or sets the GsmSimId
        /// </summary>
        public int? GsmSimId { get; set; }

        /// <summary>
        /// Gets or sets the GsmDirection
        /// </summary>
        public string? GsmDirection { get; set; }

        /// <summary>
        /// Gets or sets the GsmImei
        /// </summary>
        public string? GsmImei { get; set; }

        /// <summary>
        /// Gets or sets the GsmComPortAT
        /// </summary>
        public string? GsmComPortAT { get; set; }

        /// <summary>
        /// Gets or sets the GsmComPortAudio
        /// </summary>
        public string? GsmComPortAudio { get; set; }

        /// <summary>
        /// Gets or sets the GsmLastSignal
        /// </summary>
        public string? GsmLastSignal { get; set; }

        /// <summary>
        /// Gets or sets the GsmLastClip
        /// </summary>
        public string? GsmLastClip { get; set; }

        /// <summary>
        /// Gets or sets the GsmLastEventAt
        /// </summary>
        public DateTime? GsmLastEventAt { get; set; }

        /// <summary>
        /// Gets or sets the GsmRouteKind
        /// </summary>
        public SIPIntRouteTargetKind? GsmRouteKind { get; set; }

        /// <summary>
        /// Gets or sets the GsmRouteReason
        /// </summary>
        public string? GsmRouteReason { get; set; }

        /// <summary>
        /// Gets or sets the GsmMappedAt
        /// </summary>
        public DateTime? GsmMappedAt { get; set; }

        /// <summary>
        /// Gets or sets the GsmTargetUserId
        /// </summary>
        public int? GsmTargetUserId { get; set; }

        /// <summary>
        /// Gets or sets the MediaAdvertisedIp
        /// </summary>
        public string? MediaAdvertisedIp { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether IsTerminated
        /// </summary>
        public bool IsTerminated { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether IsAnswered
        /// </summary>
        public bool IsAnswered { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether WaitingForFinalInviteAck
        /// </summary>
        public bool WaitingForFinalInviteAck { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether AckForwarded
        /// </summary>
        public bool AckForwarded { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether ByeForwarded
        /// </summary>
        public bool ByeForwarded { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether IsTerminating
        /// </summary>
        public bool IsTerminating { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether WaitingForByeResponse
        /// </summary>
        public bool WaitingForByeResponse { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether WaitingForPeerBye
        /// </summary>
        public bool WaitingForPeerBye { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether HasOffer
        /// </summary>
        public bool HasOffer { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether HasAnswer
        /// </summary>
        public bool HasAnswer { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether Established
        /// </summary>
        public bool Established { get; set; }

        /// <summary>
        /// Gets or sets the TerminationStartedAt
        /// </summary>
        public DateTime? TerminationStartedAt { get; set; }

        /// <summary>
        /// Gets or sets the LastByeSentAt
        /// </summary>
        public DateTime? LastByeSentAt { get; set; }

        /// <summary>
        /// Gets or sets the ByeRetryCount
        /// </summary>
        public int ByeRetryCount { get; set; }

        /// <summary>
        /// Gets or sets the TerminationReason
        /// </summary>
        public string? TerminationReason { get; set; }

        /// <summary>
        /// Gets or sets the TerminationSourceEndPoint
        /// </summary>
        public SIPEndPoint? TerminationSourceEndPoint { get; set; }

        /// <summary>
        /// Gets or sets the TerminationTargetEndPoint
        /// </summary>
        public SIPEndPoint? TerminationTargetEndPoint { get; set; }

        /// <summary>
        /// Gets or sets the TerminationLocalEndPoint
        /// </summary>
        public SIPEndPoint? TerminationLocalEndPoint { get; set; }

        /// <summary>
        /// Gets or sets the ParentSessionId
        /// </summary>
        public string? ParentSessionId { get; set; }

        /// <summary>
        /// Gets or sets the LastProvisionalResponse
        /// </summary>
        public SIPResponse? LastProvisionalResponse { get; set; }

        /// <summary>
        /// Gets or sets the LastSuccessResponse
        /// </summary>
        public SIPResponse? LastSuccessResponse { get; set; }

        /// <summary>
        /// Gets or sets the Relay
        /// </summary>
        internal TKBase_MediaManager? Relay { get; set; }

        /// <summary>
        /// Gets or sets the GsmAudioBridge
        /// </summary>
        internal TKGSM_Media? GsmAudioBridge { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether GsmAudioBridgeStarting
        /// </summary>
        internal bool GsmAudioBridgeStarting { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether GsmAudioPrepared
        /// </summary>
        internal bool GsmAudioPrepared { get; set; }

        /// <summary>
        /// Gets or sets the GsmSipCodec
        /// </summary>
        internal TKBase_AudioCodecConfig? GsmSipCodec { get; set; }

        /// <summary>
        /// Gets or sets the GsmBridgeCodec
        /// </summary>
        internal TKBase_AudioCodecConfig? GsmBridgeCodec { get; set; }
    }

    /// <summary>
    /// Defines the <see cref="SIP_ServerLegA" />
    /// </summary>
    public sealed class SIP_ServerLegA {
        /// <summary>
        /// Gets the Name
        /// </summary>
        public string Name => "LegA";

        /// <summary>
        /// Gets the Direction
        /// </summary>
        public string Direction => "Incoming";

        /// <summary>
        /// Gets or sets the CallId
        /// </summary>
        public string? CallId { get; set; }

        /// <summary>
        /// Gets or sets the SipEndPoint
        /// </summary>
        public SIPEndPoint? SipEndPoint { get; set; }

        /// <summary>
        /// Gets or sets the LocalSipEndPoint
        /// </summary>
        public SIPEndPoint? LocalSipEndPoint { get; set; }

        /// <summary>
        /// Gets or sets the MediaAdvertisedIp
        /// </summary>
        public string? MediaAdvertisedIp { get; set; }

        /// <summary>
        /// Gets or sets the RequestUri
        /// </summary>
        public SIPURI? RequestUri { get; set; }

        /// <summary>
        /// Gets or sets the ContactUri
        /// </summary>
        public SIPURI? ContactUri { get; set; }

        /// <summary>
        /// Gets or sets the InviteReceived
        /// </summary>
        public SIPRequest? InviteReceived { get; set; }

        /// <summary>
        /// Gets or sets the AckReceived
        /// </summary>
        public SIPRequest? AckReceived { get; set; }

        /// <summary>
        /// Gets or sets the ByeReceived
        /// </summary>
        public SIPRequest? ByeReceived { get; set; }

        /// <summary>
        /// Gets or sets the ByeSent
        /// </summary>
        public SIPRequest? ByeSent { get; set; }

        /// <summary>
        /// Gets or sets the CancelReceived
        /// </summary>
        public SIPRequest? CancelReceived { get; set; }

        /// <summary>
        /// Gets or sets the PendingReinvite
        /// </summary>
        public SIPRequest? PendingReinvite { get; set; }

        /// <summary>
        /// Gets or sets the LastRequestReceived
        /// </summary>
        public SIPRequest? LastRequestReceived { get; set; }

        /// <summary>
        /// Gets or sets the LastRequestSent
        /// </summary>
        public SIPRequest? LastRequestSent { get; set; }

        /// <summary>
        /// Gets or sets the LastProvisionalResponse
        /// </summary>
        public SIPResponse? LastProvisionalResponse { get; set; }

        /// <summary>
        /// Gets or sets the LastSuccessResponse
        /// </summary>
        public SIPResponse? LastSuccessResponse { get; set; }

        /// <summary>
        /// Gets or sets the LastResponseReceived
        /// </summary>
        public SIPResponse? LastResponseReceived { get; set; }

        /// <summary>
        /// Gets or sets the LastResponseSent
        /// </summary>
        public SIPResponse? LastResponseSent { get; set; }

        /// <summary>
        /// Gets or sets the InviteReceivedRaw
        /// </summary>
        public string? InviteReceivedRaw { get; set; }

        /// <summary>
        /// Gets or sets the AckReceivedRaw
        /// </summary>
        public string? AckReceivedRaw { get; set; }

        /// <summary>
        /// Gets or sets the ByeReceivedRaw
        /// </summary>
        public string? ByeReceivedRaw { get; set; }

        /// <summary>
        /// Gets or sets the ByeSentRaw
        /// </summary>
        public string? ByeSentRaw { get; set; }

        /// <summary>
        /// Gets or sets the CancelReceivedRaw
        /// </summary>
        public string? CancelReceivedRaw { get; set; }

        /// <summary>
        /// Gets or sets the LastRequestReceivedRaw
        /// </summary>
        public string? LastRequestReceivedRaw { get; set; }

        /// <summary>
        /// Gets or sets the LastRequestSentRaw
        /// </summary>
        public string? LastRequestSentRaw { get; set; }

        /// <summary>
        /// Gets or sets the LastResponseReceivedRaw
        /// </summary>
        public string? LastResponseReceivedRaw { get; set; }

        /// <summary>
        /// Gets or sets the LastResponseSentRaw
        /// </summary>
        public string? LastResponseSentRaw { get; set; }

        /// <summary>
        /// Gets or sets the PreparedAnswerSdp
        /// </summary>
        public string? PreparedAnswerSdp { get; set; }

        /// <summary>
        /// Gets or sets the FromHeader
        /// </summary>
        public string? FromHeader { get; set; }

        /// <summary>
        /// Gets or sets the ToHeader
        /// </summary>
        public string? ToHeader { get; set; }

        /// <summary>
        /// Gets or sets the FromTag
        /// </summary>
        public string? FromTag { get; set; }

        /// <summary>
        /// Gets or sets the ToTag
        /// </summary>
        public string? ToTag { get; set; }

        /// <summary>
        /// Gets or sets the DialogCSeq
        /// </summary>
        public int DialogCSeq { get; set; }

        /// <summary>
        /// Gets or sets the RemoteRtpEndPoint
        /// </summary>
        public IPEndPoint? RemoteRtpEndPoint { get; set; }

        /// <summary>
        /// Gets or sets the RelayPort
        /// </summary>
        public int RelayPort { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether InviteSeen
        /// </summary>
        public bool InviteSeen { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether AckSeen
        /// </summary>
        public bool AckSeen { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether ByeSeen
        /// </summary>
        public bool ByeSeen { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether CancelSeen
        /// </summary>
        public bool CancelSeen { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether IsEnded
        /// </summary>
        public bool IsEnded { get; set; }

        /// <summary>
        /// Gets or sets the CreatedAt
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets the UpdatedAt
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Defines the <see cref="SIP_ServerLegB" />
    /// </summary>
    public sealed class SIP_ServerLegB {
        /// <summary>
        /// Gets the Name
        /// </summary>
        public string Name => "LegB";

        /// <summary>
        /// Gets the Direction
        /// </summary>
        public string Direction => "Outgoing";

        /// <summary>
        /// Gets or sets the CallId
        /// </summary>
        public string? CallId { get; set; }

        /// <summary>
        /// Gets or sets the SipEndPoint
        /// </summary>
        public SIPEndPoint? SipEndPoint { get; set; }

        /// <summary>
        /// Gets or sets the LocalSipEndPoint
        /// </summary>
        public SIPEndPoint? LocalSipEndPoint { get; set; }

        /// <summary>
        /// Gets or sets the MediaAdvertisedIp
        /// </summary>
        public string? MediaAdvertisedIp { get; set; }

        /// <summary>
        /// Gets or sets the RequestUri
        /// </summary>
        public SIPURI? RequestUri { get; set; }

        /// <summary>
        /// Gets or sets the ContactUri
        /// </summary>
        public SIPURI? ContactUri { get; set; }

        /// <summary>
        /// Gets or sets the TargetContactUri
        /// </summary>
        public string? TargetContactUri { get; set; }

        /// <summary>
        /// Gets or sets the RemoteTargetUri
        /// </summary>
        public SIPURI? RemoteTargetUri { get; set; }

        /// <summary>
        /// Gets or sets the InviteSent
        /// </summary>
        public SIPRequest? InviteSent { get; set; }

        /// <summary>
        /// Gets or sets the AckReceived
        /// </summary>
        public SIPRequest? AckReceived { get; set; }

        /// <summary>
        /// Gets or sets the AckSent
        /// </summary>
        public SIPRequest? AckSent { get; set; }

        /// <summary>
        /// Gets or sets the ByeReceived
        /// </summary>
        public SIPRequest? ByeReceived { get; set; }

        /// <summary>
        /// Gets or sets the ByeSent
        /// </summary>
        public SIPRequest? ByeSent { get; set; }

        /// <summary>
        /// Gets or sets the CancelSent
        /// </summary>
        public SIPRequest? CancelSent { get; set; }

        /// <summary>
        /// Gets or sets the PendingReinvite
        /// </summary>
        public SIPRequest? PendingReinvite { get; set; }

        /// <summary>
        /// Gets or sets the LastRequestReceived
        /// </summary>
        public SIPRequest? LastRequestReceived { get; set; }

        /// <summary>
        /// Gets or sets the LastRequestSent
        /// </summary>
        public SIPRequest? LastRequestSent { get; set; }

        /// <summary>
        /// Gets or sets the LastProvisionalResponse
        /// </summary>
        public SIPResponse? LastProvisionalResponse { get; set; }

        /// <summary>
        /// Gets or sets the LastSuccessResponse
        /// </summary>
        public SIPResponse? LastSuccessResponse { get; set; }

        /// <summary>
        /// Gets or sets the LastResponseReceived
        /// </summary>
        public SIPResponse? LastResponseReceived { get; set; }

        /// <summary>
        /// Gets or sets the LastResponseSent
        /// </summary>
        public SIPResponse? LastResponseSent { get; set; }

        /// <summary>
        /// Gets or sets the InviteSentRaw
        /// </summary>
        public string? InviteSentRaw { get; set; }

        /// <summary>
        /// Gets or sets the AckReceivedRaw
        /// </summary>
        public string? AckReceivedRaw { get; set; }

        /// <summary>
        /// Gets or sets the AckSentRaw
        /// </summary>
        public string? AckSentRaw { get; set; }

        /// <summary>
        /// Gets or sets the ByeReceivedRaw
        /// </summary>
        public string? ByeReceivedRaw { get; set; }

        /// <summary>
        /// Gets or sets the ByeSentRaw
        /// </summary>
        public string? ByeSentRaw { get; set; }

        /// <summary>
        /// Gets or sets the CancelSentRaw
        /// </summary>
        public string? CancelSentRaw { get; set; }

        /// <summary>
        /// Gets or sets the LastRequestReceivedRaw
        /// </summary>
        public string? LastRequestReceivedRaw { get; set; }

        /// <summary>
        /// Gets or sets the LastRequestSentRaw
        /// </summary>
        public string? LastRequestSentRaw { get; set; }

        /// <summary>
        /// Gets or sets the LastResponseReceivedRaw
        /// </summary>
        public string? LastResponseReceivedRaw { get; set; }

        /// <summary>
        /// Gets or sets the LastResponseSentRaw
        /// </summary>
        public string? LastResponseSentRaw { get; set; }

        /// <summary>
        /// Gets or sets the PreparedOfferSdp
        /// </summary>
        public string? PreparedOfferSdp { get; set; }

        /// <summary>
        /// Gets or sets the FromHeader
        /// </summary>
        public string? FromHeader { get; set; }

        /// <summary>
        /// Gets or sets the ToHeader
        /// </summary>
        public string? ToHeader { get; set; }

        /// <summary>
        /// Gets or sets the FromTag
        /// </summary>
        public string? FromTag { get; set; }

        /// <summary>
        /// Gets or sets the ToTag
        /// </summary>
        public string? ToTag { get; set; }

        /// <summary>
        /// Gets or sets the DialogCSeq
        /// </summary>
        public int DialogCSeq { get; set; }

        /// <summary>
        /// Gets or sets the RemoteRtpEndPoint
        /// </summary>
        public IPEndPoint? RemoteRtpEndPoint { get; set; }

        /// <summary>
        /// Gets or sets the RelayPort
        /// </summary>
        public int RelayPort { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether InviteSeen
        /// </summary>
        public bool InviteSeen { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether AckSeen
        /// </summary>
        public bool AckSeen { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether ByeSeen
        /// </summary>
        public bool ByeSeen { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether CancelSeen
        /// </summary>
        public bool CancelSeen { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether IsEnded
        /// </summary>
        public bool IsEnded { get; set; }

        /// <summary>
        /// Gets or sets the CreatedAt
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets the UpdatedAt
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Defines the <see cref="SIP_ServerLegD" />
    /// </summary>
    public sealed class SIP_ServerLegD {
        /// <summary>
        /// Gets the Name
        /// </summary>
        public string Name => "LegD";

        /// <summary>
        /// Gets the Direction
        /// </summary>
        public string Direction => "Service";

        /// <summary>
        /// Gets or sets the CallId
        /// </summary>
        public string? CallId { get; set; }

        /// <summary>
        /// Gets or sets the SipEndPoint
        /// </summary>
        public SIPEndPoint? SipEndPoint { get; set; }

        /// <summary>
        /// Gets or sets the LocalSipEndPoint
        /// </summary>
        public SIPEndPoint? LocalSipEndPoint { get; set; }

        /// <summary>
        /// Gets or sets the MediaAdvertisedIp
        /// </summary>
        public string? MediaAdvertisedIp { get; set; }

        /// <summary>
        /// Gets or sets the RequestUri
        /// </summary>
        public SIPURI? RequestUri { get; set; }

        /// <summary>
        /// Gets or sets the ContactUri
        /// </summary>
        public SIPURI? ContactUri { get; set; }

        /// <summary>
        /// Gets or sets the AckReceived
        /// </summary>
        public SIPRequest? AckReceived { get; set; }

        /// <summary>
        /// Gets or sets the ByeSent
        /// </summary>
        public SIPRequest? ByeSent { get; set; }

        /// <summary>
        /// Gets or sets the LastRequestReceived
        /// </summary>
        public SIPRequest? LastRequestReceived { get; set; }

        /// <summary>
        /// Gets or sets the LastRequestSent
        /// </summary>
        public SIPRequest? LastRequestSent { get; set; }

        /// <summary>
        /// Gets or sets the LastResponseSent
        /// </summary>
        public SIPResponse? LastResponseSent { get; set; }

        /// <summary>
        /// Gets or sets the AckReceivedRaw
        /// </summary>
        public string? AckReceivedRaw { get; set; }

        /// <summary>
        /// Gets or sets the ByeSentRaw
        /// </summary>
        public string? ByeSentRaw { get; set; }

        /// <summary>
        /// Gets or sets the LastRequestReceivedRaw
        /// </summary>
        public string? LastRequestReceivedRaw { get; set; }

        /// <summary>
        /// Gets or sets the LastRequestSentRaw
        /// </summary>
        public string? LastRequestSentRaw { get; set; }

        /// <summary>
        /// Gets or sets the LastResponseSentRaw
        /// </summary>
        public string? LastResponseSentRaw { get; set; }

        /// <summary>
        /// Gets or sets the PreparedAnswerSdp
        /// </summary>
        public string? PreparedAnswerSdp { get; set; }

        /// <summary>
        /// Gets or sets the FromTag
        /// </summary>
        public string? FromTag { get; set; }

        /// <summary>
        /// Gets or sets the ToTag
        /// </summary>
        public string? ToTag { get; set; }

        /// <summary>
        /// Gets or sets the DialogCSeq
        /// </summary>
        public int DialogCSeq { get; set; }

        /// <summary>
        /// Gets or sets the RemoteRtpEndPoint
        /// </summary>
        public IPEndPoint? RemoteRtpEndPoint { get; set; }

        /// <summary>
        /// Gets or sets the RelayPort
        /// </summary>
        public int RelayPort { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether InviteSeen
        /// </summary>
        public bool InviteSeen { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether AckSeen
        /// </summary>
        public bool AckSeen { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether ByeSeen
        /// </summary>
        public bool ByeSeen { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether IsEnded
        /// </summary>
        public bool IsEnded { get; set; }

        /// <summary>
        /// Gets or sets the ServiceLeg
        /// </summary>
        public TKSVC_Session? ServiceLeg { get; set; }

        /// <summary>
        /// Gets or sets the ServiceType
        /// </summary>
        public string? ServiceType { get; set; }

        /// <summary>
        /// Gets or sets the ServiceCode
        /// </summary>
        public string? ServiceCode { get; set; }

        /// <summary>
        /// Gets or sets the InternalNumber
        /// </summary>
        public string? InternalNumber { get; set; }

        /// <summary>
        /// Gets or sets the DisplayName
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether AutoAnswer
        /// </summary>
        public bool AutoAnswer { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether KeepSessionUntilPeerEnded
        /// </summary>
        public bool KeepSessionUntilPeerEnded { get; set; }

        /// <summary>
        /// Gets or sets the CreatedAt
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets the UpdatedAt
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

}
