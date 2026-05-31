using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication4.Data;

namespace WebApplication4.Controllers
{
    public class OrganizerController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OrganizerController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Dashboard()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            var userRole = HttpContext.Session.GetString("UserRole")?.ToLower();

            if (string.IsNullOrEmpty(userIdStr) || userRole != "organizer")
            {
                return RedirectToAction("Login", "Account");
            }

            var userId = int.Parse(userIdStr);

            // Fetch events
            var myEvents = await _context.Events
                .Where(e => e.OrganizerId == userId)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();

            var eventIds = myEvents.Select(e => e.Id).ToList();
            
            // Get all paid bookings for these events
            var paidBookings = await _context.Bookings
                .Where(b => eventIds.Contains(b.EventId) && (b.Status == "Paid" || b.Status == "Confirmed"))
                .ToListAsync();

            // Create ViewModel
            var viewModel = new WebApplication4.Models.ViewModels.OrganizerDashboardViewModel
            {
                MyEvents = myEvents,
                TotalEvents = myEvents.Count,
                ApprovedEvents = myEvents.Count(e => e.IsApproved),
                PendingEvents = myEvents.Count(e => !e.IsApproved),
                TotalRevenue = paidBookings.Sum(b => b.TotalAmount),
                TotalTicketsSold = paidBookings.Sum(b => b.NumberOfTickets),
                AverageRevenuePerEvent = myEvents.Any() ? (paidBookings.Sum(b => b.TotalAmount) / myEvents.Count) : 0
            };

            // Monthly Revenue Data for Chart
            var monthlyRevenue = paidBookings
                .GroupBy(b => b.BookingDate.ToString("MMM"))
                .Select(g => new { Month = g.Key, Amount = g.Sum(b => b.TotalAmount) })
                .ToList();

            viewModel.MonthlyRevenueLabels = monthlyRevenue.Select(m => m.Month).ToArray();
            viewModel.MonthlyRevenueData = monthlyRevenue.Select(m => m.Amount).ToArray();

            return View(viewModel);
        }
    }
}
