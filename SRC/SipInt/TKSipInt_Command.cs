/*
 * TKSipInt_Commands
 * Aufgabe:
 * - Lokale SipInt-Kommandos.
 * - Leg A und Leg B sind getrennt.
 * - Keine Leg-A-Methode nutzt Leg-B-Methoden und keine Leg-B-Methode nutzt Leg-A-Methoden.
 */

namespace TKServer.SipInt {
    using SIPSorcery.SIP;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using TKServer.BASE;
    /// TKSipInt_Commands
    /// Aufgabe:
    /// - Lokale SipInt-Kommandos.
    /// - Leg A und Leg B sind getrennt.
    /// - Keine Leg-A-Methode nutzt Leg-B-Methoden und keine Leg-B-Methode nutzt Leg-A-Methoden.

    /// <summary>
    /// Defines the <see cref="TKSipInt_Commands" />
    /// </summary>
    internal sealed class TKSipInt_Commands {
        /// <summary>
        /// Gets the LegA
        /// </summary>
        public LegACommands LegA { get; }

        /// <summary>
        /// Gets the LegB
        /// </summary>
        public LegBCommands LegB { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TKSipInt_Commands"/> class.
        /// </summary>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        /// <param name="areaName">The areaName<see cref="string"/></param>
        public TKSipInt_Commands(TKBase_LogManager? logger = null, string areaName = "SipInt") {
            LegA = new LegACommands(logger, areaName);
            LegB = new LegBCommands(logger, areaName);
        }

        /// <summary>
        /// The ResolveRouteEndPoint
        /// </summary>
        /// <param name="route">The route<see cref="SIPIntResolvedTarget"/></param>
        /// <returns>The <see cref="SIPEndPoint"/></returns>
        public SIPEndPoint ResolveRouteEndPoint(SIPIntResolvedTarget route) {
            if (!string.IsNullOrWhiteSpace(route.RemoteEndPoint)) {
                SIPEndPoint? ep = SIPEndPoint.TryParse(route.RemoteEndPoint);
                if (ep != null) {
                    return ep;
                }
            }

            if (!string.IsNullOrWhiteSpace(route.ContactUri)) {
                SIPURI uri = SIPURI.ParseSIPURIRelaxed(route.ContactUri);
                SIPEndPoint ep = uri.ToSIPEndPoint();
                if (ep != null) {
                    return ep;
                }
            }

            throw new InvalidOperationException($"SIP Zielendpunkt konnte nicht ermittelt werden | Ziel={route.CalledNumber}");
        }

        /// <summary>
        /// The SameSipEndPoint
        /// </summary>
        /// <param name="a">The a<see cref="SIPEndPoint?"/></param>
        /// <param name="b">The b<see cref="SIPEndPoint?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public bool SameSipEndPoint(SIPEndPoint? a, SIPEndPoint? b) {
            if (a == null || b == null || a.Address == null || b.Address == null) {
                return false;
            }

            return a.Protocol == b.Protocol && a.Port == b.Port && a.Address.Equals(b.Address);
        }

        /// <summary>
        /// The BuildUserUri
        /// </summary>
        /// <param name="user">The user<see cref="string?"/></param>
        /// <param name="host">The host<see cref="string"/></param>
        /// <param name="port">The port<see cref="int?"/></param>
        /// <param name="transport">The transport<see cref="string?"/></param>
        /// <returns>The <see cref="SIPURI"/></returns>
        private static SIPURI BuildUserUri(string? user, string host, int? port = null, string? transport = null) {
            string safeUser = Uri.EscapeDataString((user ?? string.Empty).Trim());
            string safeHost = host.Trim();
            if (port.HasValue && port.Value > 0 && !safeHost.Contains(':')) {
                safeHost = $"{safeHost}:{port.Value}";
            }

            SIPURI uri = SIPURI.ParseSIPURIRelaxed($"sip:{safeUser}@{safeHost}");
            if (!string.IsNullOrWhiteSpace(transport)) {
                uri.Parameters.Set("transport", transport.Trim().ToLowerInvariant());
            }
            return uri;
        }

        /// <summary>
        /// The BuildSipIntContactUri
        /// </summary>
        /// <param name="user">The user<see cref="string?"/></param>
        /// <param name="requestUri">The requestUri<see cref="SIPURI"/></param>
        /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint"/></param>
        /// <returns>The <see cref="SIPURI"/></returns>
        private static SIPURI BuildSipIntContactUri(string? user, SIPURI requestUri, SIPEndPoint localEndPoint) {
            string host = localEndPoint.Address?.ToString() ?? requestUri.Host;
            int port = localEndPoint.Port > 0 ? localEndPoint.Port : SIPConstants.GetDefaultPort(localEndPoint.Protocol);
            return BuildUserUri(ResolveSipIntContactUser(user, requestUri), host, port, localEndPoint.Protocol.ToString());
        }

        /// <summary>
        /// The BuildSipIntLegAContactUri
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="requestUri">The requestUri<see cref="SIPURI"/></param>
        /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint"/></param>
        /// <returns>The <see cref="SIPURI"/></returns>
        private static SIPURI BuildSipIntLegAContactUri(SIP_ServerSession session, SIPURI requestUri, SIPEndPoint localEndPoint) {
            return BuildSipIntContactUri(FirstContactUser(session.ToUsername, session.ToNumber, requestUri.User), requestUri, localEndPoint);
        }

        /// <summary>
        /// The BuildSipIntLegBContactUri
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="requestUri">The requestUri<see cref="SIPURI"/></param>
        /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint"/></param>
        /// <returns>The <see cref="SIPURI"/></returns>
        private static SIPURI BuildSipIntLegBContactUri(SIP_ServerSession session, SIPURI requestUri, SIPEndPoint localEndPoint) {
            return BuildSipIntContactUri(FirstContactUser(session.FromUsername, session.FromNumber, requestUri.User), requestUri, localEndPoint);
        }

        /// <summary>
        /// The BuildSipIntContactHeader
        /// </summary>
        /// <param name="contactUri">The contactUri<see cref="SIPURI"/></param>
        /// <returns>The <see cref="SIPContactHeader"/></returns>
        private static SIPContactHeader BuildSipIntContactHeader(SIPURI contactUri) {
            return new SIPContactHeader(null, contactUri);
        }

        /// <summary>
        /// The ResolveSipIntContactUser
        /// </summary>
        /// <param name="user">The user<see cref="string?"/></param>
        /// <param name="requestUri">The requestUri<see cref="SIPURI"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string ResolveSipIntContactUser(string? user, SIPURI requestUri) {
            return FirstContactUser(user, requestUri.User) ?? "unknown";
        }

        /// <summary>
        /// The FirstContactUser
        /// </summary>
        /// <param name="values">The values<see cref="string?[]"/></param>
        /// <returns>The <see cref="string?"/></returns>
        private static string? FirstContactUser(params string?[] values) {
            foreach (string? value in values) {
                string? normalized = NormalizeSipUser(value);
                if (!string.IsNullOrWhiteSpace(normalized)) {
                    return normalized;
                }
            }
            return null;
        }

        /// <summary>
        /// The NormalizeSipUser
        /// </summary>
        /// <param name="value">The value<see cref="string?"/></param>
        /// <returns>The <see cref="string?"/></returns>
        private static string? NormalizeSipUser(string? value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return null;
            }

            string user = value.Trim();
            int semicolon = user.IndexOf(';');
            if (semicolon >= 0) {
                user = user[..semicolon];
            }
            return Uri.UnescapeDataString(user);
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

            string trimmed = value.Trim();
            int semicolon = trimmed.IndexOf(';');
            if (semicolon >= 0) {
                trimmed = trimmed[..semicolon];
            }
            return Uri.UnescapeDataString(trimmed);
        }

        /// <summary>
        /// The EnsureSipIntLegALocalTag
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string EnsureSipIntLegALocalTag(SIP_ServerSession session) {
            if (string.IsNullOrWhiteSpace(session.LegA.ToTag)) {
                session.LegA.ToTag = Guid.NewGuid().ToString("N");
            }
            return session.LegA.ToTag;
        }

        /// <summary>
        /// The ApplySipIntLegAInviteDialogHeaders
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="response">The response<see cref="SIPResponse"/></param>
        /// <param name="requestUri">The requestUri<see cref="SIPURI"/></param>
        private static void ApplySipIntLegAInviteDialogHeaders(SIP_ServerSession session, SIPResponse response, SIPURI requestUri) {
            if (response.Header?.CSeqMethod != SIPMethodsEnum.INVITE) {
                return;
            }

            if (response.StatusCode != (int)SIPResponseStatusCodesEnum.Trying && response.Header.To != null) {
                response.Header.To.ToTag = EnsureSipIntLegALocalTag(session);
            }

            if (response.StatusCode >= 200 && response.StatusCode < 300 && session.LegA.LocalSipEndPoint != null) {
                response.Header.Contact = new List<SIPContactHeader> { BuildSipIntContactHeader(BuildSipIntLegAContactUri(session, requestUri, session.LegA.LocalSipEndPoint)) };
            }
        }

        /// <summary>
        /// The CopyResponseContent
        /// </summary>
        /// <param name="source">The source<see cref="SIPResponse"/></param>
        /// <param name="dest">The dest<see cref="SIPResponse"/></param>
        private static void CopyResponseContent(SIPResponse source, SIPResponse dest) {
            dest.Body = source.Body;
            dest.Header.ContentType = source.Header?.ContentType;
            dest.Header.ContentLength = dest.Body?.Length ?? 0;
        }

        /// <summary>
        /// The CloneToHeader
        /// </summary>
        /// <param name="source">The source<see cref="SIPToHeader"/></param>
        /// <returns>The <see cref="SIPToHeader"/></returns>
        private static SIPToHeader CloneToHeader(SIPToHeader source) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return SIPToHeader.ParseToHeader(source.ToString());
        }

        /// <summary>
        /// The CloneFromHeader
        /// </summary>
        /// <param name="source">The source<see cref="SIPFromHeader"/></param>
        /// <returns>The <see cref="SIPFromHeader"/></returns>
        private static SIPFromHeader CloneFromHeader(SIPFromHeader source) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return SIPFromHeader.ParseFromHeader(source.ToString());
        }

        /// <summary>
        /// Defines the <see cref="LegACommands" />
        /// </summary>
        internal sealed class LegACommands {
            /// <summary>
            /// Defines the _logger
            /// </summary>
            private readonly TKBase_LogManager? _logger;
            private readonly string _areaName;

            /// <summary>
            /// Initializes a new instance of the <see cref="LegACommands"/> class.
            /// </summary>
            /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
            /// <param name="areaName">The areaName<see cref="string"/></param>
            public LegACommands(TKBase_LogManager? logger = null, string areaName = "SipInt") {
                _logger = logger;
                _areaName = string.IsNullOrWhiteSpace(areaName) ? "SipInt" : areaName.Trim();
            }

            /// <summary>
            /// The ExtractFromNumber
            /// </summary>
            /// <param name="request">The request<see cref="SIPRequest"/></param>
            /// <returns>The <see cref="string"/></returns>
            public string ExtractFromNumber(SIPRequest request) {
                return DecodeSipUserPart(request.Header?.From?.FromURI?.User);
            }

            /// <summary>
            /// The ExtractToNumber
            /// </summary>
            /// <param name="request">The request<see cref="SIPRequest"/></param>
            /// <returns>The <see cref="string"/></returns>
            public string ExtractToNumber(SIPRequest request) {
                return DecodeSipUserPart(request.Header?.To?.ToURI?.User ?? request.URI?.User);
            }

            /// <summary>
            /// The StoreInviteReceived
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="invite">The invite<see cref="SIPRequest"/></param>
            /// <param name="remoteEndPoint">The remoteEndPoint<see cref="SIPEndPoint"/></param>
            /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint?"/></param>
            public void StoreInviteReceived(SIP_ServerSession session, SIPRequest invite, SIPEndPoint remoteEndPoint, SIPEndPoint? localEndPoint) {
                Require(session, invite);
                session.LegA.InviteReceived = invite.Copy();
                session.LegA.InviteReceivedRaw = invite.ToString();
                session.LegA.LastRequestReceived = session.LegA.InviteReceived;
                session.LegA.LastRequestReceivedRaw = session.LegA.InviteReceivedRaw;
                session.LegA.SipEndPoint = remoteEndPoint;
                session.LegA.LocalSipEndPoint = localEndPoint;
                session.LegA.CallId = invite.Header?.CallId ?? session.LegA.CallId ?? session.SessionId;
                session.LegA.RequestUri = invite.URI;
                session.LegA.FromTag = invite.Header?.From?.FromTag ?? session.LegA.FromTag;
                session.LegA.ToTag = invite.Header?.To?.ToTag ?? session.LegA.ToTag;
                session.LegA.InviteSeen = true;
                session.LegA.UpdatedAt = DateTime.Now;
                _logger?.Debug($"{_areaName} LegA INVITE IN gespeichert | Ebene=Command | SessionID={session.SessionId} | CallID={session.LegA.CallId} | Local={localEndPoint?.ToString() ?? "-"} | Remote={remoteEndPoint} | URI={invite.URI} | From={invite.Header?.From} | To={invite.Header?.To} | CSeq={invite.Header?.CSeq} {invite.Header?.CSeqMethod} | BodyLength={invite.Body?.Length ?? 0} | RawLength={session.LegA.InviteReceivedRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START {_areaName} LegA INVITE IN ---{Environment.NewLine}{session.LegA.InviteReceivedRaw}{Environment.NewLine}--- SIP RAW END {_areaName} LegA INVITE IN ---");
            }

            /// <summary>
            /// The StoreAckReceived
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="ack">The ack<see cref="SIPRequest"/></param>
            /// <param name="remoteEndPoint">The remoteEndPoint<see cref="SIPEndPoint"/></param>
            /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint?"/></param>
            public void StoreAckReceived(SIP_ServerSession session, SIPRequest ack, SIPEndPoint remoteEndPoint, SIPEndPoint? localEndPoint) {
                Require(session, ack);
                session.LegA.AckReceived = ack.Copy();
                session.LegA.AckReceivedRaw = ack.ToString();
                session.LegA.LastRequestReceived = session.LegA.AckReceived;
                session.LegA.LastRequestReceivedRaw = session.LegA.AckReceivedRaw;
                session.LegA.SipEndPoint = remoteEndPoint;
                session.LegA.LocalSipEndPoint = localEndPoint;
                session.LegA.CallId = ack.Header?.CallId ?? session.LegA.CallId ?? session.SessionId;
                session.LegA.AckSeen = true;
                session.LegA.UpdatedAt = DateTime.Now;
                _logger?.Debug($"{_areaName} LegA ACK IN gespeichert | Ebene=Command | SessionID={session.SessionId} | CallID={session.LegA.CallId} | Local={localEndPoint?.ToString() ?? "-"} | Remote={remoteEndPoint} | URI={ack.URI} | From={ack.Header?.From} | To={ack.Header?.To} | CSeq={ack.Header?.CSeq} {ack.Header?.CSeqMethod} | RawLength={session.LegA.AckReceivedRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START {_areaName} LegA ACK IN ---{Environment.NewLine}{session.LegA.AckReceivedRaw}{Environment.NewLine}--- SIP RAW END {_areaName} LegA ACK IN ---");
            }

            /// <summary>
            /// The StoreByeReceived
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="bye">The bye<see cref="SIPRequest"/></param>
            /// <param name="remoteEndPoint">The remoteEndPoint<see cref="SIPEndPoint"/></param>
            /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint?"/></param>
            public void StoreByeReceived(SIP_ServerSession session, SIPRequest bye, SIPEndPoint remoteEndPoint, SIPEndPoint? localEndPoint) {
                Require(session, bye);
                session.LegA.ByeReceived = bye.Copy();
                session.LegA.ByeReceivedRaw = bye.ToString();
                session.LegA.LastRequestReceived = session.LegA.ByeReceived;
                session.LegA.LastRequestReceivedRaw = session.LegA.ByeReceivedRaw;
                session.LegA.SipEndPoint = remoteEndPoint;
                session.LegA.LocalSipEndPoint = localEndPoint;
                session.LegA.CallId = bye.Header?.CallId ?? session.LegA.CallId ?? session.SessionId;
                session.LegA.ByeSeen = true;
                session.LegA.IsEnded = true;
                session.LegA.UpdatedAt = DateTime.Now;
                _logger?.Debug($"{_areaName} LegA BYE IN gespeichert | Ebene=Command | SessionID={session.SessionId} | CallID={session.LegA.CallId} | Local={localEndPoint?.ToString() ?? "-"} | Remote={remoteEndPoint} | URI={bye.URI} | From={bye.Header?.From} | To={bye.Header?.To} | CSeq={bye.Header?.CSeq} {bye.Header?.CSeqMethod} | RawLength={session.LegA.ByeReceivedRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START {_areaName} LegA BYE IN ---{Environment.NewLine}{session.LegA.ByeReceivedRaw}{Environment.NewLine}--- SIP RAW END {_areaName} LegA BYE IN ---");
            }

            /// <summary>
            /// The StoreByeSent
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="bye">The bye<see cref="SIPRequest"/></param>
            /// <param name="targetEndPoint">The targetEndPoint<see cref="SIPEndPoint"/></param>
            /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint?"/></param>
            public void StoreByeSent(SIP_ServerSession session, SIPRequest bye, SIPEndPoint targetEndPoint, SIPEndPoint? localEndPoint) {
                Require(session, bye);
                session.LegA.ByeSent = bye.Copy();
                session.LegA.ByeSentRaw = bye.ToString();
                session.LegA.LastRequestSent = session.LegA.ByeSent;
                session.LegA.LastRequestSentRaw = session.LegA.ByeSentRaw;
                session.LegA.SipEndPoint = targetEndPoint;
                session.LegA.LocalSipEndPoint = localEndPoint;
                session.LegA.CallId = bye.Header?.CallId ?? session.LegA.CallId ?? session.SessionId;
                session.LegA.ByeSeen = true;
                session.LegA.IsEnded = true;
                session.LegA.UpdatedAt = DateTime.Now;
                _logger?.Debug($"{_areaName} LegA BYE OUT gespeichert | Ebene=Command | SessionID={session.SessionId} | CallID={session.LegA.CallId} | Local={localEndPoint?.ToString() ?? "-"} | Target={targetEndPoint} | URI={bye.URI} | From={bye.Header?.From} | To={bye.Header?.To} | CSeq={bye.Header?.CSeq} {bye.Header?.CSeqMethod} | RawLength={session.LegA.ByeSentRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START {_areaName} LegA BYE OUT ---{Environment.NewLine}{session.LegA.ByeSentRaw}{Environment.NewLine}--- SIP RAW END {_areaName} LegA BYE OUT ---");
            }

            /// <summary>
            /// The StoreCancelReceived
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="cancel">The cancel<see cref="SIPRequest"/></param>
            /// <param name="remoteEndPoint">The remoteEndPoint<see cref="SIPEndPoint"/></param>
            /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint?"/></param>
            public void StoreCancelReceived(SIP_ServerSession session, SIPRequest cancel, SIPEndPoint remoteEndPoint, SIPEndPoint? localEndPoint) {
                Require(session, cancel);
                session.LegA.CancelReceived = cancel.Copy();
                session.LegA.CancelReceivedRaw = cancel.ToString();
                session.LegA.LastRequestReceived = session.LegA.CancelReceived;
                session.LegA.LastRequestReceivedRaw = session.LegA.CancelReceivedRaw;
                session.LegA.SipEndPoint = remoteEndPoint;
                session.LegA.LocalSipEndPoint = localEndPoint;
                session.LegA.CallId = cancel.Header?.CallId ?? session.LegA.CallId ?? session.SessionId;
                session.LegA.CancelSeen = true;
                session.LegA.IsEnded = true;
                session.LegA.UpdatedAt = DateTime.Now;
                _logger?.Debug($"{_areaName} LegA CANCEL IN gespeichert | Ebene=Command | SessionID={session.SessionId} | CallID={session.LegA.CallId} | Local={localEndPoint?.ToString() ?? "-"} | Remote={remoteEndPoint} | URI={cancel.URI} | From={cancel.Header?.From} | To={cancel.Header?.To} | CSeq={cancel.Header?.CSeq} {cancel.Header?.CSeqMethod} | RawLength={session.LegA.CancelReceivedRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START {_areaName} LegA CANCEL IN ---{Environment.NewLine}{session.LegA.CancelReceivedRaw}{Environment.NewLine}--- SIP RAW END {_areaName} LegA CANCEL IN ---");
            }

            /// <summary>
            /// The StoreResponseSent
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="response">The response<see cref="SIPResponse"/></param>
            public void StoreResponseSent(SIP_ServerSession session, SIPResponse response) {
                if (session == null) throw new ArgumentNullException(nameof(session));
                if (response == null) throw new ArgumentNullException(nameof(response));
                session.LegA.LastResponseSent = response;
                session.LegA.LastResponseSentRaw = response.ToString();
                session.LegA.CallId = response.Header?.CallId ?? session.LegA.CallId ?? session.SessionId;
                session.LegA.ToTag = response.Header?.To?.ToTag ?? session.LegA.ToTag;
                if (response.StatusCode >= 100 && response.StatusCode < 200) {
                    session.LegA.LastProvisionalResponse = response;
                    session.LastProvisionalResponse = response;
                }
                if (response.StatusCode >= 200 && response.StatusCode < 300) {
                    session.LegA.LastSuccessResponse = response;
                    session.LastSuccessResponse = response;
                }
                if (response.StatusCode >= 300) {
                    session.LegA.IsEnded = true;
                    session.WaitingForFinalInviteAck = true;
                }
                session.LegA.UpdatedAt = DateTime.Now;
                _logger?.Debug($"{_areaName} LegA RESPONSE OUT gespeichert | Ebene=Command | SessionID={session.SessionId} | Status={response.StatusCode} {response.ReasonPhrase} | CallID={session.LegA.CallId} | CSeq={response.Header?.CSeq} {response.Header?.CSeqMethod} | BodyLength={response.Body?.Length ?? 0} | RawLength={session.LegA.LastResponseSentRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START {_areaName} LegA RESPONSE OUT ---{Environment.NewLine}{session.LegA.LastResponseSentRaw}{Environment.NewLine}--- SIP RAW END {_areaName} LegA RESPONSE OUT ---");
            }

            /// <summary>
            /// The BuildResponseFromInvite
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="status">The status<see cref="SIPResponseStatusCodesEnum"/></param>
            /// <param name="reason">The reason<see cref="string?"/></param>
            /// <param name="bodySource">The bodySource<see cref="SIPResponse?"/></param>
            /// <returns>The <see cref="SIPResponse"/></returns>
            public SIPResponse BuildResponseFromInvite(SIP_ServerSession session, SIPResponseStatusCodesEnum status, string? reason, SIPResponse? bodySource = null) {
                if (session == null) throw new ArgumentNullException(nameof(session));
                SIPRequest invite = session.LegA.InviteReceived ?? throw new InvalidOperationException("LegA INVITE fehlt.");
                SIPResponse response = SIPResponse.GetResponse(invite, status, reason);
                if (bodySource != null) {
                    CopyResponseContent(bodySource, response);
                }
                ApplySipIntLegAInviteDialogHeaders(session, response, invite.URI);
                return response;
            }

            /// <summary>
            /// The BuildResponseFromInvite
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="status">The status<see cref="SIPResponseStatusCodesEnum"/></param>
            /// <param name="reason">The reason<see cref="string?"/></param>
            /// <param name="body">The body<see cref="string?"/></param>
            /// <param name="contentType">The contentType<see cref="string?"/></param>
            /// <returns>The <see cref="SIPResponse"/></returns>
            public SIPResponse BuildResponseFromInvite(SIP_ServerSession session, SIPResponseStatusCodesEnum status, string? reason, string? body, string? contentType) {
                if (session == null) throw new ArgumentNullException(nameof(session));
                SIPRequest invite = session.LegA.InviteReceived ?? throw new InvalidOperationException("LegA INVITE fehlt.");
                SIPResponse response = SIPResponse.GetResponse(invite, status, reason);
                response.Body = body;
                response.Header.ContentType = contentType;
                response.Header.ContentLength = string.IsNullOrEmpty(body) ? 0 : body.Length;
                ApplySipIntLegAInviteDialogHeaders(session, response, invite.URI);
                return response;
            }

            /// <summary>
            /// The StoreResponseReceived
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="response">The response<see cref="SIPResponse"/></param>
            /// <param name="remoteEndPoint">The remoteEndPoint<see cref="SIPEndPoint"/></param>
            /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint?"/></param>
            public void StoreResponseReceived(SIP_ServerSession session, SIPResponse response, SIPEndPoint remoteEndPoint, SIPEndPoint? localEndPoint) {
                if (session == null) throw new ArgumentNullException(nameof(session));
                if (response == null) throw new ArgumentNullException(nameof(response));
                session.LegA.LastResponseReceived = response;
                session.LegA.LastResponseReceivedRaw = response.ToString();
                session.LegA.SipEndPoint = remoteEndPoint;
                session.LegA.LocalSipEndPoint = localEndPoint;
                session.LegA.CallId = response.Header?.CallId ?? session.LegA.CallId ?? session.SessionId;
                session.LegA.UpdatedAt = DateTime.Now;
                _logger?.Debug($"{_areaName} LegA RESPONSE IN gespeichert | Ebene=Command | SessionID={session.SessionId} | Status={response.StatusCode} {response.ReasonPhrase} | CallID={session.LegA.CallId} | Local={localEndPoint?.ToString() ?? "-"} | Remote={remoteEndPoint} | CSeq={response.Header?.CSeq} {response.Header?.CSeqMethod} | BodyLength={response.Body?.Length ?? 0} | RawLength={session.LegA.LastResponseReceivedRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START {_areaName} LegA RESPONSE IN ---{Environment.NewLine}{session.LegA.LastResponseReceivedRaw}{Environment.NewLine}--- SIP RAW END {_areaName} LegA RESPONSE IN ---");
            }

            /// <summary>
            /// The BuildDialogRequest
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="method">The method<see cref="SIPMethodsEnum"/></param>
            /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint"/></param>
            /// <returns>The <see cref="SIPRequest"/></returns>
            public SIPRequest BuildDialogRequest(SIP_ServerSession session, SIPMethodsEnum method, SIPEndPoint localEndPoint) {
                if (session == null) throw new ArgumentNullException(nameof(session));
                SIPRequest dialogInvite = session.LegA.InviteReceived ?? throw new InvalidOperationException("LegA INVITE fehlt.");
                SIPResponse? dialogSuccess = session.LegA.LastSuccessResponse;
                SIPURI requestUri = session.LegA.ContactUri ?? dialogInvite.Header.Contact?.FirstOrDefault()?.ContactURI ?? dialogInvite.URI;
                string localTag = session.LegA.ToTag ?? dialogSuccess?.Header?.To?.ToTag ?? EnsureSipIntLegALocalTag(session);
                SIPFromHeader fromHeader = new SIPFromHeader(dialogInvite.Header.To?.ToName, dialogInvite.Header.To?.ToURI ?? dialogInvite.URI, localTag);
                SIPToHeader toHeader = new SIPToHeader(dialogInvite.Header.From?.FromName, dialogInvite.Header.From?.FromURI ?? dialogInvite.URI, dialogInvite.Header.From?.FromTag);
                int cseq = method == SIPMethodsEnum.CANCEL ? dialogInvite.Header.CSeq : ++session.LegA.DialogCSeq;

                SIPRequest request = SIPRequest.GetRequest(method, requestUri, toHeader, fromHeader);
                request.Header.CallId = dialogInvite.Header.CallId;
                request.Header.CSeq = cseq;
                request.Header.CSeqMethod = method;
                request.Header.MaxForwards = 70;
                request.Body = null;
                request.Header.ContentType = null;
                request.Header.ContentLength = 0;
                request.Header.Contact = new List<SIPContactHeader> { BuildSipIntContactHeader(BuildSipIntLegAContactUri(session, requestUri, localEndPoint)) };
                request.Header.Vias = new SIPViaSet();
                request.Header.Vias.PushViaHeader(new SIPViaHeader(localEndPoint, SIPConstants.SIP_BRANCH_MAGICCOOKIE + Guid.NewGuid().ToString("N")));
                request.SetSendFromHints(localEndPoint);
                return request;
            }

            /// <summary>
            /// The MarkEnded
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="reason">The reason<see cref="string?"/></param>
            public void MarkEnded(SIP_ServerSession session, string? reason = null) {
                if (session == null) throw new ArgumentNullException(nameof(session));
                session.LegA.IsEnded = true;
                session.LegA.UpdatedAt = DateTime.Now;
                session.UpdatedAt = DateTime.Now;
                _logger?.Info($"{_areaName} LegA beendet | SessionID={session.SessionId} | Grund={reason}");
            }

            /// <summary>
            /// The Require
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="request">The request<see cref="SIPRequest"/></param>
            private static void Require(SIP_ServerSession session, SIPRequest request) {
                if (session == null) throw new ArgumentNullException(nameof(session));
                if (request == null) throw new ArgumentNullException(nameof(request));
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
            private readonly string _areaName;

            /// <summary>
            /// Initializes a new instance of the <see cref="LegBCommands"/> class.
            /// </summary>
            /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
            /// <param name="areaName">The areaName<see cref="string"/></param>
            public LegBCommands(TKBase_LogManager? logger = null, string areaName = "SipInt") {
                _logger = logger;
                _areaName = string.IsNullOrWhiteSpace(areaName) ? "SipInt" : areaName.Trim();
            }

            /// <summary>
            /// The BuildInternalInvite
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="originalInvite">The originalInvite<see cref="SIPRequest"/></param>
            /// <param name="route">The route<see cref="SIPIntResolvedTarget"/></param>
            /// <param name="destinationEndPoint">The destinationEndPoint<see cref="SIPEndPoint"/></param>
            /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint"/></param>
            /// <returns>The <see cref="SIPRequest"/></returns>
            public SIPRequest BuildInternalInvite(SIP_ServerSession session, SIPRequest originalInvite, SIPIntResolvedTarget route, SIPEndPoint destinationEndPoint, SIPEndPoint localEndPoint) {
                if (session == null) throw new ArgumentNullException(nameof(session));
                if (originalInvite == null) throw new ArgumentNullException(nameof(originalInvite));

                SIPURI destinationUri = !string.IsNullOrWhiteSpace(route.ContactUri)
                    ? SIPURI.ParseSIPURIRelaxed(route.ContactUri)
                    : BuildUserUri(route.CalledNumber ?? session.ToNumber, destinationEndPoint.Address.ToString(), destinationEndPoint.Port, destinationEndPoint.Protocol.ToString());

                string fromUser = NormalizeSipUser(originalInvite.Header?.From?.FromURI?.User) ?? session.FromNumber;
                string host = destinationUri.Host;
                SIPURI fromUri = BuildUserUri(fromUser, host);
                SIPURI toUri = BuildUserUri(route.CalledNumber ?? session.ToNumber, host);
                SIPFromHeader fromHeader = new SIPFromHeader(originalInvite.Header?.From?.FromName, fromUri, Guid.NewGuid().ToString("N"));
                SIPToHeader toHeader = new SIPToHeader(originalInvite.Header?.To?.ToName, toUri, null);

                SIPRequest outboundInvite = SIPRequest.GetRequest(SIPMethodsEnum.INVITE, destinationUri, toHeader, fromHeader);
                outboundInvite.SetSendFromHints(localEndPoint);
                outboundInvite.Header.Vias = new SIPViaSet();
                outboundInvite.Header.Vias.PushViaHeader(new SIPViaHeader(localEndPoint, SIPConstants.SIP_BRANCH_MAGICCOOKIE + Guid.NewGuid().ToString("N")));
                outboundInvite.Header.CSeq = originalInvite.Header?.CSeq ?? 1;
                outboundInvite.Header.CallId = !string.IsNullOrWhiteSpace(session.LegB.CallId) ? session.LegB.CallId : throw new InvalidOperationException($"{_areaName} LegB CallID fehlt.");
                outboundInvite.Header.MaxForwards = Math.Max(1, (originalInvite.Header?.MaxForwards ?? 70) - 1);
                outboundInvite.Header.Contact = new List<SIPContactHeader> { BuildSipIntContactHeader(BuildSipIntLegBContactUri(session, destinationUri, localEndPoint)) };
                outboundInvite.Body = originalInvite.Body;
                outboundInvite.Header.ContentType = originalInvite.Header?.ContentType;
                outboundInvite.Header.ContentLength = outboundInvite.Body?.Length ?? 0;
                session.LegB.DialogCSeq = (int)outboundInvite.Header.CSeq;

                session.LegB.LocalSipEndPoint = localEndPoint;
                session.LegB.RequestUri = destinationUri;
                session.LegB.ContactUri = outboundInvite.Header.Contact.First().ContactURI;
                session.LegB.RemoteTargetUri = destinationUri;
                return outboundInvite;
            }

            /// <summary>
            /// The StoreInviteSent
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="invite">The invite<see cref="SIPRequest"/></param>
            /// <param name="targetEndPoint">The targetEndPoint<see cref="SIPEndPoint"/></param>
            /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint?"/></param>
            public void StoreInviteSent(SIP_ServerSession session, SIPRequest invite, SIPEndPoint targetEndPoint, SIPEndPoint? localEndPoint) {
                Require(session, invite);
                session.LegB.InviteSent = invite.Copy();
                session.LegB.InviteSentRaw = invite.ToString();
                session.LegB.LastRequestSent = session.LegB.InviteSent;
                session.LegB.LastRequestSentRaw = session.LegB.InviteSentRaw;
                session.LegB.SipEndPoint = targetEndPoint;
                session.LegB.LocalSipEndPoint = localEndPoint;
                session.LegB.CallId = invite.Header?.CallId ?? session.LegB.CallId ?? session.SessionId;
                session.LegB.DialogCSeq = (int)(invite.Header?.CSeq ?? session.LegB.DialogCSeq);
                session.LegB.RequestUri = invite.URI;
                session.LegB.FromTag = invite.Header?.From?.FromTag ?? session.LegB.FromTag;
                session.LegB.ToTag = invite.Header?.To?.ToTag ?? session.LegB.ToTag;
                session.LegB.InviteSeen = true;
                session.LegB.UpdatedAt = DateTime.Now;
                _logger?.Debug($"{_areaName} LegB INVITE OUT gespeichert | Ebene=Command | SessionID={session.SessionId} | CallID={session.LegB.CallId} | Local={localEndPoint?.ToString() ?? "-"} | Target={targetEndPoint} | URI={invite.URI} | From={invite.Header?.From} | To={invite.Header?.To} | CSeq={invite.Header?.CSeq} {invite.Header?.CSeqMethod} | BodyLength={invite.Body?.Length ?? 0} | RawLength={session.LegB.InviteSentRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START {_areaName} LegB INVITE OUT ---{Environment.NewLine}{session.LegB.InviteSentRaw}{Environment.NewLine}--- SIP RAW END {_areaName} LegB INVITE OUT ---");
            }

            /// <summary>
            /// The StoreAckReceived
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="ack">The ack<see cref="SIPRequest"/></param>
            /// <param name="remoteEndPoint">The remoteEndPoint<see cref="SIPEndPoint"/></param>
            /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint?"/></param>
            public void StoreAckReceived(SIP_ServerSession session, SIPRequest ack, SIPEndPoint remoteEndPoint, SIPEndPoint? localEndPoint) {
                Require(session, ack);
                session.LegB.AckReceived = ack.Copy();
                session.LegB.AckReceivedRaw = ack.ToString();
                session.LegB.LastRequestReceived = session.LegB.AckReceived;
                session.LegB.LastRequestReceivedRaw = session.LegB.AckReceivedRaw;
                session.LegB.SipEndPoint = remoteEndPoint;
                session.LegB.LocalSipEndPoint = localEndPoint;
                session.LegB.CallId = ack.Header?.CallId ?? session.LegB.CallId ?? session.SessionId;
                session.LegB.AckSeen = true;
                session.LegB.UpdatedAt = DateTime.Now;
                _logger?.Debug($"{_areaName} LegB ACK IN gespeichert | Ebene=Command | SessionID={session.SessionId} | CallID={session.LegB.CallId} | Local={localEndPoint?.ToString() ?? "-"} | Remote={remoteEndPoint} | URI={ack.URI} | From={ack.Header?.From} | To={ack.Header?.To} | CSeq={ack.Header?.CSeq} {ack.Header?.CSeqMethod} | RawLength={session.LegB.AckReceivedRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START {_areaName} LegB ACK IN ---{Environment.NewLine}{session.LegB.AckReceivedRaw}{Environment.NewLine}--- SIP RAW END {_areaName} LegB ACK IN ---");
            }

            /// <summary>
            /// The StoreAckSent
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="ack">The ack<see cref="SIPRequest"/></param>
            /// <param name="targetEndPoint">The targetEndPoint<see cref="SIPEndPoint"/></param>
            /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint?"/></param>
            public void StoreAckSent(SIP_ServerSession session, SIPRequest ack, SIPEndPoint targetEndPoint, SIPEndPoint? localEndPoint) {
                Require(session, ack);
                session.LegB.AckSent = ack.Copy();
                session.LegB.AckSentRaw = ack.ToString();
                session.LegB.LastRequestSent = session.LegB.AckSent;
                session.LegB.LastRequestSentRaw = session.LegB.AckSentRaw;
                session.LegB.SipEndPoint = targetEndPoint;
                session.LegB.LocalSipEndPoint = localEndPoint;
                session.LegB.CallId = ack.Header?.CallId ?? session.LegB.CallId ?? session.SessionId;
                session.LegB.AckSeen = true;
                session.LegB.UpdatedAt = DateTime.Now;
                _logger?.Debug($"{_areaName} LegB ACK OUT gespeichert | Ebene=Command | SessionID={session.SessionId} | CallID={session.LegB.CallId} | Local={localEndPoint?.ToString() ?? "-"} | Target={targetEndPoint} | URI={ack.URI} | From={ack.Header?.From} | To={ack.Header?.To} | CSeq={ack.Header?.CSeq} {ack.Header?.CSeqMethod} | RawLength={session.LegB.AckSentRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START {_areaName} LegB ACK OUT ---{Environment.NewLine}{session.LegB.AckSentRaw}{Environment.NewLine}--- SIP RAW END {_areaName} LegB ACK OUT ---");
            }

            /// <summary>
            /// The BuildAckForConfirmedDialog
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="response">The response<see cref="SIPResponse"/></param>
            /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint"/></param>
            /// <returns>The <see cref="SIPRequest"/></returns>
            public SIPRequest BuildAckForConfirmedDialog(SIP_ServerSession session, SIPResponse response, SIPEndPoint localEndPoint) {
                if (session == null) throw new ArgumentNullException(nameof(session));
                if (response == null) throw new ArgumentNullException(nameof(response));
                SIPRequest invite = session.LegB.InviteSent ?? throw new InvalidOperationException("LegB INVITE fehlt.");
                SIPURI ackUri = response.Header?.Contact?.FirstOrDefault()?.ContactURI?.CopyOf()
                    ?? session.LegB.RemoteTargetUri?.CopyOf()
                    ?? invite.URI.CopyOf();
                SIPToHeader toHeader = response.Header?.To ?? invite.Header.To;
                SIPFromHeader fromHeader = invite.Header.From;
                SIPRequest ack = SIPRequest.GetRequest(SIPMethodsEnum.ACK, ackUri, toHeader, fromHeader);
                ack.Header.CallId = invite.Header.CallId;
                ack.Header.CSeq = response.Header?.CSeq ?? invite.Header.CSeq;
                ack.Header.CSeqMethod = SIPMethodsEnum.ACK;
                session.LegB.DialogCSeq = (int)ack.Header.CSeq;
                ack.Header.MaxForwards = 70;
                ack.Body = null;
                ack.Header.ContentType = null;
                ack.Header.ContentLength = 0;
                ack.Header.Contact = null;
                ack.Header.Vias = new SIPViaSet();
                ack.Header.Vias.PushViaHeader(new SIPViaHeader(localEndPoint, SIPConstants.SIP_BRANCH_MAGICCOOKIE + Guid.NewGuid().ToString("N")));
                ack.SetSendFromHints(localEndPoint);
                return ack;
            }

            /// <summary>
            /// The BuildAckForFailedInviteResponse
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="response">The response<see cref="SIPResponse"/></param>
            /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint"/></param>
            /// <returns>The <see cref="SIPRequest"/></returns>
            public SIPRequest BuildAckForFailedInviteResponse(SIP_ServerSession session, SIPResponse response, SIPEndPoint localEndPoint) {
                if (session == null) throw new ArgumentNullException(nameof(session));
                if (response == null) throw new ArgumentNullException(nameof(response));
                SIPRequest invite = session.LegB.InviteSent ?? throw new InvalidOperationException("LegB INVITE fehlt.");
                SIPRequest ack = SIPRequest.GetRequest(SIPMethodsEnum.ACK, invite.URI, response.Header?.To ?? invite.Header.To, invite.Header.From);
                ack.Header.CallId = invite.Header.CallId;
                ack.Header.CSeq = response.Header?.CSeq ?? invite.Header.CSeq;
                ack.Header.CSeqMethod = SIPMethodsEnum.ACK;
                ack.Header.MaxForwards = 70;
                ack.Body = null;
                ack.Header.ContentType = null;
                ack.Header.ContentLength = 0;
                ack.Header.Contact = null;
                ack.Header.Vias = new SIPViaSet();
                ack.Header.Vias.PushViaHeader(new SIPViaHeader(localEndPoint, SIPConstants.SIP_BRANCH_MAGICCOOKIE + Guid.NewGuid().ToString("N")));
                ack.SetSendFromHints(localEndPoint);
                return ack;
            }

            /// <summary>
            /// The BuildDialogRequest
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="method">The method<see cref="SIPMethodsEnum"/></param>
            /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint"/></param>
            /// <returns>The <see cref="SIPRequest"/></returns>
            public SIPRequest BuildDialogRequest(SIP_ServerSession session, SIPMethodsEnum method, SIPEndPoint localEndPoint) {
                if (session == null) throw new ArgumentNullException(nameof(session));
                SIPRequest dialogInvite = session.LegB.InviteSent ?? throw new InvalidOperationException("LegB INVITE fehlt.");

                if (method == SIPMethodsEnum.CANCEL) {
                    SIPRequest cancel = SIPRequest.GetRequest(SIPMethodsEnum.CANCEL, dialogInvite.URI, CloneToHeader(dialogInvite.Header.To), CloneFromHeader(dialogInvite.Header.From));
                    cancel.Header.CallId = dialogInvite.Header.CallId;
                    cancel.Header.CSeq = dialogInvite.Header.CSeq;
                    cancel.Header.CSeqMethod = SIPMethodsEnum.CANCEL;
                    cancel.Header.MaxForwards = 70;
                    cancel.Header.AuthenticationHeaders = dialogInvite.Header.AuthenticationHeaders;
                    cancel.Header.Routes = dialogInvite.Header.Routes;
                    cancel.Header.ProxySendFrom = dialogInvite.Header.ProxySendFrom;
                    cancel.Header.Vias = dialogInvite.Header.Vias;
                    cancel.Body = null;
                    cancel.Header.ContentType = null;
                    cancel.Header.ContentLength = 0;
                    cancel.SetSendFromHints(localEndPoint);
                    return cancel;
                }

                SIPResponse? dialogSuccess = session.LegB.LastSuccessResponse;
                SIPURI requestUri = session.LegB.RemoteTargetUri ?? dialogSuccess?.Header?.Contact?.FirstOrDefault()?.ContactURI ?? dialogInvite.URI;
                SIPFromHeader fromHeader = CloneFromHeader(dialogInvite.Header.From);
                SIPToHeader toHeader = dialogSuccess?.Header?.To != null ? CloneToHeader(dialogSuccess.Header.To) : CloneToHeader(dialogInvite.Header.To);
                SIPContactHeader? contact = dialogInvite.Header.Contact?.FirstOrDefault();

                SIPRequest request = SIPRequest.GetRequest(method, requestUri, toHeader, fromHeader);
                request.Header.CallId = dialogInvite.Header.CallId;
                request.Header.CSeq = ++session.LegB.DialogCSeq;
                request.Header.CSeqMethod = method;
                request.Header.MaxForwards = 70;
                request.Body = null;
                request.Header.ContentType = null;
                request.Header.ContentLength = 0;
                request.Header.Contact = contact == null
                    ? new List<SIPContactHeader> { BuildSipIntContactHeader(BuildSipIntLegBContactUri(session, requestUri, localEndPoint)) }
                    : new List<SIPContactHeader> { contact };
                request.Header.Vias = new SIPViaSet();
                request.Header.Vias.PushViaHeader(new SIPViaHeader(localEndPoint, SIPConstants.SIP_BRANCH_MAGICCOOKIE + Guid.NewGuid().ToString("N")));
                request.SetSendFromHints(localEndPoint);
                return request;
            }

            /// <summary>
            /// The StoreByeReceived
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="bye">The bye<see cref="SIPRequest"/></param>
            /// <param name="remoteEndPoint">The remoteEndPoint<see cref="SIPEndPoint"/></param>
            /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint?"/></param>
            public void StoreByeReceived(SIP_ServerSession session, SIPRequest bye, SIPEndPoint remoteEndPoint, SIPEndPoint? localEndPoint) {
                Require(session, bye);
                session.LegB.ByeReceived = bye.Copy();
                session.LegB.ByeReceivedRaw = bye.ToString();
                session.LegB.LastRequestReceived = session.LegB.ByeReceived;
                session.LegB.LastRequestReceivedRaw = session.LegB.ByeReceivedRaw;
                session.LegB.SipEndPoint = remoteEndPoint;
                session.LegB.LocalSipEndPoint = localEndPoint;
                session.LegB.CallId = bye.Header?.CallId ?? session.LegB.CallId ?? session.SessionId;
                session.LegB.ByeSeen = true;
                session.LegB.IsEnded = true;
                session.LegB.UpdatedAt = DateTime.Now;
                _logger?.Debug($"{_areaName} LegB BYE IN gespeichert | Ebene=Command | SessionID={session.SessionId} | CallID={session.LegB.CallId} | Local={localEndPoint?.ToString() ?? "-"} | Remote={remoteEndPoint} | URI={bye.URI} | From={bye.Header?.From} | To={bye.Header?.To} | CSeq={bye.Header?.CSeq} {bye.Header?.CSeqMethod} | RawLength={session.LegB.ByeReceivedRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START {_areaName} LegB BYE IN ---{Environment.NewLine}{session.LegB.ByeReceivedRaw}{Environment.NewLine}--- SIP RAW END {_areaName} LegB BYE IN ---");
            }

            /// <summary>
            /// The StoreByeSent
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="bye">The bye<see cref="SIPRequest"/></param>
            /// <param name="targetEndPoint">The targetEndPoint<see cref="SIPEndPoint"/></param>
            /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint?"/></param>
            public void StoreByeSent(SIP_ServerSession session, SIPRequest bye, SIPEndPoint targetEndPoint, SIPEndPoint? localEndPoint) {
                Require(session, bye);
                session.LegB.ByeSent = bye.Copy();
                session.LegB.ByeSentRaw = bye.ToString();
                session.LegB.LastRequestSent = session.LegB.ByeSent;
                session.LegB.LastRequestSentRaw = session.LegB.ByeSentRaw;
                session.LegB.SipEndPoint = targetEndPoint;
                session.LegB.LocalSipEndPoint = localEndPoint;
                session.LegB.CallId = bye.Header?.CallId ?? session.LegB.CallId ?? session.SessionId;
                session.LegB.ByeSeen = true;
                session.LegB.IsEnded = true;
                session.LegB.UpdatedAt = DateTime.Now;
                _logger?.Debug($"{_areaName} LegB BYE OUT gespeichert | Ebene=Command | SessionID={session.SessionId} | CallID={session.LegB.CallId} | Local={localEndPoint?.ToString() ?? "-"} | Target={targetEndPoint} | URI={bye.URI} | From={bye.Header?.From} | To={bye.Header?.To} | CSeq={bye.Header?.CSeq} {bye.Header?.CSeqMethod} | RawLength={session.LegB.ByeSentRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START {_areaName} LegB BYE OUT ---{Environment.NewLine}{session.LegB.ByeSentRaw}{Environment.NewLine}--- SIP RAW END {_areaName} LegB BYE OUT ---");
            }

            /// <summary>
            /// The StoreCancelSent
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="cancel">The cancel<see cref="SIPRequest"/></param>
            /// <param name="targetEndPoint">The targetEndPoint<see cref="SIPEndPoint"/></param>
            /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint?"/></param>
            public void StoreCancelSent(SIP_ServerSession session, SIPRequest cancel, SIPEndPoint targetEndPoint, SIPEndPoint? localEndPoint) {
                Require(session, cancel);
                session.LegB.CancelSent = cancel.Copy();
                session.LegB.CancelSentRaw = cancel.ToString();
                session.LegB.LastRequestSent = session.LegB.CancelSent;
                session.LegB.LastRequestSentRaw = session.LegB.CancelSentRaw;
                session.LegB.SipEndPoint = targetEndPoint;
                session.LegB.LocalSipEndPoint = localEndPoint;
                session.LegB.CallId = cancel.Header?.CallId ?? session.LegB.CallId ?? session.SessionId;
                session.LegB.CancelSeen = true;
                session.LegB.IsEnded = true;
                session.LegB.UpdatedAt = DateTime.Now;
                _logger?.Debug($"{_areaName} LegB CANCEL OUT gespeichert | Ebene=Command | SessionID={session.SessionId} | CallID={session.LegB.CallId} | Local={localEndPoint?.ToString() ?? "-"} | Target={targetEndPoint} | URI={cancel.URI} | From={cancel.Header?.From} | To={cancel.Header?.To} | CSeq={cancel.Header?.CSeq} {cancel.Header?.CSeqMethod} | RawLength={session.LegB.CancelSentRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START {_areaName} LegB CANCEL OUT ---{Environment.NewLine}{session.LegB.CancelSentRaw}{Environment.NewLine}--- SIP RAW END {_areaName} LegB CANCEL OUT ---");
            }

            /// <summary>
            /// The StoreResponseReceived
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="response">The response<see cref="SIPResponse"/></param>
            /// <param name="remoteEndPoint">The remoteEndPoint<see cref="SIPEndPoint"/></param>
            /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint?"/></param>
            public void StoreResponseReceived(SIP_ServerSession session, SIPResponse response, SIPEndPoint remoteEndPoint, SIPEndPoint? localEndPoint) {
                if (session == null) throw new ArgumentNullException(nameof(session));
                if (response == null) throw new ArgumentNullException(nameof(response));
                session.LegB.LastResponseReceived = response;
                session.LegB.LastResponseReceivedRaw = response.ToString();
                session.LegB.SipEndPoint = remoteEndPoint;
                session.LegB.LocalSipEndPoint = localEndPoint;
                session.LegB.CallId = response.Header?.CallId ?? session.LegB.CallId ?? session.SessionId;
                session.LegB.ToTag = response.Header?.To?.ToTag ?? session.LegB.ToTag;
                if (response.StatusCode >= 100 && response.StatusCode < 200) {
                    session.LegB.LastProvisionalResponse = response;
                    session.LastProvisionalResponse = response;
                }
                if (response.StatusCode >= 200 && response.StatusCode < 300 && response.Header?.CSeqMethod == SIPMethodsEnum.INVITE) {
                    session.LegB.LastSuccessResponse = response;
                    session.LastSuccessResponse = response;
                    session.LegB.RemoteTargetUri = response.Header?.Contact?.FirstOrDefault()?.ContactURI ?? session.LegB.RemoteTargetUri;
                }
                if (response.StatusCode >= 300) {
                    session.LegB.IsEnded = true;
                }
                session.LegB.UpdatedAt = DateTime.Now;
                _logger?.Debug($"{_areaName} LegB RESPONSE IN gespeichert | Ebene=Command | SessionID={session.SessionId} | Status={response.StatusCode} {response.ReasonPhrase} | CallID={session.LegB.CallId} | Local={localEndPoint?.ToString() ?? "-"} | Remote={remoteEndPoint} | CSeq={response.Header?.CSeq} {response.Header?.CSeqMethod} | BodyLength={response.Body?.Length ?? 0} | RawLength={session.LegB.LastResponseReceivedRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START {_areaName} LegB RESPONSE IN ---{Environment.NewLine}{session.LegB.LastResponseReceivedRaw}{Environment.NewLine}--- SIP RAW END {_areaName} LegB RESPONSE IN ---");
            }

            /// <summary>
            /// The MarkEnded
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="reason">The reason<see cref="string?"/></param>
            public void MarkEnded(SIP_ServerSession session, string? reason = null) {
                if (session == null) throw new ArgumentNullException(nameof(session));
                session.LegB.IsEnded = true;
                session.LegB.UpdatedAt = DateTime.Now;
                session.UpdatedAt = DateTime.Now;
                _logger?.Info($"{_areaName} LegB beendet | SessionID={session.SessionId} | Grund={reason}");
            }

            /// <summary>
            /// The Require
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="request">The request<see cref="SIPRequest"/></param>
            private static void Require(SIP_ServerSession session, SIPRequest request) {
                if (session == null) throw new ArgumentNullException(nameof(session));
                if (request == null) throw new ArgumentNullException(nameof(request));
            }
        }
    }
}
