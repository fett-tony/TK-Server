namespace TKServer.FormView {
    partial class TK_Server
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        // --- NEU: Context Menü für Logger ---

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(TK_Server));
            tableLayoutPanel1 = new TableLayoutPanel();
            menuStrip1 = new MenuStrip();
            dateiToolStripMenuItem1 = new ToolStripMenuItem();
            neuToolStripMenuItem = new ToolStripMenuItem();
            öffnenToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator = new ToolStripSeparator();
            speichernToolStripMenuItem1 = new ToolStripMenuItem();
            speichernunterToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator1 = new ToolStripSeparator();
            druckenToolStripMenuItem = new ToolStripMenuItem();
            seitenansichtToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator2 = new ToolStripSeparator();
            beendenToolStripMenuItem1 = new ToolStripMenuItem();
            bearbeitenToolStripMenuItem1 = new ToolStripMenuItem();
            rückgängigToolStripMenuItem = new ToolStripMenuItem();
            wiederholenToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator3 = new ToolStripSeparator();
            ausschneidenToolStripMenuItem = new ToolStripMenuItem();
            kopierenToolStripMenuItem1 = new ToolStripMenuItem();
            einfügenToolStripMenuItem1 = new ToolStripMenuItem();
            toolStripSeparator4 = new ToolStripSeparator();
            allesauswählenToolStripMenuItem = new ToolStripMenuItem();
            extrasToolStripMenuItem = new ToolStripMenuItem();
            anpassenToolStripMenuItem = new ToolStripMenuItem();
            optionenToolStripMenuItem = new ToolStripMenuItem();
            GSMGeräteToolStripMenuItem = new ToolStripMenuItem();
            ProviderAccountsToolStripMenuItem = new ToolStripMenuItem();
            callLToolStripMenuItem = new ToolStripMenuItem();
            liveCallStateToolStripMenuItem = new ToolStripMenuItem();
            ServiceNummern = new ToolStripMenuItem();
            ServiceStart = new ToolStripMenuItem();
            ServiceStop = new ToolStripMenuItem();
            hilfeToolStripMenuItem1 = new ToolStripMenuItem();
            inhaltToolStripMenuItem = new ToolStripMenuItem();
            indexToolStripMenuItem = new ToolStripMenuItem();
            suchenToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator5 = new ToolStripSeparator();
            infoToolStripMenuItem = new ToolStripMenuItem();
            statusStrip1 = new StatusStrip();
            toolStripStatusLabel1 = new ToolStripStatusLabel();
            toolStripStatusLabel2 = new ToolStripStatusLabel();
            toolStripStatusLabel3 = new ToolStripStatusLabel();
            toolStripStatusLabel4 = new ToolStripStatusLabel();
            toolStripStatusLabel5 = new ToolStripStatusLabel();
            NavigationPanel = new Panel();
            TKServices = new Button();
            LiveCallState = new Button();
            OverView = new Button();
            VoIP_Clients = new Button();
            VoIP_User = new Button();
            Provider_Accounts = new Button();
            GSMGeräte = new Button();
            richTextBox1 = new RichTextBox();
            contextMenuStrip1 = new ContextMenuStrip(components);
            rtbKopierenToolStripMenuItem = new ToolStripMenuItem();
            rtbAllesMarkierenToolStripMenuItem = new ToolStripMenuItem();
            rtbLeerenToolStripMenuItem = new ToolStripMenuItem();
            rtbSpeichernToolStripMenuItem = new ToolStripMenuItem();
            ContentPanel = new Panel();
            tableLayoutPanel1.SuspendLayout();
            menuStrip1.SuspendLayout();
            statusStrip1.SuspendLayout();
            NavigationPanel.SuspendLayout();
            contextMenuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.ColumnCount = 2;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 286F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.Controls.Add(menuStrip1, 0, 0);
            tableLayoutPanel1.Controls.Add(statusStrip1, 0, 3);
            tableLayoutPanel1.Controls.Add(NavigationPanel, 0, 1);
            tableLayoutPanel1.Controls.Add(richTextBox1, 0, 2);
            tableLayoutPanel1.Controls.Add(ContentPanel, 1, 1);
            tableLayoutPanel1.Dock = DockStyle.Fill;
            tableLayoutPanel1.Location = new Point(0, 0);
            tableLayoutPanel1.Margin = new Padding(3, 4, 3, 4);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 4;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 33F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 33F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 27F));
            tableLayoutPanel1.Size = new Size(1483, 961);
            tableLayoutPanel1.TabIndex = 0;
            // 
            // menuStrip1
            // 
            tableLayoutPanel1.SetColumnSpan(menuStrip1, 2);
            menuStrip1.Dock = DockStyle.Fill;
            menuStrip1.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            menuStrip1.ImageScalingSize = new Size(20, 20);
            menuStrip1.Items.AddRange(new ToolStripItem[] { dateiToolStripMenuItem1, bearbeitenToolStripMenuItem1, extrasToolStripMenuItem, hilfeToolStripMenuItem1 });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Padding = new Padding(7, 3, 0, 3);
            menuStrip1.Size = new Size(1483, 33);
            menuStrip1.TabIndex = 0;
            menuStrip1.Text = "menuStrip1";
            // 
            // dateiToolStripMenuItem1
            // 
            dateiToolStripMenuItem1.DropDownItems.AddRange(new ToolStripItem[] { neuToolStripMenuItem, öffnenToolStripMenuItem, toolStripSeparator, speichernToolStripMenuItem1, speichernunterToolStripMenuItem, toolStripSeparator1, druckenToolStripMenuItem, seitenansichtToolStripMenuItem, toolStripSeparator2, beendenToolStripMenuItem1 });
            dateiToolStripMenuItem1.Name = "dateiToolStripMenuItem1";
            dateiToolStripMenuItem1.Size = new Size(67, 27);
            dateiToolStripMenuItem1.Text = "&Datei";
            // 
            // neuToolStripMenuItem
            // 
            neuToolStripMenuItem.Image = (Image)resources.GetObject("neuToolStripMenuItem.Image");
            neuToolStripMenuItem.ImageTransparentColor = Color.Magenta;
            neuToolStripMenuItem.Name = "neuToolStripMenuItem";
            neuToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.N;
            neuToolStripMenuItem.Size = new Size(240, 28);
            neuToolStripMenuItem.Text = "&Neu";
            // 
            // öffnenToolStripMenuItem
            // 
            öffnenToolStripMenuItem.Image = (Image)resources.GetObject("öffnenToolStripMenuItem.Image");
            öffnenToolStripMenuItem.ImageTransparentColor = Color.Magenta;
            öffnenToolStripMenuItem.Name = "öffnenToolStripMenuItem";
            öffnenToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.O;
            öffnenToolStripMenuItem.Size = new Size(240, 28);
            öffnenToolStripMenuItem.Text = "Ö&ffnen";
            // 
            // toolStripSeparator
            // 
            toolStripSeparator.Name = "toolStripSeparator";
            toolStripSeparator.Size = new Size(237, 6);
            // 
            // speichernToolStripMenuItem1
            // 
            speichernToolStripMenuItem1.Image = (Image)resources.GetObject("speichernToolStripMenuItem1.Image");
            speichernToolStripMenuItem1.ImageTransparentColor = Color.Magenta;
            speichernToolStripMenuItem1.Name = "speichernToolStripMenuItem1";
            speichernToolStripMenuItem1.ShortcutKeys = Keys.Control | Keys.S;
            speichernToolStripMenuItem1.Size = new Size(240, 28);
            speichernToolStripMenuItem1.Text = "&Speichern";
            // 
            // speichernunterToolStripMenuItem
            // 
            speichernunterToolStripMenuItem.Name = "speichernunterToolStripMenuItem";
            speichernunterToolStripMenuItem.Size = new Size(240, 28);
            speichernunterToolStripMenuItem.Text = "Speichern &unter";
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new Size(237, 6);
            // 
            // druckenToolStripMenuItem
            // 
            druckenToolStripMenuItem.Image = (Image)resources.GetObject("druckenToolStripMenuItem.Image");
            druckenToolStripMenuItem.ImageTransparentColor = Color.Magenta;
            druckenToolStripMenuItem.Name = "druckenToolStripMenuItem";
            druckenToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.P;
            druckenToolStripMenuItem.Size = new Size(240, 28);
            druckenToolStripMenuItem.Text = "&Drucken";
            // 
            // seitenansichtToolStripMenuItem
            // 
            seitenansichtToolStripMenuItem.Image = (Image)resources.GetObject("seitenansichtToolStripMenuItem.Image");
            seitenansichtToolStripMenuItem.ImageTransparentColor = Color.Magenta;
            seitenansichtToolStripMenuItem.Name = "seitenansichtToolStripMenuItem";
            seitenansichtToolStripMenuItem.Size = new Size(240, 28);
            seitenansichtToolStripMenuItem.Text = "&Seitenansicht";
            // 
            // toolStripSeparator2
            // 
            toolStripSeparator2.Name = "toolStripSeparator2";
            toolStripSeparator2.Size = new Size(237, 6);
            // 
            // beendenToolStripMenuItem1
            // 
            beendenToolStripMenuItem1.Name = "beendenToolStripMenuItem1";
            beendenToolStripMenuItem1.Size = new Size(240, 28);
            beendenToolStripMenuItem1.Text = "&Beenden";
            // 
            // bearbeitenToolStripMenuItem1
            // 
            bearbeitenToolStripMenuItem1.DropDownItems.AddRange(new ToolStripItem[] { rückgängigToolStripMenuItem, wiederholenToolStripMenuItem, toolStripSeparator3, ausschneidenToolStripMenuItem, kopierenToolStripMenuItem1, einfügenToolStripMenuItem1, toolStripSeparator4, allesauswählenToolStripMenuItem });
            bearbeitenToolStripMenuItem1.Name = "bearbeitenToolStripMenuItem1";
            bearbeitenToolStripMenuItem1.Size = new Size(111, 27);
            bearbeitenToolStripMenuItem1.Text = "&Bearbeiten";
            // 
            // rückgängigToolStripMenuItem
            // 
            rückgängigToolStripMenuItem.Name = "rückgängigToolStripMenuItem";
            rückgängigToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.Z;
            rückgängigToolStripMenuItem.Size = new Size(270, 28);
            rückgängigToolStripMenuItem.Text = "&Rückgängig";
            // 
            // wiederholenToolStripMenuItem
            // 
            wiederholenToolStripMenuItem.Name = "wiederholenToolStripMenuItem";
            wiederholenToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.Y;
            wiederholenToolStripMenuItem.Size = new Size(270, 28);
            wiederholenToolStripMenuItem.Text = "&Wiederholen";
            // 
            // toolStripSeparator3
            // 
            toolStripSeparator3.Name = "toolStripSeparator3";
            toolStripSeparator3.Size = new Size(267, 6);
            // 
            // ausschneidenToolStripMenuItem
            // 
            ausschneidenToolStripMenuItem.Image = (Image)resources.GetObject("ausschneidenToolStripMenuItem.Image");
            ausschneidenToolStripMenuItem.ImageTransparentColor = Color.Magenta;
            ausschneidenToolStripMenuItem.Name = "ausschneidenToolStripMenuItem";
            ausschneidenToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.X;
            ausschneidenToolStripMenuItem.Size = new Size(270, 28);
            ausschneidenToolStripMenuItem.Text = "Aussc&hneiden";
            // 
            // kopierenToolStripMenuItem1
            // 
            kopierenToolStripMenuItem1.Image = (Image)resources.GetObject("kopierenToolStripMenuItem1.Image");
            kopierenToolStripMenuItem1.ImageTransparentColor = Color.Magenta;
            kopierenToolStripMenuItem1.Name = "kopierenToolStripMenuItem1";
            kopierenToolStripMenuItem1.ShortcutKeys = Keys.Control | Keys.C;
            kopierenToolStripMenuItem1.Size = new Size(270, 28);
            kopierenToolStripMenuItem1.Text = "&Kopieren";
            // 
            // einfügenToolStripMenuItem1
            // 
            einfügenToolStripMenuItem1.Image = (Image)resources.GetObject("einfügenToolStripMenuItem1.Image");
            einfügenToolStripMenuItem1.ImageTransparentColor = Color.Magenta;
            einfügenToolStripMenuItem1.Name = "einfügenToolStripMenuItem1";
            einfügenToolStripMenuItem1.ShortcutKeys = Keys.Control | Keys.V;
            einfügenToolStripMenuItem1.Size = new Size(270, 28);
            einfügenToolStripMenuItem1.Text = "&Einfügen";
            // 
            // toolStripSeparator4
            // 
            toolStripSeparator4.Name = "toolStripSeparator4";
            toolStripSeparator4.Size = new Size(267, 6);
            // 
            // allesauswählenToolStripMenuItem
            // 
            allesauswählenToolStripMenuItem.Name = "allesauswählenToolStripMenuItem";
            allesauswählenToolStripMenuItem.Size = new Size(270, 28);
            allesauswählenToolStripMenuItem.Text = "&Alles auswählen";
            // 
            // extrasToolStripMenuItem
            // 
            extrasToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { anpassenToolStripMenuItem, optionenToolStripMenuItem, GSMGeräteToolStripMenuItem, ProviderAccountsToolStripMenuItem, callLToolStripMenuItem, liveCallStateToolStripMenuItem, ServiceNummern, ServiceStart, ServiceStop });
            extrasToolStripMenuItem.Name = "extrasToolStripMenuItem";
            extrasToolStripMenuItem.Size = new Size(72, 27);
            extrasToolStripMenuItem.Text = "Extras";
            // 
            // anpassenToolStripMenuItem
            // 
            anpassenToolStripMenuItem.Name = "anpassenToolStripMenuItem";
            anpassenToolStripMenuItem.Size = new Size(240, 28);
            anpassenToolStripMenuItem.Text = "&Anpassen";
            // 
            // optionenToolStripMenuItem
            // 
            optionenToolStripMenuItem.Name = "optionenToolStripMenuItem";
            optionenToolStripMenuItem.Size = new Size(240, 28);
            optionenToolStripMenuItem.Text = "&Optionen";
            optionenToolStripMenuItem.Click += optionenToolStripMenuItem_Click;
            // 
            // GSMGeräteToolStripMenuItem
            // 
            GSMGeräteToolStripMenuItem.Name = "GSMGeräteToolStripMenuItem";
            GSMGeräteToolStripMenuItem.Size = new Size(240, 28);
            GSMGeräteToolStripMenuItem.Text = "GSM-Geräte";
            // 
            // ProviderAccountsToolStripMenuItem
            // 
            ProviderAccountsToolStripMenuItem.Name = "ProviderAccountsToolStripMenuItem";
            ProviderAccountsToolStripMenuItem.Size = new Size(240, 28);
            ProviderAccountsToolStripMenuItem.Text = "Provider-Accounts";
            // 
            // callLToolStripMenuItem
            // 
            callLToolStripMenuItem.Name = "callLToolStripMenuItem";
            callLToolStripMenuItem.Size = new Size(240, 28);
            callLToolStripMenuItem.Text = "CallLog";
            callLToolStripMenuItem.Click += callLToolStripMenuItem_Click;
            // 
            // liveCallStateToolStripMenuItem
            // 
            liveCallStateToolStripMenuItem.Name = "liveCallStateToolStripMenuItem";
            liveCallStateToolStripMenuItem.Size = new Size(240, 28);
            liveCallStateToolStripMenuItem.Text = "Live Call State";
            liveCallStateToolStripMenuItem.Click += liveCallStateToolStripMenuItem_Click;
            // 
            // ServiceNummern
            // 
            ServiceNummern.Name = "ServiceNummern";
            ServiceNummern.Size = new Size(240, 28);
            ServiceNummern.Text = "Service Nummern";
            ServiceNummern.Click += serviceNummernToolStripMenuItem_Click;
            // 
            // ServiceStart
            // 
            ServiceStart.Name = "ServiceStart";
            ServiceStart.Size = new Size(240, 28);
            ServiceStart.Text = "Service Start";
            ServiceStart.Click += ServiceStart_Click;
            // 
            // ServiceStop
            // 
            ServiceStop.Name = "ServiceStop";
            ServiceStop.Size = new Size(240, 28);
            ServiceStop.Text = "Service Stop";
            ServiceStop.Click += ServiceStop_Click;
            // 
            // hilfeToolStripMenuItem1
            // 
            hilfeToolStripMenuItem1.DropDownItems.AddRange(new ToolStripItem[] { inhaltToolStripMenuItem, indexToolStripMenuItem, suchenToolStripMenuItem, toolStripSeparator5, infoToolStripMenuItem });
            hilfeToolStripMenuItem1.Name = "hilfeToolStripMenuItem1";
            hilfeToolStripMenuItem1.Size = new Size(63, 27);
            hilfeToolStripMenuItem1.Text = "&Hilfe";
            // 
            // inhaltToolStripMenuItem
            // 
            inhaltToolStripMenuItem.Name = "inhaltToolStripMenuItem";
            inhaltToolStripMenuItem.Size = new Size(151, 28);
            inhaltToolStripMenuItem.Text = "&Inhalt";
            // 
            // indexToolStripMenuItem
            // 
            indexToolStripMenuItem.Name = "indexToolStripMenuItem";
            indexToolStripMenuItem.Size = new Size(151, 28);
            indexToolStripMenuItem.Text = "&Index";
            // 
            // suchenToolStripMenuItem
            // 
            suchenToolStripMenuItem.Name = "suchenToolStripMenuItem";
            suchenToolStripMenuItem.Size = new Size(151, 28);
            suchenToolStripMenuItem.Text = "&Suchen";
            // 
            // toolStripSeparator5
            // 
            toolStripSeparator5.Name = "toolStripSeparator5";
            toolStripSeparator5.Size = new Size(148, 6);
            // 
            // infoToolStripMenuItem
            // 
            infoToolStripMenuItem.Name = "infoToolStripMenuItem";
            infoToolStripMenuItem.Size = new Size(151, 28);
            infoToolStripMenuItem.Text = "Inf&o...";
            // 
            // statusStrip1
            // 
            tableLayoutPanel1.SetColumnSpan(statusStrip1, 2);
            statusStrip1.Dock = DockStyle.Fill;
            statusStrip1.ImageScalingSize = new Size(20, 20);
            statusStrip1.Items.AddRange(new ToolStripItem[] { toolStripStatusLabel1, toolStripStatusLabel2, toolStripStatusLabel3, toolStripStatusLabel4, toolStripStatusLabel5 });
            statusStrip1.Location = new Point(0, 927);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Padding = new Padding(1, 0, 16, 0);
            statusStrip1.RightToLeft = RightToLeft.No;
            statusStrip1.Size = new Size(1483, 34);
            statusStrip1.TabIndex = 1;
            statusStrip1.Text = "statusStrip1";
            // 
            // toolStripStatusLabel1
            // 
            toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            toolStripStatusLabel1.Size = new Size(116, 28);
            toolStripStatusLabel1.Text = "GSM: unbekannt";
            // 
            // toolStripStatusLabel2
            // 
            toolStripStatusLabel2.Name = "toolStripStatusLabel2";
            toolStripStatusLabel2.Size = new Size(123, 28);
            toolStripStatusLabel2.Text = "SipInt: unbekannt";
            // 
            // toolStripStatusLabel3
            // 
            toolStripStatusLabel3.Name = "toolStripStatusLabel3";
            toolStripStatusLabel3.Size = new Size(126, 28);
            toolStripStatusLabel3.Text = "SipExt: unbekannt";
            // 
            // toolStripStatusLabel4
            // 
            toolStripStatusLabel4.Name = "toolStripStatusLabel4";
            toolStripStatusLabel4.Size = new Size(87, 28);
            toolStripStatusLabel4.Text = "GSM Busy: -";
            // 
            // toolStripStatusLabel5
            // 
            toolStripStatusLabel5.Name = "toolStripStatusLabel5";
            toolStripStatusLabel5.Size = new Size(60, 28);
            toolStripStatusLabel5.Text = "Stand: -";
            // 
            // NavigationPanel
            // 
            NavigationPanel.Controls.Add(TKServices);
            NavigationPanel.Controls.Add(LiveCallState);
            NavigationPanel.Controls.Add(OverView);
            NavigationPanel.Controls.Add(VoIP_Clients);
            NavigationPanel.Controls.Add(VoIP_User);
            NavigationPanel.Controls.Add(Provider_Accounts);
            NavigationPanel.Controls.Add(GSMGeräte);
            NavigationPanel.Dock = DockStyle.Fill;
            NavigationPanel.Location = new Point(3, 37);
            NavigationPanel.Margin = new Padding(3, 4, 3, 4);
            NavigationPanel.Name = "NavigationPanel";
            NavigationPanel.Size = new Size(280, 439);
            NavigationPanel.TabIndex = 2;
            // 
            // TKServices
            // 
            TKServices.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold);
            TKServices.Location = new Point(41, 385);
            TKServices.Margin = new Padding(3, 4, 3, 4);
            TKServices.MaximumSize = new Size(194, 40);
            TKServices.MinimumSize = new Size(194, 40);
            TKServices.Name = "TKServices";
            TKServices.Size = new Size(194, 40);
            TKServices.TabIndex = 5;
            TKServices.Text = "TK-Services";
            TKServices.UseVisualStyleBackColor = true;
            TKServices.Click += TKServices_Click;
            // 
            // LiveCallState
            // 
            LiveCallState.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold);
            LiveCallState.Location = new Point(41, 75);
            LiveCallState.Margin = new Padding(3, 4, 3, 4);
            LiveCallState.MaximumSize = new Size(194, 40);
            LiveCallState.MinimumSize = new Size(194, 40);
            LiveCallState.Name = "LiveCallState";
            LiveCallState.Size = new Size(194, 40);
            LiveCallState.TabIndex = 6;
            LiveCallState.Text = "Live Call State";
            LiveCallState.UseVisualStyleBackColor = true;
            LiveCallState.Click += LiveCallState_Click;
            // 
            // OverView
            // 
            OverView.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold);
            OverView.Location = new Point(41, 13);
            OverView.Margin = new Padding(3, 4, 3, 4);
            OverView.MaximumSize = new Size(194, 40);
            OverView.MinimumSize = new Size(194, 40);
            OverView.Name = "OverView";
            OverView.Size = new Size(194, 40);
            OverView.TabIndex = 4;
            OverView.Text = "Übersicht";
            OverView.UseVisualStyleBackColor = true;
            OverView.Click += OverView_Click;
            // 
            // VoIP_Clients
            // 
            VoIP_Clients.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold);
            VoIP_Clients.Location = new Point(41, 323);
            VoIP_Clients.Margin = new Padding(3, 4, 3, 4);
            VoIP_Clients.MaximumSize = new Size(194, 40);
            VoIP_Clients.MinimumSize = new Size(194, 40);
            VoIP_Clients.Name = "VoIP_Clients";
            VoIP_Clients.Size = new Size(194, 40);
            VoIP_Clients.TabIndex = 3;
            VoIP_Clients.Text = "VoIP-Nummern";
            VoIP_Clients.UseVisualStyleBackColor = true;
            VoIP_Clients.Click += VoIP_Clients_Click;
            // 
            // VoIP_User
            // 
            VoIP_User.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold);
            VoIP_User.Location = new Point(41, 261);
            VoIP_User.Margin = new Padding(3, 4, 3, 4);
            VoIP_User.MaximumSize = new Size(194, 40);
            VoIP_User.MinimumSize = new Size(194, 40);
            VoIP_User.Name = "VoIP_User";
            VoIP_User.Size = new Size(194, 40);
            VoIP_User.TabIndex = 2;
            VoIP_User.Text = "VoIP-User";
            VoIP_User.UseVisualStyleBackColor = true;
            VoIP_User.Click += VoIP_User_Click;
            // 
            // Provider_Accounts
            // 
            Provider_Accounts.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold);
            Provider_Accounts.Location = new Point(41, 199);
            Provider_Accounts.Margin = new Padding(3, 4, 3, 4);
            Provider_Accounts.MaximumSize = new Size(194, 40);
            Provider_Accounts.MinimumSize = new Size(194, 40);
            Provider_Accounts.Name = "Provider_Accounts";
            Provider_Accounts.Size = new Size(194, 40);
            Provider_Accounts.TabIndex = 1;
            Provider_Accounts.Text = "Externe SIP Kontos";
            Provider_Accounts.UseVisualStyleBackColor = true;
            Provider_Accounts.Click += Provider_Accounts_Click;
            // 
            // GSMGeräte
            // 
            GSMGeräte.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold);
            GSMGeräte.Location = new Point(41, 137);
            GSMGeräte.Margin = new Padding(3, 4, 3, 4);
            GSMGeräte.MaximumSize = new Size(194, 40);
            GSMGeräte.MinimumSize = new Size(194, 40);
            GSMGeräte.Name = "GSMGeräte";
            GSMGeräte.Size = new Size(194, 40);
            GSMGeräte.TabIndex = 0;
            GSMGeräte.Text = "GSM-Geräte";
            GSMGeräte.UseVisualStyleBackColor = true;
            GSMGeräte.Click += GSMGeräte_Click;
            // 
            // richTextBox1
            // 
            richTextBox1.BackColor = SystemColors.MenuHighlight;
            richTextBox1.ContextMenuStrip = contextMenuStrip1;
            richTextBox1.Dock = DockStyle.Fill;
            richTextBox1.Font = new Font("Courier New", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            richTextBox1.Location = new Point(3, 484);
            richTextBox1.Margin = new Padding(3, 4, 3, 4);
            richTextBox1.Name = "richTextBox1";
            richTextBox1.Size = new Size(280, 439);
            richTextBox1.TabIndex = 3;
            richTextBox1.Text = "";
            // 
            // contextMenuStrip1
            // 
            contextMenuStrip1.ImageScalingSize = new Size(20, 20);
            contextMenuStrip1.Items.AddRange(new ToolStripItem[] { rtbKopierenToolStripMenuItem, rtbAllesMarkierenToolStripMenuItem, rtbLeerenToolStripMenuItem, rtbSpeichernToolStripMenuItem });
            contextMenuStrip1.Name = "contextMenuStrip1";
            contextMenuStrip1.Size = new Size(181, 100);
            // 
            // rtbKopierenToolStripMenuItem
            // 
            rtbKopierenToolStripMenuItem.Name = "rtbKopierenToolStripMenuItem";
            rtbKopierenToolStripMenuItem.Size = new Size(180, 24);
            rtbKopierenToolStripMenuItem.Text = "Kopieren";
            rtbKopierenToolStripMenuItem.Click += rtbKopierenToolStripMenuItem_Click;
            // 
            // rtbAllesMarkierenToolStripMenuItem
            // 
            rtbAllesMarkierenToolStripMenuItem.Name = "rtbAllesMarkierenToolStripMenuItem";
            rtbAllesMarkierenToolStripMenuItem.Size = new Size(180, 24);
            rtbAllesMarkierenToolStripMenuItem.Text = "Alles markieren";
            rtbAllesMarkierenToolStripMenuItem.Click += rtbAllesMarkierenToolStripMenuItem_Click;
            // 
            // rtbLeerenToolStripMenuItem
            // 
            rtbLeerenToolStripMenuItem.Name = "rtbLeerenToolStripMenuItem";
            rtbLeerenToolStripMenuItem.Size = new Size(180, 24);
            rtbLeerenToolStripMenuItem.Text = "Log löschen";
            rtbLeerenToolStripMenuItem.Click += rtbLeerenToolStripMenuItem_Click;
            // 
            // rtbSpeichernToolStripMenuItem
            // 
            rtbSpeichernToolStripMenuItem.Name = "rtbSpeichernToolStripMenuItem";
            rtbSpeichernToolStripMenuItem.Size = new Size(180, 24);
            rtbSpeichernToolStripMenuItem.Text = "Log speichern";
            rtbSpeichernToolStripMenuItem.Click += rtbSpeichernToolStripMenuItem_Click;
            // 
            // ContentPanel
            // 
            ContentPanel.BorderStyle = BorderStyle.Fixed3D;
            ContentPanel.Dock = DockStyle.Fill;
            ContentPanel.Location = new Point(289, 37);
            ContentPanel.Margin = new Padding(3, 4, 3, 4);
            ContentPanel.Name = "ContentPanel";
            tableLayoutPanel1.SetRowSpan(ContentPanel, 2);
            ContentPanel.Size = new Size(1191, 886);
            ContentPanel.TabIndex = 4;
            // 
            // TK_Server
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1483, 961);
            Controls.Add(tableLayoutPanel1);
            MainMenuStrip = menuStrip1;
            Margin = new Padding(3, 4, 3, 4);
            MinimumSize = new Size(1499, 998);
            Name = "TK_Server";
            Text = "TKServer";
            Load += TKServer_Load;
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            NavigationPanel.ResumeLayout(false);
            contextMenuStrip1.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion


        private ContextMenuStrip contextMenuStrip1;
        private ToolStripMenuItem rtbKopierenToolStripMenuItem;
        private ToolStripMenuItem rtbAllesMarkierenToolStripMenuItem;
        private ToolStripMenuItem rtbLeerenToolStripMenuItem;
        private ToolStripMenuItem rtbSpeichernToolStripMenuItem;

        private TableLayoutPanel tableLayoutPanel1;
        private MenuStrip menuStrip1;
        private StatusStrip statusStrip1;
        private Panel NavigationPanel;
        private Button VoIP_Clients;
        private Button VoIP_User;
        private Button Provider_Accounts;
        private RichTextBox richTextBox1;
        private ToolStripStatusLabel toolStripStatusLabel1;
        private Panel ContentPanel;
        private ToolStripMenuItem dateiToolStripMenuItem1;
        private ToolStripMenuItem neuToolStripMenuItem;
        private ToolStripMenuItem öffnenToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator;
        private ToolStripMenuItem speichernToolStripMenuItem1;
        private ToolStripMenuItem speichernunterToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripMenuItem druckenToolStripMenuItem;
        private ToolStripMenuItem seitenansichtToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator2;
        private ToolStripMenuItem beendenToolStripMenuItem1;
        private ToolStripMenuItem bearbeitenToolStripMenuItem1;
        private ToolStripMenuItem rückgängigToolStripMenuItem;
        private ToolStripMenuItem wiederholenToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator3;
        private ToolStripMenuItem ausschneidenToolStripMenuItem;
        private ToolStripMenuItem kopierenToolStripMenuItem1;
        private ToolStripMenuItem einfügenToolStripMenuItem1;
        private ToolStripSeparator toolStripSeparator4;
        private ToolStripMenuItem allesauswählenToolStripMenuItem;
        private ToolStripMenuItem extrasToolStripMenuItem;
        private ToolStripMenuItem anpassenToolStripMenuItem;
        private ToolStripMenuItem optionenToolStripMenuItem;
        private ToolStripMenuItem hilfeToolStripMenuItem1;
        private ToolStripMenuItem inhaltToolStripMenuItem;
        private ToolStripMenuItem indexToolStripMenuItem;
        private ToolStripMenuItem suchenToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator5;
        private ToolStripMenuItem infoToolStripMenuItem;
        private Button GSMGeräte;
        private Button OverView;
        private ToolStripMenuItem GSMGeräteToolStripMenuItem;
        private ToolStripMenuItem ProviderAccountsToolStripMenuItem;
        private ToolStripMenuItem callLToolStripMenuItem;
        private ToolStripMenuItem liveCallStateToolStripMenuItem;
        private Button TKServices;
        private Button LiveCallState;
        private ToolStripMenuItem ServiceStart;
        private ToolStripMenuItem ServiceStop;
        private ToolStripMenuItem ServiceNummern;
        private ToolStripStatusLabel toolStripStatusLabel2;
        private ToolStripStatusLabel toolStripStatusLabel3;
        private ToolStripStatusLabel toolStripStatusLabel4;
        private ToolStripStatusLabel toolStripStatusLabel5;
    }
}
