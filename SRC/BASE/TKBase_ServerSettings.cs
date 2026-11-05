namespace TKServer.BASE {
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.IO;

    /// <summary>
    /// Defines the <see cref="TKBase_ServerSettings" />
    /// </summary>
    internal readonly struct TKBase_ServerSettings {
        /// <summary>
        /// Gets the ServerIP
        /// </summary>
        public string ServerIP { get; }

        /// <summary>
        /// Gets the ServerIPs
        /// </summary>
        public IReadOnlyList<IPAddress> ServerIPs { get; }

        /// <summary>
        /// Gets the SipProxy
        /// </summary>
        public string? SipProxy { get; }

        /// <summary>
        /// Gets the Registrar
        /// </summary>
        public string Registrar { get; }

        /// <summary>
        /// Gets the Realm
        /// </summary>
        public string Realm { get; }

        /// <summary>
        /// Gets the StunServer
        /// </summary>
        public string? StunServer { get; }

        /// <summary>
        /// Gets the OutboundProxy
        /// </summary>
        public string? OutboundProxy { get; }

        /// <summary>
        /// Gets the SipTransport
        /// </summary>
        public string SipTransport { get; }

        /// <summary>
        /// Gets the SipPort
        /// </summary>
        public int SipPort { get; }

        /// <summary>
        /// Gets a value indicating whether UseSRTP
        /// </summary>
        public bool UseSRTP { get; }

        /// <summary>
        /// Gets the RtpPortStart
        /// </summary>
        public int RtpPortStart { get; }

        /// <summary>
        /// Gets the RtpPortEnd
        /// </summary>
        public int RtpPortEnd { get; }

        /// <summary>
        /// Gets the GSMAudioPort
        /// </summary>
        public int? GSMAudioPort { get; }

        /// <summary>
        /// Gets the SipCodecId
        /// </summary>
        public int SipCodecId { get; }

        /// <summary>
        /// Gets the GsmCodecId
        /// </summary>
        public int GsmCodecId { get; }

        /// <summary>
        /// Gets the RegisterExpire
        /// </summary>
        public int RegisterExpire { get; }

        /// <summary>
        /// Gets a value indicating whether user media folders are resolved below TK_SIP_User.AdsProfilePath.
        /// </summary>
        public bool UseAdsProfileMediaPath { get; }

        /// <summary>
        /// Gets the server-side media root path for user media.
        /// </summary>
        public string? UserMediaRootPath { get; }

        /// <summary>
        /// Gets the folder name used below the media root.
        /// </summary>
        public string UserMediaFolderName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref=""/> class.
        /// </summary>
        /// <param name="serverIP">The serverIP<see cref="string"/></param>
        /// <param name="sipProxy">The sipProxy<see cref="string?"/></param>
        /// <param name="registrar">The registrar<see cref="string"/></param>
        /// <param name="realm">The realm<see cref="string"/></param>
        /// <param name="stunServer">The stunServer<see cref="string?"/></param>
        /// <param name="outboundProxy">The outboundProxy<see cref="string?"/></param>
        /// <param name="sipTransport">The sipTransport<see cref="string"/></param>
        /// <param name="sipPort">The sipPort<see cref="int"/></param>
        /// <param name="useSRTP">The useSRTP<see cref="bool"/></param>
        /// <param name="rtpPortStart">The rtpPortStart<see cref="int"/></param>
        /// <param name="rtpPortEnd">The rtpPortEnd<see cref="int"/></param>
        /// <param name="gsmAudioPort">The gsmAudioPort<see cref="int?"/></param>
        /// <param name="sipCodecId">The sipCodecId<see cref="int"/></param>
        /// <param name="gsmCodecId">The gsmCodecId<see cref="int"/></param>
        /// <param name="registerExpire">The registerExpire<see cref="int"/></param>
        /// <param name="useAdsProfileMediaPath">The useAdsProfileMediaPath<see cref="bool"/></param>
        /// <param name="userMediaRootPath">The userMediaRootPath<see cref="string?"/></param>
        /// <param name="userMediaFolderName">The userMediaFolderName<see cref="string"/></param>
        public TKBase_ServerSettings(
            string serverIP,
            string? sipProxy,
            string registrar,
            string realm,
            string? stunServer,
            string? outboundProxy,
            string sipTransport,
            int sipPort,
            bool useSRTP,
            int rtpPortStart,
            int rtpPortEnd,
            int? gsmAudioPort,
            int sipCodecId,
            int gsmCodecId,
            int registerExpire,
            bool useAdsProfileMediaPath,
            string? userMediaRootPath,
            string userMediaFolderName) {

            ServerIP = serverIP?.Trim() ?? string.Empty;
            ServerIPs = ParseServerIPs(ServerIP);
            SipProxy = NormalizeOptionalString(sipProxy);
            Registrar = registrar?.Trim() ?? string.Empty;
            Realm = realm?.Trim() ?? string.Empty;
            StunServer = NormalizeOptionalString(stunServer);
            OutboundProxy = NormalizeOptionalString(outboundProxy);
            SipTransport = (sipTransport ?? string.Empty).Trim().ToUpperInvariant();
            SipPort = sipPort;
            UseSRTP = useSRTP;
            RtpPortStart = rtpPortStart;
            RtpPortEnd = rtpPortEnd;
            GSMAudioPort = gsmAudioPort;
            SipCodecId = sipCodecId;
            GsmCodecId = gsmCodecId;
            RegisterExpire = registerExpire;
            UseAdsProfileMediaPath = useAdsProfileMediaPath;
            UserMediaRootPath = NormalizeOptionalString(userMediaRootPath);
            UserMediaFolderName = NormalizeMediaFolderName(userMediaFolderName);

            Validate();
        }

        /// <summary>
        /// The FromDataRow
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <returns>The <see cref="TKBase_ServerSettings"/></returns>
        public static TKBase_ServerSettings FromDataRow(DataRow row) {
            if (row == null) throw new ArgumentNullException(nameof(row));

            return new TKBase_ServerSettings(
                RequireString(row, nameof(ServerIP)),
                OptionalString(row, nameof(SipProxy)),
                RequireString(row, nameof(Registrar)),
                RequireString(row, nameof(Realm)),
                OptionalString(row, nameof(StunServer)),
                OptionalString(row, nameof(OutboundProxy)),
                RequireString(row, nameof(SipTransport)),
                RequireInt(row, nameof(SipPort)),
                RequireBool(row, nameof(UseSRTP)),
                RequireInt(row, nameof(RtpPortStart)),
                RequireInt(row, nameof(RtpPortEnd)),
                OptionalInt(row, nameof(GSMAudioPort)),
                RequireInt(row, "SIPCodec"),
                RequireInt(row, "GSMCodec"),
                RequireInt(row, nameof(RegisterExpire)),
                RequireBool(row, nameof(UseAdsProfileMediaPath)),
                OptionalString(row, nameof(UserMediaRootPath)),
                OptionalString(row, nameof(UserMediaFolderName)) ?? "User");
        }

        /// <summary>
        /// The Validate
        /// </summary>
        private void Validate() {
            if (ServerIPs.Count == 0) {
                throw new InvalidOperationException("TK_Properties.ServerIP fehlt oder enthält keine gültige IP-Adresse.");
            }

            if (string.IsNullOrWhiteSpace(Registrar)) {
                throw new InvalidOperationException("TK_Properties.Registrar fehlt oder ist leer.");
            }

            if (string.IsNullOrWhiteSpace(Realm)) {
                throw new InvalidOperationException("TK_Properties.Realm fehlt oder ist leer.");
            }

            if (SipPort <= 0 || SipPort > IPEndPoint.MaxPort) {
                throw new InvalidOperationException($"TK_Properties.SipPort ungültig: {SipPort}");
            }

            if (SipTransport != "UDP" && SipTransport != "TCP" && SipTransport != "TLS" && SipTransport != "UDP+TCP") {
                throw new InvalidOperationException($"TK_Properties.SipTransport ungültig: {SipTransport}");
            }

            if (RtpPortStart <= 0 || RtpPortStart > IPEndPoint.MaxPort) {
                throw new InvalidOperationException($"TK_Properties.RtpPortStart ungültig: {RtpPortStart}");
            }

            if (RtpPortEnd <= 0 || RtpPortEnd > IPEndPoint.MaxPort) {
                throw new InvalidOperationException($"TK_Properties.RtpPortEnd ungültig: {RtpPortEnd}");
            }

            if (RtpPortStart % 2 != 0) {
                throw new InvalidOperationException($"TK_Properties.RtpPortStart muss gerade sein: {RtpPortStart}");
            }

            if (RtpPortEnd - RtpPortStart < 2) {
                throw new InvalidOperationException($"TK_Properties RTP-Bereich ist zu klein: {RtpPortStart}-{RtpPortEnd}");
            }

            if (GSMAudioPort.HasValue && (GSMAudioPort.Value <= 0 || GSMAudioPort.Value > IPEndPoint.MaxPort)) {
                throw new InvalidOperationException($"TK_Properties.GSMAudioPort ungültig: {GSMAudioPort.Value}");
            }

            if (SipCodecId <= 0) {
                throw new InvalidOperationException($"TK_Properties.SIPCodec muss eine gültige TK_AudioCodec.ID enthalten: {SipCodecId}");
            }

            if (GsmCodecId <= 0) {
                throw new InvalidOperationException($"TK_Properties.GSMCodec muss eine gültige TK_AudioCodec.ID enthalten: {GsmCodecId}");
            }

            if (RegisterExpire <= 0) {
                throw new InvalidOperationException($"TK_Properties.RegisterExpire ungültig: {RegisterExpire}");
            }

            if (string.IsNullOrWhiteSpace(UserMediaFolderName)) {
                throw new InvalidOperationException("TK_Properties.UserMediaFolderName fehlt oder ist leer.");
            }

            if (!IsValidMediaFolderName(UserMediaFolderName)) {
                throw new InvalidOperationException($"TK_Properties.UserMediaFolderName enthält ungültige Zeichen: {UserMediaFolderName}");
            }
        }

        /// <summary>
        /// The ResolveMediaRootPath
        /// </summary>
        /// <param name="baseDirectory">The baseDirectory<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        public string ResolveMediaRootPath(string baseDirectory) {
            string configuredRoot = UserMediaRootPath?.Trim() ?? string.Empty;
            string root = string.IsNullOrWhiteSpace(configuredRoot)
                ? Path.Combine(baseDirectory, "Media")
                : configuredRoot;

            return Path.GetFullPath(root);
        }

        /// <summary>
        /// The ResolveServerUserMediaRootPath
        /// </summary>
        /// <param name="baseDirectory">The baseDirectory<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        public string ResolveServerUserMediaRootPath(string baseDirectory) {
            return Path.GetFullPath(Path.Combine(ResolveMediaRootPath(baseDirectory), UserMediaFolderName));
        }

        /// <summary>
        /// The ResolveSystemMediaFolderPath
        /// </summary>
        /// <param name="baseDirectory">The baseDirectory<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        public string ResolveSystemMediaFolderPath(string baseDirectory) {
            return Path.GetFullPath(Path.Combine(ResolveMediaRootPath(baseDirectory), "System"));
        }

        /// <summary>
        /// The ResolveUserMediaFolderPath
        /// </summary>
        /// <param name="userRow">The userRow<see cref="DataRow"/></param>
        /// <param name="baseDirectory">The baseDirectory<see cref="string"/></param>
        /// <returns>The <see cref="string?"/></returns>
        public string? ResolveUserMediaFolderPath(DataRow userRow, string baseDirectory) {
            if (userRow == null) return null;

            string username = ReadRowString(userRow, "Username");
            if (string.IsNullOrWhiteSpace(username) || !IsValidMediaFolderName(username)) {
                return null;
            }

            if (UseAdsProfileMediaPath) {
                string adsProfilePath = ReadRowString(userRow, "AdsProfilePath");
                if (!string.IsNullOrWhiteSpace(adsProfilePath)) {
                    return Path.GetFullPath(Path.Combine(adsProfilePath, UserMediaFolderName));
                }
            }

            return Path.GetFullPath(Path.Combine(ResolveServerUserMediaRootPath(baseDirectory), username));
        }

        /// <summary>
        /// The ParseServerIPs
        /// </summary>
        /// <param name="value">The value<see cref="string"/></param>
        /// <returns>The <see cref="IReadOnlyList{IPAddress}"/></returns>
        private static IReadOnlyList<IPAddress> ParseServerIPs(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                throw new InvalidOperationException("TK_Properties.ServerIP fehlt oder ist leer.");
            }

            List<IPAddress> addresses = new();
            foreach (string part in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
                if (!IPAddress.TryParse(part, out IPAddress? address)) {
                    throw new InvalidOperationException($"TK_Properties.ServerIP enthält eine ungültige IP-Adresse: {part}");
                }

                if (!addresses.Contains(address)) {
                    addresses.Add(address);
                }
            }

            if (addresses.Count == 0) {
                throw new InvalidOperationException("TK_Properties.ServerIP fehlt oder enthält keine gültige IP-Adresse.");
            }

            bool containsAny = addresses.Any(address => IPAddress.Any.Equals(address) || IPAddress.IPv6Any.Equals(address));
            if (containsAny && addresses.Count > 1) {
                throw new InvalidOperationException("TK_Properties.ServerIP darf 0.0.0.0/:: nicht mit konkreten IP-Adressen kombinieren.");
            }

            return addresses;
        }

        /// <summary>
        /// The TryResolveIngressMediaAdvertisedIp
        /// </summary>
        /// <param name="receivedLocalAddress">The receivedLocalAddress<see cref="IPAddress?"/></param>
        /// <param name="remoteAddress">The remoteAddress<see cref="IPAddress?"/></param>
        /// <param name="providerPublicIp">The providerPublicIp<see cref="string?"/></param>
        /// <param name="mediaAdvertisedIp">The mediaAdvertisedIp<see cref="string"/></param>
        /// <param name="reason">The reason<see cref="string"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public bool TryResolveIngressMediaAdvertisedIp(IPAddress? receivedLocalAddress, IPAddress? remoteAddress, string? providerPublicIp, out string mediaAdvertisedIp, out string reason) {
            mediaAdvertisedIp = string.Empty;
            reason = string.Empty;

            if (TryResolveConfiguredProviderAddress(providerPublicIp, out IPAddress? providerAddress)) {
                mediaAdvertisedIp = providerAddress!.ToString();
                return true;
            }

            IPAddress? local = NormalizeAddress(receivedLocalAddress);
            if (IsConcreteAddress(local)) {
                mediaAdvertisedIp = local!.ToString();
                return true;
            }

            IPAddress? remote = NormalizeAddress(remoteAddress);
            IPAddress resolved = ResolveServerAddressForRemote(remote);
            if (IsConcreteAddress(resolved)) {
                mediaAdvertisedIp = resolved.ToString();
                return true;
            }

            reason = "MediaAdvertisedIp konnte nicht bestimmt werden: keine konkrete konfigurierte Server-IP vorhanden.";
            return false;
        }

        /// <summary>
        /// The TryResolveEndpointMediaAdvertisedIp
        /// </summary>
        /// <param name="localAddress">The localAddress<see cref="IPAddress?"/></param>
        /// <param name="remoteAddress">The remoteAddress<see cref="IPAddress?"/></param>
        /// <param name="providerPublicIp">The providerPublicIp<see cref="string?"/></param>
        /// <param name="requireProviderPublicIp">The requireProviderPublicIp<see cref="bool"/></param>
        /// <param name="mediaAdvertisedIp">The mediaAdvertisedIp<see cref="string"/></param>
        /// <param name="reason">The reason<see cref="string"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public bool TryResolveEndpointMediaAdvertisedIp(IPAddress? localAddress, IPAddress? remoteAddress, string? providerPublicIp, bool requireProviderPublicIp, out string mediaAdvertisedIp, out string reason) {
            mediaAdvertisedIp = string.Empty;
            reason = string.Empty;

            if (requireProviderPublicIp && TryResolveConfiguredProviderAddress(providerPublicIp, out IPAddress? providerAddress)) {
                mediaAdvertisedIp = providerAddress!.ToString();
                return true;
            }

            IPAddress? local = NormalizeAddress(localAddress);
            if (IsConfiguredServerAddress(local)) {
                mediaAdvertisedIp = local!.ToString();
                return true;
            }

            IPAddress? remote = NormalizeAddress(remoteAddress);
            IPAddress resolved = ResolveServerAddressForRemote(remote);
            if (IsConcreteAddress(resolved)) {
                mediaAdvertisedIp = resolved.ToString();
                return true;
            }

            reason = requireProviderPublicIp
                ? "MediaAdvertisedIp konnte nicht bestimmt werden: keine Provider-Public-IP und keine passende konfigurierte Server-IP vorhanden."
                : "MediaAdvertisedIp konnte nicht bestimmt werden: keine passende konfigurierte Server-IP vorhanden.";
            return false;
        }

        /// <summary>
        /// The ResolveServerAddress
        /// </summary>
        /// <param name="localAddress">The localAddress<see cref="IPAddress?"/></param>
        /// <param name="remoteAddress">The remoteAddress<see cref="IPAddress?"/></param>
        /// <returns>The <see cref="IPAddress"/></returns>
        public IPAddress ResolveServerAddress(IPAddress? localAddress, IPAddress? remoteAddress) {
            IPAddress? normalizedLocal = NormalizeAddress(localAddress);
            if (IsConfiguredServerAddress(normalizedLocal)) {
                return normalizedLocal!;
            }

            IPAddress? normalizedRemote = NormalizeAddress(remoteAddress);
            if (normalizedRemote != null) {
                return ResolveServerAddressForRemote(normalizedRemote);
            }

            return ServerIPs[0];
        }

        /// <summary>
        /// The ResolveServerAddressForRemote
        /// </summary>
        /// <param name="remoteAddress">The remoteAddress<see cref="IPAddress?"/></param>
        /// <returns>The <see cref="IPAddress"/></returns>
        public IPAddress ResolveServerAddressForRemote(IPAddress? remoteAddress) {
            IPAddress? normalizedRemote = NormalizeAddress(remoteAddress);
            if (normalizedRemote != null) {
                if (TryResolveConfiguredServerAddressFromInterfacePrefix(normalizedRemote, out IPAddress? routedAddress)) {
                    return routedAddress!;
                }

                IPAddress? sameFamily = ServerIPs.FirstOrDefault(serverIp => IsConcreteAddress(serverIp) && serverIp.AddressFamily == normalizedRemote.AddressFamily);
                if (sameFamily != null) {
                    return sameFamily;
                }
            }

            return ServerIPs.FirstOrDefault(IsConcreteAddress) ?? ServerIPs[0];
        }

        /// <summary>
        /// The ResolveWanServerAddress
        /// </summary>
        /// <returns>The <see cref="IPAddress"/></returns>
        public IPAddress ResolveWanServerAddress() {
            return ServerIPs.FirstOrDefault(IsConcreteAddress) ?? ServerIPs[0];
        }

        /// <summary>
        /// The ResolveServerAddressText
        /// </summary>
        /// <param name="localAddress">The localAddress<see cref="IPAddress?"/></param>
        /// <param name="remoteAddress">The remoteAddress<see cref="IPAddress?"/></param>
        /// <returns>The <see cref="string"/></returns>
        public string ResolveServerAddressText(IPAddress? localAddress, IPAddress? remoteAddress) {
            return ResolveServerAddress(localAddress, remoteAddress).ToString();
        }

        /// <summary>
        /// Prüft eine konfigurierte Provider-Adresse ohne feste Netzklassenlogik.
        /// </summary>
        /// <param name="providerPublicIp">The providerPublicIp<see cref="string?"/></param>
        /// <param name="address">The address<see cref="IPAddress?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool TryResolveConfiguredProviderAddress(string? providerPublicIp, out IPAddress? address) {
            address = null;
            if (string.IsNullOrWhiteSpace(providerPublicIp)) return false;
            if (!IPAddress.TryParse(providerPublicIp.Trim(), out IPAddress? parsed)) return false;
            parsed = NormalizeAddress(parsed);
            if (!IsConcreteAddress(parsed)) return false;
            address = parsed;
            return true;
        }

        /// <summary>
        /// Wählt eine konfigurierte Server-IP anhand der echten Interface-Präfixe des Betriebssystems.
        /// </summary>
        /// <param name="remoteAddress">The remoteAddress<see cref="IPAddress"/></param>
        /// <param name="address">The address<see cref="IPAddress?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private bool TryResolveConfiguredServerAddressFromInterfacePrefix(IPAddress remoteAddress, out IPAddress? address) {
            address = null;
            try {
                foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces()) {
                    if (networkInterface.OperationalStatus != OperationalStatus.Up) continue;

                    foreach (UnicastIPAddressInformation unicast in networkInterface.GetIPProperties().UnicastAddresses) {
                        IPAddress local = NormalizeAddress(unicast.Address) ?? unicast.Address;
                        if (!IsConfiguredServerAddress(local)) continue;
                        if (local.AddressFamily != remoteAddress.AddressFamily) continue;
                        if (MatchesPrefix(local, remoteAddress, unicast.PrefixLength)) {
                            address = local;
                            return true;
                        }
                    }
                }
            } catch {
                address = null;
            }

            return false;
        }

        /// <summary>
        /// Vergleicht zwei IP-Adressen anhand des vom Betriebssystem gemeldeten Präfixes.
        /// </summary>
        /// <param name="localAddress">The localAddress<see cref="IPAddress"/></param>
        /// <param name="remoteAddress">The remoteAddress<see cref="IPAddress"/></param>
        /// <param name="prefixLength">The prefixLength<see cref="int"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool MatchesPrefix(IPAddress localAddress, IPAddress remoteAddress, int prefixLength) {
            byte[] localBytes = localAddress.GetAddressBytes();
            byte[] remoteBytes = remoteAddress.GetAddressBytes();
            if (localBytes.Length != remoteBytes.Length) return false;
            if (prefixLength <= 0 || prefixLength > localBytes.Length * 8) return false;

            int fullBytes = prefixLength / 8;
            int remainingBits = prefixLength % 8;
            for (int i = 0; i < fullBytes; i++) {
                if (localBytes[i] != remoteBytes[i]) return false;
            }

            if (remainingBits == 0) return true;
            int mask = 0xFF << (8 - remainingBits) & 0xFF;
            return (localBytes[fullBytes] & mask) == (remoteBytes[fullBytes] & mask);
        }

        /// <summary>
        /// The IsConfiguredServerAddress
        /// </summary>
        /// <param name="address">The address<see cref="IPAddress?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private bool IsConfiguredServerAddress(IPAddress? address) {
            if (address == null || IPAddress.Any.Equals(address) || IPAddress.IPv6Any.Equals(address)) {
                return false;
            }

            return ServerIPs.Contains(address);
        }

        /// <summary>
        /// The NormalizeAddress
        /// </summary>
        /// <param name="address">The address<see cref="IPAddress?"/></param>
        /// <returns>The <see cref="IPAddress?"/></returns>
        private static IPAddress? NormalizeAddress(IPAddress? address) {
            if (address == null) {
                return null;
            }

            return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        }

        /// <summary>
        /// The IsConcreteAddress
        /// </summary>
        /// <param name="address">The address<see cref="IPAddress?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool IsConcreteAddress(IPAddress? address) {
            return address != null
                && !IPAddress.Any.Equals(address)
                && !IPAddress.IPv6Any.Equals(address)
                && !IPAddress.IsLoopback(address);
        }

        /// <summary>
        /// The NormalizeMediaFolderName
        /// </summary>
        /// <param name="value">The value<see cref="string?"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string NormalizeMediaFolderName(string? value) {
            string folderName = value?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(folderName) ? "User" : folderName;
        }

        /// <summary>
        /// The IsValidMediaFolderName
        /// </summary>
        /// <param name="folderName">The folderName<see cref="string"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool IsValidMediaFolderName(string folderName) {
            if (string.IsNullOrWhiteSpace(folderName)) {
                return false;
            }

            if (folderName.Contains(Path.DirectorySeparatorChar) || folderName.Contains(Path.AltDirectorySeparatorChar)) {
                return false;
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in folderName) {
                if (Array.IndexOf(invalidChars, c) >= 0) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The ReadRowString
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="column">The column<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string ReadRowString(DataRow row, string column) {
            if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value) {
                return string.Empty;
            }

            return Convert.ToString(row[column])?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// The NormalizeOptionalString
        /// </summary>
        /// <param name="value">The value<see cref="string?"/></param>
        /// <returns>The <see cref="string?"/></returns>
        private static string? NormalizeOptionalString(string? value) {
            string trimmed = value?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }

        /// <summary>
        /// The RequireString
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="column">The column<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string RequireString(DataRow row, string column) {
            if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value) {
                throw new InvalidOperationException($"TK_Properties.{column} fehlt.");
            }

            string value = Convert.ToString(row[column])?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(value)) {
                throw new InvalidOperationException($"TK_Properties.{column} ist leer.");
            }

            return value;
        }

        /// <summary>
        /// The OptionalString
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="column">The column<see cref="string"/></param>
        /// <returns>The <see cref="string?"/></returns>
        private static string? OptionalString(DataRow row, string column) {
            if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value) return null;
            return NormalizeOptionalString(Convert.ToString(row[column]));
        }

        /// <summary>
        /// The RequireInt
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="column">The column<see cref="string"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int RequireInt(DataRow row, string column) {
            if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value) {
                throw new InvalidOperationException($"TK_Properties.{column} fehlt.");
            }

            string value = Convert.ToString(row[column])?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(value)) {
                throw new InvalidOperationException($"TK_Properties.{column} ist leer.");
            }

            return Convert.ToInt32(row[column]);
        }

        /// <summary>
        /// The OptionalInt
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="column">The column<see cref="string"/></param>
        /// <returns>The <see cref="int?"/></returns>
        private static int? OptionalInt(DataRow row, string column) {
            if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value) return null;
            return Convert.ToInt32(row[column]);
        }

        /// <summary>
        /// The RequireBool
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="column">The column<see cref="string"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool RequireBool(DataRow row, string column) {
            if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value) {
                throw new InvalidOperationException($"TK_Properties.{column} fehlt.");
            }

            return Convert.ToBoolean(row[column]);
        }
    }
}
