using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using HostelManagementSystem.Data;
using HostelManagementSystem.Models;

namespace HostelManagementSystem.Controllers
{
    public class MaintenanceController : Controller
    {
        private readonly HostelDbContext _context;

        public MaintenanceController(HostelDbContext context)
        {
            _context = context;
        }

        // GET: Maintenance
        public async Task<IActionResult> Index(string? searchString, string? statusFilter)
        {
            var requestsQuery = _context.MaintenanceRequests
                .Include(m => m.Room)
                .Include(m => m.Student)
                .AsQueryable();

            // If regular User is logged in, show ONLY their own maintenance tickets
            if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("User"))
            {
                var sId = await GetCurrentStudentIdAsync();
                requestsQuery = requestsQuery.Where(m => m.StudentId == sId);
            }

            if (!string.IsNullOrEmpty(searchString))
            {
                requestsQuery = requestsQuery.Where(m =>
                    m.IssueTitle.Contains(searchString) ||
                    m.Room!.RoomNumber.Contains(searchString) ||
                    (m.Student != null && m.Student.FullName.Contains(searchString)));
            }

            if (!string.IsNullOrEmpty(statusFilter))
            {
                requestsQuery = requestsQuery.Where(m => m.Status == statusFilter);
            }

            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentStatus"] = statusFilter;
            return View(await requestsQuery.OrderByDescending(m => m.RequestDate).ToListAsync());
        }

        // GET: Maintenance/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var request = await _context.MaintenanceRequests
                .Include(m => m.Room)
                .Include(m => m.Student)
                .FirstOrDefaultAsync(m => m.MaintenanceRequestId == id);

            if (request == null) return NotFound();

            // Prevent user from viewing other students' tickets
            if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("User"))
            {
                var sId = await GetCurrentStudentIdAsync();
                if (request.StudentId != sId)
                {
                    TempData["ErrorMessage"] = "You do not have permission to view other residents' maintenance tickets.";
                    return RedirectToAction(nameof(Index));
                }
            }

            return View(request);
        }

        // GET: Maintenance/Create
        public async Task<IActionResult> Create()
        {
            int? preselectedRoomId = null;
            int? currentStudentId = null;

            if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("User"))
            {
                currentStudentId = await GetCurrentStudentIdAsync();
                if (currentStudentId.HasValue)
                {
                    var activeAlloc = await _context.Allocations
                        .FirstOrDefaultAsync(a => a.StudentId == currentStudentId.Value && a.Status == "Active");
                    preselectedRoomId = activeAlloc?.RoomId;
                }
            }

            await PopulateDropDownsAsync(preselectedRoomId, currentStudentId);
            return View(new MaintenanceRequest
            {
                RequestDate = DateTime.Today,
                Status = "Pending",
                RoomId = preselectedRoomId ?? 0,
                StudentId = currentStudentId
            });
        }

        // POST: Maintenance/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MaintenanceRequestId,RoomId,StudentId,IssueTitle,Description,RequestDate,Status")] MaintenanceRequest request)
        {
            // If User, automatically link to their own StudentId
            if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("User"))
            {
                var sId = await GetCurrentStudentIdAsync();
                if (sId.HasValue)
                {
                    request.StudentId = sId.Value;
                }
            }

            if (ModelState.IsValid)
            {
                request.IssueTitle = request.IssueTitle.Trim();
                _context.Add(request);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Maintenance request submitted successfully!";
                return RedirectToAction(nameof(Index));
            }

            await PopulateDropDownsAsync(request.RoomId, request.StudentId);
            return View(request);
        }

        // GET: Maintenance/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var request = await _context.MaintenanceRequests.FindAsync(id);
            if (request == null) return NotFound();

            await PopulateDropDownsAsync(request.RoomId, request.StudentId);
            return View(request);
        }

        // POST: Maintenance/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MaintenanceRequestId,RoomId,StudentId,IssueTitle,Description,RequestDate,Status")] MaintenanceRequest request)
        {
            if (id != request.MaintenanceRequestId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    request.IssueTitle = request.IssueTitle.Trim();
                    _context.Update(request);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Maintenance request updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.MaintenanceRequests.AnyAsync(e => e.MaintenanceRequestId == request.MaintenanceRequestId))
                    {
                        return NotFound();
                    }
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }

            await PopulateDropDownsAsync(request.RoomId, request.StudentId);
            return View(request);
        }

        // GET: Maintenance/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var request = await _context.MaintenanceRequests
                .Include(m => m.Room)
                .Include(m => m.Student)
                .FirstOrDefaultAsync(m => m.MaintenanceRequestId == id);

            if (request == null) return NotFound();

            return View(request);
        }

        // POST: Maintenance/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var request = await _context.MaintenanceRequests.FindAsync(id);
            if (request != null)
            {
                _context.MaintenanceRequests.Remove(request);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Maintenance request deleted successfully!";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<int?> GetCurrentStudentIdAsync()
        {
            var claimVal = User.FindFirst("StudentId")?.Value;
            if (int.TryParse(claimVal, out int sId))
            {
                return sId;
            }

            var username = User.Identity?.Name;
            var fullName = User.FindFirst("FullName")?.Value;
            var student = await _context.Students.FirstOrDefaultAsync(s => 
                s.FullName.ToLower() == fullName!.ToLower() || 
                s.Email.ToLower() == username!.ToLower());

            return student?.StudentId;
        }

        private async Task PopulateDropDownsAsync(object? selectedRoom = null, object? selectedStudent = null)
        {
            var rooms = await _context.Rooms.OrderBy(r => r.RoomNumber).ToListAsync();
            var students = await _context.Students.OrderBy(s => s.FullName).ToListAsync();

            ViewBag.RoomId = new SelectList(rooms.Select(r => new {
                r.RoomId,
                DisplayName = $"Room {r.RoomNumber} ({r.RoomType})"
            }), "RoomId", "DisplayName", selectedRoom);

            ViewBag.StudentId = new SelectList(students.Select(s => new {
                s.StudentId,
                DisplayName = $"{s.FullName} ({s.Course})"
            }), "StudentId", "DisplayName", selectedStudent);
        }
    }
}
