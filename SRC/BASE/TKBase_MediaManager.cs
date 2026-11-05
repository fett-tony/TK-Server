namespace TKServer.BASE {
    using SIPSorcery.Media;
    using SIPSorcery.SIP;
    using SIPSorceryMedia.Abstractions;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines the <see cref="TKBase_MediaManager" />
    /// </summary>
    internal sealed class TKBase_MediaManager {
        /// <summary>
        /// Gets or sets the CallId
        /// </summary>
        public string CallId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the OriginalInvite
        /// </summary>
        public SIPRequest? OriginalInvite { get; set; }

        /// <summary>
        /// Gets or sets the CallerEndPoint
        /// </summary>
        public SIPEndPoint? CallerEndPoint { get; set; }

        /// <summary>
        /// Gets or sets the TargetEndPoint
        /// </summary>
        public SIPEndPoint? TargetEndPoint { get; set; }

        /// <summary>
        /// Gets or sets the CallerSip
        /// </summary>
        public IPEndPoint? CallerSip { get; set; }

        /// <summary>
        /// Gets or sets the CalleeSip
        /// </summary>
        public IPEndPoint? CalleeSip { get; set; }

        /// <summary>
        /// Gets or sets the CallerRtpRemote
        /// </summary>
        public IPEndPoint? CallerRtpRemote { get; set; }

        /// <summary>
        /// Gets or sets the CalleeRtpRemote
        /// </summary>
        public IPEndPoint? CalleeRtpRemote { get; set; }

        /// <summary>
        /// Gets or sets the CallerRelayPort
        /// </summary>
        public int CallerRelayPort { get; set; }

        /// <summary>
        /// Gets or sets the CalleeRelayPort
        /// </summary>
        public int CalleeRelayPort { get; set; }

        /// <summary>
        /// Gets or sets the Relay
        /// </summary>
        public TKBase_MediaManager? Relay { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether Established
        /// </summary>
        public bool Established { get; set; }

        /// <summary>
        /// Gets or sets the Ip
        /// </summary>
        public string Ip { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Port
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Gets or sets the Proto
        /// </summary>
        public string? Proto { get; set; }

        /// <summary>
        /// Gets or sets the Payloads
        /// </summary>
        public string? Payloads { get; set; }

        /// <summary>
        /// Gets the CallerPort
        /// </summary>
        public int CallerPort { get; }

        /// <summary>
        /// Gets the CalleePort
        /// </summary>
        public int CalleePort { get; }

        /// <summary>
        /// Defines the _callerSocket
        /// </summary>
        private readonly UdpClient? _callerSocket;

        /// <summary>
        /// Defines the _calleeSocket
        /// </summary>
        private readonly UdpClient? _calleeSocket;

        /// <summary>
        /// Defines the _cts
        /// </summary>
        private readonly CancellationTokenSource _cts = new();

        /// <summary>
        /// Defines the _logger
        /// </summary>
        private readonly TKBase_LogManager? _logger;

        /// <summary>
        /// Raised for every RTP packet that is relayed by this media bridge.
        /// Direction values are "Caller->Callee" and "Callee->Caller".
        /// </summary>
        public event Action<string, byte[]>? RtpPacketRelayed;

        /// <summary>
        /// Defines the _started
        /// </summary>
        private int _started;

        /// <summary>
        /// Defines the _callerRemote
        /// </summary>
        private IPEndPoint? _callerRemote;

        /// <summary>
        /// Defines the _calleeRemote
        /// </summary>
        private IPEndPoint? _calleeRemote;

        /// <summary>
        /// Defines whether the caller RTP remote endpoint is fixed from SDP.
        /// </summary>
        private bool _callerRemoteFixed;

        /// <summary>
        /// Defines whether the callee RTP remote endpoint is fixed from SDP.
        /// </summary>
        private bool _calleeRemoteFixed;

        /// <summary>
        /// Defines the _callerToCalleePackets
        /// </summary>
        private long _callerToCalleePackets;

        /// <summary>
        /// Defines the _calleeToCallerPackets
        /// </summary>
        private long _calleeToCallerPackets;

        /// <summary>
        /// Defines the _lock
        /// </summary>
        private readonly object _lock = new();

        /// <summary>
        /// Defines the _usedPorts
        /// </summary>
        private readonly HashSet<int> _usedPorts = new();

        /// <summary>
        /// Defines the _minPort
        /// </summary>
        private readonly int _minPort;

        /// <summary>
        /// Defines the _maxPort
        /// </summary>
        private readonly int _maxPort;

        /// <summary>
        /// Initializes a new instance of the <see cref="TKBase_MediaManager"/> class.
        /// </summary>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        /// <param name="minPort">The minPort<see cref="int"/></param>
        /// <param name="maxPort">The maxPort<see cref="int"/></param>
        public TKBase_MediaManager(TKBase_LogManager? logger, int minPort, int maxPort) {
            _logger = logger;
            _minPort = minPort;
            _maxPort = maxPort;
        }

        /// <summary>
        /// The CreateRelay
        /// </summary>
        /// <param name="callerPort">The callerPort<see cref="int"/></param>
        /// <param name="calleePort">The calleePort<see cref="int"/></param>
        /// <returns>The <see cref="TKBase_MediaManager"/></returns>
        public TKBase_MediaManager CreateRelay(int callerPort, int calleePort) {
            return new TKBase_MediaManager(callerPort, calleePort, _logger);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TKBase_MediaManager"/> class.
        /// </summary>
        /// <param name="callerPort">The callerPort<see cref="int"/></param>
        /// <param name="calleePort">The calleePort<see cref="int"/></param>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        public TKBase_MediaManager(int callerPort, int calleePort, TKBase_LogManager? logger = null) {
            CallerPort = callerPort;
            CalleePort = calleePort;
            _logger = logger;

            _callerSocket = new UdpClient(callerPort);
            _calleeSocket = new UdpClient(calleePort);
        }

        /// <summary>
        /// The Allocate
        /// </summary>
        /// <returns>The <see cref="(int rtp, int rtcp)?"/></returns>
        public (int rtp, int rtcp)? Allocate() {
            lock (_lock) {
                for (int p = _minPort; p < _maxPort; p += 2) {
                    if (_usedPorts.Contains(p) || _usedPorts.Contains(p + 1)) {
                        continue;
                    }

                    _usedPorts.Add(p);
                    _usedPorts.Add(p + 1);
                    return (p, p + 1);
                }
            }

            return null;
        }

        /// <summary>
        /// The Release
        /// </summary>
        /// <param name="rtpPort">The rtpPort<see cref="int"/></param>
        public void Release(int rtpPort) {
            lock (_lock) {
                _usedPorts.Remove(rtpPort);
                _usedPorts.Remove(rtpPort + 1);
            }
        }

        /// <summary>
        /// The SetCaller
        /// </summary>
        /// <param name="remote">The remote<see cref="IPEndPoint"/></param>
        /// <param name="fixedRemote">True, wenn der entfernte RTP-Endpunkt fest aus SDP übernommen und nicht durch das erste eingehende RTP-Paket überschrieben werden soll.</param>
        public void SetCaller(IPEndPoint remote, bool fixedRemote = false) {
            _callerRemote = remote;
            _callerRemoteFixed = fixedRemote;
            _logger?.Info($"RTP Caller SDP-Hinweis | {remote} | FixedRemote={fixedRemote}");
        }

        /// <summary>
        /// The SetCallee
        /// </summary>
        /// <param name="remote">The remote<see cref="IPEndPoint"/></param>
        /// <param name="fixedRemote">True, wenn der entfernte RTP-Endpunkt fest aus SDP übernommen und nicht durch das erste eingehende RTP-Paket überschrieben werden soll.</param>
        public void SetCallee(IPEndPoint remote, bool fixedRemote = false) {
            _calleeRemote = remote;
            _calleeRemoteFixed = fixedRemote;
            if (IPAddress.IsLoopback(remote.Address)) {
                _logger?.Info($"RTP Relay lokales Ziel | {remote} | FixedRemote={fixedRemote} | Hinweis=kein externes SIP-SDP-Ziel");
            } else {
                _logger?.Info($"RTP Callee SDP-Hinweis | {remote} | FixedRemote={fixedRemote}");
            }
        }

        /// <summary>
        /// The Start
        /// </summary>
        public void Start() {
            if (_callerSocket == null || _calleeSocket == null) {
                _logger?.Warn("RTP Relay Start verworfen | MediaManager wurde nur als PortAllocator initialisiert.");
                return;
            }

            if (Interlocked.Exchange(ref _started, 1) == 1) {
                return;
            }

            _logger?.Info($"RTP Relay gestartet | CallerPort={CallerPort} | CalleePort={CalleePort} | CallerHint={_callerRemote} | CalleeHint={_calleeRemote}");
            _logger?.Info($"RTP Pump aktiv | Richtung=Caller->Callee | Listen={CallerPort}");
            _logger?.Info($"RTP Pump aktiv | Richtung=Callee->Caller | Listen={CalleePort}");
            _ = Task.Run(() => PumpCallerToCalleeAsync(_cts.Token));
            _ = Task.Run(() => PumpCalleeToCallerAsync(_cts.Token));
        }

        /// <summary>
        /// The StopRelay
        /// </summary>
        public void StopRelay() {
            if (_cts.IsCancellationRequested) {
                return;
            }

            _logger?.Info($"RTP Relay stoppen | CallerPort={CallerPort} | CalleePort={CalleePort} | Caller->Callee={_callerToCalleePackets} | Callee->Caller={_calleeToCallerPackets}");
            _cts.Cancel();
        }

        /// <summary>
        /// The PumpCallerToCalleeAsync
        /// </summary>
        /// <param name="ct">The ct<see cref="CancellationToken"/></param>
        /// <returns>The <see cref="Task"/></returns>
        private async Task PumpCallerToCalleeAsync(CancellationToken ct) {
            UdpClient? callerSocket = _callerSocket;
            UdpClient? calleeSocket = _calleeSocket;
            if (callerSocket == null || calleeSocket == null) {
                return;
            }

            try {
                while (!ct.IsCancellationRequested) {
                    UdpReceiveResult packet = await callerSocket.ReceiveAsync(ct);
                    if (!AcceptCallerRemoteFromPacket(packet.RemoteEndPoint)) {
                        continue;
                    }

                    IPEndPoint? calleeRemote = _calleeRemote;
                    if (calleeRemote == null) {
                        _logger?.Warn($"RTP Caller->Callee verworfen | Kein Callee-Ziel bekannt | Source={packet.RemoteEndPoint}");
                        continue;
                    }

                    await calleeSocket.SendAsync(packet.Buffer, packet.Buffer.Length, calleeRemote);
                    NotifyRtpPacketRelayed("Caller->Callee", packet.Buffer);
                    long count = Interlocked.Increment(ref _callerToCalleePackets);
                    if (count == 1 || count % 50 == 0) {
                        _logger?.Debug($"RTP Caller->Callee | Count={count} | Source={packet.RemoteEndPoint} | Target={calleeRemote} | Bytes={packet.Buffer.Length} | PT={GetRtpPayloadType(packet.Buffer)} | Seq={GetRtpSequenceNumber(packet.Buffer)} | SSRC={GetRtpSsrc(packet.Buffer)}");
                    }
                }
            } catch (OperationCanceledException) {
            } catch (ObjectDisposedException) {
            } catch (Exception ex) {
                _logger?.Warn($"RTP Pump Caller->Callee beendet | {ex.Message}");
            }
        }

        /// <summary>
        /// The PumpCalleeToCallerAsync
        /// </summary>
        /// <param name="ct">The ct<see cref="CancellationToken"/></param>
        /// <returns>The <see cref="Task"/></returns>
        private async Task PumpCalleeToCallerAsync(CancellationToken ct) {
            UdpClient? callerSocket = _callerSocket;
            UdpClient? calleeSocket = _calleeSocket;
            if (callerSocket == null || calleeSocket == null) {
                return;
            }

            try {
                while (!ct.IsCancellationRequested) {
                    UdpReceiveResult packet = await calleeSocket.ReceiveAsync(ct);
                    if (!AcceptCalleeRemoteFromPacket(packet.RemoteEndPoint)) {
                        continue;
                    }

                    IPEndPoint? callerRemote = _callerRemote;
                    if (callerRemote == null) {
                        _logger?.Warn($"RTP Callee->Caller verworfen | Kein Caller-Ziel bekannt | Source={packet.RemoteEndPoint}");
                        continue;
                    }

                    await callerSocket.SendAsync(packet.Buffer, packet.Buffer.Length, callerRemote);
                    NotifyRtpPacketRelayed("Callee->Caller", packet.Buffer);
                    long count = Interlocked.Increment(ref _calleeToCallerPackets);
                    if (count == 1 || count % 50 == 0) {
                        _logger?.Debug($"RTP Callee->Caller | Count={count} | Source={packet.RemoteEndPoint} | Target={callerRemote} | Bytes={packet.Buffer.Length} | PT={GetRtpPayloadType(packet.Buffer)} | Seq={GetRtpSequenceNumber(packet.Buffer)} | SSRC={GetRtpSsrc(packet.Buffer)}");
                    }
                }
            } catch (OperationCanceledException) {
            } catch (ObjectDisposedException) {
            } catch (Exception ex) {
                _logger?.Warn($"RTP Pump Callee->Caller beendet | {ex.Message}");
            }
        }


        /// <summary>
        /// Sends a short service announcement directly to one RTP side without bridging it to the other side.
        /// legName "LegA" targets the caller side, "LegB" targets the callee side.
        /// </summary>
        public async Task SendPcmAnnouncementToLegAsync(string legName, byte[] pcm16LittleEndian, int sourceSampleRate, string reason) {
            if (pcm16LittleEndian == null || pcm16LittleEndian.Length < 2) {
                return;
            }

            UdpClient? socket;
            IPEndPoint? remote;
            string normalizedLeg = string.Equals(legName, "LegB", StringComparison.OrdinalIgnoreCase) ? "LegB" : "LegA";

            if (string.Equals(normalizedLeg, "LegB", StringComparison.OrdinalIgnoreCase)) {
                socket = _calleeSocket;
                remote = _calleeRemote;
            } else {
                socket = _callerSocket;
                remote = _callerRemote;
            }

            if (socket == null || remote == null || _cts.IsCancellationRequested) {
                _logger?.Warn($"SVC LegD Ansage nicht gesendet | Leg={normalizedLeg} | Grund=kein RTP-Ziel | Reason={reason ?? "-"}");
                return;
            }

            short[] sourceSamples = ConvertPcmBytesToSamples(pcm16LittleEndian);
            short[] samples8k = ResampleTo8k(sourceSamples, sourceSampleRate);
            if (samples8k.Length == 0) {
                return;
            }

            AudioEncoder encoder = new AudioEncoder();
            AudioFormat pcma = new AudioFormat(SDPWellKnownMediaFormatsEnum.PCMA);
            int seed = Environment.TickCount;
            if (!string.IsNullOrWhiteSpace(reason)) {
                seed ^= reason.GetHashCode();
            }
            Random random = new Random(seed);
            ushort sequence = (ushort)random.Next(0, ushort.MaxValue);
            uint timestamp = (uint)random.Next(0, int.MaxValue);
            uint ssrc = (uint)random.Next(1, int.MaxValue);
            const int frameSamples = 160;
            int sent = 0;

            for (int offset = 0; offset < samples8k.Length && !_cts.IsCancellationRequested; offset += frameSamples) {
                int count = Math.Min(frameSamples, samples8k.Length - offset);
                if (count <= 0) {
                    break;
                }

                short[] frame = new short[frameSamples];
                Array.Copy(samples8k, offset, frame, 0, count);
                byte[] payload = encoder.EncodeAudio(frame, pcma);
                byte[] packet = BuildRtpPacket(payload, 8, sequence++, timestamp, ssrc, sent == 0);

                await socket.SendAsync(packet, packet.Length, remote);
                sent++;
                timestamp += frameSamples;
                await Task.Delay(20);
            }

            _logger?.Info($"SVC LegD Ansage gesendet | Leg={normalizedLeg} | Ziel={remote} | Frames={sent} | Reason={reason ?? "-"}");
        }

        private static short[] ConvertPcmBytesToSamples(byte[] pcm16LittleEndian) {
            int sampleCount = pcm16LittleEndian.Length / 2;
            short[] samples = new short[sampleCount];
            Buffer.BlockCopy(pcm16LittleEndian, 0, samples, 0, sampleCount * 2);
            return samples;
        }

        private static short[] ResampleTo8k(short[] sourceSamples, int sourceSampleRate) {
            if (sourceSamples == null || sourceSamples.Length == 0) {
                return Array.Empty<short>();
            }

            if (sourceSampleRate <= 0 || sourceSampleRate == 8000) {
                return sourceSamples;
            }

            int targetCount = Math.Max(1, (int)Math.Round(sourceSamples.Length * 8000.0 / sourceSampleRate));
            short[] target = new short[targetCount];
            for (int i = 0; i < target.Length; i++) {
                int sourceIndex = (int)Math.Min(sourceSamples.Length - 1, Math.Round(i * sourceSampleRate / 8000.0));
                target[i] = sourceSamples[sourceIndex];
            }

            return target;
        }

        private static byte[] BuildRtpPacket(byte[] payload, byte payloadType, ushort sequence, uint timestamp, uint ssrc, bool marker) {
            byte[] packet = new byte[12 + payload.Length];
            packet[0] = 0x80;
            packet[1] = (byte)((marker ? 0x80 : 0x00) | (payloadType & 0x7F));
            packet[2] = (byte)(sequence >> 8);
            packet[3] = (byte)(sequence & 0xFF);
            packet[4] = (byte)(timestamp >> 24);
            packet[5] = (byte)(timestamp >> 16);
            packet[6] = (byte)(timestamp >> 8);
            packet[7] = (byte)(timestamp & 0xFF);
            packet[8] = (byte)(ssrc >> 24);
            packet[9] = (byte)(ssrc >> 16);
            packet[10] = (byte)(ssrc >> 8);
            packet[11] = (byte)(ssrc & 0xFF);
            Buffer.BlockCopy(payload, 0, packet, 12, payload.Length);
            return packet;
        }

        /// <summary>
        /// The NotifyRtpPacketRelayed
        /// </summary>
        /// <param name="direction">The direction<see cref="string"/></param>
        /// <param name="buffer">The buffer<see cref="byte[]"/></param>
        private void NotifyRtpPacketRelayed(string direction, byte[] buffer) {
            Action<string, byte[]>? handler = RtpPacketRelayed;
            if (handler == null || buffer == null || buffer.Length == 0) {
                return;
            }

            try {
                handler(direction, buffer);
            } catch (Exception ex) {
                _logger?.Warn($"RTP Relay Packet-Callback fehlgeschlagen | Richtung={direction} | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The GetRtpPayloadType
        /// </summary>
        /// <param name="buffer">The buffer<see cref="byte[]"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int GetRtpPayloadType(byte[] buffer) {
            return buffer.Length > 1 ? buffer[1] & 0x7F : -1;
        }

        /// <summary>
        /// The GetRtpSequenceNumber
        /// </summary>
        /// <param name="buffer">The buffer<see cref="byte[]"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int GetRtpSequenceNumber(byte[] buffer) {
            return buffer.Length > 3 ? (buffer[2] << 8) | buffer[3] : -1;
        }

        /// <summary>
        /// The GetRtpSsrc
        /// </summary>
        /// <param name="buffer">The buffer<see cref="byte[]"/></param>
        /// <returns>The <see cref="uint"/></returns>
        private static uint GetRtpSsrc(byte[] buffer) {
            return buffer.Length > 11
                ? ((uint)buffer[8] << 24) | ((uint)buffer[9] << 16) | ((uint)buffer[10] << 8) | buffer[11]
                : 0;
        }

        /// <summary>
        /// Prueft und uebernimmt den Caller-Quellendpunkt fuer eingehende RTP/UDPTL-Pakete.
        /// Bei festem SDP-Endpunkt werden Pakete von abweichenden Ports verworfen.
        /// </summary>
        /// <param name="actualRemote">Der tatsaechliche Paket-Quellendpunkt.</param>
        /// <returns>True, wenn das Paket weitergeleitet werden darf.</returns>
        private bool AcceptCallerRemoteFromPacket(IPEndPoint actualRemote) {
            if (_callerRemote == null) {
                _callerRemote = actualRemote;
                _logger?.Info($"RTP Caller gelernt | Actual={actualRemote}");
                return true;
            }

            if (_callerRemoteFixed) {
                if (_callerRemote.Equals(actualRemote)) {
                    return true;
                }

                _logger?.Debug($"RTP Caller Source verworfen | Fest={_callerRemote} | Paket={actualRemote}");
                return false;
            }

            if (!_callerRemote.Equals(actualRemote)) {
                _logger?.Info($"RTP Caller Source aktualisiert | Alt={_callerRemote} | Neu={actualRemote}");
                _callerRemote = actualRemote;
            }

            return true;
        }

        /// <summary>
        /// Prueft und uebernimmt den Callee-Quellendpunkt fuer eingehende RTP/UDPTL-Pakete.
        /// Bei festem SDP-Endpunkt werden Pakete von abweichenden Ports verworfen.
        /// </summary>
        /// <param name="actualRemote">Der tatsaechliche Paket-Quellendpunkt.</param>
        /// <returns>True, wenn das Paket weitergeleitet werden darf.</returns>
        private bool AcceptCalleeRemoteFromPacket(IPEndPoint actualRemote) {
            if (_calleeRemote == null) {
                _calleeRemote = actualRemote;
                _logger?.Info($"RTP Callee gelernt | Actual={actualRemote}");
                return true;
            }

            if (_calleeRemoteFixed) {
                if (_calleeRemote.Equals(actualRemote)) {
                    return true;
                }

                _logger?.Debug($"RTP Callee Source verworfen | Fest={_calleeRemote} | Paket={actualRemote}");
                return false;
            }

            if (!_calleeRemote.Equals(actualRemote)) {
                _logger?.Info($"RTP Callee Source aktualisiert | Alt={_calleeRemote} | Neu={actualRemote}");
                _calleeRemote = actualRemote;
            }

            return true;
        }

        /// <summary>
        /// The Dispose
        /// </summary>
        public void Dispose() {
            StopRelay();
            _callerSocket?.Dispose();
            _calleeSocket?.Dispose();
            _cts.Dispose();
        }
    }

    /// <summary>
    /// Defines the <see cref="AudioDescription" />
    /// </summary>
    internal sealed class AudioDescription {
        /// <summary>
        /// Gets or sets the Ip
        /// </summary>
        public string Ip { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Port
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Gets or sets the Proto
        /// </summary>
        public string? Proto { get; set; }

        /// <summary>
        /// Gets or sets the Payloads
        /// </summary>
        public string? Payloads { get; set; }

        /// <summary>
        /// The Parse
        /// </summary>
        /// <param name="sdp">The sdp<see cref="string"/></param>
        /// <returns>The <see cref="(string ip, int port)?"/></returns>
        public static (string ip, int port)? Parse(string sdp) {
            AudioDescription? audio = ParseAudioDescription(sdp);
            return audio == null ? null : (audio.Ip, audio.Port);
        }

        /// <summary>
        /// The ParseAudioDescription
        /// </summary>
        /// <param name="sdp">The sdp<see cref="string?"/></param>
        /// <returns>The <see cref="AudioDescription?"/></returns>
        public static AudioDescription? ParseAudioDescription(string? sdp) {
            if (string.IsNullOrWhiteSpace(sdp)) {
                return null;
            }

            string? sessionIp = null;
            string? mediaIp = null;
            int port = 0;
            string? proto = null;
            string? payloads = null;
            bool inAudio = false;

            foreach (string raw in SplitLines(sdp)) {
                string line = raw.Trim();
                if (line.Length == 0) {
                    continue;
                }

                if (line.StartsWith("m=", StringComparison.OrdinalIgnoreCase)) {
                    inAudio = false;

                    if (line.StartsWith("m=audio", StringComparison.OrdinalIgnoreCase)) {
                        string[] parts = line.Substring(2).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 3 && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedPort)) {
                            port = parsedPort;
                            proto = parts[2];
                            payloads = parts.Length > 3 ? string.Join(" ", parts.Skip(3)) : null;
                            inAudio = true;
                        }
                    }

                    continue;
                }

                if (line.StartsWith("c=", StringComparison.OrdinalIgnoreCase)) {
                    string? ip = ParseConnectionAddress(line);
                    if (string.IsNullOrWhiteSpace(ip)) {
                        continue;
                    }

                    if (inAudio) {
                        mediaIp = ip;
                    } else if (string.IsNullOrWhiteSpace(sessionIp)) {
                        sessionIp = ip;
                    }
                }
            }

            string? finalIp = mediaIp ?? sessionIp;
            if (string.IsNullOrWhiteSpace(finalIp) || port <= 0) {
                return null;
            }

            return new AudioDescription {
                Ip = finalIp,
                Port = port,
                Proto = proto,
                Payloads = payloads
            };
        }

        /// <summary>
        /// The HasAudioMedia
        /// </summary>
        /// <param name="sdp">The sdp<see cref="string?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public static bool HasAudioMedia(string? sdp) => ParseAudioDescription(sdp) != null;

        /// <summary>
        /// The HasConnectionAddress
        /// </summary>
        /// <param name="sdp">The sdp<see cref="string?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public static bool HasConnectionAddress(string? sdp) {
            if (string.IsNullOrWhiteSpace(sdp)) {
                return false;
            }

            return SplitLines(sdp)
                .Select(x => x.Trim())
                .Any(x => x.StartsWith("c=IN IP4", StringComparison.OrdinalIgnoreCase) || x.StartsWith("c=IN IP6", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// The Rewrite
        /// </summary>
        /// <param name="sdp">The sdp<see cref="string"/></param>
        /// <param name="newIp">The newIp<see cref="string"/></param>
        /// <param name="newPort">The newPort<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string Rewrite(string sdp, string newIp, int newPort) {
            if (string.IsNullOrWhiteSpace(sdp)) {
                return sdp;
            }

            List<string> lines = SplitLines(sdp).ToList();
            bool inAudio = false;
            bool replacedMediaConnection = false;
            bool replacedSessionConnection = false;
            int? audioLineIndex = null;

            for (int i = 0; i < lines.Count; i++) {
                string trimmed = lines[i].Trim();

                if (trimmed.StartsWith("m=", StringComparison.OrdinalIgnoreCase)) {
                    inAudio = trimmed.StartsWith("m=audio", StringComparison.OrdinalIgnoreCase);
                    if (inAudio) {
                        audioLineIndex = i;
                        string[] parts = trimmed.Substring(2).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 3) {
                            parts[1] = newPort.ToString(CultureInfo.InvariantCulture);
                            lines[i] = "m=" + string.Join(" ", parts);
                        }
                    }

                    continue;
                }

                if (trimmed.StartsWith("c=", StringComparison.OrdinalIgnoreCase)) {
                    if (inAudio) {
                        lines[i] = BuildConnectionLine(trimmed, newIp);
                        replacedMediaConnection = true;
                    } else if (!replacedSessionConnection) {
                        lines[i] = BuildConnectionLine(trimmed, newIp);
                        replacedSessionConnection = true;
                    }
                }
            }

            if (audioLineIndex.HasValue && !replacedMediaConnection) {
                lines.Insert(audioLineIndex.Value + 1, $"c=IN IP4 {newIp}");
            } else if (!replacedSessionConnection) {
                int sessionIndex = lines.FindIndex(x => x.TrimStart().StartsWith("s=", StringComparison.OrdinalIgnoreCase));
                if (sessionIndex >= 0) {
                    lines.Insert(sessionIndex + 1, $"c=IN IP4 {newIp}");
                } else {
                    lines.Insert(0, $"c=IN IP4 {newIp}");
                }
            }

            return string.Join("\r\n", lines);
        }

        /// <summary>
        /// The SplitLines
        /// </summary>
        /// <param name="sdp">The sdp<see cref="string"/></param>
        /// <returns>The <see cref="IEnumerable{string}"/></returns>
        private static IEnumerable<string> SplitLines(string sdp) {
            return sdp.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }

        /// <summary>
        /// The ParseConnectionAddress
        /// </summary>
        /// <param name="line">The line<see cref="string"/></param>
        /// <returns>The <see cref="string?"/></returns>
        private static string? ParseConnectionAddress(string line) {
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 3 ? parts[2].Trim() : null;
        }

        /// <summary>
        /// The BuildConnectionLine
        /// </summary>
        /// <param name="original">The original<see cref="string"/></param>
        /// <param name="newIp">The newIp<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string BuildConnectionLine(string original, string newIp) {
            string[] parts = original.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3) {
                parts[2] = newIp;
                return string.Join(" ", parts);
            }

            return $"c=IN IP4 {newIp}";
        }
    }
}
