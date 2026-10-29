using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using HostelManagementSystem.Data;
using HostelManagementSystem.Models;

namespace HostelManagementSystem.Controllers
{
    public class AllocationsController : Controller
    {
        private readonly HostelDbContext _context;

        public AllocationsController(HostelDbContext context)
        {
            _context = context;
        }

        // GET: Allocations
        public async Task<IActionResult> Index(string? statusFilter, string? searchString)
        {
            var allocationsQuery = _context.Allocations
                .Include(a => a.Student)
                .Include(a => a.Room)
                .AsQueryable();

            // If regular User is logged in, show ONLY their own room allocation
            if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("User"))
            {
                var sId = await GetCurrentStudentIdAsync();
                allocationsQuery = allocationsQuery.Where(a => a.StudentId == sId);
            }

            if (!string.IsNullOrEmpty(searchString))
            {
                allocationsQuery = allocationsQuery.Where(a =>
                    a.Student!.FullName.Contains(searchString) ||
                    a.Room!.RoomNumber.Contains(searchString));
            }

            if (!string.IsNullOrEmpty(statusFilter))
            {
                allocationsQuery = allocationsQuery.Where(a => a.Status == statusFilter);
            }

            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentStatus"] = statusFilter;
            return View(await allocationsQuery.OrderByDescending(a => a.AllocationDate).ToListAsync());
        }

        // GET: Allocations/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var allocation = await _context.Allocations
                .Include(a => a.Student)
                .Include(a => a.Room)
                .FirstOrDefaultAsync(m => m.AllocationId == id);

            if (allocation == null) return NotFound();

            // Prevent user from viewing other students' allocations
            if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("User"))
            {
                var sId = await GetCurrentStudentIdAsync();
                if (allocation.StudentId != sId)
                {
                    TempData["ErrorMessage"] = "You do not have permission to view other residents' room allocations.";
                    return RedirectToAction(nameof(Index));
                }
            }

            return View(allocation);
        }

        // GET: Allocations/Create
        public async Task<IActionResult> Create()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("User"))
            {
                TempData["ErrorMessage"] = "Room allocations are configured and managed directly by the Hostel Administrator.";
                return RedirectToAction(nameof(Index));
            }

            await PopulateAvailableDropDownsAsync();
            return View(new Allocation { AllocationDate = DateTime.Today, Status = "Active" });
        }

        // POST: Allocations/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("AllocationId,StudentId,RoomId,AllocationDate,Status")] Allocation allocation)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("User"))
            {
                TempData["ErrorMessage"] = "Access restricted. Only Administrators can allocate rooms.";
                return RedirectToAction(nameof(Index));
            }

            // Business Rule: Student cannot have more than one active allocation
            if (allocation.Status == "Active")
            {
                var alreadyAllocated = await _context.Allocations
                    .AnyAsync(a => a.StudentId == allocation.StudentId && a.Status == "Active");

                if (alreadyAllocated)
                {
                    ModelState.AddModelError("StudentId", "This student already has an active room allocation.");
                }
            }

            var room = await _context.Rooms.FindAsync(allocation.RoomId);
            if (room != null && allocation.Status == "Active")
            {
                if (room.Status == "Under Maintenance")
                {
                    ModelState.AddModelError("RoomId", "This room is currently under maintenance.");
                }
                else if (room.OccupiedBeds >= room.Capacity)
                {
                    ModelState.AddModelError("RoomId", $"This room is fully occupied ({room.OccupiedBeds}/{room.Capacity} beds).");
                }
            }

            if (ModelState.IsValid)
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    _context.Add(allocation);

                    if (room != null && allocation.Status == "Active")
                    {
                        room.OccupiedBeds += 1;
                        if (room.OccupiedBeds >= room.Capacity)
                        {
                            room.Status = "Occupied";
                        }
                        _context.Update(room);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["SuccessMessage"] = "Room allocated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError(string.Empty, "An error occurred while saving the allocation. Please try again.");
                }
            }

            await PopulateAvailableDropDownsAsync(allocation.StudentId, allocation.RoomId);
            return View(allocation);
        }

        // GET: Allocations/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("User"))
            {
                TempData["ErrorMessage"] = "Access restricted. Only Administrators can edit room allocations.";
                return RedirectToAction(nameof(Index));
            }

            var allocation = await _context.Allocations.FindAsync(id);
            if (allocation == null) return NotFound();

            await PopulateAllDropDownsAsync(allocation.StudentId, allocation.RoomId);
            return View(allocation);
        }

        // POST: Allocations/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("AllocationId,StudentId,RoomId,AllocationDate,CheckOutDate,Status")] Allocation allocation)
        {
            if (id != allocation.AllocationId) return NotFound();

            var oldAllocation = await _context.Allocations.AsNoTracking().FirstOrDefaultAsync(a => a.AllocationId == id);
            if (oldAllocation == null) return NotFound();

            if (ModelState.IsValid)
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    if (oldAllocation.Status == "Active" && allocation.Status == "CheckedOut")
                    {
                        var room = await _context.Rooms.FindAsync(allocation.RoomId);
                        if (room != null)
                        {
                            room.OccupiedBeds = Math.Max(0, room.OccupiedBeds - 1);
                            if (room.Status == "Occupied") room.Status = "Available";
                            _context.Update(room);
                        }
                        if (allocation.CheckOutDate == null)
                        {
                            allocation.CheckOutDate = DateTime.Today;
                        }
                    }
                    else if (oldAllocation.Status == "CheckedOut" && allocation.Status == "Active")
                    {
                        var room = await _context.Rooms.FindAsync(allocation.RoomId);
                        if (room != null)
                        {
                            room.OccupiedBeds += 1;
                            if (room.OccupiedBeds >= room.Capacity) room.Status = "Occupied";
                            _context.Update(room);
                        }
                    }

                    _context.Update(allocation);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["SuccessMessage"] = "Allocation updated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError(string.Empty, "An error occurred while updating the allocation.");
                }
            }

            await PopulateAllDropDownsAsync(allocation.StudentId, allocation.RoomId);
            return View(allocation);
        }

        // GET: Allocations/CheckOut/5
        public async Task<IActionResult> CheckOut(int? id)
        {
            if (id == null) return NotFound();

            if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("User"))
            {
                TempData["ErrorMessage"] = "Check-outs must be processed and confirmed by the Administrator.";
                return RedirectToAction(nameof(Index));
            }

            var allocation = await _context.Allocations
                .Include(a => a.Student)
                .Include(a => a.Room)
                .FirstOrDefaultAsync(m => m.AllocationId == id);

            if (allocation == null) return NotFound();

            if (allocation.Status == "CheckedOut")
            {
                TempData["ErrorMessage"] = "This student has already checked out.";
                return RedirectToAction(nameof(Index));
            }

            return View(allocation);
        }

        // POST: Allocations/CheckOut/5
        [HttpPost, ActionName("CheckOut")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckOutConfirmed(int id)
        {
            var allocation = await _context.Allocations.FindAsync(id);
            if (allocation != null && allocation.Status == "Active")
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    allocation.Status = "CheckedOut";
                    allocation.CheckOutDate = DateTime.Today;
                    _context.Update(allocation);

                    var room = await _context.Rooms.FindAsync(allocation.RoomId);
                    if (room != null)
                    {
                        room.OccupiedBeds = Math.Max(0, room.OccupiedBeds - 1);
                        if (room.Status == "Occupied") room.Status = "Available";
                        _context.Update(room);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["SuccessMessage"] = "Student checked out successfully and room bed has been released!";
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] = "Could not check out student due to a database error.";
                }
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Allocations/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("User"))
            {
                TempData["ErrorMessage"] = "Access restricted. Only Administrators can delete allocation records.";
                return RedirectToAction(nameof(Index));
            }

            var allocation = await _context.Allocations
                .Include(a => a.Student)
                .Include(a => a.Room)
                .FirstOrDefaultAsync(m => m.AllocationId == id);

            if (allocation == null) return NotFound();

            return View(allocation);
        }

        // POST: Allocations/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var allocation = await _context.Allocations.FindAsync(id);
            if (allocation != null)
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    if (allocation.Status == "Active")
                    {
                        var room = await _context.Rooms.FindAsync(allocation.RoomId);
                        if (room != null)
                        {
                            room.OccupiedBeds = Math.Max(0, room.OccupiedBeds - 1);
                            if (room.Status == "Occupied") room.Status = "Available";
                            _context.Update(room);
                        }
                    }

                    _context.Allocations.Remove(allocation);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["SuccessMessage"] = "Allocation deleted successfully!";
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] = "Error occurred while deleting allocation.";
                }
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

        private async Task PopulateAvailableDropDownsAsync(object? selectedStudent = null, object? selectedRoom = null)
        {
            var activeStudentIds = await _context.Allocations
                .Where(a => a.Status == "Active")
                .Select(a => a.StudentId)
                .ToListAsync();

            var allStudents = await _context.Students.OrderBy(s => s.FullName).ToListAsync();
            ViewBag.StudentId = new SelectList(allStudents.Select(s => new {
                s.StudentId,
                DisplayName = activeStudentIds.Contains(s.StudentId) 
                    ? $"{s.FullName} ({s.Course}) - [Already Has Active Room]" 
                    : $"{s.FullName} ({s.Course} - Sem {s.Semester})"
            }), "StudentId", "DisplayName", selectedStudent);

            var availableRooms = await _context.Rooms
                .Where(r => r.OccupiedBeds < r.Capacity && r.Status != "Under Maintenance")
                .OrderBy(r => r.RoomNumber)
                .ToListAsync();

            ViewBag.RoomId = new SelectList(availableRooms.Select(r => new {
                r.RoomId,
                DisplayName = $"Room {r.RoomNumber} ({r.RoomType} - {r.Capacity - r.OccupiedBeds} Beds Available, ₹{r.MonthlyFee}/mo)"
            }), "RoomId", "DisplayName", selectedRoom);
        }

        private async Task PopulateAllDropDownsAsync(object? selectedStudent = null, object? selectedRoom = null)
        {
            var students = await _context.Students.OrderBy(s => s.FullName).ToListAsync();
            var rooms = await _context.Rooms.OrderBy(r => r.RoomNumber).ToListAsync();

            ViewBag.StudentId = new SelectList(students.Select(s => new {
                s.StudentId,
                DisplayName = $"{s.FullName} ({s.Course})"
            }), "StudentId", "DisplayName", selectedStudent);

            ViewBag.RoomId = new SelectList(rooms.Select(r => new {
                r.RoomId,
                DisplayName = $"Room {r.RoomNumber} ({r.RoomType} - {r.OccupiedBeds}/{r.Capacity} Beds, ₹{r.MonthlyFee}/mo)"
            }), "RoomId", "DisplayName", selectedRoom);
        }
    }
}
