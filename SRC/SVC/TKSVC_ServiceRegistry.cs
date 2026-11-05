namespace TKServer.SVC {
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using TKServer.BASE;
    using TKServer.SQL;
    /// <summary>
    /// Defines the <see cref="TKSVC_ServiceRegistry" />
    /// </summary>
    public sealed class TKSVC_ServiceRegistry {
        /// <summary>
        /// Defines the _sipSql
        /// </summary>
        private readonly TKSQL_Commands _sipSql;

        /// <summary>
        /// Defines the _logger
        /// </summary>
        private readonly TKBase_LogManager? _logger;

        /// <summary>
        /// Defines the _syncRoot
        /// </summary>
        private readonly object _syncRoot = new object();

        /// <summary>
        /// Defines the _servicesByInternalNumber
        /// </summary>
        private Dictionary<string, TKSVC_Session> _servicesByInternalNumber = new Dictionary<string, TKSVC_Session>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Defines the _servicesByCode
        /// </summary>
        private Dictionary<string, TKSVC_Session> _servicesByCode = new Dictionary<string, TKSVC_Session>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of the <see cref="TKSVC_ServiceRegistry"/> class.
        /// </summary>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        public TKSVC_ServiceRegistry(TKBase_LogManager? logger = null) {
            _logger = logger;
            _sipSql = new TKSQL_Commands(logger);
        }

        /// <summary>
        /// The Reload
        /// </summary>
        /// <returns>The <see cref="int"/></returns>
        public int Reload() {
            List<TKSVC_Session> activeServices = _sipSql.GetActiveServiceLegs();

            Dictionary<string, TKSVC_Session> byInternalNumber = new Dictionary<string, TKSVC_Session>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, TKSVC_Session> byCode = new Dictionary<string, TKSVC_Session>(StringComparer.OrdinalIgnoreCase);

            foreach (TKSVC_Session service in activeServices.OrderBy(x => x.SortOrder).ThenBy(x => x.InternalNumber, StringComparer.OrdinalIgnoreCase)) {
                if (!service.IsValid) {
                    _logger?.Warn($"SIP Service übersprungen | Ungültiger Datensatz | ID={service.ID} | Number={service.InternalNumber} | Type={service.ServiceType}");
                    continue;
                }

                if (!TKSVC_ServiceTypes.IsKnown(service.ServiceType)) {
                    _logger?.Warn($"SIP ServiceType unbekannt | ID={service.ID} | Number={service.InternalNumber} | Type={service.ServiceType}");
                }

                string normalizedInternalNumber = TKSVC_Routing.NormalizeServiceNumber(service.InternalNumber);
                if (string.IsNullOrWhiteSpace(normalizedInternalNumber)) {
                    _logger?.Warn($"SIP Service übersprungen | InternalNumber ungültig | ID={service.ID} | Number={service.InternalNumber}");
                    continue;
                }

                if (byInternalNumber.ContainsKey(normalizedInternalNumber)) {
                    _logger?.Warn($"SIP Service doppelte InternalNumber gefunden | Number={normalizedInternalNumber} | ID={service.ID}");
                    continue;
                }

                byInternalNumber[normalizedInternalNumber] = service;

                if (!string.IsNullOrWhiteSpace(service.ServiceCode) && !byCode.ContainsKey(service.ServiceCode)) {
                    byCode[service.ServiceCode] = service;
                }
            }

            lock (_syncRoot) {
                _servicesByInternalNumber = byInternalNumber;
                _servicesByCode = byCode;
            }

            _logger?.Info($"SIP ServiceRegistry geladen | ActiveServices={byInternalNumber.Count}");
            return byInternalNumber.Count;
        }

        /// <summary>
        /// The EnsureLoaded
        /// </summary>
        /// <returns>The <see cref="bool"/></returns>
        public bool EnsureLoaded() {
            lock (_syncRoot) {
                if (_servicesByInternalNumber.Count > 0) {
                    return true;
                }
            }

            return Reload() > 0;
        }

        /// <summary>
        /// The TryGetByInternalNumber
        /// </summary>
        /// <param name="internalNumber">The internalNumber<see cref="string"/></param>
        /// <param name="serviceLeg">The serviceLeg<see cref="TKSVC_Session?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public bool TryGetByInternalNumber(string internalNumber, out TKSVC_Session? serviceLeg) {
            serviceLeg = null;
            string normalized = TKSVC_Routing.NormalizeServiceNumber(internalNumber);
            if (string.IsNullOrWhiteSpace(normalized)) {
                return false;
            }

            EnsureLoaded();

            lock (_syncRoot) {
                return _servicesByInternalNumber.TryGetValue(normalized, out serviceLeg);
            }
        }

        /// <summary>
        /// The TryGetByServiceCode
        /// </summary>
        /// <param name="serviceCode">The serviceCode<see cref="string"/></param>
        /// <param name="serviceLeg">The serviceLeg<see cref="TKSVC_Session?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public bool TryGetByServiceCode(string serviceCode, out TKSVC_Session? serviceLeg) {
            serviceLeg = null;
            if (string.IsNullOrWhiteSpace(serviceCode)) {
                return false;
            }

            EnsureLoaded();
            string normalized = SIP_ServiceCodes.Normalize(serviceCode);

            lock (_syncRoot) {
                return _servicesByCode.TryGetValue(normalized, out serviceLeg);
            }
        }

        /// <summary>
        /// The GetAll
        /// </summary>
        /// <returns>The <see cref="IReadOnlyList{TKSVC_Session}"/></returns>
        public IReadOnlyList<TKSVC_Session> GetAll() {
            EnsureLoaded();

            lock (_syncRoot) {
                return _servicesByInternalNumber.Values
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.InternalNumber, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }
    }
}
