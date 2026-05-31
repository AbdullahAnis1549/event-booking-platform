using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication4.Data;
using WebApplication4.Models;
using WebApplication4.Services;

namespace WebApplication4.Controllers
{
    public class ContactController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;
        private readonly IConfiguration _configuration;

        public ContactController(ApplicationDbContext context, EmailService emailService, IConfiguration configuration)
        {
            _context = context;
            _emailService = emailService;
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitContact([Bind("Name,Email,Subject,Message")] ContactMessage model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Save to database
                    _context.ContactMessages.Add(model);
                    await _context.SaveChangesAsync();

                    // Send email to admin
                    var adminEmail = _configuration["EmailSettings:FromEmail"] ?? "abdullahanis592@gmail.com";
                    var emailSubject = $"New Contact Message from {model.Name}";
                    var emailBody = $@"
                        <html>
                        <body style='font-family: Arial, sans-serif; padding: 20px;'>
                            <h2 style='color: #333;'>New Contact Message</h2>
                            <div style='background: #f5f5f5; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                                <p><strong>Name:</strong> {model.Name}</p>
                                <p><strong>Email:</strong> {model.Email}</p>
                                <p><strong>Subject:</strong> {model.Subject ?? "No Subject"}</p>
                                <p><strong>Message:</strong></p>
                                <p style='background: white; padding: 10px; border-left: 4px solid #007bff;'>{model.Message}</p>
                                <p><strong>Date:</strong> {model.CreatedAt.ToString("MMM dd, yyyy HH:mm")}</p>
                            </div>
                        </body>
                        </html>";

                    _emailService.SendEmail(adminEmail, emailSubject, emailBody);

                    TempData["SuccessMessage"] = "Thank you for contacting us! We'll get back to you soon.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "An error occurred while sending your message. Please try again later.";
                    return View("Index", model);
                }
            }

            return View("Index", model);
        }
    }
}
