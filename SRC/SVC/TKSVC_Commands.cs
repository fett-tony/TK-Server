/*
 * TKSVC_Commands
 * Aufgabe:
 * - Service-Bereich ist ausschließlich Leg-D.
 * - Keine Leg-A-/Leg-B-Kommandos im Service-Bereich.
 * - SIP-Seite bleibt im jeweiligen SIP-Bereich, z. B. TKSipInt Leg A.
 */

namespace TKServer.SVC {
    using System;

    using TKServer.BASE;

    /// TKSVC_Commands
    /// Aufgabe:
    /// - Service-Bereich ist ausschließlich Leg-D.
    /// - Keine Leg-A-/Leg-B-Kommandos im Service-Bereich.
    /// - SIP-Seite bleibt im jeweiligen SIP-Bereich, z. B. TKSipInt Leg A.

    /// <summary>
    /// Defines the <see cref="TKSVC_Commands" />
    /// </summary>
    internal sealed class TKSVC_Commands {
        /// <summary>
        /// Gets the LegD
        /// </summary>
        public LegDCommands LegD { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TKSVC_Commands"/> class.
        /// </summary>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        public TKSVC_Commands(TKBase_LogManager? logger = null) {
            LegD = new LegDCommands(logger);
        }

        /// <summary>
        /// Defines the <see cref="LegDCommands" />
        /// </summary>
        internal sealed class LegDCommands {
            /// <summary>
            /// Defines the _logger
            /// </summary>
            private readonly TKBase_LogManager? _logger;

            /// <summary>
            /// Initializes a new instance of the <see cref="LegDCommands"/> class.
            /// </summary>
            /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
            public LegDCommands(TKBase_LogManager? logger = null) {
                _logger = logger;
            }

            /// <summary>
            /// The StoreServicePrepared
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="serviceLeg">The serviceLeg<see cref="TKSVC_Session"/></param>
            public void StoreServicePrepared(SIP_ServerSession session, TKSVC_Session serviceLeg) {
                if (session == null) throw new ArgumentNullException(nameof(session));
                if (serviceLeg == null) throw new ArgumentNullException(nameof(serviceLeg));

                session.LegD.ServiceLeg = serviceLeg;
                session.LegD.ServiceType = serviceLeg.ServiceType;
                session.LegD.ServiceCode = serviceLeg.ServiceCode;
                session.LegD.InternalNumber = serviceLeg.InternalNumber;
                session.LegD.DisplayName = serviceLeg.DisplayName;
                session.LegD.AutoAnswer = serviceLeg.AutoAnswer;
                session.LegD.KeepSessionUntilPeerEnded = serviceLeg.KeepSessionUntilPeerEnded;
                session.LegD.UpdatedAt = DateTime.Now;

                _logger?.Info($"SVC LegD vorbereitet | Ebene=Command | SessionID={session.SessionId} | LegD={session.LegD.CallId ?? "-"} | Service={serviceLeg.InternalNumber} | Display={serviceLeg.DisplayName} | Type={serviceLeg.ServiceType} | AutoAnswer={serviceLeg.AutoAnswer} | KeepSession={serviceLeg.KeepSessionUntilPeerEnded} | Prompt={serviceLeg.PromptFile ?? "-"}");
            }

            /// <summary>
            /// The StoreServiceStarted
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="result">The result<see cref="SIP_ServiceStartResult"/></param>
            public void StoreServiceStarted(SIP_ServerSession session, SIP_ServiceStartResult result) {
                if (session == null) throw new ArgumentNullException(nameof(session));
                if (result == null) throw new ArgumentNullException(nameof(result));

                session.LegD.PreparedAnswerSdp = result.AnswerSdp;
                session.LegD.RelayPort = result.LocalRtpPort;
                session.LegD.InviteSeen = true;
                session.LegD.UpdatedAt = DateTime.Now;

                if (result.Success) {
                    session.LegD.IsEnded = false;
                }

                _logger?.Info($"SVC LegD gestartet | Ebene=Command | SessionID={session.SessionId} | LegD={session.LegD.CallId ?? "-"} | Service={session.LegD.ServiceCode ?? "-"} | RTP={result.LocalRtpPort} | Success={result.Success} | Reason={result.Reason ?? "-"}");
            }

            /// <summary>
            /// The StoreServiceAnswered
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            public void StoreServiceAnswered(SIP_ServerSession session) {
                if (session == null) throw new ArgumentNullException(nameof(session));

                session.LegD.AckSeen = true;
                session.LegD.UpdatedAt = DateTime.Now;

                _logger?.Info($"SVC LegD beantwortet | Ebene=Command | SessionID={session.SessionId} | LegD={session.LegD.CallId ?? "-"} | Service={session.LegD.ServiceCode ?? "-"} | Answered={session.IsAnswered}");
            }

            /// <summary>
            /// The StoreServiceEnded
            /// </summary>
            /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
            /// <param name="reason">The reason<see cref="string?"/></param>
            public void StoreServiceEnded(SIP_ServerSession session, string? reason) {
                if (session == null) throw new ArgumentNullException(nameof(session));

                session.LegD.IsEnded = true;
                session.LegD.UpdatedAt = DateTime.Now;

                _logger?.Info($"SVC LegD beendet | Ebene=Command | SessionID={session.SessionId} | LegD={session.LegD.CallId ?? "-"} | Service={session.LegD.ServiceCode ?? "-"} | Grund={reason ?? "-"}");
            }
        }
    }
}
