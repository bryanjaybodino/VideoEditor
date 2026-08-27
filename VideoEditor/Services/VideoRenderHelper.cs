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
            double duration = item.Duration;

            if (localTime < 0 || localTime > duration) return;

            // Base Transformation Variables
            float opacity = 1.0f;
            float scaleMultiplier = 1.0f;
            float offsetX = 0f;
            float offsetY = 0f;
            float blurAmount = 0f;
            float blurAngleX = 0f;
            float blurAngleY = 0f;
            float waveAmount = 0f;

            // --- IN ANIMATIONS ---
            double inDuration = item.InEffectDuration;
            if (localTime < inDuration && inDuration > 0)
            {
                float progress = Math.Clamp((float)(localTime / inDuration), 0f, 1f);
                ApplyAnimationEffect(item.InEffectType, progress, true, ref opacity, ref scaleMultiplier, ref offsetX, ref offsetY, ref blurAmount, ref blurAngleX, ref blurAngleY, ref waveAmount, canvasWidth, canvasHeight);
            }

            // --- OUT ANIMATIONS ---
            double outDuration = item.OutEffectDuration;
            double timeRemaining = duration - localTime;
            if (timeRemaining < outDuration && outDuration > 0)
            {
                float progress = Math.Clamp((float)(timeRemaining / outDuration), 0f, 1f);
                ApplyAnimationEffect(item.OutEffectType, progress, false, ref opacity, ref scaleMultiplier, ref offsetX, ref offsetY, ref blurAmount, ref blurAngleX, ref blurAngleY, ref waveAmount, canvasWidth, canvasHeight);
            }

            // --- CALCULATE BOUNDS ---
            float finalScale = Math.Max((float)canvasWidth / img.Width, (float)canvasHeight / img.Height) * item.Scale * scaleMultiplier;
            int baseW = (int)(img.Width * finalScale);
            int baseH = (int)(img.Height * finalScale);

            float posX = (item.PositionX * ((float)canvasWidth / 1080f)) + offsetX + (waveAmount * (float)Math.Sin(localTime * 10));
            float posY = (item.PositionY * ((float)canvasHeight / 1920f)) + offsetY;

            int originX = canvasX + (canvasWidth - baseW) / 2 + (int)posX;
            int originY = canvasY + (canvasHeight - baseH) / 2 + (int)posY;

            Rectangle destRect = new Rectangle(originX, originY, baseW, baseH);

            // --- RENDER WITH EFFECTS ---
            using (var imageAttributes = new ImageAttributes())
            {
                ColorMatrix colorMatrix = new ColorMatrix { Matrix33 = Math.Clamp(opacity, 0f, 1f) };
                imageAttributes.SetColorMatrix(colorMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

                // Multi-Pass Radial & Motion Blur
                if (blurAmount > 0.05f)
                {
                    int steps = 8;
                    float stepOpacity = (opacity / steps) * 0.8f;

                    for (int i = steps; i >= 1; i--)
                    {
                        float factor = (i / (float)steps) * blurAmount;
                        int blurW = (int)(baseW * (1f + (factor * 0.15f)));
                        int blurH = (int)(baseH * (1f + (factor * 0.15f)));

                        int bX = originX - ((blurW - baseW) / 2) + (int)(blurAngleX * factor * 20);
                        int bY = originY - ((blurH - baseH) / 2) + (int)(blurAngleY * factor * 20);

                        ColorMatrix blurMatrix = new ColorMatrix { Matrix33 = Math.Clamp(stepOpacity, 0f, 1f) };
                        using (var blurAttributes = new ImageAttributes())
                        {
                            blurAttributes.SetColorMatrix(blurMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                            g.DrawImage(img, new Rectangle(bX, bY, blurW, blurH), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, blurAttributes);
                        }
                    }
                }

                g.DrawImage(img, destRect, 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, imageAttributes);
            }
        }

        private static void ApplyAnimationEffect(
            string effectType, float progress, bool isInEffect,
            ref float opacity, ref float scale, ref float offsetX, ref float offsetY,
            ref float blurAmount, ref float blurAngleX, ref float blurAngleY, ref float waveAmount,
            int canvasWidth, int canvasHeight)
        {
            if (string.IsNullOrEmpty(effectType) || string.Equals(effectType, "None", StringComparison.OrdinalIgnoreCase))
                return;

            string type = effectType.Trim();

            if (string.Equals(type, "Fade", StringComparison.OrdinalIgnoreCase))
            {
                opacity *= progress;
            }
            else if (string.Equals(type, "Slide", StringComparison.OrdinalIgnoreCase))
            {
                opacity *= progress;
                float slideOffset = (1f - progress) * canvasWidth * (isInEffect ? -0.5f : 0.5f);
                offsetX += slideOffset;
            }
            else if (string.Equals(type, "Wave", StringComparison.OrdinalIgnoreCase))
            {
                opacity *= progress;
                waveAmount += (1f - progress) * 30f;
            }
            else if (string.Equals(type, "Zoom", StringComparison.OrdinalIgnoreCase))
            {
                opacity *= progress;
                scale *= (0.3f + (0.7f * progress));
            }
            else if (string.Equals(type, "ZoomBlur", StringComparison.OrdinalIgnoreCase))
            {
                opacity *= progress;
                scale *= (0.3f + (0.7f * progress));
                blurAmount += (1f - progress) * 2f;
            }
            else if (string.Equals(type, "ZoomBlurUp", StringComparison.OrdinalIgnoreCase))
            {
                opacity *= progress;
                scale *= (0.4f + (0.6f * progress));
                offsetY += (1f - progress) * (canvasHeight * 0.3f);
                blurAmount += (1f - progress) * 2f;
                blurAngleY += -1f;
            }
            else if (string.Equals(type, "ZoomBlurDown", StringComparison.OrdinalIgnoreCase))
            {
                opacity *= progress;
                scale *= (0.4f + (0.6f * progress));
                offsetY -= (1f - progress) * (canvasHeight * 0.3f);
                blurAmount += (1f - progress) * 2f;
                blurAngleY += 1f;
            }
            else if (string.Equals(type, "ZoomBlurLeft", StringComparison.OrdinalIgnoreCase))
            {
                opacity *= progress;
                scale *= (0.4f + (0.6f * progress));
                offsetX += (1f - progress) * (canvasWidth * 0.3f);
                blurAmount += (1f - progress) * 2f;
                blurAngleX += -1f;
            }
            else if (string.Equals(type, "ZoomBlurRight", StringComparison.OrdinalIgnoreCase))
            {
                opacity *= progress;
                scale *= (0.4f + (0.6f * progress));
                offsetX -= (1f - progress) * (canvasWidth * 0.3f);
                blurAmount += (1f - progress) * 2f;
                blurAngleX += 1f;
            }
            else if (string.Equals(type, "DynamicZoomBlur", StringComparison.OrdinalIgnoreCase))
            {
                float impact = (1f - progress);
                float easeImpact = impact * impact * impact;

                opacity *= (1f - (easeImpact * 0.3f));
                scale *= (1f + (easeImpact * 2.5f));
                blurAmount += easeImpact * 8f;
            }
        }

        public static Bitmap RenderExportFrame(List<MediaItem> allItems, double currentTime, int canvasWidth = 1080, int canvasHeight = 1920)
        {
            Bitmap frame = new Bitmap(canvasWidth, canvasHeight, PixelFormat.Format32bppArgb);

            using (Graphics g = Graphics.FromImage(frame))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Black);

                var activeImages = allItems
                    .Where(item => item.Type == MediaType.Image &&
                                   currentTime >= item.StartTime &&
                                   currentTime < item.StartTime + item.Duration)
                    .OrderByDescending(item => item.TrackIndex)
                    .ToList();

                foreach (var item in activeImages)
                {
                    if (File.Exists(item.FilePath))
                    {
                        using (var img = Image.FromFile(item.FilePath))
                        {
                            DrawImageItem(g, img, item, currentTime, 0, 0, canvasWidth, canvasHeight);
                        }
                    }
                }

                foreach (var item in activeImages)
                {
                    double localTime = currentTime - item.StartTime;
                    if (item.TextLabels != null)
                    {
                        foreach (var label in item.TextLabels)
                        {
                            if (localTime >= label.StartTime && localTime <= label.StartTime + label.Duration)
                            {
                                DrawTextLabel(g, label, 0, 0, canvasWidth, canvasHeight);
                            }
                        }
                    }
                }

                var activeTextItems = allItems
                    .Where(item => item.Type == MediaType.Text &&
                                   item.TextData != null &&
                                   currentTime >= item.StartTime &&
                                   currentTime < item.StartTime + item.Duration)
                    .ToList();

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

            float drawX = canvasX + (label.RelativeX * canvasWidth);
            float drawY = canvasY + (label.RelativeY * canvasHeight);
            float drawW = Math.Max(label.RelativeWidth * canvasWidth, 50);
            float drawH = Math.Max(label.RelativeHeight * canvasHeight, 30);

            using (var font = new Font("Segoe UI", label.FontSize > 0 ? label.FontSize : 16f, FontStyle.Bold))
            using (var brush = new SolidBrush(label.TextColor))
            {
                RectangleF rect = new RectangleF(drawX, drawY, drawW, drawH);
                g.DrawString(label.Content, font, brush, rect);
            }
        }
    }
}