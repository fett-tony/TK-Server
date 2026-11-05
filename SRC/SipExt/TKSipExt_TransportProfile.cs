/*
 * TKSipExt_TransportProfile
 * Aufgabe:
 * - Hält Transport- und URI-Basisdaten für den externen SIP-Bereich.
 * - Baut daraus AOR-, Contact-, Server- und Proxy-URIs.
 * - Normalisiert Host-, Port- und Protokollangaben.
 *
 * Nicht Aufgabe:
 * - Kein Routing.
 * - Keine Session-Steuerung.
 * - Keine Registration-Steuerung.
 * - Keine Call-Steuerung.
 * - Keine Media-Steuerung.
 *
 * Diese Datei ist ein technischer Hilfsbaustein für Transport- und URI-Profile im externen SIP-Bereich.
 */

namespace TKServer.SipExt {
    using SIPSorcery.SIP;
    using System;
    using System.Net;

    /// TKSipExt_TransportProfile
    /// Aufgabe:
    /// - Hält Transport- und URI-Basisdaten für den externen SIP-Bereich.
    /// - Baut daraus AOR-, Contact-, Server- und Proxy-URIs.
    /// - Normalisiert Host-, Port- und Protokollangaben.
    /// 
    /// Nicht Aufgabe:
    /// - Kein Routing.
    /// - Keine Session-Steuerung.
    /// - Keine Registration-Steuerung.
    /// - Keine Call-Steuerung.
    /// - Keine Media-Steuerung.
    /// 
    /// Diese Datei ist ein technischer Hilfsbaustein für Transport- und URI-Profile im externen SIP-Bereich.

    /// <summary>
    /// Defines the <see cref="TKSipExt_TransportProfile" />
    /// </summary>
    internal sealed class TKSipExt_TransportProfile {
        /// <summary>
        /// Gets the Protocol
        /// </summary>
        public SIPProtocolsEnum Protocol { get; private set; }

        /// <summary>
        /// Gets the Scheme
        /// </summary>
        public SIPSchemesEnum Scheme { get; private set; }

        /// <summary>
        /// Gets the Port
        /// </summary>
        public int Port { get; private set; }

        /// <summary>
        /// Gets the Host
        /// </summary>
        public string Host { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the HostWithPort
        /// </summary>
        public string HostWithPort => BuildHostWithPort(Host, Port);

        /// <summary>
        /// Gets the RegistrarHostPort
        /// </summary>
        public string RegistrarHostPort => HostWithPort;

        /// <summary>
        /// Gets the TransportToken
        /// </summary>
        public string TransportToken => Protocol.ToString().ToUpperInvariant();

        /// <summary>
        /// The FromDb
        /// </summary>
        /// <param name="host">The host<see cref="string?"/></param>
        /// <param name="port">The port<see cref="int?"/></param>
        /// <param name="proto">The proto<see cref="string?"/></param>
        /// <returns>The <see cref="TKSipExt_TransportProfile"/></returns>
        public static TKSipExt_TransportProfile FromDb(string? host, int? port, string? proto) {
            var protocol = ParseProtocol(proto);
            var scheme = protocol == SIPProtocolsEnum.tls ? SIPSchemesEnum.sips : SIPSchemesEnum.sip;
            if (!port.HasValue || port.Value <= 0 || port.Value > IPEndPoint.MaxPort) {
                throw new ArgumentException($"SIP-Port fehlt oder ist ungültig: {port}", nameof(port));
            }

            var effectivePort = port.Value;

            var normalizedHost = NormalizeHost(host);
            if (string.IsNullOrWhiteSpace(normalizedHost)) {
                throw new ArgumentException("Host darf nicht leer sein.", nameof(host));
            }

            return new TKSipExt_TransportProfile {
                Protocol = protocol,
                Scheme = scheme,
                Port = effectivePort,
                Host = normalizedHost
            };
        }

        /// <summary>
        /// The CreateAor
        /// </summary>
        /// <param name="user">The user<see cref="string"/></param>
        /// <returns>The <see cref="SIPURI"/></returns>
        public SIPURI CreateAor(string user) {
            if (string.IsNullOrWhiteSpace(user)) {
                throw new ArgumentException("SIP-User darf nicht leer sein.", nameof(user));
            }

            return new SIPURI(user, HostWithPort, null, Scheme, Protocol);
        }

        /// <summary>
        /// The CreateContact
        /// </summary>
        /// <param name="localAddress">The localAddress<see cref="IPAddress"/></param>
        /// <param name="localPort">The localPort<see cref="int"/></param>
        /// <param name="user">The user<see cref="string?"/></param>
        /// <returns>The <see cref="SIPURI"/></returns>
        public SIPURI CreateContact(IPAddress localAddress, int localPort, string? user = null) {
            if (localAddress == null || IPAddress.Any.Equals(localAddress) || IPAddress.IPv6Any.Equals(localAddress)) {
                throw new ArgumentException("Lokale Contact-IP darf nicht 0.0.0.0 sein.", nameof(localAddress));
            }

            if (localPort <= 0 || localPort > IPEndPoint.MaxPort) {
                throw new ArgumentException($"Lokaler Contact-Port ist ungueltig: {localPort}", nameof(localPort));
            }

            return new SIPURI(user, $"{localAddress}:{localPort}", null, Scheme, Protocol);
        }

        /// <summary>
        /// The CreateServerUri
        /// </summary>
        /// <returns>The <see cref="SIPURI"/></returns>
        public SIPURI CreateServerUri() {
            return new SIPURI(null, HostWithPort, null, Scheme, Protocol);
        }

        /// <summary>
        /// The CreateProxyUri
        /// </summary>
        /// <param name="proxyHost">The proxyHost<see cref="string?"/></param>
        /// <returns>The <see cref="SIPURI?"/></returns>
        public SIPURI? CreateProxyUri(string? proxyHost) {
            var normalizedProxy = NormalizeHost(proxyHost);
            if (string.IsNullOrWhiteSpace(normalizedProxy)) {
                return null;
            }

            return new SIPURI(null, BuildHostWithPort(normalizedProxy, Port), null, Scheme, Protocol);
        }

        /// <summary>
        /// The ParseProtocol
        /// </summary>
        /// <param name="proto">The proto<see cref="string?"/></param>
        /// <returns>The <see cref="SIPProtocolsEnum"/></returns>
        public static SIPProtocolsEnum ParseProtocol(string? proto) {
            switch ((proto ?? string.Empty).Trim().ToUpperInvariant()) {
                case "TLS":
                    return SIPProtocolsEnum.tls;
                case "TCP":
                    return SIPProtocolsEnum.tcp;
                case "UDP":
                    return SIPProtocolsEnum.udp;
                default:
                    throw new ArgumentException($"SIP-Protokoll fehlt oder ist ungültig: {proto}", nameof(proto));
            }
        }

        /// <summary>
        /// The NormalizeHost
        /// </summary>
        /// <param name="input">The input<see cref="string?"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string NormalizeHost(string? input) {
            if (string.IsNullOrWhiteSpace(input)) {
                return string.Empty;
            }

            var s = input.Trim();

            if (s.StartsWith("sip:", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("sips:", StringComparison.OrdinalIgnoreCase)) {
                if (SIPURI.TryParse(s, out var uri) && uri != null) {
                    return uri.HostAddress;
                }
            }

            var semi = s.IndexOf(';');
            if (semi >= 0) {
                s = s.Substring(0, semi);
            }

            if (s.StartsWith("[") && s.Contains("]:")) {
                var idx = s.IndexOf("]:", StringComparison.Ordinal);
                return s.Substring(0, idx + 1);
            }

            var colonCount = 0;
            foreach (var ch in s) {
                if (ch == ':') colonCount++;
            }

            if (colonCount == 1) {
                var parts = s.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[1], out _)) {
                    return parts[0].Trim();
                }
            }

            return s.Trim();
        }

        /// <summary>
        /// The BuildHostWithPort
        /// </summary>
        /// <param name="host">The host<see cref="string"/></param>
        /// <param name="port">The port<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string BuildHostWithPort(string host, int port) {
            if (string.IsNullOrWhiteSpace(host)) {
                throw new ArgumentException("Host darf nicht leer sein.", nameof(host));
            }

            if (host.StartsWith("[") && host.EndsWith("]")) {
                return $"{host}:{port}";
            }

            if (host.Contains(":") && !host.StartsWith("[")) {
                return $"[{host}]:{port}";
            }

            return $"{host}:{port}";
        }
    }
}
