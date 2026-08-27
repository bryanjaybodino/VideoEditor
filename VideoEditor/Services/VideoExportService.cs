using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using VideoEditor.Models;

namespace VideoEditor.Services
{
    public class VideoExportService
    {
        private string ffmpegPath = "ffmpeg";

        // Locked to 9:16 Vertical Mobile Aspect Ratio
        private const int TargetWidth = 1080;
        private const int TargetHeight = 1920;
        private const int FrameRate = 30;

        public async Task ExportToVideo(List<MediaItem> mediaItems, string outputPath)
        {
            if (mediaItems == null || mediaItems.Count == 0)
                throw new InvalidOperationException("No media items to export");

            double totalDuration = mediaItems.Max(x => x.StartTime + x.Duration);
            var audioItem = mediaItems.FirstOrDefault(x => x.Type == MediaType.Audio);

            var ffmpegArgs = $"-y -f rawvideo -pixel_format bgr24 -video_size {TargetWidth}x{TargetHeight} -framerate {FrameRate} -i - ";

            if (audioItem != null && File.Exists(audioItem.FilePath))
            {
                ffmpegArgs += $"-itsoffset {audioItem.StartTime.ToString(System.Globalization.CultureInfo.InvariantCulture)} -i \"{audioItem.FilePath}\" -c:a aac ";
            }

            ffmpegArgs += $"-t {totalDuration.ToString(System.Globalization.CultureInfo.InvariantCulture)} -c:v libx264 -pix_fmt yuv420p \"{outputPath}\"";

            var processInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = ffmpegArgs,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            await Task.Run(() =>
            {
                using (var process = Process.Start(processInfo))
                {
                    if (process == null) throw new InvalidOperationException("Failed to start FFmpeg.");

                    using (var stream = process.StandardInput.BaseStream)
                    {
                        int totalFrames = (int)(totalDuration * FrameRate);

                        for (int frame = 0; frame < totalFrames; frame++)
                        {
                            double timePosition = frame / (double)FrameRate;
                            RenderCompositeFrame(mediaItems, timePosition, stream);
                        }
                        stream.Flush();
                    }

                    string errorOutput = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        throw new InvalidOperationException($"FFmpeg export failed: {errorOutput}");
                    }
                }
            });
        }

        private void RenderCompositeFrame(List<MediaItem> mediaItems, double timePosition, Stream outputStream)
        {
            using (var bitmap = new Bitmap(TargetWidth, TargetHeight, PixelFormat.Format24bppRgb))
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Black);

                var activeItem = mediaItems.FirstOrDefault(item =>
                    item.Type == MediaType.Image &&
                    timePosition >= item.StartTime &&
                    timePosition < item.StartTime + item.Duration);

                if (activeItem != null && File.Exists(activeItem.FilePath))
                {
                    using (var sourceImg = Image.FromFile(activeItem.FilePath))
                    {
                        // Fill screen maintaining aspect ratio (Aspect Fill)
                        float scale = Math.Max((float)TargetWidth / sourceImg.Width, (float)TargetHeight / sourceImg.Height);
                        int w = (int)(sourceImg.Width * scale);
                        int h = (int)(sourceImg.Height * scale);
                        int x = (TargetWidth - w) / 2;
                        int y = (TargetHeight - h) / 2;

                        g.DrawImage(sourceImg, x, y, w, h);
                    }
                }

                BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, TargetWidth, TargetHeight),
                    ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

                int bytes = Math.Abs(bmpData.Stride) * TargetHeight;
                byte[] rgbValues = new byte[bytes];
                Marshal.Copy(bmpData.Scan0, rgbValues, 0, bytes);
                bitmap.UnlockBits(bmpData);

                outputStream.Write(rgbValues, 0, bytes);
            }
        }
    }
}