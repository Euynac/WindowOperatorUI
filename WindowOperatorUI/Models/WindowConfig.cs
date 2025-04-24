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
        public int? Order { get; set; } = null;
        public bool EnableAlwaysOnTop { get; set; } = false;
        public bool EnableAlwaysOnTopMost { get; set; } = false;
        public bool EnableMouseThrough { get; set; } = false;
    }
} 