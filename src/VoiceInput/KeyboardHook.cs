using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VoiceInput
{
    public class KeyboardHook : IDisposable
    {
        #region Win32 API

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        // 虚拟键码
        private const int VK_RMENU = 0xA5; // Right Alt
        private const int VK_LMENU = 0xA4; // Left Alt

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        #endregion

        private IntPtr _hookId = IntPtr.Zero;
        private readonly LowLevelKeyboardProc _proc;
        private bool _isKeyDown;
        private bool _disposed;

        // 事件
        public event EventHandler? KeyDown;
        public event EventHandler? KeyUp;

        public KeyboardHook()
        {
            _proc = HookCallback;
            _hookId = SetHook(_proc);
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using var process = Process.GetCurrentProcess();
            using var module = process.MainModule;
            if (module == null) return IntPtr.Zero;

            IntPtr moduleHandle = GetModuleHandle(module.ModuleName ?? "");
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc, moduleHandle, 0);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                int msg = wParam.ToInt32();

                // 检查是否是目标按键（右 Alt 键）
                if (IsTargetKey(hookStruct.vkCode))
                {
                    if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                    {
                        if (!_isKeyDown)
                        {
                            _isKeyDown = true;
                            KeyDown?.Invoke(this, EventArgs.Empty);
                        }
                        // 抑制按键传递
                        return (IntPtr)1;
                    }
                    else if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
                    {
                        if (_isKeyDown)
                        {
                            _isKeyDown = false;
                            KeyUp?.Invoke(this, EventArgs.Empty);
                        }
                        // 抑制按键传递
                        return (IntPtr)1;
                    }
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private bool IsTargetKey(uint vkCode)
        {
            string triggerKey = Settings.TriggerKey;

            return triggerKey switch
            {
                "RightAlt" => vkCode == VK_RMENU,
                "LeftAlt" => vkCode == VK_LMENU,
                _ => vkCode == VK_RMENU // 默认右 Alt
            };
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }
    }
}
