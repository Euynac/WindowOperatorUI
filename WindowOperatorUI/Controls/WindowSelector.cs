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
        private DispatcherTimer _highlightTimer;
        private IntPtr _hwndSource;
        private bool _isTransparentToInput = false; // 控制是否启用点击穿透
        private IntPtr _currentHighlightedWindow = IntPtr.Zero;
        private Border _highlightBorder;
        private TextBlock _infoTextBlock;
        private PresentationSource _presentationSource;
        private CheckBox _keepOriginalSizeCheckBox;
        private CheckBox _customExePathCheckBox;
        private TextBox _exePathTextBox;

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
                Text = "移动鼠标到窗口上可显示窗口信息"
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
                Content = "保持窗口原始大小",
                Foreground = Brushes.White,
                IsChecked = true,
                Margin = new Thickness(0, 0, 0, 5)
            };
            optionsPanel.Children.Add(_keepOriginalSizeCheckBox);
            
            // 自定义路径选项
            _customExePathCheckBox = new CheckBox
            {
                Content = "自定义程序路径",
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 5)
            };
            optionsPanel.Children.Add(_customExePathCheckBox);
            
            // 程序路径输入框
            _exePathTextBox = new TextBox
            {
                Width = 300,
                Margin = new Thickness(20, 0, 0, 0),
                Visibility = Visibility.Collapsed
            };
            _customExePathCheckBox.Checked += (s, e) => _exePathTextBox.Visibility = Visibility.Visible;
            _customExePathCheckBox.Unchecked += (s, e) => _exePathTextBox.Visibility = Visibility.Collapsed;
            optionsPanel.Children.Add(_exePathTextBox);
            
            optionsBorder.Child = optionsPanel;
            grid.Children.Add(optionsBorder);
            
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
                        
                        // 如果用户选择自定义路径，则使用自定义路径
                        if (_customExePathCheckBox.IsChecked == true && !string.IsNullOrWhiteSpace(_exePathTextBox.Text))
                        {
                            executablePath = _exePathTextBox.Text;
                        }
                        
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
                                string windowTitle = NativeMethods.GetWindowTitle(hwnd);
                                
                                var processId = 0;
                                NativeMethods.GetWindowThreadProcessId(hwnd, out processId);
                                string processName = "未知进程";
                                
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
                    _infoTextBlock.Text = "移动鼠标到窗口上可显示窗口信息";
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
                Matrix transformToDevice = _presentationSource.CompositionTarget.TransformToDevice;
                
                // 获取屏幕坐标点 (左上角和右下角)
                Point screenPointTopLeft = new Point(rect.Left, rect.Top);
                Point screenPointBottomRight = new Point(rect.Right, rect.Bottom);
                
                // 转换为设备无关的逻辑坐标
                // WPF使用设备无关像素(DIPs)，而Win32 API使用物理像素
                Point devicePointTopLeft = transformToDevice.Transform(screenPointTopLeft);
                Point devicePointBottomRight = transformToDevice.Transform(screenPointBottomRight);
                
                // 再转换为WPF窗口内的坐标
                Point windowPointTopLeft;
                Point windowPointBottomRight;
                
                try
                {
                    windowPointTopLeft = PointFromScreen(screenPointTopLeft);
                    windowPointBottomRight = PointFromScreen(screenPointBottomRight);
                    
                    // 计算宽度和高度 (DPI校正后)
                    double width = windowPointBottomRight.X - windowPointTopLeft.X;
                    double height = windowPointBottomRight.Y - windowPointTopLeft.Y;
                    
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
                        double dpiScaleX = transformToDevice.M11;
                        double dpiScaleY = transformToDevice.M22;
                        
                        double left = rect.Left / dpiScaleX;
                        double top = rect.Top / dpiScaleY;
                        double width = (rect.Right - rect.Left) / dpiScaleX;
                        double height = (rect.Bottom - rect.Top) / dpiScaleY;
                        
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
            NativeMethods.POINT point = new NativeMethods.POINT();
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