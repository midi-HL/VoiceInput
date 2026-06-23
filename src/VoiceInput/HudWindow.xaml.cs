using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Windows.Graphics;

namespace VoiceInput
{
    public sealed partial class HudWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int SW_HIDE = 0;
        private const int SW_SHOWNOACTIVATE = 4;

        private readonly DispatcherTimer _animationTimer;
        private readonly Random _random = new Random();
        private readonly float[] _barWeights = { 0.5f, 0.8f, 1.0f, 0.75f, 0.55f };
        private readonly float[] _smoothedLevels = new float[5];
        private readonly Rectangle[] _bars;

        private float _currentRmsLevel;
        private bool _isVisible;
        private IntPtr _hwnd;
        private bool _acrylicSupported;
        private Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController? _acrylicController;

        public HudWindow()
        {
            this.InitializeComponent();

            // Setup window properties
            _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            int exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
            SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);

            // Apply acrylic backdrop
            TrySetAcrylicBackdrop();

            // Configure window presenter (no border, no title bar)
            var presenter = OverlappedPresenter.CreateForContextMenu();
            this.AppWindow.SetPresenter(presenter);

            // Initialize bars array
            _bars = new[] { Bar0, Bar1, Bar2, Bar3, Bar4 };

            // Animation timer (60fps)
            _animationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _animationTimer.Tick += OnAnimationTick;

            // Initially hidden
            this.Opacity = 0;
            HudScale.ScaleX = 0.85;
            HudScale.ScaleY = 0.85;
        }

        private void TrySetAcrylicBackdrop()
        {
            try
            {
                if (Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported())
                {
                    _acrylicController = new Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController();
                    _acrylicController.SetTarget(this);
                    _acrylicSupported = true;
                }
            }
            catch
            {
                _acrylicSupported = false;
            }

            // Set fallback background if acrylic is not supported
            if (!_acrylicSupported)
            {
                HudBorder.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.ColorHelper.FromArgb(204, 28, 28, 30)); // #CC1C1C1E
            }
        }

        public void ShowAnimated()
        {
            if (_isVisible) return;
            _isVisible = true;

            // Reset state
            this.Opacity = 0;
            HudScale.ScaleX = 0.85;
            HudScale.ScaleY = 0.85;
            TranscriptionText.Text = "";

            // Reset waveform
            for (int i = 0; i < 5; i++)
            {
                _smoothedLevels[i] = 0;
                _bars[i].Height = 4;
                Canvas.SetTop(_bars[i], 14);
            }

            // Position at bottom center
            PositionAtBottomCenter();

            // Show window without activating
            ShowWindow(_hwnd, SW_SHOWNOACTIVATE);

            // Entry animation
            var storyboard = new Storyboard();

            var opacityAnim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromSeconds(0.35)),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
            };
            Storyboard.SetTarget(opacityAnim, this);
            Storyboard.SetTargetProperty(opacityAnim, "Opacity");
            storyboard.Children.Add(opacityAnim);

            var scaleAnimX = new DoubleAnimation
            {
                From = 0.85,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.35)),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
            };
            Storyboard.SetTarget(scaleAnimX, HudScale);
            Storyboard.SetTargetProperty(scaleAnimX, "ScaleX");
            storyboard.Children.Add(scaleAnimX);

            var scaleAnimY = new DoubleAnimation
            {
                From = 0.85,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.35)),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
            };
            Storyboard.SetTarget(scaleAnimY, HudScale);
            Storyboard.SetTargetProperty(scaleAnimY, "ScaleY");
            storyboard.Children.Add(scaleAnimY);

            storyboard.Begin();

            _animationTimer.Start();
        }

        public async void HideAnimated()
        {
            if (!_isVisible) return;
            _isVisible = false;

            _animationTimer.Stop();

            // Exit animation
            var storyboard = new Storyboard();

            var opacityAnim = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = new Duration(TimeSpan.FromSeconds(0.22)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(opacityAnim, this);
            Storyboard.SetTargetProperty(opacityAnim, "Opacity");
            storyboard.Children.Add(opacityAnim);

            var scaleAnimX = new DoubleAnimation
            {
                From = 1.0,
                To = 0.9,
                Duration = new Duration(TimeSpan.FromSeconds(0.22)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(scaleAnimX, HudScale);
            Storyboard.SetTargetProperty(scaleAnimX, "ScaleX");
            storyboard.Children.Add(scaleAnimX);

            var scaleAnimY = new DoubleAnimation
            {
                From = 1.0,
                To = 0.9,
                Duration = new Duration(TimeSpan.FromSeconds(0.22)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(scaleAnimY, HudScale);
            Storyboard.SetTargetProperty(scaleAnimY, "ScaleY");
            storyboard.Children.Add(scaleAnimY);

            storyboard.Begin();

            // Wait for animation to complete, then hide window
            await Task.Delay(250);

            ShowWindow(_hwnd, SW_HIDE);
        }

        public void UpdateText(string text)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                TranscriptionText.Text = text;
            });
        }

        public void UpdateRmsLevel(float level)
        {
            _currentRmsLevel = level;
        }

        private void OnAnimationTick(object? sender, object e)
        {
            UpdateWaveform();
        }

        private void UpdateWaveform()
        {
            for (int i = 0; i < 5; i++)
            {
                float target = _currentRmsLevel * _barWeights[i];

                // Add random jitter (±4%)
                float jitter = (float)(_random.NextDouble() * 0.08 - 0.04);
                target = Math.Max(0, target + jitter);

                // Smooth envelope (attack 40%, release 15%)
                float attackRate = 0.4f;
                float releaseRate = 0.15f;

                if (target > _smoothedLevels[i])
                    _smoothedLevels[i] += (target - _smoothedLevels[i]) * attackRate;
                else
                    _smoothedLevels[i] += (target - _smoothedLevels[i]) * releaseRate;

                // Calculate bar height (min 4, max 26)
                float barHeight = 4 + _smoothedLevels[i] * 22;
                barHeight = Math.Clamp(barHeight, 4, 26);

                // Update bar
                _bars[i].Height = barHeight;

                // Center vertically (canvas height = 32)
                double top = (32 - barHeight) / 2;
                Canvas.SetTop(_bars[i], top);
            }
        }

        private void PositionAtBottomCenter()
        {
            try
            {
                GetCursorPos(out POINT cursorPos);

                double dpiScale = DpiHelper.GetDpiScaleForPoint(cursorPos.X, cursorPos.Y);
                var screen = DpiHelper.GetScreenForPoint(cursorPos.X, cursorPos.Y);

                double screenWidth = screen.WorkingArea.Width / dpiScale;
                double screenHeight = screen.WorkingArea.Height / dpiScale;
                double screenLeft = screen.WorkingArea.Left / dpiScale;
                double screenTop = screen.WorkingArea.Top / dpiScale;

                double hudWidth = 200;
                double hudHeight = 40;
                double offsetY = Settings.HudOffsetY;

                double x = screenLeft + (screenWidth - hudWidth) / 2;
                double y = screenTop + screenHeight - hudHeight - offsetY;

                this.AppWindow.MoveAndResize(new RectInt32(
                    (int)(x * dpiScale),
                    (int)(y * dpiScale),
                    (int)(hudWidth * dpiScale),
                    (int)(hudHeight * dpiScale)));
            }
            catch
            {
                this.AppWindow.MoveAndResize(new RectInt32(860, 1000, 200, 40));
            }
        }
    }
}
