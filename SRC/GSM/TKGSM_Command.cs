/*
 * TKGSM_Commands
 * Aufgabe:
 * - Enthält die GSM-Bereichskommandos getrennt nach Leg A und Leg B.
 * - Leg A ruft keine Leg-B-Methode auf und Leg B ruft keine Leg-A-Methode auf.
 * - Baut SIP-Dialognachrichten, speichert Leg-Zustände und führt GSM-Kommandos aus.
 */

namespace TKServer.GSM {
    using SIPSorcery.SIP;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using TKServer.BASE;
    /// TKGSM_Commands
    /// Aufgabe:
    /// - Enthält die GSM-Bereichskommandos getrennt nach Leg A und Leg B.
    /// - Leg A ruft keine Leg-B-Methode auf und Leg B ruft keine Leg-A-Methode auf.
    /// - Baut SIP-Dialognachrichten, speichert Leg-Zustände und führt GSM-Kommandos aus.

    /// <summary>
    /// Defines the <see cref="TKGSM_Commands" />
    /// </summary>
    internal sealed class TKGSM_Commands {
        /// <summary>
        /// Gets the LegA
        /// </summary>
        public LegACommands LegA { get; }

        /// <summary>
        /// Gets the LegB
        /// </summary>
        public LegBCommands LegB { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TKGSM_Commands"/> class.
        /// </summary>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        public TKGSM_Commands(TKBase_LogManager? logger = null) {
            LegA = new LegACommands(logger);
            LegB = new LegBCommands(logger);
        }

        /// <summary>
        /// The SameSipEndPoint
        /// </summary>
        /// <param name="a">The a<see cref="SIPEndPoint?"/></param>
        /// <param name="b">The b<see cref="SIPEndPoint?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public bool SameSipEndPoint(SIPEndPoint? a, SIPEndPoint? b) {
            if (a == null || b == null || a.Address == null || b.Address == null) return false;
            return a.Protocol == b.Protocol && a.Port == b.Port && a.Address.Equals(b.Address);
        }

        /// <summary>
        /// The BuildInboundGsmOfferSdp
        /// </summary>
        /// <param name="advertisedIp">The advertisedIp<see cref="string"/></param>
        /// <param name="port">The port<see cref="int"/></param>
        /// <param name="codecs">The codecs<see cref="IReadOnlyList{TKBase_AudioCodecConfig}"/></param>
        /// <returns>The <see cref="string"/></returns>
        public string BuildInboundGsmOfferSdp(string advertisedIp, int port, IReadOnlyList<TKBase_AudioCodecConfig> codecs) {
            IReadOnlyList<TKBase_AudioCodecConfig> audioCodecs = codecs.Where(c => c.IsGsmSipCodec).ToList();
            if (audioCodecs.Count == 0) {
                throw new InvalidOperationException("Kein GSM-faehiger SIP/RTP-Audiocodec fuer GSM-SDP-Angebot verfuegbar.");
            }

            IReadOnlyList<TKBase_AudioCodecConfig> eventCodecs = TKBase_AudioCodecConfig.SelectGsmTelephoneEventCodecs(codecs);
            List<TKBase_AudioCodecConfig> sdpCodecs = audioCodecs.Concat(eventCodecs).ToList();
            return BuildAudioSdp("TK-Server-GSM-Inbound", advertisedIp, port, sdpCodecs, audioCodecs[0]);
        }

        /// <summary>
        /// The BuildGsmAnswerSdp
        /// </summary>
        /// <param name="advertisedIp">The advertisedIp<see cref="string"/></param>
        /// <param name="port">The port<see cref="int"/></param>
        /// <param name="codec">The codec<see cref="TKBase_AudioCodecConfig"/></param>
        /// <param name="telephoneEventCodecs">The telephoneEventCodecs<see cref="IReadOnlyList{TKBase_AudioCodecConfig}"/></param>
        /// <returns>The <see cref="string"/></returns>
        public string BuildGsmAnswerSdp(string advertisedIp, int port, TKBase_AudioCodecConfig codec, IReadOnlyList<TKBase_AudioCodecConfig> telephoneEventCodecs) {
            if (!codec.IsGsmSipCodec) {
                throw new InvalidOperationException($"Codec ist nicht GSM-SIP-faehig: {codec.CodecKey}");
            }

            List<TKBase_AudioCodecConfig> codecs = new List<TKBase_AudioCodecConfig> { codec };
            if (telephoneEventCodecs != null) {
                codecs.AddRange(telephoneEventCodecs.Where(c => c.IsGsmTelephoneEventCodec));
            }

            return BuildAudioSdp("TK-Server-GSM", advertisedIp, port, codecs, codec);
        }

        /// <summary>
        /// The BuildAudioSdp
        /// </summary>
        /// <param name="sessionName">The sessionName<see cref="string"/></param>
        /// <param name="advertisedIp">The advertisedIp<see cref="string"/></param>
        /// <param name="port">The port<see cref="int"/></param>
        /// <param name="codecs">The codecs<see cref="IReadOnlyList{TKBase_AudioCodecConfig}"/></param>
        /// <param name="timingCodec">The timingCodec<see cref="TKBase_AudioCodecConfig"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string BuildAudioSdp(string sessionName, string advertisedIp, int port, IReadOnlyList<TKBase_AudioCodecConfig> codecs, TKBase_AudioCodecConfig timingCodec) {
            string payloadList = string.Join(" ", codecs.Select(c => c.PayloadTypeOrThrow().ToString(System.Globalization.CultureInfo.InvariantCulture)));
            List<string> lines = new List<string> {
                "v=0",
                $"o=- {DateTimeOffset.UtcNow.ToUnixTimeSeconds()} 0 IN IP4 {advertisedIp}",
                $"s={sessionName}",
                $"c=IN IP4 {advertisedIp}",
                "t=0 0",
                $"m=audio {port} RTP/AVP {payloadList}"
            };

            foreach (TKBase_AudioCodecConfig codec in codecs) {
                lines.Add(BuildRtpMapLine(codec));
                if (!string.IsNullOrWhiteSpace(codec.RtpFormatParams)) {
                    lines.Add($"a=fmtp:{codec.PayloadTypeOrThrow()} {codec.RtpFormatParams}");
                }
            }

            lines.Add("a=sendrecv");
            if (timingCodec.PtimeMs.HasValue) {
                lines.Add($"a=ptime:{timingCodec.PtimeMs.Value}");
            }
            if (timingCodec.MaxPtimeMs.HasValue) {
                lines.Add($"a=maxptime:{timingCodec.MaxPtimeMs.Value}");
            }
            lines.Add(string.Empty);
            return string.Join("\r\n", lines);
        }

        /// <summary>
        /// The BuildRtpMapLine
        /// </summary>
        /// <param name="codec">The codec<see cref="TKBase_AudioCodecConfig"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string BuildRtpMapLine(TKBase_AudioCodecConfig codec) {
            string channelPart = codec.Channels > 1 ? $"/{codec.Channels}" : string.Empty;
            return $"a=rtpmap:{codec.PayloadTypeOrThrow()} {codec.RtpEncodingName}/{codec.RtpClockRate}{channelPart}";
        }

        /// <summary>
        /// The BuildUserUri
        /// </summary>
        /// <param name="user">The user<see cref="string?"/></param>
        /// <param name="host">The host<see cref="string"/></param>
        /// <param name="port">The port<see cref="int?"/></param>
        /// <param name="transport">The transport<see cref="string?"/></param>
        /// <returns>The <see cref="SIPURI"/></returns>
        internal static SIPURI BuildUserUri(string? user, string host, int? port = null, string? transport = null) {
            string safeUser = Uri.EscapeDataString((user ?? string.Empty).Trim());
            string safeHost = host.Trim();
            if (port.HasValue && port.Value > 0 && !safeHost.Contains(':')) safeHost = $"{safeHost}:{port.Value}";
            SIPURI uri = SIPURI.ParseSIPURIRelaxed($"sip:{safeUser}@{safeHost}");
            if (!string.IsNullOrWhiteSpace(transport)) uri.Parameters.Set("transport", transport.Trim().ToLowerInvariant());
            return uri;
        }

        /// <summary>
        /// The BuildGsmContactUri
        /// </summary>
        /// <param name="user">The user<see cref="string?"/></param>
        /// <param name="requestUri">The requestUri<see cref="SIPURI"/></param>
        /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint"/></param>
        /// <returns>The <see cref="SIPURI"/></returns>
        internal static SIPURI BuildGsmContactUri(string? user, SIPURI requestUri, SIPEndPoint localEndPoint) {
            string host = localEndPoint.Address?.ToString() ?? requestUri.Host;
            int port = localEndPoint.Port > 0 ? localEndPoint.Port : SIPConstants.GetDefaultPort(localEndPoint.Protocol);
            return BuildUserUri(FirstContactUser(user, requestUri.User) ?? "unknown", host, port, localEndPoint.Protocol.ToString());
        }

        /// <summary>
        /// The BuildGsmLegAContactUri
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="requestUri">The requestUri<see cref="SIPURI"/></param>
        /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint"/></param>
        /// <returns>The <see cref="SIPURI"/></returns>
        internal static SIPURI BuildGsmLegAContactUri(SIP_ServerSession session, SIPURI requestUri, SIPEndPoint localEndPoint) {
            string? user = string.Equals(session.GsmDirection, "Inbound", StringComparison.OrdinalIgnoreCase)
                ? FirstContactUser(session.FromNumber, session.GsmLastClip, requestUri.User)
                : FirstContactUser(session.ToNumber, session.GsmLastClip, session.FromUsername, requestUri.User);
            return BuildGsmContactUri(user, requestUri, localEndPoint);
        }

        /// <summary>
        /// The BuildGsmLegBContactUri
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="requestUri">The requestUri<see cref="SIPURI"/></param>
        /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint"/></param>
        /// <returns>The <see cref="SIPURI"/></returns>
        internal static SIPURI BuildGsmLegBContactUri(SIP_ServerSession session, SIPURI requestUri, SIPEndPoint localEndPoint) {
            string? user = string.Equals(session.GsmDirection, "Inbound", StringComparison.OrdinalIgnoreCase)
                ? FirstContactUser(session.FromNumber, session.GsmLastClip, requestUri.User)
                : FirstContactUser(session.FromUsername, session.FromNumber, requestUri.User);
            return BuildGsmContactUri(user, requestUri, localEndPoint);
        }

        /// <summary>
        /// The BuildGsmContactHeader
        /// </summary>
        /// <param name="contactUri">The contactUri<see cref="SIPURI"/></param>
        /// <returns>The <see cref="SIPContactHeader"/></returns>
        internal static SIPContactHeader BuildGsmContactHeader(SIPURI contactUri) {
            return new SIPContactHeader(null, contactUri);
        }

        /// <summary>
        /// The EnsureGsmLegALocalTag
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <returns>The <see cref="string"/></returns>
        internal static string EnsureGsmLegALocalTag(SIP_ServerSession session) {
            if (string.IsNullOrWhiteSpace(session.LegA.ToTag)) {
                session.LegA.ToTag = Guid.NewGuid().ToString("N");
            }
            return session.LegA.ToTag;
        }

        /// <summary>
        /// The ApplyGsmLegAInviteDialogHeaders
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="response">The response<see cref="SIPResponse"/></param>
        /// <param name="requestUri">The requestUri<see cref="SIPURI"/></param>
        internal static void ApplyGsmLegAInviteDialogHeaders(SIP_ServerSession session, SIPResponse response, SIPURI requestUri) {
            if (response.Header?.CSeqMethod != SIPMethodsEnum.INVITE) {
                return;
            }

            if (response.StatusCode != (int)SIPResponseStatusCodesEnum.Trying && response.Header.To != null) {
                response.Header.To.ToTag = EnsureGsmLegALocalTag(session);
            }

            if (response.StatusCode >= 200 && response.StatusCode < 300 && session.LegA.LocalSipEndPoint != null) {
                response.Header.Contact = new List<SIPContactHeader> { BuildGsmContactHeader(BuildGsmLegAContactUri(session, requestUri, session.LegA.LocalSipEndPoint)) };
            }
        }

        /// <summary>
        /// The FirstContactUser
        /// </summary>
        /// <param name="values">The values<see cref="string?[]"/></param>
        /// <returns>The <see cref="string?"/></returns>
        internal static string? FirstContactUser(params string?[] values) {
            foreach (string? value in values) {
                string user = NormalizeSipUser(value);
                if (!string.IsNullOrWhiteSpace(user)) {
                    return user;
                }
            }
            return null;
        }

        /// <summary>
        /// The DecodeSipUserPart
        /// </summary>
        /// <param name="value">The value<see cref="string?"/></param>
        /// <returns>The <see cref="string"/></returns>
        internal static string DecodeSipUserPart(string? value) {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string trimmed = value.Trim();
            int semi = trimmed.IndexOf(';');
            if (semi >= 0) trimmed = trimmed[..semi];
            return Uri.UnescapeDataString(trimmed);
        }

        /// <summary>
        /// The NormalizeSipUser
        /// </summary>
        /// <param name="value">The value<see cref="string?"/></param>
        /// <returns>The <see cref="string"/></returns>
        internal static string NormalizeSipUser(string? value) {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string user = value.Trim();
            int semi = user.IndexOf(';');
            if (semi >= 0) user = user[..semi];
            return Uri.UnescapeDataString(user);
        }

        /// <summary>
        /// Defines the <see cref="LegACommands" />
        /// </summary>
        internal sealed class LegACommands {
            /// <summary>
            /// Defines the _logger
            /// </summary>
            private readonly TKBase_LogManager? _logger;

            /// <summary>
            /// Initializes a new instance of the <see cref="LegACommands"/> class.
            /// </summary>
            /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
            public LegACommands(TKBase_LogManager? logger = null) {
                _logger = logger;
            }

            /// <summary>
            /// The ExtractFromNumber
            /// </summary>
            /// <param name="request">The request<see cref="SIPRequest"/></param>
            /// <returns>The <see cref="string"/></returns>
            public string ExtractFromNumber(SIPRequest request) => DecodeSipUserPart(request.Header?.From?.FromURI?.User);

            /// <summary>
            /// The ExtractToNumber
            /// </summary>
            /// <param name="request">The request<see cref="SIPRequest"/></param>
            /// <returns>The <see cref="string"/></returns>
            public string ExtractToNumber(SIPRequest request) => DecodeSipUserPart(request.Header?.To?.ToURI?.User ?? request.URI?.User);

            /// <summary>
            /// The StoreSipInviteReceived
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="invite">The invite<see cref="SIPRequest"/></param>
            /// <param name="remoteEndPoint">The remoteEndPoint<see cref="SIPEndPoint"/></param>
            /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint?"/></param>
            public void StoreSipInviteReceived(SIP_ServerSession session, SIPRequest invite, SIPEndPoint remoteEndPoint, SIPEndPoint? localEndPoint) {
                session.LegA.InviteReceived = invite.Copy();
                session.LegA.InviteReceivedRaw = invite.ToString();
                session.LegA.LastRequestReceived = session.LegA.InviteReceived;
                session.LegA.LastRequestReceivedRaw = session.LegA.InviteReceivedRaw;
                session.LegA.SipEndPoint = remoteEndPoint;
                session.LegA.LocalSipEndPoint = localEndPoint;
                session.LegA.CallId = invite.Header?.CallId ?? session.LegA.CallId ?? session.SessionId;
                session.LegA.RequestUri = invite.URI;
                session.LegA.ContactUri = invite.Header?.Contact?.FirstOrDefault()?.ContactURI;
                session.LegA.FromTag = invite.Header?.From?.FromTag ?? session.LegA.FromTag;
                session.LegA.ToTag = invite.Header?.To?.ToTag ?? session.LegA.ToTag;
                session.LegA.DialogCSeq = invite.Header?.CSeq ?? session.LegA.DialogCSeq;
                session.LegA.InviteSeen = true;
                session.LegA.UpdatedAt = DateTime.Now;
                _logger?.Debug($"GSM LegA SIP INVITE IN gespeichert | Ebene=Command | SessionID={session.SessionId} | CallID={session.LegA.CallId} | Local={localEndPoint?.ToString() ?? "-"} | Remote={remoteEndPoint} | URI={invite.URI} | From={invite.Header?.From} | To={invite.Header?.To} | CSeq={invite.Header?.CSeq} {invite.Header?.CSeqMethod} | BodyLength={invite.Body?.Length ?? 0} | RawLength={session.LegA.InviteReceivedRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START GSM LegA INVITE IN ---{Environment.NewLine}{session.LegA.InviteReceivedRaw}{Environment.NewLine}--- SIP RAW END GSM LegA INVITE IN ---");
            }

            /// <summary>
            /// The StoreSipAckReceived
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="ack">The ack<see cref="SIPRequest"/></param>
            /// <param name="remoteEndPoint">The remoteEndPoint<see cref="SIPEndPoint"/></param>
            /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint?"/></param>
            public void StoreSipAckReceived(SIP_ServerSession session, SIPRequest ack, SIPEndPoint remoteEndPoint, SIPEndPoint? localEndPoint) {
                session.LegA.AckReceived = ack.Copy();
                session.LegA.AckReceivedRaw = ack.ToString();
                session.LegA.LastRequestReceived = session.LegA.AckReceived;
                session.LegA.LastRequestReceivedRaw = session.LegA.AckReceivedRaw;
                session.LegA.SipEndPoint = remoteEndPoint;
                session.LegA.LocalSipEndPoint = localEndPoint;
                session.LegA.CallId = ack.Header?.CallId ?? session.LegA.CallId ?? session.SessionId;
                session.LegA.AckSeen = true;
                session.LegA.UpdatedAt = DateTime.Now;
                _logger?.Debug($"GSM LegA SIP ACK IN gespeichert | Ebene=Command | SessionID={session.SessionId} | CallID={session.LegA.CallId} | Local={localEndPoint?.ToString() ?? "-"} | Remote={remoteEndPoint} | URI={ack.URI} | From={ack.Header?.From} | To={ack.Header?.To} | CSeq={ack.Header?.CSeq} {ack.Header?.CSeqMethod} | RawLength={session.LegA.AckReceivedRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START GSM LegA ACK IN ---{Environment.NewLine}{session.LegA.AckReceivedRaw}{Environment.NewLine}--- SIP RAW END GSM LegA ACK IN ---");
            }

            /// <summary>
            /// The StoreSipByeReceived
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="bye">The bye<see cref="SIPRequest"/></param>
            /// <param name="remoteEndPoint">The remoteEndPoint<see cref="SIPEndPoint"/></param>
            /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint?"/></param>
            public void StoreSipByeReceived(SIP_ServerSession session, SIPRequest bye, SIPEndPoint remoteEndPoint, SIPEndPoint? localEndPoint) {
                session.LegA.ByeReceived = bye.Copy();
                session.LegA.ByeReceivedRaw = bye.ToString();
                session.LegA.LastRequestReceived = session.LegA.ByeReceived;
                session.LegA.LastRequestReceivedRaw = session.LegA.ByeReceivedRaw;
                session.LegA.SipEndPoint = remoteEndPoint;
                session.LegA.LocalSipEndPoint = localEndPoint;
                session.LegA.ByeSeen = true;
                session.LegA.IsEnded = true;
                session.LegA.UpdatedAt = DateTime.Now;
                _logger?.Debug($"GSM LegA SIP BYE IN gespeichert | Ebene=Command | SessionID={session.SessionId} | CallID={session.LegA.CallId} | Local={localEndPoint?.ToString() ?? "-"} | Remote={remoteEndPoint} | URI={bye.URI} | From={bye.Header?.From} | To={bye.Header?.To} | CSeq={bye.Header?.CSeq} {bye.Header?.CSeqMethod} | RawLength={session.LegA.ByeReceivedRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START GSM LegA BYE IN ---{Environment.NewLine}{session.LegA.ByeReceivedRaw}{Environment.NewLine}--- SIP RAW END GSM LegA BYE IN ---");
            }

            /// <summary>
            /// The StoreSipCancelReceived
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="cancel">The cancel<see cref="SIPRequest"/></param>
            /// <param name="remoteEndPoint">The remoteEndPoint<see cref="SIPEndPoint"/></param>
            /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint?"/></param>
            public void StoreSipCancelReceived(SIP_ServerSession session, SIPRequest cancel, SIPEndPoint remoteEndPoint, SIPEndPoint? localEndPoint) {
                session.LegA.CancelReceived = cancel.Copy();
                session.LegA.CancelReceivedRaw = cancel.ToString();
                session.LegA.LastRequestReceived = session.LegA.CancelReceived;
                session.LegA.LastRequestReceivedRaw = session.LegA.CancelReceivedRaw;
                session.LegA.SipEndPoint = remoteEndPoint;
                session.LegA.LocalSipEndPoint = localEndPoint;
                session.LegA.CancelSeen = true;
                session.LegA.IsEnded = true;
                session.LegA.UpdatedAt = DateTime.Now;
                _logger?.Debug($"GSM LegA SIP CANCEL IN gespeichert | Ebene=Command | SessionID={session.SessionId} | CallID={session.LegA.CallId} | Local={localEndPoint?.ToString() ?? "-"} | Remote={remoteEndPoint} | URI={cancel.URI} | From={cancel.Header?.From} | To={cancel.Header?.To} | CSeq={cancel.Header?.CSeq} {cancel.Header?.CSeqMethod} | RawLength={session.LegA.CancelReceivedRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START GSM LegA CANCEL IN ---{Environment.NewLine}{session.LegA.CancelReceivedRaw}{Environment.NewLine}--- SIP RAW END GSM LegA CANCEL IN ---");
            }

            /// <summary>
            /// The StoreSipResponseSent
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="response">The response<see cref="SIPResponse"/></param>
            public void StoreSipResponseSent(SIP_ServerSession session, SIPResponse response) {
                session.LegA.LastResponseSent = response;
                session.LegA.LastResponseSentRaw = response.ToString();
                session.LegA.CallId = response.Header?.CallId ?? session.LegA.CallId ?? session.SessionId;
                session.LegA.ToTag = response.Header?.To?.ToTag ?? session.LegA.ToTag;
                if (response.StatusCode >= 100 && response.StatusCode < 200) session.LegA.LastProvisionalResponse = response;
                if (response.StatusCode >= 200 && response.StatusCode < 300) { session.LegA.LastSuccessResponse = response; session.LastSuccessResponse = response; session.IsAnswered = true; }
                if (response.StatusCode >= 300) session.LegA.IsEnded = true;
                session.LegA.UpdatedAt = DateTime.Now;
                _logger?.Debug($"GSM LegA SIP RESPONSE OUT gespeichert | Ebene=Command | SessionID={session.SessionId} | Status={response.StatusCode} {response.ReasonPhrase} | CallID={session.LegA.CallId} | CSeq={response.Header?.CSeq} {response.Header?.CSeqMethod} | BodyLength={response.Body?.Length ?? 0} | RawLength={session.LegA.LastResponseSentRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START GSM LegA RESPONSE OUT ---{Environment.NewLine}{session.LegA.LastResponseSentRaw}{Environment.NewLine}--- SIP RAW END GSM LegA RESPONSE OUT ---");
            }

            /// <summary>
            /// The BuildResponseFromInvite
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="status">The status<see cref="SIPResponseStatusCodesEnum"/></param>
            /// <param name="reason">The reason<see cref="string?"/></param>
            /// <returns>The <see cref="SIPResponse"/></returns>
            public SIPResponse BuildResponseFromInvite(SIP_ServerSession session, SIPResponseStatusCodesEnum status, string? reason) {
                SIPRequest invite = session.LegA.InviteReceived ?? throw new InvalidOperationException("LegA INVITE fehlt.");
                SIPResponse response = SIPResponse.GetResponse(invite, status, reason);
                ApplyGsmLegAInviteDialogHeaders(session, response, invite.URI);
                return response;
            }


            /// <summary>
            /// The StoreSipResponseReceived
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="response">The response<see cref="SIPResponse"/></param>
            /// <param name="remoteEndPoint">The remoteEndPoint<see cref="SIPEndPoint"/></param>
            /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint?"/></param>
            public void StoreSipResponseReceived(SIP_ServerSession session, SIPResponse response, SIPEndPoint remoteEndPoint, SIPEndPoint? localEndPoint) {
                session.LegA.LastResponseReceived = response;
                session.LegA.LastResponseReceivedRaw = response.ToString();
                session.LegA.SipEndPoint = remoteEndPoint;
                session.LegA.LocalSipEndPoint = localEndPoint;
                session.LegA.CallId = response.Header?.CallId ?? session.LegA.CallId ?? session.SessionId;
                session.LegA.UpdatedAt = DateTime.Now;
                _logger?.Debug($"GSM LegA SIP RESPONSE IN gespeichert | Ebene=Command | SessionID={session.SessionId} | Status={response.StatusCode} {response.ReasonPhrase} | CallID={session.LegA.CallId} | Local={localEndPoint?.ToString() ?? "-"} | Remote={remoteEndPoint} | CSeq={response.Header?.CSeq} {response.Header?.CSeqMethod} | BodyLength={response.Body?.Length ?? 0} | RawLength={session.LegA.LastResponseReceivedRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START GSM LegA RESPONSE IN ---{Environment.NewLine}{session.LegA.LastResponseReceivedRaw}{Environment.NewLine}--- SIP RAW END GSM LegA RESPONSE IN ---");
            }


            /// <summary>
            /// The BuildByeToSipLeg
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint"/></param>
            /// <returns>The <see cref="SIPRequest"/></returns>
            public SIPRequest BuildByeToSipLeg(SIP_ServerSession session, SIPEndPoint localEndPoint) {
                SIPRequest invite = session.LegA.InviteReceived ?? throw new InvalidOperationException("LegA INVITE fehlt.");
                SIPURI requestUri = session.LegA.ContactUri ?? invite.Header.Contact?.FirstOrDefault()?.ContactURI ?? invite.URI;
                string localTag = session.LegA.ToTag ?? session.LegA.LastSuccessResponse?.Header?.To?.ToTag ?? EnsureGsmLegALocalTag(session);
                SIPFromHeader fromHeader = new SIPFromHeader(invite.Header.To?.ToName, invite.Header.To?.ToURI ?? invite.URI, localTag);
                SIPToHeader toHeader = new SIPToHeader(invite.Header.From?.FromName, invite.Header.From?.FromURI ?? invite.URI, invite.Header.From?.FromTag);
                SIPRequest bye = SIPRequest.GetRequest(SIPMethodsEnum.BYE, requestUri, toHeader, fromHeader);
                bye.Header.CallId = invite.Header.CallId;
                bye.Header.CSeq = ++session.LegA.DialogCSeq;
                bye.Header.CSeqMethod = SIPMethodsEnum.BYE;
                bye.Header.MaxForwards = 70;
                bye.Header.ContentLength = 0;
                bye.Header.Contact = new List<SIPContactHeader> { BuildGsmContactHeader(BuildGsmLegAContactUri(session, requestUri, localEndPoint)) };
                bye.SetSendFromHints(localEndPoint);
                return bye;
            }

            /// <summary>
            /// The StoreGsmInboundSignal
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="state">The state<see cref="string"/></param>
            /// <param name="rawLine">The rawLine<see cref="string?"/></param>
            public void StoreGsmInboundSignal(SIP_ServerSession session, string state, string? rawLine) {
                session.Status = state;
                session.GsmLastSignal = rawLine ?? session.GsmLastSignal;
                session.GsmLastEventAt = DateTime.Now;
                session.UpdatedAt = DateTime.Now;
            }
        }

        /// <summary>
        /// Defines the <see cref="LegBCommands" />
        /// </summary>
        internal sealed class LegBCommands {
            /// <summary>
            /// Defines the _logger
            /// </summary>
            private readonly TKBase_LogManager? _logger;

            /// <summary>
            /// Initializes a new instance of the <see cref="LegBCommands"/> class.
            /// </summary>
            /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
            public LegBCommands(TKBase_LogManager? logger = null) {
                _logger = logger;
            }

            /// <summary>
            /// The StartGsmOutboundCall
            /// </summary>
            /// <param name="route">The route<see cref="SIPIntResolvedTarget"/></param>
            /// <param name="fromNumber">The fromNumber<see cref="string"/></param>
            /// <param name="toNumber">The toNumber<see cref="string"/></param>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <param name="gsmSessionId">The gsmSessionId<see cref="string?"/></param>
            /// <param name="reason">The reason<see cref="string?"/></param>
            /// <returns>The <see cref="bool"/></returns>
            public bool StartGsmOutboundCall(SIPIntResolvedTarget route, string fromNumber, string toNumber, string sessionId, out string? gsmSessionId, out string? reason) {
                bool started = TKBase_GsmGateway.StartOutboundCall(route, fromNumber, toNumber, out gsmSessionId, out reason, sessionId);
                _logger?.Info($"GSM LegB Dial | SessionID={sessionId} | Started={started} | StatusID={route.GsmStatusId} | Ziel={toNumber} | Grund={reason ?? string.Empty}");
                return started;
            }

            /// <summary>
            /// The HangupGsmCall
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="reason">The reason<see cref="string?"/></param>
            /// <returns>The <see cref="bool"/></returns>
            public bool HangupGsmCall(SIP_ServerSession session, string? reason) {
                if (!session.GsmStatusId.HasValue) return false;
                bool ok = TKBase_GsmGateway.HangupCall(session.GsmStatusId.Value, session.SessionId);
                session.Status = string.IsNullOrWhiteSpace(reason) ? "Ended" : reason!;
                session.UpdatedAt = DateTime.Now;
                _logger?.Info($"GSM LegB Hangup | SessionID={session.SessionId} | StatusID={session.GsmStatusId} | Ok={ok} | Grund={reason ?? string.Empty}");
                return ok;
            }

            /// <summary>
            /// The AnswerGsmCall
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <returns>The <see cref="bool"/></returns>
            public bool AnswerGsmCall(SIP_ServerSession session) {
                if (!session.GsmStatusId.HasValue) return false;
                bool ok = TKBase_GsmGateway.AnswerCall(session.GsmStatusId.Value, session.SessionId);
                _logger?.Info($"GSM LegB Answer | SessionID={session.SessionId} | StatusID={session.GsmStatusId} | Ok={ok}");
                return ok;
            }

            /// <summary>
            /// The ActivateAudioRoute
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <returns>The <see cref="bool"/></returns>
            public bool ActivateAudioRoute(SIP_ServerSession session) {
                if (!session.GsmStatusId.HasValue) return false;
                bool ok = TKBase_GsmGateway.TryActivateAudioRoute(session.GsmStatusId.Value);
                _logger?.Info($"GSM LegB AudioRoute | SessionID={session.SessionId} | StatusID={session.GsmStatusId} | Ok={ok}");
                return ok;
            }
        }
    }
}
