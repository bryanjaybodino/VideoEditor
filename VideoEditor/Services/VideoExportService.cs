using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
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
                int totalFrames = (int)Math.Ceiling(totalDuration * TargetFps);

                if (totalFrames <= 0)
                    throw new Exception("Total video duration is invalid or 0 seconds.");

                string tempOutputFile = Path.Combine(Path.GetTempPath(), $"export_{Guid.NewGuid():N}.mp4");

                string totalDurationStr = totalDuration.ToString("0.00", CultureInfo.InvariantCulture);

                string audioInputs = "";
                string audioMapping = "-map 0:v:0";

                if (audioItem != null && File.Exists(audioItem.FilePath))
                {
                    double audioDuration = Math.Min(audioItem.Duration, totalDuration - audioItem.StartTime);
                    string ssStr = audioItem.StartTime.ToString("0.00", CultureInfo.InvariantCulture);
                    string durStr = audioDuration.ToString("0.00", CultureInfo.InvariantCulture);

                    audioInputs = $"-ss {ssStr} -t {durStr} -i \"{audioItem.FilePath}\"";
                    audioMapping = "-map 0:v:0 -map 1:a:0 -c:a aac -b:a 192k -shortest";
                }

                string arguments = $"-y -f rawvideo -pix_fmt bgr24 -s {OutputWidth}x{OutputHeight} -r {TargetFps} -i - {audioInputs} {audioMapping} -t {totalDurationStr} -c:v libx264 -preset ultrafast -pix_fmt yuv420p \"{tempOutputFile}\"";

                await Task.Run(async () =>
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

                    var errorLog = new StringBuilder();

                    using (var process = new Process { StartInfo = psi })
                    {
                        // Continuously read standard error to avoid pipe deadlock buffers filling up
                        process.ErrorDataReceived += (s, e) =>
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                lock (errorLog)
                                {
                                    errorLog.AppendLine(e.Data);
                                }
                            }
                        };

                        process.Start();
                        process.BeginErrorReadLine();

                        using (Stream ffmpegIn = process.StandardInput.BaseStream)
                        using (Bitmap frameBuffer = new Bitmap(OutputWidth, OutputHeight, PixelFormat.Format24bppRgb))
                        using (Graphics g = Graphics.FromImage(frameBuffer))
                        {
                            g.InterpolationMode = InterpolationMode.Bilinear;
                            g.SmoothingMode = SmoothingMode.HighSpeed;
                            g.PixelOffsetMode = PixelOffsetMode.HighSpeed;
                            g.CompositingQuality = CompositingQuality.HighSpeed;

                            byte[] frameBytes = new byte[OutputWidth * OutputHeight * 3];

                            for (int frame = 0; frame < totalFrames; frame++)
                            {
                                if (process.HasExited)
                                {
                                    string err;
                                    lock (errorLog) { err = errorLog.ToString(); }
                                    throw new Exception($"FFmpeg exited prematurely at frame {frame}/{totalFrames}.\nLog Output:\n{err}");
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
                                    await ffmpegIn.WriteAsync(frameBytes, 0, frameBytes.Length);
                                }
                                catch (Exception ex)
                                {
                                    string err;
                                    lock (errorLog) { err = errorLog.ToString(); }
                                    throw new Exception($"Failed to write frame {frame}/{totalFrames} to pipe.\nLog Output:\n{err}", ex);
                                }
                                finally
                                {
                                    frameBuffer.UnlockBits(bmpData);
                                }

                                int percent = (int)(((double)(frame + 1) / totalFrames) * 100);
                                progress?.Report(percent);
                            }

                            try
                            {
                                await ffmpegIn.FlushAsync();
                                ffmpegIn.Close();
                            }
                            catch { }
                        }

                        process.WaitForExit();

                        if (process.ExitCode != 0)
                        {
                            string err;
                            lock (errorLog) { err = errorLog.ToString(); }
                            throw new Exception($"FFmpeg Export Failed (Exit Code {process.ExitCode}):\n{err}");
                        }
                    }
                });

                if (File.Exists(tempOutputFile))
                {
                    if (File.Exists(outputPath))
                        File.Delete(outputPath);

                    File.Move(tempOutputFile, outputPath);
                }
                else
                {
                    throw new Exception("Export failed: Output MP4 file was not created.");
                }
            }
            finally
            {
                ClearExportCache();
            }
        }

        public static Bitmap GetExportImage(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath) && ExportBitmapCache.TryGetValue(filePath, out var cachedBmp))
            {
                return cachedBmp;
            }
            return null;
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