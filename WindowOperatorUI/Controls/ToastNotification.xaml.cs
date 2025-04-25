using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace WindowOperatorUI.Controls
{
    public partial class ToastNotification : Window
    {
        private readonly DispatcherTimer _closeTimer;
        private static int _activeNotificationCount = 0;
        
        public enum NotificationType
        {
            Success,
            Error,
            Warning,
            Info
        }
        
        public event EventHandler Closed;
        
        public ToastNotification(string message, NotificationType type = NotificationType.Info, int autoCloseSeconds = 3)
        {
            InitializeComponent();
            
            // Set message
            MessageTextBlock.Text = message;
            
            // Configure based on notification type
            ConfigureNotificationType(type);
            
            // Calculate position (based on notification count)
            PositionWindowAtBottomOfScreen();
            
            // Start auto-close timer
            _closeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(autoCloseSeconds)
            };
            _closeTimer.Tick += CloseTimer_Tick;
            _closeTimer.Start();
            
            // Animate in
            Loaded += (s, e) =>
            {
                var storyboard = (Storyboard)FindResource("FadeInStoryboard");
                storyboard.Begin(this);
            };
        }
        
        private void PositionWindowAtBottomOfScreen()
        {
            // Track notification count
            _activeNotificationCount++;
            int index = _activeNotificationCount;
            
            // Get the work area (screen minus taskbar)
            var workArea = SystemParameters.WorkArea;
            
            // Set initial position
            WindowStartupLocation = WindowStartupLocation.Manual;
            
            Loaded += (s, e) =>
            {
                // Get notification height with margins
                double height = ActualHeight + 20; // Add margin
                
                // Calculate position from bottom of screen
                Left = workArea.Right - ActualWidth - 20;
                Top = workArea.Bottom - (height * index);
            };
            
            // Adjust other notifications when this one is closed
            this.Closed += (s, e) =>
            {
                _activeNotificationCount--;
            };
        }
        
        private void ConfigureNotificationType(NotificationType type)
        {
            switch (type)
            {
                case NotificationType.Success:
                    IconTextBlock.Text = "\uE73E"; // Check mark icon
                    IconTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                    break;
                case NotificationType.Error:
                    IconTextBlock.Text = "\uE783"; // Error icon
                    IconTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(229, 57, 53));
                    break;
                case NotificationType.Warning:
                    IconTextBlock.Text = "\uE7BA"; // Warning icon
                    IconTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(255, 193, 7));
                    break;
                case NotificationType.Info:
                default:
                    IconTextBlock.Text = "\uE946"; // Info icon
                    IconTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                    break;
            }
        }
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        
        private void CloseTimer_Tick(object sender, EventArgs e)
        {
            _closeTimer.Stop();
            
            var storyboard = (Storyboard)FindResource("FadeOutStoryboard");
            storyboard.Completed += (s, _) => Close();
            storyboard.Begin(this);
        }
        
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }
} 