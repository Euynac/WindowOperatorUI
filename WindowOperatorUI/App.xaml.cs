using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
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

            // 检查是否需要以管理员身份运行
            bool isAdmin = IsRunningAsAdmin();
            bool shouldBeAdmin = ShouldRunAsAdmin();
            
            // 如果需要管理员权限但当前不是管理员
            if (!isAdmin && shouldBeAdmin)
            {
                RestartAsAdmin();
                Shutdown();
                return;
            }
            // 如果当前是管理员权限但配置不需要
            else if (isAdmin && !shouldBeAdmin)
            {
                // 这种情况下可能是用户手动以管理员身份启动的
                // 不强制降权，允许程序继续运行
                MessageBox.Show("当前以管理员身份运行，但配置中未启用此选项。\n如需以普通用户身份运行，请关闭程序后手动以普通权限重启。", 
                    "权限提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }

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
                // 关闭已经通过XAML创建的窗口（如果有）
                if (MainWindow != null)
                {
                    MainWindow.Close();
                }
                Shutdown();
            }
            // 不再手动创建MainWindow，因为它已经通过StartupUri在XAML中声明
        }

        private bool IsRunningAsAdmin()
        {
            try
            {
                // 获取当前Windows用户标识
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                
                // 检查是否具有管理员权限
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private bool ShouldRunAsAdmin()
        {
            try
            {
                // 检查配置文件是否存在
                if (!File.Exists(ConfigPath))
                {
                    return false;
                }

                // 读取配置
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);
                
                // 检查RunAsAdmin设置
                return config?.RunAsAdmin ?? false;
            }
            catch
            {
                return false;
            }
        }

        private void RestartAsAdmin()
        {
            try
            {
                // 获取当前执行文件路径
                string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                
                // 创建启动信息
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    Verb = "runas" // 请求以管理员身份运行
                };
                
                // 尝试启动新进程
                System.Diagnostics.Process.Start(startInfo);
            }
            catch
            {
                // 启动失败时不做任何处理，让程序继续以普通权限运行
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
                Thread.Sleep(500);
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
