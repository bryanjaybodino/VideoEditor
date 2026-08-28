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
        private static readonly Dictionary<string, byte[]> ImageByteCache = new Dictionary<string, byte[]>();
        private static readonly object CacheLock = new object();

        public static Image GetCachedImage(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            byte[] bytes;

            lock (CacheLock)
            {
                if (!ImageByteCache.TryGetValue(filePath, out bytes))
                {
                    bytes = File.ReadAllBytes(filePath);
                    ImageByteCache[filePath] = bytes;
                }
            }

            // Creating a MemoryStream copy prevents file lock & GDI+ thread conflicts
            using (var ms = new MemoryStream(bytes))
            {
                return Image.FromStream(ms);
            }
        }
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

            var underlyingImages = allItems
                .Where(i => i.Type == MediaType.Image &&
                            i.TrackIndex > blurTrackIndex &&
                            currentTime >= i.StartTime &&
                            currentTime < i.StartTime + i.Duration)
                .OrderByDescending(i => i.TrackIndex)
                .ToList();

            if (!underlyingImages.Any()) return;

            // Fast 1/16th scale resampling (Bypasses slow CPU memory loops)
            int tinyW = Math.Max(4, blurRect.Width / 16);
            int tinyH = Math.Max(4, blurRect.Height / 16);

            using (Bitmap tinyBmp = new Bitmap(tinyW, tinyH, PixelFormat.Format32bppArgb))
            {
                using (Graphics tinyG = Graphics.FromImage(tinyBmp))
                {
                    tinyG.SmoothingMode = SmoothingMode.HighSpeed;
                    tinyG.InterpolationMode = InterpolationMode.Low;
                    tinyG.Clear(Color.Black);

                    float scaleX = (float)tinyW / blurRect.Width;
                    float scaleY = (float)tinyH / blurRect.Height;
                    tinyG.ScaleTransform(scaleX, scaleY);
                    tinyG.TranslateTransform(-blurRect.X, -blurRect.Y);

                    foreach (var item in underlyingImages)
                    {
                        var img = GetCachedImage(item.FilePath);
                        if (img != null)
                        {
                            DrawImageItem(tinyG, img, item, currentTime, canvasX, canvasY, canvasWidth, canvasHeight);
                        }
                    }
                }

                using (var attr = new ImageAttributes())
                {
                    attr.SetWrapMode(WrapMode.TileFlipXY);

                    GraphicsState state = g.Save();
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                    Rectangle targetRect = new Rectangle(blurRect.X, blurRect.Y, blurRect.Width + 1, blurRect.Height + 1);

                    g.DrawImage(
                        tinyBmp,
                        targetRect,
                        0, 0, tinyBmp.Width, tinyBmp.Height,
                        GraphicsUnit.Pixel,
                        attr
                    );

                    g.Restore(state);
                }
            }
        }

        private static void ApplyMultiPassBlur(Bitmap bmp, int radius, int passes)
        {
            Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            BitmapData bmpData = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

            IntPtr ptr = bmpData.Scan0;
            int bytes = Math.Abs(bmpData.Stride) * bmp.Height;
            byte[] src = new byte[bytes];
            byte[] dst = new byte[bytes];

            System.Runtime.InteropServices.Marshal.Copy(ptr, src, 0, bytes);

            int w = bmp.Width;
            int h = bmp.Height;
            int stride = bmpData.Stride;

            for (int p = 0; p < passes; p++)
            {
                // Horizontal Pass
                for (int y = 0; y < h; y++)
                {
                    int rowOffset = y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        int rSum = 0, gSum = 0, bSum = 0, aSum = 0, count = 0;

                        for (int k = -radius; k <= radius; k++)
                        {
                            int px = Math.Clamp(x + k, 0, w - 1);
                            int idx = rowOffset + (px * 4);

                            bSum += src[idx];
                            gSum += src[idx + 1];
                            rSum += src[idx + 2];
                            aSum += src[idx + 3];
                            count++;
                        }

                        int outIdx = rowOffset + (x * 4);
                        dst[outIdx] = (byte)(bSum / count);
                        dst[outIdx + 1] = (byte)(gSum / count);
                        dst[outIdx + 2] = (byte)(rSum / count);
                        dst[outIdx + 3] = (byte)(aSum / count);
                    }
                }

                // Vertical Pass
                for (int y = 0; y < h; y++)
                {
                    int rowOffset = y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        int rSum = 0, gSum = 0, bSum = 0, aSum = 0, count = 0;

                        for (int k = -radius; k <= radius; k++)
                        {
                            int py = Math.Clamp(y + k, 0, h - 1);
                            int idx = (py * stride) + (x * 4);

                            bSum += dst[idx];
                            gSum += dst[idx + 1];
                            rSum += dst[idx + 2];
                            aSum += dst[idx + 3];
                            count++;
                        }

                        int outIdx = rowOffset + (x * 4);
                        src[outIdx] = (byte)(bSum / count);
                        src[outIdx + 1] = (byte)(gSum / count);
                        src[outIdx + 2] = (byte)(rSum / count);
                        src[outIdx + 3] = (byte)(aSum / count);
                    }
                }
            }

            System.Runtime.InteropServices.Marshal.Copy(src, 0, ptr, bytes);
            bmp.UnlockBits(bmpData);
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
                            using var img = GetCachedImage(item.FilePath);
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