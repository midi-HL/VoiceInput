# VoiceInput —— Windows 语音输入工具

## 功能介绍
按住右 Alt 键开始录音，松开后自动将语音转录为文字并注入当前输入框。
支持简体中文、英语、日语等多语言，可选接入 LLM 对转录结果进行智能纠错。
在 600p 至 2K 等各种分辨率和 DPI 缩放设置下均可正常显示，自动适配多显示器。

## 系统要求
- Windows 10 版本 1903 或更高 / Windows 11
- 已安装对应语言的 Windows 语音识别语言包（简体中文：zh-CN）
- 麦克风设备

## 安装方法
1. 前往 [Releases](../../releases) 页面下载 `VoiceInput-Setup-vX.X.X-x64.exe`
2. 运行安装程序，按向导选择安装路径、快捷方式和开机自启选项
3. 安装完成后应用自动启动，系统托盘出现麦克风图标

## 首次使用
1. 前往「Windows 设置 → 隐私和安全性 → 麦克风」确保已授权访问
2. 右键托盘图标 → 识别语言，选择语言（默认简体中文）
3. 在任意输入框中**按住右 Alt 键**开始说话，松开后文字自动输入

## LLM 纠错配置（可选）
右键托盘 → 设置，填入 OpenAI 兼容 API 的 Base URL、Key 和模型名称后保存。

## 显示异常排查
若 HUD 悬浮窗位置偏移或模糊，请确认系统缩放设置为整数倍（100%/125%/150%/200%），
并在「显示设置 → 高级缩放设置」中关闭"让应用自动修复模糊问题"选项。

---

# VoiceInput — Windows Voice Input Tool

## Features
Hold Right Alt to record; release to transcribe and inject text into any focused input.
Supports zh-CN, en-US, ja-JP, and more. Optional LLM-powered correction for mixed-language input.
Fully DPI-aware: adapts cleanly from 600p 16:10 laptops to 2K 16:9 monitors at any scaling level.

## Requirements
- Windows 10 1903+ or Windows 11
- Windows Speech Recognition language pack (e.g. zh-CN)
- Microphone

## Installation
Download `VoiceInput-Setup-vX.X.X-x64.exe` from [Releases](../../releases) and run the wizard.

## Usage
Hold **Right Alt** → speak → release → text is injected into the focused input field.

## License
MIT License - see [LICENSE](LICENSE) file for details.
