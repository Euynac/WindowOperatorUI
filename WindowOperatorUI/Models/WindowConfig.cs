namespace WindowOperatorUI.Models
{
    // Configuration model for window settings
    public class WindowConfig
    {
        public string ExePath { get; set; } = @"D:\Desktop\bongo_cat_mver_0.1.6_64\Bongo Cat Mver.exe";
        public int X { get; set; } = 100;
        public int Y { get; set; } = 200;
        public int? Width { get; set; } = 360;
        public int? Height { get; set; } = 240;
        public int? Order { get; set; } = 0;
        public bool EnableAlwaysOnTop { get; set; } = false;
        public bool EnableAlwaysOnTopMost { get; set; } = false;
        public bool EnableAlwaysOnBottom { get; set; } = false;
        public bool EnableMouseThrough { get; set; } = false;
        
        // Original configuration for undo operation
        [System.Text.Json.Serialization.JsonIgnore]
        public WindowConfig OriginalConfig { get; set; }
        
        // Window binding properties - not saved to config file
        [System.Text.Json.Serialization.JsonIgnore]
        public IntPtr BoundWindowHandle { get; set; } = IntPtr.Zero;
        
        [System.Text.Json.Serialization.JsonIgnore]
        public int BoundProcessId { get; set; } = 0;
        
        [System.Text.Json.Serialization.JsonIgnore]
        public string BoundWindowTitle { get; set; } = string.Empty;
        
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsBound => BoundWindowHandle != IntPtr.Zero;
        
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