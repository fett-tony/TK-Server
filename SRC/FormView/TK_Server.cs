namespace TKServer.FormView {
    using System.Diagnostics;
    using System.ServiceProcess;
    using System.Threading.Tasks;

    using TKServer.BASE;
    using TKServer.SQL;
    /// <summary>
    /// Defines the <see cref="TK_Server" />
    /// </summary>
    public partial class TK_Server : Form {
        private const string TKWindowsServiceName = "TK-Server";

        /// <summary>
        /// Defines the _active
        /// </summary>
        private Form? _active;

        /// <summary>
        /// Defines the _statusStripTimer
        /// </summary>
        private readonly System.Windows.Forms.Timer _statusStripTimer = new() { Interval = 3000 };

        private bool? _lastServiceRunning;

        //private System.Windows.Forms.Timer? _gsmRegisterTimer;

        /// <summary>
        /// Gets or sets the _logger
        /// </summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public TKBase_LogManager? _logger { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TK_Server"/> class.
        /// </summary>
        public TK_Server() {
            InitializeComponent();
            this.DoubleBuffered = true;
            _logger = new TKBase_LogManager(this.richTextBox1, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "log", "TK-Server.log"), LogOutputMode.Gui);
            _logger.TryConfigureFromDatabase();
            _statusStripTimer.Tick += (_, _) => UpdateStatusStrip();
            TKBase_GUIRunTime.RunningStateChanged += RuntimeStateChanged;
            TKBase_GUIRunTime.ServiceLogReceived += ServiceLogReceived;
            this.FormClosed += TKServer_FormClosed;
        }

        /// <summary>
        /// The TKServer_Load
        /// </summary>
        /// <param name="sender">The sender<see cref="object?"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void TKServer_Load(object? sender, EventArgs e) {
            _logger?.Info("TK-Server gestartet");
            UpdateStatusStrip();
            UpdateServiceButtons();
            _statusStripTimer.Start();
            OpenInContent(new TK_Start { Logger = _logger });

            // GUIOnline darf den Formularstart nicht blockieren.
            // Die GUI ist nur Anzeige/Steuerung; wenn Pipe/Auth fehlschlaegt, muss das Fenster trotzdem sichtbar bleiben.
            BeginInvoke(new Action(() => {
                _ = Task.Run(() => TKBase_GUIRunTime.SendGuiOnline(_logger));
            }));
        }

        /// <summary>
        /// The OpenInContent
        /// </summary>
        /// <param name="child">The child<see cref="Form"/></param>
        private void OpenInContent(Form child) {
            // alte Seite entfernen
            if (_active != null) {
                // Controls entfernen, dann dispose
                ContentPanel.Controls.Remove(_active);
                _active.Close();
                _active.Dispose();
                _active = null;
            }

            _active = child;

            child.TopLevel = false;
            child.FormBorderStyle = FormBorderStyle.None;
            child.MaximizeBox = false;
            child.MinimizeBox = false;
            child.ControlBox = false;
            child.Dock = DockStyle.Fill;

            ContentPanel.Controls.Clear();
            ContentPanel.Controls.Add(child);
            child.Show();
            child.BringToFront();
        }

        /// <summary>
        /// The SetActiveButton
        /// </summary>
        /// <param name="btn">The btn<see cref="Button"/></param>
        private void SetActiveButton(Button btn) {
            foreach (Control ctrl in NavigationPanel.Controls) {
                if (ctrl is Button b)
                    b.BackColor = SystemColors.Control;
            }
            btn.BackColor = Color.LightSteelBlue;
        }

        /// <summary>
        /// The RuntimeStateChanged
        /// </summary>
        /// <param name="running">The running<see cref="bool"/></param>
        /// <param name="message">The message<see cref="string"/></param>
        private void RuntimeStateChanged(bool running, string message) {
            UpdateStatusStrip();
            UpdateServiceButtons();
        }

        /// <summary>
        /// The UpdateStatusStrip
        /// </summary>
        private void UpdateStatusStrip() {
            if (IsDisposed || statusStrip1.IsDisposed) {
                return;
            }

            if (InvokeRequired) {
                BeginInvoke(new Action(UpdateStatusStrip));
                return;
            }

            try {
                bool runtimeRunning = IsWindowsServiceRunning();
                bool serviceJustStopped = _lastServiceRunning == true && !runtimeRunning;
                _lastServiceRunning = runtimeRunning;

                if (serviceJustStopped) {
                    _logger?.Warn("Windows-Service wurde gestoppt | Ohne laufenden Service ist keine Telefonie aktiv. GUI startet keine lokale Runtime.");
                    _ = Task.Run(() => TKBase_GUIRunTime.ObserveServiceDesiredState(_logger));
                }

                string serviceText = runtimeRunning ? "Läuft" : "Gestoppt";

                toolStripStatusLabel1.Text = $"TK-Service: {serviceText}";
                toolStripStatusLabel2.Text = "SipInt: Runtime-Prozess";
                toolStripStatusLabel3.Text = "SipExt: Runtime-Prozess";
                toolStripStatusLabel4.Text = "GSM: Runtime-Prozess";
                toolStripStatusLabel5.Text = $"Stand: {DateTime.Now:HH:mm:ss}";
            } catch (Exception ex) {
                _logger?.Warn($"Statusleiste konnte nicht aktualisiert werden: {ex.Message}");
            }
        }

        /// <summary>
        /// The GSMGeräte_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void GSMGeräte_Click(object sender, EventArgs e) {
            SetActiveButton((Button)sender);
            OpenInContent(new TK_GSMDevice { Logger = _logger });
        }

        /// <summary>
        /// The VoIP_Clients_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void VoIP_Clients_Click(object sender, EventArgs e) {
            SetActiveButton((Button)sender);
            OpenInContent(new TK_NumberRouting { Logger = _logger });
        }

        /// <summary>
        /// The VoIP_User_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void VoIP_User_Click(object sender, EventArgs e) {
            SetActiveButton((Button)sender);
            OpenInContent(new TK_VoIPUser { Logger = _logger });
        }

        /// <summary>
        /// The Provider_Accounts_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void Provider_Accounts_Click(object sender, EventArgs e) {
            SetActiveButton((Button)sender);
            OpenInContent(new TK_ProviderAccounts { Logger = _logger });
        }

        /// <summary>
        /// The GSMKartenToolStripMenuItem_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void GSMKartenToolStripMenuItem_Click(object sender, EventArgs e) {
            OpenInContent(new TK_GSMDevice { Logger = _logger });
        }

        /// <summary>
        /// The ProviderAccountsToolStripMenuItem_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void ProviderAccountsToolStripMenuItem_Click(object sender, EventArgs e) {
            OpenInContent(new TK_ProviderAccounts { Logger = _logger });
        }

        /// <summary>
        /// The VoIPUserToolStripMenuItem_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void VoIPUserToolStripMenuItem_Click(object sender, EventArgs e) {
            OpenInContent(new TK_VoIPUser { Logger = _logger });
        }

        /// <summary>
        /// The VoIPClientsToolStripMenuItem_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void VoIPClientsToolStripMenuItem_Click(object sender, EventArgs e) {
            OpenInContent(new TK_NumberRouting { Logger = _logger });
        }

        /// <summary>
        /// The rtbKopierenToolStripMenuItem_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void rtbKopierenToolStripMenuItem_Click(object sender, EventArgs e) {
            richTextBox1.Copy();
        }

        /// <summary>
        /// The rtbAllesMarkierenToolStripMenuItem_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void rtbAllesMarkierenToolStripMenuItem_Click(object sender, EventArgs e) {
            richTextBox1.SelectAll();
        }

        /// <summary>
        /// The rtbLeerenToolStripMenuItem_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void rtbLeerenToolStripMenuItem_Click(object sender, EventArgs e) {
            richTextBox1.Clear();
        }

        /// <summary>
        /// The rtbSpeichernToolStripMenuItem_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void rtbSpeichernToolStripMenuItem_Click(object sender, EventArgs e) {
            using SaveFileDialog dlg = new();
            dlg.Filter = "Textdatei (*.txt)|*.txt";
            if (dlg.ShowDialog() == DialogResult.OK)
                File.WriteAllText(dlg.FileName, richTextBox1.Text);
        }

        /// <summary>
        /// The sQLServerToolStripMenuItem_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void sQLServerToolStripMenuItem_Click(object sender, EventArgs e) {
        }

        /// <summary>
        /// The optionenToolStripMenuItem_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void optionenToolStripMenuItem_Click(object sender, EventArgs e) {
            OpenInContent(new TK_ServerEinstellung { Logger = _logger });
        }

        /// <summary>
        /// The OverView_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void OverView_Click(object sender, EventArgs e) {
            SetActiveButton((Button)sender);
            OpenInContent(new TK_Start { Logger = _logger });
        }

        /// <summary>
        /// The callLToolStripMenuItem_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void callLToolStripMenuItem_Click(object sender, EventArgs e) {
            OpenInContent(new TK_CallLog { Logger = _logger });
        }

        /// <summary>
        /// The liveCallStateToolStripMenuItem_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void liveCallStateToolStripMenuItem_Click(object sender, EventArgs e) {
            OpenInContent(new TK_LiveCallState { Logger = _logger });
        }

        /// <summary>
        /// The TKServices_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void TKServices_Click(object sender, EventArgs e) {
            SetActiveButton((Button)sender);
            OpenInContent(new TK_ServiceLegs { Logger = _logger });
        }

        /// <summary>
        /// The LiveCallState_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void LiveCallState_Click(object sender, EventArgs e) {
            SetActiveButton((Button)sender);
            OpenInContent(new TK_LiveCallState { Logger = _logger });
        }

        /// <summary>
        /// The serviceNummernToolStripMenuItem_Click
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void serviceNummernToolStripMenuItem_Click(object sender, EventArgs e) {
            OpenInContent(new TK_ServiceLegs { Logger = _logger });
        }

        /// <summary>
        /// Startet den Windows-Service als Dauerhalter, wenn er installiert ist.
        /// Die eigentliche Runtime kann vorher lokal in der GUI laufen und wird beim Service-Start übergeben.
        /// </summary>
        private async void ServiceStart_Click(object sender, EventArgs e) {
            _logger?.Info("TK-Runtime Start über GUI angefordert.");

            if (!TKSQL_ConnectionClass.TryEnsureDatabaseAvailable(_logger, "TK-Runtime Start über GUI", true)) {
                _logger?.Warn("TK-Runtime Start über GUI nicht gestartet | SQL-Datenbank nicht erreichbar.");
                UpdateStatusStrip();
                UpdateServiceButtons();
                return;
            }

            SetServiceButtonsBusy(true);

            try {
                await Task.Run(() => StartRuntimeProcess());
                _logger?.Info("TK-Service Start über GUI abgeschlossen | Windows-Service läuft unabhängig von der GUI weiter.");
            } catch (Exception ex) {
                _logger?.Error($"TK-Runtime Start über GUI fehlgeschlagen | {ex.Message}");
            } finally {
                SetServiceButtonsBusy(false);
                UpdateStatusStrip();
                UpdateServiceButtons();
            }
        }

        /// <summary>
        /// Stoppt den Windows-Service und damit alle vom Service gehaltenen Runtime-Bereiche.
        /// </summary>
        private async void ServiceStop_Click(object sender, EventArgs e) {
            SetServiceButtonsBusy(true);
            _logger?.Info("TK-Runtime Stop über GUI angefordert.");

            try {
                await Task.Run(() => StopRuntimeProcess());
                _logger?.Info("TK-Service Stop über Menü Extras abgeschlossen.");
            } catch (Exception ex) {
                _logger?.Error($"TK-Runtime Stop über GUI fehlgeschlagen | {ex.Message}");
            } finally {
                SetServiceButtonsBusy(false);
                UpdateStatusStrip();
                UpdateServiceButtons();
            }
        }

        /// <summary>
        /// Startet den installierten Windows-Service.
        /// Die GUI besitzt keine lokale Telefonie-Runtime mehr; der Service übernimmt den DB-Sollzustand.
        /// </summary>
        private void StartRuntimeProcess() {
            if (!IsWindowsServiceInstalled()) {
                _logger?.Warn("TK-Service Start abgebrochen | Windows-Service ist nicht installiert. Bitte zuerst unter Einstellungen den Service installieren.");
                return;
            }

            if (IsWindowsServiceRunning()) {
                _logger?.Info("TK-Service Start verworfen | Windows-Service läuft bereits und übernimmt den DB-Sollzustand.");
                return;
            }

            ExecuteServiceCommand($"start \"{TKWindowsServiceName}\"", "TK-Server Windows-Service starten");
            WaitForWindowsServiceState(ServiceControllerStatus.Running, TimeSpan.FromSeconds(45));
        }

        /// <summary>
        /// Stoppt den installierten Windows-Service.
        /// Der DB-Sollzustand bleibt erhalten; ohne laufenden Service ist keine Telefonie aktiv.
        /// </summary>
        private void StopRuntimeProcess() {
            if (!IsWindowsServiceInstalled()) {
                _logger?.Warn("TK-Service Stop abgebrochen | Windows-Service ist nicht installiert.");
                return;
            }

            if (!IsWindowsServiceRunning()) {
                _logger?.Info("TK-Service Stop verworfen | Windows-Service läuft nicht.");
                return;
            }

            ExecuteServiceCommand($"stop \"{TKWindowsServiceName}\"", "TK-Server Windows-Service stoppen");
            WaitForWindowsServiceState(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(60));
        }

        /// <summary>
        /// Prüft, ob der Windows-Service installiert ist.
        /// </summary>
        private static bool IsWindowsServiceInstalled() {
            try {
                return ServiceController.GetServices().Any(x => string.Equals(x.ServiceName, TKWindowsServiceName, StringComparison.OrdinalIgnoreCase));
            } catch {
                return false;
            }
        }

        /// <summary>
        /// Prüft, ob der Windows-Service läuft.
        /// </summary>
        private static bool IsWindowsServiceRunning() {
            try {
                using ServiceController service = new(TKWindowsServiceName);
                return service.Status == ServiceControllerStatus.Running || service.Status == ServiceControllerStatus.StartPending;
            } catch {
                return false;
            }
        }

        /// <summary>
        /// Wartet auf einen Zielstatus des Windows-Service.
        /// </summary>
        private static void WaitForWindowsServiceState(ServiceControllerStatus targetStatus, TimeSpan timeout) {
            using ServiceController service = new(TKWindowsServiceName);
            service.WaitForStatus(targetStatus, timeout);
            service.Refresh();

            if (service.Status != targetStatus) {
                throw new InvalidOperationException($"Windows-Service hat den Zielstatus nicht erreicht | Ziel={targetStatus} | Ist={service.Status}");
            }
        }

        /// <summary>
        /// Führt sc.exe mit administrativer Erhöhung aus.
        /// </summary>
        private static void ExecuteServiceCommand(string arguments, string operation) {
            using Process process = new();
            process.StartInfo = new ProcessStartInfo {
                FileName = "sc.exe",
                Arguments = arguments,
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };

            process.Start();
            process.WaitForExit();

            if (process.ExitCode != 0) {
                throw new InvalidOperationException($"{operation} fehlgeschlagen | ExitCode={process.ExitCode}");
            }
        }

        /// <summary>
        /// Liefert den Ablageort fuer Runtime-Steuerdateien.
        /// </summary>
        private static string GetRuntimeControlDirectory() {
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            return Path.Combine(programData, "TK-Server");
        }

        /// <summary>
        /// Sperrt die Service-Buttons während eines Start-/Stop-Vorgangs.
        /// </summary>
        private void SetServiceButtonsBusy(bool busy) {
            if (IsDisposed) {
                return;
            }

            if (InvokeRequired) {
                BeginInvoke(new Action(() => SetServiceButtonsBusy(busy)));
                return;
            }

            ServiceStart.Enabled = !busy;
            ServiceStop.Enabled = !busy;
        }

        /// <summary>
        /// Aktualisiert die Service-Buttons passend zum aktuellen Runtime-Prozess-Zustand.
        /// </summary>
        private void UpdateServiceButtons() {
            if (IsDisposed) {
                return;
            }

            if (InvokeRequired) {
                BeginInvoke(new Action(UpdateServiceButtons));
                return;
            }

            bool running = IsWindowsServiceRunning();

            ServiceStart.Enabled = !running;
            ServiceStop.Enabled = running;
        }


        private void ServiceLogReceived(string text) {
            if (IsDisposed || _logger == null) {
                return;
            }

            if (InvokeRequired) {
                BeginInvoke(new Action(() => ServiceLogReceived(text)));
                return;
            }

            _logger.WriteRaw(text, LogTarget.Ui);
        }

        /// <summary>
        /// The TKServer_FormClosed
        /// </summary>
        /// <param name="sender">The sender<see cref="object?"/></param>
        /// <param name="e">The e<see cref="FormClosedEventArgs"/></param>
        private void TKServer_FormClosed(object? sender, FormClosedEventArgs e) {
            _statusStripTimer.Stop();
            TKBase_GUIRunTime.RunningStateChanged -= RuntimeStateChanged;
            TKBase_GUIRunTime.ServiceLogReceived -= ServiceLogReceived;

            TKBase_GUIRunTime.NotifyGuiClosing(_logger);
        }
    }
}
