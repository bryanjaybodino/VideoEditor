using System;
using System.Drawing;
using System.Windows.Forms;
using VideoEditor.Controls;

namespace VideoEditor
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            mainLayout = new TableLayoutPanel();
            toolbar = new FlowLayoutPanel();
            btnImport = new Button();
            btnPlayPause = new Button();
            btnDelete = new Button();
            btnExport = new Button();
            leftPanel = new Panel();
            mediaListBox = new ListBox();
            previewControl = new PreviewControl();
            rightPanel = new FlowLayoutPanel();
            lblSidebarTitle = new Label();
            btnSplit = new Button();
            btnSplitLeft = new Button();
            btnSplitRight = new Button();
            lblDivider1 = new Label();
            lblHeaderText = new Label();
            btnAddText = new Button();
            lblFontSize = new Label();
            numFontSize = new NumericUpDown();
            colorFlow = new FlowLayoutPanel();
            btnTextColor = new Button();
            btnBgColor = new Button();
            lblBoxSize = new Label();
            sizeFlow = new FlowLayoutPanel();
            numBoxWidth = new NumericUpDown();
            numBoxHeight = new NumericUpDown();
            lblDivider2 = new Label();
            lblHeaderAnim = new Label();
            lblDuration = new Label();
            numDuration = new NumericUpDown();
            lblInEffect = new Label();
            cbInEffect = new ComboBox();
            lblInDur = new Label();
            numInDuration = new NumericUpDown();
            lblOutEffect = new Label();
            cbOutEffect = new ComboBox();
            lblOutDur = new Label();
            numOutDuration = new NumericUpDown();
            timelineControl = new TimelineControl();
            mainLayout.SuspendLayout();
            toolbar.SuspendLayout();
            leftPanel.SuspendLayout();
            rightPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numFontSize).BeginInit();
            colorFlow.SuspendLayout();
            sizeFlow.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numBoxWidth).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numBoxHeight).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numDuration).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numInDuration).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numOutDuration).BeginInit();
            SuspendLayout();
            // 
            // mainLayout
            // 
            mainLayout.ColumnCount = 3;
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            mainLayout.Controls.Add(toolbar, 0, 0);
            mainLayout.Controls.Add(leftPanel, 0, 1);
            mainLayout.Controls.Add(previewControl, 1, 1);
            mainLayout.Controls.Add(rightPanel, 2, 1);
            mainLayout.Controls.Add(timelineControl, 0, 2);
            mainLayout.Dock = DockStyle.Fill;
            mainLayout.Location = new Point(0, 0);
            mainLayout.Name = "mainLayout";
            mainLayout.RowCount = 3;
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 220F));
            mainLayout.Size = new Size(1400, 900);
            mainLayout.TabIndex = 0;
            // 
            // toolbar
            // 
            toolbar.BackColor = Color.FromArgb(28, 28, 28);
            mainLayout.SetColumnSpan(toolbar, 3);
            toolbar.Controls.Add(btnImport);
            toolbar.Controls.Add(btnPlayPause);
            toolbar.Controls.Add(btnDelete);
            toolbar.Controls.Add(btnExport);
            toolbar.Dock = DockStyle.Fill;
            toolbar.Location = new Point(3, 3);
            toolbar.Name = "toolbar";
            toolbar.Padding = new Padding(10);
            toolbar.Size = new Size(1394, 44);
            toolbar.TabIndex = 0;
            // 
            // btnImport
            // 
            btnImport.BackColor = Color.FromArgb(0, 120, 215);
            btnImport.FlatAppearance.BorderSize = 0;
            btnImport.FlatStyle = FlatStyle.Flat;
            btnImport.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnImport.ForeColor = Color.White;
            btnImport.Location = new Point(10, 10);
            btnImport.Margin = new Padding(0, 0, 8, 0);
            btnImport.Name = "btnImport";
            btnImport.Size = new Size(140, 32);
            btnImport.TabIndex = 0;
            btnImport.Text = "📁 Import Files";
            btnImport.UseVisualStyleBackColor = false;
            // 
            // btnPlayPause
            // 
            btnPlayPause.BackColor = Color.FromArgb(0, 120, 215);
            btnPlayPause.FlatAppearance.BorderSize = 0;
            btnPlayPause.FlatStyle = FlatStyle.Flat;
            btnPlayPause.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnPlayPause.ForeColor = Color.White;
            btnPlayPause.Location = new Point(158, 10);
            btnPlayPause.Margin = new Padding(0, 0, 8, 0);
            btnPlayPause.Name = "btnPlayPause";
            btnPlayPause.Size = new Size(140, 32);
            btnPlayPause.TabIndex = 1;
            btnPlayPause.Text = "▶ Play";
            btnPlayPause.UseVisualStyleBackColor = false;
            // 
            // btnDelete
            // 
            btnDelete.BackColor = Color.FromArgb(0, 120, 215);
            btnDelete.FlatAppearance.BorderSize = 0;
            btnDelete.FlatStyle = FlatStyle.Flat;
            btnDelete.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnDelete.ForeColor = Color.White;
            btnDelete.Location = new Point(306, 10);
            btnDelete.Margin = new Padding(0, 0, 8, 0);
            btnDelete.Name = "btnDelete";
            btnDelete.Size = new Size(140, 32);
            btnDelete.TabIndex = 2;
            btnDelete.Text = "🗑 Delete Selected";
            btnDelete.UseVisualStyleBackColor = false;
            // 
            // btnExport
            // 
            btnExport.BackColor = Color.FromArgb(0, 120, 215);
            btnExport.FlatAppearance.BorderSize = 0;
            btnExport.FlatStyle = FlatStyle.Flat;
            btnExport.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnExport.ForeColor = Color.White;
            btnExport.Location = new Point(454, 10);
            btnExport.Margin = new Padding(0, 0, 8, 0);
            btnExport.Name = "btnExport";
            btnExport.Size = new Size(140, 32);
            btnExport.TabIndex = 3;
            btnExport.Text = "Export Video";
            btnExport.UseVisualStyleBackColor = false;
            // 
            // leftPanel
            // 
            leftPanel.BackColor = Color.FromArgb(28, 28, 28);
            leftPanel.Controls.Add(mediaListBox);
            leftPanel.Dock = DockStyle.Fill;
            leftPanel.Location = new Point(3, 53);
            leftPanel.Name = "leftPanel";
            leftPanel.Size = new Size(274, 624);
            leftPanel.TabIndex = 1;
            // 
            // mediaListBox
            // 
            mediaListBox.BackColor = Color.FromArgb(38, 38, 38);
            mediaListBox.BorderStyle = BorderStyle.None;
            mediaListBox.Dock = DockStyle.Fill;
            mediaListBox.ForeColor = Color.FromArgb(240, 240, 240);
            mediaListBox.Location = new Point(0, 0);
            mediaListBox.Name = "mediaListBox";
            mediaListBox.Size = new Size(274, 624);
            mediaListBox.TabIndex = 0;
            // 
            // previewControl
            // 
            previewControl.BackColor = Color.FromArgb(15, 15, 15);
            previewControl.Dock = DockStyle.Fill;
            previewControl.Location = new Point(283, 53);
            previewControl.Name = "previewControl";
            previewControl.SelectedItem = null;
            previewControl.SelectedTextLabel = null;
            previewControl.Size = new Size(764, 624);
            previewControl.TabIndex = 2;
            // 
            // rightPanel
            // 
            rightPanel.AutoScroll = true;
            rightPanel.BackColor = Color.FromArgb(28, 28, 28);
            rightPanel.Controls.Add(lblSidebarTitle);
            rightPanel.Controls.Add(btnSplit);
            rightPanel.Controls.Add(btnSplitLeft);
            rightPanel.Controls.Add(btnSplitRight);
            rightPanel.Controls.Add(lblDivider1);
            rightPanel.Controls.Add(lblHeaderText);
            rightPanel.Controls.Add(btnAddText);
            rightPanel.Controls.Add(lblFontSize);
            rightPanel.Controls.Add(numFontSize);
            rightPanel.Controls.Add(colorFlow);
            rightPanel.Controls.Add(lblBoxSize);
            rightPanel.Controls.Add(sizeFlow);
            rightPanel.Controls.Add(lblDivider2);
            rightPanel.Controls.Add(lblHeaderAnim);
            rightPanel.Controls.Add(lblDuration);
            rightPanel.Controls.Add(numDuration);
            rightPanel.Controls.Add(lblInEffect);
            rightPanel.Controls.Add(cbInEffect);
            rightPanel.Controls.Add(lblInDur);
            rightPanel.Controls.Add(numInDuration);
            rightPanel.Controls.Add(lblOutEffect);
            rightPanel.Controls.Add(cbOutEffect);
            rightPanel.Controls.Add(lblOutDur);
            rightPanel.Controls.Add(numOutDuration);
            rightPanel.Dock = DockStyle.Fill;
            rightPanel.FlowDirection = FlowDirection.TopDown;
            rightPanel.Location = new Point(1053, 53);
            rightPanel.Name = "rightPanel";
            rightPanel.Padding = new Padding(12);
            rightPanel.Size = new Size(344, 624);
            rightPanel.TabIndex = 3;
            rightPanel.WrapContents = false;
            // 
            // lblSidebarTitle
            // 
            lblSidebarTitle.AutoSize = true;
            lblSidebarTitle.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            lblSidebarTitle.ForeColor = Color.FromArgb(240, 240, 240);
            lblSidebarTitle.Location = new Point(12, 12);
            lblSidebarTitle.Margin = new Padding(0, 0, 0, 8);
            lblSidebarTitle.Name = "lblSidebarTitle";
            lblSidebarTitle.Size = new Size(146, 25);
            lblSidebarTitle.TabIndex = 0;
            lblSidebarTitle.Text = "Editing Actions";
            // 
            // btnSplit
            // 
            btnSplit.Location = new Point(15, 48);
            btnSplit.Name = "btnSplit";
            btnSplit.Size = new Size(75, 23);
            btnSplit.TabIndex = 1;
            // 
            // btnSplitLeft
            // 
            btnSplitLeft.Location = new Point(15, 77);
            btnSplitLeft.Name = "btnSplitLeft";
            btnSplitLeft.Size = new Size(75, 23);
            btnSplitLeft.TabIndex = 2;
            // 
            // btnSplitRight
            // 
            btnSplitRight.Location = new Point(15, 106);
            btnSplitRight.Name = "btnSplitRight";
            btnSplitRight.Size = new Size(75, 23);
            btnSplitRight.TabIndex = 3;
            // 
            // lblDivider1
            // 
            lblDivider1.BackColor = Color.FromArgb(60, 60, 60);
            lblDivider1.Location = new Point(12, 142);
            lblDivider1.Margin = new Padding(0, 10, 0, 10);
            lblDivider1.Name = "lblDivider1";
            lblDivider1.Size = new Size(230, 1);
            lblDivider1.TabIndex = 4;
            // 
            // lblHeaderText
            // 
            lblHeaderText.Location = new Point(15, 153);
            lblHeaderText.Name = "lblHeaderText";
            lblHeaderText.Size = new Size(100, 23);
            lblHeaderText.TabIndex = 5;
            // 
            // btnAddText
            // 
            btnAddText.BackColor = Color.FromArgb(0, 120, 215);
            btnAddText.FlatAppearance.BorderSize = 0;
            btnAddText.FlatStyle = FlatStyle.Flat;
            btnAddText.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnAddText.ForeColor = Color.White;
            btnAddText.Location = new Point(12, 176);
            btnAddText.Margin = new Padding(0, 0, 8, 0);
            btnAddText.Name = "btnAddText";
            btnAddText.Size = new Size(230, 32);
            btnAddText.TabIndex = 6;
            btnAddText.Text = "➕ Add Text Layer";
            btnAddText.UseVisualStyleBackColor = false;
            // 
            // lblFontSize
            // 
            lblFontSize.Location = new Point(15, 208);
            lblFontSize.Name = "lblFontSize";
            lblFontSize.Size = new Size(100, 23);
            lblFontSize.TabIndex = 7;
            // 
            // numFontSize
            // 
            numFontSize.Location = new Point(15, 234);
            numFontSize.Name = "numFontSize";
            numFontSize.Size = new Size(120, 27);
            numFontSize.TabIndex = 8;
            // 
            // colorFlow
            // 
            colorFlow.Controls.Add(btnTextColor);
            colorFlow.Controls.Add(btnBgColor);
            colorFlow.Location = new Point(15, 267);
            colorFlow.Name = "colorFlow";
            colorFlow.Size = new Size(230, 40);
            colorFlow.TabIndex = 9;
            colorFlow.WrapContents = false;
            // 
            // btnTextColor
            // 
            btnTextColor.Location = new Point(3, 3);
            btnTextColor.Name = "btnTextColor";
            btnTextColor.Size = new Size(75, 23);
            btnTextColor.TabIndex = 0;
            // 
            // btnBgColor
            // 
            btnBgColor.Location = new Point(84, 3);
            btnBgColor.Name = "btnBgColor";
            btnBgColor.Size = new Size(75, 23);
            btnBgColor.TabIndex = 1;
            // 
            // lblBoxSize
            // 
            lblBoxSize.Location = new Point(15, 310);
            lblBoxSize.Name = "lblBoxSize";
            lblBoxSize.Size = new Size(100, 23);
            lblBoxSize.TabIndex = 10;
            // 
            // sizeFlow
            // 
            sizeFlow.Controls.Add(numBoxWidth);
            sizeFlow.Controls.Add(numBoxHeight);
            sizeFlow.Location = new Point(15, 336);
            sizeFlow.Name = "sizeFlow";
            sizeFlow.Size = new Size(230, 35);
            sizeFlow.TabIndex = 11;
            sizeFlow.WrapContents = false;
            // 
            // numBoxWidth
            // 
            numBoxWidth.Location = new Point(3, 3);
            numBoxWidth.Name = "numBoxWidth";
            numBoxWidth.Size = new Size(120, 27);
            numBoxWidth.TabIndex = 0;
            // 
            // numBoxHeight
            // 
            numBoxHeight.Location = new Point(129, 3);
            numBoxHeight.Name = "numBoxHeight";
            numBoxHeight.Size = new Size(120, 27);
            numBoxHeight.TabIndex = 1;
            // 
            // lblDivider2
            // 
            lblDivider2.BackColor = Color.FromArgb(60, 60, 60);
            lblDivider2.Location = new Point(12, 384);
            lblDivider2.Margin = new Padding(0, 10, 0, 10);
            lblDivider2.Name = "lblDivider2";
            lblDivider2.Size = new Size(230, 1);
            lblDivider2.TabIndex = 12;
            // 
            // lblHeaderAnim
            // 
            lblHeaderAnim.Location = new Point(15, 395);
            lblHeaderAnim.Name = "lblHeaderAnim";
            lblHeaderAnim.Size = new Size(100, 23);
            lblHeaderAnim.TabIndex = 13;
            // 
            // lblDuration
            // 
            lblDuration.Location = new Point(15, 418);
            lblDuration.Name = "lblDuration";
            lblDuration.Size = new Size(100, 23);
            lblDuration.TabIndex = 14;
            // 
            // numDuration
            // 
            numDuration.Location = new Point(15, 444);
            numDuration.Name = "numDuration";
            numDuration.Size = new Size(120, 27);
            numDuration.TabIndex = 15;
            // 
            // lblInEffect
            // 
            lblInEffect.Location = new Point(15, 474);
            lblInEffect.Name = "lblInEffect";
            lblInEffect.Size = new Size(100, 23);
            lblInEffect.TabIndex = 16;
            // 
            // cbInEffect
            // 
            cbInEffect.Location = new Point(15, 500);
            cbInEffect.Name = "cbInEffect";
            cbInEffect.Size = new Size(121, 28);
            cbInEffect.TabIndex = 17;
            // 
            // lblInDur
            // 
            lblInDur.Location = new Point(15, 531);
            lblInDur.Name = "lblInDur";
            lblInDur.Size = new Size(100, 23);
            lblInDur.TabIndex = 18;
            // 
            // numInDuration
            // 
            numInDuration.Location = new Point(15, 557);
            numInDuration.Name = "numInDuration";
            numInDuration.Size = new Size(120, 27);
            numInDuration.TabIndex = 19;
            // 
            // lblOutEffect
            // 
            lblOutEffect.Location = new Point(15, 587);
            lblOutEffect.Name = "lblOutEffect";
            lblOutEffect.Size = new Size(100, 23);
            lblOutEffect.TabIndex = 20;
            // 
            // cbOutEffect
            // 
            cbOutEffect.Location = new Point(15, 613);
            cbOutEffect.Name = "cbOutEffect";
            cbOutEffect.Size = new Size(121, 28);
            cbOutEffect.TabIndex = 21;
            // 
            // lblOutDur
            // 
            lblOutDur.Location = new Point(15, 644);
            lblOutDur.Name = "lblOutDur";
            lblOutDur.Size = new Size(100, 23);
            lblOutDur.TabIndex = 22;
            // 
            // numOutDuration
            // 
            numOutDuration.Location = new Point(15, 670);
            numOutDuration.Name = "numOutDuration";
            numOutDuration.Size = new Size(120, 27);
            numOutDuration.TabIndex = 23;
            // 
            // timelineControl
            // 
            timelineControl.BackColor = Color.FromArgb(25, 25, 25);
            mainLayout.SetColumnSpan(timelineControl, 3);
            timelineControl.CurrentTime = 0D;
            timelineControl.Dock = DockStyle.Fill;
            timelineControl.Location = new Point(3, 683);
            timelineControl.Name = "timelineControl";
            timelineControl.Size = new Size(1394, 214);
            timelineControl.TabIndex = 4;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(18, 18, 18);
            ClientSize = new Size(1400, 900);
            Controls.Add(mainLayout);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "VideoEditor - Mobile Slideshow Video Maker";
            mainLayout.ResumeLayout(false);
            toolbar.ResumeLayout(false);
            leftPanel.ResumeLayout(false);
            rightPanel.ResumeLayout(false);
            rightPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)numFontSize).EndInit();
            colorFlow.ResumeLayout(false);
            sizeFlow.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)numBoxWidth).EndInit();
            ((System.ComponentModel.ISupportInitialize)numBoxHeight).EndInit();
            ((System.ComponentModel.ISupportInitialize)numDuration).EndInit();
            ((System.ComponentModel.ISupportInitialize)numInDuration).EndInit();
            ((System.ComponentModel.ISupportInitialize)numOutDuration).EndInit();
            ResumeLayout(false);
        }

        private void ConfigureActionButton(Button btn, string text)
        {
            btn.Text = text;
            btn.Size = new Size(230, 32);
            btn.BackColor = Color.FromArgb(48, 48, 48);
            btn.ForeColor = Color.FromArgb(240, 240, 240);
            btn.FlatStyle = FlatStyle.Flat;
            btn.TextAlign = ContentAlignment.MiddleLeft;
            btn.Padding = new Padding(8, 0, 0, 0);
            btn.Margin = new Padding(0, 0, 0, 5);
            btn.Font = new Font("Segoe UI", 8.5f);
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 60);
        }

        private void ConfigureHeaderLabel(Label lbl, string text)
        {
            lbl.Text = text;
            lbl.ForeColor = Color.FromArgb(100, 180, 245);
            lbl.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            lbl.AutoSize = true;
            lbl.Margin = new Padding(0, 8, 0, 3);
        }

        private void ConfigureSubLabel(Label lbl, string text)
        {
            lbl.Text = text;
            lbl.ForeColor = Color.FromArgb(170, 170, 170);
            lbl.Font = new Font("Segoe UI", 8);
            lbl.AutoSize = true;
            lbl.Margin = new Padding(0, 4, 0, 2);
        }

        private void ConfigureNumeric(NumericUpDown num, decimal min, decimal max, decimal val, int decimals, int width = 230)
        {
            num.Width = width;
            num.Minimum = min;
            num.Maximum = max;
            num.Value = val;
            num.DecimalPlaces = decimals;
            if (decimals > 0) num.Increment = 0.1m;
            num.BackColor = Color.FromArgb(38, 38, 38);
            num.ForeColor = Color.FromArgb(240, 240, 240);
        }

        private void ConfigureDropdown(ComboBox cb)
        {
            cb.Width = 230;
            cb.DropDownStyle = ComboBoxStyle.DropDownList;
            cb.BackColor = Color.FromArgb(38, 38, 38);
            cb.ForeColor = Color.FromArgb(240, 240, 240);
            cb.FlatStyle = FlatStyle.Flat;
            cb.Items.AddRange(new object[] {
                "None", "Fade", "Slide", "Wave", "Zoom", "ZoomBlur",
                "ZoomBlurUp", "ZoomBlurDown", "ZoomBlurLeft", "ZoomBlurRight", "DynamicZoomBlur"
            });
            cb.SelectedIndex = 0;
        }

        private void ConfigureMiniColorButton(Button btn, string text)
        {
            btn.Text = text;
            btn.Width = 110;
            btn.Height = 28;
            btn.BackColor = Color.FromArgb(48, 48, 48);
            btn.ForeColor = Color.FromArgb(240, 240, 240);
            btn.FlatStyle = FlatStyle.Flat;
            btn.Font = new Font("Segoe UI", 8);
            btn.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 60);
        }

        #endregion

        private TableLayoutPanel mainLayout;
        private FlowLayoutPanel toolbar;
        private Button btnImport;
        private Button btnPlayPause;
        private Button btnDelete;
        private Button btnExport;
        private Panel leftPanel;
        private ListBox mediaListBox;
        private PreviewControl previewControl;
        private FlowLayoutPanel rightPanel;
        private Label lblSidebarTitle;
        private Button btnSplit;
        private Button btnSplitLeft;
        private Button btnSplitRight;
        private Label lblDivider1;
        private Label lblHeaderText;
        private Button btnAddText;
        private Label lblFontSize;
        private NumericUpDown numFontSize;
        private FlowLayoutPanel colorFlow;
        private Button btnTextColor;
        private Button btnBgColor;
        private Label lblBoxSize;
        private FlowLayoutPanel sizeFlow;
        private NumericUpDown numBoxWidth;
        private NumericUpDown numBoxHeight;
        private Label lblDivider2;
        private Label lblHeaderAnim;
        private Label lblDuration;
        private NumericUpDown numDuration;
        private Label lblInEffect;
        private ComboBox cbInEffect;
        private Label lblInDur;
        private NumericUpDown numInDuration;
        private Label lblOutEffect;
        private ComboBox cbOutEffect;
        private Label lblOutDur;
        private NumericUpDown numOutDuration;
        private TimelineControl timelineControl;
    }
}