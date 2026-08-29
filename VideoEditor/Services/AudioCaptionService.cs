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

        public async Task<List<Caption>> TranscribeAudioWithGemini(string audioFilePath, string ApiKey)
        {
            if (!File.Exists(audioFilePath))
            {
                throw new FileNotFoundException("Audio file not found.", audioFilePath);
            }

            byte[] audioBytes = await File.ReadAllBytesAsync(audioFilePath);
            string base64Audio = Convert.ToBase64String(audioBytes);
            string mimeType = GetMimeType(audioFilePath);

            string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash-lite:generateContent?key={ApiKey}";

            // UPDATE 1: Explicitly ask for grouped phrases/sentences (3 to 8 words per segment)
            string prompt = "Listen to this audio file and transcribe the spoken words into text subtitles. " +
                            "Group words into full sentences or natural phrasal chunks (3-8 words per line). " +
                            "Do NOT transcribe word-by-word. Provide accurate start and end timestamps for each group.";

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
                    // UPDATE 2: Provide descriptions inside the JSON schema to enforce chunking behavior
                    response_schema = new
                    {
                        type = "ARRAY",
                        description = "List of full phrase or sentence caption segments.",
                        items = new
                        {
                            type = "OBJECT",
                            properties = new
                            {
                                text = new
                                {
                                    type = "STRING",
                                    description = "A grouped line of spoken text containing multiple words or full sentences."
                                },
                                start = new { type = "NUMBER", description = "Start timestamp in seconds for the sentence or phrase." },
                                end = new { type = "NUMBER", description = "End timestamp in seconds for the sentence or phrase." }
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

            var audioItem = mediaItems.FirstOrDefault(x => x.Type == MediaType.Audio); //
            double audioStartTime = audioItem?.StartTime ?? 0; //[cite: 6]

            // 1. Find the highest existing visual track index (excluding Audio)
            var visualItems = mediaItems.Where(x => x.Type != MediaType.Audio).ToList(); //
            int targetTrackIndex = visualItems.Any() ? visualItems.Max(x => x.TrackIndex) + 1 : 0; //[cite: 2]

            // 2. Add auto-captions onto targetTrackIndex (bottom row directly above Audio)
            foreach (var caption in captions) //[cite: 6]
            {
                var textLabel = new TextLabel //[cite: 6]
                {
                    Content = caption.Text, //[cite: 6]
                                            // Relative coordinates (percentages)
                    RelativeX = 0.05f, //[cite: 6]
                    RelativeY = 0.65f, //[cite: 6]
                    RelativeWidth = 0.90f, //[cite: 6]
                    RelativeHeight = 0.25f, //[cite: 6]

                    // Absolute fallback coordinates (1080x1920 base)
                    X = 54f,          // 5% of 1080[cite: 6]
                    Y = 1248f,        // 65% of 1920[cite: 6]
                    Width = 972f,     // 90% of 1080[cite: 6]
                    Height = 480f,    // 25% of 1920[cite: 6]

                    FontSize = 30f,   //[cite: 6]
                    TextColor = System.Drawing.Color.White, //[cite: 6]
                    BackgroundColor = System.Drawing.Color.FromArgb(180, 0, 0, 0), //[cite: 6]
                    IsBold = true //[cite: 6]
                };

                mediaItems.Add(new MediaItem //[cite: 6]
                {
                    Type = MediaType.Text, //[cite: 6]
                    StartTime = audioStartTime + caption.StartTime, //[cite: 6]
                    Duration = Math.Max(0.5, caption.EndTime - caption.StartTime), //[cite: 6]
                    TextData = textLabel, //[cite: 6]
                    TrackIndex = targetTrackIndex // Placed on the bottom-most visual row
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