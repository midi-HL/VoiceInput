using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VoiceInput
{
    public class TrayIcon : IDisposable
    {
        private NotifyIcon? _notifyIcon;
        private ContextMenuStrip? _contextMenu;

        // 事件
        public event EventHandler? SettingsRequested;
        public event EventHandler? ExitRequested;
        public event EventHandler<string>? LanguageChanged;
        public event EventHandler<RecognitionMode>? ModeChanged;
        public event EventHandler<bool>? EnableToggled;

        public TrayIcon()
        {
            InitializeNotifyIcon();
        }

        private void InitializeNotifyIcon()
        {
            // 创建图标
            var icon = CreateMicrophoneIcon();

            // 创建上下文菜单
            _contextMenu = CreateContextMenu();

            // 创建 NotifyIcon
            _notifyIcon = new NotifyIcon
            {
                Icon = icon,
                Text = "VoiceInput - 语音输入",
                ContextMenuStrip = _contextMenu,
                Visible = true
            };

            // 双击打开设置
            _notifyIcon.DoubleClick += (s, e) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        }

        private ContextMenuStrip CreateContextMenu()
        {
            var menu = new ContextMenuStrip();

            // 启用/禁用
            var enableItem = new ToolStripMenuItem("🎙 语音输入  ✓ 已启用")
            {
                Checked = Settings.IsEnabled,
                CheckOnClick = true
            };
            enableItem.Click += (s, e) =>
            {
                Settings.IsEnabled = enableItem.Checked;
                enableItem.Text = enableItem.Checked ? "🎙 语音输入  ✓ 已启用" : "🎙 语音输入    已禁用";
                EnableToggled?.Invoke(this, Settings.IsEnabled);
            };
            menu.Items.Add(enableItem);

            menu.Items.Add(new ToolStripSeparator());

            // 识别语言
            var languageItem = new ToolStripMenuItem("识别语言");
            foreach (var lang in Settings.GetSupportedLanguages())
            {
                var langItem = new ToolStripMenuItem(Settings.GetLanguageDisplayName(lang))
                {
                    Tag = lang,
                    Checked = lang == Settings.RecognitionLanguage
                };
                langItem.Click += (s, e) =>
                {
                    // 取消其他选中
                    foreach (ToolStripMenuItem item in languageItem.DropDownItems)
                    {
                        item.Checked = false;
                    }
                    langItem.Checked = true;
                    Settings.RecognitionLanguage = lang;
                    LanguageChanged?.Invoke(this, lang);
                };
                languageItem.DropDownItems.Add(langItem);
            }
            menu.Items.Add(languageItem);

            menu.Items.Add(new ToolStripSeparator());

            // 识别模式
            var modeItem = new ToolStripMenuItem("识别模式");
            var aiModeItem = new ToolStripMenuItem("AI 语音转写（API）")
            {
                Tag = RecognitionMode.AiTranscription,
                Checked = Settings.RecognitionMode == RecognitionMode.AiTranscription
            };
            var localModeItem = new ToolStripMenuItem("本地识别 + LLM 纠错（默认）")
            {
                Tag = RecognitionMode.LocalWithLlm,
                Checked = Settings.RecognitionMode == RecognitionMode.LocalWithLlm
            };
            localModeItem.Click += (s, e) =>
            {
                localModeItem.Checked = true;
                aiModeItem.Checked = false;
                Settings.RecognitionMode = RecognitionMode.LocalWithLlm;
                ModeChanged?.Invoke(this, RecognitionMode.LocalWithLlm);
            };
            modeItem.DropDownItems.Add(localModeItem);

            aiModeItem.Click += (s, e) =>
            {
                localModeItem.Checked = false;
                aiModeItem.Checked = true;
                Settings.RecognitionMode = RecognitionMode.AiTranscription;
                ModeChanged?.Invoke(this, RecognitionMode.AiTranscription);
            };
            modeItem.DropDownItems.Add(aiModeItem);
            menu.Items.Add(modeItem);

            menu.Items.Add(new ToolStripSeparator());

            // 设置
            var settingsItem = new ToolStripMenuItem("设置…");
            settingsItem.Click += (s, e) => SettingsRequested?.Invoke(this, EventArgs.Empty);
            menu.Items.Add(settingsItem);

            menu.Items.Add(new ToolStripSeparator());

            // 关于
            var aboutItem = new ToolStripMenuItem("关于…");
            aboutItem.Click += (s, e) => ShowAbout();
            menu.Items.Add(aboutItem);

            // 退出
            var exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);
            menu.Items.Add(exitItem);

            return menu;
        }

        private Icon CreateMicrophoneIcon()
        {
            // 创建 16x16 的麦克风图标
            var bitmap = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                // 绘制麦克风形状
                using (var brush = new SolidBrush(Color.White))
                using (var pen = new Pen(brush, 1.5f))
                {
                    // 麦克风主体（椭圆）
                    g.FillEllipse(brush, 5, 1, 6, 9);

                    // 麦克风支架
                    g.DrawArc(pen, 3, 6, 10, 8, 0, 180);

                    // 麦克风底部线条
                    g.DrawLine(pen, 8, 14, 8, 16);
                }
            }

            return Icon.FromHandle(bitmap.GetHicon());
        }

        private void ShowAbout()
        {
            MessageBox.Show(
                "VoiceInput v1.0.0\n\n" +
                "Windows 语音输入工具\n" +
                "按住右 Alt 键录音，松开后自动转录并注入文字\n\n" +
                "支持语言：简体中文、英语、繁体中文、日语、韩语\n\n" +
                "MIT License",
                "关于 VoiceInput",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        public void UpdateTooltip(string text)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Text = text.Length > 63 ? text.Substring(0, 60) + "..." : text;
            }
        }

        public void ShowNotification(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
        {
            _notifyIcon?.ShowBalloonTip(3000, title, text, icon);
        }

        public void Dispose()
        {
            _notifyIcon?.Dispose();
            _contextMenu?.Dispose();
        }
    }
}
