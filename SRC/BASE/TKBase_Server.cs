/*
 * TKBase_Server
 * Aufgabe:
 * - Host und Transport-Dispatcher.
 * - Kontrolliert nur Ã¼ber Manager bzw. Bereichs-CallController.
 * - EnthÃ¤lt keine Leg-A-/Leg-B-Logik, kein direktes RTP/SDP, keine GSM-AT-Logik und kein REGISTER-Parsing.
 *
 * Ablaufvorgabe:
 * TKBase_Server
 * â”œâ”€â”€ TKBase_RegistrationManager
 * â”œâ”€â”€ TKBase_CallManager
 * â”‚   â”œâ”€â”€ TKBase_CallManagerForkingAgent
 * â”‚   â””â”€â”€ Bereichscontroller fuehren nur eigene Legs
 * â””â”€â”€ TKBase_RegistrationManager
 */

namespace TKServer.BASE {
    using SIPSorcery.SIP;
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Net;
    using System.Threading.Tasks;

    using TKServer.SQL;
    /// TKBase_Server
    /// Aufgabe:
    /// - Host und Transport-Dispatcher.
    /// - Kontrolliert nur Ã¼ber Manager bzw. Bereichs-CallController.
    /// - EnthÃ¤lt keine Leg-A-/Leg-B-Logik, kein direktes RTP/SDP, keine GSM-AT-Logik und kein REGISTER-Parsing.
    /// 
    /// Ablaufvorgabe:
    /// TKBase_Server
    /// â”œâ”€â”€ TKBase_RegistrationManager
    /// â”œâ”€â”€ TKBase_CallManager
    /// â”‚   â”œâ”€â”€ TKBase_CallManagerForkingAgent
    /// â”‚   â””â”€â”€ Bereichscontroller fuehren nur eigene Legs
    /// â””â”€â”€ TKBase_RegistrationManager

    /// <summary>
    /// Defines the <see cref="TKBase_Server" />
    /// </summary>
    public sealed class TKBase_Server : IDisposable {
        /// <summary>
        /// Defines the TransportConsumerName
        /// </summary>
        private const string TransportConsumerName = "TKBase_Server";

        /// <summary>
        /// Defines the _logger
        /// </summary>
        private readonly TKBase_LogManager? _logger;

        /// <summary>
        /// Defines the _sessions
        /// </summary>
        private readonly TKBase_SessionManager _sessions;

        /// <summary>
        /// Defines the _routing
        /// </summary>
        private readonly TKBase_RoutingManager _routing;

        /// <summary>
        /// Defines the _settings
        /// </summary>
        private readonly TKBase_ServerSettings _settings;

        /// <summary>
        /// Defines the _mediaManager
        /// </summary>
        private readonly TKBase_MediaManager _mediaManager;

        /// <summary>
        /// Defines the _dtmfManager
        /// </summary>
        private readonly TKBase_DTMF_Manager _dtmfManager;

        /// <summary>
        /// Defines the _audioCodecs
        /// </summary>
        private readonly IReadOnlyList<TKBase_AudioCodecConfig> _audioCodecs;

        /// <summary>
        /// Defines the _registrationManager
        /// </summary>
        private readonly TKBase_RegistrationManager _registrationManager;

        /// <summary>
        /// Defines the _callManager
        /// </summary>
        private readonly TKBase_CallManager _callManager;

        /// <summary>
        /// Defines the _transport
        /// </summary>
        private SIPTransport? _transport;

        /// <summary>
        /// Defines the _isStarted
        /// </summary>
        private bool _isStarted;

        /// <summary>
        /// Defines the _transportOwnedByHost
        /// </summary>
        private bool _transportOwnedByHost;

        /// <summary>
        /// Defines the ServerStatusChanged
        /// </summary>
        public event Action<string>? ServerStatusChanged;

        /// <summary>
        /// Gets a value indicating whether IsStarted
        /// </summary>
        public bool IsStarted => _isStarted;

        /// <summary>
        /// Applies runtime area availability inside the SIP core.
        /// </summary>
        /// <param name="sipIntEnabled">Whether SipInt is enabled.</param>
        /// <param name="sipExtEnabled">Whether SIPExt/provider handling is enabled.</param>
        public void ConfigureRuntimeAreas(bool sipIntEnabled, bool sipExtEnabled) {
            _registrationManager.ConfigureRuntimeAreas(sipIntEnabled);
            _callManager.ConfigureRuntimeAreas(sipIntEnabled, sipExtEnabled);
        }

        /// <summary>
        /// Gets the ListenPort
        /// </summary>
        public int ListenPort { get; private set; }

        /// <summary>
        /// Gets the ListenTransport
        /// </summary>
        public string ListenTransport { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the Sessions
        /// </summary>
        public TKBase_SessionManager Sessions => _sessions;

        /// <summary>
        /// Initializes a new instance of the <see cref="TKBase_Server"/> class.
        /// </summary>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        public TKBase_Server(TKBase_LogManager? logger = null) {
            _logger = logger;
            _settings = new TKSQL_Commands(_logger).LoadServerSettings();
            ListenTransport = _settings.SipTransport;
            ListenPort = _settings.SipPort;
            EnsureMediaFolders();

            _sessions = new TKBase_SessionManager(logger);
            _routing = new TKBase_RoutingManager(logger);
            _mediaManager = new TKBase_MediaManager(logger, _settings.RtpPortStart, _settings.RtpPortEnd);
            _dtmfManager = new TKBase_DTMF_Manager(logger);
            _audioCodecs = new TKSQL_Commands(_logger).GetActiveAudioCodecs();
            if (_audioCodecs.Count == 0) {
                throw new InvalidOperationException("Keine aktiven Audiocodecs in TK_AudioCodec gefunden.");
            }
            _registrationManager = new TKBase_RegistrationManager(logger, _settings);
            _callManager = new TKBase_CallManager(_sessions, _routing, _settings, _mediaManager, _dtmfManager, _audioCodecs, logger);

            _logger?.Info($"Servereinstellungen geladen | ServerIP={_settings.ServerIP} | Registrar={_settings.Registrar} | Realm={_settings.Realm} | SIP={ListenTransport}/{ListenPort} | RTP={_settings.RtpPortStart}-{_settings.RtpPortEnd} | SRTP={_settings.UseSRTP} | RegisterExpire={_settings.RegisterExpire} | SIPCodec={_settings.SipCodecId} | GSMCodec={_settings.GsmCodecId} | AudioCodecs={_audioCodecs.Count} | UseAdsProfileMediaPath={_settings.UseAdsProfileMediaPath} | UserMediaRootPath={_settings.UserMediaRootPath ?? "-"} | UserMediaFolderName={_settings.UserMediaFolderName}");
        }

        /// <summary>
        /// The EnsureMediaFolders
        /// </summary>
        private void EnsureMediaFolders() {
            string mediaRoot = _settings.ResolveMediaRootPath(AppContext.BaseDirectory);
            string systemRoot = _settings.ResolveSystemMediaFolderPath(AppContext.BaseDirectory);
            string userRoot = _settings.ResolveServerUserMediaRootPath(AppContext.BaseDirectory);

            try {
                Directory.CreateDirectory(systemRoot);
                Directory.CreateDirectory(userRoot);

                List<DataRow> users = new TKSQL_Commands(_logger).GetActiveSipUsers();
                int createdOrExisting = 0;

                foreach (DataRow user in users) {
                    string username = Convert.ToString(user["Username"])?.Trim() ?? string.Empty;
                    string? userFolder = _settings.ResolveUserMediaFolderPath(user, AppContext.BaseDirectory);
                    if (string.IsNullOrWhiteSpace(userFolder)) {
                        _logger?.Warn($"Media User-Ordner Ã¼bersprungen | UserID={user["ID"]} | Username={username} | Grund=Kein gÃ¼ltiger Ordnername oder ADS-Profilpfad fehlt");
                        continue;
                    }

                    Directory.CreateDirectory(userFolder);
                    Directory.CreateDirectory(Path.Combine(userFolder, "Recordings"));
                    Directory.CreateDirectory(Path.Combine(userFolder, "Prompts"));
                    createdOrExisting++;
                }

                _logger?.Info($"Media Ordner geprÃ¼ft | Root={mediaRoot} | UserRoot={userRoot} | ADSProfile={_settings.UseAdsProfileMediaPath} | UserFolderName={_settings.UserMediaFolderName} | UserOrdner={createdOrExisting}");
            } catch (Exception ex) {
                _logger?.Error($"Media Ordner konnten nicht geprÃ¼ft werden | Root={mediaRoot} | Fehler={ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// The IsValidMediaUserFolderName
        /// </summary>
        /// <param name="username">The username<see cref="string"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool IsValidMediaUserFolderName(string username) {
            if (string.IsNullOrWhiteSpace(username)) {
                return false;
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in username) {
                if (Array.IndexOf(invalidChars, c) >= 0) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The Start
        /// </summary>
        public void Start() {
            if (_isStarted) {
                ServerStatusChanged?.Invoke($"SIP Server lÃ¤uft bereits ({ListenTransport}/{ListenPort})");
                return;
            }

            try {
                _transport = TKBase_TransportHost.Acquire(TransportConsumerName, ListenTransport, ListenPort, _settings.ServerIPs, _logger);
                _transportOwnedByHost = true;

                _transport.SIPTransportRequestReceived += HandleIncomingRequestAsync;
                _transport.SIPTransportResponseReceived += HandleIncomingResponseAsync;

                _callManager.BindTransport(_transport, ListenPort, ListenTransport);

                _isStarted = true;
                string message = $"SIP Server gestartet ({ListenTransport}/{ListenPort})";
                _logger?.Info(message);
                ServerStatusChanged?.Invoke(message);
            } catch (Exception ex) {
                _logger?.Error($"SIP Server Start fehlgeschlagen | Transport={ListenTransport}/{ListenPort} | Fehler={ex.Message}");
                ServerStatusChanged?.Invoke($"SIP Server Start fehlgeschlagen ({ListenTransport}/{ListenPort})");

                try {
                    if (_transport != null) {
                        _transport.SIPTransportRequestReceived -= HandleIncomingRequestAsync;
                        _transport.SIPTransportResponseReceived -= HandleIncomingResponseAsync;
                    }
                } catch (Exception cleanupEx) {
                    _logger?.Warn($"SIP Server Start-Cleanup Event-Abmeldung fehlgeschlagen | Fehler={cleanupEx.Message}");
                }

                try {
                    if (_transportOwnedByHost) {
                        TKBase_TransportHost.Release(TransportConsumerName, _logger);
                    }
                } catch (Exception cleanupEx) {
                    _logger?.Warn($"SIP Server Start-Cleanup Transport-Freigabe fehlgeschlagen | Fehler={cleanupEx.Message}");
                }

                _transport = null;
                _transportOwnedByHost = false;
                _isStarted = false;
                throw;
            }
        }

        /// <summary>
        /// The Start
        /// </summary>
        /// <param name="transport">The transport<see cref="SIPTransport"/></param>
        public void Start(SIPTransport transport) {
            if (transport == null) throw new ArgumentNullException(nameof(transport));
            if (_isStarted) {
                ServerStatusChanged?.Invoke($"SIP Server lÃ¤uft bereits ({ListenTransport}/{ListenPort})");
                return;
            }

            try {
                _transport = transport;
                _transportOwnedByHost = false;

                _transport.SIPTransportRequestReceived += HandleIncomingRequestAsync;
                _transport.SIPTransportResponseReceived += HandleIncomingResponseAsync;

                _callManager.BindTransport(transport, ListenPort, ListenTransport);

                _isStarted = true;
                string message = $"SIP Server gestartet ({ListenTransport}/{ListenPort})";
                _logger?.Info(message);
                ServerStatusChanged?.Invoke(message);
            } catch (Exception ex) {
                _logger?.Error($"SIP Server Start mit vorhandenem Transport fehlgeschlagen | Transport={ListenTransport}/{ListenPort} | Fehler={ex.Message}");
                ServerStatusChanged?.Invoke($"SIP Server Start fehlgeschlagen ({ListenTransport}/{ListenPort})");

                try {
                    if (_transport != null) {
                        _transport.SIPTransportRequestReceived -= HandleIncomingRequestAsync;
                        _transport.SIPTransportResponseReceived -= HandleIncomingResponseAsync;
                    }
                } catch (Exception cleanupEx) {
                    _logger?.Warn($"SIP Server Start-Cleanup Event-Abmeldung fehlgeschlagen | Fehler={cleanupEx.Message}");
                }

                _transport = null;
                _transportOwnedByHost = false;
                _isStarted = false;
                throw;
            }
        }

        /// <summary>
        /// The Stop
        /// </summary>
        public void Stop() {
            if (!_isStarted && _transport == null) {
                return;
            }

            bool completedWithWarnings = false;

            try {
                if (_transport != null) {
                    try {
                        _transport.SIPTransportRequestReceived -= HandleIncomingRequestAsync;
                        _transport.SIPTransportResponseReceived -= HandleIncomingResponseAsync;
                    } catch (Exception ex) {
                        completedWithWarnings = true;
                        _logger?.Warn($"SIP Server Stop Event-Abmeldung fehlgeschlagen | Fehler={ex.Message}");
                    }
                }

                try {
                    _callManager.StopOwnedSessions();
                } catch (Exception ex) {
                    completedWithWarnings = true;
                    _logger?.Warn($"SIP Server Stop CallManager-Sessions fehlgeschlagen | Fehler={ex.Message}");
                }

                try {
                    if (_transportOwnedByHost) {
                        TKBase_TransportHost.Release(TransportConsumerName, _logger);
                    }
                } catch (Exception ex) {
                    completedWithWarnings = true;
                    _logger?.Warn($"SIP Server Stop Transport-Freigabe fehlgeschlagen | Fehler={ex.Message}");
                }
            } finally {
                _transport = null;
                _transportOwnedByHost = false;
                _isStarted = false;
            }

            string message = "SIP Server gestoppt";
            _logger?.Info(message);
            if (completedWithWarnings) {
                _logger?.Warn("SIP Server Stop abgeschlossen | Hinweis=Einzelne Cleanup-Schritte hatten Warnungen, Serverstatus wurde trotzdem zurÃ¼ckgesetzt.");
            }
            ServerStatusChanged?.Invoke(message);
        }

        /// <summary>
        /// The Dispose
        /// </summary>
        public void Dispose() => Stop();

        /// <summary>
        /// The HangupSessionAsync
        /// </summary>
        /// <param name="sessionId">The sessionId<see cref="string"/></param>
        /// <returns>The <see cref="Task{bool}"/></returns>
        public async Task<bool> HangupSessionAsync(string sessionId) {
            if (_transport == null || string.IsNullOrWhiteSpace(sessionId)) return false;
            return await _callManager.HangupSessionAsync(_transport, sessionId);
        }

        /// <summary>
        /// Beendet alle Sessions, die durch einen Bereichs-Stop betroffen sind.
        /// </summary>
        public async Task<int> HangupAreaSessionsAsync(string areaKey, string reason) {
            if (_transport == null || string.IsNullOrWhiteSpace(areaKey)) return 0;
            return await _callManager.HangupAreaSessionsAsync(_transport, areaKey, reason);
        }

        /// <summary>
        /// The NormalizeIncomingLocalEndPoint
        /// </summary>
        /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="remoteEndPoint">The remoteEndPoint<see cref="SIPEndPoint"/></param>
        /// <returns>The <see cref="SIPEndPoint"/></returns>
        private SIPEndPoint NormalizeIncomingLocalEndPoint(SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint) {
            IPAddress address = _settings.ResolveServerAddress(localEndPoint.Address, remoteEndPoint.Address);
            int port = localEndPoint.Port > 0 ? localEndPoint.Port : _settings.SipPort;
            return new SIPEndPoint(localEndPoint.Protocol, address, port, localEndPoint.ChannelID, localEndPoint.ConnectionID);
        }

        /// <summary>
        /// The HandleIncomingRequestAsync
        /// </summary>
        /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="remoteEndPoint">The remoteEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="request">The request<see cref="SIPRequest"/></param>
        /// <returns>The <see cref="Task"/></returns>
        private async Task HandleIncomingRequestAsync(SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIPRequest request) {
            if (request == null || _transport == null) return;

            SIPEndPoint receivedLocalEndPoint = localEndPoint;
            SIPEndPoint normalizedLocalEndPoint = NormalizeIncomingLocalEndPoint(localEndPoint, remoteEndPoint);

            if (request.Method == SIPMethodsEnum.REGISTER) {
                await _registrationManager.HandleSipIntRegisterAsync(_transport, normalizedLocalEndPoint, remoteEndPoint, request);
                return;
            }

            _logger?.Info($"SIP IN | Method={request.Method} | Local={normalizedLocalEndPoint} | ReceivedLocal={receivedLocalEndPoint} | Remote={remoteEndPoint} | CallID={request.Header?.CallId ?? "-"} | URI={request.URI}");
            _logger?.Debug($"SIP RAW IN BEGIN | Method={request.Method} | Local={normalizedLocalEndPoint} | ReceivedLocal={receivedLocalEndPoint} | Remote={remoteEndPoint} | CallID={request.Header?.CallId ?? "-"}{Environment.NewLine}{request}{Environment.NewLine}SIP RAW IN END | Method={request.Method} | CallID={request.Header?.CallId ?? "-"}");

            await _callManager.HandleIncomingRequestAsync(_transport, normalizedLocalEndPoint, remoteEndPoint, request, receivedLocalEndPoint);
        }

        /// <summary>
        /// The HandleIncomingResponseAsync
        /// </summary>
        /// <param name="localEndPoint">The localEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="remoteEndPoint">The remoteEndPoint<see cref="SIPEndPoint"/></param>
        /// <param name="response">The response<see cref="SIPResponse"/></param>
        /// <returns>The <see cref="Task"/></returns>
        private async Task HandleIncomingResponseAsync(SIPEndPoint localEndPoint, SIPEndPoint remoteEndPoint, SIPResponse response) {
            if (response == null || _transport == null) return;

            SIPEndPoint normalizedLocalEndPoint = NormalizeIncomingLocalEndPoint(localEndPoint, remoteEndPoint);
            await _callManager.HandleIncomingResponseAsync(_transport, normalizedLocalEndPoint, remoteEndPoint, response);
        }
    }
}
