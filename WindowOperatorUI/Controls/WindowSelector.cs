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
        private IntPtr _hwndSource;
        private bool _isTransparentToInput = false; // 控制是否启用点击穿透

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
                Text = "点击并拖拽以选择窗口。\n按ESC取消选择。",
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
            
            // 增加在窗口加载时获取窗口句柄
            Loaded += WindowSelector_Loaded;
        }
        
        private void WindowSelector_Loaded(object sender, RoutedEventArgs e)
        {
            // 获取窗口句柄
            var hwndSource = (HwndSource)PresentationSource.FromVisual(this);
            _hwndSource = hwndSource.Handle;
        }

        // 设置点击穿透状态
        private void SetInputTransparent(bool transparent)
        {
            if (_hwndSource == IntPtr.Zero) return;
            
            if (transparent == _isTransparentToInput) return; // 已经是目标状态
            
            var extendedStyle = NativeMethods.GetWindowLong(_hwndSource, NativeMethods.GWL_EXSTYLE);
            
            if (transparent)
            {
                // 添加透明标志
                NativeMethods.SetWindowLong(
                    _hwndSource,
                    NativeMethods.GWL_EXSTYLE,
                    extendedStyle | NativeMethods.WS_EX_TRANSPARENT);
            }
            else
            {
                // 移除透明标志
                NativeMethods.SetWindowLong(
                    _hwndSource,
                    NativeMethods.GWL_EXSTYLE,
                    extendedStyle & ~NativeMethods.WS_EX_TRANSPARENT);
            }
            
            _isTransparentToInput = transparent;
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
            // 禁用点击穿透以便能接收鼠标事件
            SetInputTransparent(false);
            
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
                
                // 启用点击穿透以便能选择下面的窗口
                SetInputTransparent(true);
                
                // 获取鼠标位置下的窗口
                _endPoint = e.GetPosition(this);
                var point = GetScreenPoint(_endPoint);
                
                // 延迟一点以确保点击穿透生效
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                timer.Tick += (s, args) =>
                {
                    timer.Stop();
                    
                    // 获取窗口句柄
                    var hwnd = GetWindowBelowPoint(point);
                    
                    if (hwnd != IntPtr.Zero && hwnd != _hwndSource)
                    {
                        // 获取窗口信息
                        try
                        {
                            var processId = 0;
                            NativeMethods.GetWindowThreadProcessId(hwnd, out processId);
                            
                            var process = Process.GetProcessById(processId);
                            var executablePath = process.MainModule?.FileName ?? "";
                            
                            var rect = new NativeMethods.RECT();
                            NativeMethods.GetWindowRect(hwnd, ref rect);
                            
                            // 触发事件
                            WindowSelected?.Invoke(this, new WindowSelectedEventArgs
                            {
                                WindowHandle = hwnd,
                                ExecutablePath = executablePath,
                                X = rect.Left,
                                Y = rect.Top,
                                Width = rect.Right - rect.Left,
                                Height = rect.Bottom - rect.Top
                            });
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"获取窗口信息时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            WindowSelected?.Invoke(this, new WindowSelectedEventArgs());
                        }
                    }
                    else
                    {
                        // 没有选择窗口或选择了自己
                        WindowSelected?.Invoke(this, new WindowSelectedEventArgs());
                    }
                    
                    Close();
                };
                timer.Start();
            }
        }

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            // 这个方法可以用来在鼠标移动时高亮显示光标下的窗口
        }
        
        private NativeMethods.POINT GetScreenPoint(Point point)
        {
            // 转换为屏幕坐标
            var screenPoint = PointToScreen(point);
            
            // 转换为系统坐标（通常以像素为单位）
            return new NativeMethods.POINT
            {
                X = (int)screenPoint.X,
                Y = (int)screenPoint.Y
            };
        }
        
        // 获取指定点位置下方的窗口（忽略自己的窗口）
        private IntPtr GetWindowBelowPoint(NativeMethods.POINT point)
        {
            // 这个方法尝试获取指定点下方的窗口
            // 我们需要临时隐藏自己的窗口来获取下方的窗口
            
            try
            {
                // 临时隐藏自己
                NativeMethods.ShowWindow(_hwndSource, NativeMethods.SW_HIDE);
                
                // 获取下方的窗口
                var hwndBelow = NativeMethods.WindowFromPoint(point);
                
                return hwndBelow;
            }
            finally
            {
                // 确保自己的窗口被重新显示
                NativeMethods.ShowWindow(_hwndSource, NativeMethods.SW_SHOW);
            }
        }
    }
} 