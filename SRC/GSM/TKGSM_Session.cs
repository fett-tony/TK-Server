/*
 * TKGSM_Session
 * Aufgabe:
 * - Bereichssicht für GSM-Sessions.
 * - Leg A und Leg B sind getrennt modelliert.
 * - Keine Session-Erzeugung; die erzeugt ausschließlich TKBase_SessionManager.
 */

namespace TKServer.GSM {
    using System;

    using TKServer.BASE;
    /// TKGSM_Session
    /// Aufgabe:
    /// - Bereichssicht für GSM-Sessions.
    /// - Leg A und Leg B sind getrennt modelliert.
    /// - Keine Session-Erzeugung; die erzeugt ausschließlich TKBase_SessionManager.

    /// <summary>
    /// Defines the <see cref="TKGSM_Session" />
    /// </summary>
    internal sealed class TKGSM_Session {
        /// <summary>
        /// Gets or sets the SessionId
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Direction
        /// </summary>
        public string Direction { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the SourceNumber
        /// </summary>
        public string SourceNumber { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the TargetNumber
        /// </summary>
        public string TargetNumber { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Status
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ModemSimStatusId
        /// </summary>
        public int? ModemSimStatusId { get; set; }

        /// <summary>
        /// Gets or sets the ModemId
        /// </summary>
        public int? ModemId { get; set; }

        /// <summary>
        /// Gets or sets the SimId
        /// </summary>
        public int? SimId { get; set; }

        /// <summary>
        /// Gets or sets the ProviderName
        /// </summary>
        public string ProviderName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Imei
        /// </summary>
        public string Imei { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ComPortAT
        /// </summary>
        public string ComPortAT { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ComPortAudio
        /// </summary>
        public string ComPortAudio { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ComPortNMEA
        /// </summary>
        public string ComPortNMEA { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the LastEventLine
        /// </summary>
        public string LastEventLine { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the LastEventAt
        /// </summary>
        public DateTime? LastEventAt { get; set; }

        /// <summary>
        /// Gets or sets the LegA
        /// </summary>
        public TKGSM_LegASession LegA { get; set; } = new();

        /// <summary>
        /// Gets or sets the LegB
        /// </summary>
        public TKGSM_LegBSession LegB { get; set; } = new();

        /// <summary>
        /// The FromServerSession
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <returns>The <see cref="TKGSM_Session"/></returns>
        internal static TKGSM_Session FromServerSession(SIP_ServerSession session) {
            TKGSM_Session gsm = new TKGSM_Session {
                SessionId = session.SessionId,
                Direction = session.GsmDirection ?? string.Empty,
                SourceNumber = session.FromNumber,
                TargetNumber = session.ToNumber,
                Status = session.Status,
                ModemSimStatusId = session.GsmStatusId,
                ModemId = session.GsmModemId,
                SimId = session.GsmSimId,
                ProviderName = session.ProviderName ?? string.Empty,
                Imei = session.GsmImei ?? string.Empty,
                ComPortAT = session.GsmComPortAT ?? string.Empty,
                ComPortAudio = session.GsmComPortAudio ?? string.Empty,
                LastEventLine = session.GsmLastSignal ?? string.Empty,
                LastEventAt = session.GsmLastEventAt
            };

            gsm.LegA.SessionId = session.SessionId;
            gsm.LegA.CallId = session.LegA.CallId ?? session.LegACallId;
            gsm.LegA.ModemSimStatusId = session.GsmStatusId;
            gsm.LegA.Number = session.FromNumber;
            gsm.LegA.Status = session.Status;

            gsm.LegB.SessionId = session.SessionId;
            gsm.LegB.CallId = session.LegB.CallId ?? string.Empty;
            gsm.LegB.ModemSimStatusId = session.GsmStatusId;
            gsm.LegB.Number = session.ToNumber;
            gsm.LegB.Status = session.Status;
            return gsm;
        }
    }

    /// <summary>
    /// Defines the <see cref="TKGSM_LegASession" />
    /// </summary>
    internal sealed class TKGSM_LegASession {
        /// <summary>
        /// Gets or sets the SessionId
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the CallId
        /// </summary>
        public string CallId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Number
        /// </summary>
        public string Number { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Status
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ModemSimStatusId
        /// </summary>
        public int? ModemSimStatusId { get; set; }
    }

    /// <summary>
    /// Defines the <see cref="TKGSM_LegBSession" />
    /// </summary>
    internal sealed class TKGSM_LegBSession {
        /// <summary>
        /// Gets or sets the SessionId
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the CallId
        /// </summary>
        public string CallId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Number
        /// </summary>
        public string Number { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Status
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ModemSimStatusId
        /// </summary>
        public int? ModemSimStatusId { get; set; }
    }
}
