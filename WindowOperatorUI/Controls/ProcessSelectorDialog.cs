using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace WindowOperatorUI.Controls
{
    public class ProcessSelectorDialog : Window
    {
        private ListBox processList;
        private TextBox searchBox;
        private Button refreshButton;
        private Button selectButton;
        private Button cancelButton;
        
        private List<Process> _processes;
        
        public Process SelectedProcess { get; private set; }
        
        public ProcessSelectorDialog()
        {
            Title = "Select Process";
            Width = 500;
            Height = 500;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(32, 32, 32));
            Foreground = Brushes.White;
            
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(50) });
            
            // Search panel
            var searchPanel = new DockPanel { LastChildFill = true };
            searchBox = new TextBox
            {
                Margin = new Thickness(10, 8, 5, 8),
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromRgb(50, 50, 50)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 70)),
                Padding = new Thickness(5),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            searchBox.TextChanged += SearchBox_TextChanged;
            
            refreshButton = new Button
            {
                Content = "Refresh",
                Margin = new Thickness(5, 8, 10, 8),
                Padding = new Thickness(10, 0, 10, 0),
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromRgb(0, 120, 215))
            };
            refreshButton.Click += RefreshButton_Click;
            
            DockPanel.SetDock(refreshButton, Dock.Right);
            searchPanel.Children.Add(refreshButton);
            searchPanel.Children.Add(searchBox);
            
            Grid.SetRow(searchPanel, 0);
            grid.Children.Add(searchPanel);
            
            // Process list
            processList = new ListBox
            {
                Margin = new Thickness(10),
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 70))
            };
            processList.SelectionChanged += ProcessList_SelectionChanged;
            processList.MouseDoubleClick += ProcessList_MouseDoubleClick;
            
            Grid.SetRow(processList, 1);
            grid.Children.Add(processList);
            
            // Button panel
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10)
            };
            
            selectButton = new Button
            {
                Content = "Select",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
                IsEnabled = false
            };
            selectButton.Click += SelectButton_Click;
            
            cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 30,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromRgb(80, 80, 80))
            };
            cancelButton.Click += CancelButton_Click;
            
            buttonPanel.Children.Add(selectButton);
            buttonPanel.Children.Add(cancelButton);
            
            Grid.SetRow(buttonPanel, 2);
            grid.Children.Add(buttonPanel);
            
            Content = grid;
            
            Loaded += ProcessSelectorDialog_Loaded;
        }

        private void ProcessSelectorDialog_Loaded(object sender, RoutedEventArgs e)
        {
            Task.Run(() => LoadProcesses());
        }

        private void ProcessList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectButton.IsEnabled = processList.SelectedItem != null;
        }

        private void ProcessList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (processList.SelectedItem != null)
            {
                SelectButton_Click(sender, e);
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterProcesses();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() => LoadProcesses());
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            if (processList.SelectedItem is ProcessItem selectedItem)
            {
                try
                {
                    SelectedProcess = Process.GetProcessById(selectedItem.Id);
                    DialogResult = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error selecting process: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void LoadProcesses()
        {
            try
            {
                var processes = Process.GetProcesses()
                    .Where(p => p.MainWindowHandle != IntPtr.Zero) // Only get processes with a main window
                    .OrderBy(p => p.ProcessName)
                    .ToList();
                
                _processes = processes;
                
                Dispatcher.Invoke(() =>
                {
                    UpdateProcessList(processes);
                    searchBox.IsEnabled = true;
                    refreshButton.IsEnabled = true;
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Error loading processes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private void UpdateProcessList(List<Process> processes)
        {
            processList.Items.Clear();
            
            foreach (var process in processes)
            {
                try
                {
                    processList.Items.Add(new ProcessItem
                    {
                        Id = process.Id,
                        Name = process.ProcessName,
                        Title = process.MainWindowTitle,
                        Memory = FormatMemorySize(process.WorkingSet64)
                    });
                }
                catch
                {
                    // Skip processes that we can't access
                }
            }
        }

        private void FilterProcesses()
        {
            if (_processes == null) return;
            
            var searchText = searchBox.Text.ToLower();
            
            if (string.IsNullOrWhiteSpace(searchText))
            {
                UpdateProcessList(_processes);
                return;
            }
            
            var filteredProcesses = _processes
                .Where(p => 
                    p.ProcessName.ToLower().Contains(searchText) || 
                    p.MainWindowTitle.ToLower().Contains(searchText))
                .ToList();
            
            UpdateProcessList(filteredProcesses);
        }

        private string FormatMemorySize(long bytes)
        {
            string[] sizeSuffixes = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            double size = bytes;
            
            while (size >= 1024 && i < sizeSuffixes.Length - 1)
            {
                size /= 1024;
                i++;
            }
            
            return $"{size:0.#} {sizeSuffixes[i]}";
        }

        private class ProcessItem
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Title { get; set; }
            public string Memory { get; set; }
            
            public override string ToString()
            {
                string title = !string.IsNullOrEmpty(Title) ? $" - {Title}" : "";
                return $"{Name} (PID: {Id}){title} - {Memory}";
            }
        }
    }
} 