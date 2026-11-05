/*
 * TKGSM_Registration
 * Aufgabe:
 * - Startet, stoppt und synchronisiert die GSM-Registrierungs-/Gerätesicht.
 * - Nutzt DeviceMapping und DeviceRegister für echte GSM-Geräteerkennung und Persistenz.
 * - Keine SIP-REGISTER- oder Provider-Registrierung.
 */

namespace TKServer.GSM {
    using System.Threading;

    using TKServer.BASE;
    /// TKGSM_Registration
    /// Aufgabe:
    /// - Startet, stoppt und synchronisiert die GSM-Registrierungs-/Gerätesicht.
    /// - Nutzt DeviceMapping und DeviceRegister für echte GSM-Geräteerkennung und Persistenz.
    /// - Keine SIP-REGISTER- oder Provider-Registrierung.

    /// <summary>
    /// Defines the <see cref="TKGSM_Registration" />
    /// </summary>
    internal sealed class TKGSM_Registration {
        /// <summary>
        /// Defines the _logger
        /// </summary>
        private readonly TKBase_LogManager? _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TKGSM_Registration"/> class.
        /// </summary>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        public TKGSM_Registration(TKBase_LogManager? logger = null) {
            _logger = logger;
        }

        /// <summary>
        /// The StartService
        /// </summary>
        /// <param name="sessionManager">The sessionManager<see cref="TKBase_SessionManager"/></param>
        public void StartService(TKBase_SessionManager sessionManager) {
            TKBase_GsmGateway.ConfigureLogger(_logger);
            TKBase_GsmGateway.ConfigureSessionManager(sessionManager);
            TKBase_GsmGateway.Start();
            _logger?.Info("GSM Registrierung/Dienst gestartet.");
        }

        /// <summary>
        /// The StopService
        /// </summary>
        public void StopService() {
            TKBase_GsmGateway.Stop();
            _logger?.Info("GSM Registrierung/Dienst gestoppt.");
        }

        /// <summary>
        /// The RunOnce
        /// </summary>
        public void RunOnce() {
            TKBase_GsmGateway.RunOnce();
            _logger?.Info("GSM Registrierung Einzelzyklus angefordert.");
        }

        /// <summary>
        /// The DiscoverAndRegister
        /// </summary>
        /// <param name="cancellationToken">The cancellationToken<see cref="CancellationToken"/></param>
        public void DiscoverAndRegister(CancellationToken cancellationToken = default) {
            TKGSM_DeviceRegister register = new TKGSM_DeviceRegister(_logger);
            register.Execute(cancellationToken);
            _logger?.Info("GSM Registrierung synchronisiert.");
        }
    }
}
