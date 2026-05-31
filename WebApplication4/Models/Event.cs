using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication4.Models
{
    public class Event
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Title is required")]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Description is required")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Event Date is required")]
        [Display(Name = "Event Date")]
        public DateTime EventDate { get; set; }

        [Required(ErrorMessage = "Location is required")]
        public string Location { get; set; } = string.Empty;

        [Required(ErrorMessage = "Price is required")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "Total Seats is required")]
        [Range(1, 10000)]
        public int TotalSeats { get; set; }

        public int AvailableSeats { get; set; }

        public string? ImageUrl { get; set; }

        public int OrganizerId { get; set; }

        [ForeignKey("OrganizerId")]
        public virtual User? Organizer { get; set; }

        public string Category { get; set; } = "Weddings"; 
        public string SubCategory { get; set; } = string.Empty;

        public bool IsApproved { get; set; } = false;
        public bool IsDeleteRequested { get; set; } = false;
        public bool IsSuccessful { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
