namespace TKServer.SVC {
    using SIPSorcery.Media;
    using SIPSorcery.Net;
    using SIPSorcery.SIP;
    using SIPSorcery.SIP.App;
    using SIPSorceryMedia.Abstractions;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using TKServer.BASE;
    using TKServer.SQL;


    /// <summary>
    /// Defines the <see cref="TKSVC_Media" />
    /// </summary>
    internal sealed class TKSVC_Media : IDisposable {
        /// <summary>
        /// Defines the _runtimes
        /// </summary>
        private readonly ConcurrentDictionary<string, SIP_ServiceRuntime> _runtimes = new();

        /// <summary>
        /// Defines the _rtpDtmfDedup
        /// </summary>
        private readonly ConcurrentDictionary<string, DateTime> _rtpDtmfDedup = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Defines the _logger
        /// </summary>
        private readonly TKBase_LogManager? _logger;

        /// <summary>
        /// Defines the _serviceRegistry
        /// </summary>
        private readonly TKSVC_ServiceRegistry _serviceRegistry;

        /// <summary>
        /// Defines the _dtmfManager
        /// </summary>
        private readonly TKBase_DTMF_Manager _dtmfManager;

        /// <summary>
        /// Defines the _menuDtmfHandler
        /// </summary>
        private readonly Func<SIP_ServerSession, string, Task<bool>>? _menuDtmfHandler;

        /// <summary>
        /// Defines the _record
        /// </summary>
        private readonly TKSVC_Record _record;

        /// <summary>
        /// Defines the _settings
        /// </summary>
        private readonly TKBase_ServerSettings _settings;

        /// <summary>
        /// Initializes a new instance of the <see cref="TKSVC_Media"/> class.
        /// </summary>
        /// <param name="settings">The settings<see cref="TKBase_ServerSettings"/></param>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        /// <param name="serviceRegistry">The serviceRegistry<see cref="TKSVC_ServiceRegistry?"/></param>
        /// <param name="dtmfManager">The dtmfManager<see cref="TKBase_DTMF_Manager?"/></param>
        /// <param name="menuDtmfHandler">The menuDtmfHandler<see cref="Func{SIP_ServerSession, string, Task{bool}}?"/></param>
        public TKSVC_Media(
            TKBase_ServerSettings settings,
            TKBase_LogManager? logger = null,
            TKSVC_ServiceRegistry? serviceRegistry = null,
            TKBase_DTMF_Manager? dtmfManager = null,
            Func<SIP_ServerSession, string, Task<bool>>? menuDtmfHandler = null) {
            _settings = settings;
            _logger = logger;
            _serviceRegistry = serviceRegistry ?? new TKSVC_ServiceRegistry(logger);
            _dtmfManager = dtmfManager ?? new TKBase_DTMF_Manager(logger);
            _menuDtmfHandler = menuDtmfHandler;
            _record = new TKSVC_Record(settings, logger);
        }

        /// <summary>
        /// The StartAsync
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="invite">The invite<see cref="SIPRequest"/></param>
        /// <param name="mediaAdvertisedIp">The mediaAdvertisedIp<see cref="string"/></param>
        /// <param name="portAllocator">The portAllocator<see cref="TKBase_MediaManager"/></param>
        /// <returns>The <see cref="Task{SIP_ServiceStartResult}"/></returns>
        public async Task<SIP_ServiceStartResult> StartAsync(
            SIP_ServerSession session,
            SIPRequest invite,
            string mediaAdvertisedIp,
            TKBase_MediaManager portAllocator) {

            ArgumentNullException.ThrowIfNull(session);
            ArgumentNullException.ThrowIfNull(invite);
            ArgumentNullException.ThrowIfNull(portAllocator);

            TKSVC_Session serviceLeg = session.LegD.ServiceLeg
                ?? throw new InvalidOperationException("ServiceLeg fehlt.");

            if (IsSessionClosed(session)) {
                _logger?.Warn($"SVC Media Start blockiert | SessionID={session.SessionId} | Grund=LegD bereits beendet");
                return SIP_ServiceStartResult.Fail("Service-Leg-D ist bereits beendet.");
            }

            string serviceType = TKSVC_ServiceTypes.Normalize(serviceLeg.ServiceType);

            return serviceType switch {
                var x when string.Equals(x, TKSVC_ServiceTypes.TimeSpeech, StringComparison.OrdinalIgnoreCase)
                    => await StartTimeSpeechAsync(session, invite, serviceLeg, mediaAdvertisedIp, portAllocator),
                var x when string.Equals(x, TKSVC_ServiceTypes.ServiceMenu, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x, TKSVC_ServiceTypes.MenuHelp, StringComparison.OrdinalIgnoreCase)
                    => await StartMenuSpeechAsync(session, invite, serviceLeg, mediaAdvertisedIp, portAllocator),
                var x when string.Equals(x, TKSVC_ServiceTypes.VoicemailGreetingPlayback, StringComparison.OrdinalIgnoreCase)
                    => await StartMenuSpeechAsync(session, invite, serviceLeg, mediaAdvertisedIp, portAllocator),
                var x when string.Equals(x, TKSVC_ServiceTypes.Recorder, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x, TKSVC_ServiceTypes.Voicemail, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x, TKSVC_ServiceTypes.VoicemailGreeting, StringComparison.OrdinalIgnoreCase)
                    => await StartRecordingAsync(session, invite, serviceLeg, mediaAdvertisedIp, portAllocator, TKSVC_RecordMode.MailboxPartner),
                var x when string.Equals(x, TKSVC_ServiceTypes.CallRecording, StringComparison.OrdinalIgnoreCase)
                    => await StartRecordingAsync(session, invite, serviceLeg, mediaAdvertisedIp, portAllocator, TKSVC_RecordMode.CallListener),
                _ => SIP_ServiceStartResult.Fail($"Service-Typ nicht implementiert: {serviceType}")
            };
        }

        /// <summary>
        /// The StopSession
        /// </summary>
        /// <param name="sessionId">The sessionId<see cref="string"/></param>
        /// <param name="reason">The reason<see cref="string?"/></param>
        public void StopSession(string sessionId, string? reason = null) {
            if (string.IsNullOrWhiteSpace(sessionId)) {
                return;
            }

            if (_runtimes.TryRemove(sessionId, out SIP_ServiceRuntime? runtime) && runtime != null) {
                runtime.Dispose(reason ?? "service-stop");
                _logger?.Info($"Service-Media gestoppt | SessionID={sessionId} | Grund={reason}");
            }
        }

        public bool HasActiveRecording(string sessionId) {
            return !string.IsNullOrWhiteSpace(sessionId)
                && _runtimes.TryGetValue(sessionId, out SIP_ServiceRuntime? runtime)
                && runtime?.Recording != null;
        }

        public async Task<bool> StopActiveRecordingAndReturnToMainMenuAsync(SIP_ServerSession session, string reason) {
            ArgumentNullException.ThrowIfNull(session);

            if (!_runtimes.TryGetValue(session.SessionId, out SIP_ServiceRuntime? runtime) || runtime == null) {
                return false;
            }

            if (!runtime.StopRecordingOnly(reason, out string? serviceNumber, out bool hadAnyAudio, out bool hadUsableAudio, out long bytesWritten, out int? durationMs)) {
                return false;
            }

            _logger?.Info($"SVC Recording gestoppt | SessionID={session.SessionId} | Service={serviceNumber ?? "-"} | Grund={reason} | AufnahmeVorhanden={hadAnyAudio} | AufnahmeGueltig={hadUsableAudio} | Bytes={bytesWritten} | DauerMs={durationMs?.ToString() ?? "-"}");
            return await PlayMainMenuOnRuntimeAsync(session, runtime, reason);
        }

        public async Task<bool> ReturnToMainMenuAsync(SIP_ServerSession session, string reason) {
            ArgumentNullException.ThrowIfNull(session);

            if (!_runtimes.TryGetValue(session.SessionId, out SIP_ServiceRuntime? runtime) || runtime == null) {
                return false;
            }

            return await PlayMainMenuOnRuntimeAsync(session, runtime, reason);
        }

        /// <summary>
        /// The HandleMenuSelectionAsync
        /// </summary>
        public async Task<bool> HandleMenuSelectionAsync(SIP_ServerSession session, TKSVC_Session targetServiceLeg) {
            ArgumentNullException.ThrowIfNull(session);
            ArgumentNullException.ThrowIfNull(targetServiceLeg);

            if (!_runtimes.TryGetValue(session.SessionId, out SIP_ServiceRuntime? runtime) || runtime == null) {
                _logger?.Warn($"Service-Menue Auswahl ohne aktive Media-Runtime | SessionID={session.SessionId} | Ziel={targetServiceLeg.InternalNumber}");
                return false;
            }

            if (IsSessionClosed(session)) {
                _logger?.Warn($"Service-Menue Auswahl ignoriert | SessionID={session.SessionId} | Ziel={targetServiceLeg.InternalNumber} | Grund=LegD bereits beendet");
                return false;
            }

            string serviceType = TKSVC_ServiceTypes.Normalize(targetServiceLeg.ServiceType);
            if (string.Equals(serviceType, TKSVC_ServiceTypes.TimeSpeech, StringComparison.OrdinalIgnoreCase)) {
                return await PlayTimeSpeechOnRuntimeAsync(runtime, targetServiceLeg);
            }

            if (string.Equals(serviceType, TKSVC_ServiceTypes.ServiceMenu, StringComparison.OrdinalIgnoreCase)
                || string.Equals(serviceType, TKSVC_ServiceTypes.MenuHelp, StringComparison.OrdinalIgnoreCase)) {
                return await PlayMenuPromptOnRuntimeAsync(runtime, targetServiceLeg);
            }

            if (string.Equals(serviceType, TKSVC_ServiceTypes.VoicemailGreetingPlayback, StringComparison.OrdinalIgnoreCase)) {
                return await PlayMailboxGreetingOnRuntimeAsync(runtime, targetServiceLeg);
            }

            if (string.Equals(serviceType, TKSVC_ServiceTypes.Recorder, StringComparison.OrdinalIgnoreCase)
                || string.Equals(serviceType, TKSVC_ServiceTypes.Voicemail, StringComparison.OrdinalIgnoreCase)
                || string.Equals(serviceType, TKSVC_ServiceTypes.VoicemailGreeting, StringComparison.OrdinalIgnoreCase)) {
                return await StartRecordingOnRuntimeAsync(runtime, targetServiceLeg, TKSVC_RecordMode.MailboxPartner);
            }

            if (string.Equals(serviceType, TKSVC_ServiceTypes.CallRecording, StringComparison.OrdinalIgnoreCase)) {
                return await StartRecordingOnRuntimeAsync(runtime, targetServiceLeg, TKSVC_RecordMode.CallListener);
            }

            return await PlayTextOnRuntimeAsync(runtime, targetServiceLeg, "Dieser Menuepunkt ist noch nicht umgesetzt.");
        }

        /// <summary>
        /// The Dispose
        /// </summary>
        public void Dispose() {
            foreach (string sessionId in _runtimes.Keys) {
                StopSession(sessionId, "dispose");
            }
        }

        private static bool IsSessionClosed(SIP_ServerSession session) {
            lock (session.SyncRoot) {
                return session.IsTerminated || session.LegD.IsEnded;
            }
        }

        private SIP_ServiceStartResult AbortStartAfterSessionClosed(SIP_ServerSession session, IMediaSession? mediaSession, TKBase_MediaManager portAllocator, int localRtpPort, string reason) {
            try {
                mediaSession?.Close(reason);
            } catch (Exception ex) {
                _logger?.Warn($"SVC Media Close nach beendetem LegD fehlgeschlagen | SessionID={session.SessionId} | Fehler={ex.Message}");
            }

            portAllocator.Release(localRtpPort);
            _logger?.Warn($"SVC Media Start abgebrochen | SessionID={session.SessionId} | Grund=LegD bereits beendet | Reason={reason}");
            return SIP_ServiceStartResult.Fail("Service-Leg-D wurde waehrend des Media-Starts beendet.");
        }

        /// <summary>
        /// The StartTimeSpeechAsync
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="invite">The invite<see cref="SIPRequest"/></param>
        /// <param name="serviceLeg">The serviceLeg<see cref="TKSVC_Session"/></param>
        /// <param name="mediaAdvertisedIp">The mediaAdvertisedIp<see cref="string"/></param>
        /// <param name="portAllocator">The portAllocator<see cref="TKBase_MediaManager"/></param>
        /// <returns>The <see cref="Task{SIP_ServiceStartResult}"/></returns>
        private async Task<SIP_ServiceStartResult> StartTimeSpeechAsync(
            SIP_ServerSession session,
            SIPRequest invite,
            TKSVC_Session serviceLeg,
            string mediaAdvertisedIp,
            TKBase_MediaManager portAllocator) {

            if (string.IsNullOrWhiteSpace(invite.Body)) {
                return SIP_ServiceStartResult.Fail("INVITE ohne SDP.");
            }

            if (!IPAddress.TryParse(mediaAdvertisedIp, out IPAddress? advertisedIp)) {
                return SIP_ServiceStartResult.Fail($"Media-IP ungültig: {mediaAdvertisedIp}");
            }

            SDP remoteOffer;
            try {
                remoteOffer = SDP.ParseSDPDescription(invite.Body);
            } catch (Exception ex) {
                return SIP_ServiceStartResult.Fail($"SDP konnte nicht gelesen werden: {ex.Message}");
            }

            (int rtp, int rtcp)? allocatedPorts = portAllocator.Allocate();
            if (allocatedPorts == null) {
                return SIP_ServiceStartResult.Fail("Kein RTP-Port für Service verfügbar.");
            }

            int localRtpPort = allocatedPorts.Value.rtp;
            ServiceMediaSession? mediaSession = null;
            MemoryStream? pcmStream = null;

            try {
                TimeSpeechOptions options = TimeSpeechOptions.FromServiceLeg(serviceLeg, _logger);

                mediaSession = new ServiceMediaSession(advertisedIp, localRtpPort, MediaStreamStatusEnum.SendRecv, _logger);
                mediaSession.AudioExtrasSource.SetSource(AudioSourcesEnum.None);
                RegisterRtpDtmfHandler(session, mediaSession);

                SetDescriptionResultEnum setRemoteResult = mediaSession.SetRemoteDescription(SdpType.offer, remoteOffer);
                if (setRemoteResult != SetDescriptionResultEnum.OK) {
                    mediaSession.Close("set-remote-description-failed");
                    portAllocator.Release(localRtpPort);
                    return SIP_ServiceStartResult.Fail($"Remote SDP nicht kompatibel: {setRemoteResult}");
                }

                SDP answer = mediaSession.CreateAnswer(advertisedIp);
                if (answer == null) {
                    mediaSession.Close("create-answer-failed");
                    portAllocator.Release(localRtpPort);
                    return SIP_ServiceStartResult.Fail("Service-SDP-Antwort konnte nicht erstellt werden.");
                }

                await mediaSession.Start();

                SIP_SystemSpeech speech = new SIP_SystemSpeech(_logger);
                pcmStream = speech.SynthesizeCurrentTimeToRawPcmStream(
                    voiceName: options.VoiceName,
                    sampleRate: options.SampleRate,
                    volume: options.Volume,
                    rate: options.Rate,
                    includeSeconds: options.IncludeSeconds);

                AudioSamplingRatesEnum streamRate = options.SampleRate <= 8000
                    ? AudioSamplingRatesEnum.Rate8KHz
                    : AudioSamplingRatesEnum.Rate16KHz;

                if (IsSessionClosed(session)) {
                    return AbortStartAfterSessionClosed(session, mediaSession, portAllocator, localRtpPort, "timespeech-legd-ended");
                }

                SIP_ServiceRuntime runtime = new SIP_ServiceRuntime(
                    session,
                    serviceLeg,
                    mediaSession,
                    pcmStream,
                    localRtpPort,
                    options.KeepSessionUntilPeerEnded,
                    portAllocator,
                    _logger);

                if (!_runtimes.TryAdd(session.SessionId, runtime)) {
                    runtime.Dispose("runtime-already-exists");
                    return SIP_ServiceStartResult.Fail("Service-Session existiert bereits.");
                }

                lock (session.SyncRoot) {
                    session.LegD.RelayPort = localRtpPort;
                    session.LegD.PreparedAnswerSdp = answer.ToString();
                    session.MediaAdvertisedIp = mediaAdvertisedIp;
                    session.UpdatedAt = DateTime.Now;
                }

                runtime.PlaybackTask = mediaSession.AudioExtrasSource.SendAudioFromStream(pcmStream, streamRate);
                _ = runtime.PlaybackTask.ContinueWith(
                    t => OnPlaybackCompleted(runtime, pcmStream, t.Exception),
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default);

                _logger?.Info(
                    $"Service TimeSpeech gestartet | SessionID={session.SessionId} | InternalNumber={serviceLeg.InternalNumber} | RTP={localRtpPort} | SampleRate={options.SampleRate} | IncludeSeconds={options.IncludeSeconds}");

                return SIP_ServiceStartResult.Ok(answer.ToString(), localRtpPort);
            } catch (Exception ex) {
                try {
                    mediaSession?.Close("timespeech-start-error");
                } catch {
                }

                try {
                    pcmStream?.Dispose();
                } catch {
                }

                portAllocator.Release(localRtpPort);
                _logger?.Error($"Service TimeSpeech Start fehlgeschlagen | SessionID={session.SessionId} | {ex}");
                return SIP_ServiceStartResult.Fail(ex.Message);
            }
        }

        /// <summary>
        /// The StartMenuSpeechAsync
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="invite">The invite<see cref="SIPRequest"/></param>
        /// <param name="serviceLeg">The serviceLeg<see cref="TKSVC_Session"/></param>
        /// <param name="mediaAdvertisedIp">The mediaAdvertisedIp<see cref="string"/></param>
        /// <param name="portAllocator">The portAllocator<see cref="TKBase_MediaManager"/></param>
        /// <returns>The <see cref="Task{SIP_ServiceStartResult}"/></returns>
        private async Task<SIP_ServiceStartResult> StartMenuSpeechAsync(
            SIP_ServerSession session,
            SIPRequest invite,
            TKSVC_Session serviceLeg,
            string mediaAdvertisedIp,
            TKBase_MediaManager portAllocator) {

            if (string.IsNullOrWhiteSpace(invite.Body)) {
                return SIP_ServiceStartResult.Fail("INVITE ohne SDP.");
            }

            if (!IPAddress.TryParse(mediaAdvertisedIp, out IPAddress? advertisedIp)) {
                return SIP_ServiceStartResult.Fail($"Media-IP ungültig: {mediaAdvertisedIp}");
            }

            SDP remoteOffer;
            try {
                remoteOffer = SDP.ParseSDPDescription(invite.Body);
            } catch (Exception ex) {
                return SIP_ServiceStartResult.Fail($"SDP konnte nicht gelesen werden: {ex.Message}");
            }

            (int rtp, int rtcp)? allocatedPorts = portAllocator.Allocate();
            if (allocatedPorts == null) {
                return SIP_ServiceStartResult.Fail("Kein RTP-Port für Service verfügbar.");
            }

            int localRtpPort = allocatedPorts.Value.rtp;
            ServiceMediaSession? mediaSession = null;
            MemoryStream? pcmStream = null;

            try {
                TKSVC_Menu menu = new TKSVC_Menu(_logger);
                IReadOnlyList<TKSVC_Session> menuEntries = GetMenuEntries(serviceLeg);
                TKSVC_MenuPrompt prompt = menu.CreatePrompt(serviceLeg, menuEntries);

                mediaSession = new ServiceMediaSession(advertisedIp, localRtpPort, MediaStreamStatusEnum.SendRecv, _logger);
                mediaSession.AudioExtrasSource.SetSource(AudioSourcesEnum.None);
                RegisterRtpDtmfHandler(session, mediaSession);

                SetDescriptionResultEnum setRemoteResult = mediaSession.SetRemoteDescription(SdpType.offer, remoteOffer);
                if (setRemoteResult != SetDescriptionResultEnum.OK) {
                    mediaSession.Close("set-remote-description-failed");
                    portAllocator.Release(localRtpPort);
                    return SIP_ServiceStartResult.Fail($"Remote SDP nicht kompatibel: {setRemoteResult}");
                }

                SDP answer = mediaSession.CreateAnswer(advertisedIp);
                if (answer == null) {
                    mediaSession.Close("create-answer-failed");
                    portAllocator.Release(localRtpPort);
                    return SIP_ServiceStartResult.Fail("Service-SDP-Antwort konnte nicht erstellt werden.");
                }

                await mediaSession.Start();

                pcmStream = menu.SynthesizePromptToRawPcmStream(prompt);

                AudioSamplingRatesEnum streamRate = prompt.SampleRate <= 8000
                    ? AudioSamplingRatesEnum.Rate8KHz
                    : AudioSamplingRatesEnum.Rate16KHz;

                if (IsSessionClosed(session)) {
                    return AbortStartAfterSessionClosed(session, mediaSession, portAllocator, localRtpPort, "servicemenu-legd-ended");
                }

                SIP_ServiceRuntime runtime = new SIP_ServiceRuntime(
                    session,
                    serviceLeg,
                    mediaSession,
                    pcmStream,
                    localRtpPort,
                    prompt.KeepSessionUntilPeerEnded,
                    portAllocator,
                    _logger);

                mediaSession.OnAudioFrameReceived += frame => runtime.WriteInboundAudioFrame(frame);

                if (!_runtimes.TryAdd(session.SessionId, runtime)) {
                    runtime.Dispose("runtime-already-exists");
                    return SIP_ServiceStartResult.Fail("Service-Session existiert bereits.");
                }

                lock (session.SyncRoot) {
                    session.LegD.RelayPort = localRtpPort;
                    session.LegD.PreparedAnswerSdp = answer.ToString();
                    session.MediaAdvertisedIp = mediaAdvertisedIp;
                    session.UpdatedAt = DateTime.Now;
                }

                runtime.PlaybackTask = mediaSession.AudioExtrasSource.SendAudioFromStream(pcmStream, streamRate);
                _ = runtime.PlaybackTask.ContinueWith(
                    t => OnPlaybackCompleted(runtime, pcmStream, t.Exception),
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default);

                _logger?.Info(
                    $"Service-Menue gestartet | SessionID={session.SessionId} | InternalNumber={serviceLeg.InternalNumber} | RTP={localRtpPort} | SampleRate={prompt.SampleRate} | TextLength={prompt.Text.Length}");

                return SIP_ServiceStartResult.Ok(answer.ToString(), localRtpPort);
            } catch (Exception ex) {
                try {
                    mediaSession?.Close("servicemenu-start-error");
                } catch {
                }

                try {
                    pcmStream?.Dispose();
                } catch {
                }

                portAllocator.Release(localRtpPort);
                _logger?.Error($"Service-Menue Start fehlgeschlagen | SessionID={session.SessionId} | {ex}");
                return SIP_ServiceStartResult.Fail(ex.Message);
            }
        }

        /// <summary>
        /// The OnPlaybackCompleted
        /// </summary>
        /// <param name="runtime">The runtime<see cref="SIP_ServiceRuntime"/></param>
        /// <param name="aggregateException">The aggregateException<see cref="AggregateException?"/></param>
        private void OnPlaybackCompleted(SIP_ServiceRuntime runtime, AggregateException? aggregateException) {
            OnPlaybackCompleted(runtime, null, aggregateException);
        }

        /// <summary>
        /// The StartRecordingAsync
        /// </summary>
        private async Task<SIP_ServiceStartResult> StartRecordingAsync(
            SIP_ServerSession session,
            SIPRequest invite,
            TKSVC_Session serviceLeg,
            string mediaAdvertisedIp,
            TKBase_MediaManager portAllocator,
            TKSVC_RecordMode mode) {

            if (string.IsNullOrWhiteSpace(invite.Body)) {
                return SIP_ServiceStartResult.Fail("INVITE ohne SDP.");
            }

            if (!IPAddress.TryParse(mediaAdvertisedIp, out IPAddress? advertisedIp)) {
                return SIP_ServiceStartResult.Fail($"Media-IP ungültig: {mediaAdvertisedIp}");
            }

            SDP remoteOffer;
            try {
                remoteOffer = SDP.ParseSDPDescription(invite.Body);
            } catch (Exception ex) {
                return SIP_ServiceStartResult.Fail($"SDP konnte nicht gelesen werden: {ex.Message}");
            }

            (int rtp, int rtcp)? allocatedPorts = portAllocator.Allocate();
            if (allocatedPorts == null) {
                return SIP_ServiceStartResult.Fail("Kein RTP-Port für Service verfügbar.");
            }

            int localRtpPort = allocatedPorts.Value.rtp;
            RTPSession? mediaSession = null;
            TKSVC_Record.TKSVC_RecordRuntime? recording = null;

            try {
                AudioEncoder audioEncoder = new AudioEncoder();
                AudioFormat selectedAudioFormat = default;
                bool hasSelectedAudioFormat = false;
                mediaSession = new RTPSession(false, false, false, advertisedIp, localRtpPort, null);
                mediaSession.addTrack(new MediaStreamTrack(
                    audioEncoder.SupportedFormats,
                    mode == TKSVC_RecordMode.MailboxPartner ? MediaStreamStatusEnum.SendRecv : MediaStreamStatusEnum.RecvOnly));
                mediaSession.OnAudioFormatsNegotiated += formats => {
                    if (formats != null && formats.Count > 0) {
                        selectedAudioFormat = formats[0];
                        hasSelectedAudioFormat = true;
                    }
                };
                RegisterRtpDtmfHandler(session, mediaSession);
                mediaSession.OnAudioFrameReceived += frame => {
                    try {
                        recording?.WriteEncodedAudioFrame(frame, audioEncoder);
                    } catch (Exception ex) {
                        _logger?.Warn($"SVC Recording Callback verworfen | SessionID={session.SessionId} | Fehler={ex.Message}");
                    }
                };

                SetDescriptionResultEnum setRemoteResult = mediaSession.SetRemoteDescription(SdpType.offer, remoteOffer);
                if (setRemoteResult != SetDescriptionResultEnum.OK) {
                    mediaSession.Close("set-remote-description-failed");
                    portAllocator.Release(localRtpPort);
                    return SIP_ServiceStartResult.Fail($"Remote SDP nicht kompatibel: {setRemoteResult}");
                }

                SDP answer = mediaSession.CreateAnswer(advertisedIp);
                if (answer == null) {
                    mediaSession.Close("create-answer-failed");
                    portAllocator.Release(localRtpPort);
                    return SIP_ServiceStartResult.Fail("Service-SDP-Antwort konnte nicht erstellt werden.");
                }

                await mediaSession.Start();
                if (IsSessionClosed(session)) {
                    return AbortStartAfterSessionClosed(session, mediaSession, portAllocator, localRtpPort, "recording-legd-ended-before-intro");
                }

                if (mode == TKSVC_RecordMode.MailboxPartner) {
                    RecordingPromptOptions promptOptions = RecordingPromptOptions.FromServiceLeg(serviceLeg, _logger);
                    await PlayRecordingIntroAsync(mediaSession, audioEncoder, hasSelectedAudioFormat ? selectedAudioFormat : audioEncoder.SupportedFormats.FirstOrDefault(), promptOptions);
                }

                if (IsSessionClosed(session)) {
                    return AbortStartAfterSessionClosed(session, mediaSession, portAllocator, localRtpPort, "recording-legd-ended-after-intro");
                }

                recording = mode == TKSVC_RecordMode.CallListener
                    ? _record.StartCallRecording(session, serviceLeg)
                    : _record.StartMailboxRecording(session, serviceLeg);

                if (IsSessionClosed(session)) {
                    recording?.Dispose();
                    recording = null;
                    return AbortStartAfterSessionClosed(session, mediaSession, portAllocator, localRtpPort, "recording-legd-ended-before-runtime");
                }

                SIP_ServiceRuntime runtime = new SIP_ServiceRuntime(
                    session,
                    serviceLeg,
                    mediaSession,
                    null,
                    localRtpPort,
                    serviceLeg.KeepSessionUntilPeerEnded,
                    portAllocator,
                    _logger) {
                    Recording = recording
                };

                if (!_runtimes.TryAdd(session.SessionId, runtime)) {
                    runtime.Dispose("runtime-already-exists");
                    return SIP_ServiceStartResult.Fail("Service-Session existiert bereits.");
                }

                lock (session.SyncRoot) {
                    session.LegD.RelayPort = localRtpPort;
                    session.LegD.PreparedAnswerSdp = answer.ToString();
                    session.MediaAdvertisedIp = mediaAdvertisedIp;
                    session.UpdatedAt = DateTime.Now;
                }

                _logger?.Info($"SVC Recording-Service gestartet | SessionID={session.SessionId} | InternalNumber={serviceLeg.InternalNumber} | Mode={mode} | RTP={localRtpPort}");
                return SIP_ServiceStartResult.Ok(answer.ToString(), localRtpPort);
            } catch (Exception ex) {
                try {
                    recording?.Dispose();
                } catch {
                }

                try {
                    mediaSession?.Close("recording-start-error");
                } catch {
                }

                portAllocator.Release(localRtpPort);
                _logger?.Error($"SVC Recording-Service Start fehlgeschlagen | SessionID={session.SessionId} | {ex}");
                return SIP_ServiceStartResult.Fail(ex.Message);
            }
        }

        /// <summary>
        /// The PlayRecordingIntroAsync
        /// </summary>
        private async Task PlayRecordingIntroAsync(
            RTPSession mediaSession,
            AudioEncoder audioEncoder,
            AudioFormat selectedAudioFormat,
            RecordingPromptOptions options) {

            AudioFormat audioFormat = selectedAudioFormat;
            if (string.IsNullOrWhiteSpace(audioFormat.FormatName)) {
                audioFormat = new AudioFormat(SDPWellKnownMediaFormatsEnum.PCMA);
            }

            SIP_SystemSpeech speech = new SIP_SystemSpeech(_logger);
            using MemoryStream introStream = speech.SynthesizeTextToRawPcmStream(
                options.IntroText,
                options.VoiceName,
                options.SampleRate,
                options.Volume,
                options.Rate);

            await SendPcmStreamAsync(mediaSession, audioEncoder, audioFormat, introStream, options.SampleRate);

            foreach (string number in options.Countdown) {
                using MemoryStream countStream = speech.SynthesizeTextToRawPcmStream(
                    number,
                    options.VoiceName,
                    options.SampleRate,
                    options.Volume,
                    options.Rate);

                await SendPcmStreamAsync(mediaSession, audioEncoder, audioFormat, countStream, options.SampleRate);
                await Task.Delay(options.CountdownPauseMs);
            }

            byte[] beep = CreateSinePcm16(options.SampleRate, options.BeepFrequencyHz, options.BeepDurationMs, options.BeepVolume);
            await SendPcmBytesAsync(mediaSession, audioEncoder, audioFormat, beep, options.SampleRate);
        }

        /// <summary>
        /// The SendPcmStreamAsync
        /// </summary>
        private static async Task SendPcmStreamAsync(
            RTPSession mediaSession,
            AudioEncoder audioEncoder,
            AudioFormat audioFormat,
            MemoryStream pcmStream,
            int sampleRate) {

            if (pcmStream == null) {
                return;
            }

            await SendPcmBytesAsync(mediaSession, audioEncoder, audioFormat, pcmStream.ToArray(), sampleRate);
        }

        /// <summary>
        /// The SendPcmBytesAsync
        /// </summary>
        private static async Task SendPcmBytesAsync(
            RTPSession mediaSession,
            AudioEncoder audioEncoder,
            AudioFormat audioFormat,
            byte[] pcmBytes,
            int sampleRate) {

            if (pcmBytes == null || pcmBytes.Length == 0) {
                return;
            }

            int bytesPerSample = sizeof(short);
            int frameSamples = Math.Max(sampleRate / 50, 1);
            int frameBytes = frameSamples * bytesPerSample;
            int offset = 0;

            while (offset < pcmBytes.Length) {
                int length = Math.Min(frameBytes, pcmBytes.Length - offset);
                short[] samples = new short[frameSamples];
                Buffer.BlockCopy(pcmBytes, offset, samples, 0, length);

                byte[] encoded = audioEncoder.EncodeAudio(samples, audioFormat);
                uint durationRtpUnits = (uint)Math.Max(1, frameSamples * audioFormat.RtpClockRate / sampleRate);
                mediaSession.SendAudio(durationRtpUnits, encoded);

                offset += length;
                await Task.Delay(20);
            }
        }

        /// <summary>
        /// The CreateSinePcm16
        /// </summary>
        private static byte[] CreateSinePcm16(int sampleRate, int frequencyHz, int durationMs, double volume) {
            int samples = Math.Max(sampleRate * durationMs / 1000, 1);
            byte[] pcm = new byte[samples * sizeof(short)];
            double amplitude = short.MaxValue * Math.Clamp(volume, 0.0, 1.0);

            for (int i = 0; i < samples; i++) {
                double angle = 2.0 * Math.PI * frequencyHz * i / sampleRate;
                short sample = (short)(Math.Sin(angle) * amplitude);
                pcm[i * 2] = (byte)(sample & 0xff);
                pcm[(i * 2) + 1] = (byte)((sample >> 8) & 0xff);
            }

            return pcm;
        }

        /// <summary>
        /// The RegisterRtpDtmfHandler
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="mediaSession">The mediaSession<see cref="IMediaSession"/></param>
        private void RegisterRtpDtmfHandler(SIP_ServerSession session, IMediaSession mediaSession) {
            mediaSession.OnRtpEvent += (remoteEndPoint, rtpEvent, rtpHeader) => {
                HandleRtpDtmfEvent(session, remoteEndPoint, rtpEvent, rtpHeader);
            };
        }

        /// <summary>
        /// The HandleRtpDtmfEvent
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="remoteEndPoint">The remoteEndPoint<see cref="IPEndPoint"/></param>
        /// <param name="rtpEvent">The rtpEvent<see cref="RTPEvent"/></param>
        /// <param name="rtpHeader">The rtpHeader<see cref="RTPHeader"/></param>
        private void HandleRtpDtmfEvent(SIP_ServerSession session, IPEndPoint remoteEndPoint, RTPEvent rtpEvent, RTPHeader rtpHeader) {
            if (session == null || rtpEvent == null || rtpHeader == null || !rtpEvent.EndOfEvent) {
                return;
            }

            if (IsSessionClosed(session)) {
                return;
            }

            if (!TryConvertRtpEventIdToDigit(rtpEvent.EventID, out char digit)) {
                _logger?.Warn($"DTMF RTP-Event ignoriert | SessionID={session.SessionId} | EventID={rtpEvent.EventID} | Remote={remoteEndPoint}");
                return;
            }

            string dedupKey = $"{session.SessionId}|{rtpHeader.Timestamp}|{rtpEvent.EventID}";
            DateTime now = DateTime.UtcNow;
            if (_rtpDtmfDedup.TryGetValue(dedupKey, out DateTime seenUtc) && now - seenUtc < TimeSpan.FromSeconds(2)) {
                return;
            }

            _rtpDtmfDedup[dedupKey] = now;
            CleanupRtpDtmfDedup(now);

            TKBase_DTMF_Result result = _dtmfManager.PushDigit(session.SessionId, session.LegA.CallId, "LegA", "RTP-EVENT", digit);
            if (_menuDtmfHandler == null) {
                return;
            }

            if (result.Completed) {
                _ = Task.Run(async () => {
                    try {
                        await _menuDtmfHandler(session, result.Sequence);
                    } catch (Exception ex) {
                        _logger?.Error($"Service-Menue RTP-DTMF Verarbeitung fehlgeschlagen | SessionID={session.SessionId} | Sequence={result.Sequence} | {ex}");
                    }
                });
                return;
            }

            if (result.Cancelled && digit == '#') {
                _ = Task.Run(async () => {
                    try {
                        await _menuDtmfHandler(session, "#");
                    } catch (Exception ex) {
                        _logger?.Error($"Service-DTMF Stop Verarbeitung fehlgeschlagen | SessionID={session.SessionId} | Source=RTP-EVENT | {ex}");
                    }
                });
            }
        }

        /// <summary>
        /// The CleanupRtpDtmfDedup
        /// </summary>
        /// <param name="now">The now<see cref="DateTime"/></param>
        private void CleanupRtpDtmfDedup(DateTime now) {
            foreach (KeyValuePair<string, DateTime> entry in _rtpDtmfDedup) {
                if (now - entry.Value > TimeSpan.FromMinutes(2)) {
                    _rtpDtmfDedup.TryRemove(entry.Key, out _);
                }
            }
        }

        /// <summary>
        /// The TryConvertRtpEventIdToDigit
        /// </summary>
        /// <param name="eventId">The eventId<see cref="byte"/></param>
        /// <param name="digit">The digit<see cref="char"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool TryConvertRtpEventIdToDigit(byte eventId, out char digit) {
            if (eventId <= 9) {
                digit = (char)('0' + eventId);
                return true;
            }

            if (eventId == 10) {
                digit = '*';
                return true;
            }

            if (eventId == 11) {
                digit = '#';
                return true;
            }

            if (eventId >= 12 && eventId <= 15) {
                digit = (char)('A' + eventId - 12);
                return true;
            }

            digit = '\0';
            return false;
        }

        /// <summary>
        /// The GetMenuEntries
        /// </summary>
        /// <param name="serviceLeg">The serviceLeg<see cref="TKSVC_Session"/></param>
        /// <returns>The <see cref="IReadOnlyList{TKSVC_Session}"/></returns>
        private IReadOnlyList<TKSVC_Session> GetMenuEntries(TKSVC_Session serviceLeg) {
            string menuRoot = TKSVC_Routing.NormalizeServiceNumber(serviceLeg.InternalNumber);
            if (string.IsNullOrWhiteSpace(menuRoot) || menuRoot.Length != 4) {
                return Array.Empty<TKSVC_Session>();
            }

            string menuPrefix = menuRoot + "*";
            return _serviceRegistry.GetAll()
                .Where(x => {
                    string number = TKSVC_Routing.NormalizeServiceNumber(x.InternalNumber);
                    return !string.Equals(number, menuRoot, StringComparison.OrdinalIgnoreCase)
                        && number.StartsWith(menuPrefix, StringComparison.OrdinalIgnoreCase)
                        && number.EndsWith("#", StringComparison.Ordinal);
                })
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.InternalNumber, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// The OnPlaybackCompleted
        /// </summary>
        /// <param name="runtime">The runtime<see cref="SIP_ServiceRuntime"/></param>
        /// <param name="playbackStream">The playbackStream<see cref="MemoryStream?"/></param>
        /// <param name="aggregateException">The aggregateException<see cref="AggregateException?"/></param>
        private void OnPlaybackCompleted(SIP_ServiceRuntime runtime, MemoryStream? playbackStream, AggregateException? aggregateException) {
            bool isCurrentPlayback = playbackStream == null || ReferenceEquals(runtime.PlaybackStream, playbackStream);
            if (!isCurrentPlayback) {
                _logger?.Info($"Service-Playback Abschluss veraltet ignoriert | SessionID={runtime.SessionId} | Service={runtime.ServiceLeg.InternalNumber}");
                return;
            }

            if (aggregateException != null) {
                _logger?.Warn($"Service-Playback beendet mit Fehler | SessionID={runtime.SessionId} | {aggregateException.Flatten().InnerException?.Message}");
            } else {
                _logger?.Info($"Service-Playback abgeschlossen | SessionID={runtime.SessionId} | Service={runtime.ServiceLeg.InternalNumber}");
            }

            bool returnToMainMenu = runtime.ReturnToMainMenuAfterPlayback;
            runtime.ReturnToMainMenuAfterPlayback = false;
            runtime.DisposePlaybackStreamOnly(playbackStream);

            if (!runtime.KeepSessionUntilPeerEnded) {
                _logger?.Warn($"Service-Playback abgeschlossen | SessionID={runtime.SessionId} | Dialog bleibt offen, da Auto-BYE noch nicht implementiert ist.");
            }

            if (returnToMainMenu) {
                _ = Task.Run(() => ForceReturnToMainMenuAfterPlaybackAsync(runtime));
            }
        }

        /// <summary>
        /// The PlayTimeSpeechOnRuntimeAsync
        /// </summary>
        private async Task<bool> PlayTimeSpeechOnRuntimeAsync(SIP_ServiceRuntime runtime, TKSVC_Session targetServiceLeg) {
            try {
                TimeSpeechOptions options = TimeSpeechOptions.FromServiceLeg(targetServiceLeg, _logger);
                SIP_SystemSpeech speech = new SIP_SystemSpeech(_logger);
                MemoryStream pcmStream = speech.SynthesizeCurrentTimeToRawPcmStream(
                    voiceName: options.VoiceName,
                    sampleRate: options.SampleRate,
                    volume: options.Volume,
                    rate: options.Rate,
                    includeSeconds: options.IncludeSeconds);

                AudioSamplingRatesEnum streamRate = options.SampleRate <= 8000
                    ? AudioSamplingRatesEnum.Rate8KHz
                    : AudioSamplingRatesEnum.Rate16KHz;

                await ReplacePlaybackAsync(runtime, targetServiceLeg, pcmStream, streamRate, true);
                _logger?.Info($"Service-Menue Auswahl gestartet | SessionID={runtime.SessionId} | Ziel={targetServiceLeg.InternalNumber} | Type={targetServiceLeg.ServiceType}");
                return true;
            } catch (Exception ex) {
                _logger?.Error($"Service-Menue TimeSpeech Auswahl fehlgeschlagen | SessionID={runtime.SessionId} | Ziel={targetServiceLeg.InternalNumber} | {ex}");
                return false;
            }
        }

        /// <summary>
        /// The PlayMenuPromptOnRuntimeAsync
        /// </summary>
        private async Task<bool> PlayMenuPromptOnRuntimeAsync(SIP_ServiceRuntime runtime, TKSVC_Session targetServiceLeg) {
            try {
                TKSVC_Menu menu = new TKSVC_Menu(_logger);
                IReadOnlyList<TKSVC_Session> menuEntries = GetMenuEntries(targetServiceLeg);
                TKSVC_MenuPrompt prompt = menu.CreatePrompt(targetServiceLeg, menuEntries);
                MemoryStream pcmStream = menu.SynthesizePromptToRawPcmStream(prompt);

                AudioSamplingRatesEnum streamRate = prompt.SampleRate <= 8000
                    ? AudioSamplingRatesEnum.Rate8KHz
                    : AudioSamplingRatesEnum.Rate16KHz;

                await ReplacePlaybackAsync(runtime, targetServiceLeg, pcmStream, streamRate);
                _logger?.Info($"Service-Menue Untermenue gestartet | SessionID={runtime.SessionId} | Ziel={targetServiceLeg.InternalNumber} | Type={targetServiceLeg.ServiceType}");
                return true;
            } catch (Exception ex) {
                _logger?.Error($"Service-Menue Untermenue fehlgeschlagen | SessionID={runtime.SessionId} | Ziel={targetServiceLeg.InternalNumber} | {ex}");
                return false;
            }
        }

        /// <summary>
        /// The PlayTextOnRuntimeAsync
        /// </summary>
        private async Task<bool> PlayTextOnRuntimeAsync(SIP_ServiceRuntime runtime, TKSVC_Session targetServiceLeg, string text, bool returnToMainMenuAfterPlayback = false) {
            try {
                SIP_SystemSpeech speech = new SIP_SystemSpeech(_logger);
                MemoryStream pcmStream = speech.SynthesizeTextToRawPcmStream(text, null, 16000, 100, 0);
                await ReplacePlaybackAsync(runtime, targetServiceLeg, pcmStream, AudioSamplingRatesEnum.Rate16KHz, returnToMainMenuAfterPlayback);
                _logger?.Warn($"Service-Menue Auswahl nicht implementiert | SessionID={runtime.SessionId} | Ziel={targetServiceLeg.InternalNumber} | Type={targetServiceLeg.ServiceType}");
                return true;
            } catch (Exception ex) {
                _logger?.Error($"Service-Menue Hinweisansage fehlgeschlagen | SessionID={runtime.SessionId} | Ziel={targetServiceLeg.InternalNumber} | {ex}");
                return false;
            }
        }

        private async Task<bool> PlayRecordingBeepOnRuntimeAsync(SIP_ServiceRuntime runtime, TKSVC_Session targetServiceLeg) {
            try {
                if (runtime.MediaSession is not RTPSession rtpSession) {
                    _logger?.Warn($"SVC Recording Pip-Ton nicht gesendet | SessionID={runtime.SessionId} | Grund=keine RTP-Session");
                    return false;
                }

                RecordingPromptOptions options = RecordingPromptOptions.FromServiceLeg(targetServiceLeg, _logger);
                AudioEncoder audioEncoder = new AudioEncoder();
                AudioFormat audioFormat = new AudioFormat(SDPWellKnownMediaFormatsEnum.PCMA);
                byte[] beep = CreateSinePcm16(options.SampleRate, options.BeepFrequencyHz, options.BeepDurationMs, options.BeepVolume);
                await SendPcmBytesAsync(rtpSession, audioEncoder, audioFormat, beep, options.SampleRate);
                await Task.Delay(120);
                _logger?.Info($"SVC Recording Pip-Ton gesendet | SessionID={runtime.SessionId} | Ziel={targetServiceLeg.InternalNumber} | DauerMs={options.BeepDurationMs} | FrequenzHz={options.BeepFrequencyHz}");
                return true;
            } catch (Exception ex) {
                _logger?.Warn($"SVC Recording Pip-Ton fehlgeschlagen | SessionID={runtime.SessionId} | Ziel={targetServiceLeg.InternalNumber} | {ex.Message}");
                return false;
            }
        }


        /// <summary>
        /// The StartRecordingOnRuntimeAsync
        /// </summary>
        private async Task<bool> StartRecordingOnRuntimeAsync(SIP_ServiceRuntime runtime, TKSVC_Session targetServiceLeg, TKSVC_RecordMode mode) {
            try {
                if (runtime.MediaSession is not RTPSession) {
                    _logger?.Warn($"Service-Menue Recording nicht gestartet | SessionID={runtime.SessionId} | Ziel={targetServiceLeg.InternalNumber} | Grund=aktive Media-Session empfaengt kein Audio");
                    return await PlayTextOnRuntimeAsync(runtime, targetServiceLeg, "Aufnahme braucht eine Recording-Media-Session.");
                }

                if (runtime.Recording != null && IsSameService(runtime.ServiceLeg, targetServiceLeg)) {
                    _logger?.Info($"Service-Menue Recording per Toggle beendet | SessionID={runtime.SessionId} | Ziel={targetServiceLeg.InternalNumber} | Mode={mode}");
                    StopSession(runtime.SessionId, "recording-toggle-stop");
                    return true;
                }

                runtime.ServiceLeg = targetServiceLeg;
                runtime.Recording?.Dispose();
                runtime.Recording = null;

                if (mode == TKSVC_RecordMode.MailboxPartner) {
                    await PlayTextOnRuntimeAsync(runtime, targetServiceLeg, "Mailboxansage aufnehmen. Sprechen Sie nach dem Signal. Beenden und speichern mit Raute.");
                    if (runtime.PlaybackTask != null) {
                        try {
                            await runtime.PlaybackTask;
                        } catch (Exception ex) {
                            _logger?.Warn($"Service-Menue Recording Hinweisansage beendet mit Fehler | SessionID={runtime.SessionId} | {ex.Message}");
                        }
                    }

                    await PlayRecordingBeepOnRuntimeAsync(runtime, targetServiceLeg);
                }

                SIP_ServerSession recordSession = runtime.CreateRecordingSession();
                runtime.Recording = mode == TKSVC_RecordMode.CallListener
                    ? _record.StartCallRecording(recordSession, targetServiceLeg)
                    : _record.StartMailboxRecording(recordSession, targetServiceLeg);

                RecordingPromptOptions recordingOptions = RecordingPromptOptions.FromServiceLeg(targetServiceLeg, _logger);
                if (mode == TKSVC_RecordMode.MailboxPartner) {
                    runtime.StartRecordingTimeout(
                        TimeSpan.FromSeconds(recordingOptions.RecordingTimeoutSec),
                        async () => {
                            if (!_runtimes.TryGetValue(runtime.SessionId, out SIP_ServiceRuntime? activeRuntime) || !ReferenceEquals(activeRuntime, runtime) || activeRuntime.Recording == null) {
                                return;
                            }

                            _logger?.Info($"SVC Recording Timeout erreicht | SessionID={runtime.SessionId} | TimeoutSec={recordingOptions.RecordingTimeoutSec}");
                            await StopActiveRecordingAndReturnToMainMenuAsync(runtime.SourceSession, "recording-timeout");
                        });
                }

                _logger?.Info($"Service-Menue Recording gestartet | SessionID={runtime.SessionId} | Ziel={targetServiceLeg.InternalNumber} | Mode={mode} | TimeoutSec={(mode == TKSVC_RecordMode.MailboxPartner ? recordingOptions.RecordingTimeoutSec.ToString() : "-")}");

                if (mode == TKSVC_RecordMode.CallListener) {
                    await PlayTextOnRuntimeAsync(runtime, targetServiceLeg, "Aufnahme gestartet.");
                }

                return true;
            } catch (Exception ex) {
                _logger?.Error($"Service-Menue Recording Auswahl fehlgeschlagen | SessionID={runtime.SessionId} | Ziel={targetServiceLeg.InternalNumber} | {ex}");
                return false;
            }
        }


        private async Task<bool> PlayMailboxGreetingOnRuntimeAsync(SIP_ServiceRuntime runtime, TKSVC_Session targetServiceLeg) {
            try {
                int? userId = ResolveRuntimeUserId(runtime);
                if (!userId.HasValue || userId.Value <= 0) {
                    await PlayTextOnRuntimeAsync(runtime, targetServiceLeg, "Es konnte kein Benutzer fuer die Mailboxansage ermittelt werden.", true);
                    return true;
                }

                byte[]? wavBytes = new TKSQL_Commands(_logger).LoadMailboxGreetingBlob(userId.Value);
                if (wavBytes == null || wavBytes.Length <= 44) {
                    await PlayTextOnRuntimeAsync(runtime, targetServiceLeg, "Es ist noch keine Mailboxansage gespeichert.", true);
                    return true;
                }

                MemoryStream pcmStream = ExtractPcmFromWav(wavBytes, out int sampleRate);
                AudioSamplingRatesEnum streamRate = sampleRate <= 8000
                    ? AudioSamplingRatesEnum.Rate8KHz
                    : AudioSamplingRatesEnum.Rate16KHz;

                await ReplacePlaybackAsync(runtime, targetServiceLeg, pcmStream, streamRate, true);
                _logger?.Info($"Mailboxansage Wiedergabe gestartet | SessionID={runtime.SessionId} | UserID={userId.Value} | Bytes={wavBytes.Length}");
                return true;
            } catch (Exception ex) {
                _logger?.Error($"Mailboxansage Wiedergabe fehlgeschlagen | SessionID={runtime.SessionId} | Ziel={targetServiceLeg.InternalNumber} | {ex}");
                await PlayTextOnRuntimeAsync(runtime, targetServiceLeg, "Die Mailboxansage konnte nicht abgespielt werden.", true);
                return true;
            }
        }

        private async Task<bool> PlayMainMenuOnRuntimeAsync(SIP_ServerSession session, SIP_ServiceRuntime runtime, string reason) {
            if (!_serviceRegistry.TryGetByServiceCode(SIP_ServiceCodes.MainMenu, out TKSVC_Session? mainMenu) || mainMenu == null) {
                _logger?.Warn($"SVC Ruecksprung Hauptmenue fehlgeschlagen | SessionID={session.SessionId} | Grund=MAIN_MENU nicht gefunden | Anlass={reason}");
                return false;
            }

            lock (session.SyncRoot) {
                session.LegD.ServiceLeg = mainMenu;
                session.LegD.InternalNumber = mainMenu.InternalNumber;
                session.LegD.ServiceType = mainMenu.ServiceType;
                session.LegD.ServiceCode = mainMenu.ServiceCode;
                session.UpdatedAt = DateTime.Now;
            }

            _logger?.Info($"SVC Ruecksprung Hauptmenue | SessionID={session.SessionId} | Anlass={reason}");
            return await PlayMenuPromptOnRuntimeAsync(runtime, mainMenu);
        }

        private async Task ReturnToMainMenuAfterPlaybackAsync(SIP_ServiceRuntime runtime) {
            try {
                if (!runtime.ReturnToMainMenuAfterPlayback) {
                    return;
                }

                runtime.ReturnToMainMenuAfterPlayback = false;
                if (!_serviceRegistry.TryGetByServiceCode(SIP_ServiceCodes.MainMenu, out TKSVC_Session? mainMenu) || mainMenu == null) {
                    _logger?.Warn($"SVC Auto-Ruecksprung Hauptmenue fehlgeschlagen | SessionID={runtime.SessionId} | Grund=MAIN_MENU nicht gefunden");
                    return;
                }

                lock (runtime.SourceSession.SyncRoot) {
                    runtime.SourceSession.LegD.ServiceLeg = mainMenu;
                    runtime.SourceSession.LegD.InternalNumber = mainMenu.InternalNumber;
                    runtime.SourceSession.LegD.ServiceType = mainMenu.ServiceType;
                    runtime.SourceSession.LegD.ServiceCode = mainMenu.ServiceCode;
                    runtime.SourceSession.UpdatedAt = DateTime.Now;
                }

                await PlayMenuPromptOnRuntimeAsync(runtime, mainMenu);
                _logger?.Info($"SVC Auto-Ruecksprung Hauptmenue ausgefuehrt | SessionID={runtime.SessionId}");
            } catch (Exception ex) {
                _logger?.Error($"SVC Auto-Ruecksprung Hauptmenue fehlgeschlagen | SessionID={runtime.SessionId} | {ex}");
            }
        }


        private async Task ForceReturnToMainMenuAfterPlaybackAsync(SIP_ServiceRuntime runtime) {
            try {
                runtime.ReturnToMainMenuAfterPlayback = false;
                if (!_serviceRegistry.TryGetByServiceCode(SIP_ServiceCodes.MainMenu, out TKSVC_Session? mainMenu) || mainMenu == null) {
                    _logger?.Warn($"SVC Auto-Ruecksprung Hauptmenue fehlgeschlagen | SessionID={runtime.SessionId} | Grund=MAIN_MENU nicht gefunden");
                    return;
                }

                lock (runtime.SourceSession.SyncRoot) {
                    if (runtime.SourceSession.IsTerminated || runtime.SourceSession.LegD.IsEnded) {
                        _logger?.Info($"SVC Auto-Ruecksprung Hauptmenue ignoriert | SessionID={runtime.SessionId} | Grund=LegD beendet");
                        return;
                    }

                    runtime.SourceSession.LegD.ServiceLeg = mainMenu;
                    runtime.SourceSession.LegD.InternalNumber = mainMenu.InternalNumber;
                    runtime.SourceSession.LegD.ServiceType = mainMenu.ServiceType;
                    runtime.SourceSession.LegD.ServiceCode = mainMenu.ServiceCode;
                    runtime.SourceSession.UpdatedAt = DateTime.Now;
                }

                await PlayMenuPromptOnRuntimeAsync(runtime, mainMenu);
                _logger?.Info($"SVC Auto-Ruecksprung Hauptmenue ausgefuehrt | SessionID={runtime.SessionId}");
            } catch (Exception ex) {
                _logger?.Error($"SVC Auto-Ruecksprung Hauptmenue fehlgeschlagen | SessionID={runtime.SessionId} | {ex}");
            }
        }

        private int? ResolveRuntimeUserId(SIP_ServiceRuntime runtime) {
            if (runtime.TargetUserId.HasValue && runtime.TargetUserId.Value > 0) {
                return runtime.TargetUserId.Value;
            }

            try {
                TKSQL_Commands sql = new TKSQL_Commands(_logger);
                string[] candidates = new[] {
                    runtime.FromNumber,
                    runtime.FromUsername ?? string.Empty,
                    runtime.ToUsername ?? string.Empty,
                    runtime.ToNumber
                };

                foreach (string candidate in candidates) {
                    string value = candidate?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(value)) {
                        continue;
                    }

                    int? id = TryReadUserId(sql.GetSipUserByNumber(value));
                    if (id.HasValue) {
                        return id.Value;
                    }

                    id = TryReadUserId(sql.GetSipUserByUsername(value));
                    if (id.HasValue) {
                        return id.Value;
                    }
                }
            } catch (Exception ex) {
                _logger?.Warn($"SVC Benutzer fuer Mailboxansage konnte nicht ermittelt werden | SessionID={runtime.SessionId} | Fehler={ex.Message}");
            }

            return null;
        }

        private static int? TryReadUserId(System.Data.DataRow? row) {
            if (row == null || !row.Table.Columns.Contains("ID") || row["ID"] == DBNull.Value) {
                return null;
            }

            int id = Convert.ToInt32(row["ID"]);
            return id > 0 ? id : null;
        }

        private static MemoryStream ExtractPcmFromWav(byte[] wavBytes, out int sampleRate) {
            sampleRate = 8000;
            if (wavBytes == null || wavBytes.Length <= 44) {
                return new MemoryStream(Array.Empty<byte>());
            }

            if (wavBytes.Length >= 28
                && wavBytes[0] == (byte)'R'
                && wavBytes[1] == (byte)'I'
                && wavBytes[2] == (byte)'F'
                && wavBytes[3] == (byte)'F') {
                sampleRate = BitConverter.ToInt32(wavBytes, 24);
            }

            int dataOffset = 44;
            int dataLength = wavBytes.Length - dataOffset;
            for (int i = 12; i <= wavBytes.Length - 8; i++) {
                if (wavBytes[i] == (byte)'d'
                    && wavBytes[i + 1] == (byte)'a'
                    && wavBytes[i + 2] == (byte)'t'
                    && wavBytes[i + 3] == (byte)'a') {
                    int declaredLength = BitConverter.ToInt32(wavBytes, i + 4);
                    dataOffset = i + 8;
                    dataLength = Math.Min(Math.Max(declaredLength, 0), wavBytes.Length - dataOffset);
                    break;
                }
            }

            if (dataLength <= 0 || dataOffset >= wavBytes.Length) {
                return new MemoryStream(Array.Empty<byte>());
            }

            byte[] pcm = new byte[dataLength];
            Buffer.BlockCopy(wavBytes, dataOffset, pcm, 0, dataLength);
            return new MemoryStream(pcm);
        }


        private static bool IsSameService(TKSVC_Session? left, TKSVC_Session? right) {
            if (left == null || right == null) {
                return false;
            }

            return string.Equals(TKSVC_Routing.NormalizeServiceNumber(left.InternalNumber), TKSVC_Routing.NormalizeServiceNumber(right.InternalNumber), StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(left.ServiceCode) && string.Equals(left.ServiceCode, right.ServiceCode, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// The ReplacePlaybackAsync
        /// </summary>
        private async Task ReplacePlaybackAsync(SIP_ServiceRuntime runtime, TKSVC_Session targetServiceLeg, MemoryStream pcmStream, AudioSamplingRatesEnum streamRate, bool returnToMainMenuAfterPlayback = false) {
            AudioExtrasSource audioSource = runtime.MediaSession switch {
                AudioSendOnlyMediaSession sendOnlyMediaSession => sendOnlyMediaSession.AudioExtrasSource,
                ServiceMediaSession serviceMediaSession => serviceMediaSession.AudioExtrasSource,
                _ => throw new InvalidOperationException("Playback ist fuer diese Service-Media-Session nicht verfuegbar.")
            };

            runtime.CancelPlayback();
            if (pcmStream.CanSeek) {
                pcmStream.Position = 0;
            }

            runtime.ServiceLeg = targetServiceLeg;
            runtime.ReplacePlaybackStream(pcmStream);
            runtime.ReturnToMainMenuAfterPlayback = returnToMainMenuAfterPlayback;
            runtime.PlaybackTask = audioSource.SendAudioFromStream(pcmStream, streamRate);
            _ = runtime.PlaybackTask.ContinueWith(
                t => OnPlaybackCompleted(runtime, pcmStream, t.Exception),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Defines the <see cref="SIP_ServiceRuntime" />
        /// </summary>
        private sealed class SIP_ServiceRuntime {
            /// <summary>
            /// Defines the _portAllocator
            /// </summary>
            private readonly TKBase_MediaManager _portAllocator;

            /// <summary>
            /// Defines the _logger
            /// </summary>
            private readonly TKBase_LogManager? _logger;

            /// <summary>
            /// Defines the _disposed
            /// </summary>
            private int _disposed;

            /// <summary>
            /// Initializes a new instance of the <see cref="SIP_ServiceRuntime"/> class.
            /// </summary>
            /// <param name="sourceSession">The sourceSession<see cref="SIP_ServerSession"/></param>
            /// <param name="serviceLeg">The serviceLeg<see cref="TKSVC_Session"/></param>
            /// <param name="mediaSession">The mediaSession<see cref="IMediaSession"/></param>
            /// <param name="playbackStream">The playbackStream<see cref="MemoryStream"/></param>
            /// <param name="localRtpPort">The localRtpPort<see cref="int"/></param>
            /// <param name="keepSessionUntilPeerEnded">The keepSessionUntilPeerEnded<see cref="bool"/></param>
            /// <param name="portAllocator">The portAllocator<see cref="TKBase_MediaManager"/></param>
            /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
            public SIP_ServiceRuntime(
                SIP_ServerSession sourceSession,
                TKSVC_Session serviceLeg,
                IMediaSession mediaSession,
                MemoryStream? playbackStream,
                int localRtpPort,
                bool keepSessionUntilPeerEnded,
                TKBase_MediaManager portAllocator,
                TKBase_LogManager? logger) {

                ArgumentNullException.ThrowIfNull(sourceSession);
                SourceSession = sourceSession;
                SessionId = sourceSession.SessionId;
                FromNumber = sourceSession.FromNumber;
                ToNumber = sourceSession.ToNumber;
                FromUsername = sourceSession.FromUsername;
                ToUsername = sourceSession.ToUsername;
                TargetUserId = sourceSession.TargetUserId;
                LegACallId = sourceSession.LegA.CallId;
                ServiceLeg = serviceLeg;
                MediaSession = mediaSession;
                PlaybackStream = playbackStream;
                LocalRtpPort = localRtpPort;
                KeepSessionUntilPeerEnded = keepSessionUntilPeerEnded;
                _portAllocator = portAllocator;
                _logger = logger;
            }

            /// <summary>
            /// Gets the SessionId
            /// </summary>
            public SIP_ServerSession SourceSession { get; }

            public string SessionId { get; }

            public string FromNumber { get; }

            public string ToNumber { get; }

            public string? FromUsername { get; }

            public string? ToUsername { get; }

            public int? TargetUserId { get; }

            public string? LegACallId { get; }

            public SIP_ServerSession CreateRecordingSession() {
                return new SIP_ServerSession {
                    SessionId = SessionId,
                    FromNumber = FromNumber,
                    ToNumber = ToNumber,
                    FromUsername = FromUsername,
                    ToUsername = ToUsername,
                    TargetUserId = TargetUserId,
                    LegACallId = LegACallId ?? string.Empty,
                    IsServiceSession = true
                };
            }

            /// <summary>
            /// Gets the ServiceLeg
            /// </summary>
            public TKSVC_Session ServiceLeg { get; set; }

            /// <summary>
            /// Gets the MediaSession
            /// </summary>
            public IMediaSession MediaSession { get; }

            /// <summary>
            /// Gets the PlaybackStream
            /// </summary>
            public MemoryStream? PlaybackStream { get; private set; }

            /// <summary>
            /// Gets the LocalRtpPort
            /// </summary>
            public int LocalRtpPort { get; }

            /// <summary>
            /// Gets a value indicating whether KeepSessionUntilPeerEnded
            /// </summary>
            public bool KeepSessionUntilPeerEnded { get; }

            /// <summary>
            /// Gets or sets the PlaybackTask
            /// </summary>
            public Task? PlaybackTask { get; set; }

            /// <summary>
            /// Gets or sets the Recording
            /// </summary>
            public TKSVC_Record.TKSVC_RecordRuntime? Recording { get; set; }

            public bool ReturnToMainMenuAfterPlayback { get; set; }

            private readonly AudioEncoder _recordingAudioDecoder = new();

            private CancellationTokenSource? _recordingTimeoutCts;

            public void StartRecordingTimeout(TimeSpan timeout, Func<Task> onTimeout) {
                CancelRecordingTimeout();
                if (timeout <= TimeSpan.Zero || onTimeout == null) {
                    return;
                }

                CancellationTokenSource cts = new CancellationTokenSource();
                _recordingTimeoutCts = cts;
                _ = Task.Run(async () => {
                    try {
                        await Task.Delay(timeout, cts.Token).ConfigureAwait(false);
                        if (!cts.IsCancellationRequested) {
                            await onTimeout().ConfigureAwait(false);
                        }
                    } catch (TaskCanceledException) {
                    } catch (Exception ex) {
                        _logger?.Warn($"SVC Recording Timeout Warnung | SessionID={SessionId} | {ex.Message}");
                    } finally {
                        try {
                            cts.Dispose();
                        } catch {
                        }
                    }
                });
            }

            public void CancelRecordingTimeout() {
                CancellationTokenSource? cts = Interlocked.Exchange(ref _recordingTimeoutCts, null);
                if (cts == null) {
                    return;
                }

                try {
                    cts.Cancel();
                } catch {
                }
            }

            public bool StopRecordingOnly(string reason, out string? serviceNumber, out bool hadAnyAudio, out bool hadUsableAudio, out long bytesWritten, out int? durationMs) {
                serviceNumber = Recording?.InternalNumber;
                hadAnyAudio = false;
                hadUsableAudio = false;
                bytesWritten = 0;
                durationMs = null;

                TKSVC_Record.TKSVC_RecordRuntime? recording = Recording;
                if (recording == null) {
                    return false;
                }

                CancelRecordingTimeout();
                hadAnyAudio = recording.HasAnyAudio;
                hadUsableAudio = recording.HasUsableAudio;
                bytesWritten = recording.BytesWritten;
                durationMs = recording.DurationMs;

                Recording = null;
                try {
                    recording.Dispose();
                } catch (Exception ex) {
                    _logger?.Warn($"Service-Recording Stop Warnung | SessionID={SessionId} | Grund={reason} | {ex.Message}");
                }

                return true;
            }

            public void WriteInboundAudioFrame(EncodedAudioFrame frame) {
                TKSVC_Record.TKSVC_RecordRuntime? recording = Recording;
                if (recording == null) {
                    return;
                }

                try {
                    recording.WriteEncodedAudioFrame(frame, _recordingAudioDecoder);
                } catch (Exception ex) {
                    _logger?.Warn($"SVC LegD RecorderInput verworfen | SessionID={SessionId} | Fehler={ex.Message}");
                }
            }

            /// <summary>
            /// The DisposePlaybackStreamOnly
            /// </summary>
            public void DisposePlaybackStreamOnly(MemoryStream? expectedStream = null) {
                MemoryStream? stream = PlaybackStream;
                if (expectedStream != null && !ReferenceEquals(stream, expectedStream)) {
                    return;
                }

                PlaybackStream = null;

                if (stream == null) {
                    return;
                }

                try {
                    stream.Dispose();
                } catch (Exception ex) {
                    _logger?.Warn($"Service-PlaybackStream Dispose Warnung | SessionID={SessionId} | {ex.Message}");
                }
            }

            /// <summary>
            /// The CancelPlayback
            /// </summary>
            public void CancelPlayback() {
                try {
                    if (MediaSession is AudioSendOnlyMediaSession sendOnlyMediaSession) {
                        sendOnlyMediaSession.AudioExtrasSource.CancelSendAudioFromStream();
                    } else if (MediaSession is ServiceMediaSession serviceMediaSession) {
                        serviceMediaSession.AudioExtrasSource.CancelSendAudioFromStream();
                    }
                } catch {
                }
            }

            /// <summary>
            /// The ReplacePlaybackStream
            /// </summary>
            /// <param name="playbackStream">The playbackStream<see cref="MemoryStream"/></param>
            public void ReplacePlaybackStream(MemoryStream playbackStream) {
                DisposePlaybackStreamOnly();
                PlaybackStream = playbackStream;
            }

            /// <summary>
            /// The Dispose
            /// </summary>
            /// <param name="reason">The reason<see cref="string"/></param>
            public void Dispose(string reason) {
                if (Interlocked.Exchange(ref _disposed, 1) == 1) {
                    return;
                }

                try {
                    if (MediaSession is AudioSendOnlyMediaSession sendOnlyMediaSession) {
                        sendOnlyMediaSession.AudioExtrasSource.CancelSendAudioFromStream();
                    } else if (MediaSession is ServiceMediaSession serviceMediaSession) {
                        serviceMediaSession.AudioExtrasSource.CancelSendAudioFromStream();
                    }
                } catch {
                }

                DisposePlaybackStreamOnly();
                CancelRecordingTimeout();

                try {
                    Recording?.Dispose();
                    Recording = null;
                } catch (Exception ex) {
                    _logger?.Warn($"Service-Recording Dispose Warnung | SessionID={SessionId} | {ex.Message}");
                }

                try {
                    MediaSession.Close(reason);
                } catch (Exception ex) {
                    _logger?.Warn($"Service-Media Close Warnung | SessionID={SessionId} | {ex.Message}");
                }

                _portAllocator.Release(LocalRtpPort);
            }
        }

        /// <summary>
        /// Leg-D Media-Endpunkt fuer Servicegespraeche. Er kann RTP von Leg-A empfangen,
        /// Prompts/TTS zur Gegenstelle senden und bei Bedarf denselben Eingangsstrom an den
        /// Recorder weiterreichen. Dadurch muss SVC keinen GSM-Audiopfad verwenden.
        /// </summary>
        private sealed class ServiceMediaSession : RTPSession, IMediaSession {
            private readonly TKBase_LogManager? _logger;

            public AudioExtrasSource AudioExtrasSource { get; }

            public ServiceMediaSession(IPAddress bindAddress, int bindPort, MediaStreamStatusEnum streamStatus, TKBase_LogManager? logger)
                : base(false, false, false, bindAddress, bindPort, null) {
                _logger = logger;
                AudioEncoder encoder = new AudioEncoder();
                AudioExtrasSource = new AudioExtrasSource(encoder, new AudioSourceOptions { AudioSource = AudioSourcesEnum.None });
                AudioExtrasSource.OnAudioSourceEncodedSample += SendAudio;
                OnAudioFormatsNegotiated += AudioFormatsNegotiated;
                addTrack(new MediaStreamTrack(AudioExtrasSource.GetAudioSourceFormats(), streamStatus));
            }

            public override async Task Start() {
                if (!IsAudioStarted) {
                    await base.Start().ConfigureAwait(false);
                    await AudioExtrasSource.StartAudio().ConfigureAwait(false);
                }
            }

            public override void Close(string reason) {
                if (IsClosed) {
                    return;
                }

                try {
                    base.Close(reason);
                } catch (Exception ex) {
                    _logger?.Warn($"Service-Media Base-Close Warnung | Grund={reason} | {ex.Message}");
                }

                try {
                    AudioExtrasSource.OnAudioSourceEncodedSample -= SendAudio;
                } catch (Exception ex) {
                    _logger?.Warn($"Service-Media Event-Cleanup Warnung | Grund={reason} | {ex.Message}");
                }

                try {
                    AudioExtrasSource.CloseAudio().ConfigureAwait(false).GetAwaiter().GetResult();
                } catch (Exception ex) {
                    _logger?.Warn($"Service-Media Audio-Close Warnung | Grund={reason} | {ex.Message}");
                }
            }

            private void AudioFormatsNegotiated(List<AudioFormat> audioFormats) {
                if (audioFormats == null || audioFormats.Count == 0) {
                    return;
                }

                try {
                    AudioExtrasSource.SetAudioSourceFormat(audioFormats[0]);
                } catch (Exception ex) {
                    _logger?.Warn($"Service-Media Audioformat Warnung | {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Defines the <see cref="RecordingPromptOptions" />
        /// </summary>
        private sealed class RecordingPromptOptions {
            /// <summary>
            /// Gets the IntroText
            /// </summary>
            public string IntroText { get; private set; } = "Mailbox Ansage aufnehmen wurde gewaehlt. Die Aufnahme startet nach dem Piepton. Beenden mit Raute.";

            /// <summary>
            /// Gets the Countdown
            /// </summary>
            public IReadOnlyList<string> Countdown { get; private set; } = new[] { "3", "2", "1" };

            /// <summary>
            /// Gets the VoiceName
            /// </summary>
            public string? VoiceName { get; private set; }

            /// <summary>
            /// Gets the SampleRate
            /// </summary>
            public int SampleRate { get; private set; } = 8000;

            /// <summary>
            /// Gets the Volume
            /// </summary>
            public int Volume { get; private set; } = 100;

            /// <summary>
            /// Gets the Rate
            /// </summary>
            public int Rate { get; private set; }

            /// <summary>
            /// Gets the CountdownPauseMs
            /// </summary>
            public int CountdownPauseMs { get; private set; } = 250;

            /// <summary>
            /// Gets the BeepFrequencyHz
            /// </summary>
            public int BeepFrequencyHz { get; private set; } = 1000;

            /// <summary>
            /// Gets the BeepDurationMs
            /// </summary>
            public int BeepDurationMs { get; private set; } = 350;

            /// <summary>
            /// Gets the BeepVolume
            /// </summary>
            public double BeepVolume { get; private set; } = 0.35;

            public int RecordingTimeoutSec { get; private set; } = 15;

            /// <summary>
            /// The FromServiceLeg
            /// </summary>
            public static RecordingPromptOptions FromServiceLeg(TKSVC_Session serviceLeg, TKBase_LogManager? logger) {
                RecordingPromptOptions options = new RecordingPromptOptions();
                if (string.IsNullOrWhiteSpace(serviceLeg.JsonConfig)) {
                    return options;
                }

                try {
                    using JsonDocument doc = JsonDocument.Parse(serviceLeg.JsonConfig);
                    JsonElement root = doc.RootElement;

                    options.IntroText = ReadString(root, "recordIntroText")
                        ?? ReadString(root, "introText")
                        ?? options.IntroText;
                    options.VoiceName = ReadString(root, "voiceName")
                        ?? ReadString(root, "voice")
                        ?? options.VoiceName;
                    options.SampleRate = ReadInt(root, "sampleRate") ?? options.SampleRate;
                    options.Volume = ReadInt(root, "volume") ?? options.Volume;
                    options.Rate = ReadInt(root, "rate") ?? options.Rate;
                    options.CountdownPauseMs = ReadInt(root, "countdownPauseMs") ?? options.CountdownPauseMs;
                    options.BeepFrequencyHz = ReadInt(root, "beepFrequencyHz") ?? options.BeepFrequencyHz;
                    options.BeepDurationMs = ReadInt(root, "beepDurationMs") ?? options.BeepDurationMs;
                    options.BeepVolume = ReadDouble(root, "beepVolume") ?? options.BeepVolume;
                    options.RecordingTimeoutSec = ReadInt(root, "recordingTimeoutSec") ?? ReadInt(root, "timeoutSec") ?? options.RecordingTimeoutSec;
                } catch (Exception ex) {
                    logger?.Warn($"SVC Recording Prompt JsonConfig konnte nicht gelesen werden | InternalNumber={serviceLeg.InternalNumber} | {ex.Message}");
                }

                return options;
            }

            private static string? ReadString(JsonElement root, string name) {
                if (!TryGetProperty(root, name, out JsonElement value) || value.ValueKind != JsonValueKind.String) {
                    return null;
                }

                string? text = value.GetString();
                return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
            }

            private static int? ReadInt(JsonElement root, string name) {
                if (!TryGetProperty(root, name, out JsonElement value) || value.ValueKind != JsonValueKind.Number) {
                    return null;
                }

                return value.TryGetInt32(out int result) ? result : null;
            }

            private static double? ReadDouble(JsonElement root, string name) {
                if (!TryGetProperty(root, name, out JsonElement value) || value.ValueKind != JsonValueKind.Number) {
                    return null;
                }

                return value.TryGetDouble(out double result) ? result : null;
            }

            private static bool TryGetProperty(JsonElement root, string name, out JsonElement value) {
                if (root.TryGetProperty(name, out value)) {
                    return true;
                }

                string pascalName = char.ToUpperInvariant(name[0]) + name[1..];
                return root.TryGetProperty(pascalName, out value);
            }
        }

        /// <summary>
        /// Defines the <see cref="TimeSpeechOptions" />
        /// </summary>
        private sealed class TimeSpeechOptions {
            /// <summary>
            /// Gets the VoiceName
            /// </summary>
            public string? VoiceName { get; private set; }

            /// <summary>
            /// Gets the SampleRate
            /// </summary>
            public int SampleRate { get; private set; } = 16000;

            /// <summary>
            /// Gets the Volume
            /// </summary>
            public int Volume { get; private set; } = 100;

            /// <summary>
            /// Gets the Rate
            /// </summary>
            public int Rate { get; private set; }

            /// <summary>
            /// Gets a value indicating whether IncludeSeconds
            /// </summary>
            public bool IncludeSeconds { get; private set; }

            /// <summary>
            /// Gets a value indicating whether KeepSessionUntilPeerEnded
            /// </summary>
            public bool KeepSessionUntilPeerEnded { get; private set; } = true;

            /// <summary>
            /// The FromServiceLeg
            /// </summary>
            /// <param name="serviceLeg">The serviceLeg<see cref="TKSVC_Session"/></param>
            /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
            /// <returns>The <see cref="TimeSpeechOptions"/></returns>
            public static TimeSpeechOptions FromServiceLeg(TKSVC_Session serviceLeg, TKBase_LogManager? logger) {
                TimeSpeechOptions options = new TimeSpeechOptions {
                    KeepSessionUntilPeerEnded = serviceLeg.KeepSessionUntilPeerEnded,
                    VoiceName = null
                };

                if (string.IsNullOrWhiteSpace(serviceLeg.JsonConfig)) {
                    return options;
                }

                try {
                    using JsonDocument doc = JsonDocument.Parse(serviceLeg.JsonConfig);
                    JsonElement root = doc.RootElement;

                    options.VoiceName = ReadString(root, "voiceName")
                        ?? ReadString(root, "voice")
                        ?? options.VoiceName;
                    options.SampleRate = ReadInt(root, "sampleRate") ?? options.SampleRate;
                    options.Volume = ReadInt(root, "volume") ?? options.Volume;
                    options.Rate = ReadInt(root, "rate") ?? options.Rate;
                    options.IncludeSeconds = ReadBool(root, "includeSeconds") ?? options.IncludeSeconds;
                    options.KeepSessionUntilPeerEnded = ReadBool(root, "keepSessionUntilPeerEnded") ?? options.KeepSessionUntilPeerEnded;
                } catch (Exception ex) {
                    logger?.Warn($"Service JsonConfig konnte nicht gelesen werden | InternalNumber={serviceLeg.InternalNumber} | {ex.Message}");
                }

                options.SampleRate = options.SampleRate <= 8000 ? 8000 : 16000;
                options.Volume = Math.Clamp(options.Volume, 0, 100);
                options.Rate = Math.Clamp(options.Rate, -10, 10);
                return options;
            }

            /// <summary>
            /// The ReadString
            /// </summary>
            /// <param name="root">The root<see cref="JsonElement"/></param>
            /// <param name="propertyName">The propertyName<see cref="string"/></param>
            /// <returns>The <see cref="string?"/></returns>
            private static string? ReadString(JsonElement root, string propertyName) {
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(propertyName, out JsonElement value)) {
                    return null;
                }

                return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            }

            /// <summary>
            /// The ReadInt
            /// </summary>
            /// <param name="root">The root<see cref="JsonElement"/></param>
            /// <param name="propertyName">The propertyName<see cref="string"/></param>
            /// <returns>The <see cref="int?"/></returns>
            private static int? ReadInt(JsonElement root, string propertyName) {
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(propertyName, out JsonElement value)) {
                    return null;
                }

                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int parsed)) {
                    return parsed;
                }

                if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out parsed)) {
                    return parsed;
                }

                return null;
            }

            /// <summary>
            /// The ReadBool
            /// </summary>
            /// <param name="root">The root<see cref="JsonElement"/></param>
            /// <param name="propertyName">The propertyName<see cref="string"/></param>
            /// <returns>The <see cref="bool?"/></returns>
            private static bool? ReadBool(JsonElement root, string propertyName) {
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(propertyName, out JsonElement value)) {
                    return null;
                }

                if (value.ValueKind == JsonValueKind.True) {
                    return true;
                }

                if (value.ValueKind == JsonValueKind.False) {
                    return false;
                }

                if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out bool parsed)) {
                    return parsed;
                }

                return null;
            }
        }
    }

    /// <summary>
    /// Defines the <see cref="SIP_ServiceStartResult" />
    /// </summary>
    internal sealed class SIP_ServiceStartResult {
        /// <summary>
        /// Gets a value indicating whether Success
        /// </summary>
        public bool Success { get; private set; }

        /// <summary>
        /// Gets the Reason
        /// </summary>
        public string? Reason { get; private set; }

        /// <summary>
        /// Gets the AnswerSdp
        /// </summary>
        public string? AnswerSdp { get; private set; }

        /// <summary>
        /// Gets the LocalRtpPort
        /// </summary>
        public int LocalRtpPort { get; private set; }

        /// <summary>
        /// The Ok
        /// </summary>
        /// <param name="answerSdp">The answerSdp<see cref="string"/></param>
        /// <param name="localRtpPort">The localRtpPort<see cref="int"/></param>
        /// <returns>The <see cref="SIP_ServiceStartResult"/></returns>
        public static SIP_ServiceStartResult Ok(string answerSdp, int localRtpPort) {
            return new SIP_ServiceStartResult {
                Success = true,
                AnswerSdp = answerSdp,
                LocalRtpPort = localRtpPort
            };
        }

        /// <summary>
        /// The Fail
        /// </summary>
        /// <param name="reason">The reason<see cref="string"/></param>
        /// <returns>The <see cref="SIP_ServiceStartResult"/></returns>
        public static SIP_ServiceStartResult Fail(string reason) {
            return new SIP_ServiceStartResult {
                Success = false,
                Reason = reason
            };
        }
    }
}
