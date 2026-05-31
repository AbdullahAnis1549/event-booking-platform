using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WebApplication4.Models.Validators;

namespace WebApplication4.Models
{
    /// <summary>
    /// User model representing a user in the authentication system
    /// </summary>
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        [Display(Name = "Full Name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address format")]
        [GmailEmail(ErrorMessage = "Email must be a Gmail address (@gmail.com)")]
        [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required")]
        [StringLength(20, ErrorMessage = "Phone cannot exceed 20 characters")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [StringLength(255, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [NotMapped]
        [Required(ErrorMessage = "Please confirm your password")]
        [Compare("Password", ErrorMessage = "Confirm password does not match with password")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        public string ConfirmedPassword { get; set; } = string.Empty;

        public bool? VerifyStatus { get; set; }

        public int? VerifyCode { get; set; }

        public DateTime? VerifyCodeExpDate { get; set; }

        public string? ForgotCode { get; set; }

        public DateTime? ForgotCodeExp { get; set; }

        public string? ImageUrl { get; set; }

        [StringLength(50)]
        public string UserRole { get; set; } = "user";

        public User()
        {
            VerifyStatus = false;
            UserRole = "user";
        }
    }
}
