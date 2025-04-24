using System;

namespace WindowOperatorUI.Models
{
    public class WindowSelectedEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; set; } = IntPtr.Zero;
        public string ExecutablePath { get; set; } = "";
        public int X { get; set; }
        public int Y { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
    }
} 