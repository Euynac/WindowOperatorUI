using System.Collections.Generic;
using System.Windows;
using WindowOperatorUI.Controls;

namespace WindowOperatorUI.Services
{
    public class NotificationService
    {
        private static readonly List<ToastNotification> _activeNotifications = new();
        
        public static void ShowSuccess(string message, int autoCloseSeconds = 3)
        {
            ShowNotification(message, ToastNotification.NotificationType.Success, autoCloseSeconds);
        }
        
        public static void ShowError(string message, int autoCloseSeconds = 4)
        {
            ShowNotification(message, ToastNotification.NotificationType.Error, autoCloseSeconds);
        }
        
        public static void ShowWarning(string message, int autoCloseSeconds = 3)
        {
            ShowNotification(message, ToastNotification.NotificationType.Warning, autoCloseSeconds);
        }
        
        public static void ShowInfo(string message, int autoCloseSeconds = 3)
        {
            ShowNotification(message, ToastNotification.NotificationType.Info, autoCloseSeconds);
        }
        
        private static void ShowNotification(string message, ToastNotification.NotificationType type, int autoCloseSeconds)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var notification = new ToastNotification(message, type, autoCloseSeconds);
                
                // Track notification
                _activeNotifications.Add(notification);
                notification.Closed += (s, e) => _activeNotifications.Remove(notification);
                
                // Show notification
                notification.Show();
            });
        }
        
        public static void CloseAllNotifications()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var notification in _activeNotifications.ToArray())
                {
                    notification.Close();
                }
                _activeNotifications.Clear();
            });
        }
    }
} 