namespace TKServer.FormView {
    partial class TK_Start
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent() {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(TK_Start));
            mainLayout = new TableLayoutPanel();
            topPanel = new Panel();
            btnRefresh = new Button();
            lblGsmError = new Label();
            label9 = new Label();
            lblGsmBusy = new Label();
            label8 = new Label();
            lblGsmLastRun = new Label();
            label7 = new Label();
            lblGsmState = new Label();
            label6 = new Label();
            lblProviderState = new Label();
            label5 = new Label();
            lblSipState = new Label();
            label4 = new Label();
            groupInternal = new GroupBox();
            internalLayout = new TableLayoutPanel();
            internalButtonPanel = new FlowLayoutPanel();
            btnSipStart = new Button();
            btnSipStop = new Button();
            gridInternalUsers = new DataGridView();
            groupProviders = new GroupBox();
            providerLayout = new TableLayoutPanel();
            providerButtonPanel = new FlowLayoutPanel();
            btnProviderStart = new Button();
            btnProviderStop = new Button();
            btnProviderReload = new Button();
            gridProviders = new DataGridView();
            groupGsm = new GroupBox();
            gsmLayout = new TableLayoutPanel();
            gsmButtonPanel = new FlowLayoutPanel();
            btnGsmStart = new Button();
            btnGsmStop = new Button();
            btnGsmRunOnce = new Button();
            gridGsm = new DataGridView();
            txtStatus = new TextBox();
            mainLayout.SuspendLayout();
            topPanel.SuspendLayout();
            groupInternal.SuspendLayout();
            internalLayout.SuspendLayout();
            internalButtonPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridInternalUsers).BeginInit();
            groupProviders.SuspendLayout();
            providerLayout.SuspendLayout();
            providerButtonPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridProviders).BeginInit();
            groupGsm.SuspendLayout();
            gsmLayout.SuspendLayout();
            gsmButtonPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridGsm).BeginInit();
            SuspendLayout();
            // 
            // mainLayout
            // 
            mainLayout.ColumnCount = 2;
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            mainLayout.Controls.Add(topPanel, 1, 0);
            mainLayout.Controls.Add(groupInternal, 0, 0);
            mainLayout.Controls.Add(groupProviders, 0, 1);
            mainLayout.Controls.Add(groupGsm, 0, 2);
            mainLayout.Dock = DockStyle.Fill;
            mainLayout.Location = new Point(0, 0);
            mainLayout.Margin = new Padding(3, 4, 3, 4);
            mainLayout.Name = "mainLayout";
            mainLayout.RowCount = 3;
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33334F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3333359F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3333321F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 27F));
            mainLayout.Size = new Size(1125, 805);
            mainLayout.TabIndex = 0;
            // 
            // topPanel
            // 
            topPanel.Controls.Add(btnRefresh);
            topPanel.Controls.Add(lblGsmError);
            topPanel.Controls.Add(label9);
            topPanel.Controls.Add(lblGsmBusy);
            topPanel.Controls.Add(label8);
            topPanel.Controls.Add(lblGsmLastRun);
            topPanel.Controls.Add(label7);
            topPanel.Controls.Add(lblGsmState);
            topPanel.Controls.Add(label6);
            topPanel.Controls.Add(lblProviderState);
            topPanel.Controls.Add(label5);
            topPanel.Controls.Add(lblSipState);
            topPanel.Controls.Add(label4);
            topPanel.Dock = DockStyle.Fill;
            topPanel.Location = new Point(846, 4);
            topPanel.Margin = new Padding(3, 4, 3, 4);
            topPanel.Name = "topPanel";
            mainLayout.SetRowSpan(topPanel, 3);
            topPanel.Size = new Size(276, 528);
            topPanel.TabIndex = 0;
            // 
            // btnRefresh
            // 
            btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnRefresh.Location = new Point(59, 461);
            btnRefresh.Margin = new Padding(3, 4, 3, 4);
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new Size(126, 45);
            btnRefresh.TabIndex = 0;
            btnRefresh.Text = "Aktualisieren";
            // 
            // lblGsmError
            // 
            lblGsmError.AutoEllipsis = true;
            lblGsmError.Location = new Point(22, 436);
            lblGsmError.Name = "lblGsmError";
            lblGsmError.Size = new Size(226, 84);
            lblGsmError.TabIndex = 1;
            lblGsmError.Text = "-";
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            label9.Location = new Point(22, 399);
            label9.Name = "label9";
            label9.Size = new Size(52, 20);
            label9.TabIndex = 2;
            label9.Text = "Fehler";
            // 
            // lblGsmBusy
            // 
            lblGsmBusy.AutoSize = true;
            lblGsmBusy.Location = new Point(22, 361);
            lblGsmBusy.Name = "lblGsmBusy";
            lblGsmBusy.Size = new Size(15, 20);
            lblGsmBusy.TabIndex = 3;
            lblGsmBusy.Text = "-";
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            label8.Location = new Point(22, 324);
            label8.Name = "label8";
            label8.Size = new Size(43, 20);
            label8.TabIndex = 4;
            label8.Text = "Busy";
            // 
            // lblGsmLastRun
            // 
            lblGsmLastRun.AutoSize = true;
            lblGsmLastRun.Location = new Point(22, 137);
            lblGsmLastRun.Name = "lblGsmLastRun";
            lblGsmLastRun.Size = new Size(15, 20);
            lblGsmLastRun.TabIndex = 5;
            lblGsmLastRun.Text = "-";
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            label7.Location = new Point(22, 100);
            label7.Name = "label7";
            label7.Size = new Size(132, 20);
            label7.TabIndex = 6;
            label7.Text = "Letzter GSM-Lauf";
            // 
            // lblGsmState
            // 
            lblGsmState.AutoSize = true;
            lblGsmState.Location = new Point(22, 212);
            lblGsmState.Name = "lblGsmState";
            lblGsmState.Size = new Size(15, 20);
            lblGsmState.TabIndex = 7;
            lblGsmState.Text = "-";
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            label6.Location = new Point(22, 175);
            label6.Name = "label6";
            label6.Size = new Size(93, 20);
            label6.TabIndex = 8;
            label6.Text = "GSM-Dienst";
            // 
            // lblProviderState
            // 
            lblProviderState.AutoSize = true;
            lblProviderState.Location = new Point(22, 63);
            lblProviderState.Name = "lblProviderState";
            lblProviderState.Size = new Size(15, 20);
            lblProviderState.TabIndex = 9;
            lblProviderState.Text = "-";
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            label5.Location = new Point(22, 25);
            label5.Name = "label5";
            label5.Size = new Size(135, 20);
            label5.TabIndex = 10;
            label5.Text = "Provider-SIP-Regs";
            // 
            // lblSipState
            // 
            lblSipState.AutoSize = true;
            lblSipState.Location = new Point(22, 287);
            lblSipState.Name = "lblSipState";
            lblSipState.Size = new Size(15, 20);
            lblSipState.TabIndex = 11;
            lblSipState.Text = "-";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            label4.Location = new Point(22, 249);
            label4.Name = "label4";
            label4.Size = new Size(82, 20);
            label4.TabIndex = 12;
            label4.Text = "SIPInt";
            // 
            // groupInternal
            // 
            groupInternal.Controls.Add(internalLayout);
            groupInternal.Dock = DockStyle.Fill;
            groupInternal.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            groupInternal.Location = new Point(3, 4);
            groupInternal.Margin = new Padding(3, 4, 3, 4);
            groupInternal.Name = "groupInternal";
            groupInternal.Padding = new Padding(3, 4, 3, 4);
            groupInternal.Size = new Size(837, 260);
            groupInternal.TabIndex = 1;
            groupInternal.TabStop = false;
            groupInternal.Text = "Interne SIP-User";
            // 
            // internalLayout
            // 
            internalLayout.ColumnCount = 1;
            internalLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            internalLayout.Controls.Add(internalButtonPanel, 0, 0);
            internalLayout.Controls.Add(gridInternalUsers, 0, 1);
            internalLayout.Dock = DockStyle.Fill;
            internalLayout.Location = new Point(3, 24);
            internalLayout.Margin = new Padding(3, 4, 3, 4);
            internalLayout.Name = "internalLayout";
            internalLayout.RowCount = 2;
            internalLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
            internalLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            internalLayout.Size = new Size(831, 232);
            internalLayout.TabIndex = 0;
            // 
            // internalButtonPanel
            // 
            internalButtonPanel.Controls.Add(btnSipStart);
            internalButtonPanel.Controls.Add(btnSipStop);
            internalButtonPanel.Dock = DockStyle.Fill;
            internalButtonPanel.Location = new Point(3, 4);
            internalButtonPanel.Margin = new Padding(3, 4, 3, 4);
            internalButtonPanel.Name = "internalButtonPanel";
            internalButtonPanel.Size = new Size(825, 48);
            internalButtonPanel.TabIndex = 0;
            // 
            // btnSipStart
            // 
            btnSipStart.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnSipStart.Location = new Point(3, 4);
            btnSipStart.Margin = new Padding(3, 4, 3, 4);
            btnSipStart.MaximumSize = new Size(171, 40);
            btnSipStart.MinimumSize = new Size(171, 40);
            btnSipStart.Name = "btnSipStart";
            btnSipStart.Size = new Size(171, 40);
            btnSipStart.TabIndex = 0;
            btnSipStart.Text = "SIPInt starten";
            // 
            // btnSipStop
            // 
            btnSipStop.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnSipStop.Location = new Point(180, 4);
            btnSipStop.Margin = new Padding(3, 4, 3, 4);
            btnSipStop.MaximumSize = new Size(171, 40);
            btnSipStop.MinimumSize = new Size(171, 40);
            btnSipStop.Name = "btnSipStop";
            btnSipStop.Size = new Size(171, 40);
            btnSipStop.TabIndex = 1;
            btnSipStop.Text = "SIPInt stoppen";
            // 
            // gridInternalUsers
            // 
            gridInternalUsers.ColumnHeadersHeight = 29;
            gridInternalUsers.Dock = DockStyle.Fill;
            gridInternalUsers.Location = new Point(3, 60);
            gridInternalUsers.Margin = new Padding(3, 4, 3, 4);
            gridInternalUsers.Name = "gridInternalUsers";
            gridInternalUsers.RowHeadersWidth = 51;
            gridInternalUsers.Size = new Size(825, 168);
            gridInternalUsers.TabIndex = 1;
            // 
            // groupProviders
            // 
            groupProviders.Controls.Add(providerLayout);
            groupProviders.Dock = DockStyle.Fill;
            groupProviders.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            groupProviders.Location = new Point(3, 272);
            groupProviders.Margin = new Padding(3, 4, 3, 4);
            groupProviders.Name = "groupProviders";
            groupProviders.Padding = new Padding(3, 4, 3, 4);
            groupProviders.Size = new Size(837, 260);
            groupProviders.TabIndex = 2;
            groupProviders.TabStop = false;
            groupProviders.Text = "Provider-SIP-Konten";
            // 
            // providerLayout
            // 
            providerLayout.ColumnCount = 1;
            providerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            providerLayout.Controls.Add(providerButtonPanel, 0, 0);
            providerLayout.Controls.Add(gridProviders, 0, 1);
            providerLayout.Dock = DockStyle.Fill;
            providerLayout.Location = new Point(3, 24);
            providerLayout.Margin = new Padding(3, 4, 3, 4);
            providerLayout.Name = "providerLayout";
            providerLayout.RowCount = 2;
            providerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
            providerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            providerLayout.Size = new Size(831, 232);
            providerLayout.TabIndex = 0;
            // 
            // providerButtonPanel
            // 
            providerButtonPanel.Controls.Add(btnProviderStart);
            providerButtonPanel.Controls.Add(btnProviderStop);
            providerButtonPanel.Controls.Add(btnProviderReload);
            providerButtonPanel.Dock = DockStyle.Fill;
            providerButtonPanel.Location = new Point(3, 4);
            providerButtonPanel.Margin = new Padding(3, 4, 3, 4);
            providerButtonPanel.Name = "providerButtonPanel";
            providerButtonPanel.Size = new Size(825, 48);
            providerButtonPanel.TabIndex = 0;
            // 
            // btnProviderStart
            // 
            btnProviderStart.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnProviderStart.Location = new Point(3, 4);
            btnProviderStart.Margin = new Padding(3, 4, 3, 4);
            btnProviderStart.MaximumSize = new Size(181, 40);
            btnProviderStart.MinimumSize = new Size(181, 40);
            btnProviderStart.Name = "btnProviderStart";
            btnProviderStart.Size = new Size(181, 40);
            btnProviderStart.TabIndex = 0;
            btnProviderStart.Text = "Registrierung starten";
            // 
            // btnProviderStop
            // 
            btnProviderStop.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnProviderStop.Location = new Point(190, 4);
            btnProviderStop.Margin = new Padding(3, 4, 3, 4);
            btnProviderStop.MaximumSize = new Size(181, 40);
            btnProviderStop.MinimumSize = new Size(181, 40);
            btnProviderStop.Name = "btnProviderStop";
            btnProviderStop.Size = new Size(181, 40);
            btnProviderStop.TabIndex = 1;
            btnProviderStop.Text = "Registrierung stoppen";
            // 
            // btnProviderReload
            // 
            btnProviderReload.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnProviderReload.Location = new Point(377, 4);
            btnProviderReload.Margin = new Padding(3, 4, 3, 4);
            btnProviderReload.MaximumSize = new Size(181, 40);
            btnProviderReload.MinimumSize = new Size(181, 40);
            btnProviderReload.Name = "btnProviderReload";
            btnProviderReload.Size = new Size(181, 40);
            btnProviderReload.TabIndex = 2;
            btnProviderReload.Text = "Neu laden";
            // 
            // gridProviders
            // 
            gridProviders.ColumnHeadersHeight = 29;
            gridProviders.Dock = DockStyle.Fill;
            gridProviders.Location = new Point(3, 60);
            gridProviders.Margin = new Padding(3, 4, 3, 4);
            gridProviders.Name = "gridProviders";
            gridProviders.RowHeadersWidth = 51;
            gridProviders.Size = new Size(825, 168);
            gridProviders.TabIndex = 1;
            // 
            // groupGsm
            // 
            groupGsm.Controls.Add(gsmLayout);
            groupGsm.Dock = DockStyle.Fill;
            groupGsm.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            groupGsm.Location = new Point(3, 540);
            groupGsm.Margin = new Padding(3, 4, 3, 4);
            groupGsm.Name = "groupGsm";
            groupGsm.Padding = new Padding(3, 4, 3, 4);
            groupGsm.Size = new Size(837, 261);
            groupGsm.TabIndex = 3;
            groupGsm.TabStop = false;
            groupGsm.Text = "GSM-Modems / SIM";
            // 
            // gsmLayout
            // 
            gsmLayout.ColumnCount = 1;
            gsmLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            gsmLayout.Controls.Add(gsmButtonPanel, 0, 0);
            gsmLayout.Controls.Add(gridGsm, 0, 1);
            gsmLayout.Dock = DockStyle.Fill;
            gsmLayout.Location = new Point(3, 24);
            gsmLayout.Margin = new Padding(3, 4, 3, 4);
            gsmLayout.Name = "gsmLayout";
            gsmLayout.RowCount = 2;
            gsmLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
            gsmLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            gsmLayout.Size = new Size(831, 233);
            gsmLayout.TabIndex = 0;
            // 
            // gsmButtonPanel
            // 
            gsmButtonPanel.Controls.Add(btnGsmStart);
            gsmButtonPanel.Controls.Add(btnGsmStop);
            gsmButtonPanel.Controls.Add(btnGsmRunOnce);
            gsmButtonPanel.Dock = DockStyle.Fill;
            gsmButtonPanel.Location = new Point(3, 4);
            gsmButtonPanel.Margin = new Padding(3, 4, 3, 4);
            gsmButtonPanel.Name = "gsmButtonPanel";
            gsmButtonPanel.Size = new Size(825, 48);
            gsmButtonPanel.TabIndex = 0;
            // 
            // btnGsmStart
            // 
            btnGsmStart.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnGsmStart.Location = new Point(3, 4);
            btnGsmStart.Margin = new Padding(3, 4, 3, 4);
            btnGsmStart.MaximumSize = new Size(171, 40);
            btnGsmStart.MinimumSize = new Size(171, 40);
            btnGsmStart.Name = "btnGsmStart";
            btnGsmStart.Size = new Size(171, 40);
            btnGsmStart.TabIndex = 0;
            btnGsmStart.Text = "Dienst starten";
            // 
            // btnGsmStop
            // 
            btnGsmStop.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnGsmStop.Location = new Point(180, 4);
            btnGsmStop.Margin = new Padding(3, 4, 3, 4);
            btnGsmStop.MaximumSize = new Size(171, 40);
            btnGsmStop.MinimumSize = new Size(171, 40);
            btnGsmStop.Name = "btnGsmStop";
            btnGsmStop.Size = new Size(171, 40);
            btnGsmStop.TabIndex = 1;
            btnGsmStop.Text = "Dienst stoppen";
            // 
            // btnGsmRunOnce
            // 
            btnGsmRunOnce.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnGsmRunOnce.Location = new Point(357, 4);
            btnGsmRunOnce.Margin = new Padding(3, 4, 3, 4);
            btnGsmRunOnce.MaximumSize = new Size(171, 40);
            btnGsmRunOnce.MinimumSize = new Size(171, 40);
            btnGsmRunOnce.Name = "btnGsmRunOnce";
            btnGsmRunOnce.Size = new Size(171, 40);
            btnGsmRunOnce.TabIndex = 2;
            btnGsmRunOnce.Text = "Einmal ausführen";
            // 
            // gridGsm
            // 
            gridGsm.ColumnHeadersHeight = 29;
            gridGsm.Dock = DockStyle.Fill;
            gridGsm.Location = new Point(3, 60);
            gridGsm.Margin = new Padding(3, 4, 3, 4);
            gridGsm.Name = "gridGsm";
            gridGsm.RowHeadersWidth = 51;
            gridGsm.Size = new Size(825, 169);
            gridGsm.TabIndex = 1;
            // 
            // txtStatus
            // 
            txtStatus.Dock = DockStyle.Fill;
            txtStatus.Visible = false;
            txtStatus.Location = new Point(846, 540);
            txtStatus.Margin = new Padding(3, 4, 3, 4);
            txtStatus.Multiline = true;
            txtStatus.Name = "txtStatus";
            txtStatus.ReadOnly = true;
            txtStatus.ScrollBars = ScrollBars.Vertical;
            txtStatus.Size = new Size(276, 261);
            txtStatus.TabIndex = 4;
            // 
            // TK_Start
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1125, 805);
            Controls.Add(mainLayout);
            Margin = new Padding(3, 4, 3, 4);
            Name = "TK_Start";
            mainLayout.ResumeLayout(false);
            mainLayout.PerformLayout();
            topPanel.ResumeLayout(false);
            topPanel.PerformLayout();
            groupInternal.ResumeLayout(false);
            internalLayout.ResumeLayout(false);
            internalButtonPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)gridInternalUsers).EndInit();
            groupProviders.ResumeLayout(false);
            providerLayout.ResumeLayout(false);
            providerButtonPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)gridProviders).EndInit();
            groupGsm.ResumeLayout(false);
            gsmLayout.ResumeLayout(false);
            gsmButtonPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)gridGsm).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel mainLayout;
        private Panel topPanel;
        private Label lblSipState;
        private Label label4;
        private Label lblProviderState;
        private Label label5;
        private Label lblGsmState;
        private Label label6;
        private Label lblGsmLastRun;
        private Label label7;
        private Label lblGsmBusy;
        private Label label8;
        private Label lblGsmError;
        private Label label9;
        private Button btnRefresh;
        private GroupBox groupInternal;
        private TableLayoutPanel internalLayout;
        private FlowLayoutPanel internalButtonPanel;
        private Button btnSipStart;
        private Button btnSipStop;
        private DataGridView gridInternalUsers;
        private GroupBox groupProviders;
        private TableLayoutPanel providerLayout;
        private FlowLayoutPanel providerButtonPanel;
        private Button btnProviderStart;
        private Button btnProviderStop;
        private Button btnProviderReload;
        private DataGridView gridProviders;
        private GroupBox groupGsm;
        private TableLayoutPanel gsmLayout;
        private FlowLayoutPanel gsmButtonPanel;
        private Button btnGsmStart;
        private Button btnGsmStop;
        private Button btnGsmRunOnce;
        private DataGridView gridGsm;
        private TextBox txtStatus;
    }
}
