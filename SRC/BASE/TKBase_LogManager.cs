namespace TKServer.BASE {
    using System;
    using System.Data;
    using System.Diagnostics;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Windows.Forms;

    using TKServer.SQL;
    /// <summary>
    /// Defines the LogLevel
    /// </summary>
    public enum LogLevel {
        /// <summary>
        /// Defines detailed diagnostic output. Debug is controlled separately and is never written to the Windows EventLog.
        /// </summary>
        Debug,

        /// <summary>
        /// Defines the Info
        /// </summary>
        Info,

        /// <summary>
        /// Defines the Warn
        /// </summary>
        Warn,

        /// <summary>
        /// Defines the Error
        /// </summary>
        Error
    }

    /// <summary>
    /// Defines the LogTarget
    /// </summary>
    [Flags]
    public enum LogTarget {
        /// <summary>
        /// Defines the None
        /// </summary>
        None = 0,

        /// <summary>
        /// Defines the File
        /// </summary>
        File = 1,

        /// <summary>
        /// Defines the Ui
        /// </summary>
        Ui = 2,

        /// <summary>
        /// Defines the EventLog
        /// </summary>
        EventLog = 4
    }

    /// <summary>
    /// Defines the central output profile used by the logger.
    /// </summary>
    public enum LogOutputMode {
        /// <summary>
        /// GUI mode writes normal logs to file, UI and Windows EventLog.
        /// </summary>
        Gui,

        /// <summary>
        /// Service mode writes normal logs to file and Windows EventLog only.
        /// </summary>
        Service
    }

    /// <summary>
    /// Defines the <see cref="LogRotationOptions" />
    /// </summary>
    public sealed class LogRotationOptions {
        /// <summary>
        /// Gets or sets the MaxFileSizeBytes
        /// </summary>
        public long MaxFileSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the MaxArchiveFiles
        /// </summary>
        public int MaxArchiveFiles { get; set; } = 10;
    }

    /// <summary>
    /// Defines the <see cref="TKBase_LogManager" />
    /// </summary>
    public class TKBase_LogManager {
        public static event Action<LogLevel?, string>? ServiceLogLineEmitted;

        /// <summary>
        /// Defines the EventSourceName
        /// </summary>
        private const string EventSourceName = "TK-Server";

        /// <summary>
        /// Defines the EventLogName
        /// </summary>
        private const string EventLogName = "Application";

        /// <summary>
        /// Defines the _logBox
        /// </summary>
        private readonly RichTextBox? _logBox;

        /// <summary>
        /// Defines the _logFilePath
        /// </summary>
        private readonly string _logFilePath;

        /// <summary>
        /// Defines the _fileLock
        /// </summary>
        private readonly object _fileLock = new object();

        /// <summary>
        /// Defines the configured minimum level for normal logging. Debug is controlled separately.
        /// </summary>
        private LogLevel _minimumLogLevel = LogLevel.Info;

        /// <summary>
        /// Defines whether Debug messages are written.
        /// </summary>
        private bool _debugLoggingEnabled;

        /// <summary>
        /// Defines the current central output profile.
        /// </summary>
        private LogOutputMode _outputMode;

        /// <summary>
        /// Gets or sets a value indicating whether UseColorsInUi
        /// </summary>
        public bool UseColorsInUi { get; set; } = true;

        /// <summary>
        /// Gets the configured minimum level for normal logging.
        /// </summary>
        public LogLevel MinimumLogLevel => _minimumLogLevel;

        /// <summary>
        /// Gets a value indicating whether Debug messages are written.
        /// </summary>
        public bool DebugLoggingEnabled => _debugLoggingEnabled;

        /// <summary>
        /// Gets the current central output profile.
        /// </summary>
        public LogOutputMode OutputMode => _outputMode;

        /// <summary>
        /// Initializes a new instance of the <see cref="TKBase_LogManager"/> class.
        /// </summary>
        /// <param name="logBox">The logBox<see cref="RichTextBox?"/></param>
        /// <param name="logFilePath">The logFilePath<see cref="string?"/></param>
        /// <param name="outputMode">The outputMode<see cref="LogOutputMode"/></param>
        /// <param name="minimumLogLevel">The minimumLogLevel<see cref="LogLevel"/></param>
        /// <param name="debugLoggingEnabled">The debugLoggingEnabled<see cref="bool"/></param>
        public TKBase_LogManager(
            RichTextBox? logBox,
            string? logFilePath = null,
            LogOutputMode outputMode = LogOutputMode.Gui,
            LogLevel minimumLogLevel = LogLevel.Info,
            bool debugLoggingEnabled = false) {
            _logBox = logBox;
            _logFilePath = string.IsNullOrWhiteSpace(logFilePath)
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory)
                : logFilePath;
            _outputMode = outputMode;
            Configure(minimumLogLevel, debugLoggingEnabled);
        }

        /// <summary>
        /// Configures the logger output profile.
        /// </summary>
        /// <param name="outputMode">The outputMode<see cref="LogOutputMode"/></param>
        public void SetOutputMode(LogOutputMode outputMode) {
            _outputMode = outputMode;
        }

        /// <summary>
        /// Configures the logger level and Debug switch.
        /// </summary>
        /// <param name="minimumLogLevel">The minimumLogLevel<see cref="LogLevel"/></param>
        /// <param name="debugLoggingEnabled">The debugLoggingEnabled<see cref="bool"/></param>
        public void Configure(LogLevel minimumLogLevel, bool debugLoggingEnabled) {
            _minimumLogLevel = minimumLogLevel == LogLevel.Debug ? LogLevel.Info : minimumLogLevel;
            _debugLoggingEnabled = debugLoggingEnabled;
        }

        /// <summary>
        /// Configures the logger from database values.
        /// </summary>
        /// <param name="minimumLogLevel">The minimumLogLevel<see cref="string?"/></param>
        /// <param name="debugLoggingEnabled">The debugLoggingEnabled<see cref="bool"/></param>
        public void Configure(string? minimumLogLevel, bool debugLoggingEnabled) {
            Configure(ParseNormalLogLevel(minimumLogLevel), debugLoggingEnabled);
        }

        /// <summary>
        /// Loads LogLevel and DebugLoggingEnabled from TK_Properties through TKSQL_Commands.
        /// </summary>
        public void TryConfigureFromDatabase() {
            try {
                if (!TKSQL_ConnectionClass.TryEnsureDatabaseAvailable(this, "Logging-Einstellungen laden", false)) {
                    Warn($"Logging-Einstellungen nicht geladen | SQL-Datenbank nicht erreichbar | Fallback Level={_minimumLogLevel} | Debug={(_debugLoggingEnabled ? "aktiv" : "aus")}");
                    return;
                }

                TKSQL_Commands sql = new TKSQL_Commands(this);
                (string LogLevel, bool DebugLoggingEnabled) settings = sql.GetLoggingSettings();
                Configure(settings.LogLevel, settings.DebugLoggingEnabled);
                Info($"Logging-Einstellungen geladen | Level={settings.LogLevel} | Debug={(settings.DebugLoggingEnabled ? "aktiv" : "aus")}");
            } catch (Exception ex) {
                Warn($"Logging-Einstellungen konnten nicht aus TK_Properties geladen werden | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// Parses the configured normal log level.
        /// </summary>
        /// <param name="minimumLogLevel">The minimumLogLevel<see cref="string?"/></param>
        /// <returns>The <see cref="LogLevel"/></returns>
        public static LogLevel ParseNormalLogLevel(string? minimumLogLevel) {
            string value = (minimumLogLevel ?? string.Empty).Trim();

            if (string.Equals(value, "Error", StringComparison.OrdinalIgnoreCase)) {
                return LogLevel.Error;
            }

            if (string.Equals(value, "Warn", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Warning", StringComparison.OrdinalIgnoreCase)) {
                return LogLevel.Warn;
            }

            return LogLevel.Info;
        }

        /// <summary>
        /// The Clear
        /// </summary>
        public void Clear() {
            if (_logBox == null) return;

            try {
                if (_logBox.InvokeRequired) {
                    _logBox.BeginInvoke(new Action(Clear));
                    return;
                }

                _logBox.Clear();
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"LogMgmt.Clear Fehler: {ex.Message}");
            }
        }

        /// <summary>
        /// The Write
        /// </summary>
        /// <param name="message">The message<see cref="string"/></param>
        /// <param name="targets">The targets<see cref="LogTarget"/></param>
        public void Write(string message, LogTarget targets = LogTarget.None) {
            Info(message, targets);
        }

        /// <summary>
        /// The Info
        /// </summary>
        /// <param name="message">The message<see cref="string"/></param>
        /// <param name="targets">The targets<see cref="LogTarget"/></param>
        public void Info(string message, LogTarget targets = LogTarget.None) {
            Write(LogLevel.Info, message, targets);
        }

        /// <summary>
        /// The Warn
        /// </summary>
        /// <param name="message">The message<see cref="string"/></param>
        /// <param name="targets">The targets<see cref="LogTarget"/></param>
        public void Warn(string message, LogTarget targets = LogTarget.None) {
            Write(LogLevel.Warn, message, targets);
        }

        /// <summary>
        /// The Error
        /// </summary>
        /// <param name="message">The message<see cref="string"/></param>
        /// <param name="targets">The targets<see cref="LogTarget"/></param>
        public void Error(string message, LogTarget targets = LogTarget.None) {
            Write(LogLevel.Error, message, targets);
        }

        /// <summary>
        /// The Debug
        /// </summary>
        /// <param name="message">The message<see cref="string"/></param>
        /// <param name="targets">The targets<see cref="LogTarget"/></param>
        public void Debug(string message, LogTarget targets = LogTarget.None) {
            Write(LogLevel.Debug, message, targets);
        }

        /// <summary>
        /// The Write
        /// </summary>
        /// <param name="level">The level<see cref="LogLevel"/></param>
        /// <param name="message">The message<see cref="string"/></param>
        /// <param name="targets">The targets<see cref="LogTarget"/></param>
        public void Write(LogLevel level, string message, LogTarget targets = LogTarget.None) {
            if (!ShouldWrite(level)) {
                return;
            }

            LogTarget effectiveTargets = ResolveTargets(level, targets);
            if (effectiveTargets == LogTarget.None) {
                return;
            }

            string line = $"[{DateTime.Now:yyyy.MM.dd_HH:mm:ss}] [{level.ToString().ToUpperInvariant()}] {message}{Environment.NewLine}";
            WriteInternal(line, level, effectiveTargets);
        }

        /// <summary>
        /// The WriteRaw
        /// </summary>
        /// <param name="text">The text<see cref="string"/></param>
        /// <param name="targets">The targets<see cref="LogTarget"/></param>
        public void WriteRaw(string text, LogTarget targets = LogTarget.File) {
            WriteInternal(text, null, targets);
        }

        /// <summary>
        /// The WriteInfoEventLog
        /// </summary>
        /// <param name="message">The message<see cref="string"/></param>
        public static void WriteInfoEventLog(string message) {
            WriteEventLogStatic(LogLevel.Info, message);
        }

        /// <summary>
        /// The WriteWarnEventLog
        /// </summary>
        /// <param name="message">The message<see cref="string"/></param>
        public static void WriteWarnEventLog(string message) {
            WriteEventLogStatic(LogLevel.Warn, message);
        }

        /// <summary>
        /// The WriteErrorEventLog
        /// </summary>
        /// <param name="message">The message<see cref="string"/></param>
        public static void WriteErrorEventLog(string message) {
            WriteEventLogStatic(LogLevel.Error, message);
        }

        /// <summary>
        /// The RotateNow
        /// </summary>
        /// <param name="maxFileSizeBytes">The maxFileSizeBytes<see cref="long"/></param>
        /// <param name="maxArchiveFiles">The maxArchiveFiles<see cref="int"/></param>
        public void RotateNow(long maxFileSizeBytes, int maxArchiveFiles = 10) {
            lock (_fileLock) {
                RotateLogIfNeeded(maxFileSizeBytes, maxArchiveFiles);
            }
        }

        /// <summary>
        /// The RotateNow
        /// </summary>
        /// <param name="options">The options<see cref="LogRotationOptions?"/></param>
        public void RotateNow(LogRotationOptions? options) {
            if (options == null)
                return;

            lock (_fileLock) {
                RotateLogIfNeeded(options.MaxFileSizeBytes, options.MaxArchiveFiles);
            }
        }

        /// <summary>
        /// The WriteInternal
        /// </summary>
        /// <param name="text">The text<see cref="string"/></param>
        /// <param name="level">The level<see cref="LogLevel?"/></param>
        /// <param name="targets">The targets<see cref="LogTarget"/></param>
        private void WriteInternal(string text, LogLevel? level, LogTarget targets) {
            if (level == LogLevel.Debug) {
                targets &= ~LogTarget.EventLog;
            }

            if ((targets & LogTarget.Ui) == LogTarget.Ui) {
                WriteToUi(text, level);
            }

            if ((targets & LogTarget.File) == LogTarget.File) {
                WriteToFile(text);
            }

            if ((targets & LogTarget.EventLog) == LogTarget.EventLog && level.HasValue && level.Value != LogLevel.Debug) {
                WriteToEventLog(level.Value, text);
            }

            if (_outputMode == LogOutputMode.Service) {
                try {
                    ServiceLogLineEmitted?.Invoke(level, text);
                } catch {
                    // Log-Weiterleitung darf Logging und Servicebetrieb nie blockieren.
                }
            }
        }

        /// <summary>
        /// Determines whether the configured level allows the message.
        /// </summary>
        /// <param name="level">The level<see cref="LogLevel"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private bool ShouldWrite(LogLevel level) {
            if (level == LogLevel.Debug) {
                return _debugLoggingEnabled;
            }

            return GetPriority(level) >= GetPriority(_minimumLogLevel);
        }

        /// <summary>
        /// Resolves targets from explicit targets or the active output profile.
        /// </summary>
        /// <param name="level">The level<see cref="LogLevel"/></param>
        /// <param name="targets">The targets<see cref="LogTarget"/></param>
        /// <returns>The <see cref="LogTarget"/></returns>
        private LogTarget ResolveTargets(LogLevel level, LogTarget targets) {
            LogTarget resolved = targets == LogTarget.None ? GetDefaultTargets(level) : targets;

            if (_outputMode == LogOutputMode.Service) {
                resolved &= ~LogTarget.Ui;
            }

            if (level == LogLevel.Debug) {
                resolved &= ~LogTarget.EventLog;
            }

            return resolved;
        }

        /// <summary>
        /// Gets the default targets for the active output profile.
        /// </summary>
        /// <param name="level">The level<see cref="LogLevel"/></param>
        /// <returns>The <see cref="LogTarget"/></returns>
        private LogTarget GetDefaultTargets(LogLevel level) {
            bool guiMode = _outputMode == LogOutputMode.Gui;

            if (level == LogLevel.Debug) {
                return guiMode
                    ? LogTarget.File | LogTarget.Ui
                    : LogTarget.File;
            }

            return guiMode
                ? LogTarget.File | LogTarget.Ui | LogTarget.EventLog
                : LogTarget.File | LogTarget.EventLog;
        }

        /// <summary>
        /// Gets the severity priority for threshold filtering.
        /// </summary>
        /// <param name="level">The level<see cref="LogLevel"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int GetPriority(LogLevel level) {
            return level switch {
                LogLevel.Error => 3,
                LogLevel.Warn => 2,
                LogLevel.Info => 1,
                _ => 0
            };
        }

        /// <summary>
        /// The WriteToUi
        /// </summary>
        /// <param name="text">The text<see cref="string"/></param>
        /// <param name="level">The level<see cref="LogLevel?"/></param>
        private void WriteToUi(string text, LogLevel? level) {
            if (_logBox == null) return;

            try {
                if (_logBox.InvokeRequired) {
                    _logBox.BeginInvoke(new Action(() => WriteToUi(text, level)));
                    return;
                }

                if (UseColorsInUi && level.HasValue) {
                    int start = _logBox.TextLength;
                    _logBox.AppendText(text);
                    int length = _logBox.TextLength - start;

                    _logBox.Select(start, length);
                    _logBox.SelectionColor = level.Value switch {
                        LogLevel.Debug => Color.Gray,
                        LogLevel.Warn => Color.DarkOrange,
                        LogLevel.Error => Color.Red,
                        _ => _logBox.ForeColor
                    };
                    _logBox.Select(_logBox.TextLength, 0);
                    _logBox.SelectionColor = _logBox.ForeColor;
                } else {
                    _logBox.AppendText(text);
                }

                _logBox.SelectionStart = _logBox.TextLength;
                _logBox.ScrollToCaret();
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"LogMgmt.WriteToUi Fehler: {ex.Message}");
            }
        }

        /// <summary>
        /// The WriteToFile
        /// </summary>
        /// <param name="text">The text<see cref="string"/></param>
        private void WriteToFile(string text) {
            try {
                string? dir = Path.GetDirectoryName(_logFilePath);

                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir)) {
                    Directory.CreateDirectory(dir);
                }

                lock (_fileLock) {
                    File.AppendAllText(_logFilePath, text, Encoding.UTF8);
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"LogMgmt.WriteToFile Fehler: {ex.Message}");
            }
        }

        /// <summary>
        /// The RotateLogIfNeeded
        /// </summary>
        /// <param name="maxFileSizeBytes">The maxFileSizeBytes<see cref="long"/></param>
        /// <param name="maxArchiveFiles">The maxArchiveFiles<see cref="int"/></param>
        private void RotateLogIfNeeded(long maxFileSizeBytes, int maxArchiveFiles) {
            try {
                if (maxFileSizeBytes <= 0)
                    return;

                if (string.IsNullOrWhiteSpace(_logFilePath))
                    return;

                if (!File.Exists(_logFilePath))
                    return;

                FileInfo fi = new FileInfo(_logFilePath);

                if (fi.Length < maxFileSizeBytes)
                    return;

                string directory = fi.DirectoryName ?? AppDomain.CurrentDomain.BaseDirectory;
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fi.Name);
                string extension = fi.Extension;

                string archiveFile = Path.Combine(
                    directory,
                    $"{fileNameWithoutExtension}_{DateTime.Now:yyyyMMdd_HHmmss}{extension}"
                );

                if (File.Exists(archiveFile)) {
                    archiveFile = Path.Combine(
                        directory,
                        $"{fileNameWithoutExtension}_{DateTime.Now:yyyyMMdd_HHmmss_fff}{extension}"
                    );
                }

                File.Move(_logFilePath, archiveFile);

                if (maxArchiveFiles > 0) {
                    string searchPattern = $"{fileNameWithoutExtension}_*{extension}";

                    FileInfo[] archives = new DirectoryInfo(directory)
                        .GetFiles(searchPattern, SearchOption.TopDirectoryOnly)
                        .OrderByDescending(f => f.CreationTimeUtc)
                        .ToArray();

                    for (int i = maxArchiveFiles; i < archives.Length; i++) {
                        try {
                            archives[i].Delete();
                        } catch {
                            // absichtlich ignorieren
                        }
                    }
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"LogMgmt.RotateLogIfNeeded Fehler: {ex.Message}");
            }
        }

        /// <summary>
        /// The WriteToEventLog
        /// </summary>
        /// <param name="level">The level<see cref="LogLevel"/></param>
        /// <param name="text">The text<see cref="string"/></param>
        private void WriteToEventLog(LogLevel level, string text) {
            try {
                EnsureEventSource();
                EventLog.WriteEntry(EventSourceName, text.TrimEnd(), MapEventType(level));
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"LogMgmt.WriteToEventLog Fehler: {ex.Message}");
            }
        }

        /// <summary>
        /// The WriteEventLogStatic
        /// </summary>
        /// <param name="level">The level<see cref="LogLevel"/></param>
        /// <param name="message">The message<see cref="string"/></param>
        private static void WriteEventLogStatic(LogLevel level, string message) {
            try {
                EnsureEventSourceStatic();
                EventLog.WriteEntry(EventSourceName, message, MapEventType(level));
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"LogMgmt.WriteEventLogStatic Fehler: {ex.Message}");
            }
        }

        /// <summary>
        /// The EnsureEventSource
        /// </summary>
        private void EnsureEventSource() {
            if (EventLog.SourceExists(EventSourceName)) return;
            EventLog.CreateEventSource(EventSourceName, EventLogName);
        }

        /// <summary>
        /// The EnsureEventSourceStatic
        /// </summary>
        private static void EnsureEventSourceStatic() {
            if (EventLog.SourceExists(EventSourceName)) return;
            EventLog.CreateEventSource(EventSourceName, EventLogName);
        }

        /// <summary>
        /// The MapEventType
        /// </summary>
        /// <param name="level">The level<see cref="LogLevel"/></param>
        /// <returns>The <see cref="EventLogEntryType"/></returns>
        private static EventLogEntryType MapEventType(LogLevel level) {
            return level switch {
                LogLevel.Warn => EventLogEntryType.Warning,
                LogLevel.Error => EventLogEntryType.Error,
                _ => EventLogEntryType.Information
            };
        }
    }
}
