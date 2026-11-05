/*
 * TKSipInt_Media
 * Aufgabe:
 * - Enthält die komplette SipInt-RTP/SDP-Audioverarbeitung für Leg A und Leg B.
 * - Prüft Caller-/Callee-SDP, reserviert Relay-Ports, schreibt Offer/Answer-SDP um.
 * - Startet und stoppt den RTP-Audiostream über TKBase_MediaManager.
 * - Verwendet keinen eigenen Port-/Relay-Allocator; der Ton läuft explizit über den Base-MediaManager.
 *
 * Nicht Aufgabe:
 * - Kein Routing.
 * - Keine REGISTER-Logik.
 * - Keine GSM-Audio-Bridge.
 * - Keine Provider-Media-Sonderlogik.
 */

namespace TKServer.SipInt {
    using SIPSorcery.SIP;
    using System;
    using System.Net;

    using TKServer.BASE;
    /// TKSipInt_Media
    /// Aufgabe:
    /// - Enthält die komplette SipInt-RTP/SDP-Audioverarbeitung für Leg A und Leg B.
    /// - Prüft Caller-/Callee-SDP, reserviert Relay-Ports, schreibt Offer/Answer-SDP um.
    /// - Startet und stoppt den RTP-Audiostream über TKBase_MediaManager.
    /// - Verwendet keinen eigenen Port-/Relay-Allocator; der Ton läuft explizit über den Base-MediaManager.
    /// 
    /// Nicht Aufgabe:
    /// - Kein Routing.
    /// - Keine REGISTER-Logik.
    /// - Keine GSM-Audio-Bridge.
    /// - Keine Provider-Media-Sonderlogik.

    /// <summary>
    /// Defines the <see cref="TKSipInt_Media" />
    /// </summary>
    internal sealed class TKSipInt_Media {
        /// <summary>
        /// Defines the _logger
        /// </summary>
        private readonly TKBase_LogManager? _logger;

        /// <summary>
        /// Defines the _mediaManager
        /// </summary>
        private readonly TKBase_MediaManager _mediaManager;

        /// <summary>
        /// Defines the _rtpPacketHandler
        /// </summary>
        private readonly Action<SIP_ServerSession, string, byte[]>? _rtpPacketHandler;

        /// <summary>
        /// Initializes a new instance of the <see cref="TKSipInt_Media"/> class.
        /// </summary>
        /// <param name="mediaManager">The mediaManager<see cref="TKBase_MediaManager"/></param>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        /// <param name="rtpPacketHandler">The rtpPacketHandler<see cref="Action{SIP_ServerSession, string, byte[]}?"/></param>
        public TKSipInt_Media(
            TKBase_MediaManager mediaManager,
            TKBase_LogManager? logger = null,
            Action<SIP_ServerSession, string, byte[]>? rtpPacketHandler = null) {
            _mediaManager = mediaManager ?? throw new ArgumentNullException(nameof(mediaManager));
            _logger = logger;
            _rtpPacketHandler = rtpPacketHandler;
        }

        /// <summary>
        /// The PrepareCallerMediaLeg
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="invite">The invite<see cref="SIPRequest"/></param>
        public void PrepareCallerMediaLeg(SIP_ServerSession session, SIPRequest invite) {
            if (session == null) {
                throw new ArgumentNullException(nameof(session));
            }
            if (invite == null) {
                throw new ArgumentNullException(nameof(invite));
            }

            AudioDescription callerMedia = ValidateCallerSdp(invite.Body);
            string advertisedIp = RequireAdvertisedIp(session, "LegB", session.LegB.MediaAdvertisedIp, session.MediaAdvertisedIp);
            (int callerRelayPort, int calleeRelayPort) = ReserveRelayPorts();
            TKBase_MediaManager relay = _mediaManager.CreateRelay(callerRelayPort, calleeRelayPort);

            StoreCallerMediaState(session, callerMedia, relay, callerRelayPort, calleeRelayPort);
            session.LegB.PreparedOfferSdp = AudioDescription.Rewrite(invite.Body, advertisedIp, calleeRelayPort);
            _logger?.Info($"SipInt Media Caller vorbereitet | SessionID={session.SessionId} | CallerRTP={callerMedia.Ip}:{callerMedia.Port} | RelayA={callerRelayPort} | RelayB={calleeRelayPort} | SDP-IP={advertisedIp}");
        }

        /// <summary>
        /// The ApplyPreparedOfferSdp
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="outboundInvite">The outboundInvite<see cref="SIPRequest"/></param>
        public void ApplyPreparedOfferSdp(SIP_ServerSession session, SIPRequest outboundInvite) {
            if (session == null) {
                throw new ArgumentNullException(nameof(session));
            }
            if (outboundInvite == null) {
                throw new ArgumentNullException(nameof(outboundInvite));
            }

            if (string.IsNullOrWhiteSpace(session.LegB.PreparedOfferSdp)) {
                return;
            }

            outboundInvite.Body = session.LegB.PreparedOfferSdp;
            outboundInvite.Header.ContentType = "application/sdp";
            outboundInvite.Header.ContentLength = outboundInvite.Body.Length;
        }

        /// <summary>
        /// The HandleCalleeMediaLeg
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="response">The response<see cref="SIPResponse"/></param>
        public void HandleCalleeMediaLeg(SIP_ServerSession session, SIPResponse response) {
            if (session == null) {
                throw new ArgumentNullException(nameof(session));
            }
            if (response == null) {
                throw new ArgumentNullException(nameof(response));
            }
            if (string.IsNullOrWhiteSpace(response.Body)) {
                return;
            }

            AudioDescription calleeMedia = ValidateCalleeSdp(response.Body);
            StoreCalleeMediaState(session, calleeMedia);
            string advertisedIp = RequireAdvertisedIp(session, "LegA", session.LegA.MediaAdvertisedIp, session.MediaAdvertisedIp);
            session.LegA.PreparedAnswerSdp = AudioDescription.Rewrite(response.Body, advertisedIp, session.LegA.RelayPort);
            _logger?.Info($"SipInt Media Callee vorbereitet | SessionID={session.SessionId} | CalleeRTP={calleeMedia.Ip}:{calleeMedia.Port} | RelayA={session.LegA.RelayPort} | RelayB={session.LegB.RelayPort}");
        }

        /// <summary>
        /// The ApplyPreparedAnswerSdp
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="okResponse">The okResponse<see cref="SIPResponse"/></param>
        public void ApplyPreparedAnswerSdp(SIP_ServerSession session, SIPResponse okResponse) {
            if (session == null) {
                throw new ArgumentNullException(nameof(session));
            }
            if (okResponse == null) {
                throw new ArgumentNullException(nameof(okResponse));
            }
            if (string.IsNullOrWhiteSpace(session.LegA.PreparedAnswerSdp)) {
                return;
            }

            okResponse.Body = session.LegA.PreparedAnswerSdp;
            okResponse.Header.ContentType = "application/sdp";
            okResponse.Header.ContentLength = okResponse.Body.Length;
        }

        /// <summary>
        /// The StartRelayIfReady
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        public void StartRelayIfReady(SIP_ServerSession session) {
            if (session == null) {
                throw new ArgumentNullException(nameof(session));
            }

            TKBase_MediaManager? relay = session.Relay;
            if (relay == null || session.LegA.RemoteRtpEndPoint == null || session.LegB.RemoteRtpEndPoint == null) {
                return;
            }

            if (session.Established) {
                return;
            }

            relay.SetCaller(session.LegA.RemoteRtpEndPoint);
            relay.SetCallee(session.LegB.RemoteRtpEndPoint);
            if (_rtpPacketHandler != null) {
                relay.RtpPacketRelayed += (direction, buffer) => _rtpPacketHandler(session, direction, buffer);
            }
            relay.Start();
            session.Established = true;
            session.HasOffer = true;
            session.HasAnswer = true;
            session.UpdatedAt = DateTime.Now;
            _logger?.Info($"SipInt RTP Relay gestartet | SessionID={session.SessionId} | Caller={session.LegA.RemoteRtpEndPoint} | Callee={session.LegB.RemoteRtpEndPoint}");
        }

        /// <summary>
        /// The UpdateCallerMediaLeg
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="sdp">The sdp<see cref="string"/></param>
        public void UpdateCallerMediaLeg(SIP_ServerSession session, string sdp) {
            if (session == null) {
                throw new ArgumentNullException(nameof(session));
            }

            AudioDescription media = ValidateCallerSdp(sdp);
            session.LegA.RemoteRtpEndPoint = new IPEndPoint(IPAddress.Parse(media.Ip), media.Port);
            session.Relay?.SetCaller(session.LegA.RemoteRtpEndPoint);
            session.LegB.PreparedOfferSdp = AudioDescription.Rewrite(sdp, RequireAdvertisedIp(session, "LegB", session.LegB.MediaAdvertisedIp, session.MediaAdvertisedIp), session.LegB.RelayPort);
            session.UpdatedAt = DateTime.Now;
            _logger?.Info($"SipInt Media Caller aktualisiert | SessionID={session.SessionId} | CallerRTP={session.LegA.RemoteRtpEndPoint}");
        }

        /// <summary>
        /// The UpdateCalleeMediaLeg
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="sdp">The sdp<see cref="string"/></param>
        public void UpdateCalleeMediaLeg(SIP_ServerSession session, string sdp) {
            if (session == null) {
                throw new ArgumentNullException(nameof(session));
            }

            AudioDescription media = ValidateCalleeSdp(sdp);
            session.LegB.RemoteRtpEndPoint = new IPEndPoint(IPAddress.Parse(media.Ip), media.Port);
            session.Relay?.SetCallee(session.LegB.RemoteRtpEndPoint);
            session.LegA.PreparedAnswerSdp = AudioDescription.Rewrite(sdp, RequireAdvertisedIp(session, "LegA", session.LegA.MediaAdvertisedIp, session.MediaAdvertisedIp), session.LegA.RelayPort);
            session.UpdatedAt = DateTime.Now;
            _logger?.Info($"SipInt Media Callee aktualisiert | SessionID={session.SessionId} | CalleeRTP={session.LegB.RemoteRtpEndPoint}");
        }

        /// <summary>
        /// The StopRelayAndReleasePorts
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        public void StopRelayAndReleasePorts(SIP_ServerSession session) {
            if (session == null) {
                return;
            }

            StopRelay(session);
            ReleaseRelayPorts(session);
            session.Established = false;
            session.UpdatedAt = DateTime.Now;
        }

        /// <summary>
        /// The StopRelay
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        public void StopRelay(SIP_ServerSession session) {
            TKBase_MediaManager? relay = session.Relay;
            if (relay == null) {
                return;
            }

            relay.StopRelay();
            relay.Dispose();
            session.Relay = null;
            _logger?.Info($"SipInt RTP Relay gestoppt | SessionID={session.SessionId}");
        }

        /// <summary>
        /// The ReleaseRelayPorts
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        public void ReleaseRelayPorts(SIP_ServerSession session) {
            if (session.LegA.RelayPort > 0) {
                _mediaManager.Release(session.LegA.RelayPort);
                session.LegA.RelayPort = 0;
            }

            if (session.LegB.RelayPort > 0) {
                _mediaManager.Release(session.LegB.RelayPort);
                session.LegB.RelayPort = 0;
            }
        }

        /// <summary>
        /// The ValidateCallerSdp
        /// </summary>
        /// <param name="sdp">The sdp<see cref="string?"/></param>
        /// <returns>The <see cref="AudioDescription"/></returns>
        private AudioDescription ValidateCallerSdp(string? sdp) {
            AudioDescription? media = AudioDescription.ParseAudioDescription(sdp);
            if (media == null || string.IsNullOrWhiteSpace(media.Ip) || media.Port <= 0) {
                throw new InvalidOperationException("Caller-SDP enthält keine gültige Audio-Verbindung.");
            }
            return media;
        }

        /// <summary>
        /// The ValidateCalleeSdp
        /// </summary>
        /// <param name="sdp">The sdp<see cref="string?"/></param>
        /// <returns>The <see cref="AudioDescription"/></returns>
        private AudioDescription ValidateCalleeSdp(string? sdp) {
            AudioDescription? media = AudioDescription.ParseAudioDescription(sdp);
            if (media == null || string.IsNullOrWhiteSpace(media.Ip) || media.Port <= 0) {
                throw new InvalidOperationException("Callee-SDP enthält keine gültige Audio-Verbindung.");
            }
            return media;
        }

        /// <summary>
        /// The ReserveRelayPorts
        /// </summary>
        /// <returns>The <see cref="(int callerRelayPort, int calleeRelayPort)"/></returns>
        private (int callerRelayPort, int calleeRelayPort) ReserveRelayPorts() {
            (int rtp, int rtcp)? caller = _mediaManager.Allocate();
            if (caller == null) {
                throw new InvalidOperationException("Kein RTP-Port für SipInt Leg A verfügbar.");
            }

            (int rtp, int rtcp)? callee = _mediaManager.Allocate();
            if (callee == null) {
                _mediaManager.Release(caller.Value.rtp);
                throw new InvalidOperationException("Kein RTP-Port für SipInt Leg B verfügbar.");
            }

            return (caller.Value.rtp, callee.Value.rtp);
        }

        /// <summary>
        /// The StoreCallerMediaState
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="callerMedia">The callerMedia<see cref="AudioDescription"/></param>
        /// <param name="relay">The relay<see cref="TKBase_MediaManager"/></param>
        /// <param name="callerRelayPort">The callerRelayPort<see cref="int"/></param>
        /// <param name="calleeRelayPort">The calleeRelayPort<see cref="int"/></param>
        private void StoreCallerMediaState(SIP_ServerSession session, AudioDescription callerMedia, TKBase_MediaManager relay, int callerRelayPort, int calleeRelayPort) {
            session.Relay = relay;
            session.LegA.RemoteRtpEndPoint = new IPEndPoint(IPAddress.Parse(callerMedia.Ip), callerMedia.Port);
            session.LegA.RelayPort = callerRelayPort;
            session.LegB.RelayPort = calleeRelayPort;
            session.HasOffer = true;
            session.UpdatedAt = DateTime.Now;
        }

        /// <summary>
        /// The StoreCalleeMediaState
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="calleeMedia">The calleeMedia<see cref="AudioDescription"/></param>
        private void StoreCalleeMediaState(SIP_ServerSession session, AudioDescription calleeMedia) {
            session.LegB.RemoteRtpEndPoint = new IPEndPoint(IPAddress.Parse(calleeMedia.Ip), calleeMedia.Port);
            session.HasAnswer = true;
            session.UpdatedAt = DateTime.Now;
        }

        /// <summary>
        /// The RequireAdvertisedIp
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="legName">The legName<see cref="string"/></param>
        /// <param name="candidates">The candidates<see cref="string?[]"/></param>
        /// <returns>The <see cref="string"/></returns>
        private string RequireAdvertisedIp(SIP_ServerSession session, string legName, params string?[] candidates) {
            foreach (string? candidate in candidates) {
                if (TryGetUsableAdvertisedIp(candidate, out string? usableIp) && usableIp != null) {
                    return usableIp;
                }
            }

            string message = $"MediaAdvertisedIp fehlt | SessionID={session.SessionId} | Leg={legName}";
            _logger?.Error(message);
            throw new InvalidOperationException(message);
        }

        /// <summary>
        /// The TryGetUsableAdvertisedIp
        /// </summary>
        /// <param name="ipText">The ipText<see cref="string?"/></param>
        /// <param name="usableIp">The usableIp<see cref="string?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool TryGetUsableAdvertisedIp(string? ipText, out string? usableIp) {
            usableIp = null;
            if (string.IsNullOrWhiteSpace(ipText)) {
                return false;
            }

            string trimmed = ipText.Trim();
            if (!IPAddress.TryParse(trimmed, out IPAddress? parsed)) {
                return false;
            }

            if (IPAddress.Any.Equals(parsed) || IPAddress.IPv6Any.Equals(parsed)) {
                return false;
            }

            usableIp = parsed.ToString();
            return true;
        }
    }
}
