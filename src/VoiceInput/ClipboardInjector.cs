using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using Application = System.Windows.Application;

namespace VoiceInput
{
    public class ClipboardInjector
    {
        #region Win32 API

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public INPUTUNION union;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_CTRL = 0x0000;
        private const ushort VK_CONTROL = 0x11;
        private const ushort VK_V = 0x56;

        #endregion

        private string _originalClipboardText = string.Empty;
        private bool _hasOriginalClipboard;

        public async Task InjectTextAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            try
            {
                // 获取前台窗口句柄
                IntPtr foregroundWindow = GetForegroundWindow();
                if (foregroundWindow == IntPtr.Zero)
                {
                    // 没有前台窗口，降级为写入剪贴板
                    await SetClipboardTextAsync(text);
                    ShowTrayNotification("已复制到剪贴板，请手动粘贴");
                    return;
                }

                // 保存原剪贴板内容
                await SaveClipboardAsync();

                // 写入新文字到剪贴板
                await SetClipboardTextAsync(text);

                // 等待确保目标窗口已获焦点
                await Task.Delay(100);

                // 模拟 Ctrl+V 粘贴
                SimulatePaste();

                // 等待粘贴完成
                await Task.Delay(200);

                // 恢复原剪贴板
                await RestoreClipboardAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"文字注入失败: {ex.Message}");

                // 降级处理：至少确保文字在剪贴板中
                try
                {
                    await SetClipboardTextAsync(text);
                    ShowTrayNotification("注入失败，文字已复制到剪贴板");
                }
                catch { }
            }
        }

        private async Task SaveClipboardAsync()
        {
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (Clipboard.ContainsText())
                    {
                        _originalClipboardText = Clipboard.GetText();
                        _hasOriginalClipboard = true;
                    }
                    else
                    {
                        _hasOriginalClipboard = false;
                    }
                });
            }
            catch
            {
                _hasOriginalClipboard = false;
            }
        }

        private async Task RestoreClipboardAsync()
        {
            if (!_hasOriginalClipboard) return;

            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Clipboard.SetText(_originalClipboardText);
                });
            }
            catch { }
        }

        private async Task SetClipboardTextAsync(string text)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Clipboard.SetText(text);
            });
        }

        private void SimulatePaste()
        {
            var inputs = new INPUT[4];

            // Ctrl 按下
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].union.ki.wVk = VK_CONTROL;
            inputs[0].union.ki.dwFlags = KEYEVENTF_CTRL;

            // V 按下
            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].union.ki.wVk = VK_V;
            inputs[1].union.ki.dwFlags = 0;

            // V 释放
            inputs[2].type = INPUT_KEYBOARD;
            inputs[2].union.ki.wVk = VK_V;
            inputs[2].union.ki.dwFlags = KEYEVENTF_KEYUP;

            // Ctrl 释放
            inputs[3].type = INPUT_KEYBOARD;
            inputs[3].union.ki.wVk = VK_CONTROL;
            inputs[3].union.ki.dwFlags = KEYEVENTF_KEYUP;

            SendInput(4, inputs, Marshal.SizeOf<INPUT>());
        }

        private void ShowTrayNotification(string message)
        {
            // 这里可以通过事件或直接调用 TrayIcon 显示通知
            System.Diagnostics.Debug.WriteLine($"通知: {message}");
        }
    }
}
