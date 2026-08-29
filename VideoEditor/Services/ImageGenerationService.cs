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

            // Use the standard generateContent endpoint
            string requestUri = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite-image:generateContent?key={apiKey}";

            var jsonPayload = JsonSerializer.Serialize(new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = imagePrompt }
                        }
                    }
                },
                generationConfig = new
                {
                    responseModalities = new[] { "IMAGE" },
                    speechConfig = new { },
                    imageGenerationConfig = new
                    {
                        aspectRatio = "9:16"
                    }
                }
            });

            HttpResponseMessage response = null;
            string responseString = string.Empty;

            int maxRetries = 3;
            int delayMs = 5000;

            for (int retry = 0; retry <= maxRetries; retry++)
            {
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                response = await client.PostAsync(requestUri, content);
                responseString = await response.Content.ReadAsStringAsync();

                if ((int)response.StatusCode == 429)
                {
                    if (retry == maxRetries)
                    {
                        throw new Exception($"Gemini Quota Exceeded (429): Ensure Pay-As-You-Go billing is active.\n{responseString}");
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

            if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
            {
                var parts = candidates[0].GetProperty("content").GetProperty("parts");
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("inlineData", out var inlineData) || part.TryGetProperty("inline_data", out inlineData))
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

            throw new Exception("No image content returned inside the response parts.");
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