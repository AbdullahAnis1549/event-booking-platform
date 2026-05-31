using System.ComponentModel.DataAnnotations;

namespace WebApplication4.Models
{
    public class ContactInfo
    {
        public int Id { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; } = "support@eliteevents.com";

        [Required]
        public string Phone { get; set; } = "+1 (555) 123-4567";

        [Required]
        public string Address { get; set; } = "123 Event Street, City, State 12345";

        public string? FacebookLink { get; set; }
        public string? TwitterLink { get; set; }
        public string? InstagramLink { get; set; }
        public string? LinkedInLink { get; set; }
    }
}
