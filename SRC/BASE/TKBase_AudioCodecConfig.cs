/*
 * TKBase_AudioCodecConfig
 * Aufgabe:
 * - Hält die Konfigurationsdaten eines Audio-Codecs aus TK_AudioCodec.
 * - Enthält die temporäre Codec-Sicht der Gegenseite aus SDP.
 * - Enthält die direkte Codec-Auswahl für SIP/RTP/GSM-Bridge.
 *
 * Nicht Aufgabe:
 * - Kein Routing.
 * - Keine Session-Steuerung.
 * - Keine Call-Steuerung.
 * - Keine Media-Ausführung.
 */

namespace TKServer.BASE {
    using SIPSorceryMedia.Abstractions;
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.Linq;

    /// TKBase_AudioCodecConfig
    /// Aufgabe:
    /// - Hält die Konfigurationsdaten eines Audio-Codecs aus TK_AudioCodec.
    /// - Enthält die temporäre Codec-Sicht der Gegenseite aus SDP.
    /// - Enthält die direkte Codec-Auswahl für SIP/RTP/GSM-Bridge.
    /// 
    /// Nicht Aufgabe:
    /// - Kein Routing.
    /// - Keine Session-Steuerung.
    /// - Keine Call-Steuerung.
    /// - Keine Media-Ausführung.

    /// <summary>
    /// Defines the <see cref="TKBase_AudioCodecConfig" />
    /// </summary>
    internal readonly struct TKBase_AudioCodecConfig {
        /// <summary>
        /// Gets the Id
        /// </summary>
        public int Id { get; init; }

        /// <summary>
        /// Gets the CodecKey
        /// </summary>
        public string CodecKey { get; init; }

        /// <summary>
        /// Gets the DisplayName
        /// </summary>
        public string DisplayName { get; init; }

        /// <summary>
        /// Gets the Description
        /// </summary>
        public string? Description { get; init; }

        /// <summary>
        /// Gets the MediaType
        /// </summary>
        public string MediaType { get; init; }

        /// <summary>
        /// Gets the RtpPayloadType
        /// </summary>
        public int? RtpPayloadType { get; init; }

        /// <summary>
        /// Gets the ClockRate
        /// </summary>
        public int ClockRate { get; init; }

        /// <summary>
        /// Gets the RtpClockRate
        /// </summary>
        public int RtpClockRate { get; init; }

        /// <summary>
        /// Gets the Channels
        /// </summary>
        public int Channels { get; init; }

        /// <summary>
        /// Gets the Bitrate
        /// </summary>
        public int? Bitrate { get; init; }

        /// <summary>
        /// Gets the RtpEncodingName
        /// </summary>
        public string RtpEncodingName { get; init; }

        /// <summary>
        /// Gets the RtpFormatParams
        /// </summary>
        public string? RtpFormatParams { get; init; }

        /// <summary>
        /// Gets the SampleFormat
        /// </summary>
        public string? SampleFormat { get; init; }

        /// <summary>
        /// Gets the FrameDurationMs
        /// </summary>
        public int? FrameDurationMs { get; init; }

        /// <summary>
        /// Gets the PtimeMs
        /// </summary>
        public int? PtimeMs { get; init; }

        /// <summary>
        /// Gets the MaxPtimeMs
        /// </summary>
        public int? MaxPtimeMs { get; init; }

        /// <summary>
        /// Gets a value indicating whether CanDecode
        /// </summary>
        public bool CanDecode { get; init; }

        /// <summary>
        /// Gets a value indicating whether CanEncode
        /// </summary>
        public bool CanEncode { get; init; }

        /// <summary>
        /// Gets a value indicating whether IsPassThrough
        /// </summary>
        public bool IsPassThrough { get; init; }

        /// <summary>
        /// Gets a value indicating whether IsDefault
        /// </summary>
        public bool IsDefault { get; init; }

        /// <summary>
        /// Gets a value indicating whether IsPreferred
        /// </summary>
        public bool IsPreferred { get; init; }

        /// <summary>
        /// Gets a value indicating whether Active
        /// </summary>
        public bool Active { get; init; }

        /// <summary>
        /// Gets a value indicating whether SupportsSip
        /// </summary>
        public bool SupportsSip { get; init; }

        /// <summary>
        /// Gets a value indicating whether SupportsRtp
        /// </summary>
        public bool SupportsRtp { get; init; }

        /// <summary>
        /// Gets a value indicating whether SupportsGsmBridge
        /// </summary>
        public bool SupportsGsmBridge { get; init; }

        /// <summary>
        /// Gets a value indicating whether SupportsInternal
        /// </summary>
        public bool SupportsInternal { get; init; }

        /// <summary>
        /// Gets a value indicating whether SupportsExternal
        /// </summary>
        public bool SupportsExternal { get; init; }

        /// <summary>
        /// Gets the SortOrder
        /// </summary>
        public int SortOrder { get; init; }

        /// <summary>
        /// Gets the JsonConfig
        /// </summary>
        public string? JsonConfig { get; init; }

        /// <summary>
        /// Gets a value indicating whether IsConfigured
        /// </summary>
        public bool IsConfigured => !string.IsNullOrWhiteSpace(CodecKey) && !string.IsNullOrWhiteSpace(RtpEncodingName);

        /// <summary>
        /// Gets a value indicating whether HasPayloadType
        /// </summary>
        public bool HasPayloadType => RtpPayloadType.HasValue && RtpPayloadType.Value >= 0 && RtpPayloadType.Value <= AudioFormat.DYNAMIC_ID_MAX;

        /// <summary>
        /// Gets a value indicating whether IsTelephoneEvent
        /// </summary>
        public bool IsTelephoneEvent => string.Equals(RtpEncodingName, "telephone-event", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Gets a value indicating whether IsSipRtpCodec
        /// </summary>
        public bool IsSipRtpCodec => Active && SupportsSip && SupportsRtp && HasPayloadType && ClockRate > 0 && RtpClockRate > 0 && Channels > 0 && !IsTelephoneEvent;

        /// <summary>
        /// Gets a value indicating whether IsGsmSipCodec
        /// </summary>
        public bool IsGsmSipCodec => IsSipRtpCodec && CanDecode && CanEncode && IsSupportedBySipsorceryAudioEncoder(RtpEncodingName);

        /// <summary>
        /// Gets a value indicating whether IsGsmTelephoneEventCodec
        /// </summary>
        public bool IsGsmTelephoneEventCodec => Active && SupportsSip && SupportsRtp && HasPayloadType && ClockRate > 0 && RtpClockRate > 0 && Channels > 0 && IsTelephoneEvent;

        /// <summary>
        /// Gets a value indicating whether IsGsmBridgeCodec
        /// </summary>
        public bool IsGsmBridgeCodec => Active && SupportsGsmBridge && ClockRate > 0 && Channels > 0 && !string.IsNullOrWhiteSpace(SampleFormat) && IsSupportedGsmBridgeSampleFormat(SampleFormat);

        /// <summary>
        /// The PayloadTypeOrThrow
        /// </summary>
        /// <returns>The <see cref="int"/></returns>
        public int PayloadTypeOrThrow() {
            if (!HasPayloadType) {
                throw new InvalidOperationException($"Codec {CodecKey} hat keinen gueltigen RTP-PayloadType.");
            }
            return RtpPayloadType.Value;
        }

        /// <summary>
        /// The ToAudioFormat
        /// </summary>
        /// <returns>The <see cref="AudioFormat"/></returns>
        public AudioFormat ToAudioFormat() {
            return new AudioFormat(
                PayloadTypeOrThrow(),
                RtpEncodingName,
                ClockRate,
                RtpClockRate,
                Channels,
                string.IsNullOrWhiteSpace(RtpFormatParams) ? null : RtpFormatParams);
        }

        /// <summary>
        /// The FromDataRow
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <returns>The <see cref="TKBase_AudioCodecConfig"/></returns>
        public static TKBase_AudioCodecConfig FromDataRow(DataRow row) {
            if (row == null) throw new ArgumentNullException(nameof(row));

            TKBase_AudioCodecConfig codec = new TKBase_AudioCodecConfig {
                Id = RequireInt(row, "ID"),
                CodecKey = RequireString(row, "CodecKey"),
                DisplayName = RequireString(row, "DisplayName"),
                Description = OptionalString(row, "Description"),
                MediaType = RequireString(row, "MediaType"),
                RtpPayloadType = OptionalInt(row, "RtpPayloadType"),
                ClockRate = RequireInt(row, "ClockRate"),
                RtpClockRate = RequireInt(row, "RtpClockRate"),
                Channels = RequireInt(row, "Channels"),
                Bitrate = OptionalInt(row, "Bitrate"),
                RtpEncodingName = RequireString(row, "RtpEncodingName"),
                RtpFormatParams = OptionalString(row, "RtpFormatParams"),
                SampleFormat = OptionalString(row, "SampleFormat"),
                FrameDurationMs = OptionalInt(row, "FrameDurationMs"),
                PtimeMs = OptionalInt(row, "PtimeMs"),
                MaxPtimeMs = OptionalInt(row, "MaxPtimeMs"),
                CanDecode = RequireBool(row, "CanDecode"),
                CanEncode = RequireBool(row, "CanEncode"),
                IsPassThrough = RequireBool(row, "IsPassThrough"),
                IsDefault = RequireBool(row, "IsDefault"),
                IsPreferred = RequireBool(row, "IsPreferred"),
                Active = RequireBool(row, "Active"),
                SupportsSip = RequireBool(row, "SupportsSip"),
                SupportsRtp = RequireBool(row, "SupportsRtp"),
                SupportsGsmBridge = RequireBool(row, "SupportsGsmBridge"),
                SupportsInternal = RequireBool(row, "SupportsInternal"),
                SupportsExternal = RequireBool(row, "SupportsExternal"),
                SortOrder = RequireInt(row, "SortOrder"),
                JsonConfig = OptionalString(row, "JsonConfig")
            };

            codec.Validate();
            return codec;
        }

        /// <summary>
        /// The Validate
        /// </summary>
        public void Validate() {
            if (string.IsNullOrWhiteSpace(CodecKey)) throw new InvalidOperationException("TK_AudioCodec.CodecKey fehlt.");
            if (string.IsNullOrWhiteSpace(RtpEncodingName)) throw new InvalidOperationException($"TK_AudioCodec.RtpEncodingName fehlt | Codec={CodecKey}");
            if (ClockRate <= 0) throw new InvalidOperationException($"TK_AudioCodec.ClockRate ungueltig | Codec={CodecKey} | ClockRate={ClockRate}");
            if (RtpClockRate <= 0) throw new InvalidOperationException($"TK_AudioCodec.RtpClockRate ungueltig | Codec={CodecKey} | RtpClockRate={RtpClockRate}");
            if (Channels <= 0) throw new InvalidOperationException($"TK_AudioCodec.Channels ungueltig | Codec={CodecKey} | Channels={Channels}");
            if (RtpPayloadType.HasValue && (RtpPayloadType.Value < 0 || RtpPayloadType.Value > AudioFormat.DYNAMIC_ID_MAX)) {
                throw new InvalidOperationException($"TK_AudioCodec.RtpPayloadType ungueltig | Codec={CodecKey} | Payload={RtpPayloadType.Value}");
            }
            if (PtimeMs.HasValue && PtimeMs.Value <= 0) throw new InvalidOperationException($"TK_AudioCodec.PtimeMs ungueltig | Codec={CodecKey}");
            if (MaxPtimeMs.HasValue && MaxPtimeMs.Value <= 0) throw new InvalidOperationException($"TK_AudioCodec.MaxPtimeMs ungueltig | Codec={CodecKey}");
            if (FrameDurationMs.HasValue && FrameDurationMs.Value <= 0) throw new InvalidOperationException($"TK_AudioCodec.FrameDurationMs ungueltig | Codec={CodecKey}");
        }

        /// <summary>
        /// The SelectGsmSipCodecs
        /// </summary>
        /// <param name="codecs">The codecs<see cref="IEnumerable{TKBase_AudioCodecConfig}"/></param>
        /// <param name="selectedCodecId">The selectedCodecId<see cref="int"/></param>
        /// <returns>The <see cref="IReadOnlyList{TKBase_AudioCodecConfig}"/></returns>
        public static IReadOnlyList<TKBase_AudioCodecConfig> SelectGsmSipCodecs(IEnumerable<TKBase_AudioCodecConfig> codecs, int selectedCodecId) {
            if (codecs == null) throw new ArgumentNullException(nameof(codecs));

            List<TKBase_AudioCodecConfig> usable = codecs
                .Where(c => c.IsGsmSipCodec)
                .OrderBy(c => c.Id == selectedCodecId ? 0 : 1)
                .ThenBy(c => c.SortOrder)
                .ThenBy(c => c.Id)
                .ToList();

            if (usable.Count == 0) {
                throw new InvalidOperationException("Kein aktiver SIP/RTP-Codec fuer GSM-Bridge gefunden. Erlaubt sind PCMA, PCMU, G722, G729 und L16/PCM_S16LE.");
            }

            return usable;
        }

        /// <summary>
        /// The SelectGsmTelephoneEventCodecs
        /// </summary>
        /// <param name="codecs">The codecs<see cref="IEnumerable{TKBase_AudioCodecConfig}"/></param>
        /// <returns>The <see cref="IReadOnlyList{TKBase_AudioCodecConfig}"/></returns>
        public static IReadOnlyList<TKBase_AudioCodecConfig> SelectGsmTelephoneEventCodecs(IEnumerable<TKBase_AudioCodecConfig> codecs) {
            if (codecs == null) throw new ArgumentNullException(nameof(codecs));
            return codecs
                .Where(c => c.IsGsmTelephoneEventCodec)
                .ToList();
        }

        /// <summary>
        /// The SelectGsmBridgeCodec
        /// </summary>
        /// <param name="codecs">The codecs<see cref="IEnumerable{TKBase_AudioCodecConfig}"/></param>
        /// <param name="selectedCodecId">The selectedCodecId<see cref="int"/></param>
        /// <returns>The <see cref="TKBase_AudioCodecConfig"/></returns>
        public static TKBase_AudioCodecConfig SelectGsmBridgeCodec(IEnumerable<TKBase_AudioCodecConfig> codecs, int selectedCodecId) {
            foreach (TKBase_AudioCodecConfig codec in codecs.Where(c => c.Id == selectedCodecId && c.IsGsmBridgeCodec)) {
                return codec;
            }

            throw new InvalidOperationException($"TK_Properties.GSMCodec ist kein aktiver GSM-Bridge-Audiocodec: {selectedCodecId}");
        }

        /// <summary>
        /// The TrySelectGsmSipCodecForPeer
        /// </summary>
        /// <param name="localCodecs">The localCodecs<see cref="IEnumerable{TKBase_AudioCodecConfig}"/></param>
        /// <param name="peerCodecs">The peerCodecs<see cref="IEnumerable{TKBase_PeerAudioCodec}"/></param>
        /// <param name="selectedLocalCodec">The selectedLocalCodec<see cref="TKBase_AudioCodecConfig"/></param>
        /// <param name="selectedPeerCodec">The selectedPeerCodec<see cref="TKBase_PeerAudioCodec"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public static bool TrySelectGsmSipCodecForPeer(
            IEnumerable<TKBase_AudioCodecConfig> localCodecs,
            IEnumerable<TKBase_PeerAudioCodec> peerCodecs,
            out TKBase_AudioCodecConfig selectedLocalCodec,
            out TKBase_PeerAudioCodec selectedPeerCodec) {

            foreach (TKBase_AudioCodecConfig local in localCodecs.Where(c => c.IsGsmSipCodec)) {
                foreach (TKBase_PeerAudioCodec peer in peerCodecs.Where(p => !p.IsTelephoneEvent)) {
                    if (local.Matches(peer)) {
                        selectedLocalCodec = local;
                        selectedPeerCodec = peer;
                        return true;
                    }
                }
            }

            selectedLocalCodec = default;
            selectedPeerCodec = default;
            return false;
        }

        /// <summary>
        /// The Matches
        /// </summary>
        /// <param name="peer">The peer<see cref="TKBase_PeerAudioCodec"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public bool Matches(TKBase_PeerAudioCodec peer) {
            if (!IsGsmSipCodec || !peer.IsConfigured) return false;

            bool payloadMatches = RtpPayloadType.HasValue && peer.PayloadType == RtpPayloadType.Value;
            bool nameMatches = !string.IsNullOrWhiteSpace(peer.EncodingName)
                && string.Equals(peer.EncodingName, RtpEncodingName, StringComparison.OrdinalIgnoreCase);
            bool clockMatches = peer.RtpClockRate <= 0 || peer.RtpClockRate == RtpClockRate;
            bool channelsMatch = peer.Channels <= 0 || peer.Channels == Channels;

            return (payloadMatches || nameMatches) && clockMatches && channelsMatch;
        }

        /// <summary>
        /// The IsSupportedGsmBridgeSampleFormat
        /// </summary>
        /// <param name="sampleFormat">The sampleFormat<see cref="string?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public static bool IsSupportedGsmBridgeSampleFormat(string? sampleFormat) {
            return !string.IsNullOrWhiteSpace(sampleFormat) && sampleFormat.Contains("PCM", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The IsSupportedBySipsorceryAudioEncoder
        /// </summary>
        /// <param name="encodingName">The encodingName<see cref="string?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool IsSupportedBySipsorceryAudioEncoder(string? encodingName) {
            if (string.IsNullOrWhiteSpace(encodingName)) return false;
            string codec = encodingName.Trim();
            return string.Equals(codec, "PCMA", StringComparison.OrdinalIgnoreCase)
                || string.Equals(codec, "PCMU", StringComparison.OrdinalIgnoreCase)
                || string.Equals(codec, "G722", StringComparison.OrdinalIgnoreCase)
                || string.Equals(codec, "G729", StringComparison.OrdinalIgnoreCase)
                || string.Equals(codec, "L16", StringComparison.OrdinalIgnoreCase)
                || string.Equals(codec, "PCM_S16LE", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The RequireString
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="column">The column<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string RequireString(DataRow row, string column) {
            string? value = OptionalString(row, column);
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException($"TK_AudioCodec.{column} fehlt.");
            return value;
        }

        /// <summary>
        /// The RequireInt
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="column">The column<see cref="string"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int RequireInt(DataRow row, string column) {
            int? value = OptionalInt(row, column);
            if (!value.HasValue) throw new InvalidOperationException($"TK_AudioCodec.{column} fehlt.");
            return value.Value;
        }

        /// <summary>
        /// The RequireBool
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="column">The column<see cref="string"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool RequireBool(DataRow row, string column) {
            bool? value = OptionalBool(row, column);
            if (!value.HasValue) throw new InvalidOperationException($"TK_AudioCodec.{column} fehlt.");
            return value.Value;
        }

        /// <summary>
        /// The OptionalString
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="column">The column<see cref="string"/></param>
        /// <returns>The <see cref="string?"/></returns>
        private static string? OptionalString(DataRow row, string column) {
            if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value) return null;
            string value = Convert.ToString(row[column], CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        /// <summary>
        /// The OptionalInt
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="column">The column<see cref="string"/></param>
        /// <returns>The <see cref="int?"/></returns>
        private static int? OptionalInt(DataRow row, string column) {
            if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value) return null;
            return Convert.ToInt32(row[column], CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The OptionalBool
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="column">The column<see cref="string"/></param>
        /// <returns>The <see cref="bool?"/></returns>
        private static bool? OptionalBool(DataRow row, string column) {
            if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value) return null;
            return Convert.ToBoolean(row[column], CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Defines the <see cref="TKBase_PeerAudioCodec" />
    /// </summary>
    internal readonly struct TKBase_PeerAudioCodec {
        /// <summary>
        /// Gets the PayloadType
        /// </summary>
        public int PayloadType { get; init; }

        /// <summary>
        /// Gets the EncodingName
        /// </summary>
        public string EncodingName { get; init; }

        /// <summary>
        /// Gets the ClockRate
        /// </summary>
        public int ClockRate { get; init; }

        /// <summary>
        /// Gets the RtpClockRate
        /// </summary>
        public int RtpClockRate { get; init; }

        /// <summary>
        /// Gets the Channels
        /// </summary>
        public int Channels { get; init; }

        /// <summary>
        /// Gets the FormatParameters
        /// </summary>
        public string FormatParameters { get; init; }

        /// <summary>
        /// Gets the PtimeMs
        /// </summary>
        public int? PtimeMs { get; init; }

        /// <summary>
        /// Gets the MaxPtimeMs
        /// </summary>
        public int? MaxPtimeMs { get; init; }

        /// <summary>
        /// Gets a value indicating whether IsConfigured
        /// </summary>
        public bool IsConfigured => PayloadType >= 0;

        /// <summary>
        /// Gets a value indicating whether IsTelephoneEvent
        /// </summary>
        public bool IsTelephoneEvent => string.Equals(EncodingName, "telephone-event", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// The ParseFromSdp
        /// </summary>
        /// <param name="sdp">The sdp<see cref="string?"/></param>
        /// <returns>The <see cref="IReadOnlyList{TKBase_PeerAudioCodec}"/></returns>
        public static IReadOnlyList<TKBase_PeerAudioCodec> ParseFromSdp(string? sdp) {
            if (string.IsNullOrWhiteSpace(sdp)) return Array.Empty<TKBase_PeerAudioCodec>();

            List<int> audioPayloads = new();
            Dictionary<int, (string name, int rtpClock, int channels)> rtpMaps = new();
            Dictionary<int, string> fmtps = new();
            int? ptime = null;
            int? maxptime = null;
            bool inAudio = false;

            foreach (string raw in SplitLines(sdp)) {
                string line = raw.Trim();
                if (line.Length == 0) continue;

                if (line.StartsWith("m=", StringComparison.OrdinalIgnoreCase)) {
                    inAudio = line.StartsWith("m=audio", StringComparison.OrdinalIgnoreCase);
                    if (inAudio) {
                        string[] parts = line.Substring(2).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        audioPayloads.Clear();
                        for (int i = 3; i < parts.Length; i++) {
                            if (int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int pt)) {
                                audioPayloads.Add(pt);
                            }
                        }
                    }
                    continue;
                }

                if (!inAudio) continue;

                if (line.StartsWith("a=rtpmap:", StringComparison.OrdinalIgnoreCase)) {
                    ParseRtpMap(line, rtpMaps);
                } else if (line.StartsWith("a=fmtp:", StringComparison.OrdinalIgnoreCase)) {
                    ParseFmtp(line, fmtps);
                } else if (line.StartsWith("a=ptime:", StringComparison.OrdinalIgnoreCase)) {
                    ptime = ParseAttributeInt(line, "a=ptime:");
                } else if (line.StartsWith("a=maxptime:", StringComparison.OrdinalIgnoreCase)) {
                    maxptime = ParseAttributeInt(line, "a=maxptime:");
                }
            }

            List<TKBase_PeerAudioCodec> codecs = new();
            foreach (int pt in audioPayloads) {
                if (!rtpMaps.TryGetValue(pt, out var map)) {
                    map = GetStaticPayloadMap(pt);
                }
                fmtps.TryGetValue(pt, out string? fmtp);
                codecs.Add(new TKBase_PeerAudioCodec {
                    PayloadType = pt,
                    EncodingName = map.name ?? string.Empty,
                    ClockRate = map.rtpClock,
                    RtpClockRate = map.rtpClock,
                    Channels = map.channels,
                    FormatParameters = fmtp ?? string.Empty,
                    PtimeMs = ptime,
                    MaxPtimeMs = maxptime
                });
            }

            return codecs;
        }

        /// <summary>
        /// The GetStaticPayloadMap
        /// </summary>
        /// <param name="payloadType">The payloadType<see cref="int"/></param>
        /// <returns>The <see cref="(string name, int rtpClock, int channels)"/></returns>
        private static (string name, int rtpClock, int channels) GetStaticPayloadMap(int payloadType) {
            return payloadType switch {
                0 => ("PCMU", 8000, 1),
                3 => ("GSM", 8000, 1),
                8 => ("PCMA", 8000, 1),
                9 => ("G722", 8000, 1),
                18 => ("G729", 8000, 1),
                _ => (string.Empty, 0, 1)
            };
        }

        /// <summary>
        /// The SplitLines
        /// </summary>
        /// <param name="sdp">The sdp<see cref="string"/></param>
        /// <returns>The <see cref="IEnumerable{string}"/></returns>
        private static IEnumerable<string> SplitLines(string sdp) {
            return sdp.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }

        /// <summary>
        /// The ParseRtpMap
        /// </summary>
        /// <param name="line">The line<see cref="string"/></param>
        /// <param name="target">The target<see cref="Dictionary{int, (string name, int rtpClock, int channels)}"/></param>
        private static void ParseRtpMap(string line, Dictionary<int, (string name, int rtpClock, int channels)> target) {
            string value = line.Substring("a=rtpmap:".Length).Trim();
            int firstSpace = value.IndexOf(' ');
            if (firstSpace <= 0) return;
            if (!int.TryParse(value[..firstSpace], NumberStyles.Integer, CultureInfo.InvariantCulture, out int pt)) return;

            string[] parts = value[(firstSpace + 1)..].Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return;
            string name = parts[0].Trim();
            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int clock)) return;
            int channels = 1;
            if (parts.Length >= 3) int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out channels);
            if (channels <= 0) channels = 1;
            target[pt] = (name, clock, channels);
        }

        /// <summary>
        /// The ParseFmtp
        /// </summary>
        /// <param name="line">The line<see cref="string"/></param>
        /// <param name="target">The target<see cref="Dictionary{int, string}"/></param>
        private static void ParseFmtp(string line, Dictionary<int, string> target) {
            string value = line.Substring("a=fmtp:".Length).Trim();
            int firstSpace = value.IndexOf(' ');
            if (firstSpace <= 0) return;
            if (!int.TryParse(value[..firstSpace], NumberStyles.Integer, CultureInfo.InvariantCulture, out int pt)) return;
            target[pt] = value[(firstSpace + 1)..].Trim();
        }

        /// <summary>
        /// The ParseAttributeInt
        /// </summary>
        /// <param name="line">The line<see cref="string"/></param>
        /// <param name="prefix">The prefix<see cref="string"/></param>
        /// <returns>The <see cref="int?"/></returns>
        private static int? ParseAttributeInt(string line, string prefix) {
            string value = line.Substring(prefix.Length).Trim();
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 0 ? parsed : null;
        }
    }
}
