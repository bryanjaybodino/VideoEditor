using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using VideoEditor.Models;

namespace VideoEditor.Controls
{
    public class PreviewControl : Control
    {
        private MediaItem currentItem;
        private Image currentImage;
        private double currentTimePosition = 0;

        public PreviewControl()
        {
            this.DoubleBuffered = true;
            this.BackColor = Color.FromArgb(15, 15, 15);
        }

        public void RenderFrame(List<MediaItem> items, double timePosition)
        {
            currentTimePosition = timePosition;

            var activeItem = items.FirstOrDefault(item =>
                item.Type == MediaType.Image &&
                timePosition >= item.StartTime &&
                timePosition < item.StartTime + item.Duration);

            if (currentItem != activeItem)
            {
                currentItem = activeItem;
                currentImage?.Dispose();
                currentImage = null;

                if (currentItem != null && File.Exists(currentItem.FilePath))
                {
                    using (var temp = Image.FromFile(currentItem.FilePath))
                    {
                        currentImage = new Bitmap(temp);
                    }
                }
            }

            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.Clear(this.BackColor);

            if (currentImage != null && currentItem != null)
            {
                DrawAnimatedImage(g, currentImage, currentItem, currentTimePosition);

                // Draw Text Overlays
                foreach (var label in currentItem.TextLabels)
                {
                    using (var font = new Font(label.FontFamily, label.FontSize, label.IsBold ? FontStyle.Bold : FontStyle.Regular))
                    using (var brush = new SolidBrush(label.Color))
                    {
                        g.DrawString(label.Content, font, brush, label.X, label.Y);
                    }
                }
            }
            else
            {
                using (var font = new Font("Segoe UI", 12))
                using (var brush = new SolidBrush(Color.Gray))
                {
                    var msg = "No Active Media Frame";
                    var sz = g.MeasureString(msg, font);
                    g.DrawString(msg, font, brush, (this.Width - sz.Width) / 2, (this.Height - sz.Height) / 2);
                }
            }
        }

        private void DrawAnimatedImage(Graphics g, Image img, MediaItem item, double timePosition)
        {
            double localTime = timePosition - item.StartTime;
            float scale = Math.Min((float)this.Width / img.Width, (float)this.Height / img.Height);
            int baseW = (int)(img.Width * scale);
            int baseH = (int)(img.Height * scale);
            int x = (this.Width - baseW) / 2;
            int y = (this.Height - baseH) / 2;

            double transitionDuration = item.InEffect?.Duration ?? 0.5;

            // --- CapCut In-Effect: Slide In From Left ---
            if (item.InEffect?.Type == "Slide" && localTime < transitionDuration)
            {
                float progress = (float)(localTime / transitionDuration);
                x = (int)(-baseW + (x + baseW) * progress);
            }
            // --- CapCut In-Effect: Wave Wobble ---
            else if (item.InEffect?.Type == "Wave" && localTime < transitionDuration)
            {
                float progress = (float)(localTime / transitionDuration);
                float waveOffset = (float)Math.Sin(progress * Math.PI * 4) * 15;
                y += (int)waveOffset;
            }

            g.DrawImage(img, x, y, baseW, baseH);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) currentImage?.Dispose();
            base.Dispose(disposing);
        }
    }
}