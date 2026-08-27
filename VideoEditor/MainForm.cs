using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media;
using VideoEditor.Controls;
using VideoEditor.Models;
using Color = System.Drawing.Color;
using VideoEditor.Services;
using Timer = System.Windows.Forms.Timer;

namespace VideoEditor
{
    public partial class MainForm : Form
    {
        private List<MediaItem> mediaItems = new List<MediaItem>();
        private PreviewControl previewControl;
        private TimelineControl timelineControl;
        private VideoExportService exportService;
        private AudioCaptionService captionService;
        private ListBox mediaListBox;

        private Timer playbackTimer;
        private Stopwatch playbackStopwatch = new Stopwatch();
        private double playbackStartOffset = 0;
        private bool isPlaying = false;
        private bool isUserScrubbing = false;
        private Button btnPlayPause;

        private MediaPlayer audioPlayer = new MediaPlayer();
        private string currentAudioPath = null;

        private Timer scrubAudioTimer;
        private double pendingScrubTime = -1;

        private NumericUpDown numDuration;
        private ComboBox cbInEffect;
        private NumericUpDown numInDuration;
        private ComboBox cbOutEffect;
        private NumericUpDown numOutDuration;

        // Sidebar styling fields
        private NumericUpDown numFontSize;
        private Button btnTextColor;
        private Button btnBgColor;
        private NumericUpDown numBoxWidth;
        private NumericUpDown numBoxHeight;

        private bool isBindingUI = false;

        public MainForm()
        {
            InitializeComponent();
            exportService = new VideoExportService();
            captionService = new AudioCaptionService();

            InitializeUI();
            InitializePlaybackEngine();
        }

        private void InitializePlaybackEngine()
        {
            playbackTimer = new Timer { Interval = 15 };
            playbackTimer.Tick += (s, e) =>
            {
                if (!isPlaying) return;

                double elapsedSecs = playbackStopwatch.Elapsed.TotalSeconds;
                timelineControl.CurrentTime = playbackStartOffset + elapsedSecs;

                if (timelineControl.CurrentTime >= timelineControl.GetTotalDuration())
                {
                    PausePlayback();
                    timelineControl.CurrentTime = 0;
                }
            };

            scrubAudioTimer = new Timer { Interval = 30 };
            scrubAudioTimer.Tick += (s, e) =>
            {
                scrubAudioTimer.Stop();
                if (pendingScrubTime >= 0)
                {
                    ApplyAudioSeek(pendingScrubTime);
                    pendingScrubTime = -1;
                }
            };
        }

        private void EnsureAudioLoaded(string filePath)
        {
            if (currentAudioPath != filePath)
            {
                audioPlayer.Close();
                audioPlayer.Open(new Uri(filePath, UriKind.Absolute));
                currentAudioPath = filePath;
            }
        }

        private void ApplyAudioSeek(double timePosition)
        {
            var audioItem = mediaItems.FirstOrDefault(x => x.Type == MediaType.Audio);
            if (audioItem != null && File.Exists(audioItem.FilePath))
            {
                EnsureAudioLoaded(audioItem.FilePath);

                if (timePosition >= audioItem.StartTime && timePosition < (audioItem.StartTime + audioItem.Duration))
                {
                    double relativeSecs = (timePosition - audioItem.StartTime) + audioItem.SourceOffset;
                    audioPlayer.Position = TimeSpan.FromSeconds(Math.Max(0, relativeSecs));

                    if (isPlaying)
                    {
                        audioPlayer.Play();
                    }
                }
                else
                {
                    audioPlayer.Pause();
                }
            }
        }

        private void StartPlayback()
        {
            playbackStartOffset = timelineControl.CurrentTime;
            playbackStopwatch.Restart();

            var audioItem = mediaItems.FirstOrDefault(x => x.Type == MediaType.Audio);
            if (audioItem != null && File.Exists(audioItem.FilePath))
            {
                double currentTime = timelineControl.CurrentTime;
                if (currentTime >= audioItem.StartTime && currentTime < (audioItem.StartTime + audioItem.Duration))
                {
                    EnsureAudioLoaded(audioItem.FilePath);

                    double relativeSecs = (currentTime - audioItem.StartTime) + audioItem.SourceOffset;
                    audioPlayer.Position = TimeSpan.FromSeconds(Math.Max(0, relativeSecs));
                    audioPlayer.Play();
                }
            }

            isPlaying = true;
            btnPlayPause.Text = "⏸ Pause";
            playbackTimer.Start();
        }

        private void PausePlayback()
        {
            playbackStopwatch.Stop();
            try
            {
                audioPlayer.Pause();
            }
            catch { }

            isPlaying = false;
            btnPlayPause.Text = "▶ Play";
            playbackTimer.Stop();
        }

        private void InitializeUI()
        {
            this.Text = "VideoEditor - Mobile Slideshow Video Maker";
            this.Size = new Size(1400, 900);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(25, 25, 25);

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 3
            };

            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));

            var toolbar = CreateToolbar();
            mainLayout.Controls.Add(toolbar, 0, 0);
            mainLayout.SetColumnSpan(toolbar, 3);

            var leftPanel = CreateMediaPanel();
            mainLayout.Controls.Add(leftPanel, 0, 1);

            previewControl = new PreviewControl { Dock = DockStyle.Fill };
            mainLayout.Controls.Add(previewControl, 1, 1);

            var rightPanel = CreateActionSidebar();
            mainLayout.Controls.Add(rightPanel, 2, 1);

            timelineControl = new TimelineControl(mediaItems) { Dock = DockStyle.Fill };

            timelineControl.TimeChanged += (time) =>
            {
                previewControl.RenderFrame(mediaItems, time);

                if (isUserScrubbing || !isPlaying)
                {
                    pendingScrubTime = time;
                    scrubAudioTimer.Stop();
                    scrubAudioTimer.Start();
                }
            };

            timelineControl.ClipSelected += (selectedItem) =>
            {
                previewControl.SelectedItem = selectedItem;
                BindSelectedMediaToUI(selectedItem);
                if (selectedItem?.Type == MediaType.Text && selectedItem.TextData != null)
                {
                    BindTextLabelToUI(selectedItem.TextData);
                    previewControl.SelectedTextLabel = selectedItem.TextData;
                }
                previewControl.RenderFrame(mediaItems, timelineControl.CurrentTime);
            };

            timelineControl.ItemResized += (resizedItem) =>
            {
                TimelineControl_ItemResized(timelineControl, resizedItem);
            };

            previewControl.TextLabelSelected += (label) =>
            {
                BindTextLabelToUI(label);
            };

            timelineControl.MouseDown += (s, e) =>
            {
                isUserScrubbing = true;
                if (isPlaying) PausePlayback();
            };

            timelineControl.MouseUp += (s, e) =>
            {
                isUserScrubbing = false;
                scrubAudioTimer.Stop();
                ApplyAudioSeek(timelineControl.CurrentTime);
            };

            mainLayout.Controls.Add(timelineControl, 0, 2);
            mainLayout.SetColumnSpan(timelineControl, 3);

            this.Controls.Add(mainLayout);
        }

        private Control CreateActionSidebar()
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(12),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true
            };

            panel.Controls.Add(new Label
            {
                Text = "Editing Actions",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8)
            });

            // --- TIMELINE ACTIONS ---
            panel.Controls.Add(CreateActionButton("✂ Split Clip", () => SplitSelectedClip()));
            panel.Controls.Add(CreateActionButton("✂◄ Split Left (Trim Left)", () => SplitLeftSelectedClip()));
            panel.Controls.Add(CreateActionButton("►✂ Split Right (Trim Right)", () => SplitRightSelectedClip()));

            panel.Controls.Add(new Label { Height = 1, Width = 230, BackColor = Color.FromArgb(60, 60, 60), Margin = new Padding(0, 10, 0, 10) });

            // --- TEXT OVERLAY TOOLKIT ---
            panel.Controls.Add(CreateSectionHeader("Text Overlays"));

            panel.Controls.Add(CreateStyledButton("➕ Add Text Layer", () =>
            {
                var newLabel = new TextLabel
                {
                    Content = "Type text here...",
                    X = 100,
                    Y = 200,
                    Width = 350,
                    Height = 90,
                    FontSize = (float)numFontSize.Value,
                };

                var textMediaItem = new MediaItem
                {
                    Type = MediaType.Text,
                    StartTime = timelineControl.CurrentTime,
                    Duration = 3.0,
                    TextData = newLabel
                };

                mediaItems.Add(textMediaItem);
                mediaListBox.Items.Add($"Text: {newLabel.Content}");
                previewControl.SelectedTextLabel = newLabel;
                RefreshTimeline();
            }));

            panel.Controls.Add(CreateSubLabel("Font Size:"));
            numFontSize = new NumericUpDown { Width = 230, Minimum = 10, Maximum = 120, Value = 32, BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White };
            numFontSize.ValueChanged += (s, e) => { if (!isBindingUI && previewControl.SelectedTextLabel != null) { previewControl.SelectedTextLabel.FontSize = (float)numFontSize.Value; previewControl.Invalidate(); } };
            panel.Controls.Add(numFontSize);

            var colorFlow = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, Width = 230, Height = 40, WrapContents = false };
            btnTextColor = CreateMiniColorButton("Text Color", Color.White, c => { if (previewControl.SelectedTextLabel != null) { previewControl.SelectedTextLabel.TextColor = c; previewControl.Invalidate(); } });
            btnBgColor = CreateMiniColorButton("Bg Color", Color.FromArgb(128, 0, 0, 0), c => { if (previewControl.SelectedTextLabel != null) { previewControl.SelectedTextLabel.BackgroundColor = c; previewControl.Invalidate(); } });
            colorFlow.Controls.Add(btnTextColor);
            colorFlow.Controls.Add(btnBgColor);
            panel.Controls.Add(colorFlow);

            panel.Controls.Add(CreateSubLabel("Box Boundaries (Width / Height):"));
            var sizeFlow = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, Width = 230, Height = 35, WrapContents = false };
            numBoxWidth = new NumericUpDown { Width = 110, Minimum = 50, Maximum = 1080, Value = 350, BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White };
            numBoxHeight = new NumericUpDown { Width = 110, Minimum = 30, Maximum = 1920, Value = 90, BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White };

            numBoxWidth.ValueChanged += (s, e) => { if (!isBindingUI && previewControl.SelectedTextLabel != null) { previewControl.SelectedTextLabel.Width = (float)numBoxWidth.Value; previewControl.Invalidate(); } };
            numBoxHeight.ValueChanged += (s, e) => { if (!isBindingUI && previewControl.SelectedTextLabel != null) { previewControl.SelectedTextLabel.Height = (float)numBoxHeight.Value; previewControl.Invalidate(); } };

            sizeFlow.Controls.Add(numBoxWidth);
            sizeFlow.Controls.Add(numBoxHeight);
            panel.Controls.Add(sizeFlow);

            panel.Controls.Add(new Label { Height = 1, Width = 230, BackColor = Color.FromArgb(60, 60, 60), Margin = new Padding(0, 10, 0, 10) });

            // --- ANIMATIONS & TIMING ---
            panel.Controls.Add(CreateSectionHeader("Clip Animations"));
            panel.Controls.Add(CreateSubLabel("Total Clip Duration (s)"));
            numDuration = CreateNumberInput();
            numDuration.ValueChanged += NumDuration_ValueChanged;
            panel.Controls.Add(numDuration);

            panel.Controls.Add(CreateSubLabel("In Animation (Entrance)"));
            cbInEffect = CreateEffectDropdown();
            cbInEffect.SelectedIndexChanged += (s, e) => SaveAnimationSettings();
            panel.Controls.Add(cbInEffect);

            panel.Controls.Add(CreateSubLabel("In Duration (s)"));
            numInDuration = CreateNumberInput();
            numInDuration.ValueChanged += (s, e) => SaveAnimationSettings();
            panel.Controls.Add(numInDuration);

            panel.Controls.Add(CreateSubLabel("Out Animation (Exit)"));
            cbOutEffect = CreateEffectDropdown();
            cbOutEffect.SelectedIndexChanged += (s, e) => SaveAnimationSettings();
            panel.Controls.Add(cbOutEffect);

            panel.Controls.Add(CreateSubLabel("Out Duration (s)"));
            numOutDuration = CreateNumberInput();
            numOutDuration.ValueChanged += (s, e) => SaveAnimationSettings();
            panel.Controls.Add(numOutDuration);

            return panel;
        }

        private Button CreateMiniColorButton(string title, Color initialColor, Action<Color> onColorPicked)
        {
            var btn = new Button
            {
                Text = title,
                Width = 110,
                Height = 28,
                BackColor = Color.FromArgb(55, 55, 55),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8)
            };
            btn.Click += (s, e) =>
            {
                using (var cd = new ColorDialog { Color = initialColor })
                {
                    if (cd.ShowDialog() == DialogResult.OK)
                    {
                        onColorPicked?.Invoke(cd.Color);
                    }
                }
            };
            return btn;
        }

        private void BindTextLabelToUI(TextLabel label)
        {
            if (label == null) return;
            isBindingUI = true;
            numFontSize.Value = (decimal)Math.Clamp(label.FontSize, 10, 120);
            numBoxWidth.Value = (decimal)Math.Clamp(label.Width, 50, 1080);
            numBoxHeight.Value = (decimal)Math.Clamp(label.Height, 30, 1920);
            isBindingUI = false;
        }

        private float[] ExtractAudioPeaks(string filePath, int targetPeakCount = 1000)
        {
            var peaks = new List<float>();
            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (var reader = new BinaryReader(fs))
                {
                    if (fs.Length > 10)
                    {
                        char[] id3Check = reader.ReadChars(3);
                        if (new string(id3Check) == "ID3")
                        {
                            reader.ReadBytes(3);
                            byte b1 = reader.ReadByte();
                            byte b2 = reader.ReadByte();
                            byte b3 = reader.ReadByte();
                            byte b4 = reader.ReadByte();
                            int tagSize = (b1 << 21) | (b2 << 14) | (b3 << 7) | b4;
                            fs.Seek(tagSize + 10, SeekOrigin.Begin);
                        }
                        else
                        {
                            fs.Seek(0, SeekOrigin.Begin);
                        }
                    }

                    string riff = new string(reader.ReadChars(4));
                    if (riff != "RIFF") throw new FormatException("Not a valid RIFF file");
                    reader.ReadInt32();
                    string wave = new string(reader.ReadChars(4));
                    if (wave != "WAVE") throw new FormatException("Not a valid WAVE file");

                    int channels = 1;
                    int bitsPerSample = 16;
                    byte[] sampleData = null;

                    while (fs.Position < fs.Length - 8)
                    {
                        string chunkId = new string(reader.ReadChars(4));
                        int chunkSize = reader.ReadInt32();

                        if (chunkId == "fmt ")
                        {
                            reader.ReadInt16();
                            channels = reader.ReadInt16();
                            reader.ReadInt32();
                            reader.ReadInt32();
                            reader.ReadInt16();
                            bitsPerSample = reader.ReadInt16();
                            if (chunkSize > 16)
                            {
                                reader.ReadBytes(chunkSize - 16);
                            }
                        }
                        else if (chunkId == "data")
                        {
                            sampleData = reader.ReadBytes(chunkSize);
                            break;
                        }
                        else
                        {
                            if (chunkSize <= 0 || fs.Position + chunkSize > fs.Length) break;
                            reader.ReadBytes(chunkSize);
                        }
                    }

                    if (sampleData != null && sampleData.Length > 0)
                    {
                        int bytesPerSample = (bitsPerSample / 8) * channels;
                        if (bytesPerSample <= 0) bytesPerSample = 2;
                        int totalSamples = sampleData.Length / bytesPerSample;
                        int samplesPerPeak = Math.Max(1, totalSamples / targetPeakCount);

                        for (int i = 0; i < totalSamples; i += samplesPerPeak)
                        {
                            float maxPeak = 0;
                            for (int j = 0; j < samplesPerPeak && (i + j) * bytesPerSample + bytesPerSample <= sampleData.Length; j++)
                            {
                                int sampleIndex = (i + j) * bytesPerSample;
                                float absSample = 0;

                                if (bitsPerSample == 16)
                                {
                                    short sample = BitConverter.ToInt16(sampleData, sampleIndex);
                                    absSample = Math.Abs(sample / 32768f);
                                }
                                else if (bitsPerSample == 8)
                                {
                                    byte sample = sampleData[sampleIndex];
                                    absSample = Math.Abs((sample - 128) / 128f);
                                }
                                else if (bitsPerSample == 32)
                                {
                                    float sample = BitConverter.ToSingle(sampleData, sampleIndex);
                                    absSample = Math.Abs(sample);
                                }

                                if (absSample > maxPeak) maxPeak = absSample;
                            }
                            peaks.Add(Math.Min(maxPeak, 1.0f));
                        }
                    }
                }
            }
            catch
            {
                int seed = filePath.GetHashCode();
                var rng = new Random(seed);
                float currentVal = 0.3f;
                for (int i = 0; i < targetPeakCount; i++)
                {
                    currentVal += (float)((rng.NextDouble() - 0.5) * 0.2);
                    currentVal = Math.Clamp(currentVal, 0.05f, 0.95f);
                    peaks.Add(currentVal);
                }
            }

            if (peaks.Count == 0)
            {
                for (int i = 0; i < targetPeakCount; i++) peaks.Add(0.2f);
            }

            return peaks.ToArray();
        }

        private void UpdateSplitEffectDurations(MediaItem item)
        {
            if (item == null || item.Type != MediaType.Image) return;

            double halfDuration = Math.Round(item.Duration / 2.0, 2);

            if (item.InEffect != null) item.InEffect.Duration = halfDuration;
            if (item.OutEffect != null) item.OutEffect.Duration = halfDuration;
        }

        private void NumDuration_ValueChanged(object sender, EventArgs e)
        {
            if (isBindingUI) return;

            var item = timelineControl.SelectedItem;
            if (item != null)
            {
                item.Duration = (double)numDuration.Value;
                if (item.Type == MediaType.Image)
                {
                    UpdateSplitEffectDurations(item);

                    isBindingUI = true;
                    if (numInDuration != null) numInDuration.Value = (decimal)item.InEffect.Duration;
                    if (numOutDuration != null) numOutDuration.Value = (decimal)item.OutEffect.Duration;
                    isBindingUI = false;
                }

                RefreshTimeline();
            }
        }

        private void SaveAnimationSettings()
        {
            if (isBindingUI) return;

            var item = timelineControl.SelectedItem;
            if (item != null && item.Type == MediaType.Image)
            {
                if (item.InEffect == null) item.InEffect = new TransitionEffect();
                if (item.OutEffect == null) item.OutEffect = new TransitionEffect();

                item.InEffect.Type = cbInEffect.SelectedItem?.ToString() ?? "None";
                item.OutEffect.Type = cbOutEffect.SelectedItem?.ToString() ?? "None";

                if (numInDuration != null) item.InEffect.Duration = (double)numInDuration.Value;
                if (numOutDuration != null) item.OutEffect.Duration = (double)numOutDuration.Value;

                RefreshTimeline();
            }
        }

        private void AddMediaItem(string filePath, MediaType type)
        {
            double duration = 4.0;
            double nextStartTime = 0;
            float[] audioPeaks = null;

            if (type == MediaType.Audio)
            {
                duration = GetAudioDuration(filePath);
                audioPeaks = ExtractAudioPeaks(filePath);
            }
            else if (type == MediaType.Image)
            {
                var lastImage = mediaItems.Where(x => x.Type == MediaType.Image).LastOrDefault();
                if (lastImage != null) nextStartTime = lastImage.StartTime + lastImage.Duration;
            }

            double halfDuration = duration / 2.0;

            var item = new MediaItem
            {
                FilePath = filePath,
                Type = type,
                Duration = duration,
                OriginalDuration = duration,
                SourceOffset = 0,
                StartTime = nextStartTime,
                AudioPeaks = audioPeaks,
                InEffect = new TransitionEffect { Type = "DynamicZoomBlur", Duration = halfDuration },
                OutEffect = new TransitionEffect { Type = "DynamicZoomBlur", Duration = halfDuration }
            };

            mediaItems.Add(item);
            mediaListBox.Items.Add(Path.GetFileName(filePath));
            RefreshTimeline();
        }

        private Label CreateSectionHeader(string text) => new Label { Text = text, ForeColor = Color.Yellow, Font = new Font("Segoe UI", 9, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 8, 0, 3) };
        private Label CreateSubLabel(string text) => new Label { Text = text, ForeColor = Color.LightGray, Font = new Font("Segoe UI", 8), AutoSize = true, Margin = new Padding(0, 4, 0, 2) };

        private ComboBox CreateEffectDropdown()
        {
            var cb = new ComboBox
            {
                Width = 230,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(55, 55, 55),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            cb.Items.AddRange(new object[] {
                "None", "Fade", "Slide", "Wave", "Zoom", "ZoomBlur",
                "ZoomBlurUp", "ZoomBlurDown", "ZoomBlurLeft", "ZoomBlurRight", "DynamicZoomBlur"
            });

            cb.SelectedIndex = 0;
            return cb;
        }

        private NumericUpDown CreateNumberInput()
        {
            return new NumericUpDown
            {
                Width = 230,
                Minimum = 0.0m,
                Maximum = 60.0m,
                DecimalPlaces = 2,
                Increment = 0.1m,
                BackColor = Color.FromArgb(55, 55, 55),
                ForeColor = Color.White
            };
        }

        private void TimelineControl_ItemResized(object sender, MediaItem item)
        {
            if (item != null)
            {
                if (item.Type == MediaType.Image)
                {
                    UpdateSplitEffectDurations(item);
                }

                if (timelineControl.SelectedItem == item)
                {
                    isBindingUI = true;

                    if (numDuration != null)
                        numDuration.Value = (decimal)Math.Clamp(item.Duration, (double)numDuration.Minimum, (double)numDuration.Maximum);

                    if (numInDuration != null && item.InEffect != null)
                        numInDuration.Value = (decimal)Math.Clamp(item.InEffect.Duration, (double)numInDuration.Minimum, (double)numInDuration.Maximum);

                    if (numOutDuration != null && item.OutEffect != null)
                        numOutDuration.Value = (decimal)Math.Clamp(item.OutEffect.Duration, (double)numOutDuration.Minimum, (double)numOutDuration.Maximum);

                    isBindingUI = false;
                }

                RefreshTimeline();
            }
        }

        private void BindSelectedMediaToUI(MediaItem item)
        {
            if (item == null) return;

            isBindingUI = true;

            if (numDuration != null)
                numDuration.Value = (decimal)Math.Clamp(item.Duration, (double)numDuration.Minimum, (double)numDuration.Maximum);

            if (item.Type == MediaType.Image)
            {
                if (cbInEffect != null) cbInEffect.SelectedItem = item.InEffect?.Type ?? "None";
                if (cbOutEffect != null) cbOutEffect.SelectedItem = item.OutEffect?.Type ?? "None";

                UpdateSplitEffectDurations(item);

                double inDur = item.InEffect?.Duration ?? 0;
                double outDur = item.OutEffect?.Duration ?? 0;

                if (numInDuration != null)
                    numInDuration.Value = (decimal)Math.Clamp(inDur, (double)numInDuration.Minimum, (double)numInDuration.Maximum);

                if (numOutDuration != null)
                    numOutDuration.Value = (decimal)Math.Clamp(outDur, (double)numOutDuration.Minimum, (double)numOutDuration.Maximum);
            }

            isBindingUI = false;
        }

        private Button CreateActionButton(string text, Action onClick)
        {
            var btn = new Button
            {
                Text = text,
                Width = 230,
                Height = 32,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                Margin = new Padding(0, 0, 0, 5),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular)
            };
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
            btn.Click += (s, e) => onClick?.Invoke();
            return btn;
        }

        private void SplitSelectedClip()
        {
            var item = timelineControl.SelectedItem;
            double playhead = timelineControl.CurrentTime;

            if (item != null && playhead > item.StartTime && playhead < (item.StartTime + item.Duration))
            {
                double splitPointRelative = playhead - item.StartTime;
                double originalDuration = item.Duration;

                item.Duration = splitPointRelative;
                if (item.Type == MediaType.Image) UpdateSplitEffectDurations(item);

                var newItem = new MediaItem
                {
                    FilePath = item.FilePath,
                    Type = item.Type,
                    StartTime = playhead,
                    Duration = originalDuration - splitPointRelative,
                    InEffect = item.InEffect != null ? new TransitionEffect { Type = item.InEffect.Type } : null,
                    OutEffect = item.OutEffect != null ? new TransitionEffect { Type = item.OutEffect.Type } : null,
                    TextData = item.TextData != null ? new TextLabel { Content = item.TextData.Content, X = item.TextData.X, Y = item.TextData.Y, Width = item.TextData.Width, Height = item.TextData.Height, FontSize = item.TextData.FontSize, TextColor = item.TextData.TextColor, BackgroundColor = item.TextData.BackgroundColor } : null
                };

                if (newItem.Type == MediaType.Image) UpdateSplitEffectDurations(newItem);

                mediaItems.Add(newItem);
                mediaListBox.Items.Add(Path.GetFileName(newItem.FilePath) ?? "Text Layer");
                RefreshTimeline();
            }
        }

        private void SplitLeftSelectedClip()
        {
            var item = timelineControl.SelectedItem;
            double playhead = timelineControl.CurrentTime;

            if (item != null && playhead > item.StartTime && playhead < (item.StartTime + item.Duration))
            {
                double cutAmount = playhead - item.StartTime;

                if (item.OriginalDuration <= 0) item.OriginalDuration = item.Duration;

                item.SourceOffset += cutAmount;
                item.StartTime = playhead;
                item.Duration -= cutAmount;

                RefreshTimeline();
            }
        }

        private void SplitRightSelectedClip()
        {
            var item = timelineControl.SelectedItem;
            double playhead = timelineControl.CurrentTime;

            if (item != null && playhead > item.StartTime && playhead < (item.StartTime + item.Duration))
            {
                if (item.OriginalDuration <= 0) item.OriginalDuration = item.Duration;

                item.Duration = playhead - item.StartTime;

                RefreshTimeline();
            }
        }

        private void RefreshTimeline()
        {
            timelineControl.Invalidate();
            previewControl.RenderFrame(mediaItems, timelineControl.CurrentTime);
        }


        private Control CreateToolbar()
        {
            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(35, 35, 35),
                Padding = new Padding(10)
            };

            toolbar.Controls.Add(CreateStyledButton("📁 Import Files", () => ImportFiles()));

            btnPlayPause = CreateStyledButton("▶ Play", () => TogglePlayback());
            toolbar.Controls.Add(btnPlayPause);

            toolbar.Controls.Add(CreateStyledButton("🗑 Delete Selected", () => DeleteSelectedMedia()));

            toolbar.Controls.Add(CreateStyledButton("Export Video", async () =>
            {
                PausePlayback();

                using (var sfd = new SaveFileDialog { Filter = "MP4 Video|*.mp4", DefaultExt = ".mp4" })
                {
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        using (var progressForm = new ProgressForm())
                        {
                            var progress = new Progress<int>(percent =>
                            {
                                progressForm.UpdateProgress(percent, $"Rendering & encoding video... {percent}%");
                            });

                            progressForm.Show(this);
                            this.Enabled = false;

                            try
                            {
                                await exportService.ExportToVideo(mediaItems, sfd.FileName, (time, g) =>
                                {
                                    using (var exportedFrame = VideoRenderHelper.RenderExportFrame(mediaItems, time, 1080, 1920))
                                    {
                                        g.DrawImage(exportedFrame, 0, 0, 1080, 1920);
                                    }
                                }, progress);

                                progressForm.Close();
                                this.Enabled = true;

                                MessageBox.Show(this, "Video exported successfully!", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            catch (Exception ex)
                            {
                                progressForm.Close();
                                this.Enabled = true;

                                MessageBox.Show(this, ex.Message, "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }
                }
            }));
            return toolbar;
        }
        public void RenderPreviewAtTime(double timePosition, Graphics g, Size canvasSize)
        {
            g.Clear(Color.Black);

            float targetAspect = 9.0f / 16.0f;
            int canvasWidth = canvasSize.Width;
            int canvasHeight = canvasSize.Height;

            if ((float)canvasWidth / canvasHeight > targetAspect)
            {
                canvasWidth = (int)(canvasHeight * targetAspect);
            }
            else
            {
                canvasHeight = (int)(canvasWidth / targetAspect);
            }

            int canvasX = (canvasSize.Width - canvasWidth) / 2;
            int canvasY = (canvasSize.Height - canvasHeight) / 2;

            g.SetClip(new Rectangle(canvasX, canvasY, canvasWidth, canvasHeight));

            // 1. Render Active Image Frame
            var activeImage = mediaItems.FirstOrDefault(item => item.Type == MediaType.Image &&
                                                                timePosition >= item.StartTime &&
                                                                timePosition < item.StartTime + item.Duration);

            if (activeImage != null && File.Exists(activeImage.FilePath))
            {
                using (var img = Image.FromFile(activeImage.FilePath))
                {
                    float scale = Math.Max((float)canvasWidth / img.Width, (float)canvasHeight / img.Height) * activeImage.Scale;
                    int baseW = (int)(img.Width * scale);
                    int baseH = (int)(img.Height * scale);

                    int originX = canvasX + (canvasWidth - baseW) / 2 + (int)activeImage.PositionX;
                    int originY = canvasY + (canvasHeight - baseH) / 2 + (int)activeImage.PositionY;

                    g.DrawImage(img, originX, originY, baseW, baseH);
                }
            }

            // 2. Render Active Separate Text Layers on top
            var activeTexts = mediaItems.Where(item => item.Type == MediaType.Text &&
                                                        item.TextData != null &&
                                                        timePosition >= item.StartTime &&
                                                        timePosition < item.StartTime + item.Duration);

            foreach (var textItem in activeTexts)
            {
                var label = textItem.TextData;
                float drawX = canvasX + label.X;
                float drawY = canvasY + label.Y;
                var rect = new RectangleF(drawX, drawY, Math.Max(label.Width, 50), Math.Max(label.Height, 30));

                using (var bgBrush = new SolidBrush(label.BackgroundColor))
                {
                    g.FillRectangle(bgBrush, rect);
                }

                using (var font = new Font(label.FontFamily, label.FontSize, label.IsBold ? FontStyle.Bold : FontStyle.Regular))
                using (var textBrush = new SolidBrush(label.TextColor))
                {
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center,
                        Trimming = StringTrimming.Word
                    };
                    g.DrawString(label.Content, font, textBrush, rect, sf);
                }
            }

            g.ResetClip();
        }

        private void ImportFiles()
        {
            using (var ofd = new OpenFileDialog
            {
                Filter = "Supported Media Files|*.jpg;*.jpeg;*.png;*.bmp;*.mp3;*.wav;*.m4a|Image Files|*.jpg;*.jpeg;*.png;*.bmp|Audio Files|*.mp3;*.wav;*.m4a",
                Multiselect = true
            })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    foreach (var file in ofd.FileNames)
                    {
                        string ext = Path.GetExtension(file).ToLower();

                        if (new[] { ".jpg", ".jpeg", ".png", ".bmp" }.Contains(ext))
                        {
                            AddMediaItem(file, MediaType.Image);
                        }
                        else if (new[] { ".mp3", ".wav", ".m4a" }.Contains(ext))
                        {
                            AddMediaItem(file, MediaType.Audio);
                        }
                    }
                }
            }
        }

        private void TogglePlayback()
        {
            if (isPlaying) PausePlayback();
            else StartPlayback();
        }

        private Control CreateMediaPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(35, 35, 35) };
            mediaListBox = new ListBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White };
            panel.Controls.Add(mediaListBox);
            return panel;
        }

        private double GetAudioDuration(string filePath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "ffmpeg.exe",
                    Arguments = $"-i \"{filePath}\"",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    string output = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    var match = System.Text.RegularExpressions.Regex.Match(output, @"Duration:\s*(\d+):(\d+):(\d+\.\d+)");
                    if (match.Success)
                    {
                        int hours = int.Parse(match.Groups[1].Value);
                        int minutes = int.Parse(match.Groups[2].Value);
                        double seconds = double.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);

                        double totalSeconds = (hours * 3600) + (minutes * 60) + seconds;
                        if (totalSeconds > 0) return totalSeconds;
                    }
                }
            }
            catch { }

            return 120.0;
        }

        private void DeleteSelectedMedia()
        {
            var itemToDelete = timelineControl.SelectedItem;
            if (itemToDelete != null)
            {
                mediaItems.Remove(itemToDelete);
                mediaListBox.Items.Remove(Path.GetFileName(itemToDelete.FilePath) ?? "Text Layer");
                timelineControl.Invalidate();
                previewControl.RenderFrame(mediaItems, timelineControl.CurrentTime);
            }
            else if (mediaListBox.SelectedIndex >= 0)
            {
                int idx = mediaListBox.SelectedIndex;
                mediaItems.RemoveAt(idx);
                mediaListBox.Items.RemoveAt(idx);
                timelineControl.Invalidate();
                previewControl.RenderFrame(mediaItems, timelineControl.CurrentTime);
            }
        }

        private Button CreateStyledButton(string text, Action onClick)
        {
            var btn = new Button
            {
                Text = text,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Width = 230,
                Height = 32,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 5)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (s, e) => onClick?.Invoke();
            return btn;
        }
    }

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
            this.BackColor = Color.FromArgb(35, 35, 35);

            lblStatus = new Label
            {
                Text = "Rendering video...",
                ForeColor = Color.White,
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
    }
}