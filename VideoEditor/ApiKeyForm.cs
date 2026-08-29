using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace VideoEditor
{
    public class ApiKeyForm : Form
    {
        public string ApiKey { get; private set; }
        public bool GenerateImages { get; private set; }

        private TextBox txtApiKey;
        private CheckBox chkGenerateImages;
        private Button btnOk;
        private Button btnCancel;
        private LinkLabel lblLink;
        private Label lblInstruction;

        public ApiKeyForm(string existingKey = "", bool initialGenerateImages = true)
        {
            this.Text = "Enter Google AI Studio API Key";
            this.Size = new Size(480, 260); // Adjusted height for checkbox
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(28, 28, 28);
            this.ForeColor = Color.White;

            lblInstruction = new Label
            {
                Text = "Please enter your Gemini API key to proceed with auto-captioning:",
                Location = new Point(20, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(220, 220, 220)
            };

            lblLink = new LinkLabel
            {
                Text = "Get API Key from Google AI Studio",
                Location = new Point(20, 40),
                AutoSize = true,
                LinkColor = Color.FromArgb(100, 180, 245),
                ActiveLinkColor = Color.FromArgb(140, 200, 255),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            lblLink.LinkClicked += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://aistudio.google.com/",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Unable to open browser: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            txtApiKey = new TextBox
            {
                Location = new Point(20, 70),
                Width = 420,
                Text = existingKey,
                UseSystemPasswordChar = true,
                BackColor = Color.FromArgb(38, 38, 38),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.5F)
            };

            // Image Generation Toggle Checkbox
            chkGenerateImages = new CheckBox
            {
                Text = "Automatically generate AI scene images for captions",
                Location = new Point(20, 110),
                AutoSize = true,
                Checked = initialGenerateImages,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(220, 220, 220)
            };

            btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(234, 160),
                Size = new Size(100, 32),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtApiKey.Text))
                {
                    MessageBox.Show(this, "API Key cannot be empty.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    this.DialogResult = DialogResult.None;
                    return;
                }
                ApiKey = txtApiKey.Text.Trim();
                GenerateImages = chkGenerateImages.Checked;
            };

            btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(340, 160),
                Size = new Size(100, 32),
                BackColor = Color.FromArgb(48, 48, 48),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            this.Controls.Add(lblInstruction);
            this.Controls.Add(lblLink);
            this.Controls.Add(txtApiKey);
            this.Controls.Add(chkGenerateImages);
            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }
    }
}