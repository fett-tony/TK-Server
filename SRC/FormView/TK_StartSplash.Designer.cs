namespace TKServer.FormView {
    public partial class TK_StartSplash {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }

            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent() {
            pictureBox1 = new PictureBox();
            lblProgramInfo = new Label();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            pictureBox1.SuspendLayout();
            SuspendLayout();
            // 
            // pictureBox1
            // 
            pictureBox1.Controls.Add(lblProgramInfo);
            pictureBox1.Dock = DockStyle.Fill;
            pictureBox1.Image = Resources.Resources.TK_Server_Start_Picture;
            pictureBox1.Location = new Point(0, 0);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(800, 450);
            pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox1.TabIndex = 0;
            pictureBox1.TabStop = false;
            // 
            // lblProgramInfo
            // 
            lblProgramInfo.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            lblProgramInfo.BackColor = Color.Transparent;
            lblProgramInfo.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
            lblProgramInfo.ForeColor = Color.White;
            lblProgramInfo.Location = new Point(15, 379);
            lblProgramInfo.Name = "lblProgramInfo";
            lblProgramInfo.Size = new Size(770, 30);
            lblProgramInfo.TabIndex = 1;
            lblProgramInfo.Text = "TK-Server Version";
            lblProgramInfo.TextAlign = ContentAlignment.MiddleRight;
            // 
            // TK_StartSplash
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.Black;
            ClientSize = new Size(800, 450);
            ControlBox = false;
            Controls.Add(pictureBox1);
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "TK_StartSplash";
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            pictureBox1.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private PictureBox pictureBox1;
        private Label lblProgramInfo;
    }
}