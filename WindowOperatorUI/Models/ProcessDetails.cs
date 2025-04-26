using System;

namespace WindowOperatorUI.Models
{
    public class ProcessDetails
    {
        // Identification
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        
        // Window metrics
        public int X { get; set; }
        public int Y { get; set; } 
        public int Width { get; set; }
        public int Height { get; set; }
        
        // Performance metrics
        public double CpuUsage { get; set; }
        public long MemoryUsageBytes { get; set; }
        
        // Formatted display properties
        public string FormattedMemoryUsage => FormatMemorySize(MemoryUsageBytes);
        public string FormattedCpuUsage => $"{CpuUsage:0.0}%";
        public string FormattedPosition => $"({X}, {Y})";
        public string FormattedSize => $"{Width}×{Height}";
        
        // Tracking for CPU calculation
        public TimeSpan LastTotalProcessorTime { get; set; }
        public DateTime LastUpdateTime { get; set; }
        
        // Helper method to format memory size
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
    }
} 