# VoiceInput

Windows 系统托盘语音输入法，按住右 Alt 键即可在任意应用中语音输入文字。

## 功能特性

- **全局语音输入**：按住右 Alt 键录音，松开后自动转写并注入文字到当前窗口
- **双模式识别**：本地 Windows 语音识别 + AI 语音转写（OpenAI 兼容 API）
- **LLM 纠错**：可选的 AI 纠错，修复谐音错误和技术术语
- **歌词识别**：上传音频文件，自动识别并提取歌词（支持时间戳）
- **精致 HUD**：录音时在屏幕底部显示胶囊悬浮窗，带实时波形动画
- **WinUI 3 Fluent Design**：遵循 Windows 11 设计语言，支持 Mica/Acrylic 材质
- **系统托盘**：最小化到托盘运行，不占用任务栏

## 系统要求

- Windows 10 20H1（19041）或更高版本
- Windows 11 22H2 或更高版本（推荐，完整 Fluent Design 效果）
- .NET 8 运行时（自包含版本无需安装）
- 模式 A 需要安装对应语言的 Windows 语音识别语言包

## 快速开始

1. 从 [Releases](../../releases) 页面下载绿色版 zip
2. 解压后运行 `VoiceInput.exe`
3. 在设置中配置 API Key（如使用 AI 转写模式）
4. 按住右 Alt 键开始语音输入，松开后文字自动注入

### 主窗口导航

- **主页**：功能概览和状态显示
- **歌词识别**：上传音频文件识别歌词
- **设置**：配置 API、识别模式、界面语言等

## 从源码构建

### 依赖项

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Visual Studio 2022](https://visualstudio.microsoft.com/)（可选，含 Windows App SDK 工作负载）
- [GitHub CLI](https://cli.github.com/)（可选，`winget install GitHub.cli`）

### 构建命令

```bash
# 克隆仓库
git clone https://github.com/xxx/VoiceInput.git
cd VoiceInput

# 还原依赖
dotnet restore src/VoiceInput/VoiceInput.csproj

# 编译
dotnet build src/VoiceInput/VoiceInput.csproj -c Release

# 本地运行
dotnet run --project src/VoiceInput/VoiceInput.csproj

# 发布自包含版本
dotnet publish src/VoiceInput/VoiceInput.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish
```

## 配置说明

所有设置存储在 Windows 注册表中：

```
HKEY_CURRENT_USER\Software\VoiceInput\
```

| 配置项 | 说明 |
|--------|------|
| RecognitionMode | 0=本地识别+LLM纠错，1=AI语音转写 |
| RecognitionLanguage | 识别语言（zh-CN, en-US, zh-TW, ja-JP, ko-KR） |
| ApiBaseUrl | OpenAI 兼容 API 地址 |
| ApiKey | API 密钥（DPAPI 加密存储） |
| TranscriptionModel | 转写模型名称（如 whisper-1） |
| LlmModel | 纠错模型名称（如 gpt-4o-mini） |
| LlmCorrectionEnabled | 是否启用 LLM 纠错 |
| HudOffsetY | HUD 距屏幕底部偏移量 |

API Key 使用 Windows DPAPI 加密，仅当前用户可解密。

## 技术栈

| 组件 | 技术 |
|------|------|
| UI 框架 | WinUI 3（Microsoft.WindowsAppSDK） |
| 音频录制 | NAudio |
| 本地语音识别 | System.Speech |
| 系统托盘 | System.Windows.Forms.NotifyIcon |
| 键盘钩子 | SetWindowsHookEx (WH_KEYBOARD_LL) |
| 文字注入 | 剪贴板 + SendInput |
| 配置存储 | Windows Registry + DPAPI |

## 许可证

[GPL v3](LICENSE)

---

# VoiceInput

A Windows system tray voice input application. Press and hold the Right Alt key to voice-type text into any application.

## Features

- **Global Voice Input**: Hold Right Alt to record, release to transcribe and inject text
- **Dual Recognition Modes**: Local Windows Speech Recognition + AI transcription (OpenAI-compatible API)
- **LLM Correction**: Optional AI correction for homophone errors and technical terms
- **Lyrics Recognition**: Upload audio files to recognize and extract lyrics with timestamps
- **Elegant HUD**: Capsule-shaped floating window with real-time waveform animation
- **WinUI 3 Fluent Design**: Follows Windows 11 design language with Mica/Acrylic materials
- **System Tray**: Runs minimized to tray, doesn't occupy taskbar

## Requirements

- Windows 10 20H1 (19041) or later
- Windows 11 22H2 or later (recommended for full Fluent Design effects)
- .NET 8 runtime (self-contained version requires no installation)
- Mode A requires Windows Speech Recognition language pack for the target language

## Getting Started

1. Download the portable zip from [Releases](../../releases)
2. Extract and run `VoiceInput.exe`
3. Configure API Key in settings (if using AI transcription mode)
4. Hold Right Alt to start voice input, text is injected on release

## Building from Source

```bash
# Clone repository
git clone https://github.com/xxx/VoiceInput.git
cd VoiceInput

# Restore dependencies
dotnet restore src/VoiceInput/VoiceInput.csproj

# Build
dotnet build src/VoiceInput/VoiceInput.csproj -c Release

# Run locally
dotnet run --project src/VoiceInput/VoiceInput.csproj

# Publish self-contained version
dotnet publish src/VoiceInput/VoiceInput.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish
```

## License

[GPL v3](LICENSE)
