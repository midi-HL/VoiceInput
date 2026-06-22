using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace VoiceInput
{
    public class LlmRefiner
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        private const string SystemPrompt = @"你是语音转录后处理助手。你的唯一任务是修复明显的语音识别错误，例如：中文谐音错误、英文技术术语被错误转为中文（如""配森""→""Python""、""杰森""→""JSON""、""歌脱""→""Git""）。绝对禁止改写、润色、补充或删除任何看起来正确的内容。如果输入内容看起来已经正确，必须原样返回，不得做任何修改。";

        public LlmRefiner()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(60);

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        /// <summary>
        /// 使用 LLM 对转录文字进行纠错
        /// </summary>
        public async Task<string> RefineTextAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            try
            {
                string apiUrl = $"{Settings.ApiBaseUrl}/v1/chat/completions";

                var request = new
                {
                    model = Settings.LlmModel,
                    messages = new[]
                    {
                        new { role = "system", content = SystemPrompt },
                        new { role = "user", content = text }
                    },
                    temperature = 0.1,
                    max_tokens = 1024
                };

                string json = JsonSerializer.Serialize(request, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", Settings.ApiKey);

                var response = await _httpClient.PostAsync(apiUrl, content);
                response.EnsureSuccessStatusCode();

                string responseJson = await response.Content.ReadAsStringAsync();
                var responseObj = JsonSerializer.Deserialize<ChatCompletionResponse>(responseJson, _jsonOptions);

                if (responseObj?.Choices?.Length > 0)
                {
                    return responseObj.Choices[0].Message?.Content?.Trim() ?? text;
                }

                return text;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LLM 纠错失败: {ex.Message}");
                return text; // 失败时返回原文
            }
        }

        /// <summary>
        /// 使用 MiMo-V2.5-ASR 转录音频（流式输出）
        /// </summary>
        public async Task<string> TranscribeAudioStreamAsync(byte[] wavData, string language, Action<string>? onPartialResult = null)
        {
            if (wavData.Length == 0) return string.Empty;

            try
            {
                // 使用 chat/completions 端点
                string apiUrl = $"{Settings.ApiBaseUrl}/v1/chat/completions";

                // 将音频转换为 base64
                string base64Audio = Convert.ToBase64String(wavData);
                string audioDataUrl = $"data:audio/wav;base64,{base64Audio}";

                // 构建请求体（启用流式输出）
                var requestBody = new
                {
                    model = Settings.AsrModel,
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = new object[]
                            {
                                new
                                {
                                    type = "input_audio",
                                    input_audio = new
                                    {
                                        data = audioDataUrl
                                    }
                                }
                            }
                        }
                    },
                    asr_options = new
                    {
                        language = ConvertLanguageCode(language)
                    },
                    stream = true
                };

                string json = JsonSerializer.Serialize(requestBody, _jsonOptions);
                var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                _httpClient.DefaultRequestHeaders.Clear();
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", Settings.ApiKey);

                // 发送请求并获取流式响应
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                // 读取 SSE 流
                var fullText = new StringBuilder();
                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);

                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(line)) continue;

                    // SSE 格式：data: {...}
                    if (line.StartsWith("data: "))
                    {
                        var data = line[6..];
                        
                        // 检查结束标志
                        if (data == "[DONE]")
                        {
                            break;
                        }

                        try
                        {
                            var chunk = JsonSerializer.Deserialize<StreamChunk>(data, _jsonOptions);
                            var content = chunk?.Choices?.FirstOrDefault()?.Delta?.Content;
                            
                            if (!string.IsNullOrEmpty(content))
                            {
                                fullText.Append(content);
                                
                                // 调用回调更新 UI
                                onPartialResult?.Invoke(fullText.ToString());
                            }
                        }
                        catch (JsonException)
                        {
                            // 忽略解析错误
                        }
                    }
                }

                return fullText.ToString();
            }
            catch (Exception ex)
            {
                Logger.Error("AI 流式转写失败", ex);
                throw;
            }
        }

        /// <summary>
        /// 使用 MiMo-V2.5-ASR 转录音频（非流式）
        /// </summary>
        public async Task<string> TranscribeAudioAsync(byte[] wavData, string language)
        {
            if (wavData.Length == 0) return string.Empty;

            try
            {
                // 使用 chat/completions 端点
                string apiUrl = $"{Settings.ApiBaseUrl}/v1/chat/completions";

                // 将音频转换为 base64
                string base64Audio = Convert.ToBase64String(wavData);
                string audioDataUrl = $"data:audio/wav;base64,{base64Audio}";

                // 构建请求体
                var requestBody = new
                {
                    model = Settings.AsrModel,
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = new object[]
                            {
                                new
                                {
                                    type = "input_audio",
                                    input_audio = new
                                    {
                                        data = audioDataUrl
                                    }
                                }
                            }
                        }
                    },
                    asr_options = new
                    {
                        language = ConvertLanguageCode(language)
                    }
                };

                string json = JsonSerializer.Serialize(requestBody, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", Settings.ApiKey);

                var response = await _httpClient.PostAsync(apiUrl, content);
                response.EnsureSuccessStatusCode();

                string responseJson = await response.Content.ReadAsStringAsync();
                var responseObj = JsonSerializer.Deserialize<ChatCompletionResponse>(responseJson, _jsonOptions);

                if (responseObj?.Choices?.Length > 0)
                {
                    return responseObj.Choices[0].Message?.Content?.Trim() ?? string.Empty;
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                Logger.Error("AI 转写失败", ex);
                throw;
            }
        }

        /// <summary>
        /// 测试 API 连接
        /// </summary>
        public async Task<(bool success, string message)> TestConnectionAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Settings.ApiBaseUrl))
                {
                    return (false, "API Base URL 未配置");
                }

                if (string.IsNullOrWhiteSpace(Settings.ApiKey))
                {
                    return (false, "API Key 未配置");
                }

                // 测试 LLM 端点
                string apiUrl = $"{Settings.ApiBaseUrl}/v1/chat/completions";

                var request = new
                {
                    model = Settings.LlmModel,
                    messages = new[]
                    {
                        new { role = "user", content = "Hello" }
                    },
                    max_tokens = 10
                };

                string json = JsonSerializer.Serialize(request, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", Settings.ApiKey);

                var response = await _httpClient.PostAsync(apiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    return (true, "LLM 连接成功");
                }
                else
                {
                    return (false, $"LLM 连接失败: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"连接失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 测试 ASR 端点
        /// </summary>
        public async Task<(bool success, string message)> TestAsrConnectionAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Settings.ApiBaseUrl))
                {
                    return (false, "API Base URL 未配置");
                }

                if (string.IsNullOrWhiteSpace(Settings.ApiKey))
                {
                    return (false, "API Key 未配置");
                }

                if (string.IsNullOrWhiteSpace(Settings.AsrModel))
                {
                    return (false, "ASR 模型未配置");
                }

                // 测试 chat/completions 端点
                string apiUrl = $"{Settings.ApiBaseUrl}/v1/chat/completions";

                var request = new
                {
                    model = Settings.AsrModel,
                    messages = new[]
                    {
                        new { role = "user", content = "Hello" }
                    },
                    max_tokens = 10
                };

                string json = JsonSerializer.Serialize(request, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", Settings.ApiKey);

                var response = await _httpClient.PostAsync(apiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    return (true, "ASR 端点可用");
                }
                else
                {
                    return (false, $"ASR 端点返回: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"连接失败: {ex.Message}");
            }
        }

        private string ConvertLanguageCode(string code)
        {
            return code switch
            {
                "zh-CN" => "zh",
                "en-US" => "en",
                "zh-TW" => "zh",
                "ja-JP" => "ja",
                "ko-KR" => "ko",
                _ => code
            };
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }

        #region API 响应模型

        private class ChatCompletionResponse
        {
            public Choice[]? Choices { get; set; }
        }

        private class Choice
        {
            public Message? Message { get; set; }
            public Delta? Delta { get; set; }
        }

        private class Message
        {
            public string? Content { get; set; }
        }

        private class Delta
        {
            public string? Content { get; set; }
        }

        private class StreamChunk
        {
            public Choice[]? Choices { get; set; }
        }

        private class TranscriptionResponse
        {
            public string? Text { get; set; }
        }

        #endregion
    }
}
