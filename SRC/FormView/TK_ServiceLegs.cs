namespace TKServer.FormView {
    using System;
    using System.Data;
    using System.Text;
    using System.Windows.Forms;

    using TKServer.BASE;
    using TKServer.SQL;
    /// <summary>
    /// Defines the <see cref="TK_ServiceLegs" />
    /// </summary>
    public partial class TK_ServiceLegs : Form {
        /// <summary>
        /// Gets or sets the Logger
        /// </summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public TKBase_LogManager? Logger { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TK_ServiceLegs"/> class.
        /// </summary>
        public TK_ServiceLegs() {
            InitializeComponent();
        }

        /// <summary>
        /// The SIP_ServiceLegs_Load
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void SIP_ServiceLegs_Load(object sender, EventArgs e) {
            LoadData();
        }

        /// <summary>
        /// The btnReload_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void btnReload_Click(object sender, EventArgs e) {
            LoadData();
        }

        /// <summary>
        /// The chkOnlyActive_CheckedChanged
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void chkOnlyActive_CheckedChanged(object sender, EventArgs e) {
            LoadData();
        }

        /// <summary>
        /// The dgvServiceLegs_SelectionChanged
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void dgvServiceLegs_SelectionChanged(object sender, EventArgs e) {
            UpdateDetails();
        }

        /// <summary>
        /// The LoadData
        /// </summary>
        private void LoadData() {
            try {
                DataTable dt = new TKSQL_Commands(Logger).LoadServiceLegsTable(chkOnlyActive.Checked);
                dgvServiceLegs.DataSource = dt;
                lblCount.Text = $"EintrÃ¤ge: {dt.Rows.Count}";

                ConfigureGridColumns();
                UpdateDetails();

                Logger?.Info($"Tabelle TK_SIP_ServiceLeg wurde geÃ¶ffnet. Rows={dt.Rows.Count}");
            } catch (Exception ex) {
                Logger?.Error("Es konnte keine Tabelle TK_SIP_ServiceLeg abgefragt werden! " + ex);
                Logger?.Error($"Die Service-Tabelle konnte nicht geladen werden | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The ConfigureGridColumns
        /// </summary>
        private void ConfigureGridColumns() {
            if (dgvServiceLegs.Columns.Count == 0) {
                return;
            }

            SetColumnVisible("PromptFile", false);
            SetColumnVisible("RecordTemplate", false);
            SetColumnVisible("JsonConfig", false);

            SetColumnWidth("ID", 60);
            SetColumnWidth("InternalNumber", 90);
            SetColumnWidth("DisplayName", 180);
            SetColumnWidth("ServiceCode", 90);
            SetColumnWidth("ServiceType", 120);
            SetColumnWidth("AutoAnswer", 80);
            SetColumnWidth("KeepSessionUntilPeerEnded", 150);
            SetColumnWidth("Active", 60);
            SetColumnWidth("SortOrder", 70);

            dgvServiceLegs.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgvServiceLegs.ReadOnly = true;
            dgvServiceLegs.AllowUserToAddRows = false;
            dgvServiceLegs.AllowUserToDeleteRows = false;
            dgvServiceLegs.AllowUserToResizeRows = false;
            dgvServiceLegs.MultiSelect = false;
            dgvServiceLegs.RowHeadersVisible = false;
            dgvServiceLegs.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvServiceLegs.BackgroundColor = System.Drawing.SystemColors.Window;
        }

        /// <summary>
        /// The SetColumnVisible
        /// </summary>
        /// <param name="columnName">The columnName<see cref="string"/></param>
        /// <param name="visible">The visible<see cref="bool"/></param>
        private void SetColumnVisible(string columnName, bool visible) {
            if (dgvServiceLegs.Columns.Contains(columnName)) {
                dgvServiceLegs.Columns[columnName].Visible = visible;
            }
        }

        /// <summary>
        /// The SetColumnWidth
        /// </summary>
        /// <param name="columnName">The columnName<see cref="string"/></param>
        /// <param name="width">The width<see cref="int"/></param>
        private void SetColumnWidth(string columnName, int width) {
            if (dgvServiceLegs.Columns.Contains(columnName)) {
                dgvServiceLegs.Columns[columnName].Width = width;
            }
        }

        /// <summary>
        /// The UpdateDetails
        /// </summary>
        private void UpdateDetails() {
            if (dgvServiceLegs.CurrentRow?.DataBoundItem is not DataRowView drv) {
                txtDetails.Clear();
                return;
            }

            DataRow row = drv.Row;
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"ID: {Value(row, "ID")}");
            sb.AppendLine($"InternalNumber: {Value(row, "InternalNumber")}");
            sb.AppendLine($"DisplayName: {Value(row, "DisplayName")}");
            sb.AppendLine($"Description: {Value(row, "Description")}");
            sb.AppendLine($"ServiceCode: {Value(row, "ServiceCode")}");
            sb.AppendLine($"ServiceType: {Value(row, "ServiceType")}");
            sb.AppendLine($"AutoAnswer: {Value(row, "AutoAnswer")}");
            sb.AppendLine($"KeepSessionUntilPeerEnded: {Value(row, "KeepSessionUntilPeerEnded")}");
            sb.AppendLine($"Active: {Value(row, "Active")}");
            sb.AppendLine($"SortOrder: {Value(row, "SortOrder")}");
            sb.AppendLine($"CreatedAt: {Value(row, "CreatedAt")}");
            sb.AppendLine($"UpdatedAt: {Value(row, "UpdatedAt")}");
            sb.AppendLine();
            sb.AppendLine("PromptFile:");
            sb.AppendLine(Value(row, "PromptFile"));
            sb.AppendLine();
            sb.AppendLine("RecordTemplate:");
            sb.AppendLine(Value(row, "RecordTemplate"));
            sb.AppendLine();
            sb.AppendLine("JsonConfig:");
            sb.AppendLine(Value(row, "JsonConfig"));

            txtDetails.Text = sb.ToString();
        }

        /// <summary>
        /// The Value
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="columnName">The columnName<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string Value(DataRow row, string columnName) {
            if (!row.Table.Columns.Contains(columnName)) {
                return string.Empty;
            }

            object value = row[columnName];
            return value == DBNull.Value ? string.Empty : Convert.ToString(value) ?? string.Empty;
        }
    }
}
