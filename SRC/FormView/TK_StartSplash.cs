namespace TKServer.FormView {
    using System;
    using System.Windows.Forms;

    /// <summary>
    /// Defines the <see cref="TK_StartSplash" />
    /// </summary>
    public partial class TK_StartSplash : Form {
        /// <summary>
        /// Defines the _closeTimer
        /// </summary>
        private readonly System.Windows.Forms.Timer _closeTimer;

        /// <summary>
        /// Initializes a new instance of the <see cref="TK_StartSplash"/> class.
        /// </summary>
        public TK_StartSplash() {
            InitializeComponent();

            lblProgramInfo.Text = $"{Application.ProductName}  Version {Application.ProductVersion}";

            _closeTimer = new System.Windows.Forms.Timer();
            _closeTimer.Interval = 2500;
            _closeTimer.Tick += CloseTimer_Tick;
            _closeTimer.Start();
        }

        /// <summary>
        /// The CloseTimer_Tick
        /// </summary>
        /// <param name="sender">The sender<see cref="object?"/></param>
        /// <param name="e">The e<see cref="EventArgs"/></param>
        private void CloseTimer_Tick(object? sender, EventArgs e) {
            _closeTimer.Stop();
            Close();
        }
    }
}
