namespace TKServer.FormView {
    partial class TK_ServiceLegs {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support.
        /// </summary>
        private void InitializeComponent() {
            tableLayoutPanel1 = new TableLayoutPanel();
            panelTop = new Panel();
            lblCount = new Label();
            chkOnlyActive = new CheckBox();
            btnReload = new Button();
            splitContainer1 = new SplitContainer();
            dgvServiceLegs = new DataGridView();
            txtDetails = new TextBox();
            tableLayoutPanel1.SuspendLayout();
            panelTop.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvServiceLegs).BeginInit();
            SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.ColumnCount = 1;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.Controls.Add(panelTop, 0, 0);
            tableLayoutPanel1.Controls.Add(splitContainer1, 0, 1);
            tableLayoutPanel1.Dock = DockStyle.Fill;
            tableLayoutPanel1.Location = new Point(0, 0);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 2;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.Size = new Size(1100, 700);
            tableLayoutPanel1.TabIndex = 0;
            // 
            // panelTop
            // 
            panelTop.Controls.Add(lblCount);
            panelTop.Controls.Add(chkOnlyActive);
            panelTop.Controls.Add(btnReload);
            panelTop.Dock = DockStyle.Fill;
            panelTop.Location = new Point(3, 3);
            panelTop.Name = "panelTop";
            panelTop.Size = new Size(1094, 36);
            panelTop.TabIndex = 0;
            // 
            // lblCount
            // 
            lblCount.AutoSize = true;
            lblCount.Location = new Point(430, 10);
            lblCount.Name = "lblCount";
            lblCount.Size = new Size(72, 15);
            lblCount.TabIndex = 2;
            lblCount.Text = "EintrÃ¤ge: 0";
            // 
            // chkOnlyActive
            // 
            chkOnlyActive.AutoSize = true;
            chkOnlyActive.Checked = true;
            chkOnlyActive.CheckState = CheckState.Checked;
            chkOnlyActive.Location = new Point(110, 8);
            chkOnlyActive.Name = "chkOnlyActive";
            chkOnlyActive.Size = new Size(83, 19);
            chkOnlyActive.TabIndex = 1;
            chkOnlyActive.Text = "nur aktive";
            chkOnlyActive.UseVisualStyleBackColor = true;
            chkOnlyActive.CheckedChanged += chkOnlyActive_CheckedChanged;
            // 
            // btnReload
            // 
            btnReload.Location = new Point(9, 6);
            btnReload.Name = "btnReload";
            btnReload.Size = new Size(85, 23);
            btnReload.TabIndex = 0;
            btnReload.Text = "Aktualisieren";
            btnReload.UseVisualStyleBackColor = true;
            btnReload.Click += btnReload_Click;
            // 
            // splitContainer1
            // 
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.Location = new Point(3, 45);
            splitContainer1.Name = "splitContainer1";
            splitContainer1.Orientation = Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(dgvServiceLegs);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(txtDetails);
            splitContainer1.Size = new Size(1094, 652);
            splitContainer1.SplitterDistance = 380;
            splitContainer1.TabIndex = 1;
            // 
            // dgvServiceLegs
            // 
            dgvServiceLegs.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvServiceLegs.Dock = DockStyle.Fill;
            dgvServiceLegs.Location = new Point(0, 0);
            dgvServiceLegs.Name = "dgvServiceLegs";
            dgvServiceLegs.Size = new Size(1094, 380);
            dgvServiceLegs.TabIndex = 0;
            dgvServiceLegs.SelectionChanged += dgvServiceLegs_SelectionChanged;
            // 
            // txtDetails
            // 
            txtDetails.Dock = DockStyle.Fill;
            txtDetails.Font = new Font("Consolas", 9F);
            txtDetails.Location = new Point(0, 0);
            txtDetails.Multiline = true;
            txtDetails.Name = "txtDetails";
            txtDetails.ReadOnly = true;
            txtDetails.ScrollBars = ScrollBars.Both;
            txtDetails.Size = new Size(1094, 268);
            txtDetails.TabIndex = 0;
            txtDetails.WordWrap = false;
            // 
            // SIP_ServiceLegs
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1100, 700);
            Controls.Add(tableLayoutPanel1);
            Name = "SIP_ServiceLegs";
            Text = "SIP Service-Legs";
            Load += SIP_ServiceLegs_Load;
            tableLayoutPanel1.ResumeLayout(false);
            panelTop.ResumeLayout(false);
            panelTop.PerformLayout();
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvServiceLegs).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel tableLayoutPanel1;
        private Panel panelTop;
        private Label lblCount;
        private CheckBox chkOnlyActive;
        private Button btnReload;
        private SplitContainer splitContainer1;
        private DataGridView dgvServiceLegs;
        private TextBox txtDetails;
    }
}