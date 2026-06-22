using System;
using System.IO;

namespace VoiceInput
{
    public static class Logger
    {
        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VoiceInput", "Logs");
        
        private static readonly string LogFile = Path.Combine(LogDir, $"log_{DateTime.Now:yyyyMMdd}.txt");
        private static readonly object _lock = new();

        static Logger()
        {
            try
            {
                Directory.CreateDirectory(LogDir);
            }
            catch { }
        }

        public static void Info(string message) => Write("INFO", message);
        public static void Warn(string message) => Write("WARN", message);
        public static void Error(string message, Exception? ex = null)
        {
            Write("ERROR", message);
            if (ex != null)
            {
                Write("ERROR", $"Exception Type: {ex.GetType().FullName}");
                Write("ERROR", $"Message: {ex.Message}");
                Write("ERROR", $"Stack Trace:\n{ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Write("ERROR", $"Inner Exception: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}");
                    Write("ERROR", $"Inner Stack Trace:\n{ex.InnerException.StackTrace}");
                }
            }
        }

        private static void Write(string level, string message)
        {
            try
            {
                lock (_lock)
                {
                    var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}\n";
                    File.AppendAllText(LogFile, line);
                    System.Diagnostics.Debug.Write(line);
                }
            }
            catch { }
        }

        public static string GetLogFilePath() => LogFile;
    }
}
