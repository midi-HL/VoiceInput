using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VoiceInput
{
    public class KeyboardHook : IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        private const int VK_RMENU = 0xA5; // Right Alt

        private IntPtr _hookId = IntPtr.Zero;
        private LowLevelKeyboardProc? _proc;
        private bool _disposed;

        public event Action? TriggerKeyDown;
        public event Action? TriggerKeyUp;

        public void Install()
        {
            _proc = HookCallback;
            using var process = Process.GetCurrentProcess();
            using var module = process.MainModule;
            if (module != null)
            {
                IntPtr moduleHandle = GetModuleHandle(module.ModuleName!);
                _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, moduleHandle, 0);
            }
        }

        public void Uninstall()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                int msg = (int)wParam;

                if (vkCode == VK_RMENU)
                {
                    if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                    {
                        TriggerKeyDown?.Invoke();
                        return (IntPtr)1; // Suppress the key
                    }
                    else if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
                    {
                        TriggerKeyUp?.Invoke();
                        return (IntPtr)1; // Suppress the key
                    }
                }
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Uninstall();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }

        ~KeyboardHook()
        {
            Dispose();
        }
    }
}
