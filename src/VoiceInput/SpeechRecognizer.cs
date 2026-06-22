using System;
using System.Globalization;
using System.IO;
using System.Speech.Recognition;
using System.Threading;
using System.Threading.Tasks;

namespace VoiceInput
{
    public class SpeechRecognizer : IDisposable
    {
        private readonly AudioCapture _audioCapture;
        private readonly LlmRefiner _llmRefiner;
        private System.Speech.Recognition.SpeechRecognitionEngine? _localRecognizer;
        private string _currentLanguage = "zh-CN";
        private bool _isRecognizing;

        // 事件
        public event EventHandler<string>? RecognitionCompleted;
        public event EventHandler<string>? RecognitionFailed;
        public event EventHandler<string>? IntermediateResult;
        
        // 流式识别中间结果回调
        public event EventHandler<string>? StreamingResult;

        public SpeechRecognizer(AudioCapture audioCapture, LlmRefiner llmRefiner)
        {
            _audioCapture = audioCapture;
            _llmRefiner = llmRefiner;

            // 初始化本地识别器
            InitializeLocalRecognizer();
        }

        private void InitializeLocalRecognizer()
        {
            try
            {
                // 释放旧的识别器
                _localRecognizer?.Dispose();

                // 创建新的识别器
                _localRecognizer = new System.Speech.Recognition.SpeechRecognitionEngine(
                    new CultureInfo(_currentLanguage));

                // 配置语法
                var grammar = new DictationGrammar();
                _localRecognizer.LoadGrammar(grammar);

                // 设置事件处理
                _localRecognizer.SpeechRecognized += OnSpeechRecognized;
                _localRecognizer.SpeechHypothesized += OnSpeechHypothesized;
                _localRecognizer.SpeechRecognitionRejected += OnSpeechRecognitionRejected;

                // 设置输入
                _localRecognizer.SetInputToDefaultAudioDevice();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"初始化本地识别器失败: {ex.Message}");
                _localRecognizer = null;
            }
        }

        public void UpdateLanguage(string language)
        {
            _currentLanguage = language;
            InitializeLocalRecognizer();
        }

        public async Task RecognizeAsync()
        {
            if (_isRecognizing) return;

            _isRecognizing = true;

            try
            {
                string result;

                if (Settings.RecognitionMode == RecognitionMode.AiTranscription)
                {
                    // AI 语音转写模式（流式输出）
                    result = await RecognizeWithAiStreamAsync();
                }
                else
                {
                    // 本地识别模式
                    result = await RecognizeLocalAsync();
                }

                // LLM 纠错
                if (Settings.LlmCorrectionEnabled && !string.IsNullOrWhiteSpace(result))
                {
                    result = await _llmRefiner.RefineTextAsync(result);
                }

                // 触发完成事件
                RecognitionCompleted?.Invoke(this, result);
            }
            catch (Exception ex)
            {
                RecognitionFailed?.Invoke(this, ex.Message);
            }
            finally
            {
                _isRecognizing = false;
            }
        }

        private Task<string> RecognizeLocalAsync()
        {
            return Task.Run(() =>
            {
                if (_localRecognizer == null)
                {
                    throw new Exception("本地语音识别器未初始化");
                }

                var tcs = new TaskCompletionSource<string>();

                void handler(object? s, SpeechRecognizedEventArgs e)
                {
                    if (e.Result.Confidence >= 0.5)
                    {
                        tcs.TrySetResult(e.Result.Text);
                    }
                }

                void rejectedHandler(object? s, SpeechRecognitionRejectedEventArgs e)
                {
                    tcs.TrySetResult(string.Empty);
                }

                _localRecognizer.SpeechRecognized += handler;
                _localRecognizer.SpeechRecognitionRejected += rejectedHandler;

                try
                {
                    // 从 AudioCapture 获取音频数据并进行识别
                    // 注意：System.Speech.Recognition 需要直接从麦克风输入
                    // 这里我们使用 SetInputToDefaultAudioDevice 已经设置好了
                    _localRecognizer.RecognizeAsync(RecognizeMode.Multiple);

                    // 等待识别完成（通过超时或手动停止）
                    if (tcs.Task.Wait(TimeSpan.FromSeconds(30)))
                    {
                        return tcs.Task.Result;
                    }

                    return string.Empty;
                }
                finally
                {
                    _localRecognizer.SpeechRecognized -= handler;
                    _localRecognizer.SpeechRecognitionRejected -= rejectedHandler;
                    _localRecognizer.RecognizeAsyncStop();
                }
            });
        }

        /// <summary>
        /// 使用 AI API 流式转录音频
        /// </summary>
        private async Task<string> RecognizeWithAiStreamAsync()
        {
            // 获取录制的音频数据
            byte[] pcmData = _audioCapture.GetRecordedAudio();

            if (pcmData.Length == 0)
            {
                return string.Empty;
            }

            // 编码为 WAV 格式
            byte[] wavData = AudioCapture.EncodeToWav(pcmData);

            // 调用 AI API 进行流式转写，实时更新 HUD
            return await _llmRefiner.TranscribeAudioStreamAsync(wavData, _currentLanguage, (partialText) =>
            {
                // 触发流式结果事件，更新 HUD 显示
                StreamingResult?.Invoke(this, partialText);
            });
        }

        /// <summary>
        /// 使用 AI API 非流式转录音频
        /// </summary>
        private async Task<string> RecognizeWithAiAsync()
        {
            // 获取录制的音频数据
            byte[] pcmData = _audioCapture.GetRecordedAudio();

            if (pcmData.Length == 0)
            {
                return string.Empty;
            }

            // 编码为 WAV 格式
            byte[] wavData = AudioCapture.EncodeToWav(pcmData);

            // 调用 AI API 进行转写
            return await _llmRefiner.TranscribeAudioAsync(wavData, _currentLanguage);
        }

        private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
        {
            if (e.Result.Confidence >= 0.5)
            {
                IntermediateResult?.Invoke(this, e.Result.Text);
            }
        }

        private void OnSpeechHypothesized(object? sender, SpeechHypothesizedEventArgs e)
        {
            IntermediateResult?.Invoke(this, e.Result.Text);
        }

        private void OnSpeechRecognitionRejected(object? sender, SpeechRecognitionRejectedEventArgs e)
        {
            // 识别被拒绝，不触发事件
        }

        public void Dispose()
        {
            _localRecognizer?.Dispose();
            _localRecognizer = null;
        }
    }
}
