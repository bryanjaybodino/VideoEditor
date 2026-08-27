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
        private const int TargetFps = 30;
        public static async Task ExportVideoAsync(
            List<MediaItem> mediaItems,
            string outputPath,
            double totalDuration,
            int frameRate = 30,
            int exportWidth = 1080,
            int exportHeight = 1920,
            IProgress<int> progress = null)
        {
            // 1. Create a temporary folder to store rendered bitmap frames
            string tempFrameFolder = Path.Combine(Path.GetTempPath(), "VideoEditor_ExportFrames_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempFrameFolder);

            int totalFrames = (int)Math.Ceiling(totalDuration * frameRate);
            double frameStep = 1.0 / frameRate;

            try
            {
                // 2. FRAME GENERATION LOOP
                await Task.Run(() =>
                {
                    for (int frameIndex = 0; frameIndex < totalFrames; frameIndex++)
                    {
                        double currentExportTime = frameIndex * frameStep;

                        // =========================================================================
                        // CALL RenderExportFrame HERE FOR EACH FRAME TIME POSITION
                        // =========================================================================
                        using (Bitmap frameBitmap = VideoRenderHelper.RenderExportFrame(mediaItems, currentExportTime, exportWidth, exportHeight))
                        {
                            string frameFileName = Path.Combine(tempFrameFolder, $"frame_{frameIndex:D6}.png");
                            frameBitmap.Save(frameFileName, ImageFormat.Png);
                        }

                        // Report rendering progress (0% - 80%)
                        int currentProgress = (int)(((double)frameIndex / totalFrames) * 80);
                        progress?.Report(currentProgress);
                    }
                });

                // 3. ENCODE FRAMES INTO MP4 USING FFMPEG
                string ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");

                if (!File.Exists(ffmpegPath))
                {
                    throw new FileNotFoundException("ffmpeg.exe was not found in the application directory. Please ensure FFmpeg is available.");
                }

                // Delete output file if it already exists
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }

                // FFmpeg command arguments to compile frame images into video
                string args = $"-r {frameRate} -i \"{Path.Combine(tempFrameFolder, "frame_%06d.png")}\" " +
                              $"-c:v libx264 -pix_fmt yuv420p -crf 18 -y \"{outputPath}\"";

                await Task.Run(() =>
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = args,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using (Process process = Process.Start(startInfo))
                    {
                        process.WaitForExit();
                    }
                });

                progress?.Report(100);
            }
            finally
            {
                // Clean up temporary PNG frames after encoding completes
                if (Directory.Exists(tempFrameFolder))
                {
                    Directory.Delete(tempFrameFolder, true);
                }
            }
        }
        public async Task ExportToVideo(List<MediaItem> items, string outputPath, Action<double, Graphics> renderPreviewFrame, IProgress<int> progress = null)
        {
            // 1. Verify FFmpeg binary location
            string ffmpegExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
            if (!File.Exists(ffmpegExePath))
            {
                // Fallback to system PATH environment
                ffmpegExePath = "ffmpeg.exe";
            }

            var imageItems = items.Where(x => x.Type == MediaType.Image).ToList();
            var audioItem = items.FirstOrDefault(x => x.Type == MediaType.Audio);

            if (imageItems.Count == 0)
                throw new Exception("No image clips found to export.");

            double totalDuration = items.Max(x => x.StartTime + x.Duration);
            int totalFrames = (int)(totalDuration * TargetFps);

            if (totalFrames <= 0)
                throw new Exception("Total video duration is invalid or 0 seconds.");

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

            string arguments = $"-y -f rawvideo -vcodec rawvideo -s {OutputWidth}x{OutputHeight} -pix_fmt bgr24 -r {TargetFps} -i - {audioArgs} -t {totalDuration.ToString(System.Globalization.CultureInfo.InvariantCulture)} -c:v libx264 -preset ultrafast -pix_fmt yuv420p \"{tempOutputFile}\"";

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
                    // Asynchronously log FFmpeg error messages to prevent pipe buffer deadlocks
                    process.ErrorDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            errorBuilder.AppendLine(e.Data);
                        }
                    };

                    try
                    {
                        process.Start();
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed to start ffmpeg.exe. Ensure 'ffmpeg.exe' exists in application directory.\nDetails: {ex.Message}");
                    }

                    process.BeginErrorReadLine();

                    using (Stream ffmpegIn = process.StandardInput.BaseStream)
                    using (Bitmap frameBuffer = new Bitmap(OutputWidth, OutputHeight, PixelFormat.Format24bppRgb))
                    using (Graphics g = Graphics.FromImage(frameBuffer))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

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
                                byte[] bytes = new byte[bmpData.Stride * OutputHeight];
                                System.Runtime.InteropServices.Marshal.Copy(bmpData.Scan0, bytes, 0, bytes.Length);
                                ffmpegIn.Write(bytes, 0, bytes.Length);
                            }
                            catch (Exception ex)
                            {
                                throw new Exception($"Failed writing frame {frame} to FFmpeg stdin stream.\nError: {ex.Message}");
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

            // Move exported temp file to final user target location
            if (File.Exists(tempOutputFile))
            {
                if (File.Exists(outputPath))
                    File.Delete(outputPath);

                File.Move(tempOutputFile, outputPath);
            }
            else
            {
                throw new Exception("Export failed: Output MP4 file was not created by FFmpeg.");
            }
        }
    }
}