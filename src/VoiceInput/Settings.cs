using System;
using Microsoft.Win32;
using System.Security.Cryptography;
using System.Text;

namespace VoiceInput
{
    public enum RecognitionMode
    {
        LocalWithLlmCorrection,
        AiTranscription
    }

    public enum RecognitionLanguage
    {
        ZhCN,
        EnUS,
        ZhTW,
        JaJP,
        KoKR
    }

    public enum AppLanguage
    {
        Chinese,
        English
    }

    public enum CloseBehavior
    {
        MinimizeToTray,
        CloseApp
    }

    public static class Settings
    {
        private const string RegistryPath = @"Software\VoiceInput";

        public static RecognitionMode RecognitionMode
        {
            get => (RecognitionMode)ReadInt(nameof(RecognitionMode), 0);
            set => WriteInt(nameof(RecognitionMode), (int)value);
        }

        public static RecognitionLanguage RecognitionLanguage
        {
            get => (RecognitionLanguage)ReadInt(nameof(RecognitionLanguage), 0);
            set => WriteInt(nameof(RecognitionLanguage), (int)value);
        }

        public static AppLanguage AppLanguage
        {
            get => (AppLanguage)ReadInt(nameof(AppLanguage), 0);
            set => WriteInt(nameof(AppLanguage), (int)value);
        }

        public static CloseBehavior CloseBehavior
        {
            get => (CloseBehavior)ReadInt(nameof(CloseBehavior), 0);
            set => WriteInt(nameof(CloseBehavior), (int)value);
        }

        public static string ApiBaseUrl
        {
            get => ReadString(nameof(ApiBaseUrl), "https://api.openai.com");
            set => WriteString(nameof(ApiBaseUrl), value);
        }

        public static string ApiKey
        {
            get => ReadEncryptedString(nameof(ApiKey), "");
            set => WriteEncryptedString(nameof(ApiKey), value);
        }

        public static string TranscriptionModel
        {
            get => ReadString(nameof(TranscriptionModel), "whisper-1");
            set => WriteString(nameof(TranscriptionModel), value);
        }

        public static string LlmModel
        {
            get => ReadString(nameof(LlmModel), "gpt-4o-mini");
            set => WriteString(nameof(LlmModel), value);
        }

        public static bool LlmCorrectionEnabled
        {
            get => ReadInt(nameof(LlmCorrectionEnabled), 0) == 1;
            set => WriteInt(nameof(LlmCorrectionEnabled), value ? 1 : 0);
        }

        public static int HudOffsetY
        {
            get => ReadInt(nameof(HudOffsetY), 32);
            set => WriteInt(nameof(HudOffsetY), value);
        }

        public static string GetLanguageCode()
        {
            return RecognitionLanguage switch
            {
                RecognitionLanguage.ZhCN => "zh-CN",
                RecognitionLanguage.EnUS => "en-US",
                RecognitionLanguage.ZhTW => "zh-TW",
                RecognitionLanguage.JaJP => "ja-JP",
                RecognitionLanguage.KoKR => "ko-KR",
                _ => "zh-CN"
            };
        }

        public static string GetLanguageShortCode()
        {
            return RecognitionLanguage switch
            {
                RecognitionLanguage.ZhCN => "zh",
                RecognitionLanguage.EnUS => "en",
                RecognitionLanguage.ZhTW => "zh",
                RecognitionLanguage.JaJP => "ja",
                RecognitionLanguage.KoKR => "ko",
                _ => "zh"
            };
        }

        public static string GetLanguageDisplayName(RecognitionLanguage lang)
        {
            return lang switch
            {
                RecognitionLanguage.ZhCN => "简体中文",
                RecognitionLanguage.EnUS => "English",
                RecognitionLanguage.ZhTW => "繁體中文",
                RecognitionLanguage.JaJP => "日本語",
                RecognitionLanguage.KoKR => "한국어",
                _ => "简体中文"
            };
        }

        private static int ReadInt(string name, int defaultValue)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
                if (key?.GetValue(name) is int val)
                    return val;
                if (key?.GetValue(name) is string str && int.TryParse(str, out var parsed))
                    return parsed;
            }
            catch { }
            return defaultValue;
        }

        private static void WriteInt(string name, int value)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
                key?.SetValue(name, value, RegistryValueKind.DWord);
            }
            catch { }
        }

        private static string ReadString(string name, string defaultValue)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
                if (key?.GetValue(name) is string val)
                    return val;
            }
            catch { }
            return defaultValue;
        }

        private static void WriteString(string name, string value)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
                key?.SetValue(name, value, RegistryValueKind.String);
            }
            catch { }
        }

        private static string ReadEncryptedString(string name, string defaultValue)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
                if (key?.GetValue(name) is byte[] encrypted)
                {
                    byte[] decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                    return Encoding.UTF8.GetString(decrypted);
                }
            }
            catch { }
            return defaultValue;
        }

        private static void WriteEncryptedString(string name, string value)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
                if (string.IsNullOrEmpty(value))
                {
                    key?.DeleteValue(name, false);
                }
                else
                {
                    byte[] data = Encoding.UTF8.GetBytes(value);
                    byte[] encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
                    key?.SetValue(name, encrypted, RegistryValueKind.Binary);
                }
            }
            catch { }
        }
    }
}
