/*
 * TKSVC_Events
 * Aufgabe:
 * - Service-Bereich meldet ausschließlich Leg-D-Ereignisse.
 * - Keine Leg-A-/Leg-B-Events im Service-Bereich.
 */

namespace TKServer.SVC {

    using TKServer.BASE;

    /// TKSVC_Events
    /// Aufgabe:
    /// - Service-Bereich meldet ausschließlich Leg-D-Ereignisse.
    /// - Keine Leg-A-/Leg-B-Events im Service-Bereich.

    /// <summary>
    /// Defines the <see cref="TKSVC_Events" />
    /// </summary>
    internal sealed class TKSVC_Events {
        /// <summary>
        /// Gets the LegD
        /// </summary>
        public LegDEvents LegD { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TKSVC_Events"/> class.
        /// </summary>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        public TKSVC_Events(TKBase_LogManager? logger = null) {
            LegD = new LegDEvents(logger);
        }

        /// <summary>
        /// Defines the <see cref="LegDEvents" />
        /// </summary>
        internal sealed class LegDEvents {
            /// <summary>
            /// Defines the _logger
            /// </summary>
            private readonly TKBase_LogManager? _logger;

            /// <summary>
            /// Initializes a new instance of the <see cref="LegDEvents"/> class.
            /// </summary>
            /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
            public LegDEvents(TKBase_LogManager? logger = null) {
                _logger = logger;
            }

            /// <summary>
            /// The HandleServiceSelected
            /// </summary>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <param name="internalNumber">The internalNumber<see cref="string?"/></param>
            /// <param name="serviceType">The serviceType<see cref="string?"/></param>
            public void HandleServiceSelected(string sessionId, string? internalNumber, string? serviceType) {
                _logger?.Info($"SVC LegD Service gewaehlt | Ebene=Event | SessionID={sessionId} | Number={internalNumber} | Type={serviceType}");
            }

            /// <summary>
            /// The HandleServiceStarted
            /// </summary>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <param name="internalNumber">The internalNumber<see cref="string?"/></param>
            /// <param name="localRtpPort">The localRtpPort<see cref="int"/></param>
            public void HandleServiceStarted(string sessionId, string? internalNumber, int localRtpPort) {
                _logger?.Info($"SVC LegD Service gestartet | Ebene=Event | SessionID={sessionId} | Number={internalNumber} | RTP={localRtpPort}");
            }

            /// <summary>
            /// The HandleServiceFailed
            /// </summary>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <param name="internalNumber">The internalNumber<see cref="string?"/></param>
            /// <param name="reason">The reason<see cref="string?"/></param>
            public void HandleServiceFailed(string sessionId, string? internalNumber, string? reason) {
                _logger?.Warn($"SVC LegD Service fehlgeschlagen | SessionID={sessionId} | Number={internalNumber} | Grund={reason}");
            }

            /// <summary>
            /// The HandleServiceAnswered
            /// </summary>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <param name="internalNumber">The internalNumber<see cref="string?"/></param>
            public void HandleServiceAnswered(string sessionId, string? internalNumber) {
                _logger?.Info($"SVC LegD Service beantwortet | Ebene=Event | SessionID={sessionId} | Number={internalNumber}");
            }

            /// <summary>
            /// The HandleServiceEnded
            /// </summary>
            /// <param name="sessionId">The sessionId<see cref="string"/></param>
            /// <param name="internalNumber">The internalNumber<see cref="string?"/></param>
            /// <param name="reason">The reason<see cref="string?"/></param>
            public void HandleServiceEnded(string sessionId, string? internalNumber, string? reason) {
                _logger?.Info($"SVC LegD Service beendet | Ebene=Event | SessionID={sessionId} | Number={internalNumber} | Grund={reason}");
            }
        }
    }
}
