namespace TKServer.FormView {
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Drawing;
    using System.Threading.Tasks;
    using System.Windows.Forms;

    using TKServer.BASE;
    using TKServer.SQL;
    /// <summary>
    /// Defines the <see cref="TK_ProviderAccounts" />
    /// </summary>
    public partial class TK_ProviderAccounts : Form {
        /// <summary>
        /// Gets or sets the Logger
        /// </summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public TKBase_LogManager? Logger { get; set; }

        /// <summary>
        /// Defines the _providerTable
        /// </summary>
        private DataTable? _providerTable;

        /// <summary>
        /// Defines the _registrationStates
        /// </summary>
        private readonly Dictionary<int, bool> _registrationStates = new();

        /// <summary>
        /// Defines the _statusTimer
        /// </summary>
        private readonly System.Windows.Forms.Timer _statusTimer;

        /// <summary>
        /// Initializes a new instance of the <see cref="TK_ProviderAccounts"/> class.
        /// </summary>
        public TK_ProviderAccounts() {
            InitializeComponent();

            button1.Click += Button1_Click;
            button2.Click += Button2_Click;
            button3.Click += Button3_Click;
            button4.Click += Button4_Click;
            dataGridView1.RowPrePaint += DataGridView1_RowPrePaint;
            dataGridView1.CellFormatting += dataGridView1_CellFormatting;

            _statusTimer = new System.Windows.Forms.Timer {
                Interval = 1500
            };
            _statusTimer.Tick += StatusTimer_Tick;
        }

        /// <summary>
        /// The OnFormClosed
        /// </summary>
        /// <param name="e">The e<see cref="FormClosedEventArgs"/></param>
        protected override void OnFormClosed(FormClosedEventArgs e) {
            _statusTimer.Stop();
            _statusTimer.Dispose();
            base.OnFormClosed(e);
        }

        /// <summary>
        /// The ProviderAccounts_Load
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void ProviderAccounts_Load(object sender, EventArgs e) {
            try {
                Logger?.Info("Tabelle ProviderAccounts wird geladen.");
                LoadProviderAccounts();
                RefreshRegistrationStates();
                _statusTimer.Start();
                Logger?.Info("Tabelle ProviderAccounts wurde geöffnet.");
            } catch (Exception ex) {
                Logger?.Error("Es konnte keine Tabelle abgefragt werden! " + ex);
            }
        }

        /// <summary>
        /// The LoadProviderAccounts
        /// </summary>
        private void LoadProviderAccounts() {
            _providerTable = new TKSQL_Commands(Logger).LoadProviderAccountsGuiTable();

            dataGridView1.AutoGenerateColumns = true;
            dataGridView1.DataSource = _providerTable;

            if (dataGridView1.Columns.Contains("ID")) {
                dataGridView1.Columns["ID"].ReadOnly = true;
            }

            if (dataGridView1.Columns.Contains("CreatedAt")) {
                dataGridView1.Columns["CreatedAt"].ReadOnly = true;
            }

            if (dataGridView1.Columns.Contains("UpdatedAt")) {
                dataGridView1.Columns["UpdatedAt"].ReadOnly = true;
            }

            Logger?.Info($"ProviderAccounts geladen | Anzahl={_providerTable.Rows.Count}");
        }

        /// <summary>
        /// The RefreshRegistrationStates
        /// </summary>
        private void RefreshRegistrationStates() {
            _registrationStates.Clear();

            try {
                Dictionary<int, bool> states = new TKSQL_Commands(Logger).GetProviderRegistrationStatesFromDatabase();
                foreach (KeyValuePair<int, bool> kvp in states) {
                    _registrationStates[kvp.Key] = kvp.Value;
                }
            } catch (Exception ex) {
                Logger?.Warn("Provider-Registrierungsstatus konnte nicht aus der Datenbank geladen werden: " + ex.Message);
            }

            ApplyRegistrationColors();
            dataGridView1.Refresh();
        }

        /// <summary>
        /// The ApplyRegistrationColors
        /// </summary>
        private void ApplyRegistrationColors() {
            foreach (DataGridViewRow row in dataGridView1.Rows) {
                if (row.IsNewRow || !dataGridView1.Columns.Contains("ID")) {
                    continue;
                }

                object? idValue = row.Cells["ID"].Value;
                if (idValue == null || idValue == DBNull.Value) {
                    continue;
                }

                int providerId = Convert.ToInt32(idValue);
                bool isRegistered = _registrationStates.TryGetValue(providerId, out bool state) && state;

                row.DefaultCellStyle.BackColor = isRegistered ? Color.LightGreen : Color.White;
                row.DefaultCellStyle.SelectionBackColor = isRegistered ? Color.LightGreen : Color.White;
                row.DefaultCellStyle.SelectionForeColor = Color.Black;
            }
        }

        /// <summary>
        /// The StatusTimer_Tick
        /// </summary>
        /// <param name="sender">The sender<see cref="object?"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void StatusTimer_Tick(object? sender, EventArgs e) {
            RefreshRegistrationStates();
        }

        /// <summary>
        /// The dataGridView1_CellFormatting
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="DataGridViewCellFormattingEventArgs"/></param>
        private void dataGridView1_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e) {
            if (dataGridView1.Columns[e.ColumnIndex].Name == "SIPPasswort" && e.Value != null) {
                string txt = e.Value.ToString() ?? string.Empty;
                e.Value = new string('*', txt.Length);
                e.FormattingApplied = true;
            }
        }

        /// <summary>
        /// The DataGridView1_RowPrePaint
        /// </summary>
        /// <param name="sender">The sender<see cref="object?"/></param>
        /// <param name="e">The e<see cref="DataGridViewRowPrePaintEventArgs"/></param>
        private void DataGridView1_RowPrePaint(object? sender, DataGridViewRowPrePaintEventArgs e) {
            if (e.RowIndex < 0 || e.RowIndex >= dataGridView1.Rows.Count) {
                return;
            }

            DataGridViewRow row = dataGridView1.Rows[e.RowIndex];
            if (row.IsNewRow || !dataGridView1.Columns.Contains("ID")) {
                return;
            }

            object? idValue = row.Cells["ID"].Value;
            if (idValue == null || idValue == DBNull.Value) {
                return;
            }

            int providerId = Convert.ToInt32(idValue);
            bool isRegistered = _registrationStates.TryGetValue(providerId, out bool state) && state;

            row.DefaultCellStyle.BackColor = isRegistered ? Color.LightGreen : Color.White;
            row.DefaultCellStyle.SelectionBackColor = isRegistered ? Color.LightGreen : Color.White;
            row.DefaultCellStyle.SelectionForeColor = Color.Black;
        }

        /// <summary>
        /// The Button1_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object?"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private async void Button1_Click(object? sender, EventArgs e) {
            await ExecuteProviderActionAsync("Start", () => TKBase_GUIRunTime.StartProviderRegistration(Logger));
        }

        /// <summary>
        /// The Button2_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object?"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private async void Button2_Click(object? sender, EventArgs e) {
            await ExecuteProviderActionAsync("Stop", () => TKBase_GUIRunTime.StopProviderRegistration(Logger));
        }

        /// <summary>
        /// The Button3_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object?"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private async void Button3_Click(object? sender, EventArgs e) {
            await ExecuteProviderActionAsync("Reload", () => TKBase_GUIRunTime.ReloadProviderRegistration(Logger));
        }

        /// <summary>
        /// The ExecuteProviderActionAsync
        /// </summary>
        /// <param name="actionName">The actionName<see cref="string"/></param>
        /// <param name="action">The action<see cref="Action"/></param>
        /// <returns>The <see cref="Task"/></returns>
        private async Task ExecuteProviderActionAsync(string actionName, Action action) {
            bool timerWasEnabled = _statusTimer.Enabled;

            try {
                Logger?.Info($"ProviderAccounts Button {actionName} geklickt.");
                SetProviderActionButtonsEnabled(false);

                if (timerWasEnabled) {
                    _statusTimer.Stop();
                }

                await Task.Run(action).ConfigureAwait(true);
                RefreshRegistrationStates();
                Logger?.Info($"ProviderAccounts {actionName} ausgeführt.");
            } catch (Exception ex) {
                Logger?.Error($"Provider-Registrierungen Aktion {actionName} fehlgeschlagen! {ex}");
            } finally {
                if (timerWasEnabled) {
                    _statusTimer.Start();
                }

                SetProviderActionButtonsEnabled(true);
            }
        }

        /// <summary>
        /// The SetProviderActionButtonsEnabled
        /// </summary>
        /// <param name="enabled">The enabled<see cref="bool"/></param>
        private void SetProviderActionButtonsEnabled(bool enabled) {
            button1.Enabled = enabled;
            button2.Enabled = enabled;
            button3.Enabled = enabled;
        }

        /// <summary>
        /// The Button4_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object?"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void Button4_Click(object? sender, EventArgs e) {
            try {
                if (_providerTable == null) {
                    Logger?.Warn("ProviderAccounts Speichern übersprungen | Grund=keine Daten geladen");
                    return;
                }

                List<int> deletedProviderAccountIds = GetDeletedProviderAccountIds();
                List<int> deactivatedProviderAccountIds = GetDeactivatedProviderAccountIds();
                List<int> activatedProviderAccountIds = GetActivatedProviderAccountIds();

                foreach (DataRow row in _providerTable.Rows) {
                    if (row.RowState == DataRowState.Added || row.RowState == DataRowState.Modified) {
                        row["UpdatedAt"] = DateTime.Now;
                    }

                    if (row.RowState == DataRowState.Added && _providerTable.Columns.Contains("CreatedAt")) {
                        row["CreatedAt"] = DateTime.Now;
                    }
                }

                new TKSQL_Commands(Logger).SaveProviderAccountsGuiTable(_providerTable);

                TKSQL_Commands commands = new TKSQL_Commands(Logger);

                foreach (int providerAccountId in deactivatedProviderAccountIds) {
                    commands.DeactivateProviderExternalNumbersAndRouting(providerAccountId);
                }

                foreach (int providerAccountId in activatedProviderAccountIds) {
                    commands.ActivateProviderExternalNumbersAndRouting(providerAccountId);
                }

                foreach (int providerAccountId in deletedProviderAccountIds) {
                    commands.DeleteExternalNumbersAndRoutingBySource("ProviderAccount", providerAccountId);
                }

                _providerTable.AcceptChanges();
                LoadProviderAccounts();
                RefreshRegistrationStates();

                Logger?.Info("ProviderAccounts wurden gespeichert.");
            } catch (Exception ex) {
                Logger?.Error("ProviderAccounts konnten nicht gespeichert werden! " + ex);
            }
        }


        /// <summary>
        /// Ermittelt ProviderAccounts, die in der Provider-GUI von aktiv auf inaktiv gesetzt wurden.
        /// </summary>
        /// <returns>The <see cref="List{int}"/></returns>
        private List<int> GetDeactivatedProviderAccountIds() {
            List<int> ids = new();
            if (_providerTable == null || !_providerTable.Columns.Contains("ID") || !_providerTable.Columns.Contains("Active")) {
                return ids;
            }

            foreach (DataRow row in _providerTable.Rows) {
                if (row.RowState != DataRowState.Modified) {
                    continue;
                }

                bool wasActive = ToBool(row["Active", DataRowVersion.Original]);
                bool isActive = ToBool(row["Active", DataRowVersion.Current]);
                if (!wasActive || isActive) {
                    continue;
                }

                object value = row["ID"];
                if (value != DBNull.Value && int.TryParse(Convert.ToString(value), out int providerAccountId) && providerAccountId > 0) {
                    ids.Add(providerAccountId);
                }
            }

            return ids;
        }

        /// <summary>
        /// Ermittelt ProviderAccounts, die in der Provider-GUI von inaktiv auf aktiv gesetzt wurden.
        /// </summary>
        /// <returns>The <see cref="List{int}"/></returns>
        private List<int> GetActivatedProviderAccountIds() {
            List<int> ids = new();
            if (_providerTable == null || !_providerTable.Columns.Contains("ID") || !_providerTable.Columns.Contains("Active")) {
                return ids;
            }

            foreach (DataRow row in _providerTable.Rows) {
                if (row.RowState != DataRowState.Modified) {
                    continue;
                }

                bool wasActive = ToBool(row["Active", DataRowVersion.Original]);
                bool isActive = ToBool(row["Active", DataRowVersion.Current]);
                if (wasActive || !isActive) {
                    continue;
                }

                object value = row["ID"];
                if (value != DBNull.Value && int.TryParse(Convert.ToString(value), out int providerAccountId) && providerAccountId > 0) {
                    ids.Add(providerAccountId);
                }
            }

            return ids;
        }

        /// <summary>
        /// Konvertiert Datenbank-/Grid-Werte sicher nach bool.
        /// </summary>
        /// <param name="value">The value<see cref="object?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool ToBool(object? value) {
            if (value == null || value == DBNull.Value) {
                return false;
            }

            return Convert.ToBoolean(value);
        }

        /// <summary>
        /// The GetDeletedProviderAccountIds
        /// </summary>
        /// <returns>The <see cref="List{int}"/></returns>
        private List<int> GetDeletedProviderAccountIds() {
            List<int> ids = new();
            if (_providerTable == null || !_providerTable.Columns.Contains("ID")) {
                return ids;
            }

            foreach (DataRow row in _providerTable.Rows) {
                if (row.RowState != DataRowState.Deleted) {
                    continue;
                }

                object value = row["ID", DataRowVersion.Original];
                if (value != DBNull.Value && int.TryParse(Convert.ToString(value), out int providerAccountId) && providerAccountId > 0) {
                    ids.Add(providerAccountId);
                }
            }

            return ids;
        }
    }
}
