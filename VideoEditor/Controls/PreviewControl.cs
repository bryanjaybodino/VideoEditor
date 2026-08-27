using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
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
            // 1. Enforce 9:16 Canvas Box inside preview panel
            float targetAspect = 9.0f / 16.0f;
            int canvasWidth = this.Width;
            int canvasHeight = this.Height;

            if ((float)canvasWidth / canvasHeight > targetAspect)
            {
                canvasWidth = (int)(canvasHeight * targetAspect);
            }
            else
            {
                canvasHeight = (int)(canvasWidth / targetAspect);
            }

            int canvasX = (this.Width - canvasWidth) / 2;
            int canvasY = (this.Height - canvasHeight) / 2;

            // Fill 9:16 area maintaining aspect ratio
            float scale = Math.Max((float)canvasWidth / img.Width, (float)canvasHeight / img.Height);
            int baseW = (int)(img.Width * scale);
            int baseH = (int)(img.Height * scale);
            int originX = canvasX + (canvasWidth - baseW) / 2;
            int originY = canvasY + (canvasHeight - baseH) / 2;

            int x = originX;
            int y = originY;

            // Clip rendering strictly to 9:16 canvas frame boundary
            g.SetClip(new Rectangle(canvasX, canvasY, canvasWidth, canvasHeight));

            double localTime = timePosition - item.StartTime;
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

            // Apply Zoom Scaling
            if (zoomFactor != 1.0f)
            {
                int newW = (int)(baseW * zoomFactor);
                int newH = (int)(baseH * zoomFactor);
                x += (baseW - newW) / 2;
                y += (baseH - newH) / 2;
                baseW = newW;
                baseH = newH;
            }

            // Render Frame
            if (zoomBlurIntensity > 0)
            {
                DrawZoomBlur(g, img, x, y, baseW, baseH, zoomBlurIntensity, opacity);
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

            g.ResetClip();
            g.DrawRectangle(Pens.Gray, canvasX, canvasY, canvasWidth, canvasHeight); // Outer border overlay
        }

        private void DrawZoomBlur(Graphics g, Image img, int x, int y, int width, int height, float intensity, float baseOpacity)
        {
            int samples = 8;
            float maxScale = 1.0f + (intensity * 0.7f);
            int centerX = x + (width / 2);
            int centerY = y + (height / 2);

            for (int i = 0; i < samples; i++)
            {
                float stepProgress = (float)i / (samples - 1);
                float currentScale = 1.0f + (maxScale - 1.0f) * stepProgress;

                int stepW = (int)(width * currentScale);
                int stepH = (int)(height * currentScale);
                int stepX = centerX - (stepW / 2);
                int stepY = centerY - (stepH / 2);

                using (var attributes = new ImageAttributes())
                {
                    var matrix = new ColorMatrix { Matrix33 = Math.Max(0.0f, Math.Min(1.0f, baseOpacity / samples)) };
                    attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

                    g.DrawImage(img, new Rectangle(stepX, stepY, stepW, stepH), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, attributes);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) currentImage?.Dispose(); base.Dispose(disposing);
        }
    }
}