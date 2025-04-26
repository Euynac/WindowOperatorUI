using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows;
using WindowOperatorUI.Models;
using WindowOperatorUI.Services;
using WindowOperatorUI.Utils;
using System.Threading.Tasks;
using System.Linq;

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

            // 检查命令行参数
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
                // 在后台模式下运行，不显示UI
                RunBackgroundOperations();
                // 阻止MainWindow的创建/显示
                StartupUri = null;
                // 关闭应用程序
                Shutdown();
                return;
            }

            // 只有在非后台模式下才继续检查管理员权限
            // 检查是否需要以管理员身份运行
            var isAdmin = IsRunningAsAdmin();
            var shouldBeAdmin = ShouldRunAsAdmin();
            
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

            // MainWindow将通过StartupUri在XAML中创建
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
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                
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

                // 按照Order属性排序窗口配置，Order小的先启动（将在底层），Order大的后启动（将在上层）
                var sortedConfigs = config.Windows
                    .OrderBy(w => w.Order ?? 0)
                    .ToList();
                
                logger.Log($"[INFO] Processing {sortedConfigs.Count} window configurations in order");

                // 依次启动每个窗口配置
                foreach (var windowConfig in sortedConfigs)
                {
                    if (string.IsNullOrEmpty(windowConfig.ExePath) || !File.Exists(windowConfig.ExePath))
                    {
                        logger.Log($"[ERROR] File not found: {windowConfig.ExePath}", true);
                        continue;
                    }

                    try
                    {
                        logger.Log($"[INFO] Launching app with Order={windowConfig.Order}: {windowConfig.ExePath}");
                        // 串行处理窗口启动，确保按顺序处理Z轴
                        LaunchAndConfigureWindow(windowConfig, windowManager, logger);
                        
                        // 添加延迟，确保窗口显示和Z轴顺序正确建立
                        Thread.Sleep(500);
                    }
                    catch (Exception ex)
                    {
                        logger.Log($"[ERROR] Failed to launch {windowConfig.ExePath}: {ex.Message}", true);
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
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = config.ExePath,
                    WorkingDirectory = Path.GetDirectoryName(config.ExePath),
                    UseShellExecute = true
                };
                
                logger.Log($"[INFO] Launching application: {config.ExePath}");
                var process = System.Diagnostics.Process.Start(startInfo);
                
                // 使用异步方法获取窗口句柄但同步等待结果
                logger.Log($"[INFO] Waiting for window handle to be available...");
                var (hwnd, windowTitle) = GetWindowHandleAsync(process, logger).GetAwaiter().GetResult();

                if (hwnd == IntPtr.Zero)
                {
                    logger.Log($"[ERROR] Could not obtain window handle for '{Path.GetFileName(config.ExePath)}'.", true);
                    return;
                }

                var dimensions = "original size";
                if (config is { Width: not null, Height: not null })
                {
                    dimensions = $"{config.Width}x{config.Height}";
                }
                
                logger.Log($"[INFO] Positioning window '{windowTitle}' at ({config.X}, {config.Y}), size: {dimensions}...");
                
                // 给应用程序一点时间初始化
                Thread.Sleep(300);
                
                if (windowManager.PositionWindow(hwnd, config, windowTitle))
                {
                    logger.Log($"[SUCCESS] Window '{windowTitle}' positioned successfully.");
                    
                    // 在调整完后再检查实际大小
                    var rect = new NativeMethods.RECT();
                    if (NativeMethods.GetWindowRect(hwnd, ref rect))
                    {
                        var width = rect.Right - rect.Left;
                        var height = rect.Bottom - rect.Top;
                        logger.Log($"[INFO] Final window '{windowTitle}' position: ({rect.Left}, {rect.Top}), size: {width}x{height}");
                    }
                }
                else
                {
                    logger.Log($"[ERROR] Failed to position window '{windowTitle}'");
                }
            }
            catch (Exception ex)
            {
                logger.Log($"[ERROR] Exception during window configuration: {ex.Message}", true);
            }
        }
        
        private Task<(IntPtr hwnd, string windowTitle)> GetWindowHandleAsync(System.Diagnostics.Process process, Logger logger)
        {
            return Task.Run(() => 
            {
                var hwnd = IntPtr.Zero;
                var windowTitle = string.Empty;
                
                for (var i = 0; i < 60; i++)
                {
                    Thread.Sleep(500);
                    try
                    {
                        process.Refresh();
                        hwnd = process.MainWindowHandle;

                        if (hwnd != IntPtr.Zero)
                        {
                            windowTitle = NativeMethods.GetWindowTitle(hwnd);
                            logger.Log($"[SUCCESS] Window handle obtained: {hwnd}, Title: '{windowTitle}'");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Log($"[WARNING] Error refreshing process: {ex.Message}");
                    }
                }
                
                return (hwnd, windowTitle);
            });
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
