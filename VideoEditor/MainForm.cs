using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private List<MediaItem> mediaItems = new List<MediaItem>();
        private VideoExportService exportService;
        private AudioCaptionService captionService;

        private Timer playbackTimer;
        private Stopwatch playbackStopwatch = new Stopwatch();
        private double playbackStartOffset = 0;
        private bool isPlaying = false;
        private bool isUserScrubbing = false;

        private MediaPlayer audioPlayer = new MediaPlayer();
        private string currentAudioPath = null;

        private Timer scrubAudioTimer;
        private double pendingScrubTime = -1;

        private bool isBindingUI = false;
        private UndoRedoManager undoRedoManager = new UndoRedoManager();
        private string userApiKey = string.Empty;
        private MediaItem currentPlayingAudioItem = null;
        public MainForm()
        {
            exportService = new VideoExportService();
            captionService = new AudioCaptionService();

            InitializeComponent();

            timelineControl.SetMediaItems(mediaItems);

            WireUpEvents();
            InitializePlaybackEngine();


            this.WindowState = FormWindowState.Maximized;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Delete)
            {
                DeleteSelectedMedia();
                return true;
            }

            if (keyData == (Keys.Control | Keys.Z))
            {
                Undo();
                return true;
            }

            if (keyData == (Keys.Control | Keys.Y))
            {
                Redo();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void DeleteSelectedMedia()
        {
            var itemToDelete = timelineControl.SelectedItem;
            if (itemToDelete != null)
            {
                var command = new VideoEditor.Commands.DeleteMediaItemCommand(mediaItems, itemToDelete);
                undoRedoManager.ExecuteCommand(command);

                mediaListBox.Items.Remove(Path.GetFileName(itemToDelete.FilePath) ?? "Text Layer");
                RefreshTimeline();
            }
            else if (mediaListBox.SelectedIndex >= 0)
            {
                int idx = mediaListBox.SelectedIndex;
                var item = mediaItems[idx];
                var command = new VideoEditor.Commands.DeleteMediaItemCommand(mediaItems, item);
                undoRedoManager.ExecuteCommand(command);

                mediaListBox.Items.RemoveAt(idx);
                RefreshTimeline();
            }
        }

        private void Undo()
        {
            if (undoRedoManager.CanUndo)
            {
                undoRedoManager.Undo();
                SyncListBox();
                RefreshTimeline();
            }
        }

        private void Redo()
        {
            if (undoRedoManager.CanRedo)
            {
                undoRedoManager.Redo();
                SyncListBox();
                RefreshTimeline();
            }
        }

        private void SyncListBox()
        {
            mediaListBox.Items.Clear();
            foreach (var item in mediaItems)
            {
                mediaListBox.Items.Add(Path.GetFileName(item.FilePath) ?? "Text Layer");
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyDarkModeTheme(this);
        }

        private void ApplyDarkModeTheme(Control parent)
        {
            if (parent == null || !parent.IsHandleCreated) return;

            SetWindowTheme(parent.Handle, "Explorer", null);

            int useDarkMode = 1;
            DwmSetWindowAttribute(parent.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));

            foreach (Control child in parent.Controls)
            {
                ApplyDarkModeTheme(child);
            }
        }

        private void WireUpEvents()
        {
            btnImport.Click += (s, e) => ImportFiles();
            btnPlayPause.Click += (s, e) => TogglePlayback();
            btnDelete.Click += (s, e) => DeleteSelectedMedia();
            btnExport.Click += async (s, e) => await ExportVideoAsync();
            btnAutoCaption.Click += btnAutoCaption_Click;

            btnSplit.Click += (s, e) => SplitSelectedClip();
            btnSplitLeft.Click += (s, e) => SplitLeftSelectedClip();
            btnSplitRight.Click += (s, e) => SplitRightSelectedClip();

            btnAddText.Click += (s, e) =>
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

                // Wrap in Command
                var command = new VideoEditor.Commands.AddMediaItemCommand(mediaItems, textMediaItem);
                undoRedoManager.ExecuteCommand(command);

                SyncListBox();
                previewControl.SelectedTextLabel = newLabel;
                RefreshTimeline();
            };

            // Inside WireUpEvents() in MainForm.cs:
            btnBlurOverlay.Click += (s, e) =>
            {
                float targetWidth = 300f;
                float targetHeight = 300f;
                float targetX = 200f;
                float targetY = 200f;

                int canvasW = previewControl.LastCanvasWidth > 0 ? previewControl.LastCanvasWidth : 1080;
                int canvasH = previewControl.LastCanvasHeight > 0 ? previewControl.LastCanvasHeight : 1920;

                var newBlur = new BlurOverlay
                {
                    X = targetX,
                    Y = targetY,
                    Width = targetWidth,
                    Height = targetHeight,
                    RelativeX = targetX / canvasW,
                    RelativeY = targetY / canvasH,
                    RelativeWidth = targetWidth / canvasW,
                    RelativeHeight = targetHeight / canvasH,
                    BlurRadius = 15
                };

                var blurMediaItem = new MediaItem
                {
                    Type = MediaType.Blur,
                    StartTime = timelineControl.CurrentTime,
                    Duration = 3.0,
                    TrackIndex = 1, // Drag & drop to any track row on timeline
                    BlurData = newBlur
                };

                var command = new VideoEditor.Commands.AddMediaItemCommand(mediaItems, blurMediaItem);
                undoRedoManager.ExecuteCommand(command);

                SyncListBox();
                RefreshTimeline();
            };


            numFontSize.ValueChanged += (s, e) =>
            {
                if (!isBindingUI && previewControl.SelectedTextLabel != null)
                {
                    previewControl.SelectedTextLabel.FontSize = (float)numFontSize.Value;
                    previewControl.Invalidate();
                }
            };

            btnTextColor.Click += (s, e) => PickColor(Color.White, c =>
            {
                if (previewControl.SelectedTextLabel != null)
                {
                    previewControl.SelectedTextLabel.TextColor = c;
                    previewControl.Invalidate();
                }
            });

            btnBgColor.Click += (s, e) => PickColor(Color.FromArgb(128, 0, 0, 0), c =>
            {
                if (previewControl.SelectedTextLabel != null)
                {
                    previewControl.SelectedTextLabel.BackgroundColor = c;
                    previewControl.Invalidate();
                }
            });

            numBoxWidth.ValueChanged += (s, e) =>
            {
                if (!isBindingUI && previewControl.SelectedTextLabel != null)
                {
                    previewControl.SelectedTextLabel.Width = (float)numBoxWidth.Value;
                    previewControl.Invalidate();
                }
            };

            numBoxHeight.ValueChanged += (s, e) =>
            {
                if (!isBindingUI && previewControl.SelectedTextLabel != null)
                {
                    previewControl.SelectedTextLabel.Height = (float)numBoxHeight.Value;
                    previewControl.Invalidate();
                }
            };

            numDuration.ValueChanged += NumDuration_ValueChanged;
            cbInEffect.SelectedIndexChanged += (s, e) => SaveAnimationSettings();
            numInDuration.ValueChanged += (s, e) => SaveAnimationSettings();
            cbOutEffect.SelectedIndexChanged += (s, e) => SaveAnimationSettings();
            numOutDuration.ValueChanged += (s, e) => SaveAnimationSettings();

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

            //previewControl.TransformCompleted += (oldState, newState) =>
            //{
            //    var command = new VideoEditor.Commands.TransformCommand(oldState, newState);
            //    undoRedoManager.ExecuteCommand(command);
            //    RefreshTimeline();
            //};

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
        }
       
        private async void btnAutoCaption_Click(object sender, EventArgs e)
        {
            var audioItem = mediaItems.FirstOrDefault(x => x.Type == MediaType.Audio);
            if (audioItem == null || !File.Exists(audioItem.FilePath))
            {
                MessageBox.Show("Please import an audio file to the timeline first.", "No Audio Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var keyForm = new ApiKeyForm(userApiKey))
            {
                if (keyForm.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                userApiKey = keyForm.ApiKey;
            }

            try
            {
                btnAutoCaption.Enabled = false;
                btnAutoCaption.Text = "Transcribing...";
                Cursor = Cursors.WaitCursor;

                var captions = await captionService.TranscribeAudioWithGemini(audioItem.FilePath, userApiKey);

                if (captions.Count == 0)
                {
                    MessageBox.Show("No speech was detected in the audio file.", "Caption Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                captionService.AddCaptionsToMedia(captions, mediaItems);

                RefreshTimeline();
                previewControl.Invalidate();

                MessageBox.Show($"Successfully added {captions.Count} auto-captions to the timeline!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to generate captions:\n{ex.Message}", "Caption Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnAutoCaption.Enabled = true;
                btnAutoCaption.Text = "Auto Caption";
                Cursor = Cursors.Default;
            }
        }

        private void PickColor(Color initialColor, Action<Color> onColorPicked)
        {
            using (var cd = new ColorDialog { Color = initialColor })
            {
                if (cd.ShowDialog() == DialogResult.OK)
                {
                    onColorPicked?.Invoke(cd.Color);
                }
            }
        }

        private async Task ExportVideoAsync()
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
        }

        private void InitializePlaybackEngine()
        {
            playbackTimer = new Timer { Interval = 15 };
            playbackTimer.Tick += (s, e) =>
            {
                if (!isPlaying) return;

                double elapsedSecs = playbackStopwatch.Elapsed.TotalSeconds;
                timelineControl.CurrentTime = playbackStartOffset + elapsedSecs;

                // Synchronize audio state on every tick to enforce clip boundaries
                ApplyAudioSeek(timelineControl.CurrentTime);

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
            var activeAudioItem = mediaItems.FirstOrDefault(x =>
                x.Type == MediaType.Audio &&
                timePosition >= x.StartTime &&
                timePosition < (x.StartTime + x.Duration));

            if (activeAudioItem != null && File.Exists(activeAudioItem.FilePath))
            {
                // If transitioning to a new clip or seeking, update the player
                if (currentPlayingAudioItem != activeAudioItem || Math.Abs(audioPlayer.Position.TotalSeconds - ((timePosition - activeAudioItem.StartTime) + activeAudioItem.SourceOffset)) > 0.2)
                {
                    EnsureAudioLoaded(activeAudioItem.FilePath);

                    double relativeSecs = (timePosition - activeAudioItem.StartTime) + activeAudioItem.SourceOffset;
                    audioPlayer.Position = TimeSpan.FromSeconds(Math.Max(0, relativeSecs));

                    if (isPlaying)
                    {
                        audioPlayer.Play();
                    }
                }
                currentPlayingAudioItem = activeAudioItem;
            }
            else
            {
                // Pause audio if playhead moves outside active audio boundaries
                try
                {
                    audioPlayer.Pause();
                }
                catch { }
                currentPlayingAudioItem = null;
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
            if (item != null && Math.Abs(item.Duration - (double)numDuration.Value) > 0.001)
            {
                double newDuration = (double)numDuration.Value;

                var command = new VideoEditor.Commands.ChangeDurationCommand(item, item.Duration, newDuration);
                undoRedoManager.ExecuteCommand(command);

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

            // Wrap in Command
            var command = new VideoEditor.Commands.AddMediaItemCommand(mediaItems, item);
            undoRedoManager.ExecuteCommand(command);

            SyncListBox();
            RefreshTimeline();
        }

        private void TimelineControl_ItemResized(object sender, MediaItem item)
        {
            if (item != null)
            {
                // Track duration change via command (captures previous original duration vs current)
                var command = new VideoEditor.Commands.ChangeDurationCommand(item, item.OriginalDuration, item.Duration);
                undoRedoManager.ExecuteCommand(command);

                item.OriginalDuration = item.Duration; // Sync reference for subsequent resizes

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

        private void SplitSelectedClip()
        {
            var item = timelineControl.SelectedItem;
            double playhead = timelineControl.CurrentTime;

            if (item != null && playhead > item.StartTime && playhead < (item.StartTime + item.Duration))
            {
                double splitPointRelative = playhead - item.StartTime;
                double originalDuration = item.Duration;

                // Update left clip parameters
                double oldLeftDuration = item.Duration;
                item.Duration = splitPointRelative;
                if (item.Type == MediaType.Image) UpdateSplitEffectDurations(item);

                // Create right clip parameters
                var newItem = new MediaItem
                {
                    FilePath = item.FilePath,
                    Type = item.Type,
                    StartTime = playhead,
                    Duration = originalDuration - splitPointRelative,
                    OriginalDuration = item.OriginalDuration,
                    SourceOffset = item.SourceOffset + splitPointRelative, // Audio offset shifts forward
                    AudioPeaks = item.AudioPeaks,
                    InEffect = item.InEffect != null ? new TransitionEffect { Type = item.InEffect.Type } : null,
                    OutEffect = item.OutEffect != null ? new TransitionEffect { Type = item.OutEffect.Type } : null,
                    TextData = item.TextData != null ? new TextLabel
                    {
                        Content = item.TextData.Content,
                        X = item.TextData.X,
                        Y = item.TextData.Y,
                        Width = item.TextData.Width,
                        Height = item.TextData.Height,
                        FontSize = item.TextData.FontSize,
                        TextColor = item.TextData.TextColor,
                        BackgroundColor = item.TextData.BackgroundColor
                    } : null
                };

                if (newItem.Type == MediaType.Image) UpdateSplitEffectDurations(newItem);

                // Execute split command
                var command = new VideoEditor.Commands.SplitMediaItemCommand(mediaItems, item, newItem, originalDuration);
                undoRedoManager.ExecuteCommand(command);

                // Re-evaluate audio engine position to force immediate sync/stop
                ApplyAudioSeek(timelineControl.CurrentTime);

                SyncListBox();
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

                double oldStart = item.StartTime;
                double oldDuration = item.Duration;
                double oldOffset = item.SourceOffset;

                double newStart = playhead;
                double newDuration = item.Duration - cutAmount;
                double newOffset = item.SourceOffset + cutAmount;

                var command = new VideoEditor.Commands.TrimMediaItemCommand(
                    item, oldStart, newStart, oldDuration, newDuration, oldOffset, newOffset);

                undoRedoManager.ExecuteCommand(command);
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

                double oldStart = item.StartTime;
                double oldDuration = item.Duration;
                double oldOffset = item.SourceOffset;

                double newStart = item.StartTime;
                double newDuration = playhead - item.StartTime;
                double newOffset = item.SourceOffset;

                var command = new VideoEditor.Commands.TrimMediaItemCommand(
                    item, oldStart, newStart, oldDuration, newDuration, oldOffset, newOffset);

                undoRedoManager.ExecuteCommand(command);
                RefreshTimeline();
            }
        }

        private void RefreshTimeline()
        {
            timelineControl.Invalidate();
            previewControl.RenderFrame(mediaItems, timelineControl.CurrentTime);
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
    }
}