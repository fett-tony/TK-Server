/*
 * TKFax_Event
 * Aufgabe:
 * - Stellt Fax-Ereignisse getrennt nach Fax.LegA und Fax.LegB bereit.
 * - Meldet Fax-Zustände nach oben in die vorhandene Log-/Runtime-Sicht.
 * - Meldet SIP- und Medienbruecken-Zustaende der getrennten Fax-Legs.
 *
 * Nicht Aufgabe:
 * - Keine Routing-, Command- oder Media-Ausführung.
 * - Keine Session erzeugen.
 * - Kein eigener Logger; ausschließlich TKBase_LogManager nutzen.
 *
 * Diese information darf nicht gelöscht werden!
 */

namespace TKServer.FAX {
    using System;

    using TKServer.BASE;
    using TKServer.SQL;
    /// <summary>
    /// Defines the <see cref="TKFax_Events" />
    /// </summary>
    internal sealed class TKFax_Events {
        /// <summary>
        /// Gets the LegA
        /// </summary>
        public LegAEvents LegA { get; }

        /// <summary>
        /// Gets the LegB
        /// </summary>
        public LegBEvents LegB { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TKFax_Events"/> class.
        /// </summary>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        public TKFax_Events(TKBase_LogManager? logger = null) {
            LegA = new LegAEvents(logger);
            LegB = new LegBEvents(logger);
        }

        /// <summary>
        /// Defines the <see cref="LegAEvents" />
        /// </summary>
        internal sealed class LegAEvents {
            private readonly TKBase_LogManager? _logger;
            private readonly TKSQL_Commands _sipSql;

            public LegAEvents(TKBase_LogManager? logger = null) {
                _logger = logger;
                _sipSql = new TKSQL_Commands(logger);
            }

            public int HandleInviteReceived(string sessionId, string? legACallId, string? inviteInRaw) {
                _logger?.Info($"FAX LegA INVITE empfangen | Ebene=Event | SessionID={sessionId} | LegA={legACallId} | RawLength={inviteInRaw?.Length ?? 0}");
                return _sipSql.UpdateCallLogStatus(sessionId, "Trying");
            }

            public void HandleFaxSendPrepared(TKFax_Session faxSession) {
                if (faxSession == null) return;
                _logger?.Debug($"FAX LegA Send vorbereitet | SessionID={faxSession.SessionId} | CallID={faxSession.LegA.CallId}");
            }

            public void HandleFaxSendStarted(TKFax_Session faxSession) {
                if (faxSession == null) return;
                _logger?.Info($"FAX LegA Send gestartet | SessionID={faxSession.SessionId} | CallID={faxSession.LegA.CallId}");
            }



            public void HandleFaxSendEnded(TKFax_Session faxSession, string? reason = null) {
                if (faxSession == null) return;
                _logger?.Info($"FAX LegA Send beendet | SessionID={faxSession.SessionId} | CallID={faxSession.LegA.CallId} | Grund={reason ?? string.Empty}");
            }

            public void HandleFaxSendFailed(TKFax_Session faxSession, Exception? exception = null, string? reason = null) {
                if (faxSession == null) return;
                _logger?.Warn($"FAX LegA Send Fehler | SessionID={faxSession.SessionId} | CallID={faxSession.LegA.CallId} | Grund={reason ?? exception?.Message ?? string.Empty}");
            }
        }

        /// <summary>
        /// Defines the <see cref="LegBEvents" />
        /// </summary>
        internal sealed class LegBEvents {
            private readonly TKBase_LogManager? _logger;
            private readonly TKSQL_Commands _sipSql;

            public LegBEvents(TKBase_LogManager? logger = null) {
                _logger = logger;
                _sipSql = new TKSQL_Commands(logger);
            }

            public int HandleInviteOutbound(string sessionId, string? legBCallId, string? inviteOutRaw) {
                _logger?.Info($"FAX LegB INVITE gesendet | Ebene=Event | SessionID={sessionId} | LegB={legBCallId} | RawLength={inviteOutRaw?.Length ?? 0}");
                int rows = _sipSql.UpdateCallLogInviteOutbound(sessionId, legBCallId, inviteOutRaw);
                TKBase_CallManager.UpdateRuntimeInviteOutbound(sessionId, legBCallId);
                return rows;
            }

            public void HandleFaxReceivePrepared(TKFax_Session faxSession) {
                if (faxSession == null) return;
                _logger?.Debug($"FAX LegB Receive vorbereitet | SessionID={faxSession.SessionId} | CallID={faxSession.LegB.CallId}");
            }

            public void HandleFaxReceiveStarted(TKFax_Session faxSession) {
                if (faxSession == null) return;
                _logger?.Info($"FAX LegB Receive gestartet | SessionID={faxSession.SessionId} | CallID={faxSession.LegB.CallId}");
            }



            public void HandleFaxReceiveEnded(TKFax_Session faxSession, string? reason = null) {
                if (faxSession == null) return;
                _logger?.Info($"FAX LegB Receive beendet | SessionID={faxSession.SessionId} | CallID={faxSession.LegB.CallId} | Grund={reason ?? string.Empty}");
            }

            public void HandleFaxReceiveFailed(TKFax_Session faxSession, Exception? exception = null, string? reason = null) {
                if (faxSession == null) return;
                _logger?.Warn($"FAX LegB Receive Fehler | SessionID={faxSession.SessionId} | CallID={faxSession.LegB.CallId} | Grund={reason ?? exception?.Message ?? string.Empty}");
            }
        }
    }
}
