using System;
using System.Collections.Generic;
using System.Windows;
using WindowOperatorUI.Controls;
using WindowOperatorUI.Utils;

namespace WindowOperatorUI.Services
{
    public class NotificationService
    {
        private static readonly List<ToastNotification> _activeNotifications = [];
        private static Action _lastUndoAction;
        
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
        
        public static void ShowInfo(string message, int autoCloseSeconds = 3, Action clickAction = null)
        {
            ShowNotification(message, ToastNotification.NotificationType.Info, autoCloseSeconds, clickAction);
        }
        
        private static void ShowNotification(string message, ToastNotification.NotificationType type, int autoCloseSeconds, Action clickAction = null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var notification = new ToastNotification(message, type, autoCloseSeconds);
                
                // If there's a click action, set it up
                if (clickAction != null)
                {
                    _lastUndoAction = clickAction;
                    notification.MouseLeftButtonDown += (s, e) => 
                    {
                        clickAction?.Invoke();
                        notification.Close();
                    };
                    notification.Cursor = System.Windows.Input.Cursors.Hand;
                }
                
                // Track notification
                _activeNotifications.Add(notification);
                notification.Closed += (s, e) => _activeNotifications.Remove(notification);
                
                // Apply the fluent acrylic effect
                notification.Loaded += (sender, e) => 
                {
                    WindowBackdrop.ApplyAcrylicEffect(notification, 0xBB202020);
                };
                
                // Show notification
                notification.Show();
            });
        }
        
        public static void ExecuteLastUndoAction()
        {
            _lastUndoAction?.Invoke();
            _lastUndoAction = null;
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