namespace TKServer.FormView {
    public partial class TK_GSMDevice {
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            tableLayoutPanel1 = new TableLayoutPanel();
            dataGridView1 = new DataGridView();
            panel1 = new Panel();
            listBox1 = new ListBox();
            GSMServiceStoppen = new Button();
            GSMServiceStarten = new Button();
            ModemRefresh = new Button();
            SIMKartePrüfen = new Button();
            tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            panel1.SuspendLayout();
            SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.ColumnCount = 2;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F));
            tableLayoutPanel1.Controls.Add(dataGridView1, 0, 0);
            tableLayoutPanel1.Controls.Add(panel1, 0, 1);
            tableLayoutPanel1.Controls.Add(listBox1, 1, 1);
            tableLayoutPanel1.Dock = DockStyle.Fill;
            tableLayoutPanel1.Location = new Point(0, 0);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 2;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.Size = new Size(800, 450);
            tableLayoutPanel1.TabIndex = 0;
            // 
            // dataGridView1
            // 
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            tableLayoutPanel1.SetColumnSpan(dataGridView1, 2);
            dataGridView1.Dock = DockStyle.Fill;
            dataGridView1.Location = new Point(3, 3);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.Size = new Size(794, 219);
            dataGridView1.TabIndex = 0;
            dataGridView1.CellClick += dataGridView1_CellClick;
            dataGridView1.CellDoubleClick += dataGridView1_CellDoubleClick;
            dataGridView1.RowPrePaint += dataGridView1_RowPrePaint;
            dataGridView1.Resize += dataGridView1_Resize;
            // 
            // panel1
            // 
            panel1.Controls.Add(GSMServiceStoppen);
            panel1.Controls.Add(GSMServiceStarten);
            panel1.Controls.Add(ModemRefresh);
            panel1.Controls.Add(SIMKartePrüfen);
            panel1.Dock = DockStyle.Fill;
            panel1.Location = new Point(3, 228);
            panel1.Name = "panel1";
            panel1.Size = new Size(594, 219);
            panel1.TabIndex = 1;
            // 
            // listBox1
            // 
            listBox1.Dock = DockStyle.Fill;
            listBox1.FormattingEnabled = true;
            listBox1.ItemHeight = 15;
            listBox1.Location = new Point(603, 228);
            listBox1.Name = "listBox1";
            listBox1.Size = new Size(194, 219);
            listBox1.TabIndex = 0;
            // 
            // GSMServiceStoppen
            // 
            GSMServiceStoppen.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            GSMServiceStoppen.Location = new Point(217, 72);
            GSMServiceStoppen.MaximumSize = new Size(170, 30);
            GSMServiceStoppen.MinimumSize = new Size(170, 30);
            GSMServiceStoppen.Name = "GSMServiceStoppen";
            GSMServiceStoppen.Size = new Size(170, 30);
            GSMServiceStoppen.TabIndex = 3;
            GSMServiceStoppen.Text = "GSM Service Stoppen";
            GSMServiceStoppen.UseVisualStyleBackColor = true;
            GSMServiceStoppen.Click += GSMServiceStoppen_Click;
            // 
            // GSMServiceStarten
            // 
            GSMServiceStarten.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            GSMServiceStarten.Location = new Point(217, 22);
            GSMServiceStarten.MaximumSize = new Size(170, 30);
            GSMServiceStarten.MinimumSize = new Size(170, 30);
            GSMServiceStarten.Name = "GSMServiceStarten";
            GSMServiceStarten.Size = new Size(170, 30);
            GSMServiceStarten.TabIndex = 2;
            GSMServiceStarten.Text = "GSM Service Starten";
            GSMServiceStarten.UseVisualStyleBackColor = true;
            GSMServiceStarten.Click += GSMServiceStarten_Click;
            // 
            // ModemRefresh
            // 
            ModemRefresh.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            ModemRefresh.Location = new Point(27, 22);
            ModemRefresh.MaximumSize = new Size(170, 30);
            ModemRefresh.MinimumSize = new Size(170, 30);
            ModemRefresh.Name = "ModemRefresh";
            ModemRefresh.Size = new Size(170, 30);
            ModemRefresh.TabIndex = 0;
            ModemRefresh.Text = "Modem Refresh";
            ModemRefresh.UseVisualStyleBackColor = true;
            ModemRefresh.Click += ModemRefresh_Click;
            // 
            // SIMKartePrüfen
            // 
            SIMKartePrüfen.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            SIMKartePrüfen.Location = new Point(27, 72);
            SIMKartePrüfen.MaximumSize = new Size(170, 30);
            SIMKartePrüfen.MinimumSize = new Size(170, 30);
            SIMKartePrüfen.Name = "SIMKartePrüfen";
            SIMKartePrüfen.Size = new Size(170, 30);
            SIMKartePrüfen.TabIndex = 1;
            SIMKartePrüfen.Text = "SIM Karte Prüfen";
            SIMKartePrüfen.UseVisualStyleBackColor = true;
            SIMKartePrüfen.Click += SIMKartePrüfen_Click;
            // 
            // TK_GSMDevice
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(tableLayoutPanel1);
            Name = "TK_GSMDevice";
            Text = "TestForm";
            Load += GSM_Geräte_Load;
            tableLayoutPanel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            panel1.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel tableLayoutPanel1;
        private DataGridView dataGridView1;
        private Panel panel1;
        private Button GSMServiceStarten;
        private Button SIMKartePrüfen;
        private Button ModemRefresh;
        private Button GSMServiceStoppen;
        private ListBox listBox1;
    }
}