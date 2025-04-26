using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WindowOperatorUI.Utils
{
    /// <summary>
    /// Provides methods to apply Windows 10/11 acrylic blur effects to windows
    /// </summary>
    public static class WindowBackdrop
    {
        // Windows 10 1803+ AccentState options
        public enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4, // Windows 10 1803+
            ACCENT_ENABLE_HOSTBACKDROP = 5, // Windows 11
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct AccentPolicy
        {
            public AccentState AccentState;
            public uint AccentFlags;
            public uint GradientColor;
            public uint AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        internal enum WindowCompositionAttribute
        {
            WCA_ACCENT_POLICY = 19
        }

        [DllImport("user32.dll")]
        internal static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        /// <summary>
        /// Apply acrylic blur effect to the window
        /// </summary>
        /// <param name="window">Window to apply effect to</param>
        /// <param name="tintColor">Tint color in RGBA format (e.g. 0x80202020 for semi-transparent dark gray)</param>
        public static void ApplyAcrylicEffect(Window window, uint tintColor = 0x20202020)
        {
            if (window == null) return;

            IntPtr windowHandle = new WindowInteropHelper(window).Handle;

            if (windowHandle == IntPtr.Zero) return;

            var accent = new AccentPolicy
            {
                AccentState = IsWindows11() ? AccentState.ACCENT_ENABLE_HOSTBACKDROP : AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                GradientColor = tintColor
            };

            var accentStructSize = Marshal.SizeOf(accent);
            var accentPtr = Marshal.AllocHGlobal(accentStructSize);
            Marshal.StructureToPtr(accent, accentPtr, false);

            var data = new WindowCompositionAttributeData
            {
                Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                SizeOfData = accentStructSize,
                Data = accentPtr
            };

            try
            {
                SetWindowCompositionAttribute(windowHandle, ref data);
            }
            finally
            {
                Marshal.FreeHGlobal(accentPtr);
            }
        }

        /// <summary>
        /// Apply regular blur effect to the window
        /// </summary>
        /// <param name="window">Window to apply effect to</param>
        public static void ApplyBlurEffect(Window window)
        {
            if (window == null) return;

            IntPtr windowHandle = new WindowInteropHelper(window).Handle;

            if (windowHandle == IntPtr.Zero) return;

            var accent = new AccentPolicy
            {
                AccentState = AccentState.ACCENT_ENABLE_BLURBEHIND,
                GradientColor = 0x00000000
            };

            var accentStructSize = Marshal.SizeOf(accent);
            var accentPtr = Marshal.AllocHGlobal(accentStructSize);
            Marshal.StructureToPtr(accent, accentPtr, false);

            var data = new WindowCompositionAttributeData
            {
                Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                SizeOfData = accentStructSize,
                Data = accentPtr
            };

            try
            {
                SetWindowCompositionAttribute(windowHandle, ref data);
            }
            finally
            {
                Marshal.FreeHGlobal(accentPtr);
            }
        }

        /// <summary>
        /// Check if the system is running Windows 11
        /// </summary>
        private static bool IsWindows11()
        {
            var os = Environment.OSVersion;
            return os.Version.Major >= 10 && os.Version.Build >= 22000;
        }
    }
} 