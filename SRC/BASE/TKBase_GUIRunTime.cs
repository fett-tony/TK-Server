namespace TKServer.BASE {
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;
    using System.IO.Pipes;
    using System.Linq;
    using System.ServiceProcess;
    using System.Text;
    using System.Security.Principal;
    using System.Threading;
    using System.Threading.Tasks;

    using TKServer.SQL;

    // ============================================================================
    // TKBase_GUIRunTime
    // ----------------------------------------------------------------------------
    // GUI-Laufzeit des TK-Servers.
    //
    // Diese Datei ist fuer den normalen GUI-Start zustaendig.
    // Die GUI ist Bedienoberflaeche, Konfigurationsoberflaeche und Statusanzeige.
    //
    // Die GUI besitzt keine Telefonie-/Server-Runtime.
    // Sie schreibt Sollzustaende in die DB und sendet Befehle an den Service.
    //
    // Die eigentliche Runtime liegt ausschliesslich in TKBase_ServiceRunTime.
    // ============================================================================
    public static class TKBase_GUIRunTime {
        private const string WindowsServiceName = "TK-Server";
        private const string ServiceControlPipeName = "TKServer_ServiceControl";
        private static readonly Encoding PipeEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private static readonly object _pipeSync = new();
        private static readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingResponses = new(StringComparer.OrdinalIgnoreCase);
        private static NamedPipeClientStream? _pipeClient;
        private static StreamReader? _pipeReader;
        private static StreamWriter? _pipeWriter;
        private static CancellationTokenSource? _pipeCts;
        private static Task? _pipeReadTask;
        private static bool _guiOnline;

        public static event Action<bool, string>? RunningStateChanged;
        public static event Action<string>? ServiceLogReceived;

        public static bool IsRunning {
            get {
                try {
                    if (!IsWindowsServiceRunning()) {
                        return false;
                    }

                    DataTable table = new TKSQL_Commands().LoadRuntimeAreaControlTable();
                    foreach (DataRow row in table.Rows) {
                        if (string.Equals(Convert.ToString(row["AreaKey"]), "SIPInt", StringComparison.OrdinalIgnoreCase)) {
                            return string.Equals(Convert.ToString(row["ActualState"]), "Running", StringComparison.OrdinalIgnoreCase);
                        }
                    }

                    return false;
                } catch {
                    return false;
                }
            }
        }

        public static void Start(TKBase_LogManager? logger) {
            logger?.Info("SIP Server Start angefordert | GUI setzt DB-Sollzustand und sendet Service-Befehl.");

            if (!EnsureDatabaseAvailable(logger, "SIP Server Start")) {
                RunningStateChanged?.Invoke(false, "SIP Server Start nicht gestartet | SQL-Datenbank nicht erreichbar.");
                return;
            }

            if (!HasRunnableSipUsers(logger)) {
                string skipped = "SIP Server Start verworfen | Keine aktiven SIP-Konten vorhanden.";
                logger?.Warn(skipped);
                RunningStateChanged?.Invoke(false, skipped);
                return;
            }

            try {
                new TKSQL_Commands(logger).SetRuntimeDesiredState(sipRuntimeEnabled: true);
                logger?.Info("SIPInt Start-Soll gesetzt | DB: TK_RuntimeAreaControl SIPInt DesiredEnabled=True.");
                string response = SendServiceCommand("StartSipInt", logger);
                RunningStateChanged?.Invoke(true, response);
            } catch (Exception ex) {
                string failed = $"SIP Server Start fehlgeschlagen | Fehler={ex.Message}";
                logger?.Error(failed);
                RunningStateChanged?.Invoke(false, failed);
            }
        }

        public static void Stop() {
            Stop(null);
        }

        public static void Stop(TKBase_LogManager? logger) {
            logger?.Info("SIP Server Stop angefordert | GUI setzt DB-Sollzustand und sendet Service-Befehl.");

            try {
                new TKSQL_Commands(logger).SetRuntimeDesiredState(sipRuntimeEnabled: false);
                logger?.Info("SIPInt Stop-Soll gesetzt | DB: TK_RuntimeAreaControl SIPInt DesiredEnabled=False.");
                string response = SendServiceCommand("StopSipInt", logger);
                RunningStateChanged?.Invoke(false, response);
            } catch (Exception ex) {
                string failed = $"SIP Server Stop fehlgeschlagen | Fehler={ex.Message}";
                logger?.Error(failed);
                RunningStateChanged?.Invoke(IsRunning, failed);
            }
        }

        public static void StartProviderRegistration(TKBase_LogManager? logger) {
            logger?.Info("ProviderRegistration Start angefordert | GUI setzt DB-Sollzustand und sendet Service-Befehl.");

            if (!EnsureDatabaseAvailable(logger, "ProviderRegistration Start")) {
                return;
            }

            if (!HasRunnableProviderAccounts(logger)) {
                logger?.Warn("ProviderRegistration Start verworfen | Keine aktiven Provider-Konten vorhanden.");
                return;
            }

            try {
                new TKSQL_Commands(logger).SetRuntimeDesiredState(providerRegistrationEnabled: true);
                logger?.Info("ProviderRegistration Start-Soll gesetzt | DB: TK_RuntimeAreaControl Provider DesiredEnabled=True.");
                SendServiceCommand("StartProvider", logger);
            } catch (Exception ex) {
                logger?.Error($"ProviderRegistration Start fehlgeschlagen | Fehler={ex.Message}");
            }
        }

        public static void StopProviderRegistration(TKBase_LogManager? logger) {
            logger?.Info("ProviderRegistration Stop angefordert | GUI setzt DB-Sollzustand und sendet Service-Befehl.");

            try {
                new TKSQL_Commands(logger).SetRuntimeDesiredState(providerRegistrationEnabled: false);
                logger?.Info("ProviderRegistration Stop-Soll gesetzt | DB: TK_RuntimeAreaControl Provider DesiredEnabled=False.");
                SendServiceCommand("StopProvider", logger);
            } catch (Exception ex) {
                logger?.Error($"ProviderRegistration Stop fehlgeschlagen | Fehler={ex.Message}");
            }
        }

        public static void ReloadProviderRegistration(TKBase_LogManager? logger) {
            logger?.Info("ProviderRegistration Reload angefordert | GUI setzt DB-Sollzustand und sendet Service-Befehle.");

            try {
                new TKSQL_Commands(logger).SetRuntimeDesiredState(providerRegistrationEnabled: false);
                SendServiceCommand("StopProvider", logger);
                new TKSQL_Commands(logger).SetRuntimeDesiredState(providerRegistrationEnabled: true);
                SendServiceCommand("StartProvider", logger);
            } catch (Exception ex) {
                logger?.Error($"ProviderRegistration Reload fehlgeschlagen | Fehler={ex.Message}");
            }
        }

        public static void StartGsm(TKBase_LogManager? logger) {
            logger?.Info("GSM Start angefordert | GUI setzt DB-Sollzustand und sendet Service-Befehl.");

            if (!EnsureDatabaseAvailable(logger, "GSM Start")) {
                return;
            }

            if (!HasRunnableGsmDeviceWithSim(logger)) {
                logger?.Warn("GSM Start verworfen | Kein freigegebenes GSM-Geraet mit zugeordneter/eingelegter SIM vorhanden.");
                return;
            }

            try {
                new TKSQL_Commands(logger).SetRuntimeDesiredState(gsmRuntimeEnabled: true);
                logger?.Info("GSM Start-Soll gesetzt | DB: TK_RuntimeAreaControl GSM DesiredEnabled=True.");
                SendServiceCommand("StartGsm", logger);
            } catch (Exception ex) {
                logger?.Error($"GSM Start fehlgeschlagen | Fehler={ex.Message}");
            }
        }

        public static void StopGsm() {
            StopGsm(null);
        }

        public static void StopGsm(TKBase_LogManager? logger) {
            logger?.Info("GSM Stop angefordert | GUI setzt DB-Sollzustand und sendet Service-Befehl.");

            try {
                new TKSQL_Commands(logger).SetRuntimeDesiredState(gsmRuntimeEnabled: false);
                logger?.Info("GSM Stop-Soll gesetzt | DB: TK_RuntimeAreaControl GSM DesiredEnabled=False.");
                SendServiceCommand("StopGsm", logger);
            } catch (Exception ex) {
                logger?.Error($"GSM Stop fehlgeschlagen | Fehler={ex.Message}");
            }
        }

        public static void RunGsmOnce(TKBase_LogManager? logger) {
            logger?.Info("GSM Reload/Neuinitialisierung angefordert | GUI sendet Service-Befehl.");
            SendServiceCommand("ReloadGsm", logger);
        }

        public static void ObserveServiceDesiredState(TKBase_LogManager? logger) {
            if (!EnsureDatabaseAvailable(logger, "Runtime-Sollzustand lesen")) {
                return;
            }

            try {
                DataTable table = new TKSQL_Commands(logger).LoadRuntimeAreaControlTable();
                foreach (DataRow row in table.Rows) {
                    logger?.Info($"RuntimeAreaControl | Bereich={row["AreaKey"]} | Soll={row["DesiredEnabled"]} | Ist={row["ActualState"]} | Fehler={row["LastError"]}");
                }
            } catch (Exception ex) {
                logger?.Warn($"RuntimeAreaControl konnte nicht gelesen werden | Fehler={ex.Message}");
            }
        }

        public static void NotifyGuiClosing(TKBase_LogManager? logger) {
            logger?.Info("GUI wird geschlossen | GUI besitzt keine lokale Telefonie-Runtime. Service-Runtime bleibt gemaess DB-Sollzustand aktiv oder gestoppt.");
            SendGuiOffline(logger);
        }

        public static void SendGuiOnline(TKBase_LogManager? logger) {
            string response = SendServiceCommand("GUIOnline", logger);
            _guiOnline = response.StartsWith("ACCEPTED", StringComparison.OrdinalIgnoreCase);
        }

        public static void SendGuiOffline(TKBase_LogManager? logger) {
            if (!IsPipeConnected()) {
                _guiOnline = false;
                return;
            }

            try {
                SendServiceCommand("GUIOffline", logger);
            } finally {
                _guiOnline = false;
                DisconnectPipe();
            }
        }

        public static string SendApplyRuntimeState(TKBase_LogManager? logger) {
            return SendServiceCommand("ApplyRuntimeState", logger);
        }

        public static string SendServiceCommand(string command, TKBase_LogManager? logger, int timeoutMs = 3000) {
            if (string.IsNullOrWhiteSpace(command)) {
                return "REJECTED: Leerer Service-Befehl.";
            }

            if (!IsWindowsServiceInstalled()) {
                string message = $"REJECTED: TK-Service ist nicht installiert | Befehl={command}";
                logger?.Warn(message);
                return message;
            }

            if (!IsWindowsServiceRunning()) {
                string message = $"REJECTED: TK-Service laeuft nicht | Befehl={command}";
                logger?.Warn(message);
                return message;
            }

            try {
                EnsurePipeConnected(logger, timeoutMs);
                string requestId = Guid.NewGuid().ToString("N");
                TaskCompletionSource<string> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
                _pendingResponses[requestId] = tcs;

                lock (_pipeSync) {
                    if (_pipeWriter == null) {
                        throw new IOException("PipeWriter ist nicht verbunden.");
                    }

                    _pipeWriter.WriteLine($"CMD|{requestId}|{command}");
                    _pipeWriter.Flush();
                }

                if (!tcs.Task.Wait(TimeSpan.FromMilliseconds(timeoutMs))) {
                    _pendingResponses.TryRemove(requestId, out _);
                    string timeout = $"ERROR: Keine Pipe-Antwort vom Service | Befehl={command}";
                    logger?.Warn(timeout);
                    return timeout;
                }

                string response = tcs.Task.Result;
                logger?.Info($"Service-Befehl Antwort | Befehl={command} | Antwort={response}");
                return response;
            } catch (Exception ex) {
                DisconnectPipe();
                string failed = $"ERROR: Service-Befehl konnte nicht gesendet werden | Befehl={command} | Fehler={ex.Message}";
                logger?.Error(failed);
                return failed;
            }
        }

        private static void EnsurePipeConnected(TKBase_LogManager? logger, int timeoutMs) {
            if (IsPipeConnected()) {
                return;
            }

            DisconnectPipe();

            NamedPipeClientStream pipe = new(".", ServiceControlPipeName, PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.Impersonation);
            pipe.Connect(timeoutMs);

            StreamReader reader = new(pipe, PipeEncoding, false, 4096, leaveOpen: true);
            StreamWriter writer = new(pipe, PipeEncoding, 4096, leaveOpen: true) { AutoFlush = true };
            CancellationTokenSource cts = new();

            lock (_pipeSync) {
                _pipeClient = pipe;
                _pipeReader = reader;
                _pipeWriter = writer;
                _pipeCts = cts;
                _pipeReadTask = Task.Run(() => ReadPipeLoopAsync(reader, cts.Token));
            }

            logger?.Info("Service-Pipe verbunden | bidirektionaler GUI/Service-Kanal aktiv.");
        }

        private static async Task ReadPipeLoopAsync(StreamReader reader, CancellationToken token) {
            try {
                while (!token.IsCancellationRequested) {
                    string? line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (line == null) {
                        break;
                    }

                    HandlePipeLine(line);
                }
            } catch {
                // Verbindungsabbruch wird beim naechsten Befehl neu aufgebaut.
            } finally {
                DisconnectPipe();
            }
        }

        private static void HandlePipeLine(string line) {
            line = NormalizePipeLine(line);
            if (line.StartsWith("RESPONSE|", StringComparison.OrdinalIgnoreCase)) {
                string[] parts = line.Split(new[] { '|' }, 4);
                if (parts.Length >= 4 && _pendingResponses.TryRemove(parts[1], out TaskCompletionSource<string>? tcs)) {
                    tcs.TrySetResult(parts[3]);
                }

                return;
            }

            if (line.StartsWith("LOG|", StringComparison.OrdinalIgnoreCase)) {
                string[] parts = line.Split(new[] { '|' }, 3);
                if (parts.Length >= 3) {
                    try {
                        string text = Encoding.UTF8.GetString(Convert.FromBase64String(parts[2]));
                        ServiceLogReceived?.Invoke(text);
                    } catch {
                        // Fehlerhafte Log-Zeile darf GUI nicht blockieren.
                    }
                }
            }
        }

        private static string NormalizePipeLine(string line) {
            if (string.IsNullOrEmpty(line)) {
                return string.Empty;
            }

            return line.TrimStart('﻿');
        }

        private static bool IsPipeConnected() {
            lock (_pipeSync) {
                return _pipeClient?.IsConnected == true && _pipeWriter != null && _pipeReader != null;
            }
        }

        private static void DisconnectPipe() {
            lock (_pipeSync) {
                try { _pipeCts?.Cancel(); } catch { }
                try { _pipeWriter?.Dispose(); } catch { }
                try { _pipeReader?.Dispose(); } catch { }
                try { _pipeClient?.Dispose(); } catch { }
                _pipeWriter = null;
                _pipeReader = null;
                _pipeClient = null;
                _pipeCts = null;
                _pipeReadTask = null;
                _guiOnline = false;

                foreach (var pending in _pendingResponses.ToArray()) {
                    if (_pendingResponses.TryRemove(pending.Key, out TaskCompletionSource<string>? tcs)) {
                        tcs.TrySetResult("ERROR: Pipe-Verbindung wurde getrennt.");
                    }
                }
            }
        }

        private static bool EnsureDatabaseAvailable(TKBase_LogManager? logger, string operation) {
            if (TKSQL_ConnectionClass.TryEnsureDatabaseAvailable(logger, operation, true)) {
                return true;
            }

            logger?.Warn($"{operation} nicht gestartet | SQL-Datenbank nicht erreichbar.");
            return false;
        }

        private static bool IsWindowsServiceInstalled() {
            try {
                return ServiceController.GetServices().Any(x => string.Equals(x.ServiceName, WindowsServiceName, StringComparison.OrdinalIgnoreCase));
            } catch {
                return false;
            }
        }

        private static bool IsWindowsServiceRunning() {
            try {
                using ServiceController service = new(WindowsServiceName);
                return service.Status == ServiceControllerStatus.Running || service.Status == ServiceControllerStatus.StartPending;
            } catch {
                return false;
            }
        }

        private static bool HasRunnableSipUsers(TKBase_LogManager? logger) {
            try {
                return new TKSQL_Commands(logger).GetActiveSipUsers().Count > 0;
            } catch (Exception ex) {
                logger?.Warn($"SIP Startpruefung fehlgeschlagen | Fehler={ex.Message}");
                return false;
            }
        }

        private static bool HasRunnableProviderAccounts(TKBase_LogManager? logger) {
            try {
                return new TKSQL_Commands(logger).GetDefaultActiveProviderAccount() != null;
            } catch (Exception ex) {
                logger?.Warn($"Provider Startpruefung fehlgeschlagen | Fehler={ex.Message}");
                return false;
            }
        }

        private static bool HasRunnableGsmDeviceWithSim(TKBase_LogManager? logger) {
            try {
                List<DataRow> devices = new TKSQL_Commands(logger).GetActiveGsmRuntimeDevices();
                foreach (DataRow row in devices) {
                    bool simInserted = row.Table.Columns.Contains("IsSimInserted")
                        && row["IsSimInserted"] != DBNull.Value
                        && Convert.ToBoolean(row["IsSimInserted"]);

                    bool simAssigned = row.Table.Columns.Contains("SimID")
                        && row["SimID"] != DBNull.Value
                        && Convert.ToInt32(row["SimID"]) > 0;

                    if (simInserted || simAssigned) {
                        return true;
                    }
                }

                return false;
            } catch (Exception ex) {
                logger?.Warn($"GSM Startpruefung fehlgeschlagen | Fehler={ex.Message}");
                return false;
            }
        }
    }
}
