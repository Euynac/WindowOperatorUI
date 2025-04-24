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
                MessageBox.Show($"Error loading configuration: {ex.Message}", "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                
                // Update window configurations
                _appConfig.Windows = _windowConfigs.ToList();
                
                // Save to file
                var json = JsonSerializer.Serialize(_appConfig, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving configuration: {ex.Message}", "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSaveConfig_Click(object sender, RoutedEventArgs e)
        {
            SaveConfiguration();
            MessageBox.Show("Configuration saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
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
                MessageBox.Show("No configurations selected. Please select at least one configuration to run.", 
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    MessageBox.Show($"The executable file does not exist: {config.ExePath}", 
                        "File Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show($"Could not get window handle for: {Path.GetFileName(config.ExePath)}", 
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (windowManager.PositionWindow(hwnd, config, windowTitle))
                {
                    MessageBox.Show($"Window '{windowTitle}' positioned successfully.", 
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Failed to position window: {windowTitle}", 
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error running configuration: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                _windowSelector = new WindowSelector();
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
            // No need to save immediately, user will save when ready
        }
        
        private void LvWindowConfigs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // You can add additional logic here if needed
        }
        
        #endregion
    }

  
}