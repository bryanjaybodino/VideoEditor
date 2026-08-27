using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using VideoEditor.Controls;

namespace VideoEditor
{
    // Custom FlowLayoutPanel that forces native dark mode scrollbars on Windows 10/11
    public class DarkScrollPanel : FlowLayoutPanel
    {
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubSubAppName);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SetWindowTheme(this.Handle, "Explorer", null);
        }
    }

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
            rightPanel = new DarkScrollPanel();
            lblSidebarTitle = new Label();
            row1Flow = new FlowLayoutPanel();
            btnSplit = new Button();
            row2Flow = new FlowLayoutPanel();
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
            lblInAnimHeader = new Label();
            inAnimFlow = new FlowLayoutPanel();
            cbInEffect = new ComboBox();
            numInDuration = new NumericUpDown();
            lblOutAnimHeader = new Label();
            outAnimFlow = new FlowLayoutPanel();
            cbOutEffect = new ComboBox();
            numOutDuration = new NumericUpDown();
            timelineControl = new TimelineControl();
            mainLayout.SuspendLayout();
            toolbar.SuspendLayout();
            leftPanel.SuspendLayout();
            rightPanel.SuspendLayout();
            row1Flow.SuspendLayout();
            row2Flow.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numFontSize).BeginInit();
            colorFlow.SuspendLayout();
            sizeFlow.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numBoxWidth).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numBoxHeight).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numDuration).BeginInit();
            inAnimFlow.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numInDuration).BeginInit();
            outAnimFlow.SuspendLayout();
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
            rightPanel.Controls.Add(row1Flow);
            rightPanel.Controls.Add(row2Flow);
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
            rightPanel.Controls.Add(lblInAnimHeader);
            rightPanel.Controls.Add(inAnimFlow);
            rightPanel.Controls.Add(lblOutAnimHeader);
            rightPanel.Controls.Add(outAnimFlow);
            rightPanel.Dock = DockStyle.Fill;
            rightPanel.FlowDirection = FlowDirection.TopDown;
            rightPanel.Location = new Point(1053, 53);
            rightPanel.Name = "rightPanel";
            rightPanel.Padding = new Padding(12);
            rightPanel.Size = new Size(344, 624);
            rightPanel.TabIndex = 3;
            rightPanel.WrapContents = false;
            rightPanel.SizeChanged += RightPanel_SizeChanged;
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
            // row1Flow
            // 
            row1Flow.Controls.Add(btnSplit);
            row1Flow.Location = new Point(12, 45);
            row1Flow.Margin = new Padding(0, 0, 0, 5);
            row1Flow.Name = "row1Flow";
            row1Flow.Size = new Size(310, 32);
            row1Flow.TabIndex = 1;
            row1Flow.WrapContents = false;
            // 
            // btnSplit
            // 
            btnSplit.BackColor = Color.FromArgb(48, 48, 48);
            btnSplit.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 60);
            btnSplit.FlatAppearance.BorderSize = 1;
            btnSplit.FlatStyle = FlatStyle.Flat;
            btnSplit.Font = new Font("Segoe UI", 8.5F);
            btnSplit.ForeColor = Color.FromArgb(240, 240, 240);
            btnSplit.Location = new Point(0, 0);
            btnSplit.Margin = new Padding(0);
            btnSplit.Name = "btnSplit";
            btnSplit.Size = new Size(310, 32);
            btnSplit.TabIndex = 0;
            btnSplit.Text = "✂ Split Clip";
            btnSplit.UseVisualStyleBackColor = false;
            // 
            // row2Flow
            // 
            row2Flow.Controls.Add(btnSplitLeft);
            row2Flow.Controls.Add(btnSplitRight);
            row2Flow.Location = new Point(12, 82);
            row2Flow.Margin = new Padding(0, 0, 0, 5);
            row2Flow.Name = "row2Flow";
            row2Flow.Size = new Size(310, 32);
            row2Flow.TabIndex = 2;
            row2Flow.WrapContents = false;
            // 
            // btnSplitLeft
            // 
            btnSplitLeft.BackColor = Color.FromArgb(48, 48, 48);
            btnSplitLeft.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 60);
            btnSplitLeft.FlatAppearance.BorderSize = 1;
            btnSplitLeft.FlatStyle = FlatStyle.Flat;
            btnSplitLeft.Font = new Font("Segoe UI", 8.5F);
            btnSplitLeft.ForeColor = Color.FromArgb(240, 240, 240);
            btnSplitLeft.Location = new Point(0, 0);
            btnSplitLeft.Margin = new Padding(0, 0, 8, 0);
            btnSplitLeft.Name = "btnSplitLeft";
            btnSplitLeft.Size = new Size(151, 32);
            btnSplitLeft.TabIndex = 0;
            btnSplitLeft.Text = "⬅ Trim Left";
            btnSplitLeft.UseVisualStyleBackColor = false;
            // 
            // btnSplitRight
            // 
            btnSplitRight.BackColor = Color.FromArgb(48, 48, 48);
            btnSplitRight.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 60);
            btnSplitRight.FlatAppearance.BorderSize = 1;
            btnSplitRight.FlatStyle = FlatStyle.Flat;
            btnSplitRight.Font = new Font("Segoe UI", 8.5F);
            btnSplitRight.ForeColor = Color.FromArgb(240, 240, 240);
            btnSplitRight.Location = new Point(159, 0);
            btnSplitRight.Margin = new Padding(0);
            btnSplitRight.Name = "btnSplitRight";
            btnSplitRight.Size = new Size(151, 32);
            btnSplitRight.TabIndex = 1;
            btnSplitRight.Text = "➡ Trim Right";
            btnSplitRight.UseVisualStyleBackColor = false;
            // 
            // lblDivider1
            // 
            lblDivider1.BackColor = Color.FromArgb(60, 60, 60);
            lblDivider1.Location = new Point(12, 129);
            lblDivider1.Margin = new Padding(0, 10, 0, 10);
            lblDivider1.Name = "lblDivider1";
            lblDivider1.Size = new Size(310, 1);
            lblDivider1.TabIndex = 3;
            // 
            // lblHeaderText
            // 
            lblHeaderText.AutoSize = true;
            lblHeaderText.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblHeaderText.ForeColor = Color.FromArgb(100, 180, 245);
            lblHeaderText.Location = new Point(12, 148);
            lblHeaderText.Margin = new Padding(0, 8, 0, 3);
            lblHeaderText.Name = "lblHeaderText";
            lblHeaderText.Size = new Size(95, 20);
            lblHeaderText.TabIndex = 4;
            lblHeaderText.Text = "Text Overlay";
            // 
            // btnAddText
            // 
            btnAddText.BackColor = Color.FromArgb(0, 120, 215);
            btnAddText.FlatAppearance.BorderSize = 0;
            btnAddText.FlatStyle = FlatStyle.Flat;
            btnAddText.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnAddText.ForeColor = Color.White;
            btnAddText.Location = new Point(12, 171);
            btnAddText.Margin = new Padding(0, 0, 8, 5);
            btnAddText.Name = "btnAddText";
            btnAddText.Size = new Size(310, 32);
            btnAddText.TabIndex = 5;
            btnAddText.Text = "➕ Add Text Layer";
            btnAddText.UseVisualStyleBackColor = false;
            // 
            // lblFontSize
            // 
            lblFontSize.AutoSize = true;
            lblFontSize.Font = new Font("Segoe UI", 8F);
            lblFontSize.ForeColor = Color.FromArgb(170, 170, 170);
            lblFontSize.Location = new Point(12, 212);
            lblFontSize.Margin = new Padding(0, 4, 0, 2);
            lblFontSize.Name = "lblFontSize";
            lblFontSize.Size = new Size(68, 19);
            lblFontSize.TabIndex = 6;
            lblFontSize.Text = "Font Size:";
            // 
            // numFontSize
            // 
            numFontSize.BackColor = Color.FromArgb(38, 38, 38);
            numFontSize.ForeColor = Color.FromArgb(240, 240, 240);
            numFontSize.Location = new Point(12, 235);
            numFontSize.Margin = new Padding(0, 2, 0, 8);
            numFontSize.Minimum = new decimal(new int[] { 8, 0, 0, 0 });
            numFontSize.Name = "numFontSize";
            numFontSize.Size = new Size(310, 27);
            numFontSize.TabIndex = 7;
            numFontSize.Value = new decimal(new int[] { 24, 0, 0, 0 });
            // 
            // colorFlow
            // 
            colorFlow.Controls.Add(btnTextColor);
            colorFlow.Controls.Add(btnBgColor);
            colorFlow.Location = new Point(12, 272);
            colorFlow.Name = "colorFlow";
            colorFlow.Size = new Size(310, 35);
            colorFlow.TabIndex = 8;
            colorFlow.WrapContents = false;
            // 
            // btnTextColor
            // 
            btnTextColor.BackColor = Color.FromArgb(48, 48, 48);
            btnTextColor.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 60);
            btnTextColor.FlatStyle = FlatStyle.Flat;
            btnTextColor.Font = new Font("Segoe UI", 8F);
            btnTextColor.ForeColor = Color.FromArgb(240, 240, 240);
            btnTextColor.Location = new Point(0, 0);
            btnTextColor.Margin = new Padding(0, 0, 8, 0);
            btnTextColor.Name = "btnTextColor";
            btnTextColor.Size = new Size(151, 28);
            btnTextColor.TabIndex = 0;
            btnTextColor.Text = "Text Color";
            btnTextColor.UseVisualStyleBackColor = false;
            // 
            // btnBgColor
            // 
            btnBgColor.BackColor = Color.FromArgb(48, 48, 48);
            btnBgColor.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 60);
            btnBgColor.FlatStyle = FlatStyle.Flat;
            btnBgColor.Font = new Font("Segoe UI", 8F);
            btnBgColor.ForeColor = Color.FromArgb(240, 240, 240);
            btnBgColor.Location = new Point(159, 0);
            btnBgColor.Margin = new Padding(0);
            btnBgColor.Name = "btnBgColor";
            btnBgColor.Size = new Size(151, 28);
            btnBgColor.TabIndex = 1;
            btnBgColor.Text = "BG Color";
            btnBgColor.UseVisualStyleBackColor = false;
            // 
            // lblBoxSize
            // 
            lblBoxSize.AutoSize = true;
            lblBoxSize.Font = new Font("Segoe UI", 8F);
            lblBoxSize.ForeColor = Color.FromArgb(170, 170, 170);
            lblBoxSize.Location = new Point(12, 314);
            lblBoxSize.Margin = new Padding(0, 4, 0, 2);
            lblBoxSize.Name = "lblBoxSize";
            lblBoxSize.Size = new Size(137, 19);
            lblBoxSize.TabIndex = 9;
            lblBoxSize.Text = "Box Size (Width, Ht):";
            // 
            // sizeFlow
            // 
            sizeFlow.Controls.Add(numBoxWidth);
            sizeFlow.Controls.Add(numBoxHeight);
            sizeFlow.Location = new Point(12, 337);
            sizeFlow.Name = "sizeFlow";
            sizeFlow.Size = new Size(310, 35);
            sizeFlow.TabIndex = 10;
            sizeFlow.WrapContents = false;
            // 
            // numBoxWidth
            // 
            numBoxWidth.BackColor = Color.FromArgb(38, 38, 38);
            numBoxWidth.ForeColor = Color.FromArgb(240, 240, 240);
            numBoxWidth.Location = new Point(0, 0);
            numBoxWidth.Margin = new Padding(0, 0, 8, 0);
            numBoxWidth.Maximum = new decimal(new int[] { 2000, 0, 0, 0 });
            numBoxWidth.Name = "numBoxWidth";
            numBoxWidth.Size = new Size(151, 27);
            numBoxWidth.TabIndex = 0;
            numBoxWidth.Value = new decimal(new int[] { 300, 0, 0, 0 });
            // 
            // numBoxHeight
            // 
            numBoxHeight.BackColor = Color.FromArgb(38, 38, 38);
            numBoxHeight.ForeColor = Color.FromArgb(240, 240, 240);
            numBoxHeight.Location = new Point(159, 0);
            numBoxHeight.Margin = new Padding(0);
            numBoxHeight.Maximum = new decimal(new int[] { 2000, 0, 0, 0 });
            numBoxHeight.Name = "numBoxHeight";
            numBoxHeight.Size = new Size(151, 27);
            numBoxHeight.TabIndex = 1;
            numBoxHeight.Value = new decimal(new int[] { 100, 0, 0, 0 });
            // 
            // lblDivider2
            // 
            lblDivider2.BackColor = Color.FromArgb(60, 60, 60);
            lblDivider2.Location = new Point(12, 385);
            lblDivider2.Margin = new Padding(0, 10, 0, 10);
            lblDivider2.Name = "lblDivider2";
            lblDivider2.Size = new Size(310, 1);
            lblDivider2.TabIndex = 11;
            // 
            // lblHeaderAnim
            // 
            lblHeaderAnim.AutoSize = true;
            lblHeaderAnim.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblHeaderAnim.ForeColor = Color.FromArgb(100, 180, 245);
            lblHeaderAnim.Location = new Point(12, 404);
            lblHeaderAnim.Margin = new Padding(0, 8, 0, 3);
            lblHeaderAnim.Name = "lblHeaderAnim";
            lblHeaderAnim.Size = new Size(139, 20);
            lblHeaderAnim.TabIndex = 12;
            lblHeaderAnim.Text = "Clip & Animation";
            // 
            // lblDuration
            // 
            lblDuration.AutoSize = true;
            lblDuration.Font = new Font("Segoe UI", 8F);
            lblDuration.ForeColor = Color.FromArgb(170, 170, 170);
            lblDuration.Location = new Point(12, 431);
            lblDuration.Margin = new Padding(0, 4, 0, 2);
            lblDuration.Name = "lblDuration";
            lblDuration.Size = new Size(122, 19);
            lblDuration.TabIndex = 13;
            lblDuration.Text = "Clip Duration (s):";
            // 
            // numDuration
            // 
            numDuration.BackColor = Color.FromArgb(38, 38, 38);
            numDuration.DecimalPlaces = 1;
            numDuration.ForeColor = Color.FromArgb(240, 240, 240);
            numDuration.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            numDuration.Location = new Point(12, 454);
            numDuration.Margin = new Padding(0, 2, 0, 8);
            numDuration.Name = "numDuration";
            numDuration.Size = new Size(310, 27);
            numDuration.TabIndex = 14;
            numDuration.Value = new decimal(new int[] { 5, 0, 0, 0 });
            // 
            // lblInAnimHeader
            // 
            lblInAnimHeader.AutoSize = true;
            lblInAnimHeader.Font = new Font("Segoe UI", 8F);
            lblInAnimHeader.ForeColor = Color.FromArgb(170, 170, 170);
            lblInAnimHeader.Location = new Point(12, 491);
            lblInAnimHeader.Margin = new Padding(0, 4, 0, 2);
            lblInAnimHeader.Name = "lblInAnimHeader";
            lblInAnimHeader.Size = new Size(190, 19);
            lblInAnimHeader.TabIndex = 15;
            lblInAnimHeader.Text = "In Animation / Duration (s):";
            // 
            // inAnimFlow
            // 
            inAnimFlow.Controls.Add(cbInEffect);
            inAnimFlow.Controls.Add(numInDuration);
            inAnimFlow.Location = new Point(12, 514);
            inAnimFlow.Margin = new Padding(0, 0, 0, 8);
            inAnimFlow.Name = "inAnimFlow";
            inAnimFlow.Size = new Size(310, 35);
            inAnimFlow.TabIndex = 16;
            inAnimFlow.WrapContents = false;
            // 
            // cbInEffect
            // 
            cbInEffect.BackColor = Color.FromArgb(38, 38, 38);
            cbInEffect.DropDownStyle = ComboBoxStyle.DropDownList;
            cbInEffect.FlatStyle = FlatStyle.Flat;
            cbInEffect.ForeColor = Color.FromArgb(240, 240, 240);
            cbInEffect.FormattingEnabled = true;
            cbInEffect.Items.AddRange(new object[] { "None", "Fade", "Slide", "Wave", "Zoom", "ZoomBlur", "ZoomBlurUp", "ZoomBlurDown", "ZoomBlurLeft", "ZoomBlurRight", "DynamicZoomBlur" });
            cbInEffect.Location = new Point(0, 0);
            cbInEffect.Margin = new Padding(0, 0, 8, 0);
            cbInEffect.Name = "cbInEffect";
            cbInEffect.Size = new Size(185, 28);
            cbInEffect.TabIndex = 0;
            // 
            // numInDuration
            // 
            numInDuration.BackColor = Color.FromArgb(38, 38, 38);
            numInDuration.DecimalPlaces = 1;
            numInDuration.ForeColor = Color.FromArgb(240, 240, 240);
            numInDuration.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            numInDuration.Location = new Point(193, 0);
            numInDuration.Margin = new Padding(0);
            numInDuration.Name = "numInDuration";
            numInDuration.Size = new Size(117, 27);
            numInDuration.TabIndex = 1;
            numInDuration.Value = new decimal(new int[] { 1, 0, 0, 0 });
            // 
            // lblOutAnimHeader
            // 
            lblOutAnimHeader.AutoSize = true;
            lblOutAnimHeader.Font = new Font("Segoe UI", 8F);
            lblOutAnimHeader.ForeColor = Color.FromArgb(170, 170, 170);
            lblOutAnimHeader.Location = new Point(12, 559);
            lblOutAnimHeader.Margin = new Padding(0, 4, 0, 2);
            lblOutAnimHeader.Name = "lblOutAnimHeader";
            lblOutAnimHeader.Size = new Size(202, 19);
            lblOutAnimHeader.TabIndex = 17;
            lblOutAnimHeader.Text = "Out Animation / Duration (s):";
            // 
            // outAnimFlow
            // 
            outAnimFlow.Controls.Add(cbOutEffect);
            outAnimFlow.Controls.Add(numOutDuration);
            outAnimFlow.Location = new Point(12, 582);
            outAnimFlow.Margin = new Padding(0, 0, 0, 8);
            outAnimFlow.Name = "outAnimFlow";
            outAnimFlow.Size = new Size(310, 35);
            outAnimFlow.TabIndex = 18;
            outAnimFlow.WrapContents = false;
            // 
            // cbOutEffect
            // 
            cbOutEffect.BackColor = Color.FromArgb(38, 38, 38);
            cbOutEffect.DropDownStyle = ComboBoxStyle.DropDownList;
            cbOutEffect.FlatStyle = FlatStyle.Flat;
            cbOutEffect.ForeColor = Color.FromArgb(240, 240, 240);
            cbOutEffect.FormattingEnabled = true;
            cbOutEffect.Items.AddRange(new object[] { "None", "Fade", "Slide", "Wave", "Zoom", "ZoomBlur", "ZoomBlurUp", "ZoomBlurDown", "ZoomBlurLeft", "ZoomBlurRight", "DynamicZoomBlur" });
            cbOutEffect.Location = new Point(0, 0);
            cbOutEffect.Margin = new Padding(0, 0, 8, 0);
            cbOutEffect.Name = "cbOutEffect";
            cbOutEffect.Size = new Size(185, 28);
            cbOutEffect.TabIndex = 0;
            // 
            // numOutDuration
            // 
            numOutDuration.BackColor = Color.FromArgb(38, 38, 38);
            numOutDuration.DecimalPlaces = 1;
            numOutDuration.ForeColor = Color.FromArgb(240, 240, 240);
            numOutDuration.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            numOutDuration.Location = new Point(193, 0);
            numOutDuration.Margin = new Padding(0);
            numOutDuration.Name = "numOutDuration";
            numOutDuration.Size = new Size(117, 27);
            numOutDuration.TabIndex = 1;
            numOutDuration.Value = new decimal(new int[] { 1, 0, 0, 0 });
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
            row1Flow.ResumeLayout(false);
            row2Flow.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)numFontSize).EndInit();
            colorFlow.ResumeLayout(false);
            sizeFlow.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)numBoxWidth).EndInit();
            ((System.ComponentModel.ISupportInitialize)numBoxHeight).EndInit();
            ((System.ComponentModel.ISupportInitialize)numDuration).EndInit();
            inAnimFlow.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)numInDuration).EndInit();
            outAnimFlow.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)numOutDuration).EndInit();
            ResumeLayout(false);
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
        private DarkScrollPanel rightPanel;
        private Label lblSidebarTitle;
        private FlowLayoutPanel row1Flow;
        private Button btnSplit;
        private FlowLayoutPanel row2Flow;
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
        private Label lblInAnimHeader;
        private FlowLayoutPanel inAnimFlow;
        private ComboBox cbInEffect;
        private NumericUpDown numInDuration;
        private Label lblOutAnimHeader;
        private FlowLayoutPanel outAnimFlow;
        private ComboBox cbOutEffect;
        private NumericUpDown numOutDuration;
        private TimelineControl timelineControl;

        private void RightPanel_SizeChanged(object sender, EventArgs e)
        {
            int availableWidth = rightPanel.ClientSize.Width - rightPanel.Padding.Horizontal - 20;
            if (availableWidth < 100) return;

            // Update full-width controls
            row1Flow.Width = availableWidth;
            btnSplit.Width = availableWidth;

            row2Flow.Width = availableWidth;
            int halfWidth = (availableWidth - 8) / 2;
            btnSplitLeft.Width = halfWidth;
            btnSplitRight.Width = halfWidth;

            lblDivider1.Width = availableWidth;
            btnAddText.Width = availableWidth;
            numFontSize.Width = availableWidth;

            colorFlow.Width = availableWidth;
            btnTextColor.Width = halfWidth;
            btnBgColor.Width = halfWidth;

            sizeFlow.Width = availableWidth;
            numBoxWidth.Width = halfWidth;
            numBoxHeight.Width = halfWidth;

            lblDivider2.Width = availableWidth;
            numDuration.Width = availableWidth;

            inAnimFlow.Width = availableWidth;
            int comboWidth = (int)(availableWidth * 0.6f);
            int numWidth = availableWidth - comboWidth - 8;
            cbInEffect.Width = comboWidth;
            numInDuration.Width = numWidth;

            outAnimFlow.Width = availableWidth;
            cbOutEffect.Width = comboWidth;
            numOutDuration.Width = numWidth;
        }
    }
}