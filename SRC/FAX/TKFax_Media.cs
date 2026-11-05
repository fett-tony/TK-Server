/*
 * TKFax_Media
 * Aufgabe:
 * - Enthaelt ausschliesslich Fax-Medienlogik fuer T.38/UDPTL und Audio-Fax.
 * - Fuehrt im Normalfall eine A/B-Medienbruecke: Fax.LegA <-> TK-Server <-> Fax.LegB.
 * - Der Server bleibt Vermittler und koppelt ausschliesslich Fax.LegA und Fax.LegB.
 * - Bleibt frei von SIP-, GSM- und Provider-Transporthoheit.
 *
 * Nicht Aufgabe:
 * - Keine Session erzeugen.
 * - Kein Routing entscheiden.
 * - Keine fremden Bereichsressourcen oeffnen oder schliessen.
 *
 * Diese information darf nicht geloescht werden!
 */

namespace TKServer.FAX {
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    using TKServer.BASE;

    internal sealed class TKFax_Media {
        private readonly TKBase_LogManager? _logger;
        private readonly TKBase_MediaManager? _mediaManager;
        private readonly ConcurrentDictionary<string, TKFaxMediaBridgeRuntime> _bridges = new(StringComparer.OrdinalIgnoreCase);

        public TKFax_Media(TKBase_MediaManager? mediaManager = null, TKBase_LogManager? logger = null) {
            _logger = logger;
            _mediaManager = mediaManager;
        }

        public void SelectT38Mode(TKFax_Session faxSession) {
            if (faxSession == null) return;
            faxSession.FaxMode = TKFaxMediaMode.T38;
            faxSession.LegA.T38Active = true;
            faxSession.LegB.T38Active = true;
            faxSession.UpdatedAt = DateTime.Now;
            _logger?.Debug($"FAX Media T.38 Bridge-Modus gesetzt | SessionID={faxSession.SessionId}");
        }

        public void SelectAudioFaxMode(TKFax_Session faxSession) {
            if (faxSession == null) return;
            faxSession.FaxMode = TKFaxMediaMode.AudioPassthrough;
            faxSession.LegA.AudioFaxActive = true;
            faxSession.LegB.AudioFaxActive = true;
            faxSession.UpdatedAt = DateTime.Now;
            _logger?.Info($"FAX Media Audio/G.711 Bridge-Modus gesetzt | SessionID={faxSession.SessionId}");
        }

        public string BuildInitialAudioOfferToLegB(TKFax_Session faxSession, string localAddress, string? legASdp) {
            return BuildInitialAudioOfferToLegB(faxSession, localAddress, localAddress, legASdp);
        }

        public string BuildInitialAudioOfferToLegB(TKFax_Session faxSession, string legALocalAddress, string legBLocalAddress, string? legASdp) {
            if (faxSession == null) throw new ArgumentNullException(nameof(faxSession));
            TKFaxMediaBridgeRuntime bridge = EnsureBridge(faxSession, legALocalAddress, legBLocalAddress, legASdp, null);
            ApplyBridgeToFaxSession(faxSession, bridge, legA: true, legB: true);
            SelectAudioFaxMode(faxSession);
            TrySetLegAAudioRemote(bridge, legASdp);
            _logger?.Info($"FAX Bridge SDP fuer LegB aufgebaut | SessionID={faxSession.SessionId} | LegALocal={bridge.AudioA.LocalAddress}:{bridge.AudioA.LocalPort} | LegBLocal={bridge.AudioB.LocalAddress}:{bridge.AudioB.LocalPort} | Codec={bridge.Codec.CodecName}/{bridge.Codec.PayloadType} | RTCP=aus");
            return bridge.BuildAudioRelaySdp(bridge.AudioB.LocalAddress, bridge.AudioB.LocalPort);
        }

        public string BuildAudioAnswerToLegA(TKFax_Session faxSession, string localAddress, string? legBAnswerSdp) {
            return BuildAudioAnswerToLegA(faxSession, localAddress, localAddress, legBAnswerSdp);
        }

        public string BuildAudioAnswerToLegA(TKFax_Session faxSession, string legALocalAddress, string legBLocalAddress, string? legBAnswerSdp) {
            if (faxSession == null) throw new ArgumentNullException(nameof(faxSession));
            TKFaxMediaBridgeRuntime bridge = EnsureBridge(faxSession, legALocalAddress, legBLocalAddress, null, legBAnswerSdp);
            TrySetLegBAudioRemote(bridge, legBAnswerSdp);
            ApplyBridgeToFaxSession(faxSession, bridge, legA: true, legB: true);
            StartAudioBridgeIfReady(faxSession);
            _logger?.Info($"FAX Bridge SDP fuer LegA aufgebaut | SessionID={faxSession.SessionId} | LegALocal={bridge.AudioA.LocalAddress}:{bridge.AudioA.LocalPort} | LegBLocal={bridge.AudioB.LocalAddress}:{bridge.AudioB.LocalPort} | Codec={bridge.Codec.CodecName}/{bridge.Codec.PayloadType} | RTCP=aus");
            return bridge.BuildAudioRelaySdp(bridge.AudioA.LocalAddress, bridge.AudioA.LocalPort);
        }

        public string BuildAudioAnswerToLegB(TKFax_Session faxSession, string localAddress, string? legAAnswerSdp) {
            return BuildAudioAnswerToLegB(faxSession, localAddress, localAddress, legAAnswerSdp);
        }

        public string BuildAudioAnswerToLegB(TKFax_Session faxSession, string legALocalAddress, string legBLocalAddress, string? legAAnswerSdp) {
            if (faxSession == null) throw new ArgumentNullException(nameof(faxSession));
            TKFaxMediaBridgeRuntime bridge = EnsureBridge(faxSession, legALocalAddress, legBLocalAddress, legAAnswerSdp, null);
            TrySetLegAAudioRemote(bridge, legAAnswerSdp);
            ApplyBridgeToFaxSession(faxSession, bridge, legA: true, legB: true);
            StartAudioBridgeIfReady(faxSession);
            _logger?.Info($"FAX Bridge SDP fuer LegB aufgebaut | SessionID={faxSession.SessionId} | LegALocal={bridge.AudioA.LocalAddress}:{bridge.AudioA.LocalPort} | LegBLocal={bridge.AudioB.LocalAddress}:{bridge.AudioB.LocalPort} | Codec={bridge.Codec.CodecName}/{bridge.Codec.PayloadType} | RTCP=aus");
            return bridge.BuildAudioRelaySdp(bridge.AudioB.LocalAddress, bridge.AudioB.LocalPort);
        }

        public string BuildT38OfferToPeer(TKFax_Session faxSession, string localAddress, bool toLegB, string? peerSdp) {
            if (faxSession == null) throw new ArgumentNullException(nameof(faxSession));
            TKFaxMediaBridgeRuntime bridge = EnsureBridge(faxSession, localAddress, null, null);

            TKFaxUdpRuntime destinationSide;
            if (toLegB) {
                EnsureT38A(bridge, faxSession, bridge.AudioA.LocalAddress);
                destinationSide = EnsureT38B(bridge, faxSession, localAddress);
                TrySetLegAT38Remote(bridge, peerSdp);
            }
            else {
                EnsureT38B(bridge, faxSession, bridge.AudioB.LocalAddress);
                destinationSide = EnsureT38A(bridge, faxSession, localAddress);
                TrySetLegBT38Remote(bridge, peerSdp);
            }

            ApplyBridgeToFaxSession(faxSession, bridge, legA: true, legB: true);
            SelectT38Mode(faxSession);
            return RewriteMediaSdpForRelay(peerSdp, destinationSide.LocalAddress, destinationSide.LocalPort, "image", bridge.Codec);
        }

        public string BuildT38AnswerToLegA(TKFax_Session faxSession, string localAddress, string? legBAnswerSdp) {
            if (faxSession == null) throw new ArgumentNullException(nameof(faxSession));
            TKFaxMediaBridgeRuntime bridge = EnsureBridge(faxSession, localAddress, null, null);
            TKFaxUdpRuntime sideA = EnsureT38A(bridge, faxSession, localAddress);
            EnsureT38B(bridge, faxSession, bridge.AudioB.LocalAddress);
            TrySetLegBT38Remote(bridge, legBAnswerSdp);
            ApplyBridgeToFaxSession(faxSession, bridge, legA: true, legB: true);
            StartT38BridgeIfReady(faxSession);
            SelectT38Mode(faxSession);
            return RewriteMediaSdpForRelay(legBAnswerSdp, sideA.LocalAddress, sideA.LocalPort, "image", bridge.Codec);
        }

        public string BuildT38AnswerToLegB(TKFax_Session faxSession, string localAddress, string? legAAnswerSdp) {
            if (faxSession == null) throw new ArgumentNullException(nameof(faxSession));
            TKFaxMediaBridgeRuntime bridge = EnsureBridge(faxSession, localAddress, null, null);
            TKFaxUdpRuntime sideB = EnsureT38B(bridge, faxSession, localAddress);
            EnsureT38A(bridge, faxSession, bridge.AudioA.LocalAddress);
            TrySetLegAT38Remote(bridge, legAAnswerSdp);
            ApplyBridgeToFaxSession(faxSession, bridge, legA: true, legB: true);
            StartT38BridgeIfReady(faxSession);
            SelectT38Mode(faxSession);
            return RewriteMediaSdpForRelay(legAAnswerSdp, sideB.LocalAddress, sideB.LocalPort, "image", bridge.Codec);
        }

        public bool ActivateAudioFaxFromSdp(TKFax_Session faxSession, string? sdp, string localAddress, int localPort) {
            if (faxSession == null) return false;
            TKFaxMediaBridgeRuntime bridge = EnsureBridge(faxSession, localAddress, sdp, null);
            bool ok = TrySetLegAAudioRemote(bridge, sdp) || TrySetLegBAudioRemote(bridge, sdp);
            ApplyBridgeToFaxSession(faxSession, bridge, legA: true, legB: true);
            StartAudioBridgeIfReady(faxSession);
            SelectAudioFaxMode(faxSession);
            return ok;
        }

        public int EnsureT38UdptlRuntime(TKFax_Session faxSession, string localAddress) {
            TKFaxMediaBridgeRuntime bridge = EnsureBridge(faxSession, localAddress, null, null);
            TKFaxUdpRuntime sideA = EnsureT38A(bridge, faxSession, localAddress);
            faxSession.LocalT38Address = sideA.LocalAddress;
            faxSession.LocalT38Port = sideA.LocalPort;
            return sideA.LocalPort;
        }

        public string BuildLocalT38Sdp(TKFax_Session faxSession, string localAddress) {
            int port = EnsureT38UdptlRuntime(faxSession, localAddress);
            return RewriteMediaSdpForRelay(null, localAddress, port, "image", TKFaxAudioCodecSelection.Pcma);
        }

        public int EnsureAudioRtpRuntime(TKFax_Session faxSession, string localAddress, string? sdp) {
            TKFaxMediaBridgeRuntime bridge = EnsureBridge(faxSession, localAddress, sdp, null);
            faxSession.LocalAudioAddress = bridge.AudioA.LocalAddress;
            faxSession.LocalAudioPort = bridge.AudioA.LocalPort;
            faxSession.AudioPayloadType = bridge.Codec.PayloadType;
            faxSession.AudioCodec = bridge.Codec.CodecName;
            return bridge.AudioA.LocalPort;
        }

        public string BuildLocalAudioFaxSdp(TKFax_Session faxSession, string localAddress, string? remoteSdp) {
            TKFaxMediaBridgeRuntime bridge = EnsureBridge(faxSession, localAddress, remoteSdp, null);
            return bridge.BuildAudioRelaySdp(bridge.AudioA.LocalAddress, bridge.AudioA.LocalPort);
        }

        public async ValueTask HandleIncomingAudioRtpPacketAsync(TKFax_Session faxSession, string direction, byte[] packet, CancellationToken cancellationToken = default) {
            if (faxSession == null || packet == null || packet.Length < 12) return;
            if (_bridges.TryGetValue(faxSession.SessionId, out TKFaxMediaBridgeRuntime? bridge)) {
                if (string.Equals(direction, "A", StringComparison.OrdinalIgnoreCase)) await bridge.ForwardAudioAToBAsync(packet, null, cancellationToken).ConfigureAwait(false);
                else if (string.Equals(direction, "B", StringComparison.OrdinalIgnoreCase)) await bridge.ForwardAudioBToAAsync(packet, null, cancellationToken).ConfigureAwait(false);
                return;
            }

            _logger?.Warn($"FAX Audio-Paket ohne aktive Leg-A/Leg-B-Bridge verworfen | SessionID={faxSession.SessionId} | Richtung={direction}");
        }

        public void Stop(TKFax_Session faxSession, string? reason = null) {
            if (faxSession == null) return;

            if (_bridges.TryRemove(faxSession.SessionId, out TKFaxMediaBridgeRuntime? bridge)) {
                bridge.Stop();
                ReleasePort(bridge.AudioA.LocalPort);
                ReleasePort(bridge.AudioB.LocalPort);
                if (bridge.T38A != null) ReleasePort(bridge.T38A.LocalPort);
                if (bridge.T38B != null) ReleasePort(bridge.T38B.LocalPort);
            }


            faxSession.LegA.IsEnded = true;
            faxSession.LegB.IsEnded = true;
            faxSession.EndedAt = DateTime.Now;
            faxSession.UpdatedAt = DateTime.Now;
            _logger?.Debug($"FAX Media beendet | SessionID={faxSession.SessionId} | Grund={reason ?? string.Empty}");
        }

        private TKFaxMediaBridgeRuntime EnsureBridge(TKFax_Session faxSession, string localAddress, string? legASdp, string? legBSdp) {
            return EnsureBridge(faxSession, localAddress, localAddress, legASdp, legBSdp);
        }

        private TKFaxMediaBridgeRuntime EnsureBridge(TKFax_Session faxSession, string legALocalAddress, string legBLocalAddress, string? legASdp, string? legBSdp) {
            return _bridges.GetOrAdd(faxSession.SessionId, _ => {
                TKFaxAudioCodecSelection codec = TKFaxAudioCodecSelection.Pcma;
                TKFaxUdpRuntime? audioA = null;
                TKFaxUdpRuntime? audioB = null;

                try {
                    audioA = CreateUdpRuntime(faxSession.SessionId, "Audio-RTP LegA", legALocalAddress, codec);
                    audioB = CreateUdpRuntime(faxSession.SessionId, "Audio-RTP LegB", legBLocalAddress, codec);
                    return new TKFaxMediaBridgeRuntime(faxSession.SessionId, audioA, audioB, codec, _logger);
                } catch {
                    if (audioB != null) {
                        audioB.Stop();
                        ReleasePort(audioB.LocalPort);
                    }
                    if (audioA != null) {
                        audioA.Stop();
                        ReleasePort(audioA.LocalPort);
                    }
                    throw;
                }
            });
        }

        private void StartAudioBridgeIfReady(TKFax_Session faxSession) {
            if (!_bridges.TryGetValue(faxSession.SessionId, out TKFaxMediaBridgeRuntime? bridge)) return;
            bridge.StartAudioBridge();
        }

        private void StartT38BridgeIfReady(TKFax_Session faxSession) {
            if (!_bridges.TryGetValue(faxSession.SessionId, out TKFaxMediaBridgeRuntime? bridge)) return;
            bridge.StartT38Bridge();
        }

        private bool TrySetLegAAudioRemote(TKFaxMediaBridgeRuntime bridge, string? sdp) {
            if (!TryParseAudioRemote(sdp, out string? remoteAddress, out int remotePort) || string.IsNullOrWhiteSpace(remoteAddress)) return false;
            return bridge.AudioA.SetRemote(remoteAddress, remotePort);
        }

        private bool TrySetLegBAudioRemote(TKFaxMediaBridgeRuntime bridge, string? sdp) {
            if (!TryParseAudioRemote(sdp, out string? remoteAddress, out int remotePort) || string.IsNullOrWhiteSpace(remoteAddress)) return false;
            return bridge.AudioB.SetRemote(remoteAddress, remotePort);
        }

        private bool TrySetLegBT38Remote(TKFaxMediaBridgeRuntime bridge, string? sdp) {
            if (!TryParseT38Remote(sdp, out string? remoteAddress, out int remotePort) || string.IsNullOrWhiteSpace(remoteAddress)) return false;
            return EnsureT38B(bridge, null, bridge.AudioB.LocalAddress).SetRemote(remoteAddress, remotePort);
        }

        private bool TrySetLegAT38Remote(TKFaxMediaBridgeRuntime bridge, string? sdp) {
            if (!TryParseT38Remote(sdp, out string? remoteAddress, out int remotePort) || string.IsNullOrWhiteSpace(remoteAddress)) return false;
            return EnsureT38A(bridge, null, bridge.AudioA.LocalAddress).SetRemote(remoteAddress, remotePort);
        }

        private TKFaxUdpRuntime EnsureT38A(TKFaxMediaBridgeRuntime bridge, TKFax_Session? faxSession, string localAddress) {
            if (bridge.T38A != null) return bridge.T38A;
            bridge.T38A = CreateUdpRuntime(faxSession?.SessionId ?? bridge.SessionId, "UDPTL LegA", localAddress, null);
            return bridge.T38A;
        }

        private TKFaxUdpRuntime EnsureT38B(TKFaxMediaBridgeRuntime bridge, TKFax_Session? faxSession, string localAddress) {
            if (bridge.T38B != null) return bridge.T38B;
            bridge.T38B = CreateUdpRuntime(faxSession?.SessionId ?? bridge.SessionId, "UDPTL LegB", localAddress, null);
            return bridge.T38B;
        }

        private void ApplyBridgeToFaxSession(TKFax_Session faxSession, TKFaxMediaBridgeRuntime bridge, bool legA, bool legB) {
            faxSession.AudioPayloadType = bridge.Codec.PayloadType;
            faxSession.AudioCodec = bridge.Codec.CodecName;
            if (legA) {
                faxSession.LocalAudioAddress = bridge.AudioA.LocalAddress;
                faxSession.LocalAudioPort = bridge.AudioA.LocalPort;
                if (bridge.T38A != null) {
                    faxSession.LocalT38Address = bridge.T38A.LocalAddress;
                    faxSession.LocalT38Port = bridge.T38A.LocalPort;
                }
            }
            if (legB) {
                if (bridge.AudioB.RemoteEndPoint != null) {
                    faxSession.RemoteAudioAddress = bridge.AudioB.RemoteEndPoint.Address.ToString();
                    faxSession.RemoteAudioPort = bridge.AudioB.RemoteEndPoint.Port;
                }
                if (bridge.T38B?.RemoteEndPoint != null) {
                    faxSession.RemoteT38Address = bridge.T38B.RemoteEndPoint.Address.ToString();
                    faxSession.RemoteT38Port = bridge.T38B.RemoteEndPoint.Port;
                }
            }
        }

        private TKFaxUdpRuntime CreateUdpRuntime(string sessionId, string mediaKind, string localAddress, TKFaxAudioCodecSelection? codec) {
            if (_mediaManager == null) {
                UdpClient dynamicUdp = CreateBoundUdpClient(sessionId, mediaKind, localAddress, 0);
                int dynamicPort = ((IPEndPoint)dynamicUdp.Client.LocalEndPoint!).Port;
                _logger?.Info($"FAX {mediaKind} Runtime gestartet | SessionID={sessionId} | Local={localAddress}:{dynamicPort} | PortQuelle=Betriebssystem");
                return new TKFaxUdpRuntime(sessionId, mediaKind, localAddress, dynamicPort, dynamicUdp, _logger);
            }

            List<int> rejectedPorts = new();
            try {
                while (true) {
                    (int rtp, int rtcp)? allocated = _mediaManager.Allocate();
                    if (!allocated.HasValue) {
                        throw new InvalidOperationException($"Kein freier Medienport fuer FAX {mediaKind} verfuegbar.");
                    }

                    int localPort = allocated.Value.rtp;
                    try {
                        UdpClient udp = CreateBoundUdpClient(sessionId, mediaKind, localAddress, localPort);
                        foreach (int rejectedPort in rejectedPorts) {
                            _mediaManager.Release(rejectedPort);
                        }

                        _logger?.Info($"FAX {mediaKind} Runtime gestartet | SessionID={sessionId} | Local={localAddress}:{localPort} | PortQuelle=RTP-Bereich");
                        return new TKFaxUdpRuntime(sessionId, mediaKind, localAddress, localPort, udp, _logger);
                    } catch (SocketException ex) {
                        // Den fehlgeschlagenen Port waehrend dieser Suche reserviert lassen,
                        // damit Allocate beim naechsten Versuch wirklich das naechste Paar liefert.
                        rejectedPorts.Add(localPort);
                        _logger?.Warn($"FAX {mediaKind} Medienport nicht bindbar | SessionID={sessionId} | Port={localPort} | Fehler={ex.Message} | Aktion=naechsten freien Port pruefen");
                    }
                }
            } catch {
                foreach (int rejectedPort in rejectedPorts) {
                    _mediaManager.Release(rejectedPort);
                }
                throw;
            }
        }

        private UdpClient CreateBoundUdpClient(string sessionId, string mediaKind, string localAddress, int localPort) {
            if (IPAddress.TryParse(localAddress, out IPAddress? parsed)) {
                IPAddress bindAddress = parsed.IsIPv4MappedToIPv6 ? parsed.MapToIPv4() : parsed;
                if (!IPAddress.Any.Equals(bindAddress) && !IPAddress.IPv6Any.Equals(bindAddress)) {
                    try {
                        UdpClient specific = new UdpClient(new IPEndPoint(bindAddress, localPort));
                        IPEndPoint actual = (IPEndPoint)specific.Client.LocalEndPoint!;
                        _logger?.Info($"FAX {mediaKind} Socket gebunden | SessionID={sessionId} | Local={actual}");
                        return specific;
                    } catch (SocketException ex) {
                        _logger?.Warn($"FAX {mediaKind} Socket Bind auf Media-IP fehlgeschlagen | SessionID={sessionId} | Local={bindAddress}:{localPort} | Fehler={ex.Message} | Fallback=Any");
                    }
                }
            }

            UdpClient any = new UdpClient(localPort);
            IPEndPoint anyActual = (IPEndPoint)any.Client.LocalEndPoint!;
            _logger?.Info($"FAX {mediaKind} Socket gebunden | SessionID={sessionId} | Local={anyActual} | Advertised={localAddress}");
            return any;
        }

        private void ReleasePort(int port) {
            if (port > 0) _mediaManager?.Release(port);
        }

        private static string RewriteMediaSdpForRelay(string? sourceSdp, string localAddress, int localPort, string mediaType, TKFaxAudioCodecSelection codec) {
            if (string.IsNullOrWhiteSpace(sourceSdp)) {
                return BuildFallbackSdp(localAddress, localPort, mediaType, codec);
            }

            string normalized = sourceSdp.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            System.Collections.Generic.List<string> lines = normalized.Split('\n').ToList();
            while (lines.Count > 0 && string.IsNullOrEmpty(lines[^1])) lines.RemoveAt(lines.Count - 1);

            int firstMediaIndex = lines.FindIndex(line => line.StartsWith("m=", StringComparison.OrdinalIgnoreCase));
            int targetMediaIndex = lines.FindIndex(line => line.StartsWith($"m={mediaType} ", StringComparison.OrdinalIgnoreCase));
            if (targetMediaIndex < 0) return BuildFallbackSdp(localAddress, localPort, mediaType, codec);

            int nextMediaIndex = lines.FindIndex(targetMediaIndex + 1, line => line.StartsWith("m=", StringComparison.OrdinalIgnoreCase));
            if (nextMediaIndex < 0) nextMediaIndex = lines.Count;

            string[] mediaParts = lines[targetMediaIndex].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (mediaParts.Length >= 2) {
                mediaParts[1] = localPort.ToString(System.Globalization.CultureInfo.InvariantCulture);
                lines[targetMediaIndex] = string.Join(" ", mediaParts);
            }

            for (int i = 0; i < lines.Count; i++) {
                if (lines[i].StartsWith("o=", StringComparison.OrdinalIgnoreCase)) {
                    string[] originParts = lines[i].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (originParts.Length >= 6) {
                        originParts[^1] = localAddress;
                        lines[i] = string.Join(" ", originParts);
                    }
                    break;
                }
            }

            bool sessionConnectionRewritten = false;
            int sessionEnd = firstMediaIndex >= 0 ? firstMediaIndex : lines.Count;
            for (int i = 0; i < sessionEnd; i++) {
                if (lines[i].StartsWith("c=", StringComparison.OrdinalIgnoreCase)) {
                    lines[i] = BuildConnectionLine(lines[i], localAddress);
                    sessionConnectionRewritten = true;
                    break;
                }
            }

            bool mediaConnectionRewritten = false;
            for (int i = targetMediaIndex + 1; i < nextMediaIndex; i++) {
                if (lines[i].StartsWith("c=", StringComparison.OrdinalIgnoreCase)) {
                    lines[i] = BuildConnectionLine(lines[i], localAddress);
                    mediaConnectionRewritten = true;
                    break;
                }
            }

            if (!sessionConnectionRewritten && !mediaConnectionRewritten) {
                lines.Insert(targetMediaIndex + 1, $"c=IN IP4 {localAddress}");
            }

            lines.Add(string.Empty);
            return string.Join("\r\n", lines);
        }

        private static string BuildConnectionLine(string sourceLine, string localAddress) {
            string[] parts = sourceLine.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            string addressType = parts.Length >= 2 ? parts[^2] : "IP4";
            return $"c=IN {addressType} {localAddress}";
        }

        private static string BuildFallbackSdp(string localAddress, int localPort, string mediaType, TKFaxAudioCodecSelection codec) {
            if (string.Equals(mediaType, "image", StringComparison.OrdinalIgnoreCase)) {
                return string.Join("\r\n", new[] {
                    "v=0",
                    $"o=TK-Server 0 0 IN IP4 {localAddress}",
                    "s=TKFax T.38 Bridge",
                    $"c=IN IP4 {localAddress}",
                    "t=0 0",
                    $"m=image {localPort} udptl t38",
                    string.Empty
                });
            }

            return string.Join("\r\n", new[] {
                "v=0",
                $"o=TK-Server 0 0 IN IP4 {localAddress}",
                "s=TKFax Audio Bridge",
                $"c=IN IP4 {localAddress}",
                "t=0 0",
                $"m=audio {localPort} RTP/AVP {codec.PayloadType}",
                $"a=rtpmap:{codec.PayloadType} {codec.CodecName}/8000",
                "a=sendrecv",
                string.Empty
            });
        }

        private static bool TryParseAudioRemote(string? sdp, out string? address, out int port) {
            address = null;
            port = 0;
            AudioDescription? audio = AudioDescription.ParseAudioDescription(sdp);
            if (audio == null || string.IsNullOrWhiteSpace(audio.Ip) || audio.Port <= 0) return false;
            address = audio.Ip;
            port = audio.Port;
            return true;
        }

        private static bool TryParseT38Remote(string? sdp, out string? address, out int port) {
            address = null;
            port = 0;
            if (string.IsNullOrWhiteSpace(sdp)) return false;
            if (!TKFax_Routing.TryReadT38Media(sdp, out TKFaxT38Media? media) || media == null || !media.HasT38) return false;
            address = media.Address;
            port = media.Port;
            return true;
        }

        private readonly struct TKFaxAudioCodecSelection {
            public TKFaxAudioCodecSelection(int payloadType, bool useALaw) {
                PayloadType = payloadType;
                UseALaw = useALaw;
            }

            public int PayloadType { get; }
            public bool UseALaw { get; }
            public string CodecName => UseALaw ? "PCMA" : "PCMU";
            public static TKFaxAudioCodecSelection Pcma => new(8, true);
        }

        private sealed class TKFaxMediaBridgeRuntime {
            private readonly TKBase_LogManager? _logger;
            private readonly long _audioSdpSessionId = DateTime.UtcNow.Ticks;
            private int _audioStarted;
            private int _t38Started;
            private long _audioSdpVersion;
            private long _aIn;
            private long _aToBOut;
            private long _bIn;
            private long _bToAOut;
            private long _t38AIn;
            private long _t38AToBOut;
            private long _t38BIn;
            private long _t38BToAOut;

            public TKFaxMediaBridgeRuntime(string sessionId, TKFaxUdpRuntime audioA, TKFaxUdpRuntime audioB, TKFaxAudioCodecSelection codec, TKBase_LogManager? logger) {
                SessionId = sessionId;
                AudioA = audioA;
                AudioB = audioB;
                Codec = codec;
                _logger = logger;
            }

            public string SessionId { get; }
            public TKFaxUdpRuntime AudioA { get; }
            public TKFaxUdpRuntime AudioB { get; }
            public TKFaxUdpRuntime? T38A { get; set; }
            public TKFaxUdpRuntime? T38B { get; set; }
            public TKFaxAudioCodecSelection Codec { get; }

            public string BuildAudioRelaySdp(string localAddress, int localPort) {
                long version = Interlocked.Increment(ref _audioSdpVersion);
                return string.Join("\r\n", new[] {
                    "v=0",
                    $"o=- {_audioSdpSessionId.ToString(System.Globalization.CultureInfo.InvariantCulture)} {version.ToString(System.Globalization.CultureInfo.InvariantCulture)} IN IP4 {localAddress}",
                    "s=TKFax Audio Relay",
                    $"c=IN IP4 {localAddress}",
                    "t=0 0",
                    $"m=audio {localPort.ToString(System.Globalization.CultureInfo.InvariantCulture)} RTP/AVP 8",
                    "a=rtpmap:8 PCMA/8000",
                    "a=ptime:20",
                    "a=sendrecv",
                    string.Empty
                });
            }

            public void StartAudioBridge() {
                if (Interlocked.Exchange(ref _audioStarted, 1) == 1) {
                    _logger?.Debug($"FAX Audio Bridge beibehalten | SessionID={SessionId} | A={AudioA.LocalAddress}:{AudioA.LocalPort} | B={AudioB.LocalAddress}:{AudioB.LocalPort}");
                    return;
                }

                AudioA.StartReceiveLoop(ForwardAudioAToBAsync);
                AudioB.StartReceiveLoop(ForwardAudioBToAAsync);
                _logger?.Info($"FAX Audio Bridge gestartet | SessionID={SessionId} | A={AudioA.LocalAddress}:{AudioA.LocalPort} | B={AudioB.LocalAddress}:{AudioB.LocalPort} | Codec=PCMA/8 | RTCP=aus");
                _logger?.Info($"FAX RTP Bridge Pfad A->B | SessionID={SessionId} | Quelle={AudioA.RemoteEndPoint?.ToString() ?? "-"} | Eingang={AudioA.LocalAddress}:{AudioA.LocalPort} | Ausgang={AudioB.LocalAddress}:{AudioB.LocalPort} | Ziel={AudioB.RemoteEndPoint?.ToString() ?? "-"}");
                _logger?.Info($"FAX RTP Bridge Pfad B->A | SessionID={SessionId} | Quelle={AudioB.RemoteEndPoint?.ToString() ?? "-"} | Eingang={AudioB.LocalAddress}:{AudioB.LocalPort} | Ausgang={AudioA.LocalAddress}:{AudioA.LocalPort} | Ziel={AudioA.RemoteEndPoint?.ToString() ?? "-"}");
            }

            public async ValueTask ForwardAudioAToBAsync(byte[] packet, IPEndPoint? source, CancellationToken cancellationToken) {
                long aIn = Interlocked.Increment(ref _aIn);
                bool sent = await AudioB.SendAsync(packet, cancellationToken).ConfigureAwait(false);
                if (sent) Interlocked.Increment(ref _aToBOut);
                LogAudioCounters("A->B", packet, source, AudioA, AudioB, aIn, sent);
            }

            public async ValueTask ForwardAudioBToAAsync(byte[] packet, IPEndPoint? source, CancellationToken cancellationToken) {
                long bIn = Interlocked.Increment(ref _bIn);
                bool sent = await AudioA.SendAsync(packet, cancellationToken).ConfigureAwait(false);
                if (sent) Interlocked.Increment(ref _bToAOut);
                LogAudioCounters("B->A", packet, source, AudioB, AudioA, bIn, sent);
            }

            public void StartT38Bridge() {
                TKFaxUdpRuntime? sideA = T38A;
                TKFaxUdpRuntime? sideB = T38B;
                if (sideA == null || sideB == null) return;
                if (Interlocked.Exchange(ref _t38Started, 1) == 1) return;
                sideA.StartReceiveLoop(ForwardT38AToBAsync);
                sideB.StartReceiveLoop(ForwardT38BToAAsync);
                _logger?.Info($"FAX T.38 UDPTL Bridge gestartet | SessionID={SessionId} | A={sideA.LocalAddress}:{sideA.LocalPort} | B={sideB.LocalAddress}:{sideB.LocalPort}");
            }

            public async ValueTask ForwardT38AToBAsync(byte[] packet, IPEndPoint? source, CancellationToken cancellationToken) {
                TKFaxUdpRuntime? sideA = T38A;
                TKFaxUdpRuntime? sideB = T38B;
                if (sideA == null || sideB == null) return;
                long aIn = Interlocked.Increment(ref _t38AIn);
                bool sent = await sideB.SendAsync(packet, cancellationToken).ConfigureAwait(false);
                if (sent) Interlocked.Increment(ref _t38AToBOut);
                LogT38Counters("A->B", packet, source, sideA, sideB, aIn, sent);
            }

            public async ValueTask ForwardT38BToAAsync(byte[] packet, IPEndPoint? source, CancellationToken cancellationToken) {
                TKFaxUdpRuntime? sideA = T38A;
                TKFaxUdpRuntime? sideB = T38B;
                if (sideA == null || sideB == null) return;
                long bIn = Interlocked.Increment(ref _t38BIn);
                bool sent = await sideA.SendAsync(packet, cancellationToken).ConfigureAwait(false);
                if (sent) Interlocked.Increment(ref _t38BToAOut);
                LogT38Counters("B->A", packet, source, sideB, sideA, bIn, sent);
            }

            public void Stop() {
                _logger?.Info($"FAX RTP Bridge Endstand | SessionID={SessionId} | A_IN={Interlocked.Read(ref _aIn)} | A_TO_B_OUT={Interlocked.Read(ref _aToBOut)} | B_IN={Interlocked.Read(ref _bIn)} | B_TO_A_OUT={Interlocked.Read(ref _bToAOut)}");
                if (Volatile.Read(ref _t38Started) == 1) {
                    _logger?.Info($"FAX T.38 UDPTL Bridge Endstand | SessionID={SessionId} | A_IN={Interlocked.Read(ref _t38AIn)} | A_TO_B_OUT={Interlocked.Read(ref _t38AToBOut)} | B_IN={Interlocked.Read(ref _t38BIn)} | B_TO_A_OUT={Interlocked.Read(ref _t38BToAOut)}");
                }
                AudioA.Stop();
                AudioB.Stop();
                T38A?.Stop();
                T38B?.Stop();
            }

            private void LogAudioCounters(string direction, byte[] packet, IPEndPoint? source, TKFaxUdpRuntime ingress, TKFaxUdpRuntime egress, long directionInputCount, bool sent) {
                int payloadType = packet.Length > 1 ? packet[1] & 0x7F : -1;
                int sequence = packet.Length > 3 ? (packet[2] << 8) | packet[3] : -1;
                IPEndPoint? expectedSource = ingress.RemoteEndPoint;
                bool sourceMatches = source == null || expectedSource == null || source.Equals(expectedSource);
                bool anomaly = !sent || payloadType != 8 || !sourceMatches;
                if (!anomaly && directionInputCount > 5 && directionInputCount % 100 != 0) return;

                long aIn = Interlocked.Read(ref _aIn);
                long aToBOut = Interlocked.Read(ref _aToBOut);
                long bIn = Interlocked.Read(ref _bIn);
                long bToAOut = Interlocked.Read(ref _bToAOut);
                string destination = egress.RemoteEndPoint?.ToString() ?? "-";
                string message = $"FAX RTP Bridge Counter | SessionID={SessionId} | Richtung={direction} | A_IN={aIn} | A_TO_B_OUT={aToBOut} | B_IN={bIn} | B_TO_A_OUT={bToAOut} | PT={payloadType} | Seq={sequence} | Bytes={packet.Length} | Quelle={source?.ToString() ?? "-"} | ErwarteteQuelle={expectedSource?.ToString() ?? "-"} | QuellePasst={sourceMatches} | Eingang={ingress.LocalAddress}:{ingress.LocalPort} | Ausgang={egress.LocalAddress}:{egress.LocalPort} | Ziel={destination} | Gesendet={sent}";
                if (anomaly) _logger?.Warn(message);
                else _logger?.Debug(message);
            }

            private void LogT38Counters(string direction, byte[] packet, IPEndPoint? source, TKFaxUdpRuntime ingress, TKFaxUdpRuntime egress, long directionInputCount, bool sent) {
                IPEndPoint? expectedSource = ingress.RemoteEndPoint;
                bool sourceMatches = source == null || expectedSource == null || source.Equals(expectedSource);
                bool anomaly = !sent || !sourceMatches;
                if (!anomaly && directionInputCount > 5 && directionInputCount % 100 != 0) return;

                long aIn = Interlocked.Read(ref _t38AIn);
                long aToBOut = Interlocked.Read(ref _t38AToBOut);
                long bIn = Interlocked.Read(ref _t38BIn);
                long bToAOut = Interlocked.Read(ref _t38BToAOut);
                string destination = egress.RemoteEndPoint?.ToString() ?? "-";
                string message = $"FAX T.38 UDPTL Bridge Counter | SessionID={SessionId} | Richtung={direction} | A_IN={aIn} | A_TO_B_OUT={aToBOut} | B_IN={bIn} | B_TO_A_OUT={bToAOut} | Bytes={packet.Length} | Quelle={source?.ToString() ?? "-"} | ErwarteteQuelle={expectedSource?.ToString() ?? "-"} | QuellePasst={sourceMatches} | Eingang={ingress.LocalAddress}:{ingress.LocalPort} | Ausgang={egress.LocalAddress}:{egress.LocalPort} | Ziel={destination} | Gesendet={sent}";
                if (anomaly) _logger?.Warn(message);
                else _logger?.Debug(message);
            }
        }

        private sealed class TKFaxUdpRuntime {
            private readonly string _sessionId;
            private readonly string _mediaKind;
            private readonly UdpClient _udp;
            private readonly TKBase_LogManager? _logger;
            private readonly CancellationTokenSource _cts = new();
            private int _started;

            public TKFaxUdpRuntime(string sessionId, string mediaKind, string localAddress, int localPort, UdpClient udp, TKBase_LogManager? logger) {
                _sessionId = sessionId;
                _mediaKind = mediaKind;
                LocalAddress = localAddress;
                LocalPort = localPort;
                _udp = udp;
                _logger = logger;
            }

            public string LocalAddress { get; }
            public int LocalPort { get; }
            public IPEndPoint? RemoteEndPoint { get; private set; }

            public bool SetRemote(string remoteAddress, int remotePort) {
                if (!IPAddress.TryParse(remoteAddress, out IPAddress? ip) || remotePort <= 0) {
                    _logger?.Warn($"FAX Media Remote ungueltig | SessionID={_sessionId} | Medium={_mediaKind} | Remote={remoteAddress}:{remotePort}");
                    return false;
                }

                IPEndPoint next = new IPEndPoint(ip, remotePort);
                IPEndPoint? previous = RemoteEndPoint;
                RemoteEndPoint = next;
                if (previous == null) {
                    _logger?.Info($"FAX Media Remote gesetzt | SessionID={_sessionId} | Medium={_mediaKind} | Local={LocalAddress}:{LocalPort} | Remote={next}");
                }
                else if (!previous.Equals(next)) {
                    _logger?.Info($"FAX Media Remote aktualisiert | SessionID={_sessionId} | Medium={_mediaKind} | Local={LocalAddress}:{LocalPort} | Alt={previous} | Neu={next} | BridgeSocket=beibehalten");
                }
                else {
                    _logger?.Debug($"FAX Media Remote unveraendert | SessionID={_sessionId} | Medium={_mediaKind} | Local={LocalAddress}:{LocalPort} | Remote={next}");
                }
                return true;
            }

            public void StartReceiveLoop(Func<byte[], IPEndPoint, CancellationToken, ValueTask> handler) {
                if (Interlocked.Exchange(ref _started, 1) == 1) return;
                _ = Task.Run(async () => {
                    while (!_cts.IsCancellationRequested) {
                        try {
                            UdpReceiveResult result = await _udp.ReceiveAsync(_cts.Token).ConfigureAwait(false);
                            if (RemoteEndPoint == null) {
                                RemoteEndPoint = result.RemoteEndPoint;
                                _logger?.Info($"FAX Media Remote gelernt | SessionID={_sessionId} | Medium={_mediaKind} | Local={LocalAddress}:{LocalPort} | Remote={RemoteEndPoint}");
                            }
                            await handler(result.Buffer, result.RemoteEndPoint, _cts.Token).ConfigureAwait(false);
                        } catch (OperationCanceledException) { break; }
                        catch (ObjectDisposedException) { break; }
                        catch (Exception ex) { _logger?.Warn($"FAX Media Receive Fehler | SessionID={_sessionId} | Medium={_mediaKind} | Local={LocalAddress}:{LocalPort} | Fehler={ex.Message}"); }
                    }
                });
            }

            public async ValueTask<bool> SendAsync(byte[] packet, CancellationToken cancellationToken = default) {
                IPEndPoint? destination = RemoteEndPoint;
                if (packet == null || packet.Length == 0 || destination == null || _cts.IsCancellationRequested) return false;
                try {
                    int sent = await _udp.SendAsync(packet.AsMemory(), destination, cancellationToken).ConfigureAwait(false);
                    return sent == packet.Length;
                } catch (OperationCanceledException) {
                    return false;
                } catch (ObjectDisposedException) {
                    return false;
                } catch (Exception ex) {
                    _logger?.Warn($"FAX Media Send Fehler | SessionID={_sessionId} | Medium={_mediaKind} | Local={LocalAddress}:{LocalPort} | Remote={destination} | Fehler={ex.Message}");
                    return false;
                }
            }

            public void Stop() {
                try { _cts.Cancel(); } catch { }
                try { _udp.Close(); } catch { }
                try { _udp.Dispose(); } catch { }
                try { _cts.Dispose(); } catch { }
            }
        }
    }
}
