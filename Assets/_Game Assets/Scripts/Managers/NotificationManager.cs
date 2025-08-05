using System;
using UnityEditor;
using UnityEngine;
#if UNITY_ANDROID
using Unity.Notifications.Android;
#elif UNITY_IOS
using Unity.Notifications.iOS;
#endif

public abstract class NotificationManager
{
    private static NotificationsData _notificationData;

    public static void Initialize()
    {
        _notificationData ??= Resources.Load<NotificationsData>("Game Resources/Notifications");
        
        AppManager.Instance.OnApplicationPauseListener += OnApplicationPause;
    }

    private static void OnApplicationPause(bool pause)
    {
        if (pause)
            ScheduleNotifications();
        else
            CancelAllNotifications();
    }

    private static void ScheduleNotifications()
    {
        foreach (var notification in _notificationData.notifications)
        {
            var index = _notificationData.notifications.FindIndex(x => x == notification);
           
            SendNotification(notification, index);
        }
    }

    public static void SendNotification(NotificationsData.NotificationDetail detail, int index = 0)
    {
#if UNITY_ANDROID
        var channel = new AndroidNotificationChannel()
        {
            Id = Application.companyName,
            Name = Application.productName,
            Importance = Importance.Default,
            Description = "Generic notifications",
        };
        AndroidNotificationCenter.RegisterNotificationChannel(channel);
        
        var notification = new AndroidNotification
        {
            Title = detail.title,
            Text = detail.description,
            FireTime = System.DateTime.Now.AddMinutes(detail.deliveryTimeInMinutes)
        };

        AndroidNotificationCenter.SendNotificationWithExplicitID(notification, channel.Id, index);
#elif UNITY_IOS
        var identifier = "_" + Application.identifier + "_" + index;

        iOSNotificationCenter.RemoveScheduledNotification(identifier);
        iOSNotificationCenter.ScheduleNotification(new iOSNotification()
        {
            Title = detail.title,
            Body = detail.description,
            Identifier = identifier,
            Trigger = new iOSNotificationTimeIntervalTrigger()
            {
                Repeats = false,
                TimeInterval = new TimeSpan(0, detail.deliveryTimeInMinutes, 0),
            }
        });
#endif

        var deliveryTime = DateTime.Now.AddMinutes(detail.deliveryTimeInMinutes);
        Debug.Log($"Notification {index} scheduled for {deliveryTime}");
    }

    private static void CancelAllNotifications()
    {
#if UNITY_ANDROID
        AndroidNotificationCenter.CancelAllNotifications();
#elif UNITY_IOS
        iOSNotificationCenter.RemoveAllScheduledNotifications();
#endif
    }
}