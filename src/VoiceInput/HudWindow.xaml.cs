using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace VoiceInput
{
    public partial class HudWindow : Window
    {
        #region Win32 API

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter, int x, int y, int width, int height, uint flags);

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_LAYERED = 0x00080000;

        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS
        {
            public int leftWidth;
            public int rightWidth;
            public int topHeight;
            public int bottomHeight;
        }

        #endregion

        private readonly DispatcherTimer _waveformTimer;
        private readonly Random _random = new();
        private readonly double[] _barWeights = { 0.5, 0.8, 1.0, 0.75, 0.55 };
        private readonly Rectangle[] _bars;
        private readonly double[] _currentHeights = { 4, 4, 4, 4, 4 };
        private readonly double[] _targetHeights = { 4, 4, 4, 4, 4 };

        private Storyboard? _showStoryboard;
        private Storyboard? _hideStoryboard;
        private bool _isVisible;
        private double _currentRmsLevel;

        // 动画参数
        private const double MinBarHeight = 4.0;
        private const double MaxBarHeight = 26.0;
        private const double AttackCoefficient = 0.4;
        private const double ReleaseCoefficient = 0.15;
        private const double RandomJitter = 0.04;

        public HudWindow()
        {
            InitializeComponent();

            // 初始化波形条数组
            _bars = new[] { Bar1, Bar2, Bar3, Bar4, Bar5 };

            // 初始化动画
            _showStoryboard = (Storyboard)Resources["ShowStoryboard"];
            _hideStoryboard = (Storyboard)Resources["HideStoryboard"];

            // 初始化波形更新定时器（60fps）
            _waveformTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _waveformTimer.Tick += WaveformTimer_Tick;

            // 设置窗口样式
            Loaded += (s, e) =>
            {
                SetupWindowStyle();
                UpdatePosition();
            };
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SetupWindowStyle();
            UpdatePosition();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 阻止窗口关闭，改为隐藏
            e.Cancel = true;
            Hide();
        }

        private void SetupWindowStyle()
        {
            var hwnd = new WindowInteropHelper(this).Handle;

            // 设置扩展样式：不激活 + 工具窗口
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

            // 设置毛玻璃效果
            try
            {
                var margins = new MARGINS { leftWidth = -1, rightWidth = -1, topHeight = -1, bottomHeight = -1 };
                DwmExtendFrameIntoClientArea(hwnd, ref margins);
            }
            catch
            {
                // 如果 DWM 不可用，使用纯色背景
            }
        }

        private void UpdatePosition()
        {
            // 计算窗口宽度
            double width = ActualWidth;
            if (double.IsNaN(width) || width == 0)
            {
                width = 200; // 默认宽度
            }

            // 获取鼠标当前显示器的工作区
            var position = DpiHelper.CalculateHudPosition(width, 56, Settings.HudBottomOffset);

            Left = position.X;
            Top = position.Y;
        }

        public void ShowRecording()
        {
            Dispatcher.Invoke(() =>
            {
                // 更新文本
                TextBlock.Text = "";

                // 显示窗口
                Show();
                Activate();
                UpdatePosition();

                // 播放入场动画
                _showStoryboard?.Begin();

                // 启动波形更新
                _waveformTimer.Start();

                _isVisible = true;
            });
        }

        public void ShowRecognizing()
        {
            Dispatcher.Invoke(() =>
            {
                TextBlock.Text = "识别中...";
            });
        }

        public void ShowCompleted(string text)
        {
            Dispatcher.Invoke(() =>
            {
                TextBlock.Text = text;
                _waveformTimer.Stop();

                // 延迟后隐藏
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    HideHud();
                };
                timer.Start();
            });
        }

        public void ShowError(string message)
        {
            Dispatcher.Invoke(() =>
            {
                TextBlock.Text = $"错误: {message}";
                _waveformTimer.Stop();

                // 延迟后隐藏
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    HideHud();
                };
                timer.Start();
            });
        }

        public new void Hide()
        {
            Dispatcher.Invoke(() =>
            {
                HideHud();
            });
        }

        private void HideHud()
        {
            if (!_isVisible) return;

            _waveformTimer.Stop();
            _isVisible = false;

            // 播放退场动画
            _hideStoryboard?.Begin();

            // 动画完成后隐藏窗口
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                base.Hide();
            };
            timer.Start();
        }

        public void UpdateRmsLevel(double level)
        {
            _currentRmsLevel = level;
        }

        private void WaveformTimer_Tick(object? sender, EventArgs e)
        {
            for (int i = 0; i < 5; i++)
            {
                // 计算目标高度
                double baseHeight = _currentRmsLevel * _barWeights[i];
                double jitter = (_random.NextDouble() - 0.5) * 2 * RandomJitter;
                double targetHeight = MinBarHeight + (MaxBarHeight - MinBarHeight) * (baseHeight + jitter);

                // 限制范围
                targetHeight = Math.Max(MinBarHeight, Math.Min(MaxBarHeight, targetHeight));

                // 平滑过渡
                double attack = AttackCoefficient;
                double release = ReleaseCoefficient;

                if (targetHeight > _currentHeights[i])
                {
                    _currentHeights[i] += (targetHeight - _currentHeights[i]) * attack;
                }
                else
                {
                    _currentHeights[i] += (targetHeight - _currentHeights[i]) * release;
                }

                // 更新波形条高度
                _bars[i].Height = _currentHeights[i];
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // 监听显示器变化
            var source = PresentationSource.FromVisual(this);
            if (source != null)
            {
                source.ContentRendered += (s, e) => UpdatePosition();
            }
        }
    }
}
