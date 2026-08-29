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
    public class ImageGenerationService
    {
        private static readonly HttpClient client = new HttpClient();

        public async Task<string> GenerateAndSaveImageAsync(string imagePrompt, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key cannot be empty.");

            string requestUri = "https://generativelanguage.googleapis.com/v1beta/interactions";

            // Payload matches the exact structure from your working curl sample
            var jsonPayload = JsonSerializer.Serialize(new
            {
                model = "models/gemini-3.1-flash-lite-image",
                input = new object[]
                {
                    new
                    {
                        type = "text",
                        text = imagePrompt
                    }
                },
                generation_config = new
                {
                    image_config = new
                    {
                        aspect_ratio = "9:16"
                    }
                }
            });

            HttpResponseMessage response = null;
            string responseString = string.Empty;

            int maxRetries = 3;
            int delayMs = 5000;

            for (int retry = 0; retry <= maxRetries; retry++)
            {
                using (var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri))
                {
                    // Required headers for the 2026 Interactions API
                    httpRequest.Headers.Add("x-goog-api-key", apiKey);
                    httpRequest.Headers.Add("Api-Revision", "2026-05-20");
                    httpRequest.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                    response = await client.SendAsync(httpRequest);
                    responseString = await response.Content.ReadAsStringAsync();
                }

                if ((int)response.StatusCode == 429)
                {
                    if (retry == maxRetries)
                    {
                        throw new Exception($"Gemini Quota Exceeded (429):\n{responseString}");
                    }

                    await Task.Delay(delayMs);
                    delayMs *= 2;
                    continue;
                }

                break;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Gemini API Error ({response.StatusCode}): {responseString}");
            }

            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;

            // Parse response structure from Interactions API
            if (root.TryGetProperty("outputs", out var outputs) && outputs.GetArrayLength() > 0)
            {
                foreach (var output in outputs.EnumerateArray())
                {
                    if (output.TryGetProperty("type", out var type) && type.GetString() == "image")
                    {
                        if (output.TryGetProperty("data", out var data))
                        {
                            string base64Image = data.GetString();
                            byte[] imageBytes = Convert.FromBase64String(base64Image);

                            string outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GeneratedImages");
                            Directory.CreateDirectory(outputDir);

                            string filePath = Path.Combine(outputDir, $"img_{Guid.NewGuid():N}.jpg");
                            await File.WriteAllBytesAsync(filePath, imageBytes);

                            return filePath;
                        }
                    }
                }
            }
            // Fallback candidate check if nested under content parts
            else if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
            {
                var parts = candidates[0].GetProperty("content").GetProperty("parts");
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("inline_data", out var inlineData) || part.TryGetProperty("inlineData", out inlineData))
                    {
                        string base64Image = inlineData.GetProperty("data").GetString();
                        byte[] imageBytes = Convert.FromBase64String(base64Image);

                        string outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GeneratedImages");
                        Directory.CreateDirectory(outputDir);

                        string filePath = Path.Combine(outputDir, $"img_{Guid.NewGuid():N}.jpg");
                        await File.WriteAllBytesAsync(filePath, imageBytes);

                        return filePath;
                    }
                }
            }

            throw new Exception("No image output returned from the response.");
        }

        public void AddImagesToMedia(List<(string FilePath, double StartTime, double Duration)> imageSegments, List<MediaItem> mediaItems)
        {
            if (imageSegments == null || imageSegments.Count == 0) return;

            var audioItem = mediaItems.FirstOrDefault(x => x.Type == MediaType.Audio);
            double audioStartTime = audioItem?.StartTime ?? 0;

            var visualItems = mediaItems.Where(x => x.Type != MediaType.Audio).ToList();
            int targetTrackIndex = visualItems.Any() ? visualItems.Max(x => x.TrackIndex) + 1 : 0;

            foreach (var segment in imageSegments)
            {
                if (string.IsNullOrEmpty(segment.FilePath)) continue;

                double halfDuration = segment.Duration / 2.0;

                mediaItems.Add(new MediaItem
                {
                    FilePath = segment.FilePath,
                    Type = MediaType.Image,
                    StartTime = audioStartTime + segment.StartTime,
                    Duration = Math.Max(0.5, segment.Duration),
                    OriginalDuration = Math.Max(0.5, segment.Duration),
                    TrackIndex = targetTrackIndex,
                    InEffect = new TransitionEffect { Type = "DynamicZoomBlur", Duration = halfDuration },
                    OutEffect = new TransitionEffect { Type = "DynamicZoomBlur", Duration = halfDuration }
                });
            }
        }
    }
}