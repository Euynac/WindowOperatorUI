using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WindowOperatorUI.Models
{
    public class ProcessDetails : INotifyPropertyChanged
    {
        // 实现INotifyPropertyChanged接口
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        // Identification
        private int _processId;
        public int ProcessId 
        { 
            get => _processId; 
            set
            {
                if (_processId != value)
                {
                    _processId = value;
                    OnPropertyChanged();
                }
            }
        }
        
        private string _processName = string.Empty;
        public string ProcessName 
        { 
            get => _processName; 
            set
            {
                if (_processName != value)
                {
                    _processName = value;
                    OnPropertyChanged();
                }
            }
        }
        
        private string _windowTitle = string.Empty;
        public string WindowTitle 
        { 
            get => _windowTitle; 
            set
            {
                if (_windowTitle != value)
                {
                    _windowTitle = value;
                    OnPropertyChanged();
                }
            }
        }
        
        // Window metrics
        private int _x;
        public int X 
        { 
            get => _x; 
            set
            {
                if (_x != value)
                {
                    _x = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormattedPosition));
                }
            }
        }
        
        private int _y;
        public int Y 
        { 
            get => _y; 
            set
            {
                if (_y != value)
                {
                    _y = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormattedPosition));
                }
            }
        }
        
        private int _width;
        public int Width 
        { 
            get => _width; 
            set
            {
                if (_width != value)
                {
                    _width = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormattedSize));
                }
            }
        }
        
        private int _height;
        public int Height 
        { 
            get => _height; 
            set
            {
                if (_height != value)
                {
                    _height = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormattedSize));
                }
            }
        }
        
        // Performance metrics
        private double _cpuUsage;
        public double CpuUsage 
        { 
            get => _cpuUsage; 
            set
            {
                if (Math.Abs(_cpuUsage - value) > 0.01)
                {
                    _cpuUsage = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormattedCpuUsage));
                }
            }
        }
        
        private long _memoryUsageBytes;
        public long MemoryUsageBytes 
        { 
            get => _memoryUsageBytes; 
            set
            {
                if (_memoryUsageBytes != value)
                {
                    _memoryUsageBytes = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormattedMemoryUsage));
                }
            }
        }
        
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