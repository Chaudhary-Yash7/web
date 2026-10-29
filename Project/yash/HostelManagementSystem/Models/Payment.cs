using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HostelManagementSystem.Models
{
    public class Payment
    {
        [Key]
        public int PaymentId { get; set; }

        [Required(ErrorMessage = "Please select a student.")]
        [Display(Name = "Student")]
        public int StudentId { get; set; }

        [ForeignKey("StudentId")]
        public Student? Student { get; set; }

        [Required(ErrorMessage = "Amount is required.")]
        [Range(1.0, 1000000.0, ErrorMessage = "Payment amount must be a positive value.")]
        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Amount (₹)")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Payment Date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Payment Date")]
        public DateTime PaymentDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Payment month is required.")]
        [StringLength(30, ErrorMessage = "Month cannot exceed 30 characters.")]
        [Display(Name = "Payment Month")]
        public string PaymentMonth { get; set; } = string.Empty; // e.g., "October 2026"

        [Required(ErrorMessage = "Payment status is required.")]
        [StringLength(20, ErrorMessage = "Status cannot exceed 20 characters.")]
        [Display(Name = "Payment Status")]
        public string PaymentStatus { get; set; } = "Paid"; // Paid, Pending

        [Required(ErrorMessage = "Payment method is required.")]
        [StringLength(30, ErrorMessage = "Payment method cannot exceed 30 characters.")]
        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; } = "UPI"; // Cash, UPI, Card, Net Banking
    }
}

