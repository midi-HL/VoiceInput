using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace VoiceInput.Pages
{
    public class FeatureCard
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string IconGlyph { get; set; } = "";
        public string Tag { get; set; } = "";
    }

    public sealed partial class HomePage : Page
    {
        public HomePage()
        {
            this.InitializeComponent();
            LoadFeatureCards();
            UpdateStatus();
        }

        private void LoadFeatureCards()
        {
            var cards = new List<FeatureCard>
            {
                new FeatureCard
                {
                    Title = "语音输入",
                    Description = "按住右Alt键开始录音，松开自动转写并注入文字",
                    IconGlyph = "\uE720", // Mic
                    Tag = ""
                },
                new FeatureCard
                {
                    Title = "歌词识别",
                    Description = "上传音频文件，识别并提取歌词",
                    IconGlyph = "\uE8D6", // MusicNote
                    Tag = "Lyrics"
                },
                new FeatureCard
                {
                    Title = "设置",
                    Description = "配置API、识别模式、界面语言等",
                    IconGlyph = "\uE713", // Settings
                    Tag = "Settings"
                }
            };

            FeatureCards.ItemsSource = cards;
        }

        private void UpdateStatus()
        {
            string mode = Settings.RecognitionMode == RecognitionMode.LocalWithLlmCorrection
                ? "本地识别 + LLM 纠错"
                : "AI 语音转写";
            string lang = Settings.GetLanguageDisplayName(Settings.RecognitionLanguage);
            string status = $"当前模式: {mode}  |  识别语言: {lang}";

            if (Settings.RecognitionMode == RecognitionMode.AiTranscription)
            {
                string apiStatus = string.IsNullOrEmpty(Settings.ApiKey) ? "未配置" : "已配置";
                status += $"  |  API: {apiStatus}";
            }

            StatusText.Text = status;
        }

        private void Card_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tag)
            {
                if (!string.IsNullOrEmpty(tag))
                {
                    NavigationService.NavigateTo(tag);
                }
            }
        }
    }
}
