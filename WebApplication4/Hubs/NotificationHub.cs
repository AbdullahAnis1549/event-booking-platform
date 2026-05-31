using Microsoft.AspNetCore.SignalR;

namespace WebApplication4.Hubs
{
    public class NotificationHub : Hub
    {
        // Hub methods for broadcasting updates
        
        public async Task SendSeatUpdate(int eventId, int availableSeats)
        {
            await Clients.All.SendAsync("ReceiveSeatUpdate", eventId, availableSeats);
        }

        public async Task NotifyAdminNewEvent(string eventTitle)
        {
            await Clients.All.SendAsync("ReceiveAdminNotification", $"New event submitted: {eventTitle}");
        }
    }
}
