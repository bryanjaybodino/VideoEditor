using System;
using System.Collections.Generic;
using System.Drawing;
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
        private const int EdgeMargin = 8; // Hotspot width in pixels for edge resizing

        public event Action<double> TimeChanged;
        public event Action<MediaItem> ClipSelected;

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
                g.DrawRectangle(item == SelectedItem ? new Pen(Color.Yellow, 2) : Pens.White, rect);
                g.DrawString($"{System.IO.Path.GetFileName(item.FilePath)} ({item.Duration:F1}s)", this.Font, Brushes.White, x + 5, y + 15);

                // Render Resize Handle indicators on edges
                if (item == SelectedItem)
                {
                    g.FillRectangle(Brushes.White, x + width - 4, y, 4, trackHeight);
                }
            }

            // 4. Draw Playhead
            int playheadX = (int)(currentTime * pixelsPerSecond);
            g.DrawLine(new Pen(Color.Red, 2), playheadX, 0, playheadX, this.Height);
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

                    // Check if mouse is near the right edge for resizing clip duration
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

            // Change mouse cursor over edges when hovering
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
                // Dragging right edge adjusts duration dynamically
                double newDuration = (e.X / pixelsPerSecond) - activeClip.StartTime;
                activeClip.Duration = Math.Max(0.5, newDuration); // Prevent 0-length clips
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