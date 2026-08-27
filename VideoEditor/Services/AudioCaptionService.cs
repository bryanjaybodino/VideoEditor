using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VideoEditor.Models;

namespace VideoEditor.Services
{
    public class AudioCaptionService
    {
        public async Task GenerateCaptions(string audioFilePath, List<MediaItem> mediaItems)
        {
            try
            {
                var captions = await TranscribeAudioWithWhisper(audioFilePath);
                AddCaptionsToMedia(captions, mediaItems);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to generate captions: {ex.Message}", ex);
            }
        }

        private async Task<List<Caption>> TranscribeAudioWithWhisper(string audioFilePath)
        {
            var scriptPath = Path.Combine(Path.GetTempPath(), $"whisper_transcribe_{Guid.NewGuid()}.py");
            string escapedAudioPath = audioFilePath.Replace("\\", "\\\\").Replace("'", "\\'");
            var pythonScript = GenerateWhisperScript(escapedAudioPath);

            try
            {
                File.WriteAllText(scriptPath, pythonScript);

                var processInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{scriptPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();
                    await Task.Run(() => process.WaitForExit());

                    if (process.ExitCode != 0)
                    {
                        throw new InvalidOperationException($"Whisper execution error: {error}");
                    }

                    return ParseWhisperOutput(output);
                }
            }
            finally
            {
                if (File.Exists(scriptPath)) File.Delete(scriptPath);
            }
        }

        private string GenerateWhisperScript(string escapedPath)
        {
            return $@"
import whisper
import json
import sys

try:
    model = whisper.load_model('base')
    result = model.transcribe('{escapedPath}')
    output = {{
        'segments': [
            {{
                'text': segment['text'].strip(),
                'start': segment['start'],
                'end': segment['end']
            }}
            for segment in result['segments']
        ]
    }}
    print(json.dumps(output, ensure_ascii=False))
except Exception as e:
    sys.stderr.write(str(e))
    sys.exit(1)
";
        }

        private List<Caption> ParseWhisperOutput(string jsonOutput)
        {
            var captions = new List<Caption>();
            var lines = jsonOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var textMatch = System.Text.RegularExpressions.Regex.Match(line, @"""text""\s*:\s*""([^""]*)""");
                var startMatch = System.Text.RegularExpressions.Regex.Match(line, @"""start""\s*:\s*([0-9.]+)");
                var endMatch = System.Text.RegularExpressions.Regex.Match(line, @"""end""\s*:\s*([0-9.]+)");

                if (textMatch.Success && startMatch.Success && endMatch.Success)
                {
                    captions.Add(new Caption
                    {
                        Text = textMatch.Groups[1].Value,
                        StartTime = double.Parse(startMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                        EndTime = double.Parse(endMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),

                    });
                }
            }
            return captions;
        }

        private void AddCaptionsToMedia(List<Caption> captions, List<MediaItem> mediaItems)
        {
            if (captions.Count == 0) return;

            foreach (var item in mediaItems)
            {
                if (item.Type == MediaType.Image)
                {
                    var itemCaptions = captions.Where(c => c.StartTime >= item.StartTime && c.StartTime < item.StartTime + item.Duration).ToList();
                    foreach (var caption in itemCaptions)
                    {
                        item.TextLabels.Add(new TextLabel
                        {
                            Content = caption.Text,
                            X = 200,
                            Y = 400,
                            FontSize = 28,
                            TextColor = System.Drawing.Color.Yellow,
                            FontFamily = "Segoe UI",
                            StartTime = caption.StartTime - item.StartTime,
                            Duration = caption.EndTime - caption.StartTime,
                            IsBold = true
                        });
                    }
                }
            }
        }
    }
}