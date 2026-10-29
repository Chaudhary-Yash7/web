using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using HostelManagementSystem.Data;
using HostelManagementSystem.Models;

namespace HostelManagementSystem.Controllers
{
    public class PaymentsController : Controller
    {
        private readonly HostelDbContext _context;

        public PaymentsController(HostelDbContext context)
        {
            _context = context;
        }

        // GET: Payments
        public async Task<IActionResult> Index(string? searchString, string? statusFilter, string? monthFilter)
        {
            var paymentsQuery = _context.Payments
                .Include(p => p.Student)
                .AsQueryable();

            // If regular User is logged in, show ONLY their own payments
            if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("User"))
            {
                var sId = await GetCurrentStudentIdAsync();
                paymentsQuery = paymentsQuery.Where(p => p.StudentId == sId);
            }

            if (!string.IsNullOrEmpty(searchString))
            {
                paymentsQuery = paymentsQuery.Where(p => p.Student!.FullName.Contains(searchString));
            }

            if (!string.IsNullOrEmpty(statusFilter))
            {
                paymentsQuery = paymentsQuery.Where(p => p.PaymentStatus == statusFilter);
            }

            if (!string.IsNullOrEmpty(monthFilter))
            {
                paymentsQuery = paymentsQuery.Where(p => p.PaymentMonth == monthFilter);
            }

            // Get distinct months for filter dropdown
            var availableMonths = await _context.Payments
                .Select(p => p.PaymentMonth)
                .Distinct()
                .ToListAsync();

            ViewBag.AvailableMonths = new SelectList(availableMonths, monthFilter);
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentStatus"] = statusFilter;
            ViewData["CurrentMonth"] = monthFilter;

            return View(await paymentsQuery.OrderByDescending(p => p.PaymentDate).ToListAsync());
        }

        // GET: Payments/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var payment = await _context.Payments
                .Include(p => p.Student)
                .FirstOrDefaultAsync(m => m.PaymentId == id);

            if (payment == null) return NotFound();

            // If regular user, prevent viewing someone else's receipt
            if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("User"))
            {
                var sId = await GetCurrentStudentIdAsync();
                if (payment.StudentId != sId)
                {
                    TempData["ErrorMessage"] = "You do not have permission to view other residents' payment receipts.";
                    return RedirectToAction(nameof(Index));
                }
            }

            return View(payment);
        }

        // GET: Payments/Create
        public async Task<IActionResult> Create()
        {
            var isUser = User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("User");
            int? currentStudentId = null;

            if (isUser)
            {
                currentStudentId = await GetCurrentStudentIdAsync();
            }

            await PopulateStudentsDropdownAsync(currentStudentId);
            return View(new Payment
            {
                StudentId = currentStudentId ?? 0,
                PaymentDate = DateTime.Today,
                PaymentMonth = DateTime.Today.ToString("MMMM yyyy"),
                PaymentStatus = "Paid",
                PaymentMethod = "UPI"
            });
        }

        // POST: Payments/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("PaymentId,StudentId,Amount,PaymentDate,PaymentMonth,PaymentStatus,PaymentMethod")] Payment payment)
        {
            // If User role, force StudentId to be their own
            if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("User"))
            {
                var sId = await GetCurrentStudentIdAsync();
                if (sId.HasValue)
                {
                    payment.StudentId = sId.Value;
                }
            }

            if (payment.Amount <= 0)
            {
                ModelState.AddModelError("Amount", "Amount must be a positive number.");
            }

            if (ModelState.IsValid)
            {
                payment.PaymentMonth = payment.PaymentMonth.Trim();
                _context.Add(payment);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Payment recorded successfully!";
                return RedirectToAction(nameof(Index));
            }

            await PopulateStudentsDropdownAsync(payment.StudentId);
            return View(payment);
        }

        // GET: Payments/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var payment = await _context.Payments.FindAsync(id);
            if (payment == null) return NotFound();

            await PopulateStudentsDropdownAsync(payment.StudentId);
            return View(payment);
        }

        // POST: Payments/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("PaymentId,StudentId,Amount,PaymentDate,PaymentMonth,PaymentStatus,PaymentMethod")] Payment payment)
        {
            if (id != payment.PaymentId) return NotFound();

            if (payment.Amount <= 0)
            {
                ModelState.AddModelError("Amount", "Amount must be a positive number.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    payment.PaymentMonth = payment.PaymentMonth.Trim();
                    _context.Update(payment);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Payment updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Payments.AnyAsync(e => e.PaymentId == payment.PaymentId))
                    {
                        return NotFound();
                    }
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }

            await PopulateStudentsDropdownAsync(payment.StudentId);
            return View(payment);
        }

        // GET: Payments/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var payment = await _context.Payments
                .Include(p => p.Student)
                .FirstOrDefaultAsync(m => m.PaymentId == id);

            if (payment == null) return NotFound();

            return View(payment);
        }

        // POST: Payments/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var payment = await _context.Payments.FindAsync(id);
            if (payment != null)
            {
                _context.Payments.Remove(payment);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Payment record deleted successfully!";
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

        private async Task PopulateStudentsDropdownAsync(object? selectedStudent = null)
        {
            var students = await _context.Students.OrderBy(s => s.FullName).ToListAsync();
            ViewBag.StudentId = new SelectList(students.Select(s => new {
                s.StudentId,
                DisplayName = $"{s.FullName} ({s.Course} - {s.Email})"
            }), "StudentId", "DisplayName", selectedStudent);
        }
    }
}
