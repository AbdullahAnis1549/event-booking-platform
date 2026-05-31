using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication4.Models;

namespace WebApplication4.Controllers
{
    public class HomeController : Controller
    {
        private readonly Data.ApplicationDbContext _context;

        public HomeController(Data.ApplicationDbContext context)
        {
            _context = context;
        }


        public async Task<IActionResult> Index(string search, string category, decimal? minPrice, decimal? maxPrice, string location, string dateFilter)
        {
            // Pass user info to view if logged in
            if (HttpContext.Session.GetString("UserId") != null)
            {
                ViewBag.UserName = HttpContext.Session.GetString("UserName");
                ViewBag.UserEmail = HttpContext.Session.GetString("UserEmail");
                ViewBag.UserImageUrl = HttpContext.Session.GetString("UserImageUrl");
            }

            // --- Fetch Hero Images ---
            var heroImages = await _context.HeroImages
                .Where(h => h.IsActive)
                .OrderBy(h => h.DisplayOrder)
                .ToListAsync();

            // Seed default images if none exist (Optional, for demo purposes)
            // Seed default images if none exist (Persist them to DB so they show in Admin Panel)
            if (!heroImages.Any())
            {
                var defaults = new List<HeroImage>
                {
                    new HeroImage { ImageUrl = "https://images.unsplash.com/photo-1519167758481-83f550bb49b3?ixlib=rb-1.2.1&auto=format&fit=crop&w=1350&q=80", Title = "Elite Event Experiences", Subtitle = "The ultimate destination for Weddings, Birthdays, and Corporate Gala Events.", DisplayOrder = 1, IsActive = true },
                    new HeroImage { ImageUrl = "https://images.unsplash.com/photo-1469334031218-e382a71b716b?ixlib=rb-1.2.1&auto=format&fit=crop&w=1350&q=80", Title = "Unforgettable Moments", Subtitle = "Crafting memories that last a lifetime.", DisplayOrder = 2, IsActive = true },
                    new HeroImage { ImageUrl = "https://images.unsplash.com/photo-1511795409834-ef04bbd61622?ixlib=rb-1.2.1&auto=format&fit=crop&w=1350&q=80", Title = "Corporate Excellence", Subtitle = "Professional events delivered with precision.", DisplayOrder = 3, IsActive = true }
                };
                
                _context.HeroImages.AddRange(defaults);
                await _context.SaveChangesAsync();
                
                heroImages = defaults;
            }

            // --- Base Query ---
            var baseQuery = _context.Events.Include(e => e.Organizer).Where(e => e.IsApproved);

            // Apply Filters (Common logic)
            if (!string.IsNullOrEmpty(search))
                baseQuery = baseQuery.Where(e => e.Title.Contains(search) || e.Description.Contains(search));
            
            if (!string.IsNullOrEmpty(category) && category != "All")
                baseQuery = baseQuery.Where(e => e.Category == category);

            if (minPrice.HasValue)
                baseQuery = baseQuery.Where(e => e.Price >= minPrice.Value);

            if (maxPrice.HasValue)
                baseQuery = baseQuery.Where(e => e.Price <= maxPrice.Value);

            if (!string.IsNullOrEmpty(location))
                baseQuery = baseQuery.Where(e => e.Location.Contains(location));

            // --- Split into Upcoming vs Successful ---
            
            // Upcoming Logic - Check both date and time
            var upcomingQuery = baseQuery.Where(e => e.EventDate >= DateTime.Now);
            
            // Date Filter only applies to Upcoming usually, or we can apply it generally. 
            // For now, let's respect the dateFilter for the "Upcoming" list if provided.
            if (!string.IsNullOrEmpty(dateFilter))
            {
                var today = DateTime.Today;
                if (dateFilter == "Today")
                    upcomingQuery = upcomingQuery.Where(e => e.EventDate.Date == today);
                else if (dateFilter == "ThisWeekend")
                {
                    var nextFriday = today.AddDays(5 - (int)today.DayOfWeek);
                    var nextSunday = nextFriday.AddDays(2);
                    upcomingQuery = upcomingQuery.Where(e => e.EventDate.Date >= nextFriday && e.EventDate.Date <= nextSunday);
                }
                else if (dateFilter == "Upcoming")
                    upcomingQuery = upcomingQuery.Where(e => e.EventDate >= DateTime.Now);
            }

            var upcomingEvents = await upcomingQuery.OrderBy(e => e.EventDate).ToListAsync();

            // Successful Logic (Past Events)
            // Filter ONLY by manually marked successful events as per user request for strict control
            var successfulEvents = await baseQuery
                .Where(e => e.IsSuccessful)
                .OrderByDescending(e => e.EventDate)
                .Take(6)
                .ToListAsync();

            var viewModel = new WebApplication4.Models.ViewModels.HomeViewModel
            {
                UpcomingEvents = upcomingEvents,
                SuccessfulEvents = successfulEvents,
                HeroImages = heroImages
            };
            
            // Pass filter values back to view
            ViewBag.Search = search;
            ViewBag.Category = category;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.Location = location;
            ViewBag.DateFilter = dateFilter;

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> SearchEvents(string search, string category, decimal? minPrice, decimal? maxPrice, string location, string dateFilter)
        {
            var query = _context.Events.Include(e => e.Organizer).Where(e => e.IsApproved);

            // Filtering logic
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(e => e.Title.Contains(search) || e.Description.Contains(search));
            }

            if (!string.IsNullOrEmpty(category) && category != "All")
            {
                query = query.Where(e => e.Category == category);
            }

            if (minPrice.HasValue)
            {
                query = query.Where(e => e.Price >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                query = query.Where(e => e.Price <= maxPrice.Value);
            }

            if (!string.IsNullOrEmpty(location))
            {
                query = query.Where(e => e.Location.Contains(location));
            }

            if (!string.IsNullOrEmpty(dateFilter))
            {
                var today = DateTime.Today;
                if (dateFilter == "Today")
                {
                    query = query.Where(e => e.EventDate.Date == today);
                }
                else if (dateFilter == "ThisWeekend")
                {
                    var nextFriday = today.AddDays(5 - (int)today.DayOfWeek);
                    var nextSunday = nextFriday.AddDays(2);
                    query = query.Where(e => e.EventDate.Date >= nextFriday && e.EventDate.Date <= nextSunday);
                }
                else if (dateFilter == "Upcoming")
                {
                    query = query.Where(e => e.EventDate >= DateTime.Now);
                }
            }
            else
            {
                query = query.Where(e => e.EventDate >= DateTime.Now);
            }

            var events = await query.OrderBy(e => e.EventDate).ToListAsync();

            return Json(events.Select(e => new
            {
                id = e.Id,
                title = e.Title,
                description = e.Description,
                eventDate = e.EventDate.ToString("MMM dd, yyyy"),
                location = e.Location,
                price = e.Price,
                availableSeats = e.AvailableSeats,
                imageUrl = e.ImageUrl,
                category = e.Category
            }));
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
