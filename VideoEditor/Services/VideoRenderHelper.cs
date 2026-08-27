using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using VideoEditor.Models;

namespace VideoEditor.Services
{
    public static class VideoRenderHelper
    {
        public static Bitmap RenderExportFrame(List<MediaItem> allItems, double currentTime, int exportWidth = 1080, int exportHeight = 1920)
        {
            Bitmap bitmap = new Bitmap(exportWidth, exportHeight);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Black);
                g.SmoothingMode = SmoothingMode.AntiAlias;

                int canvasX = 0;
                int canvasY = 0;
                int canvasWidth = exportWidth;
                int canvasHeight = exportHeight;

                // Render active image tracks (bottom-to-top)
                var activeImages = allItems
                    .Where(item => item.Type == MediaType.Image &&
                                   currentTime >= item.StartTime &&
                                   currentTime < item.StartTime + item.Duration)
                    .OrderBy(item => item.TrackIndex)
                    .ToList();

                foreach (var item in activeImages)
                {
                    if (System.IO.File.Exists(item.FilePath))
                    {
                        using (Image img = Image.FromFile(item.FilePath))
                        {
                            DrawImageItem(g, img, item, canvasX, canvasY, canvasWidth, canvasHeight);
                        }
                    }

                    double localTime = currentTime - item.StartTime;
                    foreach (var label in item.TextLabels)
                    {
                        if (localTime >= label.StartTime && localTime <= label.StartTime + label.Duration)
                        {
                            DrawTextLabel(g, label, canvasX, canvasY, canvasWidth, canvasHeight);
                        }
                    }
                }

                // Render active standalone text tracks
                var activeTexts = allItems
                    .Where(item => item.Type == MediaType.Text &&
                                   item.TextData != null &&
                                   currentTime >= item.StartTime &&
                                   currentTime < item.StartTime + item.Duration)
                    .OrderBy(item => item.TrackIndex)
                    .ToList();

                foreach (var textItem in activeTexts)
                {
                    DrawTextLabel(g, textItem.TextData, canvasX, canvasY, canvasWidth, canvasHeight);
                }
            }

            return bitmap;
        }

        public static void DrawImageItem(Graphics g, Image img, MediaItem item, int canvasX, int canvasY, int canvasWidth, int canvasHeight)
        {
            float scale = Math.Max((float)canvasWidth / img.Width, (float)canvasHeight / img.Height) * item.Scale;
            int baseW = (int)(img.Width * scale);
            int baseH = (int)(img.Height * scale);

            // Position relative offset to canvas resolution
            float posX = item.PositionX * ((float)canvasWidth / 1080f);
            float posY = item.PositionY * ((float)canvasHeight / 1920f);

            int originX = canvasX + (canvasWidth - baseW) / 2 + (int)posX;
            int originY = canvasY + (canvasHeight - baseH) / 2 + (int)posY;

            g.DrawImage(img, originX, originY, baseW, baseH);
        }

        public static void DrawTextLabel(Graphics g, TextLabel label, int canvasX, int canvasY, int canvasWidth, int canvasHeight)
        {
            float drawX = canvasX + (label.RelativeX * canvasWidth);
            float drawY = canvasY + (label.RelativeY * canvasHeight);
            float drawW = Math.Max(label.RelativeWidth * canvasWidth, 50);
            float drawH = Math.Max(label.RelativeHeight * canvasHeight, 30);

            var rect = new RectangleF(drawX, drawY, drawW, drawH);

            using (var bgBrush = new SolidBrush(label.BackgroundColor))
            {
                g.FillRectangle(bgBrush, rect);
            }

            // Adjust font sizing based on base resolution
            float scaledFontSize = Math.Max(8, label.FontSize * (canvasHeight / 1920f));

            using (var font = new Font(label.FontFamily, scaledFontSize, label.IsBold ? FontStyle.Bold : FontStyle.Regular))
            using (var textBrush = new SolidBrush(label.TextColor))
            {
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.Word,
                    FormatFlags = 0
                };
                g.DrawString(label.Content ?? "", font, textBrush, rect, sf);
            }
        }
    }
}