using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace VoiceInput
{
    public class AiTranscription : IDisposable
    {
        private HttpClient? _httpClient;

        public event Action<string>? StatusChanged;

        public async Task<string> TranscribeAsync(string audioFilePath, CancellationToken cancellationToken = default)
        {
            string apiKey = Settings.ApiKey;
            string baseUrl = Settings.ApiBaseUrl.TrimEnd('/');
            string model = Settings.TranscriptionModel;

            if (string.IsNullOrEmpty(apiKey))
                throw new InvalidOperationException("API Key not configured");

            if (string.IsNullOrEmpty(model))
                throw new InvalidOperationException("Transcription model not configured");

            try
            {
                if (_httpClient == null)
                {
                    _httpClient = new HttpClient();
                    _httpClient.Timeout = TimeSpan.FromMinutes(3);
                }

                StatusChanged?.Invoke("正在上传音频...");

                using var formContent = new MultipartFormDataContent();

                var fileBytes = await File.ReadAllBytesAsync(audioFilePath, cancellationToken);
                var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
                formContent.Add(fileContent, "file", Path.GetFileName(audioFilePath));
                formContent.Add(new StringContent(model), "model");
                formContent.Add(new StringContent("json"), "response_format");
                formContent.Add(new StringContent(Settings.GetLanguageShortCode()), "language");

                StatusChanged?.Invoke("识别中...");

                var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/audio/transcriptions")
                {
                    Content = formContent
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var response = await _httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();

                string responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("text", out var textElement))
                {
                    return textElement.GetString() ?? "";
                }

                return "";
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception($"语音转写失败: {ex.Message}", ex);
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
