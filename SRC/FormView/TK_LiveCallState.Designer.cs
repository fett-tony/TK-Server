namespace TKServer.FormView {
    partial class TK_LiveCallState {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent() {
            tableLayoutPanel1 = new TableLayoutPanel();
            headerPanel = new Panel();
            lblSummary = new Label();
            lblLastRefresh = new Label();
            actionPanel = new Panel();
            btnRefresh = new Button();
            btnHangup = new Button();
            checkAutoRefresh = new CheckBox();
            splitContainer1 = new SplitContainer();
            dataGridView1 = new DataGridView();
            txtDetails = new TextBox();
            tableLayoutPanel1.SuspendLayout();
            headerPanel.SuspendLayout();
            actionPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.ColumnCount = 1;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.Controls.Add(headerPanel, 0, 0);
            tableLayoutPanel1.Controls.Add(splitContainer1, 0, 1);
            tableLayoutPanel1.Dock = DockStyle.Fill;
            tableLayoutPanel1.Location = new Point(0, 0);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 2;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.Size = new Size(1080, 620);
            tableLayoutPanel1.TabIndex = 0;
            // 
            // headerPanel
            // 
            headerPanel.Controls.Add(lblSummary);
            headerPanel.Controls.Add(lblLastRefresh);
            headerPanel.Controls.Add(actionPanel);
            headerPanel.Dock = DockStyle.Fill;
            headerPanel.Location = new Point(3, 3);
            headerPanel.Name = "headerPanel";
            headerPanel.Size = new Size(1074, 58);
            headerPanel.TabIndex = 0;
            // 
            // lblSummary
            // 
            lblSummary.AutoSize = true;
            lblSummary.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblSummary.Location = new Point(16, 9);
            lblSummary.Name = "lblSummary";
            lblSummary.Size = new Size(130, 20);
            lblSummary.TabIndex = 0;
            lblSummary.Text = "Aktive Gespräche:";
            // 
            // lblLastRefresh
            // 
            lblLastRefresh.AutoSize = true;
            lblLastRefresh.Location = new Point(18, 33);
            lblLastRefresh.Name = "lblLastRefresh";
            lblLastRefresh.Size = new Size(117, 15);
            lblLastRefresh.TabIndex = 1;
            lblLastRefresh.Text = "Letzte Aktualisierung:";
            // 
            // actionPanel
            // 
            actionPanel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            actionPanel.Controls.Add(checkAutoRefresh);
            actionPanel.Controls.Add(btnHangup);
            actionPanel.Controls.Add(btnRefresh);
            actionPanel.Location = new Point(624, 7);
            actionPanel.Name = "actionPanel";
            actionPanel.Size = new Size(438, 44);
            actionPanel.TabIndex = 2;
            // 
            // btnRefresh
            // 
            btnRefresh.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point, 0);
            btnRefresh.Location = new Point(10, 7);
            btnRefresh.MaximumSize = new Size(150, 30);
            btnRefresh.MinimumSize = new Size(150, 30);
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new Size(150, 30);
            btnRefresh.TabIndex = 0;
            btnRefresh.Text = "Aktualisieren";
            btnRefresh.UseVisualStyleBackColor = true;
            // 
            // btnHangup
            // 
            btnHangup.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point, 0);
            btnHangup.Location = new Point(176, 7);
            btnHangup.MaximumSize = new Size(150, 30);
            btnHangup.MinimumSize = new Size(150, 30);
            btnHangup.Name = "btnHangup";
            btnHangup.Size = new Size(150, 30);
            btnHangup.TabIndex = 1;
            btnHangup.Text = "Gespräch beenden";
            btnHangup.UseVisualStyleBackColor = true;
            // 
            // checkAutoRefresh
            // 
            checkAutoRefresh.AutoSize = true;
            checkAutoRefresh.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
            checkAutoRefresh.Location = new Point(342, 12);
            checkAutoRefresh.Name = "checkAutoRefresh";
            checkAutoRefresh.Size = new Size(91, 19);
            checkAutoRefresh.TabIndex = 2;
            checkAutoRefresh.Text = "Auto-Refresh";
            checkAutoRefresh.UseVisualStyleBackColor = true;
            // 
            // splitContainer1
            // 
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.Location = new Point(3, 67);
            splitContainer1.Name = "splitContainer1";
            splitContainer1.Orientation = Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(dataGridView1);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(txtDetails);
            splitContainer1.Size = new Size(1074, 550);
            splitContainer1.SplitterDistance = 360;
            splitContainer1.TabIndex = 1;
            // 
            // dataGridView1
            // 
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Dock = DockStyle.Fill;
            dataGridView1.Location = new Point(0, 0);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.Size = new Size(1074, 360);
            dataGridView1.TabIndex = 0;
            // 
            // txtDetails
            // 
            txtDetails.Dock = DockStyle.Fill;
            txtDetails.Location = new Point(0, 0);
            txtDetails.Multiline = true;
            txtDetails.Name = "txtDetails";
            txtDetails.ReadOnly = true;
            txtDetails.ScrollBars = ScrollBars.Vertical;
            txtDetails.Size = new Size(1074, 186);
            txtDetails.TabIndex = 0;
            // 
            // TK_LiveCallState
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1080, 620);
            Controls.Add(tableLayoutPanel1);
            Name = "TK_LiveCallState";
            Text = "Live Call State";
            tableLayoutPanel1.ResumeLayout(false);
            headerPanel.ResumeLayout(false);
            headerPanel.PerformLayout();
            actionPanel.ResumeLayout(false);
            actionPanel.PerformLayout();
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel tableLayoutPanel1;
        private Panel headerPanel;
        private Label lblSummary;
        private Label lblLastRefresh;
        private Panel actionPanel;
        private Button btnRefresh;
        private Button btnHangup;
        private CheckBox checkAutoRefresh;
        private SplitContainer splitContainer1;
        private DataGridView dataGridView1;
        private TextBox txtDetails;
    }
}
