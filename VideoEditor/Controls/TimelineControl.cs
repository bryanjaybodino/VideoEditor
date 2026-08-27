using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using VideoEditor.Models;

namespace VideoEditor.Controls
{
    public class TimelineControl : Control
    {
        private List<MediaItem> mediaItems;
        private double currentTime = 0;
        private double pixelsPerSecond = 40;
        private double minPixelsPerSecond = 10;
        private double maxPixelsPerSecond = 200;

        private int scrollX = 0;
        private int scrollY = 0;

        private HScrollBar hScrollBar;
        private VScrollBar vScrollBar;

        private const int headerHeight = 30;
        private const int trackHeight = 45;
        private const int trackMargin = 5;
        private const int scrollBarSize = 18;

        private bool isDraggingPlayhead = false;
        private bool isDraggingClip = false;
        private bool isResizingClip = false;

        private MediaItem activeClip = null;
        private double clipDragOffset = 0;
        private const int EdgeMargin = 8;

        public event Action<double> TimeChanged;
        public event Action<MediaItem> ClipSelected;
        public event Action<MediaItem> ItemResized;

        public MediaItem SelectedItem { get; private set; }

        public double CurrentTime
        {
            get => currentTime;
            set
            {
                currentTime = Math.Max(0, value);
                TimeChanged?.Invoke(currentTime);
                this.Invalidate();
            }
        }

        public TimelineControl(List<MediaItem> items)
        {
            mediaItems = items;
            this.DoubleBuffered = true;
            this.BackColor = Color.FromArgb(25, 25, 25);

            hScrollBar = new HScrollBar { Dock = DockStyle.Bottom, Height = scrollBarSize };
            hScrollBar.Scroll += (s, e) => { scrollX = e.NewValue; this.Invalidate(); };

            vScrollBar = new VScrollBar { Dock = DockStyle.Right, Width = scrollBarSize, Visible = false };
            vScrollBar.Scroll += (s, e) => { scrollY = e.NewValue; this.Invalidate(); };

            this.Controls.Add(hScrollBar);
            this.Controls.Add(vScrollBar);

            this.MouseDown += Timeline_MouseDown;
            this.MouseMove += Timeline_MouseMove;
            this.MouseUp += Timeline_MouseUp;
            this.MouseWheel += Timeline_MouseWheel;
            this.Resize += (s, e) => UpdateScrollBars();
        }

        private int GetMaxVisualTrackIndex()
        {
            var visualItems = mediaItems.Where(x => x.Type == MediaType.Image || x.Type == MediaType.Text).ToList();
            int maxTrack = visualItems.Any() ? visualItems.Max(x => x.TrackIndex) : 0;
            return maxTrack + 2; // Always leaves empty space below for new rows
        }

        private int GetTotalContentHeight()
        {
            int totalTracks = GetMaxVisualTrackIndex() + 1;
            return headerHeight + (totalTracks * (trackHeight + trackMargin)) + 20;
        }

        private int GetTrackY(int trackIndex, MediaType type)
        {
            int relativeIndex = type == MediaType.Audio ? GetMaxVisualTrackIndex() : trackIndex;
            return headerHeight + trackMargin + (relativeIndex * (trackHeight + trackMargin)) - scrollY;
        }

        private int GetTrackIndexFromY(int y, MediaType type)
        {
            if (type == MediaType.Audio) return 0;
            int actualY = y + scrollY - headerHeight - trackMargin;
            return Math.Max(0, actualY / (trackHeight + trackMargin));
        }

        private void UpdateScrollBars()
        {
            double totalDuration = GetTotalDuration();
            int totalWidth = (int)(totalDuration * pixelsPerSecond) + 300;
            int maxScrollX = Math.Max(0, totalWidth - this.Width);

            hScrollBar.Maximum = maxScrollX + hScrollBar.LargeChange;
            hScrollBar.LargeChange = Math.Max(1, this.Width);
            scrollX = Math.Clamp(scrollX, 0, maxScrollX);
            hScrollBar.Value = scrollX;

            int totalHeight = GetTotalContentHeight();
            int visibleHeight = this.Height - hScrollBar.Height;

            if (totalHeight > visibleHeight)
            {
                vScrollBar.Visible = true;
                int maxScrollY = totalHeight - visibleHeight;
                vScrollBar.Maximum = maxScrollY + vScrollBar.LargeChange;
                vScrollBar.LargeChange = Math.Max(1, visibleHeight);
                scrollY = Math.Clamp(scrollY, 0, maxScrollY);
                vScrollBar.Value = scrollY;
            }
            else
            {
                vScrollBar.Visible = false;
                scrollY = 0;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;

            UpdateScrollBars();

            // 1. Tracks Background
            int maxVisualTracks = GetMaxVisualTrackIndex();
            for (int i = 0; i < maxVisualTracks; i++)
            {
                int trackY = GetTrackY(i, MediaType.Image);
                if (trackY + trackHeight < headerHeight || trackY > this.Height) continue;

                g.FillRectangle(new SolidBrush(Color.FromArgb(30, 30, 30)), 0, trackY, this.Width, trackHeight);
                g.DrawString($"Row {i + 1}", this.Font, Brushes.DimGray, 5, trackY + 2);
            }

            int audioY = GetTrackY(0, MediaType.Audio);
            if (audioY + trackHeight >= headerHeight && audioY <= this.Height)
            {
                g.FillRectangle(new SolidBrush(Color.FromArgb(20, 35, 35)), 0, audioY, this.Width, trackHeight);
                g.DrawString("Audio Row", this.Font, Brushes.DarkTurquoise, 5, audioY + 2);
            }

            // 2. Draw Clips
            foreach (var item in mediaItems)
            {
                int y = GetTrackY(item.TrackIndex, item.Type);
                int x = (int)(item.StartTime * pixelsPerSecond) - scrollX;
                int width = (int)(item.Duration * pixelsPerSecond);

                var rect = new Rectangle(x, y, Math.Max(width, 15), trackHeight);
                if (rect.Bottom < headerHeight || rect.Top > this.Height || rect.Right < 0 || rect.Left > this.Width) continue;

                var color = Color.SteelBlue;
                if (item.Type == MediaType.Audio) color = Color.FromArgb(30, 70, 70);
                else if (item.Type == MediaType.Text) color = Color.DarkGoldenrod;

                if (item == SelectedItem) color = Color.Crimson;

                // Clip Container
                g.FillRectangle(new SolidBrush(color), rect);

                if (item == SelectedItem)
                {
                    using (var selectPen = new Pen(Color.Yellow, 2))
                    {
                        g.DrawRectangle(selectPen, rect);
                    }
                }

                if (item.Type == MediaType.Image && !string.IsNullOrEmpty(item.FilePath))
                {
                    string fileName = System.IO.Path.GetFileName(item.FilePath);
                    using (var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                    using (var font = new Font(this.Font.FontFamily, 8.5f, FontStyle.Regular))
                    {
                        var textRect = new Rectangle(rect.X + 4, rect.Y + 4, rect.Width - 8, rect.Height - 8);
                        g.DrawString(fileName, font, Brushes.White, textRect, format);
                    }
                }

                if (item.Type == MediaType.Text && item.TextData != null)
                {
                    g.DrawString(item.TextData.Content, this.Font, Brushes.White, rect.X + 5, rect.Y + 12);
                }

                // Render Audio Waveform
                if (item.Type == MediaType.Audio && item.AudioPeaks != null && item.AudioPeaks.Length > 0)
                {
                    using (Pen wavePen = new Pen(Color.FromArgb(100, 255, 220), 1))
                    {
                        int centerY = rect.Y + (rect.Height / 2);
                        int peakCount = item.AudioPeaks.Length;

                        double fullAudioDur = item.OriginalDuration > 0 ? item.OriginalDuration : item.Duration;
                        if (fullAudioDur <= 0) fullAudioDur = item.Duration;

                        for (int px = 2; px < rect.Width - 2; px++)
                        {
                            double localTime = ((double)px / rect.Width) * item.Duration;
                            double sourceFileTime = item.SourceOffset + localTime;

                            double fileProgress = sourceFileTime / fullAudioDur;
                            int sampleIdx = (int)(fileProgress * peakCount);

                            if (sampleIdx >= 0 && sampleIdx < peakCount)
                            {
                                float peak = item.AudioPeaks[sampleIdx];
                                float scaledPeak = (float)Math.Sqrt(peak);
                                int amplitude = Math.Max(2, (int)(scaledPeak * (rect.Height / 2.3f)));

                                g.DrawLine(wavePen, rect.X + px, centerY - amplitude, rect.X + px, centerY + amplitude);
                            }
                        }
                    }
                }

                // Render Text Label Duration Rectangles
                if (item.TextLabels != null)
                {
                    foreach (var label in item.TextLabels)
                    {
                        int labelX = (int)((item.StartTime + label.StartTime) * pixelsPerSecond) - scrollX;
                        int labelWidth = (int)(label.Duration * pixelsPerSecond);
                        var textRect = new Rectangle(labelX, rect.Y + rect.Height - 18, Math.Max(labelWidth, 4), 14);

                        using (var textBgBrush = new SolidBrush(Color.FromArgb(200, 255, 165, 0)))
                        {
                            g.FillRectangle(textBgBrush, textRect);
                        }
                        using (var textBorder = new Pen(Color.Black, 1))
                        {
                            g.DrawRectangle(textBorder, textRect);
                        }
                    }
                }
            }

            // 3. Pinned Time Header
            g.FillRectangle(new SolidBrush(Color.FromArgb(35, 35, 35)), 0, 0, this.Width, headerHeight);
            double visibleDuration = (this.Width + scrollX) / pixelsPerSecond;
            int stepSeconds = pixelsPerSecond < 20 ? 10 : (pixelsPerSecond < 50 ? 2 : 1);

            for (int i = 0; i <= Math.Max(GetTotalDuration() + 60, visibleDuration + 10); i += stepSeconds)
            {
                int x = (int)(i * pixelsPerSecond) - scrollX;
                if (x < 0 || x > this.Width) continue;

                g.DrawLine(Pens.Gray, x, headerHeight - 8, x, headerHeight);
                g.DrawString($"{i}s", this.Font, Brushes.Gray, x + 2, 2);
            }

            // 4. Playhead Line
            int playheadX = (int)(currentTime * pixelsPerSecond) - scrollX;
            if (playheadX >= 0 && playheadX <= this.Width)
            {
                g.DrawLine(new Pen(Color.Red, 2), playheadX, 0, playheadX, this.Height);
            }
        }

        private void Timeline_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Y <= headerHeight)
            {
                isDraggingPlayhead = true;
                CurrentTime = (e.X + scrollX) / pixelsPerSecond;
                return;
            }

            SelectedItem = null;
            foreach (var item in mediaItems)
            {
                int y = GetTrackY(item.TrackIndex, item.Type);
                int x = (int)(item.StartTime * pixelsPerSecond) - scrollX;
                int width = (int)(item.Duration * pixelsPerSecond);
                var rect = new Rectangle(x, y, Math.Max(width, 15), trackHeight);

                if (rect.Contains(e.Location))
                {
                    SelectedItem = item;
                    ClipSelected?.Invoke(item);
                    activeClip = item;

                    if (e.X >= (x + width - EdgeMargin) && e.X <= (x + width + EdgeMargin)) isResizingClip = true;
                    else
                    {
                        isDraggingClip = true;
                        clipDragOffset = ((e.X + scrollX) / pixelsPerSecond) - item.StartTime;
                    }
                    this.Invalidate();
                    return;
                }
            }

            ClipSelected?.Invoke(null);
            CurrentTime = (e.X + scrollX) / pixelsPerSecond;
        }

        private void Timeline_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDraggingPlayhead)
            {
                CurrentTime = (e.X + scrollX) / pixelsPerSecond;
            }
            else if (isResizingClip && activeClip != null)
            {
                double newDuration = ((e.X + scrollX) / pixelsPerSecond) - activeClip.StartTime;
                activeClip.Duration = Math.Max(0.5, newDuration);
                ItemResized?.Invoke(activeClip);
                this.Invalidate();
            }
            else if (isDraggingClip && activeClip != null)
            {
                double newStart = ((e.X + scrollX) / pixelsPerSecond) - clipDragOffset;
                activeClip.StartTime = Math.Max(0, newStart);

                if (activeClip.Type == MediaType.Image || activeClip.Type == MediaType.Text)
                {
                    int newTrack = GetTrackIndexFromY(e.Y, activeClip.Type);
                    if (activeClip.TrackIndex != newTrack)
                    {
                        activeClip.TrackIndex = newTrack;
                        UpdateScrollBars();
                    }
                }

                this.Invalidate();
            }
        }

        private void Timeline_MouseUp(object sender, MouseEventArgs e)
        {
            isDraggingPlayhead = false;
            isDraggingClip = false;
            isResizingClip = false;
            activeClip = null;
            this.Invalidate();
        }

        private void Timeline_MouseWheel(object sender, MouseEventArgs e)
        {
            if (ModifierKeys == Keys.Control)
            {
                double mouseTime = (e.X + scrollX) / pixelsPerSecond;
                pixelsPerSecond = e.Delta > 0 ? Math.Min(pixelsPerSecond * 1.15, maxPixelsPerSecond) : Math.Max(pixelsPerSecond / 1.15, minPixelsPerSecond);
                scrollX = Math.Max(0, (int)(mouseTime * pixelsPerSecond - e.X));
            }
            else if (vScrollBar.Visible)
            {
                scrollY = Math.Clamp(scrollY - (e.Delta / 2), 0, vScrollBar.Maximum);
            }
            else
            {
                scrollX = Math.Max(0, scrollX - (e.Delta / 2));
            }

            UpdateScrollBars();
            this.Invalidate();
        }

        public double GetTotalDuration()
        {
            if (mediaItems.Count == 0) return 60.0;
            return mediaItems.Max(x => x.StartTime + x.Duration);
        }
    }
}