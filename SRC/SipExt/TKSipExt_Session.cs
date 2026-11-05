/*
 * TKSipExt_Session
 * Aufgabe:
 * - Bereichsspezifische SipExt-Session mit getrenntem Leg A und Leg B.
 */

namespace TKServer.SipExt {
    using SIPSorcery.SIP;

    /// TKSipExt_Session
    /// Aufgabe:
    /// - Bereichsspezifische SipExt-Session mit getrenntem Leg A und Leg B.

    /// <summary>
    /// Defines the <see cref="TKSipExt_Session" />
    /// </summary>
    internal sealed class TKSipExt_Session {
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
        /// Gets or sets the ProviderName
        /// </summary>
        public string? ProviderName { get; set; }

        /// <summary>
        /// Gets or sets the ProviderAccount
        /// </summary>
        public string? ProviderAccount { get; set; }

        /// <summary>
        /// Gets or sets the LegA
        /// </summary>
        public TKSipExt_LegASession LegA { get; set; } = new();

        /// <summary>
        /// Gets or sets the LegB
        /// </summary>
        public TKSipExt_LegBSession LegB { get; set; } = new();
    }

    /// <summary>
    /// Defines the <see cref="TKSipExt_LegASession" />
    /// </summary>
    internal sealed class TKSipExt_LegASession {
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
    /// Defines the <see cref="TKSipExt_LegBSession" />
    /// </summary>
    internal sealed class TKSipExt_LegBSession {
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
