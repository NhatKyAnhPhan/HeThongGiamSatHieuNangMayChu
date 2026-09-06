using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nhom3_NoSQL.Backend.Interfaces;
using Nhom3_NoSQL.Backend.Models;

namespace Nhom3_NoSQL.Backend.Services
{
    //Cài đặt mẫu: log ra Debug. Có thể mở rộng gửi email/SMS/popup
    public class NotificationService : INotificationService
    {
        public Task NotifyAsync(AlertEvent alertEvent)
        {
            System.Diagnostics.Debug.WriteLine($"[ALERT-{alertEvent.Severity}] {alertEvent.Message}");
            // TODO: gửi email qua SMTP, hoặc show BalloonTip qua NotifyIcon trong Frontend
            return Task.CompletedTask;
        }
    }
}