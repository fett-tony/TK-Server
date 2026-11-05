namespace TKServer.FormView {
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Windows.Forms;

    using TKServer.BASE;
    using TKServer.SQL;
    /// <summary>
    /// Defines the <see cref="TK_NumberRouting" />
    /// </summary>
    public partial class TK_NumberRouting : Form {
        /// <summary>
        /// Gets or sets the Logger
        /// </summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public TKBase_LogManager? Logger { get; set; }

        /// <summary>
        /// Defines the _routingTable
        /// </summary>
        private readonly DataTable _routingTable = new();

        /// <summary>
        /// Defines the _externalNumbersTable
        /// </summary>
        private readonly DataTable _externalNumbersTable = new();

        /// <summary>
        /// Defines the _sipUsersTable
        /// </summary>
        private readonly DataTable _sipUsersTable = new();

        /// <summary>
        /// Defines the _externalNumbersComboTable
        /// </summary>
        private readonly DataTable _externalNumbersComboTable = new();

        /// <summary>
        /// Defines the _internalNumbersComboTable
        /// </summary>
        private readonly DataTable _internalNumbersComboTable = new();

        /// <summary>
        /// Defines the _targetTypesComboTable
        /// </summary>
        private readonly DataTable _targetTypesComboTable = new();

        /// <summary>
        /// Defines the _isLoading
        /// </summary>
        private bool _isLoading;

        /// <summary>
        /// Defines the _hasTargetIdColumn
        /// </summary>
        private bool _hasTargetIdColumn;

        /// <summary>
        /// Defines the _hasInternalNumberIdColumn
        /// </summary>
        private bool _hasInternalNumberIdColumn;

        /// <summary>
        /// Defines the _hasInterneNrColumn
        /// </summary>
        private bool _hasInterneNrColumn;

        /// <summary>
        /// Defines the _hasExterneNrColumn
        /// </summary>
        private bool _hasExterneNrColumn;

        /// <summary>
        /// Initializes a new instance of the <see cref="TK_NumberRouting"/> class.
        /// </summary>
        public TK_NumberRouting() {
            InitializeComponent();
        }

        /// <summary>
        /// The SIP_ExternalNumberRouting_Load
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void SIP_ExternalNumberRouting_Load(object sender, EventArgs e) {
            try {
                LoadLookups();
                LoadRouting();
            } catch (Exception ex) {
                Logger?.Error($"ExternalNumberRouting konnte nicht geladen werden: {ex.Message}");
                Logger?.Error($"ExternalNumberRouting Fehler | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The LoadLookups
        /// </summary>
        private void LoadLookups() {
            _externalNumbersTable.Clear();
            _sipUsersTable.Clear();

            TKSQL_Commands sql = new TKSQL_Commands(Logger);
            _externalNumbersTable.Merge(sql.LoadExternalNumbersLookupTable());
            _sipUsersTable.Merge(sql.LoadSipUsersLookupForRoutingTable());

            BuildComboTables();
        }

        /// <summary>
        /// The BuildComboTables
        /// </summary>
        private void BuildComboTables() {
            _externalNumbersComboTable.Clear();
            _internalNumbersComboTable.Clear();
            _targetTypesComboTable.Clear();

            if (!_externalNumbersComboTable.Columns.Contains("Value")) {
                _externalNumbersComboTable.Columns.Add("Value", typeof(string));
            }

            if (!_internalNumbersComboTable.Columns.Contains("Value")) {
                _internalNumbersComboTable.Columns.Add("Value", typeof(string));
            }

            if (!_targetTypesComboTable.Columns.Contains("Value")) {
                _targetTypesComboTable.Columns.Add("Value", typeof(string));
            }

            foreach (string targetType in new[] { "SIPUser", "FAXUser" }) {
                DataRow row = _targetTypesComboTable.NewRow();
                row["Value"] = targetType;
                _targetTypesComboTable.Rows.Add(row);
            }

            foreach (string number in _externalNumbersTable.Rows.Cast<DataRow>()
                .Select(r => Convert.ToString(r["NumberValue"]) ?? string.Empty)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)) {
                DataRow row = _externalNumbersComboTable.NewRow();
                row["Value"] = number;
                _externalNumbersComboTable.Rows.Add(row);
            }

            IEnumerable<string> sipNumbers = _sipUsersTable.Rows.Cast<DataRow>()
                .Select(r => Convert.ToString(r["Telefonnummer"]) ?? string.Empty);

            foreach (string number in sipNumbers
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)) {
                DataRow row = _internalNumbersComboTable.NewRow();
                row["Value"] = number;
                _internalNumbersComboTable.Rows.Add(row);
            }
        }

        /// <summary>
        /// The LoadRouting
        /// </summary>
        private void LoadRouting() {
            _isLoading = true;
            _routingTable.Clear();

            _routingTable.Merge(new TKSQL_Commands(Logger).LoadExternalNumberRoutingTable(checkBoxShowInactive.Checked));

            DetectSchema();
            EnsureDisplayColumns();
            RefreshDerivedValues();

            if (dataGridView1.DataSource == null) {
                ConfigureGrid();
                dataGridView1.DataSource = _routingTable;
            } else {
                dataGridView1.DataSource = _routingTable;
            }

            _isLoading = false;
        }

        /// <summary>
        /// The HideColumnIfExists
        /// </summary>
        /// <param name="columnName">The columnName<see cref="string"/></param>
        private void HideColumnIfExists(string columnName) {
            if (dataGridView1 == null || dataGridView1.Columns == null) {
                return;
            }

            if (dataGridView1.Columns.Contains(columnName)) {
                dataGridView1.Columns[columnName].Visible = false;
            }
        }

        /// <summary>
        /// The DetectSchema
        /// </summary>
        private void DetectSchema() {
            _hasTargetIdColumn = _routingTable.Columns.Contains("TargetID");
            _hasInternalNumberIdColumn = _routingTable.Columns.Contains("InternalNumberID");
            _hasInterneNrColumn = _routingTable.Columns.Contains("InterneNr");
            _hasExterneNrColumn = _routingTable.Columns.Contains("ExterneNr");
        }

        /// <summary>
        /// The EnsureDisplayColumns
        /// </summary>
        private void EnsureDisplayColumns() {
            if (!_routingTable.Columns.Contains("TargetInfo")) {
                _routingTable.Columns.Add("TargetInfo", typeof(string));
            }
        }

        /// <summary>
        /// The ConfigureGrid
        /// </summary>
        private void ConfigureGrid() {
            dataGridView1.AutoGenerateColumns = false;
            dataGridView1.Columns.Clear();

            AddTextColumn("ID", "ID", true, 60);
            AddExternalNumberComboColumn();
            AddCheckBoxColumn("ExternalNumberActive", "Nummer aktiv", 90);
            AddCheckBoxColumn("ExternalNumberSelectable", "Wählbar", 80);
            AddTargetTypeComboColumn();
            AddInternalNumberComboColumn();
            AddTextColumn("TargetInfo", "Ziel (Info)", true, 220);
            AddNumericColumn("Priority", "Priorität", 90);
            AddCheckBoxColumn("Active", "Aktiv", 70);

            HideColumnIfExists("ExternalNumberID");
            HideColumnIfExists("ExterneNr");
            HideColumnIfExists("ExternalNumberSourceType");
            HideColumnIfExists("ExternalNumberSourceID");
            HideColumnIfExists("TargetID");
            HideColumnIfExists("InternalNumberID");
            HideColumnIfExists("RestorePriority");
            HideColumnIfExists("InterneNr");
            HideColumnIfExists("CreatedAt");
            HideColumnIfExists("UpdatedAt");

            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }

        /// <summary>
        /// The GetInternalIdColumnName
        /// </summary>
        /// <returns>The <see cref="string"/></returns>
        private string GetInternalIdColumnName() {
            if (_hasInternalNumberIdColumn) {
                return "InternalNumberID";
            }

            return _hasTargetIdColumn ? "TargetID" : "InternalNumberID";
        }

        /// <summary>
        /// The AddExternalNumberComboColumn
        /// </summary>
        private void AddExternalNumberComboColumn() {
            string dataPropertyName = _hasExterneNrColumn ? "ExterneNr" : string.Empty;
            if (string.IsNullOrWhiteSpace(dataPropertyName) || !_routingTable.Columns.Contains(dataPropertyName)) {
                AddTextColumn("ExternalNumberID", "Externe Nummer", false, 160);
                return;
            }

            DataGridViewComboBoxColumn col = new() {
                DataPropertyName = dataPropertyName,
                Name = "colExterneNr",
                HeaderText = "Externe Nummer",
                DisplayMember = "Value",
                ValueMember = "Value",
                DataSource = _externalNumbersComboTable,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
                FlatStyle = FlatStyle.Standard,
                MinimumWidth = 160
            };

            dataGridView1.Columns.Add(col);
        }

        /// <summary>
        /// The AddTargetTypeComboColumn
        /// </summary>
        private void AddTargetTypeComboColumn() {
            if (!_routingTable.Columns.Contains("TargetType")) {
                return;
            }

            DataGridViewComboBoxColumn col = new() {
                DataPropertyName = "TargetType",
                Name = "colTargetType",
                HeaderText = "Zieltyp",
                DisplayMember = "Value",
                ValueMember = "Value",
                DataSource = _targetTypesComboTable,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
                FlatStyle = FlatStyle.Standard,
                MinimumWidth = 100
            };

            dataGridView1.Columns.Add(col);
        }

        /// <summary>
        /// The AddInternalNumberComboColumn
        /// </summary>
        private void AddInternalNumberComboColumn() {
            string dataPropertyName = _hasInterneNrColumn ? "InterneNr" : string.Empty;
            if (string.IsNullOrWhiteSpace(dataPropertyName) || !_routingTable.Columns.Contains(dataPropertyName)) {
                AddTextColumn(GetInternalIdColumnName(), "Interne Nummer", false, 140);
                return;
            }

            DataGridViewComboBoxColumn col = new() {
                DataPropertyName = dataPropertyName,
                Name = "colInterneNr",
                HeaderText = "Interne Nummer",
                DisplayMember = "Value",
                ValueMember = "Value",
                DataSource = _internalNumbersComboTable,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
                FlatStyle = FlatStyle.Standard,
                MinimumWidth = 140
            };

            dataGridView1.Columns.Add(col);
        }

        /// <summary>
        /// The AddTextColumn
        /// </summary>
        /// <param name="dataPropertyName">The dataPropertyName<see cref="string"/></param>
        /// <param name="headerText">The headerText<see cref="string"/></param>
        /// <param name="readOnly">The readOnly<see cref="bool"/></param>
        /// <param name="width">The width<see cref="int"/></param>
        private void AddTextColumn(string dataPropertyName, string headerText, bool readOnly, int width) {
            if (!_routingTable.Columns.Contains(dataPropertyName)) {
                return;
            }

            DataGridViewTextBoxColumn col = new() {
                DataPropertyName = dataPropertyName,
                Name = dataPropertyName,
                HeaderText = headerText,
                ReadOnly = readOnly,
                MinimumWidth = width
            };

            dataGridView1.Columns.Add(col);
        }

        /// <summary>
        /// The AddNumericColumn
        /// </summary>
        /// <param name="dataPropertyName">The dataPropertyName<see cref="string"/></param>
        /// <param name="headerText">The headerText<see cref="string"/></param>
        /// <param name="width">The width<see cref="int"/></param>
        private void AddNumericColumn(string dataPropertyName, string headerText, int width) {
            if (!_routingTable.Columns.Contains(dataPropertyName)) {
                return;
            }

            DataGridViewTextBoxColumn col = new() {
                DataPropertyName = dataPropertyName,
                Name = dataPropertyName,
                HeaderText = headerText,
                MinimumWidth = width,
                ValueType = typeof(int)
            };

            dataGridView1.Columns.Add(col);
        }

        /// <summary>
        /// The AddCheckBoxColumn
        /// </summary>
        /// <param name="dataPropertyName">The dataPropertyName<see cref="string"/></param>
        /// <param name="headerText">The headerText<see cref="string"/></param>
        /// <param name="width">The width<see cref="int"/></param>
        private void AddCheckBoxColumn(string dataPropertyName, string headerText, int width) {
            if (!_routingTable.Columns.Contains(dataPropertyName)) {
                return;
            }

            DataGridViewCheckBoxColumn col = new() {
                DataPropertyName = dataPropertyName,
                Name = dataPropertyName,
                HeaderText = headerText,
                MinimumWidth = width
            };

            dataGridView1.Columns.Add(col);
        }

        /// <summary>
        /// The RefreshDerivedValues
        /// </summary>
        private void RefreshDerivedValues() {
            foreach (DataRow row in _routingTable.Rows.Cast<DataRow>().Where(r => r.RowState != DataRowState.Deleted)) {
                ApplyDerivedValues(row, false);
            }
        }

        /// <summary>
        /// The ApplyDerivedValues
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="allowNumberBackfill">The allowNumberBackfill<see cref="bool"/></param>
        private void ApplyDerivedValues(DataRow row, bool allowNumberBackfill) {
            string targetType = NormalizeTargetType(Convert.ToString(row["TargetType"]) ?? string.Empty);
            if (_routingTable.Columns.Contains("TargetType") && string.IsNullOrWhiteSpace(Convert.ToString(row["TargetType"]))) {
                row["TargetType"] = targetType;
            }

            DataRow? externalRow = ResolveExternalNumberRow(row, allowNumberBackfill);
            DataRow? targetRow = ResolveTargetRow(row, targetType, allowNumberBackfill);

            string externNr = externalRow == null ? (GetSafeString(row, _hasExterneNrColumn ? "ExterneNr" : "ExternalNumberID")) : Convert.ToString(externalRow["NumberValue"]) ?? string.Empty;
            string internNr = targetRow == null
                ? GetSafeString(row, _hasInterneNrColumn ? "InterneNr" : GetInternalIdColumnName())
                : Convert.ToString(targetRow["Telefonnummer"]) ?? string.Empty;

            if (_hasExterneNrColumn && externalRow != null) {
                row["ExterneNr"] = externNr;
            }

            if (_hasInterneNrColumn && targetRow != null) {
                row["InterneNr"] = internNr;
            }

            if (externalRow != null) {
                row["ExternalNumberID"] = Convert.ToInt32(externalRow["ID"]);
            }

            string internalColumnName = GetInternalIdColumnName();
            if (targetRow != null && _routingTable.Columns.Contains(internalColumnName)) {
                row[internalColumnName] = Convert.ToInt32(targetRow["ID"]);
            }

            row["TargetInfo"] = BuildTargetInfo(targetType, targetRow, internNr);
        }

        /// <summary>
        /// The ResolveExternalNumberRow
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="allowNumberBackfill">The allowNumberBackfill<see cref="bool"/></param>
        /// <returns>The <see cref="DataRow?"/></returns>
        private DataRow? ResolveExternalNumberRow(DataRow row, bool allowNumberBackfill) {
            int? externalNumberId = GetNullableInt(row, "ExternalNumberID");
            string externeNr = _hasExterneNrColumn ? GetSafeString(row, "ExterneNr") : string.Empty;

            if (allowNumberBackfill && !string.IsNullOrWhiteSpace(externeNr)) {
                DataRow? byNumber = FindExternalByNumber(externeNr);
                if (byNumber != null) {
                    return byNumber;
                }
            }

            if (externalNumberId.HasValue) {
                return _externalNumbersTable.Rows.Cast<DataRow>().FirstOrDefault(r => Convert.ToInt32(r["ID"]) == externalNumberId.Value);
            }

            if (!string.IsNullOrWhiteSpace(externeNr)) {
                return FindExternalByNumber(externeNr);
            }

            return null;
        }

        /// <summary>
        /// The ResolveTargetRow
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="targetType">The targetType<see cref="string"/></param>
        /// <param name="allowNumberBackfill">The allowNumberBackfill<see cref="bool"/></param>
        /// <returns>The <see cref="DataRow?"/></returns>
        private DataRow? ResolveTargetRow(DataRow row, string targetType, bool allowNumberBackfill) {
            string internalColumnName = GetInternalIdColumnName();
            int? internalId = GetNullableInt(row, internalColumnName);
            string interneNr = _hasInterneNrColumn ? GetSafeString(row, "InterneNr") : string.Empty;

            DataTable lookup = GetLookupTableForTargetType(targetType);
            if (lookup.Rows.Count == 0) {
                return null;
            }

            if (allowNumberBackfill && !string.IsNullOrWhiteSpace(interneNr)) {
                DataRow? byNumber = FindTargetByNumber(targetType, interneNr);
                if (byNumber != null) {
                    return byNumber;
                }
            }

            if (internalId.HasValue) {
                return lookup.Rows.Cast<DataRow>().FirstOrDefault(r =>
                    Convert.ToInt32(r["ID"]) == internalId.Value &&
                    string.Equals(Convert.ToString(r["UserType"]) ?? string.Empty, targetType, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(interneNr)) {
                return FindTargetByNumber(targetType, interneNr);
            }

            return null;
        }

        /// <summary>
        /// The GetLookupTableForTargetType
        /// </summary>
        /// <param name="targetType">The targetType<see cref="string"/></param>
        /// <returns>The <see cref="DataTable"/></returns>
        private DataTable GetLookupTableForTargetType(string targetType) {
            return _sipUsersTable;
        }

        /// <summary>
        /// The FindExternalByNumber
        /// </summary>
        /// <param name="number">The number<see cref="string"/></param>
        /// <returns>The <see cref="DataRow?"/></returns>
        private DataRow? FindExternalByNumber(string number) {
            string normalized = NormalizeNumber(number);
            if (string.IsNullOrWhiteSpace(normalized)) {
                return null;
            }

            return _externalNumbersTable.Rows.Cast<DataRow>()
                .FirstOrDefault(r => NormalizeNumber(Convert.ToString(r["NumberValue"]) ?? string.Empty) == normalized);
        }

        /// <summary>
        /// The FindTargetByNumber
        /// </summary>
        /// <param name="targetType">The targetType<see cref="string"/></param>
        /// <param name="number">The number<see cref="string"/></param>
        /// <returns>The <see cref="DataRow?"/></returns>
        private DataRow? FindTargetByNumber(string targetType, string number) {
            string normalized = NormalizeNumber(number);
            if (string.IsNullOrWhiteSpace(normalized)) {
                return null;
            }

            return _sipUsersTable.Rows.Cast<DataRow>()
                .FirstOrDefault(r =>
                    string.Equals(Convert.ToString(r["UserType"]) ?? string.Empty, targetType, StringComparison.OrdinalIgnoreCase) &&
                    NormalizeNumber(Convert.ToString(r["Telefonnummer"]) ?? string.Empty) == normalized);
        }

        /// <summary>
        /// The BuildTargetInfo
        /// </summary>
        /// <param name="targetType">The targetType<see cref="string"/></param>
        /// <param name="row">The row<see cref="DataRow?"/></param>
        /// <param name="fallbackNumber">The fallbackNumber<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private string BuildTargetInfo(string targetType, DataRow? row, string fallbackNumber) {
            if (row == null) {
                return string.IsNullOrWhiteSpace(fallbackNumber) ? string.Empty : $"{targetType} | {fallbackNumber}";
            }

            string telefonnummer = Convert.ToString(row["Telefonnummer"]) ?? string.Empty;
            string username = Convert.ToString(row["Username"]) ?? string.Empty;
            string sipDisplay = Convert.ToString(row["DisplayName"]) ?? string.Empty;
            return string.IsNullOrWhiteSpace(sipDisplay)
                ? $"{targetType} | {telefonnummer} - {username}"
                : $"{targetType} | {telefonnummer} - {sipDisplay} ({username})";
        }

        /// <summary>
        /// The GetNullableInt
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="columnName">The columnName<see cref="string"/></param>
        /// <returns>The <see cref="int?"/></returns>
        private int? GetNullableInt(DataRow row, string columnName) {
            if (!_routingTable.Columns.Contains(columnName)) {
                return null;
            }

            object value = row[columnName];
            if (value == DBNull.Value || value == null) {
                return null;
            }

            if (int.TryParse(Convert.ToString(value), out int parsed)) {
                return parsed;
            }

            return null;
        }

        /// <summary>
        /// The GetSafeString
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="columnName">The columnName<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private string GetSafeString(DataRow row, string columnName) {
            if (!_routingTable.Columns.Contains(columnName)) {
                return string.Empty;
            }

            return Convert.ToString(row[columnName]) ?? string.Empty;
        }

        /// <summary>
        /// The ToBool
        /// </summary>
        /// <param name="value">The value<see cref="object?"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private bool ToBool(object? value) {
            if (value == null || value == DBNull.Value) {
                return false;
            }

            return Convert.ToBoolean(value);
        }

        /// <summary>
        /// The NormalizeTargetType
        /// </summary>
        /// <param name="value">The value<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private string NormalizeTargetType(string value) {
            string trimmed = (value ?? string.Empty).Trim();
            if (string.Equals(trimmed, "FAXUser", StringComparison.OrdinalIgnoreCase)) {
                return "FAXUser";
            }

            return "SIPUser";
        }

        /// <summary>
        /// The NormalizeNumber
        /// </summary>
        /// <param name="value">The value<see cref="string"/></param>
        /// <returns>The <see cref="string"/></returns>
        private string NormalizeNumber(string value) {
            return new string((value ?? string.Empty)
                .Where(c => !char.IsWhiteSpace(c) && c != '-' && c != '/' && c != '(' && c != ')')
                .ToArray());
        }

        /// <summary>
        /// The buttonReload_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void buttonReload_Click(object sender, EventArgs e) {
            try {
                LoadLookups();
                LoadRouting();
                Logger?.Info("ExternalNumberRouting neu geladen.");
            } catch (Exception ex) {
                Logger?.Error($"ExternalNumberRouting Neu laden fehlgeschlagen: {ex.Message}");
                Logger?.Error($"ExternalNumberRouting Fehler | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The buttonAdd_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void buttonAdd_Click(object sender, EventArgs e) {
            try {
                DataRow row = _routingTable.NewRow();
                row["Priority"] = GetNextPriority();
                row["Active"] = true;
                row["TargetType"] = "SIPUser";

                if (_routingTable.Columns.Contains("ExternalNumberActive")) {
                    row["ExternalNumberActive"] = true;
                }

                if (_routingTable.Columns.Contains("ExternalNumberSelectable")) {
                    row["ExternalNumberSelectable"] = true;
                }

                if (_externalNumbersTable.Rows.Count > 0) {
                    DataRow ext = _externalNumbersTable.Rows[0];
                    row["ExternalNumberID"] = ext["ID"];
                    if (_hasExterneNrColumn) {
                        row["ExterneNr"] = Convert.ToString(ext["NumberValue"]) ?? string.Empty;
                    }

                    if (_routingTable.Columns.Contains("ExternalNumberActive") && ext.Table.Columns.Contains("Active")) {
                        row["ExternalNumberActive"] = ext["Active"];
                    }

                    if (_routingTable.Columns.Contains("ExternalNumberSelectable") && ext.Table.Columns.Contains("IsSelectable")) {
                        row["ExternalNumberSelectable"] = ext["IsSelectable"];
                    }
                }

                if (_sipUsersTable.Rows.Count > 0) {
                    DataRow sip = _sipUsersTable.Rows[0];
                    if (_routingTable.Columns.Contains(GetInternalIdColumnName())) {
                        row[GetInternalIdColumnName()] = sip["ID"];
                    }
                    if (_hasInterneNrColumn) {
                        row["InterneNr"] = Convert.ToString(sip["Telefonnummer"]) ?? string.Empty;
                    }
                }

                _routingTable.Rows.Add(row);
                ApplyDerivedValues(row, true);
                Logger?.Info("Neue ExternalNumberRouting-Zeile angelegt.");
            } catch (Exception ex) {
                Logger?.Error($"Neue Routing-Zeile konnte nicht angelegt werden: {ex.Message}");
                Logger?.Error($"ExternalNumberRouting Fehler | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The GetNextPriority
        /// </summary>
        /// <returns>The <see cref="int"/></returns>
        private int GetNextPriority() {
            IEnumerable<int> priorities = _routingTable.Rows.Cast<DataRow>()
                .Where(r => r.RowState != DataRowState.Deleted)
                .Select(r => r["Priority"] == DBNull.Value ? 0 : Convert.ToInt32(r["Priority"]));

            return priorities.Any() ? priorities.Max() + 1 : 1;
        }

        /// <summary>
        /// The buttonDelete_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void buttonDelete_Click(object sender, EventArgs e) {
            if (dataGridView1.CurrentRow?.DataBoundItem is not DataRowView view) {
                return;
            }

            Logger?.Info("ExternalNumberRouting-Zeile wird ohne blockierenden Bestätigungsdialog gelöscht.");

            try {
                view.Row.Delete();
                Logger?.Info("ExternalNumberRouting-Zeile gelöscht.");
            } catch (Exception ex) {
                Logger?.Error($"Routing-Zeile konnte nicht gelöscht werden: {ex.Message}");
                Logger?.Error($"ExternalNumberRouting Fehler | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The buttonSave_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void buttonSave_Click(object sender, EventArgs e) {
            try {
                dataGridView1.EndEdit();
                ValidateRows();
                PersistChanges();
                LoadLookups();
                LoadRouting();
                Logger?.Info("ExternalNumberRouting gespeichert.");
                Logger?.Info("Routing wurde gespeichert.");
            } catch (Exception ex) {
                Logger?.Error($"ExternalNumberRouting speichern fehlgeschlagen: {ex.Message}");
                Logger?.Error($"ExternalNumberRouting Fehler | Fehler={ex.Message}");
            }
        }

        /// <summary>
        /// The ValidateRows
        /// </summary>
        private void ValidateRows() {
            foreach (DataRow row in _routingTable.Rows.Cast<DataRow>().Where(r => r.RowState != DataRowState.Deleted)) {
                string targetType = NormalizeTargetType(Convert.ToString(row["TargetType"]) ?? string.Empty);
                row["TargetType"] = targetType;

                DataRow? externalRow = ResolveExternalNumberForSave(row);
                if (externalRow == null) {
                    throw new InvalidOperationException("Jede Zuordnung benötigt eine gültige externe Nummer.");
                }

                DataRow? targetRow = ResolveTargetForSave(row, targetType);
                if (targetRow == null) {
                    throw new InvalidOperationException($"Für TargetType '{targetType}' wurde kein gültiges internes Ziel gefunden.");
                }

                row["ExternalNumberID"] = Convert.ToInt32(externalRow["ID"]);
                if (_hasExterneNrColumn) {
                    row["ExterneNr"] = Convert.ToString(externalRow["NumberValue"]) ?? string.Empty;
                }

                string internalColumnName = GetInternalIdColumnName();
                if (_routingTable.Columns.Contains(internalColumnName)) {
                    row[internalColumnName] = Convert.ToInt32(targetRow["ID"]);
                }

                if (_hasInterneNrColumn) {
                    row["InterneNr"] = Convert.ToString(targetRow["Telefonnummer"]) ?? string.Empty;
                }

                ApplyDerivedValues(row, false);

                int priority = row["Priority"] == DBNull.Value ? 0 : Convert.ToInt32(row["Priority"]);
                if (priority <= 0) {
                    throw new InvalidOperationException("Priority muss größer 0 sein.");
                }
            }

        }

        /// <summary>
        /// Prueft, dass je internem Ziel keine Priorität doppelt belegt ist.
        /// </summary>
        private void ValidateUniquePriorities() {
            string internalColumnName = GetInternalIdColumnName();
            if (!_routingTable.Columns.Contains(internalColumnName) || !_routingTable.Columns.Contains("Priority")) {
                return;
            }

            DataRow? duplicate = _routingTable.Rows.Cast<DataRow>()
                .Where(r => r.RowState != DataRowState.Deleted)
                .GroupBy(r => new {
                    InternalNumberID = Convert.ToInt32(r[internalColumnName]),
                    Priority = Convert.ToInt32(r["Priority"])
                })
                .Where(g => g.Count() > 1)
                .Select(g => g.First())
                .FirstOrDefault();

            if (duplicate == null) {
                return;
            }

            int internalNumberId = Convert.ToInt32(duplicate[internalColumnName]);
            int priority = Convert.ToInt32(duplicate["Priority"]);
            string interneNr = _hasInterneNrColumn ? GetSafeString(duplicate, "InterneNr") : internalNumberId.ToString();
            throw new InvalidOperationException($"Priorität doppelt belegt | Intern={interneNr} | InternalNumberID={internalNumberId} | Priority={priority}");
        }

        /// <summary>
        /// The ResolveExternalNumberForSave
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <returns>The <see cref="DataRow?"/></returns>
        private DataRow? ResolveExternalNumberForSave(DataRow row) {
            string externeNr = _hasExterneNrColumn ? GetSafeString(row, "ExterneNr") : string.Empty;
            if (!string.IsNullOrWhiteSpace(externeNr)) {
                return FindExternalByNumber(externeNr);
            }

            int? externalNumberId = GetNullableInt(row, "ExternalNumberID");
            if (externalNumberId.HasValue) {
                return _externalNumbersTable.Rows.Cast<DataRow>().FirstOrDefault(r => Convert.ToInt32(r["ID"]) == externalNumberId.Value);
            }

            return null;
        }

        /// <summary>
        /// The ResolveTargetForSave
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="targetType">The targetType<see cref="string"/></param>
        /// <returns>The <see cref="DataRow?"/></returns>
        private DataRow? ResolveTargetForSave(DataRow row, string targetType) {
            string interneNr = _hasInterneNrColumn ? GetSafeString(row, "InterneNr") : string.Empty;
            if (!string.IsNullOrWhiteSpace(interneNr)) {
                return FindTargetByNumber(targetType, interneNr);
            }

            int? internalId = GetNullableInt(row, GetInternalIdColumnName());
            DataTable lookup = GetLookupTableForTargetType(targetType);
            if (internalId.HasValue) {
                return lookup.Rows.Cast<DataRow>().FirstOrDefault(r =>
                    Convert.ToInt32(r["ID"]) == internalId.Value &&
                    string.Equals(Convert.ToString(r["UserType"]) ?? string.Empty, targetType, StringComparison.OrdinalIgnoreCase));
            }

            return null;
        }

        /// <summary>
        /// The PersistChanges
        /// </summary>
        private void PersistChanges() {
            new TKSQL_Commands(Logger).SaveExternalNumberRoutingChanges(
                _routingTable,
                GetInternalIdColumnName(),
                _hasInterneNrColumn,
                _hasExterneNrColumn);
            _routingTable.AcceptChanges();
        }

        /// <summary>
        /// The dataGridView1_CurrentCellDirtyStateChanged
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void dataGridView1_CurrentCellDirtyStateChanged(object sender, EventArgs e) {
            if (dataGridView1.IsCurrentCellDirty) {
                dataGridView1.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        /// <summary>
        /// Hält die ausgeblendete RestorePriority passend zur sichtbaren Priorität.
        /// </summary>
        /// <param name="row">The row<see cref="DataRow"/></param>
        /// <param name="columnIndex">The columnIndex<see cref="int"/></param>
        private void ApplyHiddenRestorePriorityRule(DataRow row, int columnIndex) {
            if (!_routingTable.Columns.Contains("RestorePriority") || !_routingTable.Columns.Contains("Priority") || !_routingTable.Columns.Contains("Active")) {
                return;
            }

            string columnName = columnIndex >= 0 && columnIndex < dataGridView1.Columns.Count
                ? dataGridView1.Columns[columnIndex].DataPropertyName
                : string.Empty;

            if (string.Equals(columnName, "Priority", StringComparison.OrdinalIgnoreCase)) {
                row["RestorePriority"] = row["Priority"] == DBNull.Value ? 1 : Convert.ToInt32(row["Priority"]);
                return;
            }

            if (string.Equals(columnName, "Active", StringComparison.OrdinalIgnoreCase)) {
                bool active = row["Active"] != DBNull.Value && Convert.ToBoolean(row["Active"]);
                if (active && row["RestorePriority"] != DBNull.Value && Convert.ToInt32(row["RestorePriority"]) > 0) {
                    row["Priority"] = Convert.ToInt32(row["RestorePriority"]);
                } else if (!active && (row["RestorePriority"] == DBNull.Value || Convert.ToInt32(row["RestorePriority"]) <= 0)) {
                    row["RestorePriority"] = row["Priority"] == DBNull.Value ? 1 : Convert.ToInt32(row["Priority"]);
                }
            }
        }

        /// <summary>
        /// The dataGridView1_CellValueChanged
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="DataGridViewCellEventArgs"/></param>
        private void dataGridView1_CellValueChanged(object sender, DataGridViewCellEventArgs e) {
            if (_isLoading || e.RowIndex < 0) {
                return;
            }

            if (dataGridView1.Rows[e.RowIndex].DataBoundItem is DataRowView view) {
                ApplyHiddenRestorePriorityRule(view.Row, e.ColumnIndex);
                ApplyDerivedValues(view.Row, true);
            }
        }

        /// <summary>
        /// The dataGridView1_DataError
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="DataGridViewDataErrorEventArgs"/></param>
        private void dataGridView1_DataError(object sender, DataGridViewDataErrorEventArgs e) {
            e.ThrowException = false;
            Logger?.Warn($"ExternalNumberRouting DataError | Row={e.RowIndex} | Col={e.ColumnIndex} | Context={e.Context}");
        }

        /// <summary>
        /// The checkBoxShowInactive_CheckedChanged
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void checkBoxShowInactive_CheckedChanged(object sender, EventArgs e) {
            try {
                LoadRouting();
            } catch (Exception ex) {
                Logger?.Error($"ExternalNumberRouting Filterwechsel fehlgeschlagen: {ex.Message}");
                Logger?.Error($"ExternalNumberRouting Fehler | Fehler={ex.Message}");
            }
        }
    }
}
