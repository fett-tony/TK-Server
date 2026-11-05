/*
 * TKBase_DTMF_Manager
 * Aufgabe:
 * - Normalisiert DTMF-Eingaben aus technischen Quellen.
 * - Puffert Tastensequenzen je Session/Call/Leg bis zur Abschluss-Taste.
 * - Fuehrt keine Service-, Routing-, SIP- oder GSM-Fachlogik aus.
 */

namespace TKServer.BASE {
    using SIPSorcery.SIP;
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Defines the <see cref="TKBase_DTMF_Manager" />
    /// </summary>
    internal sealed class TKBase_DTMF_Manager {
        /// <summary>
        /// Defines the DefaultSequenceTimeout
        /// </summary>
        private static readonly TimeSpan DefaultSequenceTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Defines the _buffers
        /// </summary>
        private readonly ConcurrentDictionary<string, DtmfBuffer> _buffers = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Defines the _logger
        /// </summary>
        private readonly TKBase_LogManager? _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TKBase_DTMF_Manager"/> class.
        /// </summary>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        public TKBase_DTMF_Manager(TKBase_LogManager? logger = null) {
            _logger = logger;
        }

        /// <summary>
        /// The TryParseSipInfoDigit
        /// </summary>
        /// <param name="request">The request<see cref="SIPRequest"/></param>
        /// <param name="digit">The digit<see cref="char"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public bool TryParseSipInfoDigit(SIPRequest request, out char digit) {
            digit = '\0';

            if (request == null || string.IsNullOrWhiteSpace(request.Body)) {
                return false;
            }

            string body = request.Body.Trim();
            foreach (string rawLine in body.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)) {
                string line = rawLine.Trim();
                int separator = line.IndexOf('=');
                if (separator <= 0) {
                    separator = line.IndexOf(':');
                }

                if (separator <= 0) {
                    continue;
                }

                string name = line.Substring(0, separator).Trim();
                string value = line.Substring(separator + 1).Trim();
                if (!string.Equals(name, "Signal", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(name, "Digit", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                return TryNormalizeDigit(value, out digit);
            }

            return TryNormalizeDigit(body, out digit);
        }

        /// <summary>
        /// The PushDigit
        /// </summary>
        /// <param name="sessionId">The sessionId<see cref="string"/></param>
        /// <param name="callId">The callId<see cref="string?"/></param>
        /// <param name="legName">The legName<see cref="string"/></param>
        /// <param name="source">The source<see cref="string"/></param>
        /// <param name="digit">The digit<see cref="char"/></param>
        /// <returns>The <see cref="TKBase_DTMF_Result"/></returns>
        public TKBase_DTMF_Result PushDigit(string sessionId, string? callId, string legName, string source, char digit) {
            if (string.IsNullOrWhiteSpace(sessionId) || !TryNormalizeDigit(digit.ToString(), out char normalizedDigit)) {
                return TKBase_DTMF_Result.Ignored(sessionId, callId, legName, source, digit);
            }

            string key = BuildKey(sessionId, callId, legName);
            DtmfBuffer buffer = _buffers.GetOrAdd(key, _ => new DtmfBuffer());
            lock (buffer.SyncRoot) {
                DateTime now = DateTime.UtcNow;
                if (buffer.LastInputUtc.HasValue && now - buffer.LastInputUtc.Value > DefaultSequenceTimeout) {
                    buffer.Builder.Clear();
                }

                buffer.LastInputUtc = now;

                if (normalizedDigit == '#') {
                    if (buffer.Builder.Length == 0) {
                        _buffers.TryRemove(key, out _);
                    return TKBase_DTMF_Result.CreateCancelled(sessionId, callId, legName, source);
                    }

                    buffer.Builder.Append(normalizedDigit);
                    string completed = buffer.Builder.ToString();
                    buffer.Builder.Clear();
                    _buffers.TryRemove(key, out _);
                    _logger?.Info($"DTMF Sequenz abgeschlossen | SessionID={sessionId} | Leg={legName} | Source={source} | Sequence={completed}");
                    return TKBase_DTMF_Result.CreateCompleted(sessionId, callId, legName, source, completed);
                }

                buffer.Builder.Append(normalizedDigit);
                string partial = buffer.Builder.ToString();
                _logger?.Info($"DTMF empfangen | SessionID={sessionId} | Leg={legName} | Source={source} | Digit={normalizedDigit} | Buffer={partial}");
                return TKBase_DTMF_Result.Partial(sessionId, callId, legName, source, partial);
            }
        }

        /// <summary>
        /// The Clear
        /// </summary>
        /// <param name="sessionId">The sessionId<see cref="string"/></param>
        public void Clear(string sessionId) {
            if (string.IsNullOrWhiteSpace(sessionId)) {
                return;
            }

            foreach (string key in _buffers.Keys.Where(x => x.StartsWith(sessionId + "|", StringComparison.OrdinalIgnoreCase))) {
                _buffers.TryRemove(key, out _);
            }
        }

        /// <summary>
        /// The TryNormalizeDigit
        /// </summary>
        private static bool TryNormalizeDigit(string? value, out char digit) {
            digit = '\0';
            string text = (value ?? string.Empty).Trim();
            if (text.Length == 0) {
                return false;
            }

            char candidate = char.ToUpperInvariant(text[0]);
            if ((candidate >= '0' && candidate <= '9') || candidate == '*' || candidate == '#' || candidate == 'A' || candidate == 'B' || candidate == 'C' || candidate == 'D') {
                digit = candidate;
                return true;
            }

            return false;
        }

        /// <summary>
        /// The BuildKey
        /// </summary>
        private static string BuildKey(string sessionId, string? callId, string legName) {
            return $"{sessionId}|{callId ?? string.Empty}|{legName}";
        }

        /// <summary>
        /// Defines the <see cref="DtmfBuffer" />
        /// </summary>
        private sealed class DtmfBuffer {
            /// <summary>
            /// Gets the SyncRoot
            /// </summary>
            public object SyncRoot { get; } = new object();

            /// <summary>
            /// Gets the Builder
            /// </summary>
            public StringBuilder Builder { get; } = new StringBuilder();

            /// <summary>
            /// Gets or sets the LastInputUtc
            /// </summary>
            public DateTime? LastInputUtc { get; set; }
        }
    }

    /// <summary>
    /// Defines the <see cref="TKBase_DTMF_Result" />
    /// </summary>
    internal sealed class TKBase_DTMF_Result {
        /// <summary>
        /// Gets a value indicating whether Accepted
        /// </summary>
        public bool Accepted { get; private set; }

        /// <summary>
        /// Gets a value indicating whether Completed
        /// </summary>
        public bool Completed { get; private set; }

        /// <summary>
        /// Gets a value indicating whether Cancelled
        /// </summary>
        public bool Cancelled { get; private set; }

        /// <summary>
        /// Gets the SessionId
        /// </summary>
        public string SessionId { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the CallId
        /// </summary>
        public string? CallId { get; private set; }

        /// <summary>
        /// Gets the LegName
        /// </summary>
        public string LegName { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the Source
        /// </summary>
        public string Source { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the Digit
        /// </summary>
        public char Digit { get; private set; }

        /// <summary>
        /// Gets the Sequence
        /// </summary>
        public string Sequence { get; private set; } = string.Empty;

        /// <summary>
        /// The Ignored
        /// </summary>
        public static TKBase_DTMF_Result Ignored(string sessionId, string? callId, string legName, string source, char digit) {
            return new TKBase_DTMF_Result { SessionId = sessionId, CallId = callId, LegName = legName, Source = source, Digit = digit };
        }

        /// <summary>
        /// The Partial
        /// </summary>
        public static TKBase_DTMF_Result Partial(string sessionId, string? callId, string legName, string source, string sequence) {
            return new TKBase_DTMF_Result { Accepted = true, SessionId = sessionId, CallId = callId, LegName = legName, Source = source, Sequence = sequence };
        }

        /// <summary>
        /// The Completed
        /// </summary>
        public static TKBase_DTMF_Result CreateCompleted(string sessionId, string? callId, string legName, string source, string sequence) {
            return new TKBase_DTMF_Result { Accepted = true, Completed = true, SessionId = sessionId, CallId = callId, LegName = legName, Source = source, Sequence = sequence };
        }

        /// <summary>
        /// The Cancelled
        /// </summary>
        public static TKBase_DTMF_Result CreateCancelled(string sessionId, string? callId, string legName, string source) {
            return new TKBase_DTMF_Result { Accepted = true, Cancelled = true, SessionId = sessionId, CallId = callId, LegName = legName, Source = source };
        }
    }
}
