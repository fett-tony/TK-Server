/*
 * TKSVC_Menu
 * Aufgabe:
 * - Baut die Sprachansage fuer Service-Menues aus dem geladenen ServiceLeg.
 * - Verwendet keine eigene Datenbankverbindung und keine SQL-Abfragen.
 * - Fuehrt kein Routing und keine SIP-Sessionsteuerung aus.
 */

namespace TKServer.SVC {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;

    using TKServer.BASE;

    /// <summary>
    /// Defines the <see cref="TKSVC_Menu" />
    /// </summary>
    internal sealed class TKSVC_Menu {
        /// <summary>
        /// Defines the _logger
        /// </summary>
        private readonly TKBase_LogManager? _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TKSVC_Menu"/> class.
        /// </summary>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        public TKSVC_Menu(TKBase_LogManager? logger = null) {
            _logger = logger;
        }

        /// <summary>
        /// The IsMenuService
        /// </summary>
        /// <param name="serviceLeg">The serviceLeg<see cref="TKSVC_Session?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public static bool IsMenuService(TKSVC_Session? serviceLeg) {
            if (serviceLeg == null) {
                return false;
            }

            string serviceType = TKSVC_ServiceTypes.Normalize(serviceLeg.ServiceType);
            return string.Equals(serviceType, TKSVC_ServiceTypes.ServiceMenu, StringComparison.OrdinalIgnoreCase)
                || string.Equals(serviceType, TKSVC_ServiceTypes.MenuHelp, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The CreatePrompt
        /// </summary>
        /// <param name="serviceLeg">The serviceLeg<see cref="TKSVC_Session"/></param>
        /// <param name="menuEntries">The menuEntries<see cref="IReadOnlyList{TKSVC_Session}?"/></param>
        /// <returns>The <see cref="TKSVC_MenuPrompt"/></returns>
        public TKSVC_MenuPrompt CreatePrompt(TKSVC_Session serviceLeg, IReadOnlyList<TKSVC_Session>? menuEntries = null) {
            ArgumentNullException.ThrowIfNull(serviceLeg);

            TKSVC_MenuOptions options = TKSVC_MenuOptions.FromServiceLeg(serviceLeg, _logger);
            string promptText = BuildPromptText(serviceLeg, options, menuEntries);

            return new TKSVC_MenuPrompt(
                promptText,
                options.VoiceName,
                options.SampleRate,
                options.Volume,
                options.Rate,
                options.KeepSessionUntilPeerEnded);
        }

        /// <summary>
        /// The SynthesizePromptToRawPcmStream
        /// </summary>
        /// <param name="prompt">The prompt<see cref="TKSVC_MenuPrompt"/></param>
        /// <returns>The <see cref="MemoryStream"/></returns>
        public MemoryStream SynthesizePromptToRawPcmStream(TKSVC_MenuPrompt prompt) {
            ArgumentNullException.ThrowIfNull(prompt);

            SIP_SystemSpeech speech = new SIP_SystemSpeech(_logger);
            return speech.SynthesizeTextToRawPcmStream(
                prompt.Text,
                prompt.VoiceName,
                prompt.SampleRate,
                prompt.Volume,
                prompt.Rate);
        }

        /// <summary>
        /// The TryBuildDirectDialNumber
        /// </summary>
        /// <param name="menuRoot">The menuRoot<see cref="string"/></param>
        /// <param name="sequence">The sequence<see cref="string"/></param>
        /// <param name="directDialNumber">The directDialNumber<see cref="string"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public static bool TryBuildDirectDialNumber(string menuRoot, string sequence, out string directDialNumber) {
            directDialNumber = string.Empty;
            string root = TKSVC_Routing.NormalizeServiceNumber(menuRoot);
            string option = NormalizeMenuSequence(sequence);

            if (string.IsNullOrWhiteSpace(root) || !root.StartsWith("*", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(option)) {
                return false;
            }

            directDialNumber = root + option;
            return !string.IsNullOrWhiteSpace(TKSVC_Routing.NormalizeServiceNumber(directDialNumber));
        }

        /// <summary>
        /// The NormalizeMenuSequence
        /// </summary>
        /// <param name="sequence">The sequence<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string NormalizeMenuSequence(string sequence) {
            if (string.IsNullOrWhiteSpace(sequence)) {
                return string.Empty;
            }

            string value = sequence.Trim();
            if (!value.StartsWith("*", StringComparison.Ordinal) || !value.EndsWith("#", StringComparison.Ordinal) || value.Length < 3) {
                return string.Empty;
            }

            for (int i = 1; i < value.Length - 1; i++) {
                if (!char.IsDigit(value[i])) {
                    return string.Empty;
                }
            }

            return value;
        }

        /// <summary>
        /// The BuildPromptText
        /// </summary>
        /// <param name="serviceLeg">The serviceLeg<see cref="TKSVC_Session"/></param>
        /// <param name="options">The options<see cref="TKSVC_MenuOptions"/></param>
        /// <param name="menuEntries">The menuEntries<see cref="IReadOnlyList{TKSVC_Session}?"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string BuildPromptText(TKSVC_Session serviceLeg, TKSVC_MenuOptions options, IReadOnlyList<TKSVC_Session>? menuEntries) {
            StringBuilder builder = new StringBuilder();

            string serviceType = TKSVC_ServiceTypes.Normalize(serviceLeg.ServiceType);
            string firstText = string.Equals(serviceType, TKSVC_ServiceTypes.MenuHelp, StringComparison.OrdinalIgnoreCase)
                ? FirstNotEmpty(options.HelpText, serviceLeg.Description, options.MenuPromptText)
                : FirstNotEmpty(options.MenuPromptText, serviceLeg.Description, options.HelpText);

            AppendSentence(builder, firstText);

            if (menuEntries != null && menuEntries.Count > 0) {
                AppendMenuEntries(builder, menuEntries);
            } else if (options.Options.Count > 0) {
                AppendOptions(builder, options.Options);
            }

            AppendSentence(builder, options.ConfirmText);
            AppendSentence(builder, options.CancelText);

            string text = builder.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(text)) {
                return text;
            }

            return "Hallo, Sie befinden sich im Service-Menue.";
        }

        /// <summary>
        /// The AppendMenuEntries
        /// </summary>
        /// <param name="builder">The builder<see cref="StringBuilder"/></param>
        /// <param name="menuEntries">The menuEntries<see cref="IReadOnlyList{TKSVC_Session}"/></param>
        private static void AppendMenuEntries(StringBuilder builder, IReadOnlyList<TKSVC_Session> menuEntries) {
            foreach (TKSVC_Session entry in menuEntries.OrderBy(x => x.SortOrder).ThenBy(x => x.InternalNumber, StringComparer.OrdinalIgnoreCase)) {
                string option = ExtractOption(entry);
                string description = FirstNotEmpty(entry.Description, entry.DisplayName, entry.ServiceCode);

                if (string.IsNullOrWhiteSpace(option) || string.IsNullOrWhiteSpace(description)) {
                    continue;
                }

                AppendSentence(builder, $"{SpeakableOption(option)} fuer {description}");
            }
        }

        /// <summary>
        /// The AppendOptions
        /// </summary>
        /// <param name="builder">The builder<see cref="StringBuilder"/></param>
        /// <param name="options">The options<see cref="IReadOnlyList{string}"/></param>
        private static void AppendOptions(StringBuilder builder, IReadOnlyList<string> options) {
            foreach (string option in options) {
                if (string.IsNullOrWhiteSpace(option)) {
                    continue;
                }

                AppendSentence(builder, $"Waehlen Sie {SpeakableOption(option)}");
            }
        }

        /// <summary>
        /// The ExtractOption
        /// </summary>
        /// <param name="entry">The entry<see cref="TKSVC_Session"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string ExtractOption(TKSVC_Session entry) {
            if (string.IsNullOrWhiteSpace(entry.InternalNumber)) {
                return string.Empty;
            }

            string value = entry.InternalNumber.Trim();
            int rootEnd = value.IndexOf('*', 1);
            int confirm = value.LastIndexOf('#');

            if (rootEnd < 0 || confirm <= rootEnd) {
                return value;
            }

            return value.Substring(rootEnd, confirm - rootEnd);
        }

        /// <summary>
        /// The SpeakableOption
        /// </summary>
        /// <param name="option">The option<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string SpeakableOption(string option) {
            string value = option.Trim();
            if (value.StartsWith("*", StringComparison.Ordinal)) {
                return "Stern " + value.Substring(1);
            }

            return value;
        }

        /// <summary>
        /// The AppendSentence
        /// </summary>
        /// <param name="builder">The builder<see cref="StringBuilder"/></param>
        /// <param name="text">The text<see cref="string?"/></param>
        private static void AppendSentence(StringBuilder builder, string? text) {
            if (string.IsNullOrWhiteSpace(text)) {
                return;
            }

            string sentence = text.Trim();
            builder.Append(sentence);

            if (!sentence.EndsWith(".", StringComparison.Ordinal)
                && !sentence.EndsWith("!", StringComparison.Ordinal)
                && !sentence.EndsWith("?", StringComparison.Ordinal)) {
                builder.Append('.');
            }

            builder.Append(' ');
        }

        /// <summary>
        /// The FirstNotEmpty
        /// </summary>
        /// <param name="values">The values<see cref="string?[]"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string FirstNotEmpty(params string?[] values) {
            foreach (string? value in values) {
                if (!string.IsNullOrWhiteSpace(value)) {
                    return value.Trim();
                }
            }

            return string.Empty;
        }
    }

    /// <summary>
    /// Defines the <see cref="TKSVC_MenuPrompt" />
    /// </summary>
    internal sealed class TKSVC_MenuPrompt {
        /// <summary>
        /// Initializes a new instance of the <see cref="TKSVC_MenuPrompt"/> class.
        /// </summary>
        public TKSVC_MenuPrompt(string text, string? voiceName, int sampleRate, int volume, int rate, bool keepSessionUntilPeerEnded) {
            Text = text;
            VoiceName = voiceName;
            SampleRate = sampleRate;
            Volume = volume;
            Rate = rate;
            KeepSessionUntilPeerEnded = keepSessionUntilPeerEnded;
        }

        /// <summary>
        /// Gets the Text
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Gets the VoiceName
        /// </summary>
        public string? VoiceName { get; }

        /// <summary>
        /// Gets the SampleRate
        /// </summary>
        public int SampleRate { get; }

        /// <summary>
        /// Gets the Volume
        /// </summary>
        public int Volume { get; }

        /// <summary>
        /// Gets the Rate
        /// </summary>
        public int Rate { get; }

        /// <summary>
        /// Gets a value indicating whether KeepSessionUntilPeerEnded
        /// </summary>
        public bool KeepSessionUntilPeerEnded { get; }
    }

    /// <summary>
    /// Defines the <see cref="TKSVC_MenuOptions" />
    /// </summary>
    internal sealed class TKSVC_MenuOptions {
        /// <summary>
        /// Gets the MenuPromptText
        /// </summary>
        public string MenuPromptText { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the HelpText
        /// </summary>
        public string HelpText { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the ConfirmText
        /// </summary>
        public string ConfirmText { get; private set; } = "Zum Bestaetigen fuegen Sie Ihrer Wahl die Raute hinzu.";

        /// <summary>
        /// Gets the CancelText
        /// </summary>
        public string CancelText { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the Options
        /// </summary>
        public IReadOnlyList<string> Options { get; private set; } = Array.Empty<string>();

        /// <summary>
        /// Gets the VoiceName
        /// </summary>
        public string? VoiceName { get; private set; }

        /// <summary>
        /// Gets the SampleRate
        /// </summary>
        public int SampleRate { get; private set; } = 16000;

        /// <summary>
        /// Gets the Volume
        /// </summary>
        public int Volume { get; private set; } = 100;

        /// <summary>
        /// Gets the Rate
        /// </summary>
        public int Rate { get; private set; }

        /// <summary>
        /// Gets a value indicating whether KeepSessionUntilPeerEnded
        /// </summary>
        public bool KeepSessionUntilPeerEnded { get; private set; } = true;

        /// <summary>
        /// The FromServiceLeg
        /// </summary>
        /// <param name="serviceLeg">The serviceLeg<see cref="TKSVC_Session"/></param>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        /// <returns>The <see cref="TKSVC_MenuOptions"/></returns>
        public static TKSVC_MenuOptions FromServiceLeg(TKSVC_Session serviceLeg, TKBase_LogManager? logger) {
            TKSVC_MenuOptions options = new TKSVC_MenuOptions {
                KeepSessionUntilPeerEnded = serviceLeg.KeepSessionUntilPeerEnded,
                VoiceName = null
            };

            if (string.IsNullOrWhiteSpace(serviceLeg.JsonConfig)) {
                return options.Normalize();
            }

            try {
                using JsonDocument doc = JsonDocument.Parse(serviceLeg.JsonConfig);
                JsonElement root = doc.RootElement;

                options.MenuPromptText = ReadString(root, "MenuPromptText")
                    ?? ReadString(root, "menuPromptText")
                    ?? ReadString(root, "PromptText")
                    ?? ReadString(root, "promptText")
                    ?? options.MenuPromptText;
                options.HelpText = ReadString(root, "HelpText")
                    ?? ReadString(root, "helpText")
                    ?? options.HelpText;
                options.ConfirmText = ReadString(root, "ConfirmText")
                    ?? ReadString(root, "confirmText")
                    ?? options.ConfirmText;
                options.CancelText = ReadString(root, "CancelText")
                    ?? ReadString(root, "cancelText")
                    ?? options.CancelText;
                options.Options = ReadStringArray(root, "Options")
                    ?? ReadStringArray(root, "options")
                    ?? options.Options;
                options.VoiceName = ReadString(root, "voiceName")
                    ?? ReadString(root, "voice")
                    ?? options.VoiceName;
                options.SampleRate = ReadInt(root, "sampleRate") ?? options.SampleRate;
                options.Volume = ReadInt(root, "volume") ?? options.Volume;
                options.Rate = ReadInt(root, "rate") ?? options.Rate;
                options.KeepSessionUntilPeerEnded = ReadBool(root, "keepSessionUntilPeerEnded") ?? options.KeepSessionUntilPeerEnded;
            } catch (Exception ex) {
                logger?.Warn($"Service-Menue JsonConfig konnte nicht gelesen werden | InternalNumber={serviceLeg.InternalNumber} | {ex.Message}");
            }

            return options.Normalize();
        }

        /// <summary>
        /// The Normalize
        /// </summary>
        /// <returns>The <see cref="TKSVC_MenuOptions"/></returns>
        private TKSVC_MenuOptions Normalize() {
            SampleRate = SampleRate <= 8000 ? 8000 : 16000;
            Volume = Math.Clamp(Volume, 0, 100);
            Rate = Math.Clamp(Rate, -10, 10);
            Options = Options.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray();
            return this;
        }

        /// <summary>
        /// The ReadString
        /// </summary>
        private static string? ReadString(JsonElement root, string propertyName) {
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(propertyName, out JsonElement value)) {
                return null;
            }

            return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }

        /// <summary>
        /// The ReadStringArray
        /// </summary>
        private static IReadOnlyList<string>? ReadStringArray(JsonElement root, string propertyName) {
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind != JsonValueKind.Array) {
                return null;
            }

            List<string> values = new List<string>();
            foreach (JsonElement item in value.EnumerateArray()) {
                if (item.ValueKind == JsonValueKind.String) {
                    string? text = item.GetString();
                    if (!string.IsNullOrWhiteSpace(text)) {
                        values.Add(text.Trim());
                    }
                }
            }

            return values;
        }

        /// <summary>
        /// The ReadInt
        /// </summary>
        private static int? ReadInt(JsonElement root, string propertyName) {
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(propertyName, out JsonElement value)) {
                return null;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int parsed)) {
                return parsed;
            }

            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out parsed)) {
                return parsed;
            }

            return null;
        }

        /// <summary>
        /// The ReadBool
        /// </summary>
        private static bool? ReadBool(JsonElement root, string propertyName) {
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(propertyName, out JsonElement value)) {
                return null;
            }

            if (value.ValueKind == JsonValueKind.True) {
                return true;
            }

            if (value.ValueKind == JsonValueKind.False) {
                return false;
            }

            if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out bool parsed)) {
                return parsed;
            }

            return null;
        }
    }
}
