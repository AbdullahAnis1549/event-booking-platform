using WebApplication4.Models;

namespace WebApplication4.Models.ViewModels
{
    public class HomeViewModel
    {
        public IEnumerable<Event> UpcomingEvents { get; set; } = new List<Event>();
        public IEnumerable<Event> SuccessfulEvents { get; set; } = new List<Event>();
        public IEnumerable<HeroImage> HeroImages { get; set; } = new List<HeroImage>();
    }
}
