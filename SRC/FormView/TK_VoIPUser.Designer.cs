namespace TKServer.FormView {
    partial class TK_VoIPUser {
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
            groupBox1 = new GroupBox();
            listBox1 = new ListBox();
            labelActiveCount = new Label();
            flowLayoutPanel1 = new FlowLayoutPanel();
            buttonStart = new Button();
            buttonStop = new Button();
            buttonReload = new Button();
            buttonSave = new Button();
            tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            panel1.SuspendLayout();
            groupBox1.SuspendLayout();
            flowLayoutPanel1.SuspendLayout();
            SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.ColumnCount = 1;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.Controls.Add(dataGridView1, 0, 0);
            tableLayoutPanel1.Controls.Add(panel1, 0, 1);
            tableLayoutPanel1.Dock = DockStyle.Fill;
            tableLayoutPanel1.Location = new Point(0, 0);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 2;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.Size = new Size(998, 943);
            tableLayoutPanel1.TabIndex = 0;
            // 
            // dataGridView1
            // 
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Dock = DockStyle.Fill;
            dataGridView1.Location = new Point(3, 3);
            dataGridView1.MultiSelect = false;
            dataGridView1.Name = "dataGridView1";
            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView1.Size = new Size(992, 465);
            dataGridView1.TabIndex = 0;
            dataGridView1.CellFormatting += dataGridView1_CellFormatting;
            dataGridView1.CellValueChanged += DataGridView1_CellValueChanged;
            dataGridView1.CurrentCellDirtyStateChanged += DataGridView1_CurrentCellDirtyStateChanged;
            dataGridView1.DataError += DataGridView1_DataError;
            dataGridView1.UserDeletingRow += DataGridView1_UserDeletingRow;
            // 
            // panel1
            // 
            panel1.Controls.Add(groupBox1);
            panel1.Controls.Add(flowLayoutPanel1);
            panel1.Dock = DockStyle.Fill;
            panel1.Location = new Point(3, 474);
            panel1.Name = "panel1";
            panel1.Size = new Size(992, 466);
            panel1.TabIndex = 1;
            // 
            // groupBox1
            // 
            groupBox1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            groupBox1.Controls.Add(listBox1);
            groupBox1.Controls.Add(labelActiveCount);
            groupBox1.Location = new Point(9, 9);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(974, 393);
            groupBox1.TabIndex = 0;
            groupBox1.TabStop = false;
            groupBox1.Text = "Aktive / freigegebene Benutzer";
            // 
            // listBox1
            // 
            listBox1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            listBox1.FormattingEnabled = true;
            listBox1.ItemHeight = 15;
            listBox1.Location = new Point(6, 48);
            listBox1.Name = "listBox1";
            listBox1.Size = new Size(962, 334);
            listBox1.TabIndex = 1;
            // 
            // labelActiveCount
            // 
            labelActiveCount.AutoSize = true;
            labelActiveCount.Location = new Point(6, 22);
            labelActiveCount.Name = "labelActiveCount";
            labelActiveCount.Size = new Size(181, 15);
            labelActiveCount.TabIndex = 0;
            labelActiveCount.Text = "Aktive / freigegebene Benutzer: 0";
            // 
            // flowLayoutPanel1
            // 
            flowLayoutPanel1.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            flowLayoutPanel1.Controls.Add(buttonStart);
            flowLayoutPanel1.Controls.Add(buttonStop);
            flowLayoutPanel1.Controls.Add(buttonReload);
            flowLayoutPanel1.Controls.Add(buttonSave);
            flowLayoutPanel1.FlowDirection = FlowDirection.RightToLeft;
            flowLayoutPanel1.Location = new Point(521, 414);
            flowLayoutPanel1.Name = "flowLayoutPanel1";
            flowLayoutPanel1.Size = new Size(462, 43);
            flowLayoutPanel1.TabIndex = 1;
            // 
            // buttonStart
            // 
            buttonStart.Location = new Point(384, 3);
            buttonStart.Name = "buttonStart";
            buttonStart.Size = new Size(75, 34);
            buttonStart.TabIndex = 0;
            buttonStart.Text = "Start";
            buttonStart.UseVisualStyleBackColor = true;
            buttonStart.Click += buttonStart_Click;
            // 
            // buttonStop
            // 
            buttonStop.Location = new Point(303, 3);
            buttonStop.Name = "buttonStop";
            buttonStop.Size = new Size(75, 34);
            buttonStop.TabIndex = 1;
            buttonStop.Text = "Stop";
            buttonStop.UseVisualStyleBackColor = true;
            buttonStop.Click += buttonStop_Click;
            // 
            // buttonReload
            // 
            buttonReload.Location = new Point(222, 3);
            buttonReload.Name = "buttonReload";
            buttonReload.Size = new Size(75, 34);
            buttonReload.TabIndex = 2;
            buttonReload.Text = "Neu laden";
            buttonReload.UseVisualStyleBackColor = true;
            buttonReload.Click += buttonReload_Click;
            // 
            // buttonSave
            // 
            buttonSave.Location = new Point(136, 3);
            buttonSave.Name = "buttonSave";
            buttonSave.Size = new Size(80, 34);
            buttonSave.TabIndex = 3;
            buttonSave.Text = "Speichern";
            buttonSave.UseVisualStyleBackColor = true;
            buttonSave.Click += buttonSave_Click;
            // 
            // SIP_VoIPUser
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(998, 943);
            Controls.Add(tableLayoutPanel1);
            Name = "SIP_VoIPUser";
            Text = "VoIP-User";
            Load += SIP_VoIPUser_Load;
            tableLayoutPanel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            panel1.ResumeLayout(false);
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            flowLayoutPanel1.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel tableLayoutPanel1;
        private DataGridView dataGridView1;
        private Panel panel1;
        private GroupBox groupBox1;
        private ListBox listBox1;
        private Label labelActiveCount;
        private FlowLayoutPanel flowLayoutPanel1;
        private Button buttonStart;
        private Button buttonStop;
        private Button buttonReload;
        private Button buttonSave;
    }
}
