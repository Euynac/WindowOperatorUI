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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Threading;

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
        private Stack<WindowConfig> _deletedConfigs = new Stack<WindowConfig>(); // Stack for undo functionality
        private WindowConfig _draggedItem; // For drag-drop reordering
        private bool _isInitializing = true; // 添加标志以防止初始化触发事件
        private Point _dragStartPoint;
        private bool _isDragging = false;
        
        // Process monitoring timer
        private System.Windows.Threading.DispatcherTimer _processMonitoringTimer;
        private Dictionary<int, DateTime> _processLastCheckTime = new Dictionary<int, DateTime>();
        private Dictionary<int, TimeSpan> _processLastCpuTime = new Dictionary<int, TimeSpan>();

        public MainWindow()
        {
            InitializeComponent();
            _isInitializing = true; // 设置初始化标志
            
            LoadConfiguration();
            lvWindowConfigs.ItemsSource = _windowConfigs;
            
            // Initialize the window manager with a logger
            _windowManager = new WindowManager(new ConsoleLogger());
            
            // 检查当前是否以管理员身份运行
            UpdateAdminStatus();
            
            // Initialize order numbers if needed
            UpdateConfigurationOrder();
            
            // Add event handlers
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
            
            // Start process monitoring if enabled
            if (_appConfig.ShowBoundProcessDetails)
            {
                StartProcessMonitoring();
            }
            
            _isInitializing = false; // 初始化完成后关闭标志
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Apply acrylic effect to window with dark tint
            WindowBackdrop.ApplyAcrylicEffect(this, 0x99202020);
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Stop process monitoring
            StopProcessMonitoring();
            
            // Unbind all windows to ensure proper cleanup
            foreach (var config in _windowConfigs.Where(c => c.IsBound).ToList())
            {
                UnbindWindow(config);
            }
        }

        private void LoadConfiguration()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    _appConfig = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                    
                    // 确保在设置UI状态时不会触发事件
                    var oldInitializing = _isInitializing;
                    _isInitializing = true;
                    
                    try
                    {
                        // Update UI based on config
                        chkSilentMode.IsChecked = _appConfig.SilentMode;
                        chkRunAtStartup.IsChecked = _appConfig.RunAtStartup;
                        chkRunAsAdmin.IsChecked = _appConfig.RunAsAdmin;
                        chkKeepOriginalSize.IsChecked = _appConfig.KeepOriginalSize;
                        chkConfirmProcessKill.IsChecked = _appConfig.ConfirmProcessKill;
                        chkBindToProcessWhenSelectingWindow.IsChecked = _appConfig.BindToProcessWhenSelectingWindow;
                        chkShowBoundProcessDetails.IsChecked = _appConfig.ShowBoundProcessDetails;
                    }
                    finally
                    {
                        // 恢复初始化状态标志
                        _isInitializing = oldInitializing;
                    }
                    
                    // Load window configurations
                    _windowConfigs.Clear();
                    foreach (var config in _appConfig.Windows)
                    {
                        // Save original config for potential restore
                        config.OriginalConfig = config.Clone();
                        _windowConfigs.Add(config);
                    }
                    
                    // Update configuration order
                    UpdateConfigurationOrder();
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

        private void UpdateConfigurationOrder()
        {
            for (int i = 0; i < _windowConfigs.Count; i++)
            {
                _windowConfigs[i].Order = i + 1;
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
                _appConfig.ConfirmProcessKill = chkConfirmProcessKill.IsChecked ?? true;
                _appConfig.BindToProcessWhenSelectingWindow = chkBindToProcessWhenSelectingWindow.IsChecked ?? false;
                _appConfig.ShowBoundProcessDetails = chkShowBoundProcessDetails.IsChecked ?? true;
                
                // Update window configurations
                _appConfig.Windows = _windowConfigs.ToList();
                
                // After saving, update the original config references
                foreach (var config in _windowConfigs)
                {
                    config.OriginalConfig = config.Clone();
                }
                
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
            UpdateConfigurationOrder();
            lvWindowConfigs.SelectedIndex = _windowConfigs.Count - 1;
        }

        private void BtnRemoveConfig_Click(object sender, RoutedEventArgs e)
        {
            WindowConfig config = null;
            
            if (sender is Button { Tag: WindowConfig buttonConfig })
            {
                config = buttonConfig;
            }
            else if (sender is MenuItem { Tag: WindowConfig menuItemConfig })
            {
                config = menuItemConfig;
            }
            
            if (config != null)
            {
                // Save the config to the deleted stack before removing
                _deletedConfigs.Push(config);
                _windowConfigs.Remove(config);
                UpdateConfigurationOrder();
                
                // Show notification with undo option
                NotificationService.ShowInfo("Configuration removed. Click here to undo.", 5, UndoDelete);
            }
        }

        // New method to handle undo of deleted configurations
        private void UndoDelete()
        {
            if (_deletedConfigs.Count > 0)
            {
                var config = _deletedConfigs.Pop();
                _windowConfigs.Add(config);
                UpdateConfigurationOrder();
                lvWindowConfigs.SelectedIndex = _windowConfigs.Count - 1;
                NotificationService.ShowSuccess("Configuration restored.");
            }
        }
        
        // New method to restore configuration to original values
        private void RestoreOriginalConfig(WindowConfig config)
        {
            if (config.OriginalConfig != null)
            {
                config.ExePath = config.OriginalConfig.ExePath;
                config.X = config.OriginalConfig.X;
                config.Y = config.OriginalConfig.Y;
                config.Width = config.OriginalConfig.Width;
                config.Height = config.OriginalConfig.Height;
                config.EnableAlwaysOnTop = config.OriginalConfig.EnableAlwaysOnTop;
                config.EnableAlwaysOnTopMost = config.OriginalConfig.EnableAlwaysOnTopMost;
                config.EnableAlwaysOnBottom = config.OriginalConfig.EnableAlwaysOnBottom;
                config.EnableMouseThrough = config.OriginalConfig.EnableMouseThrough;
                
                lvWindowConfigs.Items.Refresh();
                NotificationService.ShowSuccess("Configuration restored to last saved state.");
            }
            else
            {
                NotificationService.ShowWarning("No saved configuration to restore.");
            }
        }
        
        // New method to clear Width and Height
        private void ClearWidthHeight(WindowConfig config)
        {
            config.Width = null;
            config.Height = null;
            lvWindowConfigs.Items.Refresh();
            NotificationService.ShowSuccess("Width and Height have been cleared.");
        }

        // Method to strip quotes from file path
        private string StripQuotesFromPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;
                
            return path.Trim('"');
        }
        
        // This method is called when a path textbox changes its value
        private void UpdatePathWithoutQuotes(TextBox textBox, WindowConfig config)
        {
            if (textBox != null && config != null)
            {
                var path = textBox.Text;
                var strippedPath = StripQuotesFromPath(path);
                
                if (path != strippedPath)
                {
                    textBox.Text = strippedPath;
                    config.ExePath = strippedPath;
                }
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

            // 按照Order属性排序配置，确保Order小的先启动（在底层），Order大的后启动（在上层）
            var sortedConfigs = selectedConfigs
                .OrderBy(config => config.Order ?? 0)
                .ToList();

            foreach (var config in sortedConfigs)
            {
                RunConfiguration(config);
                
                // 添加延迟，确保窗口Z轴顺序正确建立
                Thread.Sleep(300);
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

        private async void RunConfiguration(WindowConfig config)
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
                
                // 使用异步方法获取窗口句柄
                var (hwnd, windowTitle) = await GetWindowHandleAsync(process);
                
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
        
        private Task<(IntPtr hwnd, string windowTitle)> GetWindowHandleAsync(Process process)
        {
            return Task.Run(() => 
            {
                var hwnd = IntPtr.Zero;
                var windowTitle = string.Empty;
                
                for (var i = 0; i < 10; i++)
                {
                    Thread.Sleep(500);
                    try
                    {
                        process.Refresh();
                        hwnd = process.MainWindowHandle;

                        if (hwnd != IntPtr.Zero)
                        {
                            windowTitle = NativeMethods.GetWindowTitle(hwnd);
                            break;
                        }
                    }
                    catch (Exception)
                    {
                        // 忽略异常继续尝试
                    }
                }
                
                return (hwnd, windowTitle);
            });
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
                    
                    // 使用 NativeMethods 查询当前窗口位置和大小，用作比较和日志记录
                    var originalRect = new NativeMethods.RECT();
                    var currentSize = "Unknown";
                    var currentPosition = "Unknown";
                    
                    if (NativeMethods.GetWindowRect(config.BoundWindowHandle, ref originalRect))
                    {
                        var originalWidth = originalRect.Right - originalRect.Left;
                        var originalHeight = originalRect.Bottom - originalRect.Top;
                        currentSize = $"{originalWidth}x{originalHeight}";
                        currentPosition = $"({originalRect.Left}, {originalRect.Top})";
                    }
                    
                    var targetSize = config is { Width: not null, Height: not null }
                        ? $"{config.Width}x{config.Height}"
                        : "unchanged";
                        
                    var targetPosition = $"({config.X}, {config.Y})";
                    
                    // 显示当前和目标值，便于诊断
                    var message = $"Applying: Position {currentPosition} → {targetPosition}, Size {currentSize} → {targetSize}";
                    NotificationService.ShowInfo(message, 2);
                    
                    if (_windowManager.PositionWindow(config.BoundWindowHandle, config, config.BoundWindowTitle))
                    {
                        // 检查更新后的窗口位置和大小
                        if (NativeMethods.GetWindowRect(config.BoundWindowHandle, ref originalRect))
                        {
                            var newWidth = originalRect.Right - originalRect.Left;
                            var newHeight = originalRect.Bottom - originalRect.Top;
                            var newSize = $"{newWidth}x{newHeight}";
                            var newPosition = $"({originalRect.Left}, {originalRect.Top})";
                            
                            var resultMessage = $"Updated window: Position {newPosition}, Size {newSize}";
                            NotificationService.ShowSuccess(resultMessage);
                        }
                        else
                        {
                            NotificationService.ShowSuccess($"Updated position for window: {config.BoundWindowTitle}");
                        }
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
                    
                    // 处理Order值对Z轴顺序的影响
                    if (config.Order is > 0 && (config.EnableAlwaysOnTopMost || config.EnableAlwaysOnTop))
                    {
                        var insertAfter = config.EnableAlwaysOnTopMost ? NativeMethods.HWND_TOPMOST : NativeMethods.HWND_TOP;
                        
                        // Order值代表提升Z顺序的次数，每次提升都会将窗口移到同组窗口的顶部
                        // 为了使顺序更明显，我们使用Order值的2倍作为提升次数
                        for (int i = 0; i < config.Order.Value * 2; i++)
                        {
                            NativeMethods.SetWindowPos(hwnd, insertAfter, 0, 0, 0, 0, flags);
                            Thread.Sleep(10); // 短暂延迟，确保窗口系统能正确处理
                        }
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
            // Clear process monitoring data
            if (_processLastCheckTime.ContainsKey(config.BoundProcessId))
            {
                _processLastCheckTime.Remove(config.BoundProcessId);
            }
            
            if (_processLastCpuTime.ContainsKey(config.BoundProcessId))
            {
                _processLastCpuTime.Remove(config.BoundProcessId);
            }
            
            // Clear window binding information
            config.BoundWindowHandle = IntPtr.Zero;
            config.BoundProcessId = 0;
            config.BoundWindowTitle = string.Empty;
            config.ProcessDetails = null;
            
            // 不再需要刷新整个ListView
            // lvWindowConfigs.Items.Refresh();
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
                        
                        // If BindToProcessWhenSelectingWindow is enabled, also bind to the process
                        if (_appConfig.BindToProcessWhenSelectingWindow)
                        {
                            try
                            {
                                // Get process ID from window handle
                                int processId = 0;
                                NativeMethods.GetWindowThreadProcessId(args.WindowHandle, out processId);
                                
                                if (processId > 0)
                                {
                                    var process = Process.GetProcessById(processId);
                                    
                                    // Get the window title
                                    var windowTitle = NativeMethods.GetWindowTitle(args.WindowHandle);
                                    
                                    // Store window binding information
                                    config.BoundWindowHandle = args.WindowHandle;
                                    config.BoundProcessId = processId;
                                    config.BoundWindowTitle = windowTitle;
                                    
                                    NotificationService.ShowSuccess($"Window bound to process: {windowTitle}");
                                }
                            }
                            catch (Exception ex)
                            {
                                NotificationService.ShowError($"Error binding to process: {ex.Message}");
                            }
                        }
                        
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
            // 如果当前正在初始化，忽略所有设置变更事件
            if (_isInitializing)
                return;
                
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
                    var isChecked = checkBox.IsChecked ?? false;
                    _appConfig.RunAsAdmin = isChecked;
                    
                    // 获取当前是否以管理员身份运行
                    var isCurrentlyAdmin = IsRunningAsAdmin();
                    
                    // 如果当前状态与请求的状态不同，并且用户手动更改了此设置，询问是否重启
                    // 现在我们使用_isInitializing标志完全避免初始化时触发这段代码
                    if (isCurrentlyAdmin != isChecked)
                    {
                        var message = isChecked ? 
                            "The application needs to restart with administrator privileges to apply this setting. Restart now?" : 
                            "The application needs to restart with normal user privileges to apply this setting. Restart now?";
                        
                        var result = MessageBox.Show(
                            message, 
                            "Permission Change", 
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
                else if (checkBox == chkConfirmProcessKill)
                {
                    _appConfig.ConfirmProcessKill = checkBox.IsChecked ?? true;
                }
                else if (checkBox == chkBindToProcessWhenSelectingWindow)
                {
                    _appConfig.BindToProcessWhenSelectingWindow = checkBox.IsChecked ?? false;
                }
                else if (checkBox == chkShowBoundProcessDetails)
                {
                    _appConfig.ShowBoundProcessDetails = checkBox.IsChecked ?? true;
                    
                    // When this setting changes, we need to start or stop the process monitoring
                    if (_appConfig.ShowBoundProcessDetails)
                    {
                        StartProcessMonitoring();
                    }
                    else
                    {
                        StopProcessMonitoring();
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
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            
            // Check for valid drag data
            if (e.Data.GetDataPresent("WindowConfig") || e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = e.Data.GetDataPresent("WindowConfig") ? DragDropEffects.Move : DragDropEffects.Copy;
                
                // Highlight drop target
                if (sender is ListViewItem item)
                {
                    item.Background = new SolidColorBrush(Color.FromArgb(40, 100, 180, 255));
                }
                else if (sender is Grid grid)
                {
                    grid.Background = new SolidColorBrush(Color.FromArgb(40, 100, 180, 255));
                }
            }
        }
        
        private void ConfigItem_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            
            // Check for valid drag data
            if (e.Data.GetDataPresent("WindowConfig") || e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = e.Data.GetDataPresent("WindowConfig") ? DragDropEffects.Move : DragDropEffects.Copy;
                
                // Ensure target is highlighted
                if (sender is ListViewItem item && (item.Background is not SolidColorBrush || 
                                                   (item.Background is SolidColorBrush brush && brush.Color.A < 40)))
                {
                    item.Background = new SolidColorBrush(Color.FromArgb(40, 100, 180, 255));
                }
                else if (sender is Grid grid && grid.Background == Brushes.Transparent)
                {
                    grid.Background = new SolidColorBrush(Color.FromArgb(40, 100, 180, 255));
                }
            }
        }
        
        private void ConfigItem_DragLeave(object sender, DragEventArgs e)
        {
            // Reset visual feedback
            if (sender is ListViewItem listViewItem)
            {
                listViewItem.Background = new SolidColorBrush(Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF));
            }
            else if (sender is Grid grid)
            {
                grid.Background = Brushes.Transparent;
            }
            
            e.Handled = true;
        }
        
        private void ConfigItem_Drop(object sender, DragEventArgs e)
        {
            try
            {
                // Reset visual feedback
                if (sender is ListViewItem listViewItem)
                {
                    listViewItem.Background = new SolidColorBrush(Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF));
                }
                else if (sender is Grid grid)
                {
                    grid.Background = Brushes.Transparent;
                }
                
                // Handle configuration reordering
                if (e.Data.GetDataPresent("WindowConfig"))
                {
                    WindowConfig targetConfig = null;
                    WindowConfig sourceConfig = e.Data.GetData("WindowConfig") as WindowConfig;
                    
                    // Get target config based on the drop target
                    if (sender is FrameworkElement element)
                    {
                        // Try to get from Tag first
                        targetConfig = element.Tag as WindowConfig;
                        
                        // If Tag doesn't have the config, check if it's a ListViewItem with Content
                        if (targetConfig == null && sender is ListViewItem item)
                        {
                            targetConfig = item.Content as WindowConfig;
                        }
                    }
                    
                    // Make sure we have valid configs and they're different
                    if (sourceConfig != null && targetConfig != null && !ReferenceEquals(sourceConfig, targetConfig))
                    {
                        int sourceIndex = -1;
                        int targetIndex = -1;
                        
                        // Find the exact indices
                        for (int i = 0; i < _windowConfigs.Count; i++)
                        {
                            if (ReferenceEquals(_windowConfigs[i], sourceConfig))
                                sourceIndex = i;
                            else if (ReferenceEquals(_windowConfigs[i], targetConfig))
                                targetIndex = i;
                            
                            if (sourceIndex >= 0 && targetIndex >= 0)
                                break;
                        }
                        
                        // Log the indices for debugging
                        NotificationService.ShowInfo($"Moving config: {sourceIndex} → {targetIndex}", 1);
                        
                        if (sourceIndex >= 0 && targetIndex >= 0 && sourceIndex != targetIndex)
                        {
                            // Create a new snapshot of the collection to avoid modification issues
                            var configsList = _windowConfigs.ToList();
                            
                            // Remove source and insert at target position
                            configsList.RemoveAt(sourceIndex);
                            configsList.Insert(targetIndex, sourceConfig);
                            
                            // Clear and rebuild the observable collection
                            _windowConfigs.Clear();
                            foreach (var config in configsList)
                            {
                                _windowConfigs.Add(config);
                            }
                            
                            // Update order numbers
                            UpdateConfigurationOrder();
                            
                            // Refresh UI and select the moved item
                            lvWindowConfigs.Items.Refresh();
                            lvWindowConfigs.SelectedItem = sourceConfig;
                        }
                    }
                }
                // Handle file drop operation
                else if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    
                    if (files?.Length > 0)
                    {
                        var filePath = StripQuotesFromPath(files[0]);
                        WindowConfig targetConfig = null;
                        
                        // Find the target config
                        if (sender is FrameworkElement element)
                        {
                            targetConfig = element.Tag as WindowConfig;
                            
                            if (targetConfig == null && sender is ListViewItem item)
                            {
                                targetConfig = item.Content as WindowConfig;
                            }
                        }
                        
                        if (targetConfig != null)
                        {
                            // Update the path in the configuration
                            targetConfig.ExePath = filePath;
                            lvWindowConfigs.Items.Refresh();
                            NotificationService.ShowSuccess($"Path updated to: {Path.GetFileName(filePath)}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Drop error: {ex.Message}");
            }
            
            _isDragging = false;
            e.Handled = true;
        }
        
        private void ConfigItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Ignore clicks on interactive controls
            if (e.OriginalSource is TextBox || FindVisualParent<TextBox>(e.OriginalSource as DependencyObject) != null ||
                e.OriginalSource is Button || FindVisualParent<Button>(e.OriginalSource as DependencyObject) != null ||
                e.OriginalSource is CheckBox || FindVisualParent<CheckBox>(e.OriginalSource as DependencyObject) != null)
            {
                return;
            }
            
            // Get the ListViewItem and config
            ListViewItem listViewItem = null;
            WindowConfig config = null;
            
            if (sender is ListViewItem item)
            {
                listViewItem = item;
                config = item.Content as WindowConfig;
            }
            else
            {
                // Find the parent ListViewItem if the click was on a child element
                listViewItem = FindVisualParent<ListViewItem>(sender as DependencyObject);
                if (listViewItem != null)
                {
                    config = listViewItem.Content as WindowConfig;
                }
                else if (sender is FrameworkElement element)
                {
                    config = element.Tag as WindowConfig;
                }
            }
            
            // Select the item and start drag operation
            if (config != null)
            {
                // Select in the list view
                if (listViewItem != null)
                {
                    listViewItem.IsSelected = true;
                }
                lvWindowConfigs.SelectedItem = config;
                
                // Start drag-drop operation
                _draggedItem = config;
                _isDragging = true;
                
                // Create the drag data with a unique format name for our app
                DataObject dragData = new DataObject();
                dragData.SetData("WindowConfig", config);
                
                // Start drag-drop and handle the result
                DragDropEffects result = DragDrop.DoDragDrop(sender as DependencyObject, dragData, DragDropEffects.Move);
                
                // Reset drag state
                _isDragging = false;
                lvWindowConfigs.Items.Refresh();
            }
        }
        
        private T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            if (child == null) return null;
            
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            
            if (parentObject == null) return null;
            
            if (parentObject is T parent)
            {
                return parent;
            }
            
            return FindVisualParent<T>(parentObject);
        }
        
        #endregion

        #region Admin Privileges Handling
        
        private void RestartAsAdmin()
        {
            try
            {
                // 获取当前执行文件路径
                var exePath = Process.GetCurrentProcess().MainModule.FileName;
                
                // 创建启动信息
                var startInfo = new ProcessStartInfo
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
                NotificationService.ShowError($"Failed to restart as administrator: {ex.Message}");
            }
        }
        
        private void RestartAsNormalUser()
        {
            try
            {
                // 保存当前配置文件路径，确保同步更改保存
                SaveConfiguration();
                
                // 显示提示消息
                MessageBox.Show("The application will close. Please restart it manually with normal user privileges.", 
                    "Permission Downgrade", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // 正常关闭应用程序
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to exit application: {ex.Message}");
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
                var isAdmin = principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
                
                // 暂时禁用事件处理
                var oldInitializing = _isInitializing;
                _isInitializing = true;
                
                try
                {
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
                        chkRunAsAdmin.Content = "Run as Administrator (Currently Enabled)";
                        chkRunAsAdmin.Foreground = new SolidColorBrush(Colors.LightGreen);
                    }
                }
                finally
                {
                    // 恢复原始初始化状态
                    _isInitializing = oldInitializing;
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to check administrator status: {ex.Message}");
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
                NotificationService.ShowError($"Failed to check administrator status: {ex.Message}");
                return false;
            }
        }

        private void MenuRestoreConfig_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { Tag: WindowConfig config })
            {
                RestoreOriginalConfig(config);
            }
        }
        
        private void MenuClearWidthHeight_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { Tag: WindowConfig config })
            {
                ClearWidthHeight(config);
            }
        }

        private void TxtExePath_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox { Tag: WindowConfig config } textBox)
            {
                UpdatePathWithoutQuotes(textBox, config);
            }
        }

        private void MenuOpenInExplorer_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { Tag: WindowConfig config })
            {
                try
                {
                    var path = config.ExePath;
                    if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
                    {
                        NotificationService.ShowWarning("Invalid file path. Cannot open directory.");
                        return;
                    }
                    
                    var directory = System.IO.Path.GetDirectoryName(path);
                    if (System.IO.Directory.Exists(directory))
                    {
                        // Open Windows Explorer at the file's directory and select the file
                        Process.Start("explorer.exe", $"/select,\"{path}\"");
                    }
                    else
                    {
                        NotificationService.ShowError($"Directory not found: {directory}");
                    }
                }
                catch (Exception ex)
                {
                    NotificationService.ShowError($"Error opening explorer: {ex.Message}");
                }
            }
        }
        
        private void MenuDuplicateConfig_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { Tag: WindowConfig config })
            {
                // Create a deep copy of the configuration
                var duplicatedConfig = config.Clone();
                
                // Ensure it's seen as a new config (reset bound window info)
                duplicatedConfig.BoundWindowHandle = IntPtr.Zero;
                duplicatedConfig.BoundProcessId = 0;
                duplicatedConfig.BoundWindowTitle = string.Empty;
                
                // Add to collection
                _windowConfigs.Add(duplicatedConfig);
                
                // Update order of all configurations
                UpdateConfigurationOrder();
                
                // Refresh the UI
                lvWindowConfigs.Items.Refresh();
                
                // Select the new configuration
                lvWindowConfigs.SelectedIndex = _windowConfigs.Count - 1;
                
                NotificationService.ShowSuccess("Configuration duplicated successfully.");
            }
        }

        private void MenuOpenAppDirectory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get the directory where the application is running
                var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                
                if (System.IO.Directory.Exists(appDirectory))
                {
                    // Open Windows Explorer at the application directory
                    Process.Start("explorer.exe", appDirectory);
                    NotificationService.ShowSuccess("Application directory opened.");
                }
                else
                {
                    NotificationService.ShowError("Could not find the application directory.");
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Error opening application directory: {ex.Message}");
            }
        }

        private void BtnBindProcess_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: WindowConfig config })
            {
                // Show the process selector dialog
                var processSelector = new ProcessSelectorDialog();
                if (processSelector.ShowDialog() == true)
                {
                    var selectedProcess = processSelector.SelectedProcess;
                    if (selectedProcess != null)
                    {
                        try
                        {
                            // Get the main window handle of the process
                            var hwnd = selectedProcess.MainWindowHandle;
                            
                            if (hwnd == IntPtr.Zero)
                            {
                                NotificationService.ShowWarning("Selected process doesn't have a main window yet. Please wait for it to initialize or select another process.");
                                return;
                            }
                            
                            // Get the window title
                            var windowTitle = NativeMethods.GetWindowTitle(hwnd);
                            
                            // Get the executable path
                            var exePath = selectedProcess.MainModule?.FileName ?? "";
                            
                            if (string.IsNullOrEmpty(exePath))
                            {
                                NotificationService.ShowError("Could not determine executable path for the selected process.");
                                return;
                            }
                            
                            // Update the config
                            config.ExePath = exePath;
                            
                            // Get window position and size
                            var rect = new NativeMethods.RECT();
                            if (NativeMethods.GetWindowRect(hwnd, ref rect))
                            {
                                config.X = rect.Left;
                                config.Y = rect.Top;
                                
                                // If not keeping original size, store the current dimensions
                                if (!_appConfig.KeepOriginalSize)
                                {
                                    config.Width = rect.Right - rect.Left;
                                    config.Height = rect.Bottom - rect.Top;
                                }
                            }
                            
                            // Store window binding information
                            config.BoundWindowHandle = hwnd;
                            config.BoundProcessId = selectedProcess.Id;
                            config.BoundWindowTitle = windowTitle;
                            
                            // Refresh the list view to show bound window controls
                            lvWindowConfigs.Items.Refresh();
                            
                            NotificationService.ShowSuccess($"Successfully bound to process: {windowTitle}");
                        }
                        catch (Exception ex)
                        {
                            NotificationService.ShowError($"Error binding to process: {ex.Message}");
                        }
                    }
                }
            }
        }
        
        private void BtnKillProcess_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: WindowConfig config })
            {
                try
                {
                    // Check if the process is still running
                    if (config.BoundProcessId <= 0)
                    {
                        NotificationService.ShowWarning("No process is bound to this configuration.");
                        return;
                    }
                    
                    // Try to get the process
                    Process process = null;
                    try
                    {
                        process = Process.GetProcessById(config.BoundProcessId);
                    }
                    catch (ArgumentException)
                    {
                        NotificationService.ShowWarning("The process is no longer running.");
                        UnbindWindow(config);
                        return;
                    }
                    
                    // Check if we need to confirm
                    bool shouldKill = true;
                    if (_appConfig.ConfirmProcessKill)
                    {
                        var result = MessageBox.Show(
                            $"Are you sure you want to terminate the process '{config.BoundWindowTitle}'?",
                            "Confirm Process Termination",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);
                            
                        shouldKill = result == MessageBoxResult.Yes;
                    }
                    
                    if (shouldKill)
                    {
                        // Kill the process
                        process.Kill();
                        
                        // Wait a moment to ensure the process has been terminated
                        if (process.WaitForExit(1000))
                        {
                            NotificationService.ShowSuccess($"Process '{config.BoundWindowTitle}' has been terminated.");
                            
                            // Unbind the window since the process is gone
                            UnbindWindow(config);
                        }
                        else
                        {
                            NotificationService.ShowWarning("Process termination timed out. It might still be running.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    NotificationService.ShowError($"Error terminating process: {ex.Message}");
                }
            }
        }
        
        #region Process Monitoring

        private void StartProcessMonitoring()
        {
            // If timer already exists and is running, stop it first
            StopProcessMonitoring();
            
            // Create and start the timer
            _processMonitoringTimer = new DispatcherTimer();
            _processMonitoringTimer.Tick += ProcessMonitoringTimer_Tick;
            _processMonitoringTimer.Interval = TimeSpan.FromSeconds(1);
            _processMonitoringTimer.Start();
            
            // Update immediately
            UpdateProcessDetails();
        }
        
        private void StopProcessMonitoring()
        {
            if (_processMonitoringTimer != null)
            {
                _processMonitoringTimer.Stop();
                _processMonitoringTimer = null;
            }
            
            // Clear any existing process details
            foreach (var config in _windowConfigs)
            {
                config.ProcessDetails = null;
            }
            
            // Clear tracking dictionaries
            _processLastCheckTime.Clear();
            _processLastCpuTime.Clear();
            
            // 不再需要刷新整个列表，因为使用了INotifyPropertyChanged
            // lvWindowConfigs.Items.Refresh();
        }
        
        private void ProcessMonitoringTimer_Tick(object sender, EventArgs e)
        {
            UpdateProcessDetails();
        }
        
        private void UpdateProcessDetails()
        {
            // Get all bound configs
            var boundConfigs = _windowConfigs.Where(c => c.IsBound).ToList();
            
            if (boundConfigs.Count == 0)
            {
                return; // No bound processes to monitor
            }
            
            foreach (var config in boundConfigs)
            {
                try
                {
                    // Check if the process still exists
                    Process process = null;
                    try
                    {
                        process = Process.GetProcessById(config.BoundProcessId);
                    }
                    catch (ArgumentException)
                    {
                        // Process no longer exists
                        UnbindWindow(config);
                        continue;
                    }
                    
                    // Check if the window is still valid
                    if (!IsWindowHandleValid(config.BoundWindowHandle))
                    {
                        UnbindWindow(config);
                        continue;
                    }
                    
                    // Create or get the process details object
                    var details = config.ProcessDetails ?? new ProcessDetails();
                    
                    // Update identification info
                    details.ProcessId = config.BoundProcessId;
                    details.ProcessName = process.ProcessName;
                    details.WindowTitle = config.BoundWindowTitle;
                    
                    // Get window position and size
                    var rect = new NativeMethods.RECT();
                    if (NativeMethods.GetWindowRect(config.BoundWindowHandle, ref rect))
                    {
                        details.X = rect.Left;
                        details.Y = rect.Top;
                        details.Width = rect.Right - rect.Left;
                        details.Height = rect.Bottom - rect.Top;
                    }
                    
                    // Update memory usage
                    details.MemoryUsageBytes = process.WorkingSet64;
                    
                    // Calculate CPU usage
                    UpdateCpuUsage(process, details);
                    
                    // 只有在首次分配ProcessDetails时才需要指定给config
                    if (config.ProcessDetails == null)
                    {
                        config.ProcessDetails = details;
                    }
                }
                catch (Exception ex)
                {
                    // Just skip this process if we can't get details
                }
            }
            
            // 不再需要刷新整个ListView
            // lvWindowConfigs.Items.Refresh();
        }
        
        private void UpdateCpuUsage(Process process, ProcessDetails details)
        {
            try
            {
                int processId = process.Id;
                DateTime currentTime = DateTime.Now;
                TimeSpan currentTotalProcessorTime = process.TotalProcessorTime;
                
                // If we have previous measurements for this process
                if (_processLastCheckTime.ContainsKey(processId) && _processLastCpuTime.ContainsKey(processId))
                {
                    DateTime lastTime = _processLastCheckTime[processId];
                    TimeSpan lastTotalProcessorTime = _processLastCpuTime[processId];
                    
                    // Calculate time difference
                    TimeSpan timeDifference = currentTime - lastTime;
                    double elapsedSeconds = timeDifference.TotalSeconds;
                    
                    // Calculate CPU time difference
                    TimeSpan cpuDifference = currentTotalProcessorTime - lastTotalProcessorTime;
                    double cpuUsageTotal = cpuDifference.TotalSeconds;
                    
                    // Calculate CPU percentage (adjust for multi-core processors)
                    double cpuUsagePercentage = (cpuUsageTotal / elapsedSeconds) * 100.0;
                    
                    // Adjust for multi-core systems
                    int processorCount = Environment.ProcessorCount;
                    cpuUsagePercentage /= processorCount;
                    
                    // Cap at 100% to avoid values over 100%
                    cpuUsagePercentage = Math.Min(100.0, cpuUsagePercentage);
                    
                    // Update the details
                    details.CpuUsage = cpuUsagePercentage;
                }
                else
                {
                    // First-time measurement for this process
                    details.CpuUsage = 0;
                }
                
                // Store current values for next calculation
                _processLastCheckTime[processId] = currentTime;
                _processLastCpuTime[processId] = currentTotalProcessorTime;
            }
            catch (Exception)
            {
                // If we can't get processor time, just set to 0
                details.CpuUsage = 0;
            }
        }
        
        #endregion
    }
}
