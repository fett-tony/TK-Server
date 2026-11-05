/*
 * TKSipInt_Session
 * Aufgabe:
 * - Stellt die bereichsspezifische Sicht auf eine Laufzeitsession aus dem Bereich SipInt bereit.
 * - Hält nur den internen SIP-Blick auf Session, Leg A und Leg B.
 * - Leg A und Leg B sind getrennte Datentypen ohne gemeinsame Basisklasse.
 * - Keine Leg-A-Methode nutzt Leg-B-Methoden, keine Leg-B-Methode nutzt Leg-A-Methoden.
 * - Die echte Runtime-Session wird vom TKBase_SessionManager erzeugt.
 *
 * Nicht Aufgabe:
 * - Keine globale Sessionliste verwalten.
 * - Keine Session erzeugen.
 * - Keine Protokollsteuerung oder Dispatch-Logik.
 * - Keine fachliche Verantwortung für fremde Bereiche übernehmen.
 * 
 * Diese information darf nicht gelöscht werden!
 */

namespace TKServer.SipInt {
    using SIPSorcery.SIP;

    /// TKSipInt_Session
    /// Aufgabe:
    /// - Stellt die bereichsspezifische Sicht auf eine Laufzeitsession aus dem Bereich SipInt bereit.
    /// - Hält nur den internen SIP-Blick auf Session, Leg A und Leg B.
    /// - Leg A und Leg B sind getrennte Datentypen ohne gemeinsame Basisklasse.
    /// - Keine Leg-A-Methode nutzt Leg-B-Methoden, keine Leg-B-Methode nutzt Leg-A-Methoden.
    /// - Die echte Runtime-Session wird vom TKBase_SessionManager erzeugt.
    /// 
    /// Nicht Aufgabe:
    /// - Keine globale Sessionliste verwalten.
    /// - Keine Session erzeugen.
    /// - Keine Protokollsteuerung oder Dispatch-Logik.
    /// - Keine fachliche Verantwortung für fremde Bereiche übernehmen.
    /// 
    /// Diese information darf nicht gelöscht werden!

    /// <summary>
    /// Defines the <see cref="TKSipInt_Session" />
    /// </summary>
    internal sealed class TKSipInt_Session {
        /// <summary>
        /// Gets or sets the SessionId
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the FromNumber
        /// </summary>
        public string FromNumber { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ToNumber
        /// </summary>
        public string ToNumber { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Status
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the LegA
        /// </summary>
        public TKSipInt_LegASession LegA { get; set; } = new();

        /// <summary>
        /// Gets or sets the LegB
        /// </summary>
        public TKSipInt_LegBSession LegB { get; set; } = new();
    }

    /// <summary>
    /// Defines the <see cref="TKSipInt_LegASession" />
    /// </summary>
    internal sealed class TKSipInt_LegASession {
        /// <summary>
        /// Gets or sets the CallId
        /// </summary>
        public string? CallId { get; set; }

        /// <summary>
        /// Gets or sets the SipEndPoint
        /// </summary>
        public SIPEndPoint? SipEndPoint { get; set; }

        /// <summary>
        /// Gets or sets the LocalSipEndPoint
        /// </summary>
        public SIPEndPoint? LocalSipEndPoint { get; set; }

        /// <summary>
        /// Gets or sets the ContactUri
        /// </summary>
        public SIPURI? ContactUri { get; set; }

        /// <summary>
        /// Gets or sets the RequestUri
        /// </summary>
        public SIPURI? RequestUri { get; set; }

        /// <summary>
        /// Gets or sets the FromTag
        /// </summary>
        public string? FromTag { get; set; }

        /// <summary>
        /// Gets or sets the ToTag
        /// </summary>
        public string? ToTag { get; set; }

        /// <summary>
        /// Gets or sets the DialogCSeq
        /// </summary>
        public int DialogCSeq { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether InviteSeen
        /// </summary>
        public bool InviteSeen { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether AckSeen
        /// </summary>
        public bool AckSeen { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether ByeSeen
        /// </summary>
        public bool ByeSeen { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether CancelSeen
        /// </summary>
        public bool CancelSeen { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether IsEnded
        /// </summary>
        public bool IsEnded { get; set; }
    }

    /// <summary>
    /// Defines the <see cref="TKSipInt_LegBSession" />
    /// </summary>
    internal sealed class TKSipInt_LegBSession {
        /// <summary>
        /// Gets or sets the CallId
        /// </summary>
        public string? CallId { get; set; }

        /// <summary>
        /// Gets or sets the SipEndPoint
        /// </summary>
        public SIPEndPoint? SipEndPoint { get; set; }

        /// <summary>
        /// Gets or sets the LocalSipEndPoint
        /// </summary>
        public SIPEndPoint? LocalSipEndPoint { get; set; }

        /// <summary>
        /// Gets or sets the ContactUri
        /// </summary>
        public SIPURI? ContactUri { get; set; }

        /// <summary>
        /// Gets or sets the RequestUri
        /// </summary>
        public SIPURI? RequestUri { get; set; }

        /// <summary>
        /// Gets or sets the RemoteTargetUri
        /// </summary>
        public SIPURI? RemoteTargetUri { get; set; }

        /// <summary>
        /// Gets or sets the TargetContactUri
        /// </summary>
        public string? TargetContactUri { get; set; }

        /// <summary>
        /// Gets or sets the FromTag
        /// </summary>
        public string? FromTag { get; set; }

        /// <summary>
        /// Gets or sets the ToTag
        /// </summary>
        public string? ToTag { get; set; }

        /// <summary>
        /// Gets or sets the DialogCSeq
        /// </summary>
        public int DialogCSeq { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether InviteSeen
        /// </summary>
        public bool InviteSeen { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether AckSeen
        /// </summary>
        public bool AckSeen { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether ByeSeen
        /// </summary>
        public bool ByeSeen { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether CancelSeen
        /// </summary>
        public bool CancelSeen { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether IsEnded
        /// </summary>
        public bool IsEnded { get; set; }
    }
}
