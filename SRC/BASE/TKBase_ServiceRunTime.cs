namespace TKServer.BASE {
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;
    using System.IO.Pipes;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Threading;
    using System.ServiceProcess;
    using System.Security.Principal;

    using TKServer.SipExt;
    using TKServer.SQL;
    // ============================================================================
    // TKBase_ServiceRunTime
    // ----------------------------------------------------------------------------
    // Service-Laufzeit des TK-Servers.
    //
    // Diese Datei ist fuer den Windows-Service- bzw. -startsvc-Betrieb zustaendig.
    // Nur diese Laufzeit besitzt die echte Server-/Telefonie-Runtime.
    //
    // Sie oeffnet, haelt und schliesst SIP-, RTP-, Provider-, GSM- und Call-Ressourcen.
    // Die GUI darf diese Ressourcen nicht direkt oeffnen oder besitzen.
    //
    // Die GUI setzt Sollzustaende in der DB und stoesst den Service an.
    // Der Service liest die DB, setzt die Runtime um und schreibt Istzustaende zurueck.
    // ============================================================================
    public sealed class TKBase_ServiceRunTime : IDisposable {
        private readonly object _sync = new();
        private readonly TKBase_LogManager _logger;
        private bool _disposed;
        private bool _started;
        private CancellationTokenSource? _serviceCts;
        private TKBase_Server? _sipServer;
        private RuntimeLoadOptions _activeOptions = RuntimeLoadOptions.None;

        public TKBase_ServiceRunTime(TKBase_LogManager? logger = null) {
            _logger = logger ?? CreateFileLogger();
            _logger.TryConfigureFromDatabase();
        }

        public bool IsStarted {
            get {
                lock (_sync) {
                    return _started;
                }
            }
        }

        /// <summary>
        /// Startet alle Bereiche. Entspricht -load all.
        /// </summary>
        public void StartAll() {
            Start(RuntimeLoadOptions.All);
        }

        /// <summary>
        /// Startet nur die angeforderten Bereiche.
        /// </summary>
        /// <param name="options">Runtime-Ladeoptionen.</param>
        public void Start(RuntimeLoadOptions options) {
            ThrowIfDisposed();
            ApplyRuntimeOptions(options, "Start");
        }

        /// <summary>
        /// Wendet einen neuen Sollzustand an, ohne unverÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¤nderte Bereiche unnÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¶tig zu stoppen.
        /// </summary>
        /// <param name="options">GewÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¼nschte Runtime-Ladeoptionen.</param>
        /// <param name="reason">Grund fÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¼r die ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾nderung.</param>
        public void ApplyRuntimeOptions(RuntimeLoadOptions options, string reason) {
            ThrowIfDisposed();

            options = options.Normalize();

            if ((options.AutoDetect || !options.IsNone)
                && !TKSQL_ConnectionClass.TryEnsureDatabaseAvailable(_logger, $"ServiceRuntime Sollzustand anwenden | Grund={reason}", false)) {
                _logger.Error($"ServiceRuntime Sollzustand nicht angewendet | Grund={reason} | SQL-Datenbank nicht erreichbar | Runtime-Bereiche werden nicht gestartet.");
                return;
            }

            if (options.AutoDetect) {
                options = ResolveAutoStartOptions().Normalize();
            }

            RuntimeLoadOptions previous;
            bool wasStarted;

            lock (_sync) {
                previous = _activeOptions.Normalize();
                wasStarted = _started;

                if (!wasStarted && _serviceCts == null) {
                    _serviceCts = new CancellationTokenSource();
                }
            }

            if (options.SameRuntimeAs(previous) && wasStarted == !options.IsNone) {
                return;
            }

            _logger.Info($"ServiceRuntime Sollzustand anwenden | Grund={reason} | Alt={previous.ToLoadArgument()} | Neu={options.ToLoadArgument()}");

            try {
                if (!wasStarted && !options.IsNone) {
                    ResetVolatileRuntimeState("Start");
                }

                if (previous.StartProviderRegistration && !options.StartProviderRegistration) {
                    TrySetRuntimeAreaActualState("Provider", "Stopping");
                    StopProviderRuntime();
                    TrySetRuntimeAreaActualState("Provider", "Stopped");
                }

                if (previous.StartGsm && !options.StartGsm) {
                    TrySetRuntimeAreaActualState("GSM", "Stopping");
                    StopGsmRuntime();
                    TrySetRuntimeAreaActualState("GSM", "Stopped");
                }

                if (previous.StartSipCore && options.StartSipCore && previous.StartSipExt && !options.StartSipExt) {
                    ConfigureSipRuntimeAreas(options);
                    _logger.Info("ServiceRuntime Bereich gestoppt | Bereich=SIPExt | SIP-Kern bleibt fuer andere Bereiche aktiv.");
                }

                if (previous.StartSipCore && !options.StartSipCore) {
                    TrySetSipCoreActualState(previous, "Stopping");
                    StopSipRuntime();
                    TrySetSipCoreActualState(previous, "Stopped");
                }
                if (previous.StartSipCore && options.StartSipCore && previous.StartSipInt && !options.StartSipInt) {
                    TrySetRuntimeAreaActualState("SIPInt", "Stopping");
                    int closedSipInt = StopAffectedSessions("SIPInt", "StopSipInt");
                    ConfigureSipRuntimeAreas(options);
                    TrySetRuntimeAreaActualState("SIPInt", "Stopped");
                    _logger.Info($"ServiceRuntime Bereich gestoppt | Bereich=SIPInt | BetroffeneSessions={closedSipInt} | SIP-Kern bleibt fuer andere Bereiche aktiv.");
                }

                if (!previous.StartSipCore && options.StartSipCore) {
                    TrySetSipCoreActualState(options, "Starting");
                    StartSipRuntime(options);
                    ConfigureSipRuntimeAreas(options);
                    TrySetSipCoreActualState(options, "Running");
                }

                if (previous.StartSipCore && options.StartSipCore && !previous.StartSipInt && options.StartSipInt) {
                    ConfigureSipRuntimeAreas(options);
                    TrySetRuntimeAreaActualState("SIPInt", "Running");
                    _logger.Info("ServiceRuntime Bereich gestartet | Bereich=SIPInt | SIP-Kern war bereits aktiv.");
                }
                if (previous.StartSipCore && options.StartSipCore && !previous.StartSipExt && options.StartSipExt) {
                    ConfigureSipRuntimeAreas(options);
                    _logger.Info("ServiceRuntime Bereich gestartet | Bereich=SIPExt | SIP-Kern war bereits aktiv.");
                }

                if (!previous.StartProviderRegistration && options.StartProviderRegistration) {
                    TrySetRuntimeAreaActualState("Provider", "Starting");
                    StartProviderRuntime();
                    TrySetRuntimeAreaActualState("Provider", "Running");
                }

                if (!previous.StartGsm && options.StartGsm) {
                    TrySetRuntimeAreaActualState("GSM", "Starting");
                    StartGsmRuntime();
                    TrySetRuntimeAreaActualState("GSM", "Running");
                }

                lock (_sync) {
                    _activeOptions = options;
                    _started = !options.IsNone;
                }

                if (options.IsNone) {
                    ResetVolatileRuntimeState("Stop");
                }

                _logger.Info($"ServiceRuntime Sollzustand angewendet | Load={options.ToLoadArgument()}");
            } catch (Exception ex) {
                _logger.Error($"ServiceRuntime Sollzustand fehlgeschlagen | Grund={reason} | Fehler={ex}");
                StopAll();
                TrySetFailedAreas(options, ex.Message);
                throw;
            }
        }

        public void StopAll() {
            RuntimeLoadOptions options;

            lock (_sync) {
                if (!_started && _serviceCts == null) {
                    return;
                }

                _started = false;
                options = _activeOptions;
                _serviceCts?.Cancel();
            }

            _logger.Info("ServiceRuntime Stop angefordert.");

            if (options.StartProviderRegistration) {
                TrySetRuntimeAreaActualState("Provider", "Stopping");
                StopProviderRuntime();
                TrySetRuntimeAreaActualState("Provider", "Stopped");
            }

            if (options.StartGsm) {
                TrySetRuntimeAreaActualState("GSM", "Stopping");
                StopGsmRuntime();
                TrySetRuntimeAreaActualState("GSM", "Stopped");
            }

            if (options.StartSipCore) {
                TrySetSipCoreActualState(options, "Stopping");
                StopSipRuntime();
                TrySetSipCoreActualState(options, "Stopped");
            }

            ResetVolatileRuntimeState("Stop");

            lock (_sync) {
                _serviceCts?.Dispose();
                _serviceCts = null;
                _activeOptions = RuntimeLoadOptions.None;
            }

            _logger.Info("ServiceRuntime Stop abgeschlossen.");
        }

        /// <summary>
        /// Ermittelt fuer -startsvc die zu startenden Bereiche aus dem zentralen DB-Sollzustand.
        /// Vorhandene Benutzer, Provider und GSM-GerÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¤te werden nur zur Protokollierung und zur Vermeidung leerer Provider-/GSM-Starts geprÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¼ft.
        /// </summary>
        private RuntimeLoadOptions ResolveAutoStartOptions() {
            TKSQL_Commands sql = new(_logger);
            return ResolveDesiredStartOptions(sql, _logger);
        }

        /// <summary>
        /// Ermittelt Runtime-Optionen aus dem gespeicherten Sollzustand.
        /// </summary>
        internal static RuntimeLoadOptions ResolveDesiredStartOptions(TKSQL_Commands sql, TKBase_LogManager? logger) {
            int activeUserCount = 0;
            bool hasProviderAccounts = false;
            bool hasGsmDeviceWithSim = false;
            (bool SipRuntimeEnabled, bool ProviderRegistrationEnabled, bool GsmRuntimeEnabled) desired = (false, false, false);

            if (!TKSQL_ConnectionClass.TryEnsureDatabaseAvailable(logger, "ServiceRuntime Sollzustand auswerten", false)) {
                return RuntimeLoadOptions.None;
            }

            try {
                desired = sql.GetRuntimeDesiredState();
            } catch (Exception ex) {
                logger?.Warn($"ServiceRuntime Sollzustand konnte nicht gelesen werden | Fehler={ex.Message}");
            }

            try {
                activeUserCount = sql.GetActiveSipUsers().Count;
            } catch (Exception ex) {
                logger?.Warn($"ServiceRuntime AutoStart BenutzerprÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¼fung fehlgeschlagen | Fehler={ex.Message}");
            }

            try {
                hasProviderAccounts = sql.GetDefaultActiveProviderAccount() != null;
            } catch (Exception ex) {
                logger?.Warn($"ServiceRuntime AutoStart ProviderprÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¼fung fehlgeschlagen | Fehler={ex.Message}");
            }

            try {
                hasGsmDeviceWithSim = sql.GetActiveGsmRuntimeDevices().Any(HasInsertedSim);
            } catch (Exception ex) {
                logger?.Warn($"ServiceRuntime AutoStart GSM-PrÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¼fung fehlgeschlagen | Fehler={ex.Message}");
            }

            bool hasRunnableSipUsers = activeUserCount > 0;
            bool shouldStartSipRuntime = desired.SipRuntimeEnabled && hasRunnableSipUsers;
            bool shouldStartProviderRegistration = desired.ProviderRegistrationEnabled && hasProviderAccounts;
            bool shouldStartGsmRuntime = desired.GsmRuntimeEnabled && hasGsmDeviceWithSim;
            RuntimeLoadOptions resolved = new RuntimeLoadOptions {
                StartSipInt = shouldStartSipRuntime,
                StartSipExt = shouldStartProviderRegistration,
                StartProviderRegistration = shouldStartProviderRegistration,
                StartGsm = shouldStartGsmRuntime
            }.Normalize();


            return resolved;
        }

        /// <summary>
        /// PrÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¼ft, ob ein GSM-Runtime-Datensatz eine eingelegte SIM beschreibt.
        /// </summary>
        private static bool HasInsertedSim(DataRow row) {
            if (row == null) {
                return false;
            }

            bool simInserted = row.Table.Columns.Contains("IsSimInserted")
                && row["IsSimInserted"] != DBNull.Value
                && Convert.ToBoolean(row["IsSimInserted"]);

            bool simAssigned = row.Table.Columns.Contains("SimID")
                && row["SimID"] != DBNull.Value
                && Convert.ToInt32(row["SimID"]) > 0;

            return simInserted || simAssigned;
        }

        private void StartSipRuntime(RuntimeLoadOptions options) {
            TKBase_Server? failedServer = null;

            lock (_sync) {
                if (_sipServer == null) {
                    _sipServer = new TKBase_Server(_logger);
                    _sipServer.ServerStatusChanged += message => _logger.Info($"ServiceRuntime SIP-Status | {message}");
                }

                if (_sipServer.IsStarted) {
                    _logger.Info($"ServiceRuntime SIP-Kern laeuft bereits ({_sipServer.ListenTransport}/{_sipServer.ListenPort}).");
                    return;
                }

                _sipServer.ConfigureRuntimeAreas(options.StartSipInt, options.StartSipExt);

                try {
                    _sipServer.Start();
                } catch {
                    failedServer = _sipServer;
                    _sipServer = null;
                    throw;
                }
            }

            if (failedServer != null) {
                try {
                    failedServer.Dispose();
                } catch {
                    // Startfehler-Cleanup darf den Originalfehler nicht verdecken.
                }
            }

            _logger.Info($"ServiceRuntime Bereich gestartet | Bereich={options.ToLoadArgument()} | SIP-Kern aktiv.");
        }

        private void StartProviderRuntime() {
            TKSipExt_Registration.ConfigureProviderLogger(_logger);
            TKSipExt_Registration.StartProviderRegistration();
            _logger.Info("ServiceRuntime Bereich gestartet | Bereich=ProviderRegistration/SipExt | alle aktiven Provider-Konten werden registriert.");
        }

        private void StartGsmRuntime() {
            TKBase_GsmGateway.ConfigureLogger(_logger);
            TKBase_GsmGateway.Start();
            _logger.Info("ServiceRuntime Bereich gestartet | Bereich=GSM.");
        }

        private void StopProviderRuntime() {
            try {
                int affectedSessions = StopAffectedSessions("Provider", "StopProvider");
                TKSipExt_Registration.StopProviderRegistration();
                _logger.Info($"ServiceRuntime Bereich gestoppt | Bereich=ProviderRegistration/SipExt | BetroffeneSessions={affectedSessions}.");
            } catch (Exception ex) {
                _logger.Warn($"ServiceRuntime Stop Warnung | Bereich=ProviderRegistration/SipExt | Fehler={ex.Message}");
            }
        }

        private void StopGsmRuntime() {
            try {
                int affectedSessions = StopAffectedSessions("GSM", "StopGsm");
                TKBase_GsmGateway.Stop();
                _logger.Info($"ServiceRuntime Bereich gestoppt | Bereich=GSM | BetroffeneSessions={affectedSessions}.");
            } catch (Exception ex) {
                _logger.Warn($"ServiceRuntime Stop Warnung | Bereich=GSM | Fehler={ex.Message}");
            }
        }

        private void StopSipRuntime() {
            TKBase_Server? serverToStop = null;

            lock (_sync) {
                serverToStop = _sipServer;
                _sipServer = null;
            }

            if (serverToStop == null) {
                _logger.Info("ServiceRuntime Bereich bereits gestoppt | Bereich=SIP.");
                return;
            }

            try {
                serverToStop.Stop();
                serverToStop.Dispose();
                _logger.Info("ServiceRuntime Bereich gestoppt | Bereich=SIP.");
            } catch (Exception ex) {
                _logger.Warn($"ServiceRuntime Stop Warnung | Bereich=SIP | Fehler={ex.Message}");
            }
        }

        public Task<bool> HangupSessionAsync(string sessionId) {
            TKBase_Server? server;

            lock (_sync) {
                server = _sipServer;
            }

            if (server == null || string.IsNullOrWhiteSpace(sessionId)) {
                return Task.FromResult(false);
            }

            return server.HangupSessionAsync(sessionId);
        }

        private void ConfigureSipRuntimeAreas(RuntimeLoadOptions options) {
            TKBase_Server? server;

            lock (_sync) {
                server = _sipServer;
            }

            if (server == null) {
                return;
            }

            server.ConfigureRuntimeAreas(options.StartSipInt, options.StartSipExt);
        }

        private int StopAffectedSessions(string areaKey, string reason) {
            TKBase_Server? server;

            lock (_sync) {
                server = _sipServer;
            }

            if (server == null) {
                return 0;
            }

            try {
                return server.HangupAreaSessionsAsync(areaKey, reason).GetAwaiter().GetResult();
            } catch (Exception ex) {
                _logger.Warn($"ServiceRuntime Bereichs-Stop Session-Cleanup fehlgeschlagen | Bereich={areaKey} | Grund={reason} | Fehler={ex.Message}");
                return 0;
            }
        }

        private void TrySetSipCoreActualState(RuntimeLoadOptions options, string actualState) {
            if (options.StartSipInt) {
                TrySetRuntimeAreaActualState("SIPInt", actualState);
            }
        }

        private void TrySetFailedAreas(RuntimeLoadOptions options, string errorText) {
            if (options.StartSipInt) {
                TrySetRuntimeAreaActualState("SIPInt", "Error", errorText);
            }
            if (options.StartProviderRegistration) {
                TrySetRuntimeAreaActualState("Provider", "Error", errorText);
            }

            if (options.StartGsm) {
                TrySetRuntimeAreaActualState("GSM", "Error", errorText);
            }
        }

        private void TrySetRuntimeAreaActualState(string areaKey, string actualState, string? lastError = null) {
            if (!TKSQL_ConnectionClass.TryEnsureDatabaseAvailable(_logger, $"RuntimeAreaControl Istzustand schreiben | Bereich={areaKey} | Status={actualState}", false)) {
                return;
            }

            try {
                new TKSQL_Commands(_logger).SetRuntimeAreaActualState(areaKey, actualState, lastError);
            } catch (Exception ex) {
                _logger.Warn($"RuntimeAreaControl Istzustand konnte nicht geschrieben werden | Bereich={areaKey} | Status={actualState} | Fehler={ex.Message}");
            }
        }

        private void ResetVolatileRuntimeState(string phase) {
            if (!TKSQL_ConnectionClass.TryEnsureDatabaseAvailable(_logger, $"ServiceRuntime volatile Runtime-Daten bereinigen | Phase={phase}", false)) {
                _logger.Warn($"ServiceRuntime volatile Runtime-Daten nicht bereinigt | Phase={phase} | SQL-Datenbank nicht erreichbar.");
                return;
            }

            try {
                bool isStart = string.Equals(phase, "Start", StringComparison.OrdinalIgnoreCase);
                bool clearCalls = !isStart;
                bool clearRegistrations = !isStart;

                new TKSQL_Commands(_logger).ResetVolatileRuntimeState(clearCalls, clearRegistrations);
                _logger.Info($"ServiceRuntime volatile Runtime-Daten bereinigt | Phase={phase} | CallsGeloescht={clearCalls} | RegistrierungenGeloescht={clearRegistrations}");
            } catch (Exception ex) {
                _logger.Warn($"ServiceRuntime volatile Runtime-Daten konnten nicht bereinigt werden | Phase={phase} | Fehler={ex.Message}");
            }
        }

        private static TKBase_LogManager CreateFileLogger() {
            string logPath = Path.Combine(AppContext.BaseDirectory, "..", "log", "TK-Server.Service.log");
            return new TKBase_LogManager(null, logPath, LogOutputMode.Service);
        }

        private void ThrowIfDisposed() {
            if (_disposed) {
                throw new ObjectDisposedException(nameof(TKBase_ServiceRunTime));
            }
        }

        public void Dispose() {
            if (_disposed) {
                return;
            }

            StopAll();
            _disposed = true;
        }
    }


    /// <summary>
    /// Windows-Service-HÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¼lle fÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¼r den TK-Server. Die eigentliche Runtime bleibt im TKBase_ServiceRunTime.
    /// Der Service bleibt als Controller aktiv und startet/stoppt die Runtime nach DB-Sollzustand.
    /// </summary>
    internal sealed class TKWindowsService : ServiceBase {
        private const string ServiceControlPipeName = "TKServer_ServiceControl";
        private static readonly Encoding PipeEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private readonly RuntimeLoadOptions _loadOptions;
        private readonly object _runtimeSync = new();
        private readonly object _pipeWriteSync = new();
        private readonly TKBase_LogManager _logger;
        private TKBase_ServiceRunTime? _runtime;
        private System.Threading.Timer? _desiredStateTimer;
        private RuntimeLoadOptions? _appliedOptions;
        private bool _reconcileBusy;
        private bool _lastDatabaseAvailable;
        private CancellationTokenSource? _pipeCts;
        private Task? _pipeTask;
        private StreamWriter? _guiPipeWriter;
        private bool _guiOnline;
        private string? _guiIdentity;

        public TKWindowsService(RuntimeLoadOptions loadOptions) {
            ServiceName = "TK-Server";
            CanStop = true;
            CanShutdown = true;
            AutoLog = true;
            _loadOptions = loadOptions.Normalize();
            _logger = new TKBase_LogManager(null, Path.Combine(AppContext.BaseDirectory, "..", "log", "TK-Server.Service.log"), LogOutputMode.Service);
            _logger.TryConfigureFromDatabase();
        }

        protected override void OnStart(string[] args) {
            lock (_runtimeSync) {
                _runtime ??= new TKBase_ServiceRunTime(_logger);
                _appliedOptions = RuntimeLoadOptions.None;
            }

            StartServiceControlPipe();
            TKBase_LogManager.ServiceLogLineEmitted += ForwardServiceLogToGui;
            _desiredStateTimer = new System.Threading.Timer(_ => ReconcileDesiredRuntimeState("ServicePoll"), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3));
            _logger.Info("TK-WindowsService gestartet | Dauerhalter aktiv. Runtime wird aus dem vorhandenen DB-Sollzustand ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¼bernommen.");
        }

        protected override void OnStop() {
            _desiredStateTimer?.Dispose();
            _desiredStateTimer = null;
            TKBase_LogManager.ServiceLogLineEmitted -= ForwardServiceLogToGui;
            StopServiceControlPipe();

            lock (_runtimeSync) {
                _runtime?.StopAll();
                _runtime?.Dispose();
                _runtime = null;
                _appliedOptions = null;
            }

            _logger.Info("TK-WindowsService gestoppt.");
        }

        protected override void OnShutdown() {
            OnStop();
            base.OnShutdown();
        }

        private void StartServiceControlPipe() {
            if (_pipeCts != null) {
                return;
            }

            _pipeCts = new CancellationTokenSource();
            _pipeTask = Task.Run(() => ListenForServiceCommandsAsync(_pipeCts.Token));
            _logger.Info($"ServiceControlPipe gestartet | Pipe={ServiceControlPipeName}");
        }

        private void StopServiceControlPipe() {
            try { _pipeCts?.Cancel(); } catch { }

            lock (_pipeWriteSync) {
                _guiOnline = false;
                _guiIdentity = null;
                try { _guiPipeWriter?.Dispose(); } catch { }
                _guiPipeWriter = null;
            }

            try { _pipeTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
            try { _pipeCts?.Dispose(); } catch { }
            _pipeTask = null;
            _pipeCts = null;
        }

        private async Task ListenForServiceCommandsAsync(CancellationToken token) {
            while (!token.IsCancellationRequested) {
                NamedPipeServerStream? pipe = null;

                try {
                    pipe = CreateServiceControlPipe();
                    await pipe.WaitForConnectionAsync(token).ConfigureAwait(false);

                    NamedPipeServerStream connectedPipe = pipe;
                    pipe = null;

                    _ = Task.Run(() => HandleServicePipeClientAsync(connectedPipe, token), token);
                } catch (OperationCanceledException) {
                    try { pipe?.Dispose(); } catch { }
                    break;
                } catch (Exception ex) {
                    try { pipe?.Dispose(); } catch { }
                    _logger.Warn($"ServiceControlPipe Fehler | {ex.Message}");
                    await Task.Delay(1000, token).ContinueWith(_ => { }).ConfigureAwait(false);
                }
            }
        }

        private static NamedPipeServerStream CreateServiceControlPipe() {
            return new NamedPipeServerStream(
                ServiceControlPipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
        }

        private async Task HandleServicePipeClientAsync(NamedPipeServerStream pipe, CancellationToken token) {
            StreamWriter? writer = null;
            string clientIdentity = "Unknown";

            try {
                clientIdentity = TryGetPipeClientIdentity(pipe);
                bool clientAuthorized = IsAuthorizedPipeClient(pipe, clientIdentity);

                using StreamReader reader = new(pipe, PipeEncoding, false, 4096, leaveOpen: true);
                writer = new StreamWriter(pipe, PipeEncoding, 4096, leaveOpen: true) { AutoFlush = true };

                _logger.Info($"ServiceControlPipe Client verbunden | WindowsIdentitaet={clientIdentity} | Autorisiert={clientAuthorized}");

                while (!token.IsCancellationRequested && pipe.IsConnected) {
                    string? line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (line == null) {
                        break;
                    }

                    HandleServicePipeLine(pipe, writer, line, clientIdentity, clientAuthorized);
                }
            } catch (OperationCanceledException) {
                // Service wird beendet.
            } catch (Exception ex) {
                _logger.Warn($"ServiceControlPipe Clientfehler | WindowsIdentitaet={clientIdentity} | Fehler={ex.Message}");
            } finally {
                lock (_pipeWriteSync) {
                    if (ReferenceEquals(_guiPipeWriter, writer)) {
                        _guiPipeWriter = null;
                        _guiOnline = false;
                        _guiIdentity = null;
                    }
                }

                try { writer?.Dispose(); } catch { }
                try { pipe.Dispose(); } catch { }
            }
        }

        private void HandleServicePipeLine(NamedPipeServerStream pipe, StreamWriter writer, string line, string clientIdentity, bool clientAuthorized) {
            line = NormalizePipeLine(line);
            string[] parts = line.Split(new[] { '|' }, 3);
            if (parts.Length < 3 || !string.Equals(parts[0], "CMD", StringComparison.OrdinalIgnoreCase)) {
                SendPipeResponse(writer, "-", "REJECTED", "Ungueltiges Pipe-Format.");
                _logger.Warn($"ServiceControlPipe ungueltiges Format | WindowsIdentitaet={clientIdentity} | Inhalt={line}");
                return;
            }

            string requestId = parts[1];
            string command = parts[2].Trim();
            string normalized = NormalizeCommand(command);

            if (string.Equals(normalized, "GUIOnline", StringComparison.OrdinalIgnoreCase)) {
                if (string.IsNullOrWhiteSpace(clientIdentity) || string.Equals(clientIdentity, "Unknown", StringComparison.OrdinalIgnoreCase) || !clientAuthorized) {
                    SendPipeResponse(writer, requestId, "REJECTED", "GUIOnline abgelehnt | Windows-Identitaet nicht berechtigt oder konnte nicht geprueft werden.");
                    _logger.Warn($"GUIOnline abgelehnt | WindowsIdentitaet={clientIdentity} | Autorisiert={clientAuthorized}");
                    return;
                }

                lock (_pipeWriteSync) {
                    _guiOnline = true;
                    _guiIdentity = clientIdentity;
                }

                SendPipeResponse(writer, requestId, "ACCEPTED", $"GUIOnline akzeptiert | WindowsIdentitaet={clientIdentity}");
                _logger.Info($"GUIOnline akzeptiert | WindowsIdentitaet={clientIdentity}");
                return;
            }

            if (string.Equals(normalized, "GUIOffline", StringComparison.OrdinalIgnoreCase)) {
                lock (_pipeWriteSync) {
                    _guiOnline = false;
                }

                SendPipeResponse(writer, requestId, "ACCEPTED", "GUIOffline akzeptiert | Log-/Statusweiterleitung deaktiviert.");
                _logger.Info("GUIOffline akzeptiert | Log-/Statusweiterleitung deaktiviert.");
                return;
            }

            if (!IsGuiPipeAuthenticated(clientIdentity)) {
                SendPipeResponse(writer, requestId, "REJECTED", "Befehl abgelehnt | GUI ist nicht gueltig angemeldet.");
                return;
            }

            if (IsRuntimeApplyCommand(normalized)) {
                SendPipeResponse(writer, requestId, "ACCEPTED", $"{normalized} angenommen | Runtime-Sollzustand wird angewendet.");
                QueueRuntimeReconcile(normalized);
                return;
            }

            if (TryExtractHangupSession(command, out string sessionId)) {
                SendPipeResponse(writer, requestId, "ACCEPTED", $"HangupLiveCall angenommen | SessionID={sessionId}");
                QueueHangupLiveCall(sessionId);
                return;
            }

            SendPipeResponse(writer, requestId, "REJECTED", $"Unbekannter Service-Befehl: {command}");
        }

        private static string NormalizePipeLine(string line) {
            if (string.IsNullOrEmpty(line)) {
                return string.Empty;
            }

            return line.TrimStart('\uFEFF', '\u00C3', '\u00AF', '\u00C2', '\u00BB', '\u00BF');
        }

        private static string NormalizeCommand(string command) {
            return command.Trim() switch {
                "StartSipInt" => "StartSipInt",
                "StopSipInt" => "StopSipInt",
                "ReloadSipInt" => "ReloadSipInt",
                "StartProvider" => "StartProvider",
                "StopProvider" => "StopProvider",
                "ReloadProvider" => "ReloadProvider",
                "StartGsm" => "StartGsm",
                "StopGsm" => "StopGsm",
                "ReloadGsm" => "ReloadGsm",
                "StartAll" => "StartAll",
                "StopAll" => "StopAll",
                "ReloadAll" => "ReloadAll",
                "ApplyRuntimeState" => "ApplyRuntimeState",
                "ReloadSettings" => "ReloadSettings",
                "GetRuntimeStatus" => "GetRuntimeStatus",
                "GUIOnline" => "GUIOnline",
                "GUIOffline" => "GUIOffline",
                _ => command.Trim()
            };
        }

        private static bool IsRuntimeApplyCommand(string command) {
            return command is "ApplyRuntimeState"
                or "ReloadSettings"
                or "GetRuntimeStatus"
                or "StartSipInt"
                or "StopSipInt"
                or "ReloadSipInt"
                or "StartProvider"
                or "StopProvider"
                or "ReloadProvider"
                or "StartGsm"
                or "StopGsm"
                or "ReloadGsm"
                or "StartAll"
                or "StopAll"
                or "ReloadAll";
        }

        private static bool TryExtractHangupSession(string command, out string sessionId) {
            sessionId = string.Empty;
            string value = command.Trim();

            if (value.StartsWith("HangupLiveCall(", StringComparison.OrdinalIgnoreCase) && value.EndsWith(")", StringComparison.OrdinalIgnoreCase)) {
                sessionId = value.Substring("HangupLiveCall(".Length, value.Length - "HangupLiveCall(".Length - 1).Trim();
                return !string.IsNullOrWhiteSpace(sessionId);
            }

            if (value.StartsWith("HangupLiveCall:", StringComparison.OrdinalIgnoreCase)) {
                sessionId = value.Substring("HangupLiveCall:".Length).Trim();
                return !string.IsNullOrWhiteSpace(sessionId);
            }

            return false;
        }

        private void QueueRuntimeReconcile(string reason) {
            _ = Task.Run(() => {
                if (string.Equals(reason, "ReloadSettings", StringComparison.OrdinalIgnoreCase)) {
                    TKSQL_ConnectionClass.ResetConnectionCache();
                    _logger.Info("ServiceRuntime SQL-Konfiguration neu geladen | Verbindungscache wurde geleert.");
                }

                ReconcileDesiredRuntimeState(reason);
            });
        }

        private void QueueHangupLiveCall(string sessionId) {
            _ = Task.Run(async () => {
                try {
                    TKBase_ServiceRunTime? runtime;
                    lock (_runtimeSync) {
                        runtime = _runtime;
                    }

                    bool ok = runtime != null && await runtime.HangupSessionAsync(sessionId).ConfigureAwait(false);
                    _logger.Warn($"HangupLiveCall ausgefuehrt | SessionID={sessionId} | Result={(ok ? "Success" : "NoSession")}");
                } catch (Exception ex) {
                    _logger.Error($"HangupLiveCall fehlgeschlagen | SessionID={sessionId} | Fehler={ex.Message}");
                }
            });
        }

        private bool IsGuiPipeAuthenticated(string clientIdentity) {
            lock (_pipeWriteSync) {
                return _guiOnline
                    && !string.IsNullOrWhiteSpace(_guiIdentity)
                    && !string.Equals(_guiIdentity, "Unknown", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(_guiIdentity, clientIdentity, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static bool IsAuthorizedPipeClient(NamedPipeServerStream pipe, string clientIdentity) {
            if (pipe == null || string.IsNullOrWhiteSpace(clientIdentity) || string.Equals(clientIdentity, "Unknown", StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            bool authorized = false;

            try {
                pipe.RunAsClient(() => {
                    using WindowsIdentity? identity = WindowsIdentity.GetCurrent();
                    if (identity == null || !identity.IsAuthenticated) {
                        authorized = false;
                        return;
                    }

                    WindowsPrincipal principal = new(identity);
                    if (principal.IsInRole(WindowsBuiltInRole.Administrator)) {
                        authorized = true;
                        return;
                    }

                    authorized = HasAdministratorGroupSid(identity);
                });
            } catch {
                authorized = false;
            }

            return authorized;
        }

        private static bool HasAdministratorGroupSid(WindowsIdentity identity) {
            try {
                SecurityIdentifier adminSid = new(WellKnownSidType.BuiltinAdministratorsSid, null);
                return identity.Groups != null && identity.Groups.Contains(adminSid);
            } catch {
                return false;
            }
        }

        private static string TryGetPipeClientIdentity(NamedPipeServerStream pipe) {
            try {
                string? userName = pipe.GetImpersonationUserName();
                return string.IsNullOrWhiteSpace(userName) ? "Unknown" : userName;
            } catch {
                return "Unknown";
            }
        }

        private void SendPipeResponse(StreamWriter writer, string requestId, string status, string message) {
            lock (_pipeWriteSync) {
                writer.WriteLine($"RESPONSE|{requestId}|{status}|{status}: {message}");
                writer.Flush();
            }
        }

        private void ForwardServiceLogToGui(LogLevel? level, string text) {
            lock (_pipeWriteSync) {
                if (!_guiOnline || _guiPipeWriter == null) {
                    return;
                }
            }

            string encoded;
            try {
                encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
            } catch {
                return;
            }

            _ = Task.Run(() => {
                try {
                    lock (_pipeWriteSync) {
                        if (!_guiOnline || _guiPipeWriter == null) {
                            return;
                        }

                        _guiPipeWriter.WriteLine($"LOG|{level?.ToString() ?? "Raw"}|{encoded}");
                        _guiPipeWriter.Flush();
                    }
                } catch {
                    lock (_pipeWriteSync) {
                        _guiOnline = false;
                        _guiPipeWriter = null;
                    }
                }
            });
        }

        private void ReconcileDesiredRuntimeState(string reason) {
            if (_reconcileBusy) {
                return;
            }

            _reconcileBusy = true;

            try {
                bool databaseAvailable = TKSQL_ConnectionClass.TryEnsureDatabaseAvailable(_logger, $"Runtime ServicePoll | Grund={reason}", false);

                if (databaseAvailable && !_lastDatabaseAvailable) {
                    _logger.TryConfigureFromDatabase();
                }

                _lastDatabaseAvailable = databaseAvailable;

                RuntimeLoadOptions targetOptions = databaseAvailable
                    ? ResolveTargetOptions()
                    : RuntimeLoadOptions.None;

                lock (_runtimeSync) {
                    _runtime ??= new TKBase_ServiceRunTime(_logger);

                    if (!targetOptions.SameRuntimeAs(_appliedOptions)) {
                        _logger.Info($"Runtime Sollzustand geaendert | Grund={reason} | DB={(databaseAvailable ? "Online" : "Offline")} | Alt={_appliedOptions?.ToLoadArgument() ?? "-"} | Neu={targetOptions.ToLoadArgument()}");
                        _runtime.ApplyRuntimeOptions(targetOptions, databaseAvailable ? reason : "SQL-Offline-Schutz");
                        _appliedOptions = targetOptions;
                    }
                }
            } catch (Exception ex) {
                _logger.Error($"Runtime Sollzustand konnte nicht angewendet werden | Grund={reason} | Fehler={ex}");
            } finally {
                _reconcileBusy = false;
            }
        }

        private RuntimeLoadOptions ResolveTargetOptions() {
            if (_loadOptions.AutoDetect) {
                return TKBase_ServiceRunTime.ResolveDesiredStartOptions(new TKSQL_Commands(_logger), _logger).Normalize();
            }

            return _loadOptions.Normalize();
        }
    }

    /// <summary>
    /// Beschreibt, welche TK-Server-Bereiche im GUI-losen Runtime-Prozess geladen werden.
    /// </summary>
    public sealed class RuntimeLoadOptions {
        public static RuntimeLoadOptions None => new();
        public static RuntimeLoadOptions All => new() { StartSipInt = true, StartSipExt = true, StartGsm = true, StartProviderRegistration = true };

        public bool AutoDetect { get; init; }
        public bool StartSipInt { get; init; }
        public bool StartSipExt { get; init; }
        public bool StartGsm { get; init; }
        public bool StartProviderRegistration { get; init; }

        public bool StartSipCore => StartSipInt || StartSipExt;
        public bool IsNone => !StartSipInt && !StartSipExt && !StartGsm && !StartProviderRegistration;

        public RuntimeLoadOptions Normalize() {
            if (AutoDetect) {
                return this;
            }

            bool providerRegistration = StartProviderRegistration;
            bool sipExt = StartSipExt || providerRegistration;

            return new RuntimeLoadOptions {
                AutoDetect = false,
                StartSipInt = StartSipInt,
                StartSipExt = sipExt,
                StartGsm = StartGsm,
                StartProviderRegistration = providerRegistration
            };
        }

        public bool SameRuntimeAs(RuntimeLoadOptions? other) {
            if (other == null) {
                return false;
            }

            RuntimeLoadOptions left = Normalize();
            RuntimeLoadOptions right = other.Normalize();
            return left.AutoDetect == right.AutoDetect
                && left.StartSipInt == right.StartSipInt
                && left.StartSipExt == right.StartSipExt
                && left.StartGsm == right.StartGsm
                && left.StartProviderRegistration == right.StartProviderRegistration;
        }

        public string ToLoadArgument() {
            RuntimeLoadOptions normalized = Normalize();

            if (normalized.AutoDetect) {
                return "auto";
            }

            if (normalized.StartSipInt && normalized.StartSipExt && normalized.StartGsm && normalized.StartProviderRegistration) {
                return "all";
            }

            List<string> parts = new();

            if (normalized.StartGsm) {
                parts.Add("gsm");
            }

            if (normalized.StartSipExt) {
                parts.Add("sipext");
            }

            if (normalized.StartProviderRegistration) {
                parts.Add("providerreg");
            }

            if (normalized.StartSipInt) {
                parts.Add("sipint");
            }

            return parts.Count == 0 ? "none" : string.Join(",", parts);
        }

        public static RuntimeLoadOptions Parse(string? value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return All;
            }

            bool all = false;
            bool auto = false;
            bool gsm = false;
            bool sipExt = false;
            bool providerRegistration = false;
            bool sipInt = false;

            foreach (string rawPart in value.Split(new[] { ',', ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
                string part = rawPart.ToLowerInvariant();

                if (part == "all") {
                    all = true;
                    continue;
                }

                if (part == "auto") {
                    auto = true;
                    continue;
                }

                if (part == "gsm") {
                    gsm = true;
                    continue;
                }

                if (part == "sipext" || part == "sip-ext" || part == "ext") {
                    sipExt = true;
                    continue;
                }

                if (part == "providerreg" || part == "provider-registration" || part == "provider") {
                    providerRegistration = true;
                    continue;
                }

                if (part == "sipint" || part == "sip-int" || part == "int") {
                    sipInt = true;
                    continue;
                }
            }

            if (all) {
                return All;
            }

            if (auto) {
                return new RuntimeLoadOptions { AutoDetect = true };
            }

            return new RuntimeLoadOptions { StartGsm = gsm, StartSipExt = sipExt, StartProviderRegistration = providerRegistration, StartSipInt = sipInt }.Normalize();
        }
    }
}
