/*
 * TKGSM_CallController
 * Aufgabe:
 * - Eigenständige Gesprächssteuerung für den GSM-Bereich.
 * - Nutzt TKBase_SessionManager nur zur Session-Erzeugung und Session-Suche.
 * - Nutzt TKGSM_Commands, TKGSM_Events, TKGSM_Media und TKBase_MediaManager als Bereichsunterbau.
 * - Enthält keine SipInt-/SipExt-Controllerlogik.
 */

namespace TKServer.GSM {
    using SIPSorcery.SIP;
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;

    using TKServer.BASE;
    using TKServer.SQL;
    /// TKGSM_CallController
    /// Aufgabe:
    /// - Eigenständige Gesprächssteuerung für den GSM-Bereich.
    /// - Nutzt TKBase_SessionManager nur zur Session-Erzeugung und Session-Suche.
    /// - Nutzt TKGSM_Commands, TKGSM_Events, TKGSM_Media und TKBase_MediaManager als Bereichsunterbau.
    /// - Enthält keine SipInt-/SipExt-Controllerlogik.

    /// <summary>
    /// Defines the <see cref="TKGSM_CallController" />
    /// </summary>
    internal sealed class TKGSM_CallController {
        /// <summary>
        /// Defines the _logger
        /// </summary>
        private readonly TKBase_LogManager? _logger;

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
        /// Defines the _audioCodecs
        /// </summary>
        private readonly IReadOnlyList<TKBase_AudioCodecConfig> _audioCodecs;

        /// <summary>
        /// Defines the _gsmSipCodecs
        /// </summary>
        private readonly IReadOnlyList<TKBase_AudioCodecConfig> _gsmSipCodecs;

        /// <summary>
        /// Defines the _gsmBridgeCodec
        /// </summary>
        private readonly TKBase_AudioCodecConfig _gsmBridgeCodec;

        /// <summary>
        /// Defines the _gsmTelephoneEventCodecs
        /// </summary>
        private readonly IReadOnlyList<TKBase_AudioCodecConfig> _gsmTelephoneEventCodecs;

        /// <summary>
        /// Defines the _commands
        /// </summary>
        private readonly TKGSM_Commands _commands;

        /// <summary>
        /// Defines the _events
        /// </summary>
        private readonly TKGSM_Events _events;

        /// <summary>
        /// Defines the _transport
        /// </summary>
        private SIPTransport? _transport;

        /// <summary>
        /// Defines the _listenPort
        /// </summary>
        private int _listenPort;

        /// <summary>
        /// Defines the _listenTransport
        /// </summary>
        private string _listenTransport = string.Empty;

        /// <summary>
        /// Defines the _settings
        /// </summary>
        private readonly TKBase_ServerSettings _settings;

        /// <summary>
        /// Defines the _sipSql
        /// </summary>
        private readonly TKSQL_Commands _sipSql;

        /// <summary>
        /// Initializes a new instance of the <see cref="TKGSM_CallController"/> class.
        /// </summary>
        /// <param name="sessions">The sessions<see cref="TKBase_SessionManager"/></param>
        /// <param name="routing">The routing<see cref="TKBase_RoutingManager"/></param>
        /// <param name="settings">The settings<see cref="TKBase_ServerSettings"/></param>
        /// <param name="mediaManager">The mediaManager<see cref="TKBase_MediaManager"/></param>
        /// <param name="audioCodecs">The audioCodecs<see cref="IReadOnlyList{TKBase_AudioCodecConfig}"/></param>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        public TKGSM_CallController(
            TKBase_SessionManager sessions,
            TKBase_RoutingManager routing,
            TKBase_ServerSettings settings,
            TKBase_MediaManager mediaManager,
            IReadOnlyList<TKBase_AudioCodecConfig> audioCodecs,
            TKBase_LogManager? logger = null) {
            _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            _routing = routing ?? throw new ArgumentNullException(nameof(routing));
            _logger = logger;
            _settings = settings;
            _sipSql = new TKSQL_Commands(logger);
            _mediaManager = mediaManager ?? throw new ArgumentNullException(nameof(mediaManager));
            _audioCodecs = audioCodecs ?? throw new ArgumentNullException(nameof(audioCodecs));
            _gsmSipCodecs = TKBase_AudioCodecConfig.SelectGsmSipCodecs(_audioCodecs, _settings.SipCodecId);
            if (_gsmSipCodecs.Count == 0) {
                throw new InvalidOperationException($"TK_Properties.SIPCodec ist kein aktiver SIP/RTP-Audiocodec: {_settings.SipCodecId}");
            }
            _gsmBridgeCodec = TKBase_AudioCodecConfig.SelectGsmBridgeCodec(_audioCodecs, _settings.GsmCodecId);
            _gsmTelephoneEventCodecs = TKBase_AudioCodecConfig.SelectGsmTelephoneEventCodecs(_audioCodecs);
            _commands = new TKGSM_Commands(logger);
            _events = new TKGSM_Events(logger);
            TKBase_GsmGateway.ConfigureLogger(logger);
            TKBase_GsmGateway.ConfigureSessionManager(sessions);
            TKBase_GsmGateway.CallStateChanged += HandleGatewayCallStateChanged;
        }

        /// <summary>
        /// The BindTransport
        /// </summary>
        /// <param name="transport">The transport<see cref="SIPTransport"/></param>
        /// <param name="listenPort">The listenPort<see cref="int"/></param>
        /// <param name="listenTransport">The listenTransport<see cref="string"/></param>
        public void BindTransport(SIPTransport transport, int listenPort, string listenTransport) {
            _transport = transport;
            _listenPort = listenPort;
            if (string.IsNullOrWhiteSpace(listenTransport)) {
                throw new InvalidOperationException("SIP-Transport fuer GSM-Bindung fehlt.");
            }
            _listenTransport = listenTransport.Trim().ToUpperInvariant();
        }

        /// <summary>
        /// The CanHandleInvite
        /// </summary>
        /// <param name="request">The request<see cref="SIPRequest"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public bool CanHandleInvite(SIPRequest request) {
            string fromNumber = _commands.LegA.ExtractFromNumber(request);
            string toNumber = _commands.LegA.ExtractToNumber(request);
            SIPIntResolvedTarget route = _routing.HandleInvite(request.Header?.CallId ?? CallProperties.CreateNewCallId(), fromNumber, toNumber);
            return route.Success && route.IsGsmTarget;
        }

        /// <summary>
        /// The OwnsCall
        /// </summary>
        /// <param name="callId">The callId<see cref="string?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public bool OwnsCall(string? callId) {
            return !string.IsNullOrWhiteSpace(callId) && _sessions.TryGet(callId, out SIP_ServerSession? session) && session != null && session.IsGsmSession;
        }

        /// <summary>
        /// The StartService
        /// </summary>
        public void StartService() => TKBase_GsmGateway.Start();

        /// <summary>
        /// The StopService
        /// </summary>
        public void StopService() => TKBase_GsmGateway.Stop();

        /// <summary>
        /// The StopOwnedSessions
        /// </summary>
        public void StopOwnedSessions() {
            foreach (SIP_ServerSession session in _sessions.GetSnapshot()) {
                if (session.IsGsmSession) {
                    StopRelayAndBridge(session);
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
            if (!_sessions.TryGet(sessionId, out SIP_ServerSession? session) || session == null || !session.IsGsmSession) return false;

            try {
                _commands.LegB.HangupGsmCall(session, "control-hangup");

                if (session.LegA.SipEndPoint != null && session.LegA.LocalSipEndPoint != null && session.LegA.InviteReceived != null) {
                    if (session.IsAnswered && session.LegA.LastSuccessResponse != null) {
                        SIPRequest byeA = _commands.LegA.BuildByeToSipLeg(session, session.LegA.LocalSipEndPoint);
                        string controlByeLegARawController = byeA.ToString();
                        _logger?.Debug($"GSM Control BYE OUT LegA | Ebene=Controller | SessionID={session.SessionId} | CallID={byeA.Header?.CallId} | Ziel={session.LegA.SipEndPoint} | RawLength={controlByeLegARawController.Length}{Environment.NewLine}--- SIP RAW START GSM CONTROLLER CONTROL BYE OUT LEGA ---{Environment.NewLine}{controlByeLegARawController}{Environment.NewLine}--- SIP RAW END GSM CONTROLLER CONTROL BYE OUT LEGA ---");
                        await transport.SendRequestAsync(session.LegA.SipEndPoint, byeA);
                    } else {
                        SIPResponse unavailable = _commands.LegA.BuildResponseFromInvite(session, SIPResponseStatusCodesEnum.TemporarilyUnavailable, "GSM call ended by control");
                        await SendLegAResponseAsync(transport, session, unavailable);
                    }
                }

                StopRelayAndBridge(session);
                _sessions.SetGsmEnded(session.SessionId, "ControlHangup", "control-hangup");
                return true;
            } catch (Exception ex) {
                _logger?.Error($"GSM Control-Hangup fehlgeschlagen | SessionID={sessionId} | Fehler={ex.Message}");
                return false;
            }
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
            bool gsmDialStarted = false;

            try {
                BindTransport(transport, localEndPoint.Port, localEndPoint.Protocol.ToString());
                fromNumber = _commands.LegA.ExtractFromNumber(request);
                toNumber = _commands.LegA.ExtractToNumber(request);
                string inviteRawController = request.ToString();
                // voraussichtlich debug level
                _logger?.Debug($"GSM SIP INVITE IN | Ebene=Controller | CallID={legACallId} | Von={fromNumber} | An={toNumber} | Local={localEndPoint} | ReceivedLocal={receivedLocalEndPoint} | Remote={remoteEndPoint} | URI={request.URI} | BodyLength={request.Body?.Length ?? 0} | RawLength={inviteRawController.Length}{Environment.NewLine}--- SIP RAW START GSM CONTROLLER INVITE IN ---{Environment.NewLine}{inviteRawController}{Environment.NewLine}--- SIP RAW END GSM CONTROLLER INVITE IN ---");
                _logger?.Info($"GSM SIP INVITE IN kurz | CallID={legACallId} | Von={fromNumber} | An={toNumber} | Remote={remoteEndPoint} | URI={request.URI}");
                SIPIntResolvedTarget route = _routing.HandleInvite(legACallId, fromNumber, toNumber);

                if (!route.Success || !route.IsGsmTarget) {
                    await SendSimpleResponseSafeAsync(transport, request, SIPResponseStatusCodesEnum.TemporarilyUnavailable, route.Reason ?? "Kein GSM-Ziel.", "GSM INVITE abgewiesen");
                    return;
                }

                session = _sessions.CreateGsmSession(legACallId, fromNumber, toNumber, request, remoteEndPoint, localEndPoint, route);
                ApplyGsmOutboundContactUsers(session, fromNumber, toNumber, route);
                if (!_settings.TryResolveIngressMediaAdvertisedIp(receivedLocalEndPoint.Address, remoteEndPoint.Address, null, out string legAMediaIp, out string mediaReason)) {
                    _logger?.Error($"GSM MediaAdvertisedIp LegA fehlt | SessionID={session.SessionId} | Local={receivedLocalEndPoint.Address} | Remote={remoteEndPoint.Address} | Grund={mediaReason}");
                    await SendSimpleResponseSafeAsync(transport, request, SIPResponseStatusCodesEnum.TemporarilyUnavailable, mediaReason, "GSM MediaAdvertisedIp LegA fehlt");
                    SafeRemoveSession(session, "GSM MediaAdvertisedIp LegA fehlt");
                    return;
                }

                session.LegA.MediaAdvertisedIp = legAMediaIp;
                session.MediaAdvertisedIp = legAMediaIp;
                _commands.LegA.StoreSipInviteReceived(session, request, remoteEndPoint, localEndPoint);
                _events.LegA.HandleSipInviteReceived(session.SessionId, session.LegA.CallId, session.LegA.InviteReceivedRaw);

                if (!string.IsNullOrWhiteSpace(request.Body) && !PrepareSipCallerMedia(session, request)) {
                    SIPResponse notAcceptable = SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.NotAcceptableHere, "Kein kompatibler GSM-Audiocodec.");
                    await SendLegAResponseAsync(transport, session, notAcceptable);
                    SafeRemoveSession(session, "GSM Codec abgelehnt");
                    return;
                }

                SIPResponse trying = SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.Trying, null);
                await SendLegAResponseAsync(transport, session, trying);

                if (!_commands.LegB.StartGsmOutboundCall(route, fromNumber, toNumber, session.SessionId, out string? gsmSessionId, out string? reason)) {
                    SIPResponse failed = SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.ServiceUnavailable, reason ?? "GSM Dial fehlgeschlagen");
                    await SendLegAResponseAsync(transport, session, failed);
                    SafeRemoveSession(session, "GSM Dial fehlgeschlagen");
                    return;
                }
                gsmDialStarted = true;

                if (!string.IsNullOrWhiteSpace(gsmSessionId) && !string.Equals(gsmSessionId, session.SessionId, StringComparison.OrdinalIgnoreCase)) {
                    _sessions.RemapAlias(gsmSessionId, session.SessionId);
                }

                _events.LegB.HandleGsmDialStarted(session.SessionId, session.LegB.CallId, route.GsmStatusId, toNumber);
            } catch (Exception ex) {
                _logger?.Error($"GSM INVITE Verarbeitung fehlgeschlagen | LegA={legACallId} | Von={fromNumber} | An={toNumber} | Fehler={ex.Message}");
                if (session != null) {
                    if (gsmDialStarted) SafeHangupGsmCall(session, "invite-error", "GSM INVITE Fehler");
                    await TrySendInviteFailureToLegAAsync(transport, session, SIPResponseStatusCodesEnum.ServiceUnavailable, "GSM INVITE Fehler");
                    SafeStopRelayAndBridge(session, "GSM INVITE Fehler");
                    SafeSetGsmEnded(session, "InviteFailed", "invite-error", "GSM INVITE Fehler");
                    SafeMarkAllLegsEnded(session, "InviteFailed", "GSM INVITE Fehler");
                } else {
                    await SendSimpleResponseSafeAsync(transport, request, SIPResponseStatusCodesEnum.ServiceUnavailable, "GSM INVITE Fehler", "GSM INVITE Fehler ohne Session");
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
            if (!_sessions.TryGet(request.Header?.CallId ?? string.Empty, out SIP_ServerSession? session) || session == null || !session.IsGsmSession) return;
            if (_commands.SameSipEndPoint(remoteEndPoint, session.LegA.SipEndPoint)) {
                _commands.LegA.StoreSipAckReceived(session, request, remoteEndPoint, localEndPoint);
                TryStartGsmAudioBridge(session, "LegA ACK");
            }
            await Task.CompletedTask;
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
            if (!_sessions.TryGet(bye.Header?.CallId ?? string.Empty, out SIP_ServerSession? session) || session == null || !session.IsGsmSession) {
                await SendSimpleResponseSafeAsync(transport, bye, SIPResponseStatusCodesEnum.CallLegTransactionDoesNotExist, null, "GSM BYE ohne Session");
                return;
            }

            bool fromLegA = _commands.SameSipEndPoint(remoteEndPoint, session.LegA.SipEndPoint);
            string terminationReason = fromLegA ? "SIP LegA BYE" : "SIP LegB BYE";
            bool terminationStarted = TryBeginGsmTermination(session, "SIP-BYE", terminationReason);

            try {
                SIPResponse byeOk = SIPResponse.GetResponse(bye, SIPResponseStatusCodesEnum.Ok, null);
                string byeOkRawController = byeOk.ToString();
                // voraussichtlich debug level
                _logger?.Debug($"GSM BYE 200 OK OUT | Ebene=Controller | SessionID={session.SessionId} | CallID={byeOk.Header?.CallId} | Ziel={remoteEndPoint} | RawLength={byeOkRawController.Length}{Environment.NewLine}--- SIP RAW START GSM CONTROLLER BYE 200 OK OUT ---{Environment.NewLine}{byeOkRawController}{Environment.NewLine}--- SIP RAW END GSM CONTROLLER BYE 200 OK OUT ---");
                _logger?.Info($"GSM BYE 200 OK OUT kurz | SessionID={session.SessionId} | CallID={byeOk.Header?.CallId} | Ziel={remoteEndPoint}");
                await transport.SendResponseAsync(byeOk);

                if (!terminationStarted) {
                    return;
                }

                if (fromLegA) {
                    _commands.LegA.StoreSipByeReceived(session, bye, remoteEndPoint, localEndPoint);
                    _events.LegA.HandleSipByeReceived(session.SessionId, session.LegA.CallId, session.LegA.ByeReceivedRaw);
                    SafeHangupGsmCall(session, terminationReason, "GSM BYE LegA");
                } else if (session.LegB.SipEndPoint != null) {
                    SafeHangupGsmCall(session, terminationReason, "GSM BYE LegB");
                }
            } catch (Exception ex) {
                _logger?.Error($"GSM BYE Verarbeitung fehlgeschlagen | SessionID={session.SessionId} | CallID={bye.Header?.CallId ?? "-"} | Fehler={ex.Message}");
            } finally {
                if (terminationStarted) {
                    SafeStopRelayAndBridge(session, "GSM BYE Cleanup");
                    SafeCompleteGsmTermination(session, "Ended", "BYE", "GSM BYE Cleanup");
                }
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
            if (!_sessions.TryGet(request.Header?.CallId ?? string.Empty, out SIP_ServerSession? session) || session == null || !session.IsGsmSession) {
                await SendSimpleResponseSafeAsync(transport, request, SIPResponseStatusCodesEnum.CallLegTransactionDoesNotExist, null, "GSM CANCEL ohne Session");
                return;
            }

            bool terminationStarted = TryBeginGsmTermination(session, "SIP-CANCEL", "SIP CANCEL");
            try {
                _commands.LegA.StoreSipCancelReceived(session, request, remoteEndPoint, localEndPoint);
                _events.LegA.HandleSipCancelReceived(session.SessionId, session.LegA.CallId, session.LegA.CancelReceivedRaw);
                SIPResponse cancelOk = SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.Ok, null);
                string cancelOkRawController = cancelOk.ToString();
                // voraussichtlich debug level
                _logger?.Debug($"GSM CANCEL 200 OK OUT | Ebene=Controller | SessionID={session.SessionId} | CallID={cancelOk.Header?.CallId} | Ziel={remoteEndPoint} | RawLength={cancelOkRawController.Length}{Environment.NewLine}--- SIP RAW START GSM CONTROLLER CANCEL 200 OK OUT ---{Environment.NewLine}{cancelOkRawController}{Environment.NewLine}--- SIP RAW END GSM CONTROLLER CANCEL 200 OK OUT ---");
                _logger?.Info($"GSM CANCEL 200 OK OUT kurz | SessionID={session.SessionId} | CallID={cancelOk.Header?.CallId} | Ziel={remoteEndPoint}");
                await transport.SendResponseAsync(cancelOk);

                if (!terminationStarted) {
                    return;
                }

                SafeHangupGsmCall(session, "SIP CANCEL", "GSM CANCEL");
                SIPResponse terminated = session.LegA.InviteReceived != null ? _commands.LegA.BuildResponseFromInvite(session, SIPResponseStatusCodesEnum.RequestTerminated, null) : SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.RequestTerminated, null);
                await SendLegAResponseAsync(transport, session, terminated);
            } catch (Exception ex) {
                _logger?.Error($"GSM CANCEL Verarbeitung fehlgeschlagen | SessionID={session.SessionId} | CallID={request.Header?.CallId ?? "-"} | Fehler={ex.Message}");
            } finally {
                if (terminationStarted) {
                    SafeStopRelayAndBridge(session, "GSM CANCEL Cleanup");
                    SafeCompleteGsmTermination(session, "Cancelled", "CANCEL", "GSM CANCEL Cleanup");
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
            string callId = response.Header?.CallId ?? string.Empty;
            if (!_sessions.TryGet(callId, out SIP_ServerSession? session) || session == null || !session.IsGsmSession) return;

            try {
                bool responseFromLegA = string.Equals(callId, session.LegA.CallId, StringComparison.OrdinalIgnoreCase)
                    || _commands.SameSipEndPoint(remoteEndPoint, session.LegA.SipEndPoint);
                if (responseFromLegA) {
                    _commands.LegA.StoreSipResponseReceived(session, response, remoteEndPoint, localEndPoint);
                    return;
                }

                _logger?.Warn($"GSM SIPInt.LegB-Response bleibt im CallManager | SessionID={session.SessionId} | Status={response.StatusCode} {response.ReasonPhrase} | Grund=SIPInt.LegB gehoert in SIPInt/CallManager");
                return;
            } catch (Exception ex) {
                _logger?.Error($"GSM RESPONSE Verarbeitung fehlgeschlagen | SessionID={session.SessionId} | CallID={callId} | Status={response.StatusCode} {response.ReasonPhrase} | Fehler={ex.Message}");
                if (response.StatusCode >= 200) {
                    SafeStopRelayAndBridge(session, "GSM RESPONSE Fehler");
                    SafeSetGsmEnded(session, "ResponseError", response.ReasonPhrase, "GSM RESPONSE Fehler");
                    SafeMarkAllLegsEnded(session, "ResponseError", "GSM RESPONSE Fehler");
                }
            }
        }

        /// <summary>
        /// The HandleGatewayCallStateChanged
        /// </summary>
        /// <param name="args">The args<see cref="TKBase_GsmGateway.GsmCallStateChangedEventArgs"/></param>
        private async void HandleGatewayCallStateChanged(TKBase_GsmGateway.GsmCallStateChangedEventArgs args) {
            try {
                await HandleGatewayCallStateChangedAsync(args);
            } catch (Exception ex) {
                _logger?.Error($"GSM CallState Fehler | SessionID={args?.SessionId} | State={args?.State} | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The HandleGatewayCallStateChangedAsync
        /// </summary>
        /// <param name="args">The args<see cref="TKBase_GsmGateway.GsmCallStateChangedEventArgs?"/></param>
        /// <returns>The <see cref="Task"/></returns>
        private async Task HandleGatewayCallStateChangedAsync(TKBase_GsmGateway.GsmCallStateChangedEventArgs? args) {
            if (args == null || _transport == null || string.IsNullOrWhiteSpace(args.SessionId)) return;
            if (!_sessions.TryGet(args.SessionId, out SIP_ServerSession? session) || session == null || !session.IsGsmSession) return;

            string state = (args.State ?? string.Empty).Trim();
            if (IsInviteCancelBeforeAnswerActive(session) && !IsFinalState(state)) {
                _logger?.Info($"GSM Status nach SIP-CANCEL ignoriert | SessionID={session.SessionId} | State={state} | Raw={args.RawLine ?? "-"}");
                return;
            }

            if (session.LegA.InviteReceived == null || session.LegA.SipEndPoint == null) {
                await HandleInboundGsmStateAsync(session, args, state);
                return;
            }

            if (string.Equals(state, "Ringing", StringComparison.OrdinalIgnoreCase)) {
                SIPResponse ringing = _commands.LegA.BuildResponseFromInvite(session, SIPResponseStatusCodesEnum.Ringing, null);
                await SendLegAResponseAsync(_transport, session, ringing);
                _sessions.MarkGsmRinging(session.SessionId, args.RawLine);
                _events.LegB.HandleGsmRinging(session.SessionId, session.LegB.CallId, args.RawLine);
                return;
            }

            if (string.Equals(state, "Connected", StringComparison.OrdinalIgnoreCase) || string.Equals(state, "Answered", StringComparison.OrdinalIgnoreCase)) {
                SIPResponse ok = _commands.LegA.BuildResponseFromInvite(session, SIPResponseStatusCodesEnum.Ok, null);
                if (!string.IsNullOrWhiteSpace(session.LegA.PreparedAnswerSdp)) {
                    ok.Body = session.LegA.PreparedAnswerSdp;
                    ok.Header.ContentType = "application/sdp";
                    ok.Header.ContentLength = ok.Body.Length;
                }
                await SendLegAResponseAsync(_transport, session, ok);
                _sessions.MarkGsmLegConnected(session.SessionId);
                _events.LegB.HandleGsmConnected(session.SessionId, session.LegB.CallId, args.RawLine);
                TryStartGsmAudioBridge(session, "GSM Answered");
                return;
            }

            if (IsFinalState(state)) {
                if (!TryBeginGsmTermination(session, "GSM-Finalsignal", args.RawLine ?? state)) {
                    return;
                }

                try {
                    if (!session.IsAnswered && session.LegA.InviteReceived != null) {
                        SIPResponse fail = _commands.LegA.BuildResponseFromInvite(session, SIPResponseStatusCodesEnum.TemporarilyUnavailable, state);
                        await SendLegAResponseAsync(_transport, session, fail);
                    } else if (session.LegA.SipEndPoint != null && session.LegA.LocalSipEndPoint != null) {
                        SIPRequest bye = _commands.LegA.BuildByeToSipLeg(session, session.LegA.LocalSipEndPoint);
                        await _transport.SendRequestAsync(session.LegA.SipEndPoint, bye);
                    }
                } finally {
                    StopRelayAndBridge(session);
                    CompleteGsmTermination(session, state, args.RawLine);
                    _events.LegB.HandleGsmEnded(session.SessionId, session.LegB.CallId, state);
                }
            }
        }

        /// <summary>
        /// The HandleInboundGsmStateAsync
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="args">The args<see cref="TKBase_GsmGateway.GsmCallStateChangedEventArgs"/></param>
        /// <param name="state">The state<see cref="string"/></param>
        /// <returns>The <see cref="Task"/></returns>
        private async Task HandleInboundGsmStateAsync(SIP_ServerSession session, TKBase_GsmGateway.GsmCallStateChangedEventArgs args, string state) {
            if (_transport == null) return;
            if (string.Equals(state, "Ringing", StringComparison.OrdinalIgnoreCase)) {
                _events.LegA.HandleGsmInboundRinging(session.SessionId, session.LegA.CallId, args.RawLine);
                return;
            }

            if (string.Equals(state, "Answered", StringComparison.OrdinalIgnoreCase) || string.Equals(state, "Connected", StringComparison.OrdinalIgnoreCase)) {
                _events.LegA.HandleGsmInboundAnswered(session.SessionId, session.LegA.CallId, args.RawLine);
                return;
            }

            if (IsFinalState(state)) {
                if (!TryBeginGsmTermination(session, "GSM-Inbound-Finalsignal", args.RawLine ?? state)) {
                    return;
                }
                try {
                    _logger?.Info($"GSM Inbound-Ende ohne direkte SIPInt.LegB-Steuerung | SessionID={session.SessionId} | Grund=SIPInt.LegB gehoert in SIPInt/CallManager");
                } finally {
                    StopRelayAndBridge(session);
                    CompleteGsmTermination(session, state, args.RawLine);
                    _events.LegA.HandleGsmInboundEnded(session.SessionId, session.LegA.CallId, state);
                }
            }
        }

        /// <summary>
        /// The PrepareSipCallerMedia
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="invite">The invite<see cref="SIPRequest"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private bool PrepareSipCallerMedia(SIP_ServerSession session, SIPRequest invite) {
            AudioDescription? caller = AudioDescription.ParseAudioDescription(invite.Body);
            if (caller == null) return false;

            IReadOnlyList<TKBase_PeerAudioCodec> peerCodecs = TKBase_PeerAudioCodec.ParseFromSdp(invite.Body);
            if (!TKBase_AudioCodecConfig.TrySelectGsmSipCodecForPeer(_gsmSipCodecs, peerCodecs, out TKBase_AudioCodecConfig selectedCodec, out _)) {
                _logger?.Warn($"GSM Media abgelehnt | SessionID={session.SessionId} | Grund=Kein kompatibler Codec im SIP-SDP");
                return false;
            }

            (int callerRelayPort, int gsmRelayPort) = ReserveRelayPorts();
            TKBase_MediaManager relay = _mediaManager.CreateRelay(callerRelayPort, gsmRelayPort);
            relay.SetCaller(new IPEndPoint(IPAddress.Parse(caller.Ip), caller.Port));
            session.Relay = relay;
            session.LegA.RemoteRtpEndPoint = new IPEndPoint(IPAddress.Parse(caller.Ip), caller.Port);
            session.LegA.RelayPort = callerRelayPort;
            session.LegB.RelayPort = gsmRelayPort;
            session.GsmSipCodec = selectedCodec;
            session.GsmBridgeCodec = _gsmBridgeCodec;
            if (!TryGetSessionMediaAdvertisedIp(session, out string mediaAdvertisedIp)) {
                return false;
            }

            session.LegA.PreparedAnswerSdp = _commands.BuildGsmAnswerSdp(mediaAdvertisedIp, callerRelayPort, selectedCodec, _gsmTelephoneEventCodecs);
            return true;
        }

        /// <summary>
        /// Bereitet fuer GSM-Inbound das SIPInt.LegB-SDP vor. Die SIPInt-Signalisierung selbst bleibt im CallManager.
        /// </summary>
        internal bool PrepareInboundSipOfferForCallManager(SIP_ServerSession session, out string offerSdp, out string reason) {
            offerSdp = string.Empty;
            reason = string.Empty;
            if (session == null || !session.IsGsmSession || !string.Equals(session.GsmDirection, "Inbound", StringComparison.OrdinalIgnoreCase)) {
                reason = "Session ist keine GSM-Inbound-Session.";
                return false;
            }

            try {
                if (!TryGetSessionMediaAdvertisedIp(session, out string mediaAdvertisedIp)) {
                    reason = "MediaAdvertisedIp fehlt.";
                    return false;
                }

                (int sipRelayPort, int gsmRelayPort) = ReserveRelayPorts();
                TKBase_MediaManager relay = _mediaManager.CreateRelay(sipRelayPort, gsmRelayPort);
                session.Relay = relay;
                session.LegB.RelayPort = sipRelayPort;
                session.LegA.RelayPort = gsmRelayPort;
                session.GsmBridgeCodec = _gsmBridgeCodec;
                offerSdp = _commands.BuildInboundGsmOfferSdp(mediaAdvertisedIp, sipRelayPort, _audioCodecs);
                session.LegB.PreparedOfferSdp = offerSdp;
                _logger?.Info($"GSM Inbound SIPInt.LegB SDP vorbereitet | SessionID={session.SessionId} | SipRelayPort={sipRelayPort} | GsmRelayPort={gsmRelayPort}");
                return true;
            } catch (Exception ex) {
                reason = ex.Message;
                SafeStopRelayAndBridge(session, "GSM Inbound SDP Vorbereitung fehlgeschlagen");
                _logger?.Error($"GSM Inbound SIPInt.LegB SDP Vorbereitung fehlgeschlagen | SessionID={session.SessionId} | Fehler={ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Uebernimmt die SIPInt.LegB-Antwort-Media fuer GSM-Inbound. Die SIPInt-Signalisierung selbst bleibt im CallManager.
        /// </summary>
        internal bool PrepareInboundSipAnswerForCallManager(SIP_ServerSession session, SIPResponse response, out string reason) {
            reason = string.Empty;
            if (session == null || response == null || !session.IsGsmSession || !string.Equals(session.GsmDirection, "Inbound", StringComparison.OrdinalIgnoreCase)) {
                reason = "Session oder Response ist fuer GSM-Inbound ungueltig.";
                return false;
            }

            try {
                if (!PrepareInboundSipAnswerMedia(session, response)) {
                    reason = "SIPInt.LegB Media-Antwort ungueltig.";
                    return false;
                }

                return true;
            } catch (Exception ex) {
                reason = ex.Message;
                _logger?.Error($"GSM Inbound SIPInt.LegB Media-Antwort fehlgeschlagen | SessionID={session.SessionId} | Fehler={ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Nimmt den GSM-Inbound-Anruf an. Keine SIPInt-Leg-Steuerung.
        /// </summary>
        internal bool AnswerInboundGsmCallForCallManager(SIP_ServerSession session) {
            if (session == null || !session.IsGsmSession || !string.Equals(session.GsmDirection, "Inbound", StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            return _commands.LegB.AnswerGsmCall(session);
        }

        /// <summary>
        /// Beendet den GSM-Inbound-Anruf. Keine SIPInt-Leg-Steuerung.
        /// </summary>
        internal bool HangupInboundGsmCallForCallManager(SIP_ServerSession session, string? reason) {
            if (session == null || !session.IsGsmSession || !string.Equals(session.GsmDirection, "Inbound", StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            return _commands.LegB.HangupGsmCall(session, reason);
        }

        /// <summary>
        /// Startet nur die GSM-AudioBridge fuer GSM-Inbound. Signalisierung bleibt im CallManager.
        /// </summary>
        internal void StartInboundAudioBridgeForCallManager(SIP_ServerSession session, string trigger) {
            if (session == null || !session.IsGsmSession || !string.Equals(session.GsmDirection, "Inbound", StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            TryStartGsmAudioBridge(session, trigger);
        }

        /// <summary>
        /// Stoppt nur GSM-Relay/Bridge fuer GSM-Inbound.
        /// </summary>
        internal void StopInboundAudioBridgeForCallManager(SIP_ServerSession session) {
            if (session == null || !session.IsGsmSession || !string.Equals(session.GsmDirection, "Inbound", StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            StopRelayAndBridge(session);
        }

        /// <summary>
        /// The TryGetSessionMediaAdvertisedIp
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="mediaAdvertisedIp">The mediaAdvertisedIp<see cref="string"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private bool TryGetSessionMediaAdvertisedIp(SIP_ServerSession session, out string mediaAdvertisedIp) {
            mediaAdvertisedIp = session.MediaAdvertisedIp?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(mediaAdvertisedIp) || !IPAddress.TryParse(mediaAdvertisedIp, out IPAddress? parsed) || IPAddress.Any.Equals(parsed) || IPAddress.IPv6Any.Equals(parsed)) {
                _logger?.Error($"GSM MediaAdvertisedIp fehlt | SessionID={session.SessionId}");
                mediaAdvertisedIp = string.Empty;
                return false;
            }

            mediaAdvertisedIp = parsed.ToString();
            return true;
        }

        /// <summary>
        /// The PrepareInboundSipAnswerMedia
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="response">The response<see cref="SIPResponse"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private bool PrepareInboundSipAnswerMedia(SIP_ServerSession session, SIPResponse response) {
            if (string.IsNullOrWhiteSpace(response.Body)) return false;

            AudioDescription? remote = AudioDescription.ParseAudioDescription(response.Body);
            if (remote == null) return false;

            IReadOnlyList<TKBase_PeerAudioCodec> peerCodecs = TKBase_PeerAudioCodec.ParseFromSdp(response.Body);
            if (!TKBase_AudioCodecConfig.TrySelectGsmSipCodecForPeer(_gsmSipCodecs, peerCodecs, out TKBase_AudioCodecConfig selectedCodec, out _)) {
                _logger?.Warn($"GSM Inbound Media ohne passenden Codec | SessionID={session.SessionId}");
                return false;
            }

            session.GsmSipCodec = selectedCodec;
            session.GsmBridgeCodec = _gsmBridgeCodec;
            session.LegB.RemoteRtpEndPoint = new IPEndPoint(IPAddress.Parse(remote.Ip), remote.Port);
            session.Relay?.SetCaller(session.LegB.RemoteRtpEndPoint);
            return true;
        }

        /// <summary>
        /// The TryStartGsmAudioBridge
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="trigger">The trigger<see cref="string"/></param>
        private void TryStartGsmAudioBridge(SIP_ServerSession session, string trigger) {
            bool inboundGsmToSip = session.LegA.InviteReceived == null;
            int bridgeRelayPort = inboundGsmToSip ? session.LegA.RelayPort : session.LegB.RelayPort;
            lock (session.SyncRoot) {
                if (session.IsTerminating || session.IsTerminated || session.GsmAudioPrepared || session.GsmAudioBridge != null || session.GsmAudioBridgeStarting || string.IsNullOrWhiteSpace(session.GsmComPortAudio) || bridgeRelayPort <= 0) {
                    return;
                }

                if (inboundGsmToSip && (session.LegB.RemoteRtpEndPoint == null || session.GsmSipCodec == null)) {
                    return;
                }

                session.GsmAudioBridgeStarting = true;
            }

            TKGSM_Media? bridge = null;
            bool bridgeAssigned = false;
            try {
                TKBase_AudioCodecConfig sipCodec = session.GsmSipCodec ?? _gsmSipCodecs.Single();
                TKBase_AudioCodecConfig bridgeCodec = session.GsmBridgeCodec ?? _gsmBridgeCodec;
                if (!_settings.GSMAudioPort.HasValue || _settings.GSMAudioPort.Value <= 0) {
                    _logger?.Error("GSM AudioBridge Start abgebrochen | GSMAudioPort fehlt oder ist ungueltig.");
                    return;
                }

                if (!_commands.LegB.ActivateAudioRoute(session)) {
                    _logger?.Error($"GSM AudioBridge Start abgebrochen | SessionID={session.SessionId} | Grund=AudioRoute konnte nicht aktiviert werden");
                    return;
                }

                int gsmAudioPort = _settings.GSMAudioPort.Value;
                bridge = new TKGSM_Media(session.GsmComPortAudio!, gsmAudioPort, sipCodec, bridgeCodec, _logger);
                IPEndPoint relayEndpoint = new IPEndPoint(IPAddress.Loopback, bridgeRelayPort);
                bridge.SetRemote(relayEndpoint);
                bridge.Start();

                IPEndPoint bridgeEndpoint = new IPEndPoint(IPAddress.Loopback, bridge.LocalRtpPort);
                lock (session.SyncRoot) {
                    if (session.IsTerminating || session.IsTerminated || session.GsmAudioPrepared || session.GsmAudioBridge != null) {
                        return;
                    }

                    session.GsmAudioBridge = bridge;
                    bridgeAssigned = true;
                    if (inboundGsmToSip) {
                        session.LegA.RemoteRtpEndPoint = bridgeEndpoint;
                    } else {
                        session.LegB.RemoteRtpEndPoint = bridgeEndpoint;
                    }
                    session.GsmAudioPrepared = true;
                }

                session.Relay?.SetCallee(bridgeEndpoint);
                session.Relay?.Start();
                _logger?.Info("GSM AudioBridge Codec aktiv | SessionID=" + session.SessionId + " | Trigger=" + trigger + " | SipCodec=" + sipCodec.CodecKey + "/" + sipCodec.PayloadTypeOrThrow() + " | BridgeCodec=" + bridgeCodec.CodecKey + " | GSMAudioPort=" + gsmAudioPort + " | RelayPort=" + bridgeRelayPort);
            } catch (Exception ex) {
                _logger?.Error($"GSM AudioBridge Start fehlgeschlagen | SessionID={session.SessionId} | Trigger={trigger} | Fehler={ex.Message}");
            } finally {
                lock (session.SyncRoot) {
                    session.GsmAudioBridgeStarting = false;
                }
                if (!bridgeAssigned) {
                    bridge?.Dispose();
                }
            }
        }

        /// <summary>
        /// The TryBeginGsmTermination
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="source">The source<see cref="string"/></param>
        /// <param name="reason">The reason<see cref="string"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private bool TryBeginGsmTermination(SIP_ServerSession session, string source, string reason) {
            lock (session.SyncRoot) {
                if (session.IsTerminating || session.IsTerminated) {
                    _logger?.Info($"GSM Session-Ende bereits aktiv | SessionID={session.SessionId} | Quelle={source} | Grund={reason} | Status={session.Status}");
                    return false;
                }

                session.IsTerminating = true;
                session.TerminationReason = reason;
                session.TerminationStartedAt = DateTime.Now;
                session.UpdatedAt = DateTime.Now;
                return true;
            }
        }

        /// <summary>
        /// The CompleteGsmTermination
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="status">The status<see cref="string"/></param>
        /// <param name="signalLine">The signalLine<see cref="string?"/></param>
        private void CompleteGsmTermination(SIP_ServerSession session, string status, string? signalLine) {
            lock (session.SyncRoot) {
                session.IsTerminating = false;
                session.IsTerminated = true;
            }
            _sessions.SetGsmEnded(session.SessionId, status, signalLine);
        }

        /// <summary>
        /// The ApplyGsmOutboundContactUsers
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="fromNumber">The fromNumber<see cref="string"/></param>
        /// <param name="toNumber">The toNumber<see cref="string"/></param>
        /// <param name="route">The route<see cref="SIPIntResolvedTarget"/></param>
        private void ApplyGsmOutboundContactUsers(SIP_ServerSession session, string fromNumber, string toNumber, SIPIntResolvedTarget route) {
            session.FromUsername = ResolveSipUsernameByInput(fromNumber) ?? route.CallerUsername ?? fromNumber;
            session.ToUsername = toNumber;
        }

        /// <summary>
        /// The ResolveSipUsernameByInput
        /// </summary>
        /// <param name="value">The value<see cref="string"/></param>
        /// <returns>The <see cref="string?"/></returns>
        private string? ResolveSipUsernameByInput(string value) {
            if (string.IsNullOrWhiteSpace(value)) return null;
            DataRow? row = _sipSql.GetSipUserByUsername(value) ?? _sipSql.GetSipUserByNumber(value);
            return row == null || !row.Table.Columns.Contains("Username") || row["Username"] == DBNull.Value ? null : Convert.ToString(row["Username"])?.Trim();
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



        private static bool IsInviteCancelBeforeAnswerActive(SIP_ServerSession session) {
            if (session == null || session.IsAnswered) return false;
            return session.LegA.CancelSeen
                || session.LegB.CancelSeen
                || session.IsTerminating
                || session.IsTerminated
                || string.Equals(session.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)
                || string.Equals(session.Status, "ForkCancelled", StringComparison.OrdinalIgnoreCase);
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
                _logger?.Warn($"GSM Fehlerantwort an LegA konnte nicht gesendet werden | SessionID={session.SessionId} | Status={(int)status} {status} | Fehler={ex.Message}");
            }
        }

        private void SafeHangupGsmCall(SIP_ServerSession session, string reason, string context) {
            try {
                _commands.LegB.HangupGsmCall(session, reason);
            } catch (Exception ex) {
                _logger?.Warn($"{context} | GSM-Auflegen fehlgeschlagen | SessionID={session.SessionId} | Grund={reason} | Fehler={ex.Message}");
            }
        }

        private void SafeStopRelayAndBridge(SIP_ServerSession session, string context) {
            try {
                StopRelayAndBridge(session);
            } catch (Exception ex) {
                _logger?.Warn($"{context} | GSM Relay/Bridge Cleanup fehlgeschlagen | SessionID={session.SessionId} | Fehler={ex.Message}");
            }
        }

        private void SafeCompleteGsmTermination(SIP_ServerSession session, string status, string? signalLine, string context) {
            try {
                CompleteGsmTermination(session, status, signalLine);
            } catch (Exception ex) {
                _logger?.Warn($"{context} | GSM Terminierung konnte nicht abgeschlossen werden | SessionID={session.SessionId} | Status={status} | Fehler={ex.Message}");
            }
        }

        private void SafeSetGsmEnded(SIP_ServerSession session, string status, string? signalLine, string context) {
            try {
                _sessions.SetGsmEnded(session.SessionId, status, signalLine);
            } catch (Exception ex) {
                _logger?.Warn($"{context} | GSM Status konnte nicht gesetzt werden | SessionID={session.SessionId} | Status={status} | Fehler={ex.Message}");
            }
        }

        private void SafeSetGsmEndedById(string sessionId, string status, string? signalLine, string context) {
            try {
                _sessions.SetGsmEnded(sessionId, status, signalLine);
            } catch (Exception ex) {
                _logger?.Warn($"{context} | GSM Status konnte nicht gesetzt werden | SessionID={sessionId} | Status={status} | Fehler={ex.Message}");
            }
        }

        private void SafeMarkAllLegsEnded(SIP_ServerSession session, string status, string context) {
            SafeMarkAllLegsEndedById(session.SessionId, status, context);
        }

        private void SafeMarkAllLegsEndedById(string sessionId, string status, string context) {
            try {
                _sessions.MarkAllLegsEnded(sessionId, status);
            } catch (Exception ex) {
                _logger?.Warn($"{context} | Session-Ende konnte nicht gesetzt werden | SessionID={sessionId} | Status={status} | Fehler={ex.Message}");
            }
        }

        private void SafeRemoveSession(SIP_ServerSession session, string context) {
            try {
                _sessions.Remove(session.SessionId);
            } catch (Exception ex) {
                _logger?.Warn($"{context} | Session konnte nicht entfernt werden | SessionID={session.SessionId} | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The StopRelayAndBridge
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        
        private void StopRelayAndBridge(SIP_ServerSession session) {
            TKGSM_Media? bridge;
            TKBase_MediaManager? relay;
            int legARelayPort;
            int legBRelayPort;
            lock (session.SyncRoot) {
                bridge = session.GsmAudioBridge;
                relay = session.Relay;
                legARelayPort = session.LegA.RelayPort;
                legBRelayPort = session.LegB.RelayPort;
                session.GsmAudioBridge = null;
                session.GsmAudioPrepared = false;
                session.Relay = null;
                session.LegA.RelayPort = 0;
                session.LegB.RelayPort = 0;
            }

            try { bridge?.Dispose(); } catch (Exception ex) { _logger?.Warn($"GSM AudioBridge Dispose fehlgeschlagen | SessionID={session.SessionId} | Fehler={ex.Message}"); }
            try { relay?.Dispose(); } catch (Exception ex) { _logger?.Warn($"GSM Relay Dispose fehlgeschlagen | SessionID={session.SessionId} | Fehler={ex.Message}"); }
            try { if (legARelayPort > 0) _mediaManager.Release(legARelayPort); } catch (Exception ex) { _logger?.Warn($"GSM RelayPort LegA Freigabe fehlgeschlagen | SessionID={session.SessionId} | Port={legARelayPort} | Fehler={ex.Message}"); }
            try { if (legBRelayPort > 0) _mediaManager.Release(legBRelayPort); } catch (Exception ex) { _logger?.Warn($"GSM RelayPort LegB Freigabe fehlgeschlagen | SessionID={session.SessionId} | Port={legBRelayPort} | Fehler={ex.Message}"); }
            try { if (session.GsmStatusId.HasValue) TKBase_GsmGateway.TryReleaseAudioRoute(session.GsmStatusId.Value); } catch (Exception ex) { _logger?.Warn($"GSM AudioRoute Freigabe fehlgeschlagen | SessionID={session.SessionId} | StatusID={session.GsmStatusId} | Fehler={ex.Message}"); }
        }

        /// <summary>
        /// The ReserveRelayPorts
        /// </summary>
        /// <returns>The <see cref="(int first, int second)"/></returns>
        private (int first, int second) ReserveRelayPorts() {
            (int rtp, int rtcp)? first = null;
            (int rtp, int rtcp)? second = null;
            try {
                first = _mediaManager.Allocate();
                second = _mediaManager.Allocate();
                if (first == null || second == null) throw new InvalidOperationException("Keine freien RTP-Ports fuer GSM verfuegbar.");
                return (first.Value.rtp, second.Value.rtp);
            } catch {
                if (first != null) {
                    try { _mediaManager.Release(first.Value.rtp); } catch { }
                }
                if (second != null) {
                    try { _mediaManager.Release(second.Value.rtp); } catch { }
                }
                throw;
            }
        }

        /// <summary>
        /// The SendLegAResponseAsync
        /// </summary>
        /// <param name="transport">The transport<see cref="SIPTransport"/></param>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="response">The response<see cref="SIPResponse"/></param>
        /// <returns>The <see cref="Task"/></returns>
        private async Task SendLegAResponseAsync(SIPTransport transport, SIP_ServerSession session, SIPResponse response) {
            _commands.LegA.StoreSipResponseSent(session, response);
            _events.LegA.HandleSipResponseSent(session.SessionId, session.LegA.CallId, response.StatusCode, response.ReasonPhrase);
            string raw = response.ToString();
            // voraussichtlich debug level
            _logger?.Debug($"GSM LegA SIP RESPONSE OUT | Ebene=Controller | SessionID={session.SessionId} | Status={response.StatusCode} {response.ReasonPhrase} | CallID={response.Header?.CallId} | CSeq={response.Header?.CSeq} {response.Header?.CSeqMethod} | RawLength={raw.Length}{Environment.NewLine}--- SIP RAW START GSM CONTROLLER LEGA RESPONSE OUT ---{Environment.NewLine}{raw}{Environment.NewLine}--- SIP RAW END GSM CONTROLLER LEGA RESPONSE OUT ---");
            _logger?.Info($"GSM LegA SIP RESPONSE OUT kurz | SessionID={session.SessionId} | Status={response.StatusCode} {response.ReasonPhrase} | CallID={response.Header?.CallId} | CSeq={response.Header?.CSeq} {response.Header?.CSeqMethod}");
            await transport.SendResponseAsync(response);
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
            _logger?.Debug($"GSM SIMPLE RESPONSE OUT | Ebene=Controller | Status={response.StatusCode} {response.ReasonPhrase} | CallID={response.Header?.CallId} | CSeq={response.Header?.CSeq} {response.Header?.CSeqMethod} | RequestURI={request.URI} | RawLength={raw.Length}{Environment.NewLine}--- SIP RAW START GSM CONTROLLER SIMPLE RESPONSE OUT ---{Environment.NewLine}{raw}{Environment.NewLine}--- SIP RAW END GSM CONTROLLER SIMPLE RESPONSE OUT ---");
            _logger?.Info($"GSM SIMPLE RESPONSE OUT kurz | Status={response.StatusCode} {response.ReasonPhrase} | CallID={response.Header?.CallId} | CSeq={response.Header?.CSeq} {response.Header?.CSeqMethod} | RequestURI={request.URI}");
            await transport.SendResponseAsync(response);
        }

        /// <summary>
        /// The IsFinalState
        /// </summary>
        /// <param name="state">The state<see cref="string"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool IsFinalState(string state) {
            return string.Equals(state, "Ended", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "Hangup", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "NoCarrier", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "Cancelled", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "Busy", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "NoAnswer", StringComparison.OrdinalIgnoreCase)
                || state.StartsWith("Failed", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The ResolveRealm
        /// </summary>
        /// <returns>The <see cref="string"/></returns>
        private string ResolveRealm() {
            return _settings.Realm;
        }
    }
}
