using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WindowOperatorUI.Models;
using WindowOperatorUI.Utils;

namespace WindowOperatorUI.Controls
{
    public class WindowSelector : Window
    {
        public event EventHandler<WindowSelectedEventArgs> WindowSelected;
        private DispatcherTimer _refreshTimer;
        private Point _startPoint;
        private Point _endPoint;
        private bool _isSelecting = false;
        private IntPtr _selectedWindowHandle = IntPtr.Zero;

        public WindowSelector()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            WindowState = WindowState.Maximized;
            ShowInTaskbar = false;
            Cursor = Cursors.Cross;

            var grid = new Grid();
            grid.Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
            
            // Add instructions overlay
            var instructionText = new TextBlock
            {
                Text = "Click and drag to select a window.\nPress ESC to cancel.",
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)),
                Padding = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 50, 0, 0),
                FontSize = 16
            };
            
            grid.Children.Add(instructionText);
            Content = grid;
            
            KeyDown += WindowSelector_KeyDown;
            MouseLeftButtonDown += WindowSelector_MouseLeftButtonDown;
            MouseLeftButtonUp += WindowSelector_MouseLeftButtonUp;
            MouseMove += WindowSelector_MouseMove;

            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromMilliseconds(50);
            _refreshTimer.Tick += RefreshTimer_Tick;
        }

        private void WindowSelector_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                WindowSelected?.Invoke(this, new WindowSelectedEventArgs());
                Close();
            }
        }

        private void WindowSelector_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(this);
            _isSelecting = true;
            _refreshTimer.Start();
        }

        private void WindowSelector_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isSelecting)
            {
                _endPoint = e.GetPosition(this);
                
                // Draw selection rectangle
                var grid = Content as Grid;
                if (grid != null)
                {
                    // Remove any existing selection rectangle
                    for (var i = grid.Children.Count - 1; i >= 0; i--)
                    {
                        if (grid.Children[i] is Border { Name: "SelectionRectangle" })
                        {
                            grid.Children.RemoveAt(i);
                            break;
                        }
                    }
                    
                    // Create new rectangle
                    var left = Math.Min(_startPoint.X, _endPoint.X);
                    var top = Math.Min(_startPoint.Y, _endPoint.Y);
                    var width = Math.Abs(_endPoint.X - _startPoint.X);
                    var height = Math.Abs(_endPoint.Y - _startPoint.Y);
                    
                    var selectionBorder = new Border
                    {
                        Name = "SelectionRectangle",
                        BorderBrush = Brushes.LightBlue,
                        BorderThickness = new Thickness(2),
                        Background = new SolidColorBrush(Color.FromArgb(30, 173, 216, 230)),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(left, top, 0, 0),
                        Width = width,
                        Height = height
                    };
                    
                    grid.Children.Add(selectionBorder);
                }
            }
        }

        private void WindowSelector_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isSelecting)
            {
                _isSelecting = false;
                _refreshTimer.Stop();
                
                // Get the window under the cursor
                _selectedWindowHandle = GetWindowHandleAtPosition(_endPoint);
                
                if (_selectedWindowHandle != IntPtr.Zero)
                {
                    // Get window info
                    var processId = 0;
                    NativeMethods.GetWindowThreadProcessId(_selectedWindowHandle, out processId);
                    
                    var process = Process.GetProcessById(processId);
                    var executablePath = process.MainModule?.FileName ?? "";
                    
                    var rect = new NativeMethods.RECT();
                    NativeMethods.GetWindowRect(_selectedWindowHandle, ref rect);
                    
                    // Trigger event
                    WindowSelected?.Invoke(this, new WindowSelectedEventArgs
                    {
                        WindowHandle = _selectedWindowHandle,
                        ExecutablePath = executablePath,
                        X = rect.Left,
                        Y = rect.Top,
                        Width = rect.Right - rect.Left,
                        Height = rect.Bottom - rect.Top
                    });
                }
                else
                {
                    // No window selected
                    WindowSelected?.Invoke(this, new WindowSelectedEventArgs());
                }
                
                Close();
            }
        }

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            // This method could be used to highlight the window under the cursor
            // during selection if more advanced feedback is desired
        }

        private IntPtr GetWindowHandleAtPosition(Point point)
        {
            // Convert the point to screen coordinates
            var source = PresentationSource.FromVisual(this) as HwndSource;
            if (source == null) return IntPtr.Zero;
            
            var transform = source.CompositionTarget.TransformToDevice;
            var screenPoint = transform.Transform(point);
            
            // Convert to system coordinates (which are typically in pixels)
            var systemPoint = new NativeMethods.POINT
            {
                X = (int)screenPoint.X,
                Y = (int)screenPoint.Y
            };
            
            // Get the window handle at the specified point
            return NativeMethods.WindowFromPoint(systemPoint);
        }
    }
} 