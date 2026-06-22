using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace VoiceInput
{
    public enum RecognitionMode
    {
        LocalWithLlm,   // 本地识别 + LLM 纠错
        AiTranscription // AI 语音转写
    }

    public static class Settings
    {
        private const string RegistryPath = @"SOFTWARE\VoiceInput";
        private static readonly RegistryKey _rootKey = Registry.CurrentUser;

        // 应用状态
        public static bool IsEnabled { get; set; } = true;
        
        // 识别设置
        public static RecognitionMode RecognitionMode { get; set; } = RecognitionMode.LocalWithLlm;
        public static string RecognitionLanguage { get; set; } = "zh-CN";
        public static string TriggerKey { get; set; } = "RightAlt";
        
        // API 配置
        public static string ApiBaseUrl { get; set; } = string.Empty;
        public static string ApiKey { get; set; } = string.Empty;
        public static string AsrModel { get; set; } = "whisper-1";
        public static string LlmModel { get; set; } = "gpt-4o-mini";
        public static bool LlmCorrectionEnabled { get; set; } = true;
        
        // 界面设置
        public static string UiLanguage { get; set; } = "zh-CN";
        public static double HudBottomOffset { get; set; } = 32.0;

        public static void Initialize()
        {
            Load();
        }

        public static void Load()
        {
            try
            {
                using var key = _rootKey.OpenSubKey(RegistryPath, false);
                if (key == null) return;

                IsEnabled = GetBoolValue(key, "IsEnabled", true);
                RecognitionMode = GetEnumValue(key, "RecognitionMode", RecognitionMode.LocalWithLlm);
                RecognitionLanguage = GetStringValue(key, "RecognitionLanguage", "zh-CN");
                TriggerKey = GetStringValue(key, "TriggerKey", "RightAlt");
                
                ApiBaseUrl = GetStringValue(key, "ApiBaseUrl", string.Empty);
                ApiKey = DecryptApiKey(GetStringValue(key, "ApiKeyEncrypted", string.Empty));
                AsrModel = GetStringValue(key, "AsrModel", "whisper-1");
                LlmModel = GetStringValue(key, "LlmModel", "gpt-4o-mini");
                LlmCorrectionEnabled = GetBoolValue(key, "LlmCorrectionEnabled", true);
                
                UiLanguage = GetStringValue(key, "UiLanguage", "zh-CN");
                HudBottomOffset = GetDoubleValue(key, "HudBottomOffset", 32.0);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载设置失败: {ex.Message}");
            }
        }

        public static void Save()
        {
            try
            {
                using var key = _rootKey.CreateSubKey(RegistryPath, true);
                if (key == null) return;

                key.SetValue("IsEnabled", IsEnabled ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue("RecognitionMode", (int)RecognitionMode, RegistryValueKind.DWord);
                key.SetValue("RecognitionLanguage", RecognitionLanguage, RegistryValueKind.String);
                key.SetValue("TriggerKey", TriggerKey, RegistryValueKind.String);
                
                key.SetValue("ApiBaseUrl", ApiBaseUrl, RegistryValueKind.String);
                key.SetValue("ApiKeyEncrypted", EncryptApiKey(ApiKey), RegistryValueKind.String);
                key.SetValue("AsrModel", AsrModel, RegistryValueKind.String);
                key.SetValue("LlmModel", LlmModel, RegistryValueKind.String);
                key.SetValue("LlmCorrectionEnabled", LlmCorrectionEnabled ? 1 : 0, RegistryValueKind.DWord);
                
                key.SetValue("UiLanguage", UiLanguage, RegistryValueKind.String);
                key.SetValue("HudBottomOffset", HudBottomOffset, RegistryValueKind.String);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存设置失败: {ex.Message}");
            }
        }

        public static void Reset()
        {
            try
            {
                _rootKey.DeleteSubKeyTree(RegistryPath, false);
            }
            catch { }

            // 恢复默认值
            IsEnabled = true;
            RecognitionMode = RecognitionMode.LocalWithLlm;
            RecognitionLanguage = "zh-CN";
            TriggerKey = "RightAlt";
            ApiBaseUrl = string.Empty;
            ApiKey = string.Empty;
            AsrModel = "whisper-1";
            LlmModel = "gpt-4o-mini";
            LlmCorrectionEnabled = true;
            UiLanguage = "zh-CN";
            HudBottomOffset = 32.0;
        }

        #region DPAPI 加密/解密

        private static string EncryptApiKey(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;

            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] encryptedBytes = ProtectedData.Protect(
                    plainBytes,
                    null,
                    DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encryptedBytes);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string DecryptApiKey(string encryptedBase64)
        {
            if (string.IsNullOrEmpty(encryptedBase64)) return string.Empty;

            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(encryptedBase64);
                byte[] plainBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    null,
                    DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                return string.Empty;
            }
        }

        #endregion

        #region 注册表辅助方法

        private static string GetStringValue(RegistryKey key, string name, string defaultValue)
        {
            try
            {
                return key.GetValue(name, defaultValue) as string ?? defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        private static int GetIntValue(RegistryKey key, string name, int defaultValue)
        {
            try
            {
                return Convert.ToInt32(key.GetValue(name, defaultValue));
            }
            catch
            {
                return defaultValue;
            }
        }

        private static bool GetBoolValue(RegistryKey key, string name, bool defaultValue)
        {
            return GetIntValue(key, name, defaultValue ? 1 : 0) != 0;
        }

        private static double GetDoubleValue(RegistryKey key, string name, double defaultValue)
        {
            try
            {
                var value = key.GetValue(name, defaultValue.ToString());
                if (value is string str && double.TryParse(str, out double result))
                {
                    return result;
                }
                return defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        private static T GetEnumValue<T>(RegistryKey key, string name, T defaultValue) where T : struct
        {
            try
            {
                var value = GetIntValue(key, name, Convert.ToInt32(defaultValue));
                if (Enum.IsDefined(typeof(T), value))
                {
                    return (T)(object)value;
                }
                return defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        #endregion

        #region 语言辅助方法

        public static string GetLanguageDisplayName(string languageCode)
        {
            return languageCode switch
            {
                "zh-CN" => "简体中文",
                "en-US" => "English",
                "zh-TW" => "繁體中文",
                "ja-JP" => "日本語",
                "ko-KR" => "한국어",
                _ => languageCode
            };
        }

        public static string[] GetSupportedLanguages()
        {
            return new[] { "zh-CN", "en-US", "zh-TW", "ja-JP", "ko-KR" };
        }

        #endregion
    }
}
