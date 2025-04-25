using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using WindowOperatorUI.Controls;
using WindowOperatorUI.Models;
using WindowOperatorUI.Services;
using WindowOperatorUI.Utils;
using System.Threading;
using Microsoft.Win32;

namespace WindowOperatorUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private ObservableCollection<WindowConfig> _windowConfigs = [];
        private string _configPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        private AppConfig _appConfig;
        private bool _isDraggingWindowSelector = false;
        private WindowSelector _windowSelector;
        private WindowManager _windowManager;

        public MainWindow()
        {
            InitializeComponent();
            LoadConfiguration();
            lvWindowConfigs.ItemsSource = _windowConfigs;
            
            // Initialize the window manager with a logger
            _windowManager = new WindowManager(new ConsoleLogger());
            
            // 检查当前是否以管理员身份运行
            UpdateAdminStatus();
        }

        private void LoadConfiguration()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    _appConfig = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                    
                    // Update UI based on config
                    chkSilentMode.IsChecked = _appConfig.SilentMode;
                    chkRunAtStartup.IsChecked = _appConfig.RunAtStartup;
                    chkRunAsAdmin.IsChecked = _appConfig.RunAsAdmin;
                    chkKeepOriginalSize.IsChecked = _appConfig.KeepOriginalSize;
                    
                    // Load window configurations
                    _windowConfigs.Clear();
                    foreach (var config in _appConfig.Windows)
                    {
                        _windowConfigs.Add(config);
                    }
                }
                else
                {
                    // Create default configuration
                    _appConfig = new AppConfig();
                    SaveConfiguration();
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Error loading configuration: {ex.Message}");
                _appConfig = new AppConfig();
            }
        }

        private void SaveConfiguration()
        {
            try
            {
                // Update app config with current UI settings
                _appConfig.SilentMode = chkSilentMode.IsChecked ?? false;
                _appConfig.RunAtStartup = chkRunAtStartup.IsChecked ?? false;
                _appConfig.RunAsAdmin = chkRunAsAdmin.IsChecked ?? false;
                _appConfig.KeepOriginalSize = chkKeepOriginalSize.IsChecked ?? true;
                
                // Update window configurations
                _appConfig.Windows = _windowConfigs.ToList();
                
                // Save to file
                var json = JsonSerializer.Serialize(_appConfig, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Error saving configuration: {ex.Message}");
            }
        }

        private void BtnSaveConfig_Click(object sender, RoutedEventArgs e)
        {
            SaveConfiguration();
            NotificationService.ShowSuccess("Configuration saved successfully.");
        }

        private void BtnAddConfig_Click(object sender, RoutedEventArgs e)
        {
            var newConfig = new WindowConfig();
            _windowConfigs.Add(newConfig);
            lvWindowConfigs.SelectedIndex = _windowConfigs.Count - 1;
        }

        private void BtnRemoveConfig_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: WindowConfig config })
            {
                _windowConfigs.Remove(config);
            }
        }

        private void BtnRunConfig_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: WindowConfig config })
            {
                RunConfiguration(config);
            }
        }

        private void BtnRunSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedConfigs = GetSelectedConfigurations();
            if (selectedConfigs.Count == 0)
            {
                NotificationService.ShowWarning("No configurations selected. Please select at least one configuration to run.");
                return;
            }

            foreach (var config in selectedConfigs)
            {
                RunConfiguration(config);
            }
        }

        private List<WindowConfig> GetSelectedConfigurations()
        {
            var selectedConfigs = new List<WindowConfig>();
            
            foreach (var item in lvWindowConfigs.Items)
            {
                var container = lvWindowConfigs.ItemContainerGenerator.ContainerFromItem(item) as ListViewItem;
                if (container != null)
                {
                    // Find the checkbox within the item template
                    var checkBox = FindVisualChild<CheckBox>(container, "chkSelectConfig");
                    if (checkBox is { IsChecked: true } && item is WindowConfig config)
                    {
                        selectedConfigs.Add(config);
                    }
                }
            }
            
            return selectedConfigs;
        }

        private T FindVisualChild<T>(DependencyObject parent, string name) where T : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T typedChild and FrameworkElement element && element.Name == name)
                {
                    return typedChild;
                }

                var result = FindVisualChild<T>(child, name);
                if (result != null)
                {
                    return result;
                }
            }
            
            return null;
        }

        private void RunConfiguration(WindowConfig config)
        {
            try
            {
                if (string.IsNullOrEmpty(config.ExePath) || !File.Exists(config.ExePath))
                {
                    NotificationService.ShowError($"File not found: {config.ExePath}");
                    return;
                }
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = config.ExePath,
                    WorkingDirectory = Path.GetDirectoryName(config.ExePath),
                    UseShellExecute = true
                };
                
                var process = Process.Start(startInfo);
                
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
                        break;
                    }
                }

                if (hwnd == IntPtr.Zero)
                {
                    NotificationService.ShowError($"Could not get window handle for: {Path.GetFileName(config.ExePath)}");
                    return;
                }

                if (_windowManager.PositionWindow(hwnd, config, windowTitle))
                {
                    // Store window binding information
                    config.BoundWindowHandle = hwnd;
                    config.BoundProcessId = process.Id;
                    config.BoundWindowTitle = windowTitle;
                    
                    // Refresh the list view to show bound window controls
                    lvWindowConfigs.Items.Refresh();
                    
                    NotificationService.ShowSuccess($"Window '{windowTitle}' positioned successfully and bound to configuration.");
                }
                else
                {
                    NotificationService.ShowError($"Failed to position window: {windowTitle}");
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Error running configuration: {ex.Message}");
            }
        }

        private void BtnApplyPosition_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: WindowConfig config })
            {
                try
                {
                    if (!IsWindowHandleValid(config.BoundWindowHandle))
                    {
                        NotificationService.ShowError("Window is no longer available. Unbinding...");
                        UnbindWindow(config);
                        return;
                    }
                    
                    if (_windowManager.PositionWindow(config.BoundWindowHandle, config, config.BoundWindowTitle))
                    {
                        NotificationService.ShowSuccess($"Updated position for window: {config.BoundWindowTitle}");
                    }
                    else
                    {
                        NotificationService.ShowError($"Failed to update position for window: {config.BoundWindowTitle}");
                    }
                }
                catch (Exception ex)
                {
                    NotificationService.ShowError($"Error applying position: {ex.Message}");
                }
            }
        }
        
        private void BtnUpdateZOrder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: WindowConfig config })
            {
                try
                {
                    if (!IsWindowHandleValid(config.BoundWindowHandle))
                    {
                        NotificationService.ShowError("Window is no longer available. Unbinding...");
                        UnbindWindow(config);
                        return;
                    }
                    
                    // Just update Z-Order without changing position or size
                    var flags = NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE;
                    var hwnd = config.BoundWindowHandle;
                    
                    // First reset window Z-order to non-topmost
                    NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_NOTOPMOST, 0, 0, 0, 0, flags);
                    
                    // Handle bottom positioning
                    if (config.EnableAlwaysOnBottom)
                    {
                        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_BOTTOM, 0, 0, 0, 0, flags);
                    }
                    // Process topmost state
                    else if (config.EnableAlwaysOnTopMost)
                    {
                        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0, flags);
                    }
                    else if (config.EnableAlwaysOnTop)
                    {
                        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOP, 0, 0, 0, 0, flags);
                    }
                    
                    NotificationService.ShowSuccess($"Updated Z-order for window: {config.BoundWindowTitle}");
                }
                catch (Exception ex)
                {
                    NotificationService.ShowError($"Error updating Z-order: {ex.Message}");
                }
            }
        }
        
        private void BtnUnbindWindow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: WindowConfig config })
            {
                UnbindWindow(config);
                NotificationService.ShowInfo($"Window unbound from configuration.");
            }
        }
        
        private void UnbindWindow(WindowConfig config)
        {
            config.BoundWindowHandle = IntPtr.Zero;
            config.BoundProcessId = 0;
            config.BoundWindowTitle = string.Empty;
            lvWindowConfigs.Items.Refresh();
        }
        
        private bool IsWindowHandleValid(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return false;
                
            return NativeMethods.IsWindow(hwnd);
        }

        private void BtnSelectWindow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: WindowConfig config })
            {
                // Hide this application window temporarily
                var opacity = Opacity;
                Opacity = 0.1;
                
                // Create and show the window selector
                _windowSelector = new WindowSelector(_appConfig.KeepOriginalSize);
                
                // 添加保持窗口大小设置改变事件处理
                _windowSelector.KeepOriginalSizeChanged += (s, keepOriginalSize) => 
                {
                    // 当窗口选择器中的设置改变时，同步更新应用设置UI
                    _appConfig.KeepOriginalSize = keepOriginalSize;
                    chkKeepOriginalSize.IsChecked = keepOriginalSize;
                };
                
                _windowSelector.WindowSelected += (s, args) =>
                {
                    if (args.WindowHandle != IntPtr.Zero)
                    {
                        // Update the config with the selected window info
                        config.ExePath = args.ExecutablePath;
                        config.X = args.X;
                        config.Y = args.Y;
                        config.Width = args.Width;
                        config.Height = args.Height;
                        
                        // Refresh the list view to show updated values
                        lvWindowConfigs.Items.Refresh();
                    }
                    
                    // Restore the main window
                    Opacity = opacity;
                    _windowSelector = null;
                };
                
                _windowSelector.Show();
            }
        }

        private void BtnEditPath_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: WindowConfig config } button)
            {
                // Find the parent ListViewItem
                var listViewItem = FindAncestor<ListViewItem>(button);
                if (listViewItem == null) return;
                
                // Find the path edit grid in this item
                var pathEditGrid = FindVisualChild<Grid>(listViewItem, "pathEditGrid");
                if (pathEditGrid == null) return;
                
                // Toggle visibility
                pathEditGrid.Visibility = pathEditGrid.Visibility == Visibility.Visible 
                    ? Visibility.Collapsed 
                    : Visibility.Visible;
            }
        }

        private void BtnBrowseExePath_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: WindowConfig config })
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                    InitialDirectory = Path.GetDirectoryName(config.ExePath)
                };
                
                if (dialog.ShowDialog() == true)
                {
                    config.ExePath = dialog.FileName;
                    
                    // This is needed to refresh the UI with the new path
                    lvWindowConfigs.Items.Refresh();
                    
                    // Find the parent ListViewItem
                    var listViewItem = FindAncestor<ListViewItem>(sender as DependencyObject);
                    if (listViewItem == null) return;
                    
                    // Find the TextBox in this item and update its text
                    var txtExePath = FindVisualChild<TextBox>(listViewItem, "txtExePath");
                    if (txtExePath != null)
                    {
                        txtExePath.Text = config.ExePath;
                    }
                }
            }
        }

        private T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null && !(current is T))
            {
                current = VisualTreeHelper.GetParent(current);
            }
            return current as T;
        }

        #region Window Event Handlers
        
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        
        private void AppSettings_Changed(object sender, RoutedEventArgs e)
        {
            // 当应用设置改变时，实时更新 _appConfig 对象中的值
            if (sender is CheckBox checkBox)
            {
                if (checkBox == chkKeepOriginalSize)
                {
                    _appConfig.KeepOriginalSize = checkBox.IsChecked ?? true;
                }
                else if (checkBox == chkSilentMode)
                {
                    _appConfig.SilentMode = checkBox.IsChecked ?? false;
                }
                else if (checkBox == chkRunAtStartup)
                {
                    _appConfig.RunAtStartup = checkBox.IsChecked ?? false;
                }
                else if (checkBox == chkRunAsAdmin)
                {
                    bool isChecked = checkBox.IsChecked ?? false;
                    _appConfig.RunAsAdmin = isChecked;
                    
                    // 获取当前是否以管理员身份运行
                    bool isCurrentlyAdmin = IsRunningAsAdmin();
                    
                    // 如果当前状态与请求的状态不同，询问是否重启
                    if (isCurrentlyAdmin != isChecked)
                    {
                        string message = isChecked ? 
                            "需要以管理员身份重新启动程序才能使此设置生效。是否立即重启？" : 
                            "需要以普通用户身份重新启动程序才能使此设置生效。是否立即重启？";
                        
                        var result = MessageBox.Show(
                            message, 
                            "权限变更", 
                            MessageBoxButton.YesNo, 
                            MessageBoxImage.Question);
                        
                        if (result == MessageBoxResult.Yes)
                        {
                            SaveConfiguration();
                            if (isChecked)
                                RestartAsAdmin();
                            else
                                RestartAsNormalUser();
                        }
                    }
                }
            }
            
            // 不需要立即保存，用户将在准备好时保存
        }
        
        private void LvWindowConfigs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // You can add additional logic here if needed
        }
        
        #endregion

        #region Drag and Drop Handlers
        
        private void ConfigItem_DragEnter(object sender, DragEventArgs e)
        {
            // 清除所有默认的拖放目标效果
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            
            // 对文件拖放提供特殊处理
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // 必须设置为Copy才能显示正确的拖放图标
                e.Effects = DragDropEffects.Copy;
                
                // 视觉反馈 - 背景高亮
                if (sender is Grid grid)
                {
                    grid.Background = new SolidColorBrush(Color.FromArgb(40, 100, 180, 255));
                }
            }
        }
        
        private void ConfigItem_DragOver(object sender, DragEventArgs e)
        {
            // 清除所有默认的拖放目标效果
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            
            // 对文件拖放提供特殊处理
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // 必须设置为Copy才能显示正确的拖放图标
                e.Effects = DragDropEffects.Copy;
                
                // 视觉反馈 - 保持高亮状态
                if (sender is Grid grid && grid.Background == Brushes.Transparent)
                {
                    grid.Background = new SolidColorBrush(Color.FromArgb(40, 100, 180, 255));
                }
            }
        }
        
        private void ConfigItem_DragLeave(object sender, DragEventArgs e)
        {
            // 重置视觉反馈
            if (sender is Grid grid)
            {
                grid.Background = Brushes.Transparent;
            }
            
            e.Handled = true;
        }
        
        private void ConfigItem_Drop(object sender, DragEventArgs e)
        {
            try
            {
                // 重置视觉反馈
                if (sender is Grid grid)
                {
                    grid.Background = Brushes.Transparent;
                }
                
                // 检查是否是文件拖放
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    WindowConfig config = null;
                    
                    // 从Tag或其他方式获取WindowConfig
                    if (sender is FrameworkElement element)
                    {
                        config = element.Tag as WindowConfig;
                    }
                    
                    if (config != null)
                    {
                        // 获取拖放的文件
                        string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                        
                        // 如果有多个文件被拖放，取第一个
                        if (files != null && files.Length > 0)
                        {
                            string filePath = files[0];
                            
                            // 更新配置中的路径
                            config.ExePath = filePath;
                            
                            // 刷新ListView显示更新后的路径
                            lvWindowConfigs.Items.Refresh();
                            
                            // 显示成功通知
                            NotificationService.ShowSuccess($"Path updated to: {Path.GetFileName(filePath)}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Error during file drop: {ex.Message}");
            }
            
            e.Handled = true;
        }
        
        #endregion

        #region Admin Privileges Handling
        
        private void RestartAsAdmin()
        {
            try
            {
                // 获取当前执行文件路径
                string exePath = Process.GetCurrentProcess().MainModule.FileName;
                
                // 创建启动信息
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    Verb = "runas" // 请求以管理员身份运行
                };
                
                // 尝试启动新进程
                Process.Start(startInfo);
                
                // 关闭当前进程
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"无法以管理员身份重启: {ex.Message}");
            }
        }
        
        private void RestartAsNormalUser()
        {
            try
            {
                // 保存当前配置文件路径，确保同步更改保存
                SaveConfiguration();
                
                // 显示提示消息
                MessageBox.Show("应用程序将关闭。请手动以普通用户身份重新启动应用程序。", 
                    "权限降级", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // 正常关闭应用程序
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"无法退出应用程序: {ex.Message}");
            }
        }
        
        #endregion

        private void UpdateAdminStatus()
        {
            try
            {
                // 获取当前Windows用户标识
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                
                // 检查是否具有管理员权限
                bool isAdmin = principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
                
                // 更新UI显示
                chkRunAsAdmin.IsChecked = isAdmin;
                
                // 如果当前是管理员权限运行，更新配置
                if (isAdmin && _appConfig != null)
                {
                    _appConfig.RunAsAdmin = true;
                }
                
                // 添加管理员状态提示
                if (isAdmin)
                {
                    chkRunAsAdmin.Content = "以管理员身份运行 (当前已启用)";
                    chkRunAsAdmin.Foreground = new SolidColorBrush(Colors.LightGreen);
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"无法检查管理员状态: {ex.Message}");
            }
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
            catch (Exception ex)
            {
                NotificationService.ShowError($"无法检查管理员状态: {ex.Message}");
                return false;
            }
        }
    }
}
