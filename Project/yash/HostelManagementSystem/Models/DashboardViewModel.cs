namespace HostelManagementSystem.Models
{
    public class DashboardViewModel
    {
        public int TotalStudents { get; set; }
        public int TotalRooms { get; set; }
        public int TotalCapacity { get; set; }
        public int AvailableBeds { get; set; }
        public int OccupiedBeds { get; set; }
        public int PendingPayments { get; set; }
        public int PendingMaintenanceRequests { get; set; }

        public List<Allocation> RecentAllocations { get; set; } = new List<Allocation>();
    }
}
