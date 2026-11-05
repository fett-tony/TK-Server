/*
 * TKSipExt_Registration.cs
 * Aufgabe:
 * - Zuständig für REGISTER gegenüber externen SIP-Providern pro ProviderAccount.
 * - Lädt Konten, startet SIPRegistrationUserAgents, wertet REGISTER-Responses aus und protokolliert den Verlauf.
 * - Spiegelt Registrierungsstatus, externe Nummern und Laufzeitinformationen in die Runtime-/SQL-Sicht.
 * - Bleibt vollständig im Bereich SipExt und behandelt nur externe Provider-Registrierung.
 *
 * Nicht Aufgabe:
 * - Keine interne SIP-Benutzerregistrierung.
 * - Keine GSM-Registrierung.
 * - Keine Session-, Leg- oder Gesprächsvermittlung.
 * 
 * Diese information darf nicht gelöscht werden!
 */

namespace TKServer.SipExt {
    using sipcryptlib;
    using SIPSorcery.SIP;
    using SIPSorcery.SIP.App;
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    using TKServer.BASE;
    using TKServer.SQL;
    /// TKSipExt_Registration.cs
    /// Aufgabe:
    /// - Zuständig für REGISTER gegenüber externen SIP-Providern pro ProviderAccount.
    /// - Lädt Konten, startet SIPRegistrationUserAgents, wertet REGISTER-Responses aus und protokolliert den Verlauf.
    /// - Spiegelt Registrierungsstatus, externe Nummern und Laufzeitinformationen in die Runtime-/SQL-Sicht.
    /// - Bleibt vollständig im Bereich SipExt und behandelt nur externe Provider-Registrierung.
    /// 
    /// Nicht Aufgabe:
    /// - Keine interne SIP-Benutzerregistrierung.
    /// - Keine GSM-Registrierung.
    /// - Keine Session-, Leg- oder Gesprächsvermittlung.
    /// 
    /// Diese information darf nicht gelöscht werden!

    /// <summary>
    /// Defines the <see cref="TKSipExt_Registration" />
    /// </summary>
    internal sealed class TKSipExt_Registration : IDisposable {
        private static readonly object _providerRuntimeSync = new();
        private static TKSipExt_Registration? _providerRuntime;
        private static TKBase_LogManager? _providerRuntimeLogger;

        public static void ConfigureProviderLogger(TKBase_LogManager? logger) {
            lock (_providerRuntimeSync) {
                _providerRuntimeLogger = logger;
            }
        }

        public static bool IsProviderRuntimeStarted {
            get {
                lock (_providerRuntimeSync) {
                    return _providerRuntime?.IsStarted == true;
                }
            }
        }

        public static void StartProviderRegistration() {
            _providerRuntimeLogger?.Info("ProviderRegistration Start über SipExt-Runtime angefordert.");
            TKSipExt_Registration registration;

            lock (_providerRuntimeSync) {
                _providerRuntime ??= new TKSipExt_Registration(_providerRuntimeLogger);
                registration = _providerRuntime;
            }

            registration.Start();
        }

        public static void StopProviderRegistration() {
            _providerRuntimeLogger?.Info("ProviderRegistration Stop über SipExt-Runtime angefordert.");
            TKSipExt_Registration? registration;

            lock (_providerRuntimeSync) {
                registration = _providerRuntime;
            }

            if (registration == null) {
                _providerRuntimeLogger?.Info("ProviderRegistration Stop übersprungen | Runtime war nicht gestartet.");
                return;
            }

            registration.Stop();
        }

        public static void ReloadProviderRegistration() {
            _providerRuntimeLogger?.Info("ProviderRegistration Reload über SipExt-Runtime angefordert.");
            TKSipExt_Registration registration;

            lock (_providerRuntimeSync) {
                _providerRuntime ??= new TKSipExt_Registration(_providerRuntimeLogger);
                registration = _providerRuntime;
            }

            registration.Reload();
        }

        public static Dictionary<int, bool> GetProviderRegistrationStates() {
            lock (_providerRuntimeSync) {
                return _providerRuntime?.GetRegistrationStates() ?? new Dictionary<int, bool>();
            }
        }

        /// <summary>
        /// Defines the _logger
        /// </summary>
        private readonly TKBase_LogManager? _logger;

        /// <summary>
        /// Defines the _sipSql
        /// </summary>
        private readonly TKSQL_Commands _sipSql;

        /// <summary>
        /// Defines the _runtimeRegistrationManager
        /// </summary>
        private readonly TKBase_RegistrationManager _runtimeRegistrationManager;

        /// <summary>
        /// Verhindert parallele Public-IP-Ermittlungen.
        /// </summary>
        private readonly object _publicIpSync = new();

        /// <summary>
        /// Merkt, ob bereits eine Public-IP-Ermittlung laeuft.
        /// </summary>
        private bool _publicIpRefreshRunning;

        /// <summary>
        /// Defines the PublicIpRefreshInterval
        /// </summary>
        private static readonly TimeSpan PublicIpRefreshInterval = TimeSpan.FromHours(24);

        /// <summary>
        /// Defines the PublicIpProbeTimeout
        /// </summary>
        private static readonly TimeSpan PublicIpProbeTimeout = TimeSpan.FromSeconds(3);


        /// <summary>
        /// Defines the _items
        /// </summary>
        private readonly Dictionary<int, ProviderRegistrationItem> _items = new();

        /// <summary>
        /// Defines the _registerCallIdToProviderId
        /// </summary>
        private readonly Dictionary<string, int> _registerCallIdToProviderId = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Defines the _lastRawRegisterByProviderId
        /// </summary>
        private readonly Dictionary<int, string> _lastRawRegisterByProviderId = new();

        /// <summary>
        /// Defines the _lastRawRegisterResponseByProviderId
        /// </summary>
        private readonly Dictionary<int, string> _lastRawRegisterResponseByProviderId = new();

        /// <summary>
        /// Defines the _sync
        /// </summary>
        private readonly object _sync = new();

        /// <summary>
        /// Defines the _transport
        /// </summary>
        private SIPTransport? _transport;

        /// <summary>
        /// Defines the _started
        /// </summary>
        private bool _started;

        /// <summary>
        /// Defines the <see cref="ProviderRegistrationItem" />
        /// </summary>
        private sealed class ProviderRegistrationItem {
            /// <summary>
            /// Gets the ProviderId
            /// </summary>
            public int ProviderId { get; init; }

            /// <summary>
            /// Gets the ProviderName
            /// </summary>
            public string ProviderName { get; init; } = string.Empty;

            /// <summary>
            /// Gets the SipUser
            /// </summary>
            public string SipUser { get; init; } = string.Empty;

            /// <summary>
            /// Gets the AuthUser
            /// </summary>
            public string AuthUser { get; init; } = string.Empty;

            /// <summary>
            /// Gets the Password
            /// </summary>
            public string Password { get; init; } = string.Empty;

            /// <summary>
            /// Gets the RegistrarHost
            /// </summary>
            public string RegistrarHost { get; init; } = string.Empty;

            /// <summary>
            /// Gets the RegistrarPort
            /// </summary>
            public int RegistrarPort { get; init; }

            /// <summary>
            /// Gets the Realm
            /// </summary>
            public string? Realm { get; init; }

            /// <summary>
            /// Gets the ProtocolText
            /// </summary>
            public string ProtocolText { get; init; } = string.Empty;

            /// <summary>
            /// Gets the LocalContactIP
            /// </summary>
            public string LocalContactIP { get; init; } = string.Empty;

            /// <summary>
            /// Gets the Expiry
            /// </summary>
            public int Expiry { get; init; } = 3600;

            /// <summary>
            /// Gets or sets the Agent
            /// </summary>
            public SIPRegistrationUserAgent? Agent { get; set; }

            /// <summary>
            /// Gets or sets the LastResolvedIP
            /// </summary>
            public string? LastResolvedIP { get; set; }

            /// <summary>
            /// Gets or sets the LastRemoteEndPoint
            /// </summary>
            public string? LastRemoteEndPoint { get; set; }

            /// <summary>
            /// Gets or sets the ObservedPublicIP
            /// </summary>
            public string? ObservedPublicIP { get; set; }

            /// <summary>
            /// Gets or sets the ObservedPublicPort
            /// </summary>
            public int? ObservedPublicPort { get; set; }

            /// <summary>
            /// Gets or sets the ObservedPublicSource
            /// </summary>
            public string? ObservedPublicSource { get; set; }

            /// <summary>
            /// Gets or sets the LastRequestUri
            /// </summary>
            public string? LastRequestUri { get; set; }

            /// <summary>
            /// Gets or sets the LastServiceRoute
            /// </summary>
            public string? LastServiceRoute { get; set; }

            /// <summary>
            /// Gets or sets the LastAssociatedUri
            /// </summary>
            public string? LastAssociatedUri { get; set; }

            /// <summary>
            /// Gets or sets the LastCallID
            /// </summary>
            public string? LastCallID { get; set; }

            public bool StopRequested { get; set; }

            public bool StopStateSaved { get; set; }

            public ManualResetEventSlim StopCompleted { get; } = new(false);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TKSipExt_Registration"/> class.
        /// </summary>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        public TKSipExt_Registration(TKBase_LogManager? logger = null) {
            _logger = logger;
            _sipSql = new TKSQL_Commands(logger);
            _runtimeRegistrationManager = new TKBase_RegistrationManager(logger);
        }

        /// <summary>
        /// Gets a value indicating whether IsStarted
        /// </summary>
        public bool IsStarted {
            get {
                lock (_sync) {
                    return _started;
                }
            }
        }

        /// <summary>
        /// The GetRegistrationStates
        /// </summary>
        /// <returns>The <see cref="Dictionary{int, bool}"/></returns>
        public Dictionary<int, bool> GetRegistrationStates() {
            lock (_sync) {
                return _items.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Agent?.IsRegistered == true);
            }
        }

        /// <summary>
        /// The Start
        /// </summary>
        public void Start() {
            bool releaseTransportOnFailure = false;

            try {
                lock (_sync) {
                    _logger?.Info("ProviderRegistration Start angefordert.");

                    if (_started) {
                        _logger?.Warn("ProviderRegistration läuft bereits. Start wird übersprungen.");
                        return;
                    }

                    TKBase_ServerSettings settings = _sipSql.LoadServerSettings();
                    RefreshPublicInternetIpIfRequiredAsync(false, null, "ProviderRegistration Start").GetAwaiter().GetResult();
                    DataTable dt = LoadProviderAccounts();

                    _transport = AcquireProviderTransport(dt, settings);
                    if (_transport == null) {
                        throw new InvalidOperationException("ProviderRegistration Transport konnte nicht aufgebaut werden.");
                    }

                    releaseTransportOnFailure = true;
                    WireTraceEvents(_transport);
                    _logger?.Info("ProviderRegistration Transport aufgebaut | Quelle=ProviderAccounts/ServerSettings");

                    //int startedCount = LoadAndStartAllAccounts();
                    int startedCount = LoadAndStartAllAccounts(dt, settings);
                    _started = true;
                    releaseTransportOnFailure = false;
                    _logger?.Info($"ProviderRegistration gestartet | Gestartet={startedCount} | Fehler={Math.Max(0, _items.Count == 0 ? 0 : 0)}");
                }
            } catch (Exception ex) {
                _logger?.Error($"ProviderRegistration Start fehlgeschlagen | Fehler={ex}");
                try {
                    StopStartedItemsAfterStartFailure();
                } catch (Exception cleanupEx) {
                    _logger?.Warn($"ProviderRegistration Start-Cleanup Warnung | Fehler={cleanupEx.Message}");
                } finally {
                    ResetRuntimeStateAfterFailure(releaseTransportOnFailure);
                }
            }
        }

        /// <summary>
        /// The Stop
        /// </summary>
        public void Stop() {
            List<ProviderRegistrationItem> itemsToStop;
            bool releaseTransport;

            lock (_sync) {
                _logger?.Info("ProviderRegistration Stop angefordert.");

                itemsToStop = _items.Values.ToList();
                foreach (ProviderRegistrationItem item in itemsToStop) {
                    item.StopRequested = true;
                    item.StopStateSaved = false;
                    item.StopCompleted.Reset();
                }

                releaseTransport = _transport != null;

                _items.Clear();
                _registerCallIdToProviderId.Clear();
                _lastRawRegisterByProviderId.Clear();
                _lastRawRegisterResponseByProviderId.Clear();
                _started = false;
            }

            try {
                foreach (ProviderRegistrationItem item in itemsToStop) {
                    StopProviderItem(item);
                }
            } catch (Exception ex) {
                _logger?.Warn($"ProviderRegistration Stop Sammelwarnung | Fehler={ex.Message}");
            } finally {
                if (releaseTransport) {
                    try {
                        TKBase_TransportHost.Release("ProviderRegistration", _logger);
                    } catch (Exception ex) {
                        _logger?.Warn($"ProviderRegistration Transport-Release fehlgeschlagen | Fehler={ex.Message}");
                    }
                }

                lock (_sync) {
                    _transport = null;
                    _started = false;
                }
            }

            _logger?.Info("ProviderRegistration gestoppt.");
        }

        /// <summary>
        /// The Reload
        /// </summary>
        public void Reload() {
            _logger?.Info("ProviderRegistration Reload angefordert.");

            try {
                Stop();
                Start();
                _logger?.Info("ProviderRegistration Reload abgeschlossen.");
            } catch (Exception ex) {
                _logger?.Error($"ProviderRegistration Reload fehlgeschlagen | Fehler={ex}");
                try {
                    Stop();
                } catch (Exception stopEx) {
                    _logger?.Warn($"ProviderRegistration Reload-Cleanup Warnung | Fehler={stopEx.Message}");
                }
            }
        }

        private void StopStartedItemsAfterStartFailure() {
            List<ProviderRegistrationItem> startedItems;

            lock (_sync) {
                startedItems = _items.Values.ToList();
                foreach (ProviderRegistrationItem item in startedItems) {
                    item.StopRequested = true;
                    item.StopStateSaved = false;
                    item.StopCompleted.Reset();
                }
            }

            foreach (ProviderRegistrationItem item in startedItems) {
                StopProviderItem(item);
            }
        }

        private void StopProviderItem(ProviderRegistrationItem item) {
            try {
                _logger?.Info($"ProviderRegistration Stoppe Account | ID={item.ProviderId} | User={item.AuthUser}");
                item.Agent?.Stop(true);

                if (!item.StopCompleted.Wait(TimeSpan.FromSeconds(2))) {
                    _logger?.Warn($"ProviderRegistration Stop ohne Abschlussantwort | ID={item.ProviderId} | User={item.AuthUser} | Aktion=lokal offline gesetzt");
                }

                if (!item.StopStateSaved) {
                    SaveRegistrationState(item, false, null, BuildRequestUriText(item), "Stopped/Unregister");
                    WriteRegistrationLog(item.ProviderId, "RegisterStopLocal", null, "Stopped/Unregister", item.LastRemoteEndPoint, BuildRequestUriText(item), item.ProtocolText, item.LastCallID, GetLastRawRequest(item.ProviderId), GetLastRawResponse(item.ProviderId), "Stopped/Unregister");
                    TrySetExternalNumberOffline(item);
                    item.StopStateSaved = true;
                }
            } catch (Exception ex) {
                _logger?.Warn($"ProviderRegistration Stop Warnung | ID={item.ProviderId} | Fehler={ex.Message}");
            } finally {
                item.Agent = null;
            }
        }

        private void ResetRuntimeStateAfterFailure(bool releaseTransport) {
            if (releaseTransport) {
                try {
                    TKBase_TransportHost.Release("ProviderRegistration", _logger);
                } catch (Exception ex) {
                    _logger?.Warn($"ProviderRegistration Start-Cleanup Transport-Release fehlgeschlagen | Fehler={ex.Message}");
                }
            }

            lock (_sync) {
                _items.Clear();
                _registerCallIdToProviderId.Clear();
                _lastRawRegisterByProviderId.Clear();
                _lastRawRegisterResponseByProviderId.Clear();
                _transport = null;
                _started = false;
            }
        }

        /// <summary>
        /// The AcquireProviderTransport
        /// </summary>
        /// <param name="dt">The dt<see cref="DataTable"/></param>
        /// <param name="settings">The settings<see cref="TKBase_ServerSettings"/></param>
        /// <returns>The <see cref="SIPTransport"/></returns>
        private SIPTransport AcquireProviderTransport(DataTable dt, TKBase_ServerSettings settings) {
            string selectedProtocol = settings.SipTransport;
            int selectedPort = settings.SipPort;
            bool hasActiveAccount = false;

            foreach (DataRow row in dt.Rows) {
                if (!IsActive(row)) {
                    continue;
                }

                string accountProtocol = NormalizeProviderProtocol(Convert.ToString(row["SIPProtokoll"]), settings.SipTransport);
                int accountPort = ResolveProviderPort(row["SIPPort"], settings.SipPort);

                if (!hasActiveAccount) {
                    selectedProtocol = accountProtocol;
                    selectedPort = accountPort;
                    hasActiveAccount = true;
                    continue;
                }

                if (!string.Equals(selectedProtocol, accountProtocol, StringComparison.OrdinalIgnoreCase) || selectedPort != accountPort) {
                    _logger?.Warn($"ProviderRegistration Transportabweichung ignoriert | Gewählt={selectedProtocol}/{selectedPort} | Konto={accountProtocol}/{accountPort}");
                }
            }

            return TKBase_TransportHost.Acquire("ProviderRegistration", selectedProtocol, selectedPort, settings.ServerIPs, _logger);
        }

        /// <summary>
        /// The LoadAndStartAllAccounts
        /// </summary>
        /// <param name="dt">The dt<see cref="DataTable"/></param>
        /// <param name="settings">The settings<see cref="TKBase_ServerSettings"/></param>
        /// <returns>The <see cref="int"/></returns>
        private int LoadAndStartAllAccounts(DataTable dt, TKBase_ServerSettings settings) {
            if (_transport == null) {
                throw new InvalidOperationException("SIPTransport ist nicht initialisiert.");
            }

            _logger?.Info($"ProviderRegistration Konten geladen | Anzahl={dt.Rows.Count}");

            int started = 0;
            int failed = 0;

            foreach (DataRow row in dt.Rows) {
                int providerId = row["ID"] == DBNull.Value ? 0 : Convert.ToInt32(row["ID"]);
                string providerName = Convert.ToString(row["ProviderName"]) ?? string.Empty;

                try {
                    if (!IsActive(row)) {
                        _logger?.Info($"ProviderRegistration Konto übersprungen | ID={providerId} | Provider={providerName} | Grund=Active=0");
                        continue;
                    }

                    ProviderRegistrationItem item = CreateItem(row, settings);
                    SIPRegistrationUserAgent agent = CreateAgent(item);
                    WireRegistrationEvents(item, agent);

                    _logger?.Info($"Start: {item.ProtocolText} für {item.SipUser} an {item.RegistrarHost}:{item.RegistrarPort}");
                    _logger?.Info($"ProviderRegistration Daten | ID={item.ProviderId} | Provider={item.ProviderName} | SIPUser={item.SipUser} | AuthUser={item.AuthUser} | Registrar={item.RegistrarHost}:{item.RegistrarPort} | Realm={item.Realm ?? "-"}");

                    item.Agent = agent;
                    _items[item.ProviderId] = item;
                    SaveRegistrationState(item, false, null, BuildRequestUriText(item), "Start");
                    WriteRegistrationLog(item.ProviderId, "RegisterStart", null, "Start", item.LastRemoteEndPoint, BuildRequestUriText(item), item.ProtocolText, item.LastCallID, null, null, null);
                    agent.Start();
                    started++;
                } catch (Exception ex) {
                    failed++;
                    _logger?.Error($"ProviderRegistration Start fehlgeschlagen | ID={providerId} | Provider={providerName} | Fehler={ex}");
                }
            }

            _logger?.Info($"ProviderRegistration Abschluss | Gestartet={started} | Fehler={failed}");
            return started;
        }

        /// <summary>
        /// The WireRegistrationEvents
        /// </summary>
        /// <param name="item">The item<see cref="ProviderRegistrationItem"/></param>
        /// <param name="agent">The agent<see cref="SIPRegistrationUserAgent"/></param>
        private void WireRegistrationEvents(ProviderRegistrationItem item, SIPRegistrationUserAgent agent) {
            agent.RegistrationSuccessful += (uri, resp) => {
                try {
                    HandleRegistrationSuccessful(item, uri, resp);
                } catch (Exception ex) {
                    _logger?.Error($"ProviderRegistration Erfolg-Event fehlgeschlagen | ID={item.ProviderId} | Fehler={ex}");
                    item.StopCompleted.Set();
                }
            };

            agent.RegistrationFailed += (uri, resp, err) => {
                try {
                    HandleRegistrationFailed(item, uri, resp, err);
                } catch (Exception ex) {
                    _logger?.Error($"ProviderRegistration Fehler-Event fehlgeschlagen | ID={item.ProviderId} | Fehler={ex}");
                    item.StopCompleted.Set();
                }
            };

            agent.RegistrationTemporaryFailure += (uri, resp, msg) => {
                try {
                    HandleRegistrationTemporaryFailure(item, uri, resp, msg);
                } catch (Exception ex) {
                    _logger?.Error($"ProviderRegistration Temporär-Event fehlgeschlagen | ID={item.ProviderId} | Fehler={ex}");
                    item.StopCompleted.Set();
                }
            };

            agent.RegistrationRemoved += (uri, resp) => {
                try {
                    HandleRegistrationRemoved(item, uri, resp);
                } catch (Exception ex) {
                    _logger?.Error($"ProviderRegistration Removed-Event fehlgeschlagen | ID={item.ProviderId} | Fehler={ex}");
                    item.StopCompleted.Set();
                }
            };
        }

        private void HandleRegistrationSuccessful(ProviderRegistrationItem item, SIPURI? uri, SIPResponse? resp) {
            if (item.StopRequested) {
                _logger?.Info($"ProviderRegistration Erfolg nach Stop ignoriert | ID={item.ProviderId} | Provider={item.ProviderName} | User={item.SipUser}");
                item.StopCompleted.Set();
                return;
            }

            item.LastServiceRoute = ExtractServiceRoute(resp);
            item.LastAssociatedUri = ExtractAssociatedUri(resp);
            item.LastCallID = resp?.Header?.CallId ?? item.LastCallID;
            UpdateObservedPublicNetwork(item, resp);
            _logger?.Info("✅ Registrierung erfolgreich!");
            _logger?.Info($"ProviderRegistration Erfolgreich | ID={item.ProviderId} | Provider={item.ProviderName} | User={item.SipUser} | AOR={uri}");
            SaveRegistrationState(item, true, resp, uri?.ToString(), null);
            WriteRegistrationLog(item.ProviderId, "RegisterSuccess", resp, null, item.LastRemoteEndPoint, uri?.ToString(), item.ProtocolText, item.LastCallID, GetLastRawRequest(item.ProviderId), GetLastRawResponse(item.ProviderId), null);
            SyncExternalNumberAndRouting(item, true);
        }

        private void HandleRegistrationFailed(ProviderRegistrationItem item, SIPURI? uri, SIPResponse? resp, string? err) {
            item.LastCallID = resp?.Header?.CallId ?? item.LastCallID;
            if (item.StopRequested) {
                string stopText = IsExpectedStopDeregisterResponse(resp, err)
                    ? "Stopped/Unregister/ProviderAlreadyRemoved"
                    : err ?? "Stopped/Unregister";
                _logger?.Info($"ProviderRegistration Stop-Antwort verarbeitet | ID={item.ProviderId} | Provider={item.ProviderName} | User={item.SipUser} | Status={(resp == null ? "-" : ((int)resp.Status).ToString())} | Text={stopText}");
                SaveRegistrationState(item, false, resp, uri?.ToString(), stopText);
                WriteRegistrationLog(item.ProviderId, "RegisterStop", resp, stopText, item.LastRemoteEndPoint, uri?.ToString(), item.ProtocolText, item.LastCallID, GetLastRawRequest(item.ProviderId), GetLastRawResponse(item.ProviderId), stopText);
                TrySetExternalNumberOffline(item);
                item.StopStateSaved = true;
                item.StopCompleted.Set();
                return;
            }

            _logger?.Error($"❌ Fehler: {err}");
            _logger?.Error($"ProviderRegistration Fehlgeschlagen | ID={item.ProviderId} | Provider={item.ProviderName} | User={item.SipUser} | Fehler={err}");
            SaveRegistrationState(item, false, resp, uri?.ToString(), err);
            WriteRegistrationLog(item.ProviderId, "RegisterFailed", resp, err, item.LastRemoteEndPoint, uri?.ToString(), item.ProtocolText, item.LastCallID, GetLastRawRequest(item.ProviderId), GetLastRawResponse(item.ProviderId), err);
            TrySetExternalNumberOffline(item);
        }

        private void HandleRegistrationTemporaryFailure(ProviderRegistrationItem item, SIPURI? uri, SIPResponse? resp, string? msg) {
            item.LastCallID = resp?.Header?.CallId ?? item.LastCallID;
            if (item.StopRequested) {
                string stopText = msg ?? "Stopped/Unregister";
                _logger?.Info($"ProviderRegistration Stop temporäre Antwort verarbeitet | ID={item.ProviderId} | Provider={item.ProviderName} | User={item.SipUser} | Text={stopText}");
                SaveRegistrationState(item, false, resp, uri?.ToString(), stopText);
                WriteRegistrationLog(item.ProviderId, "RegisterStopTemporary", resp, stopText, item.LastRemoteEndPoint, uri?.ToString(), item.ProtocolText, item.LastCallID, GetLastRawRequest(item.ProviderId), GetLastRawResponse(item.ProviderId), stopText);
                TrySetExternalNumberOffline(item);
                item.StopStateSaved = true;
                item.StopCompleted.Set();
                return;
            }

            _logger?.Info($"⏳ Status: {msg}");
            _logger?.Warn($"ProviderRegistration Temporär fehlgeschlagen | ID={item.ProviderId} | Provider={item.ProviderName} | User={item.SipUser} | Fehler={msg}");
            SaveRegistrationState(item, false, resp, uri?.ToString(), msg);
            WriteRegistrationLog(item.ProviderId, "RegisterTemporaryFailure", resp, msg, item.LastRemoteEndPoint, uri?.ToString(), item.ProtocolText, item.LastCallID, GetLastRawRequest(item.ProviderId), GetLastRawResponse(item.ProviderId), msg);
            TrySetExternalNumberOffline(item);
        }

        private void HandleRegistrationRemoved(ProviderRegistrationItem item, SIPURI? uri, SIPResponse? resp) {
            item.LastCallID = resp?.Header?.CallId ?? item.LastCallID;
            _logger?.Info($"ProviderRegistration Entfernt | ID={item.ProviderId} | Provider={item.ProviderName} | User={item.SipUser}");
            SaveRegistrationState(item, false, resp, uri?.ToString(), "Removed");
            WriteRegistrationLog(item.ProviderId, "RegisterRemoved", resp, "Removed", item.LastRemoteEndPoint, uri?.ToString(), item.ProtocolText, item.LastCallID, GetLastRawRequest(item.ProviderId), GetLastRawResponse(item.ProviderId), "Removed");
            TrySetExternalNumberOffline(item);
            if (item.StopRequested) {
                item.StopStateSaved = true;
                item.StopCompleted.Set();
            }
        }

        private static bool IsExpectedStopDeregisterResponse(SIPResponse? resp, string? errorText) {
            if (resp == null || resp.Status != SIPResponseStatusCodesEnum.Forbidden) {
                return false;
            }

            List<string> parts = new();
            if (!string.IsNullOrWhiteSpace(errorText)) parts.Add(errorText);
            if (!string.IsNullOrWhiteSpace(resp.ReasonPhrase)) parts.Add(resp.ReasonPhrase);
            if (!string.IsNullOrWhiteSpace(resp.Header?.Reason)) parts.Add(resp.Header.Reason);
            if (resp.Header?.UnknownHeaders != null) parts.AddRange(resp.Header.UnknownHeaders);

            string text = string.Join(" ", parts);
            return text.IndexOf("CC_IMS_OBSOLETE_DEREG", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// The LoadProviderAccounts
        /// </summary>
        /// <returns>The <see cref="DataTable"/></returns>
        private DataTable LoadProviderAccounts() {
            return _sipSql.LoadProviderAccountsForRegistration();
        }

        /// <summary>
        /// The CreateItem
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="settings">The settings<see cref="TKBase_ServerSettings"/></param>
        /// <returns>The <see cref="ProviderRegistrationItem"/></returns>
        private ProviderRegistrationItem CreateItem(DataRow row, TKBase_ServerSettings settings) {
            int providerId = Convert.ToInt32(row["ID"]);
            string providerName = (Convert.ToString(row["ProviderName"]) ?? string.Empty).Trim();
            string sipUser = (Convert.ToString(row["SIPUser"]) ?? string.Empty).Trim();
            string authUser = (Convert.ToString(row["SipAuthUser"]) ?? string.Empty).Trim();
            string encryptedPassword = Convert.ToString(row["SIPPasswort"]) ?? string.Empty;
            string registrar = (Convert.ToString(row["SIPRegistrar"]) ?? string.Empty).Trim();
            string server = (Convert.ToString(row["SIPServer"]) ?? string.Empty).Trim();
            string outboundProxy = (Convert.ToString(row["SIPOutboundProxy"]) ?? string.Empty).Trim();
            string proxy = (Convert.ToString(row["SIPProxy"]) ?? string.Empty).Trim();
            string realm = (Convert.ToString(row["SIPRealm"]) ?? string.Empty).Trim();
            string protocol = NormalizeProviderProtocol(Convert.ToString(row["SIPProtokoll"]), settings.SipTransport);
            int port = ResolveProviderPort(row["SIPPort"], settings.SipPort);

            if (string.IsNullOrWhiteSpace(sipUser)) {
                throw new InvalidOperationException("SIPUser ist leer.");
            }

            if (string.IsNullOrWhiteSpace(authUser)) {
                throw new InvalidOperationException("SipAuthUser ist leer.");
            }

            string password = DecryptPassword(encryptedPassword, providerName, authUser);
            if (string.IsNullOrWhiteSpace(password)) {
                throw new InvalidOperationException("SIPPasswort konnte nicht entschlüsselt werden oder ist leer.");
            }

            string registrarHost = string.IsNullOrWhiteSpace(registrar) ? server : registrar;
            if (string.IsNullOrWhiteSpace(registrarHost)) {
                throw new InvalidOperationException("SIPRegistrar/SIPServer ist leer.");
            }

            registrarHost = SanitizeHost(registrarHost);

            return new ProviderRegistrationItem {
                ProviderId = providerId,
                ProviderName = providerName,
                SipUser = sipUser,
                AuthUser = authUser,
                Password = password,
                RegistrarHost = registrarHost,
                RegistrarPort = port,
                Realm = string.IsNullOrWhiteSpace(realm) ? null : realm,
                ProtocolText = protocol,
                LocalContactIP = _sipSql.GetPublicInternetIpForAddressFamily(AddressFamily.InterNetwork) ?? settings.ResolveWanServerAddress().ToString(),
                Expiry = settings.RegisterExpire
            };
        }

        /// <summary>
        /// The CreateAgent
        /// </summary>
        /// <param name="item">The item<see cref="ProviderRegistrationItem"/></param>
        /// <returns>The <see cref="SIPRegistrationUserAgent"/></returns>
        private SIPRegistrationUserAgent CreateAgent(ProviderRegistrationItem item) {
            if (_transport == null) {
                throw new InvalidOperationException("SIPTransport ist nicht initialisiert.");
            }

            SIPProtocolsEnum protocol = ParseProtocol(item.ProtocolText);
            SIPSchemesEnum scheme = protocol == SIPProtocolsEnum.tls ? SIPSchemesEnum.sips : SIPSchemesEnum.sip;

            string accountAorHost = BuildAccountAorHost(item, protocol);
            string registrarUriText = BuildRegistrarUriText(item, protocol);

            SIPURI accountAor = new SIPURI(item.SipUser, accountAorHost, null, scheme, protocol);
            SIPURI contactUri = BuildProviderContactUri(item, scheme, protocol);

            item.LastRequestUri = registrarUriText;
            item.LastRemoteEndPoint = null;
            item.LastResolvedIP = null;

            SIPRegistrationUserAgent agent = new SIPRegistrationUserAgent(
                _transport,
                null,
                accountAor,
                item.AuthUser,
                item.Password,
                item.Realm ?? item.RegistrarHost,
                registrarUriText,
                contactUri,
                item.Expiry,
                null,
                maxRegistrationAttemptTimeout: 20,
                registerFailureRetryInterval: 120,
                maxRegisterAttempts: 5,
                exitOnUnequivocalFailure: false);

            agent.AdjustRegister = request => {
                ApplyProviderContactForNat(item, request, scheme, protocol);
                return request;
            };
            agent.UserAgent = SIPConstants.SipUserAgentVersionString;
            agent.UserDisplayName = item.SipUser;
            return agent;
        }

        /// <summary>
        /// The BuildProviderContactUri
        /// </summary>
        /// <param name="item">The item<see cref="ProviderRegistrationItem"/></param>
        /// <param name="scheme">The scheme<see cref="SIPSchemesEnum"/></param>
        /// <param name="protocol">The protocol<see cref="SIPProtocolsEnum"/></param>
        /// <returns>The <see cref="SIPURI"/></returns>
        private SIPURI BuildProviderContactUri(ProviderRegistrationItem item, SIPSchemesEnum scheme, SIPProtocolsEnum protocol) {
            SIPURI contactUri;
            if (!string.IsNullOrWhiteSpace(item.LocalContactIP) && IPAddress.TryParse(item.LocalContactIP, out IPAddress? contactAddress) && !IPAddress.Any.Equals(contactAddress) && !IPAddress.IPv6Any.Equals(contactAddress)) {
                contactUri = new SIPURI(scheme, contactAddress, 0);
            } else {
                contactUri = new SIPURI(scheme, IPAddress.Any, 0);
            }

            contactUri.User = item.SipUser;
            if (protocol != SIPProtocolsEnum.udp && scheme != SIPSchemesEnum.sips) {
                contactUri.Protocol = protocol;
            }

            _logger?.Info($"ProviderRegistration Contact vorbereitet | ID={item.ProviderId} | User={item.SipUser} | Contact={contactUri}");
            return contactUri;
        }

        /// <summary>
        /// The ApplyProviderContactForNat
        /// </summary>
        /// <param name="item">The item<see cref="ProviderRegistrationItem"/></param>
        /// <param name="request">The request<see cref="SIPRequest"/></param>
        /// <param name="scheme">The scheme<see cref="SIPSchemesEnum"/></param>
        /// <param name="protocol">The protocol<see cref="SIPProtocolsEnum"/></param>
        private void ApplyProviderContactForNat(ProviderRegistrationItem item, SIPRequest request, SIPSchemesEnum scheme, SIPProtocolsEnum protocol) {
            if (request?.Header?.Contact == null || request.Header.Contact.Count != 1) {
                return;
            }

            SIPURI contactUri = request.Header.Contact[0].ContactURI;
            contactUri.Scheme = scheme;
            contactUri.User = item.SipUser;
            string contactHost = ResolveProviderContactHost(item);
            if (!string.IsNullOrWhiteSpace(contactHost)) {
                contactUri.Host = contactHost;
            }
            if (protocol != SIPProtocolsEnum.udp && scheme != SIPSchemesEnum.sips) {
                contactUri.Protocol = protocol;
            }
        }

        /// <summary>
        /// The ResolveProviderContactHost
        /// </summary>
        /// <param name="item">The item<see cref="ProviderRegistrationItem"/></param>
        /// <returns>The <see cref="string"/></returns>
        private string ResolveProviderContactHost(ProviderRegistrationItem item) {
            string? storedPublicIp = null;
            try {
                storedPublicIp = _sipSql.GetPublicInternetIpForAddressFamily(AddressFamily.InterNetwork);
            } catch (Exception ex) {
                _logger?.Warn($"ProviderRegistration Public-IP konnte nicht aus TK_Properties gelesen werden | ID={item.ProviderId} | Fehler={ex.Message}");
            }

            if (!string.IsNullOrWhiteSpace(storedPublicIp)) {
                return item.ObservedPublicPort.HasValue && item.ObservedPublicPort.Value > 0
                    ? storedPublicIp + ":" + item.ObservedPublicPort.Value.ToString()
                    : storedPublicIp;
            }

            return item.LocalContactIP?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// The ParseProtocol
        /// </summary>
        /// <param name="protocolText">The protocolText<see cref="string?"/></param>
        /// <returns>The <see cref="SIPProtocolsEnum"/></returns>
        private static SIPProtocolsEnum ParseProtocol(string? protocolText) {
            return (protocolText ?? string.Empty).Trim().ToUpperInvariant() switch {
                "UDP" => SIPProtocolsEnum.udp,
                "TCP" => SIPProtocolsEnum.tcp,
                "TLS" => SIPProtocolsEnum.tls,
                _ => throw new InvalidOperationException($"Provider SIP-Protokoll fehlt oder ist ungültig: {protocolText}")
            };
        }

        /// <summary>
        /// The SaveRegistrationState
        /// </summary>
        /// <param name="item">The item<see cref="ProviderRegistrationItem"/></param>
        /// <param name="isRegistered">The isRegistered<see cref="bool"/></param>
        /// <param name="resp">The resp<see cref="SIPResponse?"/></param>
        /// <param name="requestUriText">The requestUriText<see cref="string?"/></param>
        /// <param name="errorText">The errorText<see cref="string?"/></param>
        private void SaveRegistrationState(ProviderRegistrationItem item, bool isRegistered, SIPResponse? resp, string? requestUriText, string? errorText) {
            try {
                string? serviceRoute = item.LastServiceRoute ?? ExtractServiceRoute(resp);
                string? associatedUri = item.LastAssociatedUri ?? ExtractAssociatedUri(resp);
                int? statusCode = resp == null ? null : (int)resp.Status;
                string? statusText = resp?.ReasonPhrase;
                DateTime lastRegisterAt = DateTime.UtcNow;
                DateTime? expiresAt = isRegistered ? DateTime.UtcNow.AddSeconds(ExtractExpiresSeconds(resp) ?? item.Expiry) : null;
                string? stateResolvedIp = item.LastResolvedIP;

                _sipSql.SaveProviderRegistrationState(
                    item.ProviderId,
                    isRegistered,
                    lastRegisterAt,
                    expiresAt,
                    item.LastRemoteEndPoint,
                    stateResolvedIp,
                    item.ProtocolText,
                    requestUriText,
                    serviceRoute,
                    associatedUri,
                    item.LastCallID,
                    statusCode,
                    statusText,
                    errorText);

                _runtimeRegistrationManager.SaveExternalRegistration(
                    item.ProviderId,
                    item.ProviderName,
                    item.SipUser,
                    null,
                    item.LastRemoteEndPoint,
                    item.ProtocolText,
                    requestUriText,
                    item.LastCallID,
                    serviceRoute,
                    associatedUri,
                    stateResolvedIp,
                    statusCode,
                    statusText,
                    expiresAt,
                    isRegistered,
                    errorText);
            } catch (Exception ex) {
                _logger?.Warn($"ProviderRegistration State speichern fehlgeschlagen | ProviderID={item.ProviderId} | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The WriteRegistrationLog
        /// </summary>
        /// <param name="providerId">The providerId<see cref="int"/></param>
        /// <param name="eventType">The eventType<see cref="string"/></param>
        /// <param name="resp">The resp<see cref="SIPResponse?"/></param>
        /// <param name="statusTextOverride">The statusTextOverride<see cref="string?"/></param>
        /// <param name="remoteEndPoint">The remoteEndPoint<see cref="string?"/></param>
        /// <param name="requestUri">The requestUri<see cref="string?"/></param>
        /// <param name="transport">The transport<see cref="string?"/></param>
        /// <param name="callId">The callId<see cref="string?"/></param>
        /// <param name="rawRequest">The rawRequest<see cref="string?"/></param>
        /// <param name="rawResponse">The rawResponse<see cref="string?"/></param>
        /// <param name="errorText">The errorText<see cref="string?"/></param>
        private void WriteRegistrationLog(int providerId, string eventType, SIPResponse? resp, string? statusTextOverride, string? remoteEndPoint, string? requestUri, string? transport, string? callId, string? rawRequest, string? rawResponse, string? errorText) {
            try {
                _sipSql.InsertProviderRegistrationLog(
                    providerId,
                    eventType,
                    resp == null ? null : (int)resp.Status,
                    statusTextOverride ?? resp?.ReasonPhrase,
                    remoteEndPoint,
                    requestUri,
                    transport,
                    callId ?? resp?.Header?.CallId,
                    rawRequest,
                    rawResponse,
                    errorText);
            } catch (Exception ex) {
                _logger?.Warn($"ProviderRegistration Log speichern fehlgeschlagen | ProviderID={providerId} | EventType={eventType} | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The SyncExternalNumberAndRouting
        /// </summary>
        /// <param name="item">The item<see cref="ProviderRegistrationItem"/></param>
        /// <param name="isOnline">The isOnline<see cref="bool"/></param>
        private void SyncExternalNumberAndRouting(ProviderRegistrationItem item, bool isOnline) {
            try {
                string numberValue = GetProviderNumber(item);
                if (string.IsNullOrWhiteSpace(numberValue)) {
                    _logger?.Warn($"ExternalNumbers Sync übersprungen | ProviderID={item.ProviderId} | Grund=keine Nummer vorhanden");
                    return;
                }

                string displayName = string.IsNullOrWhiteSpace(item.ProviderName)
                    ? numberValue
                    : $"{item.ProviderName} {numberValue}";

                int externalNumberId = _sipSql.UpsertExternalNumberFromProvider(
                    item.ProviderId,
                    numberValue,
                    displayName,
                    isOnline,
                    true,
                    true,
                    "Provider",
                    "ProviderAccount");

                _logger?.Info($"ProviderRegistration Sync OK | ProviderID={item.ProviderId} | Number={numberValue} | ExternalNumberID={externalNumberId} | RoutingSync=deaktiviert");
            } catch (Exception ex) {
                _logger?.Error($"ProviderRegistration Sync fehlgeschlagen | ID={item.ProviderId} | Provider={item.ProviderName} | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The TrySetExternalNumberOffline
        /// </summary>
        /// <param name="item">The item<see cref="ProviderRegistrationItem"/></param>
        private void TrySetExternalNumberOffline(ProviderRegistrationItem item) {
            try {
                _sipSql.SetExternalNumberOnlineState("ProviderAccount", item.ProviderId, false);
            } catch (Exception ex) {
                _logger?.Warn($"ExternalNumbers Offline-Status konnte nicht gesetzt werden | ProviderID={item.ProviderId} | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The GetProviderNumber
        /// </summary>
        /// <param name="item">The item<see cref="ProviderRegistrationItem"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string GetProviderNumber(ProviderRegistrationItem item) {
            return NormalizeNumber(FirstNotEmpty(item.SipUser));
        }

        /// <summary>
        /// The NormalizeNumber
        /// </summary>
        /// <param name="value">The value<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string NormalizeNumber(string value) {
            value = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value)) {
                return string.Empty;
            }

            if (value.StartsWith("sip:", StringComparison.OrdinalIgnoreCase)) {
                value = value.Substring(4);
            } else if (value.StartsWith("sips:", StringComparison.OrdinalIgnoreCase)) {
                value = value.Substring(5);
            }

            int at = value.IndexOf('@');
            if (at >= 0) {
                value = value.Substring(0, at);
            }

            value = value.Trim();
            if (value.StartsWith("+", StringComparison.Ordinal)) {
                return "+" + new string(value.Substring(1).Where(char.IsDigit).ToArray());
            }

            return new string(value.Where(char.IsDigit).ToArray());
        }

        /// <summary>
        /// The DecryptPassword
        /// </summary>
        /// <param name="encryptedPassword">The encryptedPassword<see cref="string"/></param>
        /// <param name="providerName">The providerName<see cref="string"/></param>
        /// <param name="authUser">The authUser<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private string DecryptPassword(string encryptedPassword, string providerName, string authUser) {
            try {
                return SipCryptor.DecryptFixed(encryptedPassword);
            } catch (Exception ex) {
                _logger?.Error($"Provider Passwort konnte nicht entschlüsselt werden | Provider={providerName} | AuthUser={authUser} | Fehler={ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// The IsActive
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool IsActive(DataRow row) {
            return row["Active"] != DBNull.Value && Convert.ToBoolean(row["Active"]);
        }

        /// <summary>
        /// The UpdateObservedPublicNetwork
        /// </summary>
        /// <param name="item">The item<see cref="ProviderRegistrationItem"/></param>
        /// <param name="resp">The resp<see cref="SIPResponse?"/></param>
        private void UpdateObservedPublicNetwork(ProviderRegistrationItem item, SIPResponse? resp) {
            if (item == null || resp == null) {
                return;
            }

            string? publicIp = ExtractReceivedPublicIp(resp);
            int? publicPort = ExtractReceivedPublicPort(resp);
            string? source = null;

            if (!string.IsNullOrWhiteSpace(publicIp)) {
                source = "ViaReceived";
            } else {
                publicIp = ExtractPublicContactIp(resp);
                if (!string.IsNullOrWhiteSpace(publicIp)) {
                    source = "Contact";
                }
            }

            if (string.IsNullOrWhiteSpace(publicIp) || IsConfiguredServerIp(publicIp)) {
                return;
            }

            _ = RefreshPublicInternetIpIfRequiredAsync(false, publicIp, "ProviderRegistration Provider-Hinweis");

            bool changed = !string.Equals(item.ObservedPublicIP, publicIp, StringComparison.OrdinalIgnoreCase)
                           || item.ObservedPublicPort != publicPort
                           || !string.Equals(item.ObservedPublicSource, source, StringComparison.OrdinalIgnoreCase);

            item.ObservedPublicIP = publicIp;
            item.ObservedPublicPort = publicPort;
            item.ObservedPublicSource = source;

            if (changed) {
                string portText = publicPort.HasValue ? $":{publicPort.Value}" : string.Empty;
                _logger?.Info($"ProviderRegistration Public-IP-Hinweis erkannt | ID={item.ProviderId} | Provider={item.ProviderName} | ProviderHinweis={publicIp}{portText} | Quelle={source} | Aktion=Public-IP-Pruefung");
            }
        }

        /// <summary>
        /// The ExtractReceivedPublicIp
        /// </summary>
        /// <param name="resp">The resp<see cref="SIPResponse?"/></param>
        /// <returns>The <see cref="string?"/></returns>
        private static string? ExtractReceivedPublicIp(SIPResponse? resp) {
            string? value = resp?.Header?.Vias?.TopViaHeader?.ReceivedFromIPAddress;
            if (string.IsNullOrWhiteSpace(value)) {
                string? via = resp?.Header?.Vias?.TopViaHeader?.ToString();
                if (string.IsNullOrWhiteSpace(via)) {
                    return null;
                }

                const string token = "received=";
                int idx = via.IndexOf(token, StringComparison.OrdinalIgnoreCase);
                if (idx < 0) {
                    return null;
                }

                value = via.Substring(idx + token.Length);
                int end = value.IndexOfAny(new[] { ';', ',', ' ', '\r', '\n' });
                if (end >= 0) {
                    value = value.Substring(0, end);
                }
            }

            value = value.Trim();
            return IPAddress.TryParse(value, out IPAddress? parsed) ? parsed.ToString() : null;
        }

        /// <summary>
        /// The ExtractReceivedPublicPort
        /// </summary>
        /// <param name="resp">The resp<see cref="SIPResponse?"/></param>
        /// <returns>The <see cref="int?"/></returns>
        private static int? ExtractReceivedPublicPort(SIPResponse? resp) {
            string? via = resp?.Header?.Vias?.TopViaHeader?.ToString();
            if (string.IsNullOrWhiteSpace(via)) {
                return null;
            }

            const string token = "rport=";
            int idx = via.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) {
                return null;
            }

            string value = via.Substring(idx + token.Length);
            int end = value.IndexOfAny(new[] { ';', ',', ' ', '\r', '\n' });
            if (end >= 0) {
                value = value.Substring(0, end);
            }

            return int.TryParse(value.Trim(), out int port) ? port : null;
        }

        /// <summary>
        /// The ExtractPublicContactIp
        /// </summary>
        /// <param name="resp">The resp<see cref="SIPResponse?"/></param>
        /// <returns>The <see cref="string?"/></returns>
        private static string? ExtractPublicContactIp(SIPResponse? resp) {
            try {
                var contacts = resp?.Header?.Contact;
                if (contacts == null) {
                    return null;
                }

                foreach (var contact in contacts) {
                    string? host = contact?.ContactURI?.Host;
                    if (!string.IsNullOrWhiteSpace(host) && IPAddress.TryParse(host.Trim(), out IPAddress? parsed)) {
                        return parsed.ToString();
                    }
                }
            } catch {
            }

            return null;
        }


        /// <summary>
        /// Ermittelt die oeffentliche Internet-IP alle 24 Stunden oder sofort bei abweichendem Provider-Hinweis.
        /// </summary>
        private async Task RefreshPublicInternetIpIfRequiredAsync(bool force, string? providerObservedIp, string reason) {
            bool start;
            lock (_publicIpSync) {
                if (_publicIpRefreshRunning) {
                    return;
                }

                start = force || IsPublicInternetIpRefreshDue(providerObservedIp);
                if (!start) {
                    return;
                }

                _publicIpRefreshRunning = true;
            }

            try {
                PublicInternetIpProbeResult result = await ProbePublicInternetIpAsync().ConfigureAwait(false);
                if (!result.Success) {
                    _sipSql.SavePublicInternetIpFailure(result.ErrorText ?? "Public-IP-Ermittlung fehlgeschlagen.");
                    _logger?.Warn($"ProviderRegistration Public-IP Ermittlung fehlgeschlagen | Grund={reason} | Fehler={result.ErrorText}");
                    return;
                }

                _sipSql.SavePublicInternetIpSuccess(result.IPv4, result.IPv6, result.Source ?? "HTTP");
                _logger?.Info($"ProviderRegistration Public-IP gespeichert | Grund={reason} | IPv4={result.IPv4 ?? "-"} | IPv6={result.IPv6 ?? "-"} | Quelle={result.Source ?? "HTTP"}");
            } catch (Exception ex) {
                try {
                    _sipSql.SavePublicInternetIpFailure(ex.Message);
                } catch {
                }

                _logger?.Warn($"ProviderRegistration Public-IP Ermittlung Warnung | Grund={reason} | Fehler={ex.Message}");
            } finally {
                lock (_publicIpSync) {
                    _publicIpRefreshRunning = false;
                }
            }
        }

        /// <summary>
        /// Prueft 24h-Frist und Provider-Abweichung gegen gespeicherte TK_Properties-Werte.
        /// </summary>
        private bool IsPublicInternetIpRefreshDue(string? providerObservedIp) {
            try {
                DataRow? row = _sipSql.LoadPublicInternetIpState();
                if (row == null) {
                    return true;
                }

                string storedIPv4 = ReadRowString(row, "PublicInternetIPv4");
                string storedIPv6 = ReadRowString(row, "PublicInternetIPv6");
                if (string.IsNullOrWhiteSpace(storedIPv4) && string.IsNullOrWhiteSpace(storedIPv6)) {
                    return true;
                }

                DateTime? lastCheckedUtc = ReadNullableDateTime(row, "PublicInternetIPLastCheckedAt");
                if (!lastCheckedUtc.HasValue || DateTime.UtcNow - DateTime.SpecifyKind(lastCheckedUtc.Value, DateTimeKind.Utc) >= PublicIpRefreshInterval) {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(providerObservedIp) && !IsConfiguredServerIp(providerObservedIp)) {
                    if (IPAddress.TryParse(providerObservedIp, out IPAddress? observed)) {
                        string stored = observed.AddressFamily == AddressFamily.InterNetworkV6 ? storedIPv6 : storedIPv4;
                        return !string.Equals(stored, observed.ToString(), StringComparison.OrdinalIgnoreCase);
                    }
                }
            } catch (Exception ex) {
                _logger?.Warn($"ProviderRegistration Public-IP Status konnte nicht gelesen werden | Fehler={ex.Message}");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Ermittelt IPv4 und IPv6 ueber externe HTTP-Endpunkte. Provider-Antworten sind nur Trigger, nicht Quelle.
        /// </summary>
        private async Task<PublicInternetIpProbeResult> ProbePublicInternetIpAsync() {
            IReadOnlyList<IPAddress> configuredServerIps = LoadConfiguredServerIpsForPublicIpCheck();
            string? ipv4 = await ProbePublicInternetAddressFamilyAsync(AddressFamily.InterNetwork, configuredServerIps).ConfigureAwait(false);
            string? ipv6 = await ProbePublicInternetAddressFamilyAsync(AddressFamily.InterNetworkV6, configuredServerIps).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(ipv4) && string.IsNullOrWhiteSpace(ipv6)) {
                return PublicInternetIpProbeResult.Fail("Keine oeffentliche IPv4/IPv6 ermittelbar.");
            }

            return PublicInternetIpProbeResult.Ok(ipv4, ipv6, "Public-IP HTTP Probe");
        }

        /// <summary>
        /// Ermittelt eine oeffentliche IP fuer eine konkrete Adressfamilie.
        /// </summary>
        private static async Task<string?> ProbePublicInternetAddressFamilyAsync(AddressFamily family, IReadOnlyList<IPAddress> configuredServerIps) {
            string[] urls = family == AddressFamily.InterNetworkV6
                ? new[] { "https://api6.ipify.org", "https://ipv6.icanhazip.com" }
                : new[] { "https://api.ipify.org", "https://ipv4.icanhazip.com", "https://checkip.amazonaws.com" };

            foreach (string url in urls) {
                try {
                    using HttpClient client = new() { Timeout = PublicIpProbeTimeout };
                    using HttpResponseMessage response = await client.GetAsync(url).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode) {
                        continue;
                    }

                    string text = (await response.Content.ReadAsStringAsync().ConfigureAwait(false)).Trim();
                    int lineEnd = text.IndexOfAny(new[] { '\r', '\n', ' ', '\t' });
                    if (lineEnd >= 0) {
                        text = text.Substring(0, lineEnd).Trim();
                    }

                    if (IPAddress.TryParse(text, out IPAddress? parsed) && parsed.AddressFamily == family && !IsConfiguredServerIp(parsed, configuredServerIps)) {
                        return parsed.ToString();
                    }
                } catch {
                }
            }

            return null;
        }

        /// <summary>
        /// Laedt die lokal konfigurierten Server-Bind-IPs aus den ServerSettings.
        /// Diese Pruefung ersetzt die vorherige feste Privatnetz-Ablehnung.
        /// </summary>
        private IReadOnlyList<IPAddress> LoadConfiguredServerIpsForPublicIpCheck() {
            try {
                return _sipSql.LoadServerSettings().ServerIPs;
            } catch {
                return Array.Empty<IPAddress>();
            }
        }

        /// <summary>
        /// Prueft nur, ob ein Provider-/HTTP-IP-Kandidat eine der konfigurierten lokalen Server-Bind-IPs ist.
        /// Es werden keine festen Privatnetzbereiche mehr abgelehnt.
        /// </summary>
        private bool IsConfiguredServerIp(string value) {
            if (!IPAddress.TryParse(value?.Trim(), out IPAddress? parsed)) {
                return true;
            }

            return IsConfiguredServerIp(parsed, LoadConfiguredServerIpsForPublicIpCheck());
        }

        /// <summary>
        /// Prueft eine IP gegen die lokal konfigurierten Server-Bind-IPs.
        /// </summary>
        private static bool IsConfiguredServerIp(IPAddress address, IReadOnlyList<IPAddress> configuredServerIps) {
            if (address == null) {
                return true;
            }

            if (address.IsIPv4MappedToIPv6) {
                address = address.MapToIPv4();
            }

            foreach (IPAddress configured in configuredServerIps ?? Array.Empty<IPAddress>()) {
                IPAddress candidate = configured.IsIPv4MappedToIPv6 ? configured.MapToIPv4() : configured;
                if (candidate.Equals(address)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Liest eine optionale DateTime aus einem DataRow.
        /// </summary>
        private static DateTime? ReadNullableDateTime(DataRow row, string columnName) {
            if (row == null || !row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value) {
                return null;
            }

            return Convert.ToDateTime(row[columnName]);
        }

        /// <summary>
        /// Liest einen optionalen String aus einem DataRow.
        /// </summary>
        private static string ReadRowString(DataRow row, string columnName) {
            return row != null && row.Table.Columns.Contains(columnName) && row[columnName] != DBNull.Value
                ? Convert.ToString(row[columnName])?.Trim() ?? string.Empty
                : string.Empty;
        }

        /// <summary>
        /// Ergebnis der Public-IP-Ermittlung.
        /// </summary>
        private sealed class PublicInternetIpProbeResult {
            public bool Success { get; init; }
            public string? IPv4 { get; init; }
            public string? IPv6 { get; init; }
            public string? Source { get; init; }
            public string? ErrorText { get; init; }

            public static PublicInternetIpProbeResult Ok(string? ipv4, string? ipv6, string source) {
                return new PublicInternetIpProbeResult { Success = true, IPv4 = ipv4, IPv6 = ipv6, Source = source };
            }

            public static PublicInternetIpProbeResult Fail(string errorText) {
                return new PublicInternetIpProbeResult { Success = false, ErrorText = errorText };
            }
        }

        /// <summary>
        /// The WireTraceEvents
        /// </summary>
        /// <param name="transport">The transport<see cref="SIPTransport"/></param>
        private void WireTraceEvents(SIPTransport transport) {
            transport.SIPRequestOutTraceEvent += (lep, rep, req) => {
                try {
                    if (req == null || req.Method != SIPMethodsEnum.REGISTER) {
                        return;
                    }

                    int? providerId = TryResolveProviderIdForRegister(req);
                    ProviderRegistrationItem? item = TryGetProviderItem(providerId);
                    string requestUri = req.URI?.ToString() ?? "-";
                    string rawRequest = req.ToString() ?? string.Empty;

                    if (providerId.HasValue) {
                        RememberRawRegister(providerId.Value, req);
                    }

                    if (item != null) {
                        item.LastRemoteEndPoint = rep?.ToString();
                        item.LastResolvedIP = ExtractIpAddress(rep?.ToString());
                        item.LastRequestUri = requestUri;
                        item.LastCallID = req.Header?.CallId;
                    }

                    _logger?.Info($"=> REGISTER | ProviderID={providerId?.ToString() ?? "-"} | Provider={item?.ProviderName ?? "-"} | Local={lep} | Remote={rep} | URI={requestUri}");
                    // voraussichtlich debug level
                    _logger?.Debug($"SIP RAW OUT | Type=REGISTER | ProviderID={providerId?.ToString() ?? "-"} | Provider={item?.ProviderName ?? "-"} | Local={lep} | Remote={rep}\r\n{rawRequest}");

                    if (item != null) {
                        SaveRegistrationState(item, false, null, item.LastRequestUri, null);
                        WriteRegistrationLog(item.ProviderId, "RegisterOut", null, null, item.LastRemoteEndPoint, item.LastRequestUri, item.ProtocolText, item.LastCallID, rawRequest, null, null);
                    }
                } catch (Exception ex) {
                    _logger?.Warn($"ProviderRegistration REGISTER-Out Tracefehler | Fehler={ex.Message}");
                }
            };

            transport.SIPResponseInTraceEvent += (lep, rep, resp) => {
                try {
                    if (resp == null || resp.Header?.CSeqMethod != SIPMethodsEnum.REGISTER) {
                        return;
                    }

                    int? providerId = TryResolveProviderIdForRegisterResponse(resp);
                    ProviderRegistrationItem? item = TryGetProviderItem(providerId);
                    string rawResponse = resp.ToString() ?? string.Empty;
                    int statusCode = (int)resp.Status;
                    string reasonPhrase = resp.ReasonPhrase ?? string.Empty;

                    if (providerId.HasValue) {
                        RememberRawRegisterResponse(providerId.Value, resp);
                    }

                    if (item != null) {
                        item.LastRemoteEndPoint = rep?.ToString();
                        item.LastResolvedIP = ExtractIpAddress(rep?.ToString());
                        item.LastCallID = resp.Header?.CallId;
                        item.LastServiceRoute = ExtractServiceRoute(resp);
                        item.LastAssociatedUri = ExtractAssociatedUri(resp);
                        UpdateObservedPublicNetwork(item, resp);
                    }

                    _logger?.Info($"<= REGISTER | ProviderID={providerId?.ToString() ?? "-"} | Provider={item?.ProviderName ?? "-"} | Local={lep} | Remote={rep} | Status={statusCode} {reasonPhrase}");
                    // voraussichtlich debug level
                    _logger?.Debug($"SIP RAW IN | Type=REGISTER | ProviderID={providerId?.ToString() ?? "-"} | Provider={item?.ProviderName ?? "-"} | Local={lep} | Remote={rep} | Status={statusCode} {reasonPhrase}\r\n{rawResponse}");

                    if (item != null) {
                        SaveRegistrationState(item, resp.Status == SIPResponseStatusCodesEnum.Ok, resp, item.LastRequestUri, null);
                        WriteRegistrationLog(item.ProviderId, "RegisterIn", resp, null, item.LastRemoteEndPoint, item.LastRequestUri, item.ProtocolText, item.LastCallID, null, rawResponse, null);
                    }
                } catch (Exception ex) {
                    _logger?.Warn($"ProviderRegistration REGISTER-In Tracefehler | Fehler={ex.Message}");
                }
            };
        }

        /// <summary>
        /// The TryResolveProviderIdForRegister
        /// </summary>
        /// <param name="req">The req<see cref="SIPRequest?"/></param>
        /// <returns>The <see cref="int?"/></returns>
        private int? TryResolveProviderIdForRegister(SIPRequest? req) {
            if (req?.Header == null) {
                return null;
            }

            string? callId = req.Header.CallId;
            if (!string.IsNullOrWhiteSpace(callId)) {
                lock (_sync) {
                    if (_registerCallIdToProviderId.TryGetValue(callId, out int knownProviderId)) {
                        return knownProviderId;
                    }
                }
            }

            string requestUser = NormalizeNumber(req.Header.To?.ToURI?.User ?? req.Header.From?.FromURI?.User ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(requestUser)) {
                lock (_sync) {
                    foreach (ProviderRegistrationItem item in _items.Values) {
                        if (NormalizeNumber(item.SipUser).Equals(requestUser, StringComparison.OrdinalIgnoreCase)) {
                            if (!string.IsNullOrWhiteSpace(callId)) {
                                _registerCallIdToProviderId[callId] = item.ProviderId;
                            }
                            return item.ProviderId;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// The TryResolveProviderIdForRegisterResponse
        /// </summary>
        /// <param name="resp">The resp<see cref="SIPResponse?"/></param>
        /// <returns>The <see cref="int?"/></returns>
        private int? TryResolveProviderIdForRegisterResponse(SIPResponse? resp) {
            string? callId = resp?.Header?.CallId;
            if (string.IsNullOrWhiteSpace(callId)) {
                return null;
            }

            lock (_sync) {
                if (_registerCallIdToProviderId.TryGetValue(callId, out int providerId)) {
                    return providerId;
                }
            }

            return null;
        }

        /// <summary>
        /// The TryGetProviderItem
        /// </summary>
        /// <param name="providerId">The providerId<see cref="int?"/></param>
        /// <returns>The <see cref="ProviderRegistrationItem?"/></returns>
        private ProviderRegistrationItem? TryGetProviderItem(int? providerId) {
            if (!providerId.HasValue) {
                return null;
            }

            lock (_sync) {
                return _items.TryGetValue(providerId.Value, out ProviderRegistrationItem? item) ? item : null;
            }
        }

        /// <summary>
        /// The RememberRawRegister
        /// </summary>
        /// <param name="providerId">The providerId<see cref="int"/></param>
        /// <param name="req">The req<see cref="SIPRequest?"/></param>
        private void RememberRawRegister(int providerId, SIPRequest? req) {
            if (req == null) {
                return;
            }

            lock (_sync) {
                _lastRawRegisterByProviderId[providerId] = req.ToString();
                if (!string.IsNullOrWhiteSpace(req.Header?.CallId)) {
                    _registerCallIdToProviderId[req.Header.CallId] = providerId;
                }
            }
        }

        /// <summary>
        /// The RememberRawRegisterResponse
        /// </summary>
        /// <param name="providerId">The providerId<see cref="int"/></param>
        /// <param name="resp">The resp<see cref="SIPResponse?"/></param>
        private void RememberRawRegisterResponse(int providerId, SIPResponse? resp) {
            if (resp == null) {
                return;
            }

            lock (_sync) {
                _lastRawRegisterResponseByProviderId[providerId] = resp.ToString();
                if (!string.IsNullOrWhiteSpace(resp.Header?.CallId)) {
                    _registerCallIdToProviderId[resp.Header.CallId] = providerId;
                }
            }
        }

        /// <summary>
        /// The GetLastRawRequest
        /// </summary>
        /// <param name="providerId">The providerId<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        private string GetLastRawRequest(int providerId) {
            lock (_sync) {
                return _lastRawRegisterByProviderId.TryGetValue(providerId, out string? raw) && !string.IsNullOrWhiteSpace(raw) ? raw : string.Empty;
            }
        }

        /// <summary>
        /// The GetLastRawResponse
        /// </summary>
        /// <param name="providerId">The providerId<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        private string GetLastRawResponse(int providerId) {
            lock (_sync) {
                return _lastRawRegisterResponseByProviderId.TryGetValue(providerId, out string? raw) && !string.IsNullOrWhiteSpace(raw) ? raw : string.Empty;
            }
        }

        /// <summary>
        /// The ExtractServiceRoute
        /// </summary>
        /// <param name="resp">The resp<see cref="SIPResponse?"/></param>
        /// <returns>The <see cref="string?"/></returns>
        private static string? ExtractServiceRoute(SIPResponse? resp) {
            if (resp?.Header?.UnknownHeaders == null) {
                return null;
            }

            foreach (string header in resp.Header.UnknownHeaders) {
                if (header.StartsWith("Service-Route:", StringComparison.OrdinalIgnoreCase)) {
                    return header.Substring(header.IndexOf(':') + 1).Trim();
                }
            }

            return null;
        }

        /// <summary>
        /// The ExtractAssociatedUri
        /// </summary>
        /// <param name="resp">The resp<see cref="SIPResponse?"/></param>
        /// <returns>The <see cref="string?"/></returns>
        private static string? ExtractAssociatedUri(SIPResponse? resp) {
            if (resp?.Header?.UnknownHeaders == null) {
                return null;
            }

            foreach (string header in resp.Header.UnknownHeaders) {
                if (header.StartsWith("P-Associated-URI:", StringComparison.OrdinalIgnoreCase)) {
                    return header.Substring(header.IndexOf(':') + 1).Trim();
                }
            }

            return null;
        }

        /// <summary>
        /// The ExtractExpiresSeconds
        /// </summary>
        /// <param name="resp">The resp<see cref="SIPResponse?"/></param>
        /// <returns>The <see cref="int?"/></returns>
        private static int? ExtractExpiresSeconds(SIPResponse? resp) {
            if (resp?.Header?.Contact != null) {
                foreach (var contact in resp.Header.Contact) {
                    if (contact?.Expires != null && contact.Expires > 0) {
                        return Convert.ToInt32(contact.Expires);
                    }
                }
            }

            if (resp?.Header?.Expires != null && resp.Header.Expires > 0) {
                return Convert.ToInt32(resp.Header.Expires);
            }

            return null;
        }

        /// <summary>
        /// The BuildRequestUriText
        /// </summary>
        /// <param name="item">The item<see cref="ProviderRegistrationItem"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string BuildRequestUriText(ProviderRegistrationItem item) {
            return BuildRegistrarUriText(item, ParseProtocol(item.ProtocolText));
        }

        /// <summary>
        /// The BuildRegistrarUriText
        /// </summary>
        /// <param name="item">The item<see cref="ProviderRegistrationItem"/></param>
        /// <param name="protocol">The protocol<see cref="SIPProtocolsEnum"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string BuildRegistrarUriText(ProviderRegistrationItem item, SIPProtocolsEnum protocol) {
            string scheme = protocol == SIPProtocolsEnum.tls ? "sips" : "sip";
            bool includePort = item.RegistrarPort > 0 && item.RegistrarPort != SIPConstants.GetDefaultPort(protocol);

            string uri = includePort
                ? $"{scheme}:{item.RegistrarHost}:{item.RegistrarPort}"
                : $"{scheme}:{item.RegistrarHost}";

            if (protocol != SIPProtocolsEnum.udp && protocol != SIPProtocolsEnum.tls) {
                uri += $";transport={protocol.ToString().ToLowerInvariant()}";
            }

            return uri;
        }

        /// <summary>
        /// The BuildAccountAorHost
        /// </summary>
        /// <param name="item">The item<see cref="ProviderRegistrationItem"/></param>
        /// <param name="protocol">The protocol<see cref="SIPProtocolsEnum"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string BuildAccountAorHost(ProviderRegistrationItem item, SIPProtocolsEnum protocol) {
            bool includePort = item.RegistrarPort > 0 && item.RegistrarPort != SIPConstants.GetDefaultPort(protocol);
            return includePort ? $"{item.RegistrarHost}:{item.RegistrarPort}" : item.RegistrarHost;
        }

        /// <summary>
        /// The ExtractIpAddress
        /// </summary>
        /// <param name="endPointText">The endPointText<see cref="string?"/></param>
        /// <returns>The <see cref="string?"/></returns>
        private static string? ExtractIpAddress(string? endPointText) {
            if (string.IsNullOrWhiteSpace(endPointText)) {
                return null;
            }

            string value = endPointText.Trim();
            int firstColon = value.IndexOf(':');
            if (firstColon >= 0 && firstColon + 1 < value.Length) {
                value = value.Substring(firstColon + 1);
            }

            int lastColon = value.LastIndexOf(':');
            if (lastColon > 0) {
                value = value.Substring(0, lastColon);
            }

            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        /// <summary>
        /// The NormalizeProviderProtocol
        /// </summary>
        /// <param name="dbProtocol">The dbProtocol<see cref="string?"/></param>
        /// <param name="settingsProtocol">The settingsProtocol<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string NormalizeProviderProtocol(string? dbProtocol, string settingsProtocol) {
            string value = string.IsNullOrWhiteSpace(dbProtocol) ? settingsProtocol : dbProtocol;
            string normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
            return normalized switch {
                "UDP" => "UDP",
                "TCP" => "TCP",
                "TLS" => "TLS",
                _ => throw new InvalidOperationException($"Provider SIPProtokoll fehlt oder ist ungültig: {value}")
            };
        }

        /// <summary>
        /// The ResolveProviderPort
        /// </summary>
        /// <param name="dbPortValue">The dbPortValue<see cref="object"/></param>
        /// <param name="settingsPort">The settingsPort<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int ResolveProviderPort(object dbPortValue, int settingsPort) {
            int port = dbPortValue == DBNull.Value ? settingsPort : Convert.ToInt32(dbPortValue);
            if (port <= 0 || port > IPEndPoint.MaxPort) {
                throw new InvalidOperationException($"Provider SIPPort fehlt oder ist ungültig: {port}");
            }

            return port;
        }

        /// <summary>
        /// The FirstNotEmpty
        /// </summary>
        /// <param name="values">The values<see cref="string[]"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string FirstNotEmpty(params string[] values) {
            foreach (string value in values) {
                if (!string.IsNullOrWhiteSpace(value)) {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// The SanitizeHost
        /// </summary>
        /// <param name="value">The value<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string SanitizeHost(string value) {
            value = value.Trim();

            if (value.StartsWith("sip:", StringComparison.OrdinalIgnoreCase)) {
                value = value.Substring(4);
            } else if (value.StartsWith("sips:", StringComparison.OrdinalIgnoreCase)) {
                value = value.Substring(5);
            }

            int semi = value.IndexOf(';');
            if (semi >= 0) {
                value = value.Substring(0, semi);
            }

            int slash = value.IndexOf('/');
            if (slash >= 0) {
                value = value.Substring(0, slash);
            }

            int at = value.LastIndexOf('@');
            if (at >= 0) {
                value = value.Substring(at + 1);
            }

            if (value.Count(c => c == ':') == 1) {
                int colon = value.LastIndexOf(':');
                if (colon > 0 && int.TryParse(value.Substring(colon + 1), out _)) {
                    value = value.Substring(0, colon);
                }
            }

            return value.Trim();
        }

        /// <summary>
        /// The Dispose
        /// </summary>
        public void Dispose() {
            Stop();
        }
    }
}
