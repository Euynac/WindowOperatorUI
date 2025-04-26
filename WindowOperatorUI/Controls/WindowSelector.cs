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
using WindowOperatorUI.Services;
using WindowOperatorUI.Utils;

namespace WindowOperatorUI.Controls
{
    public class WindowSelector : Window
    {
        public event EventHandler<WindowSelectedEventArgs> WindowSelected;
        public event EventHandler<bool> KeepOriginalSizeChanged;
        private DispatcherTimer _highlightTimer;
        private IntPtr _hwndSource;
        private bool _isTransparentToInput = false; // 控制是否启用点击穿透
        private IntPtr _currentHighlightedWindow = IntPtr.Zero;
        private Border _highlightBorder;
        private TextBlock _infoTextBlock;
        private PresentationSource _presentationSource;
        private CheckBox _keepOriginalSizeCheckBox;

        public bool KeepOriginalSize
        {
            get => _keepOriginalSizeCheckBox?.IsChecked ?? true;
            set
            {
                if (_keepOriginalSizeCheckBox != null)
                {
                    _keepOriginalSizeCheckBox.IsChecked = value;
                }
            }
        }

        public WindowSelector(bool keepOriginalSize = true)
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
            
            // 添加指示边框
            _highlightBorder = new Border
            {
                BorderBrush = new SolidColorBrush(Colors.Red),
                BorderThickness = new Thickness(5),
                Background = new SolidColorBrush(Color.FromArgb(5, 255, 0, 0)),
                Visibility = Visibility.Hidden,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            grid.Children.Add(_highlightBorder);
            
            // 添加窗口信息显示文本
            _infoTextBlock = new TextBlock
            {
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
                Padding = new Thickness(5),
                Margin = new Thickness(10, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                FontSize = 12,
                Text = "Move mouse over a window to display information"
            };
            grid.Children.Add(_infoTextBlock);
            
            // 添加控制面板(使用Border包裹StackPanel来添加Padding)
            var optionsBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
                Padding = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(10, 0, 0, 10)
            };
            
            var optionsPanel = new StackPanel
            {
                Orientation = Orientation.Vertical
            };
            
            // 保持原始大小选项
            _keepOriginalSizeCheckBox = new CheckBox
            {
                Content = "Keep original window size",
                Foreground = Brushes.White,
                IsChecked = keepOriginalSize,
                Margin = new Thickness(0, 0, 0, 5)
            };
            
            // 添加复选框改变事件处理
            _keepOriginalSizeCheckBox.Checked += KeepOriginalSizeCheckBox_CheckedChanged;
            _keepOriginalSizeCheckBox.Unchecked += KeepOriginalSizeCheckBox_CheckedChanged;
            
            optionsPanel.Children.Add(_keepOriginalSizeCheckBox);
            
            optionsBorder.Child = optionsPanel;
            grid.Children.Add(optionsBorder);
            
            // 添加说明文本
            var instructionText = new TextBlock
            {
                Text = "Move mouse to select a window and click to confirm.\nPress ESC to cancel.",
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
        
        private void KeepOriginalSizeCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            // 当复选框状态改变时，触发事件
            KeepOriginalSizeChanged?.Invoke(this, _keepOriginalSizeCheckBox.IsChecked ?? true);
        }
        
        private void WindowSelector_Loaded(object sender, RoutedEventArgs e)
        {
            // 获取窗口句柄
            var hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            if (hwndSource != null)
            {
                _hwndSource = hwndSource.Handle;
                _presentationSource = hwndSource;
            }
            
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
            var mousePos = GetMousePosition();
            var point = new NativeMethods.POINT { X = (int)mousePos.X, Y = (int)mousePos.Y };
            
            // 启用点击穿透以便能选择下面的窗口
            SetInputTransparent(true);
            
            // 延迟一点以确保点击穿透生效
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            timer.Tick += (s, timerArgs) =>
            {
                timer.Stop();
                
                // 获取窗口句柄
                var hwnd = GetWindowHandleFromPoint(point);
                
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
                        
                        // 创建事件参数
                        var eventArgs = new WindowSelectedEventArgs
                        {
                            WindowHandle = hwnd,
                            ExecutablePath = executablePath,
                            X = rect.Left,
                            Y = rect.Top
                        };
                        
                        // 如果不保持原始大小，则设置宽度和高度
                        if (_keepOriginalSizeCheckBox.IsChecked != true)
                        {
                            eventArgs.Width = rect.Right - rect.Left;
                            eventArgs.Height = rect.Bottom - rect.Top;
                        }
                        
                        // 触发事件
                        WindowSelected?.Invoke(this, eventArgs);
                    }
                    catch (Exception ex)
                    {
                        NotificationService.ShowError($"获取窗口信息时出错: {ex.Message}");
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
            // 禁用点击穿透以便能接收鼠标事件
            SetInputTransparent(false);
        }

        private void HighlightTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                // 获取鼠标位置
                var mousePos = GetMousePosition();
                var point = new NativeMethods.POINT { X = (int)mousePos.X, Y = (int)mousePos.Y };
                
                // 临时设置为点击穿透以获取下方窗口
                SetInputTransparent(true);
                
                // 获取鼠标下方的窗口
                var hwnd = GetWindowHandleFromPoint(point);
                
                // 恢复非点击穿透状态以便能接收鼠标事件
                SetInputTransparent(false);
                
                if (hwnd != IntPtr.Zero && hwnd != _hwndSource)
                {
                    // 如果窗口发生变化或首次获取
                    if (hwnd != _currentHighlightedWindow)
                    {
                        _currentHighlightedWindow = hwnd;
                        
                        try
                        {
                            // 获取窗口区域
                            var rect = new NativeMethods.RECT();
                            if (NativeMethods.GetWindowRect(hwnd, ref rect))
                            {
                                // 获取窗口标题和进程信息
                                var windowTitle = NativeMethods.GetWindowTitle(hwnd);
                                
                                var processId = 0;
                                NativeMethods.GetWindowThreadProcessId(hwnd, out processId);
                                var processName = "未知进程";
                                
                                try
                                {
                                    var process = Process.GetProcessById(processId);
                                    processName = process.ProcessName;
                                }
                                catch { }
                                
                                // 更新窗口信息文本
                                _infoTextBlock.Text = $"窗口: {windowTitle}\n进程: {processName}\n位置: ({rect.Left}, {rect.Top})\n大小: {rect.Right - rect.Left} x {rect.Bottom - rect.Top}";
                                
                                // 使用正确的方法计算高亮框位置
                                UpdateHighlightBorderPosition(rect);
                            }
                        }
                        catch (Exception ex)
                        {
                            // 如果获取窗口信息出错，隐藏高亮框
                            _highlightBorder.Visibility = Visibility.Hidden;
                            _infoTextBlock.Text = $"无法获取窗口信息: {ex.Message}";
                        }
                    }
                }
                else
                {
                    // 如果没有找到窗口或是自己，隐藏高亮框
                    _highlightBorder.Visibility = Visibility.Hidden;
                    _infoTextBlock.Text = "Move mouse over a window to display information";
                    _currentHighlightedWindow = IntPtr.Zero;
                }
            }
            catch (Exception ex)
            {
                // 发生异常时更新文本
                _infoTextBlock.Text = $"出错: {ex.Message}";
                _highlightBorder.Visibility = Visibility.Hidden;
            }
        }
        
        // 更新高亮框位置的专用方法
        private void UpdateHighlightBorderPosition(NativeMethods.RECT rect)
        {
            try
            {
                if (_presentationSource == null)
                {
                    _highlightBorder.Visibility = Visibility.Hidden;
                    return;
                }
                
                // 获取系统DPI缩放
                var transformToDevice = _presentationSource.CompositionTarget.TransformToDevice;
                
                // 获取屏幕坐标点 (左上角和右下角)
                var screenPointTopLeft = new Point(rect.Left, rect.Top);
                var screenPointBottomRight = new Point(rect.Right, rect.Bottom);
                
                // 转换为设备无关的逻辑坐标
                // WPF使用设备无关像素(DIPs)，而Win32 API使用物理像素
                var devicePointTopLeft = transformToDevice.Transform(screenPointTopLeft);
                var devicePointBottomRight = transformToDevice.Transform(screenPointBottomRight);
                
                // 再转换为WPF窗口内的坐标
                Point windowPointTopLeft;
                Point windowPointBottomRight;
                
                try
                {
                    windowPointTopLeft = PointFromScreen(screenPointTopLeft);
                    windowPointBottomRight = PointFromScreen(screenPointBottomRight);
                    
                    // 计算宽度和高度 (DPI校正后)
                    var width = windowPointBottomRight.X - windowPointTopLeft.X;
                    var height = windowPointBottomRight.Y - windowPointTopLeft.Y;
                    
                    // 设置高亮框位置和大小
                    _highlightBorder.Width = width;
                    _highlightBorder.Height = height;
                    _highlightBorder.Margin = new Thickness(windowPointTopLeft.X, windowPointTopLeft.Y, 0, 0);
                    _highlightBorder.Visibility = Visibility.Visible;
                }
                catch
                {
                    // 使用备用方法作为退路 - 直接使用设备坐标计算
                    try 
                    {
                        var dpiScaleX = transformToDevice.M11;
                        var dpiScaleY = transformToDevice.M22;
                        
                        var left = rect.Left / dpiScaleX;
                        var top = rect.Top / dpiScaleY;
                        var width = (rect.Right - rect.Left) / dpiScaleX;
                        var height = (rect.Bottom - rect.Top) / dpiScaleY;
                        
                        _highlightBorder.Width = width;
                        _highlightBorder.Height = height;
                        _highlightBorder.Margin = new Thickness(left, top, 0, 0);
                        _highlightBorder.Visibility = Visibility.Visible;
                    }
                    catch
                    {
                        _highlightBorder.Visibility = Visibility.Hidden;
                    }
                }
            }
            catch
            {
                _highlightBorder.Visibility = Visibility.Hidden;
            }
        }
        
        private Point GetMousePosition()
        {
            // 获取鼠标在屏幕上的位置
            var point = new NativeMethods.POINT();
            NativeMethods.GetCursorPos(ref point);
            return new Point(point.X, point.Y);
        }
        
        // 从鼠标点获取窗口句柄，优先获取可见窗口
        private IntPtr GetWindowHandleFromPoint(NativeMethods.POINT point)
        {
            // 首先尝试直接获取鼠标下的窗口
            var hwnd = NativeMethods.WindowFromPoint(point);
            
            // 检查是否是自己
            if (hwnd == _hwndSource)
            {
                // 如果是自己，尝试获取下方的窗口
                hwnd = GetWindowBelowPoint(point);
            }
            
            return hwnd;
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