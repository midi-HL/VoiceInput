using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace VoiceInput
{
    public partial class App : Application
    {
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_HIDE = 0;

        private MainWindow? _mainWindow;
        private HudWindow? _hudWindow;
        private TrayIcon? _trayIcon;
        private KeyboardHook? _keyboardHook;
        private AudioCapture? _audioCapture;
        private SpeechRecognizer? _speechRecognizer;
        private LlmRefiner? _llmRefiner;
        private AiTranscription? _aiTranscription;

        private bool _isRecording;
        private bool _isProcessing;
        private string? _lastRecognizedText;

        public static App? Instance { get; private set; }

        public App()
        {
            Instance = this;
            this.InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            InitializeServices();
            InitializeTrayIcon();
            InitializeKeyboardHook();
        }

        private void InitializeServices()
        {
            _audioCapture = new AudioCapture();
            _audioCapture.RmsLevelChanged += OnRmsLevelChanged;

            _speechRecognizer = new SpeechRecognizer();
            _llmRefiner = new LlmRefiner();
            _aiTranscription = new AiTranscription();
        }

        private void InitializeTrayIcon()
        {
            _trayIcon = new TrayIcon();
            _trayIcon.ShowHomeRequested += () =>
            {
                EnsureMainWindow();
                _mainWindow?.NavigateToPage("Home");
                _mainWindow?.BringToFront();
            };
            _trayIcon.ShowSettingsRequested += () =>
            {
                EnsureMainWindow();
                _mainWindow?.NavigateToPage("Settings");
                _mainWindow?.BringToFront();
            };
            _trayIcon.ExitRequested += () =>
            {
                Shutdown();
            };
            _trayIcon.EnableChanged += (enabled) =>
            {
                if (enabled)
                    _keyboardHook?.Install();
                else
                    _keyboardHook?.Uninstall();
            };
            _trayIcon.Initialize();
        }

        private void InitializeKeyboardHook()
        {
            _keyboardHook = new KeyboardHook();
            _keyboardHook.TriggerKeyDown += OnTriggerKeyDown;
            _keyboardHook.TriggerKeyUp += OnTriggerKeyUp;
            _keyboardHook.Install();
        }

        private void OnTriggerKeyDown()
        {
            if (_isProcessing) return;

            if (Settings.RecognitionMode == RecognitionMode.LocalWithLlmCorrection)
            {
                // Mode A: Local recognition
                if (!_speechRecognizer!.IsReady)
                {
                    _speechRecognizer.Reinitialize();
                }
                _speechRecognizer.StartRecognition();
            }
            else
            {
                // Mode B: AI transcription - just record
                _audioCapture!.StartRecording();
            }

            _isRecording = true;

            // Show HUD
            ShowHud();
        }

        private async void OnTriggerKeyUp()
        {
            if (!_isRecording || _isProcessing) return;

            _isRecording = false;
            _isProcessing = true;

            string? transcribedText = null;

            try
            {
                if (Settings.RecognitionMode == RecognitionMode.LocalWithLlmCorrection)
                {
                    // Mode A: Stop local recognition
                    UpdateHudText("识别中...");
                    transcribedText = await _speechRecognizer!.StopRecognitionAsync();
                }
                else
                {
                    // Mode B: Stop recording and send to API
                    string? audioFile = _audioCapture!.StopRecording();
                    if (!string.IsNullOrEmpty(audioFile))
                    {
                        UpdateHudText("识别中...");
                        transcribedText = await _aiTranscription!.TranscribeAsync(audioFile);

                        // Cleanup temp file
                        try { System.IO.File.Delete(audioFile); } catch { }
                    }
                }

                // Apply LLM correction if enabled
                if (Settings.LlmCorrectionEnabled && !string.IsNullOrEmpty(transcribedText))
                {
                    UpdateHudText("纠错中...");
                    transcribedText = await _llmRefiner!.RefineAsync(transcribedText);
                }

                // Inject text
                if (!string.IsNullOrEmpty(transcribedText))
                {
                    _lastRecognizedText = transcribedText;
                    UpdateHudText(transcribedText);
                    await Task.Delay(300); // Brief delay to show result
                    await ClipboardInjector.InjectTextAsync(transcribedText);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Processing error: {ex.Message}");
                UpdateHudText("识别失败");
                await Task.Delay(1000);
            }
            finally
            {
                _isProcessing = false;
                HideHud();
            }
        }

        private void OnRmsLevelChanged(float rmsLevel)
        {
            if (_isRecording)
            {
                UpdateHudRmsLevel(rmsLevel);
            }
        }

        public void ShowHud()
        {
            if (_hudWindow == null)
            {
                _hudWindow = new HudWindow();
            }
            _hudWindow.ShowAnimated();
        }

        public void HideHud()
        {
            _hudWindow?.HideAnimated();
        }

        public void UpdateHudText(string text)
        {
            _hudWindow?.UpdateText(text);
        }

        public void UpdateHudRmsLevel(float level)
        {
            _hudWindow?.UpdateRmsLevel(level);
        }

        public void EnsureMainWindow()
        {
            if (_mainWindow == null)
            {
                _mainWindow = new MainWindow();
                _mainWindow.AppWindow.Closing += (s, e) =>
                {
                    if (Settings.CloseBehavior == CloseBehavior.MinimizeToTray)
                    {
                        e.Cancel = true;
                        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_mainWindow);
                        ShowWindow(hwnd, SW_HIDE);
                    }
                    else
                    {
                        Shutdown();
                    }
                };
            }
        }

        public void ShowMainWindow(string? initialPage = null)
        {
            EnsureMainWindow();
            if (initialPage != null)
            {
                _mainWindow?.NavigateToPage(initialPage);
            }
            _mainWindow?.BringToFront();
        }

        public void Shutdown()
        {
            _keyboardHook?.Dispose();
            _audioCapture?.Dispose();
            _speechRecognizer?.Dispose();
            _llmRefiner?.Dispose();
            _aiTranscription?.Dispose();
            _trayIcon?.Dispose();
            _hudWindow?.Close();
            _mainWindow?.Close();
            Current.Exit();
        }
    }
}
