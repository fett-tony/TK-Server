/*
 * TKFax_CallController
 * Aufgabe:
 * - Steuert ausschliesslich Fax.LegA und Fax.LegB innerhalb einer bestehenden TKBase_SessionManager-Session.
 * - Arbeitet fuer SoftFax/HardwareFax als B2BUA/Vermittler: Fax.LegA <-> TK-Server <-> Fax.LegB.
 * - LegA wird erst final beantwortet, wenn LegB geantwortet hat.
 * - Startet im Vermittlungsfall keinen lokalen Fax-Receive-/Send-Endpunkt.
 * - Koordiniert TKFax_Command, TKFax_Event und TKFax_Media fuer die getrennte Leg-A/Leg-B-Vermittlung.
 * - Nutzt den TKBase_SessionManager zur Session-Verwaltung, nicht fuer fremde Bereichslogik.
 *
 * Nicht Aufgabe:
 * - Kein globales Routing entscheiden.
 * - Keine GSM- oder Provider-Transportlogik uebernehmen.
 * - Keine DB-Verbindung oeffnen.
 *
 * Diese information darf nicht geloescht werden!
 */

namespace TKServer.FAX {
    using SIPSorcery.SIP;
    using System;
    using System.Data;
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

    internal sealed class TKFax_CallController {
        private readonly TKBase_LogManager? _logger;
        private readonly TKBase_SessionManager? _sessions;
        private readonly TKBase_ServerSettings? _settings;
        private readonly TKSQL_Commands _sipSql;
        private readonly TKBase_RoutingManager? _baseRouting;
        private readonly TKFax_Commands _commands;
        private readonly TKFax_Events _events;
        private readonly TKFax_Media _media;
        private readonly TKFax_Routing _routing;

        public TKFax_CallController(TKBase_SessionManager? sessions = null, TKBase_ServerSettings? settings = null, TKBase_MediaManager? mediaManager = null, TKBase_LogManager? logger = null)
            : this(sessions, null, settings, mediaManager, logger) {
        }

        public TKFax_CallController(TKBase_SessionManager? sessions, TKBase_RoutingManager? routingManager, TKBase_ServerSettings? settings, TKBase_MediaManager? mediaManager, TKBase_LogManager? logger) {
            _logger = logger;
            _sessions = sessions;
            _settings = settings;
            _baseRouting = routingManager;
            _sipSql = new TKSQL_Commands(logger);
            _media = new TKFax_Media(mediaManager, logger);
            _commands = new TKFax_Commands(logger);
            _events = new TKFax_Events(logger);
            _routing = new TKFax_Routing(logger);
        }

        public bool OwnsCall(string? callIdOrSessionId) {
            if (_sessions == null || string.IsNullOrWhiteSpace(callIdOrSessionId)) return false;
            return _sessions.TryGet(callIdOrSessionId, out SIP_ServerSession? session)
                && session != null
                && session.IsFaxSession;
        }

        public TKFax_Session CreateFaxView(SIP_ServerSession session) => TKFax_Session.FromServerSession(session);

        public void MarkOutboundFaxSession(SIP_ServerSession session, string faxUserNumber, string targetNumber) {
            if (session == null) return;
            session.IsFaxSession = true;
            session.FaxDirection = "Outbound";
            session.FaxUserNumber = faxUserNumber;
            session.FaxUserName = ResolveUsernameByNumberOrName(faxUserNumber) ?? faxUserNumber;
            session.FaxStatus = "Trying";
            session.FaxMode = TKFaxMediaMode.Unknown.ToString();
            session.UpdatedAt = DateTime.Now;
            _logger?.Info($"FAX Outbound Session markiert | SessionID={session.SessionId} | FaxUser={faxUserNumber} | Ziel={targetNumber}");
        }

        public bool CanHandleInvite(SIPRequest request) {
            if (request == null || _baseRouting == null) return false;
            string fromNumber = ExtractFromNumber(request);
            string toNumber = ExtractToNumber(request);
            SIP_DialPlanDecision decision = _baseRouting.CreateDecision(fromNumber, toNumber);

            // Die Zuordnung der angerufenen Nummer entscheidet ueber den Bereich.
            // Eine als Fax reservierte DID bleibt deshalb auch bei einem spaeteren
            // Routing- oder Medienfehler im FAX-Bereich und darf nicht auf SipInt fallen.
            return decision.TargetType == TKBase_DialTargetType.Fax;
        }

        public async Task HandleInviteAsync(SIPTransport transport, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIPRequest request, SIPEndPoint receivedLocalEndPoint) {
            if (transport == null || request == null) return;

            SIP_ServerSession? session = null;
            string legACallId = request.Header?.CallId ?? CallProperties.CreateNewCallId();
            string fromNumber = ExtractFromNumber(request);
            string toNumber = ExtractToNumber(request);

            try {
                if (_sessions == null || _baseRouting == null) {
                    await SendSimpleResponseAsync(transport, request, SIPResponseStatusCodesEnum.TemporarilyUnavailable, "FAX Controller nicht vollstaendig initialisiert.").ConfigureAwait(false);
                    return;
                }

                SIPIntResolvedTarget route = ResolveFaxInviteRoute(legACallId, fromNumber, toNumber);
                if (!route.Success || !route.IsFaxTarget) {
                    _logger?.Warn($"FAX INVITE abgewiesen | LegA={legACallId} | Von={fromNumber} | An={toNumber} | Grund={route.Reason}");
                    await SendSimpleResponseAsync(transport, request, SIPResponseStatusCodesEnum.TemporarilyUnavailable, route.Reason).ConfigureAwait(false);
                    return;
                }

                SIPEndPoint targetEndPoint = _commands.ResolveRouteEndPoint(route);
                session = _sessions.CreateFaxSession(legACallId, fromNumber, toNumber, request, remoteEndPoint, localEndPoint, route, direction: "Inbound", parentSessionId: "FAX-INBOUND");
                SIPEndPoint legBLocalEndPoint = BuildLocalEndPointForRemote(targetEndPoint);
                session.LegB.SipEndPoint = targetEndPoint;
                session.LegB.LocalSipEndPoint = legBLocalEndPoint;
                ApplyFaxMediaAddresses(session, receivedLocalEndPoint, remoteEndPoint, legBLocalEndPoint, targetEndPoint);

                _commands.LegA.StoreInviteReceived(session, request, remoteEndPoint, localEndPoint);
                _events.LegA.HandleInviteReceived(session.SessionId, session.LegA.CallId, session.LegA.InviteReceivedRaw);
                TKBase_CallManager.StartRuntimeCall(session, legACallId, fromNumber, toNumber, route);

                SIPResponse trying = _commands.LegA.BuildResponseFromInvite(session, SIPResponseStatusCodesEnum.Trying, null);
                await SendLegAResponseAsync(transport, session, trying).ConfigureAwait(false);

                TKFax_Session faxSession = CreateFaxView(session);
                PrepareInboundFaxBridge(faxSession);
                string legALocalAddress = ResolveStoredOrCurrentLocalMediaAddress(session, session.LegA.LocalSipEndPoint ?? receivedLocalEndPoint, session.LegA.SipEndPoint ?? remoteEndPoint, legB: false);
                string legBLocalAddress = ResolveStoredOrCurrentLocalMediaAddress(session, legBLocalEndPoint, targetEndPoint, legB: true);
                string legBAudioOffer = _media.BuildInitialAudioOfferToLegB(faxSession, legALocalAddress, legBLocalAddress, request.Body);
                ApplyFaxViewToServerSession(session, faxSession);

                SIPRequest outboundInvite = _commands.LegB.BuildInitialAudioInvite(session, request, route, targetEndPoint, legBLocalEndPoint, legBAudioOffer);
                _commands.LegB.StoreInviteSent(session, outboundInvite, targetEndPoint, legBLocalEndPoint);
                _events.LegB.HandleInviteOutbound(session.SessionId, session.LegB.CallId, session.LegB.InviteSentRaw);

                string outboundRaw = outboundInvite.ToString();
                _logger?.Debug($"FAX LegB INVITE OUT | SessionID={session.SessionId} | LegA={session.LegA.CallId} | LegB={session.LegB.CallId} | Ziel={targetEndPoint} | RawLength={outboundRaw.Length}{Environment.NewLine}--- SIP RAW START FAX LegB INVITE OUT ---{Environment.NewLine}{outboundRaw}{Environment.NewLine}--- SIP RAW END FAX LegB INVITE OUT ---");
                _logger?.Info($"FAX B2BUA INVITE weitergeleitet | SessionID={session.SessionId} | LegA={session.LegA.CallId} | LegB={session.LegB.CallId} | Ziel={targetEndPoint} | InitialMedia=Audio/G.711");
                await transport.SendRequestAsync(targetEndPoint, outboundInvite).ConfigureAwait(false);
            } catch (Exception ex) {
                _logger?.Warn($"FAX INVITE Verarbeitung fehlgeschlagen | LegA={legACallId} | Von={fromNumber} | An={toNumber} | Fehler={ex.Message}");
                if (session != null) {
                    try { await SendInviteFailureToLegAAsync(transport, session, SIPResponseStatusCodesEnum.TemporarilyUnavailable, "FAX INVITE Fehler").ConfigureAwait(false); } catch { }
                    StopSession(session, "InviteFailed");
                }
                else {
                    await SendSimpleResponseAsync(transport, request, SIPResponseStatusCodesEnum.TemporarilyUnavailable, "FAX INVITE Fehler").ConfigureAwait(false);
                }
            }
        }

        public string PrepareInboundFaxAnswer(SIP_ServerSession session, SIPRequest invite, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint) {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (invite == null) throw new ArgumentNullException(nameof(invite));
            TKFax_Session faxSession = CreateFaxView(session);
            PrepareInboundFaxBridge(faxSession);
            string localAddress = ResolveLocalMediaAddress(session, localEndPoint, remoteEndPoint, legB: false);
            string answerSdp = _media.BuildAudioAnswerToLegA(faxSession, localAddress, invite.Body);
            ApplyFaxViewToServerSession(session, faxSession);
            return answerSdp;
        }

        public void PrepareOutboundFaxAfterAnswer(SIP_ServerSession session, SIPResponse response, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint) {
            if (session == null || response == null || !session.IsFaxSession) return;
            TKFax_Session faxSession = CreateFaxView(session);
            PrepareOutboundFaxBridge(faxSession);
            string localAddress = ResolveLocalMediaAddress(session, localEndPoint, remoteEndPoint, legB: true);
            if (TKFax_Routing.DetermineFaxMode(response.Body) == TKFaxMediaMode.T38) {
                _media.BuildT38AnswerToLegA(faxSession, localAddress, response.Body);
            }
            else {
                _media.BuildAudioAnswerToLegA(faxSession, localAddress, response.Body);
            }
            ApplyFaxViewToServerSession(session, faxSession);
            _logger?.Info($"FAX Outbound Transport als Bridge vorbereitet | SessionID={session.SessionId}");
        }

        public async Task HandleReInviteAsync(SIPTransport transport, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIPRequest request) {
            if (transport == null || request == null) return;
            string callId = request.Header?.CallId ?? string.Empty;
            if (_sessions == null || !_sessions.TryGet(callId, out SIP_ServerSession? session) || session == null || !session.IsFaxSession) {
                await transport.SendResponseAsync(SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.CallLegTransactionDoesNotExist, null)).ConfigureAwait(false);
                return;
            }

            try {
                if (IsFaxSessionClosed(session)) {
                    _logger?.Debug($"FAX reINVITE verworfen | SessionID={session.SessionId} | CallID={callId} | Grund=Session beendet");
                    await transport.SendResponseAsync(SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.CallLegTransactionDoesNotExist, "FAX Session beendet")).ConfigureAwait(false);
                    return;
                }

                if (session.LegA.PendingReinvite != null || session.LegB.PendingReinvite != null) {
                    await transport.SendResponseAsync(SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.RequestPending, "FAX reINVITE pending")).ConfigureAwait(false);
                    return;
                }

                bool fromLegA = string.Equals(callId, session.LegA.CallId, StringComparison.OrdinalIgnoreCase);
                TKFax_Session faxSession = CreateFaxView(session);
                HandleSdp(faxSession, request.Body);

                SIPResponse trying = SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.Trying, null);
                await transport.SendResponseAsync(trying).ConfigureAwait(false);

                if (fromLegA) {
                    if (session.LegB.SipEndPoint == null || session.LegB.LocalSipEndPoint == null) {
                        await transport.SendResponseAsync(SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.TemporarilyUnavailable, "FAX LegB fehlt")).ConfigureAwait(false);
                        return;
                    }

                    SIPEndPoint legBRemoteEndPoint = session.LegB.SipEndPoint;
                    SIPEndPoint legBLocalEndPoint = session.LegB.LocalSipEndPoint;
                    string legAReinviteLocalAddress = ResolveStoredOrCurrentLocalMediaAddress(session, session.LegA.LocalSipEndPoint ?? localEndPoint, session.LegA.SipEndPoint ?? remoteEndPoint, legB: false);
                    string legBReinviteLocalAddress = ResolveStoredOrCurrentLocalMediaAddress(session, legBLocalEndPoint, legBRemoteEndPoint, legB: true);
                    string offerSdp = faxSession.FaxMode == TKFaxMediaMode.T38
                        ? _media.BuildT38OfferToPeer(faxSession, legBReinviteLocalAddress, toLegB: true, request.Body)
                        : _media.BuildInitialAudioOfferToLegB(faxSession, legAReinviteLocalAddress, legBReinviteLocalAddress, request.Body);
                    ApplyFaxViewToServerSession(session, faxSession);

                    SIPRequest reInviteToB = _commands.LegB.BuildReInviteFromLegA(session, request, legBLocalEndPoint, offerSdp);
                    _commands.LegB.StoreRequestSent(session, reInviteToB, legBRemoteEndPoint, legBLocalEndPoint);
                    _logger?.Info($"FAX reINVITE vermittelt | SessionID={session.SessionId} | Richtung=LegA->LegB | Mode={faxSession.FaxMode}");
                    await transport.SendRequestAsync(legBRemoteEndPoint, reInviteToB).ConfigureAwait(false);
                    return;
                }

                if (session.LegA.SipEndPoint == null || session.LegA.LocalSipEndPoint == null) {
                    await transport.SendResponseAsync(SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.TemporarilyUnavailable, "FAX LegA fehlt")).ConfigureAwait(false);
                    return;
                }

                SIPEndPoint legARemoteEndPoint = session.LegA.SipEndPoint;
                SIPEndPoint legALocalEndPoint = session.LegA.LocalSipEndPoint;
                string legAReverseReinviteLocalAddress = ResolveStoredOrCurrentLocalMediaAddress(session, legALocalEndPoint, legARemoteEndPoint, legB: false);
                string legBReverseReinviteLocalAddress = ResolveStoredOrCurrentLocalMediaAddress(session, session.LegB.LocalSipEndPoint ?? localEndPoint, session.LegB.SipEndPoint ?? remoteEndPoint, legB: true);
                string offerToA = faxSession.FaxMode == TKFaxMediaMode.T38
                    ? _media.BuildT38OfferToPeer(faxSession, legAReverseReinviteLocalAddress, toLegB: false, request.Body)
                    : _media.BuildAudioAnswerToLegA(faxSession, legAReverseReinviteLocalAddress, legBReverseReinviteLocalAddress, request.Body);
                ApplyFaxViewToServerSession(session, faxSession);

                SIPRequest reInviteToA = _commands.LegA.BuildDialogRequest(session, SIPMethodsEnum.INVITE, legALocalEndPoint);
                reInviteToA.Body = offerToA;
                reInviteToA.Header.ContentType = "application/sdp";
                reInviteToA.Header.ContentLength = reInviteToA.Body?.Length ?? 0;
                session.LegB.PendingReinvite = request.Copy();
                session.LegA.PendingReinvite = reInviteToA.Copy();
                _commands.LegA.StoreRequestSent(session, reInviteToA, legARemoteEndPoint, legALocalEndPoint);
                _logger?.Info($"FAX reINVITE vermittelt | SessionID={session.SessionId} | Richtung=LegB->LegA | Mode={faxSession.FaxMode}");
                await transport.SendRequestAsync(legARemoteEndPoint, reInviteToA).ConfigureAwait(false);
            } catch (Exception ex) {
                _logger?.Warn($"FAX reINVITE fehlgeschlagen | CallID={callId} | Fehler={ex.Message}");
                await transport.SendResponseAsync(SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.TemporarilyUnavailable, "FAX reINVITE Fehler")).ConfigureAwait(false);
            }
        }

        public async Task HandleAckAsync(SIPTransport transport, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIPRequest request) {
            if (request == null || _sessions == null) return;
            string callId = request.Header?.CallId ?? string.Empty;
            if (!_sessions.TryGet(callId, out SIP_ServerSession? session) || session == null || !session.IsFaxSession) return;
            if (IsFaxSessionClosed(session)) return;

            bool fromLegA = string.Equals(callId, session.LegA.CallId, StringComparison.OrdinalIgnoreCase);
            if (fromLegA) {
                _commands.LegA.StoreAckReceived(session, request, remoteEndPoint, localEndPoint);
                _logger?.Debug($"FAX LegA ACK empfangen | SessionID={session.SessionId} | Mode={session.FaxMode} | Aktion=nur Dialogstatus, kein lokaler Fax-Endpunkt");
                return;
            }

            _commands.LegB.StoreAckReceived(session, request, remoteEndPoint, localEndPoint);
        }

        public async Task HandleByeAsync(SIPTransport transport, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIPRequest request) {
            if (transport == null || request == null) return;
            string callId = request.Header?.CallId ?? string.Empty;
            if (_sessions != null && _sessions.TryGet(callId, out SIP_ServerSession? session) && session != null) {
                if (IsFaxSessionClosed(session)) {
                    _logger?.Debug($"FAX BYE fuer bereits beendete Session bestaetigt | SessionID={session.SessionId} | CallID={callId}");
                    await transport.SendResponseAsync(SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.Ok, null)).ConfigureAwait(false);
                    return;
                }

                bool fromLegA = string.Equals(callId, session.LegA.CallId, StringComparison.OrdinalIgnoreCase);
                if (fromLegA) {
                    _commands.LegA.StoreByeReceived(session, request, remoteEndPoint, localEndPoint);
                    if (session.LegB.SipEndPoint != null && session.LegB.LocalSipEndPoint != null && session.LegB.InviteSent != null && !session.LegB.IsEnded) {
                        SIPRequest peerBye = _commands.LegB.BuildDialogRequest(session, SIPMethodsEnum.BYE, session.LegB.LocalSipEndPoint);
                        _commands.LegB.StoreRequestSent(session, peerBye, session.LegB.SipEndPoint, session.LegB.LocalSipEndPoint);
                        await transport.SendRequestAsync(session.LegB.SipEndPoint, peerBye).ConfigureAwait(false);
                    }
                }
                else {
                    if (session.LegA.SipEndPoint != null && session.LegA.LocalSipEndPoint != null && session.LegA.InviteReceived != null && !session.LegA.IsEnded) {
                        SIPRequest peerBye = _commands.LegA.BuildDialogRequest(session, SIPMethodsEnum.BYE, session.LegA.LocalSipEndPoint);
                        _commands.LegA.StoreRequestSent(session, peerBye, session.LegA.SipEndPoint, session.LegA.LocalSipEndPoint);
                        await transport.SendRequestAsync(session.LegA.SipEndPoint, peerBye).ConfigureAwait(false);
                    }
                }

                StopSession(session, "BYE");
            }

            await transport.SendResponseAsync(SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.Ok, null)).ConfigureAwait(false);
        }

        public async Task HandleCancelAsync(SIPTransport transport, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIPRequest request) {
            if (transport == null || request == null) return;
            string callId = request.Header?.CallId ?? string.Empty;
            if (_sessions != null && _sessions.TryGet(callId, out SIP_ServerSession? session) && session != null) {
                _commands.LegA.StoreCancelReceived(session, request, remoteEndPoint, localEndPoint);
                if (session.LegB.SipEndPoint != null && session.LegB.LocalSipEndPoint != null && session.LegB.InviteSent != null && session.LegB.LastSuccessResponse == null) {
                    SIPRequest cancel = _commands.LegB.BuildDialogRequest(session, SIPMethodsEnum.CANCEL, session.LegB.LocalSipEndPoint);
                    _commands.LegB.StoreRequestSent(session, cancel, session.LegB.SipEndPoint, session.LegB.LocalSipEndPoint);
                    await transport.SendRequestAsync(session.LegB.SipEndPoint, cancel).ConfigureAwait(false);
                }
                StopSession(session, "CANCEL");
            }

            await transport.SendResponseAsync(SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.Ok, null)).ConfigureAwait(false);
        }

        public async Task HandleResponseAsync(SIPTransport transport, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIPResponse response) {
            if (transport == null || response == null || _sessions == null) return;
            string callId = response.Header?.CallId ?? string.Empty;
            if (!_sessions.TryGet(callId, out SIP_ServerSession? session) || session == null || !session.IsFaxSession) return;
            SIPMethodsEnum responseMethod = response.Header?.CSeqMethod ?? SIPMethodsEnum.NONE;
            if (IsFaxSessionClosed(session) && responseMethod == SIPMethodsEnum.INVITE) {
                _logger?.Debug($"FAX INVITE-Response nach Sessionende verworfen | SessionID={session.SessionId} | CallID={callId} | Status={response.StatusCode} {response.ReasonPhrase}");
                return;
            }

            try {
                bool responseFromLegB = string.Equals(callId, session.LegB.CallId, StringComparison.OrdinalIgnoreCase);
                if (!responseFromLegB) {
                    if (session.LegB.PendingReinvite != null) {
                        await HandleLegAResponseForPendingLegBReinviteAsync(transport, localEndPoint, remoteEndPoint, session, response).ConfigureAwait(false);
                    }
                    else {
                        _logger?.Debug($"FAX RESPONSE fuer LegA gesehen | SessionID={session.SessionId} | Status={response.StatusCode} {response.ReasonPhrase}");
                    }
                    return;
                }

                _commands.LegB.StoreResponseReceived(session, response, remoteEndPoint, localEndPoint);
                SIPMethodsEnum cseqMethod = responseMethod;
                if (cseqMethod == SIPMethodsEnum.BYE || cseqMethod == SIPMethodsEnum.CANCEL) return;
                if (cseqMethod != SIPMethodsEnum.INVITE) return;

                if (session.LegA.PendingReinvite != null) {
                    await HandleLegBResponseForPendingLegAReinviteAsync(transport, localEndPoint, remoteEndPoint, session, response).ConfigureAwait(false);
                    return;
                }

                if (response.StatusCode >= 100 && response.StatusCode < 200) {
                    SIPResponse provisional = _commands.LegA.BuildResponseFromInvite(session, response.Status, response.ReasonPhrase);
                    await SendLegAResponseAsync(transport, session, provisional).ConfigureAwait(false);
                    session.FaxStatus = "Ringing";
                    TKBase_CallManager.MarkRuntimeRinging(session.SessionId);
                    return;
                }

                if (response.StatusCode >= 200 && response.StatusCode < 300) {
                    TKFax_Session faxSession = CreateFaxView(session);
                    PrepareInboundFaxBridge(faxSession);
                    string legALocalAddress = ResolveStoredOrCurrentLocalMediaAddress(session, session.LegA.LocalSipEndPoint ?? localEndPoint, session.LegA.SipEndPoint ?? remoteEndPoint, legB: false);
                    string legBLocalAddress = ResolveStoredOrCurrentLocalMediaAddress(session, session.LegB.LocalSipEndPoint ?? localEndPoint, session.LegB.SipEndPoint ?? remoteEndPoint, legB: true);
                    string answerSdp = _media.BuildAudioAnswerToLegA(faxSession, legALocalAddress, legBLocalAddress, response.Body);
                    ApplyFaxViewToServerSession(session, faxSession);

                    SIPRequest ack = _commands.LegB.BuildAckForConfirmedDialog(session, response, session.LegB.LocalSipEndPoint ?? localEndPoint);
                    if (session.LegB.SipEndPoint != null) {
                        _commands.LegB.StoreAckSent(session, ack, session.LegB.SipEndPoint, session.LegB.LocalSipEndPoint ?? localEndPoint);
                        await transport.SendRequestAsync(session.LegB.SipEndPoint, ack).ConfigureAwait(false);
                    }

                    SIPResponse ok = _commands.LegA.BuildResponseFromInvite(session, response.Status, response.ReasonPhrase, answerSdp, "application/sdp");
                    await SendLegAResponseAsync(transport, session, ok).ConfigureAwait(false);
                    _sessions.MarkAnswered(session.SessionId);
                    session.FaxStatus = "Answered";
                    TKBase_CallManager.MarkRuntimeAnswered(session.SessionId);
                    _logger?.Info($"FAX LegB Antwort an LegA vermittelt | SessionID={session.SessionId} | Mode=Audio/G.711 Bridge");
                    return;
                }

                if (response.StatusCode >= 300) {
                    try {
                        SIPRequest failedAck = _commands.LegB.BuildAckForFailedInviteResponse(session, response, session.LegB.LocalSipEndPoint ?? localEndPoint);
                        if (session.LegB.SipEndPoint != null) await transport.SendRequestAsync(session.LegB.SipEndPoint, failedAck).ConfigureAwait(false);
                        SIPResponse fail = _commands.LegA.BuildResponseFromInvite(session, response.Status, response.ReasonPhrase);
                        await SendLegAResponseAsync(transport, session, fail).ConfigureAwait(false);
                    } finally {
                        StopSession(session, "LegBFailed");
                    }
                }
            } catch (Exception ex) {
                _logger?.Error($"FAX RESPONSE Verarbeitung fehlgeschlagen | SessionID={session?.SessionId ?? "-"} | CallID={response.Header?.CallId ?? "-"} | Status={response.StatusCode} {response.ReasonPhrase} | Fehler={ex.Message}");
                if (session != null && response.StatusCode >= 200) StopSession(session, "ResponseError");
            }
        }

        public async Task<bool> HangupSessionAsync(SIPTransport transport, string sessionId) {
            if (transport == null || _sessions == null || string.IsNullOrWhiteSpace(sessionId)) return false;
            if (!_sessions.TryGet(sessionId, out SIP_ServerSession? session) || session == null || !session.IsFaxSession) return false;

            bool signalingSucceeded = true;

            try {
                if (session.LegA.SipEndPoint != null && session.LegA.LocalSipEndPoint != null && !session.LegA.IsEnded && session.LegA.InviteReceived != null) {
                    try {
                        if (session.LegA.LastSuccessResponse != null) {
                            SIPRequest byeA = _commands.LegA.BuildDialogRequest(session, SIPMethodsEnum.BYE, session.LegA.LocalSipEndPoint);
                            _commands.LegA.StoreRequestSent(session, byeA, session.LegA.SipEndPoint, session.LegA.LocalSipEndPoint);
                            string rawByeA = byeA.ToString();
                            _logger?.Debug($"FAX Control BYE OUT LegA | SessionID={session.SessionId} | CallID={byeA.Header?.CallId} | Ziel={session.LegA.SipEndPoint} | RawLength={rawByeA.Length}{Environment.NewLine}--- SIP RAW START FAX CONTROL BYE OUT LEGA ---{Environment.NewLine}{rawByeA}{Environment.NewLine}--- SIP RAW END FAX CONTROL BYE OUT LEGA ---");
                            await transport.SendRequestAsync(session.LegA.SipEndPoint, byeA).ConfigureAwait(false);
                        }
                        else {
                            SIPResponse terminatedA = _commands.LegA.BuildResponseFromInvite(session, SIPResponseStatusCodesEnum.RequestTerminated, "Call ended by control");
                            await SendLegAResponseAsync(transport, session, terminatedA).ConfigureAwait(false);
                        }
                    } catch (Exception ex) {
                        signalingSucceeded = false;
                        _logger?.Error($"FAX Control-Hangup LegA fehlgeschlagen | SessionID={session.SessionId} | Fehler={ex.Message}");
                    }
                }

                if (session.LegB.SipEndPoint != null && session.LegB.LocalSipEndPoint != null && !session.LegB.IsEnded && session.LegB.InviteSent != null) {
                    try {
                        SIPMethodsEnum method = session.LegB.LastSuccessResponse != null ? SIPMethodsEnum.BYE : SIPMethodsEnum.CANCEL;
                        SIPRequest requestB = _commands.LegB.BuildDialogRequest(session, method, session.LegB.LocalSipEndPoint);
                        _commands.LegB.StoreRequestSent(session, requestB, session.LegB.SipEndPoint, session.LegB.LocalSipEndPoint);
                        string rawRequestB = requestB.ToString();
                        _logger?.Debug($"FAX Control {method} OUT LegB | SessionID={session.SessionId} | CallID={requestB.Header?.CallId} | Ziel={session.LegB.SipEndPoint} | RawLength={rawRequestB.Length}{Environment.NewLine}--- SIP RAW START FAX CONTROL {method} OUT LEGB ---{Environment.NewLine}{rawRequestB}{Environment.NewLine}--- SIP RAW END FAX CONTROL {method} OUT LEGB ---");
                        await transport.SendRequestAsync(session.LegB.SipEndPoint, requestB).ConfigureAwait(false);
                    } catch (Exception ex) {
                        signalingSucceeded = false;
                        _logger?.Error($"FAX Control-Hangup LegB fehlgeschlagen | SessionID={session.SessionId} | Fehler={ex.Message}");
                    }
                }
            } finally {
                try {
                    StopSession(session, "ControlHangup");
                } catch (Exception ex) {
                    signalingSucceeded = false;
                    _logger?.Error($"FAX Control-Hangup Cleanup fehlgeschlagen | SessionID={session.SessionId} | Fehler={ex.Message}");
                }
            }

            return signalingSucceeded;
        }

        public void StopOwnedSessions() {
            if (_sessions == null) return;
            foreach (SIP_ServerSession session in _sessions.GetSnapshot()) {
                if (session.IsFaxSession) StopSession(session, "server-stop");
            }
        }

        public void PrepareOutboundFax(TKFax_Session faxSession) => PrepareOutboundFaxBridge(faxSession);
        public void PrepareInboundFax(TKFax_Session faxSession) => PrepareInboundFaxBridge(faxSession);

        public void PrepareOutboundFaxBridge(TKFax_Session faxSession) {
            if (faxSession == null) return;
            faxSession.Direction = "Outbound";
            faxSession.Status = "BridgePending";
            faxSession.StartedAt ??= DateTime.Now;
            _commands.LegA.PrepareSend(faxSession);
            _events.LegA.HandleFaxSendPrepared(faxSession);
        }

        public void PrepareInboundFaxBridge(TKFax_Session faxSession) {
            if (faxSession == null) return;
            faxSession.Direction = "Inbound";
            faxSession.Status = "BridgePending";
            faxSession.StartedAt ??= DateTime.Now;
            _commands.LegB.PrepareReceive(faxSession);
            _events.LegB.HandleFaxReceivePrepared(faxSession);
        }

        public void HandleSdp(TKFax_Session faxSession, string? sdp) {
            if (faxSession == null) return;
            TKFaxMediaMode mode = TKFax_Routing.DetermineFaxMode(sdp);
            if (mode == TKFaxMediaMode.T38) {
                faxSession.FaxMode = TKFaxMediaMode.T38;
                faxSession.LegA.T38Active = true;
                faxSession.LegB.T38Active = true;
                _media.SelectT38Mode(faxSession);
                _logger?.Debug($"FAX T.38 SDP erkannt | SessionID={faxSession.SessionId}");
            }
            else if (mode == TKFaxMediaMode.AudioPassthrough || faxSession.FaxMode == TKFaxMediaMode.Unknown) {
                faxSession.FaxMode = TKFaxMediaMode.AudioPassthrough;
                _media.SelectAudioFaxMode(faxSession);
            }
        }

        public void StopFax(TKFax_Session faxSession, string? reason = null) {
            if (faxSession == null) return;
            _commands.LegA.StopSend(faxSession, reason);
            _commands.LegB.StopReceive(faxSession, reason);
            _media.Stop(faxSession, reason);
            faxSession.Status = "Ended";
            faxSession.EndedAt = DateTime.Now;
        }

        public static void ApplySdp(SIPResponse response, string sdp) {
            response.Body = sdp ?? string.Empty;
            response.Header.ContentType = "application/sdp";
            response.Header.ContentLength = response.Body.Length;
        }

        private async Task HandleLegAResponseForPendingLegBReinviteAsync(SIPTransport transport, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIP_ServerSession session, SIPResponse response) {
            if (response.StatusCode >= 100 && response.StatusCode < 200) return;

            if (response.StatusCode >= 200 && response.StatusCode < 300) {
                TKFax_Session faxSession = CreateFaxView(session);
                HandleSdp(faxSession, response.Body);
                string legALocalAddress = ResolveStoredOrCurrentLocalMediaAddress(session, session.LegA.LocalSipEndPoint ?? localEndPoint, session.LegA.SipEndPoint ?? remoteEndPoint, legB: false);
                string legBLocalAddress = ResolveStoredOrCurrentLocalMediaAddress(session, session.LegB.LocalSipEndPoint ?? localEndPoint, session.LegB.SipEndPoint ?? remoteEndPoint, legB: true);
                string answerSdp = faxSession.FaxMode == TKFaxMediaMode.T38
                    ? _media.BuildT38AnswerToLegB(faxSession, legBLocalAddress, response.Body)
                    : _media.BuildAudioAnswerToLegB(faxSession, legALocalAddress, legBLocalAddress, response.Body);
                ApplyFaxViewToServerSession(session, faxSession);

                SIPRequest ack = _commands.LegA.BuildAckForConfirmedDialog(session, response, session.LegA.LocalSipEndPoint ?? localEndPoint);
                if (session.LegA.SipEndPoint != null) {
                    _commands.LegA.StoreAckSent(session, ack, session.LegA.SipEndPoint, session.LegA.LocalSipEndPoint ?? localEndPoint);
                    await transport.SendRequestAsync(session.LegA.SipEndPoint, ack).ConfigureAwait(false);
                }

                SIPResponse okToB = _commands.LegB.BuildResponseForPendingReinvite(session, response.Status, response.ReasonPhrase, null, answerSdp, "application/sdp");
                await SendLegBResponseAsync(transport, session, okToB).ConfigureAwait(false);
                session.LegA.PendingReinvite = null;
                session.LegB.PendingReinvite = null;
                _logger?.Info($"FAX reINVITE Antwort vermittelt | SessionID={session.SessionId} | Richtung=LegB->LegA | Mode={faxSession.FaxMode}");
                return;
            }

            SIPRequest failedAck = _commands.LegA.BuildAckForFailedInviteResponse(session, response, session.LegA.LocalSipEndPoint ?? localEndPoint);
            if (session.LegA.SipEndPoint != null) await transport.SendRequestAsync(session.LegA.SipEndPoint, failedAck).ConfigureAwait(false);
            SIPResponse failToB = _commands.LegB.BuildResponseForPendingReinvite(session, response.Status, response.ReasonPhrase);
            await SendLegBResponseAsync(transport, session, failToB).ConfigureAwait(false);
            session.LegA.PendingReinvite = null;
            session.LegB.PendingReinvite = null;
        }

        private async Task HandleLegBResponseForPendingLegAReinviteAsync(SIPTransport transport, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIP_ServerSession session, SIPResponse response) {
            if (response.StatusCode >= 100 && response.StatusCode < 200) return;

            if (response.StatusCode >= 200 && response.StatusCode < 300) {
                TKFax_Session faxSession = CreateFaxView(session);
                HandleSdp(faxSession, response.Body);
                string legALocalAddress = ResolveStoredOrCurrentLocalMediaAddress(session, session.LegA.LocalSipEndPoint ?? localEndPoint, session.LegA.SipEndPoint ?? remoteEndPoint, legB: false);
                string legBLocalAddress = ResolveStoredOrCurrentLocalMediaAddress(session, session.LegB.LocalSipEndPoint ?? localEndPoint, session.LegB.SipEndPoint ?? remoteEndPoint, legB: true);
                string answerSdp = faxSession.FaxMode == TKFaxMediaMode.T38
                    ? _media.BuildT38AnswerToLegA(faxSession, legALocalAddress, response.Body)
                    : _media.BuildAudioAnswerToLegA(faxSession, legALocalAddress, legBLocalAddress, response.Body);
                ApplyFaxViewToServerSession(session, faxSession);

                SIPRequest ack = _commands.LegB.BuildAckForConfirmedDialog(session, response, session.LegB.LocalSipEndPoint ?? localEndPoint);
                if (session.LegB.SipEndPoint != null) {
                    _commands.LegB.StoreAckSent(session, ack, session.LegB.SipEndPoint, session.LegB.LocalSipEndPoint ?? localEndPoint);
                    await transport.SendRequestAsync(session.LegB.SipEndPoint, ack).ConfigureAwait(false);
                }

                SIPResponse okToA = _commands.LegA.BuildResponseForPendingReinvite(session, response.Status, response.ReasonPhrase, null, answerSdp, "application/sdp");
                await SendLegAResponseAsync(transport, session, okToA).ConfigureAwait(false);
                session.LegA.PendingReinvite = null;
                session.LegB.PendingReinvite = null;
                _logger?.Info($"FAX reINVITE Antwort vermittelt | SessionID={session.SessionId} | Richtung=LegA->LegB | Mode={faxSession.FaxMode}");
                return;
            }

            SIPRequest failedAck = _commands.LegB.BuildAckForFailedInviteResponse(session, response, session.LegB.LocalSipEndPoint ?? localEndPoint);
            if (session.LegB.SipEndPoint != null) await transport.SendRequestAsync(session.LegB.SipEndPoint, failedAck).ConfigureAwait(false);
            SIPResponse failToA = _commands.LegA.BuildResponseForPendingReinvite(session, response.Status, response.ReasonPhrase);
            await SendLegAResponseAsync(transport, session, failToA).ConfigureAwait(false);
            session.LegA.PendingReinvite = null;
            session.LegB.PendingReinvite = null;
        }

        private async Task SendLegBResponseAsync(SIPTransport transport, SIP_ServerSession session, SIPResponse response) {
            _commands.LegB.StoreResponseSent(session, response);
            string raw = response.ToString();
            _logger?.Debug($"FAX LegB RESPONSE OUT | SessionID={session.SessionId} | Status={response.StatusCode} {response.ReasonPhrase} | CallID={response.Header?.CallId} | RawLength={raw.Length}{Environment.NewLine}--- SIP RAW START FAX LegB RESPONSE OUT ---{Environment.NewLine}{raw}{Environment.NewLine}--- SIP RAW END FAX LegB RESPONSE OUT ---");
            await transport.SendResponseAsync(response).ConfigureAwait(false);
        }

        private async Task SendLegAResponseAsync(SIPTransport transport, SIP_ServerSession session, SIPResponse response) {
            _commands.LegA.StoreResponseSent(session, response);
            string raw = response.ToString();
            _logger?.Debug($"FAX LegA RESPONSE OUT | SessionID={session.SessionId} | Status={response.StatusCode} {response.ReasonPhrase} | CallID={response.Header?.CallId} | RawLength={raw.Length}{Environment.NewLine}--- SIP RAW START FAX LegA RESPONSE OUT ---{Environment.NewLine}{raw}{Environment.NewLine}--- SIP RAW END FAX LegA RESPONSE OUT ---");
            await transport.SendResponseAsync(response).ConfigureAwait(false);
        }

        private async Task SendInviteFailureToLegAAsync(SIPTransport transport, SIP_ServerSession session, SIPResponseStatusCodesEnum status, string? reason) {
            SIPResponse response = _commands.LegA.BuildResponseFromInvite(session, status, reason);
            await SendLegAResponseAsync(transport, session, response).ConfigureAwait(false);
        }

        private void StopSession(SIP_ServerSession session, string reason) {
            try {
                TKFax_Session faxSession = CreateFaxView(session);
                StopFax(faxSession, reason);
                ApplyFaxViewToServerSession(session, faxSession);
            } finally {
                try {
                    _sessions?.MarkAllLegsEnded(session.SessionId, reason);
                } finally {
                    TKBase_CallManager.MarkRuntimeEnded(session.SessionId, reason);
                }
            }
        }

        private string ResolveStoredOrCurrentLocalMediaAddress(SIP_ServerSession session, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, bool legB) {
            string? stored = legB ? session.LegB.MediaAdvertisedIp : session.LegA.MediaAdvertisedIp;
            if (!string.IsNullOrWhiteSpace(stored)) return stored.Trim();
            return ResolveLocalMediaAddress(session, localEndPoint, remoteEndPoint, legB);
        }

        private static bool IsFaxSessionClosed(SIP_ServerSession session) {
            if (session == null) return true;
            if (session.IsTerminated || session.IsTerminating) return true;
            if (session.LegA.IsEnded && session.LegB.IsEnded) return true;
            return string.Equals(session.Status, "Ended", StringComparison.OrdinalIgnoreCase)
                || string.Equals(session.FaxStatus, "Ended", StringComparison.OrdinalIgnoreCase);
        }

        private string ResolveLocalMediaAddress(SIP_ServerSession session, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, bool legB) {
            if (session == null) throw new ArgumentNullException(nameof(session));
            IPAddress? localAddress = NormalizeEndpointAddress(localEndPoint);
            IPAddress? remoteAddress = NormalizeEndpointAddress(remoteEndPoint);
            string reason = string.Empty;
            string resolvedIp = string.Empty;

            bool ok = false;
            if (_settings.HasValue) {
                TKBase_ServerSettings settings = _settings.Value;
                ok = legB
                    ? settings.TryResolveEndpointMediaAdvertisedIp(localAddress, remoteAddress, null, false, out resolvedIp!, out reason)
                    : settings.TryResolveIngressMediaAdvertisedIp(localAddress, remoteAddress, null, out resolvedIp!, out reason);
            }

            if (ok && !string.IsNullOrWhiteSpace(resolvedIp)) {
                if (legB) session.LegB.MediaAdvertisedIp = resolvedIp;
                else session.LegA.MediaAdvertisedIp = resolvedIp;
                session.MediaAdvertisedIp = session.LegA.MediaAdvertisedIp ?? resolvedIp;
                session.FaxLocalT38Address = resolvedIp;
                session.FaxLocalAudioAddress = resolvedIp;
                return resolvedIp;
            }

            if (!string.IsNullOrWhiteSpace(reason)) {
                _logger?.Warn($"FAX MediaAdvertisedIp automatische Auswahl fehlgeschlagen | SessionID={session.SessionId} | Leg={(legB ? "B" : "A")} | LocalSIP={FormatEndPoint(localEndPoint)} | RemoteSIP={FormatEndPoint(remoteEndPoint)} | Grund={reason}");
            }

            string fallbackIp = localAddress?.ToString() ?? "127.0.0.1";
            if (legB) session.LegB.MediaAdvertisedIp = fallbackIp;
            else session.LegA.MediaAdvertisedIp = fallbackIp;
            session.MediaAdvertisedIp = session.LegA.MediaAdvertisedIp ?? fallbackIp;
            session.FaxLocalT38Address = fallbackIp;
            session.FaxLocalAudioAddress = fallbackIp;
            return fallbackIp;
        }

        private void ApplyFaxMediaAddresses(SIP_ServerSession session, SIPEndPoint legALocalEndPoint, SIPEndPoint legARemoteEndPoint, SIPEndPoint legBLocalEndPoint, SIPEndPoint legBRemoteEndPoint) {
            ResolveLocalMediaAddress(session, legALocalEndPoint, legARemoteEndPoint, legB: false);
            ResolveLocalMediaAddress(session, legBLocalEndPoint, legBRemoteEndPoint, legB: true);
            session.UpdatedAt = DateTime.Now;
        }

        private SIPEndPoint BuildLocalEndPointForRemote(SIPEndPoint remoteEndPoint) {
            IPAddress localAddress = _settings.HasValue ? _settings.Value.ResolveServerAddressForRemote(remoteEndPoint.Address) : IPAddress.Any;
            int port = _settings.HasValue ? _settings.Value.SipPort : remoteEndPoint.Port;
            return new SIPEndPoint(remoteEndPoint.Protocol, localAddress, port);
        }

        private static IPAddress? NormalizeEndpointAddress(SIPEndPoint? endpoint) {
            IPAddress? address = endpoint?.Address;
            if (address == null) return null;
            return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        }

        private static string FormatEndPoint(SIPEndPoint? endpoint) {
            if (endpoint == null || endpoint.Address == null) return "-";
            return $"{endpoint.Protocol}:{endpoint.Address}:{endpoint.Port}";
        }

        private SIPIntResolvedTarget ResolveFaxInviteRoute(string legACallId, string fromNumber, string toNumber) {
            if (_baseRouting == null) return SIPIntResolvedTarget.Fail(toNumber, "RoutingManager nicht verfuegbar.");
            SIP_DialPlanDecision decision = _baseRouting.CreateDecision(fromNumber, toNumber);

            // Externe Fax-DIDs muessen ueber das Fax-Routing aufgeloest werden.
            // Das allgemeine Inbound-Routing liefert sonst ein normales SIPInt-User-Ziel
            // und der TKFax_CallController erkennt den Fax-Pfad nicht als zustaendig.
            if (decision.TargetType == TKBase_DialTargetType.Fax) {
                return _baseRouting.HandleInvite(legACallId, fromNumber, toNumber);
            }

            if (decision.SourceType == TKBase_DialSourceType.External || (decision.SourceType != TKBase_DialSourceType.Internal && decision.TargetType == TKBase_DialTargetType.External)) {
                return _baseRouting.ResolveInboundExternalTarget(toNumber);
            }
            return _baseRouting.HandleInvite(legACallId, fromNumber, toNumber);
        }

        private static string ExtractFromNumber(SIPRequest request) {
            return NormalizeSipUser(request.Header?.From?.FromURI?.User)
                ?? NormalizeSipUser(request.Header?.From?.FromName)
                ?? string.Empty;
        }

        private static string ExtractToNumber(SIPRequest request) {
            return NormalizeSipUser(request.Header?.To?.ToURI?.User)
                ?? NormalizeSipUser(request.URI?.User)
                ?? string.Empty;
        }

        private static string? NormalizeSipUser(string? value) {
            if (string.IsNullOrWhiteSpace(value)) return null;
            string normalized = value.Trim().Trim('<', '>', '\'', '"');
            int parameterIndex = normalized.IndexOf(';');
            if (parameterIndex >= 0) normalized = normalized[..parameterIndex];
            if (normalized.StartsWith("sip:", StringComparison.OrdinalIgnoreCase) || normalized.StartsWith("sips:", StringComparison.OrdinalIgnoreCase)) {
                try { normalized = SIPURI.ParseSIPURIRelaxed(normalized).User ?? string.Empty; }
                catch {
                    int colonIndex = normalized.IndexOf(':');
                    normalized = colonIndex >= 0 ? normalized[(colonIndex + 1)..] : normalized;
                }
            }
            int atIndex = normalized.IndexOf('@');
            if (atIndex >= 0) normalized = normalized[..atIndex];
            return string.IsNullOrWhiteSpace(normalized) ? null : normalized.Trim();
        }

        private static async Task SendSimpleResponseAsync(SIPTransport transport, SIPRequest request, SIPResponseStatusCodesEnum status, string? reason) {
            SIPResponse response = SIPResponse.GetResponse(request, status, reason);
            await transport.SendResponseAsync(response).ConfigureAwait(false);
        }

        private string? ResolveUsernameByNumberOrName(string value) {
            if (string.IsNullOrWhiteSpace(value)) return null;
            DataRow? row = _sipSql.GetSipUserByUsername(value) ?? _sipSql.GetSipUserByNumber(value);
            return row == null || !row.Table.Columns.Contains("Username") ? null : Convert.ToString(row["Username"]);
        }

        private static void ApplyFaxViewToServerSession(SIP_ServerSession session, TKFax_Session faxSession) {
            session.IsFaxSession = true;
            session.FaxDirection = faxSession.Direction;
            session.FaxMode = faxSession.FaxMode.ToString();
            session.FaxStatus = faxSession.Status;
            session.FaxLastError = faxSession.LastError;
            session.FaxJobId = faxSession.FaxJobId;
            session.FaxLocalT38Address = faxSession.LocalT38Address;
            session.FaxLocalT38Port = faxSession.LocalT38Port > 0 ? faxSession.LocalT38Port : null;
            session.FaxRemoteT38Address = faxSession.RemoteT38Address;
            session.FaxRemoteT38Port = faxSession.RemoteT38Port > 0 ? faxSession.RemoteT38Port : null;
            session.FaxLocalAudioAddress = faxSession.LocalAudioAddress;
            session.FaxLocalAudioPort = faxSession.LocalAudioPort > 0 ? faxSession.LocalAudioPort : null;
            session.FaxRemoteAudioAddress = faxSession.RemoteAudioAddress;
            session.FaxRemoteAudioPort = faxSession.RemoteAudioPort > 0 ? faxSession.RemoteAudioPort : null;
            session.FaxAudioPayloadType = faxSession.AudioPayloadType >= 0 ? faxSession.AudioPayloadType : null;
            session.FaxAudioCodec = faxSession.AudioCodec;
            session.UpdatedAt = DateTime.Now;
        }
    }
}
