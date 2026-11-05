/*
 * TKBase_RoutingManager
 * Aufgabe:
 * - Neutraler Routing-Koordinator fÃ¼r SipInt, SipExt, GSM, SVC und Fax.
 * - Entscheidet den Zielbereich und ruft dann ausschlieÃŸlich das passende Bereichs-Routing auf.
 * - EnthÃ¤lt keine Session-, Leg-, Transport-, Command-, Event- oder Media-AusfÃ¼hrung.
 *
 * Ablaufvorgabe:
 * TKBase_Server -> TKBase_RoutingManager -> TKSipInt_Routing / TKSipExt_Routing / TKGSM_Routing / TKSVC_Routing / TKFax_Routing
 */

namespace TKServer.BASE {
    using SIPSorcery.SIP;
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;

    using TKServer.FAX;
    using TKServer.GSM;
    using TKServer.SipExt;
    using TKServer.SipInt;
    using TKServer.SQL;
    using TKServer.SVC;

    /// TKBase_RoutingManager
    /// Aufgabe:
    /// - Neutraler Routing-Koordinator fÃ¼r SipInt, SipExt, GSM, SVC und Fax.
    /// - Entscheidet den Zielbereich und ruft dann ausschlieÃŸlich das passende Bereichs-Routing auf.
    /// - EnthÃ¤lt keine Session-, Leg-, Transport-, Command-, Event- oder Media-AusfÃ¼hrung.
    /// 
    /// Ablaufvorgabe:
    /// TKBase_Server -> TKBase_RoutingManager -> TKSipInt_Routing / TKSipExt_Routing / TKGSM_Routing / TKSVC_Routing / TKFax_Routing

    /// <summary>
    /// Defines the <see cref="TKBase_RoutingManager" />
    /// </summary>
    internal sealed class TKBase_RoutingManager {
        /// <summary>
        /// Defines the _sipSql
        /// </summary>
        private readonly TKSQL_Commands _sipSql;

        /// <summary>
        /// Defines the _logger
        /// </summary>
        private readonly TKBase_LogManager? _logger;

        /// <summary>
        /// Defines the _sipIntRouting
        /// </summary>
        private readonly TKSipInt_Routing _sipIntRouting;

        /// <summary>
        /// Defines the _sipExtRouting
        /// </summary>
        private readonly TKSipExt_Routing _sipExtRouting;

        /// <summary>
        /// Defines the _gsmRouting
        /// </summary>
        private readonly TKGSM_Routing _gsmRouting;

        /// <summary>
        /// Defines the _svcRouting
        /// </summary>
        private readonly TKSVC_Routing _svcRouting;

        /// <summary>
        /// Defines the _faxRouting
        /// </summary>
        private readonly TKFax_Routing _faxRouting;

        /// <summary>
        /// Initializes a new instance of the <see cref="TKBase_RoutingManager"/> class.
        /// </summary>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        public TKBase_RoutingManager(TKBase_LogManager? logger = null) {
            _logger = logger;
            _sipSql = new TKSQL_Commands(logger);
            _sipIntRouting = new TKSipInt_Routing(logger);
            _sipExtRouting = new TKSipExt_Routing(logger);
            _gsmRouting = new TKGSM_Routing(logger);
            _svcRouting = new TKSVC_Routing(logger);
            _faxRouting = new TKFax_Routing(logger);
        }

        /// <summary>
        /// The CreateDecision
        /// </summary>
        /// <param name="fromNumber">The fromNumber<see cref="string?"/></param>
        /// <param name="toNumber">The toNumber<see cref="string?"/></param>
        /// <returns>The <see cref="SIP_DialPlanDecision"/></returns>
        public SIP_DialPlanDecision CreateDecision(string? fromNumber, string? toNumber) {
            SIP_DialPlanDecision decision = new SIP_DialPlanDecision {
                FromNumber = (fromNumber ?? string.Empty).Trim(),
                ToNumber = (toNumber ?? string.Empty).Trim(),
                SourceType = ClassifySource(fromNumber),
                TargetType = ClassifyTarget(toNumber)
            };

            _logger?.Info($"DialPlan Entscheidung | Von={decision.FromNumber} | An={decision.ToNumber} | SourceType={decision.SourceType} | TargetType={decision.TargetType}");
            return decision;
        }

        /// <summary>
        /// Erstellt eine zentrale DialPlan-Entscheidung direkt aus dem SIP-INVITE.
        /// </summary>
        /// <param name="request">The request<see cref="SIPRequest"/></param>
        /// <returns>The <see cref="SIP_DialPlanDecision"/></returns>
        public SIP_DialPlanDecision CreateDecision(SIPRequest request) {
            return CreateDecision(ExtractFromNumber(request), ExtractToNumber(request));
        }

        /// <summary>
        /// Extrahiert den Anrufer zentral aus einem SIP-Request.
        /// </summary>
        /// <param name="request">The request<see cref="SIPRequest"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string ExtractFromNumber(SIPRequest request) {
            return NormalizeSipUser(request?.Header?.From?.FromURI?.User)
                ?? NormalizeSipUser(request?.Header?.From?.FromName)
                ?? string.Empty;
        }

        /// <summary>
        /// Extrahiert das Ziel zentral aus einem SIP-Request.
        /// </summary>
        /// <param name="request">The request<see cref="SIPRequest"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string ExtractToNumber(SIPRequest request) {
            return NormalizeSipUser(request?.Header?.To?.ToURI?.User)
                ?? NormalizeSipUser(request?.URI?.User)
                ?? string.Empty;
        }

        /// <summary>
        /// The ClassifySource
        /// </summary>
        /// <param name="fromNumber">The fromNumber<see cref="string?"/></param>
        /// <returns>The <see cref="TKBase_DialSourceType"/></returns>
        public TKBase_DialSourceType ClassifySource(string? fromNumber) {
            string value = (fromNumber ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value)) {
                return TKBase_DialSourceType.Unknown;
            }

            DataRow? sourceUser = _sipSql.GetSipUserByUsername(value) ?? _sipSql.GetSipUserByNumber(value);
            if (sourceUser != null) {
                return TKFax_Routing.IsFaxUserRow(sourceUser) ? TKBase_DialSourceType.Fax : TKBase_DialSourceType.Internal;
            }

            string digits = NormalizeDigits(value);
            if (digits.Length > 0 && digits.Length <= 5) {
                return TKBase_DialSourceType.Internal;
            }

            return LooksLikeExternalNumber(value)
                ? TKBase_DialSourceType.External
                : TKBase_DialSourceType.Unknown;
        }

        /// <summary>
        /// The ClassifyTarget
        /// </summary>
        /// <param name="toNumber">The toNumber<see cref="string?"/></param>
        /// <returns>The <see cref="TKBase_DialTargetType"/></returns>
        public TKBase_DialTargetType ClassifyTarget(string? toNumber) {
            string value = (toNumber ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value)) {
                return TKBase_DialTargetType.Unknown;
            }

            string digits = NormalizeDigits(value);
            DataRow? emergencyRule = _sipSql.GetActiveEmergencyRuleByNumber(value);
            if (emergencyRule != null) {
                _logger?.Info($"RoutingManager Notruf erkannt | Ziel={value} | Notruf={ReadString(emergencyRule, "Notrufnummer")} | TargetType={ReadString(emergencyRule, "TargetType")} | TargetValue={ReadString(emergencyRule, "TargetValue")}");
                return TKBase_DialTargetType.Emergency;
            }

            if (_svcRouting.CanResolveDirectDialTarget(value)) {
                return TKBase_DialTargetType.Service;
            }

            DataRow? targetUser = _sipSql.GetSipUserByNumber(value);
            if (targetUser == null && !string.IsNullOrWhiteSpace(digits)) {
                targetUser = _sipSql.GetSipUserByNumber(digits);
            }

            if (targetUser != null) {
                return TKFax_Routing.IsFaxUserRow(targetUser) ? TKBase_DialTargetType.Fax : TKBase_DialTargetType.Internal;
            }

            if (digits.Length == 4) {
                return TKBase_DialTargetType.Internal;
            }

            if (IsFaxDidTarget(value)) {
                return TKBase_DialTargetType.Fax;
            }

            string external = TKSipExt_Routing.NormalizeExternalNumber(value);
            if (!string.IsNullOrWhiteSpace(external)) {
                return TKBase_DialTargetType.External;
            }

            return TKBase_DialTargetType.Unknown;
        }

        /// <summary>
        /// The Resolve
        /// </summary>
        /// <param name="fromNumber">The fromNumber<see cref="string"/></param>
        /// <param name="toNumber">The toNumber<see cref="string"/></param>
        /// <param name="targetType">The targetType<see cref="TKBase_DialTargetType"/></param>
        /// <returns>The <see cref="SIPIntResolvedTarget"/></returns>
        public SIPIntResolvedTarget Resolve(string fromNumber, string toNumber, TKBase_DialTargetType targetType) {
            if (targetType == TKBase_DialTargetType.Service && !IsKnownInternalSipCaller(fromNumber)) {
                _logger?.Warn($"RoutingManager Service abgewiesen | Von={fromNumber} | An={toNumber} | Grund=nur interne SIP-Teilnehmer duerfen Serviceziele aufrufen");
                return SIPIntResolvedTarget.Fail(toNumber, "Service nur fuer interne Teilnehmer erlaubt.");
            }

            SIPIntResolvedTarget route = targetType switch {
                TKBase_DialTargetType.Internal => _sipIntRouting.ResolveInternalTarget(toNumber),
                TKBase_DialTargetType.Service => _svcRouting.ResolveInternalTarget(toNumber),
                TKBase_DialTargetType.Fax => _faxRouting.ResolveFaxTarget(toNumber),
                TKBase_DialTargetType.External => ResolveExternalByPriority(fromNumber, toNumber),
                TKBase_DialTargetType.Emergency => ResolveEmergencyByRule(fromNumber, toNumber),
                TKBase_DialTargetType.GSM => _gsmRouting.ResolveExternalTarget(fromNumber, toNumber),
                _ => ResolveFallback(fromNumber, toNumber)
            };

            _logger?.Info($"RoutingManager Resolve | Von={fromNumber} | An={toNumber} | TargetType={targetType} | Success={route.Success} | RouteKind={route.TargetKind} | Grund={route.Reason}");
            return route;
        }

        /// <summary>
        /// The HandleInvite
        /// </summary>
        /// <param name="legACallId">The legACallId<see cref="string"/></param>
        /// <param name="fromNumber">The fromNumber<see cref="string"/></param>
        /// <param name="toNumber">The toNumber<see cref="string"/></param>
        /// <returns>The <see cref="SIPIntResolvedTarget"/></returns>
        public SIPIntResolvedTarget HandleInvite(string legACallId, string fromNumber, string toNumber) {
            SIP_DialPlanDecision decision = CreateDecision(fromNumber, toNumber);
            _logger?.Info($"INVITE Routing | LegA={legACallId} | Von={fromNumber} | An={toNumber} | SourceType={decision.SourceType} | TargetType={decision.TargetType}");
            return Resolve(decision.FromNumber, decision.ToNumber, decision.TargetType);
        }

        /// <summary>
        /// Prueft, ob die gerufene Nummer als aktive externe Inbound-DID bekannt ist.
        /// </summary>
        /// <param name="calledNumber">The calledNumber<see cref="string"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public bool HasInboundExternalRoute(string calledNumber) {
            bool result = _sipExtRouting.HasActiveInboundRoute(calledNumber);
            _logger?.Info($"RoutingManager InboundExtern RouteCheck | An={calledNumber} | Treffer={result}");
            return result;
        }

        /// <summary>
        /// The ResolveInboundExternalTarget
        /// </summary>
        /// <param name="calledNumber">The calledNumber<see cref="string"/></param>
        /// <returns>The <see cref="SIPIntResolvedTarget"/></returns>
        public SIPIntResolvedTarget ResolveInboundExternalTarget(string calledNumber) {
            SIPIntResolvedTarget route = _sipExtRouting.ResolveInboundTarget(calledNumber);
            _logger?.Info($"RoutingManager InboundExtern Resolve | An={calledNumber} | Success={route.Success} | RouteKind={route.TargetKind} | UserID={route.TargetUserId} | Grund={route.Reason}");
            return route;
        }

        /// <summary>
        /// The ResolveInboundExternalTargets
        /// </summary>
        /// <param name="calledNumber">The calledNumber<see cref="string"/></param>
        /// <returns>The <see cref="List{SIPIntResolvedTarget}"/></returns>
        public List<SIPIntResolvedTarget> ResolveInboundExternalTargets(string calledNumber) {
            List<SIPIntResolvedTarget> routes = _sipExtRouting.ResolveInboundTargets(calledNumber);
            _logger?.Info($"RoutingManager InboundExtern ForkResolve | An={calledNumber} | Ziele={routes.Count}");
            return routes;
        }

        /// <summary>
        /// The ResolveEmergencyByRule
        /// </summary>
        /// <param name="fromNumber">The fromNumber<see cref="string"/></param>
        /// <param name="emergencyNumber">The emergencyNumber<see cref="string"/></param>
        /// <returns>The <see cref="SIPIntResolvedTarget"/></returns>
        private SIPIntResolvedTarget ResolveEmergencyByRule(string fromNumber, string emergencyNumber) {
            DataRow? rule = _sipSql.GetActiveEmergencyRuleByNumber(emergencyNumber);
            if (rule == null) {
                _logger?.Warn($"RoutingManager Notruf abgewiesen | Keine aktive Notruf-Regel | Von={fromNumber} | An={emergencyNumber}");
                return SIPIntResolvedTarget.Fail(emergencyNumber, "Keine aktive Notruf-Regel gefunden.");
            }

            string targetType = ReadString(rule, "TargetType");
            string targetValue = ReadString(rule, "TargetValue");
            string notrufnummer = ReadString(rule, "Notrufnummer");
            if (string.IsNullOrWhiteSpace(targetValue)) {
                targetValue = emergencyNumber;
            }

            _logger?.Info($"RoutingManager Notruf-Regel wird verwendet | Von={fromNumber} | Notruf={notrufnummer} | TargetType={targetType} | TargetValue={targetValue}");

            if (string.Equals(targetType, "Intern", StringComparison.OrdinalIgnoreCase)
                || string.Equals(targetType, "Internal", StringComparison.OrdinalIgnoreCase)
                || string.Equals(targetType, "User", StringComparison.OrdinalIgnoreCase)) {
                return _sipIntRouting.ResolveInternalTarget(targetValue);
            }

            if (string.Equals(targetType, "Service", StringComparison.OrdinalIgnoreCase)
                || string.Equals(targetType, "SVC", StringComparison.OrdinalIgnoreCase)
                || string.Equals(targetType, "TKSVC", StringComparison.OrdinalIgnoreCase)) {
                return _svcRouting.ResolveInternalTarget(targetValue);
            }

            if (string.Equals(targetType, "GSM", StringComparison.OrdinalIgnoreCase)) {
                return _gsmRouting.ResolveExternalTarget(fromNumber, targetValue);
            }

            if (string.Equals(targetType, "Provider", StringComparison.OrdinalIgnoreCase)
                || string.Equals(targetType, "Extern", StringComparison.OrdinalIgnoreCase)
                || string.Equals(targetType, "External", StringComparison.OrdinalIgnoreCase)) {
                return ResolveExternalByPriority(fromNumber, targetValue);
            }

            _logger?.Warn($"RoutingManager Notruf-Regel unbekannter TargetType | Von={fromNumber} | Notruf={notrufnummer} | TargetType={targetType} | TargetValue={targetValue}");
            return SIPIntResolvedTarget.Fail(emergencyNumber, "Unbekannter Notruf-Zieltyp.");
        }

        /// <summary>
        /// The ResolveExternalByPriority
        /// </summary>
        /// <param name="fromNumber">The fromNumber<see cref="string"/></param>
        /// <param name="toNumber">The toNumber<see cref="string"/></param>
        /// <returns>The <see cref="SIPIntResolvedTarget"/></returns>
        private SIPIntResolvedTarget ResolveExternalByPriority(string fromNumber, string toNumber) {
            string normalizedCalled = TKSipExt_Routing.NormalizeExternalNumber(toNumber);
            if (string.IsNullOrWhiteSpace(normalizedCalled)) {
                return SIPIntResolvedTarget.Fail(toNumber, "Keine externe Rufnummer.");
            }

            DataRow? caller = ResolveCaller(fromNumber);
            if (caller == null) {
                _logger?.Warn($"RoutingManager External abgewiesen | Caller unbekannt | Von={fromNumber} | An={normalizedCalled}");
                return SIPIntResolvedTarget.Fail(normalizedCalled, "Anrufer nicht gefunden.");
            }

            if (!(caller["CanDialExternal"] != DBNull.Value && Convert.ToBoolean(caller["CanDialExternal"]))) {
                _logger?.Warn($"RoutingManager External abgewiesen | Keine Berechtigung | Von={fromNumber} | An={normalizedCalled}");
                return SIPIntResolvedTarget.Fail(normalizedCalled, "Externe GesprÃ¤che nicht erlaubt.");
            }

            int callerId = Convert.ToInt32(caller["ID"]);
            string callerType = (Convert.ToString(caller["UserType"]) ?? string.Empty).Trim();
            string callerUsername = (Convert.ToString(caller["Username"]) ?? string.Empty).Trim();
            List<DataRow> routes = _sipSql.GetExternalOutboundRoutesForUser(callerId, callerType);
            if (routes.Count == 0 && !string.IsNullOrWhiteSpace(callerType)) {
                routes = _sipSql.GetExternalOutboundRoutesForUser(callerId, null);
            }

            SIPIntResolvedTarget? lastTypedFailure = null;
            foreach (DataRow route in routes.OrderBy(r => r["Priority"] == DBNull.Value ? int.MaxValue : Convert.ToInt32(r["Priority"]))) {
                string sourceType = (Convert.ToString(route["SourceType"]) ?? string.Empty).Trim();
                SIPIntResolvedTarget? resolved = null;

                if (string.Equals(sourceType, "ProviderAccount", StringComparison.OrdinalIgnoreCase)) {
                    resolved = _sipExtRouting.TryResolveCandidate(route, normalizedCalled, fromNumber, callerId, callerType, callerUsername);
                } else if (string.Equals(sourceType, "GSMStatus", StringComparison.OrdinalIgnoreCase)) {
                    resolved = _gsmRouting.TryResolveCandidate(route, normalizedCalled, fromNumber, callerId, callerType, callerUsername);
                }

                if (resolved != null && resolved.Success) {
                    _logger?.Info($"RoutingManager Prioritaet gewaehlt | UserID={callerId} | Von={fromNumber} | An={normalizedCalled} | SourceType={sourceType} | RouteKind={resolved.TargetKind}");
                    return resolved;
                }

                if (resolved != null && resolved.TargetKind != SIPIntRouteTargetKind.None) {
                    lastTypedFailure = resolved;
                }
            }

            return lastTypedFailure ?? SIPIntResolvedTarget.Fail(normalizedCalled, "Keine nutzbare externe Nummer verfuegbar.");
        }

        /// <summary>
        /// The ResolveFallback
        /// </summary>
        /// <param name="fromNumber">The fromNumber<see cref="string"/></param>
        /// <param name="toNumber">The toNumber<see cref="string"/></param>
        /// <returns>The <see cref="SIPIntResolvedTarget"/></returns>
        private SIPIntResolvedTarget ResolveFallback(string fromNumber, string toNumber) {
            if (_svcRouting.CanResolveDirectDialTarget(toNumber)) {
                if (!IsKnownInternalSipCaller(fromNumber)) {
                    _logger?.Warn($"RoutingManager Service-Fallback abgewiesen | Von={fromNumber} | An={toNumber} | Grund=nur interne SIP-Teilnehmer duerfen Serviceziele aufrufen");
                    return SIPIntResolvedTarget.Fail(toNumber, "Service nur fuer interne Teilnehmer erlaubt.");
                }

                SIPIntResolvedTarget serviceRoute = _svcRouting.ResolveInternalTarget(toNumber);
                if (serviceRoute.Success) {
                    return serviceRoute;
                }
            }

            SIPIntResolvedTarget internalRoute = _sipIntRouting.ResolveInternalTarget(toNumber);
            if (internalRoute.Success) {
                return internalRoute;
            }

            if (LooksLikeExternalNumber(toNumber)) {
                return ResolveExternalByPriority(fromNumber, toNumber);
            }

            return SIPIntResolvedTarget.Fail(toNumber, "Kein Routingziel gefunden.");
        }

        /// <summary>
        /// PrÃ¼ft zentral, ob eine angerufene externe DID auf einen FaxUser oder eine Fax-Route zeigt.
        /// </summary>
        /// <param name="toNumber">The toNumber<see cref="string?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private bool IsFaxDidTarget(string? toNumber) {
            string normalized = TKSipExt_Routing.NormalizeExternalNumber(toNumber ?? string.Empty);
            if (string.IsNullOrWhiteSpace(normalized)) return false;

            foreach (string candidate in BuildInboundNumberCandidates(normalized)) {
                DataRow? externalNumber = _sipSql.GetExternalNumberByValue(candidate);
                if (externalNumber == null) continue;

                bool isActive = !externalNumber.Table.Columns.Contains("Active")
                    || externalNumber["Active"] == DBNull.Value
                    || Convert.ToBoolean(externalNumber["Active"]);
                if (!isActive) continue;

                foreach (DataRow route in _sipSql.GetInboundExternalRoutesByCalledNumber(candidate)) {
                    string targetType = ReadString(route, "TargetType");
                    string targetUserType = ReadString(route, "TargetUserType");
                    if (IsFaxMarker(targetType) || IsFaxMarker(targetUserType)) {
                        _logger?.Info($"RoutingManager Fax-DID erkannt | Eingang={toNumber} | DID={candidate} | TargetType={targetType} | TargetUserType={targetUserType}");
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// PrÃ¼ft Fax-Marker zentral im RoutingManager.
        /// </summary>
        /// <param name="value">The value<see cref="string?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool IsFaxMarker(string? value) {
            string marker = (value ?? string.Empty).Trim();
            return string.Equals(marker, TKFax_Routing.FaxUserType, StringComparison.OrdinalIgnoreCase)
                || string.Equals(marker, TKFax_Routing.FaxRouteKind, StringComparison.OrdinalIgnoreCase)
                || string.Equals(marker, "Fax", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Bildet DID-Varianten fÃ¼r +49/49/0-Formate.
        /// </summary>
        /// <param name="normalizedCalled">The normalizedCalled<see cref="string"/></param>
        /// <returns>The <see cref="IEnumerable{string}"/></returns>
        private static IEnumerable<string> BuildInboundNumberCandidates(string normalizedCalled) {
            string value = (normalizedCalled ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value)) yield break;

            HashSet<string> emitted = new(StringComparer.OrdinalIgnoreCase);
            List<string> candidates = new();
            void Add(string candidate) {
                candidate = (candidate ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(candidate) && emitted.Add(candidate)) candidates.Add(candidate);
            }

            Add(value);
            string digits = NormalizeDigits(value);
            Add(digits);
            if (value.StartsWith("+49", StringComparison.Ordinal) && value.Length > 3) Add("0" + value.Substring(3));
            if (digits.StartsWith("49", StringComparison.Ordinal) && digits.Length > 2) Add("0" + digits.Substring(2));
            if (digits.StartsWith("0", StringComparison.Ordinal) && digits.Length > 1) {
                Add("+49" + digits.Substring(1));
                Add("49" + digits.Substring(1));
            }

            foreach (string candidate in candidates) yield return candidate;
        }

        /// <summary>
        /// Normalisiert einen SIP-Userteil fÃ¼r zentrale Routingentscheidungen.
        /// </summary>
        /// <param name="value">The value<see cref="string?"/></param>
        /// <returns>The <see cref="string?"/></returns>
        private static string? NormalizeSipUser(string? value) {
            if (string.IsNullOrWhiteSpace(value)) return null;
            string text = value.Trim();
            int lt = text.IndexOf('<');
            int gt = text.IndexOf('>', lt + 1);
            if (lt >= 0 && gt > lt) text = text.Substring(lt + 1, gt - lt - 1).Trim();
            try { text = Uri.UnescapeDataString(text); } catch { }
            if (text.StartsWith("sips:", StringComparison.OrdinalIgnoreCase)) text = text.Substring(5);
            if (text.StartsWith("sip:", StringComparison.OrdinalIgnoreCase)) text = text.Substring(4);
            if (text.StartsWith("tel:", StringComparison.OrdinalIgnoreCase)) text = text.Substring(4);
            int at = text.IndexOf('@');
            if (at >= 0) text = text.Substring(0, at);
            int semi = text.IndexOf(';');
            if (semi >= 0) text = text.Substring(0, semi);
            text = text.Trim().Trim('<', '>');
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        /// <summary>
        /// The IsKnownInternalSipCaller
        /// </summary>
        /// <param name="fromNumber">The fromNumber<see cref="string"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private bool IsKnownInternalSipCaller(string fromNumber) {
            if (string.IsNullOrWhiteSpace(fromNumber)) {
                return false;
            }

            return _sipSql.GetSipUserByUsername(fromNumber.Trim()) != null
                || _sipSql.GetSipUserByNumber(fromNumber.Trim()) != null;
        }

        /// <summary>
        /// The ResolveCaller
        /// </summary>
        /// <param name="fromNumber">The fromNumber<see cref="string"/></param>
        /// <returns>The <see cref="DataRow?"/></returns>
        private DataRow? ResolveCaller(string fromNumber) {
            DataRow? caller = _sipSql.GetSipUserByUsername(fromNumber) ?? _sipSql.GetSipUserByNumber(fromNumber);
            if (caller != null) {
                return caller;
            }

            string normalized = TKSipExt_Routing.NormalizeExternalNumber(fromNumber);
            return string.IsNullOrWhiteSpace(normalized) ? null : _sipSql.GetSipUserByNumber(normalized);
        }

        /// <summary>
        /// The ReadString
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="columnName">The columnName<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string ReadString(DataRow row, string columnName) {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value) {
                return string.Empty;
            }

            return Convert.ToString(row[columnName])?.Trim() ?? string.Empty;
        }


        /// <summary>
        /// PrÃ¼ft zentral, ob ein Wert wie eine externe Rufnummer aussieht.
        /// </summary>
        /// <param name="calledNumber">The calledNumber<see cref="string?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool LooksLikeExternalNumber(string? calledNumber) {
            string value = (calledNumber ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value)) return false;

            string digits = NormalizeDigits(value);
            if (digits.Length <= 5) return false;

            return value.StartsWith("+", StringComparison.Ordinal)
                || value.StartsWith("00", StringComparison.Ordinal)
                || value.StartsWith("0", StringComparison.Ordinal)
                || digits.Length > 5;
        }

        /// <summary>
        /// The NormalizeDigits
        /// </summary>
        /// <param name="value">The value<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string NormalizeDigits(string value) {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : new string(value.Where(char.IsDigit).ToArray());
        }
    }

    /// <summary>
    /// Defines the TKBase_DialSourceType
    /// </summary>
    internal enum TKBase_DialSourceType {
        /// <summary>
        /// Defines the Unknown
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Defines the Internal
        /// </summary>
        Internal = 1,

        /// <summary>
        /// Defines the External
        /// </summary>
        External = 2,

        /// <summary>
        /// Defines the GSM
        /// </summary>
        GSM = 3,

        /// <summary>
        /// Defines the Service
        /// </summary>
        Service = 4,

        /// <summary>
        /// Defines the Fax
        /// </summary>
        Fax = 5
    }

    /// <summary>
    /// Defines the TKBase_DialTargetType
    /// </summary>
    internal enum TKBase_DialTargetType {
        /// <summary>
        /// Defines the Unknown
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Defines the Internal
        /// </summary>
        Internal = 1,

        /// <summary>
        /// Defines the External
        /// </summary>
        External = 2,

        /// <summary>
        /// Defines the Emergency
        /// </summary>
        Emergency = 3,

        /// <summary>
        /// Defines the GSM
        /// </summary>
        GSM = 4,

        /// <summary>
        /// Defines the Service
        /// </summary>
        Service = 5,

        /// <summary>
        /// Defines the Fax
        /// </summary>
        Fax = 6
    }

    /// <summary>
    /// Defines the <see cref="SIP_DialPlanDecision" />
    /// </summary>
    internal sealed class SIP_DialPlanDecision {
        /// <summary>
        /// Gets the SourceType
        /// </summary>
        public TKBase_DialSourceType SourceType { get; init; } = TKBase_DialSourceType.Unknown;

        /// <summary>
        /// Gets the TargetType
        /// </summary>
        public TKBase_DialTargetType TargetType { get; init; } = TKBase_DialTargetType.Unknown;

        /// <summary>
        /// Gets the FromNumber
        /// </summary>
        public string FromNumber { get; init; } = string.Empty;

        /// <summary>
        /// Gets the ToNumber
        /// </summary>
        public string ToNumber { get; init; } = string.Empty;

        /// <summary>
        /// Gets a value indicating whether IsInternalPath
        /// </summary>
        public bool IsInternalPath => SourceType == TKBase_DialSourceType.Internal && TargetType == TKBase_DialTargetType.Internal;

        /// <summary>
        /// Gets a value indicating whether IsServicePath
        /// </summary>
        public bool IsServicePath => TargetType == TKBase_DialTargetType.Service;

        /// <summary>
        /// Gets a value indicating whether IsExternalPath
        /// </summary>
        public bool IsExternalPath => TargetType == TKBase_DialTargetType.External || TargetType == TKBase_DialTargetType.Emergency || SourceType == TKBase_DialSourceType.External;

        /// <summary>
        /// Gets a value indicating whether IsGsmPath
        /// </summary>
        public bool IsGsmPath => TargetType == TKBase_DialTargetType.GSM;

        /// <summary>
        /// Gets a value indicating whether IsFaxPath
        /// </summary>
        public bool IsFaxPath => SourceType == TKBase_DialSourceType.Fax || TargetType == TKBase_DialTargetType.Fax;
    }

    /// <summary>
    /// Defines the SIPIntRouteTargetKind
    /// </summary>
    public enum SIPIntRouteTargetKind {
        /// <summary>
        /// Defines the None
        /// </summary>
        None = 0,

        /// <summary>
        /// Defines the User
        /// </summary>
        User = 1,

        /// <summary>
        /// Defines the External
        /// </summary>
        External = 2,

        /// <summary>
        /// Defines the GSM
        /// </summary>
        GSM = 3,

        /// <summary>
        /// Defines the Service
        /// </summary>
        Service = 4,

        /// <summary>
        /// Defines the FaxUser
        /// </summary>
        FaxUser = 5
    }

    /// <summary>
    /// Defines the <see cref="SIPIntResolvedTarget" />
    /// </summary>
    public sealed class SIPIntResolvedTarget {
        /// <summary>
        /// Gets a value indicating whether Success
        /// </summary>
        public bool Success { get; private set; }

        /// <summary>
        /// Gets the Reason
        /// </summary>
        public string? Reason { get; private set; }

        /// <summary>
        /// Gets the CalledNumber
        /// </summary>
        public string? CalledNumber { get; private set; }

        /// <summary>
        /// Gets the TargetKind
        /// </summary>
        public SIPIntRouteTargetKind TargetKind { get; private set; }

        /// <summary>
        /// Gets the TargetUserId
        /// </summary>
        public int? TargetUserId { get; private set; }

        /// <summary>
        /// Gets the TargetUsername
        /// </summary>
        public string? TargetUsername { get; private set; }

        /// <summary>
        /// Gets the CallerUsername
        /// </summary>
        public string? CallerUsername { get; private set; }

        /// <summary>
        /// Gets the ContactUri
        /// </summary>
        public string? ContactUri { get; private set; }

        /// <summary>
        /// Gets the RemoteEndPoint
        /// </summary>
        public string? RemoteEndPoint { get; private set; }

        /// <summary>
        /// Gets the Transport
        /// </summary>
        public string? Transport { get; private set; }

        /// <summary>
        /// Gets the ServiceLeg
        /// </summary>
        public TKSVC_Session? ServiceLeg { get; private set; }

        /// <summary>
        /// Gets the ProviderAccountId
        /// </summary>
        public int? ProviderAccountId { get; private set; }

        /// <summary>
        /// Gets the ProviderName
        /// </summary>
        public string? ProviderName { get; private set; }

        /// <summary>
        /// Gets the ProviderAuthUser
        /// </summary>
        public string? ProviderAuthUser { get; private set; }

        /// <summary>
        /// Gets the ProviderSipUser
        /// </summary>
        public string? ProviderSipUser { get; private set; }

        /// <summary>
        /// Gets the ProviderPassword
        /// </summary>
        public string? ProviderPassword { get; private set; }

        /// <summary>
        /// Gets the ProviderNumber
        /// </summary>
        public string? ProviderNumber { get; private set; }

        /// <summary>
        /// Gets the ProviderServiceRoute
        /// </summary>
        public string? ProviderServiceRoute { get; private set; }

        /// <summary>
        /// Gets the ProviderAssociatedUri
        /// </summary>
        public string? ProviderAssociatedUri { get; private set; }

        /// <summary>
        /// Gets the ProviderRequestUri
        /// </summary>
        public string? ProviderRequestUri { get; private set; }

        /// <summary>
        /// Gets the ProviderPublicIp
        /// </summary>
        public string? ProviderPublicIp { get; private set; }

        /// <summary>
        /// Gets the GsmStatusId
        /// </summary>
        public int? GsmStatusId { get; private set; }

        /// <summary>
        /// Gets the GsmModemId
        /// </summary>
        public int? GsmModemId { get; private set; }

        /// <summary>
        /// Gets the GsmSimId
        /// </summary>
        public int? GsmSimId { get; private set; }

        /// <summary>
        /// Gets the GsmComPortAT
        /// </summary>
        public string? GsmComPortAT { get; private set; }

        /// <summary>
        /// Gets the GsmComPortAudio
        /// </summary>
        public string? GsmComPortAudio { get; private set; }

        /// <summary>
        /// Gets the GsmNumber
        /// </summary>
        public string? GsmNumber { get; private set; }

        /// <summary>
        /// Gets the GsmImei
        /// </summary>
        public string? GsmImei { get; private set; }

        /// <summary>
        /// Gets the GsmProviderName
        /// </summary>
        public string? GsmProviderName { get; private set; }

        /// <summary>
        /// Gets the GsmRouteReason
        /// </summary>
        public string? GsmRouteReason { get; private set; }

        /// <summary>
        /// Gets a value indicating whether IsUserTarget
        /// </summary>
        public bool IsUserTarget => Success && TargetKind == SIPIntRouteTargetKind.User;

        /// <summary>
        /// Gets a value indicating whether IsExternalTarget
        /// </summary>
        public bool IsExternalTarget => Success && TargetKind == SIPIntRouteTargetKind.External;

        /// <summary>
        /// Gets a value indicating whether IsGsmTarget
        /// </summary>
        public bool IsGsmTarget => Success && TargetKind == SIPIntRouteTargetKind.GSM;

        /// <summary>
        /// Gets a value indicating whether IsServiceTarget
        /// </summary>
        public bool IsServiceTarget => Success && TargetKind == SIPIntRouteTargetKind.Service;

        /// <summary>
        /// Gets a value indicating whether IsFaxTarget
        /// </summary>
        public bool IsFaxTarget => Success && TargetKind == SIPIntRouteTargetKind.FaxUser;

        /// <summary>
        /// The OkUser
        /// </summary>
        /// <param name="calledNumber">The calledNumber<see cref="string"/></param>
        /// <param name="userId">The userId<see cref="int"/></param>
        /// <param name="contactUri">The contactUri<see cref="string"/></param>
        /// <param name="remoteEndPoint">The remoteEndPoint<see cref="string"/></param>
        /// <param name="transport">The transport<see cref="string?"/></param>
        /// <param name="targetUsername">The targetUsername<see cref="string?"/></param>
        /// <returns>The <see cref="SIPIntResolvedTarget"/></returns>
        public static SIPIntResolvedTarget OkUser(string calledNumber, int userId, string contactUri, string remoteEndPoint, string? transport, string? targetUsername = null) {
            return new SIPIntResolvedTarget { Success = true, CalledNumber = calledNumber, TargetKind = SIPIntRouteTargetKind.User, TargetUserId = userId, TargetUsername = targetUsername, ContactUri = contactUri, RemoteEndPoint = remoteEndPoint, Transport = transport };
        }

        /// <summary>
        /// The OkService
        /// </summary>
        /// <param name="calledNumber">The calledNumber<see cref="string"/></param>
        /// <param name="serviceLeg">The serviceLeg<see cref="TKSVC_Session"/></param>
        /// <returns>The <see cref="SIPIntResolvedTarget"/></returns>
        public static SIPIntResolvedTarget OkService(string calledNumber, TKSVC_Session serviceLeg) {
            return new SIPIntResolvedTarget { Success = true, CalledNumber = calledNumber, TargetKind = SIPIntRouteTargetKind.Service, ServiceLeg = serviceLeg };
        }

        /// <summary>
        /// The OkFaxUser
        /// </summary>
        /// <param name="calledNumber">The calledNumber<see cref="string"/></param>
        /// <param name="userId">The userId<see cref="int"/></param>
        /// <param name="targetUsername">The targetUsername<see cref="string?"/></param>
        /// <param name="targetNumber">The targetNumber<see cref="string?"/></param>
        /// <param name="contactUri">The contactUri<see cref="string?"/></param>
        /// <param name="remoteEndPoint">The remoteEndPoint<see cref="string?"/></param>
        /// <param name="transport">The transport<see cref="string?"/></param>
        /// <returns>The <see cref="SIPIntResolvedTarget"/></returns>
        public static SIPIntResolvedTarget OkFaxUser(string calledNumber, int userId, string? targetUsername = null, string? targetNumber = null, string? contactUri = null, string? remoteEndPoint = null, string? transport = null) {
            string resolvedContact = string.IsNullOrWhiteSpace(contactUri) ? (targetNumber ?? string.Empty) : contactUri!;
            return new SIPIntResolvedTarget { Success = true, CalledNumber = calledNumber, TargetKind = SIPIntRouteTargetKind.FaxUser, TargetUserId = userId, TargetUsername = string.IsNullOrWhiteSpace(targetUsername) ? targetNumber : targetUsername, ContactUri = resolvedContact, RemoteEndPoint = remoteEndPoint, Transport = transport };
        }

        /// <summary>
        /// The OkExternal
        /// </summary>
        /// <param name="calledNumber">The calledNumber<see cref="string"/></param>
        /// <param name="contactUri">The contactUri<see cref="string"/></param>
        /// <param name="remoteEndPoint">The remoteEndPoint<see cref="string"/></param>
        /// <param name="transport">The transport<see cref="string?"/></param>
        /// <param name="providerAccountId">The providerAccountId<see cref="int?"/></param>
        /// <param name="providerName">The providerName<see cref="string?"/></param>
        /// <param name="providerAuthUser">The providerAuthUser<see cref="string?"/></param>
        /// <param name="providerSipUser">The providerSipUser<see cref="string?"/></param>
        /// <param name="providerPassword">The providerPassword<see cref="string?"/></param>
        /// <param name="providerNumber">The providerNumber<see cref="string?"/></param>
        /// <param name="providerServiceRoute">The providerServiceRoute<see cref="string?"/></param>
        /// <param name="providerAssociatedUri">The providerAssociatedUri<see cref="string?"/></param>
        /// <param name="providerRequestUri">The providerRequestUri<see cref="string?"/></param>
        /// <param name="providerPublicIp">The providerPublicIp<see cref="string?"/></param>
        /// <param name="callerUsername">The callerUsername<see cref="string?"/></param>
        /// <returns>The <see cref="SIPIntResolvedTarget"/></returns>
        public static SIPIntResolvedTarget OkExternal(string calledNumber, string contactUri, string remoteEndPoint, string? transport, int? providerAccountId, string? providerName, string? providerAuthUser, string? providerSipUser, string? providerPassword, string? providerNumber, string? providerServiceRoute, string? providerAssociatedUri, string? providerRequestUri, string? providerPublicIp, string? callerUsername = null) {
            return new SIPIntResolvedTarget { Success = true, CalledNumber = calledNumber, TargetKind = SIPIntRouteTargetKind.External, ContactUri = contactUri, RemoteEndPoint = remoteEndPoint, Transport = transport, ProviderAccountId = providerAccountId, ProviderName = providerName, ProviderAuthUser = providerAuthUser, ProviderSipUser = providerSipUser, ProviderPassword = providerPassword, ProviderNumber = providerNumber, ProviderServiceRoute = providerServiceRoute, ProviderAssociatedUri = providerAssociatedUri, ProviderRequestUri = providerRequestUri, ProviderPublicIp = providerPublicIp, CallerUsername = callerUsername };
        }

        /// <summary>
        /// The OkGsm
        /// </summary>
        /// <param name="calledNumber">The calledNumber<see cref="string"/></param>
        /// <param name="gsmStatusId">The gsmStatusId<see cref="int"/></param>
        /// <param name="gsmModemId">The gsmModemId<see cref="int?"/></param>
        /// <param name="gsmSimId">The gsmSimId<see cref="int?"/></param>
        /// <param name="gsmComPortAt">The gsmComPortAt<see cref="string?"/></param>
        /// <param name="gsmComPortAudio">The gsmComPortAudio<see cref="string?"/></param>
        /// <param name="gsmNumber">The gsmNumber<see cref="string?"/></param>
        /// <param name="gsmImei">The gsmImei<see cref="string?"/></param>
        /// <param name="gsmProviderName">The gsmProviderName<see cref="string?"/></param>
        /// <param name="gsmRouteReason">The gsmRouteReason<see cref="string?"/></param>
        /// <param name="callerUsername">The callerUsername<see cref="string?"/></param>
        /// <returns>The <see cref="SIPIntResolvedTarget"/></returns>
        public static SIPIntResolvedTarget OkGsm(string calledNumber, int gsmStatusId, int? gsmModemId, int? gsmSimId, string? gsmComPortAt, string? gsmComPortAudio, string? gsmNumber, string? gsmImei, string? gsmProviderName, string? gsmRouteReason, string? callerUsername = null) {
            return new SIPIntResolvedTarget { Success = true, CalledNumber = calledNumber, TargetKind = SIPIntRouteTargetKind.GSM, GsmStatusId = gsmStatusId, GsmModemId = gsmModemId, GsmSimId = gsmSimId, GsmComPortAT = gsmComPortAt, GsmComPortAudio = gsmComPortAudio, GsmNumber = gsmNumber, GsmImei = gsmImei, GsmProviderName = gsmProviderName, GsmRouteReason = gsmRouteReason, CallerUsername = callerUsername };
        }

        /// <summary>
        /// The Fail
        /// </summary>
        /// <param name="calledNumber">The calledNumber<see cref="string?"/></param>
        /// <param name="reason">The reason<see cref="string"/></param>
        /// <returns>The <see cref="SIPIntResolvedTarget"/></returns>
        public static SIPIntResolvedTarget Fail(string? calledNumber, string reason) {
            return new SIPIntResolvedTarget { Success = false, CalledNumber = calledNumber, TargetKind = SIPIntRouteTargetKind.None, Reason = reason };
        }
    }
}
