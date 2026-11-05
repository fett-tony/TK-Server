/*
 * TKSVC_Routing
 * Aufgabe:
 * - Ermittelt Service-Ziele aus internen Service-Nummern.
 * - Arbeitet nur im Service-Bereich und fuehrt keine Session-/Command-Logik aus.
 */

namespace TKServer.SVC {
    using System;

    using TKServer.BASE;
    /// TKSVC_Routing
    /// Aufgabe:
    /// - Ermittelt Service-Ziele aus internen Service-Nummern.
    /// - Arbeitet nur im Service-Bereich und fuehrt keine Session-/Command-Logik aus.

    /// <summary>
    /// Defines the <see cref="TKSVC_Routing" />
    /// </summary>
    public sealed class TKSVC_Routing {
        /// <summary>
        /// Defines the _serviceRegistry
        /// </summary>
        private readonly TKSVC_ServiceRegistry _serviceRegistry;

        /// <summary>
        /// Defines the _logger
        /// </summary>
        private readonly TKBase_LogManager? _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TKSVC_Routing"/> class.
        /// </summary>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        /// <param name="serviceRegistry">The serviceRegistry<see cref="TKSVC_ServiceRegistry?"/></param>
        public TKSVC_Routing(TKBase_LogManager? logger = null, TKSVC_ServiceRegistry? serviceRegistry = null) {
            _logger = logger;
            _serviceRegistry = serviceRegistry ?? new TKSVC_ServiceRegistry(logger);
        }

        /// <summary>
        /// The ResolveInternalTarget
        /// </summary>
        /// <param name="calledNumber">The calledNumber<see cref="string"/></param>
        /// <returns>The <see cref="SIPIntResolvedTarget"/></returns>
        public SIPIntResolvedTarget ResolveInternalTarget(string calledNumber) {
            string normalized = NormalizeServiceNumber(calledNumber);
            if (string.IsNullOrWhiteSpace(normalized)) {
                return SIPIntResolvedTarget.Fail(calledNumber, "Keine interne Service-Nummer.");
            }

            if (!_serviceRegistry.TryGetByInternalNumber(normalized, out TKSVC_Session? serviceLeg) || serviceLeg == null) {
                _logger?.Warn($"SVC Routing fehlgeschlagen | Service unbekannt | Ziel={normalized}");
                return SIPIntResolvedTarget.Fail(normalized, "Service nicht gefunden.");
            }

            _logger?.Info($"SVC Routing OK | Ziel={normalized} | Type={serviceLeg.ServiceType} | Code={serviceLeg.ServiceCode}");
            return SIPIntResolvedTarget.OkService(normalized, serviceLeg);
        }

        /// <summary>
        /// The CanResolveDirectDialTarget
        /// </summary>
        /// <param name="calledNumber">The calledNumber<see cref="string?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public bool CanResolveDirectDialTarget(string? calledNumber) {
            string normalized = NormalizeDirectDialServiceNumber(calledNumber);
            return !string.IsNullOrWhiteSpace(normalized)
                && _serviceRegistry.TryGetByInternalNumber(normalized, out TKSVC_Session? serviceLeg)
                && serviceLeg != null;
        }

        /// <summary>
        /// The LooksLikeServiceNumber
        /// </summary>
        /// <param name="calledNumber">The calledNumber<see cref="string?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public static bool LooksLikeServiceNumber(string? calledNumber) {
            return !string.IsNullOrWhiteSpace(NormalizeServiceNumber(calledNumber));
        }

        /// <summary>
        /// The LooksLikeDirectDialServiceNumber
        /// </summary>
        /// <param name="calledNumber">The calledNumber<see cref="string?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public static bool LooksLikeDirectDialServiceNumber(string? calledNumber) {
            return !string.IsNullOrWhiteSpace(NormalizeDirectDialServiceNumber(calledNumber));
        }

        /// <summary>
        /// The NormalizeDirectDialServiceNumber
        /// </summary>
        /// <param name="calledNumber">The calledNumber<see cref="string?"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string NormalizeDirectDialServiceNumber(string? calledNumber) {
            string normalized = NormalizeServiceNumber(calledNumber);
            return normalized;
        }

        /// <summary>
        /// The NormalizeServiceNumber
        /// </summary>
        /// <param name="calledNumber">The calledNumber<see cref="string?"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string NormalizeServiceNumber(string? calledNumber) {
            if (string.IsNullOrWhiteSpace(calledNumber)) {
                return string.Empty;
            }

            string value = calledNumber.Trim();
            try { value = Uri.UnescapeDataString(value); } catch { }

            if (value.StartsWith("#", StringComparison.Ordinal)) {
                value = "*" + value.Substring(1);
            }

            if (!value.StartsWith("*", StringComparison.Ordinal) || value.Length < 4) {
                return string.Empty;
            }

            if (value.Length == 4) {
                for (int i = 1; i < value.Length; i++) {
                    if (!char.IsDigit(value[i])) {
                        return string.Empty;
                    }
                }

                return value;
            }

            if (!value.EndsWith("#", StringComparison.Ordinal)) {
                return string.Empty;
            }

            int optionStart = value.IndexOf('*', 1);
            if (optionStart != 4 || optionStart >= value.Length - 2) {
                return string.Empty;
            }

            for (int i = 1; i < 4; i++) {
                if (!char.IsDigit(value[i])) {
                    return string.Empty;
                }
            }

            for (int i = optionStart + 1; i < value.Length - 1; i++) {
                if (!char.IsDigit(value[i])) {
                    return string.Empty;
                }
            }

            return value;
        }
    }
}
