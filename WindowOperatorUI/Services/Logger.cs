using System;
using System.IO;

namespace WindowOperatorUI.Services
{
    public class Logger
    {
        private readonly string _logPath;
        private readonly bool _silentMode;

        public Logger(string logPath, bool silentMode)
        {
            _logPath = logPath;
            _silentMode = silentMode;
        }

        public void Log(string message, bool isError = false)
        {
            if (!_silentMode || isError)
            {
                Console.ForegroundColor = isError ? ConsoleColor.Red : ConsoleColor.White;
                Console.WriteLine(message);
                Console.ResetColor();
            }

            // Always write to log file regardless of silent mode
            try
            {
                File.AppendAllText(_logPath, $"[{DateTime.Now}] {message}{Environment.NewLine}");
            }
            catch
            {
                // If logging fails, there's not much we can do
            }
        }
    }
    
    // Simple console logger for testing
    public class ConsoleLogger : Logger
    {
        public ConsoleLogger() : base(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt"), false)
        {
        }
    }
} 