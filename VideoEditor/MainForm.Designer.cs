using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using VideoEditor.Controls;

namespace VideoEditor
{
    public class DarkListBox : ListBox
    {
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubSubAppName);

        [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            if (Environment.OSVersion.Version.Major >= 10)
            {
                int useDarkMode = 1;
                DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
                SetWindowTheme(this.Handle, "DarkMode_Explorer", null);
            }
        }
    }
    // Custom FlowLayoutPanel that forces native dark mode scrollbars on Windows 10/11
    public class DarkScrollPanel : FlowLayoutPanel
    {
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubSubAppName);

        [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20; // Use 19 for Windows 10 versions prior to 20H1

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            if (Environment.OSVersion.Version.Major >= 10)
            {
                // Force DWM to apply immersive dark mode to native frame/scrollbar elements
                int useDarkMode = 1;
                DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));

                // Set uxtheme sub-app to DarkMode Explorer
                SetWindowTheme(this.Handle, "DarkMode_Explorer", null);
            }
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            mainLayout = new TableLayoutPanel();
            toolbar = new FlowLayoutPanel();
            btnImport = new Button();
            btnAutoCaption = new Button();
            btnExport = new Button();
            btnClearAll = new Button();
            leftPanel = new Panel();
            mediaListBox = new DarkListBox();
            previewControl = new PreviewControl();
            rightPanel = new DarkScrollPanel();
            lblSidebarTitle = new Label();
            lblHeaderText = new Label();
            btnAddText = new Button();
            lblFontSize = new Label();
            numFontSize = new NumericUpDown();
            colorFlow = new FlowLayoutPanel();
            btnTextColor = new Button();
            btnBgColor = new Button();
            lblHeaderBlur = new Label();
            btnBlurOverlay = new Button();
            lblDivider3 = new Label();
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
            timelineHeaderLayout = new TableLayoutPanel();
            timelineHeaderLeft = new Panel();
            timelineHeaderCenter = new TableLayoutPanel();
            btnPlayPause = new Button();
            btnSplitLeft = new Button();
            btnSplit = new Button();
            btnSplitRight = new Button();
            timelineHeaderRight = new Panel();
            timelineControl = new TimelineControl();
            mainLayout.SuspendLayout();
            toolbar.SuspendLayout();
            leftPanel.SuspendLayout();
            rightPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numFontSize).BeginInit();
            colorFlow.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numDuration).BeginInit();
            inAnimFlow.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numInDuration).BeginInit();
            outAnimFlow.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numOutDuration).BeginInit();
            timelineHeaderLayout.SuspendLayout();
            timelineHeaderCenter.SuspendLayout();
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
            mainLayout.Controls.Add(timelineHeaderLayout, 0, 2);
            mainLayout.Controls.Add(timelineControl, 0, 3);
            mainLayout.Dock = DockStyle.Fill;
            mainLayout.Location = new Point(0, 0);
            mainLayout.Name = "mainLayout";
            mainLayout.RowCount = 4;
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 220F));
            mainLayout.Size = new Size(1400, 900);
            mainLayout.TabIndex = 0;
            // 
            // toolbar
            // 
            toolbar.BackColor = Color.FromArgb(28, 28, 28);
            mainLayout.SetColumnSpan(toolbar, 3);
            toolbar.Controls.Add(btnImport);
            toolbar.Controls.Add(btnAutoCaption);
            toolbar.Controls.Add(btnExport);
            toolbar.Controls.Add(btnClearAll);
            toolbar.Dock = DockStyle.Fill;
            toolbar.Location = new Point(3, 3);
            toolbar.Name = "toolbar";
            toolbar.Padding = new Padding(10);
            toolbar.Size = new Size(1394, 44);
            toolbar.TabIndex = 0;
            // 
            // btnImport
            // 
            btnImport.AccessibleName = "";
            btnImport.BackColor = Color.FromArgb(48, 48, 48);
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
            // btnAutoCaption
            // 
            btnAutoCaption.AccessibleName = "";
            btnAutoCaption.BackColor = Color.FromArgb(48, 48, 48);
            btnAutoCaption.FlatAppearance.BorderSize = 0;
            btnAutoCaption.FlatStyle = FlatStyle.Flat;
            btnAutoCaption.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnAutoCaption.ForeColor = Color.White;
            btnAutoCaption.Location = new Point(158, 10);
            btnAutoCaption.Margin = new Padding(0, 0, 8, 0);
            btnAutoCaption.Name = "btnAutoCaption";
            btnAutoCaption.Size = new Size(140, 32);
            btnAutoCaption.TabIndex = 4;
            btnAutoCaption.Text = "Auto Caption";
            btnAutoCaption.UseVisualStyleBackColor = false;
            // 
            // btnExport
            // 
            btnExport.BackColor = SystemColors.Highlight;
            btnExport.FlatAppearance.BorderSize = 0;
            btnExport.FlatStyle = FlatStyle.Flat;
            btnExport.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnExport.ForeColor = Color.White;
            btnExport.Location = new Point(306, 10);
            btnExport.Margin = new Padding(0, 0, 8, 0);
            btnExport.Name = "btnExport";
            btnExport.Size = new Size(140, 32);
            btnExport.TabIndex = 3;
            btnExport.Text = "Export Video";
            btnExport.UseVisualStyleBackColor = false;
            // 
            // btnClearAll
            // 
            btnClearAll.BackColor = Color.Crimson;
            btnClearAll.FlatAppearance.BorderSize = 0;
            btnClearAll.FlatStyle = FlatStyle.Flat;
            btnClearAll.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnClearAll.ForeColor = Color.White;
            btnClearAll.Location = new Point(454, 10);
            btnClearAll.Margin = new Padding(0, 0, 8, 0);
            btnClearAll.Name = "btnClearAll";
            btnClearAll.Size = new Size(140, 32);
            btnClearAll.TabIndex = 5;
            btnClearAll.Text = "Clear All";
            btnClearAll.UseVisualStyleBackColor = false;
            // 
            // leftPanel
            // 
            leftPanel.BackColor = Color.FromArgb(28, 28, 28);
            leftPanel.Controls.Add(mediaListBox);
            leftPanel.Dock = DockStyle.Fill;
            leftPanel.Location = new Point(3, 53);
            leftPanel.Name = "leftPanel";
            leftPanel.Size = new Size(274, 589);
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
            mediaListBox.Size = new Size(274, 589);
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
            previewControl.Size = new Size(764, 589);
            previewControl.TabIndex = 2;
            previewControl.TimelineRef = null;
            previewControl.UndoRedoManager = null;
            // 
            // rightPanel
            // 
            rightPanel.AutoScroll = true;
            rightPanel.BackColor = Color.FromArgb(28, 28, 28);
            rightPanel.Controls.Add(lblSidebarTitle);
            rightPanel.Controls.Add(lblHeaderText);
            rightPanel.Controls.Add(btnAddText);
            rightPanel.Controls.Add(lblFontSize);
            rightPanel.Controls.Add(numFontSize);
            rightPanel.Controls.Add(colorFlow);
            rightPanel.Controls.Add(lblHeaderBlur);
            rightPanel.Controls.Add(btnBlurOverlay);
            rightPanel.Controls.Add(lblDivider3);
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
            rightPanel.Size = new Size(344, 589);
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
            // lblHeaderText
            // 
            lblHeaderText.AutoSize = true;
            lblHeaderText.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblHeaderText.ForeColor = Color.FromArgb(100, 180, 245);
            lblHeaderText.Location = new Point(12, 53);
            lblHeaderText.Margin = new Padding(0, 8, 0, 3);
            lblHeaderText.Name = "lblHeaderText";
            lblHeaderText.Size = new Size(96, 20);
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
            btnAddText.Location = new Point(12, 76);
            btnAddText.Margin = new Padding(0, 0, 0, 5);
            btnAddText.Name = "btnAddText";
            btnAddText.Size = new Size(320, 32);
            btnAddText.TabIndex = 5;
            btnAddText.Text = "➕ Add Text Layer";
            btnAddText.UseVisualStyleBackColor = false;
            // 
            // lblFontSize
            // 
            lblFontSize.AutoSize = true;
            lblFontSize.Font = new Font("Segoe UI", 8F);
            lblFontSize.ForeColor = Color.FromArgb(170, 170, 170);
            lblFontSize.Location = new Point(12, 117);
            lblFontSize.Margin = new Padding(0, 4, 0, 2);
            lblFontSize.Name = "lblFontSize";
            lblFontSize.Size = new Size(67, 19);
            lblFontSize.TabIndex = 6;
            lblFontSize.Text = "Font Size:";
            // 
            // numFontSize
            // 
            numFontSize.BackColor = Color.FromArgb(38, 38, 38);
            numFontSize.ForeColor = Color.FromArgb(240, 240, 240);
            numFontSize.Location = new Point(12, 140);
            numFontSize.Margin = new Padding(0, 2, 0, 8);
            numFontSize.Minimum = new decimal(new int[] { 8, 0, 0, 0 });
            numFontSize.Name = "numFontSize";
            numFontSize.Size = new Size(320, 27);
            numFontSize.TabIndex = 7;
            numFontSize.Value = new decimal(new int[] { 24, 0, 0, 0 });
            // 
            // colorFlow
            // 
            colorFlow.Controls.Add(btnTextColor);
            colorFlow.Controls.Add(btnBgColor);
            colorFlow.Location = new Point(15, 178);
            colorFlow.Name = "colorFlow";
            colorFlow.Size = new Size(320, 35);
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
            btnTextColor.Size = new Size(156, 28);
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
            btnBgColor.Location = new Point(164, 0);
            btnBgColor.Margin = new Padding(0);
            btnBgColor.Name = "btnBgColor";
            btnBgColor.Size = new Size(156, 28);
            btnBgColor.TabIndex = 1;
            btnBgColor.Text = "BG Color";
            btnBgColor.UseVisualStyleBackColor = false;
            // 
            // lblHeaderBlur
            // 
            lblHeaderBlur.AutoSize = true;
            lblHeaderBlur.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblHeaderBlur.ForeColor = Color.FromArgb(100, 180, 245);
            lblHeaderBlur.Location = new Point(12, 224);
            lblHeaderBlur.Margin = new Padding(0, 8, 0, 3);
            lblHeaderBlur.Name = "lblHeaderBlur";
            lblHeaderBlur.Size = new Size(95, 20);
            lblHeaderBlur.TabIndex = 12;
            lblHeaderBlur.Text = "Blur Overlay";
            // 
            // btnBlurOverlay
            // 
            btnBlurOverlay.BackColor = Color.FromArgb(70, 70, 70);
            btnBlurOverlay.FlatAppearance.BorderSize = 0;
            btnBlurOverlay.FlatStyle = FlatStyle.Flat;
            btnBlurOverlay.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnBlurOverlay.ForeColor = Color.White;
            btnBlurOverlay.Location = new Point(12, 247);
            btnBlurOverlay.Margin = new Padding(0, 0, 0, 5);
            btnBlurOverlay.Name = "btnBlurOverlay";
            btnBlurOverlay.Size = new Size(320, 32);
            btnBlurOverlay.TabIndex = 13;
            btnBlurOverlay.Text = "💧 Add Blur Overlay";
            btnBlurOverlay.UseVisualStyleBackColor = false;
            // 
            // lblDivider3
            // 
            lblDivider3.BackColor = Color.FromArgb(60, 60, 60);
            lblDivider3.Location = new Point(12, 294);
            lblDivider3.Margin = new Padding(0, 10, 0, 10);
            lblDivider3.Name = "lblDivider3";
            lblDivider3.Size = new Size(320, 1);
            lblDivider3.TabIndex = 14;
            // 
            // lblHeaderAnim
            // 
            lblHeaderAnim.AutoSize = true;
            lblHeaderAnim.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblHeaderAnim.ForeColor = Color.FromArgb(100, 180, 245);
            lblHeaderAnim.Location = new Point(12, 313);
            lblHeaderAnim.Margin = new Padding(0, 8, 0, 3);
            lblHeaderAnim.Name = "lblHeaderAnim";
            lblHeaderAnim.Size = new Size(117, 20);
            lblHeaderAnim.TabIndex = 15;
            lblHeaderAnim.Text = "Clip & Animation";
            // 
            // lblDuration
            // 
            lblDuration.AutoSize = true;
            lblDuration.Font = new Font("Segoe UI", 8F);
            lblDuration.ForeColor = Color.FromArgb(170, 170, 170);
            lblDuration.Location = new Point(12, 340);
            lblDuration.Margin = new Padding(0, 4, 0, 2);
            lblDuration.Name = "lblDuration";
            lblDuration.Size = new Size(111, 19);
            lblDuration.TabIndex = 16;
            lblDuration.Text = "Clip Duration (s):";
            // 
            // numDuration
            // 
            numDuration.BackColor = Color.FromArgb(38, 38, 38);
            numDuration.DecimalPlaces = 1;
            numDuration.ForeColor = Color.FromArgb(240, 240, 240);
            numDuration.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            numDuration.Location = new Point(12, 363);
            numDuration.Margin = new Padding(0, 2, 0, 8);
            numDuration.Name = "numDuration";
            numDuration.Size = new Size(320, 27);
            numDuration.TabIndex = 17;
            numDuration.Value = new decimal(new int[] { 5, 0, 0, 0 });
            // 
            // lblInAnimHeader
            // 
            lblInAnimHeader.AutoSize = true;
            lblInAnimHeader.Font = new Font("Segoe UI", 8F);
            lblInAnimHeader.ForeColor = Color.FromArgb(170, 170, 170);
            lblInAnimHeader.Location = new Point(12, 402);
            lblInAnimHeader.Margin = new Padding(0, 4, 0, 2);
            lblInAnimHeader.Name = "lblInAnimHeader";
            lblInAnimHeader.Size = new Size(176, 19);
            lblInAnimHeader.TabIndex = 18;
            lblInAnimHeader.Text = "In Animation / Duration (s):";
            // 
            // inAnimFlow
            // 
            inAnimFlow.Controls.Add(cbInEffect);
            inAnimFlow.Controls.Add(numInDuration);
            inAnimFlow.Location = new Point(12, 423);
            inAnimFlow.Margin = new Padding(0, 0, 0, 8);
            inAnimFlow.Name = "inAnimFlow";
            inAnimFlow.Size = new Size(320, 35);
            inAnimFlow.TabIndex = 19;
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
            numInDuration.Size = new Size(127, 27);
            numInDuration.TabIndex = 1;
            numInDuration.Value = new decimal(new int[] { 1, 0, 0, 0 });
            // 
            // lblOutAnimHeader
            // 
            lblOutAnimHeader.AutoSize = true;
            lblOutAnimHeader.Font = new Font("Segoe UI", 8F);
            lblOutAnimHeader.ForeColor = Color.FromArgb(170, 170, 170);
            lblOutAnimHeader.Location = new Point(12, 470);
            lblOutAnimHeader.Margin = new Padding(0, 4, 0, 2);
            lblOutAnimHeader.Name = "lblOutAnimHeader";
            lblOutAnimHeader.Size = new Size(188, 19);
            lblOutAnimHeader.TabIndex = 20;
            lblOutAnimHeader.Text = "Out Animation / Duration (s):";
            // 
            // outAnimFlow
            // 
            outAnimFlow.Controls.Add(cbOutEffect);
            outAnimFlow.Controls.Add(numOutDuration);
            outAnimFlow.Location = new Point(12, 491);
            outAnimFlow.Margin = new Padding(0, 0, 0, 8);
            outAnimFlow.Name = "outAnimFlow";
            outAnimFlow.Size = new Size(320, 35);
            outAnimFlow.TabIndex = 21;
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
            numOutDuration.Size = new Size(127, 27);
            numOutDuration.TabIndex = 1;
            numOutDuration.Value = new decimal(new int[] { 1, 0, 0, 0 });
            // 
            // timelineHeaderLayout
            // 
            timelineHeaderLayout.ColumnCount = 3;
            mainLayout.SetColumnSpan(timelineHeaderLayout, 3);
            timelineHeaderLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            timelineHeaderLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));
            timelineHeaderLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            timelineHeaderLayout.Controls.Add(timelineHeaderLeft, 0, 0);
            timelineHeaderLayout.Controls.Add(timelineHeaderCenter, 1, 0);
            timelineHeaderLayout.Controls.Add(timelineHeaderRight, 2, 0);
            timelineHeaderLayout.Dock = DockStyle.Fill;
            timelineHeaderLayout.Location = new Point(0, 645);
            timelineHeaderLayout.Margin = new Padding(0);
            timelineHeaderLayout.Name = "timelineHeaderLayout";
            timelineHeaderLayout.RowCount = 1;
            timelineHeaderLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            timelineHeaderLayout.Size = new Size(1400, 35);
            timelineHeaderLayout.TabIndex = 5;
            // 
            // timelineHeaderLeft
            // 
            timelineHeaderLeft.BackColor = Color.FromArgb(28, 28, 28);
            timelineHeaderLeft.Dock = DockStyle.Fill;
            timelineHeaderLeft.Location = new Point(0, 0);
            timelineHeaderLeft.Margin = new Padding(0);
            timelineHeaderLeft.Name = "timelineHeaderLeft";
            timelineHeaderLeft.Size = new Size(280, 35);
            timelineHeaderLeft.TabIndex = 0;
            // 
            // timelineHeaderCenter
            // 
            timelineHeaderCenter.BackColor = Color.FromArgb(20, 20, 20);
            timelineHeaderCenter.ColumnCount = 4;
            timelineHeaderCenter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            timelineHeaderCenter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            timelineHeaderCenter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            timelineHeaderCenter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            timelineHeaderCenter.Controls.Add(btnPlayPause, 0, 0);
            timelineHeaderCenter.Controls.Add(btnSplitLeft, 1, 0);
            timelineHeaderCenter.Controls.Add(btnSplit, 2, 0);
            timelineHeaderCenter.Controls.Add(btnSplitRight, 3, 0);
            timelineHeaderCenter.Dock = DockStyle.Fill;
            timelineHeaderCenter.Location = new Point(280, 0);
            timelineHeaderCenter.Margin = new Padding(0);
            timelineHeaderCenter.Name = "timelineHeaderCenter";
            timelineHeaderCenter.RowCount = 1;
            timelineHeaderCenter.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            timelineHeaderCenter.Size = new Size(770, 35);
            timelineHeaderCenter.TabIndex = 1;
            // 
            // btnPlayPause
            // 
            btnPlayPause.AccessibleName = "";
            btnPlayPause.BackColor = Color.FromArgb(83, 168, 83);
            btnPlayPause.Dock = DockStyle.Fill;
            btnPlayPause.FlatAppearance.BorderSize = 0;
            btnPlayPause.FlatStyle = FlatStyle.Flat;
            btnPlayPause.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnPlayPause.ForeColor = Color.White;
            btnPlayPause.Location = new Point(1, 1);
            btnPlayPause.Margin = new Padding(1);
            btnPlayPause.Name = "btnPlayPause";
            btnPlayPause.Size = new Size(190, 33);
            btnPlayPause.TabIndex = 0;
            btnPlayPause.Text = "▶ Play";
            btnPlayPause.UseVisualStyleBackColor = false;
            // 
            // btnSplitLeft
            // 
            btnSplitLeft.BackColor = Color.FromArgb(48, 48, 48);
            btnSplitLeft.Dock = DockStyle.Fill;
            btnSplitLeft.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 60);
            btnSplitLeft.FlatStyle = FlatStyle.Flat;
            btnSplitLeft.Font = new Font("Segoe UI", 8.5F);
            btnSplitLeft.ForeColor = Color.FromArgb(240, 240, 240);
            btnSplitLeft.Location = new Point(193, 1);
            btnSplitLeft.Margin = new Padding(1);
            btnSplitLeft.Name = "btnSplitLeft";
            btnSplitLeft.Size = new Size(190, 33);
            btnSplitLeft.TabIndex = 1;
            btnSplitLeft.Text = "⬅ Trim Left";
            btnSplitLeft.UseVisualStyleBackColor = false;
            // 
            // btnSplit
            // 
            btnSplit.BackColor = Color.FromArgb(48, 48, 48);
            btnSplit.Dock = DockStyle.Fill;
            btnSplit.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 60);
            btnSplit.FlatStyle = FlatStyle.Flat;
            btnSplit.Font = new Font("Segoe UI", 8.5F);
            btnSplit.ForeColor = Color.FromArgb(240, 240, 240);
            btnSplit.Location = new Point(385, 1);
            btnSplit.Margin = new Padding(1);
            btnSplit.Name = "btnSplit";
            btnSplit.Size = new Size(190, 33);
            btnSplit.TabIndex = 2;
            btnSplit.Text = "✂ Split Clip";
            btnSplit.UseVisualStyleBackColor = false;
            // 
            // btnSplitRight
            // 
            btnSplitRight.BackColor = Color.FromArgb(48, 48, 48);
            btnSplitRight.Dock = DockStyle.Fill;
            btnSplitRight.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 60);
            btnSplitRight.FlatStyle = FlatStyle.Flat;
            btnSplitRight.Font = new Font("Segoe UI", 8.5F);
            btnSplitRight.ForeColor = Color.FromArgb(240, 240, 240);
            btnSplitRight.Location = new Point(577, 1);
            btnSplitRight.Margin = new Padding(1);
            btnSplitRight.Name = "btnSplitRight";
            btnSplitRight.Size = new Size(192, 33);
            btnSplitRight.TabIndex = 3;
            btnSplitRight.Text = "➡ Trim Right";
            btnSplitRight.UseVisualStyleBackColor = false;
            // 
            // timelineHeaderRight
            // 
            timelineHeaderRight.BackColor = Color.FromArgb(28, 28, 28);
            timelineHeaderRight.Dock = DockStyle.Fill;
            timelineHeaderRight.Location = new Point(1050, 0);
            timelineHeaderRight.Margin = new Padding(0);
            timelineHeaderRight.Name = "timelineHeaderRight";
            timelineHeaderRight.Size = new Size(350, 35);
            timelineHeaderRight.TabIndex = 2;
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
            timelineControl.UndoRedoManager = null;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(18, 18, 18);
            ClientSize = new Size(1400, 900);
            Controls.Add(mainLayout);
            Icon = (Icon)resources.GetObject("$this.Icon");
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
            ((System.ComponentModel.ISupportInitialize)numDuration).EndInit();
            inAnimFlow.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)numInDuration).EndInit();
            outAnimFlow.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)numOutDuration).EndInit();
            timelineHeaderLayout.ResumeLayout(false);
            timelineHeaderCenter.ResumeLayout(false);
            ResumeLayout(false);
        }


        #endregion

        private TableLayoutPanel mainLayout;
        private FlowLayoutPanel toolbar;
        private Button btnImport;
        private Button btnPlayPause;
        private Button btnAutoCaption;
        private Button btnExport;
        private Panel leftPanel;
        private PreviewControl previewControl;
        private DarkScrollPanel rightPanel;
        private Label lblSidebarTitle;
        private Button btnSplit;
        private Button btnSplitLeft;
        private Button btnSplitRight;
        private Label lblHeaderText;
        private Button btnAddText;
        private Label lblFontSize;
        private NumericUpDown numFontSize;
        private FlowLayoutPanel colorFlow;
        private Button btnTextColor;
        private Button btnBgColor;
        private Label lblHeaderBlur;
        private Button btnBlurOverlay;
        private Label lblDivider3;
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
        private TableLayoutPanel timelineHeaderLayout;
        private Panel timelineHeaderLeft;
        private TableLayoutPanel timelineHeaderCenter;
        private Panel timelineHeaderRight;
        private TimelineControl timelineControl;
        private Button btnClearAll;
        private DarkListBox mediaListBox;
    }
}