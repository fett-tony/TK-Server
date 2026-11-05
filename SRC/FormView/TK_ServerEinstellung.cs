namespace TKServer.FormView {
    using sipcryptlib; // Deine neue DLL einbinden
    using System;
    using System.Data;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Windows.Forms;
    using TKServer.Properties;

    using TKServer.BASE;
    using TKServer.SQL;
    /// <summary>
    /// Defines the <see cref="TK_ServerEinstellung" />
    /// </summary>
    public partial class TK_ServerEinstellung : Form {
        /// <summary>
        /// Gets or sets the Logger
        /// </summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public TKBase_LogManager? Logger { get; set; }

        /// <summary>
        /// Gets the SqlCommands
        /// </summary>
        private TKSQL_Commands SqlCommands => new(Logger);

        /// <summary>
        /// Defines the _tkproperties
        /// </summary>
        private readonly DataTable _tkproperties = new();

        /// <summary>
        /// Defines the _audioCodecs
        /// </summary>
        private readonly DataTable _audioCodecs = new();

        /// <summary>
        /// Defines the _audioCodecBinding
        /// </summary>
        private readonly BindingSource _audioCodecBinding = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="TK_ServerEinstellung"/> class.
        /// </summary>
        public TK_ServerEinstellung() {
            InitializeComponent();
            Codecs.Text = "SIPCodec ID";
            textBox9.ReadOnly = true;
            AudioCodecGrid.DataSource = _audioCodecBinding;
        }

        /// <summary>
        /// The TKServerEinstellung_Load
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void TKServerEinstellung_Load(object sender, EventArgs e) {
            try {
                Logger?.Info("TKServerEinstellung wird geladen");
                LoadTkProperties();
                BindTkProperties();
                LoadAudioCodecSelectionTab();
                SqlSettingLoad();
                RefreshFirewallStatus();
                Logger?.Info("TKServerEinstellung wurde erfolgreich geladen");
            } catch (Exception ex) {
                Logger?.Error($"Fehler beim Laden der TKServerEinstellung: {ex}");
                Logger?.Error($"Die Einstellungen konnten nicht vollständig geladen werden | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The LoadAudioCodecSelectionTab
        /// </summary>
        private void LoadAudioCodecSelectionTab() {
            try {
                _audioCodecs.Clear();
                _audioCodecs.Columns.Clear();

                DataTable table = SqlCommands.LoadAudioCodecSelectionTable();
                _audioCodecs.Merge(table);

                _audioCodecBinding.DataSource = _audioCodecs;
                SelectConfiguredAudioCodecRow();
                UpdateAudioCodecCurrentLabel();
                AudioCodecGrid_SelectionChanged(this, EventArgs.Empty);
                Logger?.Info($"TK_AudioCodec geladen | Datensaetze={_audioCodecs.Rows.Count}");
            } catch (Exception ex) {
                Logger?.Error($"TK_AudioCodec konnte nicht geladen werden: {ex}");
                if (AudioCodecCurrentLabel != null) AudioCodecCurrentLabel.Text = $"Codec-Liste konnte nicht geladen werden: {ex.Message}";
            }
        }

        /// <summary>
        /// The SelectConfiguredAudioCodecRow
        /// </summary>
        private void SelectConfiguredAudioCodecRow() {
            if (AudioCodecGrid == null || _tkproperties.Rows.Count == 0 || _audioCodecs.Rows.Count == 0) return;

            string configuredCodecId = Convert.ToString(_tkproperties.Rows[0]["SIPCodec"])?.Trim() ?? string.Empty;
            if (!int.TryParse(configuredCodecId, out int codecId)) return;

            foreach (DataGridViewRow gridRow in AudioCodecGrid.Rows) {
                if (gridRow.DataBoundItem is DataRowView view && Convert.ToInt32(view.Row["ID"]) == codecId) {
                    gridRow.Selected = true;
                    AudioCodecGrid.CurrentCell = gridRow.Cells[0];
                    break;
                }
            }
        }

        /// <summary>
        /// The UpdateAudioCodecCurrentLabel
        /// </summary>
        private void UpdateAudioCodecCurrentLabel() {
            if (AudioCodecCurrentLabel == null) return;
            AudioCodecCurrentLabel.Text = BuildCurrentCodecLabel();
        }

        /// <summary>
        /// The BuildCurrentCodecLabel
        /// </summary>
        /// <returns>The <see cref="string"/></returns>
        private string BuildCurrentCodecLabel() {
            if (_tkproperties.Rows.Count == 0) {
                return "Aktuelle Codecs: SIP=- | GSM=-";
            }

            string sip = ResolveCodecLabel("SIP", Convert.ToString(_tkproperties.Rows[0]["SIPCodec"])?.Trim() ?? string.Empty);
            string gsm = ResolveCodecLabel("GSM", Convert.ToString(_tkproperties.Rows[0]["GSMCodec"])?.Trim() ?? string.Empty);
            return $"Aktuelle Codecs: {sip} | {gsm}";
        }

        /// <summary>
        /// The ResolveCodecLabel
        /// </summary>
        /// <param name="label">The label<see cref="string"/></param>
        /// <param name="configuredCodecId">The configuredCodecId<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private string ResolveCodecLabel(string label, string configuredCodecId) {
            if (!int.TryParse(configuredCodecId, out int codecId)) {
                return $"{label}=-";
            }

            DataRow[] rows = _audioCodecs.Select($"ID = {codecId}");
            if (rows.Length == 0) {
                return $"{label}=ID {codecId} nicht sichtbar";
            }

            string codecKey = Convert.ToString(rows[0]["CodecKey"]) ?? string.Empty;
            return $"{label}=ID {codecId} {codecKey}";
        }

        /// <summary>
        /// The AudioCodecGrid_SelectionChanged
        /// </summary>
        /// <param name="sender">The sender<see cref="object?"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void AudioCodecGrid_SelectionChanged(object? sender, EventArgs e) {
            if (AudioCodecSelectedLabel == null || AudioCodecGrid == null) return;
            if (AudioCodecGrid.CurrentRow?.DataBoundItem is not DataRowView view) {
                AudioCodecSelectedLabel.Text = "Auswahl: -";
                return;
            }

            string id = Convert.ToString(view.Row["ID"]) ?? string.Empty;
            string codecKey = Convert.ToString(view.Row["CodecKey"]) ?? string.Empty;
            string displayName = Convert.ToString(view.Row["DisplayName"]) ?? string.Empty;
            AudioCodecSelectedLabel.Text = $"Auswahl: ID {id} | {codecKey} | {displayName}";
        }

        /// <summary>
        /// The IsSelectableSipAudioCodec
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool IsSelectableSipAudioCodec(DataRow row) {
            return Convert.ToBoolean(row["Active"])
                && Convert.ToBoolean(row["SupportsSip"])
                && Convert.ToBoolean(row["SupportsRtp"]);
        }

        /// <summary>
        /// The IsSelectableGsmBridgeCodec
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool IsSelectableGsmBridgeCodec(DataRow row) {
            return Convert.ToBoolean(row["Active"])
                && Convert.ToBoolean(row["SupportsGsmBridge"]);
        }

        /// <summary>
        /// The SaveSelectedSipCodec_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object?"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void SaveSelectedSipCodec_Click(object? sender, EventArgs e) {
            try {
                if (AudioCodecGrid?.CurrentRow?.DataBoundItem is not DataRowView view) {
                    Logger?.Error("SIPCodec speichern abgebrochen: keine Zeile ausgewaehlt");
                    return;
                }
                if (_tkproperties.Rows.Count == 0) {
                    Logger?.Error("SIPCodec speichern abgebrochen: TK_Properties nicht geladen");
                    return;
                }

                int codecId = Convert.ToInt32(view.Row["ID"]);
                if (!IsSelectableSipAudioCodec(view.Row)) {
                    Logger?.Error($"SIPCodec speichern abgebrochen: Codec ist nicht fuer SIP/RTP geeignet | AudioCodecID={codecId}");
                    return;
                }

                DataRow propertiesRow = _tkproperties.Rows[0];
                SqlCommands.UpdateTkPropertiesCodec(Convert.ToInt32(propertiesRow["Id"]), "SIPCodec", codecId);

                propertiesRow["SIPCodec"] = codecId;
                textBox9.Text = codecId.ToString();
                UpdateAudioCodecCurrentLabel();
                Logger?.Info($"TK_Properties.SIPCodec gespeichert | AudioCodecID={codecId}");
            } catch (Exception ex) {
                Logger?.Error($"SIPCodec-Auswahl konnte nicht gespeichert werden: {ex}");
            }
        }

        /// <summary>
        /// The SaveSelectedGsmCodec_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object?"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void SaveSelectedGsmCodec_Click(object? sender, EventArgs e) {
            try {
                if (AudioCodecGrid?.CurrentRow?.DataBoundItem is not DataRowView view) {
                    Logger?.Error("GSMCodec speichern abgebrochen: keine Zeile ausgewaehlt");
                    return;
                }
                if (_tkproperties.Rows.Count == 0) {
                    Logger?.Error("GSMCodec speichern abgebrochen: TK_Properties nicht geladen");
                    return;
                }

                int codecId = Convert.ToInt32(view.Row["ID"]);
                if (!IsSelectableGsmBridgeCodec(view.Row)) {
                    Logger?.Error($"GSMCodec speichern abgebrochen: Codec ist nicht fuer GSM-Bridge geeignet | AudioCodecID={codecId}");
                    return;
                }

                DataRow propertiesRow = _tkproperties.Rows[0];
                SqlCommands.UpdateTkPropertiesCodec(Convert.ToInt32(propertiesRow["Id"]), "GSMCodec", codecId);

                propertiesRow["GSMCodec"] = codecId;
                UpdateAudioCodecCurrentLabel();
                Logger?.Info($"TK_Properties.GSMCodec gespeichert | AudioCodecID={codecId}");
            } catch (Exception ex) {
                Logger?.Error($"GSMCodec-Auswahl konnte nicht gespeichert werden: {ex}");
            }
        }

        /// <summary>
        /// The LoadTkProperties
        /// </summary>
        private void LoadTkProperties() {
            try {
                Logger?.Info("Lade TK_Properties aus SQL");
                _tkproperties.Clear();
                _tkproperties.Columns.Clear();

                DataTable table = SqlCommands.LoadTkPropertiesTable();
                _tkproperties.Merge(table);

                Logger?.Info($"TK_Properties geladen | Datensaetze={_tkproperties.Rows.Count}");
            } catch (Exception ex) {
                Logger?.Error($"Fehler beim Laden von TK_Properties: {ex}");
                throw;
            }
        }

        /// <summary>
        /// The BindTkProperties
        /// </summary>
        private void BindTkProperties() {
            try {
                if (_tkproperties.Rows.Count == 0) {
                    Logger?.Info("BindTkProperties uebersprungen, da keine Datensaetze vorhanden sind");
                    return;
                }

                Logger?.Info("Binde TK_Properties an die UI");
                textBox1.DataBindings.Clear();
                textBox2.DataBindings.Clear();
                textBox3.DataBindings.Clear();
                textBox4.DataBindings.Clear();
                textBox5.DataBindings.Clear();
                textBox6.DataBindings.Clear();
                textBox20.DataBindings.Clear();
                textBox21.DataBindings.Clear();
                textBox7.DataBindings.Clear();
                textBox8.DataBindings.Clear();
                textBox16.DataBindings.Clear();
                textBox9.DataBindings.Clear();
                UseSRTP.DataBindings.Clear();
                textBox10.DataBindings.Clear();
                textBox17.DataBindings.Clear();
                textBox18.DataBindings.Clear();
                textBox19.DataBindings.Clear();
                AudioFoldersUseAdsProfileCheckBox.DataBindings.Clear();
                AudioFoldersRootPathTextBox.DataBindings.Clear();
                AudioFoldersFolderNameTextBox.DataBindings.Clear();

                textBox1.DataBindings.Add("Text", _tkproperties, "ServerIP", true, DataSourceUpdateMode.OnPropertyChanged);
                textBox2.DataBindings.Add("Text", _tkproperties, "Registrar", true, DataSourceUpdateMode.OnPropertyChanged);
                textBox20.DataBindings.Add("Text", _tkproperties, "Realm", true, DataSourceUpdateMode.OnPropertyChanged);
                textBox3.DataBindings.Add("Text", _tkproperties, "SipProxy", true, DataSourceUpdateMode.OnPropertyChanged);
                textBox21.DataBindings.Add("Text", _tkproperties, "OutboundProxy", true, DataSourceUpdateMode.OnPropertyChanged);
                textBox4.DataBindings.Add("Text", _tkproperties, "SipPort", true, DataSourceUpdateMode.OnPropertyChanged);
                textBox5.DataBindings.Add("Text", _tkproperties, "SipTransport", true, DataSourceUpdateMode.OnPropertyChanged);
                textBox6.DataBindings.Add("Text", _tkproperties, "StunServer", true, DataSourceUpdateMode.OnPropertyChanged);
                textBox7.DataBindings.Add("Text", _tkproperties, "RtpPortStart", true, DataSourceUpdateMode.OnPropertyChanged);
                textBox8.DataBindings.Add("Text", _tkproperties, "RtpPortEnd", true, DataSourceUpdateMode.OnPropertyChanged);
                textBox16.DataBindings.Add("Text", _tkproperties, "GSMAudioPort", true, DataSourceUpdateMode.OnPropertyChanged);
                textBox9.DataBindings.Add("Text", _tkproperties, "SIPCodec", true, DataSourceUpdateMode.OnPropertyChanged);
                UseSRTP.DataBindings.Add("Checked", _tkproperties, "UseSRTP", true, DataSourceUpdateMode.OnPropertyChanged);
                textBox10.DataBindings.Add("Text", _tkproperties, "RegisterExpire", true, DataSourceUpdateMode.OnPropertyChanged);
                textBox17.DataBindings.Add("Text", _tkproperties, "Country", true, DataSourceUpdateMode.OnPropertyChanged);
                textBox18.DataBindings.Add("Text", _tkproperties, "City", true, DataSourceUpdateMode.OnPropertyChanged);
                textBox19.DataBindings.Add("Text", _tkproperties, "PLZ", true, DataSourceUpdateMode.OnPropertyChanged);
                AudioFoldersUseAdsProfileCheckBox.DataBindings.Add("Checked", _tkproperties, "UseAdsProfileMediaPath", true, DataSourceUpdateMode.OnPropertyChanged);
                AudioFoldersRootPathTextBox.DataBindings.Add("Text", _tkproperties, "UserMediaRootPath", true, DataSourceUpdateMode.OnPropertyChanged);
                AudioFoldersFolderNameTextBox.DataBindings.Add("Text", _tkproperties, "UserMediaFolderName", true, DataSourceUpdateMode.OnPropertyChanged);

                RefreshCountrySettingsLabels();
                RefreshAudioFoldersInfoLabel();
                RefreshLoggingSettingsControls();
            } catch (Exception ex) {
                Logger?.Error($"TK_Properties konnten nicht gebunden werden: {ex}");
                Logger?.Error($"TK Server Einstellungen Fehler | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The button1_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void button1_Click(object sender, EventArgs e) {
            try {
                if (_tkproperties.Rows.Count == 0) {
                    Logger?.Error("Speichern von TK_Properties abgebrochen: kein Datensatz vorhanden");
                    Logger?.Warn("Es ist kein Datensatz in TK_Properties geladen.");
                    return;
                }

                DataRow row = _tkproperties.Rows[0];
                ResolveAndApplyCountryReferences(row);
                PushAudioFolderSettingsToDataRow(row);
                SqlCommands.UpdateTkProperties(row);

                RefreshCountrySettingsLabels();
                Logger?.Info("TK_Properties gespeichert");
            } catch (Exception ex) {
                Logger?.Error($"TK_Properties konnten nicht gespeichert werden: {ex}");
                Logger?.Error($"TK Server Einstellungen Fehler | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The SaveAudioFolders_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object?"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void SaveAudioFolders_Click(object? sender, EventArgs e) {
            try {
                SaveAudioFolderSettingsToDatabase();
                Logger?.Info("Audioordner-Einstellungen wurden gespeichert.");
            } catch (Exception ex) {
                Logger?.Error($"Audioordner-Einstellungen konnten nicht gespeichert werden: {ex}");
                Logger?.Error($"Audioordner Einstellungen Fehler | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The SaveAudioFolderSettingsToDatabase
        /// </summary>
        private void SaveAudioFolderSettingsToDatabase() {
            if (_tkproperties.Rows.Count == 0) {
                Logger?.Error("Audioordner speichern abgebrochen: kein TK_Properties Datensatz geladen");
                throw new InvalidOperationException("Es ist kein Datensatz in TK_Properties geladen.");
            }

            DataRow row = _tkproperties.Rows[0];
            PushAudioFolderSettingsToDataRow(row);

            int propertiesId = Convert.ToInt32(row["Id"]);
            bool useAdsProfileMediaPath = row.Table.Columns.Contains("UseAdsProfileMediaPath")
                && row["UseAdsProfileMediaPath"] != DBNull.Value
                && Convert.ToBoolean(row["UseAdsProfileMediaPath"]);
            string? userMediaRootPath = Convert.ToString(row["UserMediaRootPath"])?.Trim();
            string userMediaFolderName = Convert.ToString(row["UserMediaFolderName"])?.Trim() ?? "User";

            SqlCommands.UpdateTkPropertiesAudioFolderSettings(propertiesId, useAdsProfileMediaPath, userMediaRootPath, userMediaFolderName);
            RefreshAudioFoldersInfoLabel();
            Logger?.Info($"Audioordner gespeichert | UseAdsProfileMediaPath={useAdsProfileMediaPath} | UserMediaRootPath={(string.IsNullOrWhiteSpace(userMediaRootPath) ? "-" : userMediaRootPath)} | UserMediaFolderName={userMediaFolderName}");
        }

        /// <summary>
        /// The PushAudioFolderSettingsToDataRow
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        private void PushAudioFolderSettingsToDataRow(DataRow row) {
            string folderName = AudioFoldersFolderNameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(folderName)) {
                folderName = "User";
                AudioFoldersFolderNameTextBox.Text = folderName;
            }

            ValidateMediaFolderName(folderName);

            SetRowValue(row, "UseAdsProfileMediaPath", AudioFoldersUseAdsProfileCheckBox.Checked);
            SetRowValue(row, "UserMediaRootPath", string.IsNullOrWhiteSpace(AudioFoldersRootPathTextBox.Text) ? null : AudioFoldersRootPathTextBox.Text.Trim());
            SetRowValue(row, "UserMediaFolderName", folderName);
        }

        /// <summary>
        /// The AudioFoldersValueChanged
        /// </summary>
        /// <param name="sender">The sender<see cref="object?"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void AudioFoldersValueChanged(object? sender, EventArgs e) {
            RefreshAudioFoldersInfoLabel();
        }

        /// <summary>
        /// The RefreshAudioFoldersInfoLabel
        /// </summary>
        private void RefreshAudioFoldersInfoLabel() {
            string root = string.IsNullOrWhiteSpace(AudioFoldersRootPathTextBox.Text)
                ? @"<EXE>\Media"
                : AudioFoldersRootPathTextBox.Text.Trim();
            string folderName = string.IsNullOrWhiteSpace(AudioFoldersFolderNameTextBox.Text)
                ? "User"
                : AudioFoldersFolderNameTextBox.Text.Trim();
            string mode = AudioFoldersUseAdsProfileCheckBox.Checked
                ? "ADS-Profilpfad aktiv: <AdsProfilePath>\\" + folderName
                : "Serverpfad aktiv: " + root + @"\" + folderName + @"\<Username>";

            AudioFoldersInfoLabel.Text = mode;
        }

        /// <summary>
        /// Aktualisiert die Logging-Einstellungen im Reiter Logging.
        /// </summary>
        private void RefreshLoggingSettingsControls() {
            if (LoggingLevelComboBox == null || LoggingDebugCheckBox == null || LoggingSaveButton == null || LoggingInfoLabel == null) {
                return;
            }

            if (_tkproperties.Rows.Count == 0) {
                LoggingLevelComboBox.SelectedItem = "Info";
                LoggingDebugCheckBox.Checked = false;
                LoggingLevelComboBox.Enabled = false;
                LoggingDebugCheckBox.Enabled = false;
                LoggingSaveButton.Enabled = false;
                LoggingInfoLabel.Text = "Logging-Einstellungen konnten nicht geladen werden: kein TK_Properties Datensatz.";
                return;
            }

            DataRow row = _tkproperties.Rows[0];
            bool schemaReady = row.Table.Columns.Contains("LogLevel")
                && row.Table.Columns.Contains("DebugLoggingEnabled");

            LoggingLevelComboBox.Enabled = schemaReady;
            LoggingDebugCheckBox.Enabled = schemaReady;
            LoggingSaveButton.Enabled = schemaReady;

            if (!schemaReady) {
                LoggingLevelComboBox.SelectedItem = "Info";
                LoggingDebugCheckBox.Checked = false;
                LoggingInfoLabel.Text = "Logging-Spalten fehlen in TK_Properties. Bitte zuerst TK_Properties_LoggingSettings_AddColumns_V0001.sql ausfuehren.";
                Logger?.Warn("Logging-Einstellungen nicht verfügbar: LogLevel oder DebugLoggingEnabled fehlt in TK_Properties.");
                return;
            }

            string logLevel = NormalizeLogLevel(Convert.ToString(row["LogLevel"]));
            bool debugLoggingEnabled = row["DebugLoggingEnabled"] != DBNull.Value
                && Convert.ToBoolean(row["DebugLoggingEnabled"]);

            LoggingLevelComboBox.SelectedItem = logLevel;
            LoggingDebugCheckBox.Checked = debugLoggingEnabled;
            LoggingInfoLabel.Text = BuildLoggingInfoText(logLevel, debugLoggingEnabled);
        }

        /// <summary>
        /// Speichert die Logging-Einstellungen aus dem Reiter Logging.
        /// </summary>
        /// <param name="sender">The sender<see cref="object?"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void SaveLoggingSettings_Click(object? sender, EventArgs e) {
            try {
                SaveLoggingSettingsToDatabase();
                Logger?.Info("Logging-Einstellungen wurden gespeichert.");
            } catch (Exception ex) {
                Logger?.Error($"Logging-Einstellungen konnten nicht gespeichert werden: {ex}");
                Logger?.Error($"Logging Einstellungen Fehler | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// Speichert LogLevel und DebugLoggingEnabled in TK_Properties.
        /// </summary>
        private void SaveLoggingSettingsToDatabase() {
            if (_tkproperties.Rows.Count == 0) {
                throw new InvalidOperationException("Es ist kein Datensatz in TK_Properties geladen.");
            }

            DataRow row = _tkproperties.Rows[0];
            if (!row.Table.Columns.Contains("LogLevel") || !row.Table.Columns.Contains("DebugLoggingEnabled")) {
                throw new InvalidOperationException("Logging-Spalten fehlen in dbo.TK_Properties. Bitte zuerst die SQL-Datei TK_Properties_LoggingSettings_AddColumns_V0001.sql ausfuehren.");
            }

            int propertiesId = Convert.ToInt32(row["Id"]);
            string logLevel = NormalizeLogLevel(Convert.ToString(LoggingLevelComboBox.SelectedItem ?? LoggingLevelComboBox.Text));
            bool debugLoggingEnabled = LoggingDebugCheckBox.Checked;

            SqlCommands.UpdateTkPropertiesLoggingSettings(propertiesId, logLevel, debugLoggingEnabled);

            SetRowValue(row, "LogLevel", logLevel);
            SetRowValue(row, "DebugLoggingEnabled", debugLoggingEnabled);
            Logger?.Configure(logLevel, debugLoggingEnabled);
            RefreshLoggingSettingsControls();
        }

        /// <summary>
        /// Normalisiert den LogLevel auf die erlaubten Werte Error, Warn oder Info.
        /// </summary>
        /// <param name="value">The value<see cref="string?"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string NormalizeLogLevel(string? value) {
            string text = (value ?? string.Empty).Trim();
            return text.Equals("Error", StringComparison.OrdinalIgnoreCase)
                ? "Error"
                : text.Equals("Warn", StringComparison.OrdinalIgnoreCase)
                    ? "Warn"
                    : "Info";
        }

        /// <summary>
        /// Erstellt den Infotext fuer den Logging-Reiter.
        /// </summary>
        /// <param name="logLevel">The logLevel<see cref="string"/></param>
        /// <param name="debugLoggingEnabled">The debugLoggingEnabled<see cref="bool"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string BuildLoggingInfoText(string logLevel, bool debugLoggingEnabled) {
            string normal = logLevel switch {
                "Error" => "Normales Logging: nur Error",
                "Warn" => "Normales Logging: Warn + Error",
                _ => "Normales Logging: Info + Warn + Error"
            };

            string debug = debugLoggingEnabled
                ? "Debug ist aktiv: Raw SIP/GSM/AT und technische Details werden zusätzlich geloggt."
                : "Debug ist aus: Raw SIP/GSM/AT werden nicht zusätzlich im Log ausgegeben.";

            return normal + Environment.NewLine + debug;
        }

        /// <summary>
        /// The ValidateMediaFolderName
        /// </summary>
        /// <param name="folderName">The folderName<see cref="string"/></param>
        private static void ValidateMediaFolderName(string folderName) {
            if (string.IsNullOrWhiteSpace(folderName)) {
                throw new InvalidOperationException("UserMediaFolderName darf nicht leer sein.");
            }

            if (folderName.Contains(Path.DirectorySeparatorChar) || folderName.Contains(Path.AltDirectorySeparatorChar)) {
                throw new InvalidOperationException("UserMediaFolderName darf kein Pfad sein, sondern nur ein Ordnername.");
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in folderName) {
                if (Array.IndexOf(invalidChars, c) >= 0) {
                    throw new InvalidOperationException($"UserMediaFolderName enthält ungültige Zeichen: {folderName}");
                }
            }
        }

        /// <summary>
        /// The SaveCountrySettings_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object?"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void SaveCountrySettings_Click(object? sender, EventArgs e) {
            try {
                SaveCountrySettingsToDatabase();
                Logger?.Info("Ländereinstellungen wurden gespeichert.");
            } catch (Exception ex) {
                Logger?.Error($"Ländereinstellungen konnten nicht gespeichert werden: {ex}");
                Logger?.Error($"TK Server Einstellungen Fehler | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The SaveCountrySettingsToDatabase
        /// </summary>
        private void SaveCountrySettingsToDatabase() {
            if (_tkproperties.Rows.Count == 0) {
                Logger?.Error("Ländereinstellungen speichern abgebrochen: kein TK_Properties Datensatz geladen");
                throw new InvalidOperationException("Es ist kein Datensatz in TK_Properties geladen.");
            }

            DataRow row = _tkproperties.Rows[0];
            PushCountryTextBoxesToDataRow(row);
            ResolveAndApplyCountryReferences(row);

            int propertiesId = Convert.ToInt32(row["Id"]);
            int? plz = DbNullableInt(row, "PLZ");
            string city = Convert.ToString(row["City"])?.Trim() ?? string.Empty;
            string country = Convert.ToString(row["Country"])?.Trim() ?? string.Empty;
            int? countryId = DbNullableInt(row, "CountryID");
            int? areaId = DbNullableInt(row, "AreaID");

            SqlCommands.UpdateTkPropertiesCountrySettings(propertiesId, plz, city, country, countryId, areaId);

            RefreshCountrySettingsLabels();
            Logger?.Info($"Ländereinstellungen gespeichert | PLZ={textBox19.Text.Trim()} | City={textBox18.Text.Trim()} | Country={textBox17.Text.Trim()} | CountryID={FormatNullableId(row, "CountryID")} | AreaID={FormatNullableId(row, "AreaID")}");
        }

        /// <summary>
        /// The PushCountryTextBoxesToDataRow
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        private void PushCountryTextBoxesToDataRow(DataRow row) {
            SetRowValue(row, "Country", textBox17.Text.Trim());
            SetRowValue(row, "City", textBox18.Text.Trim());
            SetRowValue(row, "PLZ", ParseNullableInt(textBox19.Text, "PLZ"));
        }

        /// <summary>
        /// The ResolveAndApplyCountryReferences
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        private void ResolveAndApplyCountryReferences(DataRow row) {
            PushCountryTextBoxesToDataRow(row);

            CountryPrefixLookup? country = ResolveCountryPrefix(Convert.ToString(row["Country"]) ?? string.Empty);
            AreaCodeLookup? area = ResolveAreaCode(Convert.ToString(row["City"]) ?? string.Empty, country?.Iso2);

            SetRowValue(row, "CountryID", country?.Id);
            SetRowValue(row, "AreaID", area?.Id);

            if (country == null && !string.IsNullOrWhiteSpace(Convert.ToString(row["Country"]))) {
                Logger?.Error($"Kein CountryID-Treffer in TK_CountryPrefix gefunden | Country={row["Country"]}");
            }

            if (area == null && !string.IsNullOrWhiteSpace(Convert.ToString(row["City"]))) {
                Logger?.Error($"Kein AreaID-Treffer in TK_DEAreaCode gefunden | City={row["City"]}");
            }
        }

        /// <summary>
        /// The ResolveCountryPrefix
        /// </summary>
        /// <param name="countryInput">The countryInput<see cref="string"/></param>
        /// <returns>The <see cref="CountryPrefixLookup?"/></returns>
        private CountryPrefixLookup? ResolveCountryPrefix(string countryInput) {
            DataRow? row = SqlCommands.ResolveCountryPrefix(countryInput);
            return CountryPrefixLookup.FromDataRow(row);
        }

        /// <summary>
        /// The ResolveAreaCode
        /// </summary>
        /// <param name="cityInput">The cityInput<see cref="string"/></param>
        /// <param name="countryIso2">The countryIso2<see cref="string?"/></param>
        /// <returns>The <see cref="AreaCodeLookup?"/></returns>
        private AreaCodeLookup? ResolveAreaCode(string cityInput, string? countryIso2) {
            DataRow? row = SqlCommands.ResolveDeAreaCode(cityInput, countryIso2);
            return AreaCodeLookup.FromDataRow(row);
        }

        /// <summary>
        /// The RefreshCountrySettingsLabels
        /// </summary>
        private void RefreshCountrySettingsLabels() {
            try {
                if (_tkproperties.Rows.Count == 0) {
                    SetCountrySettingsLabels(null, null);
                    return;
                }

                DataRow row = _tkproperties.Rows[0];
                CountryPrefixLookup? country = LoadCountryPrefixById(DbNullableInt(row, "CountryID"));
                AreaCodeLookup? area = LoadAreaCodeById(DbNullableInt(row, "AreaID"));
                SetCountrySettingsLabels(country, area);
            } catch (Exception ex) {
                Logger?.Error($"Ländereinstellungs-Labels konnten nicht aktualisiert werden: {ex}");
                CountryPrefixValueLabel.Text = "Fehler";
                AreaPrefixValueLabel.Text = "Fehler";
            }
        }

        /// <summary>
        /// The LoadCountryPrefixById
        /// </summary>
        /// <param name="countryId">The countryId<see cref="int?"/></param>
        /// <returns>The <see cref="CountryPrefixLookup?"/></returns>
        private CountryPrefixLookup? LoadCountryPrefixById(int? countryId) {
            if (!countryId.HasValue) return null;

            DataRow? row = SqlCommands.GetCountryPrefixById(countryId.Value);
            return CountryPrefixLookup.FromDataRow(row);
        }

        /// <summary>
        /// The LoadAreaCodeById
        /// </summary>
        /// <param name="areaId">The areaId<see cref="int?"/></param>
        /// <returns>The <see cref="AreaCodeLookup?"/></returns>
        private AreaCodeLookup? LoadAreaCodeById(int? areaId) {
            if (!areaId.HasValue) return null;

            DataRow? row = SqlCommands.GetDeAreaCodeById(areaId.Value);
            return AreaCodeLookup.FromDataRow(row);
        }

        /// <summary>
        /// The SetCountrySettingsLabels
        /// </summary>
        /// <param name="country">The country<see cref="CountryPrefixLookup?"/></param>
        /// <param name="area">The area<see cref="AreaCodeLookup?"/></param>
        private void SetCountrySettingsLabels(CountryPrefixLookup? country, AreaCodeLookup? area) {
            CountryPrefixValueLabel.Text = country == null
                ? "-"
                : $"{country.PrefixDisplay} ({country.CountryName})";

            AreaPrefixValueLabel.Text = area == null
                ? "-"
                : $"{area.NationalPrefixDisplay} / {area.InternationalPrefixDisplay} ({area.AreaName})";

            CountryReferenceValueLabel.Text = country == null
                ? "CountryID: -"
                : $"CountryID: {country.Id}";

            AreaReferenceValueLabel.Text = area == null
                ? "AreaID: -"
                : $"AreaID: {area.Id}";
        }

        /// <summary>
        /// The SetRowValue
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="column">The column<see cref="string"/></param>
        /// <param name="value">The value<see cref="object?"/></param>
        private static void SetRowValue(DataRow row, string column, object? value) {
            if (!row.Table.Columns.Contains(column)) return;
            row[column] = value ?? DBNull.Value;
        }

        /// <summary>
        /// The ParseNullableInt
        /// </summary>
        /// <param name="input">The input<see cref="string"/></param>
        /// <param name="fieldName">The fieldName<see cref="string"/></param>
        /// <returns>The <see cref="int?"/></returns>
        private static int? ParseNullableInt(string input, string fieldName) {
            string value = (input ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value)) return null;
            if (int.TryParse(value, out int result)) return result;
            throw new InvalidOperationException($"{fieldName} ist ungültig.");
        }

        /// <summary>
        /// The DbNullableInt
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="column">The column<see cref="string"/></param>
        /// <returns>The <see cref="int?"/></returns>
        private static int? DbNullableInt(DataRow row, string column) {
            if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value) return null;
            string value = Convert.ToString(row[column])?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(value)) return null;
            return Convert.ToInt32(value);
        }

        /// <summary>
        /// The FormatNullableId
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="column">The column<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string FormatNullableId(DataRow row, string column) {
            int? id = DbNullableInt(row, column);
            return id.HasValue ? id.Value.ToString() : "-";
        }

        /// <summary>
        /// The OnlyDigits
        /// </summary>
        /// <param name="input">The input<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string OnlyDigits(string input) {
            StringBuilder builder = new();
            foreach (char c in input) {
                if (char.IsDigit(c)) builder.Append(c);
            }
            return builder.ToString();
        }

        private sealed record CountryPrefixLookup(int Id, string Iso2, string CountryName, string PrefixDisplay, string PrefixDigits) {
            public static CountryPrefixLookup? FromDataRow(DataRow? row) {
                if (row == null) return null;
                return new CountryPrefixLookup(
                    Convert.ToInt32(row["ID"]),
                    row["ISO2"] == DBNull.Value ? string.Empty : Convert.ToString(row["ISO2"]) ?? string.Empty,
                    row["CountryName"] == DBNull.Value ? string.Empty : Convert.ToString(row["CountryName"]) ?? string.Empty,
                    row["PrefixDisplay"] == DBNull.Value ? string.Empty : Convert.ToString(row["PrefixDisplay"]) ?? string.Empty,
                    row["PrefixDigits"] == DBNull.Value ? string.Empty : Convert.ToString(row["PrefixDigits"]) ?? string.Empty);
            }
        }

        private sealed record AreaCodeLookup(int Id, string AreaName, string AreaCodeDigits, string NationalPrefixDisplay, string InternationalPrefixDisplay) {
            public static AreaCodeLookup? FromDataRow(DataRow? row) {
                if (row == null) return null;
                return new AreaCodeLookup(
                    Convert.ToInt32(row["ID"]),
                    row["AreaName"] == DBNull.Value ? string.Empty : Convert.ToString(row["AreaName"]) ?? string.Empty,
                    row["AreaCodeDigits"] == DBNull.Value ? string.Empty : Convert.ToString(row["AreaCodeDigits"]) ?? string.Empty,
                    row["NationalPrefixDisplay"] == DBNull.Value ? string.Empty : Convert.ToString(row["NationalPrefixDisplay"]) ?? string.Empty,
                    row["InternationalPrefixDisplay"] == DBNull.Value ? string.Empty : Convert.ToString(row["InternationalPrefixDisplay"]) ?? string.Empty);
            }
        }
        /// <summary>
        /// The SqlSettingLoad
        /// </summary>
        private void SqlSettingLoad() {
            try {
                TKSQL_ConnectionClass.SqlConnectionSettings settings = TKSQL_ConnectionClass.LoadSqlConnectionSettings();
                string encryptedPass = settings.Password;

                textBox11.Text = settings.Server;
                textBox12.Text = settings.Database;
                textBox13.Text = settings.UserID;
                textBox14.Text = SipCryptor.Decrypt(encryptedPass);
                textBox15.Text = settings.Port.ToString();

                checkBox1.Checked = settings.IntegratedSecurity;
                checkBox2.Checked = settings.Encrypt;
                checkBox3.Checked = settings.TrustServerCertificate;

                comboBox1.Text = settings.ConBuilder;

                Logger?.Info($"SQL Einstellungen in die UI geladen | Quelle={TKSQL_ConnectionClass.GetSqlConnectionSettingsPath()}");
            } catch (Exception ex) {
                Logger?.Error($"Fehler beim Laden der SQL Einstellungen: {ex}");
                throw;
            }
        }

        /// <summary>
        /// The button3_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void button3_Click(object sender, EventArgs e) {
            try {
                string encryptedPass = textBox14.Text;
                TKSQL_ConnectionClass.SqlConnectionSettings settings = new TKSQL_ConnectionClass.SqlConnectionSettings {
                    Server = textBox11.Text.Trim(),
                    Database = textBox12.Text.Trim(),
                    UserID = textBox13.Text.Trim(),
                    Password = SipCryptor.Encrypt(encryptedPass),
                    Port = GetIntValue(textBox15, "SQL Port"),
                    IntegratedSecurity = checkBox1.Checked,
                    Encrypt = checkBox2.Checked,
                    TrustServerCertificate = checkBox3.Checked,
                    ConBuilder = comboBox1.Text.Trim()
                };

                TKSQL_ConnectionClass.SaveSqlConnectionSettings(settings);

                Logger?.Info($"SQL Einstellungen zentral gespeichert | Datei={TKSQL_ConnectionClass.GetSqlConnectionSettingsPath()}");
                Logger?.Info("SQL Einstellungen wurden gespeichert und gelten fuer GUI und Service.");

                try {
                    TKBase_GUIRunTime.SendServiceCommand("ReloadSettings", Logger);
                } catch (Exception reloadEx) {
                    Logger?.Warn($"SQL Einstellungen gespeichert, aber Service-Reload konnte nicht gesendet werden | Fehler={reloadEx.Message}");
                }
            } catch (Exception ex) {
                Logger?.Error($"Fehler beim Speichern der SQL Einstellungen: {ex}");
                Logger?.Error($"SQL DB Einstellung Fehler | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The button2_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object?"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void button2_Click(object? sender, EventArgs e) {
            try {
                string sipTransport = GetSipTransport();
                int sipPort = GetIntValue(textBox4, "SIP Port");
                int rtpStart = GetIntValue(textBox7, "RTP Start");
                int rtpEnd = GetIntValue(textBox8, "RTP Ende");
                ValidatePortRange(rtpStart, rtpEnd);

                string sipRuleName = GetSipRuleName(sipTransport, sipPort);
                string rtpRuleName = GetRtpRuleName(rtpStart, rtpEnd);

                Logger?.Info($"Firewall setzen gestartet | SIP={sipRuleName} | RTP={rtpRuleName}");

                FirewallCommandResult sipResult = ExecuteElevatedNetsh(
                    $"advfirewall firewall add rule name=\"{sipRuleName}\" dir=in action=allow protocol={sipTransport} localport={sipPort}",
                    $"Setze SIP Firewall-Regel {sipRuleName}");

                FirewallCommandResult rtpResult = ExecuteElevatedNetsh(
                    $"advfirewall firewall add rule name=\"{rtpRuleName}\" dir=in action=allow protocol=UDP localport={rtpStart}-{rtpEnd}",
                    $"Setze RTP Firewall-Regel {rtpRuleName}");

                RefreshFirewallStatus();

                bool allOk = sipResult.Success && rtpResult.Success;
                Logger?.Info($"Firewall setzen beendet | SIP={sipResult.Success} | RTP={rtpResult.Success}");

                if (allOk) {
                    Logger?.Info("Firewall-Regeln wurden gesetzt.");
                } else {
                    Logger?.Warn(BuildFirewallResultMessage("Mindestens eine Firewall-Regel konnte nicht gesetzt werden.", sipResult, rtpResult));
                }
            } catch (Exception ex) {
                Logger?.Error($"Fehler beim Setzen der Firewall-Regeln: {ex}");
                Logger?.Error($"Firewall Fehler | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The RefreshFirewallStatus
        /// </summary>
        private void RefreshFirewallStatus() {
            try {
                listView1.BeginUpdate();
                listView1.Items.Clear();

                string sipTransport = GetSipTransport();
                int sipPort = GetIntValue(textBox4, "SIP Port");
                int rtpStart = GetIntValue(textBox7, "RTP Start");
                int rtpEnd = GetIntValue(textBox8, "RTP Ende");
                ValidatePortRange(rtpStart, rtpEnd);

                string sipRuleName = GetSipRuleName(sipTransport, sipPort);
                string rtpRuleName = GetRtpRuleName(rtpStart, rtpEnd);

                Logger?.Info($"Firewall-Status aktualisieren | SIP={sipRuleName} | RTP={rtpRuleName}");

                AddRuleStatus(sipRuleName);
                AddRuleStatus(rtpRuleName);
            } catch (Exception ex) {
                Logger?.Error($"Fehler beim Aktualisieren des Firewall-Status: {ex}");
                listView1.Items.Clear();
                ListViewItem errorItem = new("Konfiguration");
                errorItem.SubItems.Add("Fehler");
                errorItem.SubItems.Add(ex.Message);
                listView1.Items.Add(errorItem);
            } finally {
                listView1.EndUpdate();
            }
        }

        /// <summary>
        /// The AddRuleStatus
        /// </summary>
        /// <param name="ruleName">The ruleName<see cref="string"/></param>
        private void AddRuleStatus(string ruleName) {
            FirewallCommandResult statusResult = QueryFirewallRule(ruleName);

            ListViewItem item = new(ruleName);
            item.SubItems.Add(statusResult.Success ? "Vorhanden" : "Fehlt");
            item.SubItems.Add(statusResult.Message);
            listView1.Items.Add(item);

            Logger?.Info($"Firewall-Status | Regel={ruleName} | Vorhanden={statusResult.Success} | Detail={statusResult.Message}");
        }

        /// <summary>
        /// The GetSipRuleName
        /// </summary>
        /// <param name="sipTransport">The sipTransport<see cref="string"/></param>
        /// <param name="sipPort">The sipPort<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string GetSipRuleName(string sipTransport, int sipPort) => $"TK-Server_SIP_{sipTransport}_{sipPort}";

        /// <summary>
        /// The GetRtpRuleName
        /// </summary>
        /// <param name="rtpStart">The rtpStart<see cref="int"/></param>
        /// <param name="rtpEnd">The rtpEnd<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string GetRtpRuleName(int rtpStart, int rtpEnd) => $"TK-Server_RTP_UDP_{rtpStart}_{rtpEnd}";

        /// <summary>
        /// The GetIntValue
        /// </summary>
        /// <param name="textBox">The textBox<see cref="TextBox"/></param>
        /// <param name="fieldName">The fieldName<see cref="string"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int GetIntValue(TextBox textBox, string fieldName) {
            if (!int.TryParse(textBox.Text, out int value)) {
                throw new Exception($"{fieldName} ist ungültig.");
            }
            return value;
        }

        /// <summary>
        /// The ValidatePortRange
        /// </summary>
        /// <param name="rtpStart">The rtpStart<see cref="int"/></param>
        /// <param name="rtpEnd">The rtpEnd<see cref="int"/></param>
        private static void ValidatePortRange(int rtpStart, int rtpEnd) {
            if (rtpStart <= 0 || rtpEnd <= 0) {
                throw new Exception("RTP Ports müssen größer als 0 sein.");
            }

            if (rtpStart > rtpEnd) {
                throw new Exception("RTP Start darf nicht größer als RTP Ende sein.");
            }
        }

        /// <summary>
        /// The GetSipTransport
        /// </summary>
        /// <returns>The <see cref="string"/></returns>
        private string GetSipTransport() {
            string sipTransport = (textBox5.Text ?? string.Empty).Trim().ToUpperInvariant();
            if (sipTransport != "UDP" && sipTransport != "TCP" && sipTransport != "TLS" && sipTransport != "UDP+TCP") {
                throw new InvalidOperationException($"Ungültiger SIP Transport: {textBox5.Text}");
            }
            return sipTransport;
        }

        /// <summary>
        /// The QueryFirewallRule
        /// </summary>
        /// <param name="ruleName">The ruleName<see cref="string"/></param>
        /// <returns>The <see cref="FirewallCommandResult"/></returns>
        private FirewallCommandResult QueryFirewallRule(string ruleName) {
            try {
                using Process process = new();
                process.StartInfo = new ProcessStartInfo {
                    FileName = "netsh",
                    Arguments = $"advfirewall firewall show rule name=\"{ruleName}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                string combined = CombineProcessMessage(output, error);

                if (output.Contains("Keine Regeln entsprechen", StringComparison.OrdinalIgnoreCase) ||
                    output.Contains("No rules match", StringComparison.OrdinalIgnoreCase)) {
                    return new FirewallCommandResult(false, "Regel nicht vorhanden", process.ExitCode, output, error);
                }

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output)) {
                    return new FirewallCommandResult(true, "Regel vorhanden", process.ExitCode, output, error);
                }

                return new FirewallCommandResult(false, string.IsNullOrWhiteSpace(combined) ? "Status konnte nicht ermittelt werden" : combined, process.ExitCode, output, error);
            } catch (Exception ex) {
                Logger?.Error($"Fehler beim Prüfen der Firewall-Regel {ruleName}: {ex}");
                return new FirewallCommandResult(false, ex.Message, -1, string.Empty, ex.ToString());
            }
        }

        /// <summary>
        /// The ExecuteElevatedNetsh
        /// </summary>
        /// <param name="arguments">The arguments<see cref="string"/></param>
        /// <param name="operation">The operation<see cref="string"/></param>
        /// <returns>The <see cref="FirewallCommandResult"/></returns>
        private FirewallCommandResult ExecuteElevatedNetsh(string arguments, string operation) {
            try {
                using Process process = new();
                process.StartInfo = new ProcessStartInfo {
                    FileName = "netsh",
                    Arguments = arguments,
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Logger?.Info($"Firewall Kommando gestartet | Operation={operation} | Args={arguments}");
                process.Start();
                process.WaitForExit();

                bool success = process.ExitCode == 0;
                string message = success ? "OK" : $"ExitCode={process.ExitCode}";

                if (!success) {
                    Logger?.Error($"Firewall Kommando fehlgeschlagen | Operation={operation} | ExitCode={process.ExitCode}");
                }

                return new FirewallCommandResult(success, message, process.ExitCode, string.Empty, string.Empty);
            } catch (Exception ex) {
                Logger?.Error($"Fehler bei Firewall Kommando | Operation={operation}: {ex}");
                return new FirewallCommandResult(false, ex.Message, -1, string.Empty, ex.ToString());
            }
        }

        /// <summary>
        /// The BuildFirewallResultMessage
        /// </summary>
        /// <param name="headline">The headline<see cref="string"/></param>
        /// <param name="results">The results<see cref="FirewallCommandResult[]"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string BuildFirewallResultMessage(string headline, params FirewallCommandResult[] results) {
            StringBuilder builder = new();
            builder.AppendLine(headline);

            for (int i = 0; i < results.Length; i++) {
                builder.AppendLine($"Vorgang {i + 1}: {results[i].Message}");
            }

            return builder.ToString().TrimEnd();
        }

        /// <summary>
        /// The CombineProcessMessage
        /// </summary>
        /// <param name="output">The output<see cref="string"/></param>
        /// <param name="error">The error<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string CombineProcessMessage(string output, string error) {
            string trimmedOutput = (output ?? string.Empty).Trim();
            string trimmedError = (error ?? string.Empty).Trim();

            if (!string.IsNullOrWhiteSpace(trimmedError) && !string.IsNullOrWhiteSpace(trimmedOutput)) {
                return $"{trimmedOutput} | {trimmedError}";
            }

            if (!string.IsNullOrWhiteSpace(trimmedError)) {
                return trimmedError;
            }

            return trimmedOutput;
        }

        /// <summary>
        /// The button4_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void button4_Click(object sender, EventArgs e) {
            try {
                string sipTransport = GetSipTransport();
                int sipPort = GetIntValue(textBox4, "SIP Port");
                int rtpStart = GetIntValue(textBox7, "RTP Start");
                int rtpEnd = GetIntValue(textBox8, "RTP Ende");
                ValidatePortRange(rtpStart, rtpEnd);

                string sipRuleName = GetSipRuleName(sipTransport, sipPort);
                string rtpRuleName = GetRtpRuleName(rtpStart, rtpEnd);

                Logger?.Info($"Firewall entfernen gestartet | SIP={sipRuleName} | RTP={rtpRuleName}");

                FirewallCommandResult sipResult = ExecuteElevatedNetsh(
                    $"advfirewall firewall delete rule name=\"{sipRuleName}\"",
                    $"Entferne SIP Firewall-Regel {sipRuleName}");

                FirewallCommandResult rtpResult = ExecuteElevatedNetsh(
                    $"advfirewall firewall delete rule name=\"{rtpRuleName}\"",
                    $"Entferne RTP Firewall-Regel {rtpRuleName}");

                RefreshFirewallStatus();

                bool allOk = sipResult.Success && rtpResult.Success;
                Logger?.Info($"Firewall entfernen beendet | SIP={sipResult.Success} | RTP={rtpResult.Success}");

                if (allOk) {
                    Logger?.Info("Firewall-Regeln wurden entfernt.");
                } else {
                    Logger?.Warn(BuildFirewallResultMessage("Mindestens eine Firewall-Regel konnte nicht entfernt werden.", sipResult, rtpResult));
                }
            } catch (Exception ex) {
                Logger?.Error($"Fehler beim Entfernen der Firewall-Regeln: {ex}");
                Logger?.Error($"Firewall Fehler | Fehler={ex.Message}");
            }
        }



        private const string TKWindowsServiceName = "TK-Server";

        /// <summary>
        /// Installiert den TK-Server als Windows-Service mit GUI-losem Runtime-Start.
        /// </summary>
        private void InstallWindowsService_Click(object sender, EventArgs e) {
            try {
                string exePath = Application.ExecutablePath;
                string binPath = $"\"{exePath}\" -startsvc";

                Logger?.Info($"Windows-Service Installation gestartet | Service={TKWindowsServiceName} | BinPath={binPath}");

                FirewallCommandResult createResult = ExecuteElevatedSc(
                    $"create \"{TKWindowsServiceName}\" binPath= \"\\\"{exePath}\\\" -startsvc\" start= auto DisplayName= \"TK-Server\"",
                    "Installiere TK-Server Windows-Service");

                FirewallCommandResult descriptionResult = ExecuteElevatedSc(
                    $"description \"{TKWindowsServiceName}\" \"TK-Server Runtime: -startsvc\"",
                    "Setze TK-Server Service-Beschreibung");

                if (createResult.Success) {
                    Logger?.Info("Windows-Service wurde installiert. Startparameter: -startsvc");
                } else {
                    Logger?.Warn(BuildFirewallResultMessage("Windows-Service konnte nicht installiert werden.", createResult));
                }

                if (!descriptionResult.Success) {
                    Logger?.Warn(BuildFirewallResultMessage("Windows-Service Beschreibung konnte nicht gesetzt werden.", descriptionResult));
                }
            } catch (Exception ex) {
                Logger?.Error($"Fehler bei Windows-Service Installation: {ex}");
                Logger?.Error($"Windows-Service Fehler | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// Stoppt und löscht den installierten TK-Server Windows-Service.
        /// </summary>
        private void RemoveWindowsService_Click(object sender, EventArgs e) {
            try {
                Logger?.Info($"Windows-Service Löschung gestartet | Service={TKWindowsServiceName}");

                FirewallCommandResult stopResult = ExecuteElevatedSc(
                    $"stop \"{TKWindowsServiceName}\"",
                    "Stoppe TK-Server Windows-Service");

                FirewallCommandResult deleteResult = ExecuteElevatedSc(
                    $"delete \"{TKWindowsServiceName}\"",
                    "Lösche TK-Server Windows-Service");

                if (deleteResult.Success) {
                    Logger?.Info("Windows-Service wurde gelöscht.");
                } else {
                    Logger?.Warn(BuildFirewallResultMessage("Windows-Service konnte nicht gelöscht werden.", stopResult, deleteResult));
                }
            } catch (Exception ex) {
                Logger?.Error($"Fehler bei Windows-Service Löschung: {ex}");
                Logger?.Error($"Windows-Service Fehler | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// Führt sc.exe mit administrativer Erhöhung aus.
        /// </summary>
        private FirewallCommandResult ExecuteElevatedSc(string arguments, string operation) {
            try {
                using Process process = new();
                process.StartInfo = new ProcessStartInfo {
                    FileName = "sc.exe",
                    Arguments = arguments,
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Logger?.Info($"Windows-Service Kommando gestartet | Operation={operation} | Args={arguments}");
                process.Start();
                process.WaitForExit();

                bool success = process.ExitCode == 0;
                string message = success ? "OK" : $"ExitCode={process.ExitCode}";

                if (!success) {
                    Logger?.Warn($"Windows-Service Kommando nicht erfolgreich | Operation={operation} | ExitCode={process.ExitCode}");
                }

                return new FirewallCommandResult(success, message, process.ExitCode, string.Empty, string.Empty);
            } catch (Exception ex) {
                Logger?.Error($"Fehler bei Windows-Service Kommando | Operation={operation}: {ex}");
                return new FirewallCommandResult(false, ex.Message, -1, string.Empty, ex.ToString());
            }
        }

        /// <summary>
        /// The button5_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void button5_Click(object sender, EventArgs e) {
            try {
                RefreshFirewallStatus();
            } catch (Exception ex) {
                Logger?.Error($"Fehler beim manuellen Aktualisieren des Firewall-Status: {ex}");
                Logger?.Error($"Firewall Fehler | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// Defines the <see cref="FirewallCommandResult" />
        /// </summary>
        private sealed class FirewallCommandResult {
            /// <summary>
            /// Gets a value indicating whether Success
            /// </summary>
            public bool Success { get; }

            /// <summary>
            /// Gets the Message
            /// </summary>
            public string Message { get; }

            /// <summary>
            /// Gets the ExitCode
            /// </summary>
            public int ExitCode { get; }

            /// <summary>
            /// Gets the Output
            /// </summary>
            public string Output { get; }

            /// <summary>
            /// Gets the Error
            /// </summary>
            public string Error { get; }

            /// <summary>
            /// Initializes a new instance of the <see cref="FirewallCommandResult"/> class.
            /// </summary>
            /// <param name="success">The success<see cref="bool"/></param>
            /// <param name="message">The message<see cref="string"/></param>
            /// <param name="exitCode">The exitCode<see cref="int"/></param>
            /// <param name="output">The output<see cref="string"/></param>
            /// <param name="error">The error<see cref="string"/></param>
            public FirewallCommandResult(bool success, string message, int exitCode, string output, string error) {
                Success = success;
                Message = message;
                ExitCode = exitCode;
                Output = output;
                Error = error;
            }
        }
    }
}
