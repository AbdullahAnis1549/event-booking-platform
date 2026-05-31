using System.ComponentModel.DataAnnotations;

namespace WebApplication4.Models.Validators
{
    /// <summary>
    /// Custom validation attribute to ensure email is from Gmail domain
    /// </summary>
    public class GmailEmailAttribute : ValidationAttribute
    {
        public GmailEmailAttribute()
        {
            ErrorMessage = "Email must be a Gmail address (@gmail.com)";
        }

        public override bool IsValid(object? value)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return false;
            }

            string email = value.ToString()!.ToLower();
            
            // Check if email ends with @gmail.com
            return email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase);
        }
    }
}
