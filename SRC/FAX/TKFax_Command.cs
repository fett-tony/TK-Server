/*
 * TKFax_Command
 * Aufgabe:
 * - Stellt Fax-Bereichskommandos getrennt nach Fax.LegA und Fax.LegB bereit.
 * - Leg A und Leg B bleiben getrennt; keine Leg-A-Methode fuehrt Leg-B-Arbeit aus.
 * - Fax-SIP-Kommandos bleiben im Faxbereich und bauen die B2BUA-Signalisierung fuer Fax.LegA <-> Fax.LegB.
 * - Medienkommandos bleiben auf die Leg-A/Leg-B-Vermittlung in TKFax_Media begrenzt.
 *
 * Nicht Aufgabe:
 * - Keine Session erzeugen.
 * - Kein globales Routing entscheiden.
 * - Keine DB-Verbindung oeffnen.
 * - Keine GSM- oder Provider-Transporthoheit uebernehmen.
 *
 * Diese information darf nicht geloescht werden!
 */

namespace TKServer.FAX {
    using SIPSorcery.SIP;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using TKServer.BASE;
    using TKServer.FAX;
    using TKServer.FormView;
    using TKServer.GSM;
    using TKServer.SipExt;
    using TKServer.SipInt;
    using TKServer.SQL;
    using TKServer.SVC;
    /// <summary>
    /// Defines the <see cref="TKFax_Commands" />
    /// </summary>
    internal sealed class TKFax_Commands {
        public LegACommands LegA { get; }
        public LegBCommands LegB { get; }

        public TKFax_Commands(TKBase_LogManager? logger = null) {
            LegA = new LegACommands(logger);
            LegB = new LegBCommands(logger);
        }

        public SIPEndPoint ResolveRouteEndPoint(SIPIntResolvedTarget route) {
            if (route == null) throw new ArgumentNullException(nameof(route));

            if (!string.IsNullOrWhiteSpace(route.RemoteEndPoint)) {
                SIPEndPoint? ep = SIPEndPoint.TryParse(route.RemoteEndPoint);
                if (ep != null) return ep;
            }

            if (!string.IsNullOrWhiteSpace(route.ContactUri)) {
                SIPURI uri = SIPURI.ParseSIPURIRelaxed(route.ContactUri);
                SIPEndPoint ep = uri.ToSIPEndPoint();
                if (ep != null) return ep;
            }

            throw new InvalidOperationException($"FAX Zielendpunkt konnte nicht ermittelt werden | Ziel={route.CalledNumber}");
        }

        private static void Require(SIP_ServerSession session, SIPRequest request) {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (request == null) throw new ArgumentNullException(nameof(request));
        }

        private static SIPURI BuildUserUri(string? user, string host, int? port = null, string? transport = null) {
            string safeUser = Uri.EscapeDataString((user ?? string.Empty).Trim());
            string safeHost = host.Trim();
            if (port.HasValue && port.Value > 0 && !safeHost.Contains(':')) safeHost = $"{safeHost}:{port.Value}";

            SIPURI uri = SIPURI.ParseSIPURIRelaxed($"sip:{safeUser}@{safeHost}");
            if (!string.IsNullOrWhiteSpace(transport)) uri.Parameters.Set("transport", transport.Trim().ToLowerInvariant());
            return uri;
        }

        private static SIPURI BuildFaxContactUri(string? user, SIPURI requestUri, SIPEndPoint localEndPoint) {
            string host = localEndPoint.Address?.ToString() ?? requestUri.Host;
            int port = localEndPoint.Port > 0 ? localEndPoint.Port : SIPConstants.GetDefaultPort(localEndPoint.Protocol);
            return BuildUserUri(FirstContactUser(user, requestUri.User) ?? "fax", host, port, localEndPoint.Protocol.ToString());
        }

        private static SIPContactHeader BuildFaxContactHeader(SIPURI contactUri) => new SIPContactHeader(null, contactUri);

        private static string? FirstContactUser(params string?[] values) {
            foreach (string? value in values) {
                string? normalized = NormalizeSipUser(value);
                if (!string.IsNullOrWhiteSpace(normalized)) return normalized;
            }
            return null;
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

        private static void CopyResponseContent(SIPResponse source, SIPResponse dest) {
            dest.Body = source.Body;
            dest.Header.ContentType = source.Header?.ContentType;
            dest.Header.ContentLength = dest.Body?.Length ?? 0;
        }

        private static void ApplyLegAInviteDialogHeaders(SIP_ServerSession session, SIPResponse response, SIPURI requestUri) {
            if (string.IsNullOrWhiteSpace(response.Header.To.ToTag)) {
                response.Header.To.ToTag = session.LegA.ToTag ?? Guid.NewGuid().ToString("N");
            }
            session.LegA.ToTag = response.Header.To.ToTag;
            if (session.LegA.LocalSipEndPoint != null) {
                response.Header.Contact = new List<SIPContactHeader> {
                    BuildFaxContactHeader(BuildFaxContactUri(FirstContactUser(session.ToUsername, session.ToNumber), requestUri, session.LegA.LocalSipEndPoint))
                };
            }
        }

        private static SIPURI ResolveLegBRequestUri(SIP_ServerSession session, SIPIntResolvedTarget route, SIPEndPoint destinationEndPoint) {
            if (!string.IsNullOrWhiteSpace(route.ContactUri)) return SIPURI.ParseSIPURIRelaxed(route.ContactUri);
            if (session.LegB.RequestUri != null) return session.LegB.RequestUri.CopyOf();
            return BuildUserUri(route.CalledNumber ?? session.ToNumber, destinationEndPoint.Address.ToString(), destinationEndPoint.Port, destinationEndPoint.Protocol.ToString());
        }

        private static SIPURI ResolveDialogTargetUri(SIP_ServerSession session, bool toLegB) {
            if (toLegB) {
                return session.LegB.LastSuccessResponse?.Header?.Contact?.FirstOrDefault()?.ContactURI?.CopyOf()
                    ?? session.LegB.RemoteTargetUri?.CopyOf()
                    ?? session.LegB.RequestUri?.CopyOf()
                    ?? session.LegB.InviteSent?.URI?.CopyOf()
                    ?? throw new InvalidOperationException("FAX LegB Dialogziel fehlt.");
            }

            return session.LegA.ContactUri?.CopyOf()
                ?? session.LegA.InviteReceived?.Header?.Contact?.FirstOrDefault()?.ContactURI?.CopyOf()
                ?? session.LegA.RequestUri?.CopyOf()
                ?? throw new InvalidOperationException("FAX LegA Dialogziel fehlt.");
        }

        internal sealed class LegACommands {
            private readonly TKBase_LogManager? _logger;

            public LegACommands(TKBase_LogManager? logger = null) {
                _logger = logger;
            }

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
                session.LegA.ContactUri = invite.Header?.Contact?.FirstOrDefault()?.ContactURI;
                session.LegA.FromTag = invite.Header?.From?.FromTag ?? session.LegA.FromTag;
                session.LegA.ToTag = invite.Header?.To?.ToTag ?? session.LegA.ToTag;
                session.LegA.DialogCSeq = (int)(invite.Header?.CSeq ?? session.LegA.DialogCSeq);
                session.LegA.InviteSeen = true;
                session.LegA.UpdatedAt = DateTime.Now;
                _logger?.Debug($"FAX LegA INVITE IN gespeichert | SessionID={session.SessionId} | CallID={session.LegA.CallId} | Local={localEndPoint?.ToString() ?? "-"} | Remote={remoteEndPoint} | RawLength={session.LegA.InviteReceivedRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START FAX LegA INVITE IN ---{Environment.NewLine}{session.LegA.InviteReceivedRaw}{Environment.NewLine}--- SIP RAW END FAX LegA INVITE IN ---");
            }

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
                _logger?.Debug($"FAX LegA ACK IN gespeichert | SessionID={session.SessionId} | CallID={session.LegA.CallId} | RawLength={session.LegA.AckReceivedRaw?.Length ?? 0}");
            }

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
                _logger?.Debug($"FAX LegA BYE IN gespeichert | SessionID={session.SessionId} | CallID={session.LegA.CallId}");
            }

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
                _logger?.Debug($"FAX LegA CANCEL IN gespeichert | SessionID={session.SessionId} | CallID={session.LegA.CallId}");
            }

            public SIPResponse BuildResponseFromInvite(SIP_ServerSession session, SIPResponseStatusCodesEnum status, string? reason, SIPResponse? bodySource = null) {
                if (session == null) throw new ArgumentNullException(nameof(session));
                SIPRequest invite = session.LegA.InviteReceived ?? throw new InvalidOperationException("FAX LegA INVITE fehlt.");
                SIPResponse response = SIPResponse.GetResponse(invite, status, reason);
                if (bodySource != null) CopyResponseContent(bodySource, response);
                ApplyLegAInviteDialogHeaders(session, response, invite.URI);
                return response;
            }

            public SIPResponse BuildResponseFromInvite(SIP_ServerSession session, SIPResponseStatusCodesEnum status, string? reason, string? body, string? contentType) {
                if (session == null) throw new ArgumentNullException(nameof(session));
                SIPRequest invite = session.LegA.InviteReceived ?? throw new InvalidOperationException("FAX LegA INVITE fehlt.");
                SIPResponse response = SIPResponse.GetResponse(invite, status, reason);
                response.Body = body;
                response.Header.ContentType = contentType;
                response.Header.ContentLength = string.IsNullOrEmpty(body) ? 0 : body.Length;
                ApplyLegAInviteDialogHeaders(session, response, invite.URI);
                return response;
            }

            public SIPResponse BuildResponseForPendingReinvite(SIP_ServerSession session, SIPResponseStatusCodesEnum status, string? reason, SIPResponse? bodySource = null, string? body = null, string? contentType = null) {
                if (session == null) throw new ArgumentNullException(nameof(session));
                SIPRequest reinvite = session.LegA.PendingReinvite ?? throw new InvalidOperationException("FAX LegA Pending-reINVITE fehlt.");
                SIPResponse response = SIPResponse.GetResponse(reinvite, status, reason);
                if (bodySource != null) CopyResponseContent(bodySource, response);
                if (body != null || contentType != null) {
                    response.Body = body;
                    response.Header.ContentType = contentType;
                    response.Header.ContentLength = string.IsNullOrEmpty(body) ? 0 : body.Length;
                }
                ApplyLegAInviteDialogHeaders(session, response, reinvite.URI);
                return response;
            }

            public void StoreResponseSent(SIP_ServerSession session, SIPResponse response) {
                if (session == null) throw new ArgumentNullException(nameof(session));
                if (response == null) throw new ArgumentNullException(nameof(response));
                session.LegA.LastResponseSent = response;
                session.LegA.LastResponseSentRaw = response.ToString();
                session.LegA.CallId = response.Header?.CallId ?? session.LegA.CallId ?? session.SessionId;
                session.LegA.ToTag = response.Header?.To?.ToTag ?? session.LegA.ToTag;
                if (response.StatusCode >= 100 && response.StatusCode < 200) session.LegA.LastProvisionalResponse = response;
                if (response.StatusCode >= 200 && response.StatusCode < 300) session.LegA.LastSuccessResponse = response;
                if (response.StatusCode >= 300) session.LegA.IsEnded = true;
                session.LegA.UpdatedAt = DateTime.Now;
                _logger?.Debug($"FAX LegA RESPONSE OUT gespeichert | SessionID={session.SessionId} | Status={response.StatusCode} {response.ReasonPhrase} | RawLength={session.LegA.LastResponseSentRaw?.Length ?? 0}");
            }

            public SIPRequest BuildDialogRequest(SIP_ServerSession session, SIPMethodsEnum method, SIPEndPoint localEndPoint) {
                if (session == null) throw new ArgumentNullException(nameof(session));
                SIPRequest invite = session.LegA.InviteReceived ?? throw new InvalidOperationException("FAX LegA INVITE fehlt.");
                SIPURI requestUri = ResolveDialogTargetUri(session, toLegB: false);
                string localTag = session.LegA.ToTag ?? session.LegA.LastSuccessResponse?.Header?.To?.ToTag ?? Guid.NewGuid().ToString("N");
                SIPFromHeader fromHeader = new SIPFromHeader(invite.Header.To?.ToName, invite.Header.To?.ToURI ?? invite.URI, localTag);
                SIPToHeader toHeader = new SIPToHeader(invite.Header.From?.FromName, invite.Header.From?.FromURI ?? invite.URI, invite.Header.From?.FromTag);
                SIPRequest request = SIPRequest.GetRequest(method, requestUri, toHeader, fromHeader);
                request.Header.CallId = invite.Header.CallId;
                request.Header.CSeq = ++session.LegA.DialogCSeq;
                request.Header.CSeqMethod = method;
                request.Header.MaxForwards = 70;
                request.Header.Contact = method == SIPMethodsEnum.INVITE && session.LegA.LocalSipEndPoint != null
                    ? new List<SIPContactHeader> { BuildFaxContactHeader(BuildFaxContactUri(FirstContactUser(session.ToUsername, session.ToNumber), requestUri, session.LegA.LocalSipEndPoint)) }
                    : null;
                request.Header.Vias = new SIPViaSet();
                request.Header.Vias.PushViaHeader(new SIPViaHeader(localEndPoint, SIPConstants.SIP_BRANCH_MAGICCOOKIE + Guid.NewGuid().ToString("N")));
                request.SetSendFromHints(localEndPoint);
                return request;
            }

            public void StoreRequestSent(SIP_ServerSession session, SIPRequest request, SIPEndPoint targetEndPoint, SIPEndPoint? localEndPoint) {
                Require(session, request);
                session.LegA.LastRequestSent = request.Copy();
                session.LegA.LastRequestSentRaw = request.ToString();
                session.LegA.SipEndPoint = targetEndPoint;
                session.LegA.LocalSipEndPoint = localEndPoint;
                session.LegA.UpdatedAt = DateTime.Now;
                if (request.Method == SIPMethodsEnum.BYE) {
                    session.LegA.ByeSent = request.Copy();
                    session.LegA.ByeSentRaw = request.ToString();
                    session.LegA.IsEnded = true;
                }
                _logger?.Debug($"FAX LegA {request.Method} OUT gespeichert | SessionID={session.SessionId} | Ziel={targetEndPoint} | RawLength={session.LegA.LastRequestSentRaw?.Length ?? 0}");
            }

            public SIPRequest BuildAckForConfirmedDialog(SIP_ServerSession session, SIPResponse response, SIPEndPoint localEndPoint) {
                if (session == null) throw new ArgumentNullException(nameof(session));
                if (response == null) throw new ArgumentNullException(nameof(response));
                SIPRequest invite = session.LegA.PendingReinvite ?? session.LegA.InviteReceived ?? throw new InvalidOperationException("FAX LegA INVITE fehlt.");
                SIPURI ackUri = response.Header?.Contact?.FirstOrDefault()?.ContactURI?.CopyOf()
                    ?? session.LegA.RequestUri?.CopyOf()
                    ?? invite.URI.CopyOf();
                SIPRequest ack = SIPRequest.GetRequest(SIPMethodsEnum.ACK, ackUri, response.Header?.To ?? invite.Header.To, invite.Header.From);
                ack.Header.CallId = invite.Header.CallId;
                ack.Header.CSeq = response.Header?.CSeq ?? invite.Header.CSeq;
                ack.Header.CSeqMethod = SIPMethodsEnum.ACK;
                session.LegA.DialogCSeq = (int)ack.Header.CSeq;
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

            public SIPRequest BuildAckForFailedInviteResponse(SIP_ServerSession session, SIPResponse response, SIPEndPoint localEndPoint) {
                if (session == null) throw new ArgumentNullException(nameof(session));
                if (response == null) throw new ArgumentNullException(nameof(response));
                SIPRequest invite = session.LegA.PendingReinvite ?? session.LegA.InviteReceived ?? throw new InvalidOperationException("FAX LegA INVITE fehlt.");
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

            public void StoreAckSent(SIP_ServerSession session, SIPRequest ack, SIPEndPoint targetEndPoint, SIPEndPoint? localEndPoint) {
                Require(session, ack);
                session.LegA.LastRequestSent = ack.Copy();
                session.LegA.LastRequestSentRaw = ack.ToString();
                session.LegA.SipEndPoint = targetEndPoint;
                session.LegA.LocalSipEndPoint = localEndPoint;
                session.LegA.AckSeen = true;
                session.LegA.UpdatedAt = DateTime.Now;
                _logger?.Debug($"FAX LegA ACK OUT gespeichert | SessionID={session.SessionId} | CallID={session.LegA.CallId} | Ziel={targetEndPoint}");
            }

            public void PrepareSend(TKFax_Session faxSession) {
                if (faxSession == null) return;
                faxSession.LegA.IsSending = true;
                faxSession.LegA.Status = "PreparingSend";
                faxSession.Status = "PreparingSend";
                faxSession.UpdatedAt = DateTime.Now;
                _logger?.Debug($"FAX LegA Send vorbereitet | SessionID={faxSession.SessionId} | CallID={faxSession.LegA.CallId}");
            }

            public void StopSend(TKFax_Session faxSession, string? reason = null) {
                if (faxSession == null) return;
                faxSession.LegA.IsSending = false;
                faxSession.LegA.IsEnded = true;
                faxSession.LegA.Status = "Ended";
                faxSession.UpdatedAt = DateTime.Now;
                _logger?.Debug($"FAX LegA Send beendet | SessionID={faxSession.SessionId} | Grund={reason ?? string.Empty}");
            }
        }

        internal sealed class LegBCommands {
            private readonly TKBase_LogManager? _logger;

            public LegBCommands(TKBase_LogManager? logger = null) {
                _logger = logger;
            }

            public SIPRequest BuildInitialAudioInvite(SIP_ServerSession session, SIPRequest originalInvite, SIPIntResolvedTarget route, SIPEndPoint destinationEndPoint, SIPEndPoint localEndPoint, string audioOfferSdp) {
                if (session == null) throw new ArgumentNullException(nameof(session));
                if (originalInvite == null) throw new ArgumentNullException(nameof(originalInvite));
                SIPURI destinationUri = ResolveLegBRequestUri(session, route, destinationEndPoint);
                string fromUser = NormalizeSipUser(originalInvite.Header?.From?.FromURI?.User) ?? session.FromNumber;
                string host = destinationUri.Host;
                SIPURI fromUri = BuildUserUri(fromUser, host);
                SIPURI toUri = BuildUserUri(route.CalledNumber ?? session.FaxUserNumber ?? session.ToNumber, host);
                SIPFromHeader fromHeader = new SIPFromHeader(originalInvite.Header?.From?.FromName, fromUri, Guid.NewGuid().ToString("N"));
                SIPToHeader toHeader = new SIPToHeader(originalInvite.Header?.To?.ToName, toUri, null);

                SIPRequest outboundInvite = SIPRequest.GetRequest(SIPMethodsEnum.INVITE, destinationUri, toHeader, fromHeader);
                outboundInvite.SetSendFromHints(localEndPoint);
                outboundInvite.Header.Vias = new SIPViaSet();
                outboundInvite.Header.Vias.PushViaHeader(new SIPViaHeader(localEndPoint, SIPConstants.SIP_BRANCH_MAGICCOOKIE + Guid.NewGuid().ToString("N")));
                outboundInvite.Header.CSeq = originalInvite.Header?.CSeq ?? 1;
                outboundInvite.Header.CallId = !string.IsNullOrWhiteSpace(session.LegB.CallId) ? session.LegB.CallId : throw new InvalidOperationException("FAX LegB CallID fehlt.");
                outboundInvite.Header.MaxForwards = Math.Max(1, (originalInvite.Header?.MaxForwards ?? 70) - 1);
                outboundInvite.Header.Contact = new List<SIPContactHeader> { BuildFaxContactHeader(BuildFaxContactUri(FirstContactUser(session.FromUsername, session.FromNumber), destinationUri, localEndPoint)) };
                outboundInvite.Body = audioOfferSdp;
                outboundInvite.Header.ContentType = "application/sdp";
                outboundInvite.Header.ContentLength = outboundInvite.Body?.Length ?? 0;
                session.LegB.DialogCSeq = (int)outboundInvite.Header.CSeq;
                session.LegB.LocalSipEndPoint = localEndPoint;
                session.LegB.RequestUri = destinationUri;
                session.LegB.ContactUri = outboundInvite.Header.Contact.First().ContactURI;
                session.LegB.RemoteTargetUri = destinationUri;
                return outboundInvite;
            }

            public SIPRequest BuildDialogRequest(SIP_ServerSession session, SIPMethodsEnum method, SIPEndPoint localEndPoint) {
                if (session == null) throw new ArgumentNullException(nameof(session));
                SIPRequest invite = session.LegB.InviteSent ?? throw new InvalidOperationException("FAX LegB INVITE fehlt.");

                if (method == SIPMethodsEnum.CANCEL) {
                    SIPRequest cancelRequest = new SIPRequest(SIPMethodsEnum.CANCEL, invite.URI.CopyOf());
                    cancelRequest.SetSendFromHints(localEndPoint);

                    SIPHeader inviteHeader = invite.Header;
                    SIPHeader cancelHeader = new SIPHeader(inviteHeader.From, inviteHeader.To, inviteHeader.CSeq, inviteHeader.CallId) {
                        CSeqMethod = SIPMethodsEnum.CANCEL,
                        Routes = inviteHeader.Routes,
                        ProxySendFrom = inviteHeader.ProxySendFrom,
                        Vias = inviteHeader.Vias
                    };

                    cancelRequest.Header = cancelHeader;
                    return cancelRequest;
                }

                SIPURI requestUri = ResolveDialogTargetUri(session, toLegB: true);
                SIPToHeader toHeader = new SIPToHeader(
                    invite.Header.To?.ToName,
                    invite.Header.To?.ToURI ?? requestUri,
                    session.LegB.ToTag ?? invite.Header.To?.ToTag);
                SIPFromHeader fromHeader = new SIPFromHeader(
                    invite.Header.From?.FromName,
                    invite.Header.From?.FromURI ?? invite.URI,
                    session.LegB.FromTag ?? invite.Header.From?.FromTag);
                SIPRequest request = SIPRequest.GetRequest(method, requestUri, toHeader, fromHeader);
                request.Header.CallId = invite.Header.CallId;
                request.Header.CSeq = ++session.LegB.DialogCSeq;
                request.Header.CSeqMethod = method;
                request.Header.MaxForwards = 70;
                request.Header.Contact = method == SIPMethodsEnum.INVITE
                    ? new List<SIPContactHeader> { BuildFaxContactHeader(BuildFaxContactUri(FirstContactUser(session.FromUsername, session.FromNumber), requestUri, localEndPoint)) }
                    : null;
                request.Header.Vias = new SIPViaSet();
                request.Header.Vias.PushViaHeader(new SIPViaHeader(localEndPoint, SIPConstants.SIP_BRANCH_MAGICCOOKIE + Guid.NewGuid().ToString("N")));
                request.SetSendFromHints(localEndPoint);
                return request;
            }

            public SIPRequest BuildReInviteFromLegA(SIP_ServerSession session, SIPRequest legAReInvite, SIPEndPoint localEndPoint, string offerSdp) {
                SIPRequest request = BuildDialogRequest(session, SIPMethodsEnum.INVITE, localEndPoint);
                request.Body = offerSdp;
                request.Header.ContentType = "application/sdp";
                request.Header.ContentLength = request.Body?.Length ?? 0;
                session.LegA.PendingReinvite = legAReInvite.Copy();
                session.LegB.PendingReinvite = request.Copy();
                return request;
            }

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
                _logger?.Debug($"FAX LegB INVITE OUT gespeichert | SessionID={session.SessionId} | CallID={session.LegB.CallId} | Local={localEndPoint?.ToString() ?? "-"} | Ziel={targetEndPoint} | RawLength={session.LegB.InviteSentRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START FAX LegB INVITE OUT ---{Environment.NewLine}{session.LegB.InviteSentRaw}{Environment.NewLine}--- SIP RAW END FAX LegB INVITE OUT ---");
            }

            public void StoreRequestSent(SIP_ServerSession session, SIPRequest request, SIPEndPoint targetEndPoint, SIPEndPoint? localEndPoint) {
                Require(session, request);
                session.LegB.LastRequestSent = request.Copy();
                session.LegB.LastRequestSentRaw = request.ToString();
                session.LegB.SipEndPoint = targetEndPoint;
                session.LegB.LocalSipEndPoint = localEndPoint;
                session.LegB.UpdatedAt = DateTime.Now;
                if (request.Method == SIPMethodsEnum.BYE) {
                    session.LegB.ByeSent = request.Copy();
                    session.LegB.ByeSentRaw = request.ToString();
                    session.LegB.IsEnded = true;
                }
                _logger?.Debug($"FAX LegB {request.Method} OUT gespeichert | SessionID={session.SessionId} | Ziel={targetEndPoint} | RawLength={session.LegB.LastRequestSentRaw?.Length ?? 0}");
            }

            public void StoreResponseReceived(SIP_ServerSession session, SIPResponse response, SIPEndPoint remoteEndPoint, SIPEndPoint? localEndPoint) {
                if (session == null) throw new ArgumentNullException(nameof(session));
                if (response == null) throw new ArgumentNullException(nameof(response));
                session.LegB.LastResponseReceived = response;
                session.LegB.LastResponseReceivedRaw = response.ToString();
                session.LegB.SipEndPoint = remoteEndPoint;
                session.LegB.LocalSipEndPoint = localEndPoint;
                session.LegB.CallId = response.Header?.CallId ?? session.LegB.CallId ?? session.SessionId;
                session.LegB.ToTag = response.Header?.To?.ToTag ?? session.LegB.ToTag;
                if (response.StatusCode >= 100 && response.StatusCode < 200) session.LegB.LastProvisionalResponse = response;
                if (response.StatusCode >= 200 && response.StatusCode < 300) session.LegB.LastSuccessResponse = response;
                if (response.StatusCode >= 300) session.LegB.IsEnded = true;
                session.LegB.UpdatedAt = DateTime.Now;
                _logger?.Debug($"FAX LegB RESPONSE IN gespeichert | SessionID={session.SessionId} | Status={response.StatusCode} {response.ReasonPhrase} | CallID={session.LegB.CallId} | RawLength={session.LegB.LastResponseReceivedRaw?.Length ?? 0}{Environment.NewLine}--- SIP RAW START FAX LegB RESPONSE IN ---{Environment.NewLine}{session.LegB.LastResponseReceivedRaw}{Environment.NewLine}--- SIP RAW END FAX LegB RESPONSE IN ---");
            }

            public SIPResponse BuildResponseForPendingReinvite(SIP_ServerSession session, SIPResponseStatusCodesEnum status, string? reason = null, SIPResponse? bodySource = null, string? body = null, string? contentType = null) {
                if (session == null) throw new ArgumentNullException(nameof(session));
                SIPRequest reinvite = session.LegB.PendingReinvite ?? throw new InvalidOperationException("FAX LegB Pending reINVITE fehlt.");
                SIPResponse response = SIPResponse.GetResponse(reinvite, status, reason);
                if (bodySource != null) CopyResponseContent(bodySource, response);
                if (body != null || contentType != null) {
                    response.Body = body;
                    response.Header.ContentType = contentType;
                    response.Header.ContentLength = string.IsNullOrEmpty(body) ? 0 : body.Length;
                }
                return response;
            }

            public void StoreResponseSent(SIP_ServerSession session, SIPResponse response) {
                if (session == null) throw new ArgumentNullException(nameof(session));
                if (response == null) throw new ArgumentNullException(nameof(response));
                session.LegB.LastResponseSent = response;
                session.LegB.LastResponseSentRaw = response.ToString();
                if (response.StatusCode >= 100 && response.StatusCode < 200) session.LegB.LastProvisionalResponse = response;
                if (response.StatusCode >= 200 && response.StatusCode < 300) session.LegB.LastSuccessResponse = response;
                if (response.StatusCode >= 300) session.LegB.IsEnded = true;
                session.LegB.UpdatedAt = DateTime.Now;
                _logger?.Debug($"FAX LegB RESPONSE OUT gespeichert | SessionID={session.SessionId} | Status={response.StatusCode} {response.ReasonPhrase} | RawLength={session.LegB.LastResponseSentRaw?.Length ?? 0}");
            }

            public SIPRequest BuildAckForConfirmedDialog(SIP_ServerSession session, SIPResponse response, SIPEndPoint localEndPoint) {
                if (session == null) throw new ArgumentNullException(nameof(session));
                if (response == null) throw new ArgumentNullException(nameof(response));
                SIPRequest invite = session.LegB.PendingReinvite ?? session.LegB.InviteSent ?? throw new InvalidOperationException("FAX LegB INVITE fehlt.");
                SIPURI ackUri = response.Header?.Contact?.FirstOrDefault()?.ContactURI?.CopyOf()
                    ?? session.LegB.RemoteTargetUri?.CopyOf()
                    ?? invite.URI.CopyOf();
                SIPRequest ack = SIPRequest.GetRequest(SIPMethodsEnum.ACK, ackUri, response.Header?.To ?? invite.Header.To, invite.Header.From);
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

            public SIPRequest BuildAckForFailedInviteResponse(SIP_ServerSession session, SIPResponse response, SIPEndPoint localEndPoint) {
                if (session == null) throw new ArgumentNullException(nameof(session));
                if (response == null) throw new ArgumentNullException(nameof(response));
                SIPRequest invite = session.LegB.PendingReinvite ?? session.LegB.InviteSent ?? throw new InvalidOperationException("FAX LegB INVITE fehlt.");
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
                _logger?.Debug($"FAX LegB ACK OUT gespeichert | SessionID={session.SessionId} | CallID={session.LegB.CallId} | Ziel={targetEndPoint}");
            }

            public void StoreAckReceived(SIP_ServerSession session, SIPRequest ack, SIPEndPoint remoteEndPoint, SIPEndPoint? localEndPoint) {
                Require(session, ack);
                session.LegB.AckReceived = ack.Copy();
                session.LegB.AckReceivedRaw = ack.ToString();
                session.LegB.LastRequestReceived = session.LegB.AckReceived;
                session.LegB.LastRequestReceivedRaw = session.LegB.AckReceivedRaw;
                session.LegB.SipEndPoint = remoteEndPoint;
                session.LegB.LocalSipEndPoint = localEndPoint;
                session.LegB.AckSeen = true;
                session.LegB.UpdatedAt = DateTime.Now;
            }

            public void PrepareReceive(TKFax_Session faxSession) {
                if (faxSession == null) return;
                faxSession.LegB.IsReceiving = true;
                faxSession.LegB.Status = "BridgePending";
                faxSession.Status = "BridgePending";
                faxSession.UpdatedAt = DateTime.Now;
                _logger?.Debug($"FAX LegB Bridge vorbereitet | SessionID={faxSession.SessionId} | CallID={faxSession.LegB.CallId}");
            }

            public void StopReceive(TKFax_Session faxSession, string? reason = null) {
                if (faxSession == null) return;
                faxSession.LegB.IsReceiving = false;
                faxSession.LegB.IsEnded = true;
                faxSession.LegB.Status = "Ended";
                faxSession.UpdatedAt = DateTime.Now;
                _logger?.Debug($"FAX LegB Bridge beendet | SessionID={faxSession.SessionId} | Grund={reason ?? string.Empty}");
            }
        }
    }
}
