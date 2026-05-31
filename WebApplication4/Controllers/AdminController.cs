using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication4.Data;

namespace WebApplication4.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly WebApplication4.Services.CloudinaryService _cloudinaryService;

        public AdminController(ApplicationDbContext context, WebApplication4.Services.CloudinaryService cloudinaryService)
        {
            _context = context;
            _cloudinaryService = cloudinaryService;
        }

        public async Task<IActionResult> Dashboard()
        {
            var userRole = HttpContext.Session.GetString("UserRole")?.ToLower();
            if (userRole != "admin")
            {
                return RedirectToAction("Login", "Account");
            }

            ViewBag.TotalUsers = await _context.Users.CountAsync();
            ViewBag.TotalEvents = await _context.Events.CountAsync();
            ViewBag.TotalBookings = await _context.Bookings.CountAsync();
            ViewBag.PendingOrganizerRequests = await _context.OrganizerRequests.CountAsync(r => r.Status == "Pending");
            
            // Calculate Total Revenue
            ViewBag.TotalRevenue = await _context.Bookings
                .Where(b => b.Status == "Paid" || b.Status == "Confirmed")
                .SumAsync(b => b.TotalAmount);

            // Get monthly stats for the last 6 months
            var startDate = DateTime.UtcNow.AddMonths(-5);
            startDate = new DateTime(startDate.Year, startDate.Month, 1);

            var monthlyStats = await _context.Bookings
                .Where(b => (b.Status == "Paid" || b.Status == "Confirmed") && b.BookingDate >= startDate)
                .GroupBy(b => new { b.BookingDate.Year, b.BookingDate.Month })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Revenue = g.Sum(b => b.TotalAmount),
                    Bookings = g.Count()
                })
                .ToListAsync();

            // Prepare labels and data for Chart.js
            var labels = new List<string>();
            var revenueData = new List<decimal>();
            var bookingsData = new List<int>();

            for (int i = 0; i < 6; i++)
            {
                var currentMonth = startDate.AddMonths(i);
                labels.Add(currentMonth.ToString("MMMM"));
                
                var stat = monthlyStats.FirstOrDefault(s => s.Year == currentMonth.Year && s.Month == currentMonth.Month);
                revenueData.Add(stat?.Revenue ?? 0);
                bookingsData.Add(stat?.Bookings ?? 0);
            }

            ViewBag.ChartLabels = labels;
            ViewBag.ChartRevenue = revenueData;
            ViewBag.ChartBookings = bookingsData;

            var requests = await _context.Events
                .Include(e => e.Organizer)
                .Where(e => !e.IsApproved || e.IsDeleteRequested)
                .OrderBy(e => e.IsDeleteRequested)
                .ToListAsync();

            return View(requests);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveEvent(int id)
        {
            var userRole = HttpContext.Session.GetString("UserRole")?.ToLower();
            if (userRole != "admin") return Unauthorized();

            var @event = await _context.Events.FindAsync(id);
            if (@event != null)
            {
                @event.IsApproved = true;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Event approved successfully!";
            }
            return RedirectToAction(nameof(Dashboard));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectEvent(int id)
        {
            var userRole = HttpContext.Session.GetString("UserRole")?.ToLower();
            if (userRole != "admin") return Unauthorized();

            var @event = await _context.Events.FindAsync(id);
            if (@event != null)
            {
                _context.Events.Remove(@event);
                await _context.SaveChangesAsync();
                TempData["ErrorMessage"] = "Event rejected and removed.";
            }
            return RedirectToAction(nameof(Dashboard));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmDeleteRequest(int id)
        {
            var userRole = HttpContext.Session.GetString("UserRole")?.ToLower();
            if (userRole != "admin") return Unauthorized();

            var @event = await _context.Events.FindAsync(id);
            if (@event != null)
            {
                _context.Events.Remove(@event);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Event permanently deleted as per request.";
            }
            return RedirectToAction(nameof(Dashboard));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectDeleteRequest(int id)
        {
            var userRole = HttpContext.Session.GetString("UserRole")?.ToLower();
            if (userRole != "admin") return Unauthorized();

            var @event = await _context.Events.FindAsync(id);
            if (@event != null)
            {
                @event.IsDeleteRequested = false;
                await _context.SaveChangesAsync();
                TempData["InfoMessage"] = "Deletion request rejected. Event kept active.";
            }
            return RedirectToAction(nameof(Dashboard));
        }

        public async Task<IActionResult> ManageUsers()
        {
            var userRole = HttpContext.Session.GetString("UserRole")?.ToLower();
            if (userRole != "admin") return RedirectToAction("Login", "Account");

            var users = await _context.Users.OrderByDescending(u => u.Id).ToListAsync();
            return View(users);
        }

        public async Task<IActionResult> Events()
        {
            var userRole = HttpContext.Session.GetString("UserRole")?.ToLower();
            if (userRole != "admin") return RedirectToAction("Login", "Account");

            var events = await _context.Events
                .Include(e => e.Organizer)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();
            return View(events);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var userRole = HttpContext.Session.GetString("UserRole")?.ToLower();
            if (userRole != "admin") return Unauthorized();

            var user = await _context.Users.FindAsync(id);
            if (user != null && user.UserRole != "admin")
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "User deleted successfully!";
            }
            return RedirectToAction(nameof(ManageUsers));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            var userRole = HttpContext.Session.GetString("UserRole")?.ToLower();
            if (userRole != "admin") return Unauthorized();

            var evt = await _context.Events.FindAsync(id);
            if (evt != null)
            {
                _context.Events.Remove(evt);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Event deleted successfully!";
            }
            return RedirectToAction(nameof(Events));
        }



        public async Task<IActionResult> ManageHeroImages()
        {
            var userRole = HttpContext.Session.GetString("UserRole")?.ToLower();
            if (userRole != "admin") return RedirectToAction("Login", "Account");

            var heroes = await _context.HeroImages.OrderBy(h => h.DisplayOrder).ToListAsync();
            return View(heroes);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddHeroImage(IFormFile imageFile, string title, string subtitle, int displayOrder)
        {
            var userRole = HttpContext.Session.GetString("UserRole")?.ToLower();
            if (userRole != "admin") return Unauthorized();

            if (imageFile != null && imageFile.Length > 0)
            {
                try
                {
                    string imageUrl = await _cloudinaryService.UploadImageAsync(imageFile, "hero-carousel");
                    
                    if (displayOrder <= 0)
                    {
                        displayOrder = (await _context.HeroImages.CountAsync()) + 1;
                    }

                    var hero = new WebApplication4.Models.HeroImage
                    {
                        ImageUrl = imageUrl,
                        Title = title ?? "New Event",
                        Subtitle = subtitle ?? "Join us now",
                        DisplayOrder = displayOrder,
                        IsActive = true
                    };

                    _context.HeroImages.Add(hero);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Hero image uploaded and added successfully!";
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "Upload failed: " + ex.Message;
                }
            }
            else
            {
                TempData["ErrorMessage"] = "Please select an image file.";
            }

            return RedirectToAction(nameof(ManageHeroImages));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteHeroImage(int id)
        {
            var userRole = HttpContext.Session.GetString("UserRole")?.ToLower();
            if (userRole != "admin") return Unauthorized();

            var hero = await _context.HeroImages.FindAsync(id);
            if (hero != null)
            {
                _context.HeroImages.Remove(hero);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Hero image deleted!";
            }
            return RedirectToAction(nameof(ManageHeroImages));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleSuccessfulEvent(int id)
        {
            var userRole = HttpContext.Session.GetString("UserRole")?.ToLower();
            if (userRole != "admin") return Unauthorized();

            var evt = await _context.Events.FindAsync(id);
            if (evt != null)
            {
                evt.IsSuccessful = !evt.IsSuccessful;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Event '{(evt.IsSuccessful ? "Marked as Successful" : "Removed from Successful")}'";
            }
            return RedirectToAction(nameof(Events));
        }

        // About Content Management
        public async Task<IActionResult> ManageAbout()
        {
            var userRole = HttpContext.Session.GetString("UserRole")?.ToLower();
            if (userRole != "admin") return RedirectToAction("Login", "Account");

            var aboutContents = await _context.AboutContents.OrderBy(a => a.DisplayOrder).ToListAsync();
            return View(aboutContents);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAboutContent(string sectionTitle, string content, IFormFile imageFile, int displayOrder)
        {
            var userRole = HttpContext.Session.GetString("UserRole")?.ToLower();
            if (userRole != "admin") return Unauthorized();

            if (!string.IsNullOrEmpty(sectionTitle) && !string.IsNullOrEmpty(content))
            {
                string imageUrl = null;
                if (imageFile != null && imageFile.Length > 0)
                {
                    try
                    {
                        imageUrl = await _cloudinaryService.UploadImageAsync(imageFile, "about-page");
                    }
                    catch (Exception ex)
                    {
                        TempData["ErrorMessage"] = "Image upload failed: " + ex.Message;
                        // We might still want to add the content even if image fails, 
                        // but usually it's better to report error.
                    }
                }

                var aboutContent = new WebApplication4.Models.AboutContent
                {
                    SectionTitle = sectionTitle,
                    Content = content,
                    ImageUrl = imageUrl,
                    DisplayOrder = displayOrder,
                    IsActive = true
                };

                _context.AboutContents.Add(aboutContent);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "About content added successfully!";
            }

            return RedirectToAction(nameof(ManageAbout));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAboutContent(int id, string sectionTitle, string content, IFormFile imageFile, int displayOrder, bool isActive)
        {
            var userRole = HttpContext.Session.GetString("UserRole")?.ToLower();
            if (userRole != "admin") return Unauthorized();

            var aboutContent = await _context.AboutContents.FindAsync(id);
            if (aboutContent != null)
            {
                if (imageFile != null && imageFile.Length > 0)
                {
                    try
                    {
                        // Optional: delete old image from Cloudinary
                        if (!string.IsNullOrEmpty(aboutContent.ImageUrl))
                        {
                            await _cloudinaryService.DeleteImageAsync(aboutContent.ImageUrl);
                        }
                        aboutContent.ImageUrl = await _cloudinaryService.UploadImageAsync(imageFile, "about-page");
                    }
                    catch (Exception ex)
                    {
                        TempData["ErrorMessage"] = "Image upload failed: " + ex.Message;
                    }
                }

                aboutContent.SectionTitle = sectionTitle;
                aboutContent.Content = content;
                aboutContent.DisplayOrder = displayOrder;
                aboutContent.IsActive = isActive;
                aboutContent.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "About content updated successfully!";
            }

            return RedirectToAction(nameof(ManageAbout));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAboutContent(int id)
        {
            var userRole = HttpContext.Session.GetString("UserRole")?.ToLower();
            if (userRole != "admin") return Unauthorized();

            var aboutContent = await _context.AboutContents.FindAsync(id);
            if (aboutContent != null)
            {
                _context.AboutContents.Remove(aboutContent);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "About content deleted successfully!";
            }

            return RedirectToAction(nameof(ManageAbout));
        }

        // Contact Messages Management
        public async Task<IActionResult> ContactMessages()
        {
            var userRole = HttpContext.Session.GetString("UserRole")?.ToLower();
            if (userRole != "admin") return RedirectToAction("Login", "Account");

            var messages = await _context.ContactMessages
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();

            return View(messages);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkMessageAsRead(int id)
        {
            var userRole = HttpContext.Session.GetString("UserRole")?.ToLower();
            if (userRole != "admin") return Unauthorized();

            var message = await _context.ContactMessages.FindAsync(id);
            if (message != null)
            {
                message.IsRead = !message.IsRead;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(ContactMessages));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteContactMessage(int id)
        {
            var userRole = HttpContext.Session.GetString("UserRole")?.ToLower();
            if (userRole != "admin") return Unauthorized();

            var message = await _context.ContactMessages.FindAsync(id);
            if (message != null)
            {
                _context.ContactMessages.Remove(message);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Message deleted successfully!";
            }

            return RedirectToAction(nameof(ContactMessages));
        }
        public async Task<IActionResult> OrganizerRequests()
        {
            var userRole = HttpContext.Session.GetString("UserRole")?.ToLower();
            if (userRole != "admin") return RedirectToAction("Login", "Account");

            var requests = await _context.OrganizerRequests
                .Include(r => r.User)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            return View(requests);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveOrganizerRequest(int id)
        {
            var userRole = HttpContext.Session.GetString("UserRole")?.ToLower();
            if (userRole != "admin") return Unauthorized();

            var request = await _context.OrganizerRequests.Include(r => r.User).FirstOrDefaultAsync(r => r.Id == id);
            if (request != null && request.Status == "Pending")
            {
                request.Status = "Approved";
                if (request.User != null)
                {
                    request.User.UserRole = "organizer";
                    _context.Users.Update(request.User);
                }
                
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"User {request.User?.Name} is now an Organizer!";
            }
            return RedirectToAction(nameof(OrganizerRequests));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectOrganizerRequest(int id, string adminComment)
        {
            var userRole = HttpContext.Session.GetString("UserRole")?.ToLower();
            if (userRole != "admin") return Unauthorized();

            var request = await _context.OrganizerRequests.FindAsync(id);
            if (request != null && request.Status == "Pending")
            {
                request.Status = "Rejected";
                request.AdminComment = adminComment;
                await _context.SaveChangesAsync();
                TempData["InfoMessage"] = "Organizer request rejected.";
            }
            return RedirectToAction(nameof(OrganizerRequests));
        }
    }
}
