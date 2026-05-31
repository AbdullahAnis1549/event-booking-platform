using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using WebApplication4.Data;
using WebApplication4.Models;

namespace WebApplication4.Controllers
{
    public class BookingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly Microsoft.AspNetCore.SignalR.IHubContext<WebApplication4.Hubs.NotificationHub> _hubContext;
        private readonly WebApplication4.Services.EmailService _emailService;

        public BookingController(ApplicationDbContext context, 
            Microsoft.AspNetCore.SignalR.IHubContext<WebApplication4.Hubs.NotificationHub> hubContext,
            WebApplication4.Services.EmailService emailService)
        {
            _context = context;
            _hubContext = hubContext;
            _emailService = emailService;
        }

        // POST: Booking/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(int eventId, int tickets)
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
            {
                TempData["ErrorMessage"] = "Please login to book tickets.";
                return RedirectToAction("Login", "Account");
            }

            int userId = int.Parse(userIdStr);
            var @event = await _context.Events.FindAsync(eventId);

            if (@event == null) return NotFound();

            if (!@event.IsApproved || @event.EventDate < DateTime.Now)
            {
                TempData["ErrorMessage"] = "This event is not available for booking.";
                return RedirectToAction("Details", "Event", new { id = eventId });
            }

            if (@event.AvailableSeats < tickets)
            {
                TempData["ErrorMessage"] = "Not enough seats available.";
                return RedirectToAction("Details", "Event", new { id = eventId });
            }

            // Create Booking in Pending state
            var booking = new Booking
            {
                UserId = userId,
                EventId = eventId,
                NumberOfTickets = tickets,
                TotalAmount = @event.Price * tickets,
                BookingDate = DateTime.UtcNow,
                Status = "Pending"
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            // Redirect to Checkout/Payment page
            return RedirectToAction("Checkout", new { id = booking.Id });
        }

        // GET: Booking/Checkout/5
        public async Task<IActionResult> Checkout(int id)
        {
            var booking = await _context.Bookings
                .Include(b => b.Event)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null) return NotFound();
            if (booking.Status != "Pending") return RedirectToAction("MyBookings");

            return View(booking);
        }

        // POST: Booking/ProcessPayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(int bookingId)
        {
            var booking = await _context.Bookings
                .Include(b => b.Event)
                .Include(b => b.User)
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking == null) return NotFound();
            if (booking.Status != "Pending") return BadRequest();

            var @event = booking.Event;
            if (@event == null) return NotFound();

            // Check seats again just in case
            if (@event.AvailableSeats < booking.NumberOfTickets)
            {
                TempData["ErrorMessage"] = "Sorry, seats are no longer available.";
                booking.Status = "Cancelled";
                await _context.SaveChangesAsync();
                return RedirectToAction("Index", "Home");
            }

            // Simulate Payment Success
            booking.Status = "Paid";
            booking.TransactionId = "TXN-" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
            
            // Deduct seats now that payment is "successful"
            @event.AvailableSeats -= booking.NumberOfTickets;

            await _context.SaveChangesAsync();

            // Send Email Notification
            try 
            {
                if (booking.User != null)
                {
                    _emailService.SendBookingConfirmationEmail(
                        booking.User.Email,
                        booking.User.Name,
                        @event.Title,
                        booking.NumberOfTickets,
                        booking.TotalAmount,
                        booking.TransactionId
                    );
                }
            }
            catch (Exception)
            {
                // Log email failure but don't stop the flow
            }

            // Notify all users about seat update
            await _hubContext.Clients.All.SendAsync("ReceiveSeatUpdate", @event.Id, @event.AvailableSeats);

            TempData["SuccessMessage"] = "Payment successful! Your tickets are confirmed and a confirmation email has been sent.";
            return RedirectToAction("MyBookings");
        }

        // GET: Booking/MyBookings
        public async Task<IActionResult> MyBookings()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login", "Account");

            int userId = int.Parse(userIdStr);

            var myBookings = await _context.Bookings
                .Include(b => b.Event)
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.BookingDate)
                .ToListAsync();

            return View(myBookings);
        }
    }
}
