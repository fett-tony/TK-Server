namespace TKServer.GSM {
    using SIPSorcery.Media;
    using SIPSorceryMedia.Abstractions;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO.Ports;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;

    using TKServer.BASE;
    
    /// <summary>
    /// Defines the <see cref="TKGSM_Media" />
    /// </summary>
    internal sealed class TKGSM_Media : IDisposable {
        /// <summary>
        /// Defines the _audioPortName
        /// </summary>
        private readonly string _audioPortName;

        /// <summary>
        /// Defines the _localRtpPort
        /// </summary>
        private int _localRtpPort;

        /// <summary>
        /// Defines the _payloadType
        /// </summary>
        private readonly int _payloadType;

        /// <summary>
        /// Defines the _sipEncodingName
        /// </summary>
        private readonly string _sipEncodingName;

        /// <summary>
        /// Defines the _sipClockRate
        /// </summary>
        private readonly int _sipClockRate;

        /// <summary>
        /// Defines the _sipRtpClockRate
        /// </summary>
        private readonly int _sipRtpClockRate;

        /// <summary>
        /// Defines the _sipPtimeMs
        /// </summary>
        private readonly int _sipPtimeMs;

        /// <summary>
        /// Defines the _sipAudioFormat
        /// </summary>
        private readonly AudioFormat _sipAudioFormat;

        /// <summary>
        /// Defines the _sipDecoder
        /// </summary>
        private readonly AudioEncoder _sipDecoder = new();

        /// <summary>
        /// Defines the _sipEncoder
        /// </summary>
        private readonly AudioEncoder _sipEncoder = new();

        /// <summary>
        /// Defines the _bridgeCodecKey
        /// </summary>
        private readonly string _bridgeCodecKey;

        /// <summary>
        /// Defines the _bridgeSampleRate
        /// </summary>
        private readonly int _bridgeSampleRate;

        /// <summary>
        /// Defines the _bridgePtimeMs
        /// </summary>
        private readonly int _bridgePtimeMs;

        /// <summary>
        /// Defines the _logger
        /// </summary>
        private readonly TKBase_LogManager? _logger;

        /// <summary>
        /// Defines the _sync
        /// </summary>
        private readonly object _sync = new();

        /// <summary>
        /// Defines the _cts
        /// </summary>
        private readonly CancellationTokenSource _cts = new();

        /// <summary>
        /// Defines the _serialReadSync
        /// </summary>
        private readonly object _serialReadSync = new();

        /// <summary>
        /// Defines the _serialWriteSync
        /// </summary>
        private readonly object _serialWriteSync = new();

        /// <summary>
        /// Defines the _audioPort
        /// </summary>
        private SerialPort? _audioPort;

        /// <summary>
        /// Defines the _udp
        /// </summary>
        private UdpClient? _udp;

        /// <summary>
        /// Defines the _remote
        /// </summary>
        private IPEndPoint? _remote;

        /// <summary>
        /// Defines the _txQueue
        /// </summary>
        private BlockingCollection<byte[]>? _txQueue;

        /// <summary>
        /// Defines the _rtpRxThread
        /// </summary>
        private Thread? _rtpRxThread;

        /// <summary>
        /// Defines the _serialTxThread
        /// </summary>
        private Thread? _serialTxThread;

        /// <summary>
        /// Defines the _serialRxThread
        /// </summary>
        private Thread? _serialRxThread;

        /// <summary>
        /// Defines the _running
        /// </summary>
        private volatile bool _running;

        /// <summary>
        /// Defines the _disposed
        /// </summary>
        private volatile bool _disposed;

        /// <summary>
        /// Defines the _seq
        /// </summary>
        private ushort _seq;

        /// <summary>
        /// Defines the _timestamp
        /// </summary>
        private uint _timestamp;

        /// <summary>
        /// Defines the _ssrc
        /// </summary>
        private readonly uint _ssrc = (uint)Random.Shared.NextInt64(1, uint.MaxValue);

        /// <summary>
        /// Gets the LocalRtpPort
        /// </summary>
        public int LocalRtpPort => _localRtpPort;

        /// <summary>
        /// Gets the LocalRtpEndPoint
        /// </summary>
        public IPEndPoint LocalRtpEndPoint => new IPEndPoint(IPAddress.Loopback, _localRtpPort);

        /// <summary>
        /// Initializes a new instance of the <see cref="TKGSM_Media"/> class.
        /// </summary>
        /// <param name="audioPortName">The audioPortName<see cref="string"/></param>
        /// <param name="localRtpPort">The localRtpPort<see cref="int"/></param>
        /// <param name="sipCodec">The sipCodec<see cref="TKBase_AudioCodecConfig"/></param>
        /// <param name="bridgeCodec">The bridgeCodec<see cref="TKBase_AudioCodecConfig"/></param>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        public TKGSM_Media(
            string audioPortName,
            int localRtpPort,
            TKBase_AudioCodecConfig sipCodec,
            TKBase_AudioCodecConfig bridgeCodec,
            TKBase_LogManager? logger) {

            if (!sipCodec.IsGsmSipCodec) {
                throw new InvalidOperationException($"GSM SIP-Codec ist nicht verwendbar: {sipCodec.CodecKey}");
            }
            if (!bridgeCodec.IsGsmBridgeCodec) {
                throw new InvalidOperationException($"GSM Bridge-Codec ist nicht verwendbar: {bridgeCodec.CodecKey}");
            }

            int? sipPtime = sipCodec.PtimeMs ?? sipCodec.FrameDurationMs;
            int? bridgePtime = bridgeCodec.PtimeMs ?? bridgeCodec.FrameDurationMs;
            if (!sipPtime.HasValue || sipPtime.Value <= 0) {
                throw new InvalidOperationException($"GSM SIP-Codec hat keine gueltige Paketzeit: {sipCodec.CodecKey}");
            }
            if (!bridgePtime.HasValue || bridgePtime.Value <= 0) {
                throw new InvalidOperationException($"GSM Bridge-Codec hat keine gueltige Paketzeit: {bridgeCodec.CodecKey}");
            }
            if (!IsSupportedSampleRateRelation(sipCodec.ClockRate, bridgeCodec.ClockRate)) {
                throw new InvalidOperationException($"GSM Bridge Sampling nicht unterstuetzt | SIP={sipCodec.ClockRate} | Bridge={bridgeCodec.ClockRate}");
            }

            if (localRtpPort <= 0 || localRtpPort > IPEndPoint.MaxPort) {
                throw new InvalidOperationException("GSMAudioPort ungueltig: " + localRtpPort);
            }

            _audioPortName = audioPortName ?? throw new ArgumentNullException(nameof(audioPortName));
            _localRtpPort = localRtpPort;
            _payloadType = sipCodec.PayloadTypeOrThrow();
            _sipEncodingName = sipCodec.RtpEncodingName.Trim().ToUpperInvariant();
            _sipClockRate = sipCodec.ClockRate;
            _sipRtpClockRate = sipCodec.RtpClockRate;
            _sipPtimeMs = sipPtime.Value;
            _sipAudioFormat = sipCodec.ToAudioFormat();
            _bridgeCodecKey = bridgeCodec.CodecKey.Trim().ToUpperInvariant();
            _bridgeSampleRate = bridgeCodec.ClockRate;
            _bridgePtimeMs = bridgePtime.Value;
            _logger = logger;
        }

        /// <summary>
        /// The IsSupportedSampleRateRelation
        /// </summary>
        /// <param name="sipClockRate">The sipClockRate<see cref="int"/></param>
        /// <param name="bridgeSampleRate">The bridgeSampleRate<see cref="int"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool IsSupportedSampleRateRelation(int sipClockRate, int bridgeSampleRate) {
            return sipClockRate > 0
                && bridgeSampleRate > 0
                && (sipClockRate == bridgeSampleRate
                    || bridgeSampleRate == sipClockRate * 2
                    || sipClockRate == bridgeSampleRate * 2);
        }

        /// <summary>
        /// The SetRemote
        /// </summary>
        /// <param name="remote">The remote<see cref="IPEndPoint"/></param>
        public void SetRemote(IPEndPoint remote) {
            if (remote == null) throw new ArgumentNullException(nameof(remote));
            lock (_sync) {
                _remote = remote;
            }
        }

        /// <summary>
        /// The Start
        /// </summary>
        public void Start() {
            lock (_sync) {
                if (_running) {
                    return;
                }

                try {
                    _udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, _localRtpPort));
                    if (_udp.Client.LocalEndPoint is IPEndPoint actualLocalEndPoint) {
                        _localRtpPort = actualLocalEndPoint.Port;
                    }
                    _udp.Client.ReceiveTimeout = 200;
                    _udp.Client.SendTimeout = 1000;

                    _audioPort = new SerialPort(_audioPortName, 460800, Parity.None, 8, StopBits.One) {
                        DtrEnable = true,
                        RtsEnable = true,
                        Handshake = Handshake.RequestToSend,
                        ReadTimeout = 20,
                        WriteTimeout = 5000
                    };
                    _audioPort.Open();

                    _txQueue = new BlockingCollection<byte[]>(new ConcurrentQueue<byte[]>(), 64);
                    PrepareForCall();
                    _running = true;

                    _rtpRxThread = new Thread(RtpReceiveLoop) { IsBackground = true, Name = $"GSM-RTP-RX-{_localRtpPort}" };
                    _serialTxThread = new Thread(SerialTxLoop) { IsBackground = true, Name = $"GSM-SERIAL-TX-{_audioPortName}" };
                    _serialRxThread = new Thread(SerialRxLoop) { IsBackground = true, Name = $"GSM-SERIAL-RX-{_audioPortName}" };
                    _rtpRxThread.Start();
                    _serialTxThread.Start();
                    _serialRxThread.Start();

                    _logger?.Info($"GSM AudioBridge gestartet | AudioPort={_audioPortName} | RTP={_localRtpPort} | SipCodec={_sipEncodingName}/{_payloadType} | BridgeCodec={_bridgeCodecKey} | SampleRate={_bridgeSampleRate} | Ptime={_bridgePtimeMs} | Baud=460800 | Handshake=RTS/CTS");
                } catch (Exception ex) {
                    CleanupFailedStart();
                    _logger?.Error($"GSM AudioBridge Start fehlgeschlagen | AudioPort={_audioPortName} | RTP={_localRtpPort} | Fehler={ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// The PrepareForCall
        /// </summary>
        private void CleanupFailedStart() {
            _running = false;

            try { _txQueue?.CompleteAdding(); } catch (Exception ex) { _logger?.Warn($"GSM AudioBridge Start-Cleanup Warnung | Queue | {ex.Message}"); }
            TryJoinThread(_rtpRxThread, "RTP-RX");
            TryJoinThread(_serialTxThread, "Serial-TX");
            TryJoinThread(_serialRxThread, "Serial-RX");
            _rtpRxThread = null;
            _serialTxThread = null;
            _serialRxThread = null;

            try { _audioPort?.DiscardInBuffer(); } catch { }
            try { _audioPort?.DiscardOutBuffer(); } catch { }
            try { _udp?.Dispose(); } catch (Exception ex) { _logger?.Warn($"GSM AudioBridge Start-Cleanup Warnung | UDP | {ex.Message}"); }
            _udp = null;
            try { _audioPort?.Dispose(); } catch (Exception ex) { _logger?.Warn($"GSM AudioBridge Start-Cleanup Warnung | AudioPort={_audioPortName} | {ex.Message}"); }
            _audioPort = null;
            try { _txQueue?.Dispose(); } catch (Exception ex) { _logger?.Warn($"GSM AudioBridge Start-Cleanup Warnung | Queue Dispose | {ex.Message}"); }
            _txQueue = null;
        }

        private void TryJoinThread(Thread? thread, string name) {
            if (thread == null || !thread.IsAlive) {
                return;
            }

            try {
                thread.Join(500);
            } catch (Exception ex) {
                _logger?.Warn($"GSM AudioBridge Thread-Cleanup Warnung | Thread={name} | {ex.Message}");
            }
        }

        public void PrepareForCall() {
            var sp = _audioPort;
            if (sp == null || !sp.IsOpen) return;
            try { sp.DiscardInBuffer(); } catch { }
            try { sp.DiscardOutBuffer(); } catch { }
            NudgeRts();
            int start = Environment.TickCount;
            while (Environment.TickCount - start < 1000) {
                try {
                    if (sp.CtsHolding) return;
                } catch {
                    break;
                }
                Thread.Sleep(25);
            }
        }

        /// <summary>
        /// The NudgeRts
        /// </summary>
        public void NudgeRts() {
            var sp = _audioPort;
            if (sp == null || !sp.IsOpen) return;
            try {
                sp.RtsEnable = false;
                Thread.Sleep(10);
                sp.RtsEnable = true;
            } catch { }
        }

        /// <summary>
        /// The RtpReceiveLoop
        /// </summary>
        private void RtpReceiveLoop() {
            if (_udp == null) return;
            while (_running && !_cts.IsCancellationRequested) {
                try {
                    IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] packet = _udp.Receive(ref remote);
                    if (packet.Length < 12) continue;
                    int version = (packet[0] >> 6) & 0x03;
                    if (version != 2) continue;

                    int csrcCount = packet[0] & 0x0F;
                    bool hasExtension = (packet[0] & 0x10) != 0;
                    bool hasPadding = (packet[0] & 0x20) != 0;
                    int headerLen = 12 + (csrcCount * 4);
                    if (packet.Length < headerLen) continue;
                    if (hasExtension) {
                        if (packet.Length < headerLen + 4) continue;
                        int extLenWords = (packet[headerLen + 2] << 8) | packet[headerLen + 3];
                        headerLen += 4 + (extLenWords * 4);
                        if (packet.Length < headerLen) continue;
                    }

                    int payloadLength = packet.Length - headerLen;
                    if (hasPadding && payloadLength > 0) {
                        int padding = packet[^1];
                        if (padding > 0 && padding <= payloadLength) {
                            payloadLength -= padding;
                        }
                    }
                    if (payloadLength <= 0) continue;

                    byte pt = (byte)(packet[1] & 0x7F);
                    if (pt != _payloadType) continue;

                    byte[] payload = new byte[payloadLength];
                    Buffer.BlockCopy(packet, headerLen, payload, 0, payloadLength);
                    byte[] serialFrame = DecodeSipPayloadToBridge(payload);
                    if (serialFrame.Length > 0) {
                        EnqueueSerial(serialFrame);
                    }
                } catch (SocketException ex) {
                    if (ex.SocketErrorCode != SocketError.TimedOut) {
                        _logger?.Warn($"GSM AudioBridge RTP->Serial Fehler | {ex.Message}");
                        Thread.Sleep(10);
                    }
                } catch (ObjectDisposedException) {
                    break;
                } catch (Exception ex) {
                    _logger?.Warn($"GSM AudioBridge RTP->Serial Fehler | {ex.Message}");
                    Thread.Sleep(10);
                }
            }
        }

        /// <summary>
        /// The SerialTxLoop
        /// </summary>
        private void SerialTxLoop() {
            var sp = _audioPort;
            var q = _txQueue;
            if (sp == null || q == null) return;
            int ctsLogCooldown = 0;
            while (_running && !_cts.IsCancellationRequested && !q.IsCompleted) {
                try {
                    if (!q.TryTake(out byte[]? frame, 50)) continue;
                    if (frame == null || frame.Length == 0) continue;

                    bool ctsLow = false;
                    try { ctsLow = !sp.CtsHolding; } catch { }
                    if (ctsLow) {
                        if (ctsLogCooldown-- <= 0) {
                            _logger?.Warn("GSM AudioBridge TX pausiert | CTS low | Frame verworfen");
                            ctsLogCooldown = 20;
                        }
                        continue;
                    }

                    lock (_serialWriteSync) {
                        if (sp.BytesToWrite > 4096) Thread.Sleep(5);
                        sp.Write(frame, 0, frame.Length);
                    }
                } catch (TimeoutException) {
                    _logger?.Warn("GSM AudioBridge Serial-TX Timeout");
                } catch (ObjectDisposedException) {
                    break;
                } catch (InvalidOperationException) {
                    break;
                } catch (Exception ex) {
                    _logger?.Warn($"GSM AudioBridge Serial-TX Fehler | {ex.Message}");
                }
            }
        }

        /// <summary>
        /// The SerialRxLoop
        /// </summary>
        private void SerialRxLoop() {
            var sp = _audioPort;
            var udp = _udp;
            if (sp == null || udp == null) return;

            int bytesPer20ms = GetBridgeFrameBytes();
            List<byte> buffer = new List<byte>(bytesPer20ms * 4);

            while (_running && !_cts.IsCancellationRequested) {
                try {
                    byte[] readBuffer = new byte[bytesPer20ms];
                    int read;
                    lock (_serialReadSync) {
                        read = sp.Read(readBuffer, 0, readBuffer.Length);
                    }

                    if (read <= 0) {
                        continue;
                    }

                    for (int i = 0; i < read; i++) {
                        buffer.Add(readBuffer[i]);
                    }

                    while (buffer.Count >= bytesPer20ms) {
                        byte[] serialFrame = buffer.GetRange(0, bytesPer20ms).ToArray();
                        buffer.RemoveRange(0, bytesPer20ms);

                        byte[] rtpPayload = EncodeBridgeToSipPayload(serialFrame);
                        if (rtpPayload.Length == 0) {
                            continue;
                        }

                        IPEndPoint? remote;
                        lock (_sync) {
                            remote = _remote;
                        }

                        if (remote == null) {
                            continue;
                        }

                        byte[] rtpPacket = BuildRtpPacket(rtpPayload);
                        udp.Send(rtpPacket, rtpPacket.Length, remote);
                    }
                } catch (TimeoutException) {
                } catch (ObjectDisposedException) {
                    break;
                } catch (InvalidOperationException) {
                    break;
                } catch (Exception ex) {
                    _logger?.Warn($"GSM AudioBridge Serial-RX Fehler | {ex.Message}");
                    Thread.Sleep(10);
                }
            }
        }

        /// <summary>
        /// The EnqueueSerial
        /// </summary>
        /// <param name="payload">The payload<see cref="byte[]"/></param>
        private void EnqueueSerial(byte[] payload) {
            var q = _txQueue;
            if (q == null || q.IsAddingCompleted) return;
            if (!q.TryAdd(payload)) {
                if (q.TryTake(out _)) {
                    q.TryAdd(payload);
                } else {
                    _logger?.Warn("GSM AudioBridge Serial-TX Queue voll | Frame verworfen");
                }
            }
        }

        /// <summary>
        /// The BuildRtpPacket
        /// </summary>
        /// <param name="payload">The payload<see cref="byte[]"/></param>
        /// <returns>The <see cref="byte[]"/></returns>
        private byte[] BuildRtpPacket(byte[] payload) {
            byte[] packet = new byte[12 + payload.Length];
            packet[0] = 0x80;
            packet[1] = (byte)_payloadType;
            packet[2] = (byte)(_seq >> 8);
            packet[3] = (byte)(_seq & 0xFF);
            _seq++;
            packet[4] = (byte)(_timestamp >> 24);
            packet[5] = (byte)(_timestamp >> 16);
            packet[6] = (byte)(_timestamp >> 8);
            packet[7] = (byte)(_timestamp & 0xFF);
            _timestamp += (uint)(_sipRtpClockRate * _sipPtimeMs / 1000);
            packet[8] = (byte)(_ssrc >> 24);
            packet[9] = (byte)(_ssrc >> 16);
            packet[10] = (byte)(_ssrc >> 8);
            packet[11] = (byte)(_ssrc & 0xFF);
            Buffer.BlockCopy(payload, 0, packet, 12, payload.Length);
            return packet;
        }

        /// <summary>
        /// The GetBridgeFrameBytes
        /// </summary>
        /// <returns>The <see cref="int"/></returns>
        private int GetBridgeFrameBytes() {
            int samples = (_bridgeSampleRate * _bridgePtimeMs) / 1000;
            if (samples <= 0) {
                throw new InvalidOperationException($"GSM Bridge Framegroesse ungueltig | Codec={_bridgeCodecKey} | SampleRate={_bridgeSampleRate} | Ptime={_bridgePtimeMs}");
            }
            return samples * 2;
        }

        /// <summary>
        /// The DecodeSipPayloadToBridge
        /// </summary>
        /// <param name="payload">The payload<see cref="byte[]"/></param>
        /// <returns>The <see cref="byte[]"/></returns>
        private byte[] DecodeSipPayloadToBridge(byte[] payload) {
            short[] sipPcm = _sipDecoder.DecodeAudio(payload, _sipAudioFormat);
            short[] bridgePcm = ConvertSipSamplesToBridgeSamples(sipPcm);
            return ShortsToLittleEndianBytes(bridgePcm);
        }

        /// <summary>
        /// The EncodeBridgeToSipPayload
        /// </summary>
        /// <param name="serialFrame">The serialFrame<see cref="byte[]"/></param>
        /// <returns>The <see cref="byte[]"/></returns>
        private byte[] EncodeBridgeToSipPayload(byte[] serialFrame) {
            short[] bridgePcm = LittleEndianBytesToShorts(serialFrame);
            if (bridgePcm.Length == 0) {
                return Array.Empty<byte>();
            }

            short[] sipPcm = ConvertBridgeSamplesToSipSamples(bridgePcm);
            return _sipEncoder.EncodeAudio(sipPcm, _sipAudioFormat);
        }

        /// <summary>
        /// The ConvertSipSamplesToBridgeSamples
        /// </summary>
        /// <param name="source">The source<see cref="short[]"/></param>
        /// <returns>The <see cref="short[]"/></returns>
        private short[] ConvertSipSamplesToBridgeSamples(short[] source) {
            if (_bridgeSampleRate == _sipClockRate) return source;
            if (_bridgeSampleRate == _sipClockRate * 2) return Upsample8kTo16k(source);
            if (_sipClockRate == _bridgeSampleRate * 2) return Downsample16kTo8k(source);
            throw new InvalidOperationException($"GSM Sampling-Konvertierung nicht unterstuetzt | SIP={_sipClockRate} | Bridge={_bridgeSampleRate}");
        }

        /// <summary>
        /// The ConvertBridgeSamplesToSipSamples
        /// </summary>
        /// <param name="source">The source<see cref="short[]"/></param>
        /// <returns>The <see cref="short[]"/></returns>
        private short[] ConvertBridgeSamplesToSipSamples(short[] source) {
            if (_bridgeSampleRate == _sipClockRate) return source;
            if (_bridgeSampleRate == _sipClockRate * 2) return Downsample16kTo8k(source);
            if (_sipClockRate == _bridgeSampleRate * 2) return Upsample8kTo16k(source);
            throw new InvalidOperationException($"GSM Sampling-Konvertierung nicht unterstuetzt | SIP={_sipClockRate} | Bridge={_bridgeSampleRate}");
        }

        /// <summary>
        /// The ShortsToLittleEndianBytes
        /// </summary>
        /// <param name="samples">The samples<see cref="short[]"/></param>
        /// <returns>The <see cref="byte[]"/></returns>
        private static byte[] ShortsToLittleEndianBytes(short[] samples) {
            byte[] bytes = new byte[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++) {
                short sample = samples[i];
                bytes[(i * 2)] = (byte)(sample & 0xFF);
                bytes[(i * 2) + 1] = (byte)((sample >> 8) & 0xFF);
            }
            return bytes;
        }

        /// <summary>
        /// The LittleEndianBytesToShorts
        /// </summary>
        /// <param name="data">The data<see cref="byte[]"/></param>
        /// <returns>The <see cref="short[]"/></returns>
        private static short[] LittleEndianBytesToShorts(byte[] data) {
            if (data.Length < 2) {
                return Array.Empty<short>();
            }
            int count = data.Length / 2;
            short[] samples = new short[count];
            for (int i = 0; i < count; i++) {
                samples[i] = (short)(data[i * 2] | (data[(i * 2) + 1] << 8));
            }
            return samples;
        }

        /// <summary>
        /// The Upsample8kTo16k
        /// </summary>
        /// <param name="source">The source<see cref="short[]"/></param>
        /// <returns>The <see cref="short[]"/></returns>
        private static short[] Upsample8kTo16k(short[] source) {
            short[] up = new short[source.Length * 2];
            for (int i = 0; i < source.Length; i++) {
                short current = source[i];
                short next = i + 1 < source.Length ? source[i + 1] : current;
                up[i * 2] = current;
                up[(i * 2) + 1] = (short)((current + next) / 2);
            }
            return up;
        }

        /// <summary>
        /// The Downsample16kTo8k
        /// </summary>
        /// <param name="source">The source<see cref="short[]"/></param>
        /// <returns>The <see cref="short[]"/></returns>
        private static short[] Downsample16kTo8k(short[] source) {
            int count = source.Length / 2;
            if (count <= 0) {
                return Array.Empty<short>();
            }
            short[] down = new short[count];
            for (int i = 0; i < count; i++) {
                int a = source[i * 2];
                int b = source[(i * 2) + 1];
                down[i] = (short)((a + b) / 2);
            }
            return down;
        }

        /// <summary>
        /// The Stop
        /// </summary>
        public void Stop() {
            lock (_sync) {
                if (!_running) {
                    try { _audioPort?.DiscardInBuffer(); } catch { }
                    try { _audioPort?.DiscardOutBuffer(); } catch { }
                    return;
                }
                _running = false;
                _cts.Cancel();
                try { _txQueue?.CompleteAdding(); } catch { }
                try { _rtpRxThread?.Join(500); } catch { }
                try { _serialTxThread?.Join(500); } catch { }
                try { _serialRxThread?.Join(500); } catch { }
                try { _audioPort?.DiscardInBuffer(); } catch { }
                try { _audioPort?.DiscardOutBuffer(); } catch { }
                try { _udp?.Dispose(); } catch { }
                _udp = null;
                try { _audioPort?.Dispose(); } catch { }
                _audioPort = null;
                try { _txQueue?.Dispose(); } catch { }
                _txQueue = null;
                _logger?.Info($"GSM AudioBridge gestoppt | AudioPort={_audioPortName} | RTP={_localRtpPort}");
            }
        }

        /// <summary>
        /// The Dispose
        /// </summary>
        public void Dispose() {
            if (_disposed) return;
            _disposed = true;
            Stop();
            _cts.Dispose();
        }
    }
}
