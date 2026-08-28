using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoEditor.Models;

namespace VideoEditor.Services
{
    public class VideoExportService
    {
        private const int OutputWidth = 1080;
        private const int OutputHeight = 1920;

        // 1. Drop FPS to 30 to cut total frame rendering work in half (120 frames instead of 240 for 4s)
        private const int TargetFps = 30;

        private static readonly Dictionary<string, Bitmap> ExportBitmapCache = new Dictionary<string, Bitmap>();

        public async Task ExportToVideo(List<MediaItem> items, string outputPath, Action<double, Graphics> renderPreviewFrame, IProgress<int> progress = null)
        {
            ClearExportCache();
            var imageItems = items.Where(x => x.Type == MediaType.Image && !string.IsNullOrEmpty(x.FilePath) && File.Exists(x.FilePath)).ToList();

            foreach (var item in imageItems)
            {
                if (!ExportBitmapCache.ContainsKey(item.FilePath))
                {
                    using (var temp = Image.FromFile(item.FilePath))
                    {
                        ExportBitmapCache[item.FilePath] = new Bitmap(temp);
                    }
                }
            }

            try
            {
                string ffmpegExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
                if (!File.Exists(ffmpegExePath))
                {
                    ffmpegExePath = "ffmpeg.exe";
                }

                var audioItem = items.FirstOrDefault(x => x.Type == MediaType.Audio);

                if (imageItems.Count == 0)
                    throw new Exception("No image clips found to export.");

                double totalDuration = items.Max(x => x.StartTime + x.Duration);
                int totalFrames = (int)(totalDuration * TargetFps);

                if (totalFrames <= 0)
                    throw new Exception("Total video duration is invalid or 0 seconds.");

                string tempOutputFile = Path.Combine(Path.GetTempPath(), $"export_{Guid.NewGuid():N}.mp4");

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

                // Added fast FFmpeg encoding flags (-threads 0 and -tune zerolatency)
                string arguments = $"-y -f rawvideo -vcodec rawvideo -s {OutputWidth}x{OutputHeight} -pix_fmt bgr24 -r {TargetFps} -i - {audioArgs} -t {totalDuration.ToString(System.Globalization.CultureInfo.InvariantCulture)} -c:v libx264 -preset ultrafast -tune zerolatency -threads 0 -crf 23 -pix_fmt yuv420p \"{tempOutputFile}\"";

                await Task.Run(() =>
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = ffmpegExePath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    var errorBuilder = new StringBuilder();

                    using (var process = new Process { StartInfo = psi })
                    {
                        process.ErrorDataReceived += (s, e) =>
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                errorBuilder.AppendLine(e.Data);
                            }
                        };

                        process.Start();
                        process.BeginErrorReadLine();

                        using (Stream ffmpegIn = process.StandardInput.BaseStream)
                        using (Bitmap frameBuffer = new Bitmap(OutputWidth, OutputHeight, PixelFormat.Format24bppRgb))
                        using (Graphics g = Graphics.FromImage(frameBuffer))
                        {
                            // Optimized quality/speed balance
                            g.InterpolationMode = InterpolationMode.Bilinear;
                            g.SmoothingMode = SmoothingMode.HighSpeed;
                            g.PixelOffsetMode = PixelOffsetMode.HighSpeed;
                            g.CompositingQuality = CompositingQuality.HighSpeed;

                            byte[] frameBytes = new byte[OutputWidth * OutputHeight * 3];

                            for (int frame = 0; frame < totalFrames; frame++)
                            {
                                if (process.HasExited)
                                {
                                    throw new Exception($"FFmpeg closed unexpectedly at frame {frame}/{totalFrames}.\nError Output:\n{errorBuilder}");
                                }

                                double timeInSeconds = (double)frame / TargetFps;

                                g.Clear(Color.Black);
                                renderPreviewFrame(timeInSeconds, g);

                                BitmapData bmpData = frameBuffer.LockBits(
                                    new Rectangle(0, 0, OutputWidth, OutputHeight),
                                    ImageLockMode.ReadOnly,
                                    PixelFormat.Format24bppRgb);

                                try
                                {
                                    System.Runtime.InteropServices.Marshal.Copy(bmpData.Scan0, frameBytes, 0, frameBytes.Length);
                                    ffmpegIn.Write(frameBytes, 0, frameBytes.Length);
                                }
                                finally
                                {
                                    frameBuffer.UnlockBits(bmpData);
                                }

                                int percent = (int)(((double)(frame + 1) / totalFrames) * 100);
                                progress?.Report(percent);
                            }

                            ffmpegIn.Flush();
                            ffmpegIn.Close();
                        }

                        process.WaitForExit();

                        if (process.ExitCode != 0)
                        {
                            throw new Exception($"FFmpeg Export Failed with Exit Code {process.ExitCode}:\n{errorBuilder}");
                        }
                    }
                });

                if (File.Exists(tempOutputFile))
                {
                    if (File.Exists(outputPath))
                        File.Delete(outputPath);

                    File.Move(tempOutputFile, outputPath);
                }
            }
            finally
            {
                ClearExportCache();
            }
        }

        private static void ClearExportCache()
        {
            foreach (var bmp in ExportBitmapCache.Values)
            {
                bmp?.Dispose();
            }
            ExportBitmapCache.Clear();
        }
    }
}