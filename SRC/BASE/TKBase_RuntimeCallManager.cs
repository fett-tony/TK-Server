/*
 * TKBase_RuntimeCallManager
 * Aufgabe:
 * - Führt die zentrale Runtime-Call-Sicht aller laufenden Gespräche.
 * - Hält die Live-Zustände für die Anzeige, insbesondere für die LiveCall-Form.
 * - Schreibt, aktualisiert und entfernt Runtime-Call-Einträge bei Statusänderungen.
 * - Führt die laufende Gesprächssicht aus Session-, Leg-, Richtungs- und Statusdaten zusammen.
 *
 * Nicht Aufgabe:
 * - Keine Routingentscheidung.
 * - Keine Session-Erzeugung.
 * - Keine Call-Steuerung.
 * - Keine Media-Steuerung.
 * - Keine Registration-Steuerung.
 *
 * Diese Datei ist die zentrale Laufzeit- und LiveCall-Sicht aktiver Gespräche.
 */

namespace TKServer.BASE {
    using System;
    using System.Data;

    using TKServer.FAX;
    using TKServer.SipExt;
    using TKServer.SQL;
    /// TKBase_RuntimeCallManager
    /// Aufgabe:
    /// - Führt die zentrale Runtime-Call-Sicht aller laufenden Gespräche.
    /// - Hält die Live-Zustände für die Anzeige, insbesondere für die LiveCall-Form.
    /// - Schreibt, aktualisiert und entfernt Runtime-Call-Einträge bei Statusänderungen.
    /// - Führt die laufende Gesprächssicht aus Session-, Leg-, Richtungs- und Statusdaten zusammen.
    /// 
    /// Nicht Aufgabe:
    /// - Keine Routingentscheidung.
    /// - Keine Session-Erzeugung.
    /// - Keine Call-Steuerung.
    /// - Keine Media-Steuerung.
    /// - Keine Registration-Steuerung.
    /// 
    /// Diese Datei ist die zentrale Laufzeit- und LiveCall-Sicht aktiver Gespräche.

    /// <summary>
    /// Defines the <see cref="TKBase_RuntimeCallManager" />
    /// </summary>
    internal sealed class TKBase_RuntimeCallManager {
        /// <summary>
        /// Defines the _sipSql
        /// </summary>
        private readonly TKSQL_Commands _sipSql;

        /// <summary>
        /// Defines the _logger
        /// </summary>
        private readonly TKBase_LogManager? _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TKBase_RuntimeCallManager"/> class.
        /// </summary>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        public TKBase_RuntimeCallManager(TKBase_LogManager? logger = null) {
            _logger = logger;
            _sipSql = new TKSQL_Commands(logger);
        }

        /// <summary>
        /// The StartInternalCall
        /// </summary>
        /// <param name="sessionId">The sessionId<see cref="string"/></param>
        /// <param name="bridgeId">The bridgeId<see cref="Guid?"/></param>
        /// <param name="legACallId">The legACallId<see cref="string"/></param>
        /// <param name="legBCallId">The legBCallId<see cref="string?"/></param>
        /// <param name="fromNumber">The fromNumber<see cref="string"/></param>
        /// <param name="toNumber">The toNumber<see cref="string"/></param>
        /// <param name="inviteInRaw">The inviteInRaw<see cref="string?"/></param>
        /// <param name="route">The route<see cref="SIPIntResolvedTarget?"/></param>
        /// <param name="isServiceSession">The isServiceSession<see cref="bool"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int StartInternalCall(
            string sessionId,
            Guid? bridgeId,
            string legACallId,
            string? legBCallId,
            string fromNumber,
            string toNumber,
            string? inviteInRaw = null,
            SIPIntResolvedTarget? route = null,
            bool isServiceSession = false) {

            DataRow? fromUser = ResolveUser(fromNumber);
            DataRow? toUser = route?.TargetUserId != null
                ? _sipSql.GetSipUserById(route.TargetUserId.Value)
                : ResolveUser(toNumber);

            int? fromUserId = ReadNullableInt(fromUser, "ID");
            int? toUserId = ReadNullableInt(toUser, "ID");
            string callDirection = DetermineCallDirection(fromUserId, toUserId, route, fromNumber, toNumber);
            string callType = DetermineCallType(callDirection, route, isServiceSession);
            string callLogStatus = string.Equals(callType, "Service", StringComparison.OrdinalIgnoreCase) ? "Trying" : "Ringing";

            int id = _sipSql.CreateCallLog(
                sessionId,
                fromUserId,
                toUserId,
                fromNumber,
                toNumber,
                callDirection,
                callLogStatus,
                DateTime.Now,
                bridgeId: bridgeId,
                legACallId: legACallId,
                legBCallId: legBCallId,
                inviteInRaw: inviteInRaw);

            SaveInviteSession(sessionId, bridgeId, legACallId, legBCallId, fromNumber, toNumber, route, isServiceSession);
            return id;
        }

        /// <summary>
        /// The SaveInviteSession
        /// </summary>
        /// <param name="sessionId">The sessionId<see cref="string"/></param>
        /// <param name="bridgeId">The bridgeId<see cref="Guid?"/></param>
        /// <param name="legACallId">The legACallId<see cref="string"/></param>
        /// <param name="legBCallId">The legBCallId<see cref="string?"/></param>
        /// <param name="fromNumber">The fromNumber<see cref="string"/></param>
        /// <param name="toNumber">The toNumber<see cref="string"/></param>
        /// <param name="route">The route<see cref="SIPIntResolvedTarget?"/></param>
        /// <param name="isServiceSession">The isServiceSession<see cref="bool"/></param>
        public void SaveInviteSession(
            string sessionId,
            Guid? bridgeId,
            string legACallId,
            string? legBCallId,
            string fromNumber,
            string toNumber,
            SIPIntResolvedTarget? route,
            bool isServiceSession = false) {

            DataRow? fromUser = ResolveUser(fromNumber);
            DataRow? toUser = route?.TargetUserId != null
                ? _sipSql.GetSipUserById(route.TargetUserId.Value)
                : ResolveUser(toNumber);

            int? fromUserId = ReadNullableInt(fromUser, "ID");
            int? toUserId = ReadNullableInt(toUser, "ID");

            string callDirection = DetermineCallDirection(fromUserId, toUserId, route, fromNumber, toNumber);
            string callType = DetermineCallType(callDirection, route, isServiceSession);
            string? routeKind = DetermineRouteKind(route, isServiceSession, fromUser, toUser);

            string? internalNumber = DetermineInternalNumber(callDirection, fromUser, toUser, fromNumber, toNumber);
            string? externalNumber = DetermineExternalNumber(callDirection, fromUserId, toUserId, fromNumber, toNumber);
            string? outboundExternalNumber = DetermineOutboundExternalNumber(route);

            string? displayFrom = DetermineDisplayName(fromUser, fromNumber);
            string? displayTo = DetermineDisplayName(toUser, toNumber);

            _sipSql.SaveOrUpdateRuntimeCall(
                sessionId: sessionId,
                bridgeId: bridgeId,
                callDirection: callDirection,
                callType: callType,
                routeKind: routeKind,
                status: "Trying",
                fromUserId: fromUserId,
                toUserId: toUserId,
                fromNumber: NormalizeNullableNumber(fromNumber),
                toNumber: NormalizeNullableNumber(toNumber),
                displayFrom: displayFrom,
                displayTo: displayTo,
                internalNumber: NormalizeNullableNumber(internalNumber),
                externalNumber: NormalizeNullableNumber(externalNumber),
                outboundExternalNumber: NormalizeNullableNumber(outboundExternalNumber),
                providerAccountId: route?.ProviderAccountId,
                gsmStatusId: route?.GsmStatusId,
                gsmModemId: route?.GsmModemId,
                gsmSimId: route?.GsmSimId,
                legACallId: legACallId,
                legBCallId: legBCallId,
                legDCallId: null,
                legAStatus: "Trying",
                legBStatus: string.IsNullOrWhiteSpace(legBCallId) ? null : "Trying",
                legDStatus: isServiceSession ? "Trying" : null,
                contactUriA: null,
                contactUriB: route?.ContactUri,
                remoteEndPointA: null,
                remoteEndPointB: route?.RemoteEndPoint,
                canHangup: true,
                canTransfer: false,
                transferTarget: null,
                startedAt: DateTime.Now,
                answeredAt: null);

            _logger?.Info($"RuntimeCall gespeichert | SessionID={sessionId} | Direction={callDirection} | Type={callType} | RouteKind={routeKind} | OutboundLine={outboundExternalNumber}");
            TKBase_RuntimeCallEvents.RaiseChanged();
        }

        /// <summary>
        /// The UpdateInviteOutbound
        /// </summary>
        /// <param name="sessionId">The sessionId<see cref="string"/></param>
        /// <param name="legBCallId">The legBCallId<see cref="string?"/></param>
        public void UpdateInviteOutbound(string sessionId, string? legBCallId) {
            int rows = _sipSql.UpdateRuntimeCallOutboundInvite(sessionId, legBCallId);
            if (rows > 0) {
                TKBase_RuntimeCallEvents.RaiseChanged();
            }
        }

        /// <summary>
        /// The MarkRinging
        /// </summary>
        /// <param name="sessionId">The sessionId<see cref="string"/></param>
        public void MarkRinging(string sessionId) {
            int rows = _sipSql.MarkRuntimeCallRinging(sessionId);
            if (rows > 0) {
                _logger?.Info($"RuntimeCall Ringing | SessionID={sessionId} | Rows={rows}");
                TKBase_RuntimeCallEvents.RaiseChanged();
            }
        }

        /// <summary>
        /// The MarkAnswered
        /// </summary>
        /// <param name="sessionId">The sessionId<see cref="string"/></param>
        public void MarkAnswered(string sessionId) {
            int rows = _sipSql.MarkRuntimeCallAnswered(sessionId, DateTime.Now);
            if (rows > 0) {
                TKBase_RuntimeCallEvents.RaiseChanged();
            }
        }

        /// <summary>
        /// The MarkEnded
        /// </summary>
        /// <param name="sessionId">The sessionId<see cref="string"/></param>
        /// <param name="reason">The reason<see cref="string?"/></param>
        public void MarkEnded(string sessionId, string? reason = null) {
            int rows = _sipSql.DeleteRuntimeCall(sessionId);
            _logger?.Info($"RuntimeCall entfernt | SessionID={sessionId} | Grund={reason} | Rows={rows}");
            if (rows > 0) {
                TKBase_RuntimeCallEvents.RaiseChanged();
            }
        }

        /// <summary>
        /// The MarkFailed
        /// </summary>
        /// <param name="sessionId">The sessionId<see cref="string"/></param>
        /// <param name="reason">The reason<see cref="string?"/></param>
        public void MarkFailed(string sessionId, string? reason = null) {
            int rows = _sipSql.DeleteRuntimeCall(sessionId);
            _logger?.Info($"RuntimeCall entfernt | SessionID={sessionId} | Fehler={reason} | Rows={rows}");
            if (rows > 0) {
                TKBase_RuntimeCallEvents.RaiseChanged();
            }
        }

        /// <summary>
        /// The ResolveUser
        /// </summary>
        /// <param name="value">The value<see cref="string?"/></param>
        /// <returns>The <see cref="DataRow?"/></returns>
        private DataRow? ResolveUser(string? value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return null;
            }

            DataRow? user = _sipSql.GetSipUserByUsername(value);
            if (user != null) {
                return user;
            }

            user = _sipSql.GetSipUserByNumber(value);
            if (user != null) {
                return user;
            }

            string normalized = TKSipExt_Routing.NormalizeExternalNumber(value);
            if (!string.IsNullOrWhiteSpace(normalized)) {
                user = _sipSql.GetSipUserByNumber(normalized);
            }

            return user;
        }

        /// <summary>
        /// The DetermineCallDirection
        /// </summary>
        /// <param name="fromUserId">The fromUserId<see cref="int?"/></param>
        /// <param name="toUserId">The toUserId<see cref="int?"/></param>
        /// <param name="route">The route<see cref="SIPIntResolvedTarget?"/></param>
        /// <param name="fromNumber">The fromNumber<see cref="string"/></param>
        /// <param name="toNumber">The toNumber<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string DetermineCallDirection(int? fromUserId, int? toUserId, SIPIntResolvedTarget? route, string fromNumber, string toNumber) {
            bool fromInternal = fromUserId.HasValue;
            bool toInternal = toUserId.HasValue || route?.TargetUserId != null;

            if (fromInternal && !toInternal) {
                return "Outbound";
            }

            if (!fromInternal && toInternal) {
                return "Inbound";
            }

            return "Internal";
        }

        /// <summary>
        /// The DetermineCallType
        /// </summary>
        /// <param name="callDirection">The callDirection<see cref="string"/></param>
        /// <param name="route">The route<see cref="SIPIntResolvedTarget?"/></param>
        /// <param name="isServiceSession">The isServiceSession<see cref="bool"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string DetermineCallType(string callDirection, SIPIntResolvedTarget? route, bool isServiceSession) {
            if (isServiceSession || (route?.IsServiceTarget ?? false)) {
                return "Service";
            }

            if (route?.IsFaxTarget ?? false) {
                return "Fax";
            }

            if (route?.IsGsmTarget ?? false) {
                return "GSM";
            }

            if (route?.IsExternalTarget ?? false) {
                return "External";
            }

            return callDirection == "Internal" ? "Internal" : "External";
        }

        /// <summary>
        /// The DetermineRouteKind
        /// </summary>
        /// <param name="route">The route<see cref="SIPIntResolvedTarget?"/></param>
        /// <param name="isServiceSession">The isServiceSession<see cref="bool"/></param>
        /// <param name="fromUser">The fromUser<see cref="DataRow?"/></param>
        /// <param name="toUser">The toUser<see cref="DataRow?"/></param>
        /// <returns>The <see cref="string?"/></returns>
        private static string? DetermineRouteKind(SIPIntResolvedTarget? route, bool isServiceSession, DataRow? fromUser, DataRow? toUser) {
            if (route == null) {
                return null;
            }

            if (isServiceSession || route.IsServiceTarget) {
                return "Service";
            }

            if (route.IsFaxTarget || IsFaxUser(fromUser) || IsFaxUser(toUser)) {
                return "FaxUser";
            }

            if (route.IsGsmTarget) {
                return "GSM";
            }

            if (route.IsExternalTarget) {
                return "Provider";
            }

            if (route.IsUserTarget) {
                return "User";
            }

            return "Unknown";
        }

        /// <summary>
        /// The IsFaxUser
        /// </summary>
        /// <param name="user">The user<see cref="DataRow?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool IsFaxUser(DataRow? user) {
            if (user == null || !user.Table.Columns.Contains("UserType") || user["UserType"] == DBNull.Value) {
                return false;
            }

            return string.Equals(Convert.ToString(user["UserType"])?.Trim(), TKFax_Routing.FaxUserType, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The DetermineInternalNumber
        /// </summary>
        /// <param name="callDirection">The callDirection<see cref="string"/></param>
        /// <param name="fromUser">The fromUser<see cref="DataRow?"/></param>
        /// <param name="toUser">The toUser<see cref="DataRow?"/></param>
        /// <param name="fromNumber">The fromNumber<see cref="string"/></param>
        /// <param name="toNumber">The toNumber<see cref="string"/></param>
        /// <returns>The <see cref="string?"/></returns>
        private static string? DetermineInternalNumber(string callDirection, DataRow? fromUser, DataRow? toUser, string fromNumber, string toNumber) {
            if (callDirection == "Outbound") {
                return ReadString(fromUser, "Telefonnummer", fromNumber);
            }

            if (callDirection == "Inbound") {
                return ReadString(toUser, "Telefonnummer", toNumber);
            }

            return ReadString(toUser, "Telefonnummer", toNumber);
        }

        /// <summary>
        /// The DetermineExternalNumber
        /// </summary>
        /// <param name="callDirection">The callDirection<see cref="string"/></param>
        /// <param name="fromUserId">The fromUserId<see cref="int?"/></param>
        /// <param name="toUserId">The toUserId<see cref="int?"/></param>
        /// <param name="fromNumber">The fromNumber<see cref="string"/></param>
        /// <param name="toNumber">The toNumber<see cref="string"/></param>
        /// <returns>The <see cref="string?"/></returns>
        private static string? DetermineExternalNumber(string callDirection, int? fromUserId, int? toUserId, string fromNumber, string toNumber) {
            if (callDirection == "Outbound") {
                return toNumber;
            }

            if (callDirection == "Inbound") {
                return fromNumber;
            }

            if (!fromUserId.HasValue && toUserId.HasValue) {
                return fromNumber;
            }

            if (fromUserId.HasValue && !toUserId.HasValue) {
                return toNumber;
            }

            return null;
        }

        /// <summary>
        /// The DetermineOutboundExternalNumber
        /// </summary>
        /// <param name="route">The route<see cref="SIPIntResolvedTarget?"/></param>
        /// <returns>The <see cref="string?"/></returns>
        private static string? DetermineOutboundExternalNumber(SIPIntResolvedTarget? route) {
            if (route == null) {
                return null;
            }

            if (route.IsGsmTarget) {
                return route.GsmNumber;
            }

            if (route.IsExternalTarget) {
                return !string.IsNullOrWhiteSpace(route.ProviderNumber)
                    ? route.ProviderNumber
                    : route.ProviderSipUser;
            }

            return null;
        }

        /// <summary>
        /// The DetermineDisplayName
        /// </summary>
        /// <param name="user">The user<see cref="DataRow?"/></param>
        /// <param name="fallbackNumber">The fallbackNumber<see cref="string"/></param>
        /// <returns>The <see cref="string?"/></returns>
        private static string? DetermineDisplayName(DataRow? user, string fallbackNumber) {
            if (user == null) {
                return fallbackNumber;
            }

            string displayName = ReadString(user, "DisplayName", null);
            if (!string.IsNullOrWhiteSpace(displayName)) {
                return displayName;
            }

            string username = ReadString(user, "Username", null);
            if (!string.IsNullOrWhiteSpace(username)) {
                return username;
            }

            return ReadString(user, "Telefonnummer", fallbackNumber);
        }

        /// <summary>
        /// The NormalizeNullableNumber
        /// </summary>
        /// <param name="value">The value<see cref="string?"/></param>
        /// <returns>The <see cref="string?"/></returns>
        private static string? NormalizeNullableNumber(string? value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return null;
            }

            return TKSipExt_Routing.NormalizeExternalNumber(value);
        }

        /// <summary>
        /// The ReadNullableInt
        /// </summary>
        /// <param name="row">The row<see cref="DataRow?"/></param>
        /// <param name="columnName">The columnName<see cref="string"/></param>
        /// <returns>The <see cref="int?"/></returns>
        private static int? ReadNullableInt(DataRow? row, string columnName) {
            if (row == null || !row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value) {
                return null;
            }

            return Convert.ToInt32(row[columnName]);
        }

        /// <summary>
        /// The ReadString
        /// </summary>
        /// <param name="row">The row<see cref="DataRow?"/></param>
        /// <param name="columnName">The columnName<see cref="string"/></param>
        /// <param name="fallback">The fallback<see cref="string?"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string ReadString(DataRow? row, string columnName, string? fallback) {
            if (row == null || !row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value) {
                return fallback ?? string.Empty;
            }

            return (Convert.ToString(row[columnName]) ?? fallback ?? string.Empty).Trim();
        }
    }
}
