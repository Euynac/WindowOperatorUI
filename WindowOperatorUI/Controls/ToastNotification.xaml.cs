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
        
        public enum NotificationType
        {
            Success,
            Error,
            Warning,
            Info
        }
        
        public ToastNotification(string message, NotificationType type = NotificationType.Info, int autoCloseSeconds = 3)
        {
            InitializeComponent();
            
            // Set message
            MessageTextBlock.Text = message;
            
            // Configure based on notification type
            ConfigureNotificationType(type);
            
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
    }
} 