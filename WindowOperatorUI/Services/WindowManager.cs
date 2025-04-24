using System;
using WindowOperatorUI.Models;
using WindowOperatorUI.Utils;

namespace WindowOperatorUI.Services
{
    public class WindowManager
    {
        private readonly Logger _logger;

        public WindowManager(Logger logger)
        {
            _logger = logger;
        }

        public bool PositionWindow(IntPtr hwnd, WindowConfig config, string windowTitle)
        {
            try
            {
                // First, handle size and position
                if (config is { Width: not null, Height: not null })
                {
                    // Move and resize window
                    if (!NativeMethods.MoveWindow(hwnd, config.X, config.Y, config.Width.Value, config.Height.Value, true))
                    {
                        _logger.Log($"[ERROR] Failed to move and resize window '{windowTitle}'.", true);
                        return false;
                    }
                }
                else
                {
                    // Only move window without resizing
                    if (!NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, config.X, config.Y, 0, 0, 
                        NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE))
                    {
                        _logger.Log($"[ERROR] Failed to move window '{windowTitle}'.", true);
                        return false;
                    }
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

        private void SetWindowZOrder(IntPtr hwnd, WindowConfig config, string windowTitle)
        {
            var zOrderPosition = IntPtr.Zero;
            
            // Process topmost state regardless of Order value
            if (config.EnableAlwaysOnTopMost)
            {
                zOrderPosition = NativeMethods.HWND_TOPMOST;
                _logger.Log($"[INFO] Setting window '{windowTitle}' to be always on top most (above system windows)");
                
                var flags = NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE;
                if (!NativeMethods.SetWindowPos(hwnd, zOrderPosition, 0, 0, 0, 0, flags))
                {
                    _logger.Log($"[ERROR] Failed to set window '{windowTitle}' as topmost", true);
                }
            }
            else if (config.EnableAlwaysOnTop)
            {
                zOrderPosition = NativeMethods.HWND_TOP;
                _logger.Log($"[INFO] Setting window '{windowTitle}' always on top of normal windows");
                
                var flags = NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE;
                if (!NativeMethods.SetWindowPos(hwnd, zOrderPosition, 0, 0, 0, 0, flags))
                {
                    _logger.Log($"[ERROR] Failed to set window '{windowTitle}' as always on top", true);
                }
            }
            
            // Handle Order if specified (this is separate from topmost setting)
            if (config.Order.HasValue)
            {
                _logger.Log($"[INFO] Set window '{windowTitle}' Z-order to {config.Order.Value}");
                // Note: Windows doesn't support exact Z-order numbers, but this could be extended
                // in the future to track window handles and order them relatively
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