using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using WindowOperatorUI.Models;
using WindowOperatorUI.Services;
using WindowOperatorUI.Utils;

namespace WindowOperatorUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static string ConfigPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        private static string LogPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt");

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Check command line arguments
            var runInBackground = false;
            
            if (e.Args.Length > 0)
            {
                foreach (var arg in e.Args)
                {
                    if (arg.ToLower() == "--background" || arg.ToLower() == "-b")
                    {
                        runInBackground = true;
                        break;
                    }
                }
            }

            if (runInBackground)
            {
                // Run in background mode (no UI)
                RunBackgroundOperations();
                Shutdown();
            }
            else
            {
                // Show the main window (UI mode)
                var mainWindow = new MainWindow();
                mainWindow.Show();
            }
        }

        private void RunBackgroundOperations()
        {
            try
            {
                // Load configuration
                if (!File.Exists(ConfigPath))
                {
                    WriteToLog("[ERROR] Configuration file not found");
                    return;
                }

                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);

                if (config == null)
                {
                    WriteToLog("[ERROR] Failed to load configuration");
                    return;
                }

                // Create logger based on silent mode
                var logger = new Logger(LogPath, config.SilentMode);
                
                // Create window manager
                var windowManager = new WindowManager(logger);

                // Process each window configuration
                foreach (var windowConfig in config.Windows)
                {
                    if (string.IsNullOrEmpty(windowConfig.ExePath) || !File.Exists(windowConfig.ExePath))
                    {
                        logger.Log($"[ERROR] File not found: {windowConfig.ExePath}", true);
                        continue;
                    }

                    try
                    {
                        // This will launch and position each configured window
                        LaunchAndConfigureWindow(windowConfig, windowManager, logger);
                    }
                    catch (Exception ex)
                    {
                        logger.Log($"[ERROR] {ex.Message}", true);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteToLog($"[ERROR] {ex.Message}");
            }
        }

        private void LaunchAndConfigureWindow(WindowConfig config, WindowManager windowManager, Logger logger)
        {
            // This method is similar to the one in Program.cs
            // We're replicating the functionality here for the background mode
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = config.ExePath,
                WorkingDirectory = Path.GetDirectoryName(config.ExePath),
                UseShellExecute = true
            };
            
            var process = System.Diagnostics.Process.Start(startInfo);

            var hwnd = IntPtr.Zero;
            var windowTitle = string.Empty;
            
            for (var i = 0; i < 10; i++)
            {
                System.Threading.Thread.Sleep(500);
                process.Refresh();
                hwnd = process.MainWindowHandle;

                if (hwnd != IntPtr.Zero)
                {
                    windowTitle = NativeMethods.GetWindowTitle(hwnd);
                    logger.Log($"[SUCCESS] Window handle obtained: {hwnd}, Title: '{windowTitle}'");
                    break;
                }
            }

            if (hwnd == IntPtr.Zero)
            {
                logger.Log($"[ERROR] Could not obtain window handle for '{Path.GetFileName(config.ExePath)}'.", true);
                return;
            }

            var dimensions = $"{config.Width}x{config.Height}";
            if (!config.Width.HasValue || !config.Height.HasValue)
            {
                dimensions = "original size";
            }
            
            logger.Log($"[INFO] Positioning window '{windowTitle}' at ({config.X}, {config.Y}), {dimensions}...");
            
            if (windowManager.PositionWindow(hwnd, config, windowTitle))
            {
                logger.Log($"[SUCCESS] Window '{windowTitle}' positioned successfully.");
            }
        }

        private void WriteToLog(string message)
        {
            try
            {
                File.AppendAllText(LogPath, $"[{DateTime.Now}] {message}{Environment.NewLine}");
            }
            catch
            {
                // If we can't write to the log file, there's not much we can do
            }
        }
    }
}
