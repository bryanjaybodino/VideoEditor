using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using VideoEditor.Commands;
using VideoEditor.Models;
using VideoEditor.Services;

namespace VideoEditor.Controls
{
    public class TimelineControl : UserControl
    {
        [DllImport("uxtheme.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);

        private List<MediaItem> mediaItems = new List<MediaItem>();
        private HashSet<int> lockedTracks = new HashSet<int>();
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
        public int SelectedTrackIndex { get; private set; } = 0;
        private bool isDraggingPlayhead = false;
        private bool isDraggingClip = false;
        private bool isResizingClip = false;

        private MediaItem activeClip = null;
        private double clipDragOffset = 0;
        private const int EdgeMargin = 8;

        // Command tracking variables
        private double initialClipStartTime = 0;
        private int initialClipTrackIndex = 0;
        private double initialClipDuration = 0;

        public UndoRedoManager UndoRedoManager { get; set; }

        public event Action<double> TimeChanged;
        public event Action<MediaItem> ClipSelected;
        public event Action<MediaItem> ItemResized;

        public MediaItem SelectedItem { get; private set; }

        private const double SnapThresholdPixels = 10; // Distance in pixels to trigger snap/stop
        private const double BreakoutThresholdPixels = 25; // Drag distance required past edge to force continue
        private bool isSnappedToBoundary = false;
        private double targetSnapTime = 0;

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

        public TimelineControl()
        {
            this.DoubleBuffered = true;
            this.BackColor = Color.FromArgb(25, 25, 25);

            hScrollBar = new HScrollBar { Dock = DockStyle.Bottom, Height = scrollBarSize };
            hScrollBar.Scroll += (s, e) => { scrollX = e.NewValue; this.Invalidate(); };

            vScrollBar = new VScrollBar { Dock = DockStyle.Right, Width = scrollBarSize, Visible = false };
            vScrollBar.Scroll += (s, e) => { scrollY = e.NewValue; this.Invalidate(); };

            this.Controls.Add(hScrollBar);
            this.Controls.Add(vScrollBar);

            ApplyDarkModeScrollbars();

            this.MouseDown += Timeline_MouseDown;
            this.MouseMove += Timeline_MouseMove;
            this.MouseUp += Timeline_MouseUp;
            this.MouseWheel += Timeline_MouseWheel;
            this.Resize += (s, e) => UpdateScrollBars();
        }

        private void ApplyDarkModeScrollbars()
        {
            if (hScrollBar.IsHandleCreated) SetWindowTheme(hScrollBar.Handle, "DarkMode_Explorer", null);
            else hScrollBar.HandleCreated += (s, e) => SetWindowTheme(hScrollBar.Handle, "DarkMode_Explorer", null);

            if (vScrollBar.IsHandleCreated) SetWindowTheme(vScrollBar.Handle, "DarkMode_Explorer", null);
            else vScrollBar.HandleCreated += (s, e) => SetWindowTheme(vScrollBar.Handle, "DarkMode_Explorer", null);
        }

        public TimelineControl(List<MediaItem> items) : this()
        {
            SetMediaItems(items);
        }

        public void SetMediaItems(List<MediaItem> items)
        {
            mediaItems = items ?? new List<MediaItem>();
            UpdateScrollBars();
            this.Invalidate();
        }

        private int GetMaxVisualTrackIndex()
        {
            var visualItems = mediaItems.Where(x => x.Type == MediaType.Image || x.Type == MediaType.Text || x.Type == MediaType.Blur).ToList();
            int maxTrack = visualItems.Any() ? visualItems.Max(x => x.TrackIndex) : 0;
            return maxTrack + 2;
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

            int maxVisualTracks = GetMaxVisualTrackIndex();
            int leftPanelWidth = 80;

            for (int i = 0; i < maxVisualTracks; i++)
            {
                int trackY = GetTrackY(i, MediaType.Image);
                if (trackY + trackHeight < headerHeight || trackY > this.Height) continue;

                bool isRowSelected = (i == SelectedTrackIndex);

                Color rowBgColor = isRowSelected ? Color.FromArgb(45, 55, 75) : Color.FromArgb(30, 30, 30);
                using (var bgBrush = new SolidBrush(rowBgColor))
                {
                    g.FillRectangle(bgBrush, leftPanelWidth, trackY, this.Width - leftPanelWidth, trackHeight);
                }

                if (isRowSelected)
                {
                    using (var borderPen = new Pen(Color.FromArgb(0, 122, 204), 1.5f))
                    {
                        g.DrawRectangle(borderPen, leftPanelWidth, trackY, this.Width - leftPanelWidth - 1, trackHeight);
                    }
                }
            }

            int audioY = GetTrackY(0, MediaType.Audio);
            if (audioY + trackHeight >= headerHeight && audioY <= this.Height)
            {
                using (var audioBgBrush = new SolidBrush(Color.FromArgb(20, 35, 35)))
                {
                    g.FillRectangle(audioBgBrush, leftPanelWidth, audioY, this.Width - leftPanelWidth, trackHeight);
                }
            }

            var originalClip = g.Clip;
            g.SetClip(new Rectangle(leftPanelWidth, 0, this.Width - leftPanelWidth, this.Height));

            var sortedItems = mediaItems.OrderBy(item => item.Type switch
            {
                MediaType.Audio => 0,
                MediaType.Image => 1,
                MediaType.Blur => 2,
                MediaType.Text => 3,
                _ => 0
            });

            foreach (var item in sortedItems)
            {
                int y = GetTrackY(item.TrackIndex, item.Type);
                int x = leftPanelWidth + (int)(item.StartTime * pixelsPerSecond) - scrollX;
                int width = (int)(item.Duration * pixelsPerSecond);

                var rect = new Rectangle(x, y, Math.Max(width, 15), trackHeight);
                if (rect.Bottom < headerHeight || rect.Top > this.Height || rect.Right < leftPanelWidth || rect.Left > this.Width) continue;

                var color = Color.SteelBlue;
                if (item.Type == MediaType.Audio) color = Color.FromArgb(30, 70, 70);
                else if (item.Type == MediaType.Text) color = Color.DarkGoldenrod;
                else if (item.Type == MediaType.Blur) color = Color.Purple;

                if (item == SelectedItem) color = Color.Crimson;

                using (var clipBrush = new SolidBrush(color))
                {
                    g.FillRectangle(clipBrush, rect);
                }

                if (item == SelectedItem)
                {
                    using (var selectPen = new Pen(Color.Yellow, 2))
                    {
                        g.DrawRectangle(selectPen, rect);
                    }
                }

                if (item.Type == MediaType.Image && !string.IsNullOrEmpty(item.FilePath))
                {
                    string fileName = Path.GetFileName(item.FilePath);
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

                if (item.Type == MediaType.Blur)
                {
                    using (var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                    using (var font = new Font(this.Font.FontFamily, 8.5f, FontStyle.Regular))
                    {
                        var textRect = new Rectangle(rect.X + 4, rect.Y + 4, rect.Width - 8, rect.Height - 8);
                        g.DrawString("Blur Overlay", font, Brushes.White, textRect, format);
                    }
                }

                if (item.Type == MediaType.Audio && item.AudioPeaks != null && item.AudioPeaks.Length > 0)
                {
                    using (Pen wavePen = new Pen(Color.FromArgb(100, 255, 220), 1))
                    {
                        int centerY = rect.Y + (rect.Height / 2);
                        int peakCount = item.AudioPeaks.Length;
                        double fullAudioDur = item.OriginalDuration > 0 ? item.OriginalDuration : item.Duration;

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

                if (item.TextLabels != null)
                {
                    foreach (var label in item.TextLabels)
                    {
                        int labelX = leftPanelWidth + (int)((item.StartTime + label.StartTime) * pixelsPerSecond) - scrollX;
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

                if (lockedTracks.Contains(item.TrackIndex) && item.Type != MediaType.Audio)
                {
                    using (var lockHatch = new SolidBrush(Color.FromArgb(90, 0, 0, 0)))
                    {
                        g.FillRectangle(lockHatch, rect);
                    }
                }
            }

            int playheadX = leftPanelWidth + (int)(currentTime * pixelsPerSecond) - scrollX;
            if (playheadX >= leftPanelWidth && playheadX <= this.Width)
            {
                using (var playheadPen = new Pen(Color.Red, 2))
                {
                    g.DrawLine(playheadPen, playheadX, 0, playheadX, this.Height);
                }
            }

            g.Clip = originalClip;

            using (var headerBrush = new SolidBrush(Color.FromArgb(35, 35, 35)))
            {
                g.FillRectangle(headerBrush, 0, 0, this.Width, headerHeight);
            }

            double visibleDuration = (this.Width - leftPanelWidth + scrollX) / pixelsPerSecond;
            int stepSeconds = pixelsPerSecond < 20 ? 10 : (pixelsPerSecond < 50 ? 2 : 1);

            for (int i = 0; i <= Math.Max(GetTotalDuration() + 60, visibleDuration + 10); i += stepSeconds)
            {
                int x = leftPanelWidth + (int)(i * pixelsPerSecond) - scrollX;
                if (x < leftPanelWidth || x > this.Width) continue;

                g.DrawLine(Pens.Gray, x, headerHeight - 8, x, headerHeight);
                g.DrawString($"{i}s", this.Font, Brushes.Gray, x + 2, 2);
            }

            for (int i = 0; i < maxVisualTracks; i++)
            {
                int trackY = GetTrackY(i, MediaType.Image);
                if (trackY + trackHeight < headerHeight || trackY > this.Height) continue;

                bool isRowSelected = (i == SelectedTrackIndex);
                bool isTrackLocked = lockedTracks.Contains(i);

                Color leftBgColor = isRowSelected ? Color.FromArgb(40, 50, 70) : Color.FromArgb(25, 25, 25);
                using (var leftBrush = new SolidBrush(leftBgColor))
                {
                    g.FillRectangle(leftBrush, 0, trackY, leftPanelWidth, trackHeight);
                }

                g.DrawLine(Pens.Gray, leftPanelWidth, trackY, leftPanelWidth, trackY + trackHeight);

                int btnY = trackY + 12;
                int btnW = 20;
                int btnH = 20;

                Rectangle rectUp = new Rectangle(5, btnY, btnW, btnH);
                Rectangle rectDelete = new Rectangle(30, btnY, btnW, btnH);
                Rectangle rectLock = new Rectangle(55, btnY, btnW, btnH);

                using (var btnBrush = new SolidBrush(Color.FromArgb(50, 50, 50)))
                {
                    g.FillRectangle(btnBrush, rectUp);
                }

                using (var deleteBtnBrush = new SolidBrush(Color.FromArgb(80, 30, 30)))
                {
                    g.FillRectangle(deleteBtnBrush, rectDelete);
                }

                Color lockBgColor = isTrackLocked ? Color.FromArgb(120, 80, 20) : Color.FromArgb(50, 50, 50);
                using (var lockBtnBrush = new SolidBrush(lockBgColor))
                {
                    g.FillRectangle(lockBtnBrush, rectLock);
                }

                g.DrawRectangle(Pens.Gray, rectUp);
                g.DrawRectangle(Pens.IndianRed, rectDelete);
                g.DrawRectangle(isTrackLocked ? Pens.Gold : Pens.Gray, rectLock);

                g.DrawString("▲", new Font(this.Font.FontFamily, 7.5f), Brushes.White, rectUp.X + 3, rectUp.Y + 2);
                g.DrawString("✕", new Font(this.Font.FontFamily, 7.5f, FontStyle.Bold), Brushes.Tomato, rectDelete.X + 3, rectDelete.Y + 2);

                string lockText = isTrackLocked ? "L" : "U";
                Brush lockTextBrush = isTrackLocked ? Brushes.Gold : Brushes.LightGray;
                using (var lockFont = new Font("Segoe UI", 7.5f, FontStyle.Bold))
                {
                    g.DrawString(lockText, lockFont, lockTextBrush, rectLock.X + 5, rectLock.Y + 2);
                }
            }

            if (audioY + trackHeight >= headerHeight && audioY <= this.Height)
            {
                using (var audioHeaderBrush = new SolidBrush(Color.FromArgb(15, 30, 30)))
                {
                    g.FillRectangle(audioHeaderBrush, 0, audioY, leftPanelWidth, trackHeight);
                }
                g.DrawLine(Pens.Gray, leftPanelWidth, audioY, leftPanelWidth, audioY + trackHeight);
                g.DrawString("Audio", this.Font, Brushes.DarkTurquoise, 22, audioY + 14);
            }

            using (var cornerBrush = new SolidBrush(Color.FromArgb(20, 20, 20)))
            {
                g.FillRectangle(cornerBrush, 0, 0, leftPanelWidth, headerHeight);
            }
            g.DrawLine(Pens.Gray, leftPanelWidth, 0, leftPanelWidth, headerHeight);
        }

        private void Timeline_MouseDown(object sender, MouseEventArgs e)
        {
            int leftPanelWidth = 80;

            if (isDraggingPlayhead || isDraggingClip || isResizingClip) return;

            if (e.Y <= headerHeight)
            {
                isDraggingPlayhead = true;
                CurrentTime = Math.Max(0, (e.X - leftPanelWidth + scrollX) / pixelsPerSecond);
                return;
            }

            int clickedTrackIndex = GetTrackIndexFromY(e.Y, MediaType.Image);
            int trackY = GetTrackY(clickedTrackIndex, MediaType.Image);

            if (e.X < leftPanelWidth)
            {
                int btnY = trackY + 12;
                int btnW = 20, btnH = 20;

                if (new Rectangle(5, btnY, btnW, btnH).Contains(e.Location)) { InsertTrackAbove(clickedTrackIndex); return; }
                if (new Rectangle(30, btnY, btnW, btnH).Contains(e.Location)) { DeleteTrackAt(clickedTrackIndex); return; }
                if (new Rectangle(55, btnY, btnW, btnH).Contains(e.Location)) { ToggleTrackLock(clickedTrackIndex); return; }
            }

            SelectedTrackIndex = clickedTrackIndex;

            bool isAudioRowClicked = (e.Y >= GetTrackY(0, MediaType.Audio));

            var hitTestOrder = mediaItems
                .Where(item => isAudioRowClicked
                    ? item.Type == MediaType.Audio
                    : item.TrackIndex == clickedTrackIndex && item.Type != MediaType.Audio)
                .OrderByDescending(item => item.Type switch
                {
                    MediaType.Text => 3,
                    MediaType.Blur => 2,
                    MediaType.Image => 1,
                    _ => 0
                });

            foreach (var item in hitTestOrder)
            {
                int y = GetTrackY(item.TrackIndex, item.Type);
                int x = leftPanelWidth + (int)(item.StartTime * pixelsPerSecond) - scrollX;
                int width = (int)(item.Duration * pixelsPerSecond);
                var rect = new Rectangle(x, y, Math.Max(width, 15), trackHeight);

                if (rect.Contains(e.Location))
                {
                    if (lockedTracks.Contains(item.TrackIndex) && item.Type != MediaType.Audio) continue;

                    SelectedItem = item;
                    SelectedTrackIndex = item.TrackIndex;
                    ClipSelected?.Invoke(item);

                    activeClip = item;
                    initialClipStartTime = item.StartTime;
                    initialClipTrackIndex = item.TrackIndex;
                    initialClipDuration = item.Duration;

                    if (e.X >= (x + width - EdgeMargin) && e.X <= (x + width + EdgeMargin))
                    {
                        isSnappedToBoundary = false;
                        isResizingClip = true;
                    }
                    else
                    {
                        isDraggingClip = true;
                        clipDragOffset = ((e.X - leftPanelWidth + scrollX) / pixelsPerSecond) - item.StartTime;
                    }
                    this.Invalidate();
                    return;
                }
            }

            SelectedItem = null;
            ClipSelected?.Invoke(null);
            CurrentTime = Math.Max(0, (e.X - leftPanelWidth + scrollX) / pixelsPerSecond);
            this.Invalidate();
        }

        private void Timeline_MouseMove(object sender, MouseEventArgs e)
        {
            int leftPanelWidth = 80;

            if (isDraggingPlayhead)
            {
                this.Cursor = Cursors.Default;
                CurrentTime = Math.Max(0, (e.X - leftPanelWidth + scrollX) / pixelsPerSecond);
            }
            else if (isResizingClip && activeClip != null)
            {
                this.Cursor = Cursors.SizeWE;

                // Calculate raw target duration based on cursor position
                double rawDuration = ((e.X - leftPanelWidth + scrollX) / pixelsPerSecond) - activeClip.StartTime;
                double candidateDuration = Math.Max(0.5, rawDuration);
                double candidateEndTime = activeClip.StartTime + candidateDuration;

                // Find closest clip on the same track that starts AFTER the current clip
                var nextClip = mediaItems
                    .Where(item => item != activeClip &&
                                   item.TrackIndex == activeClip.TrackIndex &&
                                   item.Type == activeClip.Type &&
                                   item.StartTime >= activeClip.StartTime)
                    .OrderBy(item => item.StartTime)
                    .FirstOrDefault();

                if (nextClip != null)
                {
                    double obstacleStartTime = nextClip.StartTime;

                    // Convert distance to pixels for intuitive snapping across different zoom levels
                    double pixelDiff = (candidateEndTime - obstacleStartTime) * pixelsPerSecond;
                    double snapDistancePx = SnapThresholdPixels;
                    double breakoutDistancePx = BreakoutThresholdPixels;

                    if (!isSnappedToBoundary)
                    {
                        // Check if we hit or crossed the neighbor's boundary
                        if (pixelDiff >= -snapDistancePx && pixelDiff <= breakoutDistancePx)
                        {
                            isSnappedToBoundary = true;
                            targetSnapTime = obstacleStartTime;
                            candidateDuration = Math.Max(0.5, targetSnapTime - activeClip.StartTime);
                        }
                    }
                    else
                    {
                        // Currently snapped: check if user pulled back before boundary OR pushed far enough to force through
                        if (pixelDiff < -snapDistancePx)
                        {
                            // Pulled mouse back
                            isSnappedToBoundary = false;
                        }
                        else if (pixelDiff > breakoutDistancePx)
                        {
                            // User forced through the obstacle
                            isSnappedToBoundary = false;
                        }
                        else
                        {
                            // Hold at the boundary edge
                            candidateDuration = Math.Max(0.5, targetSnapTime - activeClip.StartTime);
                        }
                    }
                }
                else
                {
                    isSnappedToBoundary = false;
                }

                activeClip.Duration = candidateDuration;
                this.Invalidate();
            }
            else if (isDraggingClip && activeClip != null)
            {
                this.Cursor = Cursors.Default;
                double newStart = ((e.X - leftPanelWidth + scrollX) / pixelsPerSecond) - clipDragOffset;
                activeClip.StartTime = Math.Max(0, newStart);

                if (activeClip.Type == MediaType.Image || activeClip.Type == MediaType.Text || activeClip.Type == MediaType.Blur)
                {
                    int newTrack = GetTrackIndexFromY(e.Y, activeClip.Type);

                    if (activeClip.TrackIndex != newTrack && !lockedTracks.Contains(newTrack))
                    {
                        activeClip.TrackIndex = newTrack;
                        UpdateScrollBars();
                    }
                }

                this.Invalidate();
            }
            else
            {
                // Hover Detection for Resize Handles
                bool isOverResizeEdge = false;
                int hoveredTrackIndex = GetTrackIndexFromY(e.Y, MediaType.Image);
                bool isAudioRowHovered = (e.Y >= GetTrackY(0, MediaType.Audio));

                var hoveredItems = mediaItems
                    .Where(item => isAudioRowHovered
                        ? item.Type == MediaType.Audio
                        : item.TrackIndex == hoveredTrackIndex && item.Type != MediaType.Audio);

                foreach (var item in hoveredItems)
                {
                    if (lockedTracks.Contains(item.TrackIndex) && item.Type != MediaType.Audio) continue;

                    int y = GetTrackY(item.TrackIndex, item.Type);
                    int x = leftPanelWidth + (int)(item.StartTime * pixelsPerSecond) - scrollX;
                    int width = (int)(item.Duration * pixelsPerSecond);
                    var rect = new Rectangle(x, y, Math.Max(width, 15), trackHeight);

                    // Check if cursor is on the right edge handle
                    if (rect.Contains(e.Location) && e.X >= (x + width - EdgeMargin) && e.X <= (x + width + EdgeMargin))
                    {
                        isOverResizeEdge = true;
                        break;
                    }
                }

                this.Cursor = isOverResizeEdge ? Cursors.SizeWE : Cursors.Default;
            }
        }

        private void Timeline_MouseUp(object sender, MouseEventArgs e)
        {
            if (isResizingClip && activeClip != null)
            {
                if (Math.Abs(activeClip.Duration - initialClipDuration) > 0.001)
                {
                    var cmd = new ChangeDurationCommand(activeClip, initialClipDuration, activeClip.Duration);
                    UndoRedoManager?.ExecuteCommand(cmd);
                    ItemResized?.Invoke(activeClip);
                }
            }
            else if (isDraggingClip && activeClip != null)
            {
                if (Math.Abs(activeClip.StartTime - initialClipStartTime) > 0.001 || activeClip.TrackIndex != initialClipTrackIndex)
                {
                    var cmd = new MoveClipCommand(activeClip, initialClipStartTime, activeClip.StartTime, initialClipTrackIndex, activeClip.TrackIndex);
                    UndoRedoManager?.ExecuteCommand(cmd);
                }
            }
            isSnappedToBoundary = false;
            isDraggingPlayhead = false;
            isDraggingClip = false;
            isResizingClip = false;
            activeClip = null;
            this.Cursor = Cursors.Default;
            this.Invalidate();
        }
        private void Timeline_MouseWheel(object sender, MouseEventArgs e)
        {
            int leftPanelWidth = 80;

            if (ModifierKeys == Keys.Control)
            {
                double mouseTime = (e.X - leftPanelWidth + scrollX) / pixelsPerSecond;
                pixelsPerSecond = e.Delta > 0 ? Math.Min(pixelsPerSecond * 1.15, maxPixelsPerSecond) : Math.Max(pixelsPerSecond / 1.15, minPixelsPerSecond);
                scrollX = Math.Max(0, (int)(mouseTime * pixelsPerSecond - (e.X - leftPanelWidth)));
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

        public void ToggleTrackLock(int trackIndex)
        {
            var command = new ToggleLockCommand(lockedTracks, trackIndex);
            UndoRedoManager?.ExecuteCommand(command);
            this.Invalidate();
        }

        public void InsertTrackAbove(int targetTrackIndex)
        {
            var command = new InsertTrackRowCommand(mediaItems, lockedTracks, targetTrackIndex);
            UndoRedoManager?.ExecuteCommand(command);
            SelectedTrackIndex = targetTrackIndex;
            this.Invalidate();
        }

        public void DeleteTrackAt(int trackIndex)
        {
            if (trackIndex < 0) return;

            bool hasItemsOnTrack = mediaItems.Any(item =>
                item.Type != MediaType.Audio && item.TrackIndex == trackIndex
            );

            if (hasItemsOnTrack)
            {
                MessageBox.Show(
                    "Cannot delete a row that contains media items. Please clear or move the clips first.",
                    "Row Not Empty",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                return;
            }

            var command = new DeleteTrackRowCommand(mediaItems, lockedTracks, trackIndex);
            UndoRedoManager?.ExecuteCommand(command);

            int maxTracks = GetMaxVisualTrackIndex();
            if (SelectedTrackIndex >= maxTracks - 1 && SelectedTrackIndex > 0)
            {
                SelectedTrackIndex--;
            }

            this.Invalidate();
        }

        public bool IsTrackLocked(int trackIndex)
        {
            return lockedTracks.Contains(trackIndex);
        }

        public double GetTotalDuration()
        {
            if (mediaItems.Count == 0) return 60.0;
            return mediaItems.Max(x => x.StartTime + x.Duration);
        }
    }
}