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
        private bool isResizingRight = false;
        private bool isResizingLeft = false;

        // Row Dragging State
        private bool isDraggingRow = false;
        private int dragSourceTrackIndex = -1;
        private int dragTargetTrackIndex = -1;

        // Multi-selection state
        private HashSet<MediaItem> selectedItems = new HashSet<MediaItem>();
        private bool isSelectionBoxActive = false;
        private Point selectionBoxStart = Point.Empty;
        private Rectangle currentSelectionBox = Rectangle.Empty;

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
        public event Action<IEnumerable<MediaItem>> SelectionChanged;
        public event Action<MediaItem> ItemResized;

        public MediaItem SelectedItem => selectedItems.FirstOrDefault();
        public IReadOnlySet<MediaItem> SelectedItems => selectedItems;
        private Dictionary<MediaItem, (double StartTime, int TrackIndex)> initialItemStates = new Dictionary<MediaItem, (double StartTime, int TrackIndex)>();
        private const double SnapThresholdPixels = 10;
        private const double BreakoutThresholdPixels = 25;
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
            this.KeyDown += Timeline_KeyDown;
            this.Resize += (s, e) => UpdateScrollBars();

            this.SetStyle(ControlStyles.Selectable, true);
            this.TabStop = true;
        }


        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Extract key code without modifiers
            Keys keyCode = keyData & Keys.KeyCode;

            if (keyCode == Keys.Left || keyCode == Keys.Right || keyCode == Keys.Up || keyCode == Keys.Down)
            {
                // Adjust movement speed: Shift increases horizontal step time
                double timeStep = (keyData & Keys.Shift) == Keys.Shift ? 0.5 : 0.1; // in seconds
                int trackDelta = 0;
                double timeDelta = 0;

                switch (keyCode)
                {
                    case Keys.Left: timeDelta = -timeStep; break;
                    case Keys.Right: timeDelta = timeStep; break;
                    case Keys.Up: trackDelta = -1; break;
                    case Keys.Down: trackDelta = 1; break;
                }

                if (NudgeSelectedClips(timeDelta, trackDelta))
                {
                    return true; // Key handled
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }
        private bool NudgeSelectedClips(double timeDelta, int trackDelta)
        {
            if (selectedItems.Count == 0) return false;

            var moves = new List<(MediaItem Item, double OldStart, double NewStart, int OldTrack, int NewTrack)>();

            foreach (var item in selectedItems)
            {
                if (lockedTracks.Contains(item.TrackIndex) && item.Type != MediaType.Audio)
                    continue;

                double oldStart = item.StartTime;
                double newStart = Math.Max(0, oldStart + timeDelta);

                int oldTrack = item.TrackIndex;
                int newTrack = oldTrack;

                // Apply track movement only for visual items (Audio stays on audio track)
                if (item.Type == MediaType.Image || item.Type == MediaType.Text || item.Type == MediaType.Blur)
                {
                    newTrack = Math.Max(0, oldTrack + trackDelta);
                    if (lockedTracks.Contains(newTrack))
                    {
                        newTrack = oldTrack; // Skip moving to a locked track
                    }
                }

                if (Math.Abs(newStart - oldStart) > 0.0001 || newTrack != oldTrack)
                {
                    item.StartTime = newStart;
                    item.TrackIndex = newTrack;
                    moves.Add((item, oldStart, newStart, oldTrack, newTrack));
                }
            }

            if (moves.Count > 0)
            {
                var cmd = new MoveClipCommand(moves);
                UndoRedoManager?.ExecuteCommand(cmd);

                UpdateScrollBars();
                NotifySelectionChanged();
                this.Invalidate();
                return true;
            }

            return false;
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
            selectedItems.Clear();
            UpdateScrollBars();
            this.Invalidate();
        }

        public void DeleteSelectedItems()
        {
            if (selectedItems.Count == 0) return;

            var itemsToDelete = selectedItems.ToList();
            selectedItems.Clear();

            foreach (var item in itemsToDelete)
            {
                if (lockedTracks.Contains(item.TrackIndex) && item.Type != MediaType.Audio)
                    continue;

                var cmd = new DeleteMediaItemCommand(mediaItems, item);
                UndoRedoManager?.ExecuteCommand(cmd);
            }

            NotifySelectionChanged();
            this.Invalidate();
        }

        private void Timeline_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                DeleteSelectedItems();
            }
        }

        private void NotifySelectionChanged()
        {
            ClipSelected?.Invoke(SelectedItem);
            SelectionChanged?.Invoke(selectedItems);
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

        private Rectangle GetClipRectangle(MediaItem item, int leftPanelWidth)
        {
            int y = GetTrackY(item.TrackIndex, item.Type);
            int x = leftPanelWidth + (int)(item.StartTime * pixelsPerSecond) - scrollX;
            int width = (int)(item.Duration * pixelsPerSecond);
            return new Rectangle(x, y, Math.Max(width, 15), trackHeight);
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

        public void MoveRowContent(int sourceTrackIndex, int targetTrackIndex)
        {
            if (sourceTrackIndex == targetTrackIndex) return;

            var command = new MoveTrackRowCommand(mediaItems, sourceTrackIndex, targetTrackIndex);
            UndoRedoManager?.ExecuteCommand(command);

            SelectedTrackIndex = targetTrackIndex;
            this.Invalidate();
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
                if (isDraggingRow && i == dragTargetTrackIndex)
                {
                    rowBgColor = Color.FromArgb(60, 80, 110);
                }

                using (var bgBrush = new SolidBrush(rowBgColor))
                {
                    g.FillRectangle(bgBrush, leftPanelWidth, trackY, this.Width - leftPanelWidth, trackHeight);
                }

                if (isRowSelected || (isDraggingRow && i == dragTargetTrackIndex))
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
                var rect = GetClipRectangle(item, leftPanelWidth);
                if (rect.Bottom < headerHeight || rect.Top > this.Height || rect.Right < leftPanelWidth || rect.Left > this.Width) continue;

                bool isSelected = selectedItems.Contains(item);

                var color = Color.SteelBlue;
                if (item.Type == MediaType.Audio) color = Color.FromArgb(30, 70, 70);
                else if (item.Type == MediaType.Text) color = Color.DarkGoldenrod;
                else if (item.Type == MediaType.Blur) color = Color.Purple;

                if (isSelected) color = Color.Crimson;

                using (var clipBrush = new SolidBrush(color))
                {
                    g.FillRectangle(clipBrush, rect);
                }

                if (isSelected)
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

                if (lockedTracks.Contains(item.TrackIndex) && item.Type != MediaType.Audio)
                {
                    using (var lockHatch = new SolidBrush(Color.FromArgb(90, 0, 0, 0)))
                    {
                        g.FillRectangle(lockHatch, rect);
                    }
                }
            }

            if (isSelectionBoxActive && currentSelectionBox.Width > 0 && currentSelectionBox.Height > 0)
            {
                using (var fillBrush = new SolidBrush(Color.FromArgb(40, 0, 122, 204)))
                using (var borderPen = new Pen(Color.FromArgb(0, 122, 204), 1))
                {
                    g.FillRectangle(fillBrush, currentSelectionBox);
                    g.DrawRectangle(borderPen, currentSelectionBox);
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
                if (isDraggingRow && i == dragTargetTrackIndex)
                {
                    leftBgColor = Color.FromArgb(60, 80, 110);
                }

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
            this.Focus();
            int leftPanelWidth = 80;

            if (isDraggingPlayhead || isDraggingClip || isResizingRight || isResizingLeft || isSelectionBoxActive || isDraggingRow) return;

            if (e.Y <= headerHeight)
            {
                isDraggingPlayhead = true;
                CurrentTime = Math.Max(0, (e.X - leftPanelWidth + scrollX) / pixelsPerSecond);
                return;
            }

            int clickedTrackIndex = GetTrackIndexFromY(e.Y, MediaType.Image);
            int trackY = GetTrackY(clickedTrackIndex, MediaType.Image);

            if (e.X < leftPanelWidth && e.Y > headerHeight)
            {
                int btnY = trackY + 12;
                int btnW = 20, btnH = 20;

                if (new Rectangle(5, btnY, btnW, btnH).Contains(e.Location)) { InsertTrackAbove(clickedTrackIndex); return; }
                if (new Rectangle(30, btnY, btnW, btnH).Contains(e.Location)) { DeleteTrackAt(clickedTrackIndex); return; }
                if (new Rectangle(55, btnY, btnW, btnH).Contains(e.Location)) { ToggleTrackLock(clickedTrackIndex); return; }

                isDraggingRow = true;
                dragSourceTrackIndex = clickedTrackIndex;
                dragTargetTrackIndex = clickedTrackIndex;
                SelectedTrackIndex = clickedTrackIndex;
                this.Invalidate();
                return;
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

            bool ctrlPressed = ModifierKeys.HasFlag(Keys.Control);

            foreach (var item in hitTestOrder)
            {
                var rect = GetClipRectangle(item, leftPanelWidth);

                if (rect.Contains(e.Location))
                {
                    if (lockedTracks.Contains(item.TrackIndex) && item.Type != MediaType.Audio) continue;

                    if (ctrlPressed)
                    {
                        if (selectedItems.Contains(item)) selectedItems.Remove(item);
                        else selectedItems.Add(item);
                    }
                    else
                    {
                        if (!selectedItems.Contains(item))
                        {
                            selectedItems.Clear();
                            selectedItems.Add(item);
                        }
                    }

                    SelectedTrackIndex = item.TrackIndex;
                    activeClip = item;
                    initialClipStartTime = item.StartTime;
                    initialClipTrackIndex = item.TrackIndex;
                    initialClipDuration = item.Duration;

                    // Capture initial positions for all selected items for undoing multi-clip moves
                    initialItemStates.Clear();
                    foreach (var selected in selectedItems)
                    {
                        initialItemStates[selected] = (selected.StartTime, selected.TrackIndex);
                    }

                    if (e.X >= (rect.X - EdgeMargin) && e.X <= (rect.X + EdgeMargin))
                    {
                        isSnappedToBoundary = false;
                        isResizingLeft = true;
                    }
                    else if (e.X >= (rect.Right - EdgeMargin) && e.X <= (rect.Right + EdgeMargin))
                    {
                        isSnappedToBoundary = false;
                        isResizingRight = true;
                    }
                    else
                    {
                        isDraggingClip = true;
                        clipDragOffset = ((e.X - leftPanelWidth + scrollX) / pixelsPerSecond) - item.StartTime;
                    }

                    NotifySelectionChanged();
                    this.Invalidate();
                    return;
                }
            }

            if (!ctrlPressed)
            {
                selectedItems.Clear();
                NotifySelectionChanged();
            }

            isSelectionBoxActive = true;
            selectionBoxStart = e.Location;
            currentSelectionBox = new Rectangle(e.X, e.Y, 0, 0);

            CurrentTime = Math.Max(0, (e.X - leftPanelWidth + scrollX) / pixelsPerSecond);
            this.Invalidate();
        }

        private void Timeline_MouseMove(object sender, MouseEventArgs e)
        {
            int leftPanelWidth = 80;

            if (isDraggingRow)
            {
                this.Cursor = Cursors.SizeAll;
                int hoverTrack = GetTrackIndexFromY(e.Y, MediaType.Image);
                if (hoverTrack != dragTargetTrackIndex)
                {
                    dragTargetTrackIndex = hoverTrack;
                    this.Invalidate();
                }
                return;
            }

            if (isSelectionBoxActive)
            {
                this.Cursor = Cursors.Cross;

                int x = Math.Min(selectionBoxStart.X, e.X);
                int y = Math.Min(selectionBoxStart.Y, e.Y);
                int width = Math.Abs(e.X - selectionBoxStart.X);
                int height = Math.Abs(e.Y - selectionBoxStart.Y);

                currentSelectionBox = new Rectangle(x, y, width, height);

                bool ctrlPressed = ModifierKeys.HasFlag(Keys.Control);
                if (!ctrlPressed) selectedItems.Clear();

                foreach (var item in mediaItems)
                {
                    if (lockedTracks.Contains(item.TrackIndex) && item.Type != MediaType.Audio) continue;

                    var itemRect = GetClipRectangle(item, leftPanelWidth);
                    if (currentSelectionBox.IntersectsWith(itemRect))
                    {
                        selectedItems.Add(item);
                    }
                }

                NotifySelectionChanged();
                this.Invalidate();
                return;
            }

            if (isDraggingPlayhead)
            {
                this.Cursor = Cursors.Default;
                CurrentTime = Math.Max(0, (e.X - leftPanelWidth + scrollX) / pixelsPerSecond);
            }
            else if (isResizingLeft && activeClip != null)
            {
                this.Cursor = Cursors.SizeWE;

                double rawTime = (e.X - leftPanelWidth + scrollX) / pixelsPerSecond;
                double clipEndTime = initialClipStartTime + initialClipDuration;
                double candidateStartTime = Math.Clamp(rawTime, 0, clipEndTime - 0.5);

                var snapCandidates = mediaItems
                    .Where(item => item != activeClip &&
                                   Math.Abs(item.TrackIndex - activeClip.TrackIndex) <= 1)
                    .SelectMany(item => new[] { item.StartTime, item.StartTime + item.Duration });

                double? closestSnapTime = null;
                double minDistance = double.MaxValue;

                foreach (double targetTime in snapCandidates)
                {
                    if (targetTime >= clipEndTime) continue;

                    double distance = Math.Abs((candidateStartTime - targetTime) * pixelsPerSecond);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestSnapTime = targetTime;
                    }
                }

                if (closestSnapTime.HasValue)
                {
                    double pixelDiff = (candidateStartTime - closestSnapTime.Value) * pixelsPerSecond;

                    if (!isSnappedToBoundary)
                    {
                        if (Math.Abs(pixelDiff) <= SnapThresholdPixels)
                        {
                            isSnappedToBoundary = true;
                            targetSnapTime = closestSnapTime.Value;
                            candidateStartTime = targetSnapTime;
                        }
                    }
                    else
                    {
                        if (Math.Abs((rawTime - targetSnapTime) * pixelsPerSecond) > BreakoutThresholdPixels)
                        {
                            isSnappedToBoundary = false;
                        }
                        else
                        {
                            candidateStartTime = targetSnapTime;
                        }
                    }
                }
                else
                {
                    isSnappedToBoundary = false;
                }

                activeClip.StartTime = candidateStartTime;
                activeClip.Duration = clipEndTime - candidateStartTime;
                this.Invalidate();
            }
            else if (isResizingRight && activeClip != null)
            {
                this.Cursor = Cursors.SizeWE;

                double rawDuration = ((e.X - leftPanelWidth + scrollX) / pixelsPerSecond) - activeClip.StartTime;
                double candidateDuration = Math.Max(0.5, rawDuration);
                double candidateEndTime = activeClip.StartTime + candidateDuration;

                var snapCandidates = mediaItems
                    .Where(item => item != activeClip &&
                                   Math.Abs(item.TrackIndex - activeClip.TrackIndex) <= 1)
                    .SelectMany(item => new[] { item.StartTime, item.StartTime + item.Duration });

                double? closestSnapTime = null;
                double minDistance = double.MaxValue;

                foreach (double targetTime in snapCandidates)
                {
                    if (targetTime <= activeClip.StartTime) continue;

                    double distance = Math.Abs((candidateEndTime - targetTime) * pixelsPerSecond);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestSnapTime = targetTime;
                    }
                }

                if (closestSnapTime.HasValue)
                {
                    double pixelDiff = (candidateEndTime - closestSnapTime.Value) * pixelsPerSecond;

                    if (!isSnappedToBoundary)
                    {
                        if (Math.Abs(pixelDiff) <= SnapThresholdPixels)
                        {
                            isSnappedToBoundary = true;
                            targetSnapTime = closestSnapTime.Value;
                            candidateDuration = Math.Max(0.5, targetSnapTime - activeClip.StartTime);
                        }
                    }
                    else
                    {
                        double rawEndTime = activeClip.StartTime + rawDuration;
                        if (Math.Abs((rawEndTime - targetSnapTime) * pixelsPerSecond) > BreakoutThresholdPixels)
                        {
                            isSnappedToBoundary = false;
                        }
                        else
                        {
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
                double deltaStart = Math.Max(0, newStart) - activeClip.StartTime;

                int trackDelta = 0;
                if (activeClip.Type == MediaType.Image || activeClip.Type == MediaType.Text || activeClip.Type == MediaType.Blur)
                {
                    int newTrack = GetTrackIndexFromY(e.Y, activeClip.Type);
                    trackDelta = newTrack - activeClip.TrackIndex;
                }

                foreach (var item in selectedItems)
                {
                    item.StartTime = Math.Max(0, item.StartTime + deltaStart);

                    if (item.Type == MediaType.Image || item.Type == MediaType.Text || item.Type == MediaType.Blur)
                    {
                        int targetTrack = Math.Max(0, item.TrackIndex + trackDelta);
                        if (!lockedTracks.Contains(targetTrack))
                        {
                            item.TrackIndex = targetTrack;
                        }
                    }
                }

                UpdateScrollBars();
                this.Invalidate();
            }
            else
            {
                bool isOverResizeEdge = false;
                int hoveredTrackIndex = GetTrackIndexFromY(e.Y, MediaType.Image);
                bool isAudioRowHovered = (e.Y >= GetTrackY(0, MediaType.Audio));

                // 1. Check if hovering over the Left Control Panel for Row Dragging
                if (e.X < leftPanelWidth && e.Y > headerHeight && !isAudioRowHovered)
                {
                    int trackY = GetTrackY(hoveredTrackIndex, MediaType.Image);
                    int btnY = trackY + 12;
                    int btnW = 20, btnH = 20;

                    // Exclude action buttons (Up, Delete, Lock)
                    bool isOverButton = new Rectangle(5, btnY, btnW, btnH).Contains(e.Location) ||
                                        new Rectangle(30, btnY, btnW, btnH).Contains(e.Location) ||
                                        new Rectangle(55, btnY, btnW, btnH).Contains(e.Location);

                    if (!isOverButton)
                    {
                        // Show hand/move cursor to indicate the row can be dragged
                        this.Cursor = Cursors.SizeAll;
                        return;
                    }
                }

                // 2. Check if hovering over clip edges for resizing
                var hoveredItems = mediaItems
                    .Where(item => isAudioRowHovered
                        ? item.Type == MediaType.Audio
                        : item.TrackIndex == hoveredTrackIndex && item.Type != MediaType.Audio);

                foreach (var item in hoveredItems)
                {
                    if (lockedTracks.Contains(item.TrackIndex) && item.Type != MediaType.Audio) continue;

                    var rect = GetClipRectangle(item, leftPanelWidth);

                    if (rect.Contains(e.Location))
                    {
                        if ((e.X >= (rect.X - EdgeMargin) && e.X <= (rect.X + EdgeMargin)) ||
                            (e.X >= (rect.Right - EdgeMargin) && e.X <= (rect.Right + EdgeMargin)))
                        {
                            isOverResizeEdge = true;
                            break;
                        }
                    }
                }

                this.Cursor = isOverResizeEdge ? Cursors.SizeWE : Cursors.Default;
            }
        }

        private void Timeline_MouseUp(object sender, MouseEventArgs e)
        {
            if (isDraggingRow)
            {
                isDraggingRow = false;
                this.Cursor = Cursors.Default;

                if (dragSourceTrackIndex != -1 && dragTargetTrackIndex != -1 && dragSourceTrackIndex != dragTargetTrackIndex)
                {
                    MoveRowContent(dragSourceTrackIndex, dragTargetTrackIndex);
                }

                dragSourceTrackIndex = -1;
                dragTargetTrackIndex = -1;
                this.Invalidate();
                return;
            }

            if (isSelectionBoxActive)
            {
                isSelectionBoxActive = false;
                currentSelectionBox = Rectangle.Empty;
                this.Cursor = Cursors.Default;
                this.Invalidate();
                return;
            }

            if ((isResizingRight || isResizingLeft) && activeClip != null)
            {
                if (Math.Abs(activeClip.Duration - initialClipDuration) > 0.001 || Math.Abs(activeClip.StartTime - initialClipStartTime) > 0.001)
                {
                    if (Math.Abs(activeClip.StartTime - initialClipStartTime) > 0.001)
                    {
                        var moveCmd = new MoveClipCommand(activeClip, initialClipStartTime, activeClip.StartTime, initialClipTrackIndex, activeClip.TrackIndex);
                        UndoRedoManager?.ExecuteCommand(moveCmd);
                    }

                    if (Math.Abs(activeClip.Duration - initialClipDuration) > 0.001)
                    {
                        var durationCmd = new ChangeDurationCommand(activeClip, initialClipDuration, activeClip.Duration);
                        UndoRedoManager?.ExecuteCommand(durationCmd);
                    }

                    ItemResized?.Invoke(activeClip);
                }
            }
            else if (isDraggingClip && activeClip != null)
            {
                var moves = new List<(MediaItem Item, double OldStart, double NewStart, int OldTrack, int NewTrack)>();

                foreach (var item in selectedItems)
                {
                    if (initialItemStates.TryGetValue(item, out var initialState))
                    {
                        if (Math.Abs(item.StartTime - initialState.StartTime) > 0.001 || item.TrackIndex != initialState.TrackIndex)
                        {
                            moves.Add((item, initialState.StartTime, item.StartTime, initialState.TrackIndex, item.TrackIndex));
                        }
                    }
                }

                if (moves.Count > 0)
                {
                    var cmd = new MoveClipCommand(moves);
                    UndoRedoManager?.ExecuteCommand(cmd);
                }

                initialItemStates.Clear();
            }

            isSnappedToBoundary = false;
            isDraggingPlayhead = false;
            isDraggingClip = false;
            isResizingRight = false;
            isResizingLeft = false;
            activeClip = null;
            this.Cursor = Cursors.Default;
            this.Invalidate();
        }
        public void SelectItem(MediaItem item)
        {
            selectedItems.Clear();
            if (item != null)
            {
                selectedItems.Add(item);
                SelectedTrackIndex = item.TrackIndex;
            }
            NotifySelectionChanged();
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