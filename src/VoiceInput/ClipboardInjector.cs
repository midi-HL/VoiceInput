using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace VoiceInput
{
    public static class ClipboardInjector
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll")]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll")]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll")]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("user32.dll")]
        private static extern IntPtr GetClipboardData(uint uFormat);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll")]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll")]
        private static extern UIntPtr GlobalSize(IntPtr hMem);

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
            public KEYBDINPUT ki;
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
        private const uint KEYEVENTF_CTRLDOWN = 0x0000;
        private const ushort VK_CONTROL = 0x11;
        private const ushort VK_V = 0x56;
        private const uint CF_UNICODETEXT = 13;
        private const uint GMEM_MOVEABLE = 0x0002;

        public static async Task InjectTextAsync(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            IntPtr foregroundHwnd = GetForegroundWindow();

            // Save original clipboard content
            string? originalClipboardText = null;
            try
            {
                var thread = new Thread(() =>
                {
                    try
                    {
                        if (System.Windows.Forms.Clipboard.ContainsText())
                            originalClipboardText = System.Windows.Forms.Clipboard.GetText();
                    }
                    catch { }
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join(500);
            }
            catch { }

            // Write text to clipboard
            bool clipboardOk = false;
            try
            {
                var thread = new Thread(() =>
                {
                    try
                    {
                        System.Windows.Forms.Clipboard.SetText(text);
                        clipboardOk = true;
                    }
                    catch { }
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join(500);
            }
            catch { }

            if (!clipboardOk)
            {
                // Fallback: just notify user
                System.Windows.Forms.MessageBox.Show(
                    "文字已复制到剪贴板，请手动粘贴。\nText copied to clipboard, please paste manually.",
                    "VoiceInput",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Information);
                return;
            }

            // Small delay to ensure clipboard is ready
            await Task.Delay(50);

            // Restore focus to foreground window
            if (foregroundHwnd != IntPtr.Zero)
            {
                SetForegroundWindow(foregroundHwnd);
                await Task.Delay(30);
            }

            // Simulate Ctrl+V
            SimulateCtrlV();

            // Wait for paste to complete, then restore clipboard
            await Task.Delay(200);

            // Restore original clipboard
            try
            {
                var thread = new Thread(() =>
                {
                    try
                    {
                        if (originalClipboardText != null)
                            System.Windows.Forms.Clipboard.SetText(originalClipboardText);
                        else
                            System.Windows.Forms.Clipboard.Clear();
                    }
                    catch { }
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join(500);
            }
            catch { }
        }

        private static void SimulateCtrlV()
        {
            INPUT[] inputs = new INPUT[4];

            // Ctrl down
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].union.ki.wVk = VK_CONTROL;

            // V down
            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].union.ki.wVk = VK_V;

            // V up
            inputs[2].type = INPUT_KEYBOARD;
            inputs[2].union.ki.wVk = VK_V;
            inputs[2].union.ki.dwFlags = KEYEVENTF_KEYUP;

            // Ctrl up
            inputs[3].type = INPUT_KEYBOARD;
            inputs[3].union.ki.wVk = VK_CONTROL;
            inputs[3].union.ki.dwFlags = KEYEVENTF_KEYUP;

            SendInput(4, inputs, Marshal.SizeOf(typeof(INPUT)));
        }
    }
}
