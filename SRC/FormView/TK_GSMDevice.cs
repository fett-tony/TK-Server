namespace TKServer.FormView {
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Drawing;
    using System.Reflection;
    using System.Windows.Forms;

    using TKServer.BASE;
    using TKServer.GSM;
    using TKServer.SQL;

    /// <summary>
    /// Defines the <see cref="TK_GSMDevice" />
    /// </summary>
    public partial class TK_GSMDevice : Form {
        /// <summary>
        /// Gets or sets the Logger
        /// </summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public TKBase_LogManager? Logger { get; set; }

        /// <summary>
        /// Defines the _sourceTable
        /// </summary>
        private DataTable? _sourceTable;

        /// <summary>
        /// Defines the _expandedStatusIds
        /// </summary>
        private readonly HashSet<int> _expandedStatusIds = new HashSet<int>();

        //private readonly WinTimer _refreshTimer = new WinTimer();

        /// <summary>
        /// Defines the ColExpand
        /// </summary>
        private const string ColExpand = "colExpand";

        /// <summary>
        /// Defines the ColStatusId
        /// </summary>
        private const string ColStatusId = "colStatusId";

        /// <summary>
        /// Defines the ColPhone
        /// </summary>
        private const string ColPhone = "colPhone";

        /// <summary>
        /// Defines the ColModem
        /// </summary>
        private const string ColModem = "colModem";

        /// <summary>
        /// Defines the ColSignal
        /// </summary>
        private const string ColSignal = "colSignal";

        /// <summary>
        /// Defines the ColRegistered
        /// </summary>
        private const string ColRegistered = "colRegistered";

        /// <summary>
        /// Defines the ColLastSeen
        /// </summary>
        private const string ColLastSeen = "colLastSeen";

        /// <summary>
        /// Initializes a new instance of the <see cref="TK_GSMDevice"/> class.
        /// </summary>
        public TK_GSMDevice() {
            InitializeComponent();
        }

        /// <summary>
        /// The GSM_Geräte_Load
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void GSM_Geräte_Load(object sender, EventArgs e) {
            try {
                Logger?.Info("TestForm geladen.");

                ConfigureGrid();
                //ConfigureRefreshTimer();
                LoadStatusRows(true);

            } catch (Exception ex) {
                Logger?.Error($"TestForm_Load Fehler | Fehler={ex.Message}");
                Logger?.Error($"GSM-Geräte UI Fehler | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The ConfigureGrid
        /// </summary>
        private void ConfigureGrid() {
            dataGridView1.DataSource = null;
            dataGridView1.Rows.Clear();
            dataGridView1.Columns.Clear();

            dataGridView1.AutoGenerateColumns = false;
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.AllowUserToDeleteRows = false;
            dataGridView1.AllowUserToResizeRows = false;
            dataGridView1.MultiSelect = false;
            dataGridView1.ReadOnly = true;
            dataGridView1.RowHeadersVisible = false;
            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dataGridView1.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            dataGridView1.BackgroundColor = SystemColors.Window;
            dataGridView1.BorderStyle = BorderStyle.FixedSingle;
            dataGridView1.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            dataGridView1.RowsDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;

            SetDoubleBuffered(dataGridView1, true);

            DataGridViewTextBoxColumn expandCol = new DataGridViewTextBoxColumn {
                Name = ColExpand,
                HeaderText = "",
                Width = 35,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };

            DataGridViewTextBoxColumn statusIdCol = new DataGridViewTextBoxColumn {
                Name = ColStatusId,
                HeaderText = "Status-ID",
                Width = 80,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };

            DataGridViewTextBoxColumn phoneCol = new DataGridViewTextBoxColumn {
                Name = ColPhone,
                HeaderText = "SIM / Rufnummer",
                Width = 220,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle {
                    WrapMode = DataGridViewTriState.True
                }
            };

            DataGridViewTextBoxColumn modemCol = new DataGridViewTextBoxColumn {
                Name = ColModem,
                HeaderText = "Modem",
                Width = 340,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle {
                    WrapMode = DataGridViewTriState.True
                }
            };

            DataGridViewTextBoxColumn signalCol = new DataGridViewTextBoxColumn {
                Name = ColSignal,
                HeaderText = "Signal",
                Width = 110,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle {
                    Font = new Font("Consolas", 10F, FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleLeft
                }
            };

            DataGridViewTextBoxColumn regCol = new DataGridViewTextBoxColumn {
                Name = ColRegistered,
                HeaderText = "Registriert",
                Width = 90,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };

            DataGridViewTextBoxColumn lastSeenCol = new DataGridViewTextBoxColumn {
                Name = ColLastSeen,
                HeaderText = "Zuletzt gesehen",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };

            dataGridView1.Columns.Add(expandCol);
            dataGridView1.Columns.Add(statusIdCol);
            dataGridView1.Columns.Add(phoneCol);
            dataGridView1.Columns.Add(modemCol);
            dataGridView1.Columns.Add(signalCol);
            dataGridView1.Columns.Add(regCol);
            dataGridView1.Columns.Add(lastSeenCol);
        }

        /// <summary>
        /// The ConfigureRefreshTimer
        /// </summary>
        private void ConfigureRefreshTimer() {
        }

        /// <summary>
        /// The RefreshTimer_Tick
        /// </summary>
        /// <param name="sender">The sender<see cref="object?"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void RefreshTimer_Tick(object? sender, EventArgs e) {
            try {
                LoadStatusRows(false);
            } catch (Exception ex) {
                Logger?.Error($"Auto-Refresh Fehler | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The LoadStatusRows
        /// </summary>
        /// <param name="resetExpandedRows">The resetExpandedRows<see cref="bool"/></param>
        private void LoadStatusRows(bool resetExpandedRows) {
            try {
                int? selectedStatusId = GetSelectedStatusId();

                Logger?.Info("Lade Statuszeilen für TreeGrid-Darstellung.");

                TKSQL_Commands commands = new TKSQL_Commands(Logger);
                _sourceTable = commands.GetGsmDeviceStatusTable();

                dataGridView1.SuspendLayout();
                dataGridView1.Rows.Clear();

                HashSet<int> expandedToRestore = resetExpandedRows
                    ? new HashSet<int>()
                    : new HashSet<int>(_expandedStatusIds);

                _expandedStatusIds.Clear();

                foreach (DataRow row in _sourceTable.Rows) {
                    int statusId = ToInt(row["ID"]);
                    AddStatusRow(row);

                    if (expandedToRestore.Contains(statusId)) {
                        int parentIndex = FindParentRowIndexByStatusId(statusId);
                        if (parentIndex >= 0) {
                            ExpandRow(parentIndex, statusId, false);
                        }
                    }
                }

                RestoreSelection(selectedStatusId);
                dataGridView1.ResumeLayout();
                dataGridView1.ClearSelection();

                if (selectedStatusId.HasValue) {
                    int rowIndex = FindParentRowIndexByStatusId(selectedStatusId.Value);
                    if (rowIndex >= 0) {
                        dataGridView1.Rows[rowIndex].Selected = true;
                        if (rowIndex >= 0)
                            dataGridView1.FirstDisplayedScrollingRowIndex = Math.Max(0, rowIndex);
                    }
                }

                AdjustAllDetailRowHeights();

                Logger?.Info($"Statuszeilen geladen | Rows={_sourceTable.Rows.Count}");
            } catch (Exception ex) {
                Logger?.Error($"Fehler beim Laden der Statuszeilen | Fehler={ex.Message}");
                Logger?.Error($"GSM-Geräte SQL-Fehler | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The dataGridView1_RowPrePaint
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="DataGridViewRowPrePaintEventArgs"/></param>
        private void dataGridView1_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e) {
            DataGridViewRow row = dataGridView1.Rows[e.RowIndex];

            if (row.Tag == null)
                return;

            if (row.Tag.ToString()?.StartsWith("DETAIL:") == true)
                return;

            int signal = 0;
            bool registered = false;

            if (row.Cells["colSignal"].Value != null)
                int.TryParse(row.Cells["colSignal"].Value.ToString(), out signal);

            if (row.Cells["colRegistered"].Value != null) {
                string reg = row.Cells["colRegistered"].Value.ToString();
                registered = reg == "Ja" || reg == "True" || reg == "1";
            }

            if (!registered) {
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 240, 240);
            } else if (signal < 10) {
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 250, 230);
            } else {
                row.DefaultCellStyle.BackColor = Color.FromArgb(240, 255, 240);
            }
        }

        /// <summary>
        /// The AddStatusRow
        /// </summary>
        /// <param name="sourceRow">The sourceRow<see cref="DataRow"/></param>
        private void AddStatusRow(DataRow sourceRow) {
            int statusId = ToInt(sourceRow["ID"]);
            bool isRegistered = ToBool(sourceRow["IsRegistered"]);
            int signal = ToInt(sourceRow["SignalStrength"]);

            int rowIndex = dataGridView1.Rows.Add(
                "+",
                statusId.ToString(),
                Nz(sourceRow["Telefonnummer"]),
                Nz(sourceRow["ModemName"]),
                BuildSignalBar(signal),
                BoolText(sourceRow["IsRegistered"]),
                FormatDate(sourceRow["LastSeen"])
            );

            DataGridViewRow gridRow = dataGridView1.Rows[rowIndex];
            gridRow.Tag = sourceRow;
            gridRow.Height = 28;
            gridRow.DefaultCellStyle.Font = new Font(dataGridView1.Font, FontStyle.Regular);
            gridRow.DefaultCellStyle.BackColor = Color.White;

            ApplyStatusColors(gridRow, isRegistered, signal);
        }

        /// <summary>
        /// The BuildSignalBar
        /// </summary>
        /// <param name="signal">The signal<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        private string BuildSignalBar(int signal) {
            if (signal <= 0)
                return "□□□□□ 0";

            int level;

            if (signal >= 25)
                level = 5;
            else if (signal >= 20)
                level = 4;
            else if (signal >= 15)
                level = 3;
            else if (signal >= 10)
                level = 2;
            else
                level = 1;

            return new string('■', level) + new string('□', 5 - level) + " " + signal;
        }

        /// <summary>
        /// The ApplyStatusColors
        /// </summary>
        /// <param name="row">The row<see cref="DataGridViewRow"/></param>
        /// <param name="isRegistered">The isRegistered<see cref="bool"/></param>
        /// <param name="signal">The signal<see cref="int"/></param>
        private void ApplyStatusColors(DataGridViewRow row, bool isRegistered, int signal) {
            row.Cells[ColRegistered].Style.ForeColor = isRegistered ? Color.DarkGreen : Color.DarkRed;
            row.Cells[ColRegistered].Style.Font = new Font(dataGridView1.Font, FontStyle.Bold);

            if (signal >= 25)
                row.Cells[ColSignal].Style.ForeColor = Color.DarkGreen;
            else if (signal >= 15)
                row.Cells[ColSignal].Style.ForeColor = Color.OliveDrab;
            else if (signal >= 10)
                row.Cells[ColSignal].Style.ForeColor = Color.DarkOrange;
            else if (signal > 0)
                row.Cells[ColSignal].Style.ForeColor = Color.DarkRed;
            else
                row.Cells[ColSignal].Style.ForeColor = Color.Gray;

            row.Cells[ColSignal].Style.Font = new Font("Consolas", 10F, FontStyle.Bold);

            if (!isRegistered)
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 248, 248);
            else if (signal > 0 && signal < 10)
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 252, 245);
            else
                row.DefaultCellStyle.BackColor = Color.White;
        }

        /// <summary>
        /// The ToggleRow
        /// </summary>
        /// <param name="rowIndex">The rowIndex<see cref="int"/></param>
        private void ToggleRow(int rowIndex) {
            if (rowIndex < 0 || rowIndex >= dataGridView1.Rows.Count)
                return;

            DataGridViewRow clickedRow = dataGridView1.Rows[rowIndex];

            if (IsDetailRow(clickedRow))
                return;

            int statusId = ToInt(clickedRow.Cells[ColStatusId].Value);

            if (_expandedStatusIds.Contains(statusId))
                CollapseRow(rowIndex, statusId);
            else
                ExpandRow(rowIndex, statusId, true);
        }

        /// <summary>
        /// The ExpandRow
        /// </summary>
        /// <param name="rowIndex">The rowIndex<see cref="int"/></param>
        /// <param name="statusId">The statusId<see cref="int"/></param>
        /// <param name="writeLog">The writeLog<see cref="bool"/></param>
        private void ExpandRow(int rowIndex, int statusId, bool writeLog) {
            try {
                DataGridViewRow parentRow = dataGridView1.Rows[rowIndex];
                DataRow? sourceRow = parentRow.Tag as DataRow;

                if (sourceRow == null)
                    return;

                string simDetail = BuildSimDetailText(sourceRow);
                string modemDetail = BuildModemDetailText(sourceRow);

                int detailIndex = rowIndex + 1;
                dataGridView1.Rows.Insert(detailIndex, "", "", "", "", "", "");

                DataGridViewRow detailRow = dataGridView1.Rows[detailIndex];
                detailRow.Tag = $"DETAIL:{statusId}";
                detailRow.DefaultCellStyle.BackColor = Color.FromArgb(245, 248, 252);
                detailRow.DefaultCellStyle.SelectionBackColor = Color.FromArgb(225, 235, 245);
                detailRow.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
                detailRow.ReadOnly = true;

                detailRow.Cells[ColExpand].Value = "";
                detailRow.Cells[ColStatusId].Value = "";
                detailRow.Cells[ColPhone].Value = simDetail;
                detailRow.Cells[ColModem].Value = modemDetail;
                detailRow.Cells[ColSignal].Value = "";
                detailRow.Cells[ColRegistered].Value = "";
                detailRow.Cells[ColLastSeen].Value = "";

                detailRow.Cells[ColPhone].Style.Alignment = DataGridViewContentAlignment.TopLeft;
                detailRow.Cells[ColModem].Style.Alignment = DataGridViewContentAlignment.TopLeft;

                AdjustDetailRowHeight(detailRow);

                parentRow.Cells[ColExpand].Value = "-";
                _expandedStatusIds.Add(statusId);

                if (writeLog)
                    Logger?.Info($"Statuszeile aufgeklappt | StatusID={statusId}");
            } catch (Exception ex) {
                Logger?.Error($"Fehler beim Aufklappen | StatusID={statusId} | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The CollapseRow
        /// </summary>
        /// <param name="rowIndex">The rowIndex<see cref="int"/></param>
        /// <param name="statusId">The statusId<see cref="int"/></param>
        private void CollapseRow(int rowIndex, int statusId) {
            try {
                int detailIndex = rowIndex + 1;

                if (detailIndex < dataGridView1.Rows.Count) {
                    DataGridViewRow nextRow = dataGridView1.Rows[detailIndex];

                    if (IsDetailRowForStatus(nextRow, statusId))
                        dataGridView1.Rows.RemoveAt(detailIndex);
                }

                dataGridView1.Rows[rowIndex].Cells[ColExpand].Value = "+";
                _expandedStatusIds.Remove(statusId);

                Logger?.Info($"Statuszeile zugeklappt | StatusID={statusId}");
            } catch (Exception ex) {
                Logger?.Error($"Fehler beim Zuklappen | StatusID={statusId} | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The AdjustAllDetailRowHeights
        /// </summary>
        private void AdjustAllDetailRowHeights() {
            foreach (DataGridViewRow row in dataGridView1.Rows) {
                if (IsDetailRow(row))
                    AdjustDetailRowHeight(row);
            }
        }

        /// <summary>
        /// The AdjustDetailRowHeight
        /// </summary>
        /// <param name="detailRow">The detailRow<see cref="DataGridViewRow"/></param>
        private void AdjustDetailRowHeight(DataGridViewRow detailRow) {
            try {
                int phoneWidth = dataGridView1.Columns[ColPhone].Width - 20;
                int modemWidth = dataGridView1.Columns[ColModem].Width - 20;

                if (phoneWidth < 50) phoneWidth = 50;
                if (modemWidth < 50) modemWidth = 50;

                string phoneText = detailRow.Cells[ColPhone].Value?.ToString() ?? "";
                string modemText = detailRow.Cells[ColModem].Value?.ToString() ?? "";

                Size phoneSize = TextRenderer.MeasureText(
                    phoneText,
                    dataGridView1.Font,
                    new Size(phoneWidth, int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);

                Size modemSize = TextRenderer.MeasureText(
                    modemText,
                    dataGridView1.Font,
                    new Size(modemWidth, int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);

                int maxHeight = Math.Max(phoneSize.Height, modemSize.Height) + 16;
                if (maxHeight < 80)
                    maxHeight = 80;

                detailRow.Height = maxHeight;
            } catch (Exception ex) {
                Logger?.Error($"Fehler beim Anpassen der Detailzeilenhöhe | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The GetSelectedStatusId
        /// </summary>
        /// <returns>The <see cref="int?"/></returns>
        private int? GetSelectedStatusId() {
            if (dataGridView1.CurrentRow == null)
                return null;

            if (IsDetailRow(dataGridView1.CurrentRow))
                return null;

            return ToInt(dataGridView1.CurrentRow.Cells[ColStatusId].Value);
        }

        /// <summary>
        /// The RestoreSelection
        /// </summary>
        /// <param name="statusId">The statusId<see cref="int?"/></param>
        private void RestoreSelection(int? statusId) {
            if (!statusId.HasValue)
                return;

            int rowIndex = FindParentRowIndexByStatusId(statusId.Value);
            if (rowIndex >= 0)
                dataGridView1.Rows[rowIndex].Selected = true;
        }

        /// <summary>
        /// The FindParentRowIndexByStatusId
        /// </summary>
        /// <param name="statusId">The statusId<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        private int FindParentRowIndexByStatusId(int statusId) {
            foreach (DataGridViewRow row in dataGridView1.Rows) {
                if (IsDetailRow(row))
                    continue;

                if (ToInt(row.Cells[ColStatusId].Value) == statusId)
                    return row.Index;
            }

            return -1;
        }

        /// <summary>
        /// The IsDetailRow
        /// </summary>
        /// <param name="row">The row<see cref="DataGridViewRow"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private bool IsDetailRow(DataGridViewRow row) {
            if (row.Tag == null)
                return false;

            return row.Tag.ToString()?.StartsWith("DETAIL:") == true;
        }

        /// <summary>
        /// The IsDetailRowForStatus
        /// </summary>
        /// <param name="row">The row<see cref="DataGridViewRow"/></param>
        /// <param name="statusId">The statusId<see cref="int"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private bool IsDetailRowForStatus(DataGridViewRow row, int statusId) {
            if (row.Tag == null)
                return false;

            return string.Equals(row.Tag.ToString(), $"DETAIL:{statusId}", StringComparison.Ordinal);
        }

        /// <summary>
        /// The BuildSimDetailText
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <returns>The <see cref="string"/></returns>
        private string BuildSimDetailText(DataRow row) {
            return
                "SIM\r\n" +
                "----------------------\r\n" +
                "Rufnummer: " + Nz(row["Telefonnummer"]) + "\r\n" +
                "Provider: " + Nz(row["ProviderName"]) + "\r\n" +
                "ICCID: " + Nz(row["ICCID"]) + "\r\n" +
                "IMSI: " + Nz(row["IMSI"]) + "\r\n" +
                "Aktiv: " + BoolText(row["SimActive"]);
        }

        /// <summary>
        /// The BuildModemDetailText
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <returns>The <see cref="string"/></returns>
        private string BuildModemDetailText(DataRow row) {
            return
                "MODEM / STATUS\r\n" +
                "----------------------\r\n" +
                "Modem: " + Nz(row["ModemName"]) + "\r\n" +
                "Hersteller: " + Nz(row["Manufacturer"]) + "\r\n" +
                "Modell: " + Nz(row["Model"]) + "\r\n" +
                "IMEI: " + Nz(row["IMEI"]) + "\r\n" +
                "Aktiv: " + BoolText(row["ModemActive"]) + "\r\n" +
                "\r\n" +
                "Vorhanden: " + BoolText(row["IsPresent"]) + "\r\n" +
                "SIM eingelegt: " + BoolText(row["IsSimInserted"]) + "\r\n" +
                "Registriert: " + BoolText(row["IsRegistered"]) + "\r\n" +
                "Signal: " + Nz(row["SignalStrength"]) + "\r\n" +
                "COM AT: " + Nz(row["ComPortAT"]) + "\r\n" +
                "COM Audio: " + Nz(row["ComPortAudio"]) + "\r\n" +
                "COM Diag: " + Nz(row["ComPortDiag"]) + "\r\n" +
                "COM NMEA: " + Nz(row["ComPortNMEA"]) + "\r\n" +
                "Zuletzt gesehen: " + FormatDate(row["LastSeen"]) + "\r\n" +
                "Letzter Fehler: " + Nz(row["LastError"]);
        }

        /// <summary>
        /// The Nz
        /// </summary>
        /// <param name="value">The value<see cref="object?"/></param>
        /// <returns>The <see cref="string"/></returns>
        private string Nz(object? value) {
            if (value == null || value == DBNull.Value)
                return string.Empty;

            return value.ToString() ?? string.Empty;
        }

        /// <summary>
        /// The BoolText
        /// </summary>
        /// <param name="value">The value<see cref="object?"/></param>
        /// <returns>The <see cref="string"/></returns>
        private string BoolText(object? value) {
            string text = Nz(value);

            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            if (text == "True" || text == "1")
                return "Ja";

            if (text == "False" || text == "0")
                return "Nein";

            return text;
        }

        /// <summary>
        /// The FormatDate
        /// </summary>
        /// <param name="value">The value<see cref="object?"/></param>
        /// <returns>The <see cref="string"/></returns>
        private string FormatDate(object? value) {
            if (value == null || value == DBNull.Value)
                return string.Empty;

            if (DateTime.TryParse(value.ToString(), out DateTime dt))
                return dt.ToString("dd.MM.yyyy HH:mm:ss");

            return value.ToString() ?? string.Empty;
        }

        /// <summary>
        /// The ToInt
        /// </summary>
        /// <param name="value">The value<see cref="object?"/></param>
        /// <returns>The <see cref="int"/></returns>
        private int ToInt(object? value) {
            if (value == null || value == DBNull.Value)
                return 0;

            int.TryParse(value.ToString(), out int result);
            return result;
        }

        /// <summary>
        /// The ToBool
        /// </summary>
        /// <param name="value">The value<see cref="object?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private bool ToBool(object? value) {
            string text = Nz(value);

            return text == "True" || text == "1";
        }

        /// <summary>
        /// The SetDoubleBuffered
        /// </summary>
        /// <param name="control">The control<see cref="Control"/></param>
        /// <param name="enabled">The enabled<see cref="bool"/></param>
        private void SetDoubleBuffered(Control control, bool enabled) {
            try {
                typeof(Control)
                    .GetProperty("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(control, enabled, null);
            } catch {
            }
        }

        /// <summary>
        /// The dataGridView1_CellClick
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="DataGridViewCellEventArgs"/></param>
        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e) {
            if (e.RowIndex < 0)
                return;

            if (e.ColumnIndex == dataGridView1.Columns[ColExpand].Index)
                ToggleRow(e.RowIndex);
        }

        /// <summary>
        /// The dataGridView1_CellDoubleClick
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="DataGridViewCellEventArgs"/></param>
        private void dataGridView1_CellDoubleClick(object sender, DataGridViewCellEventArgs e) {
            if (e.RowIndex < 0)
                return;

            DataGridViewRow row = dataGridView1.Rows[e.RowIndex];

            if (IsDetailRow(row))
                return;

            ToggleRow(e.RowIndex);
        }

        /// <summary>
        /// The dataGridView1_Resize
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void dataGridView1_Resize(object sender, EventArgs e) {
            AdjustAllDetailRowHeights();
        }

        /// <summary>
        /// The ModemRefresh_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private async void ModemRefresh_Click(object sender, EventArgs e) {
            try {
                ModemRefresh.Enabled = false;
                SIMKartePrüfen.Enabled = false;
                Cursor = Cursors.WaitCursor;

                await Task.Run(() => RebuildAllGsmTables());

                LoadStatusRows(true);
                Logger?.Info("GSM-Tabellen wurden geleert und komplett neu aufgebaut.");
            } catch (Exception ex) {
                Logger?.Error("Fehler beim Neuerstellen der GSM-Daten: " + ex.Message);
                Logger?.Error($"GSM-Geräte UI Fehler | Fehler={ex.Message}");
            } finally {
                Cursor = Cursors.Default;
                ModemRefresh.Enabled = true;
                SIMKartePrüfen.Enabled = true;
            }
        }

        /// <summary>
        /// The SIMKartePrüfen_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private async void SIMKartePrüfen_Click(object sender, EventArgs e) {
            try {
                ModemRefresh.Enabled = false;
                SIMKartePrüfen.Enabled = false;
                Cursor = Cursors.WaitCursor;

                GuidCheckResult result = await Task.Run(() => ValidateModemSimStatusGuids());

                LoadStatusRows(false);

                Logger?.Info($"Modem/SIM GUID-Prüfung | Geprüft={result.CheckedRows} | Korrekt={result.ValidRows} | Abweichend={result.InvalidRows}");
            } catch (Exception ex) {
                Logger?.Error("Fehler bei der GUID-Prüfung: " + ex.Message);
                Logger?.Error($"GSM-Geräte UI Fehler | Fehler={ex.Message}");
            } finally {
                Cursor = Cursors.Default;
                ModemRefresh.Enabled = true;
                SIMKartePrüfen.Enabled = true;
            }
        }

        /// <summary>
        /// The RebuildAllGsmTables
        /// </summary>
        private void RebuildAllGsmTables() {
            Logger?.Info("GSM-Neuaufbau gestartet | Lösche TK_GSM_ModemSimStatus, TK_GSM_Sim und TK_GSM_Modems.");

            TKSQL_Commands commands = new TKSQL_Commands(Logger);
            commands.ResetGsmTablesForRefresh();

            TKGSM_DeviceRegister register = new TKGSM_DeviceRegister(Logger);
            register.Execute();

            Logger?.Info("GSM-Neuaufbau abgeschlossen.");
        }

        /// <summary>
        /// The ValidateModemSimStatusGuids
        /// </summary>
        /// <returns>The <see cref="GuidCheckResult"/></returns>
        private GuidCheckResult ValidateModemSimStatusGuids() {
            GuidCheckResult result = new GuidCheckResult();

            Logger?.Info("GUID-Prüfung für TK_GSM_ModemSimStatus gestartet.");

            TKSQL_Commands commands = new TKSQL_Commands(Logger);
            DataTable dt = commands.GetGsmModemSimStatusGuidCheckTable();

            foreach (DataRow row in dt.Rows) {
                result.CheckedRows++;

                int statusId = Convert.ToInt32(row["ID"]);
                int modemId = Convert.ToInt32(row["ModemID"]);
                string modemName = row["ModemName"] == DBNull.Value ? string.Empty : row["ModemName"].ToString() ?? string.Empty;
                string telefonnummer = row["Telefonnummer"] == DBNull.Value ? string.Empty : row["Telefonnummer"].ToString() ?? string.Empty;

                int? simId = row["SimID"] == DBNull.Value ? null : Convert.ToInt32(row["SimID"]);
                Guid currentGuid = (Guid)row["CurrentGuid"];
                Guid expectedGuid = (Guid)row["ExpectedGuid"];

                if (currentGuid == expectedGuid) {
                    result.ValidRows++;
                    Logger?.Info(
                        $"GUID OK | StatusID={statusId} | ModemID={modemId} | SimID={(simId.HasValue ? simId.Value.ToString() : "NULL")} | Modem={modemName} | Telefon={telefonnummer} | GUID={currentGuid}"
                    );
                } else {
                    result.InvalidRows++;
                    Logger?.Warn(
                        $"GUID ABWEICHUNG | StatusID={statusId} | ModemID={modemId} | SimID={(simId.HasValue ? simId.Value.ToString() : "NULL")} | Modem={modemName} | Telefon={telefonnummer} | Ist={currentGuid} | Soll={expectedGuid}"
                    );
                }
            }

            if (result.CheckedRows == 0)
                Logger?.Warn("GUID-Prüfung beendet | Keine Statuszeilen vorhanden.");
            else
                Logger?.Info($"GUID-Prüfung beendet | Geprüft={result.CheckedRows} | Korrekt={result.ValidRows} | Abweichend={result.InvalidRows}");

            return result;
        }

        /// <summary>
        /// Defines the <see cref="GuidCheckResult" />
        /// </summary>
        private sealed class GuidCheckResult {
            /// <summary>
            /// Gets or sets the CheckedRows
            /// </summary>
            public int CheckedRows { get; set; }

            /// <summary>
            /// Gets or sets the ValidRows
            /// </summary>
            public int ValidRows { get; set; }

            /// <summary>
            /// Gets or sets the InvalidRows
            /// </summary>
            public int InvalidRows { get; set; }
        }

        /// <summary>
        /// The GSMServiceStarten_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void GSMServiceStarten_Click(object sender, EventArgs e) {
        }

        /// <summary>
        /// The GSMServiceStoppen_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void GSMServiceStoppen_Click(object sender, EventArgs e) {
        }

        /// <summary>
        /// The OnFormClosing
        /// </summary>
        /// <param name="e">The e<see cref="FormClosingEventArgs"/></param>
        protected override void OnFormClosing(FormClosingEventArgs e) {
            //_refreshTimer.Stop();
            //_refreshTimer.Dispose();
            base.OnFormClosing(e);
        }
    }
}
