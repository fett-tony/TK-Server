namespace TKServer.BASE {
    using SIPSorcery.SIP;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.NetworkInformation;

    /// <summary>
    /// Defines the <see cref="TKBase_TransportHost" />
    /// </summary>
    internal static class TKBase_TransportHost {
        /// <summary>
        /// Defines the _sync
        /// </summary>
        private static readonly object _sync = new();

        /// <summary>
        /// Defines the _consumers
        /// </summary>
        private static readonly HashSet<string> _consumers = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Defines the _channels
        /// </summary>
        private static readonly Dictionary<string, SIPChannel> _channels = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Defines the _transport
        /// </summary>
        private static SIPTransport? _transport;

        /// <summary>
        /// The Acquire
        /// </summary>
        /// <param name="consumer">The consumer<see cref="string"/></param>
        /// <param name="mode">The mode<see cref="string"/></param>
        /// <param name="port">The port<see cref="int"/></param>
        /// <param name="listenAddresses">The listenAddresses<see cref="IReadOnlyList{IPAddress}"/></param>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        /// <returns>The <see cref="SIPTransport"/></returns>
        public static SIPTransport Acquire(string consumer, string mode, int port, IReadOnlyList<IPAddress> listenAddresses, TKBase_LogManager? logger) {
            if (string.IsNullOrWhiteSpace(consumer)) {
                throw new ArgumentException("consumer darf nicht leer sein.", nameof(consumer));
            }

            string normalizedMode = NormalizeMode(mode);
            int normalizedPort = ValidatePort(port);
            IReadOnlyList<IPAddress> normalizedAddresses = NormalizeListenAddresses(listenAddresses, logger);

            lock (_sync) {
                SIPTransport? transportForCleanup = null;
                bool createdTransport = _transport == null;

                try {
                    _transport ??= new SIPTransport();
                    SetupChannels(_transport, normalizedMode, normalizedPort, normalizedAddresses, logger);

                    _consumers.Add(consumer);
                    logger?.Info($"SIP_TransportHost Acquire | Consumer={consumer} | Aktiv={string.Join(",", _consumers.OrderBy(x => x))} | Angefordert={normalizedMode}/{normalizedPort} | IPs={string.Join(";", normalizedAddresses.Select(x => x.ToString()))} | Channels={DescribeChannels()}");
                    return _transport;
                } catch {
                    if (createdTransport && _consumers.Count == 0) {
                        transportForCleanup = _transport;
                        _transport = null;
                        _channels.Clear();
                    }

                    if (transportForCleanup != null) {
                        try {
                            transportForCleanup.Shutdown();
                            transportForCleanup.Dispose();
                        } catch (Exception ex) {
                            logger?.Warn($"SIP_TransportHost Acquire-Cleanup Warnung | {ex.Message}");
                        }
                    }

                    throw;
                }
            }
        }

        /// <summary>
        /// The Release
        /// </summary>
        /// <param name="consumer">The consumer<see cref="string"/></param>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        public static void Release(string consumer, TKBase_LogManager? logger) {
            if (string.IsNullOrWhiteSpace(consumer)) {
                return;
            }

            SIPTransport? transportToShutdown = null;

            lock (_sync) {
                _consumers.Remove(consumer);
                logger?.Info($"SIP_TransportHost Release | Consumer={consumer} | Verbleibend={string.Join(",", _consumers.OrderBy(x => x))}");

                if (_consumers.Count == 0) {
                    transportToShutdown = _transport;
                    _transport = null;
                    _channels.Clear();
                }
            }

            if (transportToShutdown != null) {
                try {
                    transportToShutdown.Shutdown();
                    transportToShutdown.Dispose();
                } catch (Exception ex) {
                    logger?.Warn($"SIP_TransportHost Shutdown Warnung | {ex.Message}");
                }

                logger?.Info("SIP_TransportHost freigegeben");
            }
        }

        /// <summary>
        /// The SetupChannels
        /// </summary>
        /// <param name="transport">The transport<see cref="SIPTransport"/></param>
        /// <param name="mode">The mode<see cref="string"/></param>
        /// <param name="port">The port<see cref="int"/></param>
        /// <param name="listenAddresses">The listenAddresses<see cref="IReadOnlyList{IPAddress}"/></param>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        private static void SetupChannels(SIPTransport transport, string mode, int port, IReadOnlyList<IPAddress> listenAddresses, TKBase_LogManager? logger) {
            foreach (IPAddress listenAddress in listenAddresses) {
                switch (mode) {
                    case "TLS":
                        EnsureTlsChannel(transport, listenAddress, port, logger);
                        break;

                    case "TCP":
                        EnsureTcpChannel(transport, listenAddress, port, logger);
                        break;

                    case "UDP+TCP":
                        EnsureUdpChannel(transport, listenAddress, port, logger);
                        EnsureTcpChannel(transport, listenAddress, port, logger);
                        break;

                    case "UDP":
                        EnsureUdpChannel(transport, listenAddress, port, logger);
                        break;

                    default:
                        throw new InvalidOperationException($"SIP Transportmodus ungültig: {mode}");
                }
            }
        }

        /// <summary>
        /// The EnsureUdpChannel
        /// </summary>
        /// <param name="transport">The transport<see cref="SIPTransport"/></param>
        /// <param name="listenAddress">The listenAddress<see cref="IPAddress"/></param>
        /// <param name="port">The port<see cref="int"/></param>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        private static void EnsureUdpChannel(SIPTransport transport, IPAddress listenAddress, int port, TKBase_LogManager? logger) {
            string key = BuildKey("UDP", listenAddress, port);
            if (_channels.ContainsKey(key)) {
                return;
            }

            SIPUDPChannel channel = new(new IPEndPoint(listenAddress, port));
            transport.AddSIPChannel(channel);
            _channels[key] = channel;
            logger?.Info($"SIP_TransportHost Channel aktiv | Mode=UDP | IP={listenAddress} | Port={port} | Local={channel.ListeningSIPEndPoint}");
        }

        /// <summary>
        /// The EnsureTcpChannel
        /// </summary>
        /// <param name="transport">The transport<see cref="SIPTransport"/></param>
        /// <param name="listenAddress">The listenAddress<see cref="IPAddress"/></param>
        /// <param name="port">The port<see cref="int"/></param>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        private static void EnsureTcpChannel(SIPTransport transport, IPAddress listenAddress, int port, TKBase_LogManager? logger) {
            string key = BuildKey("TCP", listenAddress, port);
            if (_channels.ContainsKey(key)) {
                return;
            }

            SIPTCPChannel channel = new(new IPEndPoint(listenAddress, port));
            transport.AddSIPChannel(channel);
            _channels[key] = channel;
            logger?.Info($"SIP_TransportHost Channel aktiv | Mode=TCP | IP={listenAddress} | Port={port} | Local={channel.ListeningSIPEndPoint}");
        }

        /// <summary>
        /// The EnsureTlsChannel
        /// </summary>
        /// <param name="transport">The transport<see cref="SIPTransport"/></param>
        /// <param name="listenAddress">The listenAddress<see cref="IPAddress"/></param>
        /// <param name="port">The port<see cref="int"/></param>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        private static void EnsureTlsChannel(SIPTransport transport, IPAddress listenAddress, int port, TKBase_LogManager? logger) {
            string key = BuildKey("TLS", listenAddress, port);
            if (_channels.ContainsKey(key)) {
                return;
            }

            SIPTLSChannel channel = new(new IPEndPoint(listenAddress, port)) {
                BypassCertificateValidation = false
            };
            transport.AddSIPChannel(channel);
            _channels[key] = channel;
            logger?.Info($"SIP_TransportHost Channel aktiv | Mode=TLS | IP={listenAddress} | Port={port} | Local={channel.ListeningSIPEndPoint}");
        }

        /// <summary>
        /// The NormalizeMode
        /// </summary>
        /// <param name="mode">The mode<see cref="string?"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string NormalizeMode(string? mode) {
            string normalized = (mode ?? string.Empty).Trim().ToUpperInvariant();
            return normalized switch {
                "UDP" => "UDP",
                "TCP" => "TCP",
                "TLS" => "TLS",
                "UDP+TCP" => "UDP+TCP",
                _ => throw new InvalidOperationException($"SIP Transportmodus fehlt oder ist ungültig: {mode}")
            };
        }

        /// <summary>
        /// The ValidatePort
        /// </summary>
        /// <param name="port">The port<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int ValidatePort(int port) {
            if (port <= 0 || port > IPEndPoint.MaxPort) {
                throw new InvalidOperationException($"SIP Listenport fehlt oder ist ungültig: {port}");
            }

            return port;
        }

        /// <summary>
        /// The NormalizeListenAddresses
        /// </summary>
        /// <param name="listenAddresses">The listenAddresses<see cref="IReadOnlyList{IPAddress}?"/></param>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        /// <returns>The <see cref="IReadOnlyList{IPAddress}"/></returns>
        private static IReadOnlyList<IPAddress> NormalizeListenAddresses(IReadOnlyList<IPAddress>? listenAddresses, TKBase_LogManager? logger) {
            if (listenAddresses == null || listenAddresses.Count == 0) {
                throw new InvalidOperationException("TK_Properties.ServerIP fehlt oder enthält keine gültige SIP-Bind-IP.");
            }

            HashSet<IPAddress> localAddresses = GetLocalUnicastAddresses();
            List<IPAddress> result = new();
            List<string> skipped = new();

            foreach (IPAddress address in listenAddresses) {
                if (address == null) {
                    continue;
                }

                if (!IsBindableAddress(address, localAddresses)) {
                    skipped.Add(address.ToString());
                    logger?.Warn($"SIP_TransportHost Bind-IP übersprungen | IP={address} | Grund=Nicht auf lokalem Host vorhanden");
                    continue;
                }

                if (!result.Contains(address)) {
                    result.Add(address);
                }
            }

            if (result.Count == 0) {
                string configured = string.Join(";", listenAddresses.Select(x => x?.ToString() ?? "<null>"));
                string local = string.Join(";", localAddresses.Select(x => x.ToString()).OrderBy(x => x));
                throw new InvalidOperationException($"TK_Properties.ServerIP enthält keine lokal bindbare SIP-IP. Konfiguriert={configured} | Lokal={local}");
            }

            if (skipped.Count > 0) {
                logger?.Warn($"SIP_TransportHost Bind-IP Filter aktiv | Übersprungen={string.Join(";", skipped)} | Genutzt={string.Join(";", result.Select(x => x.ToString()))}");
            }

            return result;
        }

        private static bool IsBindableAddress(IPAddress address, HashSet<IPAddress> localAddresses) {
            if (IPAddress.Any.Equals(address) || IPAddress.IPv6Any.Equals(address)) {
                return true;
            }

            if (IPAddress.Loopback.Equals(address) || IPAddress.IPv6Loopback.Equals(address)) {
                return true;
            }

            return localAddresses.Contains(address);
        }

        private static HashSet<IPAddress> GetLocalUnicastAddresses() {
            HashSet<IPAddress> result = new();

            try {
                foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces()) {
                    if (networkInterface.OperationalStatus != OperationalStatus.Up) {
                        continue;
                    }

                    IPInterfaceProperties properties = networkInterface.GetIPProperties();
                    foreach (UnicastIPAddressInformation addressInfo in properties.UnicastAddresses) {
                        if (addressInfo?.Address != null) {
                            result.Add(addressInfo.Address);
                        }
                    }
                }
            } catch {
                // Wenn die Adapterabfrage fehlschlägt, soll die eigentliche SIPSorcery-Bind-Fehlermeldung sichtbar bleiben.
            }

            return result;
        }
        /// <summary>
        /// The BuildKey
        /// </summary>
        /// <param name="mode">The mode<see cref="string"/></param>
        /// <param name="listenAddress">The listenAddress<see cref="IPAddress"/></param>
        /// <param name="port">The port<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string BuildKey(string mode, IPAddress listenAddress, int port) => $"{mode}:{listenAddress}:{port}";

        /// <summary>
        /// The DescribeChannels
        /// </summary>
        /// <returns>The <see cref="string"/></returns>
        private static string DescribeChannels() {
            return _channels.Count == 0 ? "-" : string.Join(",", _channels.Keys.OrderBy(x => x));
        }
    }
}
