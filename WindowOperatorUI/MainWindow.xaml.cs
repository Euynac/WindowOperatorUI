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

        public MainWindow()
        {
            InitializeComponent();
            LoadConfiguration();
            lvWindowConfigs.ItemsSource = _windowConfigs;
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
                    NotificationService.ShowError($"The executable file does not exist: {config.ExePath}");
                    return;
                }

                var windowManager = new WindowManager(new ConsoleLogger());
                
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

                if (windowManager.PositionWindow(hwnd, config, windowTitle))
                {
                    NotificationService.ShowSuccess($"Window '{windowTitle}' positioned successfully.");
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
                    _appConfig.RunAsAdmin = checkBox.IsChecked ?? false;
                }
            }
            
            // 不需要立即保存，用户将在准备好时保存
        }
        
        private void LvWindowConfigs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // You can add additional logic here if needed
        }
        
        #endregion
    }

  
}