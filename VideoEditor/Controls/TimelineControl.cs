using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using VideoEditor.Models;

namespace VideoEditor.Controls
{
    public class TimelineControl : UserControl
    {
        // P/Invoke to apply Windows 10/11 Dark Theme to standard WinForms ScrollBars
        [DllImport("uxtheme.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);

        private List<MediaItem> mediaItems = new List<MediaItem>();
        private HashSet<int> lockedTracks = new HashSet<int>(); // Tracks set to locked state
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

        // Parameterless constructor required by WinForms Designer
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

            // Apply Windows Dark Theme to Scrollbar Controls
            ApplyDarkModeScrollbars();

            this.MouseDown += Timeline_MouseDown;
            this.MouseMove += Timeline_MouseMove;
            this.MouseUp += Timeline_MouseUp;
            this.MouseWheel += Timeline_MouseWheel;
            this.Resize += (s, e) => UpdateScrollBars();
        }

        // Enables Dark Theme via UxTheme API
        private void ApplyDarkModeScrollbars()
        {
            if (hScrollBar.IsHandleCreated) SetWindowTheme(hScrollBar.Handle, "DarkMode_Explorer", null);
            else hScrollBar.HandleCreated += (s, e) => SetWindowTheme(hScrollBar.Handle, "DarkMode_Explorer", null);

            if (vScrollBar.IsHandleCreated) SetWindowTheme(vScrollBar.Handle, "DarkMode_Explorer", null);
            else vScrollBar.HandleCreated += (s, e) => SetWindowTheme(vScrollBar.Handle, "DarkMode_Explorer", null);
        }

        // Overloaded constructor for instantiation with existing items
        public TimelineControl(List<MediaItem> items) : this()
        {
            SetMediaItems(items);
        }

        // Public method to bind/update media items from MainForm
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
            int leftPanelWidth = 80; // Margin boundary for controls

            // 1. Full-Width Row Backgrounds
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

            // Audio Row Background
            int audioY = GetTrackY(0, MediaType.Audio);
            if (audioY + trackHeight >= headerHeight && audioY <= this.Height)
            {
                using (var audioBgBrush = new SolidBrush(Color.FromArgb(20, 35, 35)))
                {
                    g.FillRectangle(audioBgBrush, leftPanelWidth, audioY, this.Width - leftPanelWidth, trackHeight);
                }
            }

            // Set Clipping Region so clips never draw inside the left control panel
            var originalClip = g.Clip;
            g.SetClip(new Rectangle(leftPanelWidth, 0, this.Width - leftPanelWidth, this.Height));

            // 2. Draw Clips (Sorted in ascending priority order so higher priority layers paint on top)
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

                if (item.Type == MediaType.Blur)
                {
                    using (var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                    using (var font = new Font(this.Font.FontFamily, 8.5f, FontStyle.Regular))
                    {
                        var textRect = new Rectangle(rect.X + 4, rect.Y + 4, rect.Width - 8, rect.Height - 8);
                        g.DrawString("Blur Overlay", font, Brushes.White, textRect, format);
                    }
                }

                // Waveforms
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

                // Text Duration Rectangles
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

                // Draw translucent lock overlay if track is locked
                if (lockedTracks.Contains(item.TrackIndex) && item.Type != MediaType.Audio)
                {
                    using (var lockHatch = new SolidBrush(Color.FromArgb(90, 0, 0, 0)))
                    {
                        g.FillRectangle(lockHatch, rect);
                    }
                }
            }

            // Playhead Line
            int playheadX = leftPanelWidth + (int)(currentTime * pixelsPerSecond) - scrollX;
            if (playheadX >= leftPanelWidth && playheadX <= this.Width)
            {
                using (var playheadPen = new Pen(Color.Red, 2))
                {
                    g.DrawLine(playheadPen, playheadX, 0, playheadX, this.Height);
                }
            }

            // Reset graphics clipping region
            g.Clip = originalClip;

            // 3. Pinned Time Header (Top Bar)
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

            // 4. Left Header Controls Panel: Insert Up | Delete | Lock
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

            // Audio Row Header
            if (audioY + trackHeight >= headerHeight && audioY <= this.Height)
            {
                using (var audioHeaderBrush = new SolidBrush(Color.FromArgb(15, 30, 30)))
                {
                    g.FillRectangle(audioHeaderBrush, 0, audioY, leftPanelWidth, trackHeight);
                }
                g.DrawLine(Pens.Gray, leftPanelWidth, audioY, leftPanelWidth, audioY + trackHeight);
                g.DrawString("Audio", this.Font, Brushes.DarkTurquoise, 22, audioY + 14);
            }

            // Top-Left Junction Box
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

            // 1. Playhead interaction
            if (e.Y <= headerHeight)
            {
                isDraggingPlayhead = true;
                CurrentTime = Math.Max(0, (e.X - leftPanelWidth + scrollX) / pixelsPerSecond);
                return;
            }

            int clickedTrackIndex = GetTrackIndexFromY(e.Y, MediaType.Image);
            int trackY = GetTrackY(clickedTrackIndex, MediaType.Image);

            // 2. Left Panel Track Controls
            if (e.X < leftPanelWidth)
            {
                int btnY = trackY + 12;
                int btnW = 20, btnH = 20;

                if (new Rectangle(5, btnY, btnW, btnH).Contains(e.Location)) { InsertTrackAbove(clickedTrackIndex); return; }
                if (new Rectangle(30, btnY, btnW, btnH).Contains(e.Location)) { DeleteTrackAt(clickedTrackIndex); return; }
                if (new Rectangle(55, btnY, btnW, btnH).Contains(e.Location)) { ToggleTrackLock(clickedTrackIndex); return; }
            }

            SelectedTrackIndex = clickedTrackIndex;

            // FIX: Filter items to only check the clicked track (or audio row), 
            // then sort by type priority (Text > Blur > Image) in case multiple items exist on the same row.
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

                    if (e.X >= (x + width - EdgeMargin) && e.X <= (x + width + EdgeMargin))
                    {
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

            // 4. Clicked on empty space on the timeline track
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
                CurrentTime = Math.Max(0, (e.X - leftPanelWidth + scrollX) / pixelsPerSecond);
            }
            else if (isResizingClip && activeClip != null)
            {
                double newDuration = ((e.X - leftPanelWidth + scrollX) / pixelsPerSecond) - activeClip.StartTime;
                activeClip.Duration = Math.Max(0.5, newDuration);
                ItemResized?.Invoke(activeClip);
                this.Invalidate();
            }
            else if (isDraggingClip && activeClip != null)
            {
                double newStart = ((e.X - leftPanelWidth + scrollX) / pixelsPerSecond) - clipDragOffset;
                activeClip.StartTime = Math.Max(0, newStart);

                if (activeClip.Type == MediaType.Image || activeClip.Type == MediaType.Text || activeClip.Type == MediaType.Blur)
                {
                    int newTrack = GetTrackIndexFromY(e.Y, activeClip.Type);

                    // Prevent moving clips into a locked track
                    if (activeClip.TrackIndex != newTrack && !lockedTracks.Contains(newTrack))
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

        // Toggles lock state for a track
        public void ToggleTrackLock(int trackIndex)
        {
            if (lockedTracks.Contains(trackIndex))
            {
                lockedTracks.Remove(trackIndex);
            }
            else
            {
                lockedTracks.Add(trackIndex);
            }
            this.Invalidate();
        }

        // Inserts a new empty track ABOVE the target track
        public void InsertTrackAbove(int targetTrackIndex)
        {
            foreach (var item in mediaItems.Where(x => x.Type != MediaType.Audio))
            {
                if (item.TrackIndex >= targetTrackIndex)
                {
                    item.TrackIndex++; // Shift current row and everything below it down by 1
                }
            }

            // Remap locked track indices so lock states remain aligned with rows
            var updatedLocks = new HashSet<int>();
            foreach (var locked in lockedTracks)
            {
                updatedLocks.Add(locked >= targetTrackIndex ? locked + 1 : locked);
            }
            lockedTracks = updatedLocks;

            SelectedTrackIndex = targetTrackIndex; // Set active selection to the new empty track
            this.Invalidate();
        }

        // Deletes a specific track if it contains no media clips
        public void DeleteTrackAt(int trackIndex)
        {
            if (trackIndex < 0) return;

            // Check if the target track contains any media clips
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

            // Shift track indices down for all tracks above the deleted index
            foreach (var item in mediaItems)
            {
                if (item.Type != MediaType.Audio && item.TrackIndex > trackIndex)
                {
                    item.TrackIndex--;
                }
            }

            // Remap locked track indices
            lockedTracks.Remove(trackIndex);
            var updatedLocks = new HashSet<int>();
            foreach (var locked in lockedTracks)
            {
                updatedLocks.Add(locked > trackIndex ? locked - 1 : locked);
            }
            lockedTracks = updatedLocks;

            // Ensure selection remains within valid range
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