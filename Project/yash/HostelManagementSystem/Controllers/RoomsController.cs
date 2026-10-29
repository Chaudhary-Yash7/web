using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HostelManagementSystem.Data;
using HostelManagementSystem.Models;

namespace HostelManagementSystem.Controllers
{
    public class RoomsController : Controller
    {
        private readonly HostelDbContext _context;

        public RoomsController(HostelDbContext context)
        {
            _context = context;
        }

        // GET: Rooms
        public async Task<IActionResult> Index(string? searchString, string? statusFilter, string? typeFilter)
        {
            var roomsQuery = _context.Rooms.AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                roomsQuery = roomsQuery.Where(r => r.RoomNumber.Contains(searchString));
            }

            if (!string.IsNullOrEmpty(statusFilter))
            {
                roomsQuery = roomsQuery.Where(r => r.Status == statusFilter);
            }

            if (!string.IsNullOrEmpty(typeFilter))
            {
                roomsQuery = roomsQuery.Where(r => r.RoomType == typeFilter);
            }

            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentStatus"] = statusFilter;
            ViewData["CurrentType"] = typeFilter;
            return View(await roomsQuery.OrderBy(r => r.RoomNumber).ToListAsync());
        }

        // GET: Rooms/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var room = await _context.Rooms
                .Include(r => r.Allocations!)
                    .ThenInclude(a => a.Student)
                .Include(r => r.MaintenanceRequests)
                .FirstOrDefaultAsync(m => m.RoomId == id);

            if (room == null) return NotFound();

            return View(room);
        }

        // GET: Rooms/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Rooms/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("RoomId,RoomNumber,RoomType,Capacity,MonthlyFee,Status")] Room room)
        {
            // Validation: Prevent duplicate RoomNumber
            if (await _context.Rooms.AnyAsync(r => r.RoomNumber.ToLower() == room.RoomNumber.Trim().ToLower()))
            {
                ModelState.AddModelError("RoomNumber", "A room with this room number already exists.");
            }

            if (room.Capacity <= 0)
            {
                ModelState.AddModelError("Capacity", "Capacity must be at least 1 bed.");
            }

            if (room.MonthlyFee <= 0)
            {
                ModelState.AddModelError("MonthlyFee", "Monthly fee must be greater than zero.");
            }

            if (ModelState.IsValid)
            {
                room.RoomNumber = room.RoomNumber.Trim();
                room.OccupiedBeds = 0;
                _context.Add(room);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Room created successfully!";
                return RedirectToAction(nameof(Index));
            }
            return View(room);
        }

        // GET: Rooms/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var room = await _context.Rooms.FindAsync(id);
            if (room == null) return NotFound();

            return View(room);
        }

        // POST: Rooms/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("RoomId,RoomNumber,RoomType,Capacity,OccupiedBeds,MonthlyFee,Status")] Room room)
        {
            if (id != room.RoomId) return NotFound();

            // Validation: Prevent duplicate RoomNumber across other rooms
            if (await _context.Rooms.AnyAsync(r => r.RoomId != id && r.RoomNumber.ToLower() == room.RoomNumber.Trim().ToLower()))
            {
                ModelState.AddModelError("RoomNumber", "Another room with this room number already exists.");
            }

            if (room.Capacity <= 0)
            {
                ModelState.AddModelError("Capacity", "Capacity must be at least 1 bed.");
            }

            if (room.OccupiedBeds > room.Capacity)
            {
                ModelState.AddModelError("Capacity", $"Capacity cannot be less than currently occupied beds ({room.OccupiedBeds}).");
            }

            if (room.MonthlyFee <= 0)
            {
                ModelState.AddModelError("MonthlyFee", "Monthly fee must be greater than zero.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    room.RoomNumber = room.RoomNumber.Trim();

                    // Auto-update availability status
                    if (room.Status != "Under Maintenance")
                    {
                        if (room.OccupiedBeds >= room.Capacity)
                        {
                            room.Status = "Occupied";
                        }
                        else
                        {
                            room.Status = "Available";
                        }
                    }

                    _context.Update(room);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Room updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Rooms.AnyAsync(e => e.RoomId == room.RoomId))
                    {
                        return NotFound();
                    }
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(room);
        }

        // GET: Rooms/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var room = await _context.Rooms
                .FirstOrDefaultAsync(m => m.RoomId == id);
            if (room == null) return NotFound();

            return View(room);
        }

        // POST: Rooms/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var room = await _context.Rooms.FindAsync(id);
            if (room != null)
            {
                var activeAllocations = await _context.Allocations.AnyAsync(a => a.RoomId == id && a.Status == "Active");
                if (activeAllocations)
                {
                    TempData["ErrorMessage"] = "Cannot delete room because it currently has active student allocations. Please check out students first.";
                    return RedirectToAction(nameof(Index));
                }

                _context.Rooms.Remove(room);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Room deleted successfully!";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
