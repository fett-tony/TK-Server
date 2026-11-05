namespace TKServer.FormView {
  public partial class TK_ServerEinstellung {
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
            tabControl1 = new TabControl();
            tabPage1 = new TabPage();
            label8 = new Label();
            OutboundProxyLabel = new Label();
            RealmLabel = new Label();
            textBox21 = new TextBox();
            textBox20 = new TextBox();
            textBox10 = new TextBox();
            TKsave = new Button();
            UseSRTP = new CheckBox();
            Codecs = new Label();
            label2 = new Label();
            textBox9 = new TextBox();
            textBox8 = new TextBox();
            label1 = new Label();
            textBox7 = new TextBox();
            StunServer = new Label();
            SIPProtokoll = new Label();
            SIPPort = new Label();
            SIPProxy = new Label();
            SIPRegistrar = new Label();
            SIPDomain = new Label();
            textBox6 = new TextBox();
            textBox5 = new TextBox();
            textBox4 = new TextBox();
            textBox3 = new TextBox();
            textBox2 = new TextBox();
            textBox1 = new TextBox();
            tabPage2 = new TabPage();
            button3 = new Button();
            comboBox1 = new ComboBox();
            checkBox3 = new CheckBox();
            checkBox2 = new CheckBox();
            checkBox1 = new CheckBox();
            label7 = new Label();
            label6 = new Label();
            label5 = new Label();
            label4 = new Label();
            label3 = new Label();
            textBox15 = new TextBox();
            textBox14 = new TextBox();
            textBox13 = new TextBox();
            textBox12 = new TextBox();
            textBox11 = new TextBox();
            tabPage3 = new TabPage();
            tabPage4 = new TabPage();
            tabPage5 = new TabPage();
            AudioFoldersUseAdsProfileCheckBox = new CheckBox();
            AudioFoldersRootPathTextBox = new TextBox();
            AudioFoldersFolderNameTextBox = new TextBox();
            AudioFoldersSaveButton = new Button();
            AudioFoldersInfoLabel = new Label();
            AudioFoldersUseAdsProfileLabel = new Label();
            AudioFoldersRootPathLabel = new Label();
            AudioFoldersFolderNameLabel = new Label();
            tabPage6 = new TabPage();
            LoggingLevelLabel = new Label();
            LoggingLevelComboBox = new ComboBox();
            LoggingDebugCheckBox = new CheckBox();
            LoggingSaveButton = new Button();
            LoggingInfoLabel = new Label();
            AudioCodecLayout = new TableLayoutPanel();
            AudioCodecCurrentLabel = new Label();
            AudioCodecGrid = new DataGridView();
            AudioCodec_ID = new DataGridViewTextBoxColumn();
            AudioCodec_CodecKey = new DataGridViewTextBoxColumn();
            AudioCodec_DisplayName = new DataGridViewTextBoxColumn();
            AudioCodec_SupportsSip = new DataGridViewTextBoxColumn();
            AudioCodec_SupportsRtp = new DataGridViewTextBoxColumn();
            AudioCodec_SupportsGsmBridge = new DataGridViewTextBoxColumn();
            AudioCodec_Active = new DataGridViewTextBoxColumn();
            AudioCodecSelectedLabel = new Label();
            AudioCodecSave = new Button();
            AudioCodecGsmSave = new Button();
            textBox19 = new TextBox();
            textBox18 = new TextBox();
            textBox17 = new TextBox();
            CountrySettingsSave = new Button();
            CountryPrefixLabel = new Label();
            CountryPrefixValueLabel = new Label();
            AreaPrefixLabel = new Label();
            AreaPrefixValueLabel = new Label();
            CountryReferenceValueLabel = new Label();
            AreaReferenceValueLabel = new Label();
            label12 = new Label();
            label11 = new Label();
            label10 = new Label();
            panel1 = new Panel();
            RefreshFirewallState = new Button();
            RemoveFirewallRule = new Button();
            SetFirewall = new Button();
            InstallWindowsService = new Button();
            RemoveWindowsService = new Button();
            listView1 = new ListView();
            columnHeader1 = new ColumnHeader();
            columnHeader2 = new ColumnHeader();
            columnHeader3 = new ColumnHeader();
            textBox16 = new TextBox();
            label9 = new Label();
            tableLayoutPanel1.SuspendLayout();
            tabControl1.SuspendLayout();
            tabPage1.SuspendLayout();
            tabPage2.SuspendLayout();
            tabPage3.SuspendLayout();
            tabPage4.SuspendLayout();
            tabPage5.SuspendLayout();
            tabPage6.SuspendLayout();
            AudioCodecLayout.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)AudioCodecGrid).BeginInit();
            panel1.SuspendLayout();
            SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.ColumnCount = 2;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 343F));
            tableLayoutPanel1.Controls.Add(tabControl1, 0, 0);
            tableLayoutPanel1.Controls.Add(panel1, 1, 1);
            tableLayoutPanel1.Controls.Add(listView1, 1, 0);
            tableLayoutPanel1.Dock = DockStyle.Fill;
            tableLayoutPanel1.Location = new Point(0, 0);
            tableLayoutPanel1.Margin = new Padding(3, 4, 3, 4);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 2;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.Size = new Size(970, 717);
            tableLayoutPanel1.TabIndex = 0;
            // 
            // tabControl1
            // 
            tabControl1.Controls.Add(tabPage1);
            tabControl1.Controls.Add(tabPage2);
            tabControl1.Controls.Add(tabPage3);
            tabControl1.Controls.Add(tabPage4);
            tabControl1.Controls.Add(tabPage5);
            tabControl1.Controls.Add(tabPage6);
            tabControl1.Dock = DockStyle.Fill;
            tabControl1.Font = new Font("Segoe UI", 12F);
            tabControl1.Location = new Point(3, 4);
            tabControl1.Margin = new Padding(3, 4, 3, 4);
            tabControl1.Name = "tabControl1";
            tableLayoutPanel1.SetRowSpan(tabControl1, 2);
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(621, 709);
            tabControl1.TabIndex = 0;
            // 
            // tabPage1
            // 
            tabPage1.AutoScroll = true;
            tabPage1.BackColor = Color.CornflowerBlue;
            tabPage1.BorderStyle = BorderStyle.Fixed3D;
            tabPage1.Controls.Add(textBox16);
            tabPage1.Controls.Add(label9);
            tabPage1.Controls.Add(label8);
            tabPage1.Controls.Add(OutboundProxyLabel);
            tabPage1.Controls.Add(RealmLabel);
            tabPage1.Controls.Add(textBox21);
            tabPage1.Controls.Add(textBox20);
            tabPage1.Controls.Add(textBox10);
            tabPage1.Controls.Add(TKsave);
            tabPage1.Controls.Add(UseSRTP);
            tabPage1.Controls.Add(Codecs);
            tabPage1.Controls.Add(label2);
            tabPage1.Controls.Add(textBox9);
            tabPage1.Controls.Add(textBox8);
            tabPage1.Controls.Add(label1);
            tabPage1.Controls.Add(textBox7);
            tabPage1.Controls.Add(StunServer);
            tabPage1.Controls.Add(SIPProtokoll);
            tabPage1.Controls.Add(SIPPort);
            tabPage1.Controls.Add(SIPProxy);
            tabPage1.Controls.Add(SIPRegistrar);
            tabPage1.Controls.Add(SIPDomain);
            tabPage1.Controls.Add(textBox6);
            tabPage1.Controls.Add(textBox5);
            tabPage1.Controls.Add(textBox4);
            tabPage1.Controls.Add(textBox3);
            tabPage1.Controls.Add(textBox2);
            tabPage1.Controls.Add(textBox1);
            tabPage1.Font = new Font("Segoe UI", 12F);
            tabPage1.Location = new Point(4, 37);
            tabPage1.Margin = new Padding(3, 4, 3, 4);
            tabPage1.Name = "tabPage1";
            tabPage1.Padding = new Padding(3, 4, 3, 4);
            tabPage1.Size = new Size(613, 668);
            tabPage1.TabIndex = 0;
            tabPage1.Text = "TK Server";
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold);
            label8.Location = new Point(290, 423);
            label8.Name = "label8";
            label8.Size = new Size(152, 25);
            label8.TabIndex = 22;
            label8.Text = "RegisterExpire";
            // 
            // OutboundProxyLabel
            // 
            OutboundProxyLabel.AutoSize = true;
            OutboundProxyLabel.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold);
            OutboundProxyLabel.Location = new Point(22, 423);
            OutboundProxyLabel.Name = "OutboundProxyLabel";
            OutboundProxyLabel.Size = new Size(168, 25);
            OutboundProxyLabel.TabIndex = 26;
            OutboundProxyLabel.Text = "Outbound Proxy";
            // 
            // RealmLabel
            // 
            RealmLabel.AutoSize = true;
            RealmLabel.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold);
            RealmLabel.Location = new Point(22, 267);
            RealmLabel.Name = "RealmLabel";
            RealmLabel.Size = new Size(72, 25);
            RealmLabel.TabIndex = 25;
            RealmLabel.Text = "Realm";
            // 
            // textBox21
            // 
            textBox21.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold);
            textBox21.Location = new Point(22, 457);
            textBox21.Margin = new Padding(3, 4, 3, 4);
            textBox21.MaximumSize = new Size(230, 35);
            textBox21.MinimumSize = new Size(230, 35);
            textBox21.Name = "textBox21";
            textBox21.Size = new Size(230, 35);
            textBox21.TabIndex = 24;
            // 
            // textBox20
            // 
            textBox20.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold);
            textBox20.Location = new Point(22, 301);
            textBox20.Margin = new Padding(3, 4, 3, 4);
            textBox20.MaximumSize = new Size(230, 35);
            textBox20.MinimumSize = new Size(230, 35);
            textBox20.Name = "textBox20";
            textBox20.Size = new Size(230, 35);
            textBox20.TabIndex = 23;
            // 
            // textBox10
            // 
            textBox10.Location = new Point(290, 454);
            textBox10.Margin = new Padding(3, 4, 3, 4);
            textBox10.MaximumSize = new Size(230, 35);
            textBox10.MinimumSize = new Size(230, 35);
            textBox10.Name = "textBox10";
            textBox10.Size = new Size(230, 35);
            textBox10.TabIndex = 21;
            // 
            // TKsave
            // 
            TKsave.Location = new Point(53, 554);
            TKsave.Margin = new Padding(3, 4, 3, 4);
            TKsave.Name = "TKsave";
            TKsave.Size = new Size(137, 49);
            TKsave.TabIndex = 19;
            TKsave.Text = "Speichern";
            TKsave.UseVisualStyleBackColor = true;
            TKsave.Click += button1_Click;
            // 
            // UseSRTP
            // 
            UseSRTP.AutoSize = true;
            UseSRTP.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold);
            UseSRTP.Location = new Point(328, 523);
            UseSRTP.Margin = new Padding(3, 4, 3, 4);
            UseSRTP.Name = "UseSRTP";
            UseSRTP.Size = new Size(135, 29);
            UseSRTP.TabIndex = 18;
            UseSRTP.Text = "Use SRTP";
            UseSRTP.UseVisualStyleBackColor = true;
            // 
            // Codecs
            // 
            Codecs.AutoSize = true;
            Codecs.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold);
            Codecs.Location = new Point(290, 184);
            Codecs.Name = "Codecs";
            Codecs.Size = new Size(86, 25);
            Codecs.TabIndex = 17;
            Codecs.Text = "SIPCodec ID";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold);
            label2.Location = new Point(409, 116);
            label2.Name = "label2";
            label2.RightToLeft = RightToLeft.No;
            label2.Size = new Size(110, 25);
            label2.TabIndex = 16;
            label2.Text = "RTP Ende";
            // 
            // textBox9
            // 
            textBox9.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold);
            textBox9.Location = new Point(290, 216);
            textBox9.Margin = new Padding(3, 4, 3, 4);
            textBox9.MaximumSize = new Size(230, 35);
            textBox9.MinimumSize = new Size(230, 35);
            textBox9.Name = "textBox9";
            textBox9.Size = new Size(230, 35);
            textBox9.TabIndex = 15;
            // 
            // textBox8
            // 
            textBox8.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold);
            textBox8.Location = new Point(409, 145);
            textBox8.Margin = new Padding(3, 4, 3, 4);
            textBox8.MaximumSize = new Size(85, 35);
            textBox8.MinimumSize = new Size(85, 35);
            textBox8.Name = "textBox8";
            textBox8.Size = new Size(85, 35);
            textBox8.TabIndex = 14;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold);
            label1.Location = new Point(289, 110);
            label1.Name = "label1";
            label1.Size = new Size(106, 25);
            label1.TabIndex = 13;
            label1.Text = "RTP Start";
            // 
            // textBox7
            // 
            textBox7.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold);
            textBox7.Location = new Point(290, 142);
            textBox7.Margin = new Padding(3, 4, 3, 4);
            textBox7.MaximumSize = new Size(85, 35);
            textBox7.MinimumSize = new Size(85, 35);
            textBox7.Name = "textBox7";
            textBox7.Size = new Size(85, 35);
            textBox7.TabIndex = 12;
            // 
            // StunServer
            // 
            StunServer.AutoSize = true;
            StunServer.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold);
            StunServer.Location = new Point(22, 345);
            StunServer.Name = "StunServer";
            StunServer.Size = new Size(127, 25);
            StunServer.TabIndex = 11;
            StunServer.Text = "Stun Server";
            // 
            // SIPProtokoll
            // 
            SIPProtokoll.AutoSize = true;
            SIPProtokoll.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold);
            SIPProtokoll.Location = new Point(290, 335);
            SIPProtokoll.Name = "SIPProtokoll";
            SIPProtokoll.Size = new Size(137, 25);
            SIPProtokoll.TabIndex = 10;
            SIPProtokoll.Text = "SIP Protokoll";
            // 
            // SIPPort
            // 
            SIPPort.AutoSize = true;
            SIPPort.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold);
            SIPPort.Location = new Point(289, 36);
            SIPPort.Name = "SIPPort";
            SIPPort.Size = new Size(92, 25);
            SIPPort.TabIndex = 9;
            SIPPort.Text = "SIP Port";
            // 
            // SIPProxy
            // 
            SIPProxy.AutoSize = true;
            SIPProxy.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold);
            SIPProxy.Location = new Point(22, 111);
            SIPProxy.Name = "SIPProxy";
            SIPProxy.Size = new Size(108, 25);
            SIPProxy.TabIndex = 8;
            SIPProxy.Text = "SIP Proxy";
            // 
            // SIPRegistrar
            // 
            SIPRegistrar.AutoSize = true;
            SIPRegistrar.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold);
            SIPRegistrar.Location = new Point(22, 189);
            SIPRegistrar.Name = "SIPRegistrar";
            SIPRegistrar.Size = new Size(98, 25);
            SIPRegistrar.TabIndex = 7;
            SIPRegistrar.Text = "Registrar";
            // 
            // SIPDomain
            // 
            SIPDomain.AutoSize = true;
            SIPDomain.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold);
            SIPDomain.Location = new Point(22, 33);
            SIPDomain.Name = "SIPDomain";
            SIPDomain.Size = new Size(216, 25);
            SIPDomain.TabIndex = 6;
            SIPDomain.Text = "Server IP (; getrennt)";
            // 
            // textBox6
            // 
            textBox6.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold);
            textBox6.Location = new Point(22, 379);
            textBox6.Margin = new Padding(3, 4, 3, 4);
            textBox6.MaximumSize = new Size(230, 35);
            textBox6.MinimumSize = new Size(230, 35);
            textBox6.Name = "textBox6";
            textBox6.Size = new Size(230, 35);
            textBox6.TabIndex = 5;
            // 
            // textBox5
            // 
            textBox5.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold);
            textBox5.Location = new Point(290, 367);
            textBox5.Margin = new Padding(3, 4, 3, 4);
            textBox5.MaximumSize = new Size(230, 35);
            textBox5.MinimumSize = new Size(230, 35);
            textBox5.Name = "textBox5";
            textBox5.Size = new Size(230, 35);
            textBox5.TabIndex = 4;
            // 
            // textBox4
            // 
            textBox4.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold);
            textBox4.Location = new Point(289, 68);
            textBox4.Margin = new Padding(3, 4, 3, 4);
            textBox4.MaximumSize = new Size(230, 35);
            textBox4.MinimumSize = new Size(230, 35);
            textBox4.Name = "textBox4";
            textBox4.Size = new Size(230, 35);
            textBox4.TabIndex = 3;
            // 
            // textBox3
            // 
            textBox3.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold);
            textBox3.Location = new Point(22, 145);
            textBox3.Margin = new Padding(3, 4, 3, 4);
            textBox3.MaximumSize = new Size(230, 35);
            textBox3.MinimumSize = new Size(230, 35);
            textBox3.Name = "textBox3";
            textBox3.Size = new Size(230, 35);
            textBox3.TabIndex = 2;
            // 
            // textBox2
            // 
            textBox2.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold);
            textBox2.Location = new Point(22, 223);
            textBox2.Margin = new Padding(3, 4, 3, 4);
            textBox2.MaximumSize = new Size(230, 35);
            textBox2.MinimumSize = new Size(230, 35);
            textBox2.Name = "textBox2";
            textBox2.Size = new Size(230, 35);
            textBox2.TabIndex = 1;
            // 
            // textBox1
            // 
            textBox1.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold);
            textBox1.Location = new Point(22, 67);
            textBox1.Margin = new Padding(3, 4, 3, 4);
            textBox1.MaximumSize = new Size(230, 35);
            textBox1.MinimumSize = new Size(230, 35);
            textBox1.Name = "textBox1";
            textBox1.Size = new Size(230, 35);
            textBox1.TabIndex = 0;
            // 
            // tabPage2
            // 
            tabPage2.BackColor = Color.CornflowerBlue;
            tabPage2.BorderStyle = BorderStyle.Fixed3D;
            tabPage2.Controls.Add(button3);
            tabPage2.Controls.Add(comboBox1);
            tabPage2.Controls.Add(checkBox3);
            tabPage2.Controls.Add(checkBox2);
            tabPage2.Controls.Add(checkBox1);
            tabPage2.Controls.Add(label7);
            tabPage2.Controls.Add(label6);
            tabPage2.Controls.Add(label5);
            tabPage2.Controls.Add(label4);
            tabPage2.Controls.Add(label3);
            tabPage2.Controls.Add(textBox15);
            tabPage2.Controls.Add(textBox14);
            tabPage2.Controls.Add(textBox13);
            tabPage2.Controls.Add(textBox12);
            tabPage2.Controls.Add(textBox11);
            tabPage2.Font = new Font("Segoe UI", 12F);
            tabPage2.Location = new Point(4, 37);
            tabPage2.Margin = new Padding(3, 4, 3, 4);
            tabPage2.Name = "tabPage2";
            tabPage2.Padding = new Padding(3, 4, 3, 4);
            tabPage2.Size = new Size(613, 668);
            tabPage2.TabIndex = 1;
            tabPage2.Text = "SQL DB Einstellung";
            // 
            // button3
            // 
            button3.Location = new Point(42, 496);
            button3.Margin = new Padding(3, 4, 3, 4);
            button3.Name = "button3";
            button3.Size = new Size(191, 61);
            button3.TabIndex = 18;
            button3.Text = "Speichern";
            button3.UseVisualStyleBackColor = true;
            button3.Click += button3_Click;
            // 
            // comboBox1
            // 
            comboBox1.FormattingEnabled = true;
            comboBox1.Items.AddRange(new object[] { "LocalBuilder", "SQLLoginBuilder", "WinAuthBuilder" });
            comboBox1.Location = new Point(285, 319);
            comboBox1.Margin = new Padding(3, 4, 3, 4);
            comboBox1.Name = "comboBox1";
            comboBox1.Size = new Size(221, 36);
            comboBox1.TabIndex = 17;
            // 
            // checkBox3
            // 
            checkBox3.AutoSize = true;
            checkBox3.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            checkBox3.Location = new Point(285, 236);
            checkBox3.Margin = new Padding(3, 4, 3, 4);
            checkBox3.Name = "checkBox3";
            checkBox3.Size = new Size(232, 32);
            checkBox3.TabIndex = 16;
            checkBox3.Text = "TrustServerCertifikat";
            checkBox3.UseVisualStyleBackColor = true;
            // 
            // checkBox2
            // 
            checkBox2.AutoSize = true;
            checkBox2.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            checkBox2.Location = new Point(285, 153);
            checkBox2.Margin = new Padding(3, 4, 3, 4);
            checkBox2.Name = "checkBox2";
            checkBox2.Size = new Size(107, 32);
            checkBox2.TabIndex = 15;
            checkBox2.Text = "Encrypt";
            checkBox2.UseVisualStyleBackColor = true;
            // 
            // checkBox1
            // 
            checkBox1.AutoSize = true;
            checkBox1.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            checkBox1.Location = new Point(285, 71);
            checkBox1.Margin = new Padding(3, 4, 3, 4);
            checkBox1.Name = "checkBox1";
            checkBox1.Size = new Size(218, 32);
            checkBox1.TabIndex = 14;
            checkBox1.Text = "Integrated_Security";
            checkBox1.UseVisualStyleBackColor = true;
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            label7.Location = new Point(29, 365);
            label7.Name = "label7";
            label7.Size = new Size(52, 28);
            label7.TabIndex = 13;
            label7.Text = "Port";
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            label6.Location = new Point(29, 283);
            label6.Name = "label6";
            label6.Size = new Size(98, 28);
            label6.TabIndex = 12;
            label6.Text = "Passwort";
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            label5.Location = new Point(29, 200);
            label5.Name = "label5";
            label5.Size = new Size(75, 28);
            label5.TabIndex = 11;
            label5.Text = "UserID";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            label4.Location = new Point(29, 117);
            label4.Name = "label4";
            label4.Size = new Size(100, 28);
            label4.TabIndex = 10;
            label4.Text = "Database";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            label3.Location = new Point(29, 35);
            label3.Name = "label3";
            label3.Size = new Size(73, 28);
            label3.TabIndex = 9;
            label3.Text = "Server";
            // 
            // textBox15
            // 
            textBox15.Location = new Point(29, 401);
            textBox15.Margin = new Padding(3, 4, 3, 4);
            textBox15.MaximumSize = new Size(228, 29);
            textBox15.MinimumSize = new Size(228, 29);
            textBox15.Name = "textBox15";
            textBox15.Size = new Size(228, 29);
            textBox15.TabIndex = 4;
            // 
            // textBox14
            // 
            textBox14.Location = new Point(29, 319);
            textBox14.Margin = new Padding(3, 4, 3, 4);
            textBox14.MaximumSize = new Size(228, 29);
            textBox14.MinimumSize = new Size(228, 29);
            textBox14.Name = "textBox14";
            textBox14.PasswordChar = '*';
            textBox14.Size = new Size(228, 29);
            textBox14.TabIndex = 3;
            textBox14.UseSystemPasswordChar = true;
            // 
            // textBox13
            // 
            textBox13.Location = new Point(29, 236);
            textBox13.Margin = new Padding(3, 4, 3, 4);
            textBox13.MaximumSize = new Size(228, 29);
            textBox13.MinimumSize = new Size(228, 29);
            textBox13.Name = "textBox13";
            textBox13.Size = new Size(228, 29);
            textBox13.TabIndex = 2;
            // 
            // textBox12
            // 
            textBox12.Location = new Point(29, 153);
            textBox12.Margin = new Padding(3, 4, 3, 4);
            textBox12.MaximumSize = new Size(228, 29);
            textBox12.MinimumSize = new Size(228, 29);
            textBox12.Name = "textBox12";
            textBox12.Size = new Size(228, 29);
            textBox12.TabIndex = 1;
            // 
            // textBox11
            // 
            textBox11.Location = new Point(29, 71);
            textBox11.Margin = new Padding(3, 4, 3, 4);
            textBox11.MaximumSize = new Size(228, 29);
            textBox11.MinimumSize = new Size(228, 29);
            textBox11.Name = "textBox11";
            textBox11.Size = new Size(228, 29);
            textBox11.TabIndex = 0;
            // 
            // tabPage3
            // 
            tabPage3.BackColor = Color.CornflowerBlue;
            tabPage3.BorderStyle = BorderStyle.Fixed3D;
            tabPage3.Controls.Add(AreaReferenceValueLabel);
            tabPage3.Controls.Add(CountryReferenceValueLabel);
            tabPage3.Controls.Add(AreaPrefixValueLabel);
            tabPage3.Controls.Add(AreaPrefixLabel);
            tabPage3.Controls.Add(CountryPrefixValueLabel);
            tabPage3.Controls.Add(CountryPrefixLabel);
            tabPage3.Controls.Add(CountrySettingsSave);
            tabPage3.Controls.Add(textBox19);
            tabPage3.Controls.Add(textBox18);
            tabPage3.Controls.Add(textBox17);
            tabPage3.Controls.Add(label12);
            tabPage3.Controls.Add(label11);
            tabPage3.Controls.Add(label10);
            tabPage3.Font = new Font("Segoe UI", 12F);
            tabPage3.Location = new Point(4, 37);
            tabPage3.Margin = new Padding(3, 4, 3, 4);
            tabPage3.Name = "tabPage3";
            tabPage3.Size = new Size(613, 668);
            tabPage3.TabIndex = 2;
            tabPage3.Text = "Ländereinstellung";
            // 
            // tabPage4
            // 
            tabPage4.BackColor = Color.CornflowerBlue;
            tabPage4.BorderStyle = BorderStyle.Fixed3D;
            tabPage4.Controls.Add(AudioCodecLayout);
            tabPage4.Font = new Font("Segoe UI", 12F);
            tabPage4.Location = new Point(4, 37);
            tabPage4.Margin = new Padding(3, 4, 3, 4);
            tabPage4.Name = "tabPage4";
            tabPage4.Padding = new Padding(8);
            tabPage4.Size = new Size(613, 668);
            tabPage4.TabIndex = 3;
            tabPage4.Text = "Audio Codec";
            // 
            // tabPage5
            // 
            tabPage5.BackColor = Color.CornflowerBlue;
            tabPage5.BorderStyle = BorderStyle.Fixed3D;
            tabPage5.Controls.Add(AudioFoldersInfoLabel);
            tabPage5.Controls.Add(AudioFoldersSaveButton);
            tabPage5.Controls.Add(AudioFoldersFolderNameTextBox);
            tabPage5.Controls.Add(AudioFoldersRootPathTextBox);
            tabPage5.Controls.Add(AudioFoldersUseAdsProfileCheckBox);
            tabPage5.Controls.Add(AudioFoldersFolderNameLabel);
            tabPage5.Controls.Add(AudioFoldersRootPathLabel);
            tabPage5.Controls.Add(AudioFoldersUseAdsProfileLabel);
            tabPage5.Font = new Font("Segoe UI", 12F);
            tabPage5.Location = new Point(4, 37);
            tabPage5.Margin = new Padding(3, 4, 3, 4);
            tabPage5.Name = "tabPage5";
            tabPage5.Padding = new Padding(8);
            tabPage5.Size = new Size(613, 668);
            tabPage5.TabIndex = 4;
            tabPage5.Text = "Audiofolders";
            // 
            // AudioFoldersUseAdsProfileCheckBox
            // 
            AudioFoldersUseAdsProfileCheckBox.AutoSize = true;
            AudioFoldersUseAdsProfileCheckBox.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            AudioFoldersUseAdsProfileCheckBox.Location = new Point(287, 65);
            AudioFoldersUseAdsProfileCheckBox.Name = "AudioFoldersUseAdsProfileCheckBox";
            AudioFoldersUseAdsProfileCheckBox.Size = new Size(18, 17);
            AudioFoldersUseAdsProfileCheckBox.TabIndex = 1;
            AudioFoldersUseAdsProfileCheckBox.UseVisualStyleBackColor = true;
            AudioFoldersUseAdsProfileCheckBox.CheckedChanged += AudioFoldersValueChanged;
            // 
            // AudioFoldersRootPathTextBox
            // 
            AudioFoldersRootPathTextBox.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            AudioFoldersRootPathTextBox.Location = new Point(29, 151);
            AudioFoldersRootPathTextBox.Margin = new Padding(3, 4, 3, 4);
            AudioFoldersRootPathTextBox.Name = "AudioFoldersRootPathTextBox";
            AudioFoldersRootPathTextBox.PlaceholderText = "leer = <EXE>\\Media";
            AudioFoldersRootPathTextBox.Size = new Size(525, 34);
            AudioFoldersRootPathTextBox.TabIndex = 3;
            AudioFoldersRootPathTextBox.TextChanged += AudioFoldersValueChanged;
            // 
            // AudioFoldersFolderNameTextBox
            // 
            AudioFoldersFolderNameTextBox.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            AudioFoldersFolderNameTextBox.Location = new Point(29, 244);
            AudioFoldersFolderNameTextBox.Margin = new Padding(3, 4, 3, 4);
            AudioFoldersFolderNameTextBox.Name = "AudioFoldersFolderNameTextBox";
            AudioFoldersFolderNameTextBox.PlaceholderText = "User";
            AudioFoldersFolderNameTextBox.Size = new Size(230, 34);
            AudioFoldersFolderNameTextBox.TabIndex = 5;
            AudioFoldersFolderNameTextBox.TextChanged += AudioFoldersValueChanged;
            // 
            // AudioFoldersSaveButton
            // 
            AudioFoldersSaveButton.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            AudioFoldersSaveButton.Location = new Point(29, 325);
            AudioFoldersSaveButton.Name = "AudioFoldersSaveButton";
            AudioFoldersSaveButton.Size = new Size(191, 61);
            AudioFoldersSaveButton.TabIndex = 6;
            AudioFoldersSaveButton.Text = "Speichern";
            AudioFoldersSaveButton.UseVisualStyleBackColor = true;
            AudioFoldersSaveButton.Click += SaveAudioFolders_Click;
            // 
            // AudioFoldersInfoLabel
            // 
            AudioFoldersInfoLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            AudioFoldersInfoLabel.Location = new Point(29, 420);
            AudioFoldersInfoLabel.Name = "AudioFoldersInfoLabel";
            AudioFoldersInfoLabel.Size = new Size(525, 92);
            AudioFoldersInfoLabel.TabIndex = 7;
            AudioFoldersInfoLabel.Text = "Serverpfad aktiv: <EXE>\\Media\\User\\<Username>";
            // 
            // AudioFoldersUseAdsProfileLabel
            // 
            AudioFoldersUseAdsProfileLabel.AutoSize = true;
            AudioFoldersUseAdsProfileLabel.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            AudioFoldersUseAdsProfileLabel.Location = new Point(29, 59);
            AudioFoldersUseAdsProfileLabel.Name = "AudioFoldersUseAdsProfileLabel";
            AudioFoldersUseAdsProfileLabel.Size = new Size(235, 28);
            AudioFoldersUseAdsProfileLabel.TabIndex = 0;
            AudioFoldersUseAdsProfileLabel.Text = "ADS-Profilpfad nutzen";
            // 
            // AudioFoldersRootPathLabel
            // 
            AudioFoldersRootPathLabel.AutoSize = true;
            AudioFoldersRootPathLabel.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            AudioFoldersRootPathLabel.Location = new Point(29, 116);
            AudioFoldersRootPathLabel.Name = "AudioFoldersRootPathLabel";
            AudioFoldersRootPathLabel.Size = new Size(207, 28);
            AudioFoldersRootPathLabel.TabIndex = 2;
            AudioFoldersRootPathLabel.Text = "UserMediaRootPath";
            // 
            // AudioFoldersFolderNameLabel
            // 
            AudioFoldersFolderNameLabel.AutoSize = true;
            AudioFoldersFolderNameLabel.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            AudioFoldersFolderNameLabel.Location = new Point(29, 209);
            AudioFoldersFolderNameLabel.Name = "AudioFoldersFolderNameLabel";
            AudioFoldersFolderNameLabel.Size = new Size(234, 28);
            AudioFoldersFolderNameLabel.TabIndex = 4;
            AudioFoldersFolderNameLabel.Text = "UserMediaFolderName";
            // 
            // tabPage6
            // 
            tabPage6.BackColor = Color.CornflowerBlue;
            tabPage6.BorderStyle = BorderStyle.Fixed3D;
            tabPage6.Controls.Add(LoggingInfoLabel);
            tabPage6.Controls.Add(LoggingSaveButton);
            tabPage6.Controls.Add(LoggingDebugCheckBox);
            tabPage6.Controls.Add(LoggingLevelComboBox);
            tabPage6.Controls.Add(LoggingLevelLabel);
            tabPage6.Font = new Font("Segoe UI", 12F);
            tabPage6.Location = new Point(4, 37);
            tabPage6.Margin = new Padding(3, 4, 3, 4);
            tabPage6.Name = "tabPage6";
            tabPage6.Padding = new Padding(8);
            tabPage6.Size = new Size(613, 668);
            tabPage6.TabIndex = 5;
            tabPage6.Text = "Logging";
            // 
            // LoggingLevelLabel
            // 
            LoggingLevelLabel.AutoSize = true;
            LoggingLevelLabel.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            LoggingLevelLabel.Location = new Point(29, 59);
            LoggingLevelLabel.Name = "LoggingLevelLabel";
            LoggingLevelLabel.Size = new Size(97, 28);
            LoggingLevelLabel.TabIndex = 0;
            LoggingLevelLabel.Text = "Log-Level";
            // 
            // LoggingLevelComboBox
            // 
            LoggingLevelComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            LoggingLevelComboBox.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            LoggingLevelComboBox.FormattingEnabled = true;
            LoggingLevelComboBox.Items.AddRange(new object[] { "Error", "Warn", "Info" });
            LoggingLevelComboBox.Location = new Point(29, 95);
            LoggingLevelComboBox.Name = "LoggingLevelComboBox";
            LoggingLevelComboBox.Size = new Size(230, 36);
            LoggingLevelComboBox.TabIndex = 1;
            // 
            // LoggingDebugCheckBox
            // 
            LoggingDebugCheckBox.AutoSize = true;
            LoggingDebugCheckBox.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            LoggingDebugCheckBox.Location = new Point(29, 163);
            LoggingDebugCheckBox.Name = "LoggingDebugCheckBox";
            LoggingDebugCheckBox.Size = new Size(211, 32);
            LoggingDebugCheckBox.TabIndex = 2;
            LoggingDebugCheckBox.Text = "Debug aktivieren";
            LoggingDebugCheckBox.UseVisualStyleBackColor = true;
            // 
            // LoggingSaveButton
            // 
            LoggingSaveButton.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            LoggingSaveButton.Location = new Point(29, 238);
            LoggingSaveButton.Name = "LoggingSaveButton";
            LoggingSaveButton.Size = new Size(191, 61);
            LoggingSaveButton.TabIndex = 3;
            LoggingSaveButton.Text = "Speichern";
            LoggingSaveButton.UseVisualStyleBackColor = true;
            LoggingSaveButton.Click += SaveLoggingSettings_Click;
            // 
            // LoggingInfoLabel
            // 
            LoggingInfoLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            LoggingInfoLabel.Location = new Point(29, 338);
            LoggingInfoLabel.Name = "LoggingInfoLabel";
            LoggingInfoLabel.Size = new Size(525, 140);
            LoggingInfoLabel.TabIndex = 4;
            LoggingInfoLabel.Text = "Normales Logging: Info + Warn + Error";
            // 
            // AudioCodecLayout
            // 
            AudioCodecLayout.ColumnCount = 1;
            AudioCodecLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            AudioCodecLayout.Controls.Add(AudioCodecCurrentLabel, 0, 0);
            AudioCodecLayout.Controls.Add(AudioCodecGrid, 0, 1);
            AudioCodecLayout.Controls.Add(AudioCodecSelectedLabel, 0, 2);
            AudioCodecLayout.Controls.Add(AudioCodecSave, 0, 3);
            AudioCodecLayout.Controls.Add(AudioCodecGsmSave, 0, 4);
            AudioCodecLayout.Dock = DockStyle.Fill;
            AudioCodecLayout.Location = new Point(8, 8);
            AudioCodecLayout.Name = "AudioCodecLayout";
            AudioCodecLayout.RowCount = 5;
            AudioCodecLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            AudioCodecLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            AudioCodecLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            AudioCodecLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            AudioCodecLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            AudioCodecLayout.Size = new Size(593, 648);
            AudioCodecLayout.TabIndex = 0;
            // 
            // AudioCodecCurrentLabel
            // 
            AudioCodecCurrentLabel.Dock = DockStyle.Fill;
            AudioCodecCurrentLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            AudioCodecCurrentLabel.Location = new Point(3, 0);
            AudioCodecCurrentLabel.Name = "AudioCodecCurrentLabel";
            AudioCodecCurrentLabel.Size = new Size(587, 36);
            AudioCodecCurrentLabel.TabIndex = 0;
            AudioCodecCurrentLabel.Text = "Aktuelle Codecs: SIP=- | GSM=-";
            AudioCodecCurrentLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // AudioCodecGrid
            // 
            AudioCodecGrid.AllowUserToAddRows = false;
            AudioCodecGrid.AllowUserToDeleteRows = false;
            AudioCodecGrid.AllowUserToResizeRows = false;
            AudioCodecGrid.AutoGenerateColumns = false;
            AudioCodecGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            AudioCodecGrid.BackgroundColor = Color.White;
            AudioCodecGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            AudioCodecGrid.Columns.AddRange(new DataGridViewColumn[] { AudioCodec_ID, AudioCodec_CodecKey, AudioCodec_DisplayName, AudioCodec_SupportsSip, AudioCodec_SupportsRtp, AudioCodec_SupportsGsmBridge, AudioCodec_Active });
            AudioCodecGrid.Dock = DockStyle.Fill;
            AudioCodecGrid.Location = new Point(3, 39);
            AudioCodecGrid.MultiSelect = false;
            AudioCodecGrid.Name = "AudioCodecGrid";
            AudioCodecGrid.ReadOnly = true;
            AudioCodecGrid.RowHeadersWidth = 51;
            AudioCodecGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            AudioCodecGrid.Size = new Size(587, 474);
            AudioCodecGrid.TabIndex = 1;
            AudioCodecGrid.SelectionChanged += AudioCodecGrid_SelectionChanged;
            // 
            // AudioCodec_ID
            // 
            AudioCodec_ID.DataPropertyName = "ID";
            AudioCodec_ID.HeaderText = "ID";
            AudioCodec_ID.MinimumWidth = 52;
            AudioCodec_ID.Name = "AudioCodec_ID";
            AudioCodec_ID.ReadOnly = true;
            AudioCodec_ID.Width = 57;
            // 
            // AudioCodec_CodecKey
            // 
            AudioCodec_CodecKey.DataPropertyName = "CodecKey";
            AudioCodec_CodecKey.HeaderText = "CodecKey";
            AudioCodec_CodecKey.MinimumWidth = 90;
            AudioCodec_CodecKey.Name = "AudioCodec_CodecKey";
            AudioCodec_CodecKey.ReadOnly = true;
            AudioCodec_CodecKey.Width = 126;
            // 
            // AudioCodec_DisplayName
            // 
            AudioCodec_DisplayName.DataPropertyName = "DisplayName";
            AudioCodec_DisplayName.HeaderText = "DisplayName";
            AudioCodec_DisplayName.MinimumWidth = 150;
            AudioCodec_DisplayName.Name = "AudioCodec_DisplayName";
            AudioCodec_DisplayName.ReadOnly = true;
            AudioCodec_DisplayName.Width = 160;
            // 
            // AudioCodec_SupportsSip
            // 
            AudioCodec_SupportsSip.DataPropertyName = "SupportsSip";
            AudioCodec_SupportsSip.HeaderText = "SupportsSip";
            AudioCodec_SupportsSip.MinimumWidth = 90;
            AudioCodec_SupportsSip.Name = "AudioCodec_SupportsSip";
            AudioCodec_SupportsSip.ReadOnly = true;
            AudioCodec_SupportsSip.Width = 139;
            // 
            // AudioCodec_SupportsRtp
            // 
            AudioCodec_SupportsRtp.DataPropertyName = "SupportsRtp";
            AudioCodec_SupportsRtp.HeaderText = "SupportsRtp";
            AudioCodec_SupportsRtp.MinimumWidth = 90;
            AudioCodec_SupportsRtp.Name = "AudioCodec_SupportsRtp";
            AudioCodec_SupportsRtp.ReadOnly = true;
            AudioCodec_SupportsRtp.Width = 140;
            // 
            // AudioCodec_SupportsGsmBridge
            // 
            AudioCodec_SupportsGsmBridge.DataPropertyName = "SupportsGsmBridge";
            AudioCodec_SupportsGsmBridge.HeaderText = "SupportsGsmBridge";
            AudioCodec_SupportsGsmBridge.MinimumWidth = 120;
            AudioCodec_SupportsGsmBridge.Name = "AudioCodec_SupportsGsmBridge";
            AudioCodec_SupportsGsmBridge.ReadOnly = true;
            AudioCodec_SupportsGsmBridge.Width = 206;
            // 
            // AudioCodec_Active
            // 
            AudioCodec_Active.DataPropertyName = "Active";
            AudioCodec_Active.HeaderText = "Active";
            AudioCodec_Active.MinimumWidth = 70;
            AudioCodec_Active.Name = "AudioCodec_Active";
            AudioCodec_Active.ReadOnly = true;
            AudioCodec_Active.Width = 91;
            // 
            // AudioCodecSelectedLabel
            // 
            AudioCodecSelectedLabel.Dock = DockStyle.Fill;
            AudioCodecSelectedLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            AudioCodecSelectedLabel.Location = new Point(3, 516);
            AudioCodecSelectedLabel.Name = "AudioCodecSelectedLabel";
            AudioCodecSelectedLabel.Size = new Size(587, 36);
            AudioCodecSelectedLabel.TabIndex = 2;
            AudioCodecSelectedLabel.Text = "Auswahl: -";
            AudioCodecSelectedLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // AudioCodecSave
            // 
            AudioCodecSave.Dock = DockStyle.Left;
            AudioCodecSave.Location = new Point(3, 555);
            AudioCodecSave.Name = "AudioCodecSave";
            AudioCodecSave.Size = new Size(240, 42);
            AudioCodecSave.TabIndex = 3;
            AudioCodecSave.Text = "Als SIP Codec speichern";
            AudioCodecSave.UseVisualStyleBackColor = true;
            AudioCodecSave.Click += SaveSelectedSipCodec_Click;
            // 
            // AudioCodecGsmSave
            // 
            AudioCodecGsmSave.Dock = DockStyle.Left;
            AudioCodecGsmSave.Location = new Point(3, 603);
            AudioCodecGsmSave.Name = "AudioCodecGsmSave";
            AudioCodecGsmSave.Size = new Size(240, 42);
            AudioCodecGsmSave.TabIndex = 4;
            AudioCodecGsmSave.Text = "Als GSM Codec speichern";
            AudioCodecGsmSave.UseVisualStyleBackColor = true;
            AudioCodecGsmSave.Click += SaveSelectedGsmCodec_Click;
            // 
            // textBox19
            // 
            textBox19.Location = new Point(130, 165);
            textBox19.Name = "textBox19";
            textBox19.Size = new Size(284, 34);
            textBox19.TabIndex = 11;
            // 
            // textBox18
            // 
            textBox18.Location = new Point(130, 103);
            textBox18.Name = "textBox18";
            textBox18.Size = new Size(284, 34);
            textBox18.TabIndex = 10;
            // 
            // textBox17
            // 
            textBox17.Location = new Point(130, 41);
            textBox17.Name = "textBox17";
            textBox17.Size = new Size(284, 34);
            textBox17.TabIndex = 9;
            // 
            // CountrySettingsSave
            // 
            CountrySettingsSave.Location = new Point(130, 230);
            CountrySettingsSave.Name = "CountrySettingsSave";
            CountrySettingsSave.Size = new Size(160, 44);
            CountrySettingsSave.TabIndex = 12;
            CountrySettingsSave.Text = "Speichern";
            CountrySettingsSave.UseVisualStyleBackColor = true;
            CountrySettingsSave.Click += SaveCountrySettings_Click;
            // 
            // CountryPrefixLabel
            // 
            CountryPrefixLabel.AutoSize = true;
            CountryPrefixLabel.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            CountryPrefixLabel.Location = new Point(43, 315);
            CountryPrefixLabel.Name = "CountryPrefixLabel";
            CountryPrefixLabel.Size = new Size(165, 28);
            CountryPrefixLabel.TabIndex = 13;
            CountryPrefixLabel.Text = "Ländervorwahl:";
            // 
            // CountryPrefixValueLabel
            // 
            CountryPrefixValueLabel.AutoSize = true;
            CountryPrefixValueLabel.Font = new Font("Segoe UI", 12F);
            CountryPrefixValueLabel.Location = new Point(230, 315);
            CountryPrefixValueLabel.Name = "CountryPrefixValueLabel";
            CountryPrefixValueLabel.Size = new Size(20, 28);
            CountryPrefixValueLabel.TabIndex = 14;
            CountryPrefixValueLabel.Text = "-";
            // 
            // AreaPrefixLabel
            // 
            AreaPrefixLabel.AutoSize = true;
            AreaPrefixLabel.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            AreaPrefixLabel.Location = new Point(43, 360);
            AreaPrefixLabel.Name = "AreaPrefixLabel";
            AreaPrefixLabel.Size = new Size(134, 28);
            AreaPrefixLabel.TabIndex = 15;
            AreaPrefixLabel.Text = "Ortsvorwahl:";
            // 
            // AreaPrefixValueLabel
            // 
            AreaPrefixValueLabel.AutoSize = true;
            AreaPrefixValueLabel.Font = new Font("Segoe UI", 12F);
            AreaPrefixValueLabel.Location = new Point(230, 360);
            AreaPrefixValueLabel.Name = "AreaPrefixValueLabel";
            AreaPrefixValueLabel.Size = new Size(20, 28);
            AreaPrefixValueLabel.TabIndex = 16;
            AreaPrefixValueLabel.Text = "-";
            // 
            // CountryReferenceValueLabel
            // 
            CountryReferenceValueLabel.AutoSize = true;
            CountryReferenceValueLabel.Font = new Font("Segoe UI", 10F);
            CountryReferenceValueLabel.Location = new Point(43, 410);
            CountryReferenceValueLabel.Name = "CountryReferenceValueLabel";
            CountryReferenceValueLabel.Size = new Size(100, 23);
            CountryReferenceValueLabel.TabIndex = 17;
            CountryReferenceValueLabel.Text = "CountryID: -";
            // 
            // AreaReferenceValueLabel
            // 
            AreaReferenceValueLabel.AutoSize = true;
            AreaReferenceValueLabel.Font = new Font("Segoe UI", 10F);
            AreaReferenceValueLabel.Location = new Point(43, 440);
            AreaReferenceValueLabel.Name = "AreaReferenceValueLabel";
            AreaReferenceValueLabel.Size = new Size(74, 23);
            AreaReferenceValueLabel.TabIndex = 18;
            AreaReferenceValueLabel.Text = "AreaID: -";
            // 
            // label12
            // 
            label12.AutoSize = true;
            label12.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            label12.Location = new Point(43, 165);
            label12.Name = "label12";
            label12.Size = new Size(52, 28);
            label12.TabIndex = 8;
            label12.Text = "PLZ:";
            // 
            // label11
            // 
            label11.AutoSize = true;
            label11.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            label11.Location = new Point(43, 103);
            label11.Name = "label11";
            label11.Size = new Size(67, 28);
            label11.TabIndex = 7;
            label11.Text = "Stadt:";
            // 
            // label10
            // 
            label10.AutoSize = true;
            label10.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            label10.Location = new Point(43, 41);
            label10.Name = "label10";
            label10.Size = new Size(62, 28);
            label10.TabIndex = 6;
            label10.Text = "Land:";
            // 
            // panel1
            // 
            panel1.Controls.Add(RemoveWindowsService);
            panel1.Controls.Add(InstallWindowsService);
            panel1.Controls.Add(RefreshFirewallState);
            panel1.Controls.Add(RemoveFirewallRule);
            panel1.Controls.Add(SetFirewall);
            panel1.Dock = DockStyle.Fill;
            panel1.Location = new Point(630, 362);
            panel1.Margin = new Padding(3, 4, 3, 4);
            panel1.Name = "panel1";
            panel1.Size = new Size(337, 351);
            panel1.TabIndex = 1;
            // 
            // RefreshFirewallState
            // 
            RefreshFirewallState.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            RefreshFirewallState.Location = new Point(51, 172);
            RefreshFirewallState.Margin = new Padding(3, 4, 3, 4);
            RefreshFirewallState.MaximumSize = new Size(229, 49);
            RefreshFirewallState.MinimumSize = new Size(229, 49);
            RefreshFirewallState.Name = "RefreshFirewallState";
            RefreshFirewallState.Size = new Size(229, 49);
            RefreshFirewallState.TabIndex = 22;
            RefreshFirewallState.Text = "Firewall Status";
            RefreshFirewallState.UseVisualStyleBackColor = true;
            RefreshFirewallState.Click += button5_Click;
            // 
            // InstallWindowsService
            // 
            InstallWindowsService.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            InstallWindowsService.Location = new Point(51, 244);
            InstallWindowsService.Margin = new Padding(3, 4, 3, 4);
            InstallWindowsService.MaximumSize = new Size(229, 49);
            InstallWindowsService.MinimumSize = new Size(229, 49);
            InstallWindowsService.Name = "InstallWindowsService";
            InstallWindowsService.Size = new Size(229, 49);
            InstallWindowsService.TabIndex = 23;
            InstallWindowsService.Text = "Service installieren";
            InstallWindowsService.UseVisualStyleBackColor = true;
            InstallWindowsService.Click += InstallWindowsService_Click;
            // 
            // RemoveWindowsService
            // 
            RemoveWindowsService.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            RemoveWindowsService.Location = new Point(51, 300);
            RemoveWindowsService.Margin = new Padding(3, 4, 3, 4);
            RemoveWindowsService.MaximumSize = new Size(229, 49);
            RemoveWindowsService.MinimumSize = new Size(229, 49);
            RemoveWindowsService.Name = "RemoveWindowsService";
            RemoveWindowsService.Size = new Size(229, 49);
            RemoveWindowsService.TabIndex = 24;
            RemoveWindowsService.Text = "Service löschen";
            RemoveWindowsService.UseVisualStyleBackColor = true;
            RemoveWindowsService.Click += RemoveWindowsService_Click;
            // 
            // RemoveFirewallRule
            // 
            RemoveFirewallRule.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            RemoveFirewallRule.Location = new Point(51, 100);
            RemoveFirewallRule.Margin = new Padding(3, 4, 3, 4);
            RemoveFirewallRule.MaximumSize = new Size(229, 49);
            RemoveFirewallRule.MinimumSize = new Size(229, 49);
            RemoveFirewallRule.Name = "RemoveFirewallRule";
            RemoveFirewallRule.Size = new Size(229, 49);
            RemoveFirewallRule.TabIndex = 21;
            RemoveFirewallRule.Text = "Firewall freigabe löschen";
            RemoveFirewallRule.UseVisualStyleBackColor = true;
            RemoveFirewallRule.Click += button4_Click;
            // 
            // SetFirewall
            // 
            SetFirewall.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            SetFirewall.Location = new Point(51, 28);
            SetFirewall.Margin = new Padding(3, 4, 3, 4);
            SetFirewall.MaximumSize = new Size(229, 49);
            SetFirewall.MinimumSize = new Size(229, 49);
            SetFirewall.Name = "SetFirewall";
            SetFirewall.Size = new Size(229, 49);
            SetFirewall.TabIndex = 20;
            SetFirewall.Text = "Firewall setzen";
            SetFirewall.UseVisualStyleBackColor = true;
            SetFirewall.Click += button2_Click;
            // 
            // listView1
            // 
            listView1.Columns.AddRange(new ColumnHeader[] { columnHeader1, columnHeader2, columnHeader3 });
            listView1.Dock = DockStyle.Fill;
            listView1.FullRowSelect = true;
            listView1.GridLines = true;
            listView1.Location = new Point(630, 4);
            listView1.Margin = new Padding(3, 4, 3, 4);
            listView1.MultiSelect = false;
            listView1.Name = "listView1";
            listView1.Size = new Size(337, 350);
            listView1.TabIndex = 2;
            listView1.UseCompatibleStateImageBehavior = false;
            listView1.View = View.Details;
            // 
            // columnHeader1
            // 
            columnHeader1.Text = "Regel";
            columnHeader1.Width = 150;
            // 
            // columnHeader2
            // 
            columnHeader2.Text = "Status";
            columnHeader2.Width = 70;
            // 
            // columnHeader3
            // 
            columnHeader3.Text = "Info";
            columnHeader3.Width = 300;
            // 
            // textBox16
            // 
            textBox16.Location = new Point(289, 293);
            textBox16.Margin = new Padding(3, 4, 3, 4);
            textBox16.MaximumSize = new Size(230, 35);
            textBox16.MinimumSize = new Size(230, 35);
            textBox16.Name = "textBox16";
            textBox16.Size = new Size(230, 35);
            textBox16.TabIndex = 28;
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            label9.Location = new Point(289, 258);
            label9.Name = "label9";
            label9.Size = new Size(165, 28);
            label9.TabIndex = 27;
            label9.Text = "GSM Audio port";
            // 
            // TK_ServerEinstellung
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(970, 717);
            Controls.Add(tableLayoutPanel1);
            Margin = new Padding(3, 4, 3, 4);
            Name = "TK_ServerEinstellung";
            Text = "TKServerEinstellung";
            Load += TKServerEinstellung_Load;
            tableLayoutPanel1.ResumeLayout(false);
            tabControl1.ResumeLayout(false);
            tabPage1.ResumeLayout(false);
            tabPage1.PerformLayout();
            tabPage2.ResumeLayout(false);
            tabPage2.PerformLayout();
            tabPage3.ResumeLayout(false);
            tabPage3.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)AudioCodecGrid).EndInit();
            AudioCodecLayout.ResumeLayout(false);
            tabPage4.ResumeLayout(false);
            tabPage5.ResumeLayout(false);
            tabPage5.PerformLayout();
            tabPage6.ResumeLayout(false);
            tabPage6.PerformLayout();
            panel1.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel tableLayoutPanel1;
    private TabControl tabControl1;
    private TabPage tabPage1;
    private TextBox textBox6;
    private TextBox textBox5;
    private TextBox textBox4;
    private TextBox textBox3;
    private TextBox textBox2;
    private TextBox textBox1;
    private TabPage tabPage2;
    private Panel panel1;
    private ListView listView1;
    private ColumnHeader columnHeader1;
    private ColumnHeader columnHeader2;
    private ColumnHeader columnHeader3;
    private Label StunServer;
    private Label SIPProtokoll;
    private Label SIPPort;
    private Label SIPProxy;
    private Label SIPRegistrar;
    private Label SIPDomain;
    private Label Codecs;
    private Label label2;
    private TextBox textBox9;
    private TextBox textBox8;
    private Label label1;
    private TextBox textBox7;
    private CheckBox UseSRTP;
        private Button SetFirewall;
        private Button TKsave;
        private TextBox textBox10;
        private Label label7;
        private Label label6;
        private Label label5;
        private Label label4;
        private Label label3;
        private TextBox textBox15;
        private TextBox textBox14;
        private TextBox textBox13;
        private TextBox textBox12;
        private TextBox textBox11;
        private ComboBox comboBox1;
        private CheckBox checkBox3;
        private CheckBox checkBox2;
        private CheckBox checkBox1;
        private Button button3;
        private Button RefreshFirewallState;
        private Button RemoveFirewallRule;
        private Button InstallWindowsService;
        private Button RemoveWindowsService;
        private Label label8;
        private Label OutboundProxyLabel;
        private Label RealmLabel;
        private TextBox textBox21;
        private TextBox textBox20;
        private TabPage tabPage3;
        private TextBox textBox19;
        private TextBox textBox18;
        private TextBox textBox17;
        private Button CountrySettingsSave;
        private Label CountryPrefixLabel;
        private Label CountryPrefixValueLabel;
        private Label AreaPrefixLabel;
        private Label AreaPrefixValueLabel;
        private Label CountryReferenceValueLabel;
        private Label AreaReferenceValueLabel;
        private Label label12;
        private Label label11;
        private Label label10;
        private TextBox textBox16;
        private Label label9;
        private TabPage tabPage4;
        private TableLayoutPanel AudioCodecLayout;
        private Label AudioCodecCurrentLabel;
        private DataGridView AudioCodecGrid;
        private DataGridViewTextBoxColumn AudioCodec_ID;
        private DataGridViewTextBoxColumn AudioCodec_CodecKey;
        private DataGridViewTextBoxColumn AudioCodec_DisplayName;
        private DataGridViewTextBoxColumn AudioCodec_SupportsSip;
        private DataGridViewTextBoxColumn AudioCodec_SupportsRtp;
        private DataGridViewTextBoxColumn AudioCodec_SupportsGsmBridge;
        private DataGridViewTextBoxColumn AudioCodec_Active;
        private Label AudioCodecSelectedLabel;
        private Button AudioCodecSave;
        private Button AudioCodecGsmSave;
        private TabPage tabPage5;
        private CheckBox AudioFoldersUseAdsProfileCheckBox;
        private TextBox AudioFoldersRootPathTextBox;
        private TextBox AudioFoldersFolderNameTextBox;
        private Button AudioFoldersSaveButton;
        private Label AudioFoldersInfoLabel;
        private Label AudioFoldersUseAdsProfileLabel;
        private Label AudioFoldersRootPathLabel;
        private Label AudioFoldersFolderNameLabel;
        private TabPage tabPage6;
        private Label LoggingLevelLabel;
        private ComboBox LoggingLevelComboBox;
        private CheckBox LoggingDebugCheckBox;
        private Button LoggingSaveButton;
        private Label LoggingInfoLabel;
    }
}