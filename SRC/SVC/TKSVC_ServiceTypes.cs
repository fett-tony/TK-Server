namespace TKServer.SVC {
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Defines the <see cref="TKSVC_ServiceTypes" />
    /// </summary>
    public static class TKSVC_ServiceTypes {
        /// <summary>
        /// Defines the Voicemail
        /// </summary>
        public const string Voicemail = "Voicemail";

        /// <summary>
        /// Defines the Echo
        /// </summary>
        public const string Echo = "Echo";

        /// <summary>
        /// Defines the Tone
        /// </summary>
        public const string Tone = "Tone";

        /// <summary>
        /// Defines the Recorder
        /// </summary>
        public const string Recorder = "Recorder";

        /// <summary>
        /// Defines the Playback
        /// </summary>
        public const string Playback = "Playback";

        /// <summary>
        /// Defines the Blackhole
        /// </summary>
        public const string Blackhole = "Blackhole";

        /// <summary>
        /// Defines the TimeSpeech
        /// </summary>
        public const string TimeSpeech = "TimeSpeech";

        /// <summary>
        /// Defines the ServiceMenu
        /// </summary>
        public const string ServiceMenu = "ServiceMenu";

        /// <summary>
        /// Defines the MenuHelp
        /// </summary>
        public const string MenuHelp = "MenuHelp";

        /// <summary>
        /// Defines the VoicemailGreeting
        /// </summary>
        public const string VoicemailGreeting = "VoicemailGreeting";

        /// <summary>
        /// Defines the VoicemailGreetingPlayback
        /// </summary>
        public const string VoicemailGreetingPlayback = "VoicemailGreetingPlayback";

        /// <summary>
        /// Defines the CancelTransfer
        /// </summary>
        public const string CancelTransfer = "CancelTransfer";

        /// <summary>
        /// Defines the HoldResume
        /// </summary>
        public const string HoldResume = "HoldResume";

        /// <summary>
        /// Defines the BlindTransfer
        /// </summary>
        public const string BlindTransfer = "BlindTransfer";

        /// <summary>
        /// Defines the AttendedTransfer
        /// </summary>
        public const string AttendedTransfer = "AttendedTransfer";

        /// <summary>
        /// Defines the TransferToService
        /// </summary>
        public const string TransferToService = "TransferToService";

        /// <summary>
        /// Defines the ConfirmTransfer
        /// </summary>
        public const string ConfirmTransfer = "ConfirmTransfer";

        /// <summary>
        /// Defines the CallRecording
        /// </summary>
        public const string CallRecording = "CallRecording";

        /// <summary>
        /// Defines the _knownTypes
        /// </summary>
        private static readonly string[] _knownTypes = new[] {
            Voicemail,
            Echo,
            Tone,
            Recorder,
            Playback,
            Blackhole,
            TimeSpeech,
            ServiceMenu,
            MenuHelp,
            VoicemailGreeting,
            VoicemailGreetingPlayback,
            CancelTransfer,
            HoldResume,
            BlindTransfer,
            AttendedTransfer,
            TransferToService,
            ConfirmTransfer,
            CallRecording
        };

        /// <summary>
        /// Gets the KnownTypes
        /// </summary>
        public static IReadOnlyList<string> KnownTypes => _knownTypes;

        /// <summary>
        /// The IsKnown
        /// </summary>
        /// <param name="serviceType">The serviceType<see cref="string?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public static bool IsKnown(string? serviceType) {
            if (string.IsNullOrWhiteSpace(serviceType)) {
                return false;
            }

            return _knownTypes.Any(x => string.Equals(x, serviceType.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// The Normalize
        /// </summary>
        /// <param name="serviceType">The serviceType<see cref="string?"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string Normalize(string? serviceType) {
            if (string.IsNullOrWhiteSpace(serviceType)) {
                return string.Empty;
            }

            string value = serviceType.Trim();
            string? known = _knownTypes.FirstOrDefault(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase));
            return known ?? value;
        }
    }

    /// <summary>
    /// Defines the <see cref="SIP_ServiceCodes" />
    /// </summary>
    public static class SIP_ServiceCodes {
        /// <summary>
        /// Defines the Voicemail
        /// </summary>
        public const string Voicemail = "VOICEMAIL";

        /// <summary>
        /// Defines the EchoTest
        /// </summary>
        public const string EchoTest = "ECHO_TEST";

        /// <summary>
        /// Defines the ToneTest
        /// </summary>
        public const string ToneTest = "TONE_TEST";

        /// <summary>
        /// Defines the Recorder
        /// </summary>
        public const string Recorder = "RECORDER";

        /// <summary>
        /// Defines the Playback
        /// </summary>
        public const string Playback = "PLAYBACK";

        /// <summary>
        /// Defines the Blackhole
        /// </summary>
        public const string Blackhole = "BLACKHOLE";

        /// <summary>
        /// Defines the TimeSpeech
        /// </summary>
        public const string TimeSpeech = "TIMESPEECH";

        /// <summary>
        /// Defines the MainMenu
        /// </summary>
        public const string MainMenu = "MAIN_MENU";

        /// <summary>
        /// Defines the MenuMore
        /// </summary>
        public const string MenuMore = "MENU_MORE";

        /// <summary>
        /// Defines the MenuHelp
        /// </summary>
        public const string MenuHelp = "MENU_HELP";

        /// <summary>
        /// Defines the VoicemailGreetingRecord
        /// </summary>
        public const string VoicemailGreetingRecord = "VOICEMAIL_GREETING_RECORD";

        /// <summary>
        /// Defines the VoicemailGreetingPlayback
        /// </summary>
        public const string VoicemailGreetingPlayback = "VOICEMAIL_GREETING_PLAYBACK";

        /// <summary>
        /// Defines the CancelTransfer
        /// </summary>
        public const string CancelTransfer = "CANCEL_TRANSFER";

        /// <summary>
        /// Defines the HoldResume
        /// </summary>
        public const string HoldResume = "HOLD_RESUME";

        /// <summary>
        /// Defines the BlindTransfer
        /// </summary>
        public const string BlindTransfer = "BLIND_TRANSFER";

        /// <summary>
        /// Defines the AttendedTransfer
        /// </summary>
        public const string AttendedTransfer = "ATTENDED_TRANSFER";

        /// <summary>
        /// Defines the TransferToService
        /// </summary>
        public const string TransferToService = "TRANSFER_TO_SERVICE";

        /// <summary>
        /// Defines the ConfirmTransfer
        /// </summary>
        public const string ConfirmTransfer = "CONFIRM_TRANSFER";

        /// <summary>
        /// Defines the CallRecordingToggle
        /// </summary>
        public const string CallRecordingToggle = "CALL_RECORDING_TOGGLE";

        /// <summary>
        /// Defines the _knownCodes
        /// </summary>
        private static readonly string[] _knownCodes = new[] {
            Voicemail,
            EchoTest,
            ToneTest,
            Recorder,
            Playback,
            Blackhole,
            TimeSpeech,
            MainMenu,
            MenuMore,
            MenuHelp,
            VoicemailGreetingRecord,
            VoicemailGreetingPlayback,
            CancelTransfer,
            HoldResume,
            BlindTransfer,
            AttendedTransfer,
            TransferToService,
            ConfirmTransfer,
            CallRecordingToggle
        };

        /// <summary>
        /// Gets the KnownCodes
        /// </summary>
        public static IReadOnlyList<string> KnownCodes => _knownCodes;

        /// <summary>
        /// The IsKnown
        /// </summary>
        /// <param name="serviceCode">The serviceCode<see cref="string?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public static bool IsKnown(string? serviceCode) {
            if (string.IsNullOrWhiteSpace(serviceCode)) {
                return false;
            }

            return _knownCodes.Any(x => string.Equals(x, serviceCode.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// The Normalize
        /// </summary>
        /// <param name="serviceCode">The serviceCode<see cref="string?"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string Normalize(string? serviceCode) {
            if (string.IsNullOrWhiteSpace(serviceCode)) {
                return string.Empty;
            }

            string value = serviceCode.Trim();
            string? known = _knownCodes.FirstOrDefault(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase));
            return known ?? value;
        }
    }
}
