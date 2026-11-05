namespace TKServer.BASE {
    using SIPSorcery.SIP;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;

    using TKServer.BASE;
    using TKServer.FAX;
    using TKServer.FormView;
    using TKServer.GSM;
    using TKServer.SipExt;
    using TKServer.SipInt;
    using TKServer.SQL;
    using TKServer.SVC;

    /// <summary>
    /// Zentraler Call-Dirigent des TK-Servers.
    ///
    /// Aufgabe:
    /// - TKBase_Server von Call-/Controller-Dispatching entlasten.
    /// - Eingehende SIP-Requests/Responses an den zustÃ¤ndigen Bereichscontroller verteilen.
    /// - LaufzeitverfÃ¼gbarkeit der Bereiche berÃ¼cksichtigen.
    /// - Callzustand/LiveCall zentral Ã¼ber TKBase_RuntimeCallManager fÃ¼hren.
    /// - Forking Ã¼ber TKBase_CallManagerForkingAgent koordinieren.
    ///
    /// Wichtige Abgrenzung:
    /// - Dieser Manager ist neutral und enthÃ¤lt keine Bereichs-Fachlogik.
    /// - Bereichscontroller bleiben fÃ¼r ihre jeweilige Protokoll-/Leg-Logik zustÃ¤ndig.
    /// - Session-/Leg-Erzeugung bleibt ausschlieÃŸlich im TKBase_SessionManager.
    /// </summary>
    internal sealed class TKBase_CallManager {
        private readonly TKBase_LogManager? _logger;
        private readonly TKBase_SessionManager _sessions;
        private readonly TKBase_RoutingManager _routing;
        private readonly TKSipInt_Commands _sipIntCommands;
        private readonly TKSipInt_Events _sipIntEvents;
        private readonly TKSipInt_Media _sipIntMedia;
        private readonly TKSipExt_Commands _sipExtCommands;
        private readonly TKBase_ServerSettings _settings;
        private readonly TKBase_DTMF_Manager _dtmfManager;
        private readonly TKBase_CallManagerForkingAgent _forkingAgent;
        private readonly TKSipInt_CallController _sipIntCallController;
        private readonly TKSipExt_CallController _sipExtCallController;
        private readonly TKGSM_CallController _gsmCallController;
        private readonly TKSVC_CallController _svcCallController;
        private readonly TKFax_CallController _faxCallController;
        private SIPTransport? _boundTransport;

        private static readonly object s_runtimeCallSyncRoot = new();
        private static TKBase_RuntimeCallManager? s_runtimeCalls;

        private static TKBase_RuntimeCallManager RuntimeCalls {
            get {
                if (s_runtimeCalls != null) return s_runtimeCalls;
                lock (s_runtimeCallSyncRoot) {
                    s_runtimeCalls ??= new TKBase_RuntimeCallManager(null);
                    return s_runtimeCalls;
                }
            }
        }

        internal static void ConfigureRuntimeCallState(TKBase_LogManager? logger) {
            lock (s_runtimeCallSyncRoot) {
                s_runtimeCalls = new TKBase_RuntimeCallManager(logger);
            }
        }

        internal static int StartRuntimeCall(SIP_ServerSession session, string legACallId, string fromNumber, string toNumber, SIPIntResolvedTarget? route, bool isServiceSession = false) {
            if (session == null) return 0;
            return RuntimeCalls.StartInternalCall(
                session.SessionId,
                session.BridgeId,
                session.LegA.CallId ?? legACallId,
                isServiceSession ? session.LegD.CallId : session.LegB.CallId,
                fromNumber,
                toNumber,
                session.LegA.InviteReceivedRaw,
                route,
                isServiceSession);
        }

        internal static void UpdateRuntimeInviteOutbound(string sessionId, string? legBCallId) {
            RuntimeCalls.UpdateInviteOutbound(sessionId, legBCallId);
        }

        internal static void MarkRuntimeRinging(string sessionId) {
            RuntimeCalls.MarkRinging(sessionId);
        }

        internal static void MarkRuntimeAnswered(string sessionId) {
            RuntimeCalls.MarkAnswered(sessionId);
        }

        internal static void MarkRuntimeEnded(string sessionId, string? reason = null) {
            RuntimeCalls.MarkEnded(sessionId, reason);
        }

        internal static void MarkRuntimeFailed(string sessionId, string? reason = null) {
            RuntimeCalls.MarkFailed(sessionId, reason);
        }

        private volatile bool _sipIntRuntimeEnabled = true;
        private volatile bool _sipExtRuntimeEnabled = true;
        private volatile bool _svcAvailable = true;

        internal TKBase_CallManager(
            TKBase_SessionManager sessions,
            TKBase_RoutingManager routing,
            TKBase_ServerSettings settings,
            TKBase_MediaManager mediaManager,
            TKBase_DTMF_Manager dtmfManager,
            IReadOnlyList<TKBase_AudioCodecConfig> audioCodecs,
            TKBase_LogManager? logger = null) {
            _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            _routing = routing ?? throw new ArgumentNullException(nameof(routing));
            _settings = settings;
            _dtmfManager = dtmfManager ?? throw new ArgumentNullException(nameof(dtmfManager));
            _logger = logger;
            ConfigureRuntimeCallState(logger);
            _sipIntCommands = new TKSipInt_Commands(logger);
            _sipIntEvents = new TKSipInt_Events(logger);
            _sipIntMedia = new TKSipInt_Media(mediaManager, logger);
            _sipExtCommands = new TKSipExt_Commands(logger);
            _forkingAgent = new TKBase_CallManagerForkingAgent(_sessions, _sipIntCommands, _sipIntEvents, _sipIntMedia, settings, logger, _sipExtCommands);
            _svcCallController = new TKSVC_CallController(sessions, settings, mediaManager, logger, dtmfManager);
            _faxCallController = new TKFax_CallController(sessions, routing, settings, mediaManager, logger);
            _sipIntCallController = new TKSipInt_CallController(sessions, routing, settings, mediaManager, logger, dtmfManager);
            _sipExtCallController = new TKSipExt_CallController(sessions, routing, settings, mediaManager, logger);
            _gsmCallController = new TKGSM_CallController(sessions, routing, settings, mediaManager, audioCodecs, logger);
            TKBase_GsmGateway.CallStateChanged += HandleGsmGatewayCallStateChanged;
        }

        public void BindTransport(SIPTransport transport, int listenPort, string listenTransport) {
            _boundTransport = transport;
            _gsmCallController.BindTransport(transport, listenPort, listenTransport);
        }

        public void StopOwnedSessions() {
            TryStopOwnedSessions(_sipIntCallController.StopOwnedSessions, "SIPInt");
            TryStopOwnedSessions(_sipExtCallController.StopOwnedSessions, "SIPExt");
            TryStopOwnedSessions(_gsmCallController.StopOwnedSessions, "GSM");
            TryStopOwnedSessions(_faxCallController.StopOwnedSessions, "FAX");
            TryStopOwnedSessions(_svcCallController.StopOwnedSessions, "SVC");
        }

        public async Task<bool> HangupSessionAsync(SIPTransport transport, string sessionId) {
            if (transport == null || string.IsNullOrWhiteSpace(sessionId)) return false;
            if (!_sessions.TryGet(sessionId, out SIP_ServerSession? session) || session == null) {
                MarkRuntimeEnded(sessionId, "ControlHangup ohne aktive Session");
                _logger?.Warn($"Control-Hangup ohne aktive Session | SessionID={sessionId} | RuntimeCall wurde bereinigt.");
                return false;
            }

            if (session.IsServiceSession || session.HasAttachedServiceLegD) {
                _svcCallController.EndLegD(session, "control-hangup");
                return await _sipIntCallController.HangupSessionAsync(transport, session.SessionId).ConfigureAwait(false);
            }

            if (session.IsGsmSession) {
                return await _gsmCallController.HangupSessionAsync(transport, session.SessionId).ConfigureAwait(false);
            }

            if (session.IsFaxSession) {
                return await _faxCallController.HangupSessionAsync(transport, session.SessionId).ConfigureAwait(false);
            }

            if (IsSipExtSession(session)) {
                return await _sipExtCallController.HangupSessionAsync(transport, session.SessionId);
            }

            return await _sipIntCallController.HangupSessionAsync(transport, session.SessionId);
        }

        public async Task<int> HangupAreaSessionsAsync(SIPTransport transport, string areaKey, string reason) {
            if (transport == null || string.IsNullOrWhiteSpace(areaKey)) return 0;

            string normalizedArea = areaKey.Trim().ToUpperInvariant();
            List<SIP_ServerSession> affectedSessions = new();

            foreach (SIP_ServerSession session in _sessions.GetSnapshot()) {
                if (session == null || session.IsTerminated) {
                    continue;
                }

                bool affected = normalizedArea switch {
                    "GSM" => session.IsGsmSession,
                    "PROVIDER" => IsSipExtSession(session),
                    "SVC" => session.IsServiceSession || session.HasAttachedServiceLegD,
                    "SIPINT" => IsSipIntAreaSession(session),
                    _ => false
                };

                if (affected) {
                    affectedSessions.Add(session);
                }
            }

            int closedCount = 0;
            foreach (SIP_ServerSession session in affectedSessions) {
                try {
                    if (normalizedArea == "SVC") {
                        _svcCallController.EndLegD(session, reason);
                        closedCount++;
                        continue;
                    }

                    if (await HangupSessionAsync(transport, session.SessionId).ConfigureAwait(false)) {
                        closedCount++;
                    }
                } catch (Exception ex) {
                    _logger?.Warn($"Bereichs-Stop Session-Cleanup Warnung | Bereich={areaKey} | SessionID={session.SessionId} | Grund={reason} | Fehler={ex.Message}");
                }
            }

            return closedCount;
        }

        private void TryStopOwnedSessions(Action stopAction, string area) {
            try {
                stopAction();
            } catch (Exception ex) {
                _logger?.Warn($"SIP Server Stop {area}-Sessions fehlgeschlagen | Fehler={ex.Message}");
            }
        }

        public void ConfigureRuntimeAreas(bool sipIntEnabled, bool sipExtEnabled) {
            _sipIntRuntimeEnabled = sipIntEnabled;
            _sipExtRuntimeEnabled = sipExtEnabled;
            _svcAvailable = sipIntEnabled;
            _sipIntCallController.ConfigureRuntimeAreas(sipIntEnabled);
            _logger?.Info($"CallManager Runtimebereiche gesetzt | SIPInt={sipIntEnabled} | SIPExt={sipExtEnabled}");
        }

        public async Task HandleIncomingRequestAsync(SIPTransport transport, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIPRequest request, SIPEndPoint receivedLocalEndPoint) {
            if (request == null || transport == null) return;

            SIPMethodsEnum method = request.Method;
            string callId = request.Header?.CallId ?? "-";

            try {
                switch (method) {
                    case SIPMethodsEnum.INVITE:
                        await HandleInviteAsync(transport, localEndPoint, remoteEndPoint, request, receivedLocalEndPoint);
                        break;
                    case SIPMethodsEnum.ACK:
                        await HandleAckAsync(transport, localEndPoint, remoteEndPoint, request);
                        break;
                    case SIPMethodsEnum.BYE:
                        await HandleByeAsync(transport, localEndPoint, remoteEndPoint, request);
                        break;
                    case SIPMethodsEnum.INFO:
                        await HandleInfoAsync(transport, localEndPoint, remoteEndPoint, request);
                        break;
                    case SIPMethodsEnum.CANCEL:
                        await HandleCancelAsync(transport, localEndPoint, remoteEndPoint, request);
                        break;
                    default:
                        _logger?.Warn($"SIP Request abgewiesen | Method={method} | CallID={callId} | Grund=Methode nicht unterstÃ¼tzt");
                        await SendSimpleResponseAsync(transport, request, SIPResponseStatusCodesEnum.MethodNotAllowed, "Only SIP REGISTER, INVITE, ACK, BYE, INFO and CANCEL are handled.");
                        break;
                }
            } catch (Exception ex) {
                _logger?.Error($"CallManager SIP Request Fehler | Method={method} | CallID={callId} | Remote={remoteEndPoint} | Fehler={ex.Message}");
                await SendSimpleResponseAsync(transport, request, SIPResponseStatusCodesEnum.InternalServerError, ex.Message);
            }
        }

        public async Task HandleIncomingResponseAsync(SIPTransport transport, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIPResponse response) {
            if (response == null || transport == null) return;

            string callId = response.Header?.CallId ?? "-";
            int statusCode = response.StatusCode;

            try {
                if (await _forkingAgent.TryHandleResponseAsync(transport, localEndPoint, remoteEndPoint, response).ConfigureAwait(false)) {
                    return;
                }

                if (await TryHandleGsmInboundLegBResponseAsync(transport, localEndPoint, remoteEndPoint, response).ConfigureAwait(false)) {
                    return;
                }

                if (_faxCallController.OwnsCall(response.Header?.CallId)) {
                    await _faxCallController.HandleResponseAsync(transport, localEndPoint, remoteEndPoint, response);
                } else if (_gsmCallController.OwnsCall(response.Header?.CallId)) {
                    await _gsmCallController.HandleResponseAsync(transport, localEndPoint, remoteEndPoint, response);
                } else if (_sipExtCallController.OwnsCall(response.Header?.CallId)) {
                    await _sipExtCallController.HandleResponseAsync(transport, localEndPoint, remoteEndPoint, response);
                } else {
                    await _sipIntCallController.HandleResponseAsync(transport, localEndPoint, remoteEndPoint, response);
                }
            } catch (Exception ex) {
                _logger?.Error($"CallManager SIP Response Fehler | Code={statusCode} | CallID={callId} | Remote={remoteEndPoint} | Fehler={ex.Message}");
            }
        }

        private async Task HandleInviteAsync(SIPTransport transport, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIPRequest request, SIPEndPoint receivedLocalEndPoint) {
            if (_faxCallController.OwnsCall(request.Header?.CallId)) {
                await _faxCallController.HandleReInviteAsync(transport, localEndPoint, remoteEndPoint, request);
            } else if (_faxCallController.CanHandleInvite(request)) {
                await _faxCallController.HandleInviteAsync(transport, localEndPoint, remoteEndPoint, request, receivedLocalEndPoint);
            } else if (_sipExtRuntimeEnabled && TryClassifyExternalInboundInvite(request, out string extFromNumber, out string extToNumber)) {
                await HandleExternalInboundToSipIntAsync(transport, localEndPoint, remoteEndPoint, request, receivedLocalEndPoint, extFromNumber, extToNumber);
            } else if (_gsmCallController.CanHandleInvite(request)) {
                await _gsmCallController.HandleInviteAsync(transport, localEndPoint, remoteEndPoint, request, receivedLocalEndPoint);
            } else if (_sipIntRuntimeEnabled && await TryHandleServiceInviteAsync(transport, localEndPoint, remoteEndPoint, request, receivedLocalEndPoint).ConfigureAwait(false)) {
                return;
            } else if (_sipIntRuntimeEnabled && await TryHandleSipIntUserInviteWithForkingAgentAsync(transport, localEndPoint, remoteEndPoint, request, receivedLocalEndPoint).ConfigureAwait(false)) {
                return;
            } else if (_sipExtRuntimeEnabled && _sipExtCallController.CanHandleInvite(request)) {
                await _sipExtCallController.HandleInviteAsync(transport, localEndPoint, remoteEndPoint, request, receivedLocalEndPoint);
            } else if (_sipIntRuntimeEnabled) {
                await _sipIntCallController.HandleInviteAsync(transport, localEndPoint, remoteEndPoint, request, receivedLocalEndPoint);
            } else {
                _logger?.Warn($"SIP INVITE abgewiesen | CallID={request.Header?.CallId ?? "-"} | Grund=SIPInt RuntimeAreaControl deaktiviert und kein anderer Bereich zustÃ¤ndig");
                await SendSimpleResponseAsync(transport, request, SIPResponseStatusCodesEnum.TemporarilyUnavailable, "SIPInt RuntimeAreaControl deaktiviert.");
            }
        }

        private async Task HandleAckAsync(SIPTransport transport, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIPRequest request) {
            if (await TryHandleServiceAckAsync(localEndPoint, remoteEndPoint, request).ConfigureAwait(false)) return;
            if (await TryHandleGsmInboundLegBRequestAsync(transport, localEndPoint, remoteEndPoint, request).ConfigureAwait(false)) return;
            if (_faxCallController.OwnsCall(request.Header?.CallId)) await _faxCallController.HandleAckAsync(transport, localEndPoint, remoteEndPoint, request);
            else if (_gsmCallController.OwnsCall(request.Header?.CallId)) await _gsmCallController.HandleAckAsync(transport, localEndPoint, remoteEndPoint, request);
            else if (_sipExtCallController.OwnsCall(request.Header?.CallId)) await _sipExtCallController.HandleAckAsync(transport, localEndPoint, remoteEndPoint, request);
            else await _sipIntCallController.HandleAckAsync(transport, localEndPoint, remoteEndPoint, request);
        }

        private async Task HandleByeAsync(SIPTransport transport, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIPRequest request) {
            if (await TryHandleServiceByeAsync(transport, localEndPoint, remoteEndPoint, request).ConfigureAwait(false)) return;
            if (await TryHandleGsmInboundLegBRequestAsync(transport, localEndPoint, remoteEndPoint, request).ConfigureAwait(false)) return;
            if (_faxCallController.OwnsCall(request.Header?.CallId)) await _faxCallController.HandleByeAsync(transport, localEndPoint, remoteEndPoint, request);
            else if (_gsmCallController.OwnsCall(request.Header?.CallId)) await _gsmCallController.HandleByeAsync(transport, localEndPoint, remoteEndPoint, request);
            else if (_sipExtCallController.OwnsCall(request.Header?.CallId)) await _sipExtCallController.HandleByeAsync(transport, localEndPoint, remoteEndPoint, request);
            else await _sipIntCallController.HandleByeAsync(transport, localEndPoint, remoteEndPoint, request);
        }

        private async Task HandleInfoAsync(SIPTransport transport, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIPRequest request) {
            if (await TryHandleServiceInfoAsync(transport, request).ConfigureAwait(false)) return;
            if (_faxCallController.OwnsCall(request.Header?.CallId)) {
                await SendSimpleResponseAsync(transport, request, SIPResponseStatusCodesEnum.Ok, "FAX INFO ignoriert.");
            } else if (_gsmCallController.OwnsCall(request.Header?.CallId) || _sipExtCallController.OwnsCall(request.Header?.CallId)) {
                _logger?.Warn($"SIP INFO abgewiesen | Bereich=GSM/SIPExt | CallID={request.Header?.CallId ?? "-"} | Grund=DTMF gehoert in die eigene Bereichslogik");
                await SendSimpleResponseAsync(transport, request, SIPResponseStatusCodesEnum.MethodNotAllowed, "SIP INFO DTMF ist fuer diesen Bereich nicht im CallManager freigegeben.");
            } else {
                await _sipIntCallController.HandleInfoAsync(transport, localEndPoint, remoteEndPoint, request);
            }
        }

        private async Task HandleCancelAsync(SIPTransport transport, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIPRequest request) {
            if (await _forkingAgent.TryCancelForkAsync(transport, localEndPoint, remoteEndPoint, request).ConfigureAwait(false)) return;

            if (await TryHandleServiceCancelAsync(transport, localEndPoint, remoteEndPoint, request).ConfigureAwait(false)) return;
            if (await TryHandleGsmInboundLegBRequestAsync(transport, localEndPoint, remoteEndPoint, request).ConfigureAwait(false)) return;

            if (_faxCallController.OwnsCall(request.Header?.CallId)) await _faxCallController.HandleCancelAsync(transport, localEndPoint, remoteEndPoint, request);
            else if (_gsmCallController.OwnsCall(request.Header?.CallId)) await _gsmCallController.HandleCancelAsync(transport, localEndPoint, remoteEndPoint, request);
            else if (_sipExtCallController.OwnsCall(request.Header?.CallId)) await _sipExtCallController.HandleCancelAsync(transport, localEndPoint, remoteEndPoint, request);
            else await _sipIntCallController.HandleCancelAsync(transport, localEndPoint, remoteEndPoint, request);
        }


        private async Task<bool> TryHandleServiceInviteAsync(
            SIPTransport transport,
            SIPEndPoint localEndPoint,
            SIPEndPoint remoteEndPoint,
            SIPRequest request,
            SIPEndPoint receivedLocalEndPoint) {

            try {
                string legACallId = request.Header?.CallId ?? CallProperties.CreateNewCallId();
                string fromNumber = _sipIntCommands.LegA.ExtractFromNumber(request);
                string toNumber = _sipIntCommands.LegA.ExtractToNumber(request);
                SIP_DialPlanDecision decision = _routing.CreateDecision(fromNumber, toNumber);
                SIPIntResolvedTarget route = _routing.HandleInvite(legACallId, fromNumber, toNumber);

                if (!decision.IsInternalPath || !route.Success || !route.IsServiceTarget) {
                    return false;
                }

                if (!_svcAvailable) {
                    await SendSimpleResponseAsync(transport, request, SIPResponseStatusCodesEnum.TemporarilyUnavailable, "SVC ist ohne aktiven SIPInt-Benutzer nicht verfuegbar.").ConfigureAwait(false);
                    return true;
                }

                if (route.ServiceLeg == null) {
                    await SendSimpleResponseAsync(transport, request, SIPResponseStatusCodesEnum.TemporarilyUnavailable, "Service-Leg-D nicht gefunden.").ConfigureAwait(false);
                    return true;
                }

                SIP_ServerSession session = _sessions.CreateServiceSession(legACallId, fromNumber, toNumber, request, remoteEndPoint, receivedLocalEndPoint, route.ServiceLeg);
                ApplyIngressMediaAddress(session, receivedLocalEndPoint, remoteEndPoint, null);
                StartRuntimeCall(session, legACallId, fromNumber, toNumber, route, isServiceSession: true);

                SIPResponse trying = _sipIntCommands.LegA.BuildResponseFromInvite(session, SIPResponseStatusCodesEnum.Trying, null);
                await SendSipIntLegAResponseAsync(transport, session, trying).ConfigureAwait(false);

                SIP_ServiceStartResult result = await _svcCallController.StartLegDAsync(session, request).ConfigureAwait(false);
                if (!result.Success) {
                    SIPResponse fail = _sipIntCommands.LegA.BuildResponseFromInvite(session, SIPResponseStatusCodesEnum.ServiceUnavailable, result.Reason ?? "Service konnte nicht gestartet werden.");
                    await SendSipIntLegAResponseAsync(transport, session, fail).ConfigureAwait(false);
                    _svcCallController.EndLegD(session, result.Reason ?? "service-start-failed");
                    _sessions.MarkAllLegsEnded(session.SessionId, "ServiceFailed");
                    MarkRuntimeFailed(session.SessionId, result.Reason ?? "ServiceFailed");
                    return true;
                }

                SIPResponse ok = _sipIntCommands.LegA.BuildResponseFromInvite(session, SIPResponseStatusCodesEnum.Ok, null, result.AnswerSdp, string.IsNullOrWhiteSpace(result.AnswerSdp) ? null : "application/sdp");
                await SendSipIntLegAResponseAsync(transport, session, ok).ConfigureAwait(false);
                _logger?.Info($"CallManager Service gestartet | SessionID={session.SessionId} | LegA={session.LegA.CallId} | LegD={session.LegD.CallId} | Service={route.ServiceLeg.InternalNumber}");
                return true;
            } catch (Exception ex) {
                _logger?.Error($"CallManager Service-INVITE Fehler | CallID={request.Header?.CallId ?? "-"} | Fehler={ex.Message}");
                await SendSimpleResponseAsync(transport, request, SIPResponseStatusCodesEnum.InternalServerError, ex.Message).ConfigureAwait(false);
                return true;
            }
        }

        private async Task<bool> TryHandleServiceAckAsync(SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIPRequest request) {
            if (!TryGetServiceSession(request.Header?.CallId, out SIP_ServerSession? session)) return false;
            _sipIntCommands.LegA.StoreAckReceived(session, request, remoteEndPoint, localEndPoint);
            _svcCallController.MarkLegDAnswered(session);
            _sessions.MarkAnswered(session.SessionId);
            MarkRuntimeAnswered(session.SessionId);
            return await Task.FromResult(true).ConfigureAwait(false);
        }

        private async Task<bool> TryHandleServiceByeAsync(SIPTransport transport, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIPRequest request) {
            if (!TryGetServiceSession(request.Header?.CallId, out SIP_ServerSession? session)) return false;
            _sipIntCommands.LegA.StoreByeReceived(session, request, remoteEndPoint, localEndPoint);
            await SendSimpleResponseAsync(transport, request, SIPResponseStatusCodesEnum.Ok, null).ConfigureAwait(false);
            _svcCallController.EndLegD(session, "sipint-legA-bye");
            _sessions.MarkAllLegsEnded(session.SessionId, "Ended");
            MarkRuntimeEnded(session.SessionId, "BYE");
            return true;
        }

        private async Task<bool> TryHandleServiceCancelAsync(SIPTransport transport, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIPRequest request) {
            if (!TryGetServiceSession(request.Header?.CallId, out SIP_ServerSession? session)) return false;
            _sipIntCommands.LegA.StoreCancelReceived(session, request, remoteEndPoint, localEndPoint);
            await SendSimpleResponseAsync(transport, request, SIPResponseStatusCodesEnum.Ok, null).ConfigureAwait(false);
            if (session.LegA.InviteReceived != null) {
                SIPResponse terminated = _sipIntCommands.LegA.BuildResponseFromInvite(session, SIPResponseStatusCodesEnum.RequestTerminated, null);
                await SendSipIntLegAResponseAsync(transport, session, terminated).ConfigureAwait(false);
            }
            _svcCallController.EndLegD(session, "sipint-legA-cancel");
            _sessions.MarkAllLegsEnded(session.SessionId, "Cancelled");
            MarkRuntimeEnded(session.SessionId, "CANCEL");
            return true;
        }

        private async Task<bool> TryHandleServiceInfoAsync(SIPTransport transport, SIPRequest request) {
            if (!TryGetServiceSession(request.Header?.CallId, out SIP_ServerSession? session)) return false;
            if (!_dtmfManager.TryParseSipInfoDigit(request, out char digit)) {
                await SendSimpleResponseAsync(transport, request, SIPResponseStatusCodesEnum.BadRequest, "DTMF digit fehlt.").ConfigureAwait(false);
                return true;
            }

            TKBase_DTMF_Result result = _dtmfManager.PushDigit(session.SessionId, request.Header?.CallId, "LegA", "SIP-INFO", digit);
            if (result.Completed || result.Cancelled) {
                await _svcCallController.HandleDtmfSequenceAsync(session, result.Sequence ?? digit.ToString()).ConfigureAwait(false);
            }
            await SendSimpleResponseAsync(transport, request, SIPResponseStatusCodesEnum.Ok, null).ConfigureAwait(false);
            return true;
        }

        private async void HandleGsmGatewayCallStateChanged(TKBase_GsmGateway.GsmCallStateChangedEventArgs args) {
            try {
                await HandleGsmGatewayCallStateChangedAsync(args).ConfigureAwait(false);
            } catch (Exception ex) {
                _logger?.Error($"CallManager GSM-Inbound Gateway-Status Fehler | SessionID={args?.SessionId ?? "-"} | State={args?.State ?? "-"} | Fehler={ex.Message}");
            }
        }

        private async Task HandleGsmGatewayCallStateChangedAsync(TKBase_GsmGateway.GsmCallStateChangedEventArgs? args) {
            if (args == null || string.IsNullOrWhiteSpace(args.SessionId)) return;
            if (!string.Equals(args.Direction, "Inbound", StringComparison.OrdinalIgnoreCase)) return;
            if (!_sessions.TryGet(args.SessionId, out SIP_ServerSession? rootSession) || rootSession == null || !rootSession.IsGsmSession) return;

            string state = (args.State ?? string.Empty).Trim();
            if (string.Equals(state, "Ringing", StringComparison.OrdinalIgnoreCase)) {
                await TryStartGsmInboundSipIntLegAsync(rootSession).ConfigureAwait(false);
                return;
            }

            if ((string.Equals(state, "Answered", StringComparison.OrdinalIgnoreCase) || string.Equals(state, "Connected", StringComparison.OrdinalIgnoreCase)) && IsGsmInboundBranch(rootSession) && rootSession.IsAnswered) {
                _gsmCallController.StartInboundAudioBridgeForCallManager(rootSession, "GSM inbound connected");
                return;
            }

            if (IsGsmFinalState(state)) {
                await TryEndGsmInboundSipIntLegAsync(rootSession, state, args.RawLine).ConfigureAwait(false);
            }
        }

        private async Task<bool> TryStartGsmInboundSipIntLegAsync(SIP_ServerSession rootSession) {
            if (!_sipIntRuntimeEnabled) {
                _gsmCallController.HangupInboundGsmCallForCallManager(rootSession, "SIPInt RuntimeAreaControl deaktiviert");
                _sessions.SetGsmEnded(rootSession.SessionId, "SipIntDisabled", null);
                MarkRuntimeFailed(rootSession.SessionId, "SIPInt RuntimeAreaControl deaktiviert");
                return true;
            }

            if (rootSession.LegB.InviteSent != null
                || !string.IsNullOrWhiteSpace(rootSession.ParentSessionId)
                || _sessions.GetSnapshot().Any(x => IsGsmInboundBranch(x) && string.Equals(x.ParentSessionId, rootSession.SessionId, StringComparison.OrdinalIgnoreCase))) {
                return false;
            }

            SIPIntResolvedTarget route = _routing.ResolveInboundExternalTarget(rootSession.ToNumber);
            if (!route.Success || !route.IsUserTarget || !route.TargetUserId.HasValue) {
                _logger?.Warn($"CallManager GSM-Inbound abgewiesen | RootSessionID={rootSession.SessionId} | Von={rootSession.FromNumber} | An={rootSession.ToNumber} | Grund={route.Reason ?? "Kein SIPInt-Ziel"}");
                _gsmCallController.HangupInboundGsmCallForCallManager(rootSession, route.Reason ?? "Kein SIPInt-Ziel");
                _sessions.SetGsmEnded(rootSession.SessionId, "NoSipIntTarget", null);
                MarkRuntimeFailed(rootSession.SessionId, route.Reason ?? "NoSipIntTarget");
                return true;
            }

            SIP_ServerSession branch = _sessions.CreateGsmInboundForkBranch(rootSession, route);
            SIPEndPoint targetEndPoint = _sipIntCommands.ResolveRouteEndPoint(route);
            SIPEndPoint legBLocalEndPoint = BuildLocalEndPointForRemote(targetEndPoint);
            ApplyIngressMediaAddress(branch, legBLocalEndPoint, targetEndPoint, null);
            StartRuntimeCall(branch, branch.LegA.CallId ?? rootSession.LegA.CallId ?? branch.SessionId, branch.FromNumber, branch.ToNumber, route, isServiceSession: false);

            if (!_gsmCallController.PrepareInboundSipOfferForCallManager(branch, out string offerSdp, out string mediaReason)) {
                _gsmCallController.HangupInboundGsmCallForCallManager(branch, mediaReason);
                _sessions.MarkAllLegsEnded(branch.SessionId, "MediaFailed");
                MarkRuntimeFailed(branch.SessionId, mediaReason);
                return true;
            }

            SIPRequest syntheticInvite = BuildGsmInboundSyntheticInvite(branch, route, targetEndPoint, legBLocalEndPoint, offerSdp);
            SIPRequest outboundInvite = _sipIntCommands.LegB.BuildInternalInvite(branch, syntheticInvite, route, targetEndPoint, legBLocalEndPoint);
            _sipIntCommands.LegB.StoreInviteSent(branch, outboundInvite, targetEndPoint, legBLocalEndPoint);
            await _transportSendRequestAsync(targetEndPoint, outboundInvite).ConfigureAwait(false);
            UpdateRuntimeInviteOutbound(branch.SessionId, branch.LegB.CallId);
            _logger?.Info($"CallManager GSM-Inbound SipInt.LegB INVITE gesendet | SessionID={branch.SessionId} | LegA={branch.LegA.CallId} | LegB={branch.LegB.CallId} | Ziel={targetEndPoint}");
            return true;
        }

        private async Task _transportSendRequestAsync(SIPEndPoint targetEndPoint, SIPRequest request) {
            SIPTransport? transport = FindBoundTransport();
            if (transport == null) throw new InvalidOperationException("SIP-Transport fuer CallManager GSM-Inbound fehlt.");
            await transport.SendRequestAsync(targetEndPoint, request).ConfigureAwait(false);
        }

        private async Task<bool> TryHandleGsmInboundLegBResponseAsync(SIPTransport transport, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIPResponse response) {
            string callId = response.Header?.CallId ?? string.Empty;
            if (!_sessions.TryGet(callId, out SIP_ServerSession? session) || !IsGsmInboundBranch(session)) return false;

            _sipIntCommands.LegB.StoreResponseReceived(session!, response, remoteEndPoint, localEndPoint);
            if (response.Header?.CSeqMethod != SIPMethodsEnum.INVITE) {
                return true;
            }

            if (response.StatusCode >= 100 && response.StatusCode < 200) {
                MarkRuntimeRinging(session!.SessionId);
                return true;
            }

            if (response.StatusCode >= 200 && response.StatusCode < 300) {
                if (!_gsmCallController.PrepareInboundSipAnswerForCallManager(session!, response, out string reason)) {
                    _gsmCallController.HangupInboundGsmCallForCallManager(session!, reason);
                    _sessions.MarkAllLegsEnded(session!.SessionId, "MediaFailed");
                    MarkRuntimeFailed(session!.SessionId, reason);
                    return true;
                }

                SIPRequest ack = _sipIntCommands.LegB.BuildAckForConfirmedDialog(session!, response, localEndPoint);
                _sipIntCommands.LegB.StoreAckSent(session!, ack, remoteEndPoint, localEndPoint);
                await transport.SendRequestAsync(remoteEndPoint, ack).ConfigureAwait(false);
                _gsmCallController.AnswerInboundGsmCallForCallManager(session!);
                _sessions.MarkGsmLegConnected(session!.SessionId);
                MarkRuntimeAnswered(session!.SessionId);
                _gsmCallController.StartInboundAudioBridgeForCallManager(session!, "SIPInt.LegB answered");
                return true;
            }

            SIPRequest failAck = _sipIntCommands.LegB.BuildAckForFailedInviteResponse(session!, response, localEndPoint);
            _sipIntCommands.LegB.StoreAckSent(session!, failAck, remoteEndPoint, localEndPoint);
            await transport.SendRequestAsync(remoteEndPoint, failAck).ConfigureAwait(false);
            _gsmCallController.HangupInboundGsmCallForCallManager(session!, response.ReasonPhrase ?? "SIPInt.LegB rejected");
            _sessions.MarkAllLegsEnded(session!.SessionId, "Rejected");
            MarkRuntimeFailed(session!.SessionId, response.ReasonPhrase ?? "Rejected");
            return true;
        }

        private async Task<bool> TryHandleGsmInboundLegBRequestAsync(SIPTransport transport, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIPRequest request) {
            string callId = request.Header?.CallId ?? string.Empty;
            if (!_sessions.TryGet(callId, out SIP_ServerSession? session) || !IsGsmInboundBranch(session)) return false;

            if (request.Method == SIPMethodsEnum.ACK) {
                _sipIntCommands.LegB.StoreAckReceived(session!, request, remoteEndPoint, localEndPoint);
                _gsmCallController.StartInboundAudioBridgeForCallManager(session!, "SIPInt.LegB ACK");
                return true;
            }

            if (request.Method == SIPMethodsEnum.BYE) {
                _sipIntCommands.LegB.StoreByeReceived(session!, request, remoteEndPoint, localEndPoint);
                await SendSimpleResponseAsync(transport, request, SIPResponseStatusCodesEnum.Ok, null).ConfigureAwait(false);
                _gsmCallController.HangupInboundGsmCallForCallManager(session!, "SIPInt.LegB BYE");
                _gsmCallController.StopInboundAudioBridgeForCallManager(session!);
                _sessions.MarkAllLegsEnded(session!.SessionId, "Ended");
                MarkRuntimeEnded(session!.SessionId, "BYE");
                return true;
            }

            if (request.Method == SIPMethodsEnum.CANCEL) {
                await SendSimpleResponseAsync(transport, request, SIPResponseStatusCodesEnum.Ok, null).ConfigureAwait(false);
                _gsmCallController.HangupInboundGsmCallForCallManager(session!, "SIPInt.LegB CANCEL");
                _gsmCallController.StopInboundAudioBridgeForCallManager(session!);
                _sessions.MarkAllLegsEnded(session!.SessionId, "Cancelled");
                MarkRuntimeEnded(session!.SessionId, "CANCEL");
                return true;
            }

            return false;
        }

        private async Task TryEndGsmInboundSipIntLegAsync(SIP_ServerSession rootOrBranch, string status, string? rawLine) {
            SIP_ServerSession? branch = IsGsmInboundBranch(rootOrBranch)
                ? rootOrBranch
                : _sessions.GetSnapshot().FirstOrDefault(x => IsGsmInboundBranch(x) && string.Equals(x.ParentSessionId, rootOrBranch.SessionId, StringComparison.OrdinalIgnoreCase));

            if (branch == null) {
                _sessions.SetGsmEnded(rootOrBranch.SessionId, status, rawLine);
                MarkRuntimeEnded(rootOrBranch.SessionId, status);
                return;
            }

            try {
                SIPTransport? transport = FindBoundTransport();
                if (transport != null && branch.LegB.SipEndPoint != null && branch.LegB.LocalSipEndPoint != null && branch.LegB.InviteSent != null && !branch.LegB.IsEnded) {
                    SIPRequest request = branch.IsAnswered
                        ? _sipIntCommands.LegB.BuildDialogRequest(branch, SIPMethodsEnum.BYE, branch.LegB.LocalSipEndPoint)
                        : _sipIntCommands.LegB.BuildDialogRequest(branch, SIPMethodsEnum.CANCEL, branch.LegB.LocalSipEndPoint);
                    await transport.SendRequestAsync(branch.LegB.SipEndPoint, request).ConfigureAwait(false);
                }
            } catch (Exception ex) {
                _logger?.Warn($"CallManager GSM-Inbound SIPInt.LegB Abschlusswarnung | SessionID={branch.SessionId} | Fehler={ex.Message}");
            } finally {
                _gsmCallController.StopInboundAudioBridgeForCallManager(branch);
                _sessions.SetGsmEnded(branch.SessionId, status, rawLine);
                _sessions.MarkAllLegsEnded(branch.SessionId, status);
                MarkRuntimeEnded(branch.SessionId, status);
            }
        }

        private bool TryGetServiceSession(string? callId, out SIP_ServerSession? session) {
            session = null;
            return !string.IsNullOrWhiteSpace(callId)
                && _sessions.TryGet(callId, out session)
                && session != null
                && (session.IsServiceSession || session.HasAttachedServiceLegD);
        }

        private async Task SendSipIntLegAResponseAsync(SIPTransport transport, SIP_ServerSession session, SIPResponse response) {
            _sipIntCommands.LegA.StoreResponseSent(session, response);
            await transport.SendResponseAsync(response).ConfigureAwait(false);
        }

        private void ApplyIngressMediaAddress(SIP_ServerSession session, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, string? providerPublicIp) {
            if (_settings.TryResolveIngressMediaAdvertisedIp(localEndPoint.Address, remoteEndPoint.Address, providerPublicIp, out string mediaIp, out string reason)) {
                session.MediaAdvertisedIp = mediaIp;
                session.LegA.MediaAdvertisedIp = mediaIp;
                session.LegB.MediaAdvertisedIp = mediaIp;
                session.LegD.MediaAdvertisedIp = mediaIp;
                return;
            }

            _logger?.Warn($"CallManager MediaAdvertisedIp konnte nicht eindeutig ermittelt werden | SessionID={session.SessionId} | Local={localEndPoint} | Remote={remoteEndPoint} | Grund={reason}");
        }

        private SIPEndPoint BuildLocalEndPointForRemote(SIPEndPoint remoteEndPoint) {
            IPAddress localAddress = _settings.ResolveServerAddressForRemote(remoteEndPoint.Address);
            return new SIPEndPoint(remoteEndPoint.Protocol, localAddress, _settings.SipPort);
        }

        private SIPRequest BuildGsmInboundSyntheticInvite(SIP_ServerSession session, SIPIntResolvedTarget route, SIPEndPoint targetEndPoint, SIPEndPoint localEndPoint, string offerSdp) {
            SIPURI destinationUri = !string.IsNullOrWhiteSpace(route.ContactUri)
                ? SIPURI.ParseSIPURIRelaxed(route.ContactUri)
                : SIPURI.ParseSIPURIRelaxed($"sip:{EscapeSipUser(route.CalledNumber ?? session.ToNumber)}@{targetEndPoint.Address}:{targetEndPoint.Port}");
            string host = destinationUri.Host;
            SIPURI fromUri = SIPURI.ParseSIPURIRelaxed($"sip:{EscapeSipUser(session.FromNumber)}@{localEndPoint.Address}:{localEndPoint.Port}");
            SIPURI toUri = SIPURI.ParseSIPURIRelaxed($"sip:{EscapeSipUser(route.CalledNumber ?? session.ToNumber)}@{host}");
            SIPFromHeader fromHeader = new SIPFromHeader(session.FromNumber, fromUri, Guid.NewGuid().ToString("N"));
            SIPToHeader toHeader = new SIPToHeader(route.TargetUsername ?? route.CalledNumber ?? session.ToNumber, toUri, null);
            SIPRequest invite = SIPRequest.GetRequest(SIPMethodsEnum.INVITE, destinationUri, toHeader, fromHeader);
            invite.Header.CallId = session.LegA.CallId ?? session.SessionId;
            invite.Header.CSeq = 1;
            invite.Header.CSeqMethod = SIPMethodsEnum.INVITE;
            invite.Header.MaxForwards = 70;
            invite.Body = offerSdp;
            invite.Header.ContentType = "application/sdp";
            invite.Header.ContentLength = offerSdp?.Length ?? 0;
            return invite;
        }

        private static string EscapeSipUser(string? value) {
            return Uri.EscapeDataString((value ?? "unknown").Trim());
        }

        private SIPTransport? FindBoundTransport() => _boundTransport;

        private static bool IsGsmInboundBranch(SIP_ServerSession? session) {
            return session != null
                && session.IsGsmSession
                && string.Equals(session.GsmDirection, "Inbound", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(session.ParentSessionId);
        }

        private static bool IsGsmFinalState(string state) {
            return string.Equals(state, "Ended", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "Hangup", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "Busy", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "NoCarrier", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "Error", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "Cancelled", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSipIntAreaSession(SIP_ServerSession session) {
            if (session == null || session.IsFaxSession) return false;
            if (session.IsServiceSession || session.HasAttachedServiceLegD) return true;
            if (session.IsGsmSession) {
                if (string.Equals(session.GsmDirection, "Outbound", StringComparison.OrdinalIgnoreCase)) return true;
                return string.Equals(session.GsmDirection, "Inbound", StringComparison.OrdinalIgnoreCase)
                    && (session.TargetUserId.HasValue || session.GsmTargetUserId.HasValue || session.LegB.SipEndPoint != null || !string.IsNullOrWhiteSpace(session.LegB.TargetContactUri));
            }

            return true;
        }

        private async Task<bool> TryHandleSipIntUserInviteWithForkingAgentAsync(
            SIPTransport transport,
            SIPEndPoint localEndPoint,
            SIPEndPoint remoteEndPoint,
            SIPRequest request,
            SIPEndPoint receivedLocalEndPoint) {

            if (transport == null || request == null) {
                return false;
            }

            try {
                string legACallId = request.Header?.CallId ?? CallProperties.CreateNewCallId();
                string fromNumber = _sipIntCommands.LegA.ExtractFromNumber(request);
                string toNumber = _sipIntCommands.LegA.ExtractToNumber(request);
                SIP_DialPlanDecision decision = _routing.CreateDecision(fromNumber, toNumber);
                SIPIntResolvedTarget route = _routing.HandleInvite(legACallId, fromNumber, toNumber);

                if (!route.Success
                    || !decision.IsInternalPath
                    || !route.IsUserTarget
                    || route.IsExternalTarget
                    || route.IsServiceTarget
                    || route.IsFaxTarget) {
                    return false;
                }

                bool started = await _forkingAgent.TryStartForkAsync(
                    transport,
                    localEndPoint,
                    remoteEndPoint,
                    request,
                    receivedLocalEndPoint,
                    legACallId,
                    fromNumber,
                    toNumber,
                    route,
                    legAIsSipExt: false).ConfigureAwait(false);

                if (started) {
                    _logger?.Info($"CallManager SipInt-Forking gestartet | LegA={legACallId} | Von={fromNumber} | An={toNumber} | ZielUserID={route.TargetUserId}");
                    return true;
                }
            } catch (Exception ex) {
                _logger?.Warn($"CallManager SipInt-Forking nicht uebernommen | CallID={request.Header?.CallId ?? "-"} | Fehler={ex.Message}");
            }

            return false;
        }

        private bool TryClassifyExternalInboundInvite(SIPRequest request, out string fromNumber, out string toNumber) {
            fromNumber = string.Empty;
            toNumber = string.Empty;

            if (request == null) {
                return false;
            }

            try {
                fromNumber = _sipExtCommands.LegA.ExtractFromNumber(request);
                toNumber = _sipExtCommands.LegA.ExtractToNumber(request);
                SIP_DialPlanDecision decision = _routing.CreateDecision(fromNumber, toNumber);
                if (decision.IsFaxPath) {
                    return false;
                }

                foreach (string candidate in BuildInboundCalledNumberCandidates(request, toNumber)) {
                    if (_routing.HasInboundExternalRoute(candidate)) {
                        toNumber = candidate;
                        _logger?.Info($"CallManager INVITE als Provider-Inbound erkannt | CallID={request.Header?.CallId ?? "-"} | Von={fromNumber} | An={toNumber} | SourceType={decision.SourceType} | Grund=ZielDID mit aktiver Inbound-Route");
                        return true;
                    }
                }

                return decision.SourceType == TKBase_DialSourceType.External;
            } catch (Exception ex) {
                _logger?.Warn($"CallManager INVITE Klassifizierung fehlgeschlagen | CallID={request.Header?.CallId ?? "-"} | Fehler={ex.Message}");
                return false;
            }
        }

        private static IEnumerable<string> BuildInboundCalledNumberCandidates(SIPRequest request, string extractedToNumber) {
            HashSet<string> emitted = new(StringComparer.OrdinalIgnoreCase);

            void Add(string? value) {
                value = (value ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(value)) {
                    emitted.Add(value);
                }
            }

            Add(extractedToNumber);
            Add(request.URI?.User);
            Add(request.Header?.To?.ToURI?.User);

            if (request.Header?.UnknownHeaders != null) {
                foreach (string header in request.Header.UnknownHeaders) {
                    if (string.IsNullOrWhiteSpace(header)) {
                        continue;
                    }

                    int colonIndex = header.IndexOf(':');
                    if (colonIndex <= 0) {
                        continue;
                    }

                    string headerName = header.Substring(0, colonIndex).Trim();
                    if (!string.Equals(headerName, "P-Called-Party-ID", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(headerName, "X-Called-Number", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(headerName, "Diversion", StringComparison.OrdinalIgnoreCase)) {
                        continue;
                    }

                    Add(ExtractSipUserFromHeaderValue(header.Substring(colonIndex + 1)));
                }
            }

            foreach (string value in emitted) {
                yield return value;
            }
        }

        private static string? ExtractSipUserFromHeaderValue(string? value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return null;
            }

            string text = value.Trim().Trim('<', '>');
            int sipIndex = text.IndexOf("sip:", StringComparison.OrdinalIgnoreCase);
            int sipsIndex = text.IndexOf("sips:", StringComparison.OrdinalIgnoreCase);
            int telIndex = text.IndexOf("tel:", StringComparison.OrdinalIgnoreCase);

            if (sipIndex >= 0) {
                text = text.Substring(sipIndex + 4);
            } else if (sipsIndex >= 0) {
                text = text.Substring(sipsIndex + 5);
            } else if (telIndex >= 0) {
                text = text.Substring(telIndex + 4);
            }

            int at = text.IndexOf('@');
            if (at >= 0) text = text.Substring(0, at);
            int semi = text.IndexOf(';');
            if (semi >= 0) text = text.Substring(0, semi);
            int comma = text.IndexOf(',');
            if (comma >= 0) text = text.Substring(0, comma);

            text = text.Trim().Trim('"', '<', '>');
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        private async Task HandleExternalInboundToSipIntAsync(
            SIPTransport transport,
            SIPEndPoint localEndPoint,
            SIPEndPoint remoteEndPoint,
            SIPRequest request,
            SIPEndPoint receivedLocalEndPoint,
            string fromNumber,
            string toNumber) {

            string legACallId = request.Header?.CallId ?? CallProperties.CreateNewCallId();

            if (!_sipIntRuntimeEnabled) {
                _logger?.Warn($"CallManager InboundExtern abgewiesen | LegA={legACallId} | Von={fromNumber} | An={toNumber} | Grund=SIPInt RuntimeAreaControl deaktiviert");
                await SendSimpleResponseAsync(transport, request, SIPResponseStatusCodesEnum.TemporarilyUnavailable, "SIPInt RuntimeAreaControl deaktiviert.");
                return;
            }

            SIPIntResolvedTarget route = _routing.ResolveInboundExternalTarget(toNumber);
            if (!route.Success) {
                _logger?.Warn($"CallManager InboundExtern abgewiesen | LegA={legACallId} | Von={fromNumber} | An={toNumber} | Grund={route.Reason}");
                await SendSimpleResponseAsync(transport, request, SIPResponseStatusCodesEnum.TemporarilyUnavailable, route.Reason ?? "Inbound-Routing fehlgeschlagen.");
                return;
            }

            if (route.IsFaxTarget) {
                _logger?.Warn($"CallManager InboundExtern Fax-Ziel abgewiesen | LegA={legACallId} | Von={fromNumber} | An={toNumber} | Grund=Fax gehoert in TKFax_CallController");
                await SendSimpleResponseAsync(transport, request, SIPResponseStatusCodesEnum.TemporarilyUnavailable, "Fax-Ziel gehoert in den Fax-Bereich.");
                return;
            }

            if (!route.IsUserTarget || !route.TargetUserId.HasValue) {
                _logger?.Warn($"CallManager InboundExtern abgewiesen | LegA={legACallId} | Von={fromNumber} | An={toNumber} | RouteKind={route.TargetKind} | Grund=Nur SIPInt-User-Ziele erlaubt");
                await SendSimpleResponseAsync(transport, request, SIPResponseStatusCodesEnum.TemporarilyUnavailable, "Inbound-Ziel ist kein SIPInt-User.");
                return;
            }

            bool started = await _forkingAgent.TryStartForkAsync(
                transport,
                localEndPoint,
                remoteEndPoint,
                request,
                receivedLocalEndPoint,
                legACallId,
                fromNumber,
                toNumber,
                route,
                legAIsSipExt: true).ConfigureAwait(false);

            if (!started) {
                _logger?.Warn($"CallManager InboundExtern abgewiesen | LegA={legACallId} | Von={fromNumber} | An={toNumber} | Grund=Keine SIPInt-Branches gestartet");
                await SendSimpleResponseAsync(transport, request, SIPResponseStatusCodesEnum.TemporarilyUnavailable, "Kein internes Ziel erreichbar.");
                return;
            }

            _logger?.Info($"CallManager InboundExtern gestartet | LegA={legACallId} | Von={fromNumber} | An={toNumber} | ZielUserID={route.TargetUserId}");
        }

        private static bool IsSipExtSession(SIP_ServerSession session) {
            return session.RouteTargetKind == SIPIntRouteTargetKind.External
                || session.ProviderAccountId.HasValue
                || !string.IsNullOrWhiteSpace(session.ProviderName)
                || (!string.IsNullOrWhiteSpace(session.ParentSessionId)
                    && (session.ParentSessionId.StartsWith("FORK-EXT-", StringComparison.OrdinalIgnoreCase)
                        || session.ParentSessionId.StartsWith("SIPEXT-", StringComparison.OrdinalIgnoreCase)));
        }

        private async Task SendSimpleResponseAsync(SIPTransport transport, SIPRequest request, SIPResponseStatusCodesEnum status, string? reason) {
            string method = request?.Method.ToString() ?? "-";
            string callId = request?.Header?.CallId ?? "-";

            try {
                await transport.SendResponseAsync(SIPResponse.GetResponse(request, status, reason));
            } catch (Exception ex) {
                _logger?.Warn($"SIP Antwort konnte nicht gesendet werden | Method={method} | Status={(int)status} {status} | CallID={callId} | Fehler={ex.Message}");
            }
        }
    }
}
