using System.Collections.Generic;

namespace WindowOperatorUI.Models
{
    // Application settings
    public class AppConfig
    {
        public List<WindowConfig> Windows { get; set; } = [new WindowConfig()];
        public bool SilentMode { get; set; } = false;
        public bool RunAtStartup { get; set; } = false;
        public bool RunAsAdmin { get; set; } = false;
        public bool KeepOriginalSize { get; set; } = true;
        public bool ConfirmProcessKill { get; set; } = true;
    }
} 