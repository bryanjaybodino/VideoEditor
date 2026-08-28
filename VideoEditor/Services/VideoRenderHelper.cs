using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using VideoEditor.Models;

namespace VideoEditor.Services
{
    public static class VideoRenderHelper
    {
        public static void DrawImageItem(Graphics g, Image img, MediaItem item, double currentTime, int canvasX, int canvasY, int canvasWidth, int canvasHeight)
        {
            if (img == null || item == null) return;

            double localTime = currentTime - item.StartTime;
            if (localTime < 0 || localTime > item.Duration) return;

            // Transform state
            float opacity = 1.0f, scale = 1.0f, offsetX = 0f, offsetY = 0f;
            float blurAmount = 0f, blurAngleX = 0f, blurAngleY = 0f, waveAmount = 0f;

            // Apply In / Out Animations
            if (localTime < item.InEffectDuration && item.InEffectDuration > 0)
                ApplyEffect(item.InEffectType, (float)(localTime / item.InEffectDuration), true, ref opacity, ref scale, ref offsetX, ref offsetY, ref blurAmount, ref blurAngleX, ref blurAngleY, ref waveAmount, canvasWidth, canvasHeight);

            double timeRemaining = item.Duration - localTime;
            if (timeRemaining < item.OutEffectDuration && item.OutEffectDuration > 0)
                ApplyEffect(item.OutEffectType, (float)(timeRemaining / item.OutEffectDuration), false, ref opacity, ref scale, ref offsetX, ref offsetY, ref blurAmount, ref blurAngleX, ref blurAngleY, ref waveAmount, canvasWidth, canvasHeight);

            // Bounds Calculation (REPLACE OLD SCALING LINES HERE)
            float scaleX = (float)canvasWidth / img.Width;
            float scaleY = (float)canvasHeight / img.Height;
            float finalScale = Math.Max(scaleX, scaleY) * item.Scale * scale;
            int w = (int)(img.Width * finalScale);
            int h = (int)(img.Height * finalScale);

            float posX = (item.PositionX * (canvasWidth / 1080f)) + offsetX + (waveAmount * (float)Math.Sin(localTime * 10));
            float posY = (item.PositionY * (canvasHeight / 1920f)) + offsetY;
            int x = canvasX + (canvasWidth - w) / 2 + (int)posX;
            int y = canvasY + (canvasHeight - h) / 2 + (int)posY;

            Rectangle destRect = new Rectangle(x, y, w, h);

            // Render Blur Passes
            if (blurAmount > 0.05f)
            {
                int steps = 6;
                float stepAlpha = (opacity / steps) * 0.7f;
                for (int i = steps; i >= 1; i--)
                {
                    float factor = (i / (float)steps) * blurAmount;
                    int bw = (int)(w * (1f + factor * 0.15f)), bh = (int)(h * (1f + factor * 0.15f));
                    int bx = x - (bw - w) / 2 + (int)(blurAngleX * factor * 20);
                    int by = y - (bh - h) / 2 + (int)(blurAngleY * factor * 20);

                    DrawWithAlpha(g, img, new Rectangle(bx, by, bw, bh), stepAlpha);
                }
            }

            DrawWithAlpha(g, img, destRect, opacity);
        }

        private static void ApplyEffect(string type, float progress, bool isIn, ref float opacity, ref float scale, ref float offsetX, ref float offsetY, ref float blur, ref float blurX, ref float blurY, ref float wave, int cw, int ch)
        {
            if (string.IsNullOrWhiteSpace(type) || type.Equals("None", StringComparison.OrdinalIgnoreCase)) return;

            progress = Math.Clamp(progress, 0f, 1f);
            opacity *= progress;
            float inv = 1f - progress;

            switch (type.Trim().ToLowerInvariant())
            {
                case "fade": break;
                case "slide": offsetX += inv * cw * (isIn ? -0.5f : 0.5f); break;
                case "wave": wave += inv * 30f; break;
                case "zoom": scale *= (0.3f + 0.7f * progress); break;
                case "zoomblur": scale *= (0.3f + 0.7f * progress); blur += inv * 2f; break;
                case "zoomblurup": scale *= (0.4f + 0.6f * progress); offsetY += inv * (ch * 0.3f); blur += inv * 2f; blurY -= 1f; break;
                case "zoomblurdown": scale *= (0.4f + 0.6f * progress); offsetY -= inv * (ch * 0.3f); blur += inv * 2f; blurY += 1f; break;
                case "zoomblurleft": scale *= (0.4f + 0.6f * progress); offsetX += inv * (cw * 0.3f); blur += inv * 2f; blurX -= 1f; break;
                case "zoomblurright": scale *= (0.4f + 0.6f * progress); offsetX -= inv * (cw * 0.3f); blur += inv * 2f; blurX += 1f; break;
                case "dynamiczoomblur":
                    float impact = inv * inv * inv;
                    opacity = (opacity / progress) * (1f - impact * 0.3f);
                    scale *= (1f + impact * 2.5f);
                    blur += impact * 8f;
                    break;
            }
        }

        private static void DrawWithAlpha(Graphics g, Image img, Rectangle bounds, float alpha)
        {
            using var attr = new ImageAttributes();
            attr.SetColorMatrix(new ColorMatrix { Matrix33 = Math.Clamp(alpha, 0f, 1f) }, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
            g.DrawImage(img, bounds, 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, attr);
        }

        // Inside VideoRenderHelper.cs
        public static void DrawBlurOverlay(Graphics g, BlurOverlay blur, List<MediaItem> allItems, double currentTime, int canvasX, int canvasY, int canvasWidth, int canvasHeight, int blurTrackIndex)
        {
            if (blur == null) return;

            int bx = canvasX + (int)(blur.RelativeX * canvasWidth);
            int by = canvasY + (int)(blur.RelativeY * canvasHeight);
            int bw = (int)(blur.RelativeWidth * canvasWidth);
            int bh = (int)(blur.RelativeHeight * canvasHeight);

            Rectangle blurRect = new Rectangle(bx, by, bw, bh);
            blurRect.Intersect(new Rectangle(canvasX, canvasY, canvasWidth, canvasHeight));

            if (blurRect.Width <= 0 || blurRect.Height <= 0) return;

            using (Bitmap region = new Bitmap(blurRect.Width, blurRect.Height, PixelFormat.Format32bppArgb))
            {
                using (Graphics regG = Graphics.FromImage(region))
                {
                    regG.SmoothingMode = SmoothingMode.AntiAlias;
                    regG.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    regG.Clear(Color.Black);

                    regG.TranslateTransform(-blurRect.X, -blurRect.Y);

                    // Filter images that exist strictly UNDERNEATH this blur overlay (higher track index)
                    var underlyingImages = allItems
                        .Where(i => i.Type == MediaType.Image &&
                                    i.TrackIndex > blurTrackIndex &&
                                    currentTime >= i.StartTime &&
                                    currentTime < i.StartTime + i.Duration)
                        .OrderByDescending(i => i.TrackIndex);

                    foreach (var item in underlyingImages)
                    {
                        if (!string.IsNullOrEmpty(item.FilePath) && File.Exists(item.FilePath))
                        {
                            using (var img = Image.FromFile(item.FilePath))
                            {
                                DrawImageItem(regG, img, item, currentTime, canvasX, canvasY, canvasWidth, canvasHeight);
                            }
                        }
                    }
                }

                ApplyBoxBlur(region, blur.BlurRadius > 0 ? blur.BlurRadius : 15);
                g.DrawImage(region, blurRect.Location);
            }
        }

        // Fast box blur algorithm operating directly on bitmap memory
        private static void ApplyBoxBlur(Bitmap bmp, int radius)
        {
            Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            BitmapData bmpData = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

            IntPtr ptr = bmpData.Scan0;
            int bytes = Math.Abs(bmpData.Stride) * bmp.Height;
            byte[] rgbValues = new byte[bytes];
            System.Runtime.InteropServices.Marshal.Copy(ptr, rgbValues, 0, bytes);

            int w = bmp.Width;
            int h = bmp.Height;
            int stride = bmpData.Stride;

            // Horizontal & Vertical box blur passes
            for (int pass = 0; pass < 2; pass++)
            {
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int rSum = 0, gSum = 0, bSum = 0, count = 0;

                        for (int k = -radius; k <= radius; k += 2)
                        {
                            int px = Math.Clamp(x + k, 0, w - 1);
                            int idx = (y * stride) + (px * 4);

                            bSum += rgbValues[idx];
                            gSum += rgbValues[idx + 1];
                            rSum += rgbValues[idx + 2];
                            count++;
                        }

                        int currentIdx = (y * stride) + (x * 4);
                        rgbValues[currentIdx] = (byte)(bSum / count);
                        rgbValues[currentIdx + 1] = (byte)(gSum / count);
                        rgbValues[currentIdx + 2] = (byte)(rSum / count);
                    }
                }
            }

            System.Runtime.InteropServices.Marshal.Copy(rgbValues, 0, ptr, bytes);
            bmp.UnlockBits(bmpData);
        }

        private static Bitmap GetCanvasBitmap(Graphics g)
        {
            // Native handle bitmap fallback
            return new Bitmap((int)g.VisibleClipBounds.Width, (int)g.VisibleClipBounds.Height, g);
        }

        public static Bitmap RenderExportFrame(List<MediaItem> allItems, double currentTime, int canvasWidth = 1080, int canvasHeight = 1920)
        {
            Bitmap frame = new Bitmap(canvasWidth, canvasHeight, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(frame))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.Clear(Color.Black);

                // 1. Get all active visual items (Images AND Blurs) sorted by TrackIndex (back-to-front)
                var activeVisualItems = allItems
                    .Where(i => (i.Type == MediaType.Image || (i.Type == MediaType.Blur && i.BlurData != null)) &&
                                currentTime >= i.StartTime &&
                                currentTime < i.StartTime + i.Duration)
                    .OrderByDescending(i => i.TrackIndex); // Higher TrackIndex = Background, Lower TrackIndex = Foreground

                // 2. Render images and blurs in exact layer order
                foreach (var item in activeVisualItems)
                {
                    if (item.Type == MediaType.Image)
                    {
                        if (File.Exists(item.FilePath))
                        {
                            using var img = Image.FromFile(item.FilePath);
                            DrawImageItem(g, img, item, currentTime, 0, 0, canvasWidth, canvasHeight);
                        }

                        if (item.TextLabels != null)
                        {
                            double localTime = currentTime - item.StartTime;
                            foreach (var lbl in item.TextLabels.Where(l => localTime >= l.StartTime && localTime <= l.StartTime + l.Duration))
                                DrawTextLabel(g, lbl, 0, 0, canvasWidth, canvasHeight);
                        }
                    }
                    else if (item.Type == MediaType.Blur)
                    {
                        // Draws blur on top of background images, but BEFORE foreground images!
                        DrawBlurOverlay(g, item.BlurData, allItems, currentTime, 0, 0, canvasWidth, canvasHeight, item.TrackIndex);
                    }
                }

                // 3. Render Text Overlays on top of everything
                var activeTextItems = allItems
                    .Where(i => i.Type == MediaType.Text &&
                                i.TextData != null &&
                                currentTime >= i.StartTime &&
                                currentTime < i.StartTime + i.Duration);

                foreach (var textItem in activeTextItems)
                {
                    DrawTextLabel(g, textItem.TextData, 0, 0, canvasWidth, canvasHeight);
                }
            }
            return frame;
        }

        public static void DrawTextLabel(Graphics g, TextLabel label, int canvasX, int canvasY, int canvasWidth, int canvasHeight)
        {
            if (label == null || string.IsNullOrEmpty(label.Content)) return;

            // Calculate relative bounding rect based on canvas dimensions
            RectangleF rect = new RectangleF(
                canvasX + (label.RelativeX * canvasWidth),
                canvasY + (label.RelativeY * canvasHeight),
                Math.Max(label.RelativeWidth * canvasWidth, 50),
                Math.Max(label.RelativeHeight * canvasHeight, 30)
            );

            // 1. Fill background
            using (var bgBrush = new SolidBrush(label.BackgroundColor))
            {
                g.FillRectangle(bgBrush, rect);
            }

            // 2. Scale font size dynamically relative to 1080x1920 base canvas
            float baseFontSize = label.FontSize > 0 ? label.FontSize : 15f;
            float scaleFactor = (float)canvasHeight / 1920f;
            float scaledFontSize = Math.Max(baseFontSize * scaleFactor, 8f);

            using var font = new Font(
                !string.IsNullOrEmpty(label.FontFamily) ? label.FontFamily : "Segoe UI",
                scaledFontSize,
                label.IsBold ? FontStyle.Bold : FontStyle.Regular
            );
            using var textBrush = new SolidBrush(label.TextColor);

            var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.Word
            };

            g.DrawString(label.Content, font, textBrush, rect, sf);
        }
    }
}