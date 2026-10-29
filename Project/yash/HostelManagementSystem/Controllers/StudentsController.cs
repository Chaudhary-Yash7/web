using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HostelManagementSystem.Data;
using HostelManagementSystem.Models;

namespace HostelManagementSystem.Controllers
{
    public class StudentsController : Controller
    {
        private readonly HostelDbContext _context;

        public StudentsController(HostelDbContext context)
        {
            _context = context;
        }

        // GET: Students
        public async Task<IActionResult> Index(string? searchString)
        {
            var studentsQuery = _context.Students.AsQueryable();

            // If regular User is logged in, show ONLY their own student record
            if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("User"))
            {
                var sId = await GetCurrentStudentIdAsync();
                studentsQuery = studentsQuery.Where(s => s.StudentId == sId);
            }

            if (!string.IsNullOrEmpty(searchString))
            {
                studentsQuery = studentsQuery.Where(s =>
                    s.FullName.Contains(searchString) ||
                    s.Email.Contains(searchString) ||
                    s.Phone.Contains(searchString) ||
                    s.Course.Contains(searchString));
            }

            ViewData["CurrentFilter"] = searchString;
            return View(await studentsQuery.OrderByDescending(s => s.StudentId).ToListAsync());
        }

        // GET: Students/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            // If regular user, prevent viewing other students' profiles
            if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("User"))
            {
                var sId = await GetCurrentStudentIdAsync();
                if (id != sId)
                {
                    TempData["ErrorMessage"] = "You can only view your own student profile.";
                    return RedirectToAction(nameof(Index));
                }
            }

            var student = await _context.Students
                .Include(s => s.Allocations!)
                    .ThenInclude(a => a.Room)
                .Include(s => s.Payments)
                .Include(s => s.MaintenanceRequests)
                .FirstOrDefaultAsync(m => m.StudentId == id);

            if (student == null) return NotFound();

            return View(student);
        }

        // GET: Students/Create
        public IActionResult Create()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("User"))
            {
                TempData["ErrorMessage"] = "New students are enrolled directly by the Hostel Administrator.";
                return RedirectToAction(nameof(Index));
            }
            return View();
        }

        // POST: Students/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("StudentId,FullName,Email,Phone,Course,Semester,Address")] Student student)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("User"))
            {
                TempData["ErrorMessage"] = "Access restricted. Only Administrators can enroll new students.";
                return RedirectToAction(nameof(Index));
            }

            // Validation: Prevent duplicate email
            if (await _context.Students.AnyAsync(s => s.Email.ToLower() == student.Email.Trim().ToLower()))
            {
                ModelState.AddModelError("Email", "A student with this email address is already registered.");
            }

            if (ModelState.IsValid)
            {
                student.Email = student.Email.Trim();
                student.FullName = student.FullName.Trim();
                _context.Add(student);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Student added successfully!";
                return RedirectToAction(nameof(Index));
            }
            return View(student);
        }

        // GET: Students/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("User"))
            {
                var sId = await GetCurrentStudentIdAsync();
                if (id != sId)
                {
                    TempData["ErrorMessage"] = "You can only edit your own contact information.";
                    return RedirectToAction(nameof(Index));
                }
            }

            var student = await _context.Students.FindAsync(id);
            if (student == null) return NotFound();

            return View(student);
        }

        // POST: Students/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("StudentId,FullName,Email,Phone,Course,Semester,Address")] Student student)
        {
            if (id != student.StudentId) return NotFound();

            if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("User"))
            {
                var sId = await GetCurrentStudentIdAsync();
                if (id != sId)
                {
                    TempData["ErrorMessage"] = "You can only edit your own contact information.";
                    return RedirectToAction(nameof(Index));
                }
            }

            // Validation: Prevent duplicate email across other students
            if (await _context.Students.AnyAsync(s => s.StudentId != id && s.Email.ToLower() == student.Email.Trim().ToLower()))
            {
                ModelState.AddModelError("Email", "Another student with this email address already exists.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    student.Email = student.Email.Trim();
                    student.FullName = student.FullName.Trim();
                    _context.Update(student);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Student updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Students.AnyAsync(e => e.StudentId == student.StudentId))
                    {
                        return NotFound();
                    }
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(student);
        }

        // GET: Students/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("User"))
            {
                TempData["ErrorMessage"] = "Access restricted. Only Administrators can delete student records.";
                return RedirectToAction(nameof(Index));
            }

            var student = await _context.Students
                .FirstOrDefaultAsync(m => m.StudentId == id);
            if (student == null) return NotFound();

            return View(student);
        }

        // POST: Students/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("User"))
            {
                TempData["ErrorMessage"] = "Access restricted. Only Administrators can delete student records.";
                return RedirectToAction(nameof(Index));
            }

            var student = await _context.Students.FindAsync(id);
            if (student != null)
            {
                // Check if active allocation exists
                var hasActiveAllocation = await _context.Allocations.AnyAsync(a => a.StudentId == id && a.Status == "Active");
                if (hasActiveAllocation)
                {
                    TempData["ErrorMessage"] = "Cannot delete student because an active room allocation exists. Please check out first.";
                    return RedirectToAction(nameof(Index));
                }

                _context.Students.Remove(student);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Student deleted successfully!";
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
    }
}
