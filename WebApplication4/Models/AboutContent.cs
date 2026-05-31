using System.ComponentModel.DataAnnotations;

namespace WebApplication4.Models
{
    public class AboutContent
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Section title is required")]
        [StringLength(200)]
        public string SectionTitle { get; set; } = string.Empty;

        [Required(ErrorMessage = "Content is required")]
        public string Content { get; set; } = string.Empty;

        public string? ImageUrl { get; set; }

        public int DisplayOrder { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
