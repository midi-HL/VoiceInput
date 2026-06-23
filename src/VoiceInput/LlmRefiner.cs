using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace VoiceInput
{
    public class LlmRefiner : IDisposable
    {
        private HttpClient? _httpClient;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private const string SystemPrompt =
            "你是语音转录后处理助手。你的唯一任务是修复明显的语音识别错误，例如：中文谐音错误、" +
            "英文技术术语被错误转为中文（如\"配森\"→\"Python\"、\"杰森\"→\"JSON\"、\"歌脱\"→\"Git\"）。" +
            "绝对禁止改写、润色、补充或删除任何看起来正确的内容。如果输入内容看起来已经正确，必须原样返回，不得做任何修改。";

        public event Action<string>? StreamChunkReceived;

        public async Task<string> RefineAsync(string input, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            string apiKey = Settings.ApiKey;
            string baseUrl = Settings.ApiBaseUrl.TrimEnd('/');
            string model = Settings.LlmModel;

            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(model))
                return input;

            try
            {
                if (_httpClient == null)
                {
                    _httpClient = new HttpClient();
                    _httpClient.Timeout = TimeSpan.FromSeconds(30);
                }

                var requestBody = new
                {
                    model = model,
                    messages = new object[]
                    {
                        new { role = "system", content = SystemPrompt },
                        new { role = "user", content = input }
                    },
                    stream = false,
                    temperature = 0.1,
                    max_tokens = 2048
                };

                string json = JsonSerializer.Serialize(requestBody, JsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/chat/completions")
                {
                    Content = content
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var response = await _httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();

                string responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var firstChoice = choices[0];
                    if (firstChoice.TryGetProperty("message", out var message) &&
                        message.TryGetProperty("content", out var contentElement))
                    {
                        return contentElement.GetString() ?? input;
                    }
                }

                return input;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LLM refine error: {ex.Message}");
                return input;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                if (_httpClient == null)
                {
                    _httpClient = new HttpClient();
                    _httpClient.Timeout = TimeSpan.FromSeconds(10);
                }

                string apiKey = Settings.ApiKey;
                string baseUrl = Settings.ApiBaseUrl.TrimEnd('/');

                if (string.IsNullOrEmpty(apiKey))
                    return false;

                var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/v1/models");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            _httpClient = null;
            GC.SuppressFinalize(this);
        }
    }
}
