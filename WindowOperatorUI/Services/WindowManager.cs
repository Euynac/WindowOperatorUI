using System;
using System.Threading;
using WindowOperatorUI.Models;
using WindowOperatorUI.Utils;

namespace WindowOperatorUI.Services
{
    public class WindowManager
    {
        private readonly Logger _logger;
        private const int MAX_RESIZE_RETRIES = 3;
        private const int RESIZE_DELAY_MS = 100;

        public WindowManager(Logger logger)
        {
            _logger = logger;
        }

        public bool PositionWindow(IntPtr hwnd, WindowConfig config, string windowTitle)
        {
            try
            {
                // Log original window size and position for debugging
                var originalRect = new NativeMethods.RECT();
                if (NativeMethods.GetWindowRect(hwnd, ref originalRect))
                {
                    var originalWidth = originalRect.Right - originalRect.Left;
                    var originalHeight = originalRect.Bottom - originalRect.Top;
                    _logger.Log($"[INFO] Window '{windowTitle}' original position: ({originalRect.Left}, {originalRect.Top}), size: {originalWidth}x{originalHeight}");
                }

                _logger.Log($"[INFO] Setting window '{windowTitle}' position to ({config.X}, {config.Y})");
                
                var resizeSuccess = false;
                
                // First, handle size and position
                if (config is { Width: not null, Height: not null })
                {
                    _logger.Log($"[INFO] Attempting to resize window '{windowTitle}' to {config.Width}x{config.Height}");

                    // 尝试多次设置窗口大小，提高成功率
                    for (var attempt = 1; attempt <= MAX_RESIZE_RETRIES; attempt++)
                    {
                        // 先使用SetWindowPos移动窗口到目标位置（不改变大小）
                        var posSuccess = NativeMethods.SetWindowPos(
                            hwnd, 
                            IntPtr.Zero,
                            config.X, 
                            config.Y,
                            0, 
                            0,
                            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE
                        );
                        
                        if (!posSuccess)
                        {
                            _logger.Log($"[WARNING] Attempt {attempt} - Failed to position window '{windowTitle}' at ({config.X}, {config.Y})");
                        }
                        
                        // 短暂延迟，让窗口系统处理位置变化
                        Thread.Sleep(RESIZE_DELAY_MS);
                        
                        // 然后使用MoveWindow同时设置位置和大小
                        resizeSuccess = NativeMethods.MoveWindow(
                            hwnd, 
                            config.X, 
                            config.Y,

                            config.Width.Value, 
                            config.Height.Value, 
                            true
                        );
                        
                        if (resizeSuccess)
                        {
                            _logger.Log($"[SUCCESS] Attempt {attempt} - Successfully resized window '{windowTitle}' to {config.Width}x{config.Height}");
                            break;
                        }
                        else
                        {
                            _logger.Log($"[WARNING] Attempt {attempt} - Failed to resize window '{windowTitle}' using MoveWindow");
                            
                            // 再尝试使用SetWindowPos来设置大小
                            resizeSuccess = NativeMethods.SetWindowPos(
                                hwnd, 
                                IntPtr.Zero, 
                                config.X, 
                                config.Y, 
                                config.Width.Value, 
                                config.Height.Value, 
                                NativeMethods.SWP_NOACTIVATE
                            );
                            
                            if (resizeSuccess)
                            {
                                _logger.Log($"[SUCCESS] Attempt {attempt} - Successfully resized window '{windowTitle}' using SetWindowPos");
                                break;
                            }
                            else
                            {
                                _logger.Log($"[WARNING] Attempt {attempt} - Failed to resize window '{windowTitle}' using SetWindowPos");
                            }
                        }
                        
                        // 如果这不是最后一次尝试，等待一段时间再试
                        if (attempt < MAX_RESIZE_RETRIES)
                        {
                            _logger.Log($"[INFO] Waiting before next resize attempt for window '{windowTitle}'");
                            Thread.Sleep(RESIZE_DELAY_MS * 2);
                        }
                    }
                    
                    if (!resizeSuccess)
                    {
                        _logger.Log($"[ERROR] Failed to move and resize window '{windowTitle}' after {MAX_RESIZE_RETRIES} attempts", true);
                        return false;
                    }
                    
                    // 验证窗口大小是否正确设置
                    VerifyWindowSizeAndPosition(hwnd, config, windowTitle);
                }
                else
                {
                    _logger.Log($"[INFO] Moving window '{windowTitle}' without resizing");
                    
                    // Only move window without resizing
                    if (!NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, config.X, config.Y, 0, 0, 
                        NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE))
                    {
                        _logger.Log($"[ERROR] Failed to move window '{windowTitle}'.", true);
                        return false;
                    }
                    
                    _logger.Log($"[SUCCESS] Successfully moved window '{windowTitle}' to ({config.X}, {config.Y})");
                }

                // Handle Z-order and window style
                SetWindowZOrder(hwnd, config, windowTitle);
                
                // Handle mouse-through
                if (config.EnableMouseThrough)
                {
                    SetMouseThrough(hwnd, windowTitle);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.Log($"[ERROR] Failed to position window '{windowTitle}': {ex.Message}", true);
                return false;
            }
        }
        
        private void VerifyWindowSizeAndPosition(IntPtr hwnd, WindowConfig config, string windowTitle)
        {
            // 验证窗口大小是否符合预期
            var rect = new NativeMethods.RECT();
            if (NativeMethods.GetWindowRect(hwnd, ref rect))
            {
                var width = rect.Right - rect.Left;
                var height = rect.Bottom - rect.Top;
                
                _logger.Log($"[INFO] After resize: Window '{windowTitle}' position is ({rect.Left}, {rect.Top}), size is {width}x{height}");
                
                var sizeMatchesExpected = true;
                
                // 允许1像素的误差，避免舍入问题
                if (Math.Abs(width - config.Width.Value) > 1)
                {
                    _logger.Log($"[WARNING] Width mismatch for window '{windowTitle}': expected {config.Width.Value}, got {width}");
                    sizeMatchesExpected = false;
                }
                
                if (Math.Abs(height - config.Height.Value) > 1)
                {
                    _logger.Log($"[WARNING] Height mismatch for window '{windowTitle}': expected {config.Height.Value}, got {height}");
                    sizeMatchesExpected = false;
                }
                
                if (sizeMatchesExpected)
                {
                    _logger.Log($"[SUCCESS] Window '{windowTitle}' size verification passed");
                }
            }
            else
            {
                _logger.Log($"[WARNING] Could not verify window '{windowTitle}' size after resize");
            }
        }

        private void SetWindowZOrder(IntPtr hwnd, WindowConfig config, string windowTitle)
        {
            var flags = NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE;
            
            // First reset the window's Z-order to non-topmost to ensure we're starting from a clean state
            // This helps with windows that might already be in a topmost state
            NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_NOTOPMOST, 0, 0, 0, 0, flags);
            _logger.Log($"[INFO] Reset Z-order of window '{windowTitle}' to non-topmost state");
            
            // Handle bottom positioning
            if (config.EnableAlwaysOnBottom || config.Order is <= 0)
            {
                NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_BOTTOM, 0, 0, 0, 0, flags);
                _logger.Log($"[INFO] Set window '{windowTitle}' to bottom of Z-order");
                return; // Skip other positioning if we're setting to bottom
            }
            
            // Process topmost state regardless of Order value
            if (config.EnableAlwaysOnTopMost)
            {
                _logger.Log($"[INFO] Setting window '{windowTitle}' to be always on top most (above system windows)");
                
                if (!NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0, flags))
                {
                    _logger.Log($"[ERROR] Failed to set window '{windowTitle}' as topmost", true);
                }
            }
            else if (config.EnableAlwaysOnTop)
            {
                _logger.Log($"[INFO] Setting window '{windowTitle}' always on top of normal windows");
                
                if (!NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOP, 0, 0, 0, 0, flags))
                {
                    _logger.Log($"[ERROR] Failed to set window '{windowTitle}' as always on top", true);
                }
            }
            
            // Handle Order if specified (this is separate from topmost setting)
            if (config.Order is > 0)
            {
                _logger.Log($"[INFO] Set window '{windowTitle}' Z-order to {config.Order.Value}");
                
                // 如果存在多个TopMost或Top窗口，我们要确保Order大的窗口在上层
                // 这里我们使用循环提升Z顺序，Order值越大，提升次数越多
                if (config.EnableAlwaysOnTopMost || config.EnableAlwaysOnTop)
                {
                    var insertAfter = config.EnableAlwaysOnTopMost ? NativeMethods.HWND_TOPMOST : NativeMethods.HWND_TOP;
                    
                    // Order值代表提升Z顺序的次数，每次提升都会将窗口移到同组窗口的顶部
                    // 为了使顺序更明显，我们使用Order值的2倍作为提升次数
                    for (int i = 0; i < config.Order.Value * 2; i++)
                    {
                        NativeMethods.SetWindowPos(hwnd, insertAfter, 0, 0, 0, 0, flags);
                        Thread.Sleep(10); // 短暂延迟，确保窗口系统能正确处理
                    }
                    
                    _logger.Log($"[INFO] Enhanced Z-order for window '{windowTitle}' with Order={config.Order.Value}");
                }
            }
        }

        private void SetMouseThrough(IntPtr hwnd, string windowTitle)
        {
            // Make window transparent to mouse events
            var exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            exStyle |= NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TRANSPARENT;
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle);
            _logger.Log($"[INFO] Enabled mouse click-through for window '{windowTitle}'");
        }
    }
} 