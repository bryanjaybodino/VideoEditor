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

        // Check RAM export cache first; fall back to disk/stream cache for UI preview
        public static Image GetCachedImage(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return null;

            // Direct RAM lookup for fast export mode
            var exportBmp = VideoExportService.GetExportImage(filePath);
            if (exportBmp != null) return exportBmp;

            if (!File.Exists(filePath)) return null;

            byte[] bytes;
            lock (CacheLock)
            {
                if (!ImageByteCache.TryGetValue(filePath, out bytes))
                {
                    bytes = File.ReadAllBytes(filePath);
                    ImageByteCache[filePath] = bytes;
                }
            }

            using (var ms = new MemoryStream(bytes))
            {
                return Image.FromStream(ms);
            }
        }

        public static Bitmap RenderExportFrame(List<MediaItem> allItems, double currentTime, int canvasWidth = 1080, int canvasHeight = 1920)
        {
            Bitmap frame = new Bitmap(canvasWidth, canvasHeight, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(frame))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.Clear(Color.Black);

                // Get active images and blurs sorted back-to-front by track index
                var activeVisualItems = allItems
                    .Where(i => (i.Type == MediaType.Image || (i.Type == MediaType.Blur && i.BlurData != null)) &&
                                currentTime >= i.StartTime &&
                                currentTime < i.StartTime + i.Duration)
                    .OrderByDescending(i => i.TrackIndex);

                foreach (var item in activeVisualItems)
                {
                    if (item.Type == MediaType.Image)
                    {
                        var img = GetCachedImage(item.FilePath);
                        if (img != null)
                        {
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
                        DrawBlurOverlay(g, item.BlurData, allItems, currentTime, 0, 0, canvasWidth, canvasHeight, item.TrackIndex);
                    }
                }

                // Render standalone text overlays
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

        public static void DrawImageItem(Graphics g, Image img, MediaItem item, double currentTime, int canvasX, int canvasY, int canvasWidth, int canvasHeight)
        {
            if (img == null || item == null) return;

            double localTime = currentTime - item.StartTime;
            if (localTime < 0 || localTime > item.Duration) return;

            float opacity = 1.0f, scale = 1.0f, offsetX = 0f, offsetY = 0f;
            float blurAmount = 0f, blurAngleX = 0f, blurAngleY = 0f, waveAmount = 0f;

            if (localTime < item.InEffectDuration && item.InEffectDuration > 0)
                ApplyEffect(item.InEffectType, (float)(localTime / item.InEffectDuration), true, ref opacity, ref scale, ref offsetX, ref offsetY, ref blurAmount, ref blurAngleX, ref blurAngleY, ref waveAmount, canvasWidth, canvasHeight);

            double timeRemaining = item.Duration - localTime;
            if (timeRemaining < item.OutEffectDuration && item.OutEffectDuration > 0)
                ApplyEffect(item.OutEffectType, (float)(timeRemaining / item.OutEffectDuration), false, ref opacity, ref scale, ref offsetX, ref offsetY, ref blurAmount, ref blurAngleX, ref blurAngleY, ref waveAmount, canvasWidth, canvasHeight);

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

            if (blurAmount > 0.05f)
            {
                int steps = 3;
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

            float bx = canvasX + ((float)blur.RelativeX * canvasWidth);
            float by = canvasY + ((float)blur.RelativeY * canvasHeight);
            float bw = (float)blur.RelativeWidth * canvasWidth;
            float bh = (float)blur.RelativeHeight * canvasHeight;

            RectangleF blurRectF = new RectangleF(bx, by, bw, bh);
            RectangleF canvasRectF = new RectangleF(canvasX, canvasY, canvasWidth, canvasHeight);
            blurRectF.Intersect(canvasRectF);

            if (blurRectF.Width <= 0 || blurRectF.Height <= 0) return;

            var underlyingImages = allItems
                .Where(i => i.Type == MediaType.Image &&
                            i.TrackIndex > blurTrackIndex &&
                            currentTime >= i.StartTime &&
                            currentTime < i.StartTime + i.Duration)
                .OrderByDescending(i => i.TrackIndex)
                .ToList();

            if (!underlyingImages.Any()) return;

            float scaleFactor = 0.0625f;
            int tinyW = Math.Max(8, (int)Math.Round(blurRectF.Width * scaleFactor));
            int tinyH = Math.Max(8, (int)Math.Round(blurRectF.Height * scaleFactor));

            using (Bitmap tinyBmp = new Bitmap(tinyW, tinyH, PixelFormat.Format32bppArgb))
            {
                using (Graphics tinyG = Graphics.FromImage(tinyBmp))
                {
                    tinyG.SmoothingMode = SmoothingMode.AntiAlias;
                    tinyG.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    tinyG.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    tinyG.Clear(Color.Black);

                    using (Matrix transform = new Matrix())
                    {
                        transform.Scale((float)tinyW / blurRectF.Width, (float)tinyH / blurRectF.Height);
                        transform.Translate(-blurRectF.X, -blurRectF.Y);
                        tinyG.Transform = transform;
                    }

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
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                    g.DrawImage(
                        tinyBmp,
                        new PointF[] {
                            new PointF(blurRectF.Left, blurRectF.Top),
                            new PointF(blurRectF.Right, blurRectF.Top),
                            new PointF(blurRectF.Left, blurRectF.Bottom)
                        },
                        new RectangleF(0, 0, tinyBmp.Width, tinyBmp.Height),
                        GraphicsUnit.Pixel,
                        attr
                    );

                    g.Restore(state);
                }
            }
        }

        public static void DrawTextLabel(Graphics g, TextLabel label, int canvasX, int canvasY, int canvasWidth, int canvasHeight)
        {
            if (label == null || string.IsNullOrEmpty(label.Content)) return;

            RectangleF rect = new RectangleF(
                canvasX + (label.RelativeX * canvasWidth),
                canvasY + (label.RelativeY * canvasHeight),
                Math.Max(label.RelativeWidth * canvasWidth, 50),
                Math.Max(label.RelativeHeight * canvasHeight, 30)
            );

            using (var bgBrush = new SolidBrush(label.BackgroundColor))
            {
                g.FillRectangle(bgBrush, rect);
            }

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