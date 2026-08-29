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

            // Keep width and height as floating-point precision
            float w = img.Width * finalScale;
            float h = img.Height * finalScale;

            // High-precision floating point placement
            float posX = (item.PositionX * (canvasWidth / 1080f)) + offsetX + (waveAmount * (float)Math.Sin(localTime * 10));
            float posY = (item.PositionY * (canvasHeight / 1920f)) + offsetY;
            float x = canvasX + (canvasWidth - w) / 2f + posX;
            float y = canvasY + (canvasHeight - h) / 2f + posY;

            RectangleF destRect = new RectangleF(x, y, w, h);

            if (blurAmount > 0.05f)
            {
                int steps = 3;
                float stepAlpha = (opacity / steps) * 0.7f;
                for (int i = steps; i >= 1; i--)
                {
                    float factor = (i / (float)steps) * blurAmount;
                    float bw = w * (1f + factor * 0.15f);
                    float bh = h * (1f + factor * 0.15f);
                    float bx = x - (bw - w) / 2f + (blurAngleX * factor * 20f);
                    float by = y - (bh - h) / 2f + (blurAngleY * factor * 20f);

                    DrawWithAlpha(g, img, new RectangleF(bx, by, bw, bh), stepAlpha);
                }
            }

            DrawWithAlpha(g, img, destRect, opacity);
        }
        private static void DrawWithAlpha(Graphics g, Image img, RectangleF bounds, float alpha)
        {
            using var attr = new ImageAttributes();
            attr.SetColorMatrix(new ColorMatrix { Matrix33 = Math.Clamp(alpha, 0f, 1f) }, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
            g.DrawImage(img, Rectangle.Round(bounds), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, attr);
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

        public static void DrawBlurOverlay(Graphics g, BlurOverlay blur, List<MediaItem> allItems, double currentTime, int canvasX, int canvasY, int canvasWidth, int canvasHeight, int blurTrackIndex)
        {
            if (blur == null) return;

            // Calculate exact blur box bounds on canvas
            float bx = canvasX + ((float)blur.RelativeX * canvasWidth);
            float by = canvasY + ((float)blur.RelativeY * canvasHeight);
            float bw = (float)blur.RelativeWidth * canvasWidth;
            float bh = (float)blur.RelativeHeight * canvasHeight;

            RectangleF blurRectF = new RectangleF(bx, by, bw, bh);
            RectangleF canvasRectF = new RectangleF(canvasX, canvasY, canvasWidth, canvasHeight);
            blurRectF.Intersect(canvasRectF);

            if (blurRectF.Width <= 1 || blurRectF.Height <= 1) return;

            // Fetch images underneath this blur track (higher TrackIndex = visually behind)
            var underlyingImages = allItems
                .Where(i => i.Type == MediaType.Image &&
                            i.TrackIndex > blurTrackIndex &&
                            currentTime >= i.StartTime &&
                            currentTime < i.StartTime + i.Duration)
                .OrderByDescending(i => i.TrackIndex)
                .ToList();

            if (!underlyingImages.Any()) return;

            // STEP 1: Render the full scene portion underneath at full size into an intermediate buffer
            int cropX = (int)Math.Floor(blurRectF.X);
            int cropY = (int)Math.Floor(blurRectF.Y);
            int cropW = (int)Math.Ceiling(blurRectF.Width);
            int cropH = (int)Math.Ceiling(blurRectF.Height);

            if (cropW <= 0 || cropH <= 0) return;

            using (Bitmap fullRegionBmp = new Bitmap(cropW, cropH, PixelFormat.Format32bppArgb))
            {
                using (Graphics fullG = Graphics.FromImage(fullRegionBmp))
                {
                    fullG.SmoothingMode = SmoothingMode.AntiAlias;
                    fullG.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    fullG.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    fullG.Clear(Color.Black);

                    // Shift origin so (0,0) of fullRegionBmp corresponds to cropX, cropY
                    fullG.TranslateTransform(-cropX, -cropY);

                    foreach (var item in underlyingImages)
                    {
                        var img = GetCachedImage(item.FilePath);
                        if (img != null)
                        {
                            DrawImageItem(fullG, img, item, currentTime, canvasX, canvasY, canvasWidth, canvasHeight);
                        }
                    }
                }

                // STEP 2: Smooth downscale to create the blur effect without matrix transform shifts
                int scaleDownFactor = 16;
                int tinyW = Math.Max(4, cropW / scaleDownFactor);
                int tinyH = Math.Max(4, cropH / scaleDownFactor);

                using (Bitmap tinyBmp = new Bitmap(tinyW, tinyH, PixelFormat.Format32bppArgb))
                {
                    using (Graphics tinyG = Graphics.FromImage(tinyBmp))
                    {
                        tinyG.InterpolationMode = InterpolationMode.HighQualityBilinear;
                        tinyG.PixelOffsetMode = PixelOffsetMode.Half;
                        tinyG.DrawImage(fullRegionBmp, 0, 0, tinyW, tinyH);
                    }

                    // STEP 3: Stretch the low-res blurred snapshot back over the target blur bounds on main canvas
                    using (var attr = new ImageAttributes())
                    {
                        attr.SetWrapMode(WrapMode.TileFlipXY);

                        GraphicsState state = g.Save();
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.PixelOffsetMode = PixelOffsetMode.Half;

                        g.DrawImage(
                            tinyBmp,
                            new Rectangle((int)blurRectF.X, (int)blurRectF.Y, (int)blurRectF.Width, (int)blurRectF.Height),
                            0, 0, tinyBmp.Width, tinyBmp.Height,
                            GraphicsUnit.Pixel,
                            attr
                        );

                        g.Restore(state);
                    }
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