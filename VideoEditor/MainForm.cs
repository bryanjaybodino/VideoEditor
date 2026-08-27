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

        // Debouncing timer to smooth out scrubbing audio updates
        private Timer scrubAudioTimer;
        private double pendingScrubTime = -1;

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
            playbackTimer = new Timer { Interval = 33 }; // ~30 FPS
            playbackTimer.Tick += (s, e) =>
            {
                // Advance frame counter naturally during normal playback
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

            // Throttles manual audio seeks to prevent driver flickering
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
            this.Text = "VideoEditor - Slideshow Video Maker";
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
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

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

            var rightPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(35, 35, 35) };
            mainLayout.Controls.Add(rightPanel, 2, 1);

            timelineControl = new TimelineControl(mediaItems) { Dock = DockStyle.Fill };

            // Handle scrubbing safely
            timelineControl.TimeChanged += (time) =>
            {
                previewControl.RenderFrame(mediaItems, time);

                // Only perform audio seeks when the position change was triggered by the user (scrubbing/clicking)
                if (!isPlaying || isUserScrubbing)
                {
                    pendingScrubTime = time;
                    scrubAudioTimer.Stop();
                    scrubAudioTimer.Start();
                }
            };

            // Detect mouse interaction on timeline control to safely isolate manual scrubbing
            timelineControl.MouseDown += (s, e) => { isUserScrubbing = true; };
            timelineControl.MouseUp += (s, e) =>
            {
                isUserScrubbing = false;
                if (isPlaying)
                {
                    // Resume smooth playback immediately after scrubbing release
                    StartPlayback();
                }
            };

            mainLayout.Controls.Add(timelineControl, 0, 2);
            mainLayout.SetColumnSpan(timelineControl, 3);

            this.Controls.Add(mainLayout);
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

            toolbar.Controls.Add(CreateStyledButton("Add Images", () =>
            {
                using (var ofd = new OpenFileDialog { Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp", Multiselect = true })
                {
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        foreach (var file in ofd.FileNames) AddMediaItem(file, MediaType.Image);
                    }
                }
            }));

            toolbar.Controls.Add(CreateStyledButton("Add Audio", () =>
            {
                using (var ofd = new OpenFileDialog { Filter = "Audio Files|*.mp3;*.wav;*.m4a" })
                {
                    if (ofd.ShowDialog() == DialogResult.OK) AddMediaItem(ofd.FileName, MediaType.Audio);
                }
            }));

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

        private void AddMediaItem(string filePath, MediaType type)
        {
            double duration = 3.0;
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

            var item = new MediaItem
            {
                FilePath = filePath,
                Type = type,
                Duration = duration,
                StartTime = nextStartTime,
                InEffect = new TransitionEffect { Type = mediaItems.Count % 2 == 0 ? "Slide" : "Wave", Duration = 0.5 }
            };

            mediaItems.Add(item);
            mediaListBox.Items.Add(Path.GetFileName(filePath));
            timelineControl.Invalidate();
            previewControl.RenderFrame(mediaItems, timelineControl.CurrentTime);
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