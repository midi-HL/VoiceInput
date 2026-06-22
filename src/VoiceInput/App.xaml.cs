using System;
using System.Threading;
using System.Threading.Tasks;
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

            Logger.Info("=== VoiceInput 启动 ===");
            Logger.Info($"版本: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}");
            Logger.Info($"操作系统: {Environment.OSVersion}");
            Logger.Info($"运行时: {Environment.Version}");

            // 注册全局异常处理
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                Logger.Error("AppDomain.UnhandledException", ex);
                ShowErrorDialog("未处理的异常", ex);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                Logger.Error("DispatcherUnhandledException", args.Exception);
                ShowErrorDialog("UI线程异常", args.Exception);
                args.Handled = true;
            };

            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                Logger.Error("TaskScheduler.UnobservedTaskException", args.Exception);
                args.SetObserved();
            };

            try
            {
                // 单实例检查
                _mutex = new Mutex(true, "VoiceInput_SingleInstance_Mutex", out bool createdNew);
                if (!createdNew)
                {
                    Logger.Warn("检测到已有实例运行，退出");
                    MessageBox.Show("VoiceInput 已经在运行中。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    Shutdown();
                    return;
                }

                Logger.Info("初始化设置...");
                Settings.Initialize();

                Logger.Info("初始化剪贴板注入器...");
                _clipboardInjector = new ClipboardInjector();

                Logger.Info("初始化 LLM 纠错器...");
                _llmRefiner = new LlmRefiner();

                Logger.Info("初始化音频捕获...");
                try
                {
                    _audioCapture = new AudioCapture();
                    Logger.Info("音频捕获初始化成功");
                }
                catch (Exception audioEx)
                {
                    Logger.Error("音频捕获初始化失败", audioEx);
                    ShowErrorDialog("音频设备初始化失败", audioEx, 
                        "请检查：\n1. 麦克风是否已连接\n2. 麦克风是否已启用\n3. 应用是否有麦克风权限");
                    Shutdown();
                    return;
                }

                Logger.Info("初始化语音识别器...");
                _speechRecognizer = new SpeechRecognizer(_audioCapture, _llmRefiner);

                Logger.Info("初始化 HUD 窗口...");
                _hudWindow = new HudWindow();

                Logger.Info("初始化系统托盘...");
                _trayIcon = new TrayIcon();
                _trayIcon.SettingsRequested += OnSettingsRequested;
                _trayIcon.ExitRequested += OnExitRequested;
                _trayIcon.LanguageChanged += OnLanguageChanged;
                _trayIcon.ModeChanged += OnModeChanged;
                _trayIcon.EnableToggled += OnEnableToggled;

                Logger.Info("初始化键盘钩子...");
                try
                {
                    _keyboardHook = new KeyboardHook();
                    Logger.Info("键盘钩子初始化成功");
                }
                catch (Exception hookEx)
                {
                    Logger.Error("键盘钩子初始化失败", hookEx);
                    ShowErrorDialog("键盘钩子初始化失败", hookEx,
                        "请尝试以管理员权限运行程序");
                    Shutdown();
                    return;
                }
                
                _keyboardHook.KeyDown += OnKeyDown;
                _keyboardHook.KeyUp += OnKeyUp;

                // 语音识别完成事件
                _speechRecognizer.RecognitionCompleted += OnRecognitionCompleted;
                _speechRecognizer.RecognitionFailed += OnRecognitionFailed;

                // 隐藏主窗口
                MainWindow = null;

                Logger.Info("=== 启动完成 ===");
            }
            catch (Exception ex)
            {
                Logger.Error("启动失败", ex);
                ShowErrorDialog("启动失败", ex);
                Shutdown();
            }
        }

        private void ShowErrorDialog(string title, Exception? ex, string? additionalInfo = null)
        {
            var message = $"VoiceInput {title}\n\n";
            
            if (ex != null)
            {
                message += $"错误类型: {ex.GetType().Name}\n";
                message += $"错误信息: {ex.Message}\n";
                
                if (ex.InnerException != null)
                {
                    message += $"\n内部错误: {ex.InnerException.Message}\n";
                }
            }

            if (!string.IsNullOrEmpty(additionalInfo))
            {
                message += $"\n{additionalInfo}\n";
            }

            message += $"\n日志文件: {Logger.GetLogFilePath()}";

            try
            {
                MessageBox.Show(message, "VoiceInput 错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { }
        }

        private void OnKeyDown(object? sender, EventArgs e)
        {
            if (!Settings.IsEnabled) return;

            try
            {
                // 开始录音
                _audioCapture?.StartCapture();
                _hudWindow?.ShowRecording();
            }
            catch (Exception ex)
            {
                Logger.Error("开始录音失败", ex);
            }
        }

        private async void OnKeyUp(object? sender, EventArgs e)
        {
            if (!Settings.IsEnabled) return;

            try
            {
                // 停止录音并开始识别
                _audioCapture?.StopCapture();
                _hudWindow?.ShowRecognizing();

                await _speechRecognizer.RecognizeAsync();
            }
            catch (Exception ex)
            {
                Logger.Error("语音识别失败", ex);
                _hudWindow?.ShowError(ex.Message);
            }
        }

        private async void OnRecognitionCompleted(object? sender, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                _hudWindow?.Hide();
                return;
            }

            try
            {
                // 注入文字
                await _clipboardInjector.InjectTextAsync(text);
                _hudWindow?.ShowCompleted(text);

                // 延迟隐藏
                await System.Threading.Tasks.Task.Delay(1500);
                _hudWindow?.Hide();
            }
            catch (Exception ex)
            {
                Logger.Error("文字注入失败", ex);
                _hudWindow?.ShowError(ex.Message);
            }
        }

        private void OnRecognitionFailed(object? sender, string error)
        {
            _hudWindow?.ShowError(error);
        }

        private void OnSettingsRequested(object? sender, EventArgs e)
        {
            try
            {
                var settingsWindow = new SettingsWindow();
                settingsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                Logger.Error("打开设置窗口失败", ex);
                MessageBox.Show($"打开设置窗口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnExitRequested(object? sender, EventArgs e)
        {
            Shutdown();
        }

        private void OnLanguageChanged(object? sender, string language)
        {
            Settings.RecognitionLanguage = language;
            _speechRecognizer?.UpdateLanguage(language);
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
            Logger.Info("=== VoiceInput 退出 ===");

            // 清理资源
            _keyboardHook?.Dispose();
            _audioCapture?.Dispose();
            _speechRecognizer?.Dispose();
            _trayIcon?.Dispose();
            
            try
            {
                _hudWindow?.Close();
            }
            catch { }
            
            try
            {
                _mutex?.ReleaseMutex();
            }
            catch (Exception ex)
            {
                Logger.Warn($"释放 Mutex 失败: {ex.Message}");
            }
            
            _mutex?.Dispose();

            base.OnExit(e);
        }
    }
}
