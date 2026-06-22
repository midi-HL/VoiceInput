using System;
using System.Threading;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace VoiceInput
{
    public partial class App : System.Windows.Application
    {
        private static Mutex? _mutex;
        private TrayIcon? _trayIcon;
        private KeyboardHook? _keyboardHook;
        private AudioCapture? _audioCapture;
        private SpeechRecognizer? _speechRecognizer;
        private ClipboardInjector? _clipboardInjector;
        private LlmRefiner? _llmRefiner;
        private HudWindow? _hudWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 单实例检查
            _mutex = new Mutex(true, "VoiceInput_SingleInstance_Mutex", out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show("VoiceInput 已经在运行中。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            // 初始化设置
            Settings.Initialize();

            // 初始化核心组件
            _clipboardInjector = new ClipboardInjector();
            _llmRefiner = new LlmRefiner();
            _audioCapture = new AudioCapture();
            _speechRecognizer = new SpeechRecognizer(_audioCapture, _llmRefiner);
            _hudWindow = new HudWindow();

            // 初始化系统托盘
            _trayIcon = new TrayIcon();
            _trayIcon.SettingsRequested += OnSettingsRequested;
            _trayIcon.ExitRequested += OnExitRequested;
            _trayIcon.LanguageChanged += OnLanguageChanged;
            _trayIcon.ModeChanged += OnModeChanged;
            _trayIcon.EnableToggled += OnEnableToggled;

            // 初始化键盘钩子
            _keyboardHook = new KeyboardHook();
            _keyboardHook.KeyDown += OnKeyDown;
            _keyboardHook.KeyUp += OnKeyUp;

            // 语音识别完成事件
            _speechRecognizer.RecognitionCompleted += OnRecognitionCompleted;
            _speechRecognizer.RecognitionFailed += OnRecognitionFailed;

            // 隐藏主窗口
            MainWindow = null;
        }

        private void OnKeyDown(object? sender, EventArgs e)
        {
            if (!Settings.IsEnabled) return;

            // 开始录音
            _audioCapture.StartCapture();
            _hudWindow.ShowRecording();
        }

        private async void OnKeyUp(object? sender, EventArgs e)
        {
            if (!Settings.IsEnabled) return;

            // 停止录音并开始识别
            _audioCapture.StopCapture();
            _hudWindow.ShowRecognizing();

            try
            {
                await _speechRecognizer.RecognizeAsync();
            }
            catch (Exception ex)
            {
                _hudWindow.ShowError(ex.Message);
            }
        }

        private async void OnRecognitionCompleted(object? sender, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                _hudWindow.Hide();
                return;
            }

            // 注入文字
            await _clipboardInjector.InjectTextAsync(text);
            _hudWindow.ShowCompleted(text);

            // 延迟隐藏
            await System.Threading.Tasks.Task.Delay(1500);
            _hudWindow.Hide();
        }

        private void OnRecognitionFailed(object? sender, string error)
        {
            _hudWindow.ShowError(error);
        }

        private void OnSettingsRequested(object? sender, EventArgs e)
        {
            var settingsWindow = new SettingsWindow();
            settingsWindow.ShowDialog();
        }

        private void OnExitRequested(object? sender, EventArgs e)
        {
            Shutdown();
        }

        private void OnLanguageChanged(object? sender, string language)
        {
            Settings.RecognitionLanguage = language;
            _speechRecognizer.UpdateLanguage(language);
        }

        private void OnModeChanged(object? sender, RecognitionMode mode)
        {
            Settings.RecognitionMode = mode;
        }

        private void OnEnableToggled(object? sender, bool isEnabled)
        {
            Settings.IsEnabled = isEnabled;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // 清理资源
            _keyboardHook?.Dispose();
            _audioCapture?.Dispose();
            _speechRecognizer?.Dispose();
            _trayIcon?.Dispose();
            _hudWindow?.Close();
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();

            base.OnExit(e);
        }
    }
}
