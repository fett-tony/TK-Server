namespace TKServer.BASE {
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Data;
    using System.IO.Ports;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    using TKServer.GSM;
    using TKServer.SipExt;
    using TKServer.SQL;

    /// <summary>
    /// Defines the <see cref="TKBase_GsmGateway" />
    /// </summary>
    internal static class TKBase_GsmGateway {
        /// <summary>
        /// Defines the <see cref="DashboardRow" />
        /// </summary>
        internal sealed class DashboardRow {
            /// <summary>
            /// Gets the StatusId
            /// </summary>
            public int StatusId { get; init; }

            /// <summary>
            /// Gets the Telefonnummer
            /// </summary>
            public string Telefonnummer { get; init; } = "-";

            /// <summary>
            /// Gets the IMEI
            /// </summary>
            public string IMEI { get; init; } = "-";

            /// <summary>
            /// Gets a value indicating whether IsServiceStarted
            /// </summary>
            public bool IsServiceStarted { get; init; }
        }

        /// <summary>
        /// Defines the <see cref="GsmCallStateChangedEventArgs" />
        /// </summary>
        internal sealed class GsmCallStateChangedEventArgs {
            /// <summary>
            /// Gets the SessionId
            /// </summary>
            public string SessionId { get; init; } = string.Empty;

            /// <summary>
            /// Gets the StatusId
            /// </summary>
            public int StatusId { get; init; }

            /// <summary>
            /// Gets the State
            /// </summary>
            public string State { get; init; } = string.Empty;

            /// <summary>
            /// Gets the RawLine
            /// </summary>
            public string RawLine { get; init; } = string.Empty;

            /// <summary>
            /// Gets the Direction
            /// </summary>
            public string Direction { get; init; } = string.Empty;

            /// <summary>
            /// Gets the FromNumber
            /// </summary>
            public string FromNumber { get; init; } = string.Empty;

            /// <summary>
            /// Gets the ToNumber
            /// </summary>
            public string ToNumber { get; init; } = string.Empty;

            /// <summary>
            /// Gets the ModemId
            /// </summary>
            public int? ModemId { get; init; }

            /// <summary>
            /// Gets the SimId
            /// </summary>
            public int? SimId { get; init; }
        }

        /// <summary>
        /// Defines the GsmAtSignalKind
        /// </summary>
        internal enum GsmAtSignalKind {
            /// <summary>
            /// Defines the Ignored
            /// </summary>
            Ignored,

            /// <summary>
            /// Defines the Ringing
            /// </summary>
            Ringing,

            /// <summary>
            /// Defines the Clip
            /// </summary>
            Clip,

            /// <summary>
            /// Defines the Answered
            /// </summary>
            Answered,

            /// <summary>
            /// Defines the Busy
            /// </summary>
            Busy,

            /// <summary>
            /// Defines the NoAnswer
            /// </summary>
            NoAnswer,

            /// <summary>
            /// Defines the NoDialTone
            /// </summary>
            NoDialTone,

            /// <summary>
            /// Defines the NoCarrier
            /// </summary>
            NoCarrier,

            /// <summary>
            /// Defines the Ended
            /// </summary>
            Ended,

            /// <summary>
            /// Defines the Waiting
            /// </summary>
            Waiting,

            /// <summary>
            /// Defines the CurrentCallState
            /// </summary>
            CurrentCallState,

            /// <summary>
            /// Defines the Colp
            /// </summary>
            Colp,

            /// <summary>
            /// Defines the ExtendedError
            /// </summary>
            ExtendedError,

            /// <summary>
            /// Defines the Error
            /// </summary>
            Error,

            /// <summary>
            /// Defines the Unknown
            /// </summary>
            Unknown
        }

        /// <summary>
        /// Defines the <see cref="GsmAtSignal" />
        /// </summary>
        internal sealed class GsmAtSignal {
            /// <summary>
            /// Gets the Kind
            /// </summary>
            public GsmAtSignalKind Kind { get; init; } = GsmAtSignalKind.Ignored;

            /// <summary>
            /// Gets the RawLine
            /// </summary>
            public string RawLine { get; init; } = string.Empty;

            /// <summary>
            /// Gets the NormalizedLine
            /// </summary>
            public string NormalizedLine { get; init; } = string.Empty;

            /// <summary>
            /// Gets the CoreState
            /// </summary>
            public string CoreState { get; init; } = string.Empty;

            /// <summary>
            /// Gets the Number
            /// </summary>
            public string Number { get; init; } = string.Empty;

            /// <summary>
            /// Gets the CauseText
            /// </summary>
            public string CauseText { get; init; } = string.Empty;

            /// <summary>
            /// Gets the CallType
            /// </summary>
            public string CallType { get; init; } = string.Empty;

            /// <summary>
            /// Gets the DurationSeconds
            /// </summary>
            public int? DurationSeconds { get; init; }

            /// <summary>
            /// Gets the ClccIndex
            /// </summary>
            public int? ClccIndex { get; init; }

            /// <summary>
            /// Gets the ClccDirection
            /// </summary>
            public int? ClccDirection { get; init; }

            /// <summary>
            /// Gets the ClccStatus
            /// </summary>
            public int? ClccStatus { get; init; }

            /// <summary>
            /// Gets a value indicating whether IsFinal
            /// </summary>
            public bool IsFinal =>
                Kind == GsmAtSignalKind.Error ||
                Kind == GsmAtSignalKind.Busy ||
                Kind == GsmAtSignalKind.NoAnswer ||
                Kind == GsmAtSignalKind.NoDialTone ||
                Kind == GsmAtSignalKind.NoCarrier ||
                Kind == GsmAtSignalKind.Ended;
        }

        /// <summary>
        /// Defines the <see cref="RuntimeDeviceInfo" />
        /// </summary>
        private sealed class RuntimeDeviceInfo {
            /// <summary>
            /// Gets the StatusId
            /// </summary>
            public int StatusId { get; init; }

            /// <summary>
            /// Gets the ModemId
            /// </summary>
            public int? ModemId { get; init; }

            /// <summary>
            /// Gets the SimId
            /// </summary>
            public int? SimId { get; init; }

            /// <summary>
            /// Gets the Telefonnummer
            /// </summary>
            public string Telefonnummer { get; init; } = string.Empty;

            /// <summary>
            /// Gets the IMEI
            /// </summary>
            public string IMEI { get; init; } = string.Empty;

            /// <summary>
            /// Gets the ProviderName
            /// </summary>
            public string ProviderName { get; init; } = string.Empty;

            /// <summary>
            /// Gets the ComPortAT
            /// </summary>
            public string ComPortAT { get; init; } = string.Empty;

            /// <summary>
            /// Gets the ComPortAudio
            /// </summary>
            public string ComPortAudio { get; init; } = string.Empty;

            /// <summary>
            /// Gets the ComPortNMEA
            /// </summary>
            public string ComPortNMEA { get; init; } = string.Empty;
        }

        /// <summary>
        /// Defines the <see cref="ModemRuntimeMonitor" />
        /// </summary>
        internal sealed class ModemRuntimeMonitor {
            /// <summary>
            /// Gets the StatusId
            /// </summary>
            public int StatusId { get; init; }

            /// <summary>
            /// Gets the ModemId
            /// </summary>
            public int? ModemId { get; init; }

            /// <summary>
            /// Gets the SimId
            /// </summary>
            public int? SimId { get; init; }

            /// <summary>
            /// Gets the Telefonnummer
            /// </summary>
            public string Telefonnummer { get; init; } = string.Empty;

            /// <summary>
            /// Gets the IMEI
            /// </summary>
            public string IMEI { get; init; } = string.Empty;

            /// <summary>
            /// Gets the ProviderName
            /// </summary>
            public string ProviderName { get; init; } = string.Empty;

            /// <summary>
            /// Gets the ComPortAT
            /// </summary>
            public string ComPortAT { get; init; } = string.Empty;

            /// <summary>
            /// Gets the ComPortAudio
            /// </summary>
            public string ComPortAudio { get; init; } = string.Empty;

            /// <summary>
            /// Gets the ComPortNMEA
            /// </summary>
            public string ComPortNMEA { get; init; } = string.Empty;

            /// <summary>
            /// Gets or sets a value indicating whether AudioRouteActive
            /// </summary>
            public bool AudioRouteActive { get; set; }

            /// <summary>
            /// Gets or sets the Cancellation
            /// </summary>
            public CancellationTokenSource? Cancellation { get; set; }

            /// <summary>
            /// Gets or sets the WorkerTask
            /// </summary>
            public Task? WorkerTask { get; set; }

            /// <summary>
            /// Gets or sets the PendingCallerNumber
            /// </summary>
            public string PendingCallerNumber { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the PendingConnectedNumber
            /// </summary>
            public string PendingConnectedNumber { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the PendingExtendedCause
            /// </summary>
            public string PendingExtendedCause { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the PendingLastClccState
            /// </summary>
            public string PendingLastClccState { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the ActiveSessionId
            /// </summary>
            public string ActiveSessionId { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets a value indicating whether ClipEnabled
            /// </summary>
            public bool ClipEnabled { get; set; }

            /// <summary>
            /// Gets or sets the Port
            /// </summary>
            public SerialPort? Port { get; set; }

            /// <summary>
            /// Gets the PortSync
            /// </summary>
            public object PortSync { get; } = new object();
        }

        /// <summary>
        /// Defines the _sync
        /// </summary>
        private static readonly object _sync = new();

        /// <summary>
        /// Defines the _defaultInterval
        /// </summary>
        private static readonly TimeSpan _defaultInterval = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Defines the _runtimeMonitors
        /// </summary>
        internal static readonly ConcurrentDictionary<int, ModemRuntimeMonitor> _runtimeMonitors = new();

        /// <summary>
        /// Defines the _sessionManager
        /// </summary>
        internal static TKBase_SessionManager? _sessionManager;

        /// <summary>
        /// Defines the _logger
        /// </summary>
        internal static TKBase_LogManager? _logger;

        /// <summary>
        /// Defines the _timer
        /// </summary>
        private static System.Threading.Timer? _timer;

        /// <summary>
        /// Defines the _runCts
        /// </summary>
        private static CancellationTokenSource? _runCts;

        /// <summary>
        /// Defines the _isRunning
        /// </summary>
        private static bool _isRunning;

        /// <summary>
        /// Defines the _isBusy
        /// </summary>
        private static bool _isBusy;

        /// <summary>
        /// Defines the _lastRunAt
        /// </summary>
        private static DateTime? _lastRunAt;

        /// <summary>
        /// Defines the _lastError
        /// </summary>
        private static string? _lastError;

        /// <summary>
        /// Defines the RunningStateChanged
        /// </summary>
        public static event Action<bool, string>? RunningStateChanged;

        /// <summary>
        /// Defines the CallStateChanged
        /// </summary>
        public static event Action<GsmCallStateChangedEventArgs>? CallStateChanged;

        /// <summary>
        /// Gets a value indicating whether IsRunning
        /// </summary>
        public static bool IsRunning {
            get { lock (_sync) { return _isRunning; } }
        }

        /// <summary>
        /// Gets a value indicating whether IsBusy
        /// </summary>
        public static bool IsBusy {
            get { lock (_sync) { return _isBusy; } }
        }

        /// <summary>
        /// Gets the LastRunAt
        /// </summary>
        public static DateTime? LastRunAt {
            get { lock (_sync) { return _lastRunAt; } }
        }

        /// <summary>
        /// Gets the LastError
        /// </summary>
        public static string? LastError {
            get { lock (_sync) { return _lastError; } }
        }

        /// <summary>
        /// The ConfigureSessionManager
        /// </summary>
        /// <param name="sessionManager">The sessionManager<see cref="TKBase_SessionManager?"/></param>
        public static void ConfigureSessionManager(TKBase_SessionManager? sessionManager) {
            lock (_sync) {
                _sessionManager = sessionManager;
            }
        }

        /// <summary>
        /// The ConfigureLogger
        /// </summary>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        public static void ConfigureLogger(TKBase_LogManager? logger) {
            lock (_sync) {
                _logger = logger;
            }
        }

        /// <summary>
        /// The Start
        /// </summary>
        public static void Start() {
            bool raiseEvent = false;

            lock (_sync) {
                if (_isRunning) {
                    RunningStateChanged?.Invoke(true, "GSM-Dienst läuft bereits.");
                    return;
                }

                _runCts?.Cancel();
                _runCts?.Dispose();
                _runCts = new CancellationTokenSource();
                _timer?.Dispose();
                _timer = new System.Threading.Timer(async _ => await RunCycleAsync(_runCts.Token).ConfigureAwait(false), null, TimeSpan.Zero, _defaultInterval);
                _isRunning = true;
                _lastError = null;
                raiseEvent = true;
            }

            if (raiseEvent) {
                RunningStateChanged?.Invoke(true, "GSM-Dienst gestartet.");
            }
        }

        /// <summary>
        /// The Stop
        /// </summary>
        public static void Stop() {
            bool raiseEvent = false;

            lock (_sync) {
                if (!_isRunning) {
                    RunningStateChanged?.Invoke(false, "GSM-Dienst ist bereits gestoppt.");
                    return;
                }

                _isRunning = false;
                _runCts?.Cancel();
                _timer?.Dispose();
                _timer = null;
                raiseEvent = true;
            }

            StopAllRuntimeMonitors();
            SetAllGsmExternalNumbersOffline();

            if (raiseEvent) {
                RunningStateChanged?.Invoke(false, "GSM-Dienst gestoppt.");
            }
        }

        /// <summary>
        /// The RunOnce
        /// </summary>
        public static void RunOnce() {
            CancellationToken token;
            lock (_sync) {
                token = _runCts?.Token ?? CancellationToken.None;
            }

            _ = Task.Run(() => RunCycleAsync(token));
        }

        /// <summary>
        /// The RunCycleAsync
        /// </summary>
        /// <param name="cancellationToken">The cancellationToken<see cref="CancellationToken"/></param>
        /// <returns>The <see cref="Task"/></returns>
        private static async Task RunCycleAsync(CancellationToken cancellationToken) {
            lock (_sync) {
                if (_isBusy) {
                    return;
                }

                _isBusy = true;
                _lastError = null;
            }

            try {
                await Task.Run(() => {
                    cancellationToken.ThrowIfCancellationRequested();
                    _logger?.Info("GSM_ServiceHost Zyklus gestartet.");

                    RefreshRuntimeMonitors(cancellationToken);
                    SyncExternalNumbers(cancellationToken);
                }).ConfigureAwait(false);

                lock (_sync) {
                    _lastRunAt = DateTime.Now;
                }

                _logger?.Info("GSM_ServiceHost Zyklus abgeschlossen.");
            } catch (OperationCanceledException) {
                _logger?.Info("GSM_ServiceHost Zyklus abgebrochen.");
            } catch (Exception ex) {
                lock (_sync) {
                    _lastError = ex.Message;
                }

                _logger?.Error($"GSM_ServiceHost Fehler | Fehler={ex}");
            } finally {
                lock (_sync) {
                    _isBusy = false;
                }
            }
        }

        /// <summary>
        /// The StartRuntimeMonitor
        /// </summary>
        /// <param name="info">The info<see cref="RuntimeDeviceInfo"/></param>
        /// <param name="serviceToken">The serviceToken<see cref="CancellationToken"/></param>
        /// <returns>The <see cref="ModemRuntimeMonitor"/></returns>
        private static ModemRuntimeMonitor StartRuntimeMonitor(RuntimeDeviceInfo info, CancellationToken serviceToken) {
            ModemRuntimeMonitor monitor = new ModemRuntimeMonitor {
                StatusId = info.StatusId,
                ModemId = info.ModemId,
                SimId = info.SimId,
                Telefonnummer = info.Telefonnummer,
                IMEI = info.IMEI,
                ProviderName = info.ProviderName,
                ComPortAT = info.ComPortAT,
                ComPortAudio = info.ComPortAudio,
                ComPortNMEA = info.ComPortNMEA,
                Cancellation = CancellationTokenSource.CreateLinkedTokenSource(serviceToken)
            };

            monitor.WorkerTask = Task.Run(() => MonitorLoop(monitor, monitor.Cancellation!.Token), monitor.Cancellation.Token);
            _logger?.Info($"GSM_ServiceHost Monitor gestartet | StatusID={monitor.StatusId} | AT={monitor.ComPortAT} | Audio={monitor.ComPortAudio} | NMEA={monitor.ComPortNMEA} | TEL={monitor.Telefonnummer} | IMEI={monitor.IMEI}");
            return monitor;
        }

        /// <summary>
        /// The EnsurePcm16KFraming
        /// </summary>
        /// <param name="monitor">The monitor<see cref="ModemRuntimeMonitor"/></param>
        /// <param name="port">The port<see cref="SerialPort"/></param>
        private static void EnsurePcm16KFraming(ModemRuntimeMonitor monitor, SerialPort port) {
            try {
                string raw = SendMonitorCommandWithTerminalResponse(monitor, port, "AT+CPCMFRM?", 1500, throwOnError: false);
                bool is16k = raw.Contains(": 1", StringComparison.OrdinalIgnoreCase);
                if (!is16k) {
                    string setRaw = SendMonitorCommandWithTerminalResponse(monitor, port, "AT+CPCMFRM=1", 2000, throwOnError: false);
                    if (setRaw.Contains("OK", StringComparison.OrdinalIgnoreCase)) {
                        _logger?.Info($"GSM_ServiceHost AudioFormat gesetzt | StatusID={monitor.StatusId} | AT={monitor.ComPortAT} | CPCMFRM=1");
                        LogGsmMediaProfile(monitor, "CPCMFRM-SET", "CPCMFRM=1");
                    } else {
                        _logger?.Warn($"GSM_ServiceHost AudioFormat nicht bestaetigt | StatusID={monitor.StatusId} | AT={monitor.ComPortAT} | Antwort={CompactSingleLine(setRaw)}");
                    }
                } else {
                    _logger?.Info($"GSM_ServiceHost AudioFormat aktiv | StatusID={monitor.StatusId} | AT={monitor.ComPortAT} | CPCMFRM=1");
                    LogGsmMediaProfile(monitor, "CPCMFRM-ACTIVE", "CPCMFRM=1");
                }
            } catch (Exception ex) {
                _logger?.Warn($"GSM_ServiceHost AudioFormat Fehler | StatusID={monitor.StatusId} | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The StopAllRuntimeMonitors
        /// </summary>
        private static void StopAllRuntimeMonitors() {
            foreach (ModemRuntimeMonitor monitor in _runtimeMonitors.Values) {
                StopRuntimeMonitor(monitor, "Service stop");
            }
            _runtimeMonitors.Clear();
        }

        /// <summary>
        /// The StopRuntimeMonitor
        /// </summary>
        /// <param name="monitor">The monitor<see cref="ModemRuntimeMonitor"/></param>
        /// <param name="reason">The reason<see cref="string"/></param>
        private static void StopRuntimeMonitor(ModemRuntimeMonitor monitor, string reason) {
            try {
                monitor.Cancellation?.Cancel();
            } catch {
            }

            _logger?.Info($"GSM_ServiceHost Monitor gestoppt | StatusID={monitor.StatusId} | AT={monitor.ComPortAT} | Grund={reason}");
        }

        /// <summary>
        /// The BuildGsmLegContext
        /// </summary>
        /// <param name="monitor">The monitor<see cref="ModemRuntimeMonitor"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string BuildGsmLegContext(ModemRuntimeMonitor monitor) {
            SIP_ServerSession? session = null;
            if (_sessionManager != null && !string.IsNullOrWhiteSpace(monitor.ActiveSessionId)) {
                _sessionManager.TryGet(monitor.ActiveSessionId, out session);
            }

            bool isInbound = string.Equals(session?.GsmDirection, "Inbound", StringComparison.OrdinalIgnoreCase);
            string leg = session == null ? "-" : isInbound ? "A" : "B";
            string callId = session == null
                ? "-"
                : isInbound
                    ? session.LegA?.CallId ?? session.SessionId ?? "-"
                    : session.LegB?.CallId ?? session.SessionId ?? "-";
            string sessionId = session?.SessionId ?? monitor.ActiveSessionId ?? string.Empty;
            string direction = session?.GsmDirection ?? string.Empty;
            string from = session?.FromNumber ?? monitor.PendingCallerNumber ?? string.Empty;
            string to = session?.ToNumber ?? monitor.Telefonnummer ?? string.Empty;

            return $"Leg={leg} | SessionID={(string.IsNullOrWhiteSpace(sessionId) ? "-" : sessionId)} | CallID={(string.IsNullOrWhiteSpace(callId) ? "-" : callId)} | Direction={(string.IsNullOrWhiteSpace(direction) ? "-" : direction)} | StatusID={monitor.StatusId} | AT={monitor.ComPortAT} | Audio={monitor.ComPortAudio} | NMEA={monitor.ComPortNMEA} | From={(string.IsNullOrWhiteSpace(from) ? "-" : from)} | To={(string.IsNullOrWhiteSpace(to) ? "-" : to)}";
        }

        /// <summary>
        /// The LogGsmRaw
        /// </summary>
        /// <param name="monitor">The monitor<see cref="ModemRuntimeMonitor"/></param>
        /// <param name="phase">The phase<see cref="string"/></param>
        /// <param name="command">The command<see cref="string"/></param>
        /// <param name="raw">The raw<see cref="string"/></param>
        private static void LogGsmRaw(ModemRuntimeMonitor monitor, string phase, string command, string raw) {
            string context = BuildGsmLegContext(monitor);
            string safeRaw = raw ?? string.Empty;
            _logger?.Debug($"GSM RAW {phase} | {context} | CMD={command}{Environment.NewLine}{safeRaw}");
        }

        /// <summary>
        /// The MonitorLoop
        /// </summary>
        /// <param name="monitor">The monitor<see cref="ModemRuntimeMonitor"/></param>
        /// <param name="cancellationToken">The cancellationToken<see cref="CancellationToken"/></param>
        private static void MonitorLoop(ModemRuntimeMonitor monitor, CancellationToken cancellationToken) {
            SerialPort? port = null;
            try {
                cancellationToken.ThrowIfCancellationRequested();
                port = new SerialPort(monitor.ComPortAT, 115200, Parity.None, 8, StopBits.One) {
                    Handshake = Handshake.None,
                    DtrEnable = true,
                    RtsEnable = true,
                    NewLine = "\r\n",
                    ReadTimeout = 1000,
                    WriteTimeout = 1000,
                    Encoding = Encoding.ASCII
                };

                port.Open();
                monitor.Port = port;
                Thread.Sleep(250);
                SendMonitorCommand(monitor, port, "AT");
                SendMonitorCommand(monitor, port, "ATE0");
                SendMonitorCommand(monitor, port, "AT+CMEE=2");
                SendMonitorCommand(monitor, port, "AT+CLIP=1");
                SendMonitorCommand(monitor, port, "AT+CRC=1");
                EnsurePcm16KFraming(monitor, port);
                monitor.ClipEnabled = true;

                StringBuilder buffer = new StringBuilder();
                DateTime nextClccPollAtUtc = DateTime.UtcNow.AddMilliseconds(800);
                while (!cancellationToken.IsCancellationRequested) {
                    string chunk = string.Empty;
                    try {
                        lock (monitor.PortSync) {
                            chunk = port.ReadExisting();
                        }
                    } catch (TimeoutException) {
                        chunk = string.Empty;
                    }

                    if (!string.IsNullOrEmpty(chunk)) {
                        buffer.Append(chunk);
                        ProcessMonitorBuffer(monitor, buffer, cancellationToken);
                    }

                    if (DateTime.UtcNow >= nextClccPollAtUtc) {
                        TryPollCurrentCalls(monitor);
                        nextClccPollAtUtc = DateTime.UtcNow.AddSeconds(1);
                    }

                    Thread.Sleep(100);
                }
            } catch (OperationCanceledException) {
                _logger?.Info($"GSM_ServiceHost Monitor abgebrochen | StatusID={monitor.StatusId} | AT={monitor.ComPortAT}");
            } catch (Exception ex) {
                lock (_sync) {
                    _lastError = ex.Message;
                }
                _logger?.Error($"GSM_ServiceHost Monitorfehler | StatusID={monitor.StatusId} | AT={monitor.ComPortAT} | Fehler={ex.Message}");
            } finally {
                try {
                    monitor.Port = null;
                    if (port?.IsOpen == true) {
                        port.Close();
                    }
                } catch {
                }

                _runtimeMonitors.TryRemove(monitor.StatusId, out _);
            }
        }

        /// <summary>
        /// The SendMonitorCommand
        /// </summary>
        /// <param name="monitor">The monitor<see cref="ModemRuntimeMonitor"/></param>
        /// <param name="port">The port<see cref="SerialPort"/></param>
        /// <param name="command">The command<see cref="string"/></param>
        internal static void SendMonitorCommand(ModemRuntimeMonitor monitor, SerialPort port, string command) {
            try {
                lock (monitor.PortSync) {
                    lock (port) {
                        port.Write(command + "\r");
                        Thread.Sleep(150);
                        string raw = port.ReadExisting();
                        if (!string.IsNullOrWhiteSpace(raw)) {
                            _logger?.Debug($"GSM_ServiceHost Monitor INIT | Port={port.PortName} | CMD={command} | RAW=[{raw}]");
                            LogGsmRaw(monitor, "IN", command, raw);
                            ProcessInlineCommandResponse(monitor, raw, command);
                        }
                    }
                }
            } catch (Exception ex) {
                _logger?.Warn($"GSM_ServiceHost Monitor INIT fehlgeschlagen | Port={port.PortName} | CMD={command} | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The SendMonitorCommandWithResponse
        /// </summary>
        /// <param name="monitor">The monitor<see cref="ModemRuntimeMonitor"/></param>
        /// <param name="port">The port<see cref="SerialPort"/></param>
        /// <param name="command">The command<see cref="string"/></param>
        /// <param name="waitMs">The waitMs<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        internal static string SendMonitorCommandWithResponse(ModemRuntimeMonitor monitor, SerialPort port, string command, int waitMs) {
            lock (monitor.PortSync) {
                lock (port) {
                    port.Write(command + "\r");
                    Thread.Sleep(waitMs <= 0 ? 150 : waitMs);
                    string raw = port.ReadExisting();
                    if (!string.IsNullOrWhiteSpace(raw)) {
                        _logger?.Debug($"GSM_ServiceHost Monitor CMD | Port={port.PortName} | CMD={command} | RAW=[{raw}]");
                        LogGsmRaw(monitor, "IN", command, raw);
                    }

                    string normalizedRaw = raw ?? string.Empty;
                    ProcessInlineCommandResponse(monitor, normalizedRaw, command);

                    if (normalizedRaw.Contains("ERROR", StringComparison.OrdinalIgnoreCase) &&
                        !normalizedRaw.Contains("VOICE CALL: END", StringComparison.OrdinalIgnoreCase)) {
                        throw new InvalidOperationException($"AT Fehler bei {command}: {normalizedRaw}");
                    }

                    return normalizedRaw;
                }
            }
        }

        /// <summary>
        /// The SendMonitorCommandWithTerminalResponse
        /// </summary>
        /// <param name="monitor">The monitor<see cref="ModemRuntimeMonitor"/></param>
        /// <param name="port">The port<see cref="SerialPort"/></param>
        /// <param name="command">The command<see cref="string"/></param>
        /// <param name="timeoutMs">The timeoutMs<see cref="int"/></param>
        /// <param name="throwOnError">The throwOnError<see cref="bool"/></param>
        /// <returns>The <see cref="string"/></returns>
        internal static string SendMonitorCommandWithTerminalResponse(ModemRuntimeMonitor monitor, SerialPort port, string command, int timeoutMs, bool throwOnError) {
            lock (monitor.PortSync) {
                lock (port) {
                    port.Write(command + "\r");

                    StringBuilder rawBuilder = new();
                    long deadline = Environment.TickCount64 + Math.Max(timeoutMs, 250);

                    while (Environment.TickCount64 <= deadline) {
                        string chunk = port.ReadExisting();
                        if (!string.IsNullOrEmpty(chunk)) {
                            rawBuilder.Append(chunk);
                            if (HasTerminalResponse(command, rawBuilder.ToString())) {
                                break;
                            }
                        } else {
                            Thread.Sleep(10);
                        }
                    }

                    string raw = rawBuilder.ToString();
                    if (!string.IsNullOrWhiteSpace(raw)) {
                        _logger?.Debug($"GSM_ServiceHost Monitor CMD | Port={port.PortName} | CMD={command} | RAW=[{raw}]");
                        LogGsmRaw(monitor, "IN", command, raw);
                    }

                    string normalizedRaw = raw ?? string.Empty;
                    ProcessInlineCommandResponse(monitor, normalizedRaw, command);

                    if (throwOnError &&
                        normalizedRaw.Contains("ERROR", StringComparison.OrdinalIgnoreCase) &&
                        !normalizedRaw.Contains("VOICE CALL: END", StringComparison.OrdinalIgnoreCase)) {
                        throw new InvalidOperationException($"AT Fehler bei {command}: {normalizedRaw}");
                    }

                    return normalizedRaw;
                }
            }
        }

        /// <summary>
        /// The HasTerminalResponse
        /// </summary>
        /// <param name="command">The command<see cref="string"/></param>
        /// <param name="raw">The raw<see cref="string"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool HasTerminalResponse(string command, string raw) {
            if (string.IsNullOrWhiteSpace(raw)) {
                return false;
            }

            string normalized = (raw ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n");
            string trimmed = normalized.Trim();

            bool hasOk = trimmed.Equals("OK", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("\nOK", StringComparison.OrdinalIgnoreCase)
                || normalized.EndsWith("OK\n", StringComparison.OrdinalIgnoreCase);

            bool hasError = trimmed.Equals("ERROR", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("\nERROR", StringComparison.OrdinalIgnoreCase)
                || normalized.EndsWith("ERROR\n", StringComparison.OrdinalIgnoreCase);

            if (hasOk || hasError) {
                return true;
            }

            if (command.Equals("AT+CLCC", StringComparison.OrdinalIgnoreCase) && normalized.IndexOf("+CLCC:", StringComparison.OrdinalIgnoreCase) >= 0) {
                return normalized.IndexOf("OK", StringComparison.OrdinalIgnoreCase) >= 0
                    || normalized.IndexOf("ERROR", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            return normalized.IndexOf("VOICE CALL: END", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// The ContainsClccLine
        /// </summary>
        /// <param name="raw">The raw<see cref="string"/></param>
        /// <returns>The <see cref="bool"/></returns>
        internal static bool ContainsClccLine(string raw) {
            return (raw ?? string.Empty).IndexOf("+CLCC:", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// The CompactSingleLine
        /// </summary>
        /// <param name="value">The value<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        internal static string CompactSingleLine(string value) {
            string compact = (value ?? string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " ");

            while (compact.Contains("  ", StringComparison.Ordinal)) {
                compact = compact.Replace("  ", " ");
            }

            return compact.Trim();
        }

        /// <summary>
        /// The TryPollCurrentCalls
        /// </summary>
        /// <param name="monitor">The monitor<see cref="ModemRuntimeMonitor"/></param>
        private static void TryPollCurrentCalls(ModemRuntimeMonitor monitor) {
            SerialPort? port = monitor.Port;
            if (port == null || !port.IsOpen) {
                return;
            }

            bool shouldPoll = !string.IsNullOrWhiteSpace(monitor.ActiveSessionId)
                || !string.IsNullOrWhiteSpace(monitor.PendingCallerNumber)
                || !string.IsNullOrWhiteSpace(monitor.PendingConnectedNumber)
                || !string.IsNullOrWhiteSpace(monitor.PendingLastClccState);

            if (!shouldPoll) {
                return;
            }

            try {
                lock (monitor.PortSync) {
                    SendMonitorCommandWithResponse(monitor, port, "AT+CLCC", 250);
                }
            } catch (Exception ex) {
                _logger?.Warn($"GSM_ServiceHost CLCC Poll fehlgeschlagen | StatusID={monitor.StatusId} | AT={monitor.ComPortAT} | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The ProcessMonitorBuffer
        /// </summary>
        /// <param name="monitor">The monitor<see cref="ModemRuntimeMonitor"/></param>
        /// <param name="buffer">The buffer<see cref="StringBuilder"/></param>
        /// <param name="cancellationToken">The cancellationToken<see cref="CancellationToken"/></param>
        private static void ProcessMonitorBuffer(ModemRuntimeMonitor monitor, StringBuilder buffer, CancellationToken cancellationToken) {
            string all = buffer.ToString();
            if (string.IsNullOrEmpty(all)) {
                return;
            }

            string normalized = all.Replace("\r", "\n");
            string[] parts = normalized.Split('\n');
            bool endsWithNewline = normalized.EndsWith("\n", StringComparison.Ordinal);
            int completeCount = endsWithNewline ? parts.Length : parts.Length - 1;

            for (int i = 0; i < completeCount; i++) {
                cancellationToken.ThrowIfCancellationRequested();
                string line = parts[i].Trim();
                if (string.IsNullOrWhiteSpace(line)) {
                    continue;
                }

                HandleMonitorLine(monitor, line);
            }

            string remainder = endsWithNewline ? string.Empty : parts[^1];
            buffer.Clear();
            if (!string.IsNullOrEmpty(remainder)) {
                buffer.Append(remainder);
            }
        }

        /// <summary>
        /// The HandleMonitorLine
        /// </summary>
        /// <param name="monitor">The monitor<see cref="ModemRuntimeMonitor"/></param>
        /// <param name="line">The line<see cref="string"/></param>
        private static void HandleMonitorLine(ModemRuntimeMonitor monitor, string line) {
            GsmAtSignal signal = ParseSignal(line);
            _logger?.Info($"GSM_ServiceHost Signal | StatusID={monitor.StatusId} | AT={monitor.ComPortAT} | LINE={line} | Kind={signal.Kind} | CoreState={signal.CoreState}");

            if (signal.Kind == GsmAtSignalKind.Ignored || signal.Kind == GsmAtSignalKind.Unknown) {
                return;
            }

            if (signal.Kind == GsmAtSignalKind.ExtendedError) {
                monitor.PendingExtendedCause = signal.CauseText;
                return;
            }

            SIP_ServerSession? activeServerSession = null;
            bool hasActiveSession = _sessionManager != null
                && !string.IsNullOrWhiteSpace(monitor.ActiveSessionId)
                && _sessionManager.TryGet(monitor.ActiveSessionId, out activeServerSession)
                && activeServerSession != null;
            TKGSM_Session? activeSession = hasActiveSession && activeServerSession != null
                ? TKGSM_Session.FromServerSession(activeServerSession)
                : null;
            bool isOutboundSession = activeSession != null
                && string.Equals(activeSession.Direction, "Outbound", StringComparison.OrdinalIgnoreCase);

            if (signal.Kind == GsmAtSignalKind.Clip) {
                if (!string.IsNullOrWhiteSpace(signal.Number)) {
                    monitor.PendingCallerNumber = signal.Number;
                    if (hasActiveSession && !isOutboundSession && activeServerSession != null) {
                        _sessionManager.UpdateExistingSessionCallerInfo(activeServerSession, signal.Number, signal.RawLine);
                        _sessionManager.ApplySignalStateToExistingSession(activeServerSession, "Ringing", signal.RawLine);
                        RaiseCallStateChanged(monitor, TKGSM_Session.FromServerSession(activeServerSession), "Ringing", signal.RawLine);
                    } else if (!isOutboundSession && _sessionManager != null && !string.IsNullOrWhiteSpace(monitor.Telefonnummer)) {
                        TKGSM_Session inbound = _sessionManager.UpsertInboundMonitorSession(
                            monitor,
                            signal.Number,
                            monitor.Telefonnummer,
                            signal.RawLine);
                        string sessionId = inbound.SessionId ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(sessionId)) {
                            _sessionManager.ApplySignalStateToExistingSession(sessionId, "Ringing", signal.RawLine);
                            if (_sessionManager.TryGet(sessionId, out SIP_ServerSession? clipSession) && clipSession != null) {
                                RaiseCallStateChanged(monitor, TKGSM_Session.FromServerSession(clipSession), "Ringing", signal.RawLine);
                            } else {
                                RaiseCallStateChanged(monitor, inbound, "Ringing", signal.RawLine);
                            }
                        }
                    }
                }
                return;
            }

            if (signal.Kind == GsmAtSignalKind.Colp) {
                if (!string.IsNullOrWhiteSpace(signal.Number)) {
                    monitor.PendingConnectedNumber = signal.Number;
                    if (hasActiveSession) {
                        _sessionManager.UpdateConnectedPartyInfo(activeServerSession!, signal.Number, signal.RawLine);
                    }
                }
                return;
            }

            if (signal.Kind == GsmAtSignalKind.Waiting) {
                if (!string.IsNullOrWhiteSpace(signal.Number)) {
                    monitor.PendingCallerNumber = signal.Number;
                }
                return;
            }

            if (signal.Kind == GsmAtSignalKind.CurrentCallState) {
                monitor.PendingLastClccState = signal.CoreState;
                if (string.IsNullOrWhiteSpace(signal.CoreState)) {
                    return;
                }
            }

            if (signal.Kind == GsmAtSignalKind.Ringing) {
                if (hasActiveSession && activeServerSession != null) {
                    _sessionManager.ApplySignalStateToExistingSession(activeServerSession, signal.CoreState, signal.RawLine);
                    RaiseCallStateChanged(monitor, TKGSM_Session.FromServerSession(activeServerSession), signal.CoreState, signal.RawLine);
                    return;
                }

                if (isOutboundSession && activeSession != null) {
                    _sessionManager.ApplySignalStateToExistingSession(activeServerSession!, signal.CoreState, signal.RawLine);
                    RaiseCallStateChanged(monitor, TKGSM_Session.FromServerSession(activeServerSession!), signal.CoreState, signal.RawLine);
                    return;
                }

                if (string.IsNullOrWhiteSpace(monitor.ActiveSessionId) && string.IsNullOrWhiteSpace(monitor.PendingCallerNumber)) {
                    _logger?.Info($"GSM_ServiceHost Inbound-Ringing vorgemerkt | StatusID={monitor.StatusId} | An={monitor.Telefonnummer} | Signal={signal.RawLine} | Grund=CallerNumber fehlt noch");
                    return;
                }

                TKGSM_Session inbound = _sessionManager.UpsertInboundMonitorSession(
                    monitor,
                    monitor.PendingCallerNumber,
                    monitor.Telefonnummer,
                    signal.RawLine);
                string sessionId = inbound.SessionId ?? string.Empty;
                _sessionManager.ApplySignalStateToExistingSession(sessionId, signal.CoreState, signal.RawLine);
                if (_sessionManager != null && _sessionManager.TryGet(sessionId, out SIP_ServerSession? ringingSession) && ringingSession != null) {
                    RaiseCallStateChanged(monitor, TKGSM_Session.FromServerSession(ringingSession), signal.CoreState, signal.RawLine);
                } else {
                    RaiseCallStateChanged(monitor, inbound, signal.CoreState, signal.RawLine);
                }
                return;
            }

            if (signal.Kind == GsmAtSignalKind.Answered) {
                if (hasActiveSession && activeServerSession != null) {
                    if (!string.IsNullOrWhiteSpace(monitor.PendingConnectedNumber)) {
                        _sessionManager.UpdateConnectedPartyInfo(activeServerSession, monitor.PendingConnectedNumber, signal.RawLine);
                    }
                    _sessionManager.ApplySignalStateToExistingSession(activeServerSession, signal.CoreState, signal.RawLine);
                    RaiseCallStateChanged(monitor, TKGSM_Session.FromServerSession(activeServerSession), signal.CoreState, signal.RawLine);
                    return;
                }

                TKGSM_Session inbound = _sessionManager.UpsertInboundMonitorSession(
                    monitor,
                    monitor.PendingCallerNumber,
                    monitor.Telefonnummer,
                    signal.RawLine);
                string sessionId = inbound.SessionId ?? string.Empty;
                _sessionManager.ApplySignalStateToExistingSession(sessionId, signal.CoreState, signal.RawLine);
                if (_sessionManager != null && _sessionManager.TryGet(sessionId, out SIP_ServerSession? answeredSession) && answeredSession != null) {
                    RaiseCallStateChanged(monitor, TKGSM_Session.FromServerSession(answeredSession), signal.CoreState, signal.RawLine);
                }
                return;
            }

            if (signal.IsFinal) {
                string finalState = ResolveFinalCoreState(monitor, activeServerSession, signal);
                string finalReason = BuildFinalReason(signal, monitor.PendingExtendedCause);

                if (hasActiveSession && activeServerSession != null) {
                    _sessionManager.ApplySignalStateToExistingSession(activeServerSession, finalState, finalReason);
                    RaiseCallStateChanged(monitor, TKGSM_Session.FromServerSession(activeServerSession), finalState, finalReason);
                }

                monitor.ActiveSessionId = string.Empty;
                monitor.PendingCallerNumber = string.Empty;
                monitor.PendingConnectedNumber = string.Empty;
                monitor.PendingExtendedCause = string.Empty;
                monitor.PendingLastClccState = string.Empty;
            }
        }

        /// <summary>
        /// The ParseSignal
        /// </summary>
        /// <param name="line">The line<see cref="string?"/></param>
        /// <returns>The <see cref="GsmAtSignal"/></returns>
        private static GsmAtSignal ParseSignal(string? line) {
            string raw = line ?? string.Empty;
            string normalized = NormalizeSignalLine(raw);

            if (string.IsNullOrWhiteSpace(normalized)) {
                return CreateSignal(GsmAtSignalKind.Ignored, raw, normalized);
            }

            if (normalized.StartsWith("AT", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("OK", StringComparison.OrdinalIgnoreCase)) {
                return CreateSignal(GsmAtSignalKind.Ignored, raw, normalized);
            }

            if (normalized.Equals("ERROR", StringComparison.OrdinalIgnoreCase)) {
                return CreateSignal(GsmAtSignalKind.Error, raw, normalized, coreState: "Failed", causeText: "ERROR");
            }

            Match errorMatch = Regex.Match(normalized, @"^\+(?<kind>CME|CMS)\s+ERROR:\s*(?<cause>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (errorMatch.Success) {
                return CreateSignal(GsmAtSignalKind.Error, raw, normalized, coreState: "Failed", causeText: errorMatch.Groups["cause"].Value.Trim());
            }

            if (normalized.StartsWith("+CLIP:", StringComparison.OrdinalIgnoreCase)) {
                return CreateSignal(GsmAtSignalKind.Clip, raw, normalized, coreState: "Ringing", number: ExtractQuotedNumber(normalized));
            }

            Match cringMatch = Regex.Match(normalized, @"^\+CRING:\s*(?<type>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (cringMatch.Success) {
                return CreateSignal(GsmAtSignalKind.Ringing, raw, normalized, coreState: "Ringing", callType: cringMatch.Groups["type"].Value.Trim());
            }

            if (normalized.Equals("RING", StringComparison.OrdinalIgnoreCase)) {
                return CreateSignal(GsmAtSignalKind.Ringing, raw, normalized, coreState: "Ringing", callType: "VOICE");
            }

            if (normalized.Equals("CONNECT", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("VOICE CALL: BEGIN", StringComparison.OrdinalIgnoreCase)) {
                return CreateSignal(GsmAtSignalKind.Answered, raw, normalized, coreState: "Answered");
            }

            if (normalized.Equals("BUSY", StringComparison.OrdinalIgnoreCase)) {
                return CreateSignal(GsmAtSignalKind.Busy, raw, normalized, coreState: "Failed:Busy", causeText: "BUSY");
            }

            if (normalized.Equals("NO ANSWER", StringComparison.OrdinalIgnoreCase)) {
                return CreateSignal(GsmAtSignalKind.NoAnswer, raw, normalized, coreState: "Failed", causeText: "NO ANSWER");
            }

            if (normalized.Equals("NO DIALTONE", StringComparison.OrdinalIgnoreCase)) {
                return CreateSignal(GsmAtSignalKind.NoDialTone, raw, normalized, coreState: "Failed", causeText: "NO DIALTONE");
            }

            if (normalized.Equals("NO CARRIER", StringComparison.OrdinalIgnoreCase)) {
                return CreateSignal(GsmAtSignalKind.NoCarrier, raw, normalized, coreState: "Ended", causeText: "NO CARRIER");
            }

            Match voiceEndMatch = Regex.Match(normalized, @"^VOICE\s+CALL:\s+END\s*:?\s*(?<dur>\d+)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (voiceEndMatch.Success) {
                int? durationSeconds = int.TryParse(voiceEndMatch.Groups["dur"].Value, out int parsedDuration) ? parsedDuration : null;
                return CreateSignal(GsmAtSignalKind.Ended, raw, normalized, coreState: "Ended", causeText: "VOICE CALL END", durationSeconds: durationSeconds);
            }

            if (normalized.StartsWith("MISSED_CALL:", StringComparison.OrdinalIgnoreCase)) {
                return CreateSignal(GsmAtSignalKind.NoAnswer, raw, normalized, coreState: "Cancelled", causeText: normalized);
            }

            if (normalized.StartsWith("+CCWA:", StringComparison.OrdinalIgnoreCase)) {
                return CreateSignal(GsmAtSignalKind.Waiting, raw, normalized, coreState: "Ringing", number: ExtractQuotedNumber(normalized));
            }

            if (normalized.StartsWith("+COLP:", StringComparison.OrdinalIgnoreCase)) {
                return CreateSignal(GsmAtSignalKind.Colp, raw, normalized, coreState: "Answered", number: ExtractQuotedNumber(normalized));
            }

            Match ceerMatch = Regex.Match(normalized, @"^\+CEER:\s*(?<cause>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (ceerMatch.Success) {
                return CreateSignal(GsmAtSignalKind.ExtendedError, raw, normalized, causeText: ceerMatch.Groups["cause"].Value.Trim());
            }

            Match clccMatch = Regex.Match(normalized, @"^\+CLCC:\s*(?<idx>\d+)\s*,\s*(?<dir>\d+)\s*,\s*(?<stat>\d+)\s*,\s*(?<mode>\d+)\s*,\s*(?<mpty>\d+)(?:\s*,\s*""(?<num>[^""]*)"")?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (clccMatch.Success) {
                int? idxVal = int.TryParse(clccMatch.Groups["idx"].Value, out int idxParsed) ? idxParsed : null;
                int? dirVal = int.TryParse(clccMatch.Groups["dir"].Value, out int dirParsed) ? dirParsed : null;
                int? statVal = int.TryParse(clccMatch.Groups["stat"].Value, out int statParsed) ? statParsed : null;
                string coreState = MapClccToCoreState(statVal);
                return CreateSignal(GsmAtSignalKind.CurrentCallState, raw, normalized, coreState: coreState, number: clccMatch.Groups["num"].Value.Trim(), clccIndex: idxVal, clccDirection: dirVal, clccStatus: statVal);
            }

            return CreateSignal(GsmAtSignalKind.Unknown, raw, normalized);
        }

        /// <summary>
        /// The TryParseClccLine
        /// </summary>
        /// <param name="line">The line<see cref="string"/></param>
        /// <param name="clccIndex">The clccIndex<see cref="int?"/></param>
        /// <param name="clccDirection">The clccDirection<see cref="int?"/></param>
        /// <param name="clccStatus">The clccStatus<see cref="int?"/></param>
        /// <param name="number">The number<see cref="string"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool TryParseClccLine(string line, out int? clccIndex, out int? clccDirection, out int? clccStatus, out string number) {
            clccIndex = null;
            clccDirection = null;
            clccStatus = null;
            number = string.Empty;

            string normalized = (line ?? string.Empty).Trim();
            if (!normalized.StartsWith("+CLCC:", StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            int colonIndex = normalized.IndexOf(':');
            if (colonIndex < 0 || colonIndex >= normalized.Length - 1) {
                return false;
            }

            List<string> values = SplitCommaSeparatedValues(normalized[(colonIndex + 1)..].Trim());
            if (values.Count < 5) {
                return false;
            }

            clccIndex = TryParseNullableInt(values[0]);
            clccDirection = TryParseNullableInt(values[1]);
            clccStatus = TryParseNullableInt(values[2]);

            if (values.Count >= 6) {
                number = UnquoteCsvValue(values[5]);
            }

            return clccIndex != null && clccDirection != null && clccStatus != null;
        }

        /// <summary>
        /// The SplitCommaSeparatedValues
        /// </summary>
        /// <param name="value">The value<see cref="string"/></param>
        /// <returns>The <see cref="List{string}"/></returns>
        private static List<string> SplitCommaSeparatedValues(string value) {
            List<string> parts = new List<string>();
            if (string.IsNullOrWhiteSpace(value)) {
                return parts;
            }

            StringBuilder current = new StringBuilder();
            bool inQuotes = false;

            foreach (char ch in value) {
                if (ch == '"') {
                    inQuotes = !inQuotes;
                    current.Append(ch);
                    continue;
                }

                if (ch == ',' && !inQuotes) {
                    parts.Add(current.ToString().Trim());
                    current.Clear();
                    continue;
                }

                current.Append(ch);
            }

            parts.Add(current.ToString().Trim());
            return parts;
        }

        /// <summary>
        /// The TryParseNullableInt
        /// </summary>
        /// <param name="value">The value<see cref="string"/></param>
        /// <returns>The <see cref="int?"/></returns>
        private static int? TryParseNullableInt(string value) {
            return int.TryParse((value ?? string.Empty).Trim(), out int parsed) ? parsed : null;
        }

        /// <summary>
        /// The UnquoteCsvValue
        /// </summary>
        /// <param name="value">The value<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string UnquoteCsvValue(string value) {
            string text = (value ?? string.Empty).Trim();
            if (text.Length >= 2 && text[0] == '"' && text[^1] == '"') {
                return text[1..^1].Trim();
            }
            return text;
        }

        /// <summary>
        /// The CreateSignal
        /// </summary>
        /// <param name="kind">The kind<see cref="GsmAtSignalKind"/></param>
        /// <param name="rawLine">The rawLine<see cref="string"/></param>
        /// <param name="normalizedLine">The normalizedLine<see cref="string"/></param>
        /// <param name="coreState">The coreState<see cref="string"/></param>
        /// <param name="number">The number<see cref="string"/></param>
        /// <param name="causeText">The causeText<see cref="string"/></param>
        /// <param name="callType">The callType<see cref="string"/></param>
        /// <param name="durationSeconds">The durationSeconds<see cref="int?"/></param>
        /// <param name="clccIndex">The clccIndex<see cref="int?"/></param>
        /// <param name="clccDirection">The clccDirection<see cref="int?"/></param>
        /// <param name="clccStatus">The clccStatus<see cref="int?"/></param>
        /// <returns>The <see cref="GsmAtSignal"/></returns>
        private static GsmAtSignal CreateSignal(
            GsmAtSignalKind kind,
            string rawLine,
            string normalizedLine,
            string coreState = "",
            string number = "",
            string causeText = "",
            string callType = "",
            int? durationSeconds = null,
            int? clccIndex = null,
            int? clccDirection = null,
            int? clccStatus = null) {
            return new GsmAtSignal {
                Kind = kind,
                RawLine = rawLine ?? string.Empty,
                NormalizedLine = normalizedLine ?? string.Empty,
                CoreState = coreState ?? string.Empty,
                Number = number ?? string.Empty,
                CauseText = causeText ?? string.Empty,
                CallType = callType ?? string.Empty,
                DurationSeconds = durationSeconds,
                ClccIndex = clccIndex,
                ClccDirection = clccDirection,
                ClccStatus = clccStatus
            };
        }

        /// <summary>
        /// The NormalizeSignalLine
        /// </summary>
        /// <param name="line">The line<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string NormalizeSignalLine(string line) {
            return (line ?? string.Empty)
                .Replace("\0", string.Empty)
                .Replace("\r", string.Empty)
                .Replace("\n", string.Empty)
                .Trim();
        }

        /// <summary>
        /// The ExtractQuotedNumber
        /// </summary>
        /// <param name="line">The line<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string ExtractQuotedNumber(string line) {
            Match match = Regex.Match(line ?? string.Empty, "\"(?<num>[^\"]+)\"");
            return match.Success ? match.Groups["num"].Value.Trim() : string.Empty;
        }

        /// <summary>
        /// The ParseClipNumber
        /// </summary>
        /// <param name="line">The line<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string ParseClipNumber(string line) {
            Match match = Regex.Match(line ?? string.Empty, "\"(?<num>[^\"]+)\"");
            return match.Success ? match.Groups["num"].Value.Trim() : string.Empty;
        }

        /// <summary>
        /// The ReadInt
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="columnName">The columnName<see cref="string"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int ReadInt(DataRow row, string columnName) {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value) {
                return 0;
            }
            return Convert.ToInt32(row[columnName]);
        }

        /// <summary>
        /// The ReadNullableInt
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="columnName">The columnName<see cref="string"/></param>
        /// <returns>The <see cref="int?"/></returns>
        private static int? ReadNullableInt(DataRow row, string columnName) {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value) {
                return null;
            }
            return Convert.ToInt32(row[columnName]);
        }

        /// <summary>
        /// The ReadString
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="columnName">The columnName<see cref="string"/></param>
        /// <param name="fallback">The fallback<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string ReadString(DataRow row, string columnName, string fallback) {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value) {
                return fallback;
            }

            string value = Convert.ToString(row[columnName])?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        /// <summary>
        /// The GetDashboardRows
        /// </summary>
        /// <returns>The <see cref="IReadOnlyList{DashboardRow}"/></returns>
        public static IReadOnlyList<DashboardRow> GetDashboardRows() {
            List<DashboardRow> rows = new List<DashboardRow>();
            bool isRunning = IsRunning;

            try {
                DataTable table = new TKSQL_Commands(_logger).LoadGsmGatewayDashboardRowsTable();
                foreach (DataRow row in table.Rows) {
                    rows.Add(new DashboardRow {
                        StatusId = row["ID"] == DBNull.Value ? 0 : Convert.ToInt32(row["ID"]),
                        Telefonnummer = ReadString(row, "Telefonnummer", "-"),
                        IMEI = ReadString(row, "IMEI", "-"),
                        IsServiceStarted = isRunning
                    });
                }
            } catch (OperationCanceledException) {
                _logger?.Info("GSM_ServiceHost DashboardRows abgebrochen.");
            } catch (Exception ex) {
                lock (_sync) {
                    _lastError = ex.Message;
                }

                _logger?.Error($"GSM_ServiceHost DashboardRows fehlgeschlagen | Fehler={ex}");
            }

            return rows;
        }

        /// <summary>
        /// The SetAllGsmExternalNumbersOffline
        /// </summary>
        private static void SetAllGsmExternalNumbersOffline() {
            try {
                TKSQL_Commands commands = new TKSQL_Commands(_logger);
                List<DataRow> rows = commands.GetActiveGsmRuntimeDevices();
                foreach (DataRow row in rows) {
                    int statusId = ReadInt(row, "ID");
                    if (statusId <= 0) {
                        continue;
                    }

                    commands.SetExternalNumberOnlineState("GSMStatus", statusId, false);
                }
            } catch (Exception ex) {
                _logger?.Warn($"GSM ExternalNumbers Offline fehlgeschlagen | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The SyncExternalNumbers
        /// </summary>
        /// <param name="cancellationToken">The cancellationToken<see cref="CancellationToken"/></param>
        private static void SyncExternalNumbers(CancellationToken cancellationToken) {
            TKSQL_Commands commands = new TKSQL_Commands(_logger);
            commands.DeleteExternalNumbersAndRoutingForMissingGsmCards();

            List<DataRow> rows = commands.GetActiveGsmRuntimeDevices();
            foreach (DataRow row in rows) {
                cancellationToken.ThrowIfCancellationRequested();
                int statusId = ReadInt(row, "ID");
                if (statusId <= 0) {
                    continue;
                }

                commands.UpsertExternalNumberFromGsmStatus(statusId, true, true);
            }
        }

        /// <summary>
        /// The RefreshRuntimeMonitors
        /// </summary>
        /// <param name="cancellationToken">The cancellationToken<see cref="CancellationToken"/></param>
        private static void RefreshRuntimeMonitors(CancellationToken cancellationToken) {
            TKSQL_Commands commands = new TKSQL_Commands(_logger);
            List<DataRow> rows = commands.GetActiveGsmRuntimeDevices();
            Dictionary<int, RuntimeDeviceInfo> desired = new Dictionary<int, RuntimeDeviceInfo>();

            foreach (DataRow row in rows) {
                cancellationToken.ThrowIfCancellationRequested();
                int statusId = ReadInt(row, "ID");
                string comPortAT = ReadString(row, "ComPortAT", string.Empty);
                string comPortAudio = ReadString(row, "ComPortAudio", string.Empty);
                string comPortNMEA = ReadString(row, "ComPortNMEA", string.Empty);
                if (statusId <= 0 || string.IsNullOrWhiteSpace(comPortAT)) {
                    continue;
                }

                desired[statusId] = new RuntimeDeviceInfo {
                    StatusId = statusId,
                    ModemId = ReadNullableInt(row, "ModemID"),
                    SimId = ReadNullableInt(row, "SimID"),
                    Telefonnummer = ReadString(row, "Telefonnummer", string.Empty),
                    IMEI = ReadString(row, "IMEI", string.Empty),
                    ProviderName = ReadString(row, "ProviderName", string.Empty),
                    ComPortAT = comPortAT,
                    ComPortAudio = comPortAudio,
                    ComPortNMEA = comPortNMEA
                };
            }

            foreach (KeyValuePair<int, ModemRuntimeMonitor> existing in _runtimeMonitors.ToArray()) {
                cancellationToken.ThrowIfCancellationRequested();
                if (!desired.TryGetValue(existing.Key, out RuntimeDeviceInfo? desiredInfo) ||
                    !string.Equals(existing.Value.ComPortAT, desiredInfo.ComPortAT, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(existing.Value.ComPortAudio, desiredInfo.ComPortAudio, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(existing.Value.ComPortNMEA, desiredInfo.ComPortNMEA, StringComparison.OrdinalIgnoreCase)) {
                    StopRuntimeMonitor(existing.Value, "Portzuordnung entfernt oder geändert");
                    _runtimeMonitors.TryRemove(existing.Key, out _);
                }
            }

            foreach (RuntimeDeviceInfo info in desired.Values) {
                cancellationToken.ThrowIfCancellationRequested();
                _runtimeMonitors.AddOrUpdate(
                    info.StatusId,
                    _ => StartRuntimeMonitor(info, cancellationToken),
                    (_, existing) => existing);
            }
        }

        /// <summary>
        /// The NormalizeGsmDialNumber
        /// </summary>
        /// <param name="number">The number<see cref="string?"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string NormalizeGsmDialNumber(string? number) {
            if (string.IsNullOrWhiteSpace(number)) {
                return string.Empty;
            }

            string n = number.Trim();

            if (n.StartsWith("+", StringComparison.Ordinal)) {
                n = "00" + n.Substring(1);
            }

            n = n
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace("/", string.Empty, StringComparison.Ordinal)
                .Replace("(", string.Empty, StringComparison.Ordinal)
                .Replace(")", string.Empty, StringComparison.Ordinal);

            return n;
        }

        /// <summary>
        /// The LogDialNormalization
        /// </summary>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        /// <param name="sessionId">The sessionId<see cref="string?"/></param>
        /// <param name="originalNumber">The originalNumber<see cref="string?"/></param>
        /// <param name="normalizedNumber">The normalizedNumber<see cref="string?"/></param>
        public static void LogDialNormalization(
            TKBase_LogManager? logger,
            string? sessionId,
            string? originalNumber,
            string? normalizedNumber) {

            logger?.Info(
                $"GSM Dial normalize | SessionID={sessionId} | Original={originalNumber} | Dial={normalizedNumber}");
        }

        /// <summary>
        /// The StartOutboundCall
        /// </summary>
        /// <param name="route">The route<see cref="SIPIntResolvedTarget"/></param>
        /// <param name="fromNumber">The fromNumber<see cref="string"/></param>
        /// <param name="calledNumber">The calledNumber<see cref="string"/></param>
        /// <param name="sessionId">The sessionId<see cref="string?"/></param>
        /// <param name="reason">The reason<see cref="string?"/></param>
        /// <param name="preferredSessionId">The preferredSessionId<see cref="string?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public static bool StartOutboundCall(
            SIPIntResolvedTarget route,
            string fromNumber,
            string calledNumber,
            out string? sessionId,
            out string? reason,
            string? preferredSessionId = null) {

            sessionId = null;
            reason = null;

            if (route == null || !route.IsGsmTarget || route.GsmStatusId == null || route.GsmStatusId <= 0) {
                reason = "Kein gültiges GSM-Ziel aufgelöst.";
                return false;
            }

            if (!_runtimeMonitors.TryGetValue(route.GsmStatusId.Value, out ModemRuntimeMonitor? monitor) || monitor == null) {
                reason = $"GSM-Monitor nicht gefunden | StatusID={route.GsmStatusId}";
                return false;
            }

            if (monitor.Port == null || !monitor.Port.IsOpen) {
                reason = $"AT-Port nicht geöffnet | StatusID={route.GsmStatusId} | AT={monitor.ComPortAT}";
                return false;
            }

            TKBase_SessionManager? sessionManager = _sessionManager;
            if (sessionManager == null) {
                reason = "SessionManager nicht konfiguriert.";
                return false;
            }

            string normalizedNumber = TKSipExt_Routing.NormalizeExternalNumber(calledNumber);
            string dialNumber = NormalizeGsmDialNumber(normalizedNumber);
            LogDialNormalization(_logger, preferredSessionId, calledNumber, dialNumber);
            if (string.IsNullOrWhiteSpace(normalizedNumber) || string.IsNullOrWhiteSpace(dialNumber)) {
                reason = "Keine wählbare Zielrufnummer.";
                return false;
            }

            string newSessionId = !string.IsNullOrWhiteSpace(preferredSessionId) ? preferredSessionId!.Trim() : string.Empty;
            SIP_ServerSession serverSession = sessionManager.UpsertGsmMonitorSession(
                newSessionId,
                "Outbound",
                fromNumber,
                dialNumber,
                route.GsmStatusId ?? monitor.StatusId,
                route.GsmModemId,
                route.GsmSimId,
                route.GsmProviderName,
                route.GsmImei,
                route.GsmComPortAT,
                route.GsmComPortAudio,
                null,
                null,
                null);

            newSessionId = serverSession.SessionId;

            lock (serverSession.SyncRoot) {
                serverSession.Status = "Dialing";
                serverSession.GsmRouteKind = route.TargetKind;
                serverSession.GsmRouteReason = route.GsmRouteReason ?? string.Empty;
                serverSession.GsmMappedAt = DateTime.Now;
                serverSession.UpdatedAt = DateTime.Now;
            }

            TKGSM_Session session = TKGSM_Session.FromServerSession(serverSession);

            lock (monitor.PortSync) {
                monitor.PendingCallerNumber = fromNumber ?? string.Empty;
                monitor.ActiveSessionId = newSessionId;
                try {
                    SendMonitorCommand(monitor, monitor.Port, "AT");
                    SendMonitorCommand(monitor, monitor.Port, "ATE0");
                    SendMonitorCommand(monitor, monitor.Port, "AT+CLIP=1");
                    SendMonitorCommand(monitor, monitor.Port, "AT+CRC=1");
                    string raw = SendMonitorCommandWithResponse(monitor, monitor.Port, $"ATD{dialNumber};", 1500);
                    session.LastEventLine = raw ?? string.Empty;
                    _logger?.Debug($"GSM_ServiceHost Outbound Dial | SessionID={newSessionId} | StatusID={monitor.StatusId} | Von={fromNumber} | An={dialNumber} | RAW=[{raw}]");
                } catch (Exception ex) {
                    monitor.ActiveSessionId = string.Empty;
                    reason = ex.Message;
                    sessionManager.SetGsmEnded(newSessionId, "DialFailed");
                    _logger?.Error($"GSM_ServiceHost Outbound Dial fehlgeschlagen | SessionID={newSessionId} | StatusID={monitor.StatusId} | Fehler={ex.Message}");
                    return false;
                }
            }

            sessionId = newSessionId;
            RaiseCallStateChanged(monitor, session, "Trying", session.LastEventLine);
            return true;
        }

        /// <summary>
        /// The HangupCall
        /// </summary>
        /// <param name="statusId">The statusId<see cref="int"/></param>
        /// <param name="sessionId">The sessionId<see cref="string?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public static bool HangupCall(int statusId, string? sessionId = null) {
            if (!_runtimeMonitors.TryGetValue(statusId, out ModemRuntimeMonitor? monitor) || monitor == null || monitor.Port == null || !monitor.Port.IsOpen) {
                return false;
            }

            string hangupCommand = "AT+CHUP";
            string hangupDetail = string.Empty;
            bool hangupSucceeded;

            lock (monitor.PortSync) {
                try {
                    string chupRaw = SendMonitorCommandWithTerminalResponse(monitor, monitor.Port, "AT+CHUP", 1500, throwOnError: false);
                    hangupDetail = chupRaw;

                    hangupCommand = "AT+CLCC";
                    string clccAfterChup = SendMonitorCommandWithTerminalResponse(monitor, monitor.Port, "AT+CLCC", 1500, throwOnError: false);
                    hangupDetail = clccAfterChup;

                    if (!ContainsClccLine(clccAfterChup)) {
                        hangupSucceeded = true;
                    } else {
                        hangupCommand = "ATH";
                        SendMonitorCommandWithTerminalResponse(monitor, monitor.Port, "ATH", 1500, throwOnError: false);

                        hangupCommand = "AT+CLCC";
                        string clccAfterAth = SendMonitorCommandWithTerminalResponse(monitor, monitor.Port, "AT+CLCC", 1500, throwOnError: false);
                        hangupDetail = clccAfterAth;
                        hangupSucceeded = !ContainsClccLine(clccAfterAth);
                    }
                } catch (Exception ex) {
                    _logger?.Warn($"GSM_ServiceHost Hangup Ausnahme | StatusID={monitor.StatusId} | AT={monitor.ComPortAT} | Command={hangupCommand} | Fehler={ex.Message}");
                    return false;
                }
            }

            if (!hangupSucceeded) {
                string compactDetail = CompactSingleLine(hangupDetail);
                _logger?.Warn($"GSM_ServiceHost Hangup fehlgeschlagen | StatusID={monitor.StatusId} | AT={monitor.ComPortAT} | Command={hangupCommand} | Detail={compactDetail}");
                return false;
            }

            string effectiveSessionId = !string.IsNullOrWhiteSpace(sessionId) ? sessionId! : monitor.ActiveSessionId;
            if (!string.IsNullOrWhiteSpace(effectiveSessionId)) {
                TryReleaseAudioRoute(statusId);
                _sessionManager?.SetGsmEnded(effectiveSessionId, "Hangup", hangupCommand);
                if (_sessionManager != null && _sessionManager.TryGet(effectiveSessionId, out SIP_ServerSession? endedSession) && endedSession != null) {
                    RaiseCallStateChanged(monitor, TKGSM_Session.FromServerSession(endedSession), "Hangup", hangupCommand);
                }
            }

            monitor.ActiveSessionId = string.Empty;
            monitor.PendingCallerNumber = string.Empty;
            return true;
        }

        /// <summary>
        /// The AnswerCall
        /// </summary>
        /// <param name="statusId">The statusId<see cref="int"/></param>
        /// <param name="sessionId">The sessionId<see cref="string?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public static bool AnswerCall(int statusId, string? sessionId = null) {
            if (!_runtimeMonitors.TryGetValue(statusId, out ModemRuntimeMonitor? monitor) || monitor == null || monitor.Port == null || !monitor.Port.IsOpen) {
                return false;
            }

            string raw;
            lock (monitor.PortSync) {
                try {
                    raw = SendMonitorCommandWithTerminalResponse(monitor, monitor.Port, "ATA", 6000, throwOnError: false);
                } catch (Exception ex) {
                    _logger?.Warn($"GSM_ServiceHost Answer Ausnahme | StatusID={monitor.StatusId} | AT={monitor.ComPortAT} | Fehler={ex.Message}");
                    return false;
                }
            }

            bool ok = raw.Contains("OK", StringComparison.OrdinalIgnoreCase)
                || raw.Contains("VOICE CALL: BEGIN", StringComparison.OrdinalIgnoreCase)
                || raw.Contains("+CLCC:", StringComparison.OrdinalIgnoreCase);
            if (!ok) {
                _logger?.Warn($"GSM_ServiceHost Answer nicht bestaetigt | StatusID={monitor.StatusId} | AT={monitor.ComPortAT} | Antwort={CompactSingleLine(raw)}");
                return false;
            }

            string effectiveSessionId = !string.IsNullOrWhiteSpace(sessionId) ? sessionId! : monitor.ActiveSessionId;
            if (!string.IsNullOrWhiteSpace(effectiveSessionId)) {
                monitor.ActiveSessionId = effectiveSessionId;
                _sessionManager?.MarkGsmLegConnected(effectiveSessionId);
                if (_sessionManager != null && _sessionManager.TryGet(effectiveSessionId, out SIP_ServerSession? answeredSession) && answeredSession != null) {
                    RaiseCallStateChanged(monitor, TKGSM_Session.FromServerSession(answeredSession), "Answered", "ATA");
                }
            }

            return true;
        }

        /// <summary>
        /// The TryActivateAudioRoute
        /// </summary>
        /// <param name="statusId">The statusId<see cref="int"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public static bool TryActivateAudioRoute(int statusId) {
            if (!_runtimeMonitors.TryGetValue(statusId, out ModemRuntimeMonitor? monitor) || monitor == null) {
                _logger?.Warn($"GSM_ServiceHost AudioRoute nicht moeglich | StatusID={statusId} | Grund=Monitor nicht aktiv");
                return false;
            }

            if (monitor.Port == null || !monitor.Port.IsOpen) {
                _logger?.Warn($"GSM_ServiceHost AudioRoute nicht moeglich | StatusID={statusId} | AT={monitor.ComPortAT} | Grund=AT-Port nicht offen");
                return false;
            }

            lock (monitor.PortSync) {
                if (monitor.AudioRouteActive) {
                    return true;
                }

                try {
                    EnsurePcm16KFraming(monitor, monitor.Port);
                    LogGsmMediaProfile(monitor, "AUDIO-ROUTE-PREPARE", "Command=AT+CPCMREG=1,0 | RouteTarget=Modem->PCM | AudioPort=" + (string.IsNullOrWhiteSpace(monitor.ComPortAudio) ? "-" : monitor.ComPortAudio));

                    for (int attempt = 1; attempt <= 2; attempt++) {
                        string raw = SendMonitorCommandWithTerminalResponse(monitor, monitor.Port, "AT+CPCMREG=1,0", attempt == 1 ? 3000 : 5000, throwOnError: false);
                        if (string.IsNullOrWhiteSpace(raw)) {
                            Thread.Sleep(250);
                            raw = SendMonitorCommandWithResponse(monitor, monitor.Port, "AT+CPCMREG=1,0", 1500);
                        }

                        bool ok = raw.Contains("OK", StringComparison.OrdinalIgnoreCase);
                        if (ok) {
                            monitor.AudioRouteActive = true;
                            _logger?.Info($"GSM_ServiceHost AudioRoute aktiv | StatusID={monitor.StatusId} | AT={monitor.ComPortAT} | Audio={monitor.ComPortAudio} | NMEA={monitor.ComPortNMEA} | Versuch={attempt}");
                            LogGsmMediaProfile(monitor, "AUDIO-ROUTE-ACTIVE", "Command=AT+CPCMREG=1,0 | AudioPort=" + (string.IsNullOrWhiteSpace(monitor.ComPortAudio) ? "-" : monitor.ComPortAudio));
                            return true;
                        }

                        _logger?.Warn($"GSM_ServiceHost AudioRoute nicht bestaetigt | StatusID={monitor.StatusId} | AT={monitor.ComPortAT} | Versuch={attempt} | Antwort={CompactSingleLine(raw)}");
                        Thread.Sleep(250);
                    }

                    return false;
                } catch (Exception ex) {
                    _logger?.Warn($"GSM_ServiceHost AudioRoute Fehler | StatusID={monitor.StatusId} | Fehler={ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// The TryReleaseAudioRoute
        /// </summary>
        /// <param name="statusId">The statusId<see cref="int"/></param>
        public static void TryReleaseAudioRoute(int statusId) {
            if (!_runtimeMonitors.TryGetValue(statusId, out ModemRuntimeMonitor? monitor) || monitor == null || monitor.Port == null || !monitor.Port.IsOpen) {
                return;
            }

            lock (monitor.PortSync) {
                if (!monitor.AudioRouteActive) {
                    return;
                }

                try {
                    SendMonitorCommandWithResponse(monitor, monitor.Port, "AT+CPCMREG=0,1", 2000);
                } catch {
                }

                monitor.AudioRouteActive = false;
                _logger?.Info($"GSM_ServiceHost AudioRoute freigegeben | StatusID={monitor.StatusId} | AT={monitor.ComPortAT}");
                LogGsmMediaProfile(monitor, "AUDIO-ROUTE-RELEASE", "Command=AT+CPCMREG=0,1 | AudioPort=" + (string.IsNullOrWhiteSpace(monitor.ComPortAudio) ? "-" : monitor.ComPortAudio));
            }
        }

        /// <summary>
        /// The LogGsmMediaProfile
        /// </summary>
        /// <param name="monitor">The monitor<see cref="ModemRuntimeMonitor"/></param>
        /// <param name="phase">The phase<see cref="string"/></param>
        /// <param name="details">The details<see cref="string"/></param>
        private static void LogGsmMediaProfile(ModemRuntimeMonitor monitor, string phase, string details) {
            string context = BuildGsmLegContext(monitor);
            _logger?.Info($"GSM MEDIA PROFILE | {context} | Phase={phase} | {details}");
        }

        /// <summary>
        /// The ProcessInlineCommandResponse
        /// </summary>
        /// <param name="monitor">The monitor<see cref="ModemRuntimeMonitor"/></param>
        /// <param name="raw">The raw<see cref="string"/></param>
        /// <param name="command">The command<see cref="string"/></param>
        private static void ProcessInlineCommandResponse(ModemRuntimeMonitor monitor, string raw, string command) {
            if (string.IsNullOrWhiteSpace(raw)) {
                return;
            }

            string normalizedRaw = (raw ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n");
            string[] parts = normalizedRaw.Split('\n');
            bool hasClccLine = false;

            foreach (string part in parts) {
                string line = (part ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(line)) {
                    continue;
                }

                if (line.Equals(command, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                if (line.StartsWith("AT", StringComparison.OrdinalIgnoreCase) &&
                    line.Equals(command, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                if (line.StartsWith("+CLCC:", StringComparison.OrdinalIgnoreCase)) {
                    hasClccLine = true;
                }

                HandleMonitorLine(monitor, line);
            }

            if (command.Equals("AT+CLCC", StringComparison.OrdinalIgnoreCase) && !hasClccLine) {
                HandleClccNoActiveCall(monitor);
            }
        }

        /// <summary>
        /// The HandleClccNoActiveCall
        /// </summary>
        /// <param name="monitor">The monitor<see cref="ModemRuntimeMonitor"/></param>
        private static void HandleClccNoActiveCall(ModemRuntimeMonitor monitor) {
            if (string.IsNullOrWhiteSpace(monitor.ActiveSessionId)) {
                monitor.PendingLastClccState = string.Empty;
                return;
            }

            if (_sessionManager == null || !_sessionManager.TryGet(monitor.ActiveSessionId, out SIP_ServerSession? activeServerSession) || activeServerSession == null) {
                monitor.ActiveSessionId = string.Empty;
                monitor.PendingCallerNumber = string.Empty;
                monitor.PendingConnectedNumber = string.Empty;
                monitor.PendingExtendedCause = string.Empty;
                monitor.PendingLastClccState = string.Empty;
                return;
            }

            string finalState = activeServerSession.IsAnswered ||
                string.Equals(activeServerSession.Status, "Answered", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(activeServerSession.Status, "Connected", StringComparison.OrdinalIgnoreCase)
                ? "Ended"
                : "Cancelled";

            if (!activeServerSession.IsAnswered &&
                !string.Equals(activeServerSession.Status, "Answered", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(activeServerSession.Status, "Connected", StringComparison.OrdinalIgnoreCase)) {
                string originalNumber = activeServerSession.ToNumber ?? string.Empty;
                string normalizedNumber = TKSipExt_Routing.NormalizeExternalNumber(originalNumber);
                string dialNumber = NormalizeGsmDialNumber(normalizedNumber);
                LogTimeoutDiagnostics(monitor.Port, _logger, activeServerSession.SessionId, originalNumber, dialNumber);
            }

            _sessionManager.ApplySignalStateToExistingSession(activeServerSession, finalState, "AT+CLCC: no active call");
            RaiseCallStateChanged(monitor, TKGSM_Session.FromServerSession(activeServerSession), finalState, "AT+CLCC: no active call");

            monitor.ActiveSessionId = string.Empty;
            monitor.PendingCallerNumber = string.Empty;
            monitor.PendingConnectedNumber = string.Empty;
            monitor.PendingExtendedCause = string.Empty;
            monitor.PendingLastClccState = string.Empty;
        }

        /// <summary>
        /// The MapClccToCoreState
        /// </summary>
        /// <param name="status">The status<see cref="int?"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string MapClccToCoreState(int? status) {
            return status switch {
                0 => "Answered",
                2 => "Trying",
                3 => "Ringing",
                4 => "Ringing",
                5 => "Ringing",
                _ => string.Empty
            };
        }

        /// <summary>
        /// The ResolveFinalCoreState
        /// </summary>
        /// <param name="monitor">The monitor<see cref="ModemRuntimeMonitor"/></param>
        /// <param name="activeSession">The activeSession<see cref="SIP_ServerSession?"/></param>
        /// <param name="signal">The signal<see cref="GsmAtSignal"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string ResolveFinalCoreState(ModemRuntimeMonitor monitor, SIP_ServerSession? activeSession, GsmAtSignal signal) {
            string state = signal.CoreState;
            bool wasAnswered = activeSession?.IsAnswered == true ||
                string.Equals(activeSession?.Status, "Answered", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(activeSession?.Status, "Connected", StringComparison.OrdinalIgnoreCase);
            bool isInbound = string.Equals(activeSession?.GsmDirection, "Inbound", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(monitor.ActiveSessionId) && string.IsNullOrWhiteSpace(activeSession?.GsmDirection));

            if (signal.Kind == GsmAtSignalKind.NoAnswer) {
                if (!wasAnswered) {
                    state = isInbound ? "Cancelled" : "Failed";
                }
            } else if (signal.Kind == GsmAtSignalKind.NoCarrier) {
                state = wasAnswered ? "Ended" : (isInbound ? "Cancelled" : "Failed");
            } else if (signal.Kind == GsmAtSignalKind.Error || signal.Kind == GsmAtSignalKind.NoDialTone) {
                state = "Failed";
            }

            return string.IsNullOrWhiteSpace(state) ? "Ended" : state;
        }

        /// <summary>
        /// The BuildFinalReason
        /// </summary>
        /// <param name="signal">The signal<see cref="GsmAtSignal"/></param>
        /// <param name="extendedCause">The extendedCause<see cref="string?"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string BuildFinalReason(GsmAtSignal signal, string? extendedCause) {
            string reason = string.IsNullOrWhiteSpace(signal.CauseText)
                ? signal.RawLine
                : signal.CauseText;
            if (!string.IsNullOrWhiteSpace(extendedCause)) {
                if (string.IsNullOrWhiteSpace(reason)) {
                    reason = extendedCause.Trim();
                } else if (!reason.Contains(extendedCause.Trim(), StringComparison.OrdinalIgnoreCase)) {
                    reason += " | " + extendedCause.Trim();
                }
            }
            return string.IsNullOrWhiteSpace(reason) ? signal.RawLine : reason;
        }

        /// <summary>
        /// The RaiseCallStateChanged
        /// </summary>
        /// <param name="monitor">The monitor<see cref="ModemRuntimeMonitor"/></param>
        /// <param name="session">The session<see cref="TKGSM_Session"/></param>
        /// <param name="state">The state<see cref="string"/></param>
        /// <param name="rawLine">The rawLine<see cref="string?"/></param>
        private static void RaiseCallStateChanged(ModemRuntimeMonitor monitor, TKGSM_Session session, string state, string? rawLine) {
            try {
                CallStateChanged?.Invoke(new GsmCallStateChangedEventArgs {
                    SessionId = session.SessionId ?? string.Empty,
                    StatusId = monitor.StatusId,
                    State = state ?? string.Empty,
                    RawLine = rawLine ?? string.Empty,
                    Direction = session.Direction ?? string.Empty,
                    FromNumber = session.SourceNumber ?? string.Empty,
                    ToNumber = session.TargetNumber ?? string.Empty,
                    ModemId = monitor.ModemId,
                    SimId = monitor.SimId
                });
            } catch {
            }
        }

        /// <summary>
        /// The LogTimeoutDiagnostics
        /// </summary>
        /// <param name="modemPort">The modemPort<see cref="SerialPort?"/></param>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        /// <param name="sessionId">The sessionId<see cref="string?"/></param>
        /// <param name="originalNumber">The originalNumber<see cref="string?"/></param>
        /// <param name="normalizedNumber">The normalizedNumber<see cref="string?"/></param>
        public static void LogTimeoutDiagnostics(
     SerialPort? modemPort,
     TKBase_LogManager? logger,
     string? sessionId,
     string? originalNumber,
     string? normalizedNumber) {

            string ceer = SafeAt(modemPort, "AT+CEER", 1000);
            string cpas = SafeAt(modemPort, "AT+CPAS", 1000);
            string csq = SafeAt(modemPort, "AT+CSQ", 1000);
            string cops = SafeAt(modemPort, "AT+COPS?", 1500);
            string creg = SafeAt(modemPort, "AT+CREG?", 1000);
            string cgreg = SafeAt(modemPort, "AT+CGREG?", 1000);
            string cpsms = SafeAt(modemPort, "AT+CPSMS?", 1000);

            logger?.Warn(
                $"GSM Outbound Diagnose | SessionID={sessionId} | Original={originalNumber} | Dial={normalizedNumber} | CEER=[{ceer}] | CPAS=[{cpas}] | CSQ=[{csq}] | COPS=[{cops}] | CREG=[{creg}] | CGREG=[{cgreg}] | CPSMS=[{cpsms}]");
        }

        /// <summary>
        /// The SafeAt
        /// </summary>
        /// <param name="port">The port<see cref="SerialPort?"/></param>
        /// <param name="command">The command<see cref="string"/></param>
        /// <param name="timeoutMs">The timeoutMs<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string SafeAt(SerialPort? port, string command, int timeoutMs) {
            if (port == null) {
                return "PORT=NULL";
            }

            if (!port.IsOpen) {
                return "PORT=CLOSED";
            }

            lock (port) {
                try {
                    int oldReadTimeout = port.ReadTimeout;
                    int oldWriteTimeout = port.WriteTimeout;
                    bool oldDtr = port.DtrEnable;
                    bool oldRts = port.RtsEnable;

                    try {
                        port.ReadTimeout = Math.Max(50, timeoutMs);
                        port.WriteTimeout = Math.Max(250, timeoutMs);

                        try { port.DiscardInBuffer(); } catch { }
                        try { port.DiscardOutBuffer(); } catch { }

                        port.DtrEnable = true;
                        port.RtsEnable = true;

                        port.Write(command + "\r");

                        DateTime until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                        StringBuilder sb = new StringBuilder();

                        while (DateTime.UtcNow < until) {
                            try {
                                string chunk = port.ReadExisting();
                                if (!string.IsNullOrEmpty(chunk)) {
                                    sb.Append(chunk);

                                    string text = sb.ToString();
                                    if (text.Contains("\r\nOK\r\n", StringComparison.Ordinal) ||
                                        text.Contains("\r\nERROR\r\n", StringComparison.Ordinal) ||
                                        text.Contains("OK\r\n", StringComparison.Ordinal) ||
                                        text.Contains("ERROR\r\n", StringComparison.Ordinal)) {
                                        break;
                                    }
                                }
                            } catch {
                            }

                            Thread.Sleep(20);
                        }

                        string result = sb.ToString().Trim();
                        return string.IsNullOrWhiteSpace(result) ? "NO_RESPONSE" : result.Replace("\r", "\\r").Replace("\n", "\\n");
                    } finally {
                        try { port.ReadTimeout = oldReadTimeout; } catch { }
                        try { port.WriteTimeout = oldWriteTimeout; } catch { }
                        try { port.DtrEnable = oldDtr; } catch { }
                        try { port.RtsEnable = oldRts; } catch { }
                    }
                } catch (Exception ex) {
                    return "EX=" + ex.Message;
                }
            }
        }
    }
}
