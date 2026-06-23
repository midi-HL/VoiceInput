using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using VoiceInput.Pages;

namespace VoiceInput
{
    public sealed partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int SW_SHOWNORMAL = 1;

        private bool _isNavigating;
        private Microsoft.UI.Composition.SystemBackdrops.MicaController? _micaController;
        private Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController? _acrylicController;

        public MainWindow()
        {
            this.InitializeComponent();

            // Extend content into title bar
            this.ExtendsContentIntoTitleBar = true;

            // Set window properties
            var appWindow = this.AppWindow;
            if (appWindow != null)
            {
                appWindow.Title = "VoiceInput";

                // Hide from taskbar
                HideFromTaskbar();
            }

            // Set default size
            this.AppWindow.Resize(new Windows.Graphics.SizeInt32(900, 600));

            // Set minimum size
            this.SizeChanged += OnSizeChanged;

            // Apply backdrop
            TrySetMicaBackdrop();
        }

        private void HideFromTaskbar()
        {
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
            }
            catch { }
        }

        private void TrySetMicaBackdrop()
        {
            try
            {
                if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
                {
                    _micaController = new Microsoft.UI.Composition.SystemBackdrops.MicaController();
                    _micaController.SetTarget(this);
                }
                else if (Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported())
                {
                    _acrylicController = new Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController();
                    _acrylicController.SetTarget(this);
                }
            }
            catch
            {
                // Backdrop not supported, use default
            }
        }

        private void OnSizeChanged(object sender, WindowSizeChangedEventArgs args)
        {
            if (args.Size.Width < 700 || args.Size.Height < 450)
            {
                this.AppWindow.Resize(new Windows.Graphics.SizeInt32(
                    Math.Max(700, (int)args.Size.Width),
                    Math.Max(450, (int)args.Size.Height)));
            }
        }

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            NavigateToPage("Home");
        }

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer is NavigationViewItem item)
            {
                string? tag = item.Tag?.ToString();
                if (!string.IsNullOrEmpty(tag))
                {
                    NavigateToPage(tag);
                }
            }
        }

        public void NavigateToPage(string pageTag)
        {
            if (_isNavigating) return;
            _isNavigating = true;

            try
            {
                Type? pageType = pageTag switch
                {
                    "Home" => typeof(HomePage),
                    "Lyrics" => typeof(LyricsPage),
                    "Settings" => typeof(SettingsPage),
                    _ => typeof(HomePage)
                };

                if (pageType != null && ContentFrame.CurrentType != pageType)
                {
                    var transitionInfo = new SlideNavigationTransitionInfo
                    {
                        Effect = SlideNavigationTransitionEffect.FromRight
                    };
                    ContentFrame.Navigate(pageType, null, transitionInfo);

                    // Update selected item
                    foreach (var item in NavView.MenuItems)
                    {
                        if (item is NavigationViewItem navItem && navItem.Tag?.ToString() == pageTag)
                        {
                            NavView.SelectedItem = navItem;
                            break;
                        }
                    }

                    // Check footer items
                    foreach (var item in NavView.FooterMenuItems)
                    {
                        if (item is NavigationViewItem navItem && navItem.Tag?.ToString() == pageTag)
                        {
                            NavView.SelectedItem = navItem;
                            break;
                        }
                    }
                }
            }
            finally
            {
                _isNavigating = false;
            }
        }

        public void BringToFront()
        {
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                ShowWindow(hwnd, SW_SHOWNORMAL);
                this.Activate();
                SetForegroundWindow(hwnd);
            }
            catch { }
        }
    }
}
