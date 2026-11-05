namespace TKServer.FormView {
    using System;
    using System.Data;
    using System.Drawing;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows.Forms;

    using TKServer.BASE;
    using TKServer.SQL;
    /// <summary>
    /// Defines the <see cref="TK_Start" />
    /// </summary>
    public partial class TK_Start : Form {
        /// <summary>
        /// Gets or sets the Logger
        /// </summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public TKBase_LogManager? Logger { get; set; }

        /// <summary>
        /// Defines the dashboard refresh interval while SQL is reachable.
        /// </summary>
        private const int DashboardOnlineRefreshIntervalMs = 3000;

        /// <summary>
        /// Defines the dashboard reconnect interval while SQL is unreachable.
        /// </summary>
        private const int DashboardOfflineReconnectIntervalMs = 5000;

        /// <summary>
        /// Defines the _refreshTimer
        /// </summary>
        private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = DashboardOnlineRefreshIntervalMs };

        /// <summary>
        /// Defines whether the dashboard may execute database-dependent refresh and runtime actions.
        /// </summary>
        private bool _dashboardDatabaseAvailable;

        /// <summary>
        /// Anzeige der gespeicherten oeffentlichen Internet-IP.
        /// </summary>
        private Label? _lblPublicInternetIp;

        /// <summary>
        /// Anzeige der lokalen Server-Bind-IPs.
        /// </summary>
        private Label? _lblLocalServerIps;


        /// <summary>
        /// Initializes a new instance of the <see cref="TK_Start"/> class.
        /// </summary>
        public TK_Start() {
            InitializeComponent();
            InitializeDashboardIpLabels();

            Load += TK_Start_Load;
            FormClosed += TK_Start_FormClosed;

            btnSipStart.Click += (_, _) => ExecuteUiAction("SIPInt starten", () => TKBase_GUIRunTime.Start(Logger));
            btnSipStop.Click += (_, _) => ExecuteUiAction("SIPInt stoppen", () => TKBase_GUIRunTime.Stop(Logger));
            btnProviderStart.Click += async (_, _) => await ExecuteProviderUiActionAsync("Provider-Registrierungen starten", () => TKBase_GUIRunTime.StartProviderRegistration(Logger));
            btnProviderStop.Click += async (_, _) => await ExecuteProviderUiActionAsync("Provider-Registrierungen stoppen", () => TKBase_GUIRunTime.StopProviderRegistration(Logger));
            btnProviderReload.Click += async (_, _) => await ExecuteProviderUiActionAsync("Provider-Registrierungen neu laden", () => TKBase_GUIRunTime.ReloadProviderRegistration(Logger));
            btnGsmStart.Click += (_, _) => ExecuteUiAction("GSM-Dienst starten", () => TKBase_GUIRunTime.StartGsm(Logger));
            btnGsmStop.Click += (_, _) => ExecuteUiAction("GSM-Dienst stoppen", () => TKBase_GUIRunTime.StopGsm(Logger));
            btnGsmRunOnce.Click += (_, _) => ExecuteUiAction("GSM Reload/Neuinitialisierung", () => TKBase_GUIRunTime.RunGsmOnce(Logger));
            btnRefresh.Click += (_, _) => RefreshAll(true);

            _refreshTimer.Tick += (_, _) => RefreshAll();

            TKBase_GUIRunTime.RunningStateChanged += SIP_ServerHost_RunningStateChanged;
        }

        /// <summary>
        /// The TK_Start_Load
        /// </summary>
        /// <param name="sender">The sender<see cref="object?"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void TK_Start_Load(object? sender, EventArgs e) {
            try {
                if (!EnsureDashboardDatabaseAvailable("TK_Start Dashboard laden", true)) {
                    if (!_refreshTimer.Enabled) {
                        _refreshTimer.Start();
                    }

                    Logger?.Warn("TK_Start Dashboard im Offline-Modus gestartet | SQL-Datenbank nicht erreichbar | automatische Wiederverbindung aktiv.");
                    return;
                }

                TKBase_GUIRunTime.ObserveServiceDesiredState(Logger);
                RefreshAll(true);
                if (!_refreshTimer.Enabled) {
                    _refreshTimer.Start();
                }

                Logger?.Info("TK_Start Dashboard geladen.");
            } catch (Exception ex) {
                Logger?.Error($"TK_Start Laden fehlgeschlagen | Fehler={ex}");
            }
        }

        /// <summary>
        /// The TK_Start_FormClosed
        /// </summary>
        /// <param name="sender">The sender<see cref="object?"/></param>
        /// <param name="e">The e<see cref="FormClosedEventArgs"/></param>
        private void TK_Start_FormClosed(object? sender, FormClosedEventArgs e) {
            _refreshTimer.Stop();
            TKBase_GUIRunTime.RunningStateChanged -= SIP_ServerHost_RunningStateChanged;
        }

        /// <summary>
        /// The SIP_ServerHost_RunningStateChanged
        /// </summary>
        /// <param name="running">The running<see cref="bool"/></param>
        /// <param name="message">The message<see cref="string"/></param>
        private void SIP_ServerHost_RunningStateChanged(bool running, string message) {
            if (InvokeRequired) {
                BeginInvoke(new Action(() => SIP_ServerHost_RunningStateChanged(running, message)));
                return;
            }

            lblSipState.Text = running ? "Läuft" : "Gestoppt";
            Logger?.Info(message);
            if (EnsureDashboardDatabaseAvailable("SIP Statusänderung aktualisieren", false)) {
                RefreshInternalUsers();
            }
        }

        /// <summary>
        /// The GSM_ServiceHost_RunningStateChanged
        /// </summary>
        /// <param name="running">The running<see cref="bool"/></param>
        /// <param name="message">The message<see cref="string"/></param>
        private void GSM_ServiceHost_RunningStateChanged(bool running, string message) {
            if (InvokeRequired) {
                BeginInvoke(new Action(() => GSM_ServiceHost_RunningStateChanged(running, message)));
                return;
            }

            lblGsmState.Text = running ? "Läuft" : "Gestoppt";
            Logger?.Info(message);
            if (EnsureDashboardDatabaseAvailable("GSM Statusänderung aktualisieren", false)) {
                RefreshGsm();
            }
        }

        /// <summary>
        /// The ExecuteUiAction
        /// </summary>
        /// <param name="actionName">The actionName<see cref="string"/></param>
        /// <param name="action">The action<see cref="Action"/></param>
        private void ExecuteUiAction(string actionName, Action action) {
            try {
                if (!EnsureDashboardDatabaseAvailable(actionName, true)) {
                    Logger?.Warn($"TK_Start Aktion nicht gestartet | Aktion={actionName} | SQL-Datenbank nicht erreichbar.");
                    return;
                }

                Logger?.Info($"TK_Start Aktion | {actionName}");
                action();
                RefreshAll();
            } catch (Exception ex) {
                Logger?.Error($"TK_Start Aktion fehlgeschlagen | Aktion={actionName} | Fehler={ex}");
            }
        }

        /// <summary>
        /// The ExecuteProviderUiActionAsync
        /// </summary>
        /// <param name="actionName">The actionName<see cref="string"/></param>
        /// <param name="action">The action<see cref="Action"/></param>
        /// <returns>The <see cref="Task"/></returns>
        private async Task ExecuteProviderUiActionAsync(string actionName, Action action) {
            bool timerWasEnabled = _refreshTimer.Enabled;

            try {
                if (!EnsureDashboardDatabaseAvailable(actionName, true)) {
                    Logger?.Warn($"TK_Start Aktion nicht gestartet | Aktion={actionName} | SQL-Datenbank nicht erreichbar.");
                    return;
                }

                Logger?.Info($"TK_Start Aktion | {actionName}");
                SetProviderButtonsEnabled(false);

                if (timerWasEnabled) {
                    _refreshTimer.Stop();
                }

                await Task.Run(action).ConfigureAwait(true);
                RefreshAll();
            } catch (Exception ex) {
                Logger?.Error($"TK_Start Aktion fehlgeschlagen | Aktion={actionName} | Fehler={ex}");
            } finally {
                if (timerWasEnabled && _dashboardDatabaseAvailable) {
                    _refreshTimer.Start();
                }

                SetProviderButtonsEnabled(_dashboardDatabaseAvailable);
            }
        }

        /// <summary>
        /// The SetProviderButtonsEnabled
        /// </summary>
        /// <param name="enabled">The enabled<see cref="bool"/></param>
        private void SetProviderButtonsEnabled(bool enabled) {
            btnProviderStart.Enabled = enabled;
            btnProviderStop.Enabled = enabled;
            btnProviderReload.Enabled = enabled;
        }

        /// <summary>
        /// The RefreshAll
        /// </summary>
        private void RefreshAll(bool forceDatabaseCheck = false) {
            if (!EnsureDashboardDatabaseAvailable("TK_Start Dashboard aktualisieren", forceDatabaseCheck)) {
                if (!_refreshTimer.Enabled) {
                    _refreshTimer.Start();
                }

                return;
            }

            RefreshHeader();
            RefreshInternalUsers();
            RefreshProviders();
            RefreshGsm();

            if (!_refreshTimer.Enabled) {
                _refreshTimer.Start();
            }
        }

        /// <summary>
        /// Ensures the SQL database is reachable before database-dependent dashboard actions are started.
        /// </summary>
        /// <param name="operation">The operation<see cref="string"/></param>
        /// <param name="forceCheck">The forceCheck<see cref="bool"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private bool EnsureDashboardDatabaseAvailable(string operation, bool forceCheck) {
            if (!forceCheck && _dashboardDatabaseAvailable && TKSQL_ConnectionClass.DatabaseAvailable) {
                return true;
            }

            bool wasOffline = !_dashboardDatabaseAvailable;
            bool available = TKSQL_ConnectionClass.TryEnsureDatabaseAvailable(Logger, operation, forceCheck);
            if (available) {
                if (wasOffline) {
                    Logger?.Info("TK_Start Datenbankstatus | SQL-Datenbank erreichbar, Dashboard-Aktionen werden freigegeben.");
                    Logger?.TryConfigureFromDatabase();
                    TKBase_GUIRunTime.ObserveServiceDesiredState(Logger);
                }

                _dashboardDatabaseAvailable = true;
                _refreshTimer.Interval = DashboardOnlineRefreshIntervalMs;
                SetRuntimeButtonsEnabled(true);
                return true;
            }

            _dashboardDatabaseAvailable = false;
            SetDashboardDatabaseUnavailable();
            return false;
        }

        /// <summary>
        /// Puts the dashboard into a safe offline state when SQL is unavailable.
        /// </summary>
        private void SetDashboardDatabaseUnavailable() {
            _refreshTimer.Interval = DashboardOfflineReconnectIntervalMs;
            lblSipState.Text = "DB nicht erreichbar";
            lblProviderState.Text = "DB nicht erreichbar";
            lblGsmState.Text = "DB nicht erreichbar";
            lblGsmLastRun.Text = "-";
            lblGsmBusy.Text = "Nein";
            lblGsmError.Text = string.IsNullOrWhiteSpace(TKSQL_ConnectionClass.LastDatabaseAvailabilityError)
                ? "SQL-Datenbank nicht erreichbar"
                : TKSQL_ConnectionClass.LastDatabaseAvailabilityError;
            if (_lblPublicInternetIp != null) _lblPublicInternetIp.Text = "DB nicht erreichbar";
            if (_lblLocalServerIps != null) _lblLocalServerIps.Text = "DB nicht erreichbar";

            gridInternalUsers.DataSource = null;
            gridProviders.DataSource = null;
            gridGsm.DataSource = null;
            SetRuntimeButtonsEnabled(false);
            btnRefresh.Enabled = true;
        }

        /// <summary>
        /// Enables or disables DB-dependent dashboard runtime buttons.
        /// </summary>
        /// <param name="enabled">The enabled<see cref="bool"/></param>
        private void SetRuntimeButtonsEnabled(bool enabled) {
            btnSipStart.Enabled = enabled;
            btnSipStop.Enabled = enabled;
            btnProviderStart.Enabled = enabled;
            btnProviderStop.Enabled = enabled;
            btnProviderReload.Enabled = enabled;
            btnGsmStart.Enabled = enabled;
            btnGsmStop.Enabled = enabled;
            btnGsmRunOnce.Enabled = enabled;
        }


        /// <summary>
        /// Erstellt die IP-Anzeige im TK_Start Dashboard ohne Designer-Abhängigkeit.
        /// </summary>
        private void InitializeDashboardIpLabels() {
            topPanel.AutoScroll = true;
            label8.Location = new Point(22, 735);
            lblGsmBusy.Location = new Point(22, 760);
            label9.Location = new Point(22, 795);
            lblGsmError.Location = new Point(22, 820);
            btnRefresh.Location = new Point(59, 915);

            Label titlePublic = new() {
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(22, 324),
                Text = "Internet-IP"
            };
            _lblPublicInternetIp = new Label {
                AutoEllipsis = true,
                Location = new Point(22, 349),
                Size = new Size(230, 86),
                Text = "-"
            };

            Label titleLocal = new() {
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(22, 455),
                Text = "Server-Bind-IPs"
            };
            _lblLocalServerIps = new Label {
                AutoEllipsis = true,
                Location = new Point(22, 480),
                Size = new Size(230, 90),
                Text = "-"
            };

            topPanel.Controls.Add(titlePublic);
            topPanel.Controls.Add(_lblPublicInternetIp);
            topPanel.Controls.Add(titleLocal);
            topPanel.Controls.Add(_lblLocalServerIps);
        }

        /// <summary>
        /// The RefreshHeader
        /// </summary>
        private void RefreshHeader() {
            try {
                DataTable runtimeState = new TKSQL_Commands(Logger).LoadRuntimeAreaControlTable();

                DataRow? sipRow = FindRuntimeAreaRow(runtimeState, "SIPInt");
                DataRow? providerRow = FindRuntimeAreaRow(runtimeState, "Provider");
                DataRow? gsmRow = FindRuntimeAreaRow(runtimeState, "GSM");

                lblSipState.Text = FormatRuntimeAreaState(sipRow);
                lblProviderState.Text = FormatRuntimeAreaState(providerRow);
                lblGsmState.Text = FormatRuntimeAreaState(gsmRow);
                lblGsmLastRun.Text = FormatDateTimeValue(gsmRow, "LastAppliedAt");
                lblGsmBusy.Text = string.Equals(Convert.ToString(gsmRow?["ActualState"]), "Starting", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Convert.ToString(gsmRow?["ActualState"]), "Stopping", StringComparison.OrdinalIgnoreCase)
                    ? "Ja"
                    : "Nein";
                lblGsmError.Text = string.IsNullOrWhiteSpace(Convert.ToString(gsmRow?["LastError"])) ? "-" : Convert.ToString(gsmRow?["LastError"]);
                RefreshIpOverview();
            } catch (Exception ex) {
                Logger?.Warn($"TK_Start Header Refresh Warnung | Fehler={ex.Message}");
            }
        }


        /// <summary>
        /// Aktualisiert Internet-IP und lokale Bind-IPs im TK_Start Dashboard.
        /// </summary>
        private void RefreshIpOverview() {
            try {
                TKSQL_Commands sql = new(Logger);
                DataRow? publicRow = sql.LoadPublicInternetIpState();
                DataTable properties = sql.LoadTkPropertiesTable();
                DataRow? propertiesRow = properties.Rows.Count > 0 ? properties.Rows[0] : null;

                string ipv4 = ReadDashboardString(publicRow, "PublicInternetIPv4");
                string ipv6 = ReadDashboardString(publicRow, "PublicInternetIPv6");
                string checkedAt = FormatDashboardDateTime(publicRow, "PublicInternetIPLastCheckedAt");
                string lastError = ReadDashboardString(publicRow, "PublicInternetIPLastError");
                string localIps = ReadDashboardString(propertiesRow, "ServerIP");

                if (_lblPublicInternetIp != null) {
                    string ipText = $"IPv4: {(string.IsNullOrWhiteSpace(ipv4) ? "-" : ipv4)}\r\nIPv6: {(string.IsNullOrWhiteSpace(ipv6) ? "-" : ipv6)}";
                    if (!string.IsNullOrWhiteSpace(checkedAt) && checkedAt != "-") {
                        ipText += $"\r\nCheck: {checkedAt}";
                    }
                    if (!string.IsNullOrWhiteSpace(lastError)) {
                        ipText += $"\r\nFehler: {lastError}";
                    }
                    _lblPublicInternetIp.Text = ipText;
                    _lblPublicInternetIp.Height = string.IsNullOrWhiteSpace(lastError) ? 86 : 116;
                }

                if (_lblLocalServerIps != null) {
                    _lblLocalServerIps.Text = string.IsNullOrWhiteSpace(localIps) ? "-" : localIps.Replace(";", "; ");
                }
            } catch (Exception ex) {
                Logger?.Warn($"TK_Start IP-Übersicht konnte nicht aktualisiert werden | Fehler={ex.Message}");
            }
        }

        private static string ReadDashboardString(DataRow? row, string columnName) {
            return row != null && row.Table.Columns.Contains(columnName) && row[columnName] != DBNull.Value
                ? Convert.ToString(row[columnName])?.Trim() ?? string.Empty
                : string.Empty;
        }

        private static string FormatDashboardDateTime(DataRow? row, string columnName) {
            if (row == null || !row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value) {
                return "-";
            }

            try {
                return Convert.ToDateTime(row[columnName]).ToString("dd.MM.yyyy HH:mm:ss");
            } catch {
                return "-";
            }
        }

        private static DataRow? FindRuntimeAreaRow(DataTable table, string areaKey) {
            foreach (DataRow row in table.Rows) {
                if (string.Equals(Convert.ToString(row["AreaKey"]), areaKey, StringComparison.OrdinalIgnoreCase)) {
                    return row;
                }
            }

            return null;
        }

        private static string FormatRuntimeAreaState(DataRow? row) {
            if (row == null) {
                return "Unbekannt";
            }

            bool desired = row["DesiredEnabled"] != DBNull.Value && Convert.ToBoolean(row["DesiredEnabled"]);
            string actual = Convert.ToString(row["ActualState"]) ?? "Stopped";

            return desired
                ? actual
                : string.Equals(actual, "Stopped", StringComparison.OrdinalIgnoreCase) ? "Gestoppt" : $"Stop angefordert ({actual})";
        }

        private static string FormatDateTimeValue(DataRow? row, string columnName) {
            if (row == null || !row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value) {
                return "-";
            }

            try {
                return Convert.ToDateTime(row[columnName]).ToString("dd.MM.yyyy HH:mm:ss");
            } catch {
                return "-";
            }
        }

        /// <summary>
        /// The RefreshInternalUsers
        /// </summary>
        private void RefreshInternalUsers() {
            try {
                DataTable table = new TKSQL_Commands(Logger).LoadDashboardInternalUsersTable();
                gridInternalUsers.DataSource = table;
                ApplyGridFormatting(gridInternalUsers, "IsRegistered");
            } catch (Exception ex) {
                Logger?.Error($"TK_Start Interne User Refresh fehlgeschlagen | Fehler={ex}");
            }
        }

        /// <summary>
        /// The RefreshProviders
        /// </summary>
        private void RefreshProviders() {
            try {
                DataTable table = new TKSQL_Commands(Logger).LoadDashboardProviderStatusTable();
                gridProviders.DataSource = table;
                ApplyGridFormatting(gridProviders, "IsRegistered");
            } catch (Exception ex) {
                Logger?.Error($"TK_Start Provider Refresh fehlgeschlagen | Fehler={ex}");
            }
        }

        /// <summary>
        /// The RefreshGsm
        /// </summary>
        private void RefreshGsm() {
            try {
                DataTable sourceRows = new TKSQL_Commands(Logger).GetGsmDashboardStateFromDatabase();
                DataTable table = new();
                table.Columns.Add("Telefonnummer", typeof(string));
                table.Columns.Add("IMEI", typeof(string));
                table.Columns.Add("Service gestartet", typeof(string));
                table.Columns.Add("__IsRunning", typeof(bool));

                foreach (DataRow sourceRow in sourceRows.Rows) {
                    DataRow row = table.NewRow();
                    bool isServiceStarted = sourceRow["IsServiceStarted"] != DBNull.Value && Convert.ToBoolean(sourceRow["IsServiceStarted"]);
                    row["Telefonnummer"] = sourceRow["Telefonnummer"]?.ToString() ?? "-";
                    row["IMEI"] = sourceRow["IMEI"]?.ToString() ?? "-";
                    row["Service gestartet"] = isServiceStarted ? "Ja" : "Nein";
                    row["__IsRunning"] = isServiceStarted;
                    table.Rows.Add(row);
                }

                gridGsm.DataSource = table;
                ApplyGridFormatting(gridGsm, "__IsRunning");

                if (gridGsm.Columns.Contains("__IsRunning")) {
                    gridGsm.Columns["__IsRunning"].Visible = false;
                }
            } catch (Exception ex) {
                Logger?.Error($"TK_Start GSM Refresh fehlgeschlagen | Fehler={ex}");
            }
        }

        /// <summary>
        /// The ApplyGridFormatting
        /// </summary>
        /// <param name="grid">The grid<see cref="DataGridView"/></param>
        /// <param name="activeColumnName">The activeColumnName<see cref="string?"/></param>
        private static void ApplyGridFormatting(DataGridView grid, string? activeColumnName = null) {
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            grid.RowHeadersVisible = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.ReadOnly = true;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.BackgroundColor = SystemColors.Window;
            grid.DefaultCellStyle.BackColor = SystemColors.Window;
            grid.DefaultCellStyle.ForeColor = SystemColors.ControlText;
            grid.AlternatingRowsDefaultCellStyle.BackColor = SystemColors.Window;
            grid.AlternatingRowsDefaultCellStyle.ForeColor = SystemColors.ControlText;

            foreach (DataGridViewColumn column in grid.Columns) {
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
                if (string.Equals(column.Name, activeColumnName, StringComparison.OrdinalIgnoreCase)) {
                    column.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                }
            }

            ApplyRowHighlighting(grid, activeColumnName);
        }

        /// <summary>
        /// The ApplyRowHighlighting
        /// </summary>
        /// <param name="grid">The grid<see cref="DataGridView"/></param>
        /// <param name="activeColumnName">The activeColumnName<see cref="string?"/></param>
        private static void ApplyRowHighlighting(DataGridView grid, string? activeColumnName) {
            foreach (DataGridViewRow row in grid.Rows.Cast<DataGridViewRow>()) {
                row.DefaultCellStyle.BackColor = SystemColors.Window;
                row.DefaultCellStyle.ForeColor = SystemColors.ControlText;
                row.DefaultCellStyle.SelectionBackColor = SystemColors.Highlight;
                row.DefaultCellStyle.SelectionForeColor = SystemColors.HighlightText;

                if (string.IsNullOrWhiteSpace(activeColumnName) || !grid.Columns.Contains(activeColumnName)) {
                    continue;
                }

                object? value = row.Cells[activeColumnName].Value;
                bool isActive = value != null && value != DBNull.Value && Convert.ToBoolean(value);
                if (!isActive) {
                    continue;
                }

                Color greenBack = Color.FromArgb(198, 239, 206);
                Color greenFore = Color.FromArgb(0, 97, 0);
                row.DefaultCellStyle.BackColor = greenBack;
                row.DefaultCellStyle.ForeColor = greenFore;
                row.DefaultCellStyle.SelectionBackColor = greenBack;
                row.DefaultCellStyle.SelectionForeColor = greenFore;
            }
        }
    }
}
