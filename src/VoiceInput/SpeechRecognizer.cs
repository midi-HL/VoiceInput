using System;
using System.IO;
using System.Speech.Recognition;
using System.Threading.Tasks;

namespace VoiceInput
{
    public class SpeechRecognizer : IDisposable
    {
        private SpeechRecognitionEngine? _engine;
        private bool _disposed;
        private string _intermediateResult = "";
        private TaskCompletionSource<string>? _recognitionTcs;

        public event Action<string>? IntermediateResult;
        public event Action<string>? FinalResult;

        public bool IsReady => _engine != null;

        public bool Initialize()
        {
            try
            {
                var culture = new System.Globalization.CultureInfo(Settings.GetLanguageCode());
                _engine = new SpeechRecognitionEngine(culture);

                // Load default dictation grammar
                _engine.LoadGrammar(new DictationGrammar());

                _engine.SpeechRecognized += OnSpeechRecognized;
                _engine.SpeechHypothesized += OnSpeechHypothesized;
                _engine.SpeechRecognitionRejected += OnRecognitionRejected;

                _engine.SetInputToDefaultAudioDevice();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Speech recognizer init failed: {ex.Message}");
                _engine = null;
                return false;
            }
        }

        public void StartRecognition()
        {
            if (_engine == null) return;
            _intermediateResult = "";
            _recognitionTcs = new TaskCompletionSource<string>();
            try
            {
                _engine.RecognizeAsync(RecognizeMode.Multiple);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Start recognition error: {ex.Message}");
                _recognitionTcs.TrySetResult("");
            }
        }

        public Task<string> StopRecognitionAsync()
        {
            if (_engine == null)
                return Task.FromResult("");

            try
            {
                _engine.RecognizeAsyncStop();
            }
            catch { }

            // Wait a bit for final result
            return Task.Run(async () =>
            {
                if (_recognitionTcs != null)
                {
                    var completed = await Task.WhenAny(_recognitionTcs.Task, Task.Delay(2000));
                    if (completed == _recognitionTcs.Task)
                        return _recognitionTcs.Task.Result;
                }
                return _intermediateResult;
            });
        }

        private void OnSpeechHypothesized(object? sender, SpeechHypothesizedEventArgs e)
        {
            string text = e.Result.Text ?? "";
            _intermediateResult = text;
            IntermediateResult?.Invoke(text);
        }

        private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
        {
            string text = e.Result.Text ?? "";
            _intermediateResult = text;
            IntermediateResult?.Invoke(text);
            FinalResult?.Invoke(text);
            _recognitionTcs?.TrySetResult(text);
        }

        private void OnRecognitionRejected(object? sender, SpeechRecognitionRejectedEventArgs e)
        {
            string text = e.Result.Text ?? "";
            if (!string.IsNullOrEmpty(text))
            {
                _intermediateResult = text;
            }
            _recognitionTcs?.TrySetResult(_intermediateResult);
        }

        public void Reinitialize()
        {
            DisposeEngine();
            Initialize();
        }

        private void DisposeEngine()
        {
            try
            {
                _engine?.RecognizeAsyncStop();
                _engine?.Dispose();
            }
            catch { }
            _engine = null;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                DisposeEngine();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }

        ~SpeechRecognizer()
        {
            Dispose();
        }
    }
}
