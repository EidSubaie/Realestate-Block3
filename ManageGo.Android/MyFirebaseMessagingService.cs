﻿using System;
using System.Collections.Generic;
using System.Linq;
using Android.App;
using Android.Content;
using Android.Media;
using Android.Support.V4.App;
using Android.Util;
using Firebase.Messaging;
using Microsoft.AppCenter.Analytics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static Android.App.Notification;

namespace ManageGo.Droid
{

    [Service]
    [IntentFilter(new[] { "com.google.firebase.MESSAGING_EVENT" })]
    public class MyFirebaseMessagingService : FirebaseMessagingService
    {
        const string TAG = "MyFirebaseMsgService";
        public override void OnMessageReceived(RemoteMessage message)
        {
            var not = message.GetNotification();
            if (message.Data.TryGetValue("message", out string _message))
            {
                var msg = JsonConvert.DeserializeObject<Models.PushNotificationMessage>(_message);

                var _type = (Models.PushNotificationType)msg.Type;
                var _notificationObject = msg.NotificationObject;

                SendNotification(msg.Title, msg.Body, msg);
            }

        }

        private void SendNotification(string messageTitle, string messageBody, Models.PushNotificationMessage data)
        {
            var intent = new Intent(this, typeof(MainActivity));

            intent.SetFlags(ActivityFlags.SingleTop);
            if (data != null)
            {
                intent.PutExtra("IsGroup", 0);
                intent.PutExtra("Type", data.Type);
                intent.PutExtra("NotificationObject", data.NotificationObject);
            }
            int uniqueInt = (int)(DateTime.Now.Millisecond & 0xfffffff);
            var pendingIntent = PendingIntent.GetActivity(this, uniqueInt, intent, PendingIntentFlags.UpdateCurrent);


            var _intent = new Intent(this, typeof(MainActivity));

            _intent.SetFlags(ActivityFlags.SingleTop);
            if (data != null)
            {
                _intent.PutExtra("IsGroup", 1);
                _intent.PutExtra("Type", data.Type);
                _intent.PutExtra("NotificationObject", data.NotificationObject);
            }
            uniqueInt = (int)(DateTime.Now.Millisecond & 0xfffffff);
            var _pendingIntent = PendingIntent.GetActivity(this, uniqueInt, _intent, PendingIntentFlags.UpdateCurrent);


            NotificationCompat.Builder groupBuilder =
                new NotificationCompat.Builder(this, MainActivity.CHANNEL_ID)
                    .SetSmallIcon(Resource.Drawable.not_icon_white)
                    .SetGroupSummary(true)
                    .SetGroup($"com.ManageGo.ManageGo.{data.Type}")
                    .SetStyle(new NotificationCompat.InboxStyle())
                    .SetAutoCancel(true)
                    .SetContentIntent(_pendingIntent);

            var notificationBuilder = new NotificationCompat.Builder(this, MainActivity.CHANNEL_ID)
                .SetSmallIcon(Resource.Drawable.not_icon_white)
                .SetContentTitle(messageTitle).SetContentText(messageBody)
                .SetStyle(new NotificationCompat.BigTextStyle())
                .SetAutoCancel(true)
                .SetGroup($"com.ManageGo.ManageGo.{data.Type}")
                .SetContentIntent(pendingIntent);
            var notificationManager = NotificationManagerCompat.From(this);





            notificationManager.Notify(data.Type, groupBuilder.Build());
            notificationManager.Notify(Guid.NewGuid().GetHashCode(), notificationBuilder.Build());
        }
    }
}
