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
        private const int headerHeight = 30;
        private const int trackHeight = 50;

        private bool isDraggingPlayhead = false;
        private bool isDraggingClip = false;
        private bool isResizingClip = false;

        private MediaItem activeClip = null;
        private double clipDragOffset = 0;
        private const int EdgeMargin = 8;

        // Cache waveforms per audio file path to avoid re-calculating on every frame paint
        private Dictionary<string, float[]> waveformCache = new Dictionary<string, float[]>();

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

            this.MouseDown += Timeline_MouseDown;
            this.MouseMove += Timeline_MouseMove;
            this.MouseUp += Timeline_MouseUp;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;

            // 1. Draw Time Ruler Header
            g.FillRectangle(new SolidBrush(Color.FromArgb(35, 35, 35)), 0, 0, this.Width, headerHeight);
            double totalDuration = GetTotalDuration();

            for (int i = 0; i <= Math.Max(totalDuration + 10, 60); i += 2)
            {
                int x = (int)(i * pixelsPerSecond);
                g.DrawLine(Pens.Gray, x, headerHeight - 8, x, headerHeight);
                g.DrawString($"{i}s", this.Font, Brushes.Gray, x + 2, 2);
            }

            // 2. Draw Track Area Backgrounds
            int imageTrackY = headerHeight + 5;
            int audioTrackY = imageTrackY + trackHeight + 5;

            g.FillRectangle(new SolidBrush(Color.FromArgb(30, 30, 30)), 0, imageTrackY, this.Width, trackHeight);
            g.FillRectangle(new SolidBrush(Color.FromArgb(30, 30, 30)), 0, audioTrackY, this.Width, trackHeight);

            // 3. Draw Clips
            foreach (var item in mediaItems)
            {
                int y = item.Type == MediaType.Image ? imageTrackY : audioTrackY;
                int x = (int)(item.StartTime * pixelsPerSecond);
                int width = (int)(item.Duration * pixelsPerSecond);

                var rect = new Rectangle(x, y, Math.Max(width, 15), trackHeight);

                var color = item.Type == MediaType.Image ? Color.SteelBlue : Color.MediumPurple;
                if (item == SelectedItem) color = Color.Crimson;

                g.FillRectangle(new SolidBrush(color), rect);

                // --- AUDIO WAVEFORM / BAR GRAPH RENDER ---
                if (item.Type == MediaType.Audio)
                {
                    DrawAudioClip(g, item, rect);
                }

                g.DrawRectangle(item == SelectedItem ? new Pen(Color.Yellow, 2) : Pens.White, rect);
                g.DrawString($"{Path.GetFileName(item.FilePath)} ({item.Duration:F1}s)", this.Font, Brushes.White, x + 5, y + 2);

                if (item == SelectedItem)
                {
                    g.FillRectangle(Brushes.White, x + width - 4, y, 4, trackHeight);
                }
            }

            // 4. Draw Playhead
            int playheadX = (int)(currentTime * pixelsPerSecond);
            g.DrawLine(new Pen(Color.Red, 2), playheadX, 0, playheadX, this.Height);
        }

        private void DrawAudioClip(Graphics g, MediaItem item, Rectangle clipBounds)
        {
            // 1. Restrict drawing area so anything outside the trimmed clip is clipped out
            g.SetClip(clipBounds);

            // 2. Use the timeline scale (pixelsPerSecond) so 1 second always equals the same pixel width
            double originalDuration = item.OriginalDuration > 0 ? item.OriginalDuration : item.Duration;
            int fullUnclippedWidth = (int)(originalDuration * pixelsPerSecond);

            // 3. Shift the target drawing box left by the trim offset (SourceOffset)
            int renderX = clipBounds.X - (int)(item.SourceOffset * pixelsPerSecond);

            Rectangle fullWaveformBounds = new Rectangle(renderX, clipBounds.Y, fullUnclippedWidth, clipBounds.Height);

            // 4. Render the UNSTRETCHED waveform
            DrawWaveformPeaks(g, item, fullWaveformBounds);

            // Reset clip region
            g.ResetClip();
        }

        private void DrawWaveformPeaks(Graphics g, MediaItem item, Rectangle bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            if (!waveformCache.ContainsKey(item.FilePath))
            {
                waveformCache[item.FilePath] = GenerateAudioPeaks(item.FilePath);
            }

            float[] peaks = waveformCache[item.FilePath];
            if (peaks == null || peaks.Length == 0) return;

            int centerY = bounds.Y + (bounds.Height / 2);
            int maxWaveHeight = (bounds.Height / 2) - 4;

            int barWidth = 2;
            int gap = 1;
            int totalBarSpace = barWidth + gap;

            using (Pen wavePen = new Pen(Color.FromArgb(200, 255, 255, 255), barWidth))
            using (Pen centerLinePen = new Pen(Color.FromArgb(100, 255, 255, 255), 1f))
            {
                g.DrawLine(centerLinePen, bounds.Left, centerY, bounds.Right, centerY);

                // Step through screen coordinates across the full unclipped width
                for (int x = bounds.Left; x < bounds.Right; x += totalBarSpace)
                {
                    // Map physical screen position back to full audio progress
                    float progress = (float)(x - bounds.Left) / bounds.Width;
                    int peakIndex = (int)(progress * peaks.Length);
                    peakIndex = Math.Clamp(peakIndex, 0, peaks.Length - 1);

                    int height = (int)(peaks[peakIndex] * maxWaveHeight);
                    height = Math.Max(height, 2);

                    g.DrawLine(wavePen, x, centerY - height, x, centerY + height);
                }
            }
        }

        private float[] GenerateAudioPeaks(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return new float[0];

                byte[] fileBytes = File.ReadAllBytes(filePath);

                // Standard WAV headers are at least 44 bytes long
                int headerOffset = 44;
                if (fileBytes.Length <= headerOffset) return new float[0];

                // Parse channels and bit depth from header if available
                int channels = BitConverter.ToUInt16(fileBytes, 22);
                int bitsPerSample = BitConverter.ToUInt16(fileBytes, 34);

                if (channels == 0) channels = 2; // Default fallback to stereo
                if (bitsPerSample == 0) bitsPerSample = 16;

                int bytesPerSample = bitsPerSample / 8;
                int totalAudioBytes = fileBytes.Length - headerOffset;
                int totalSamples = totalAudioBytes / (bytesPerSample * channels);

                int targetBarCount = 400; // Number of bars to display across timeline
                float[] peaks = new float[targetBarCount];
                int samplesPerBar = Math.Max(1, totalSamples / targetBarCount);

                for (int i = 0; i < targetBarCount; i++)
                {
                    float maxAmplitude = 0f;
                    int startSample = i * samplesPerBar;
                    int endSample = Math.Min(startSample + samplesPerBar, totalSamples);

                    for (int s = startSample; s < endSample; s++)
                    {
                        int byteIndex = headerOffset + (s * channels * bytesPerSample);
                        if (byteIndex + 1 >= fileBytes.Length) break;

                        // Read 16-bit PCM Audio Sample
                        short sampleValue = (short)(fileBytes[byteIndex] | (fileBytes[byteIndex + 1] << 8));
                        float absValue = Math.Abs(sampleValue / 32768f);

                        if (absValue > maxAmplitude)
                        {
                            maxAmplitude = absValue;
                        }
                    }

                    // Noise gate: remove background jitter/silence artifacts
                    peaks[i] = maxAmplitude < 0.02f ? 0f : Math.Min(maxAmplitude * 1.5f, 1.0f);
                }

                return peaks;
            }
            catch
            {
                return new float[0];
            }
        }
        public void ClearWaveformCache()
        {
            waveformCache.Clear();
            this.Invalidate();
        }
        private void Timeline_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Y <= headerHeight)
            {
                isDraggingPlayhead = true;
                CurrentTime = e.X / pixelsPerSecond;
                return;
            }

            int imageTrackY = headerHeight + 5;
            int audioTrackY = imageTrackY + trackHeight + 5;

            SelectedItem = null;

            foreach (var item in mediaItems)
            {
                int y = item.Type == MediaType.Image ? imageTrackY : audioTrackY;
                int x = (int)(item.StartTime * pixelsPerSecond);
                int width = (int)(item.Duration * pixelsPerSecond);
                var rect = new Rectangle(x, y, Math.Max(width, 15), trackHeight);

                if (rect.Contains(e.Location))
                {
                    SelectedItem = item;
                    ClipSelected?.Invoke(item);
                    activeClip = item;

                    if (e.X >= (x + width - EdgeMargin) && e.X <= (x + width + EdgeMargin))
                    {
                        isResizingClip = true;
                    }
                    else
                    {
                        isDraggingClip = true;
                        clipDragOffset = (e.X / pixelsPerSecond) - item.StartTime;
                    }

                    this.Invalidate();
                    return;
                }
            }

            ClipSelected?.Invoke(null);
            CurrentTime = e.X / pixelsPerSecond;
        }

        private void Timeline_MouseMove(object sender, MouseEventArgs e)
        {
            int imageTrackY = headerHeight + 5;
            int audioTrackY = imageTrackY + trackHeight + 5;

            if (!isDraggingClip && !isResizingClip && !isDraggingPlayhead)
            {
                bool overEdge = false;
                foreach (var item in mediaItems)
                {
                    int y = item.Type == MediaType.Image ? imageTrackY : audioTrackY;
                    int x = (int)(item.StartTime * pixelsPerSecond);
                    int width = (int)(item.Duration * pixelsPerSecond);

                    if (e.Y >= y && e.Y <= y + trackHeight && Math.Abs(e.X - (x + width)) <= EdgeMargin)
                    {
                        overEdge = true;
                        break;
                    }
                }
                this.Cursor = overEdge ? Cursors.SizeWE : Cursors.Default;
            }

            if (isDraggingPlayhead)
            {
                CurrentTime = e.X / pixelsPerSecond;
            }
            else if (isResizingClip && activeClip != null)
            {
                double newDuration = (e.X / pixelsPerSecond) - activeClip.StartTime;
                activeClip.Duration = Math.Max(0.5, newDuration);
                ItemResized?.Invoke(activeClip);

                this.Invalidate();
                TimeChanged?.Invoke(currentTime);
            }
            else if (isDraggingClip && activeClip != null)
            {
                double newStart = (e.X / pixelsPerSecond) - clipDragOffset;
                activeClip.StartTime = Math.Max(0, newStart);
                this.Invalidate();
                TimeChanged?.Invoke(currentTime);
            }
        }

        private void Timeline_MouseUp(object sender, MouseEventArgs e)
        {
            isDraggingPlayhead = false;
            isDraggingClip = false;
            isResizingClip = false;
            activeClip = null;
            this.Cursor = Cursors.Default;
            this.Invalidate();
        }

        public double GetTotalDuration()
        {
            if (mediaItems.Count == 0) return 60.0;
            return mediaItems.Max(x => x.StartTime + x.Duration);
        }
    }
}