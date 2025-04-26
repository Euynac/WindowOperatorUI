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
            
            // Add loaded event handler for acrylic effect
            this.Loaded += MainWindow_Loaded;
            
            _isInitializing = false; // 初始化完成后关闭标志
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Apply acrylic effect to window with dark tint
            WindowBackdrop.ApplyAcrylicEffect(this, 0x99202020);
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
            var orderIndex = 1;
            foreach (var config in _windowConfigs)
            {
                config.Order = orderIndex++;
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
            // Clear all default drag/drop target effects
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            
            // Apply visual feedback based on the sender type
            if (sender is ListViewItem item)
            {
                if (e.Data.GetDataPresent("WindowConfig") || e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    e.Effects = e.Data.GetDataPresent("WindowConfig") ? DragDropEffects.Move : DragDropEffects.Copy;
                    item.Background = new SolidColorBrush(Color.FromArgb(40, 100, 180, 255));
                }
            }
            else if (sender is Grid grid)
            {
                if (e.Data.GetDataPresent("WindowConfig") || e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    e.Effects = e.Data.GetDataPresent("WindowConfig") ? DragDropEffects.Move : DragDropEffects.Copy;
                    grid.Background = new SolidColorBrush(Color.FromArgb(40, 100, 180, 255));
                }
            }
        }
        
        private void ConfigItem_DragOver(object sender, DragEventArgs e)
        {
            // Clear all default drag/drop target effects
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            
            // Apply visual feedback based on the sender type
            if (sender is ListViewItem item)
            {
                if (e.Data.GetDataPresent("WindowConfig") || e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    e.Effects = e.Data.GetDataPresent("WindowConfig") ? DragDropEffects.Move : DragDropEffects.Copy;
                    
                    if (item.Background.Opacity < 0.1)
                    {
                        item.Background = new SolidColorBrush(Color.FromArgb(40, 100, 180, 255));
                    }
                }
            }
            else if (sender is Grid grid)
            {
                if (e.Data.GetDataPresent("WindowConfig") || e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    e.Effects = e.Data.GetDataPresent("WindowConfig") ? DragDropEffects.Move : DragDropEffects.Copy;
                    
                    if (grid.Background == Brushes.Transparent)
                    {
                        grid.Background = new SolidColorBrush(Color.FromArgb(40, 100, 180, 255));
                    }
                }
            }
        }
        
        private void ConfigItem_DragLeave(object sender, DragEventArgs e)
        {
            // Reset visual feedback
            if (sender is ListViewItem item)
            {
                item.Background = new SolidColorBrush(Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF));
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
                if (sender is FrameworkElement element)
                {
                    if (element is ListViewItem listViewItem)
                    {
                        listViewItem.Background = new SolidColorBrush(Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF));
                    }
                    else if (element is Grid grid)
                    {
                        grid.Background = Brushes.Transparent;
                    }
                }
                
                WindowConfig targetConfig = null;
                
                // From Tag or Content property
                if (sender is FrameworkElement senderElement)
                {
                    targetConfig = senderElement.Tag as WindowConfig;
                    
                    // If it's a ListViewItem, try to get the config from the Content
                    if (targetConfig == null && sender is ListViewItem item)
                    {
                        targetConfig = item.Content as WindowConfig;
                    }
                }
                
                if (targetConfig == null) return;
                
                // Check if the dragging operation is within the window configuration list
                if (e.Data.GetDataPresent("WindowConfig"))
                {
                    var draggedConfig = e.Data.GetData("WindowConfig") as WindowConfig;
                    if (draggedConfig != null && !ReferenceEquals(draggedConfig, targetConfig))
                    {
                        var draggedIndex = _windowConfigs.IndexOf(draggedConfig);
                        var targetIndex = _windowConfigs.IndexOf(targetConfig);
                        
                        if (draggedIndex >= 0 && targetIndex >= 0)
                        {
                            _windowConfigs.RemoveAt(draggedIndex);
                            
                            // If the target index is greater than the dragged index, we need to subtract 1
                            if (targetIndex > draggedIndex)
                            {
                                targetIndex--;
                            }
                            
                            _windowConfigs.Insert(targetIndex, draggedConfig);
                            
                            // Update the order of all configurations
                            UpdateConfigurationOrder();
                            lvWindowConfigs.Items.Refresh();
                        }
                    }
                }
                // Check if it's a file drop operation
                else if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    // Get the dropped files
                    string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    
                    // If there are multiple files, just take the first one
                    if (files is { Length: > 0 })
                    {
                        var filePath = StripQuotesFromPath(files[0]);
                        
                        // Update the path in the configuration
                        targetConfig.ExePath = filePath;
                        
                        // Refresh the ListView to show the updated path
                        lvWindowConfigs.Items.Refresh();
                        
                        // Show a success notification
                        NotificationService.ShowSuccess($"Path updated to: {Path.GetFileName(filePath)}");
                    }
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Error during drop operation: {ex.Message}");
            }
            
            e.Handled = true;
        }
        
        // New method to handle dragging of config items
        private void ConfigItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Skip if the click target is a text box, button, or checkbox
            if (e.OriginalSource is TextBox || 
                FindVisualParent<TextBox>(e.OriginalSource as DependencyObject) != null ||
                e.OriginalSource is Button || 
                FindVisualParent<Button>(e.OriginalSource as DependencyObject) != null ||
                e.OriginalSource is CheckBox || 
                FindVisualParent<CheckBox>(e.OriginalSource as DependencyObject) != null)
            {
                return;
            }

            // Get the window config from the Tag property
            WindowConfig config = null;
            
            if (sender is FrameworkElement element)
            {
                config = element.Tag as WindowConfig;
            }
            
            if (config == null && sender is ListViewItem listViewItem)
            {
                config = listViewItem.Content as WindowConfig;
            }
            
            if (config != null)
            {
                _draggedItem = config;
                
                // Set up the drag & drop operation
                var dragData = new DataObject("WindowConfig", config);
                DragDrop.DoDragDrop(sender as DependencyObject, dragData, DragDropEffects.Move);
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
                        chkRunAsAdmin.Content = "以管理员身份运行 (当前已启用)";
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
    }
}
