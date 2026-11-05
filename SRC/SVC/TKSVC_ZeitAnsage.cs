namespace TKServer.SVC {
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Speech.AudioFormat;
    using System.Speech.Synthesis;
    using System.Text;

    using TKServer.BASE;
    /// <summary>
    /// Defines the <see cref="SIP_SystemSpeech" />
    /// </summary>
    internal sealed class SIP_SystemSpeech {
        /// <summary>
        /// Defines the _logger
        /// </summary>
        private readonly TKBase_LogManager? _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SIP_SystemSpeech"/> class.
        /// </summary>
        /// <param name="logger">The logger<see cref="TKBase_LogManager?"/></param>
        public SIP_SystemSpeech(TKBase_LogManager? logger = null) {
            _logger = logger;
        }

        /// <summary>
        /// The GetInstalledVoiceNames
        /// </summary>
        /// <returns>The <see cref="IReadOnlyList{string}"/></returns>
        public IReadOnlyList<string> GetInstalledVoiceNames() {
            using SpeechSynthesizer synth = new SpeechSynthesizer();

            return synth
                .GetInstalledVoices()
                .Where(x => x.Enabled && x.VoiceInfo != null)
                .Select(x => x.VoiceInfo.Name)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        /// <summary>
        /// The GetPreferredGermanVoiceName
        /// </summary>
        /// <returns>The <see cref="string?"/></returns>
        public string? GetPreferredGermanVoiceName() {
            using SpeechSynthesizer synth = new SpeechSynthesizer();
            return GetPreferredGermanVoiceName(synth);
        }

        /// <summary>
        /// The HasVoice
        /// </summary>
        /// <param name="voiceName">The voiceName<see cref="string?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public bool HasVoice(string? voiceName) {
            if (string.IsNullOrWhiteSpace(voiceName)) {
                return false;
            }

            using SpeechSynthesizer synth = new SpeechSynthesizer();

            return synth
                .GetInstalledVoices()
                .Any(x => x.Enabled &&
                          x.VoiceInfo != null &&
                          string.Equals(x.VoiceInfo.Name, voiceName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// The BuildTimeAnnouncementText
        /// </summary>
        /// <param name="time">The time<see cref="DateTime"/></param>
        /// <param name="includeSeconds">The includeSeconds<see cref="bool"/></param>
        /// <returns>The <see cref="string"/></returns>
        public string BuildTimeAnnouncementText(DateTime time, bool includeSeconds = false) {
            CultureInfo culture = CultureInfo.GetCultureInfo("de-DE");

            string text = $"Es ist {time.Hour.ToString(culture)} Uhr";

            if (time.Minute > 0) {
                text += $" {time.Minute.ToString(culture)}";
            } else {
                text += " genau";
            }

            if (includeSeconds) {
                if (time.Second > 0) {
                    text += $" und {time.Second.ToString(culture)} Sekunden";
                } else {
                    text += " und 0 Sekunden";
                }
            }

            return text + ".";
        }

        /// <summary>
        /// The SynthesizeTextToRawPcmBytes
        /// </summary>
        /// <param name="text">The text<see cref="string"/></param>
        /// <param name="voiceName">The voiceName<see cref="string?"/></param>
        /// <param name="sampleRate">The sampleRate<see cref="int"/></param>
        /// <param name="volume">The volume<see cref="int"/></param>
        /// <param name="rate">The rate<see cref="int"/></param>
        /// <returns>The <see cref="byte[]"/></returns>
        public byte[] SynthesizeTextToRawPcmBytes(string text, string? voiceName = null, int sampleRate = 16000, int volume = 100, int rate = 0) {

            using MemoryStream pcmStream = SynthesizeTextToRawPcmStream(text, voiceName, sampleRate, volume, rate);
            return pcmStream.ToArray();
        }

        /// <summary>
        /// The SynthesizeTextToRawPcmStream
        /// </summary>
        /// <param name="text">The text<see cref="string"/></param>
        /// <param name="voiceName">The voiceName<see cref="string?"/></param>
        /// <param name="sampleRate">The sampleRate<see cref="int"/></param>
        /// <param name="volume">The volume<see cref="int"/></param>
        /// <param name="rate">The rate<see cref="int"/></param>
        /// <returns>The <see cref="MemoryStream"/></returns>
        public MemoryStream SynthesizeTextToRawPcmStream(
            string text,
            string? voiceName = null,
            int sampleRate = 16000,
            int volume = 100,
            int rate = 0) {

            ValidateText(text);
            ValidateSampleRate(sampleRate);
            ValidateVolume(volume);
            ValidateRate(rate);

            SpeechAudioFormatInfo formatInfo = CreateFormatInfo(sampleRate);
            MemoryStream audioStream = new MemoryStream();

            using SpeechSynthesizer synth = CreateSynthesizer(voiceName, volume, rate);
            synth.SetOutputToAudioStream(audioStream, formatInfo);
            synth.Speak(text);
            synth.SetOutputToNull();

            byte[] pcmBytes = ExtractPcmPayload(audioStream);
            MemoryStream pcmStream = new MemoryStream(pcmBytes, writable: false);
            pcmStream.Position = 0;

            _logger?.Info($"System.Speech PCM erzeugt | Bytes={pcmBytes.Length} | SampleRate={sampleRate}");
            return pcmStream;
        }

        /// <summary>
        /// The SynthesizeCurrentTimeToRawPcmStream
        /// </summary>
        /// <param name="voiceName">The voiceName<see cref="string?"/></param>
        /// <param name="sampleRate">The sampleRate<see cref="int"/></param>
        /// <param name="volume">The volume<see cref="int"/></param>
        /// <param name="rate">The rate<see cref="int"/></param>
        /// <param name="includeSeconds">The includeSeconds<see cref="bool"/></param>
        /// <returns>The <see cref="MemoryStream"/></returns>
        public MemoryStream SynthesizeCurrentTimeToRawPcmStream(
            string? voiceName = null,
            int sampleRate = 16000,
            int volume = 100,
            int rate = 0,
            bool includeSeconds = false) {

            string text = BuildTimeAnnouncementText(DateTime.Now, includeSeconds);
            return SynthesizeTextToRawPcmStream(text, voiceName, sampleRate, volume, rate);
        }

        /// <summary>
        /// The SynthesizeCurrentTimeToRawPcmBytes
        /// </summary>
        /// <param name="voiceName">The voiceName<see cref="string?"/></param>
        /// <param name="sampleRate">The sampleRate<see cref="int"/></param>
        /// <param name="volume">The volume<see cref="int"/></param>
        /// <param name="rate">The rate<see cref="int"/></param>
        /// <param name="includeSeconds">The includeSeconds<see cref="bool"/></param>
        /// <returns>The <see cref="byte[]"/></returns>
        public byte[] SynthesizeCurrentTimeToRawPcmBytes(
            string? voiceName = null,
            int sampleRate = 16000,
            int volume = 100,
            int rate = 0,
            bool includeSeconds = false) {

            string text = BuildTimeAnnouncementText(DateTime.Now, includeSeconds);
            return SynthesizeTextToRawPcmBytes(text, voiceName, sampleRate, volume, rate);
        }

        /// <summary>
        /// The SaveTextAsWaveFile
        /// </summary>
        /// <param name="filePath">The filePath<see cref="string"/></param>
        /// <param name="text">The text<see cref="string"/></param>
        /// <param name="voiceName">The voiceName<see cref="string?"/></param>
        /// <param name="sampleRate">The sampleRate<see cref="int"/></param>
        /// <param name="volume">The volume<see cref="int"/></param>
        /// <param name="rate">The rate<see cref="int"/></param>
        public void SaveTextAsWaveFile(string filePath, string text, string? voiceName = null, int sampleRate = 16000, int volume = 100, int rate = 0) {

            ValidateText(text);
            ValidateSampleRate(sampleRate);
            ValidateVolume(volume);
            ValidateRate(rate);

            if (string.IsNullOrWhiteSpace(filePath)) {
                throw new ArgumentException("Dateipfad darf nicht leer sein.", nameof(filePath));
            }

            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
            }

            using SpeechSynthesizer synth = CreateSynthesizer(voiceName, volume, rate);
            synth.SetOutputToWaveFile(filePath, CreateFormatInfo(sampleRate));
            synth.Speak(text);
            synth.SetOutputToNull();

            _logger?.Info($"System.Speech WAV gespeichert | Datei={filePath} | SampleRate={sampleRate}");
        }

        /// <summary>
        /// The SaveCurrentTimeAsWaveFile
        /// </summary>
        /// <param name="filePath">The filePath<see cref="string"/></param>
        /// <param name="voiceName">The voiceName<see cref="string?"/></param>
        /// <param name="sampleRate">The sampleRate<see cref="int"/></param>
        /// <param name="volume">The volume<see cref="int"/></param>
        /// <param name="rate">The rate<see cref="int"/></param>
        /// <param name="includeSeconds">The includeSeconds<see cref="bool"/></param>
        public void SaveCurrentTimeAsWaveFile(string filePath, string? voiceName = null, int sampleRate = 16000, int volume = 100, int rate = 0, bool includeSeconds = false) {

            string text = BuildTimeAnnouncementText(DateTime.Now, includeSeconds);
            SaveTextAsWaveFile(filePath, text, voiceName, sampleRate, volume, rate);
        }

        /// <summary>
        /// The CreateSynthesizer
        /// </summary>
        /// <param name="voiceName">The voiceName<see cref="string?"/></param>
        /// <param name="volume">The volume<see cref="int"/></param>
        /// <param name="rate">The rate<see cref="int"/></param>
        /// <returns>The <see cref="SpeechSynthesizer"/></returns>
        private SpeechSynthesizer CreateSynthesizer(string? voiceName, int volume, int rate) {
            SpeechSynthesizer synth = new SpeechSynthesizer();

            string? selectedVoice = null;

            if (!string.IsNullOrWhiteSpace(voiceName)) {
                if (!HasVoice(voiceName)) {
                    synth.Dispose();
                    throw new InvalidOperationException($"System.Speech Stimme nicht gefunden: {voiceName}");
                }

                synth.SelectVoice(voiceName);
                selectedVoice = voiceName;
            } else {
                selectedVoice = GetPreferredGermanVoiceName(synth);
                if (!string.IsNullOrWhiteSpace(selectedVoice)) {
                    synth.SelectVoice(selectedVoice);
                }
            }

            synth.Volume = volume;
            synth.Rate = rate;

            _logger?.Info($"System.Speech initialisiert | Voice={selectedVoice ?? "Default"} | Volume={volume} | Rate={rate}");
            return synth;
        }

        /// <summary>
        /// The GetPreferredGermanVoiceName
        /// </summary>
        /// <param name="synth">The synth<see cref="SpeechSynthesizer"/></param>
        /// <returns>The <see cref="string?"/></returns>
        private string? GetPreferredGermanVoiceName(SpeechSynthesizer synth) {
            return synth
                .GetInstalledVoices()
                .Where(x => x.Enabled && x.VoiceInfo?.Culture != null)
                .OrderByDescending(x => string.Equals(x.VoiceInfo.Culture.Name, "de-DE", StringComparison.OrdinalIgnoreCase))
                .ThenBy(x => x.VoiceInfo.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.VoiceInfo)
                .FirstOrDefault(x => string.Equals(x.Culture.TwoLetterISOLanguageName, "de", StringComparison.OrdinalIgnoreCase))
                ?.Name;
        }

        /// <summary>
        /// The CreateFormatInfo
        /// </summary>
        /// <param name="sampleRate">The sampleRate<see cref="int"/></param>
        /// <returns>The <see cref="SpeechAudioFormatInfo"/></returns>
        private static SpeechAudioFormatInfo CreateFormatInfo(int sampleRate) {
            return new SpeechAudioFormatInfo(sampleRate, AudioBitsPerSample.Sixteen, AudioChannel.Mono);
        }

        /// <summary>
        /// The ExtractPcmPayload
        /// </summary>
        /// <param name="audioStream">The audioStream<see cref="Stream"/></param>
        /// <returns>The <see cref="byte[]"/></returns>
        private static byte[] ExtractPcmPayload(Stream audioStream) {
            if (audioStream == null) {
                throw new ArgumentNullException(nameof(audioStream));
            }

            audioStream.Position = 0;

            using MemoryStream copy = new MemoryStream();
            audioStream.CopyTo(copy);
            byte[] buffer = copy.ToArray();

            if (buffer.Length < 12) {
                return buffer;
            }

            string riff = Encoding.ASCII.GetString(buffer, 0, 4);
            string wave = Encoding.ASCII.GetString(buffer, 8, 4);

            if (!string.Equals(riff, "RIFF", StringComparison.Ordinal) ||
                !string.Equals(wave, "WAVE", StringComparison.Ordinal)) {
                return buffer;
            }

            int offset = 12;
            while (offset + 8 <= buffer.Length) {
                string chunkId = Encoding.ASCII.GetString(buffer, offset, 4);
                int chunkSize = BitConverter.ToInt32(buffer, offset + 4);
                offset += 8;

                if (chunkSize < 0 || offset + chunkSize > buffer.Length) {
                    break;
                }

                if (string.Equals(chunkId, "data", StringComparison.Ordinal)) {
                    byte[] pcm = new byte[chunkSize];
                    Buffer.BlockCopy(buffer, offset, pcm, 0, chunkSize);
                    return pcm;
                }

                offset += chunkSize;

                if ((chunkSize & 1) == 1) {
                    offset++;
                }
            }

            throw new InvalidDataException("Kein WAV-data-Chunk im System.Speech Stream gefunden.");
        }

        /// <summary>
        /// The ValidateText
        /// </summary>
        /// <param name="text">The text<see cref="string"/></param>
        private static void ValidateText(string text) {
            if (string.IsNullOrWhiteSpace(text)) {
                throw new ArgumentException("Text darf nicht leer sein.", nameof(text));
            }
        }

        /// <summary>
        /// The ValidateSampleRate
        /// </summary>
        /// <param name="sampleRate">The sampleRate<see cref="int"/></param>
        private static void ValidateSampleRate(int sampleRate) {
            if (sampleRate != 8000 && sampleRate != 16000) {
                throw new ArgumentOutOfRangeException(nameof(sampleRate), "Erlaubt sind nur 8000 oder 16000 Hz.");
            }
        }

        /// <summary>
        /// The ValidateVolume
        /// </summary>
        /// <param name="volume">The volume<see cref="int"/></param>
        private static void ValidateVolume(int volume) {
            if (volume < 0 || volume > 100) {
                throw new ArgumentOutOfRangeException(nameof(volume), "Volume muss zwischen 0 und 100 liegen.");
            }
        }

        /// <summary>
        /// The ValidateRate
        /// </summary>
        /// <param name="rate">The rate<see cref="int"/></param>
        private static void ValidateRate(int rate) {
            if (rate < -10 || rate > 10) {
                throw new ArgumentOutOfRangeException(nameof(rate), "Rate muss zwischen -10 und 10 liegen.");
            }
        }
    }
}
