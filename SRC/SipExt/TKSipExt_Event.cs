/*
 * TKSipExt_Events
 * Aufgabe:
 * - Nimmt SipExt-Ereignisse getrennt nach Leg A und Leg B entgegen.
 * - Schreibt externe SIP-/Provider-Ereignisse in CallLog/Runtime-Sicht.
 * - Leg-A-Ereignisse nutzen keine Leg-B-Methoden und Leg-B-Ereignisse nutzen keine Leg-A-Methoden.
 */

namespace TKServer.SipExt {
    using System;

    using TKServer.BASE;
    using TKServer.SQL;
    /// TKSipExt_Events
    /// Aufgabe:
    /// - Nimmt SipExt-Ereignisse getrennt nach Leg A und Leg B entgegen.
    /// - Schreibt externe SIP-/Provider-Ereignisse in CallLog/Runtime-Sicht.
    /// - Leg-A-Ereignisse nutzen keine Leg-B-Methoden und Leg-B-Ereignisse nutzen keine Leg-A-Methoden.

    /// <summary>
    /// Defines the <see cref="TKSipExt_Events" />
    /// </summary>
    internal sealed class TKSipExt_Events {
        /// <summary>
        /// Gets the LegA
        /// </summary>
        public LegAEvents LegA { get; }

        /// <summary>
        /// Gets the LegB
        /// </summary>
        public LegBEvents LegB { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TKSipExt_Events"/> class.
        /// </summary>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        public TKSipExt_Events(TKBase_LogManager? logger = null) {
            LegA = new LegAEvents(logger);
            LegB = new LegBEvents(logger);
        }

        /// <summary>
        /// Defines the <see cref="LegAEvents" />
        /// </summary>
        internal sealed class LegAEvents {
            /// <summary>
            /// Defines the _logger
            /// </summary>
            private readonly TKBase_LogManager? _logger;

            /// <summary>
            /// Defines the _sipSql
            /// </summary>
            private readonly TKSQL_Commands _sipSql;

            /// <summary>
            /// Initializes a new instance of the <see cref="LegAEvents"/> class.
            /// </summary>
            /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
            public LegAEvents(TKBase_LogManager? logger = null) {
                _logger = logger;
                _sipSql = new TKSQL_Commands(logger);
                }

            /// <summary>
            /// The HandleInviteReceived
            /// </summary>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <param name="legACallId">The legACallId<see cref="string?"/></param>
            /// <param name="inviteInRaw">The inviteInRaw<see cref="string?"/></param>
            /// <returns>The <see cref="int"/></returns>
            public int HandleInviteReceived(string sessionId, string? legACallId, string? inviteInRaw) {
                _logger?.Info($"SipExt LegA INVITE empfangen | Ebene=Event | SessionID={sessionId} | LegA={legACallId} | RawLength={inviteInRaw?.Length ?? 0}");
                return _sipSql.UpdateCallLogStatus(sessionId, "Trying");
            }

            /// <summary>
            /// The HandleRingingSent
            /// </summary>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <returns>The <see cref="int"/></returns>
            public int HandleRingingSent(string sessionId) {
                _logger?.Info($"SipExt LegA Ringing gesendet | SessionID={sessionId}");
                int rows = _sipSql.UpdateCallLogStatus(sessionId, "Ringing");
                TKBase_CallManager.MarkRuntimeRinging(sessionId);
                return rows;
            }

            /// <summary>
            /// The HandleAnsweredSent
            /// </summary>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <returns>The <see cref="int"/></returns>
            public int HandleAnsweredSent(string sessionId) {
                _logger?.Info($"SipExt LegA Answered gesendet | SessionID={sessionId}");
                int rows = _sipSql.UpdateCallLogStatus(sessionId, "Answered", answerTime: DateTime.Now);
                TKBase_CallManager.MarkRuntimeAnswered(sessionId);
                return rows;
            }

            /// <summary>
            /// The HandleByeIn
            /// </summary>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <param name="sipCallId">The sipCallId<see cref="string"/></param>
            /// <param name="byeInRaw">The byeInRaw<see cref="string?"/></param>
            /// <returns>The <see cref="int"/></returns>
            public int HandleByeIn(string sessionId, string sipCallId, string? byeInRaw) {
                _logger?.Info($"SipExt LegA BYE empfangen | Ebene=Event | SessionID={sessionId} | CallID={sipCallId} | RawLength={byeInRaw?.Length ?? 0}");
                int rows = _sipSql.RegisterByeIn(sipCallId, byeInRaw);
                _sipSql.UpdateCallLogStatus(sessionId, "Ended", endTime: DateTime.Now, disconnectReason: "SipExt LegA BYE");
                TKBase_CallManager.MarkRuntimeEnded(sessionId, "SipExt LegA BYE");
                return rows;
            }

            /// <summary>
            /// The HandleCancelIn
            /// </summary>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <param name="reason">The reason<see cref="string?"/></param>
            /// <returns>The <see cref="int"/></returns>
            public int HandleCancelIn(string sessionId, string? reason = null) {
                string text = string.IsNullOrWhiteSpace(reason) ? "SipExt LegA CANCEL" : reason;
                _logger?.Info($"SipExt LegA CANCEL empfangen | SessionID={sessionId} | Grund={text}");
                int rows = _sipSql.UpdateCallLogStatus(sessionId, "Ended", endTime: DateTime.Now, disconnectReason: text);
                TKBase_CallManager.MarkRuntimeEnded(sessionId, text);
                return rows;
            }

            /// <summary>
            /// The HandleFailedSent
            /// </summary>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <param name="reason">The reason<see cref="string?"/></param>
            /// <returns>The <see cref="int"/></returns>
            public int HandleFailedSent(string sessionId, string? reason = null) {
                _logger?.Warn($"SipExt LegA Fehlerantwort gesendet | SessionID={sessionId} | Grund={reason}");
                int rows = _sipSql.UpdateCallLogStatus(sessionId, "Failed", endTime: DateTime.Now, disconnectReason: reason);
                TKBase_CallManager.MarkRuntimeFailed(sessionId, reason);
                return rows;
            }

            /// <summary>
            /// The HandleEnded
            /// </summary>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <param name="reason">The reason<see cref="string?"/></param>
            /// <returns>The <see cref="int"/></returns>
            public int HandleEnded(string sessionId, string? reason = null) {
                _logger?.Info($"SipExt LegA beendet | SessionID={sessionId} | Grund={reason}");
                int rows = _sipSql.UpdateCallLogStatus(sessionId, "Ended", endTime: DateTime.Now, disconnectReason: reason);
                TKBase_CallManager.MarkRuntimeEnded(sessionId, reason);
                return rows;
            }
        }

        /// <summary>
        /// Defines the <see cref="LegBEvents" />
        /// </summary>
        internal sealed class LegBEvents {
            /// <summary>
            /// Defines the _logger
            /// </summary>
            private readonly TKBase_LogManager? _logger;

            /// <summary>
            /// Defines the _sipSql
            /// </summary>
            private readonly TKSQL_Commands _sipSql;

            /// <summary>
            /// Initializes a new instance of the <see cref="LegBEvents"/> class.
            /// </summary>
            /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
            public LegBEvents(TKBase_LogManager? logger = null) {
                _logger = logger;
                _sipSql = new TKSQL_Commands(logger);
                }

            /// <summary>
            /// The HandleInviteOutbound
            /// </summary>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <param name="legBCallId">The legBCallId<see cref="string?"/></param>
            /// <param name="inviteOutRaw">The inviteOutRaw<see cref="string?"/></param>
            /// <returns>The <see cref="int"/></returns>
            public int HandleInviteOutbound(string sessionId, string? legBCallId, string? inviteOutRaw) {
                _logger?.Info($"SipExt LegB INVITE gesendet | Ebene=Event | SessionID={sessionId} | LegB={legBCallId} | RawLength={inviteOutRaw?.Length ?? 0}");
                int rows = _sipSql.UpdateCallLogInviteOutbound(sessionId, legBCallId, inviteOutRaw);
                TKBase_CallManager.UpdateRuntimeInviteOutbound(sessionId, legBCallId);
                return rows;
            }

            /// <summary>
            /// The HandleAuthInviteOutbound
            /// </summary>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <param name="legBCallId">The legBCallId<see cref="string?"/></param>
            /// <param name="inviteOutRaw">The inviteOutRaw<see cref="string?"/></param>
            /// <returns>The <see cref="int"/></returns>
            public int HandleAuthInviteOutbound(string sessionId, string? legBCallId, string? inviteOutRaw) {
                _logger?.Info($"SipExt LegB Auth-INVITE gesendet | Ebene=Event | SessionID={sessionId} | LegB={legBCallId} | RawLength={inviteOutRaw?.Length ?? 0}");
                int rows = _sipSql.UpdateCallLogInviteOutbound(sessionId, legBCallId, inviteOutRaw);
                TKBase_CallManager.UpdateRuntimeInviteOutbound(sessionId, legBCallId);
                return rows;
            }

            /// <summary>
            /// The HandleRingingReceived
            /// </summary>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <returns>The <see cref="int"/></returns>
            public int HandleRingingReceived(string sessionId) {
                _logger?.Info($"SipExt LegB Ringing empfangen | SessionID={sessionId}");
                int rows = _sipSql.UpdateCallLogStatus(sessionId, "Ringing");
                TKBase_CallManager.MarkRuntimeRinging(sessionId);
                return rows;
            }

            /// <summary>
            /// The HandleAnsweredReceived
            /// </summary>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <returns>The <see cref="int"/></returns>
            public int HandleAnsweredReceived(string sessionId) {
                _logger?.Info($"SipExt LegB Answered empfangen | SessionID={sessionId}");
                int rows = _sipSql.UpdateCallLogStatus(sessionId, "Answered", answerTime: DateTime.Now);
                TKBase_CallManager.MarkRuntimeAnswered(sessionId);
                return rows;
            }

            /// <summary>
            /// The HandleByeOut
            /// </summary>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <param name="sipCallId">The sipCallId<see cref="string"/></param>
            /// <param name="byeOutRaw">The byeOutRaw<see cref="string?"/></param>
            /// <returns>The <see cref="int"/></returns>
            public int HandleByeOut(string sessionId, string sipCallId, string? byeOutRaw) {
                _logger?.Info($"SipExt LegB BYE gesendet | Ebene=Event | SessionID={sessionId} | CallID={sipCallId} | RawLength={byeOutRaw?.Length ?? 0}");
                int rows = _sipSql.RegisterByeOut(sipCallId, byeOutRaw);
                _sipSql.UpdateCallLogStatus(sessionId, "Ended", endTime: DateTime.Now, disconnectReason: "SipExt LegB BYE");
                TKBase_CallManager.MarkRuntimeEnded(sessionId, "SipExt LegB BYE");
                return rows;
            }

            /// <summary>
            /// The HandleFailedReceived
            /// </summary>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <param name="reason">The reason<see cref="string?"/></param>
            /// <returns>The <see cref="int"/></returns>
            public int HandleFailedReceived(string sessionId, string? reason = null) {
                _logger?.Warn($"SipExt LegB Fehlerantwort empfangen | SessionID={sessionId} | Grund={reason}");
                int rows = _sipSql.UpdateCallLogStatus(sessionId, "Failed", endTime: DateTime.Now, disconnectReason: reason);
                TKBase_CallManager.MarkRuntimeFailed(sessionId, reason);
                return rows;
            }

            /// <summary>
            /// The HandleEnded
            /// </summary>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <param name="reason">The reason<see cref="string?"/></param>
            /// <returns>The <see cref="int"/></returns>
            public int HandleEnded(string sessionId, string? reason = null) {
                _logger?.Info($"SipExt LegB beendet | SessionID={sessionId} | Grund={reason}");
                int rows = _sipSql.UpdateCallLogStatus(sessionId, "Ended", endTime: DateTime.Now, disconnectReason: reason);
                TKBase_CallManager.MarkRuntimeEnded(sessionId, reason);
                return rows;
            }
        }
    }
}
