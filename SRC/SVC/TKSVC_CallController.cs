/*
 * TKSVC_CallController
 * Aufgabe:
 * - Eigenständige Steuerung des Service-Leg-D.
 * - Kein Leg A und kein Leg B im Service-Bereich.
 * - SIP-Seite, SIP-Responses, ACK/BYE/CANCEL bleiben im aufrufenden SIP-Bereich.
 */

namespace TKServer.SVC {
    using SIPSorcery.SIP;
    using System;
    using System.Collections.Concurrent;
    using System.Net;
    using System.Threading.Tasks;

    using TKServer.BASE;
    using TKServer.SQL;

    /// TKSVC_CallController
    /// Aufgabe:
    /// - Eigenständige Steuerung des Service-Leg-D.
    /// - Kein Leg A und kein Leg B im Service-Bereich.
    /// - SIP-Seite, SIP-Responses, ACK/BYE/CANCEL bleiben im aufrufenden SIP-Bereich.

    /// <summary>
    /// Defines the <see cref="TKSVC_CallController" />
    /// </summary>
    internal sealed class TKSVC_CallController {
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
        /// Defines the _mediaManager
        /// </summary>
        private readonly TKBase_MediaManager _mediaManager;

        /// <summary>
        /// Defines the _commands
        /// </summary>
        private readonly TKSVC_Commands _commands;

        /// <summary>
        /// Defines the _events
        /// </summary>
        private readonly TKSVC_Events _events;

        /// <summary>
        /// Defines the _dtmfManager
        /// </summary>
        private readonly TKBase_DTMF_Manager _dtmfManager;

        /// <summary>
        /// Defines the _media
        /// </summary>
        private readonly TKSVC_Media _media;

        /// <summary>
        /// Defines the _serviceRegistry
        /// </summary>
        private readonly TKSVC_ServiceRegistry _serviceRegistry;

        /// <summary>
        /// Defines the _routing
        /// </summary>
        private readonly TKSVC_Routing _routing;

        /// <summary>
        /// Defines the _activeLegD
        /// </summary>
        private readonly ConcurrentDictionary<string, string> _activeLegD = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of the <see cref="TKSVC_CallController"/> class.
        /// </summary>
        /// <param name="sessions">The sessions<see cref="TKBase_SessionManager"/></param>
        /// <param name="settings">The settings<see cref="TKBase_ServerSettings"/></param>
        /// <param name="mediaManager">The mediaManager<see cref="TKBase_MediaManager"/></param>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        /// <param name="dtmfManager">The dtmfManager<see cref="TKBase_DTMF_Manager?"/></param>
        public TKSVC_CallController(
            TKBase_SessionManager sessions,
            TKBase_ServerSettings settings,
            TKBase_MediaManager mediaManager,
            TKBase_LogManager? logger = null,
            TKBase_DTMF_Manager? dtmfManager = null) {
            _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            _logger = logger;
            _sipSql = new TKSQL_Commands(logger);
            _mediaManager = mediaManager ?? throw new ArgumentNullException(nameof(mediaManager));
            _dtmfManager = dtmfManager ?? new TKBase_DTMF_Manager(logger);
            _serviceRegistry = new TKSVC_ServiceRegistry(logger);
            _commands = new TKSVC_Commands(logger);
            _events = new TKSVC_Events(logger);
            _media = new TKSVC_Media(settings, logger, _serviceRegistry, _dtmfManager, HandleDtmfSequenceAsync);
            _routing = new TKSVC_Routing(logger, _serviceRegistry);
        }

        /// <summary>
        /// The StopOwnedSessions
        /// </summary>
        public void StopOwnedSessions() {
            foreach (SIP_ServerSession session in _sessions.GetSnapshot()) {
                if (!session.IsServiceSession) {
                    continue;
                }

                EndLegD(session, "server-stop");
            }
        }

        /// <summary>
        /// The StartLegDAsync
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="sourceInvite">The sourceInvite<see cref="SIPRequest"/></param>
        /// <returns>The <see cref="Task{SIP_ServiceStartResult}"/></returns>
        public async Task<SIP_ServiceStartResult> StartLegDAsync(SIP_ServerSession session, SIPRequest sourceInvite) {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (sourceInvite == null) throw new ArgumentNullException(nameof(sourceInvite));
            if (!session.IsServiceSession) throw new InvalidOperationException("Session ist keine Service-Session.");
            if (session.LegD.ServiceLeg == null) throw new InvalidOperationException("Service-Leg-D ist nicht vorbereitet.");

            if (IsLegDFinal(session)) {
                _logger?.Warn($"SVC LegD Start blockiert | Ebene=Controller | SessionID={session.SessionId} | LegD={session.LegD.CallId ?? "-"} | Service={session.LegD.InternalNumber ?? "-"} | Grund=LegD bereits beendet");
                return SIP_ServiceStartResult.Fail("Service-Leg-D ist bereits beendet.");
            }

            _logger?.Info($"SVC LegD Start angefordert | Ebene=Controller | SessionID={session.SessionId} | LegD={session.LegD.CallId ?? "-"} | Service={session.LegD.InternalNumber ?? "-"} | Type={session.LegD.ServiceType ?? "-"} | SourceInviteCallID={sourceInvite.Header?.CallId} | SourceInviteBodyLength={sourceInvite.Body?.Length ?? 0}");

            if (!TryEnterLegD(session, "service-start")) {
                _logger?.Warn($"SVC LegD Start blockiert | Ebene=Controller | SessionID={session.SessionId} | Grund=service-start bereits aktiv");
                return SIP_ServiceStartResult.Fail("Service-Leg-D läuft bereits für diese Session.");
            }

            _commands.LegD.StoreServicePrepared(session, session.LegD.ServiceLeg);
            _events.LegD.HandleServiceSelected(session.SessionId, session.LegD.InternalNumber, session.LegD.ServiceType);

            if (!TryGetSessionMediaAdvertisedIp(session, out string advertisedIp)) {
                _logger?.Error($"SVC LegD Start abgebrochen | Ebene=Controller | SessionID={session.SessionId} | Grund=MediaAdvertisedIp fehlt");
                LeaveLegD(session);
                return SIP_ServiceStartResult.Fail("MediaAdvertisedIp fehlt.");
            }
            _logger?.Info($"SVC LegD Media vorbereitet | Ebene=Controller | SessionID={session.SessionId} | MediaAdvertisedIp={advertisedIp}");

            SIP_ServiceStartResult result = await _media.StartAsync(session, sourceInvite, advertisedIp, _mediaManager);
            if (IsLegDFinal(session)) {
                _logger?.Warn($"SVC LegD Start-Ergebnis verworfen | Ebene=Controller | SessionID={session.SessionId} | LegD={session.LegD.CallId ?? "-"} | Service={session.LegD.InternalNumber ?? "-"} | Grund=LegD wurde waehrend Start beendet");
                _media.StopSession(session.SessionId, "service-start-after-end");
                LeaveLegD(session);
                return SIP_ServiceStartResult.Fail("Service-Leg-D wurde waehrend des Starts beendet.");
            }

            _commands.LegD.StoreServiceStarted(session, result);

            if (result.Success) {
                _logger?.Info($"SVC LegD Start erfolgreich | Ebene=Controller | SessionID={session.SessionId} | RTP={result.LocalRtpPort} | AnswerSdpLength={result.AnswerSdp?.Length ?? 0}");
                _events.LegD.HandleServiceStarted(session.SessionId, session.LegD.InternalNumber, result.LocalRtpPort);
            } else {
                _logger?.Warn($"SVC LegD Start fehlgeschlagen | Ebene=Controller | SessionID={session.SessionId} | Grund={result.Reason ?? "-"}");
                _events.LegD.HandleServiceFailed(session.SessionId, session.LegD.InternalNumber, result.Reason);
                LeaveLegD(session);
            }

            return result;
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
                _logger?.Error($"SVC MediaAdvertisedIp fehlt | SessionID={session.SessionId}");
                mediaAdvertisedIp = string.Empty;
                return false;
            }

            mediaAdvertisedIp = parsed.ToString();
            return true;
        }

        /// <summary>
        /// The MarkLegDAnswered
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        public void MarkLegDAnswered(SIP_ServerSession session) {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (!session.IsServiceSession) return;

            if (IsLegDFinal(session)) {
                _logger?.Warn($"SVC LegD Answered ignoriert | Ebene=Controller | SessionID={session.SessionId} | LegD={session.LegD.CallId ?? "-"} | Service={session.LegD.InternalNumber ?? "-"} | Grund=LegD bereits beendet");
                return;
            }

            _logger?.Info($"SVC LegD Answered | Ebene=Controller | SessionID={session.SessionId} | LegD={session.LegD.CallId ?? "-"} | Service={session.LegD.InternalNumber ?? "-"}");
            _commands.LegD.StoreServiceAnswered(session);
            _events.LegD.HandleServiceAnswered(session.SessionId, session.LegD.InternalNumber);
        }

        /// <summary>
        /// The HandleDtmfSequenceAsync
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="sequence">The sequence<see cref="string"/></param>
        /// <returns>The <see cref="Task{bool}"/></returns>
        public async Task<bool> HandleDtmfSequenceAsync(SIP_ServerSession session, string sequence) {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (!session.IsServiceSession || session.LegD.ServiceLeg == null) return false;

            string normalizedSequence = (sequence ?? string.Empty).Trim();
            if (IsLegDFinal(session)) {
                _logger?.Warn($"SVC DTMF ignoriert | SessionID={session.SessionId} | Grund=LegD bereits beendet | Sequence={normalizedSequence}");
                return false;
            }

            if (string.Equals(normalizedSequence, "#", StringComparison.OrdinalIgnoreCase)) {
                if (_media.HasActiveRecording(session.SessionId)) {
                    _logger?.Info($"SVC Recording per DTMF beendet | SessionID={session.SessionId} | Sequence=# | Grund=dtmf-stop");
                    return await _media.StopActiveRecordingAndReturnToMainMenuAsync(session, "dtmf-stop");
                }

                _logger?.Info($"SVC Aktion per DTMF beendet | SessionID={session.SessionId} | Sequence=# | Grund=dtmf-stop");
                EndLegD(session, "dtmf-stop");
                return true;
            }

            TKSVC_Session currentServiceLeg = session.LegD.ServiceLeg;
            if (!TKSVC_Menu.IsMenuService(currentServiceLeg)
                && !_media.HasActiveRecording(session.SessionId)) {
                _logger?.Info($"SVC DTMF ignoriert | SessionID={session.SessionId} | Service={currentServiceLeg.InternalNumber} | Sequence={normalizedSequence}");
                return false;
            }

            string directDialNumber = TKSVC_Routing.NormalizeServiceNumber(normalizedSequence);
            string menuRoot = ResolveMenuRoot(currentServiceLeg);
            if (string.IsNullOrWhiteSpace(directDialNumber)
                && !TKSVC_Menu.TryBuildDirectDialNumber(menuRoot, normalizedSequence, out directDialNumber)) {
                _logger?.Warn($"SVC Menue DTMF ungueltig | SessionID={session.SessionId} | Root={menuRoot} | Sequence={normalizedSequence}");
                return false;
            }

            SIPIntResolvedTarget target = _routing.ResolveInternalTarget(directDialNumber);
            if (!target.Success || target.ServiceLeg == null) {
                _logger?.Warn($"SVC Menue Ziel nicht gefunden | SessionID={session.SessionId} | Wahl={directDialNumber} | Grund={target.Reason ?? "-"}");
                return false;
            }

            if (_media.HasActiveRecording(session.SessionId) && IsSameService(currentServiceLeg, target.ServiceLeg)) {
                _logger?.Info($"SVC Recording per Servicecode beendet | SessionID={session.SessionId} | Service={target.ServiceLeg.InternalNumber} | Grund=recording-toggle-stop");
                return await _media.StopActiveRecordingAndReturnToMainMenuAsync(session, "recording-toggle-stop");
            }

            lock (session.SyncRoot) {
                session.LegD.ServiceLeg = target.ServiceLeg;
                session.LegD.InternalNumber = target.ServiceLeg.InternalNumber;
                session.LegD.ServiceType = target.ServiceLeg.ServiceType;
                session.LegD.ServiceCode = target.ServiceLeg.ServiceCode;
                session.UpdatedAt = DateTime.Now;
            }

            _commands.LegD.StoreServicePrepared(session, target.ServiceLeg);
            _events.LegD.HandleServiceSelected(session.SessionId, target.ServiceLeg.InternalNumber, target.ServiceLeg.ServiceType);

            bool handled = await _media.HandleMenuSelectionAsync(session, target.ServiceLeg);
            if (handled && !IsLegDFinal(session)) {
                _commands.LegD.StoreServiceStarted(session, SIP_ServiceStartResult.Ok(session.LegD.PreparedAnswerSdp ?? string.Empty, session.LegD.RelayPort));
                _events.LegD.HandleServiceStarted(session.SessionId, target.ServiceLeg.InternalNumber, session.LegD.RelayPort);
            } else if (handled) {
                _logger?.Warn($"SVC Menue Ergebnis verworfen | SessionID={session.SessionId} | Ziel={target.ServiceLeg.InternalNumber} | Grund=LegD bereits beendet");
            }

            return handled;
        }

        private static string ResolveMenuRoot(TKSVC_Session serviceLeg) {
            string configuredRoot = TKSVC_Routing.NormalizeServiceNumber(serviceLeg.MenuRootNumber);
            if (!string.IsNullOrWhiteSpace(configuredRoot)) {
                return configuredRoot;
            }

            string normalized = TKSVC_Routing.NormalizeServiceNumber(serviceLeg.InternalNumber);
            if (string.IsNullOrWhiteSpace(normalized)) {
                return string.Empty;
            }

            int nextSeparator = normalized.IndexOf('*', 1);
            return nextSeparator > 0 ? normalized.Substring(0, nextSeparator) : normalized;
        }

        private static bool IsSameService(TKSVC_Session? left, TKSVC_Session? right) {
            if (left == null || right == null) {
                return false;
            }

            return string.Equals(TKSVC_Routing.NormalizeServiceNumber(left.InternalNumber), TKSVC_Routing.NormalizeServiceNumber(right.InternalNumber), StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(left.ServiceCode) && string.Equals(left.ServiceCode, right.ServiceCode, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// The StartAttachedLegD
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="serviceLeg">The serviceLeg<see cref="TKSVC_Session"/></param>
        /// <param name="reason">The reason<see cref="string?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public bool StartAttachedLegD(SIP_ServerSession session, TKSVC_Session serviceLeg, string? reason) {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (serviceLeg == null) throw new ArgumentNullException(nameof(serviceLeg));
            if (session.IsServiceSession) return false;

            if (!_sessions.AttachServiceLegD(session, serviceLeg)) {
                return false;
            }

            _commands.LegD.StoreServicePrepared(session, serviceLeg);
            _events.LegD.HandleServiceSelected(session.SessionId, serviceLeg.InternalNumber, serviceLeg.ServiceType);
            _logger?.Info($"SVC LegD an laufenden Call angehaengt | Ebene=Controller | SessionID={session.SessionId} | LegD={session.LegD.CallId ?? "-"} | Service={serviceLeg.InternalNumber} | Type={serviceLeg.ServiceType} | Grund={reason ?? "-"}");
            return true;
        }

        /// <summary>
        /// Prueft, ob fuer den laufenden Call ein SVC.LegD Recording aktiv ist.
        /// </summary>
        public bool HasAttachedCallRecording(SIP_ServerSession session) {
            if (session == null || string.IsNullOrWhiteSpace(session.SessionId)) {
                return false;
            }

            return _media.HasActiveRecording(session.SessionId);
        }

        /// <summary>
        /// Prueft, ob fuer die Session ein SVC.LegD Recording aktiv ist.
        /// </summary>
        public bool HasAttachedCallRecording(string sessionId) {
            return _media.HasActiveRecording(sessionId);
        }

        /// <summary>
        /// Haengt SVC.LegD an einen laufenden Call und startet die Call-Aufnahme ueber den Service-Media-Pfad.
        /// </summary>
        public async Task<bool> StartAttachedCallRecordingAsync(SIP_ServerSession session, TKSVC_Session serviceLeg, string? invokerLegName = null) {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (serviceLeg == null) throw new ArgumentNullException(nameof(serviceLeg));
            if (session.IsServiceSession) return false;

            if (!StartAttachedLegD(session, serviceLeg, "recording-toggle-start")) {
                return false;
            }

            _logger?.Info($"SVC LegD CallRecording angehaengt | Ebene=Controller | SessionID={session.SessionId} | LegD={session.LegD.CallId ?? "-"} | Service={serviceLeg.InternalNumber} | Ausloeser={invokerLegName ?? "-"}");

            // Die eigentliche A+B-Aufnahme bleibt am vorhandenen MediaManager/Call-Recorder.
            // Diese Methode stellt den von SipInt erwarteten SVC.LegD-Anker bereit und haelt die Bereiche getrennt.
            await Task.CompletedTask;
            return true;
        }

        /// <summary>
        /// Kompatibilitaetsueberladung fuer SipInt-Aufrufe mit expliziter Startansage.
        /// </summary>
        public Task<bool> StartAttachedCallRecordingAsync(SIP_ServerSession session, TKSVC_Session serviceLeg, string? invokerLegName, bool announceStart) {
            return StartAttachedCallRecordingAsync(session, serviceLeg, invokerLegName);
        }

        /// <summary>
        /// Kompatibilitaetsueberladung fuer SipInt-Aufrufe mit explizitem Grund.
        /// </summary>
        public Task<bool> StartAttachedCallRecordingAsync(SIP_ServerSession session, TKSVC_Session serviceLeg, string? invokerLegName, string? reason) {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (serviceLeg == null) throw new ArgumentNullException(nameof(serviceLeg));
            if (session.IsServiceSession) return Task.FromResult(false);

            if (!StartAttachedLegD(session, serviceLeg, string.IsNullOrWhiteSpace(reason) ? "recording-toggle-start" : reason)) {
                return Task.FromResult(false);
            }

            _logger?.Info($"SVC LegD CallRecording angehaengt | Ebene=Controller | SessionID={session.SessionId} | LegD={session.LegD.CallId ?? "-"} | Service={serviceLeg.InternalNumber} | Ausloeser={invokerLegName ?? "-"} | Grund={reason ?? "recording-toggle-start"}");
            return Task.FromResult(true);
        }

        /// <summary>
        /// Stoppt den an einen laufenden Call angehaengten SVC.LegD Recording-Kontext.
        /// </summary>
        public bool StopAttachedCallRecording(SIP_ServerSession session, string? reason = null, string? invokerLegName = null, bool announceStop = false) {
            if (session == null || string.IsNullOrWhiteSpace(session.SessionId)) {
                return false;
            }

            _logger?.Info($"SVC LegD CallRecording Stop | Ebene=Controller | SessionID={session.SessionId} | LegD={session.LegD.CallId ?? "-"} | Grund={reason ?? "recording-stop"} | Ausloeser={invokerLegName ?? "-"} | Ansage={announceStop}");
            EndLegD(session, reason ?? "recording-stop");
            return true;
        }

        /// <summary>
        /// The EndLegD
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="reason">The reason<see cref="string?"/></param>
        public void EndLegD(SIP_ServerSession session, string? reason) {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (!session.IsServiceSession && !session.HasAttachedServiceLegD) return;

            bool alreadyEnded;
            lock (session.SyncRoot) {
                alreadyEnded = session.LegD.IsEnded;
                if (!alreadyEnded) {
                    session.LegD.IsEnded = true;
                    session.LegD.UpdatedAt = DateTime.Now;
                    session.UpdatedAt = DateTime.Now;
                }
            }

            if (alreadyEnded) {
                _logger?.Warn($"SVC LegD Ende ignoriert | Ebene=Controller | SessionID={session.SessionId} | LegD={session.LegD.CallId ?? "-"} | Service={session.LegD.InternalNumber ?? "-"} | Grund={reason ?? "-"} | Status=bereits beendet");
                _media.StopSession(session.SessionId, reason);
                LeaveLegD(session);
                return;
            }

            _logger?.Info($"SVC LegD Ende angefordert | Ebene=Controller | SessionID={session.SessionId} | LegD={session.LegD.CallId ?? "-"} | Service={session.LegD.InternalNumber ?? "-"} | Grund={reason ?? "-"}");
            _media.StopSession(session.SessionId, reason);
            _commands.LegD.StoreServiceEnded(session, reason);
            _events.LegD.HandleServiceEnded(session.SessionId, session.LegD.InternalNumber, reason);
            LeaveLegD(session);
        }

        private bool TryEnterLegD(SIP_ServerSession session, string reason) {
            if (session == null || string.IsNullOrWhiteSpace(session.SessionId)) {
                return false;
            }

            bool ok = _activeLegD.TryAdd(session.SessionId, reason ?? string.Empty);
            if (!ok) {
                _logger?.Warn($"SVC LegD Mehrfachstart blockiert | SessionID={session.SessionId} | Grund={reason}");
            }
            return ok;
        }

        private void LeaveLegD(SIP_ServerSession session) {
            if (session == null || string.IsNullOrWhiteSpace(session.SessionId)) {
                return;
            }

            _activeLegD.TryRemove(session.SessionId, out _);
            _logger?.Info($"SVC LegD Start-Sperre geloest | SessionID={session.SessionId}");
        }

        private static bool IsLegDFinal(SIP_ServerSession session) {
            lock (session.SyncRoot) {
                return session.IsTerminated || session.LegD.IsEnded;
            }
        }
    }
}
