using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace NotesAPI.Services
{
    public class GeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _geminiApiKey;
        private readonly string _model;

        public GeminiService(IConfiguration config)
        {
            _httpClient = new HttpClient();
            _geminiApiKey = config["Gemini:ApiKey"]; // Store API key in appsettings.json or env
            _model = config["Gemini:Model"] ?? "gemini-1.5-flash-001";
        }

        /// <summary>
        /// Sends a prompt to Gemini with optional Base64 image.
        /// </summary>
        /// <param name="prompt">Text prompt for Gemini</param>
        /// <param name="base64Image">Optional Base64-encoded image</param>
        /// <param name="mimeType">Image mime type (default: image/png)</param>
        public async Task<string> GenerateContent(string prompt, string? base64Image = null, string mimeType = "image/png")
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent";

            // Build "parts" dynamically
            var parts = new List<object> { new { text = prompt } };

            // If image is provided, attach it
            if (!string.IsNullOrEmpty(base64Image))
            {
                parts.Add(new
                {
                    inlineData = new
                    {
                        mimeType,
                        data = base64Image
                    }
                });
            }

            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = parts.ToArray()
                    }
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
                throw new System.Exception($"Gemini API error: {response.StatusCode}: {responseContent}");
            }

            using var doc = JsonDocument.Parse(responseContent);
            var root = doc.RootElement;

            // Extract generated text from response
            var text = root
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return text ?? string.Empty;
        }
    }
}
