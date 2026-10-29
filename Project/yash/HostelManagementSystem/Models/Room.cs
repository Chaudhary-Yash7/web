using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HostelManagementSystem.Models
{
    public class Room
    {
        [Key]
        public int RoomId { get; set; }

        [Required(ErrorMessage = "Room Number is required.")]
        [StringLength(20, ErrorMessage = "Room Number cannot exceed 20 characters.")]
        [Display(Name = "Room Number")]
        public string RoomNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Room Type is required.")]
        [StringLength(30, ErrorMessage = "Room Type cannot exceed 30 characters.")]
        [Display(Name = "Room Type")]
        public string RoomType { get; set; } = "Single"; // e.g., Single, Double, Triple

        [Required(ErrorMessage = "Capacity is required.")]
        [Range(1, 20, ErrorMessage = "Capacity must be positive (at least 1 bed).")]
        [Display(Name = "Capacity (Beds)")]
        public int Capacity { get; set; } = 1;

        [Range(0, 20, ErrorMessage = "Occupied beds cannot be negative.")]
        [Display(Name = "Occupied Beds")]
        public int OccupiedBeds { get; set; } = 0;

        [Required(ErrorMessage = "Monthly Fee is required.")]
        [Range(1.0, 1000000.0, ErrorMessage = "Monthly fee must be a positive amount.")]
        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Monthly Fee (₹)")]
        public decimal MonthlyFee { get; set; }

        [Required(ErrorMessage = "Status is required.")]
        [StringLength(20, ErrorMessage = "Status cannot exceed 20 characters.")]
        public string Status { get; set; } = "Available"; // Available, Occupied, Under Maintenance

        // Navigation Properties
        public ICollection<Allocation>? Allocations { get; set; }
        public ICollection<MaintenanceRequest>? MaintenanceRequests { get; set; }
    }
}

