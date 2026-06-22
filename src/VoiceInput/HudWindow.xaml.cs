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

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

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
        
        // 7 根波形条的权重
        private readonly double[] _barWeights = { 0.4, 0.7, 0.9, 1.0, 0.9, 0.7, 0.4 };
        private readonly Rectangle[] _bars;
        private readonly double[] _currentHeights;
        private readonly double[] _targetHeights;

        private Storyboard? _showStoryboard;
        private Storyboard? _hideStoryboard;
        private bool _isVisible;
        private double _currentRmsLevel;

        // 动画参数
        private const double MinBarHeight = 3.0;
        private const double MaxBarHeight = 20.0;
        private const double AttackCoefficient = 0.5;
        private const double ReleaseCoefficient = 0.2;
        private const double RandomJitter = 0.05;

        public HudWindow()
        {
            InitializeComponent();

            // 初始化波形条数组（7 根）
            _bars = new[] { Bar1, Bar2, Bar3, Bar4, Bar5, Bar6, Bar7 };
            _currentHeights = new double[7];
            _targetHeights = new double[7];
            
            for (int i = 0; i < 7; i++)
            {
                _currentHeights[i] = MinBarHeight;
                _targetHeights[i] = MinBarHeight;
            }

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

            // 设置扩展样式：不激活 + 工具窗口（不显示在任务栏）
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
            var position = DpiHelper.CalculateHudPosition(width, 40, Settings.HudBottomOffset);

            Left = position.X;
            Top = position.Y;
        }

        public void ShowRecording()
        {
            Dispatcher.Invoke(() =>
            {
                // 更新文本
                TextBlock.Text = "";

                // 显示窗口（不调用 Activate()，避免抢夺焦点）
                Show();
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
                _waveformTimer.Stop();
            });
        }

        public void ShowRecognizingText(string text)
        {
            Dispatcher.Invoke(() =>
            {
                TextBlock.Text = text;
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
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
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
            for (int i = 0; i < 7; i++)
            {
                // 计算目标高度
                double baseHeight = _currentRmsLevel * _barWeights[i];
                double jitter = (_random.NextDouble() - 0.5) * 2 * RandomJitter;
                double targetHeight = MinBarHeight + (MaxBarHeight - MinBarHeight) * (baseHeight + jitter);

                // 限制范围
                targetHeight = Math.Max(MinBarHeight, Math.Min(MaxBarHeight, targetHeight));

                // 平滑过渡
                if (targetHeight > _currentHeights[i])
                {
                    _currentHeights[i] += (targetHeight - _currentHeights[i]) * AttackCoefficient;
                }
                else
                {
                    _currentHeights[i] += (targetHeight - _currentHeights[i]) * ReleaseCoefficient;
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
