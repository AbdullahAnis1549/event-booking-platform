using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication4.Data;
using WebApplication4.Models;

namespace WebApplication4.Controllers
{
    public class ReviewController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReviewController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitReview(int eventId, int rating, string comment)
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
            {
                return RedirectToAction("Login", "Account");
            }

            int userId = int.Parse(userIdStr);

            // Check if user has booked this event and it's confirmed/paid
            var hasBooked = await _context.Bookings.AnyAsync(b => b.EventId == eventId && b.UserId == userId && (b.Status == "Paid" || b.Status == "Confirmed"));

            if (!hasBooked)
            {
                TempData["ErrorMessage"] = "You can only review events you have booked.";
                return RedirectToAction("Details", "Event", new { id = eventId });
            }

            // Check if user already reviewed
            var existingReview = await _context.Reviews.FirstOrDefaultAsync(r => r.EventId == eventId && r.UserId == userId);
            if (existingReview != null)
            {
                TempData["ErrorMessage"] = "You have already reviewed this event.";
                return RedirectToAction("Details", "Event", new { id = eventId });
            }

            var review = new Review
            {
                EventId = eventId,
                UserId = userId,
                Rating = rating,
                Comment = comment,
                CreatedAt = DateTime.UtcNow
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Thank you for your review!";
            return RedirectToAction("Details", "Event", new { id = eventId });
        }
    }
}
