using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication4.Data;
using WebApplication4.Models;
using WebApplication4.Services;
using BCrypt.Net;

namespace WebApplication4.Controllers
{
    /// <summary>
    /// Account controller handling user authentication operations
    /// </summary>
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;
        private readonly CloudinaryService _cloudinaryService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            ApplicationDbContext context,
            EmailService emailService,
            CloudinaryService cloudinaryService,
            ILogger<AccountController> logger)
        {
            _context = context;
            _emailService = emailService;
            _cloudinaryService = cloudinaryService;
            _logger = logger;
        }

        #region Registration

        /// <summary>
        /// GET: Display registration form
        /// </summary>
        [HttpGet]
        public IActionResult Register()
        {
            // If user is already logged in, redirect to home
            if (IsUserLoggedIn())
            {
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        /// <summary>
        /// POST: Handle user registration
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(User model, IFormFile? imageFile)
        {
            try
            {
                // Validate model
                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                // Check if email already exists
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == model.Email.ToLower());

                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", "Email is already registered.");
                    return View(model);
                }

                // Upload image to Cloudinary if provided
                if (imageFile != null && imageFile.Length > 0)
                {
                    try
                    {
                        model.ImageUrl = await _cloudinaryService.UploadImageAsync(imageFile);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error uploading image");
                        ModelState.AddModelError("ImageUrl", "Failed to upload image. Please try again.");
                        return View(model);
                    }
                }

                // Generate verification code (6 digits)
                var random = new Random();
                int verificationCode = random.Next(100000, 999999);

                // Hash password using BCrypt
                model.Password = BCrypt.Net.BCrypt.HashPassword(model.Password);

                // Set verification details
                model.VerifyStatus = false;
                model.VerifyCode = verificationCode;
                model.VerifyCodeExpDate = DateTime.UtcNow.AddMinutes(15); // Expires in 15 minutes
                // Default to 'user' if null or empty, though the form should provide it
                model.UserRole = !string.IsNullOrEmpty(model.UserRole) ? model.UserRole : "user";

                // Save user to database
                _context.Users.Add(model);
                await _context.SaveChangesAsync();

                // Send verification email
                try
                {
                    _emailService.SendVerificationEmail(model.Email, verificationCode);
                    TempData["SuccessMessage"] = "Registration successful! Please check your email for verification code.";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send verification email");
                    TempData["WarningMessage"] = "Registration successful, but verification email could not be sent. Please contact support.";
                }

                // Store email in TempData for verification page
                TempData["UserEmail"] = model.Email;

                return RedirectToAction("VerifyUser");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration");
                ModelState.AddModelError("", "An error occurred during registration. Please try again.");
                return View(model);
            }
        }

        #endregion

        #region Email Verification

        /// <summary>
        /// GET: Display email verification form
        /// </summary>
        [HttpGet]
        public IActionResult VerifyUser()
        {
            // If user is already logged in, redirect to home
            if (IsUserLoggedIn())
            {
                return RedirectToAction("Index", "Home");
            }

            // Get email from TempData
            var email = TempData["UserEmail"]?.ToString();
            if (string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "Please register first.";
                return RedirectToAction("Register");
            }

            ViewBag.Email = email;
            return View();
        }

        /// <summary>
        /// POST: Verify user email with verification code
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyUser(string email, int verificationCode)
        {
            try
            {
                if (string.IsNullOrEmpty(email) || verificationCode == 0)
                {
                    ModelState.AddModelError("", "Email and verification code are required.");
                    ViewBag.Email = email;
                    return View();
                }

                // Find user by email
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

                if (user == null)
                {
                    ModelState.AddModelError("", "User not found.");
                    ViewBag.Email = email;
                    return View();
                }

                // Check if already verified
                if (user.VerifyStatus == true)
                {
                    TempData["InfoMessage"] = "Your email is already verified. Please login.";
                    return RedirectToAction("Login");
                }

                // Verify code
                if (user.VerifyCode != verificationCode)
                {
                    ModelState.AddModelError("", "Invalid verification code.");
                    ViewBag.Email = email;
                    return View();
                }

                // Check if code expired
                if (user.VerifyCodeExpDate < DateTime.UtcNow)
                {
                    ModelState.AddModelError("", "Verification code has expired. Please register again.");
                    ViewBag.Email = email;
                    return View();
                }

                // Update user verification status
                user.VerifyStatus = true;
                user.VerifyCode = null;
                user.VerifyCodeExpDate = null;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Email verified successfully! You can now login.";
                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during email verification");
                ModelState.AddModelError("", "An error occurred during verification. Please try again.");
                ViewBag.Email = email;
                return View();
            }
        }

        #endregion

        #region Login

        /// <summary>
        /// GET: Display login form
        /// </summary>
        [HttpGet]
        public IActionResult Login()
        {
            // If user is already logged in, redirect to home
            if (IsUserLoggedIn())
            {
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        /// <summary>
        /// POST: Handle user login
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password)
        {
            try
            {
                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                {
                    ModelState.AddModelError("", "Email and password are required.");
                    return View();
                }

                // Find user by email
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

                if (user == null)
                {
                    ModelState.AddModelError("", "Invalid email or password.");
                    return View();
                }

                // Verify password
                bool isPasswordValid = BCrypt.Net.BCrypt.Verify(password, user.Password);
                if (!isPasswordValid)
                {
                    ModelState.AddModelError("", "Invalid email or password.");
                    return View();
                }

                // Check if email is verified
                if (user.VerifyStatus != true)
                {
                    TempData["ErrorMessage"] = "Please verify your email first. Check your email for verification code.";
                    TempData["UserEmail"] = user.Email;
                    return RedirectToAction("VerifyUser");
                }

                // Set session variables
                HttpContext.Session.SetString("UserId", user.Id.ToString());
                HttpContext.Session.SetString("UserName", user.Name);
                HttpContext.Session.SetString("UserEmail", user.Email);
                HttpContext.Session.SetString("UserRole", user.UserRole ?? "user");
                HttpContext.Session.SetString("UserImageUrl", user.ImageUrl ?? "");

                TempData["SuccessMessage"] = $"Welcome back, {user.Name}!";

                // ROLE BASED REDIRECT
                if (user.UserRole != null && user.UserRole.ToLower() == "admin")
                {
                    return RedirectToAction("Dashboard", "Admin");
                }
                else if (user.UserRole != null && user.UserRole.ToLower() == "organizer")
                {
                    return RedirectToAction("Dashboard", "Organizer");
                }
                else
                {
                    return RedirectToAction("Index", "Home");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login");
                ModelState.AddModelError("", "An error occurred during login. Please try again.");
                return View();
            }
        }


        /// <summary>
        /// Logout user and clear session
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            TempData["SuccessMessage"] = "You have been logged out successfully.";
            return RedirectToAction("Login");
        }

        #endregion

        #region Forgot Password

        /// <summary>
        /// GET: Display forgot password form
        /// </summary>
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        /// <summary>
        /// POST: Handle forgot password request
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            try
            {
                if (string.IsNullOrEmpty(email))
                {
                    ModelState.AddModelError("", "Email is required.");
                    return View();
                }

                // Find user by email
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

                if (user == null)
                {
                    // Don't reveal if email exists for security
                    TempData["InfoMessage"] = "If the email exists, a password reset code has been sent.";
                    return RedirectToAction("Login");
                }

                // Generate reset code (alphanumeric, 8 characters)
                var random = new Random();
                const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
                string resetCode = new string(Enumerable.Repeat(chars, 8)
                    .Select(s => s[random.Next(s.Length)]).ToArray());

                // Set reset code and expiry (30 minutes)
                user.ForgotCode = resetCode;
                user.ForgotCodeExp = DateTime.UtcNow.AddMinutes(30);

                await _context.SaveChangesAsync();

                // Send password reset email
                try
                {
                    _emailService.SendPasswordResetEmail(user.Email, resetCode);
                    TempData["SuccessMessage"] = "Password reset code has been sent to your email.";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send password reset email");
                    TempData["ErrorMessage"] = "Failed to send reset code. Please try again later.";
                    return View();
                }

                // Store email in TempData for reset code page
                TempData["UserEmail"] = user.Email;

                return RedirectToAction("ResetCode");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during forgot password");
                ModelState.AddModelError("", "An error occurred. Please try again.");
                return View();
            }
        }

        #endregion

        #region Reset Code Verification

        /// <summary>
        /// GET: Display reset code verification form
        /// </summary>
        [HttpGet]
        public IActionResult ResetCode()
        {
            var email = TempData["UserEmail"]?.ToString();
            if (string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "Please request password reset first.";
                return RedirectToAction("ForgotPassword");
            }

            ViewBag.Email = email;
            return View();
        }

        /// <summary>
        /// POST: Verify reset code
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetCode(string email, string resetCode)
        {
            try
            {
                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(resetCode))
                {
                    ModelState.AddModelError("", "Email and reset code are required.");
                    ViewBag.Email = email;
                    return View();
                }

                // Find user by email
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

                if (user == null)
                {
                    ModelState.AddModelError("", "User not found.");
                    ViewBag.Email = email;
                    return View();
                }

                // Verify reset code
                if (user.ForgotCode != resetCode)
                {
                    ModelState.AddModelError("", "Invalid reset code.");
                    ViewBag.Email = email;
                    return View();
                }

                // Check if code expired
                if (user.ForgotCodeExp < DateTime.UtcNow)
                {
                    ModelState.AddModelError("", "Reset code has expired. Please request a new one.");
                    ViewBag.Email = email;
                    return View();
                }

                // Store email in TempData for reset password page
                TempData["UserEmail"] = user.Email;
                TempData["ResetCode"] = resetCode;

                return RedirectToAction("ResetPassword");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during reset code verification");
                ModelState.AddModelError("", "An error occurred. Please try again.");
                ViewBag.Email = email;
                return View();
            }
        }

        #endregion

        #region Reset Password

        /// <summary>
        /// GET: Display reset password form
        /// </summary>
        [HttpGet]
        public IActionResult ResetPassword()
        {
            var email = TempData["UserEmail"]?.ToString();
            var resetCode = TempData["ResetCode"]?.ToString();

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(resetCode))
            {
                TempData["ErrorMessage"] = "Please verify reset code first.";
                return RedirectToAction("ResetCode");
            }

            ViewBag.Email = email;
            ViewBag.ResetCode = resetCode;
            return View();
        }

        /// <summary>
        /// POST: Handle password reset
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string email, string resetCode, string newPassword, string confirmPassword)
        {
            try
            {
                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(resetCode) ||
                    string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmPassword))
                {
                    ModelState.AddModelError("", "All fields are required.");
                    ViewBag.Email = email;
                    ViewBag.ResetCode = resetCode;
                    return View();
                }

                // Validate password match
                if (newPassword != confirmPassword)
                {
                    ModelState.AddModelError("", "Passwords do not match.");
                    ViewBag.Email = email;
                    ViewBag.ResetCode = resetCode;
                    return View();
                }

                // Validate password length
                if (newPassword.Length < 6)
                {
                    ModelState.AddModelError("", "Password must be at least 6 characters long.");
                    ViewBag.Email = email;
                    ViewBag.ResetCode = resetCode;
                    return View();
                }

                // Find user by email
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

                if (user == null)
                {
                    ModelState.AddModelError("", "User not found.");
                    ViewBag.Email = email;
                    ViewBag.ResetCode = resetCode;
                    return View();
                }

                // Verify reset code again
                if (user.ForgotCode != resetCode)
                {
                    ModelState.AddModelError("", "Invalid reset code.");
                    ViewBag.Email = email;
                    ViewBag.ResetCode = resetCode;
                    return View();
                }

                // Check if code expired
                if (user.ForgotCodeExp < DateTime.UtcNow)
                {
                    ModelState.AddModelError("", "Reset code has expired. Please request a new one.");
                    ViewBag.Email = email;
                    ViewBag.ResetCode = resetCode;
                    return View();
                }

                // Update password
                user.Password = BCrypt.Net.BCrypt.HashPassword(newPassword);
                user.ForgotCode = null;
                user.ForgotCodeExp = null;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Password reset successfully! You can now login with your new password.";
                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during password reset");
                ModelState.AddModelError("", "An error occurred. Please try again.");
                ViewBag.Email = email;
                ViewBag.ResetCode = resetCode;
                return View();
            }
        }

        #endregion

        #region Profile

        /// <summary>
        /// GET: Display user profile
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction("Login");
            }

            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return RedirectToAction("Login");
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            ViewBag.OrganizerRequest = await _context.OrganizerRequests
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.RequestDate)
                .FirstOrDefaultAsync();

            return View(user);
        }

        /// <summary>
        /// POST: Update user profile
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(User model, IFormFile? imageFile)
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction("Login");
            }

            // Remove password validation since we are not changing it here
            ModelState.Remove("Password");
            ModelState.Remove("ConfirmedPassword");

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return RedirectToAction("Login");
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            try
            {
                // Update basic info
                user.Name = model.Name;
                user.Phone = model.Phone;

                // Handle image upload if new image provided
                if (imageFile != null && imageFile.Length > 0)
                {
                    try
                    {
                        var imageUrl = await _cloudinaryService.UploadImageAsync(imageFile);
                        user.ImageUrl = imageUrl;
                        
                        // Update session image
                        HttpContext.Session.SetString("UserImageUrl", imageUrl);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error uploading profile image");
                        ModelState.AddModelError("", "Failed to upload image. Please try again.");
                        return View(user);
                    }
                }

                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                // Update session name if it changed
                HttpContext.Session.SetString("UserName", user.Name);

                TempData["SuccessMessage"] = "Profile updated successfully!";
                return View(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile");
                ModelState.AddModelError("", "An error occurred while updating your profile.");
                return View(user);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestOrganizer()
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction("Login");
            }

            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return RedirectToAction("Login");
            }

            // Check if user is already an organizer or admin
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            if (user.UserRole?.ToLower() == "organizer" || user.UserRole?.ToLower() == "admin")
            {
                TempData["InfoMessage"] = "You already have organizer permissions.";
                return RedirectToAction("Profile");
            }

            // Check for existing pending request
            var existingRequest = await _context.OrganizerRequests
                .FirstOrDefaultAsync(r => r.UserId == userId && r.Status == "Pending");

            if (existingRequest != null)
            {
                TempData["InfoMessage"] = "Your request is already pending approval.";
                return RedirectToAction("Profile");
            }

            // Create new request
            var request = new OrganizerRequest
            {
                UserId = userId,
                RequestDate = DateTime.Now,
                Status = "Pending"
            };

            _context.OrganizerRequests.Add(request);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Your request to become an organizer has been submitted successfully!";
            return RedirectToAction("Profile");
        }

        #endregion
        #region Helper Methods

        /// <summary>
        /// Check if user is logged in
        /// </summary>
        private bool IsUserLoggedIn()
        {
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("UserId"));
        }

        #endregion
    }
}
