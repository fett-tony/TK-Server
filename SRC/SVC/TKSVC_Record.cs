namespace TKServer.SVC {
    using SIPSorcery.Media;
    using SIPSorceryMedia.Abstractions;
    using NAudio.Wave;
    using System;
    using System.Data;
    using System.IO;
    using System.Text.Json;
    using System.Threading;

    using TKServer.BASE;
    using TKServer.SQL;

    /// <summary>
    /// Central SVC recording logic for mailbox and call-recording Leg-D.
    /// </summary>
    internal sealed class TKSVC_Record {
        private const int DefaultSampleRate = 8000;
        private const int DefaultChannels = 1;
        private const int DefaultBitsPerSample = 16;

        private readonly TKBase_ServerSettings _settings;

        private readonly TKBase_LogManager? _logger;

        public TKSVC_Record(TKBase_ServerSettings settings, TKBase_LogManager? logger = null) {
            _settings = settings;
            _logger = logger;
        }

        public TKSVC_RecordRuntime StartMailboxRecording(SIP_ServerSession session, TKSVC_Session serviceLeg) {
            return StartRecording(session, serviceLeg, TKSVC_RecordMode.MailboxPartner);
        }

        public TKSVC_RecordRuntime StartCallRecording(SIP_ServerSession session, TKSVC_Session serviceLeg) {
            return StartRecording(session, serviceLeg, TKSVC_RecordMode.CallListener);
        }

        private TKSVC_RecordRuntime StartRecording(SIP_ServerSession session, TKSVC_Session serviceLeg, TKSVC_RecordMode mode) {
            ArgumentNullException.ThrowIfNull(session);
            ArgumentNullException.ThrowIfNull(serviceLeg);

            TKSVC_RecordOptions options = TKSVC_RecordOptions.FromServiceLeg(serviceLeg, _logger);
            RecordingOwner owner = ResolveRecordingOwner(session);
            string path = BuildRecordingPath(session, serviceLeg, mode, options.RecordTemplate, owner);
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) {
                Directory.CreateDirectory(directory);
            }

            WaveFormat format = new WaveFormat(options.SampleRate, options.BitsPerSample, options.Channels);
            WaveFileWriter writer = new WaveFileWriter(path, format);

            _logger?.Info(
                $"SVC Recording gestartet | SessionID={session.SessionId} | LegD={session.LegD.CallId ?? "-"} | Service={serviceLeg.InternalNumber} | Mode={mode} | UserID={(owner.UserId.HasValue ? owner.UserId.Value.ToString() : "-")} | UserOrdner={owner.UserFolderName ?? "-"} | Datei={path}");

            return new TKSVC_RecordRuntime(
                session.SessionId,
                serviceLeg.InternalNumber,
                serviceLeg.ServiceCode,
                serviceLeg.ServiceType,
                mode,
                path,
                writer,
                owner.UserId,
                options.SampleRate,
                options.Channels,
                options.BitsPerSample,
                options.MinDurationMs,
                _logger);
        }

        private string BuildRecordingPath(SIP_ServerSession session, TKSVC_Session serviceLeg, TKSVC_RecordMode mode, string? recordTemplate, RecordingOwner owner) {
            string template = string.IsNullOrWhiteSpace(recordTemplate)
                ? "Recordings\\record_{sessionid}_{timestamp}.wav"
                : recordTemplate.Trim();

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string service = SanitizeFileToken(serviceLeg.ServiceCode ?? serviceLeg.InternalNumber ?? "service");
            string username = SanitizeFileToken(owner.UserFolderName ?? "unknown");
            string userId = owner.UserId.HasValue ? owner.UserId.Value.ToString() : "0";
            string file = template
                .Replace("{sessionid}", SanitizeFileToken(session.SessionId), StringComparison.OrdinalIgnoreCase)
                .Replace("{timestamp}", timestamp, StringComparison.OrdinalIgnoreCase)
                .Replace("{service}", service, StringComparison.OrdinalIgnoreCase)
                .Replace("{mode}", mode.ToString(), StringComparison.OrdinalIgnoreCase)
                .Replace("{username}", username, StringComparison.OrdinalIgnoreCase)
                .Replace("{userid}", userId, StringComparison.OrdinalIgnoreCase);

            if (Path.IsPathRooted(file)) {
                return file;
            }

            string ownerRoot = owner.HasUserFolder && !string.IsNullOrWhiteSpace(owner.UserFolderPath)
                ? owner.UserFolderPath!
                : _settings.ResolveSystemMediaFolderPath(AppContext.BaseDirectory);

            return Path.GetFullPath(Path.Combine(ownerRoot, file));
        }

        private RecordingOwner ResolveRecordingOwner(SIP_ServerSession session) {
            try {
                TKSQL_Commands sql = new TKSQL_Commands(_logger);

                if (session.TargetUserId.HasValue) {
                    DataRow? byId = sql.GetSipUserById(session.TargetUserId.Value);
                    if (TryBuildOwner(byId, out RecordingOwner ownerById)) {
                        return ownerById;
                    }
                }

                string[] candidates = new[] {
                    session.FromNumber,
                    session.FromUsername ?? string.Empty,
                    session.ToUsername ?? string.Empty,
                    session.ToNumber
                };

                foreach (string candidate in candidates) {
                    string value = candidate?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(value)) {
                        continue;
                    }

                    DataRow? byNumber = sql.GetSipUserByNumber(value);
                    if (TryBuildOwner(byNumber, out RecordingOwner ownerByNumber)) {
                        return ownerByNumber;
                    }

                    DataRow? byUsername = sql.GetSipUserByUsername(value);
                    if (TryBuildOwner(byUsername, out RecordingOwner ownerByUsername)) {
                        return ownerByUsername;
                    }
                }
            } catch (Exception ex) {
                _logger?.Warn($"SVC Recording User-Aufloesung fehlgeschlagen | SessionID={session.SessionId} | Fehler={ex.Message}");
            }

            return RecordingOwner.SystemFallback();
        }

        private bool TryBuildOwner(DataRow? row, out RecordingOwner owner) {
            owner = RecordingOwner.SystemFallback();
            if (row == null) {
                return false;
            }

            int userId = row.Table.Columns.Contains("ID") && row["ID"] != DBNull.Value
                ? Convert.ToInt32(row["ID"])
                : 0;
            string username = row.Table.Columns.Contains("Username") && row["Username"] != DBNull.Value
                ? Convert.ToString(row["Username"])?.Trim() ?? string.Empty
                : string.Empty;

            if (userId <= 0 || string.IsNullOrWhiteSpace(username)) {
                return false;
            }

            string? userFolderPath = _settings.ResolveUserMediaFolderPath(row, AppContext.BaseDirectory);
            if (string.IsNullOrWhiteSpace(userFolderPath)) {
                return false;
            }

            owner = new RecordingOwner(userId, SanitizeFileToken(username), userFolderPath);
            return owner.HasUserFolder;
        }

        private static string SanitizeFileToken(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return "empty";
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            char[] chars = value.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++) {
                if (Array.IndexOf(invalid, chars[i]) >= 0 || chars[i] == Path.DirectorySeparatorChar || chars[i] == Path.AltDirectorySeparatorChar) {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }

        internal sealed class TKSVC_RecordRuntime : IDisposable {
            private readonly WaveFileWriter _writer;
            private readonly TKBase_LogManager? _logger;
            private readonly int? _userId;
            private readonly int _sampleRateHz;
            private readonly int _channels;
            private readonly int _bitsPerSample;
            private readonly int _minDurationMs;
            private int _disposed;
            private int _writeFailureLogged;

            public TKSVC_RecordRuntime(
                string sessionId,
                string? internalNumber,
                string? serviceCode,
                string? serviceType,
                TKSVC_RecordMode mode,
                string filePath,
                WaveFileWriter writer,
                int? userId,
                int sampleRateHz,
                int channels,
                int bitsPerSample,
                int minDurationMs,
                TKBase_LogManager? logger) {
                SessionId = sessionId;
                InternalNumber = internalNumber;
                ServiceCode = serviceCode;
                ServiceType = serviceType;
                Mode = mode;
                FilePath = filePath;
                _writer = writer;
                _userId = userId;
                _sampleRateHz = sampleRateHz;
                _channels = channels;
                _bitsPerSample = bitsPerSample;
                _minDurationMs = Math.Max(minDurationMs, 0);
                _logger = logger;
            }

            public string SessionId { get; }

            public string? InternalNumber { get; }

            public string? ServiceCode { get; }

            public string? ServiceType { get; }

            public TKSVC_RecordMode Mode { get; }

            public string FilePath { get; }

            public long BytesWritten { get; private set; }

            public int? DurationMs => CalculateDurationMs();

            public bool HasAnyAudio => BytesWritten > 0;

            public bool HasUsableAudio {
                get {
                    int? durationMs = DurationMs;
                    return BytesWritten > 0 && (!durationMs.HasValue || durationMs.Value >= _minDurationMs);
                }
            }

            public void WritePcmFrame(byte[] pcmFrame) {
                if (pcmFrame == null || pcmFrame.Length == 0 || Volatile.Read(ref _disposed) == 1) {
                    return;
                }

                try {
                    lock (_writer) {
                        if (Volatile.Read(ref _disposed) == 1) {
                            return;
                        }

                        _writer.Write(pcmFrame, 0, pcmFrame.Length);
                        BytesWritten += pcmFrame.Length;
                    }
                } catch (Exception ex) {
                    LogWriteFailureOnce("PCM-Write", ex);
                }
            }

            public void WriteEncodedAudioFrame(EncodedAudioFrame frame, AudioEncoder audioEncoder) {
                if (frame == null || frame.EncodedAudio == null || frame.EncodedAudio.Length == 0 || audioEncoder == null || Volatile.Read(ref _disposed) == 1) {
                    return;
                }

                try {
                    short[] samples = audioEncoder.DecodeAudio(frame.EncodedAudio, frame.AudioFormat);
                    if (samples == null || samples.Length == 0) {
                        return;
                    }

                    byte[] pcmFrame = new byte[samples.Length * sizeof(short)];
                    Buffer.BlockCopy(samples, 0, pcmFrame, 0, pcmFrame.Length);
                    WritePcmFrame(pcmFrame);
                } catch (Exception ex) {
                    LogWriteFailureOnce("EncodedAudioFrame", ex);
                }
            }

            private void LogWriteFailureOnce(string stage, Exception ex) {
                if (Interlocked.Exchange(ref _writeFailureLogged, 1) == 1) {
                    return;
                }

                _logger?.Warn($"SVC Recording Schreibfehler | SessionID={SessionId} | Stage={stage} | Fehler={ex.Message}");
            }

            public void Dispose() {
                if (Interlocked.Exchange(ref _disposed, 1) == 1) {
                    return;
                }

                try {
                    lock (_writer) {
                        _writer.Dispose();
                    }
                } catch (Exception ex) {
                    _logger?.Warn($"SVC Recording Dispose Warnung | SessionID={SessionId} | {ex.Message}");
                }

                try {
                    TryStoreMailboxGreetingBlob();
                } catch (Exception ex) {
                    _logger?.Error($"SVC Recording Finalisierung fehlgeschlagen | SessionID={SessionId} | Datei={FilePath} | Fehler={ex.Message}");
                }

                _logger?.Info(
                    $"SVC Recording beendet | SessionID={SessionId} | Service={InternalNumber ?? "-"} | Mode={Mode} | Bytes={BytesWritten} | Datei={FilePath}");
            }

            private void TryStoreMailboxGreetingBlob() {
                if (!IsMailboxGreeting()) {
                    return;
                }

                if (!_userId.HasValue || _userId.Value <= 0) {
                    _logger?.Warn($"Mailboxansage nicht in SQL gespeichert | SessionID={SessionId} | Grund=UserID fehlt | Datei={FilePath}");
                    return;
                }

                if (!File.Exists(FilePath)) {
                    _logger?.Warn($"Mailboxansage nicht in SQL gespeichert | SessionID={SessionId} | Grund=Datei fehlt | Datei={FilePath}");
                    return;
                }

                int? durationMs = CalculateDurationMs();
                if (BytesWritten <= 0) {
                    _logger?.Warn($"Mailboxansage nicht in SQL gespeichert | SessionID={SessionId} | Grund=keine Audiodaten empfangen | Bytes={BytesWritten} | DauerMs={durationMs?.ToString() ?? "-"} | Datei={FilePath}");
                    return;
                }

                if (durationMs.HasValue && durationMs.Value < _minDurationMs) {
                    _logger?.Warn($"Mailboxansage nicht in SQL gespeichert | SessionID={SessionId} | Grund=Aufnahme zu kurz | Bytes={BytesWritten} | DauerMs={durationMs.Value} | MindestdauerMs={_minDurationMs} | Datei={FilePath}");
                    return;
                }

                try {
                    byte[] audioBlob = File.ReadAllBytes(FilePath);
                    if (audioBlob.Length <= 44) {
                        _logger?.Warn($"Mailboxansage nicht in SQL gespeichert | SessionID={SessionId} | Grund=nur WAV-Header/keine Nutzdaten | BlobBytes={audioBlob.Length} | Datei={FilePath}");
                        return;
                    }

                    string escapedPath = FilePath.Replace("\\", "\\\\").Replace("\"", "\\\"");
                    string escapedSessionId = SessionId.Replace("\"", "\\\"");
                    string jsonConfig = $"{{\"Source\":\"SVC\",\"SessionID\":\"{escapedSessionId}\",\"FilePath\":\"{escapedPath}\"}}";
                    int affected = new TKSQL_Commands(_logger).UpsertMailboxGreetingBlob(
                        _userId.Value,
                        audioBlob,
                        durationMs,
                        _sampleRateHz,
                        _channels,
                        jsonConfig);

                    _logger?.Info($"Mailboxansage in SQL gespeichert | SessionID={SessionId} | UserID={_userId.Value} | Bytes={audioBlob.Length} | Rows={affected}");
                } catch (Exception ex) {
                    _logger?.Error($"Mailboxansage SQL-Speicherung fehlgeschlagen | SessionID={SessionId} | UserID={_userId.Value} | Datei={FilePath} | Fehler={ex.Message}");
                }
            }

            private bool IsMailboxGreeting() {
                return string.Equals(SIP_ServiceCodes.Normalize(ServiceCode), SIP_ServiceCodes.VoicemailGreetingRecord, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(TKSVC_ServiceTypes.Normalize(ServiceType), TKSVC_ServiceTypes.VoicemailGreeting, StringComparison.OrdinalIgnoreCase);
            }

            private int? CalculateDurationMs() {
                long bytesPerSecond = (long)_sampleRateHz * _channels * (_bitsPerSample / 8);
                if (bytesPerSecond <= 0 || BytesWritten <= 0) {
                    return null;
                }

                long duration = BytesWritten * 1000L / bytesPerSecond;
                if (duration <= 0 || duration > int.MaxValue) {
                    return null;
                }

                return (int)duration;
            }
        }

        private sealed class TKSVC_RecordOptions {
            public string? RecordTemplate { get; private set; }

            public int SampleRate { get; private set; } = DefaultSampleRate;

            public int Channels { get; private set; } = DefaultChannels;

            public int BitsPerSample { get; private set; } = DefaultBitsPerSample;

            public int MinDurationMs { get; private set; } = 1000;

            public static TKSVC_RecordOptions FromServiceLeg(TKSVC_Session serviceLeg, TKBase_LogManager? logger) {
                TKSVC_RecordOptions options = new TKSVC_RecordOptions {
                    RecordTemplate = serviceLeg.RecordTemplate
                };

                if (string.IsNullOrWhiteSpace(serviceLeg.JsonConfig)) {
                    return options;
                }

                try {
                    using JsonDocument doc = JsonDocument.Parse(serviceLeg.JsonConfig);
                    JsonElement root = doc.RootElement;
                    options.SampleRate = ReadInt(root, "sampleRate") ?? options.SampleRate;
                    options.Channels = ReadInt(root, "channels") ?? options.Channels;
                    options.BitsPerSample = ReadInt(root, "bitsPerSample") ?? options.BitsPerSample;
                    options.MinDurationMs = ReadInt(root, "minDurationMs") ?? ReadInt(root, "minimumDurationMs") ?? options.MinDurationMs;
                } catch (Exception ex) {
                    logger?.Warn($"SVC Recording JsonConfig konnte nicht gelesen werden | InternalNumber={serviceLeg.InternalNumber} | {ex.Message}");
                }

                return options;
            }

            private static int? ReadInt(JsonElement root, string name) {
                if (!root.TryGetProperty(name, out JsonElement value)
                    && !root.TryGetProperty(char.ToUpperInvariant(name[0]) + name[1..], out value)) {
                    return null;
                }

                return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int result)
                    ? result
                    : null;
            }
        }

        private sealed class RecordingOwner {
            public RecordingOwner(int? userId, string? userFolderName, string? userFolderPath) {
                UserId = userId;
                UserFolderName = string.IsNullOrWhiteSpace(userFolderName) ? null : userFolderName.Trim();
                UserFolderPath = string.IsNullOrWhiteSpace(userFolderPath) ? null : userFolderPath.Trim();
            }

            public int? UserId { get; }

            public string? UserFolderName { get; }

            public string? UserFolderPath { get; }

            public bool HasUserFolder => UserId.HasValue && UserId.Value > 0 && !string.IsNullOrWhiteSpace(UserFolderName) && !string.IsNullOrWhiteSpace(UserFolderPath);

            public static RecordingOwner SystemFallback() => new RecordingOwner(null, null, null);
        }
    }

    internal enum TKSVC_RecordMode {
        MailboxPartner,
        CallListener
    }
}
