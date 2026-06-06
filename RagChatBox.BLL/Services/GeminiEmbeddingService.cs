using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RagChatBox.BLL.Interfaces;

namespace RagChatBox.BLL.Services
{
    public class GeminiEmbeddingService : IEmbeddingService
    {
        private static DateTime _cooldownUntilUtc = DateTime.MinValue;
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _endpoint;
        private readonly string _model;
        private readonly int _dimensions;
        private readonly ILogger<GeminiEmbeddingService> _logger;

        public GeminiEmbeddingService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<GeminiEmbeddingService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiKey = FirstNonEmpty(
                configuration["EmbeddingSettings:ApiKey"],
                configuration["LlmSettings:ApiKey"],
                Environment.GetEnvironmentVariable("GEMINI_API_KEY"));
            _endpoint = configuration["EmbeddingSettings:Endpoint"] ?? "https://generativelanguage.googleapis.com/v1beta";
            _model = configuration["EmbeddingSettings:Model"] ?? "gemini-embedding-2";
            _dimensions = int.TryParse(configuration["EmbeddingSettings:Dimensions"], out var dimensions)
                ? dimensions
                : 768;
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
        }

        public Task<float[]?> GenerateDocumentEmbeddingAsync(string text, string? title = null)
        {
            var safeTitle = string.IsNullOrWhiteSpace(title) ? "none" : title.Trim();
            return GenerateEmbeddingAsync($"title: {safeTitle} | text: {text}");
        }

        public Task<float[]?> GenerateQueryEmbeddingAsync(string query)
        {
            return GenerateEmbeddingAsync($"task: question answering | query: {query}");
        }

        private async Task<float[]?> GenerateEmbeddingAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(_apiKey) || string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Gemini embedding skipped because API key or text is empty.");
                return null;
            }

            if (DateTime.UtcNow < _cooldownUntilUtc)
            {
                _logger.LogWarning("Gemini embedding skipped because service is cooling down until {CooldownUntilUtc}.", _cooldownUntilUtc);
                return null;
            }

            try
            {
                var url = $"{_endpoint.TrimEnd('/')}/models/{_model}:embedContent";
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("x-goog-api-key", _apiKey);

                var payload = new
                {
                    content = new
                    {
                        parts = new[]
                        {
                            new { text }
                        }
                    },
                    output_dimensionality = _dimensions
                };

                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    if (response.StatusCode == (HttpStatusCode)429)
                    {
                        _cooldownUntilUtc = DateTime.UtcNow.Add(GetRetryDelay(response));
                    }

                    _logger.LogWarning(
                        "Gemini embedding failed. StatusCode={StatusCode}, Model={Model}, Error={Error}",
                        (int)response.StatusCode,
                        _model,
                        errorBody);
                    return null;
                }

                var responseString = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseString);
                var root = doc.RootElement;

                if (root.TryGetProperty("embedding", out var embedding)
                    && embedding.TryGetProperty("values", out var values))
                {
                    return values.EnumerateArray().Select(v => v.GetSingle()).ToArray();
                }

                if (root.TryGetProperty("embeddings", out var embeddings)
                    && embeddings.GetArrayLength() > 0
                    && embeddings[0].TryGetProperty("values", out var firstValues))
                {
                    return firstValues.EnumerateArray().Select(v => v.GetSingle()).ToArray();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while calling Gemini embedding API.");
            }

            return null;
        }

        private static TimeSpan GetRetryDelay(HttpResponseMessage response)
        {
            if (response.Headers.RetryAfter?.Delta is { } delta)
            {
                return delta;
            }

            if (response.Headers.RetryAfter?.Date is { } date)
            {
                var delay = date.UtcDateTime - DateTime.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    return delay;
                }
            }

            return TimeSpan.FromSeconds(60);
        }
    }
}
