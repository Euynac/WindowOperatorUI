using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowOperatorUI.Models;
using WindowOperatorUI.Utils;

namespace WindowOperatorUI.Controls
{
    public class WindowSelector : Window
    {
        public event EventHandler<WindowSelectedEventArgs> WindowSelected;
        private DispatcherTimer _highlightTimer;
        private IntPtr _hwndSource;
        private bool _isTransparentToInput = false; // 控制是否启用点击穿透
        private IntPtr _currentHighlightedWindow = IntPtr.Zero;
        private Border _highlightBorder;

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
            
            // 添加指示边框（初始不可见）
            _highlightBorder = new Border
            {
                BorderBrush = new SolidColorBrush(Colors.Red),
                BorderThickness = new Thickness(2),
                Background = new SolidColorBrush(Color.FromArgb(20, 255, 0, 0)), // 半透明红色
                Visibility = Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            grid.Children.Add(_highlightBorder);
            
            // 添加说明文本
            var instructionText = new TextBlock
            {
                Text = "移动鼠标选择窗口，点击鼠标左键确认选择。\n按ESC取消选择。",
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
            MouseMove += WindowSelector_MouseMove;

            // 设置高亮定时器
            _highlightTimer = new DispatcherTimer();
            _highlightTimer.Interval = TimeSpan.FromMilliseconds(100);
            _highlightTimer.Tick += HighlightTimer_Tick;
            
            // 窗口加载时获取窗口句柄
            Loaded += WindowSelector_Loaded;
        }
        
        private void WindowSelector_Loaded(object sender, RoutedEventArgs e)
        {
            // 获取窗口句柄
            var hwndSource = (HwndSource)PresentationSource.FromVisual(this);
            _hwndSource = hwndSource.Handle;
            
            // 启动高亮定时器
            _highlightTimer.Start();
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
                _highlightTimer.Stop();
                WindowSelected?.Invoke(this, new WindowSelectedEventArgs());
                Close();
            }
        }

        private void WindowSelector_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 禁用点击穿透以便能接收鼠标事件
            SetInputTransparent(false);
            
            // 停止高亮定时器
            _highlightTimer.Stop();
            
            // 获取鼠标位置
            var point = GetScreenPoint(e.GetPosition(this));
            
            // 启用点击穿透以便能选择下面的窗口
            SetInputTransparent(true);
            
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

        private void WindowSelector_MouseMove(object sender, MouseEventArgs e)
        {
            // 鼠标移动时更新当前位置，高亮定时器会处理窗口高亮
        }

        private void HighlightTimer_Tick(object sender, EventArgs e)
        {
            // 获取鼠标位置
            var mousePos = GetMousePosition();
            var point = new NativeMethods.POINT { X = (int)mousePos.X, Y = (int)mousePos.Y };
            
            // 临时设置为点击穿透以获取下方窗口
            SetInputTransparent(true);
            
            try
            {
                // 获取鼠标下方的窗口
                var hwnd = NativeMethods.WindowFromPoint(point);
                
                // 如果是自己，尝试获取下一层窗口
                if (hwnd == _hwndSource)
                {
                    hwnd = GetWindowBelowPoint(point);
                }
                
                // 如果窗口变化了，更新高亮框
                if (hwnd != _currentHighlightedWindow && hwnd != IntPtr.Zero && hwnd != _hwndSource)
                {
                    _currentHighlightedWindow = hwnd;
                    
                    // 获取窗口区域
                    var rect = new NativeMethods.RECT();
                    if (NativeMethods.GetWindowRect(hwnd, ref rect))
                    {
                        // 计算相对于屏幕的位置
                        var left = rect.Left;
                        var top = rect.Top;
                        var width = rect.Right - rect.Left;
                        var height = rect.Bottom - rect.Top;
                        
                        // 转换为相对于我们窗口的位置
                        var windowPos = PointFromScreen(new Point(left, top));
                        
                        // 更新高亮边框
                        _highlightBorder.Margin = new Thickness(windowPos.X, windowPos.Y, 0, 0);
                        _highlightBorder.Width = width;
                        _highlightBorder.Height = height;
                        _highlightBorder.Visibility = Visibility.Visible;
                    }
                }
                else if (hwnd == IntPtr.Zero || hwnd == _hwndSource)
                {
                    // 如果没有找到窗口或是自己，隐藏高亮框
                    _highlightBorder.Visibility = Visibility.Collapsed;
                    _currentHighlightedWindow = IntPtr.Zero;
                }
            }
            finally
            {
                // 恢复非点击穿透状态以便能接收鼠标事件
                SetInputTransparent(false);
            }
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
        
        private Point GetMousePosition()
        {
            // 获取鼠标在屏幕上的位置
            NativeMethods.POINT point = new NativeMethods.POINT();
            NativeMethods.GetCursorPos(ref point);
            return new Point(point.X, point.Y);
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