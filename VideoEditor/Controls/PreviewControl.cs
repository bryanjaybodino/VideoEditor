using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using VideoEditor.Models;

namespace VideoEditor.Controls
{
    public class PreviewControl : Control
    {
        private List<MediaItem> activeFrameItems = new List<MediaItem>();
        private Dictionary<string, Image> imageCache = new Dictionary<string, Image>();
        private double currentTimePosition = 0;

        private MediaItem selectedPreviewItem = null;
        private bool isDraggingImage = false;
        private Point lastMousePos;

        public int LastCanvasWidth { get; private set; } = 1080;
        public int LastCanvasHeight { get; private set; } = 1920;

        public MediaItem SelectedItem { get; set; }

        public event Action ItemTransformChanged;

        public PreviewControl()
        {
            this.DoubleBuffered = true;
            this.BackColor = Color.FromArgb(15, 15, 15);

            this.MouseDown += PreviewControl_MouseDown;
            this.MouseMove += PreviewControl_MouseMove;
            this.MouseUp += PreviewControl_MouseUp;
            this.MouseWheel += PreviewControl_MouseWheel;
        }

        public void RenderFrame(List<MediaItem> items, double timePosition)
        {
            currentTimePosition = timePosition;

            activeFrameItems = items
                .Where(item => item.Type == MediaType.Image &&
                               timePosition >= item.StartTime &&
                               timePosition < item.StartTime + item.Duration)
                .OrderByDescending(item => item.TrackIndex)
                .ToList();

            foreach (var item in activeFrameItems)
            {
                if (!imageCache.ContainsKey(item.FilePath) && File.Exists(item.FilePath))
                {
                    using (var temp = Image.FromFile(item.FilePath))
                    {
                        imageCache[item.FilePath] = new Bitmap(temp);
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

            float targetAspect = 9.0f / 16.0f;
            int canvasWidth = this.Width;
            int canvasHeight = this.Height;

            if ((float)canvasWidth / canvasHeight > targetAspect)
                canvasWidth = (int)(canvasHeight * targetAspect);
            else
                canvasHeight = (int)(canvasWidth / targetAspect);

            LastCanvasWidth = canvasWidth;
            LastCanvasHeight = canvasHeight;

            int canvasX = (this.Width - canvasWidth) / 2;
            int canvasY = (this.Height - canvasHeight) / 2;

            g.SetClip(new Rectangle(canvasX, canvasY, canvasWidth, canvasHeight));

            if (activeFrameItems.Count > 0)
            {
                foreach (var item in activeFrameItems)
                {
                    if (imageCache.TryGetValue(item.FilePath, out Image img) && img != null)
                    {
                        DrawTransformedImage(g, img, item, canvasX, canvasY, canvasWidth, canvasHeight);
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

            g.ResetClip();
            g.DrawRectangle(Pens.Gray, canvasX, canvasY, canvasWidth, canvasHeight);
        }

        private void DrawTransformedImage(Graphics g, Image img, MediaItem item, int canvasX, int canvasY, int canvasWidth, int canvasHeight)
        {
            float scale = Math.Max((float)canvasWidth / img.Width, (float)canvasHeight / img.Height) * item.Scale;
            int baseW = (int)(img.Width * scale);
            int baseH = (int)(img.Height * scale);

            int originX = canvasX + (canvasWidth - baseW) / 2 + (int)item.PositionX;
            int originY = canvasY + (canvasHeight - baseH) / 2 + (int)item.PositionY;

            int x = originX;
            int y = originY;

            double localTime = currentTimePosition - item.StartTime;
            double remainingTime = item.Duration - localTime;

            float opacity = 1.0f;
            float zoomFactor = 1.0f;
            float zoomBlurIntensity = 0.0f;

            // --- IN ANIMATION ---
            double inDur = item.InEffect?.Duration ?? 0;
            if (localTime >= 0 && localTime < inDur && inDur > 0 && item.InEffect != null)
            {
                float progress = Math.Max(0.0f, Math.Min(1.0f, (float)(localTime / inDur)));
                float invertProgress = 1.0f - progress;

                switch (item.InEffect.Type)
                {
                    case "Fade":
                        opacity *= progress;
                        break;
                    case "Slide":
                        x = (int)(originX - canvasWidth + (canvasWidth * progress));
                        break;
                    case "Wave":
                        y += (int)(Math.Sin(progress * Math.PI * 4) * 15);
                        break;
                    case "Zoom":
                        zoomFactor *= (0.5f + 0.5f * progress);
                        break;
                    case "ZoomBlur":
                        zoomBlurIntensity = Math.Max(zoomBlurIntensity, invertProgress);
                        break;
                    case "ZoomBlurUp":
                        zoomBlurIntensity = Math.Max(zoomBlurIntensity, invertProgress);
                        y -= (int)(canvasHeight * invertProgress);
                        break;
                    case "ZoomBlurDown":
                        zoomBlurIntensity = Math.Max(zoomBlurIntensity, invertProgress);
                        y += (int)(canvasHeight * invertProgress);
                        break;
                    case "ZoomBlurLeft":
                        zoomBlurIntensity = Math.Max(zoomBlurIntensity, invertProgress);
                        x -= (int)(canvasWidth * invertProgress);
                        break;
                    case "ZoomBlurRight":
                        zoomBlurIntensity = Math.Max(zoomBlurIntensity, invertProgress);
                        x += (int)(canvasWidth * invertProgress);
                        break;
                }
            }

            // --- OUT ANIMATION ---
            double outDur = item.OutEffect?.Duration ?? 0;
            if (remainingTime >= 0 && remainingTime < outDur && outDur > 0 && item.OutEffect != null)
            {
                float progress = Math.Max(0.0f, Math.Min(1.0f, (float)(remainingTime / outDur)));
                float invertProgress = 1.0f - progress;

                switch (item.OutEffect.Type)
                {
                    case "Fade":
                        opacity *= progress;
                        break;
                    case "Slide":
                        x += (int)(canvasWidth * invertProgress);
                        break;
                    case "Wave":
                        y += (int)(Math.Sin(invertProgress * Math.PI * 4) * 15);
                        break;
                    case "Zoom":
                        zoomFactor *= (1.0f + 0.5f * invertProgress);
                        break;
                    case "ZoomBlur":
                        zoomBlurIntensity = Math.Max(zoomBlurIntensity, invertProgress);
                        break;
                    case "ZoomBlurUp":
                        zoomBlurIntensity = Math.Max(zoomBlurIntensity, invertProgress);
                        y -= (int)(canvasHeight * invertProgress);
                        break;
                    case "ZoomBlurDown":
                        zoomBlurIntensity = Math.Max(zoomBlurIntensity, invertProgress);
                        y += (int)(canvasHeight * invertProgress);
                        break;
                    case "ZoomBlurLeft":
                        zoomBlurIntensity = Math.Max(zoomBlurIntensity, invertProgress);
                        x -= (int)(canvasWidth * invertProgress);
                        break;
                    case "ZoomBlurRight":
                        zoomBlurIntensity = Math.Max(zoomBlurIntensity, invertProgress);
                        x += (int)(canvasWidth * invertProgress);
                        break;
                }
            }

            if (zoomFactor != 1.0f)
            {
                int newW = (int)(baseW * zoomFactor);
                int newH = (int)(baseH * zoomFactor);
                x += (baseW - newW) / 2;
                y += (baseH - newH) / 2;
                baseW = newW;
                baseH = newH;
            }

            if (zoomBlurIntensity > 0)
            {
                int samples = 8;
                float maxScale = 1.0f + (zoomBlurIntensity * 0.7f);
                int centerX = x + (baseW / 2);
                int centerY = y + (baseH / 2);

                for (int i = 0; i < samples; i++)
                {
                    float stepProgress = (float)i / (samples - 1);
                    float currentScale = 1.0f + (maxScale - 1.0f) * stepProgress;

                    int stepW = (int)(baseW * currentScale);
                    int stepH = (int)(baseH * currentScale);
                    int stepX = centerX - (stepW / 2);
                    int stepY = centerY - (stepH / 2);

                    using (var attributes = new ImageAttributes())
                    {
                        var matrix = new ColorMatrix { Matrix33 = Math.Max(0.0f, Math.Min(1.0f, opacity / samples)) };
                        attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                        g.DrawImage(img, new Rectangle(stepX, stepY, stepW, stepH), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, attributes);
                    }
                }
            }
            else if (opacity < 0.99f)
            {
                using (var attributes = new ImageAttributes())
                {
                    var matrix = new ColorMatrix { Matrix33 = Math.Max(0.0f, Math.Min(1.0f, opacity)) };
                    attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                    g.DrawImage(img, new Rectangle(x, y, baseW, baseH), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, attributes);
                }
            }
            else
            {
                g.DrawImage(img, x, y, baseW, baseH);
            }

            if (item == SelectedItem)
            {
                using (Pen selectPen = new Pen(Color.Cyan, 2) { DashStyle = DashStyle.Dash })
                {
                    g.DrawRectangle(selectPen, x, y, baseW, baseH);
                }
            }
        }

        private void PreviewControl_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && activeFrameItems.Count > 0)
            {
                selectedPreviewItem = activeFrameItems.LastOrDefault();
                if (selectedPreviewItem != null)
                {
                    isDraggingImage = true;
                    lastMousePos = e.Location;
                }
            }
        }

        private void PreviewControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDraggingImage && selectedPreviewItem != null)
            {
                int deltaX = e.X - lastMousePos.X;
                int deltaY = e.Y - lastMousePos.Y;

                selectedPreviewItem.PositionX += deltaX;
                selectedPreviewItem.PositionY += deltaY;

                lastMousePos = e.Location;
                ItemTransformChanged?.Invoke();
                this.Invalidate();
            }
        }

        private void PreviewControl_MouseUp(object sender, MouseEventArgs e)
        {
            isDraggingImage = false;
        }

        private void PreviewControl_MouseWheel(object sender, MouseEventArgs e)
        {
            var topItem = activeFrameItems.LastOrDefault();
            if (topItem != null)
            {
                float zoomDelta = e.Delta > 0 ? 1.05f : 0.95f;
                topItem.Scale = Math.Clamp(topItem.Scale * zoomDelta, 0.1f, 5.0f);

                ItemTransformChanged?.Invoke();
                this.Invalidate();
            }
        }
    }
}