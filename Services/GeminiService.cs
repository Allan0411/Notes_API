using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System;
using System.IO;

namespace NotesAPI.Services
{
    public class GeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _geminiApiKey;
        private readonly string _textModel;
        private readonly string _imageModel;

        public GeminiService(IConfiguration config)
        {
            _httpClient = new HttpClient();
            _geminiApiKey = config["Gemini:ApiKey"];
            _textModel = config["Gemini:Model"] ?? "gemini-2.5-flash";
            _imageModel = config["Gemini_Image:Model"] ?? "gemini-2.0-flash-experimental";
        }

        /// <summary>
        /// Generate text content (FIXED variable names and JSON parsing)
        /// </summary>
        public async Task<string> GenerateContent(string prompt, string? base64Image = null, string mimeType = "image/png")
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_textModel}:generateContent";

            var requestParts = new List<object> { new { text = prompt } };

            if (!string.IsNullOrEmpty(base64Image))
            {
                requestParts.Add(new
                {
                    inlineData = new
                    {
                        mimeType,
                        data = base64Image
                    }
                });
            }

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = requestParts.ToArray()
                    }
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, url + $"?key={_geminiApiKey}")
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
            };

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Gemini API error: {response.StatusCode}: {responseContent}");
            }

            using var doc = JsonDocument.Parse(responseContent);
            var root = doc.RootElement;

            // FIXED: Proper JSON parsing with correct method names
            var candidates = root.GetProperty("candidates");
            if (candidates.ValueKind != JsonValueKind.Array || candidates.GetArrayLength() == 0)
            {
                throw new Exception("No candidates returned from Gemini API");
            }

            var firstCandidate = candidates[0];
            var content = firstCandidate.GetProperty("content");
            var responseParts = content.GetProperty("parts");

            if (responseParts.ValueKind != JsonValueKind.Array || responseParts.GetArrayLength() == 0)
            {
                throw new Exception("No parts returned in content");
            }

            // Get the first part that contains text
            foreach (var part in responseParts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var textElement))
                {
                    return textElement.GetString() ?? string.Empty;
                }
            }

            throw new Exception("No text found in response parts");
        }

        /// <summary>
        /// Generate image from text description using Gemini 2.0 Flash
        /// </summary>
        public async Task<string> GenerateImageFromText(string description)
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_imageModel}:generateContent";

            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[]
                        {
                            new { text = $"Generate a detailed, realistic, high-quality image based on this description: {description}" }
                        }
                    }
                },
                generationConfig = new
                {
                    responseModalities = new[] { "IMAGE" },
                    candidateCount = 1
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, url + $"?key={_geminiApiKey}")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Gemini 2.0 Image API error: {response.StatusCode}: {responseContent}");
            }

            using var doc = JsonDocument.Parse(responseContent);
            var root = doc.RootElement;

            // FIXED: Proper parsing with different variable names
            var candidates = root.GetProperty("candidates");
            if (candidates.ValueKind != JsonValueKind.Array || candidates.GetArrayLength() == 0)
            {
                throw new Exception("No candidates returned from image generation");
            }

            var firstCandidate = candidates[0];
            var content = firstCandidate.GetProperty("content");
            var imageParts = content.GetProperty("parts");

            foreach (var part in imageParts.EnumerateArray())
            {
                if (part.TryGetProperty("inlineData", out var inlineData))
                {
                    if (inlineData.TryGetProperty("data", out var imageData))
                    {
                        return imageData.GetString() ?? string.Empty;
                    }
                }
            }

            throw new Exception("No image data found in response");
        }

        /// <summary>
        /// Complete pipeline: sketch → description → refined image
        /// </summary>
        public async Task<RefineImageResult> RefineSketchToImage(string imageUrl, string? customInstructions = null)
        {
            try
            {
                // Step 1: Download and convert image to base64
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                var imageBytes = await httpClient.GetByteArrayAsync(imageUrl);
                var base64Image = Convert.ToBase64String(imageBytes);
                var mimeType = GetMimeTypeFromUrl(imageUrl);

                // Step 2: Get description from image using Gemini text model
                var descriptionPrompt = "Describe this sketch or image in detail. Focus on the main objects, shapes, colors, and composition. Be specific and detailed.";
                var description = await GenerateContent(descriptionPrompt, base64Image, mimeType);

                // Step 3: Generate refined image using Gemini 2.0 Flash
                var refinementPrompt = string.IsNullOrEmpty(customInstructions)
                    ? $"Create a detailed, realistic, high-quality image based on this description: {description}"
                    : $"{customInstructions} Based on this description: {description}";

                var refinedImageBase64 = await GenerateImageFromText(refinementPrompt);

                return new RefineImageResult
                {
                    OriginalDescription = description,
                    RefinedImageBase64 = refinedImageBase64,
                    OriginalImageUrl = imageUrl,
                    RefinementPrompt = refinementPrompt,
                    Success = true
                };
            }
            catch (Exception ex)
            {
                return new RefineImageResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private string GetMimeTypeFromUrl(string url)
        {
            try
            {
                var extension = Path.GetExtension(url.Split('?')[0]).ToLower();
                return extension switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    ".bmp" => "image/bmp",
                    _ => "image/png"
                };
            }
            catch
            {
                return "image/png";
            }
        }
    }

    public class RefineImageResult
    {
        public string? OriginalDescription { get; set; }
        public string? RefinedImageBase64 { get; set; }
        public string? OriginalImageUrl { get; set; }
        public string? RefinementPrompt { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
