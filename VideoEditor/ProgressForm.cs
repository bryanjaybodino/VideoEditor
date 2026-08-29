using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoEditor
{
    public class ProgressForm : Form
    {
        private ProgressBar progressBar;
        private Label lblStatus;

        public ProgressForm()
        {
            this.Size = new Size(400, 150);
            this.Text = "Exporting Video";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ControlBox = false;
            this.BackColor = Color.FromArgb(28, 28, 28);

            lblStatus = new Label
            {
                Text = "Rendering video...",
                ForeColor = Color.FromArgb(240, 240, 240),
                Location = new Point(20, 20),
                AutoSize = true
            };

            progressBar = new ProgressBar
            {
                Location = new Point(20, 50),
                Size = new Size(340, 25),
                Minimum = 0,
                Maximum = 100
            };

            this.Controls.Add(lblStatus);
            this.Controls.Add(progressBar);
        }

        public void UpdateProgress(int value, string text)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate { UpdateProgress(value, text); });
                return;
            }

            progressBar.Value = Math.Clamp(value, 0, 100);
            lblStatus.Text = text;
        }

        private void InitializeComponent()
        {

        }
    }
}
