/*
 * TKSipExt_CallController
 * Aufgabe:
 * - Eigenständige Gesprächssteuerung für den externen SIP-Bereich.
 * - Verarbeitet INVITE, ACK, BYE, CANCEL und Responses für SipExt.
 * - Nutzt TKBase_SessionManager nur zur Session-Erzeugung und Session-Suche.
 * - Nutzt TKSipExt_Commands, TKSipExt_Events und TKSipExt_Media als eigenen Bereichsunterbau.
 *
 * Diese information darf nicht gelöscht werden!
 */

namespace TKServer.SipExt {
    using SIPSorcery.SIP;
    using System;
    using System.Data;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading.Tasks;

    using TKServer.BASE;
    using TKServer.SQL;
    /// TKSipExt_CallController
    /// Aufgabe:
    /// - Eigenständige Gesprächssteuerung für den externen SIP-Bereich.
    /// - Verarbeitet INVITE, ACK, BYE, CANCEL und Responses für SipExt.
    /// - Nutzt TKBase_SessionManager nur zur Session-Erzeugung und Session-Suche.
    /// - Nutzt TKSipExt_Commands, TKSipExt_Events und TKSipExt_Media als eigenen Bereichsunterbau.
    /// 
    /// Diese information darf nicht gelöscht werden!

    /// <summary>
    /// Defines the <see cref="TKSipExt_CallController" />
    /// </summary>
    internal sealed class TKSipExt_CallController {
        /// <summary>
        /// Defines the _logger
        /// </summary>
        private readonly TKBase_LogManager? _logger;

        /// <summary>
        /// Defines the _sipSql
        /// </summary>
        private readonly TKSQL_Commands _sipSql;

        /// <summary>
        /// Defines the _sessions
        /// </summary>
        private readonly TKBase_SessionManager _sessions;

        /// <summary>
        /// Defines the _routing
        /// </summary>
        private readonly TKBase_RoutingManager _routing;

        /// <summary>
        /// Defines the _mediaManager
        /// </summary>
        private readonly TKBase_MediaManager _mediaManager;

        /// <summary>
        /// Defines the _commands
        /// </summary>
        private readonly TKSipExt_Commands _commands;

        /// <summary>
        /// Defines the _events
        /// </summary>
        private readonly TKSipExt_Events _events;

        /// <summary>
        /// Defines the _media
        /// </summary>
        private readonly TKSipExt_Media _media;

        /// <summary>
        /// Defines the _settings
        /// </summary>
        private readonly TKBase_ServerSettings _settings;

        /// <summary>
        /// Initializes a new instance of the <see cref="TKSipExt_CallController"/> class.
        /// </summary>
        /// <param name="sessions">The sessions<see cref="TKBase_SessionManager"/></param>
        /// <param name="routing">The routing<see cref="TKBase_RoutingManager"/></param>
        /// <param name="settings">The settings<see cref="TKBase_ServerSettings"/></param>
        /// <param name="mediaManager">The mediaManager<see cref="TKBase_MediaManager"/></param>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        public TKSipExt_CallController(
            TKBase_SessionManager sessions,
            TKBase_RoutingManager routing,
            TKBase_ServerSettings settings,
            TKBase_MediaManager mediaManager,
            TKBase_LogManager? logger = null) {

            _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            _routing = routing ?? throw new ArgumentNullException(nameof(routing));
            _logger = logger;
            _settings = settings;
            _sipSql = new TKSQL_Commands(logger);
            _mediaManager = mediaManager ?? throw new ArgumentNullException(nameof(mediaManager));
            _media = new TKSipExt_Media(_mediaManager, logger);
            _commands = new TKSipExt_Commands(logger);
            _events = new TKSipExt_Events(logger);
        }

        /// <summary>
        /// The OwnsCall
        /// </summary>
        /// <param name="callId">The callId<see cref="string?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public bool OwnsCall(string? callId) {
            if (string.IsNullOrWhiteSpace(callId)) {
                return false;
            }

            return _sessions.TryGet(callId, out SIP_ServerSession? session) && session != null && IsSipExtSession(session) && !string.Equals(session.RouteTargetKind?.ToString(), "FaxUser", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The StopOwnedSessions
        /// </summary>
        public void StopOwnedSessions() {
            foreach (SIP_ServerSession session in _sessions.GetSnapshot()) {
                if (IsSipExtSession(session)) {
                    _media.StopRelayAndReleasePorts(session);
                }
            }
        }

        /// <summary>
        /// The HangupSessionAsync
        /// </summary>
        /// <param name="transport">The transport<see cref="SIPTransport"/></param>
        /// <param name="sessionId">The sessionId<see cref="string"/></param>
        /// <returns>The <see cref="Task{bool}"/></returns>
        public async Task<bool> HangupSessionAsync(SIPTransport transport, string sessionId) {
            if (transport == null || string.IsNullOrWhiteSpace(sessionId)) return false;
            if (!_sessions.TryGet(sessionId, out SIP_ServerSession? session) || session == null || !IsSipExtSession(session)) return false;

            try {
                if (session.LegA.SipEndPoint != null && session.LegA.LocalSipEndPoint != null && session.LegA.InviteReceived != null) {
                    if (session.IsAnswered && session.LegA.LastSuccessResponse != null) {
                        SIPRequest byeA = _commands.LegA.BuildDialogRequest(session, SIPMethodsEnum.BYE, session.LegA.LocalSipEndPoint);
                        _commands.LegA.StoreByeSent(session, byeA, session.LegA.SipEndPoint, session.LegA.LocalSipEndPoint);
                        string controlByeLegARawController = byeA.ToString();
                        _logger?.Debug($"SipExt Control BYE OUT LegA | Ebene=Controller | SessionID={session.SessionId} | CallID={byeA.Header?.CallId} | Ziel={session.LegA.SipEndPoint} | RawLength={controlByeLegARawController.Length}{Environment.NewLine}--- SIP RAW START SipExt CONTROLLER CONTROL BYE OUT LEGA ---{Environment.NewLine}{controlByeLegARawController}{Environment.NewLine}--- SIP RAW END SipExt CONTROLLER CONTROL BYE OUT LEGA ---");
                        await transport.SendRequestAsync(session.LegA.SipEndPoint, byeA);
                    } else {
                        SIPResponse unavailable = _commands.LegA.BuildResponseFromInvite(session, SIPResponseStatusCodesEnum.TemporarilyUnavailable, "Call ended by control");
                        await SendLegAResponseAsync(transport, session, unavailable);
                    }
                }

                if (session.LegB.SipEndPoint != null && session.LegB.LocalSipEndPoint != null && session.LegB.InviteSent != null) {
                    SIPMethodsEnum method = session.IsAnswered && session.LegB.LastSuccessResponse != null ? SIPMethodsEnum.BYE : SIPMethodsEnum.CANCEL;
                    SIPRequest requestB = _commands.LegB.BuildDialogRequest(session, method, session.LegB.LocalSipEndPoint);
                    if (method == SIPMethodsEnum.BYE) _commands.LegB.StoreByeSent(session, requestB, session.LegB.SipEndPoint, session.LegB.LocalSipEndPoint);
                    else _commands.LegB.StoreCancelSent(session, requestB, session.LegB.SipEndPoint, session.LegB.LocalSipEndPoint);
                    string controlRequestBRawController = requestB.ToString();
                    _logger?.Debug($"SipExt Control {method} OUT LegB | Ebene=Controller | SessionID={session.SessionId} | CallID={requestB.Header?.CallId} | Ziel={session.LegB.SipEndPoint} | RawLength={controlRequestBRawController.Length}{Environment.NewLine}--- SIP RAW START SipExt CONTROLLER CONTROL REQUEST OUT LEGB ---{Environment.NewLine}{controlRequestBRawController}{Environment.NewLine}--- SIP RAW END SipExt CONTROLLER CONTROL REQUEST OUT LEGB ---");
                    await transport.SendRequestAsync(session.LegB.SipEndPoint, requestB);
                }

                _media.StopRelayAndReleasePorts(session);
                _sessions.MarkAllLegsEnded(session.SessionId, "ControlHangup");
                TKBase_CallManager.MarkRuntimeEnded(session.SessionId, "ControlHangup");
                return true;
            } catch (Exception ex) {
                _logger?.Error($"SipExt Control-Hangup fehlgeschlagen | SessionID={sessionId} | Fehler={ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// The CanHandleInvite
        /// </summary>
        /// <param name="request">The request<see cref="SIPRequest"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public bool CanHandleInvite(SIPRequest request) {
            string fromNumber = _commands.LegA.ExtractFromNumber(request);
            string toNumber = _commands.LegA.ExtractToNumber(request);
            string legACallId = request.Header?.CallId ?? CallProperties.CreateNewCallId();
            SIP_DialPlanDecision decision = _routing.CreateDecision(fromNumber, toNumber);
            SIPIntResolvedTarget route = ResolveInviteRoute(legACallId, fromNumber, toNumber, decision);

            if (decision.IsFaxPath || decision.SourceType != TKBase_DialSourceType.Internal) {
                return false;
            }

            return route.Success && route.IsExternalTarget;
        }

        /// <summary>
        /// The HandleInviteAsync
        /// </summary>
        /// <param name="transport">The transport<see cref="SIPTransport"/></param>
        /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="remoteEndPoint">The remoteEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="request">The request<see cref="SIPRequest"/></param>
        /// <param name="receivedLocalEndPoint">The receivedLocalEndPoint<see cref="SIPEndPoint"/></param>
        /// <returns>The <see cref="Task"/></returns>
        
        public async Task HandleInviteAsync(SIPTransport transport, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIPRequest request, SIPEndPoint receivedLocalEndPoint) {
            SIP_ServerSession? session = null;
            string legACallId = request.Header?.CallId ?? CallProperties.CreateNewCallId();
            string fromNumber = string.Empty;
            string toNumber = string.Empty;

            try {
                fromNumber = _commands.LegA.ExtractFromNumber(request);
                toNumber = _commands.LegA.ExtractToNumber(request);
                string inviteRawController = request.ToString();
                // voraussichtlich debug level
                _logger?.Debug($"SipExt INVITE IN | Ebene=Controller | CallID={legACallId} | Von={fromNumber} | An={toNumber} | Local={localEndPoint} | ReceivedLocal={receivedLocalEndPoint} | Remote={remoteEndPoint} | URI={request.URI} | BodyLength={request.Body?.Length ?? 0} | RawLength={inviteRawController.Length}{Environment.NewLine}--- SIP RAW START SipExt CONTROLLER INVITE IN ---{Environment.NewLine}{inviteRawController}{Environment.NewLine}--- SIP RAW END SipExt CONTROLLER INVITE IN ---");
                _logger?.Info($"SipExt INVITE IN kurz | CallID={legACallId} | Von={fromNumber} | An={toNumber} | Remote={remoteEndPoint} | URI={request.URI}");
                SIP_DialPlanDecision decision = _routing.CreateDecision(fromNumber, toNumber);
                SIPIntResolvedTarget route = ResolveInviteRoute(legACallId, fromNumber, toNumber, decision);

                if (!route.Success) {
                    _logger?.Warn($"SipExt INVITE abgewiesen | LegA={legACallId} | Von={fromNumber} | An={toNumber} | Grund={route.Reason}");
                    await SendSimpleResponseSafeAsync(transport, request, SIPResponseStatusCodesEnum.TemporarilyUnavailable, route.Reason, "SipExt INVITE abgewiesen");
                    return;
                }

                if (decision.IsFaxPath || route.IsFaxTarget) {
                    _logger?.Warn($"SipExt INVITE abgewiesen | LegA={legACallId} | Von={fromNumber} | An={toNumber} | Grund=Fax-Pfad gehört in TKFax_CallController");
                    await SendSimpleResponseSafeAsync(transport, request, SIPResponseStatusCodesEnum.TemporarilyUnavailable, "Fax-Pfad gehört in den Fax-Bereich.", "SipExt Fax-Pfad abgewiesen");
                    return;
                }

                if (!route.IsExternalTarget) {
                    _logger?.Warn($"SipExt INVITE abgewiesen | LegA={legACallId} | Von={fromNumber} | An={toNumber} | SourceType={decision.SourceType} | TargetType={decision.TargetType} | Grund=Kein SipExt-Ziel");
                    await SendSimpleResponseSafeAsync(transport, request, SIPResponseStatusCodesEnum.TemporarilyUnavailable, "Kein SipExt-Ziel.", "SipExt INVITE ohne SipExt-Ziel");
                    return;
                }

                SIPEndPoint targetEndPoint = _commands.ResolveRouteEndPoint(route);
                session = _sessions.CreateInviteSession(legACallId, fromNumber, toNumber, request, remoteEndPoint, localEndPoint, targetEndPoint, route.ContactUri, route, parentSessionId: null);
                ApplySipExtContactUsers(session, fromNumber, toNumber, route, decision);

                SIPEndPoint legBLocalEndPoint = BuildLocalEndPointForRemote(targetEndPoint);
                ApplyLegMediaAddresses(session, receivedLocalEndPoint, remoteEndPoint, legBLocalEndPoint, targetEndPoint, route, toNumber);

                _commands.LegA.StoreInviteReceived(session, request, remoteEndPoint, localEndPoint);
                _events.LegA.HandleInviteReceived(session.SessionId, session.LegA.CallId, session.LegA.InviteReceivedRaw);
                TKBase_CallManager.StartRuntimeCall(session, legACallId, fromNumber, toNumber, route);

                if (!string.IsNullOrWhiteSpace(request.Body)) {
                    _media.PrepareCallerMediaLeg(session, request);
                }

                SIPResponse trying = SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.Trying, null);
                await SendLegAResponseAsync(transport, session, trying);

                SIPRequest outboundInvite = _commands.LegB.BuildProviderInvite(session, request, route, targetEndPoint, legBLocalEndPoint);
                _media.ApplyPreparedOfferSdp(session, outboundInvite);
                _commands.LegB.StoreInviteSent(session, outboundInvite, targetEndPoint, legBLocalEndPoint);
                _events.LegB.HandleInviteOutbound(session.SessionId, session.LegB.CallId, session.LegB.InviteSentRaw);

                string outboundInviteRawController = outboundInvite.ToString();
                // voraussichtlich debug level
                _logger?.Debug($"SipExt INVITE OUT | Ebene=Controller | SessionID={session.SessionId} | LegA={session.LegA.CallId} | LegB={session.LegB.CallId} | Ziel={targetEndPoint} | Provider={session.ProviderName} | RawLength={outboundInviteRawController.Length}{Environment.NewLine}--- SIP RAW START SipExt CONTROLLER INVITE OUT ---{Environment.NewLine}{outboundInviteRawController}{Environment.NewLine}--- SIP RAW END SipExt CONTROLLER INVITE OUT ---");
                _logger?.Info($"SipExt INVITE OUT kurz | SessionID={session.SessionId} | LegA={session.LegA.CallId} | LegB={session.LegB.CallId} | Ziel={targetEndPoint} | Provider={session.ProviderName}");
                await transport.SendRequestAsync(targetEndPoint, outboundInvite);
                _logger?.Info($"SipExt INVITE weitergeleitet | Ebene=Controller | SessionID={session.SessionId} | LegA={session.LegA.CallId} | LegB={session.LegB.CallId} | Ziel={targetEndPoint} | Provider={session.ProviderName}");
            } catch (Exception ex) {
                _logger?.Error($"SipExt INVITE Verarbeitung fehlgeschlagen | LegA={legACallId} | Von={fromNumber} | An={toNumber} | Fehler={ex.Message}");
                if (session != null) {
                    await TrySendInviteFailureToLegAAsync(transport, session, SIPResponseStatusCodesEnum.TemporarilyUnavailable, "SipExt INVITE Fehler");
                    SafeStopRelayAndReleasePorts(session, "SipExt INVITE Fehler");
                    SafeMarkAllLegsEnded(session, "InviteFailed", "SipExt INVITE Fehler");
                } else {
                    await SendSimpleResponseSafeAsync(transport, request, SIPResponseStatusCodesEnum.TemporarilyUnavailable, "SipExt INVITE Fehler", "SipExt INVITE Fehler ohne Session");
                }
            }
        }


        /// <summary>
        /// The HandleAckAsync
        /// </summary>
        /// <param name="transport">The transport<see cref="SIPTransport"/></param>
        /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="remoteEndPoint">The remoteEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="request">The request<see cref="SIPRequest"/></param>
        /// <returns>The <see cref="Task"/></returns>
        public async Task HandleAckAsync(SIPTransport transport, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIPRequest request) {
            string callId = request.Header?.CallId ?? string.Empty;
            if (!_sessions.TryGet(callId, out SIP_ServerSession? session) || session == null || !IsSipExtSession(session)) {
                return;
            }

            bool fromLegA = IsRequestFromLegA(session, request, remoteEndPoint);
            if (fromLegA) {
                _commands.LegA.StoreAckReceived(session, request, remoteEndPoint, localEndPoint);
                if (session.AckForwarded) {
                    _logger?.Info($"SipExt ACK IN LegA verarbeitet | Ebene=Controller | SessionID={session.SessionId} | LegB ACK war bereits gesendet");
                    return;
                }
                if (session.LegB.SipEndPoint != null && session.LegB.LastSuccessResponse != null) {
                    SIPRequest ack = _commands.LegB.BuildAckForConfirmedDialog(session, session.LegB.LastSuccessResponse, session.LegB.LocalSipEndPoint ?? localEndPoint);
                    _commands.LegB.StoreAckSent(session, ack, session.LegB.SipEndPoint, session.LegB.LocalSipEndPoint ?? localEndPoint);
                    string ackToLegBRawController = ack.ToString();
                    _logger?.Debug($"SipExt ACK OUT LegB | Ebene=Controller | SessionID={session.SessionId} | CallID={ack.Header?.CallId} | Ziel={session.LegB.SipEndPoint} | RawLength={ackToLegBRawController.Length}{Environment.NewLine}--- SIP RAW START SipExt CONTROLLER ACK OUT LEGB ---{Environment.NewLine}{ackToLegBRawController}{Environment.NewLine}--- SIP RAW END SipExt CONTROLLER ACK OUT LEGB ---");
                    await transport.SendRequestAsync(session.LegB.SipEndPoint, ack);
                    session.AckForwarded = true;
                }
                return;
            }

            _commands.LegB.StoreAckReceived(session, request, remoteEndPoint, localEndPoint);
        }

        /// <summary>
        /// The HandleByeAsync
        /// </summary>
        /// <param name="transport">The transport<see cref="SIPTransport"/></param>
        /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="remoteEndPoint">The remoteEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="bye">The bye<see cref="SIPRequest"/></param>
        /// <returns>The <see cref="Task"/></returns>
        
        public async Task HandleByeAsync(SIPTransport transport, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIPRequest bye) {
            string callId = bye.Header?.CallId ?? string.Empty;
            if (!_sessions.TryGet(callId, out SIP_ServerSession? session) || session == null || !IsSipExtSession(session)) {
                await SendSimpleResponseSafeAsync(transport, bye, SIPResponseStatusCodesEnum.CallLegTransactionDoesNotExist, null, "SipExt BYE ohne Session");
                return;
            }

            try {
                bool fromLegA = IsRequestFromLegA(session, bye, remoteEndPoint);
                SIPResponse ok = SIPResponse.GetResponse(bye, SIPResponseStatusCodesEnum.Ok, null);
                string byeOkRawController = ok.ToString();
                // voraussichtlich debug level
                _logger?.Debug($"SipExt BYE 200 OK OUT | Ebene=Controller | SessionID={session.SessionId} | CallID={ok.Header?.CallId} | Ziel={remoteEndPoint} | RawLength={byeOkRawController.Length}{Environment.NewLine}--- SIP RAW START SipExt CONTROLLER BYE 200 OK OUT ---{Environment.NewLine}{byeOkRawController}{Environment.NewLine}--- SIP RAW END SipExt CONTROLLER BYE 200 OK OUT ---");
                _logger?.Info($"SipExt BYE 200 OK OUT kurz | SessionID={session.SessionId} | CallID={ok.Header?.CallId} | Ziel={remoteEndPoint}");
                await transport.SendResponseAsync(ok);

                if (fromLegA) {
                    _commands.LegA.StoreByeReceived(session, bye, remoteEndPoint, localEndPoint);
                    _events.LegA.HandleByeIn(session.SessionId, session.LegA.CallId ?? callId, session.LegA.ByeReceivedRaw);
                    if (session.LegB.SipEndPoint != null) {
                        SIPRequest peerBye = _commands.LegB.BuildDialogRequest(session, SIPMethodsEnum.BYE, session.LegB.LocalSipEndPoint ?? localEndPoint);
                        _commands.LegB.StoreByeSent(session, peerBye, session.LegB.SipEndPoint, session.LegB.LocalSipEndPoint ?? localEndPoint);
                        _events.LegB.HandleByeOut(session.SessionId, session.LegB.CallId ?? session.SessionId, peerBye.ToString());
                        string peerByeLegBRawController = peerBye.ToString();
                        // voraussichtlich debug level
                        _logger?.Debug($"SipExt BYE OUT LegB | Ebene=Controller | SessionID={session.SessionId} | CallID={peerBye.Header?.CallId} | Ziel={session.LegB.SipEndPoint} | RawLength={peerByeLegBRawController.Length}{Environment.NewLine}--- SIP RAW START SipExt CONTROLLER BYE OUT LEGB ---{Environment.NewLine}{peerByeLegBRawController}{Environment.NewLine}--- SIP RAW END SipExt CONTROLLER BYE OUT LEGB ---");
                        _logger?.Info($"SipExt BYE OUT LegB kurz | SessionID={session.SessionId} | CallID={peerBye.Header?.CallId} | Ziel={session.LegB.SipEndPoint}");
                        await transport.SendRequestAsync(session.LegB.SipEndPoint, peerBye);
                    }
                } else {
                    _commands.LegB.StoreByeReceived(session, bye, remoteEndPoint, localEndPoint);
                    _events.LegB.HandleEnded(session.SessionId, "LegB BYE");
                    if (session.LegA.SipEndPoint != null) {
                        SIPRequest peerBye = _commands.LegA.BuildDialogRequest(session, SIPMethodsEnum.BYE, session.LegA.LocalSipEndPoint ?? localEndPoint);
                        _commands.LegA.StoreByeSent(session, peerBye, session.LegA.SipEndPoint, session.LegA.LocalSipEndPoint ?? localEndPoint);
                        string peerByeLegARawController = peerBye.ToString();
                        // voraussichtlich debug level
                        _logger?.Debug($"SipExt BYE OUT LegA | Ebene=Controller | SessionID={session.SessionId} | CallID={peerBye.Header?.CallId} | Ziel={session.LegA.SipEndPoint} | RawLength={peerByeLegARawController.Length}{Environment.NewLine}--- SIP RAW START SipExt CONTROLLER BYE OUT LEGA ---{Environment.NewLine}{peerByeLegARawController}{Environment.NewLine}--- SIP RAW END SipExt CONTROLLER BYE OUT LEGA ---");
                        _logger?.Info($"SipExt BYE OUT LegA kurz | SessionID={session.SessionId} | CallID={peerBye.Header?.CallId} | Ziel={session.LegA.SipEndPoint}");
                        await transport.SendRequestAsync(session.LegA.SipEndPoint, peerBye);
                    }
                }
            } catch (Exception ex) {
                _logger?.Error($"SipExt BYE Verarbeitung fehlgeschlagen | SessionID={session.SessionId} | CallID={callId} | Fehler={ex.Message}");
            } finally {
                SafeStopRelayAndReleasePorts(session, "SipExt BYE Cleanup");
                SafeMarkAllLegsEnded(session, "Ended", "SipExt BYE Cleanup");
            }
        }

        /// <summary>
        /// The HandleCancelAsync
        /// </summary>
        /// <param name="transport">The transport<see cref="SIPTransport"/></param>
        /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="remoteEndPoint">The remoteEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="request">The request<see cref="SIPRequest"/></param>
        /// <returns>The <see cref="Task"/></returns>
        
        public async Task HandleCancelAsync(SIPTransport transport, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIPRequest request) {
            SIP_ServerSession? session = null;
            try {
string callId = request.Header?.CallId ?? string.Empty;
                if (!_sessions.TryGet(callId, out session) || session == null || !IsSipExtSession(session)) {
                    await SendSimpleResponseSafeAsync(transport, request, SIPResponseStatusCodesEnum.CallLegTransactionDoesNotExist, null, "SipExt CANCEL ohne Session");
                    return;
                }

                _commands.LegA.StoreCancelReceived(session, request, remoteEndPoint, localEndPoint);
                MarkCancelBeforeAnswerStarted(session, "SipExt LegA CANCEL", remoteEndPoint, localEndPoint);
                _events.LegA.HandleCancelIn(session.SessionId, "SipExt LegA CANCEL vor Annahme");
                await SendSimpleResponseSafeAsync(transport, request, SIPResponseStatusCodesEnum.Ok, null, "SipExt CANCEL 200 OK");

                if (session.LegB.SipEndPoint != null && session.LegB.InviteSent != null) {
                    SIPRequest cancel = _commands.LegB.BuildDialogRequest(session, SIPMethodsEnum.CANCEL, session.LegB.LocalSipEndPoint ?? localEndPoint);
                    _commands.LegB.StoreCancelSent(session, cancel, session.LegB.SipEndPoint, session.LegB.LocalSipEndPoint ?? localEndPoint);
                    string cancelLegBRawController = cancel.ToString();
                    // voraussichtlich debug level
                    _logger?.Debug($"SipExt CANCEL OUT LegB | Ebene=Controller | SessionID={session.SessionId} | CallID={cancel.Header?.CallId} | Ziel={session.LegB.SipEndPoint} | RawLength={cancelLegBRawController.Length}{Environment.NewLine}--- SIP RAW START SipExt CONTROLLER CANCEL OUT LEGB ---{Environment.NewLine}{cancelLegBRawController}{Environment.NewLine}--- SIP RAW END SipExt CONTROLLER CANCEL OUT LEGB ---");
                    _logger?.Info($"SipExt CANCEL OUT LegB kurz | SessionID={session.SessionId} | CallID={cancel.Header?.CallId} | Ziel={session.LegB.SipEndPoint}");
                    await transport.SendRequestAsync(session.LegB.SipEndPoint, cancel);
                }

                if (session.LegA.InviteReceived != null) {
                    SIPResponse terminated = _commands.LegA.BuildResponseFromInvite(session, SIPResponseStatusCodesEnum.RequestTerminated, null);
                    await SendLegAResponseAsync(transport, session, terminated);
                }
            } catch (Exception ex) {
                _logger?.Error($"SipExt CANCEL Verarbeitung fehlgeschlagen | SessionID={session?.SessionId ?? "-"} | CallID={request.Header?.CallId ?? "-"} | Fehler={ex.Message}");
            } finally {
                if (session != null) {
                    SafeStopRelayAndReleasePorts(session, "SipExt CANCEL Cleanup");
                    SafeMarkAllLegsEnded(session, "Cancelled", "SipExt CANCEL Cleanup");
                }
            }
        }

        /// <summary>
        /// The HandleResponseAsync
        /// </summary>
        /// <param name="transport">The transport<see cref="SIPTransport"/></param>
        /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="remoteEndPoint">The remoteEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="response">The response<see cref="SIPResponse"/></param>
        /// <returns>The <see cref="Task"/></returns>
        
        public async Task HandleResponseAsync(SIPTransport transport, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIPResponse response) {
            SIP_ServerSession? session = null;
            try {
string callId = response.Header?.CallId ?? string.Empty;
                if (!_sessions.TryGet(callId, out session) || session == null || !IsSipExtSession(session)) {
                    return;
                }

                bool responseFromLegA = string.Equals(callId, session.LegA.CallId, StringComparison.OrdinalIgnoreCase)
                    || _commands.SameSipEndPoint(remoteEndPoint, session.LegA.SipEndPoint);
                if (responseFromLegA) {
                    _commands.LegA.StoreResponseReceived(session, response, remoteEndPoint, localEndPoint);
                    return;
                }

                _commands.LegB.StoreResponseReceived(session, response, remoteEndPoint, localEndPoint);

                SIPMethodsEnum cseqMethod = response.Header?.CSeqMethod ?? SIPMethodsEnum.NONE;
                if (cseqMethod == SIPMethodsEnum.BYE || cseqMethod == SIPMethodsEnum.CANCEL) {
                    _logger?.Info($"SipExt LegB RESPONSE für Dialog-Ende verarbeitet | SessionID={session.SessionId} | CSeq={response.Header?.CSeq} {cseqMethod} | Status={response.StatusCode} {response.ReasonPhrase}");
                    return;
                }

                if (cseqMethod != SIPMethodsEnum.INVITE) {
                    _logger?.Info($"SipExt LegB RESPONSE ignoriert | SessionID={session.SessionId} | CSeq={response.Header?.CSeq} {cseqMethod} | Status={response.StatusCode} {response.ReasonPhrase}");
                    return;
                }

                if (IsInviteCancelBeforeAnswerActive(session)) {
                    await HandleLegBInviteResponseAfterCancelAsync(transport, localEndPoint, remoteEndPoint, session, response);
                    return;
                }

                if ((response.Status == SIPResponseStatusCodesEnum.ProxyAuthenticationRequired || response.Status == SIPResponseStatusCodesEnum.Unauthorised) && session.LegB.InviteSent?.Header?.HasAuthenticationHeader != true) {
                    SIPRequest challengeAck = _commands.LegB.BuildAckForFailedInviteResponse(session, response, session.LegB.LocalSipEndPoint ?? localEndPoint);
                    SIPEndPoint challengeTarget = session.LegB.SipEndPoint ?? remoteEndPoint;
                    string challengeAckRaw = challengeAck.ToString();
                    // voraussichtlich debug level
                    _logger?.Debug($"SipExt ACK OUT LegB Challenge | Ebene=Controller | SessionID={session.SessionId} | CallID={challengeAck.Header?.CallId} | Ziel={challengeTarget} | RawLength={challengeAckRaw.Length}{Environment.NewLine}--- SIP RAW START SipExt CONTROLLER CHALLENGE ACK OUT LEGB ---{Environment.NewLine}{challengeAckRaw}{Environment.NewLine}--- SIP RAW END SipExt CONTROLLER CHALLENGE ACK OUT LEGB ---");
                    _logger?.Info($"SipExt ACK OUT LegB Challenge kurz | SessionID={session.SessionId} | CallID={challengeAck.Header?.CallId} | Ziel={challengeTarget}");
                    await transport.SendRequestAsync(challengeTarget, challengeAck);

                    SIPRequest authInvite = _commands.LegB.BuildAuthenticatedInvite(session, response, localEndPoint);
                    SIPEndPoint authTarget = session.LegB.SipEndPoint ?? remoteEndPoint;
                    _commands.LegB.StoreInviteSent(session, authInvite, authTarget, localEndPoint);
                    _events.LegB.HandleAuthInviteOutbound(session.SessionId, authInvite.Header?.CallId, authInvite.ToString());
                    string authInviteRawController = authInvite.ToString();
                    // voraussichtlich debug level
                    _logger?.Debug($"SipExt AUTH INVITE OUT | Ebene=Controller | SessionID={session.SessionId} | CallID={authInvite.Header?.CallId} | Ziel={authTarget} | RawLength={authInviteRawController.Length}{Environment.NewLine}--- SIP RAW START SipExt CONTROLLER AUTH INVITE OUT ---{Environment.NewLine}{authInviteRawController}{Environment.NewLine}--- SIP RAW END SipExt CONTROLLER AUTH INVITE OUT ---");
                    _logger?.Info($"SipExt AUTH INVITE OUT kurz | SessionID={session.SessionId} | CallID={authInvite.Header?.CallId} | Ziel={authTarget}");
                    await transport.SendRequestAsync(authTarget, authInvite);
                    return;
                }

                if (response.StatusCode >= 100 && response.StatusCode < 200) {
                    if (session.LegA.InviteReceived != null) {
                        if (!string.IsNullOrWhiteSpace(response.Body)) {
                            _media.UpdateCalleeMediaLeg(session, response.Body);
                        }

                        SIPResponse provisional = _commands.LegA.BuildResponseFromInvite(session, response.Status, response.ReasonPhrase, response);
                        _media.ApplyPreparedAnswerSdp(session, provisional);
                        await SendLegAResponseAsync(transport, session, provisional);
                        _events.LegB.HandleRingingReceived(session.SessionId);
                        TKBase_CallManager.MarkRuntimeRinging(session.SessionId);
                        if (!string.IsNullOrWhiteSpace(response.Body)) {
                            _media.StartRelayIfReady(session);
                        }
                    }
                    return;
                }

                if (response.StatusCode >= 200 && response.StatusCode < 300) {
                    bool alreadyAnswered = session.IsAnswered || session.AckForwarded;

                    SIPRequest ack = _commands.LegB.BuildAckForConfirmedDialog(session, response, session.LegB.LocalSipEndPoint ?? localEndPoint);
                    if (session.LegB.SipEndPoint != null) {
                        _commands.LegB.StoreAckSent(session, ack, session.LegB.SipEndPoint, session.LegB.LocalSipEndPoint ?? localEndPoint);
                        string answerAckLegBRawController = ack.ToString();
                        // voraussichtlich debug level
                        _logger?.Debug($"SipExt ACK OUT LegB Answer | Ebene=Controller | SessionID={session.SessionId} | CallID={ack.Header?.CallId} | Ziel={session.LegB.SipEndPoint} | RawLength={answerAckLegBRawController.Length}{Environment.NewLine}--- SIP RAW START SipExt CONTROLLER ANSWER ACK OUT LEGB ---{Environment.NewLine}{answerAckLegBRawController}{Environment.NewLine}--- SIP RAW END SipExt CONTROLLER ANSWER ACK OUT LEGB ---");
                        _logger?.Info($"SipExt ACK OUT LegB Answer kurz | SessionID={session.SessionId} | CallID={ack.Header?.CallId} | Ziel={session.LegB.SipEndPoint}");
                        await transport.SendRequestAsync(session.LegB.SipEndPoint, ack);
                        session.AckForwarded = true;
                    }

                    if (alreadyAnswered) {
                        _logger?.Info($"SipExt LegB 200 OK Wiederholung bestaetigt | SessionID={session.SessionId} | CallID={response.Header?.CallId} | CSeq={response.Header?.CSeq} {response.Header?.CSeqMethod}");
                        return;
                    }

                    if (!string.IsNullOrWhiteSpace(response.Body)) {
                        _media.HandleCalleeMediaLeg(session, response);
                    }

                    if (session.LegA.InviteReceived != null) {
                        SIPResponse ok = _commands.LegA.BuildResponseFromInvite(session, response.Status, response.ReasonPhrase, response);
                        _media.ApplyPreparedAnswerSdp(session, ok);
                        await SendLegAResponseAsync(transport, session, ok);
                    }

                    _media.StartRelayIfReady(session);
                    _sessions.MarkAnswered(session.SessionId);
                    _events.LegB.HandleAnsweredReceived(session.SessionId);
                    TKBase_CallManager.MarkRuntimeAnswered(session.SessionId);
                    return;
                }

                if (response.StatusCode >= 300) {
                    try {
                        SIPRequest failedAck = _commands.LegB.BuildAckForFailedInviteResponse(session, response, session.LegB.LocalSipEndPoint ?? localEndPoint);
                        if (session.LegB.SipEndPoint != null) {
                            string failedAckRawController = failedAck.ToString();
                            // voraussichtlich debug level
                            _logger?.Debug($"SipExt ACK OUT LegB FailedResponse | Ebene=Controller | SessionID={session.SessionId} | CallID={failedAck.Header?.CallId} | Ziel={session.LegB.SipEndPoint} | RawLength={failedAckRawController.Length}{Environment.NewLine}--- SIP RAW START SipExt CONTROLLER FAILED ACK OUT LEGB ---{Environment.NewLine}{failedAckRawController}{Environment.NewLine}--- SIP RAW END SipExt CONTROLLER FAILED ACK OUT LEGB ---");
                            _logger?.Info($"SipExt ACK OUT LegB FailedResponse kurz | SessionID={session.SessionId} | CallID={failedAck.Header?.CallId} | Ziel={session.LegB.SipEndPoint}");
                            await transport.SendRequestAsync(session.LegB.SipEndPoint, failedAck);
                        }

                        if (session.LegA.InviteReceived != null) {
                            SIPResponse fail = _commands.LegA.BuildResponseFromInvite(session, response.Status, response.ReasonPhrase, response);
                            await SendLegAResponseAsync(transport, session, fail);
                        }

                        _events.LegB.HandleFailedReceived(session.SessionId, response.ReasonPhrase);
                    } finally {
                        SafeStopRelayAndReleasePorts(session, "SipExt Response Fehler-Cleanup");
                        SafeMarkAllLegsEnded(session, "Failed", "SipExt Response Fehler-Cleanup");
                    }
                }
            } catch (Exception ex) {
                _logger?.Error($"SipExt RESPONSE Verarbeitung fehlgeschlagen | SessionID={session?.SessionId ?? "-"} | CallID={response.Header?.CallId ?? "-"} | Status={response.StatusCode} {response.ReasonPhrase} | Fehler={ex.Message}");
                if (session != null && response.StatusCode >= 200) {
                    SafeStopRelayAndReleasePorts(session, "SipExt RESPONSE Fehler");
                    SafeMarkAllLegsEnded(session, "ResponseError", "SipExt RESPONSE Fehler");
                }
            }
        }



        private static void MarkCancelBeforeAnswerStarted(SIP_ServerSession session, string reason, SIPEndPoint? sourceEndPoint, SIPEndPoint? localEndPoint) {
            if (session == null) return;
            lock (session.SyncRoot) {
                if (!session.IsAnswered) {
                    session.IsTerminating = true;
                    session.Status = "Cancelled";
                    session.TerminationReason = reason;
                    session.TerminationStartedAt ??= DateTime.Now;
                    session.TerminationSourceEndPoint = sourceEndPoint;
                    session.TerminationLocalEndPoint = localEndPoint;
                    session.LegA.IsEnded = true;
                }
                session.UpdatedAt = DateTime.Now;
            }
        }

        private static bool IsInviteCancelBeforeAnswerActive(SIP_ServerSession session) {
            if (session == null || session.IsAnswered) return false;
            return session.LegA.CancelSeen
                || session.LegB.CancelSeen
                || session.IsTerminating
                || session.IsTerminated
                || string.Equals(session.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)
                || string.Equals(session.Status, "ForkCancelled", StringComparison.OrdinalIgnoreCase);
        }

        private async Task HandleLegBInviteResponseAfterCancelAsync(SIPTransport transport, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIP_ServerSession session, SIPResponse response) {
            if (response.StatusCode < 200) {
                _logger?.Info($"SipExt LegB RESPONSE nach LegA-CANCEL ignoriert | SessionID={session.SessionId} | Status={response.StatusCode} {response.ReasonPhrase}");
                return;
            }

            if (response.StatusCode < 300) {
                SIPRequest ack = _commands.LegB.BuildAckForConfirmedDialog(session, response, session.LegB.LocalSipEndPoint ?? localEndPoint);
                SIPEndPoint target = session.LegB.SipEndPoint ?? remoteEndPoint;
                _commands.LegB.StoreAckSent(session, ack, target, session.LegB.LocalSipEndPoint ?? localEndPoint);
                await transport.SendRequestAsync(target, ack);

                if (session.LegB.ByeSent == null) {
                    SIPRequest bye = _commands.LegB.BuildDialogRequest(session, SIPMethodsEnum.BYE, session.LegB.LocalSipEndPoint ?? localEndPoint);
                    _commands.LegB.StoreByeSent(session, bye, target, session.LegB.LocalSipEndPoint ?? localEndPoint);
                    await transport.SendRequestAsync(target, bye);
                }

                _logger?.Info($"SipExt LegB 200 OK nach LegA-CANCEL mit ACK+BYE geschlossen | SessionID={session.SessionId} | CallID={response.Header?.CallId}");
                SafeStopRelayAndReleasePorts(session, "SipExt LegB 200 OK nach CANCEL");
                SafeMarkAllLegsEnded(session, "Cancelled", "SipExt LegB 200 OK nach CANCEL");
                return;
            }

            try {
                SIPRequest failedAck = _commands.LegB.BuildAckForFailedInviteResponse(session, response, session.LegB.LocalSipEndPoint ?? localEndPoint);
                SIPEndPoint target = session.LegB.SipEndPoint ?? remoteEndPoint;
                _commands.LegB.StoreAckSent(session, failedAck, target, session.LegB.LocalSipEndPoint ?? localEndPoint);
                await transport.SendRequestAsync(target, failedAck);
            } catch (Exception ex) {
                _logger?.Warn($"SipExt ACK nach LegA-CANCEL konnte nicht gesendet werden | SessionID={session.SessionId} | Status={response.StatusCode} {response.ReasonPhrase} | Fehler={ex.Message}");
            }

            _logger?.Info($"SipExt LegB Final RESPONSE nach LegA-CANCEL verarbeitet ohne Weiterleitung zu LegA | SessionID={session.SessionId} | Status={response.StatusCode} {response.ReasonPhrase}");
            SafeStopRelayAndReleasePorts(session, "SipExt LegB Final nach CANCEL");
            SafeMarkAllLegsEnded(session, "Cancelled", "SipExt LegB Final nach CANCEL");
        }

        private async Task SendSimpleResponseSafeAsync(SIPTransport transport, SIPRequest request, SIPResponseStatusCodesEnum status, string? reason, string context) {
            try {
                await SendSimpleResponseAsync(transport, request, status, reason);
            } catch (Exception ex) {
                _logger?.Warn($"{context} | Antwort konnte nicht gesendet werden | Status={(int)status} {status} | CallID={request.Header?.CallId ?? "-"} | Fehler={ex.Message}");
            }
        }

        private async Task TrySendInviteFailureToLegAAsync(SIPTransport transport, SIP_ServerSession session, SIPResponseStatusCodesEnum status, string reason) {
            try {
                if (session.LegA.InviteReceived == null) return;
                SIPResponse fail = _commands.LegA.BuildResponseFromInvite(session, status, reason);
                await SendLegAResponseAsync(transport, session, fail);
            } catch (Exception ex) {
                _logger?.Warn($"SipExt Fehlerantwort an LegA konnte nicht gesendet werden | SessionID={session.SessionId} | Status={(int)status} {status} | Fehler={ex.Message}");
            }
        }

        private void SafeStopRelayAndReleasePorts(SIP_ServerSession session, string context) {
            try {
                _media.StopRelayAndReleasePorts(session);
            } catch (Exception ex) {
                _logger?.Warn($"{context} | RTP-Relay-Cleanup fehlgeschlagen | SessionID={session.SessionId} | Fehler={ex.Message}");
            }
        }

        private void SafeMarkAllLegsEnded(SIP_ServerSession session, string status, string context) {
            try {
                _sessions.MarkAllLegsEnded(session.SessionId, status);
                if (string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(status, "InviteFailed", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(status, "ResponseError", StringComparison.OrdinalIgnoreCase)) {
                    TKBase_CallManager.MarkRuntimeFailed(session.SessionId, context);
                } else {
                    TKBase_CallManager.MarkRuntimeEnded(session.SessionId, context);
                }
            } catch (Exception ex) {
                _logger?.Warn($"{context} | Session-Ende konnte nicht gesetzt werden | SessionID={session.SessionId} | Status={status} | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The ResolveInviteRoute
        /// </summary>
        /// <param name="legACallId">The legACallId<see cref="string"/></param>
        /// <param name="fromNumber">The fromNumber<see cref="string"/></param>
        /// <param name="toNumber">The toNumber<see cref="string"/></param>
        /// <param name="decision">The decision<see cref="SIP_DialPlanDecision"/></param>
        /// <returns>The <see cref="SIPIntResolvedTarget"/></returns>
        private SIPIntResolvedTarget ResolveInviteRoute(string legACallId, string fromNumber, string toNumber, SIP_DialPlanDecision decision) {
            if (decision.SourceType != TKBase_DialSourceType.Internal) {
                _logger?.Warn($"SipExt INVITE nicht uebernommen | LegA={legACallId} | Von={fromNumber} | An={toNumber} | SourceType={decision.SourceType} | Grund=SIPExt verarbeitet nur SipInt.LegA -> SipExt.LegB; externe Inbound-Rufe gehoeren in den CallManager.");
                return SIPIntResolvedTarget.Fail(toNumber, "SIPExt verarbeitet keine externen Inbound-INVITEs.");
            }

            return _routing.HandleInvite(legACallId, fromNumber, toNumber);
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
            SIPResponse response = SIPResponse.GetResponse(request, status, reason);
            string raw = response.ToString();
            // voraussichtlich debug level
            _logger?.Debug($"SipExt SIMPLE RESPONSE OUT | Ebene=Controller | Status={response.StatusCode} {response.ReasonPhrase} | CallID={response.Header?.CallId} | CSeq={response.Header?.CSeq} {response.Header?.CSeqMethod} | RequestURI={request.URI} | RawLength={raw.Length}{Environment.NewLine}--- SIP RAW START SipExt CONTROLLER SIMPLE RESPONSE OUT ---{Environment.NewLine}{raw}{Environment.NewLine}--- SIP RAW END SipExt CONTROLLER SIMPLE RESPONSE OUT ---");
            _logger?.Info($"SipExt SIMPLE RESPONSE OUT kurz | Status={response.StatusCode} {response.ReasonPhrase} | CallID={response.Header?.CallId} | CSeq={response.Header?.CSeq} {response.Header?.CSeqMethod} | RequestURI={request.URI}");
            await transport.SendResponseAsync(response);
        }

        /// <summary>
        /// The SendLegAResponseAsync
        /// </summary>
        /// <param name="transport">The transport<see cref="SIPTransport"/></param>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="response">The response<see cref="SIPResponse"/></param>
        /// <returns>The <see cref="Task"/></returns>
        private async Task SendLegAResponseAsync(SIPTransport transport, SIP_ServerSession session, SIPResponse response) {
            _commands.LegA.StoreResponseSent(session, response);
            string raw = response.ToString();
            // voraussichtlich debug level
            _logger?.Debug($"SipExt LegA RESPONSE OUT | Ebene=Controller | SessionID={session.SessionId} | Status={response.StatusCode} {response.ReasonPhrase} | CallID={response.Header?.CallId} | CSeq={response.Header?.CSeq} {response.Header?.CSeqMethod} | RawLength={raw.Length}{Environment.NewLine}--- SIP RAW START SipExt CONTROLLER LEGA RESPONSE OUT ---{Environment.NewLine}{raw}{Environment.NewLine}--- SIP RAW END SipExt CONTROLLER LEGA RESPONSE OUT ---");
            _logger?.Info($"SipExt LegA RESPONSE OUT kurz | SessionID={session.SessionId} | Status={response.StatusCode} {response.ReasonPhrase} | CallID={response.Header?.CallId} | CSeq={response.Header?.CSeq} {response.Header?.CSeqMethod}");
            await transport.SendResponseAsync(response);
        }

        /// <summary>
        /// The ApplySipExtContactUsers
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="fromNumber">The fromNumber<see cref="string"/></param>
        /// <param name="toNumber">The toNumber<see cref="string"/></param>
        /// <param name="route">The route<see cref="SIPIntResolvedTarget"/></param>
        /// <param name="decision">The decision<see cref="SIP_DialPlanDecision"/></param>
        private void ApplySipExtContactUsers(SIP_ServerSession session, string fromNumber, string toNumber, SIPIntResolvedTarget route, SIP_DialPlanDecision decision) {
            if (route.IsExternalTarget) {
                session.FromUsername = ResolveSipUsernameByInput(fromNumber) ?? route.CallerUsername ?? fromNumber;
                session.ToUsername = toNumber;
                return;
            }

            session.FromUsername = fromNumber;
            session.ToUsername = ResolveSipUsernameByRoute(route, toNumber) ?? toNumber;
        }

        /// <summary>
        /// The ResolveSipUsernameByRoute
        /// </summary>
        /// <param name="route">The route<see cref="SIPIntResolvedTarget"/></param>
        /// <param name="fallbackNumber">The fallbackNumber<see cref="string"/></param>
        /// <returns>The <see cref="string?"/></returns>
        private string? ResolveSipUsernameByRoute(SIPIntResolvedTarget route, string fallbackNumber) {
            if (route.TargetUserId.HasValue) {
                DataRow? target = _sipSql.GetSipUserById(route.TargetUserId.Value);
                string? username = ReadUsername(target);
                if (!string.IsNullOrWhiteSpace(username)) return username;
            }
            return !string.IsNullOrWhiteSpace(route.TargetUsername) ? route.TargetUsername : ResolveSipUsernameByInput(fallbackNumber);
        }

        /// <summary>
        /// The ResolveSipUsernameByInput
        /// </summary>
        /// <param name="value">The value<see cref="string"/></param>
        /// <returns>The <see cref="string?"/></returns>
        private string? ResolveSipUsernameByInput(string value) {
            if (string.IsNullOrWhiteSpace(value)) return null;
            DataRow? row = _sipSql.GetSipUserByUsername(value) ?? _sipSql.GetSipUserByNumber(value);
            return ReadUsername(row);
        }

        /// <summary>
        /// The ReadUsername
        /// </summary>
        /// <param name="row">The row<see cref="DataRow?"/></param>
        /// <returns>The <see cref="string?"/></returns>
        private static string? ReadUsername(DataRow? row) {
            return row == null || !row.Table.Columns.Contains("Username") || row["Username"] == DBNull.Value ? null : Convert.ToString(row["Username"])?.Trim();
        }

        /// <summary>
        /// The IsRequestFromLegA
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="request">The request<see cref="SIPRequest"/></param>
        /// <param name="remoteEndPoint">The remoteEndPoint<see cref="SIPEndPoint"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private bool IsRequestFromLegA(SIP_ServerSession session, SIPRequest request, SIPEndPoint remoteEndPoint) {
            string callId = request.Header?.CallId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(callId)) {
                if (string.Equals(callId, session.LegA.CallId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(callId, session.LegACallId, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }

                if (string.Equals(callId, session.LegB.CallId, StringComparison.OrdinalIgnoreCase)) {
                    return false;
                }
            }

            return _commands.SameSipEndPoint(remoteEndPoint, session.LegA.SipEndPoint);
        }

        /// <summary>
        /// The BuildLocalEndPointForRemote
        /// </summary>
        /// <param name="remoteEndPoint">The remoteEndPoint<see cref="SIPEndPoint"/></param>
        /// <returns>The <see cref="SIPEndPoint"/></returns>
        private SIPEndPoint BuildLocalEndPointForRemote(SIPEndPoint remoteEndPoint) {
            IPAddress localAddress = _settings.ResolveServerAddressForRemote(remoteEndPoint.Address);
            return new SIPEndPoint(remoteEndPoint.Protocol, localAddress, _settings.SipPort);
        }

        /// <summary>
        /// The ApplyLegMediaAddresses
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="legALocalEndPoint">The legALocalEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="legARemoteEndPoint">The legARemoteEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="legBLocalEndPoint">The legBLocalEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="legBRemoteEndPoint">The legBRemoteEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="route">The route<see cref="SIPIntResolvedTarget"/></param>
        /// <param name="originalToNumber">The originalToNumber<see cref="string"/></param>
        private void ApplyLegMediaAddresses(SIP_ServerSession session, SIPEndPoint legALocalEndPoint, SIPEndPoint legARemoteEndPoint, SIPEndPoint legBLocalEndPoint, SIPEndPoint legBRemoteEndPoint, SIPIntResolvedTarget route, string originalToNumber) {
            if (!_settings.TryResolveIngressMediaAdvertisedIp(legALocalEndPoint.Address, legARemoteEndPoint.Address, null, out string legAIp, out string legAReason)) {
                _logger?.Error($"SipExt MediaAdvertisedIp LegA fehlt | SessionID={session.SessionId} | Local={legALocalEndPoint} | Remote={legARemoteEndPoint} | Grund={legAReason}");
                throw new InvalidOperationException(legAReason);
            }

            bool providerLeg = route.IsExternalTarget;
            string? providerPublicIp = providerLeg ? _sipSql.GetPublicInternetIpForAddressFamily(legBRemoteEndPoint.Address.AddressFamily) : null;
            if (!_settings.TryResolveEndpointMediaAdvertisedIp(legBLocalEndPoint.Address, legBRemoteEndPoint.Address, providerPublicIp, providerLeg, out string legBIp, out string legBReason)) {
                _logger?.Error($"SipExt MediaAdvertisedIp LegB fehlt | SessionID={session.SessionId} | ProviderLeg={providerLeg} | PublicIP={providerPublicIp ?? "-"} | Local={legBLocalEndPoint} | Remote={legBRemoteEndPoint} | Grund={legBReason}");
                throw new InvalidOperationException(legBReason);
            }

            session.LegA.MediaAdvertisedIp = legAIp;
            session.LegB.MediaAdvertisedIp = legBIp;
            session.MediaAdvertisedIp = legAIp;
        }

        /// <summary>
        /// The ResolveProviderPublicIpForInboundDid
        /// </summary>
        /// <param name="originalToNumber">The originalToNumber<see cref="string"/></param>
        /// <returns>The <see cref="string?"/></returns>
        private string? ResolveProviderPublicIpForInboundDid(string originalToNumber) {
            string normalized = TKSipExt_Routing.NormalizeExternalNumber(originalToNumber);
            if (string.IsNullOrWhiteSpace(normalized)) {
                return null;
            }

            DataRow? externalNumber = _sipSql.GetExternalNumberByValue(normalized);
            if (externalNumber == null) {
                return null;
            }

            string sourceType = externalNumber.Table.Columns.Contains("SourceType") && externalNumber["SourceType"] != DBNull.Value
                ? Convert.ToString(externalNumber["SourceType"]) ?? string.Empty
                : string.Empty;
            if (!string.Equals(sourceType, "ProviderAccount", StringComparison.OrdinalIgnoreCase)) {
                return null;
            }

            int sourceId = externalNumber.Table.Columns.Contains("SourceID") && externalNumber["SourceID"] != DBNull.Value
                ? Convert.ToInt32(externalNumber["SourceID"])
                : 0;
            if (sourceId <= 0) {
                return null;
            }

            DataRow? registrationState = _sipSql.GetProviderRegistrationStateByProviderAccountId(sourceId);
            if (registrationState == null || !registrationState.Table.Columns.Contains("LastResolvedIP") || registrationState["LastResolvedIP"] == DBNull.Value) {
                return null;
            }

            string value = (Convert.ToString(registrationState["LastResolvedIP"]) ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        /// <summary>
        /// The IsSipExtSession
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool IsSipExtSession(SIP_ServerSession session) {
            return session.RouteTargetKind == SIPIntRouteTargetKind.External
                || session.ProviderAccountId.HasValue
                || !string.IsNullOrWhiteSpace(session.ProviderName)
                || (!string.IsNullOrWhiteSpace(session.ParentSessionId)
                    && (session.ParentSessionId.StartsWith("FORK-EXT-", StringComparison.OrdinalIgnoreCase)
                        || session.ParentSessionId.StartsWith("SIPEXT-", StringComparison.OrdinalIgnoreCase)));
        }
    }
}
