namespace TKServer.FormView {
    using System;
    using System.Data;
    using System.Windows.Forms;

    using TKServer.BASE;
    using TKServer.SQL;
    /// <summary>
    /// Defines the <see cref="TK_CallLog" />
    /// </summary>
    public partial class TK_CallLog : Form {
        /// <summary>
        /// Gets or sets the Logger
        /// </summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public TKBase_LogManager? Logger { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TK_CallLog"/> class.
        /// </summary>
        public TK_CallLog() {
            InitializeComponent();
        }

        /// <summary>
        /// The CallLog_Load
        /// </summary>
        /// <param name="sender">The sender<see cref="object"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void CallLog_Load(object sender, EventArgs e) {
            try {
                DataTable dt = new TKSQL_Commands(Logger).LoadSipCallLogTable();
                dataGridView1.DataSource = dt;
                Logger?.Info($"Tabelle ProviderAccounts wurde geöffnet.");
            } catch (Exception ex) {
                Logger?.Error($"Es konnte keine Tabelle abgefragt werden!" + ex);
            }
        }
    }
}
