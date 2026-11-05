using Org.BouncyCastle.Asn1.Crmf;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace TKServer.FormView {
    partial class TK_NumberRouting {
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
            checkBoxShowInactive = new CheckBox();
            buttonAdd = new Button();
            buttonDelete = new Button();
            buttonReload = new Button();
            buttonSave = new Button();
            labelHint = new Label();
            tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            panel1.SuspendLayout();
            groupBox1.SuspendLayout();
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
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 110F));
            tableLayoutPanel1.Size = new Size(1100, 700);
            tableLayoutPanel1.TabIndex = 0;
            // 
            // dataGridView1
            // 
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.AllowUserToDeleteRows = false;
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Dock = DockStyle.Fill;
            dataGridView1.Location = new Point(3, 3);
            dataGridView1.MultiSelect = false;
            dataGridView1.Name = "dataGridView1";
            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView1.Size = new Size(1094, 584);
            dataGridView1.TabIndex = 0;
            dataGridView1.CellValueChanged += dataGridView1_CellValueChanged;
            dataGridView1.CurrentCellDirtyStateChanged += dataGridView1_CurrentCellDirtyStateChanged;
            dataGridView1.DataError += dataGridView1_DataError;
            // 
            // panel1
            // 
            panel1.Controls.Add(groupBox1);
            panel1.Dock = DockStyle.Fill;
            panel1.Location = new Point(3, 593);
            panel1.Name = "panel1";
            panel1.Size = new Size(1094, 104);
            panel1.TabIndex = 1;
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(checkBoxShowInactive);
            groupBox1.Controls.Add(buttonAdd);
            groupBox1.Controls.Add(buttonDelete);
            groupBox1.Controls.Add(buttonReload);
            groupBox1.Controls.Add(buttonSave);
            groupBox1.Controls.Add(labelHint);
            groupBox1.Dock = DockStyle.Fill;
            groupBox1.Location = new Point(0, 0);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(1094, 104);
            groupBox1.TabIndex = 0;
            groupBox1.TabStop = false;
            groupBox1.Text = "Externe Nummernzuordnung";
            // 
            // checkBoxShowInactive
            // 
            checkBoxShowInactive.AutoSize = true;
            checkBoxShowInactive.Location = new Point(18, 68);
            checkBoxShowInactive.Name = "checkBoxShowInactive";
            checkBoxShowInactive.Size = new Size(123, 19);
            checkBoxShowInactive.TabIndex = 5;
            checkBoxShowInactive.Text = "Inaktive anzeigen";
            checkBoxShowInactive.UseVisualStyleBackColor = true;
            checkBoxShowInactive.CheckedChanged += checkBoxShowInactive_CheckedChanged;
            // 
            // buttonAdd
            // 
            buttonAdd.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            buttonAdd.Location = new Point(732, 60);
            buttonAdd.Name = "buttonAdd";
            buttonAdd.Size = new Size(90, 30);
            buttonAdd.TabIndex = 1;
            buttonAdd.Text = "Neu";
            buttonAdd.UseVisualStyleBackColor = true;
            buttonAdd.Click += buttonAdd_Click;
            // 
            // buttonDelete
            // 
            buttonDelete.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            buttonDelete.Location = new Point(828, 60);
            buttonDelete.Name = "buttonDelete";
            buttonDelete.Size = new Size(90, 30);
            buttonDelete.TabIndex = 2;
            buttonDelete.Text = "Löschen";
            buttonDelete.UseVisualStyleBackColor = true;
            buttonDelete.Click += buttonDelete_Click;
            // 
            // buttonReload
            // 
            buttonReload.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            buttonReload.Location = new Point(924, 60);
            buttonReload.Name = "buttonReload";
            buttonReload.Size = new Size(80, 30);
            buttonReload.TabIndex = 3;
            buttonReload.Text = "Neu laden";
            buttonReload.UseVisualStyleBackColor = true;
            buttonReload.Click += buttonReload_Click;
            // 
            // buttonSave
            // 
            buttonSave.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            buttonSave.Location = new Point(1010, 60);
            buttonSave.Name = "buttonSave";
            buttonSave.Size = new Size(75, 30);
            buttonSave.TabIndex = 4;
            buttonSave.Text = "Speichern";
            buttonSave.UseVisualStyleBackColor = true;
            buttonSave.Click += buttonSave_Click;
            // 
            // labelHint
            // 
            labelHint.AutoSize = true;
            labelHint.Location = new Point(18, 29);
            labelHint.Name = "labelHint";
            labelHint.Size = new Size(587, 15);
            labelHint.TabIndex = 0;
            labelHint.Text = "Pflegt die manuelle Zuordnung externer Nummern zu internen Zielen. Für Outbound wird nach Priority aufsteigend gesucht.";
            // 
            // SIP_ExternalNumberRouting
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1100, 700);
            Controls.Add(tableLayoutPanel1);
            Name = "SIP_ExternalNumberRouting";
            Text = "Externe Nummern-Routing";
            Load += SIP_ExternalNumberRouting_Load;
            tableLayoutPanel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            panel1.ResumeLayout(false);
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel tableLayoutPanel1;
        private DataGridView dataGridView1;
        private Panel panel1;
        private GroupBox groupBox1;
        private Label labelHint;
        private Button buttonAdd;
        private Button buttonDelete;
        private Button buttonReload;
        private Button buttonSave;
        private CheckBox checkBoxShowInactive;
    }
}
