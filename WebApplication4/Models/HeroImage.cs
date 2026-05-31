using System.ComponentModel.DataAnnotations;

namespace WebApplication4.Models
{
    public class HeroImage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string ImageUrl { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;
        
        public string Subtitle { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public int DisplayOrder { get; set; } = 0;
    }
}
