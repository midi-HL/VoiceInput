using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VoiceInput
{
    public class TrayIcon : IDisposable
    {
        private NotifyIcon? _notifyIcon;
        private bool _disposed;
        private bool _enabled = true;

        public event Action? ShowHomeRequested;
        public event Action? ShowSettingsRequested;
        public event Action? ExitRequested;
        public event Action<bool>? EnableChanged;

        public void Initialize()
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = CreateTrayIcon(),
                Text = "VoiceInput - 语音输入",
                Visible = true
            };

            var contextMenu = new ContextMenuStrip();

            // Enable/Disable toggle
            var enableItem = new ToolStripMenuItem("语音输入  ✓ 已启用");
            enableItem.Click += (s, e) =>
            {
                _enabled = !_enabled;
                enableItem.Text = _enabled ? "语音输入  ✓ 已启用" : "语音输入  ✗ 已禁用";
                EnableChanged?.Invoke(_enabled);
            };
            contextMenu.Items.Add(enableItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            // Home
            var homeItem = new ToolStripMenuItem("主页");
            homeItem.Click += (s, e) => ShowHomeRequested?.Invoke();
            contextMenu.Items.Add(homeItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            // Recognition Language submenu
            var langMenu = new ToolStripMenuItem("识别语言");
            foreach (RecognitionLanguage lang in Enum.GetValues(typeof(RecognitionLanguage)))
            {
                var langItem = new ToolStripMenuItem(Settings.GetLanguageDisplayName(lang));
                langItem.Tag = lang;
                langItem.Checked = lang == Settings.RecognitionLanguage;
                langItem.Click += (s, e) =>
                {
                    if (s is ToolStripMenuItem item && item.Tag is RecognitionLanguage l)
                    {
                        Settings.RecognitionLanguage = l;
                        foreach (ToolStripMenuItem mi in langMenu.DropDownItems)
                            mi.Checked = (RecognitionLanguage)mi.Tag! == l;
                    }
                };
                langMenu.DropDownItems.Add(langItem);
            }
            contextMenu.Items.Add(langMenu);

            // Recognition Mode submenu
            var modeMenu = new ToolStripMenuItem("识别模式");
            var localModeItem = new ToolStripMenuItem("本地识别 + LLM 纠错");
            localModeItem.Checked = Settings.RecognitionMode == RecognitionMode.LocalWithLlmCorrection;
            localModeItem.Click += (s, e) =>
            {
                Settings.RecognitionMode = RecognitionMode.LocalWithLlmCorrection;
                localModeItem.Checked = true;
                aiModeItem.Checked = false;
            };
            var aiModeItem = new ToolStripMenuItem("AI 语音转写（API）");
            aiModeItem.Checked = Settings.RecognitionMode == RecognitionMode.AiTranscription;
            aiModeItem.Click += (s, e) =>
            {
                Settings.RecognitionMode = RecognitionMode.AiTranscription;
                localModeItem.Checked = false;
                aiModeItem.Checked = true;
            };
            modeMenu.DropDownItems.Add(localModeItem);
            modeMenu.DropDownItems.Add(aiModeItem);
            contextMenu.Items.Add(modeMenu);

            contextMenu.Items.Add(new ToolStripSeparator());

            // Settings
            var settingsItem = new ToolStripMenuItem("设置");
            settingsItem.Click += (s, e) => ShowSettingsRequested?.Invoke();
            contextMenu.Items.Add(settingsItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            // About
            var aboutItem = new ToolStripMenuItem("关于...");
            aboutItem.Click += (s, e) =>
            {
                MessageBox.Show(
                    "VoiceInput v1.0.0\n\nWindows 系统托盘语音输入法\n\n按住右 Alt 键开始语音输入",
                    "关于 VoiceInput",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            };
            contextMenu.Items.Add(aboutItem);

            // Exit
            var exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += (s, e) => ExitRequested?.Invoke();
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (s, e) => ShowHomeRequested?.Invoke();
        }

        private static Icon CreateTrayIcon()
        {
            int size = 256;
            using var bmp = new Bitmap(size, size);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            // Draw microphone icon
            using var brush = new SolidBrush(Color.White);
            using var pen = new Pen(Color.White, size * 0.04f);

            float cx = size / 2f;
            float cy = size * 0.35f;
            float micWidth = size * 0.2f;
            float micHeight = size * 0.3f;

            // Microphone body (rounded rectangle)
            var micRect = new RectangleF(cx - micWidth / 2, cy - micHeight / 2, micWidth, micHeight);
            using var micPath = new GraphicsPath();
            float radius = micWidth * 0.4f;
            micPath.AddArc(micRect.X, micRect.Y, radius * 2, radius * 2, 180, 90);
            micPath.AddArc(micRect.Right - radius * 2, micRect.Y, radius * 2, radius * 2, 270, 90);
            micPath.AddLine(micRect.Right, micRect.Bottom, micRect.X, micRect.Bottom);
            micPath.CloseFigure();
            g.FillPath(brush, micPath);

            // Microphone arc
            float arcRadius = size * 0.18f;
            g.DrawArc(pen, cx - arcRadius, cy + micHeight * 0.3f, arcRadius * 2, arcRadius * 2, 0, 180);

            // Microphone stand
            float standX = cx;
            float standTop = cy + micHeight * 0.3f + arcRadius;
            float standBottom = standTop + size * 0.1f;
            g.DrawLine(pen, standX, standTop, standX, standBottom);

            // Base
            float baseWidth = size * 0.15f;
            g.DrawLine(pen, standX - baseWidth, standBottom, standX + baseWidth, standBottom);

            // Convert to icon
            IntPtr hIcon = bmp.GetHicon();
            return Icon.FromHandle(hIcon);
        }

        public void ShowBalloonTip(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
        {
            _notifyIcon?.ShowBalloonTip(3000, title, text, icon);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _notifyIcon?.Dispose();
                _notifyIcon = null;
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}
