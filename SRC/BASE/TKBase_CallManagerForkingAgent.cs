/*
 * TKBase_CallManagerForkingAgent
 * Aufgabe:
 * - Forking fuer interne SIP-Ziele.
 * - Ein Leg-A INVITE kann mehrere getrennte Leg-B-Branches erzeugen.
 * - Der erste erfolgreiche Branch gewinnt; alle anderen Branches werden sauber per CANCEL beendet.
 * - Bereichscontroller fuehren keine fremden Forking-Dateien mehr; die Branch-Koordination liegt in TKBase.
 */

namespace TKServer.BASE {
    using SIPSorcery.SIP;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;


    using TKServer.SipExt;
    using TKServer.SipInt;
    using TKServer.SQL;

    /// TKBase_CallManagerForkingAgent
    /// Aufgabe:
    /// - Neutrale Forking-Koordination fuer CallManager-verwaltete Ziel-Branches.
    /// - Ein Leg-A INVITE kann mehrere getrennte Leg-B-Branches erzeugen.
    /// - Der erste erfolgreiche Branch gewinnt; alle anderen Branches werden sauber per CANCEL beendet.
    /// - Bereichscontroller fuehren keine fremden Forking-Dateien mehr; die Branch-Koordination liegt in TKBase.

    /// <summary>
    /// Zentraler Forking-Agent des TKBase_CallManager.
    /// Legs werden nicht hier direkt gebaut, sondern ueber TKBase_SessionManager erzeugt.
    /// Bereichsspezifische SIP-Nachrichten werden ueber die zustaendigen Bereichs-Commands erstellt.
    /// </summary>
    internal sealed class TKBase_CallManagerForkingAgent {
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
        /// Defines the _commands
        /// </summary>
        private readonly TKSipInt_Commands _commands;

        /// <summary>
        /// Optional SIPExt command set for external LegA responses coordinated by TKBase_CallManager.
        /// </summary>
        private readonly TKSipExt_Commands? _sipExtCommands;

        /// <summary>
        /// Defines the _events
        /// </summary>
        private readonly TKSipInt_Events _events;

        /// <summary>
        /// Defines the _media
        /// </summary>
        private readonly TKSipInt_Media _media;

        /// <summary>
        /// Defines the _settings
        /// </summary>
        private readonly TKBase_ServerSettings _settings;

        /// <summary>
        /// Defines the _groupsById
        /// </summary>
        private readonly ConcurrentDictionary<string, SipIntForkGroup> _groupsById = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Defines the _groupIdByLegACallId
        /// </summary>
        private readonly ConcurrentDictionary<string, string> _groupIdByLegACallId = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Defines the _groupIdBySessionId
        /// </summary>
        private readonly ConcurrentDictionary<string, string> _groupIdBySessionId = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of the <see cref="TKBase_CallManagerForkingAgent"/> class.
        /// </summary>
        /// <param name="sessions">The sessions<see cref="TKBase_SessionManager"/></param>
        /// <param name="commands">The commands<see cref="TKSipInt_Commands"/></param>
        /// <param name="events">The events<see cref="TKSipInt_Events"/></param>
        /// <param name="media">The media<see cref="TKSipInt_Media"/></param>
        /// <param name="settings">The settings<see cref="TKBase_ServerSettings"/></param>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        /// <param name="sipExtCommands">The sipExtCommands<see cref="TKSipExt_Commands?"/></param>
        public TKBase_CallManagerForkingAgent(
            TKBase_SessionManager sessions,
            TKSipInt_Commands commands,
            TKSipInt_Events events,
            TKSipInt_Media media,
            TKBase_ServerSettings settings,
            TKBase_LogManager? logger = null,
            TKSipExt_Commands? sipExtCommands = null) {
            _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _sipExtCommands = sipExtCommands;
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _media = media ?? throw new ArgumentNullException(nameof(media));
            _settings = settings;
            _logger = logger;
            _sipSql = new TKSQL_Commands(logger);
        }

        /// <summary>
        /// The TryStartForkAsync
        /// </summary>
        /// <param name="transport">The transport<see cref="SIPTransport"/></param>
        /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="remoteEndPoint">The remoteEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="originalInvite">The originalInvite<see cref="SIPRequest"/></param>
        /// <param name="receivedLocalEndPoint">The receivedLocalEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="legACallId">The legACallId<see cref="string"/></param>
        /// <param name="fromNumber">The fromNumber<see cref="string"/></param>
        /// <param name="toNumber">The toNumber<see cref="string"/></param>
        /// <param name="primaryRoute">The primaryRoute<see cref="SIPIntResolvedTarget"/></param>
        /// <param name="legAIsSipExt">The legAIsSipExt<see cref="bool"/></param>
        /// <returns>The <see cref="Task{bool}"/></returns>
        public async Task<bool> TryStartForkAsync(
            SIPTransport transport,
            SIPEndPoint localEndPoint,
            SIPEndPoint remoteEndPoint,
            SIPRequest originalInvite,
            SIPEndPoint receivedLocalEndPoint,
            string legACallId,
            string fromNumber,
            string toNumber,
            SIPIntResolvedTarget primaryRoute,
            bool legAIsSipExt = false) {

            if (transport == null || originalInvite == null || !primaryRoute.IsUserTarget || !primaryRoute.TargetUserId.HasValue) {
                return false;
            }

            List<SIPIntResolvedTarget> targets = ResolveTargets(primaryRoute);
            if (targets.Count == 0) {
                return false;
            }

            string groupId = $"FORK-INT-{Guid.NewGuid():N}";
            SipIntForkGroup group = new(groupId, legACallId, fromNumber, toNumber, originalInvite, legAIsSipExt);
            _groupsById[groupId] = group;
            _groupIdByLegACallId[legACallId] = groupId;

            SIPResponse trying = SIPResponse.GetResponse(originalInvite, SIPResponseStatusCodesEnum.Trying, null);
            await transport.SendResponseAsync(trying);

            foreach (SIPIntResolvedTarget target in targets) {
                try {
                    SIPEndPoint targetEndPoint = _commands.ResolveRouteEndPoint(target);
                    SIP_ServerSession session = _sessions.CreateInviteSession(
                        legACallId,
                        fromNumber,
                        toNumber,
                        originalInvite,
                        remoteEndPoint,
                        localEndPoint,
                        targetEndPoint,
                        target.ContactUri,
                        target,
                        registerLegAAlias: false,
                        parentSessionId: groupId);
                    ApplySipIntForkContactUsers(session, fromNumber, toNumber, target);

                    SIPEndPoint legBLocalEndPoint = BuildLocalEndPointForRemote(targetEndPoint);
                    ApplyLegMediaAddresses(session, receivedLocalEndPoint, remoteEndPoint, legBLocalEndPoint, targetEndPoint, group.LegAIsSipExt);
                    group.Branches.Add(new SipIntForkBranch(session.SessionId, session.LegB.CallId ?? string.Empty));
                    _groupIdBySessionId[session.SessionId] = groupId;

                    StoreLegAInviteReceived(group, session, originalInvite, remoteEndPoint, localEndPoint);
                    _events.LegA.HandleInviteReceived(session.SessionId, session.LegA.CallId, session.LegA.InviteReceivedRaw);
                    TKBase_CallManager.StartRuntimeCall(session, legACallId, fromNumber, toNumber, target);

                    if (!string.IsNullOrWhiteSpace(originalInvite.Body)) {
                        _media.PrepareCallerMediaLeg(session, originalInvite);
                    }

                    SIPRequest outboundInvite = _commands.LegB.BuildInternalInvite(session, originalInvite, target, targetEndPoint, legBLocalEndPoint);
                    _media.ApplyPreparedOfferSdp(session, outboundInvite);
                    _commands.LegB.StoreInviteSent(session, outboundInvite, targetEndPoint, legBLocalEndPoint);
                    _events.LegB.HandleInviteOutbound(session.SessionId, session.LegB.CallId, session.LegB.InviteSentRaw);

                    await transport.SendRequestAsync(targetEndPoint, outboundInvite);
                    _logger?.Info($"SipInt Fork Branch gestartet | Group={groupId} | SessionID={session.SessionId} | LegB={session.LegB.CallId} | Ziel={targetEndPoint}");
                } catch (Exception ex) {
                    _logger?.Error($"SipInt Fork Branch fehlgeschlagen | Group={groupId} | Ziel={target.ContactUri} | Fehler={ex.Message}");
                }
            }

            if (group.Branches.Count == 0) {
                CleanupGroup(group);
                return false;
            }

            _logger?.Info($"SipInt Fork gestartet | Group={groupId} | LegA={legACallId} | Branches={group.Branches.Count}");
            return true;
        }

        /// <summary>
        /// The TryHandleResponseAsync
        /// </summary>
        /// <param name="transport">The transport<see cref="SIPTransport"/></param>
        /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="remoteEndPoint">The remoteEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="response">The response<see cref="SIPResponse"/></param>
        /// <returns>The <see cref="Task{bool}"/></returns>
        public async Task<bool> TryHandleResponseAsync(SIPTransport transport, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIPResponse response) {
            string callId = response?.Header?.CallId ?? string.Empty;
            if (transport == null || response == null || !_sessions.TryGet(callId, out SIP_ServerSession? session) || session == null) {
                return false;
            }

            if (!_groupIdBySessionId.TryGetValue(session.SessionId, out string? groupId) || !_groupsById.TryGetValue(groupId, out SipIntForkGroup? group)) {
                return false;
            }

            _commands.LegB.StoreResponseReceived(session, response, remoteEndPoint, localEndPoint);

            if (IsInviteCancelBeforeAnswerActive(session)) {
                await HandleBranchInviteResponseAfterCancelAsync(transport, localEndPoint, remoteEndPoint, session, response);
                return true;
            }

            if (response.Header?.CSeqMethod == SIPMethodsEnum.CANCEL || response.Header?.CSeqMethod == SIPMethodsEnum.BYE) {
                return true;
            }

            if (response.StatusCode < 200) {
                if (!string.IsNullOrWhiteSpace(response.Body)) {
                    _media.UpdateCalleeMediaLeg(session, response.Body);
                }

                SIPResponse provisional = BuildLegAResponse(group, session, (SIPResponseStatusCodesEnum)response.StatusCode, response.ReasonPhrase, response);
                _media.ApplyPreparedAnswerSdp(session, provisional);
                await SendLegAResponseAsync(transport, group, session, provisional);
                _events.LegB.HandleRingingReceived(session.SessionId);
                TKBase_CallManager.MarkRuntimeRinging(session.SessionId);
                if (!string.IsNullOrWhiteSpace(response.Body)) {
                    _media.StartRelayIfReady(session);
                }
                return true;
            }

            if (response.StatusCode < 300) {
                bool winner;
                lock (group.SyncRoot) {
                    winner = string.IsNullOrWhiteSpace(group.WinnerSessionId);
                    if (winner) {
                        group.WinnerSessionId = session.SessionId;
                        group.Completed = true;
                    }
                }

                if (winner) {
                    _sessions.RegisterAlias(group.LegACallId, session.SessionId);
                    if (!string.IsNullOrWhiteSpace(response.Body)) {
                        _media.HandleCalleeMediaLeg(session, response);
                    }

                    SIPRequest ack = _commands.LegB.BuildAckForConfirmedDialog(session, response, session.LegB.LocalSipEndPoint ?? localEndPoint);
                    _commands.LegB.StoreAckSent(session, ack, session.LegB.SipEndPoint ?? remoteEndPoint, session.LegB.LocalSipEndPoint ?? localEndPoint);
                    await transport.SendRequestAsync(session.LegB.SipEndPoint ?? remoteEndPoint, ack);
                    session.AckForwarded = true;

                    SIPResponse ok = BuildLegAResponse(group, session, SIPResponseStatusCodesEnum.Ok, null, response);
                    _media.ApplyPreparedAnswerSdp(session, ok);
                    await SendLegAResponseAsync(transport, group, session, ok);
                    _events.LegB.HandleAnsweredReceived(session.SessionId);
                    TKBase_CallManager.MarkRuntimeAnswered(session.SessionId);
                    _media.StartRelayIfReady(session);

                    await CancelSiblingBranchesAsync(transport, localEndPoint, group, session.SessionId);
                    _logger?.Info($"SipInt Fork Gewinner | Group={group.GroupId} | Winner={session.SessionId}");
                } else {
                    await AckAndByeLateSuccessAsync(transport, localEndPoint, remoteEndPoint, session, response);
                }

                return true;
            }

            SIPRequest failedAck = _commands.LegB.BuildAckForFailedInviteResponse(session, response, session.LegB.LocalSipEndPoint ?? localEndPoint);
            _commands.LegB.StoreAckSent(session, failedAck, session.LegB.SipEndPoint ?? remoteEndPoint, session.LegB.LocalSipEndPoint ?? localEndPoint);
            await transport.SendRequestAsync(session.LegB.SipEndPoint ?? remoteEndPoint, failedAck);
            MarkBranchFinal(group, session.SessionId, response);
            _events.LegB.HandleFailedReceived(session.SessionId, response.ReasonPhrase);

            if (AllBranchesFinal(group)) {
                SIPResponse fail = BuildLegAResponse(group, session, (SIPResponseStatusCodesEnum)response.StatusCode, response.ReasonPhrase, response);
                await SendLegAResponseAsync(transport, group, session, fail);
                CleanupGroup(group);
            }

            return true;
        }

        /// <summary>
        /// The TryCancelForkAsync
        /// </summary>
        /// <param name="transport">The transport<see cref="SIPTransport"/></param>
        /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="remoteEndPoint">The remoteEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="cancel">The cancel<see cref="SIPRequest"/></param>
        /// <returns>The <see cref="Task{bool}"/></returns>
        public async Task<bool> TryCancelForkAsync(SIPTransport transport, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIPRequest cancel) {
            string legACallId = cancel?.Header?.CallId ?? string.Empty;
            if (transport == null || cancel == null || !_groupIdByLegACallId.TryGetValue(legACallId, out string? groupId) || !_groupsById.TryGetValue(groupId, out SipIntForkGroup? group)) {
                return false;
            }

            await transport.SendResponseAsync(SIPResponse.GetResponse(cancel, SIPResponseStatusCodesEnum.Ok, null));
            lock (group.SyncRoot) {
                group.Completed = true;
                group.WinnerSessionId = "CANCELLED";
            }
            foreach (SipIntForkBranch branch in group.Branches.ToList()) {
                if (!_sessions.TryGet(branch.SessionId, out SIP_ServerSession? session) || session == null || session.LegB.SipEndPoint == null || session.LegB.LocalSipEndPoint == null || session.LegB.InviteSent == null) {
                    continue;
                }

                MarkCancelBeforeAnswerStarted(session, "SipInt Fork LegA CANCEL", remoteEndPoint, localEndPoint);
                SIPRequest branchCancel = _commands.LegB.BuildDialogRequest(session, SIPMethodsEnum.CANCEL, session.LegB.LocalSipEndPoint);
                _commands.LegB.StoreCancelSent(session, branchCancel, session.LegB.SipEndPoint, session.LegB.LocalSipEndPoint);
                await transport.SendRequestAsync(session.LegB.SipEndPoint, branchCancel);
                _media.StopRelayAndReleasePorts(session);
                _sessions.MarkAllLegsEnded(session.SessionId, "Cancelled");
                TKBase_CallManager.MarkRuntimeEnded(session.SessionId, "Fork cancelled");
            }

            if (group.Branches.Count > 0 && _sessions.TryGet(group.Branches[0].SessionId, out SIP_ServerSession? first) && first != null) {
                SIPResponse terminated = BuildLegAResponse(group, first, SIPResponseStatusCodesEnum.RequestTerminated, "Fork cancelled", null);
                await SendLegAResponseAsync(transport, group, first, terminated);
            }

            CleanupGroup(group);
            _logger?.Info($"SipInt Fork CANCEL | Group={group.GroupId} | LegA={legACallId}");
            return true;
        }

        /// <summary>
        /// The ApplySipIntForkContactUsers
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="fromNumber">The fromNumber<see cref="string"/></param>
        /// <param name="toNumber">The toNumber<see cref="string"/></param>
        /// <param name="route">The route<see cref="SIPIntResolvedTarget"/></param>
        private void ApplySipIntForkContactUsers(SIP_ServerSession session, string fromNumber, string toNumber, SIPIntResolvedTarget route) {
            session.FromUsername = ResolveSipUsernameByInput(fromNumber) ?? route.CallerUsername ?? fromNumber;
            session.ToUsername = ResolveSipUsernameByRoute(route, toNumber) ?? toNumber;
        }

        /// <summary>
        /// The ResolveSipUsernameByRoute
        /// </summary>
        /// <param name="route">The route<see cref="SIPIntResolvedTarget"/></param>
        /// <param name="fallbackNumber">The fallbackNumber<see cref="string"/></param>
        /// <returns>The <see cref="string?"/></returns>
        private string? ResolveSipUsernameByRoute(SIPIntResolvedTarget route, string fallbackNumber) {
            if (route.TargetUserId.HasValue) {
                DataRow? target = _sipSql.GetSipUserById(route.TargetUserId.Value);
                string? username = ReadUsername(target);
                if (!string.IsNullOrWhiteSpace(username)) return username;
            }
            return !string.IsNullOrWhiteSpace(route.TargetUsername) ? route.TargetUsername : ResolveSipUsernameByInput(fallbackNumber);
        }

        /// <summary>
        /// The ResolveSipUsernameByInput
        /// </summary>
        /// <param name="value">The value<see cref="string"/></param>
        /// <returns>The <see cref="string?"/></returns>
        private string? ResolveSipUsernameByInput(string value) {
            if (string.IsNullOrWhiteSpace(value)) return null;
            DataRow? row = _sipSql.GetSipUserByUsername(value) ?? _sipSql.GetSipUserByNumber(value);
            return ReadUsername(row);
        }

        /// <summary>
        /// The ReadUsername
        /// </summary>
        /// <param name="row">The row<see cref="DataRow?"/></param>
        /// <returns>The <see cref="string?"/></returns>
        private static string? ReadUsername(DataRow? row) {
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

        /// <summary>
        /// The ApplyLegMediaAddresses
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="legALocalEndPoint">The legALocalEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="legARemoteEndPoint">The legARemoteEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="legBLocalEndPoint">The legBLocalEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="legBRemoteEndPoint">The legBRemoteEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="legAIsSipExt">The legAIsSipExt<see cref="bool"/></param>
        private void ApplyLegMediaAddresses(SIP_ServerSession session, SIPEndPoint legALocalEndPoint, SIPEndPoint legARemoteEndPoint, SIPEndPoint legBLocalEndPoint, SIPEndPoint legBRemoteEndPoint, bool legAIsSipExt) {
            string? legAProviderPublicIp = legAIsSipExt ? _sipSql.GetPublicInternetIpForAddressFamily(legARemoteEndPoint.Address.AddressFamily) : null;
            if (!_settings.TryResolveIngressMediaAdvertisedIp(legALocalEndPoint.Address, legARemoteEndPoint.Address, legAProviderPublicIp, out string legAIp, out string legAReason)) {
                _logger?.Error($"SipInt Fork MediaAdvertisedIp LegA fehlt | SessionID={session.SessionId} | LegAIsSipExt={legAIsSipExt} | PublicIP={legAProviderPublicIp ?? "-"} | Local={legALocalEndPoint.Address} | Remote={legARemoteEndPoint.Address} | Grund={legAReason}");
                throw new InvalidOperationException(legAReason);
            }

            if (!_settings.TryResolveEndpointMediaAdvertisedIp(legBLocalEndPoint.Address, legBRemoteEndPoint.Address, null, false, out string legBIp, out string legBReason)) {
                _logger?.Error($"SipInt Fork MediaAdvertisedIp LegB fehlt | SessionID={session.SessionId} | Local={legBLocalEndPoint.Address} | Remote={legBRemoteEndPoint.Address} | Grund={legBReason}");
                throw new InvalidOperationException(legBReason);
            }

            session.LegA.MediaAdvertisedIp = legAIp;
            session.LegB.MediaAdvertisedIp = legBIp;
            session.MediaAdvertisedIp = legAIp;
            _logger?.Info($"SipInt Fork MediaAdvertisedIp gesetzt | SessionID={session.SessionId} | LegA={legAIp} | LegB={legBIp}");
        }

        /// <summary>
        /// The ResolveTargets
        /// </summary>
        /// <param name="primaryRoute">The primaryRoute<see cref="SIPIntResolvedTarget"/></param>
        /// <returns>The <see cref="List{SIPIntResolvedTarget}"/></returns>
        private List<SIPIntResolvedTarget> ResolveTargets(SIPIntResolvedTarget primaryRoute) {
            if (!primaryRoute.TargetUserId.HasValue) {
                return new List<SIPIntResolvedTarget> { primaryRoute };
            }

            List<SIPIntResolvedTarget> results = new();
            HashSet<string> unique = new(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow row in _sipSql.GetActiveRegistrationsByUserId(primaryRoute.TargetUserId.Value)) {
                string contactUri = Convert.ToString(row["ContactURI"]) ?? string.Empty;
                string remoteEndPoint = Convert.ToString(row["RemoteEndPoint"]) ?? string.Empty;
                string? transport = row.Table.Columns.Contains("Transport") && row["Transport"] != DBNull.Value ? Convert.ToString(row["Transport"]) : null;
                string key = $"{contactUri}|{remoteEndPoint}";
                if (string.IsNullOrWhiteSpace(contactUri) || string.IsNullOrWhiteSpace(remoteEndPoint) || !unique.Add(key)) {
                    continue;
                }
                results.Add(SIPIntResolvedTarget.OkUser(primaryRoute.CalledNumber, primaryRoute.TargetUserId.Value, contactUri, remoteEndPoint, transport));
            }

            if (results.Count == 0) {
                results.Add(primaryRoute);
            }
            return results;
        }

        /// <summary>
        /// The CancelSiblingBranchesAsync
        /// </summary>
        /// <param name="transport">The transport<see cref="SIPTransport"/></param>
        /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="group">The group<see cref="SipIntForkGroup"/></param>
        /// <param name="winnerSessionId">The winnerSessionId<see cref="string"/></param>
        /// <returns>The <see cref="Task"/></returns>
        private async Task CancelSiblingBranchesAsync(SIPTransport transport, SIPEndPoint localEndPoint, SipIntForkGroup group, string winnerSessionId) {
            foreach (SipIntForkBranch branch in group.Branches.Where(x => !string.Equals(x.SessionId, winnerSessionId, StringComparison.OrdinalIgnoreCase)).ToList()) {
                if (!_sessions.TryGet(branch.SessionId, out SIP_ServerSession? sibling) || sibling == null || sibling.LegB.SipEndPoint == null || sibling.LegB.LocalSipEndPoint == null || sibling.LegB.InviteSent == null || sibling.LegB.IsEnded) {
                    continue;
                }

                SIPRequest cancel = _commands.LegB.BuildDialogRequest(sibling, SIPMethodsEnum.CANCEL, sibling.LegB.LocalSipEndPoint);
                _commands.LegB.StoreCancelSent(sibling, cancel, sibling.LegB.SipEndPoint, sibling.LegB.LocalSipEndPoint);
                await transport.SendRequestAsync(sibling.LegB.SipEndPoint, cancel);
                _media.StopRelayAndReleasePorts(sibling);
                _sessions.MarkAllLegsEnded(sibling.SessionId, "ForkCancelled");
                TKBase_CallManager.MarkRuntimeEnded(sibling.SessionId, "Fork sibling cancelled");
                branch.Final = true;
            }
        }

        /// <summary>
        /// The AckAndByeLateSuccessAsync
        /// </summary>
        /// <param name="transport">The transport<see cref="SIPTransport"/></param>
        /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="remoteEndPoint">The remoteEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="response">The response<see cref="SIPResponse"/></param>
        /// <returns>The <see cref="Task"/></returns>
        private async Task AckAndByeLateSuccessAsync(SIPTransport transport, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIP_ServerSession session, SIPResponse response) {
            SIPRequest ack = _commands.LegB.BuildAckForConfirmedDialog(session, response, session.LegB.LocalSipEndPoint ?? localEndPoint);
            _commands.LegB.StoreAckSent(session, ack, session.LegB.SipEndPoint ?? remoteEndPoint, session.LegB.LocalSipEndPoint ?? localEndPoint);
            await transport.SendRequestAsync(session.LegB.SipEndPoint ?? remoteEndPoint, ack);

            SIPRequest bye = _commands.LegB.BuildDialogRequest(session, SIPMethodsEnum.BYE, session.LegB.LocalSipEndPoint ?? localEndPoint);
            _commands.LegB.StoreByeSent(session, bye, session.LegB.SipEndPoint ?? remoteEndPoint, session.LegB.LocalSipEndPoint ?? localEndPoint);
            await transport.SendRequestAsync(session.LegB.SipEndPoint ?? remoteEndPoint, bye);
            _media.StopRelayAndReleasePorts(session);
            _sessions.MarkAllLegsEnded(session.SessionId, "LateForkSuccessClosed");
            TKBase_CallManager.MarkRuntimeEnded(session.SessionId, "Late fork success closed");
        }

        /// <summary>
        /// The MarkCancelBeforeAnswerStarted
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <param name="reason">The reason<see cref="string"/></param>
        /// <param name="sourceEndPoint">The sourceEndPoint<see cref="SIPEndPoint?"/></param>
        /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint?"/></param>
        private static void MarkCancelBeforeAnswerStarted(SIP_ServerSession session, string reason, SIPEndPoint? sourceEndPoint, SIPEndPoint? localEndPoint) {
            if (session == null) return;
            lock (session.SyncRoot) {
                if (!session.IsAnswered) {
                    session.IsTerminating = true;
                    session.Status = "Cancelled";
                    session.TerminationReason = reason;
                    session.TerminationStartedAt ??= DateTime.Now;
                    session.TerminationSourceEndPoint = sourceEndPoint;
                    session.TerminationLocalEndPoint = localEndPoint;
                    session.LegA.CancelSeen = true;
                    session.LegA.IsEnded = true;
                }
                session.UpdatedAt = DateTime.Now;
            }
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

        private async Task HandleBranchInviteResponseAfterCancelAsync(SIPTransport transport, SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIP_ServerSession session, SIPResponse response) {
            if (response.Header?.CSeqMethod != SIPMethodsEnum.INVITE) {
                return;
            }

            if (response.StatusCode < 200) {
                _logger?.Info($"SipInt Fork RESPONSE nach LegA-CANCEL ignoriert | SessionID={session.SessionId} | Status={response.StatusCode} {response.ReasonPhrase}");
                return;
            }

            if (response.StatusCode < 300) {
                await AckAndByeLateSuccessAsync(transport, localEndPoint, remoteEndPoint, session, response);
                _sessions.MarkAllLegsEnded(session.SessionId, "Cancelled");
                TKBase_CallManager.MarkRuntimeEnded(session.SessionId, "Fork cancelled");
                _logger?.Info($"SipInt Fork 200 OK nach LegA-CANCEL mit ACK+BYE geschlossen | SessionID={session.SessionId} | CallID={response.Header?.CallId}");
                return;
            }

            try {
                SIPRequest failedAck = _commands.LegB.BuildAckForFailedInviteResponse(session, response, session.LegB.LocalSipEndPoint ?? localEndPoint);
                _commands.LegB.StoreAckSent(session, failedAck, session.LegB.SipEndPoint ?? remoteEndPoint, session.LegB.LocalSipEndPoint ?? localEndPoint);
                await transport.SendRequestAsync(session.LegB.SipEndPoint ?? remoteEndPoint, failedAck);
            } catch (Exception ex) {
                _logger?.Warn($"SipInt Fork ACK nach LegA-CANCEL konnte nicht gesendet werden | SessionID={session.SessionId} | Status={response.StatusCode} {response.ReasonPhrase} | Fehler={ex.Message}");
            }

            _media.StopRelayAndReleasePorts(session);
            _sessions.MarkAllLegsEnded(session.SessionId, "Cancelled");
            TKBase_CallManager.MarkRuntimeEnded(session.SessionId, "Fork cancelled");
            _logger?.Info($"SipInt Fork Final RESPONSE nach LegA-CANCEL verarbeitet | SessionID={session.SessionId} | Status={response.StatusCode} {response.ReasonPhrase}");
        }


        private void StoreLegAInviteReceived(SipIntForkGroup group, SIP_ServerSession session, SIPRequest invite, SIPEndPoint remoteEndPoint, SIPEndPoint? localEndPoint) {
            if (group.LegAIsSipExt && _sipExtCommands != null) {
                _sipExtCommands.LegA.StoreInviteReceived(session, invite, remoteEndPoint, localEndPoint);
                return;
            }

            _commands.LegA.StoreInviteReceived(session, invite, remoteEndPoint, localEndPoint);
        }

        private SIPResponse BuildLegAResponse(SipIntForkGroup group, SIP_ServerSession session, SIPResponseStatusCodesEnum status, string? reason, SIPResponse? bodySource) {
            if (group.LegAIsSipExt && _sipExtCommands != null) {
                return _sipExtCommands.LegA.BuildResponseFromInvite(session, status, reason, bodySource);
            }

            return _commands.LegA.BuildResponseFromInvite(session, status, reason, bodySource);
        }

        private void StoreLegAResponseSent(SipIntForkGroup group, SIP_ServerSession session, SIPResponse response) {
            if (group.LegAIsSipExt && _sipExtCommands != null) {
                _sipExtCommands.LegA.StoreResponseSent(session, response);
                return;
            }

            _commands.LegA.StoreResponseSent(session, response);
        }

        private async Task SendLegAResponseAsync(SIPTransport transport, SipIntForkGroup group, SIP_ServerSession session, SIPResponse response) {
            StoreLegAResponseSent(group, session, response);
            if (response.StatusCode >= 100 && response.StatusCode < 200) _events.LegA.HandleRingingSent(session.SessionId);
            else if (response.StatusCode >= 200 && response.StatusCode < 300) _events.LegA.HandleAnsweredSent(session.SessionId);
            else _events.LegA.HandleFailedSent(session.SessionId, response.ReasonPhrase);
            await transport.SendResponseAsync(response);
        }

        /// <summary>
        /// The MarkBranchFinal
        /// </summary>
        /// <param name="group">The group<see cref="SipIntForkGroup"/></param>
        /// <param name="sessionId">The sessionId<see cref="string"/></param>
        /// <param name="response">The response<see cref="SIPResponse"/></param>
        private static void MarkBranchFinal(SipIntForkGroup group, string sessionId, SIPResponse response) {
            SipIntForkBranch? branch = group.Branches.FirstOrDefault(x => string.Equals(x.SessionId, sessionId, StringComparison.OrdinalIgnoreCase));
            if (branch == null) return;
            branch.Final = true;
            branch.LastStatusCode = response.StatusCode;
            branch.LastReason = response.ReasonPhrase;
        }

        /// <summary>
        /// The AllBranchesFinal
        /// </summary>
        /// <param name="group">The group<see cref="SipIntForkGroup"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool AllBranchesFinal(SipIntForkGroup group) {
            lock (group.SyncRoot) {
                return string.IsNullOrWhiteSpace(group.WinnerSessionId) && group.Branches.Count > 0 && group.Branches.All(x => x.Final);
            }
        }

        /// <summary>
        /// The CleanupGroup
        /// </summary>
        /// <param name="group">The group<see cref="SipIntForkGroup"/></param>
        private void CleanupGroup(SipIntForkGroup group) {
            _groupsById.TryRemove(group.GroupId, out _);
            _groupIdByLegACallId.TryRemove(group.LegACallId, out _);
            foreach (SipIntForkBranch branch in group.Branches) {
                _groupIdBySessionId.TryRemove(branch.SessionId, out _);
            }
        }

        /// <summary>
        /// Defines the <see cref="SipIntForkGroup" />
        /// </summary>
        private sealed class SipIntForkGroup {
            /// <summary>
            /// Initializes a new instance of the <see cref="SipIntForkGroup"/> class.
            /// </summary>
            /// <param name="groupId">The groupId<see cref="string"/></param>
            /// <param name="legACallId">The legACallId<see cref="string"/></param>
            /// <param name="fromNumber">The fromNumber<see cref="string"/></param>
            /// <param name="toNumber">The toNumber<see cref="string"/></param>
            /// <param name="originalInvite">The originalInvite<see cref="SIPRequest"/></param>
            /// <param name="legAIsSipExt">The legAIsSipExt<see cref="bool"/></param>
            public SipIntForkGroup(string groupId, string legACallId, string fromNumber, string toNumber, SIPRequest originalInvite, bool legAIsSipExt) {
                GroupId = groupId;
                LegACallId = legACallId;
                FromNumber = fromNumber;
                ToNumber = toNumber;
                OriginalInvite = originalInvite;
                LegAIsSipExt = legAIsSipExt;
            }

            /// <summary>
            /// Gets the SyncRoot
            /// </summary>
            public object SyncRoot { get; } = new();

            /// <summary>
            /// Gets the GroupId
            /// </summary>
            public string GroupId { get; }

            /// <summary>
            /// Gets the LegACallId
            /// </summary>
            public string LegACallId { get; }

            /// <summary>
            /// Gets the FromNumber
            /// </summary>
            public string FromNumber { get; }

            /// <summary>
            /// Gets the ToNumber
            /// </summary>
            public string ToNumber { get; }

            /// <summary>
            /// Gets the OriginalInvite
            /// </summary>
            public SIPRequest OriginalInvite { get; }

            public bool LegAIsSipExt { get; }

            /// <summary>
            /// Gets the Branches
            /// </summary>
            public List<SipIntForkBranch> Branches { get; } = new();

            /// <summary>
            /// Gets or sets the WinnerSessionId
            /// </summary>
            public string? WinnerSessionId { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether Completed
            /// </summary>
            public bool Completed { get; set; }
        }

        /// <summary>
        /// Defines the <see cref="SipIntForkBranch" />
        /// </summary>
        private sealed class SipIntForkBranch {
            /// <summary>
            /// Initializes a new instance of the <see cref="SipIntForkBranch"/> class.
            /// </summary>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <param name="legBCallId">The legBCallId<see cref="string"/></param>
            public SipIntForkBranch(string sessionId, string legBCallId) {
                SessionId = sessionId;
                LegBCallId = legBCallId;
            }

            /// <summary>
            /// Gets the SessionId
            /// </summary>
            public string SessionId { get; }

            /// <summary>
            /// Gets the LegBCallId
            /// </summary>
            public string LegBCallId { get; }

            /// <summary>
            /// Gets or sets a value indicating whether Final
            /// </summary>
            public bool Final { get; set; }

            /// <summary>
            /// Gets or sets the LastStatusCode
            /// </summary>
            public int LastStatusCode { get; set; }

            /// <summary>
            /// Gets or sets the LastReason
            /// </summary>
            public string? LastReason { get; set; }
        }
    }
}
