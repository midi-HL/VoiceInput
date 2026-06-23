using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace VoiceInput
{
    public class LyricsRecognizer : IDisposable
    {
        private HttpClient? _httpClient;

        public event Action<string>? ProgressUpdated;
        public event Action<string>? RecognitionCompleted;

        public async Task<string> RecognizeAsync(string audioFilePath, CancellationToken cancellationToken = default)
        {
            string apiKey = Settings.ApiKey;
            string baseUrl = Settings.ApiBaseUrl.TrimEnd('/');
            string model = Settings.TranscriptionModel;

            if (string.IsNullOrEmpty(apiKey))
                throw new InvalidOperationException("API Key not configured");

            try
            {
                if (_httpClient == null)
                {
                    _httpClient = new HttpClient();
                    _httpClient.Timeout = TimeSpan.FromMinutes(5);
                }

                ProgressUpdated?.Invoke("正在上传音频文件...");

                using var formContent = new MultipartFormDataContent();
                var fileBytes = await File.ReadAllBytesAsync(audioFilePath, cancellationToken);
                var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
                formContent.Add(fileContent, "file", Path.GetFileName(audioFilePath));
                formContent.Add(new StringContent(model), "model");
                formContent.Add(new StringContent("verbose_json"), "response_format");
                formContent.Add(new StringContent(Settings.GetLanguageShortCode()), "language");

                ProgressUpdated?.Invoke("正在识别歌词...");

                var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/audio/transcriptions")
                {
                    Content = formContent
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var response = await _httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();

                string responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

                // Parse response
                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;

                string result = "";

                // Try to get segments with timestamps
                if (root.TryGetProperty("segments", out var segments))
                {
                    var sb = new System.Text.StringBuilder();
                    foreach (var segment in segments.EnumerateArray())
                    {
                        if (segment.TryGetProperty("start", out var start) &&
                            segment.TryGetProperty("text", out var text))
                        {
                            double startSeconds = start.GetDouble();
                            int minutes = (int)(startSeconds / 60);
                            int secs = (int)(startSeconds % 60);
                            int millis = (int)((startSeconds % 1) * 1000);
                            sb.AppendLine($"[{minutes:D2}:{secs:D2}.{millis / 10:D2}] {text.GetString()?.Trim()}");
                        }
                    }
                    result = sb.ToString().Trim();
                }
                else if (root.TryGetProperty("text", out var textElement))
                {
                    result = textElement.GetString() ?? "";
                }

                // Apply LLM correction if enabled
                if (Settings.LlmCorrectionEnabled && !string.IsNullOrEmpty(result))
                {
                    ProgressUpdated?.Invoke("纠错中...");
                    var refiner = new LlmRefiner();
                    result = await refiner.RefineAsync(result, cancellationToken);
                    refiner.Dispose();
                }

                RecognitionCompleted?.Invoke(result);
                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception($"歌词识别失败: {ex.Message}", ex);
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
