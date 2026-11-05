/*
 * TKFax_Session
 * Aufgabe:
 * - Stellt die bereichsspezifische Sicht auf eine Laufzeitsession aus dem Bereich Fax bereit.
 * - Hält nur den Fax-Blick auf Session, Leg A und Leg B.
 * - Leg A und Leg B sind getrennte Datentypen ohne gemeinsame Basisklasse.
 * - Keine Leg-A-Methode nutzt Leg-B-Methoden, keine Leg-B-Methode nutzt Leg-A-Methoden.
 * - Die echte Runtime-Session wird vom TKBase_SessionManager erzeugt.
 *
 * Nicht Aufgabe:
 * - Keine globale Sessionliste verwalten.
 * - Keine Session erzeugen.
 * - Keine SIP-, GSM-, Provider- oder Service-Transportlogik.
 * - Keine fachliche Verantwortung für fremde Bereiche übernehmen.
 *
 * Diese information darf nicht gelöscht werden!
 */

namespace TKServer.FAX {
    using System;

    using TKServer.BASE;
    /// <summary>
    /// Fax media mode negotiated between Fax.LegA and Fax.LegB.
    /// The server only relays media and is not a fax endpoint.
    /// </summary>
    internal enum TKFaxMediaMode {
        Unknown = 0,
        AudioPassthrough = 1,
        T38 = 2
    }

    /// <summary>
    /// Defines the <see cref="TKFax_Session" />
    /// </summary>
    internal sealed class TKFax_Session {
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
        /// Gets or sets the ViaNumber
        /// </summary>
        public string ViaNumber { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Status
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the FaxMode
        /// </summary>
        public TKFaxMediaMode FaxMode { get; set; } = TKFaxMediaMode.Unknown;

        /// <summary>
        /// Gets or sets the LastError
        /// </summary>
        public string LastError { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the StartedAt
        /// </summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>
        /// Gets or sets the UpdatedAt
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Gets or sets the EndedAt
        /// </summary>
        public DateTime? EndedAt { get; set; }

        /// <summary>
        /// Gets or sets the PagesTransferred
        /// </summary>
        public int PagesTransferred { get; set; }

        /// <summary>
        /// Gets or sets the FaxJobId
        /// </summary>
        public string FaxJobId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the LocalT38Address
        /// </summary>
        public string LocalT38Address { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the LocalT38Port
        /// </summary>
        public int LocalT38Port { get; set; }

        /// <summary>
        /// Gets or sets the RemoteT38Address
        /// </summary>
        public string RemoteT38Address { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the RemoteT38Port
        /// </summary>
        public int RemoteT38Port { get; set; }

        /// <summary>
        /// Gets or sets the LocalAudioAddress
        /// </summary>
        public string LocalAudioAddress { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the LocalAudioPort
        /// </summary>
        public int LocalAudioPort { get; set; }

        /// <summary>
        /// Gets or sets the RemoteAudioAddress
        /// </summary>
        public string RemoteAudioAddress { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the RemoteAudioPort
        /// </summary>
        public int RemoteAudioPort { get; set; }

        /// <summary>
        /// Gets or sets the AudioPayloadType
        /// </summary>
        public int AudioPayloadType { get; set; }

        /// <summary>
        /// Gets or sets the AudioCodec
        /// </summary>
        public string AudioCodec { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the LegA
        /// </summary>
        public TKFax_LegASession LegA { get; set; } = new();

        /// <summary>
        /// Gets or sets the LegB
        /// </summary>
        public TKFax_LegBSession LegB { get; set; } = new();

        /// <summary>
        /// The FromServerSession
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <returns>The <see cref="TKFax_Session"/></returns>
        internal static TKFax_Session FromServerSession(SIP_ServerSession session) {
            if (session == null) throw new ArgumentNullException(nameof(session));

            TKFax_Session fax = new TKFax_Session {
                SessionId = session.SessionId,
                SourceNumber = session.FromNumber,
                TargetNumber = session.ToNumber,
                Status = session.FaxStatus ?? session.Status,
                Direction = DetermineDirection(session),
                FaxMode = ParseFaxMode(session.FaxMode),
                LastError = session.FaxLastError ?? string.Empty,
                FaxJobId = session.FaxJobId ?? string.Empty,
                LocalT38Address = session.FaxLocalT38Address ?? string.Empty,
                LocalT38Port = session.FaxLocalT38Port ?? 0,
                RemoteT38Address = session.FaxRemoteT38Address ?? string.Empty,
                RemoteT38Port = session.FaxRemoteT38Port ?? 0,
                LocalAudioAddress = session.FaxLocalAudioAddress ?? string.Empty,
                LocalAudioPort = session.FaxLocalAudioPort ?? 0,
                RemoteAudioAddress = session.FaxRemoteAudioAddress ?? string.Empty,
                RemoteAudioPort = session.FaxRemoteAudioPort ?? 0,
                AudioPayloadType = session.FaxAudioPayloadType ?? 0,
                AudioCodec = session.FaxAudioCodec ?? string.Empty,
                StartedAt = session.CreatedAt,
                UpdatedAt = session.UpdatedAt
            };

            fax.LegA.SessionId = session.SessionId;
            fax.LegA.CallId = session.LegA.CallId ?? session.LegACallId;
            fax.LegA.Number = session.FromNumber;
            fax.LegA.ViaNumber = session.ProviderNumber ?? string.Empty;
            fax.LegA.Status = session.Status;
            fax.LegA.IsEnded = session.LegA.IsEnded;
            fax.LegA.IsAnswered = session.IsAnswered;

            fax.LegB.SessionId = session.SessionId;
            fax.LegB.CallId = session.LegB.CallId ?? string.Empty;
            fax.LegB.Number = session.ToNumber;
            fax.LegB.ViaNumber = session.ProviderNumber ?? string.Empty;
            fax.LegB.Status = session.Status;
            fax.LegB.IsEnded = session.LegB.IsEnded;
            fax.LegB.IsAnswered = session.IsAnswered;

            return fax;
        }

        /// <summary>
        /// The DetermineDirection
        /// </summary>
        /// <param name="session">The session<see cref="SIP_ServerSession"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string DetermineDirection(SIP_ServerSession session) {
            if (!string.IsNullOrWhiteSpace(session.FaxDirection)) {
                return session.FaxDirection!;
            }

            if (string.Equals(session.RouteTargetKind?.ToString(), "FaxUser", StringComparison.OrdinalIgnoreCase)) {
                return "Inbound";
            }

            if (string.Equals(session.FromUsername, "FAXUser", StringComparison.OrdinalIgnoreCase)) {
                return "Outbound";
            }

            return string.Empty;
        }

        /// <summary>
        /// The ParseFaxMode
        /// </summary>
        /// <param name="value">The value<see cref="string?"/></param>
        /// <returns>The <see cref="TKFaxMediaMode"/></returns>
        private static TKFaxMediaMode ParseFaxMode(string? value) {
            return Enum.TryParse(value, true, out TKFaxMediaMode mode) ? mode : TKFaxMediaMode.Unknown;
        }
    }

    /// <summary>
    /// Defines the <see cref="TKFax_LegASession" />
    /// </summary>
    internal sealed class TKFax_LegASession {
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
        /// Gets or sets the ViaNumber
        /// </summary>
        public string ViaNumber { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Status
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether IsAnswered
        /// </summary>
        public bool IsAnswered { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether IsEnded
        /// </summary>
        public bool IsEnded { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether IsSending
        /// </summary>
        public bool IsSending { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether T38Active
        /// </summary>
        public bool T38Active { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether AudioFaxActive
        /// </summary>
        public bool AudioFaxActive { get; set; }

    }

    /// <summary>
    /// Defines the <see cref="TKFax_LegBSession" />
    /// </summary>
    internal sealed class TKFax_LegBSession {
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
        /// Gets or sets the ViaNumber
        /// </summary>
        public string ViaNumber { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Status
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether IsAnswered
        /// </summary>
        public bool IsAnswered { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether IsEnded
        /// </summary>
        public bool IsEnded { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether IsReceiving
        /// </summary>
        public bool IsReceiving { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether T38Active
        /// </summary>
        public bool T38Active { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether AudioFaxActive
        /// </summary>
        public bool AudioFaxActive { get; set; }

    }
}
