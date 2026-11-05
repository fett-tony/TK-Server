namespace TKServer.FormView {
    using sipcryptlib;
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Drawing;
    using System.Linq;
    using System.Windows.Forms;

    using TKServer.BASE;
    using TKServer.SQL;
    /// <summary>
    /// Defines the <see cref="TK_VoIPUser" />
    /// </summary>
    public partial class TK_VoIPUser : Form {
        /// <summary>
        /// Gets or sets the Logger
        /// </summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public TKBase_LogManager? Logger { get; set; }

        /// <summary>
        /// Defines the _userTable
        /// </summary>
        private readonly DataTable _userTable = new();

        /// <summary>
        /// Defines the _isLoading
        /// </summary>
        private bool _isLoading;

        /// <summary>
        /// Defines the _serverEventsBound
        /// </summary>
        private bool _serverEventsBound;

        /// <summary>
        /// Defines the _registrationTimer
        /// </summary>
        private readonly System.Windows.Forms.Timer _registrationTimer = new();

        /// <summary>
        /// Defines the _registeredUserIds
        /// </summary>
        private readonly HashSet<int> _registeredUserIds = new();

        /// <summary>
        /// Defines the <see cref="VoipUserListItem" />
        /// </summary>
        private sealed class VoipUserListItem {
            /// <summary>
            /// Gets the UserId
            /// </summary>
            public int UserId { get; }

            /// <summary>
            /// Gets the Text
            /// </summary>
            public string Text { get; }

            /// <summary>
            /// Initializes a new instance of the <see cref="VoipUserListItem"/> class.
            /// </summary>
            /// <param name="userId">The userId<see cref="int"/></param>
            /// <param name="text">The text<see cref="string"/></param>
            public VoipUserListItem(int userId, string text) {
                UserId = userId;
                Text = text;
            }

            /// <summary>
            /// The ToString
            /// </summary>
            /// <returns>The <see cref="string"/></returns>
            public override string ToString() {
                return Text;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TK_VoIPUser"/> class.
        /// </summary>
        public TK_VoIPUser() {
            InitializeComponent();
            _registrationTimer.Interval = 3000;
            _registrationTimer.Tick += RegistrationTimer_Tick;
        }

        /// <summary>
        /// The SIP_VoIPUser_Load
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void SIP_VoIPUser_Load(object sender, EventArgs e) {
            try {
                BindServerEvents();
                InitializeGrid();
                LoadUsers();
                UpdateServerUiState();
            } catch (Exception ex) {
                Logger?.Error($"VoIP-User konnten nicht geladen werden: {ex.Message}");
                Logger?.Error($"VoIP-User Fehler | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The dataGridView1_CellFormatting
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="DataGridViewCellFormattingEventArgs"/></param>
        private void dataGridView1_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e) {
            if (e.ColumnIndex == 3 && e.Value is string password) {
                e.Value = new string('*', password.Length);
                e.FormattingApplied = true;
            }
        }

        /// <summary>
        /// The InitializeGrid
        /// </summary>
        private void InitializeGrid() {
            dataGridView1.RowPrePaint -= DataGridView1_RowPrePaint;
            dataGridView1.RowPrePaint += DataGridView1_RowPrePaint;

            listBox1.DrawMode = DrawMode.OwnerDrawFixed;
            listBox1.DrawItem -= ListBox1_DrawItem;
            listBox1.DrawItem += ListBox1_DrawItem;
        }

        /// <summary>
        /// Defines the _isRefreshingRegistrations
        /// </summary>
        private bool _isRefreshingRegistrations;

        /// <summary>
        /// The RegistrationTimer_Tick
        /// </summary>
        /// <param name="sender">The sender<see cref="object?"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void RegistrationTimer_Tick(object? sender, EventArgs e) {
            if (_isRefreshingRegistrations) {
                return;
            }

            try {
                _isRefreshingRegistrations = true;
                RefreshRegisteredUsers();
            } catch (Exception ex) {
                Logger?.Error($"Fehler im RegistrationTimer: {ex.Message}");
            } finally {
                _isRefreshingRegistrations = false;
            }
        }

        /// <summary>
        /// The DecryptPasswordsForGrid
        /// </summary>
        private void DecryptPasswordsForGrid() {
            foreach (DataRow row in _userTable.Rows) {
                if (_userTable.Columns.Contains("Password") && row["Password"] != DBNull.Value) {
                    string encryptedPassword = row["Password"]?.ToString() ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(encryptedPassword)) {
                        continue;
                    }

                    try {
                        row["Password"] = SipCryptor.DecryptFixed(encryptedPassword);
                    } catch (Exception ex) {
                        Logger?.Error($"Passwort konnte nicht entschlüsselt werden | User={row["Username"]} | Fehler={ex.Message}");
                        row["Password"] = string.Empty;
                    }
                }
            }
        }

        /// <summary>
        /// The LoadUsers
        /// </summary>
        private void LoadUsers() {
            _isLoading = true;
            _userTable.Clear();

            _userTable.Merge(new TKSQL_Commands(Logger).LoadVoipUsersTable());

            DecryptPasswordsForGrid();

            if (dataGridView1.DataSource == null) {
                dataGridView1.DataSource = _userTable;
                ConfigureGrid();
            }

            RefreshActiveUsers();
            RefreshRegisteredUsers();
            StartRegistrationTimer();
            UpdateServerUiState();
            _isLoading = false;
        }

        /// <summary>
        /// The ConfigureGrid
        /// </summary>
        private void ConfigureGrid() {
            SetColumn("ID", "ID", true, 60);
            SetColumn("Username", "Benutzername", false, 130);
            SetColumn("Telefonnummer", "Rufnummer", false, 110);
            SetColumn("Password", "Passwort", false, 110);
            SetColumn("DisplayName", "Anzeigename", false, 150);
            SetColumn("UserType", "Typ", false, 90);
            SetColumn("Active", "Aktiv", false, 60);

            SetHiddenColumn("Email");
            SetHiddenColumn("Description");
            SetHiddenColumn("CanDialInternal");
            SetHiddenColumn("CanDialExternal");
            SetHiddenColumn("CanReceiveInternal");
            SetHiddenColumn("CanReceiveExternal");
            SetHiddenColumn("SortOrder");
            SetHiddenColumn("CreatedAt");
            SetHiddenColumn("UpdatedAt");
        }

        /// <summary>
        /// The SetColumn
        /// </summary>
        /// <param name="columnName">The columnName<see cref="string"/></param>
        /// <param name="headerText">The headerText<see cref="string"/></param>
        /// <param name="readOnly">The readOnly<see cref="bool"/></param>
        /// <param name="width">The width<see cref="int"/></param>
        private void SetColumn(string columnName, string headerText, bool readOnly, int width) {
            if (dataGridView1.Columns[columnName] == null) {
                return;
            }

            var col = dataGridView1.Columns[columnName];
            col.HeaderText = headerText;
            col.ReadOnly = readOnly;
            col.MinimumWidth = width;
        }

        /// <summary>
        /// The SetHiddenColumn
        /// </summary>
        /// <param name="columnName">The columnName<see cref="string"/></param>
        private void SetHiddenColumn(string columnName) {
            if (dataGridView1.Columns[columnName] != null) {
                dataGridView1.Columns[columnName]!.Visible = false;
            }
        }

        /// <summary>
        /// The StartRegistrationTimer
        /// </summary>
        private void StartRegistrationTimer() {
            if (!_registrationTimer.Enabled) {
                _registrationTimer.Start();
            }
        }

        /// <summary>
        /// The StopRegistrationTimer
        /// </summary>
        private void StopRegistrationTimer() {
            if (_registrationTimer.Enabled) {
                _registrationTimer.Stop();
            }
        }

        /// <summary>
        /// The ListBox1_DrawItem
        /// </summary>
        /// <param name="sender">The sender<see cref="object?"/></param>
        /// <param name="e">The e<see cref="DrawItemEventArgs"/></param>
        private void ListBox1_DrawItem(object? sender, DrawItemEventArgs e) {
            if (e.Index < 0 || e.Index >= listBox1.Items.Count) {
                return;
            }

            e.DrawBackground();

            if (listBox1.Items[e.Index] is VoipUserListItem item) {
                bool isRegistered = item.UserId > 0 && _registeredUserIds.Contains(item.UserId);
                Color textColor = isRegistered ? Color.Green : e.ForeColor;
                TextRenderer.DrawText(e.Graphics, item.Text, e.Font, e.Bounds, textColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }

            e.DrawFocusRectangle();
        }

        /// <summary>
        /// The RefreshActiveUsers
        /// </summary>
        private void RefreshActiveUsers() {
            listBox1.Items.Clear();

            IEnumerable<DataRow> rows = _userTable.Rows.Cast<DataRow>()
                .Where(r => r.RowState != DataRowState.Deleted)
                .Where(r => ToBool(r["Active"]));

            foreach (DataRow row in rows) {
                int userId = row["ID"] == DBNull.Value ? 0 : Convert.ToInt32(row["ID"]);
                string number = row["Telefonnummer"]?.ToString() ?? string.Empty;
                string displayName = row["DisplayName"]?.ToString() ?? string.Empty;
                string username = row["Username"]?.ToString() ?? string.Empty;
                string userType = row["UserType"]?.ToString() ?? "User";

                string text = string.IsNullOrWhiteSpace(displayName)
                    ? $"{number} - {username} [{userType}]"
                    : $"{number} - {displayName} [{userType}]";

                listBox1.Items.Add(new VoipUserListItem(userId, text));
            }

            labelActiveCount.Text = $"Aktive / freigegebene Benutzer: {listBox1.Items.Count}";
        }

        /// <summary>
        /// The RefreshRegisteredUsers
        /// </summary>
        private void RefreshRegisteredUsers() {
            HashSet<int> registeredUserIds = new TKSQL_Commands(Logger).LoadRegisteredInternalUserIds();

            _registeredUserIds.Clear();
            foreach (int userId in registeredUserIds) {
                _registeredUserIds.Add(userId);
            }

            dataGridView1.Invalidate();
            listBox1.Invalidate();
            UpdateServerUiState();
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
            if (row.DataBoundItem is not DataRowView rowView) {
                return;
            }

            int userId = rowView.Row["ID"] == DBNull.Value ? 0 : Convert.ToInt32(rowView.Row["ID"]);
            bool isRegistered = userId > 0 && _registeredUserIds.Contains(userId);

            row.DefaultCellStyle.BackColor = isRegistered ? Color.LightGreen : Color.White;
            row.DefaultCellStyle.SelectionBackColor = isRegistered ? Color.ForestGreen : SystemColors.Highlight;
            row.DefaultCellStyle.SelectionForeColor = isRegistered ? Color.White : SystemColors.HighlightText;
        }

        /// <summary>
        /// The ToBool
        /// </summary>
        /// <param name="value">The value<see cref="object?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool ToBool(object? value) {
            return value != null && value != DBNull.Value && Convert.ToBoolean(value);
        }

        /// <summary>
        /// The buttonSave_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void buttonSave_Click(object sender, EventArgs e) {
            try {
                dataGridView1.EndEdit();
                SaveUsers();
                LoadUsers();
                Logger?.Info("VoIP-User gespeichert.");
                Logger?.Info("VoIP-User Änderungen gespeichert.");
            } catch (Exception ex) {
                Logger?.Error($"Fehler beim Speichern der VoIP-User: {ex.Message}");
                Logger?.Error($"VoIP-User Speicherfehler | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The SaveUsers
        /// </summary>
        private void SaveUsers() {
            new TKSQL_Commands(Logger).SaveVoipUsers(_userTable);
        }

        /// <summary>
        /// The ValidateRow
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="isNew">The isNew<see cref="bool"/></param>
        private static void ValidateRow(DataRow row, bool isNew) {
            _ = RequiredText(row, "Username");
            _ = RequiredText(row, "Telefonnummer");
            _ = RequiredText(row, "Password");

            if (!isNew && (row["ID"] == DBNull.Value || Convert.ToInt32(row["ID"]) <= 0)) {
                throw new InvalidOperationException("Ein bestehender Benutzer hat keine gültige ID.");
            }
        }

        /// <summary>
        /// The RequiredText
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="columnName">The columnName<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string RequiredText(DataRow row, string columnName) {
            string value = row[columnName]?.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(value)) {
                throw new InvalidOperationException($"Feld '{columnName}' darf nicht leer sein.");
            }
            return value;
        }

        /// <summary>
        /// The OptionalText
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="columnName">The columnName<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string OptionalText(DataRow row, string columnName) {
            return row.Table.Columns.Contains(columnName) && row[columnName] != DBNull.Value
                ? row[columnName]?.ToString()?.Trim() ?? string.Empty
                : string.Empty;
        }

        /// <summary>
        /// The DefaultText
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="columnName">The columnName<see cref="string"/></param>
        /// <param name="defaultValue">The defaultValue<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string DefaultText(DataRow row, string columnName, string defaultValue) {
            string value = OptionalText(row, columnName);
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
        }

        /// <summary>
        /// The DefaultBool
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="columnName">The columnName<see cref="string"/></param>
        /// <param name="defaultValue">The defaultValue<see cref="bool"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool DefaultBool(DataRow row, string columnName, bool defaultValue) {
            return row.Table.Columns.Contains(columnName) && row[columnName] != DBNull.Value
                ? Convert.ToBoolean(row[columnName])
                : defaultValue;
        }

        /// <summary>
        /// The DefaultInt
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="columnName">The columnName<see cref="string"/></param>
        /// <param name="defaultValue">The defaultValue<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int DefaultInt(DataRow row, string columnName, int defaultValue) {
            return row.Table.Columns.Contains(columnName) && row[columnName] != DBNull.Value
                ? Convert.ToInt32(row[columnName])
                : defaultValue;
        }

        /// <summary>
        /// The GetDateTimeOrDefault
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="columnName">The columnName<see cref="string"/></param>
        /// <param name="defaultValue">The defaultValue<see cref="DateTime"/></param>
        /// <returns>The <see cref="DateTime"/></returns>
        private static DateTime GetDateTimeOrDefault(DataRow row, string columnName, DateTime defaultValue) {
            return row.Table.Columns.Contains(columnName) && row[columnName] != DBNull.Value
                ? Convert.ToDateTime(row[columnName])
                : defaultValue;
        }

        /// <summary>
        /// The buttonStart_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void buttonStart_Click(object sender, EventArgs e) {
            try {
                TKBase_GUIRunTime.Start(Logger);
                UpdateServerUiState();
            } catch (Exception ex) {
                Logger?.Error($"SIP Server konnte nicht gestartet werden: {ex}");
                Logger?.Error($"SIP Server Start fehlgeschlagen | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The buttonStop_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void buttonStop_Click(object sender, EventArgs e) {
            try {
                TKBase_GUIRunTime.Stop(Logger);
                UpdateServerUiState();
            } catch (Exception ex) {
                Logger?.Error($"SIP Server konnte nicht gestoppt werden: {ex}");
                Logger?.Error($"SIP Server Stop fehlgeschlagen | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The BindServerEvents
        /// </summary>
        private void BindServerEvents() {
            if (_serverEventsBound) {
                return;
            }

            TKBase_GUIRunTime.RunningStateChanged += SIP_ServerHost_RunningStateChanged;
            this.FormClosed += SIP_VoIPUser_FormClosed;
            _serverEventsBound = true;
        }

        /// <summary>
        /// The SIP_ServerHost_RunningStateChanged
        /// </summary>
        /// <param name="isRunning">The isRunning<see cref="bool"/></param>
        /// <param name="message">The message<see cref="string"/></param>
        private void SIP_ServerHost_RunningStateChanged(bool isRunning, string message) {
            if (IsDisposed) {
                return;
            }

            if (InvokeRequired) {
                BeginInvoke(new Action(() => SIP_ServerHost_RunningStateChanged(isRunning, message)));
                return;
            }

            Logger?.Info(message);
            UpdateServerUiState();
        }

        /// <summary>
        /// The SIP_VoIPUser_FormClosed
        /// </summary>
        /// <param name="sender">The sender<see cref="object?"/></param>
        /// <param name="e">The e<see cref="FormClosedEventArgs"/></param>
        private void SIP_VoIPUser_FormClosed(object? sender, FormClosedEventArgs e) {
            if (!_serverEventsBound) {
                return;
            }

            StopRegistrationTimer();
            _registrationTimer.Dispose();
            TKBase_GUIRunTime.RunningStateChanged -= SIP_ServerHost_RunningStateChanged;
            this.FormClosed -= SIP_VoIPUser_FormClosed;
            _serverEventsBound = false;
        }

        /// <summary>
        /// The UpdateServerUiState
        /// </summary>
        private void UpdateServerUiState() {
            bool isRunning = TKBase_GUIRunTime.IsRunning;

            buttonStart.Enabled = !isRunning;
            buttonStop.Enabled = isRunning;

            string serverStatus = isRunning
                ? "SIP Server: läuft im TK-Service"
                : "SIP Server: gestoppt";

            labelActiveCount.Text = $"Aktive / freigegebene Benutzer: {listBox1.Items.Count} | {serverStatus}";
        }

        /// <summary>
        /// The buttonReload_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void buttonReload_Click(object sender, EventArgs e) {
            try {
                LoadUsers();
                Logger?.Info("VoIP-User neu geladen.");
            } catch (Exception ex) {
                Logger?.Error($"Fehler beim Neuladen der VoIP-User: {ex.Message}");
                Logger?.Error($"VoIP-User Fehler | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The DataGridView1_UserDeletingRow
        /// </summary>
        /// <param name="sender">The sender<see cref="object?"/></param>
        /// <param name="e">The e<see cref="DataGridViewRowCancelEventArgs"/></param>
        private void DataGridView1_UserDeletingRow(object? sender, DataGridViewRowCancelEventArgs e) {
            if (e.Row.DataBoundItem is not DataRowView rowView) {
                return;
            }

            object idValue = rowView.Row["ID"];
            string title = idValue == DBNull.Value ? "Neuen Benutzer verwerfen" : "Benutzer löschen";
            string text = idValue == DBNull.Value
                ? "Die neue, noch nicht gespeicherte Zeile verwerfen?"
                : "Diesen Benutzer wirklich aus der Tabelle TK_SIP_User löschen?";

            Logger?.Info($"{title} | {text} | Ausführung ohne blockierenden Bestätigungsdialog.");

            if (idValue != DBNull.Value) {
                try {
                    new TKSQL_Commands(Logger).DeleteVoipUser(Convert.ToInt32(idValue));
                    Logger?.Info($"VoIP-User gelöscht | ID={idValue}");
                } catch (Exception ex) {
                    e.Cancel = true;
                    Logger?.Error($"Fehler beim Löschen des VoIP-Users: {ex.Message}");
                    Logger?.Error($"VoIP-User Löschen fehlgeschlagen | Fehler={ex.Message}");
                }
            }
        }

        /// <summary>
        /// The DataGridView1_CellValueChanged
        /// </summary>
        /// <param name="sender">The sender<see cref="object?"/></param>
        /// <param name="e">The e<see cref="DataGridViewCellEventArgs"/></param>
        private void DataGridView1_CellValueChanged(object? sender, DataGridViewCellEventArgs e) {
            if (!_isLoading) {
                RefreshActiveUsers();
            }
        }

        /// <summary>
        /// The DataGridView1_CurrentCellDirtyStateChanged
        /// </summary>
        /// <param name="sender">The sender<see cref="object?"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void DataGridView1_CurrentCellDirtyStateChanged(object? sender, EventArgs e) {
            if (dataGridView1.IsCurrentCellDirty) {
                dataGridView1.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        /// <summary>
        /// The DataGridView1_DataError
        /// </summary>
        /// <param name="sender">The sender<see cref="object?"/></param>
        /// <param name="e">The e<see cref="DataGridViewDataErrorEventArgs"/></param>
        private void DataGridView1_DataError(object? sender, DataGridViewDataErrorEventArgs e) {
            e.ThrowException = false;
            Logger?.Warn("Ungültiger Wert im VoIP-User Grid.");
        }
    }
}
