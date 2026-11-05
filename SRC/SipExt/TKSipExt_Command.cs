/*
 * TKSipExt_Commands
 * Aufgabe:
 * - Führt externe SIP-/Provider-Kommandos für SipExt aus.
 * - Der Bereich hält Leg-A- und Leg-B-Kommandos getrennt.
 * - Keine Leg-A-Methode nutzt Leg-B-Methoden, keine Leg-B-Methode nutzt Leg-A-Methoden.
 */

namespace TKServer.SipExt {
    using SIPSorcery.SIP;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;

    using TKServer.BASE;
    /// TKSipExt_Commands
    /// Aufgabe:
    /// - Führt externe SIP-/Provider-Kommandos für SipExt aus.
    /// - Der Bereich hält Leg-A- und Leg-B-Kommandos getrennt.
    /// - Keine Leg-A-Methode nutzt Leg-B-Methoden, keine Leg-B-Methode nutzt Leg-A-Methoden.

    /// <summary>
    /// Defines the <see cref="TKSipExt_Commands" />
    /// </summary>
    internal sealed class TKSipExt_Commands {
        /// <summary>
        /// Gets the LegA
        /// </summary>
        public LegACommands LegA { get; }

        /// <summary>
        /// Gets the LegB
        /// </summary>
        public LegBCommands LegB { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TKSipExt_Commands"/> class.
        /// </summary>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        public TKSipExt_Commands(TKBase_LogManager? logger = null) {
            LegA = new LegACommands(logger);
            LegB = new LegBCommands(logger);
        }

        /// <summary>
        /// The ResolveRouteEndPoint
        /// </summary>
        /// <param name="route">The route<see cref="SIPIntResolvedTarget"/></param>
        /// <returns>The <see cref="SIPEndPoint"/></returns>
        public SIPEndPoint ResolveRouteEndPoint(SIPIntResolvedTarget route) {
            if (route == null || string.IsNullOrWhiteSpace(route.RemoteEndPoint)) {
                throw new InvalidOperationException("SipExt Route enthält keinen RemoteEndPoint.");
            }
            SIPEndPoint? parsed = SIPEndPoint.TryParse(route.RemoteEndPoint);
            if (parsed != null) {
                return parsed;
            }

            SIPURI uri = SIPURI.ParseSIPURIRelaxed(route.RemoteEndPoint);
            SIPEndPoint? endpoint = uri.ToSIPEndPoint();
            if (endpoint == null) {
                throw new InvalidOperationException($"SipExt RemoteEndPoint ungültig: {route.RemoteEndPoint}");
            }

            return endpoint;
        }

        /// <summary>
        /// The SameSipEndPoint
        /// </summary>
        /// <param name="a">The a<see cref="SIPEndPoint?"/></param>
        /// <param name="b">The b<see cref="SIPEndPoint?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public bool SameSipEndPoint(SIPEndPoint? a, SIPEndPoint? b) {
            if (a == null || b == null) return false;
            return a.Protocol == b.Protocol &&
                   Equals(a.Address, b.Address) &&
                   a.Port == b.Port;
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
            string safeUser = NormalizeSipUser(user) ?? "unknown";
            if (string.IsNullOrWhiteSpace(host)) {
                throw new InvalidOperationException("SIP-URI Host fehlt.");
            }

            string safeHost = NormalizeSipHost(host) ?? host.Trim();
            string hostPart = port.HasValue && port.Value > 0 && !safeHost.Contains(':') ? $"{safeHost}:{port.Value}" : safeHost;
            string scheme = string.Equals(transport, "tls", StringComparison.OrdinalIgnoreCase) ? "sips" : "sip";
            string transportPart = string.IsNullOrWhiteSpace(transport) || string.Equals(transport, "udp", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : $";transport={transport.Trim().ToLowerInvariant()}";
            return SIPURI.ParseSIPURIRelaxed($"{scheme}:{safeUser}@{hostPart}{transportPart}");
        }

        /// <summary>
        /// The BuildSipExtContactUri
        /// </summary>
        /// <param name="user">The user<see cref="string?"/></param>
        /// <param name="requestUri">The requestUri<see cref="SIPURI"/></param>
        /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="schemeOverride">The schemeOverride<see cref="SIPSchemesEnum?"/></param>
        /// <param name="advertisedHostOverride">The advertisedHostOverride<see cref="string?"/></param>
        /// <returns>The <see cref="SIPURI"/></returns>
        private static SIPURI BuildSipExtContactUri(string? user, SIPURI requestUri, SIPEndPoint localEndPoint, SIPSchemesEnum? schemeOverride = null, string? advertisedHostOverride = null) {
            string safeUser = Uri.EscapeDataString(FirstContactUser(user, requestUri.User) ?? "unknown");
            string schemeText = (schemeOverride ?? (localEndPoint.Protocol == SIPProtocolsEnum.tls ? SIPSchemesEnum.sips : SIPSchemesEnum.sip)) == SIPSchemesEnum.sips ? "sips" : "sip";
            string transport = localEndPoint.Protocol.ToString().ToLowerInvariant();
            string host = string.IsNullOrWhiteSpace(advertisedHostOverride) ? localEndPoint.Address.ToString() : advertisedHostOverride.Trim();
            string hostPart = FormatSipHost(host);
            return SIPURI.ParseSIPURIRelaxed($"{schemeText}:{safeUser}@{hostPart}:{localEndPoint.Port};transport={transport}");
        }

        private static string FormatSipHost(string host) {
            if (IPAddress.TryParse(host, out IPAddress? address) && address.AddressFamily == AddressFamily.InterNetworkV6) {
                return $"[{address}]";
            }

            return host;
        }

        /// <summary>
        /// The BuildSipExtLegAContactUri
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="requestUri">The requestUri<see cref="SIPURI"/></param>
        /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint"/></param>
        /// <returns>The <see cref="SIPURI"/></returns>
        private static SIPURI BuildSipExtLegAContactUri(SIP_ServerSession session, SIPURI requestUri, SIPEndPoint localEndPoint) {
            string? contactUser = session.RouteTargetKind == SIPIntRouteTargetKind.External
                ? FirstContactUser(session.ToNumber, requestUri.User)
                : FirstContactUser(session.ToUsername, session.ToNumber, requestUri.User);
            return BuildSipExtContactUri(contactUser, requestUri, localEndPoint);
        }

        /// <summary>
        /// The BuildSipExtInternalLegBContactUri
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="requestUri">The requestUri<see cref="SIPURI"/></param>
        /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint"/></param>
        /// <returns>The <see cref="SIPURI"/></returns>
        private static SIPURI BuildSipExtInternalLegBContactUri(SIP_ServerSession session, SIPURI requestUri, SIPEndPoint localEndPoint) {
            string? contactUser = session.RouteTargetKind == SIPIntRouteTargetKind.User
                ? FirstContactUser(session.FromNumber, requestUri.User)
                : FirstContactUser(session.FromUsername, session.FromNumber, requestUri.User);
            return BuildSipExtContactUri(contactUser, requestUri, localEndPoint);
        }

        /// <summary>
        /// The BuildSipExtProviderLegBContactUri
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="requestUri">The requestUri<see cref="SIPURI"/></param>
        /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="scheme">The scheme<see cref="SIPSchemesEnum"/></param>
        /// <returns>The <see cref="SIPURI"/></returns>
        private static SIPURI BuildSipExtProviderLegBContactUri(SIP_ServerSession session, SIPURI requestUri, SIPEndPoint localEndPoint, SIPSchemesEnum scheme) {
            string? providerAdvertisedHost = !string.IsNullOrWhiteSpace(session.LegB.MediaAdvertisedIp)
                ? session.LegB.MediaAdvertisedIp
                : null;
            return BuildSipExtContactUri(FirstContactUser(session.FromUsername, session.FromNumber, requestUri.User), requestUri, localEndPoint, scheme, providerAdvertisedHost);
        }

        /// <summary>
        /// The BuildSipExtContactHeader
        /// </summary>
        /// <param name="contactUri">The contactUri<see cref="SIPURI"/></param>
        /// <returns>The <see cref="SIPContactHeader"/></returns>
        private static SIPContactHeader BuildSipExtContactHeader(SIPURI contactUri) {
            return new SIPContactHeader(null, contactUri);
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
            if (string.IsNullOrWhiteSpace(value)) return null;
            string text = DecodeSipUserPart(ExtractSipUriText(value) ?? value.Trim());

            if (text.StartsWith("sip:", StringComparison.OrdinalIgnoreCase) || text.StartsWith("sips:", StringComparison.OrdinalIgnoreCase)) {
                try {
                    SIPURI uri = SIPURI.ParseSIPURIRelaxed(text);
                    text = uri.User ?? string.Empty;
                } catch {
                    if (text.StartsWith("sips:", StringComparison.OrdinalIgnoreCase)) text = text.Substring(5);
                    if (text.StartsWith("sip:", StringComparison.OrdinalIgnoreCase)) text = text.Substring(4);
                }
            } else if (text.StartsWith("tel:", StringComparison.OrdinalIgnoreCase)) {
                text = text.Substring(4);
            }

            int at = text.IndexOf('@');
            if (at >= 0) text = text.Substring(0, at);
            int semi = text.IndexOf(';');
            if (semi >= 0) text = text.Substring(0, semi);
            text = text.Trim().Trim('<', '>');
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        /// <summary>
        /// The NormalizeSipHost
        /// </summary>
        /// <param name="value">The value<see cref="string?"/></param>
        /// <returns>The <see cref="string?"/></returns>
        private static string? NormalizeSipHost(string? value) {
            if (string.IsNullOrWhiteSpace(value)) return null;
            string text = ExtractSipUriText(value) ?? value.Trim();

            if (text.StartsWith("sip:", StringComparison.OrdinalIgnoreCase) || text.StartsWith("sips:", StringComparison.OrdinalIgnoreCase)) {
                try {
                    SIPURI uri = SIPURI.ParseSIPURIRelaxed(text);
                    text = uri.HostAddress ?? uri.Host ?? string.Empty;
                } catch {
                    if (text.StartsWith("sips:", StringComparison.OrdinalIgnoreCase)) text = text.Substring(5);
                    if (text.StartsWith("sip:", StringComparison.OrdinalIgnoreCase)) text = text.Substring(4);
                }
            }

            int at = text.LastIndexOf('@');
            if (at >= 0) text = text.Substring(at + 1);
            int semi = text.IndexOf(';');
            if (semi >= 0) text = text.Substring(0, semi);
            text = text.Trim().Trim('<', '>');
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        /// <summary>
        /// The ParseOptionalSipUri
        /// </summary>
        /// <param name="value">The value<see cref="string?"/></param>
        /// <returns>The <see cref="SIPURI?"/></returns>
        private static SIPURI? ParseOptionalSipUri(string? value) {
            string? text = ExtractSipUriText(value);
            if (string.IsNullOrWhiteSpace(text)) return null;
            return SIPURI.ParseSIPURIRelaxed(text);
        }

        /// <summary>
        /// The CopyDialogUriWithTransport
        /// </summary>
        /// <param name="uri">The uri<see cref="SIPURI"/></param>
        /// <param name="protocol">The protocol<see cref="SIPProtocolsEnum"/></param>
        /// <returns>The <see cref="SIPURI"/></returns>
        private static SIPURI CopyDialogUriWithTransport(SIPURI uri, SIPProtocolsEnum protocol) {
            if (uri == null) throw new ArgumentNullException(nameof(uri));
            SIPURI copy = SIPURI.ParseSIPURIRelaxed(uri.ToString());
            copy.Parameters.Set("transport", protocol.ToString().ToLowerInvariant());
            return copy;
        }

        /// <summary>
        /// The ExtractSipUriText
        /// </summary>
        /// <param name="value">The value<see cref="string?"/></param>
        /// <returns>The <see cref="string?"/></returns>
        private static string? ExtractSipUriText(string? value) {
            if (string.IsNullOrWhiteSpace(value)) return null;
            string text = value.Trim();
            int start = text.IndexOf('<');
            int end = text.IndexOf('>', start + 1);
            if (start >= 0 && end > start) {
                text = text.Substring(start + 1, end - start - 1).Trim();
            }
            text = text.Trim().Trim('<', '>').Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        /// <summary>
        /// The DecodeSipUserPart
        /// </summary>
        /// <param name="value">The value<see cref="string?"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string DecodeSipUserPart(string? value) {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            try { return Uri.UnescapeDataString(value); } catch { return value ?? string.Empty; }
        }

        /// <summary>
        /// The EnsureSipExtLegALocalTag
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string EnsureSipExtLegALocalTag(SIP_ServerSession session) {
            if (string.IsNullOrWhiteSpace(session.LegA.ToTag)) {
                session.LegA.ToTag = Guid.NewGuid().ToString("N");
            }
            return session.LegA.ToTag;
        }

        /// <summary>
        /// The ApplySipExtLegAInviteDialogHeaders
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="response">The response<see cref="SIPResponse"/></param>
        /// <param name="requestUri">The requestUri<see cref="SIPURI"/></param>
        private static void ApplySipExtLegAInviteDialogHeaders(SIP_ServerSession session, SIPResponse response, SIPURI requestUri) {
            if (response.Header?.CSeqMethod != SIPMethodsEnum.INVITE) {
                return;
            }

            if (response.StatusCode != (int)SIPResponseStatusCodesEnum.Trying && response.Header.To != null) {
                response.Header.To.ToTag = EnsureSipExtLegALocalTag(session);
            }

            if (response.StatusCode >= 200 && response.StatusCode < 300 && session.LegA.LocalSipEndPoint != null) {
                response.Header.Contact = new List<SIPContactHeader> { BuildSipExtContactHeader(BuildSipExtLegAContactUri(session, requestUri, session.LegA.LocalSipEndPoint)) };
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
        /// The BuildInDialogRouteSet
        /// </summary>
        /// <param name="response">The response<see cref="SIPResponse?"/></param>
        /// <param name="originalInvite">The originalInvite<see cref="SIPRequest"/></param>
        /// <returns>The <see cref="SIPRouteSet?"/></returns>
        private static SIPRouteSet? BuildInDialogRouteSet(SIPResponse? response, SIPRequest originalInvite) {
            if (response?.Header?.RecordRoutes != null && response.Header.RecordRoutes.Length > 0) {
                return response.Header.RecordRoutes.Reversed();
            }

            return originalInvite.Header?.Routes;
        }

        /// <summary>
        /// The BuildLegBDialogToHeader
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="originalInvite">The originalInvite<see cref="SIPRequest"/></param>
        /// <param name="response">The response<see cref="SIPResponse?"/></param>
        /// <returns>The <see cref="SIPToHeader"/></returns>
        private static SIPToHeader BuildLegBDialogToHeader(SIP_ServerSession session, SIPRequest originalInvite, SIPResponse? response = null) {
            SIPToHeader toHeader = response?.Header?.To != null
                ? CloneToHeader(response.Header.To)
                : CloneToHeader(originalInvite.Header.To);

            string? toTag = response?.Header?.To?.ToTag ?? session.LegB.ToTag;
            if (!string.IsNullOrWhiteSpace(toTag)) {
                toHeader.ToTag = toTag;
            }

            return toHeader;
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
            public string ExtractFromNumber(SIPRequest request) {
                return NormalizeSipUser(request.Header?.From?.FromURI?.User) ?? NormalizeSipUser(request.Header?.From?.FromName) ?? string.Empty;
            }

            /// <summary>
            /// The ExtractToNumber
            /// </summary>
            /// <param name="request">The request<see cref="SIPRequest"/></param>
            /// <returns>The <see cref="string"/></returns>
            public string ExtractToNumber(SIPRequest request) {
                return NormalizeSipUser(request.Header?.To?.ToURI?.User) ?? NormalizeSipUser(request.URI?.User) ?? string.Empty;
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
                session.LegA.FromTag = invite.Header?.From?.FromTag;
                session.LegA.ToTag = invite.Header?.To?.ToTag;
                session.LegA.InviteSeen = true;
                session.LegA.UpdatedAt = DateTime.Now;
                _logger?.Debug($"SipExt LegA INVITE IN gespeichert | Ebene=Command | SessionID={session.SessionId} | CallID={session.LegA.CallId} | Local={localEndPoint?.ToString() ?? "-"} | Remote={remoteEndPoint} | URI={invite.URI} | From={invite.Header?.From} | To={invite.Header?.To} | CSeq={invite.Header?.CSeq} {invite.Header?.CSeqMethod} | BodyLength={invite.Body?.Length ?? 0} | RawLength={session.LegA.InviteReceivedRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START SipExt LegA INVITE IN ---{Environment.NewLine}{session.LegA.InviteReceivedRaw}{Environment.NewLine}--- SIP RAW END SipExt LegA INVITE IN ---");
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
                session.LegA.AckSeen = true;
                session.LegA.UpdatedAt = DateTime.Now;
                _logger?.Debug($"SipExt LegA ACK IN gespeichert | Ebene=Command | SessionID={session.SessionId} | CallID={session.LegA.CallId} | Local={localEndPoint?.ToString() ?? "-"} | Remote={remoteEndPoint} | URI={ack.URI} | From={ack.Header?.From} | To={ack.Header?.To} | CSeq={ack.Header?.CSeq} {ack.Header?.CSeqMethod} | RawLength={session.LegA.AckReceivedRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START SipExt LegA ACK IN ---{Environment.NewLine}{session.LegA.AckReceivedRaw}{Environment.NewLine}--- SIP RAW END SipExt LegA ACK IN ---");
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
                session.LegA.ByeSeen = true;
                session.LegA.IsEnded = true;
                session.LegA.UpdatedAt = DateTime.Now;
                _logger?.Debug($"SipExt LegA BYE IN gespeichert | Ebene=Command | SessionID={session.SessionId} | CallID={session.LegA.CallId} | Local={localEndPoint?.ToString() ?? "-"} | Remote={remoteEndPoint} | URI={bye.URI} | From={bye.Header?.From} | To={bye.Header?.To} | CSeq={bye.Header?.CSeq} {bye.Header?.CSeqMethod} | RawLength={session.LegA.ByeReceivedRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START SipExt LegA BYE IN ---{Environment.NewLine}{session.LegA.ByeReceivedRaw}{Environment.NewLine}--- SIP RAW END SipExt LegA BYE IN ---");
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
                session.LegA.IsEnded = true;
                session.LegA.UpdatedAt = DateTime.Now;
                _logger?.Debug($"SipExt LegA BYE OUT gespeichert | Ebene=Command | SessionID={session.SessionId} | CallID={session.LegA.CallId} | Local={localEndPoint?.ToString() ?? "-"} | Target={targetEndPoint} | URI={bye.URI} | From={bye.Header?.From} | To={bye.Header?.To} | CSeq={bye.Header?.CSeq} {bye.Header?.CSeqMethod} | RawLength={session.LegA.ByeSentRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START SipExt LegA BYE OUT ---{Environment.NewLine}{session.LegA.ByeSentRaw}{Environment.NewLine}--- SIP RAW END SipExt LegA BYE OUT ---");
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
                _logger?.Debug($"SipExt LegA CANCEL IN gespeichert | Ebene=Command | SessionID={session.SessionId} | CallID={session.LegA.CallId} | Local={localEndPoint?.ToString() ?? "-"} | Remote={remoteEndPoint} | URI={cancel.URI} | From={cancel.Header?.From} | To={cancel.Header?.To} | CSeq={cancel.Header?.CSeq} {cancel.Header?.CSeqMethod} | RawLength={session.LegA.CancelReceivedRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START SipExt LegA CANCEL IN ---{Environment.NewLine}{session.LegA.CancelReceivedRaw}{Environment.NewLine}--- SIP RAW END SipExt LegA CANCEL IN ---");
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
                _logger?.Debug($"SipExt LegA RESPONSE OUT gespeichert | Ebene=Command | SessionID={session.SessionId} | Status={response.StatusCode} {response.ReasonPhrase} | CallID={session.LegA.CallId} | CSeq={response.Header?.CSeq} {response.Header?.CSeqMethod} | BodyLength={response.Body?.Length ?? 0} | RawLength={session.LegA.LastResponseSentRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START SipExt LegA RESPONSE OUT ---{Environment.NewLine}{session.LegA.LastResponseSentRaw}{Environment.NewLine}--- SIP RAW END SipExt LegA RESPONSE OUT ---");
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
                SIPRequest invite = session.LegA.InviteReceived ?? throw new InvalidOperationException("SipExt LegA INVITE fehlt.");
                SIPResponse response = SIPResponse.GetResponse(invite, status, reason);
                if (bodySource != null) {
                    CopyResponseContent(bodySource, response);
                }
                ApplySipExtLegAInviteDialogHeaders(session, response, invite.URI);
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
                _logger?.Debug($"SipExt LegA RESPONSE IN gespeichert | Ebene=Command | SessionID={session.SessionId} | Status={response.StatusCode} {response.ReasonPhrase} | CallID={session.LegA.CallId} | Local={localEndPoint?.ToString() ?? "-"} | Remote={remoteEndPoint} | CSeq={response.Header?.CSeq} {response.Header?.CSeqMethod} | BodyLength={response.Body?.Length ?? 0} | RawLength={session.LegA.LastResponseReceivedRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START SipExt LegA RESPONSE IN ---{Environment.NewLine}{session.LegA.LastResponseReceivedRaw}{Environment.NewLine}--- SIP RAW END SipExt LegA RESPONSE IN ---");
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
                SIPRequest dialogInvite = session.LegA.InviteReceived ?? throw new InvalidOperationException("SipExt LegA INVITE fehlt.");
                SIPResponse? dialogSuccess = session.LegA.LastSuccessResponse;
                SIPURI remoteTargetUri = session.LegA.ContactUri ?? dialogInvite.Header.Contact?.FirstOrDefault()?.ContactURI ?? dialogInvite.URI;
                SIPProtocolsEnum remoteProtocol = session.LegA.SipEndPoint?.Protocol ?? remoteTargetUri.Protocol;
                SIPURI requestUri = CopyDialogUriWithTransport(remoteTargetUri, remoteProtocol);
                string localTag = session.LegA.ToTag ?? dialogSuccess?.Header?.To?.ToTag ?? EnsureSipExtLegALocalTag(session);
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
                request.Header.Contact = new List<SIPContactHeader> { BuildSipExtContactHeader(BuildSipExtLegAContactUri(session, requestUri, localEndPoint)) };
                if (method == SIPMethodsEnum.BYE && !string.IsNullOrWhiteSpace(session.LegB.ByeReceived?.Header?.Reason)) {
                    request.Header.Reason = session.LegB.ByeReceived.Header.Reason;
                }
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
                _logger?.Info($"SipExt LegA beendet | SessionID={session.SessionId} | Grund={reason}");
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

            /// <summary>
            /// Initializes a new instance of the <see cref="LegBCommands"/> class.
            /// </summary>
            /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
            public LegBCommands(TKBase_LogManager? logger = null) {
                _logger = logger;
            }


            /// <summary>
            /// The BuildProviderInvite
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="originalInvite">The originalInvite<see cref="SIPRequest"/></param>
            /// <param name="route">The route<see cref="SIPIntResolvedTarget"/></param>
            /// <param name="destinationEndPoint">The destinationEndPoint<see cref="SIPEndPoint"/></param>
            /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint"/></param>
            /// <returns>The <see cref="SIPRequest"/></returns>
            public SIPRequest BuildProviderInvite(SIP_ServerSession session, SIPRequest originalInvite, SIPIntResolvedTarget route, SIPEndPoint destinationEndPoint, SIPEndPoint localEndPoint) {
                if (session == null) throw new ArgumentNullException(nameof(session));
                if (originalInvite == null) throw new ArgumentNullException(nameof(originalInvite));

                SIPURI? routeUri = ParseOptionalSipUri(route.ContactUri);
                SIPURI? providerAssociatedUri = ParseOptionalSipUri(route.ProviderAssociatedUri);
                SIPRouteSet? providerRouteSet = null;
                if (!string.IsNullOrWhiteSpace(route.ProviderServiceRoute)) {
                    providerRouteSet = SIPRouteSet.ParseSIPRouteSet(route.ProviderServiceRoute);
                }

                string calledNumber = NormalizeSipUser(route.CalledNumber) ?? NormalizeSipUser(originalInvite.Header?.To?.ToURI?.User) ?? NormalizeSipUser(originalInvite.URI?.User) ?? string.Empty;
                string originalCallerUser = NormalizeSipUser(originalInvite.Header?.From?.FromURI?.User) ?? session.FromNumber;
                string providerFromUser = NormalizeSipUser(providerAssociatedUri?.User) ?? NormalizeSipUser(route.ProviderSipUser) ?? NormalizeSipUser(route.ProviderNumber) ?? originalCallerUser;
                string providerHost = NormalizeSipHost(providerAssociatedUri?.HostAddress) ?? NormalizeSipHost(providerAssociatedUri?.Host) ?? NormalizeSipHost(routeUri?.HostAddress) ?? NormalizeSipHost(routeUri?.Host) ?? destinationEndPoint.Address.ToString();

                SIPProtocolsEnum outboundProtocol = routeUri?.Protocol ?? destinationEndPoint.Protocol;
                SIPSchemesEnum outboundScheme = routeUri?.Scheme ?? (outboundProtocol == SIPProtocolsEnum.tls ? SIPSchemesEnum.sips : SIPSchemesEnum.sip);
                int providerPort = destinationEndPoint.Port;
                if (providerPort <= 0 || providerPort > IPEndPoint.MaxPort) {
                    throw new InvalidOperationException($"SipExt Provider-Zielport fehlt oder ist ungültig: {providerPort}");
                }

                SIPURI destinationUri = routeUri != null
                    ? SIPURI.ParseSIPURIRelaxed(routeUri.ToString())
                    : BuildUserUri(calledNumber, providerHost, providerPort, outboundProtocol.ToString());
                SIPURI fromUri = BuildUserUri(providerFromUser, providerHost);
                SIPURI toUri = BuildUserUri(calledNumber, providerHost);
                SIPURI localContactUri = BuildSipExtProviderLegBContactUri(session, destinationUri, localEndPoint, outboundScheme);

                SIPFromHeader fromHeader = new SIPFromHeader(originalInvite.Header?.From?.FromName, fromUri, Guid.NewGuid().ToString("N"));
                SIPToHeader toHeader = new SIPToHeader(originalInvite.Header?.To?.ToName, toUri, null);
                SIPRequest outboundInvite = SIPRequest.GetRequest(SIPMethodsEnum.INVITE, destinationUri, toHeader, fromHeader);
                outboundInvite.SetSendFromHints(localEndPoint);
                outboundInvite.Header.Vias = new SIPViaSet();
                outboundInvite.Header.Vias.PushViaHeader(new SIPViaHeader(localEndPoint, SIPConstants.SIP_BRANCH_MAGICCOOKIE + Guid.NewGuid().ToString("N")));
                outboundInvite.Header.CSeq = originalInvite.Header?.CSeq ?? 1;
                outboundInvite.Header.CallId = !string.IsNullOrWhiteSpace(session.LegB.CallId) ? session.LegB.CallId : throw new InvalidOperationException("SipExt LegB CallID fehlt.");
                outboundInvite.Header.MaxForwards = Math.Max(1, (originalInvite.Header?.MaxForwards ?? 70) - 1);
                outboundInvite.Header.Contact = new List<SIPContactHeader> { BuildSipExtContactHeader(localContactUri) };
                outboundInvite.Header.Routes = providerRouteSet;
                outboundInvite.Header.RecordRoutes = null;
                outboundInvite.Body = originalInvite.Body;
                outboundInvite.Header.ContentType = originalInvite.Header?.ContentType;
                outboundInvite.Header.ContentLength = outboundInvite.Body?.Length ?? 0;
                session.LegB.DialogCSeq = (int)outboundInvite.Header.CSeq;

                session.LegB.LocalSipEndPoint = localEndPoint;
                session.LegB.RequestUri = destinationUri;
                session.LegB.ContactUri = localContactUri;
                session.LegB.RemoteTargetUri = destinationUri;
                session.ProviderAccountId = route.ProviderAccountId;
                session.ProviderName = route.ProviderName;
                session.ProviderAuthUser = route.ProviderAuthUser;
                session.ProviderSipUser = route.ProviderSipUser;
                session.ProviderPassword = route.ProviderPassword;
                session.ProviderNumber = route.ProviderNumber;
                session.ProviderServiceRoute = route.ProviderServiceRoute;
                session.ProviderAssociatedUri = route.ProviderAssociatedUri;
                session.ProviderRequestUri = route.ProviderRequestUri;
                return outboundInvite;
            }

            /// <summary>
            /// The BuildAuthenticatedInvite
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="challengeResponse">The challengeResponse<see cref="SIPResponse"/></param>
            /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint"/></param>
            /// <returns>The <see cref="SIPRequest"/></returns>
            public SIPRequest BuildAuthenticatedInvite(SIP_ServerSession session, SIPResponse challengeResponse, SIPEndPoint localEndPoint) {
                if (session == null) throw new ArgumentNullException(nameof(session));
                if (challengeResponse == null) throw new ArgumentNullException(nameof(challengeResponse));
                SIPRequest original = session.LegB.InviteSent ?? throw new InvalidOperationException("SipExt LegB INVITE für Auth fehlt.");
                string user = !string.IsNullOrWhiteSpace(session.ProviderAuthUser) ? session.ProviderAuthUser! : (!string.IsNullOrWhiteSpace(session.ProviderSipUser) ? session.ProviderSipUser! : session.FromNumber);
                string password = session.ProviderPassword ?? string.Empty;
                if (string.IsNullOrWhiteSpace(password)) {
                    throw new InvalidOperationException("SipExt Provider-Passwort fehlt.");
                }

                List<SIPAuthenticationHeader> challenges = challengeResponse.Header?.AuthenticationHeaders ?? new List<SIPAuthenticationHeader>();
                if (challenges.Count == 0) {
                    throw new InvalidOperationException("SipExt Provider-Challenge fehlt.");
                }

                SIPRequest authInvite = original.DuplicateAndAuthenticate(challenges, user, password);
                authInvite.SetSendFromHints(localEndPoint);
                return authInvite;
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
                _logger?.Debug($"SipExt LegB INVITE OUT gespeichert | Ebene=Command | SessionID={session.SessionId} | CallID={session.LegB.CallId} | Local={localEndPoint?.ToString() ?? "-"} | Target={targetEndPoint} | URI={invite.URI} | From={invite.Header?.From} | To={invite.Header?.To} | CSeq={invite.Header?.CSeq} {invite.Header?.CSeqMethod} | BodyLength={invite.Body?.Length ?? 0} | RawLength={session.LegB.InviteSentRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START SipExt LegB INVITE OUT ---{Environment.NewLine}{session.LegB.InviteSentRaw}{Environment.NewLine}--- SIP RAW END SipExt LegB INVITE OUT ---");
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
                _logger?.Debug($"SipExt LegB ACK IN gespeichert | Ebene=Command | SessionID={session.SessionId} | CallID={session.LegB.CallId} | Local={localEndPoint?.ToString() ?? "-"} | Remote={remoteEndPoint} | URI={ack.URI} | From={ack.Header?.From} | To={ack.Header?.To} | CSeq={ack.Header?.CSeq} {ack.Header?.CSeqMethod} | RawLength={session.LegB.AckReceivedRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START SipExt LegB ACK IN ---{Environment.NewLine}{session.LegB.AckReceivedRaw}{Environment.NewLine}--- SIP RAW END SipExt LegB ACK IN ---");
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
                _logger?.Debug($"SipExt LegB ACK OUT gespeichert | Ebene=Command | SessionID={session.SessionId} | CallID={session.LegB.CallId} | Local={localEndPoint?.ToString() ?? "-"} | Target={targetEndPoint} | URI={ack.URI} | From={ack.Header?.From} | To={ack.Header?.To} | CSeq={ack.Header?.CSeq} {ack.Header?.CSeqMethod} | RawLength={session.LegB.AckSentRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START SipExt LegB ACK OUT ---{Environment.NewLine}{session.LegB.AckSentRaw}{Environment.NewLine}--- SIP RAW END SipExt LegB ACK OUT ---");
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
                SIPRequest originalInvite = session.LegB.InviteSent ?? throw new InvalidOperationException("SipExt LegB INVITE fehlt.");
                SIPURI requestUri = response.Header.Contact?.FirstOrDefault()?.ContactURI ?? session.LegB.RemoteTargetUri ?? originalInvite.URI;
                session.LegB.RemoteTargetUri = requestUri;
                session.LegB.ToHeader = response.Header?.To?.ToString() ?? session.LegB.ToHeader;
                session.LegB.ToTag = response.Header?.To?.ToTag ?? session.LegB.ToTag;
                session.LegB.DialogCSeq = (int)(originalInvite.Header?.CSeq ?? session.LegB.DialogCSeq);

                SIPToHeader toHeader = BuildLegBDialogToHeader(session, originalInvite, response);
                SIPFromHeader fromHeader = CloneFromHeader(originalInvite.Header.From);
                SIPRequest ack = SIPRequest.GetRequest(SIPMethodsEnum.ACK, requestUri, toHeader, fromHeader);
                ack.Header.CallId = originalInvite.Header.CallId;
                ack.Header.CSeq = originalInvite.Header.CSeq;
                ack.Header.CSeqMethod = SIPMethodsEnum.ACK;
                ack.Header.MaxForwards = 70;
                ack.Header.Contact = originalInvite.Header.Contact;
                ack.Header.AuthenticationHeaders = originalInvite.Header.AuthenticationHeaders;
                ack.Header.Routes = BuildInDialogRouteSet(response, originalInvite);
                ack.Header.ProxySendFrom = originalInvite.Header.ProxySendFrom;
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
                SIPRequest originalInvite = session.LegB.InviteSent ?? throw new InvalidOperationException("SipExt LegB INVITE fehlt.");
                SIPToHeader toHeader = response.Header?.To != null ? CloneToHeader(response.Header.To) : CloneToHeader(originalInvite.Header.To);
                SIPFromHeader fromHeader = CloneFromHeader(originalInvite.Header.From);
                SIPRequest ack = SIPRequest.GetRequest(SIPMethodsEnum.ACK, originalInvite.URI, toHeader, fromHeader);
                ack.Header.CallId = originalInvite.Header.CallId;
                ack.Header.CSeq = originalInvite.Header.CSeq;
                ack.Header.CSeqMethod = SIPMethodsEnum.ACK;
                ack.Header.MaxForwards = 70;
                ack.Header.AuthenticationHeaders = originalInvite.Header.AuthenticationHeaders;
                ack.Header.Routes = originalInvite.Header.Routes;
                ack.Header.ProxySendFrom = originalInvite.Header.ProxySendFrom;
                ack.Header.Vias = new SIPViaSet();
                string branch = originalInvite.Header?.Vias?.TopViaHeader?.Branch;
                ack.Header.Vias.PushViaHeader(new SIPViaHeader(localEndPoint, string.IsNullOrWhiteSpace(branch) ? SIPConstants.SIP_BRANCH_MAGICCOOKIE + Guid.NewGuid().ToString("N") : branch));
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
                SIPRequest dialogInvite = session.LegB.InviteSent ?? throw new InvalidOperationException("SipExt LegB INVITE fehlt.");

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
                SIPURI remoteTargetUri = session.LegB.RemoteTargetUri ?? session.LegB.ContactUri ?? dialogInvite.URI;
                SIPProtocolsEnum remoteProtocol = session.LegB.SipEndPoint?.Protocol ?? remoteTargetUri.Protocol;
                SIPURI requestUri = CopyDialogUriWithTransport(remoteTargetUri, remoteProtocol);
                SIPToHeader toHeader = BuildLegBDialogToHeader(session, dialogInvite, dialogSuccess);
                SIPFromHeader fromHeader = CloneFromHeader(dialogInvite.Header.From);
                SIPRequest request = SIPRequest.GetRequest(method, requestUri, toHeader, fromHeader);
                request.Header.CallId = dialogInvite.Header.CallId;
                request.Header.CSeq = ++session.LegB.DialogCSeq;
                request.Header.CSeqMethod = method;
                request.Header.MaxForwards = 70;
                request.Header.Contact = dialogInvite.Header.Contact;
                if (method == SIPMethodsEnum.BYE && !string.IsNullOrWhiteSpace(session.LegA.ByeReceived?.Header?.Reason)) {
                    request.Header.Reason = session.LegA.ByeReceived.Header.Reason;
                }
                request.Header.AuthenticationHeaders = dialogInvite.Header.AuthenticationHeaders;
                request.Header.Routes = BuildInDialogRouteSet(dialogSuccess, dialogInvite);
                request.Header.ProxySendFrom = dialogInvite.Header.ProxySendFrom;
                request.Body = null;
                request.Header.ContentType = null;
                request.Header.ContentLength = 0;
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
                session.LegB.ByeSeen = true;
                session.LegB.IsEnded = true;
                session.LegB.UpdatedAt = DateTime.Now;
                _logger?.Debug($"SipExt LegB BYE IN gespeichert | Ebene=Command | SessionID={session.SessionId} | CallID={session.LegB.CallId} | Local={localEndPoint?.ToString() ?? "-"} | Remote={remoteEndPoint} | URI={bye.URI} | From={bye.Header?.From} | To={bye.Header?.To} | CSeq={bye.Header?.CSeq} {bye.Header?.CSeqMethod} | RawLength={session.LegB.ByeReceivedRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START SipExt LegB BYE IN ---{Environment.NewLine}{session.LegB.ByeReceivedRaw}{Environment.NewLine}--- SIP RAW END SipExt LegB BYE IN ---");
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
                session.LegB.IsEnded = true;
                session.LegB.UpdatedAt = DateTime.Now;
                _logger?.Debug($"SipExt LegB BYE OUT gespeichert | Ebene=Command | SessionID={session.SessionId} | CallID={session.LegB.CallId} | Local={localEndPoint?.ToString() ?? "-"} | Target={targetEndPoint} | URI={bye.URI} | From={bye.Header?.From} | To={bye.Header?.To} | CSeq={bye.Header?.CSeq} {bye.Header?.CSeqMethod} | RawLength={session.LegB.ByeSentRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START SipExt LegB BYE OUT ---{Environment.NewLine}{session.LegB.ByeSentRaw}{Environment.NewLine}--- SIP RAW END SipExt LegB BYE OUT ---");
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
                _logger?.Debug($"SipExt LegB CANCEL OUT gespeichert | Ebene=Command | SessionID={session.SessionId} | CallID={session.LegB.CallId} | Local={localEndPoint?.ToString() ?? "-"} | Target={targetEndPoint} | URI={cancel.URI} | From={cancel.Header?.From} | To={cancel.Header?.To} | CSeq={cancel.Header?.CSeq} {cancel.Header?.CSeqMethod} | RawLength={session.LegB.CancelSentRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START SipExt LegB CANCEL OUT ---{Environment.NewLine}{session.LegB.CancelSentRaw}{Environment.NewLine}--- SIP RAW END SipExt LegB CANCEL OUT ---");
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
                if (response.Header?.CSeqMethod == SIPMethodsEnum.INVITE && response.Header.To != null) {
                    session.LegB.ToHeader = response.Header.To.ToString();
                    session.LegB.ToTag = response.Header.To.ToTag ?? session.LegB.ToTag;
                }
                if (response.StatusCode >= 100 && response.StatusCode < 200) {
                    session.LegB.LastProvisionalResponse = response;
                    session.LastProvisionalResponse = response;
                }
                if (response.StatusCode >= 200 && response.StatusCode < 300 && response.Header?.CSeqMethod == SIPMethodsEnum.INVITE) {
                    session.LegB.LastSuccessResponse = response;
                    session.LastSuccessResponse = response;
                    session.LegB.ToTag = response.Header?.To?.ToTag ?? session.LegB.ToTag;
                    if (response.Header?.Contact?.Count > 0) {
                        session.LegB.RemoteTargetUri = response.Header.Contact.First().ContactURI;
                    }
                }
                if (response.StatusCode >= 300) {
                    session.LegB.IsEnded = true;
                }
                session.LegB.UpdatedAt = DateTime.Now;
                _logger?.Debug($"SipExt LegB RESPONSE IN gespeichert | Ebene=Command | SessionID={session.SessionId} | Status={response.StatusCode} {response.ReasonPhrase} | CallID={session.LegB.CallId} | Local={localEndPoint?.ToString() ?? "-"} | Remote={remoteEndPoint} | CSeq={response.Header?.CSeq} {response.Header?.CSeqMethod} | BodyLength={response.Body?.Length ?? 0} | RawLength={session.LegB.LastResponseReceivedRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START SipExt LegB RESPONSE IN ---{Environment.NewLine}{session.LegB.LastResponseReceivedRaw}{Environment.NewLine}--- SIP RAW END SipExt LegB RESPONSE IN ---");
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
                _logger?.Info($"SipExt LegB beendet | SessionID={session.SessionId} | Grund={reason}");
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
