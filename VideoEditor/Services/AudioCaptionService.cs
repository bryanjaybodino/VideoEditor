using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VideoEditor.Models;

namespace VideoEditor.Services
{
    public class AudioCaptionService
    {
        private static readonly HttpClient httpClient = new HttpClient();

        // Environment variable or your active Gemini API key
        private static string ApiKey => Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "AQ.Ab8RN6I-K5DLkrztPPuzv9aM8Ij4MgutRAcZAblJ6iNsXklcIw";

        public async Task<List<Caption>> TranscribeAudioWithGemini(string audioFilePath)
        {
            if (!File.Exists(audioFilePath))
            {
                throw new FileNotFoundException("Audio file not found.", audioFilePath);
            }

            // 1. Read local audio file bytes and encode to base64 for audio-to-text processing
            byte[] audioBytes = await File.ReadAllBytesAsync(audioFilePath);
            string base64Audio = Convert.ToBase64String(audioBytes);
            string mimeType = GetMimeType(audioFilePath);

            // 2. Use stable Gemini endpoint
            string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash-lite:generateContent?key={ApiKey}";

            string prompt = "Listen to this audio file and transcribe the spoken words into text subtitles. " +
                            "Break down the transcript into short, readable caption segments with accurate start and end timestamps in seconds.";

            // 3. Request structured JSON output directly from Gemini
            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = prompt },
                            new
                            {
                                inline_data = new
                                {
                                    mime_type = mimeType,
                                    data = base64Audio
                                }
                            }
                        }
                    }
                },
                generationConfig = new
                {
                    response_mime_type = "application/json",
                    response_schema = new
                    {
                        type = "ARRAY",
                        items = new
                        {
                            type = "OBJECT",
                            properties = new
                            {
                                text = new { type = "STRING" },
                                start = new { type = "NUMBER" },
                                end = new { type = "NUMBER" }
                            },
                            required = new[] { "text", "start", "end" }
                        }
                    }
                }
            };

            string jsonPayload = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(url, content);
            string responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Gemini API Error: {responseString}");
            }

            return ParseGeminiResponse(responseString);
        }

        private List<Caption> ParseGeminiResponse(string jsonResponse)
        {
            var captions = new List<Caption>();

            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            string rawText = root
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            using var captionDoc = JsonDocument.Parse(rawText);
            foreach (var element in captionDoc.RootElement.EnumerateArray())
            {
                double startTime = element.GetProperty("start").GetDouble();
                double endTime = element.GetProperty("end").GetDouble();

                captions.Add(new Caption
                {
                    Text = element.GetProperty("text").GetString(),
                    StartTime = startTime,
                    EndTime = endTime
                });
            }

            return captions;
        }

        public void AddCaptionsToMedia(List<Caption> captions, List<MediaItem> mediaItems)
        {
            if (captions == null || captions.Count == 0) return;

            var audioItem = mediaItems.FirstOrDefault(x => x.Type == MediaType.Audio);
            double audioStartTime = audioItem?.StartTime ?? 0;

            foreach (var caption in captions)
            {
                var textLabel = new TextLabel
                {
                    Content = caption.Text,
                    // Relative coordinates (percentages)
                    RelativeX = 0.05f,
                    RelativeY = 0.65f,
                    RelativeWidth = 0.90f,
                    RelativeHeight = 0.25f, // Expanded vertical area for multi-line text

                    // Absolute fallback coordinates (1080x1920 base)
                    X = 54f,          // 5% of 1080
                    Y = 1248f,        // 65% of 1920
                    Width = 972f,     // 90% of 1080
                    Height = 480f,    // 25% of 1920

                    FontSize = 15f,   // Set to 15
                    TextColor = System.Drawing.Color.White,
                    BackgroundColor = System.Drawing.Color.FromArgb(180, 0, 0, 0),
                    IsBold = true
                };

                mediaItems.Add(new MediaItem
                {
                    Type = MediaType.Text,
                    StartTime = audioStartTime + caption.StartTime,
                    Duration = Math.Max(0.5, caption.EndTime - caption.StartTime),
                    TextData = textLabel,
                    TrackIndex = 1
                });
            }
        }

        private string GetMimeType(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLower();
            return ext switch
            {
                ".mp3" => "audio/mp3",
                ".wav" => "audio/wav",
                ".m4a" => "audio/m4a",
                ".ogg" => "audio/ogg",
                _ => "audio/mp3"
            };
        }
    }
}