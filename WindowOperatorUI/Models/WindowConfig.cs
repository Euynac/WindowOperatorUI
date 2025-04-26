using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WindowOperatorUI.Models
{
    // Configuration model for window settings
    public class WindowConfig : INotifyPropertyChanged
    {
        // 实现INotifyPropertyChanged接口
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        // 生成可绑定属性
        private string _exePath = @"D:\Desktop\bongo_cat_mver_0.1.6_64\Bongo Cat Mver.exe";
        public string ExePath 
        { 
            get => _exePath; 
            set 
            {
                if (_exePath != value)
                {
                    _exePath = value;
                    OnPropertyChanged();
                }
            } 
        }

        private int _x = 100;
        public int X 
        { 
            get => _x; 
            set 
            {
                if (_x != value)
                {
                    _x = value;
                    OnPropertyChanged();
                }
            } 
        }

        private int _y = 200;
        public int Y 
        { 
            get => _y; 
            set 
            {
                if (_y != value)
                {
                    _y = value;
                    OnPropertyChanged();
                }
            } 
        }

        private int? _width = 360;
        public int? Width 
        { 
            get => _width; 
            set 
            {
                if (_width != value)
                {
                    _width = value;
                    OnPropertyChanged();
                }
            } 
        }

        private int? _height = 240;
        public int? Height 
        { 
            get => _height; 
            set 
            {
                if (_height != value)
                {
                    _height = value;
                    OnPropertyChanged();
                }
            } 
        }

        private int? _order = 0;
        public int? Order 
        { 
            get => _order; 
            set 
            {
                if (_order != value)
                {
                    _order = value;
                    OnPropertyChanged();
                }
            } 
        }

        private bool _enableAlwaysOnTop = false;
        public bool EnableAlwaysOnTop
        {
            get => _enableAlwaysOnTop;
            set
            {
                if (_enableAlwaysOnTop != value)
                {
                    _enableAlwaysOnTop = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _enableAlwaysOnTopMost = false;
        public bool EnableAlwaysOnTopMost
        {
            get => _enableAlwaysOnTopMost;
            set
            {
                if (_enableAlwaysOnTopMost != value)
                {
                    _enableAlwaysOnTopMost = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _enableAlwaysOnBottom = false;
        public bool EnableAlwaysOnBottom
        {
            get => _enableAlwaysOnBottom;
            set
            {
                if (_enableAlwaysOnBottom != value)
                {
                    _enableAlwaysOnBottom = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _enableMouseThrough = false;
        public bool EnableMouseThrough
        {
            get => _enableMouseThrough;
            set
            {
                if (_enableMouseThrough != value)
                {
                    _enableMouseThrough = value;
                    OnPropertyChanged();
                }
            }
        }
        
        // Original configuration for undo operation
        [System.Text.Json.Serialization.JsonIgnore]
        public WindowConfig OriginalConfig { get; set; }
        
        // Window binding properties - not saved to config file
        private IntPtr _boundWindowHandle = IntPtr.Zero;
        
        [System.Text.Json.Serialization.JsonIgnore]
        public IntPtr BoundWindowHandle 
        { 
            get => _boundWindowHandle;
            set 
            {
                if (_boundWindowHandle != value)
                {
                    _boundWindowHandle = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsBound));
                }
            }
        }
        
        private int _boundProcessId = 0;
        
        [System.Text.Json.Serialization.JsonIgnore]
        public int BoundProcessId 
        { 
            get => _boundProcessId;
            set 
            {
                if (_boundProcessId != value)
                {
                    _boundProcessId = value;
                    OnPropertyChanged();
                }
            }
        }
        
        private string _boundWindowTitle = string.Empty;
        
        [System.Text.Json.Serialization.JsonIgnore]
        public string BoundWindowTitle 
        { 
            get => _boundWindowTitle;
            set 
            {
                if (_boundWindowTitle != value)
                {
                    _boundWindowTitle = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsBound => BoundWindowHandle != IntPtr.Zero;
        
        // Real-time process monitoring information
        private ProcessDetails _processDetails;
        
        [System.Text.Json.Serialization.JsonIgnore]
        public ProcessDetails ProcessDetails
        {
            get => _processDetails;
            set 
            {
                if (_processDetails != value)
                {
                    _processDetails = value;
                    OnPropertyChanged();
                }
            }
        }
        
        // Creates a deep copy of the current configuration
        public WindowConfig Clone()
        {
            return new WindowConfig
            {
                ExePath = this.ExePath,
                X = this.X,
                Y = this.Y,
                Width = this.Width,
                Height = this.Height,
                Order = this.Order,
                EnableAlwaysOnTop = this.EnableAlwaysOnTop,
                EnableAlwaysOnTopMost = this.EnableAlwaysOnTopMost,
                EnableAlwaysOnBottom = this.EnableAlwaysOnBottom,
                EnableMouseThrough = this.EnableMouseThrough
            };
        }
    }
} 