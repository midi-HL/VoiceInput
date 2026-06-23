using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace VoiceInput.Pages
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            this.InitializeComponent();
            SettingsFrame.Navigate(typeof(RecognitionSettingsPage));
        }

        private void SettingsNav_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer is NavigationViewItem item)
            {
                string? tag = item.Tag?.ToString();
                Type? pageType = tag switch
                {
                    "Recognition" => typeof(RecognitionSettingsPage),
                    "Api" => typeof(ApiSettingsPage),
                    "Interface" => typeof(InterfaceSettingsPage),
                    _ => typeof(RecognitionSettingsPage)
                };

                if (pageType != null && SettingsFrame.CurrentType != pageType)
                {
                    SettingsFrame.Navigate(pageType);
                }
            }
        }
    }

    // === Recognition Settings Page ===
    public sealed partial class RecognitionSettingsPage : Page
    {
        public RecognitionSettingsPage()
        {
            this.InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            LocalRadio.IsChecked = Settings.RecognitionMode == RecognitionMode.LocalWithLlmCorrection;
            AiRadio.IsChecked = Settings.RecognitionMode == RecognitionMode.AiTranscription;

            LanguageComboBox.SelectedIndex = Settings.RecognitionLanguage switch
            {
                RecognitionLanguage.ZhCN => 0,
                RecognitionLanguage.EnUS => 1,
                RecognitionLanguage.ZhTW => 2,
                RecognitionLanguage.JaJP => 3,
                RecognitionLanguage.KoKR => 4,
                _ => 0
            };

            TriggerKeyComboBox.SelectedIndex = 0; // Right Alt
        }

        private void RecognitionMode_Changed(object sender, RoutedEventArgs e)
        {
            if (LocalRadio.IsChecked == true)
                Settings.RecognitionMode = RecognitionMode.LocalWithLlmCorrection;
            else if (AiRadio.IsChecked == true)
                Settings.RecognitionMode = RecognitionMode.AiTranscription;
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Settings.RecognitionLanguage = LanguageComboBox.SelectedIndex switch
            {
                0 => RecognitionLanguage.ZhCN,
                1 => RecognitionLanguage.EnUS,
                2 => RecognitionLanguage.ZhTW,
                3 => RecognitionLanguage.JaJP,
                4 => RecognitionLanguage.KoKR,
                _ => RecognitionLanguage.ZhCN
            };
        }
    }

    // === API Settings Page ===
    public sealed partial class ApiSettingsPage : Page
    {
        public ApiSettingsPage()
        {
            this.InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            ApiBaseUrlBox.Text = Settings.ApiBaseUrl;
            ApiKeyBox.Password = Settings.ApiKey;
            TranscriptionModelBox.Text = Settings.TranscriptionModel;
            LlmModelBox.Text = Settings.LlmModel;
            LlmCorrectionToggle.IsOn = Settings.LlmCorrectionEnabled;
        }

        private void ApiBaseUrl_Changed(object sender, TextChangedEventArgs e)
        {
            Settings.ApiBaseUrl = ApiBaseUrlBox.Text;
        }

        private void ApiKey_Changed(object sender, RoutedEventArgs e)
        {
            Settings.ApiKey = ApiKeyBox.Password;
        }

        private void TranscriptionModel_Changed(object sender, TextChangedEventArgs e)
        {
            Settings.TranscriptionModel = TranscriptionModelBox.Text;
        }

        private void LlmModel_Changed(object sender, TextChangedEventArgs e)
        {
            Settings.LlmModel = LlmModelBox.Text;
        }

        private void LlmCorrection_Toggled(object sender, RoutedEventArgs e)
        {
            Settings.LlmCorrectionEnabled = LlmCorrectionToggle.IsOn;
        }

        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            TestConnectionButton.IsEnabled = false;
            TestResultText.Text = "测试中...";
            TestResultText.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];

            try
            {
                var refiner = new LlmRefiner();
                bool success = await refiner.TestConnectionAsync();
                refiner.Dispose();

                if (success)
                {
                    TestResultText.Text = "连接成功";
                    TestResultText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);
                }
                else
                {
                    TestResultText.Text = "连接失败，请检查配置";
                    TestResultText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
                }
            }
            catch
            {
                TestResultText.Text = "连接失败";
                TestResultText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
            }
            finally
            {
                TestConnectionButton.IsEnabled = true;
            }
        }
    }

    // === Interface Settings Page ===
    public sealed partial class InterfaceSettingsPage : Page
    {
        public InterfaceSettingsPage()
        {
            this.InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            ChineseRadio.IsChecked = Settings.AppLanguage == AppLanguage.Chinese;
            EnglishRadio.IsChecked = Settings.AppLanguage == AppLanguage.English;

            HudOffsetSlider.Value = Settings.HudOffsetY;

            MinimizeRadio.IsChecked = Settings.CloseBehavior == CloseBehavior.MinimizeToTray;
            CloseRadio.IsChecked = Settings.CloseBehavior == CloseBehavior.CloseApp;
        }

        private void AppLanguage_Changed(object sender, RoutedEventArgs e)
        {
            if (ChineseRadio.IsChecked == true)
                Settings.AppLanguage = AppLanguage.Chinese;
            else if (EnglishRadio.IsChecked == true)
                Settings.AppLanguage = AppLanguage.English;
        }

        private void HudOffset_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            Settings.HudOffsetY = (int)e.NewValue;
        }

        private void CloseBehavior_Changed(object sender, RoutedEventArgs e)
        {
            if (MinimizeRadio.IsChecked == true)
                Settings.CloseBehavior = CloseBehavior.MinimizeToTray;
            else if (CloseRadio.IsChecked == true)
                Settings.CloseBehavior = CloseBehavior.CloseApp;
        }
    }
}
