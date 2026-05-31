using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using WebApplication4.Data;
using WebApplication4.Models;
using WebApplication4.Services;

namespace WebApplication4.Controllers
{
    public class EventController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly CloudinaryService _cloudinaryService;
        private readonly Microsoft.AspNetCore.SignalR.IHubContext<WebApplication4.Hubs.NotificationHub> _hubContext;

        public EventController(ApplicationDbContext context, CloudinaryService cloudinaryService, Microsoft.AspNetCore.SignalR.IHubContext<WebApplication4.Hubs.NotificationHub> hubContext)
        {
            _context = context;
            _cloudinaryService = cloudinaryService;
            _hubContext = hubContext;
        }

        // GET: Event
        // Public page showing all events (Past & Future), sorted by Date (Newest/Latest first)
        public async Task<IActionResult> Index()
        {
            var events = await _context.Events
                .Where(e => e.IsApproved)
                .Include(e => e.Organizer)
                .OrderByDescending(e => e.EventDate) // Shows Future dates first (Latest), then Past dates
                .ToListAsync();
            return View(events);
        }

        // GET: Event/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var @event = await _context.Events
                .Include(e => e.Organizer)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (@event == null) return NotFound();

            // Load reviews
            var reviews = await _context.Reviews
                .Include(r => r.User)
                .Where(r => r.EventId == id)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            ViewBag.Reviews = reviews;
            ViewBag.AverageRating = reviews.Any() ? (double)reviews.Average(r => r.Rating) : 0.0;

            // Check if user has booked this event (for review form)
            var userIdStr = HttpContext.Session.GetString("UserId");
            bool hasBooked = false;
            if (!string.IsNullOrEmpty(userIdStr))
            {
                int userId = int.Parse(userIdStr);
                hasBooked = await _context.Bookings.AnyAsync(b => b.EventId == id && b.UserId == userId && (b.Status == "Paid" || b.Status == "Confirmed"));
                ViewBag.UserAlreadyReviewed = await _context.Reviews.AnyAsync(r => r.EventId == id && r.UserId == userId);
            }
            ViewBag.HasBooked = hasBooked;

            return View(@event);
        }

        // GET: Event/Create
        public IActionResult Create()
        {
            if (HttpContext.Session.GetString("UserRole") != "organizer")
            {
                return RedirectToAction("Login", "Account");
            }
            return View();
        }

        // POST: Event/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Event @event, IFormFile? imageFile)
        {
            if (HttpContext.Session.GetString("UserRole") != "organizer")
            {
                return RedirectToAction("Login", "Account");
            }

            // Set OrganizerId from session
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            {
                return RedirectToAction("Login", "Account");
            }

            @event.OrganizerId = userId;
            @event.AvailableSeats = @event.TotalSeats;
            @event.IsApproved = false; // Requires Admin approval

            if (imageFile != null && imageFile.Length > 0)
            {
                @event.ImageUrl = await _cloudinaryService.UploadImageAsync(imageFile);
            }

            // Manual validation check since model state might be tricky with navigation properties
            if (string.IsNullOrEmpty(@event.Title) || string.IsNullOrEmpty(@event.Description) || @event.TotalSeats <= 0)
            {
                ModelState.AddModelError("", "Please fill all required fields correctly.");
                return View(@event);
            }

            _context.Add(@event);
            await _context.SaveChangesAsync();

            // Notify Admin via SignalR
            await _hubContext.Clients.All.SendAsync("ReceiveAdminNotification", $"New event submitted for approval: {@event.Title}");

            TempData["SuccessMessage"] = "Event created successfully! Waiting for Admin approval.";
            return RedirectToAction("Dashboard", "Organizer");
        }

        // GET: Event/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var @event = await _context.Events.FindAsync(id);
            if (@event == null) return NotFound();

            // Authorization check
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr) || @event.OrganizerId != int.Parse(userIdStr))
            {
                return Unauthorized();
            }

            return View(@event);
        }

        // POST: Event/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Event @event, IFormFile? imageFile)
        {
            if (id != @event.Id) return NotFound();

            var existingEvent = await _context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
            if (existingEvent == null) return NotFound();

            // Authorization check
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr) || existingEvent.OrganizerId != int.Parse(userIdStr))
            {
                return Unauthorized();
            }

            try
            {
                @event.OrganizerId = existingEvent.OrganizerId;
                @event.IsApproved = existingEvent.IsApproved; // Keep approval status
                
                if (imageFile != null && imageFile.Length > 0)
                {
                    @event.ImageUrl = await _cloudinaryService.UploadImageAsync(imageFile);
                }
                else
                {
                    @event.ImageUrl = existingEvent.ImageUrl;
                }

                _context.Update(@event);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Event updated successfully!";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EventExists(@event.Id)) return NotFound();
                else throw;
            }
            return RedirectToAction("Dashboard", "Organizer");
        }

        // GET: Event/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var @event = await _context.Events
                .Include(e => e.Organizer)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (@event == null) return NotFound();

            // Authorization check
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr) || @event.OrganizerId != int.Parse(userIdStr))
            {
                return Unauthorized();
            }

            return View(@event);
        }

        // POST: Event/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var @event = await _context.Events.FindAsync(id);
            if (@event == null) return NotFound();

            // Authorization check
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr) || @event.OrganizerId != int.Parse(userIdStr))
            {
                return Unauthorized();
            }

            // Instead of deleting, we request deletion from Admin
            @event.IsDeleteRequested = true;
            await _context.SaveChangesAsync();

            // Notify Admin via SignalR
            await _hubContext.Clients.All.SendAsync("ReceiveAdminNotification", $"Delete Request: Event '{@event.Title}' has been requested for removal by Organizer.");

            TempData["SuccessMessage"] = "Request sent to Admin! The event will be removed once approved.";
            return RedirectToAction("Dashboard", "Organizer");
        }

        private bool EventExists(int id)
        {
            return _context.Events.Any(e => e.Id == id);
        }
    }
}
