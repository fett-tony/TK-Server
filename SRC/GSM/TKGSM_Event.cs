/*
 * TKGSM_Events
 * Aufgabe:
 * - Nimmt GSM-Ereignisse getrennt nach Leg A und Leg B entgegen.
 * - Schreibt nur Bereichsereignisse nach oben ins Log/Runtime-Bild.
 * - Keine Routing-, Command- oder Media-Ausführung.
 */

namespace TKServer.GSM {
    using System;

    using TKServer.BASE;
    /// TKGSM_Events
    /// Aufgabe:
    /// - Nimmt GSM-Ereignisse getrennt nach Leg A und Leg B entgegen.
    /// - Schreibt nur Bereichsereignisse nach oben ins Log/Runtime-Bild.
    /// - Keine Routing-, Command- oder Media-Ausführung.

    /// <summary>
    /// Defines the <see cref="TKGSM_Events" />
    /// </summary>
    internal sealed class TKGSM_Events {
        /// <summary>
        /// Gets the LegA
        /// </summary>
        public LegAEvents LegA { get; }

        /// <summary>
        /// Gets the LegB
        /// </summary>
        public LegBEvents LegB { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TKGSM_Events"/> class.
        /// </summary>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        public TKGSM_Events(TKBase_LogManager? logger = null) {
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
            /// Initializes a new instance of the <see cref="LegAEvents"/> class.
            /// </summary>
            /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
            public LegAEvents(TKBase_LogManager? logger = null) {
                _logger = logger;
            }

            /// <summary>
            /// The HandleGsmInboundRinging
            /// </summary>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <param name="callId">The callId<see cref="string?"/></param>
            /// <param name="rawLine">The rawLine<see cref="string?"/></param>
            public void HandleGsmInboundRinging(string sessionId, string? callId, string? rawLine) => _logger?.Info($"GSM LegA Ringing | SessionID={sessionId} | CallID={callId ?? "-"} | Raw={rawLine ?? string.Empty}");

            /// <summary>
            /// The HandleGsmInboundAnswered
            /// </summary>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <param name="callId">The callId<see cref="string?"/></param>
            /// <param name="rawLine">The rawLine<see cref="string?"/></param>
            public void HandleGsmInboundAnswered(string sessionId, string? callId, string? rawLine) => _logger?.Info($"GSM LegA Answered | SessionID={sessionId} | CallID={callId ?? "-"} | Raw={rawLine ?? string.Empty}");

            /// <summary>
            /// The HandleGsmInboundEnded
            /// </summary>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <param name="callId">The callId<see cref="string?"/></param>
            /// <param name="reason">The reason<see cref="string?"/></param>
            public void HandleGsmInboundEnded(string sessionId, string? callId, string? reason) => _logger?.Info($"GSM LegA Ended | SessionID={sessionId} | CallID={callId ?? "-"} | Grund={reason ?? string.Empty}");

            /// <summary>
            /// The HandleSipInviteReceived
            /// </summary>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <param name="callId">The callId<see cref="string?"/></param>
            /// <param name="rawInvite">The rawInvite<see cref="string?"/></param>
            public void HandleSipInviteReceived(string sessionId, string? callId, string? rawInvite) => _logger?.Debug($"GSM LegA SIP INVITE empfangen | Ebene=Event | SessionID={sessionId} | CallID={callId ?? "-"} | RawLength={rawInvite?.Length ?? 0}{Environment.NewLine}--- SIP RAW START GSM LegA INVITE EVENT ---{Environment.NewLine}{rawInvite ?? string.Empty}{Environment.NewLine}--- SIP RAW END GSM LegA INVITE EVENT ---");

            /// <summary>
            /// The HandleSipByeReceived
            /// </summary>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <param name="callId">The callId<see cref="string?"/></param>
            /// <param name="rawBye">The rawBye<see cref="string?"/></param>
            public void HandleSipByeReceived(string sessionId, string? callId, string? rawBye) => _logger?.Debug($"GSM LegA SIP BYE empfangen | Ebene=Event | SessionID={sessionId} | CallID={callId ?? "-"} | RawLength={rawBye?.Length ?? 0}{Environment.NewLine}--- SIP RAW START GSM LegA BYE EVENT ---{Environment.NewLine}{rawBye ?? string.Empty}{Environment.NewLine}--- SIP RAW END GSM LegA BYE EVENT ---");

            /// <summary>
            /// The HandleSipCancelReceived
            /// </summary>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <param name="callId">The callId<see cref="string?"/></param>
            /// <param name="rawCancel">The rawCancel<see cref="string?"/></param>
            public void HandleSipCancelReceived(string sessionId, string? callId, string? rawCancel) => _logger?.Debug($"GSM LegA SIP CANCEL empfangen | Ebene=Event | SessionID={sessionId} | CallID={callId ?? "-"} | RawLength={rawCancel?.Length ?? 0}{Environment.NewLine}--- SIP RAW START GSM LegA CANCEL EVENT ---{Environment.NewLine}{rawCancel ?? string.Empty}{Environment.NewLine}--- SIP RAW END GSM LegA CANCEL EVENT ---");

            /// <summary>
            /// The HandleSipResponseSent
            /// </summary>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <param name="callId">The callId<see cref="string?"/></param>
            /// <param name="statusCode">The statusCode<see cref="int"/></param>
            /// <param name="reason">The reason<see cref="string?"/></param>
            public void HandleSipResponseSent(string sessionId, string? callId, int statusCode, string? reason) => _logger?.Info($"GSM LegA SIP Response gesendet | SessionID={sessionId} | CallID={callId ?? "-"} | Status={statusCode} | Grund={reason ?? string.Empty}");
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
            /// Initializes a new instance of the <see cref="LegBEvents"/> class.
            /// </summary>
            /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
            public LegBEvents(TKBase_LogManager? logger = null) {
                _logger = logger;
            }

            /// <summary>
            /// The HandleGsmDialStarted
            /// </summary>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <param name="callId">The callId<see cref="string?"/></param>
            /// <param name="statusId">The statusId<see cref="int?"/></param>
            /// <param name="target">The target<see cref="string"/></param>
            public void HandleGsmDialStarted(string sessionId, string? callId, int? statusId, string target) => _logger?.Info($"GSM LegB Dial gestartet | SessionID={sessionId} | CallID={callId ?? "-"} | StatusID={statusId?.ToString() ?? "-"} | Ziel={target}");

            /// <summary>
            /// The HandleGsmRinging
            /// </summary>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <param name="callId">The callId<see cref="string?"/></param>
            /// <param name="rawLine">The rawLine<see cref="string?"/></param>
            public void HandleGsmRinging(string sessionId, string? callId, string? rawLine) => _logger?.Info($"GSM LegB Ringing | SessionID={sessionId} | CallID={callId ?? "-"} | Raw={rawLine ?? string.Empty}");

            /// <summary>
            /// The HandleGsmConnected
            /// </summary>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <param name="callId">The callId<see cref="string?"/></param>
            /// <param name="rawLine">The rawLine<see cref="string?"/></param>
            public void HandleGsmConnected(string sessionId, string? callId, string? rawLine) => _logger?.Info($"GSM LegB Connected | SessionID={sessionId} | CallID={callId ?? "-"} | Raw={rawLine ?? string.Empty}");

            /// <summary>
            /// The HandleGsmEnded
            /// </summary>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <param name="callId">The callId<see cref="string?"/></param>
            /// <param name="reason">The reason<see cref="string?"/></param>
            public void HandleGsmEnded(string sessionId, string? callId, string? reason) => _logger?.Info($"GSM LegB Ended | SessionID={sessionId} | CallID={callId ?? "-"} | Grund={reason ?? string.Empty}");

            /// <summary>
            /// The HandleSipInviteSent
            /// </summary>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <param name="callId">The callId<see cref="string?"/></param>
            /// <param name="rawInvite">The rawInvite<see cref="string?"/></param>
            public void HandleSipInviteSent(string sessionId, string? callId, string? rawInvite) => _logger?.Debug($"GSM LegB SIP INVITE gesendet | Ebene=Event | SessionID={sessionId} | CallID={callId ?? "-"} | RawLength={rawInvite?.Length ?? 0}{Environment.NewLine}--- SIP RAW START GSM LegB INVITE EVENT ---{Environment.NewLine}{rawInvite ?? string.Empty}{Environment.NewLine}--- SIP RAW END GSM LegB INVITE EVENT ---");

            /// <summary>
            /// The HandleSipResponseReceived
            /// </summary>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <param name="callId">The callId<see cref="string?"/></param>
            /// <param name="statusCode">The statusCode<see cref="int"/></param>
            /// <param name="reason">The reason<see cref="string?"/></param>
            public void HandleSipResponseReceived(string sessionId, string? callId, int statusCode, string? reason) => _logger?.Info($"GSM LegB SIP Response empfangen | SessionID={sessionId} | CallID={callId ?? "-"} | Status={statusCode} | Grund={reason ?? string.Empty}");

            /// <summary>
            /// The HandleSipByeSent
            /// </summary>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <param name="callId">The callId<see cref="string?"/></param>
            /// <param name="rawBye">The rawBye<see cref="string?"/></param>
            public void HandleSipByeSent(string sessionId, string? callId, string? rawBye) => _logger?.Debug($"GSM LegB SIP BYE gesendet | Ebene=Event | SessionID={sessionId} | CallID={callId ?? "-"} | RawLength={rawBye?.Length ?? 0}{Environment.NewLine}--- SIP RAW START GSM LegB BYE EVENT ---{Environment.NewLine}{rawBye ?? string.Empty}{Environment.NewLine}--- SIP RAW END GSM LegB BYE EVENT ---");
        }
    }
}
