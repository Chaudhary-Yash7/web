using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HostelManagementSystem.Data;
using HostelManagementSystem.Models;

namespace HostelManagementSystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly HostelDbContext _context;

        public AccountController(HostelDbContext context)
        {
            _context = context;
        }

        // GET: /Account/Login
        [HttpGet]
        public async Task<IActionResult> Login(string? returnUrl = null)
        {
            await EnsureUsersTableCreatedAsync();

            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            await EnsureUsersTableCreatedAsync();
            ViewData["ReturnUrl"] = returnUrl;

            if (ModelState.IsValid)
            {
                var inputUser = model.Username.Trim().ToLower();
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username.ToLower() == inputUser && u.Password == model.Password);

                if (user != null)
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                        new Claim(ClaimTypes.Name, user.Username),
                        new Claim("FullName", user.FullName),
                        new Claim(ClaimTypes.Role, user.Role)
                    };

                    // If user is a student, link/find their unique StudentId
                    if (user.Role == "User")
                    {
                        var student = await GetOrCreateStudentForUserAsync(user);
                        if (student != null)
                        {
                            claims.Add(new Claim("StudentId", student.StudentId.ToString()));
                        }
                    }

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                    };

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity),
                        authProperties);

                    TempData["SuccessMessage"] = $"Welcome back, {user.FullName}!";

                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }
                    return RedirectToAction("Index", "Home");
                }

                ModelState.AddModelError(string.Empty, "Invalid username or password. Please try again.");
            }

            return View(model);
        }

        // GET: /Account/Register (Sign In / Sign Up)
        [HttpGet]
        public async Task<IActionResult> Register()
        {
            await EnsureUsersTableCreatedAsync();

            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            await EnsureUsersTableCreatedAsync();

            if (ModelState.IsValid)
            {
                var inputUser = model.Username.Trim().ToLower();

                // Check if username already exists
                var exists = await _context.Users
                    .AnyAsync(u => u.Username.ToLower() == inputUser);

                if (exists)
                {
                    ModelState.AddModelError("Username", "This username is already taken. Please choose another.");
                    return View(model);
                }

                var newUser = new User
                {
                    Username = model.Username.Trim(),
                    Password = model.Password,
                    FullName = model.FullName.Trim(),
                    Email = model.Email?.Trim(),
                    Role = string.IsNullOrEmpty(model.Role) ? "User" : model.Role
                };

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, newUser.UserId.ToString()),
                    new Claim(ClaimTypes.Name, newUser.Username),
                    new Claim("FullName", newUser.FullName),
                    new Claim(ClaimTypes.Role, newUser.Role)
                };

                // Create dedicated student profile for regular user
                if (newUser.Role == "User")
                {
                    var student = await GetOrCreateStudentForUserAsync(newUser);
                    if (student != null)
                    {
                        claims.Add(new Claim("StudentId", student.StudentId.ToString()));
                    }
                }

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity));

                TempData["SuccessMessage"] = "Account created successfully! You are now logged in.";
                return RedirectToAction("Index", "Home");
            }

            return View(model);
        }

        // GET/POST: /Account/Logout
        [HttpGet, HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["SuccessMessage"] = "You have been logged out successfully.";
            return RedirectToAction("Index", "Home");
        }

        private async Task<Student?> GetOrCreateStudentForUserAsync(User user)
        {
            try
            {
                // Match existing student by email, username, or name
                var student = await _context.Students.FirstOrDefaultAsync(s =>
                    (!string.IsNullOrEmpty(user.Email) && s.Email.ToLower() == user.Email.ToLower()) ||
                    s.Email.ToLower() == user.Username.ToLower() ||
                    s.FullName.ToLower() == user.FullName.ToLower());

                if (student == null)
                {
                    student = new Student
                    {
                        FullName = user.FullName,
                        Email = !string.IsNullOrEmpty(user.Email) ? user.Email : $"{user.Username}@student.hostel.com",
                        Phone = "9800000000",
                        Course = "B.Tech",
                        Semester = 1,
                        Address = "Hostel Resident"
                    };
                    _context.Students.Add(student);
                    await _context.SaveChangesAsync();
                }

                return student;
            }
            catch
            {
                return null;
            }
        }

        private async Task EnsureUsersTableCreatedAsync()
        {
            try
            {
                const string sql = @"
                    CREATE TABLE IF NOT EXISTS `Users` (
                        `UserId` INT NOT NULL AUTO_INCREMENT,
                        `Username` VARCHAR(50) NOT NULL,
                        `Password` VARCHAR(100) NOT NULL,
                        `FullName` VARCHAR(100) NOT NULL,
                        `Email` VARCHAR(100) NULL,
                        `Role` VARCHAR(20) NOT NULL DEFAULT 'User',
                        PRIMARY KEY (`UserId`),
                        UNIQUE KEY `IX_Users_Username` (`Username`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

                await _context.Database.ExecuteSqlRawAsync(sql);

                const string seedAdminSql = @"
                    INSERT IGNORE INTO `Users` (`Username`, `Password`, `FullName`, `Email`, `Role`)
                    VALUES 
                    ('yash', 'yash', 'Yash (Admin)', 'yash@hostelcare.com', 'Admin'),
                    ('user', 'user123', 'Rahul Sharma', 'rahul.sharma@example.com', 'User');";

                await _context.Database.ExecuteSqlRawAsync(seedAdminSql);
            }
            catch
            {
            }
        }
    }
}
