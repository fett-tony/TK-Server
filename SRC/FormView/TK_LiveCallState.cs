namespace TKServer.FormView {
    using System;
    using System.Data;
    using System.Drawing;
    using System.Text;
    using System.Threading;
    using System.Windows.Forms;

    using TKServer.BASE;
    using TKServer.SQL;
    /// <summary>
    /// Defines the <see cref="TK_LiveCallState" />
    /// </summary>
    public partial class TK_LiveCallState : Form {
        /// <summary>
        /// Gets or sets the Logger
        /// </summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public TKBase_LogManager? Logger { get; set; }

        /// <summary>
        /// Defines the _refreshTimer
        /// </summary>
        private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 2000 };

        /// <summary>
        /// Defines the _sipSql
        /// </summary>
        private readonly TKSQL_Commands _sipSql;

        /// <summary>
        /// Defines the _sourceTable
        /// </summary>
        private DataTable? _sourceTable;


        /// <summary>
        /// Initializes a new instance of the <see cref="TK_LiveCallState"/> class.
        /// </summary>
        public TK_LiveCallState() {
            InitializeComponent();
            _sipSql = new TKSQL_Commands();

            Load += TK_LiveCallState_Load;
            FormClosed += TK_LiveCallState_FormClosed;
            btnRefresh.Click += (_, _) => RefreshLiveCalls(true);
            btnHangup.Click += BtnHangup_Click;
            checkAutoRefresh.CheckedChanged += (_, _) => UpdateTimerState();
            dataGridView1.SelectionChanged += (_, _) => RefreshSelectionDetails();
            _refreshTimer.Tick += (_, _) => RefreshLiveCalls(false);
        }

        /// <summary>
        /// The TK_LiveCallState_Load
        /// </summary>
        /// <param name="sender">The sender<see cref="object?"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void TK_LiveCallState_Load(object? sender, EventArgs e) {
            try {
                ConfigureGrid();
                checkAutoRefresh.Checked = true;
                RefreshLiveCalls(true);
                Logger?.Info("Live Call State geöffnet.");
            } catch (Exception ex) {
                Logger?.Error($"Live Call State Laden fehlgeschlagen | Fehler={ex}");
                txtDetails.Text = ex.Message;
            }
        }

        /// <summary>
        /// The TK_LiveCallState_FormClosed
        /// </summary>
        /// <param name="sender">The sender<see cref="object?"/></param>
        /// <param name="e">The e<see cref="FormClosedEventArgs"/></param>
        private void TK_LiveCallState_FormClosed(object? sender, FormClosedEventArgs e) {
            _refreshTimer.Stop();
        }

        /// <summary>
        /// The UpdateTimerState
        /// </summary>
        private void UpdateTimerState() {
            if (checkAutoRefresh.Checked) {
                _refreshTimer.Start();
            } else {
                _refreshTimer.Stop();
            }
        }

        /// <summary>
        /// The ConfigureGrid
        /// </summary>
        private void ConfigureGrid() {
            dataGridView1.AutoGenerateColumns = true;
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.AllowUserToDeleteRows = false;
            dataGridView1.AllowUserToResizeRows = false;
            dataGridView1.MultiSelect = false;
            dataGridView1.ReadOnly = true;
            dataGridView1.RowHeadersVisible = false;
            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridView1.BackgroundColor = SystemColors.Window;
            dataGridView1.BorderStyle = BorderStyle.FixedSingle;
            dataGridView1.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            dataGridView1.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 248, 252);
            dataGridView1.RowPrePaint += DataGridView1_RowPrePaint;
        }

        /// <summary>
        /// The RefreshLiveCalls
        /// </summary>
        /// <param name="logSuccess">The logSuccess<see cref="bool"/></param>
        private void RefreshLiveCalls(bool logSuccess) {
            try {
                _sourceTable = _sipSql.GetRuntimeCallMonitoringTable();
                dataGridView1.DataSource = _sourceTable;
                ApplyGridLayout();
                RefreshHeader();
                RefreshSelectionDetails();

                if (logSuccess) {
                    Logger?.Info($"Live Call State geladen | Aktive Calls={_sourceTable.Rows.Count}");
                }
            } catch (Exception ex) {
                Logger?.Error($"Live Call State Refresh fehlgeschlagen | Fehler={ex}");
                lblSummary.Text = "Aktive Gespräche: Fehler";
                txtDetails.Text = ex.Message;
            }
        }

        /// <summary>
        /// The ApplyGridLayout
        /// </summary>
        private void ApplyGridLayout() {
            if (dataGridView1.Columns.Count == 0) {
                return;
            }

            HideColumnIfExists("CanHangup");
            HideColumnIfExists("CanTransfer");
            HideColumnIfExists("Info");

            SetFillWeight("SessionID", 150);
            SetFillWeight("Status", 70);
            SetFillWeight("RouteKind", 70);
            SetFillWeight("CallDirection", 70);
            SetFillWeight("FromNumber", 80);
            SetFillWeight("ToNumber", 80);
            SetFillWeight("OutboundExternalNumber", 90);
            SetFillWeight("DisplayFrom", 110);
            SetFillWeight("DisplayTo", 110);
            SetFillWeight("StartedAt", 90);
            SetFillWeight("AnsweredAt", 90);
            SetFillWeight("LastUpdatedAt", 90);

            RenameColumn("SessionID", "Session");
            RenameColumn("Status", "Status");
            RenameColumn("RouteKind", "Route");
            RenameColumn("CallDirection", "Richtung");
            RenameColumn("FromNumber", "Von Nummer");
            RenameColumn("ToNumber", "An Nummer");
            RenameColumn("OutboundExternalNumber", "Ausgangsnummer");
            RenameColumn("DisplayFrom", "Von");
            RenameColumn("DisplayTo", "An");
            RenameColumn("StartedAt", "Start");
            RenameColumn("AnsweredAt", "Angenommen");
            RenameColumn("LastUpdatedAt", "Aktualisiert");
        }

        /// <summary>
        /// The RefreshHeader
        /// </summary>
        private void RefreshHeader() {
            int count = _sourceTable?.Rows.Count ?? 0;
            lblSummary.Text = $"Aktive Gespräche: {count}";
            lblLastRefresh.Text = $"Letzte Aktualisierung: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";
        }

        /// <summary>
        /// The RefreshSelectionDetails
        /// </summary>
        private void RefreshSelectionDetails() {
            if (dataGridView1.CurrentRow?.DataBoundItem is not DataRowView rowView) {
                txtDetails.Text = "Kein aktives Gespräch ausgewählt.";
                btnHangup.Enabled = false;
                return;
            }

            DataRow row = rowView.Row;
            btnHangup.Enabled = row.Table.Columns.Contains("CanHangup")
                && row["CanHangup"] != DBNull.Value
                && Convert.ToBoolean(row["CanHangup"]);

            StringBuilder sb = new();
            foreach (DataColumn column in row.Table.Columns) {
                sb.AppendLine($"{column.ColumnName}: {FormatValue(row[column])}");
            }

            txtDetails.Text = sb.ToString();
        }

        /// <summary>
        /// The BtnHangup_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object?"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private async void BtnHangup_Click(object? sender, EventArgs e) {
            if (dataGridView1.CurrentRow?.DataBoundItem is not DataRowView rowView) {
                Logger?.Warn("Live Call State Hangup abgebrochen | Kein aktives Gespräch ausgewählt.");
                txtDetails.Text = "Kein aktives Gespräch ausgewählt.";
                return;
            }

            string sessionId = Convert.ToString(rowView.Row["SessionID"]) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(sessionId)) {
                Logger?.Warn("Live Call State Hangup abgebrochen | Ungültige SessionID.");
                txtDetails.Text = "Die ausgewählte Session enthält keine gültige SessionID.";
                return;
            }

            Logger?.Warn($"Live Call State Hangup gestartet | SessionID={sessionId}");

            try {
                btnHangup.Enabled = false;
                string response = await Task.Run(() => TKBase_GUIRunTime.SendServiceCommand($"HangupLiveCall({sessionId})", Logger));
                Logger?.Warn($"Live Call State Hangup an Service gesendet | SessionID={sessionId} | Antwort={response}");

                if (!response.StartsWith("ACCEPTED", StringComparison.OrdinalIgnoreCase)) {
                    txtDetails.Text = response;
                }
            } catch (Exception ex) {
                Logger?.Error($"Live Call State Hangup fehlgeschlagen | SessionID={sessionId} | Fehler={ex.Message}");
                txtDetails.Text = ex.Message;
            } finally {
                RefreshLiveCalls(true);
                RefreshSelectionDetails();
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
            string status = Convert.ToString(row.Cells["Status"].Value) ?? string.Empty;

            if (string.Equals(status, "Answered", StringComparison.OrdinalIgnoreCase)) {
                row.DefaultCellStyle.BackColor = Color.FromArgb(240, 255, 240);
            } else if (string.Equals(status, "Ringing", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "Trying", StringComparison.OrdinalIgnoreCase)) {
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 250, 230);
            } else if (string.Equals(status, "Holding", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "Transferring", StringComparison.OrdinalIgnoreCase)) {
                row.DefaultCellStyle.BackColor = Color.FromArgb(235, 245, 255);
            } else {
                row.DefaultCellStyle.BackColor = Color.White;
            }
        }

        /// <summary>
        /// The HideColumnIfExists
        /// </summary>
        /// <param name="name">The name<see cref="string"/></param>
        private void HideColumnIfExists(string name) {
            if (dataGridView1.Columns.Contains(name)) {
                dataGridView1.Columns[name].Visible = false;
            }
        }

        /// <summary>
        /// The RenameColumn
        /// </summary>
        /// <param name="name">The name<see cref="string"/></param>
        /// <param name="text">The text<see cref="string"/></param>
        private void RenameColumn(string name, string text) {
            if (dataGridView1.Columns.Contains(name)) {
                dataGridView1.Columns[name].HeaderText = text;
            }
        }

        /// <summary>
        /// The SetFillWeight
        /// </summary>
        /// <param name="name">The name<see cref="string"/></param>
        /// <param name="fillWeight">The fillWeight<see cref="float"/></param>
        private void SetFillWeight(string name, float fillWeight) {
            if (dataGridView1.Columns.Contains(name)) {
                dataGridView1.Columns[name].FillWeight = fillWeight;
            }
        }

        /// <summary>
        /// The FormatValue
        /// </summary>
        /// <param name="value">The value<see cref="object"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string FormatValue(object value) {
            if (value == DBNull.Value || value == null) {
                return "-";
            }

            if (value is DateTime dt) {
                return dt.ToString("dd.MM.yyyy HH:mm:ss");
            }

            if (value is bool b) {
                return b ? "Ja" : "Nein";
            }

            return Convert.ToString(value) ?? "-";
        }
    }
}
