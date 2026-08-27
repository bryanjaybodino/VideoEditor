using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using VideoEditor.Controls;
using VideoEditor.Models;
using VideoEditor.Services;
using Timer = System.Windows.Forms.Timer;

namespace VideoEditor
{
    public partial class MainForm : Form
    {
        [DllImport("winmm.dll", CharSet = CharSet.Auto)]
        private static extern int mciSendString(string command, StringBuilder buffer, int bufferSize, IntPtr hwndCallback);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern uint GetShortPathName(string lpszLongPath, StringBuilder lpszShortPath, uint cchBuffer);

        private List<MediaItem> mediaItems = new List<MediaItem>();
        private PreviewControl previewControl;
        private TimelineControl timelineControl;
        private VideoExportService exportService;
        private AudioCaptionService captionService;
        private ListBox mediaListBox;

        private Timer playbackTimer;
        private bool isPlaying = false;
        private bool isUserScrubbing = false;
        private Button btnPlayPause;
        private string currentAudioPath = null;

        private Timer scrubAudioTimer;
        private double pendingScrubTime = -1;

        // Animation UI Controls
        private NumericUpDown numDuration;
        private ComboBox cbInEffect;
        private NumericUpDown numInDuration;
        private ComboBox cbOutEffect;
        private NumericUpDown numOutDuration;
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
            playbackTimer = new Timer { Interval = 33 };
            playbackTimer.Tick += (s, e) =>
            {
                timelineControl.CurrentTime += 0.033;

                var audioItem = mediaItems.FirstOrDefault(x => x.Type == MediaType.Audio);
                if (audioItem != null)
                {
                    double time = timelineControl.CurrentTime;
                    if (time < audioItem.StartTime || time >= (audioItem.StartTime + audioItem.Duration))
                    {
                        mciSendString("pause audioTrack", null, 0, IntPtr.Zero);
                    }
                }

                if (timelineControl.CurrentTime >= timelineControl.GetTotalDuration())
                {
                    PausePlayback();
                    timelineControl.CurrentTime = 0;
                }
            };

            scrubAudioTimer = new Timer { Interval = 60 };
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

            // Mobile-Optimized Grid Dimensions (20% Left, 55% Center Canvas, 25% Right Controls)
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

                if (!isPlaying || isUserScrubbing)
                {
                    pendingScrubTime = time;
                    scrubAudioTimer.Stop();
                    scrubAudioTimer.Start();
                }
            };

            // Sync Timeline item click event with Sidebar Controls
            timelineControl.ClipSelected += (selectedItem) =>
            {
                BindSelectedMediaToUI(selectedItem);
            };

            // Hook up live drag-resizing event from TimelineControl
            timelineControl.ItemResized += (resizedItem) =>
            {
                TimelineControl_ItemResized(timelineControl, resizedItem);
            };

            timelineControl.MouseDown += (s, e) => { isUserScrubbing = true; };
            timelineControl.MouseUp += (s, e) =>
            {
                isUserScrubbing = false;
                if (isPlaying) StartPlayback();
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
                BackColor = Color.FromArgb(35, 35, 35),
                Padding = new Padding(15),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true
            };

            panel.Controls.Add(new Label
            {
                Text = "Editing Actions",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 10)
            });

            // Split Controls
            panel.Controls.Add(CreateActionButton("✂ Split Clip", () => SplitSelectedClip()));
            panel.Controls.Add(CreateActionButton("✂◄ Split Left (Trim Left)", () => SplitLeftSelectedClip()));
            panel.Controls.Add(CreateActionButton("►✂ Split Right (Trim Right)", () => SplitRightSelectedClip()));

            panel.Controls.Add(new Label { Height = 1, Width = 220, BackColor = Color.Gray, Margin = new Padding(0, 10, 0, 10) });

            // Total Image Duration
            panel.Controls.Add(CreateSectionHeader("Total Clip Duration (s)"));
            numDuration = CreateNumberInput();
            numDuration.ValueChanged += NumDuration_ValueChanged;
            panel.Controls.Add(numDuration);

            panel.Controls.Add(new Label { Height = 1, Width = 220, BackColor = Color.Gray, Margin = new Padding(0, 10, 0, 10) });

            // IN (Entrance) Animation
            panel.Controls.Add(CreateSectionHeader("In Animation (Entrance)"));
            cbInEffect = CreateEffectDropdown();
            cbInEffect.SelectedIndexChanged += (s, e) => SaveAnimationSettings();
            panel.Controls.Add(cbInEffect);

            panel.Controls.Add(CreateSubLabel("In Duration (s):"));
            numInDuration = CreateNumberInput();
            numInDuration.ReadOnly = true; // Auto-calculated (50% Split)
            panel.Controls.Add(numInDuration);

            // OUT (Exit) Animation
            panel.Controls.Add(CreateSectionHeader("Out Animation (Exit)"));
            cbOutEffect = CreateEffectDropdown();
            cbOutEffect.SelectedIndexChanged += (s, e) => SaveAnimationSettings();
            panel.Controls.Add(cbOutEffect);

            panel.Controls.Add(CreateSubLabel("Out Duration (s):"));
            numOutDuration = CreateNumberInput();
            numOutDuration.ReadOnly = true; // Auto-calculated (50% Split)
            panel.Controls.Add(numOutDuration);

            return panel;
        }

        private void BindSelectedMediaToUI(MediaItem item)
        {
            if (item == null || item.Type != MediaType.Image) return;

            isBindingUI = true;

            // Load clip duration
            numDuration.Value = (decimal)item.Duration;

            // Set drop-downs
            cbInEffect.SelectedItem = item.InEffect?.Type ?? "None";
            cbOutEffect.SelectedItem = item.OutEffect?.Type ?? "None";

            // Re-calculate 50/50 split on selection
            UpdateSplitEffectDurations(item);

            numInDuration.Value = (decimal)(item.InEffect?.Duration ?? 0);
            numOutDuration.Value = (decimal)(item.OutEffect?.Duration ?? 0);

            isBindingUI = false;
        }

        // Helper method: Splits image duration 50/50 and assigns it to In and Out effects
        private void UpdateSplitEffectDurations(MediaItem item)
        {
            if (item == null || item.Type != MediaType.Image) return;

            double halfDuration = Math.Round(item.Duration / 2.0, 2);

            if (item.InEffect != null)
            {
                item.InEffect.Duration = halfDuration;
            }

            if (item.OutEffect != null)
            {
                item.OutEffect.Duration = halfDuration;
            }
        }

        // Triggered when editing the numerical input
        private void NumDuration_ValueChanged(object sender, EventArgs e)
        {
            if (isBindingUI) return;

            var item = timelineControl.SelectedItem;
            if (item != null && item.Type == MediaType.Image)
            {
                item.Duration = (double)numDuration.Value;

                UpdateSplitEffectDurations(item);

                isBindingUI = true;
                numInDuration.Value = (decimal)item.InEffect.Duration;
                numOutDuration.Value = (decimal)item.OutEffect.Duration;
                isBindingUI = false;

                RefreshTimeline();
            }
        }

        // Triggered dynamically during timeline clip edge dragging
        private void TimelineControl_ItemResized(object sender, MediaItem item)
        {
            if (item != null && item.Type == MediaType.Image)
            {
                UpdateSplitEffectDurations(item);

                if (timelineControl.SelectedItem == item)
                {
                    isBindingUI = true;
                    numDuration.Value = (decimal)item.Duration;
                    numInDuration.Value = (decimal)item.InEffect.Duration;
                    numOutDuration.Value = (decimal)item.OutEffect.Duration;
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
                item.InEffect.Type = cbInEffect.SelectedItem?.ToString() ?? "None";
                item.OutEffect.Type = cbOutEffect.SelectedItem?.ToString() ?? "None";

                UpdateSplitEffectDurations(item);

                isBindingUI = true;
                numInDuration.Value = (decimal)item.InEffect.Duration;
                numOutDuration.Value = (decimal)item.OutEffect.Duration;
                isBindingUI = false;

                RefreshTimeline();
            }
        }

        private void AddMediaItem(string filePath, MediaType type)
        {
            double duration = 4.0;
            double nextStartTime = 0;

            if (type == MediaType.Audio)
            {
                duration = GetAudioDuration(filePath);
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
                StartTime = nextStartTime,
                InEffect = new TransitionEffect { Type = "ZoomBlurUp", Duration = halfDuration },
                OutEffect = new TransitionEffect { Type = "ZoomBlurDown", Duration = halfDuration }
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
                Width = 220,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(55, 55, 55),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            cb.Items.AddRange(new object[] {
                "None",
                "Fade",
                "Slide",
                "Wave",
                "Zoom",
                "ZoomBlur",
                "ZoomBlurUp",
                "ZoomBlurDown",
                "ZoomBlurLeft",
                "ZoomBlurRight"
            });

            cb.SelectedIndex = 0;
            return cb;
        }

        private NumericUpDown CreateNumberInput()
        {
            return new NumericUpDown { Width = 220, Minimum = 0.5m, Maximum = 60.0m, DecimalPlaces = 1, Increment = 0.5m, BackColor = Color.FromArgb(55, 55, 55), ForeColor = Color.White };
        }

        private Button CreateActionButton(string text, Action onClick)
        {
            var btn = new Button
            {
                Text = text,
                Width = 220,
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
                UpdateSplitEffectDurations(item);

                var newItem = new MediaItem
                {
                    FilePath = item.FilePath,
                    Type = item.Type,
                    StartTime = playhead,
                    Duration = originalDuration - splitPointRelative,
                    InEffect = new TransitionEffect { Type = item.InEffect.Type },
                    OutEffect = new TransitionEffect { Type = item.OutEffect.Type }
                };

                UpdateSplitEffectDurations(newItem);

                mediaItems.Add(newItem);
                mediaListBox.Items.Add(Path.GetFileName(newItem.FilePath));
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
                item.StartTime = playhead;
                item.Duration -= cutAmount;
                UpdateSplitEffectDurations(item);
                RefreshTimeline();
            }
        }

        private void SplitRightSelectedClip()
        {
            var item = timelineControl.SelectedItem;
            double playhead = timelineControl.CurrentTime;

            if (item != null && playhead > item.StartTime && playhead < (item.StartTime + item.Duration))
            {
                item.Duration = playhead - item.StartTime;
                UpdateSplitEffectDurations(item);
                RefreshTimeline();
            }
        }

        private void RefreshTimeline()
        {
            timelineControl.Invalidate();
            previewControl.RenderFrame(mediaItems, timelineControl.CurrentTime);
        }

        private string GetShortPath(string path)
        {
            var shortPath = new StringBuilder(255);
            uint result = GetShortPathName(path, shortPath, (uint)shortPath.Capacity);
            return result != 0 ? shortPath.ToString() : path;
        }

        private void EnsureAudioLoaded(string filePath)
        {
            if (currentAudioPath != filePath)
            {
                mciSendString("close audioTrack", null, 0, IntPtr.Zero);
                string safePath = GetShortPath(filePath);
                mciSendString($"open \"{safePath}\" alias audioTrack", null, 0, IntPtr.Zero);
                mciSendString("set audioTrack time format ms", null, 0, IntPtr.Zero);
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
                    int millis = (int)((timePosition - audioItem.StartTime) * 1000);

                    if (isPlaying)
                    {
                        mciSendString($"play audioTrack from {millis}", null, 0, IntPtr.Zero);
                    }
                    else
                    {
                        mciSendString($"seek audioTrack to {millis}", null, 0, IntPtr.Zero);
                    }
                }
                else
                {
                    mciSendString("pause audioTrack", null, 0, IntPtr.Zero);
                }
            }
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
                        this.Enabled = false;
                        try
                        {
                            await exportService.ExportToVideo(mediaItems, sfd.FileName);
                            MessageBox.Show("Video exported successfully!");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message, "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        finally { this.Enabled = true; }
                    }
                }
            }));

            return toolbar;
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

        private void StartPlayback()
        {
            var audioItem = mediaItems.FirstOrDefault(x => x.Type == MediaType.Audio);
            if (audioItem != null && File.Exists(audioItem.FilePath))
            {
                double currentTime = timelineControl.CurrentTime;
                if (currentTime >= audioItem.StartTime && currentTime < (audioItem.StartTime + audioItem.Duration))
                {
                    EnsureAudioLoaded(audioItem.FilePath);
                    int millis = (int)((currentTime - audioItem.StartTime) * 1000);

                    mciSendString($"play audioTrack from {millis}", null, 0, IntPtr.Zero);
                }
            }

            isPlaying = true;
            btnPlayPause.Text = "⏸ Pause";
            playbackTimer.Start();
        }

        private void PausePlayback()
        {
            try { mciSendString("pause audioTrack", null, 0, IntPtr.Zero); } catch { }
            isPlaying = false;
            btnPlayPause.Text = "▶ Play";
            playbackTimer.Stop();
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
                string safePath = GetShortPath(filePath);
                StringBuilder lengthBuf = new StringBuilder(128);

                mciSendString("close tempAudio", null, 0, IntPtr.Zero);
                mciSendString($"open \"{safePath}\" alias tempAudio", null, 0, IntPtr.Zero);
                mciSendString("set tempAudio time format ms", null, 0, IntPtr.Zero);
                mciSendString("status tempAudio length", lengthBuf, lengthBuf.Capacity, IntPtr.Zero);
                mciSendString("close tempAudio", null, 0, IntPtr.Zero);

                if (long.TryParse(lengthBuf.ToString().Trim(), out long lengthInMs) && lengthInMs > 0)
                {
                    return lengthInMs / 1000.0;
                }
            }
            catch { }

            return 60.0;
        }

        private void DeleteSelectedMedia()
        {
            var itemToDelete = timelineControl.SelectedItem;
            if (itemToDelete != null)
            {
                mediaItems.Remove(itemToDelete);
                mediaListBox.Items.Remove(Path.GetFileName(itemToDelete.FilePath));
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
                Width = 120,
                Height = 32,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (s, e) => onClick?.Invoke();
            return btn;
        }
    }
}