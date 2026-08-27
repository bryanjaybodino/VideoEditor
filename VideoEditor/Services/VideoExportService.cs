using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VideoEditor.Models;

namespace VideoEditor.Services
{
    public class VideoExportService
    {
        private const int OutputWidth = 1080;
        private const int OutputHeight = 1920;
        private const int TargetFps = 30;

        public async Task ExportToVideo(List<MediaItem> items, string outputPath, Action<double, Graphics> renderPreviewFrame)
        {
            var imageItems = items.Where(x => x.Type == MediaType.Image).ToList();
            var audioItem = items.FirstOrDefault(x => x.Type == MediaType.Audio);

            if (imageItems.Count == 0)
                throw new Exception("No image clips found to export.");

            double totalDuration = items.Max(x => x.StartTime + x.Duration);
            int totalFrames = (int)(totalDuration * TargetFps);

            string tempOutputFile = Path.Combine(Path.GetTempPath(), $"export_{Guid.NewGuid():N}.mp4");

            // Build audio arguments if audio exists
            string audioArgs = "";
            if (audioItem != null && File.Exists(audioItem.FilePath))
            {
                double audioDuration = Math.Min(audioItem.Duration, totalDuration - audioItem.StartTime);
                audioArgs = $"-ss {audioItem.StartTime.ToString(System.Globalization.CultureInfo.InvariantCulture)} -t {audioDuration.ToString(System.Globalization.CultureInfo.InvariantCulture)} -i \"{audioItem.FilePath}\" -map 0:v -map 1:a -c:a aac -b:a 192k";
            }
            else
            {
                audioArgs = "-map 0:v";
            }

            // Pipe raw video frames directly into FFmpeg
            string arguments = $"-y -f rawvideo -vcodec rawvideo -s {OutputWidth}x{OutputHeight} -pix_fmt bgr24 -r {TargetFps} -i - {audioArgs} -t {totalDuration.ToString(System.Globalization.CultureInfo.InvariantCulture)} -c:v libx264 -preset ultrafast -pix_fmt yuv420p \"{tempOutputFile}\"";

            await Task.Run(() =>
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "ffmpeg.exe",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    using (Stream ffmpegIn = process.StandardInput.BaseStream)
                    using (Bitmap frameBuffer = new Bitmap(OutputWidth, OutputHeight, PixelFormat.Format24bppRgb))
                    using (Graphics g = Graphics.FromImage(frameBuffer))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                        // Render frame by frame matching the exact preview math
                        for (int frame = 0; frame < totalFrames; frame++)
                        {
                            double timeInSeconds = (double)frame / TargetFps;

                            // Clear canvas
                            g.Clear(Color.Black);

                            // Delegate rendering to your existing UI preview render logic
                            renderPreviewFrame(timeInSeconds, g);

                            // Write raw bitmap bytes directly into FFmpeg process stdin
                            BitmapData bmpData = frameBuffer.LockBits(
                                new Rectangle(0, 0, OutputWidth, OutputHeight),
                                ImageLockMode.ReadOnly,
                                PixelFormat.Format24bppRgb);

                            try
                            {
                                byte[] bytes = new byte[bmpData.Stride * OutputHeight];
                                System.Runtime.InteropServices.Marshal.Copy(bmpData.Scan0, bytes, 0, bytes.Length);
                                ffmpegIn.Write(bytes, 0, bytes.Length);
                            }
                            finally
                            {
                                frameBuffer.UnlockBits(bmpData);
                            }
                        }

                        ffmpegIn.Flush();
                    }

                    string errorLog = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        throw new Exception($"FFmpeg Export Failed:\n{errorLog}");
                    }
                }
            });

            if (File.Exists(tempOutputFile))
            {
                if (File.Exists(outputPath)) File.Delete(outputPath);
                File.Move(tempOutputFile, outputPath);
            }
        }
    }
}