using System;
using System.Windows;
using System.Windows.Controls;

namespace VoiceInput
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            // 识别模式
            if (Settings.RecognitionMode == RecognitionMode.AiTranscription)
            {
                ModeAiRadio.IsChecked = true;
            }
            else
            {
                ModeLocalRadio.IsChecked = true;
            }

            // 识别语言
            SelectLanguage(Settings.RecognitionLanguage);

            // 触发按键
            SelectTriggerKey(Settings.TriggerKey);

            // API 配置
            ApiBaseUrlTextBox.Text = Settings.ApiBaseUrl;
            ApiKeyPasswordBox.Password = Settings.ApiKey;
            AsrModelTextBox.Text = Settings.AsrModel;
            LlmModelTextBox.Text = Settings.LlmModel;
            EnableLlmCorrectionCheckBox.IsChecked = Settings.LlmCorrectionEnabled;

            // 界面语言
            if (Settings.UiLanguage == "en-US")
            {
                UiLangEnRadio.IsChecked = true;
            }
            else
            {
                UiLangZhRadio.IsChecked = true;
            }

            // HUD 偏移
            HudOffsetSlider.Value = Settings.HudBottomOffset;
            HudOffsetValueText.Text = $"{Settings.HudBottomOffset:F0} DIU";
        }

        private void SelectLanguage(string language)
        {
            foreach (ComboBoxItem item in LanguageComboBox.Items)
            {
                if (item.Tag?.ToString() == language)
                {
                    LanguageComboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private void SelectTriggerKey(string key)
        {
            foreach (ComboBoxItem item in TriggerKeyComboBox.Items)
            {
                if (item.Tag?.ToString() == key)
                {
                    TriggerKeyComboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private string GetSelectedLanguage()
        {
            if (LanguageComboBox.SelectedItem is ComboBoxItem item)
            {
                return item.Tag?.ToString() ?? "zh-CN";
            }
            return "zh-CN";
        }

        private string GetSelectedTriggerKey()
        {
            if (TriggerKeyComboBox.SelectedItem is ComboBoxItem item)
            {
                return item.Tag?.ToString() ?? "RightAlt";
            }
            return "RightAlt";
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // 保存识别模式
            Settings.RecognitionMode = ModeAiRadio.IsChecked == true
                ? RecognitionMode.AiTranscription
                : RecognitionMode.LocalWithLlm;

            // 保存识别语言
            Settings.RecognitionLanguage = GetSelectedLanguage();

            // 保存触发按键
            Settings.TriggerKey = GetSelectedTriggerKey();

            // 保存 API 配置
            Settings.ApiBaseUrl = ApiBaseUrlTextBox.Text.Trim();
            Settings.ApiKey = ApiKeyPasswordBox.Password;
            Settings.AsrModel = AsrModelTextBox.Text.Trim();
            Settings.LlmModel = LlmModelTextBox.Text.Trim();
            Settings.LlmCorrectionEnabled = EnableLlmCorrectionCheckBox.IsChecked == true;

            // 保存界面语言
            Settings.UiLanguage = UiLangEnRadio.IsChecked == true ? "en-US" : "zh-CN";

            // 保存 HUD 偏移
            Settings.HudBottomOffset = HudOffsetSlider.Value;

            // 写入注册表
            Settings.Save();

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            TestConnectionButton.IsEnabled = false;
            TestResultText.Text = "测试中...";
            TestResultText.Foreground = System.Windows.Media.Brushes.Gray;

            // 临时保存 API 配置用于测试
            string originalBaseUrl = Settings.ApiBaseUrl;
            string originalApiKey = Settings.ApiKey;
            string originalAsrModel = Settings.AsrModel;
            string originalLlmModel = Settings.LlmModel;

            try
            {
                Settings.ApiBaseUrl = ApiBaseUrlTextBox.Text.Trim();
                Settings.ApiKey = ApiKeyPasswordBox.Password;
                Settings.AsrModel = AsrModelTextBox.Text.Trim();
                Settings.LlmModel = LlmModelTextBox.Text.Trim();

                var llmRefiner = new LlmRefiner();

                // 测试 LLM 连接
                var (llmSuccess, llmMessage) = await llmRefiner.TestConnectionAsync();

                if (llmSuccess)
                {
                    TestResultText.Text = llmMessage;
                    TestResultText.Foreground = System.Windows.Media.Brushes.Green;
                }
                else
                {
                    TestResultText.Text = llmMessage;
                    TestResultText.Foreground = System.Windows.Media.Brushes.Red;
                }
            }
            catch (Exception ex)
            {
                TestResultText.Text = $"测试失败: {ex.Message}";
                TestResultText.Foreground = System.Windows.Media.Brushes.Red;
            }
            finally
            {
                // 恢复原配置
                Settings.ApiBaseUrl = originalBaseUrl;
                Settings.ApiKey = originalApiKey;
                Settings.AsrModel = originalAsrModel;
                Settings.LlmModel = originalLlmModel;

                TestConnectionButton.IsEnabled = true;
            }
        }

        private void HudOffsetSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (HudOffsetValueText != null)
            {
                HudOffsetValueText.Text = $"{e.NewValue:F0} DIU";
            }
        }
    }
}
