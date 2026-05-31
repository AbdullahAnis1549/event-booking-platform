using System.Collections.Generic;
using WebApplication4.Models;

namespace WebApplication4.Models.ViewModels
{
    public class OrganizerDashboardViewModel
    {
        public List<Event> MyEvents { get; set; } = new List<Event>();
        
        public int TotalEvents { get; set; }
        public int ApprovedEvents { get; set; }
        public int PendingEvents { get; set; }
        
        public decimal TotalRevenue { get; set; }
        public int TotalTicketsSold { get; set; }
        public decimal AverageRevenuePerEvent { get; set; }
        
        public string[] MonthlyRevenueLabels { get; set; } = new string[0];
        public decimal[] MonthlyRevenueData { get; set; } = new decimal[0];
    }
}
