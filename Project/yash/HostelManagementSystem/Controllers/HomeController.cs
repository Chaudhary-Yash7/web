using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HostelManagementSystem.Data;
using HostelManagementSystem.Models;

namespace HostelManagementSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly HostelDbContext _context;
        private readonly ILogger<HomeController> _logger;

        public HomeController(HostelDbContext context, ILogger<HomeController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var viewModel = new DashboardViewModel();

            try
            {
                // If logged in as regular student user, fetch only THEIR own student record
                if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("User"))
                {
                    var studentIdClaim = User.FindFirst("StudentId")?.Value;
                    Student? student = null;

                    if (int.TryParse(studentIdClaim, out int sId))
                    {
                        student = await _context.Students
                            .Include(s => s.Allocations!)
                                .ThenInclude(a => a.Room)
                            .Include(s => s.Payments)
                            .Include(s => s.MaintenanceRequests!)
                                .ThenInclude(m => m.Room)
                            .FirstOrDefaultAsync(s => s.StudentId == sId);
                    }

                    if (student == null)
                    {
                        var username = User.Identity.Name;
                        var fullName = User.FindFirst("FullName")?.Value;

                        student = await _context.Students
                            .Include(s => s.Allocations!)
                                .ThenInclude(a => a.Room)
                            .Include(s => s.Payments)
                            .Include(s => s.MaintenanceRequests!)
                                .ThenInclude(m => m.Room)
                            .FirstOrDefaultAsync(s => s.FullName.ToLower() == fullName!.ToLower() || s.Email.ToLower() == username!.ToLower());
                    }

                    ViewBag.CurrentStudent = student;
                }

                // Global metrics for admin dashboard
                viewModel.TotalStudents = await _context.Students.CountAsync();
                viewModel.TotalRooms = await _context.Rooms.CountAsync();
                
                var rooms = await _context.Rooms.ToListAsync();
                viewModel.TotalCapacity = rooms.Sum(r => r.Capacity);
                viewModel.OccupiedBeds = rooms.Sum(r => r.OccupiedBeds);
                viewModel.AvailableBeds = Math.Max(0, viewModel.TotalCapacity - viewModel.OccupiedBeds);

                viewModel.PendingPayments = await _context.Payments.CountAsync(p => p.PaymentStatus == "Pending");
                viewModel.PendingMaintenanceRequests = await _context.MaintenanceRequests.CountAsync(m => m.Status == "Pending");

                viewModel.RecentAllocations = await _context.Allocations
                    .Include(a => a.Student)
                    .Include(a => a.Room)
                    .OrderByDescending(a => a.AllocationDate)
                    .Take(5)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard metrics from database.");
                ViewBag.DatabaseError = "Notice: MySQL server is not connected or reachable yet. Please ensure MySQL is running with password 'saral@6562'.";
            }

            return View(viewModel);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
