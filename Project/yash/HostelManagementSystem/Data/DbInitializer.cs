using HostelManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace HostelManagementSystem.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(HostelDbContext context)
        {
            // 1. Ensure database schema is created
            await context.Database.EnsureCreatedAsync();

            // 2. Explicitly ensure Users table exists (in case database was created prior to Users model)
            await EnsureUsersTableAsync(context);

            // 3. Seed Users (Admin: yash/yash, User: user/user123)
            try
            {
                if (!await context.Users.AnyAsync())
                {
                    var users = new User[]
                    {
                        new User
                        {
                            Username = "yash",
                            Password = "yash",
                            FullName = "Yash (Admin)",
                            Email = "yash@hostelcare.com",
                            Role = "Admin"
                        },
                        new User
                        {
                            Username = "user",
                            Password = "user123",
                            FullName = "Rahul Sharma",
                            Email = "rahul.sharma@example.com",
                            Role = "User"
                        }
                    };
                    await context.Users.AddRangeAsync(users);
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception)
            {
                // Fallback in case EF tracking needs fresh state
                await EnsureUsersTableAsync(context);
            }

            // 4. Check if rooms/students data already exists
            if (await context.Rooms.AnyAsync() || await context.Students.AnyAsync())
            {
                return; // DB has already been seeded
            }

            // Seed Rooms
            var rooms = new Room[]
            {
                new Room { RoomNumber = "101", RoomType = "Single", Capacity = 1, OccupiedBeds = 1, MonthlyFee = 6000, Status = "Occupied" },
                new Room { RoomNumber = "102", RoomType = "Double", Capacity = 2, OccupiedBeds = 1, MonthlyFee = 4500, Status = "Available" },
                new Room { RoomNumber = "103", RoomType = "Double", Capacity = 2, OccupiedBeds = 0, MonthlyFee = 4500, Status = "Available" },
                new Room { RoomNumber = "201", RoomType = "Triple", Capacity = 3, OccupiedBeds = 0, MonthlyFee = 3500, Status = "Available" },
                new Room { RoomNumber = "202", RoomType = "Single", Capacity = 1, OccupiedBeds = 0, MonthlyFee = 6000, Status = "Under Maintenance" }
            };
            await context.Rooms.AddRangeAsync(rooms);
            await context.SaveChangesAsync();

            // Seed Students
            var students = new Student[]
            {
                new Student { FullName = "Rahul Sharma", Email = "rahul.sharma@example.com", Phone = "9876543210", Course = "B.Tech CSE", Semester = 3, Address = "Sector 14, Jaipur" },
                new Student { FullName = "Amit Patel", Email = "amit.patel@example.com", Phone = "9876543211", Course = "B.Tech IT", Semester = 5, Address = "Navrangpura, Ahmedabad" },
                new Student { FullName = "Priya Verma", Email = "priya.verma@example.com", Phone = "9876543212", Course = "BCA", Semester = 1, Address = "Aliganj, Lucknow" },
                new Student { FullName = "Rohit Kumar", Email = "rohit.kumar@example.com", Phone = "9876543213", Course = "MCA", Semester = 2, Address = "Kankarbagh, Patna" }
            };
            await context.Students.AddRangeAsync(students);
            await context.SaveChangesAsync();

            // Seed Allocations
            var allocations = new Allocation[]
            {
                new Allocation { StudentId = students[0].StudentId, RoomId = rooms[0].RoomId, AllocationDate = DateTime.Today.AddMonths(-2), Status = "Active" },
                new Allocation { StudentId = students[1].StudentId, RoomId = rooms[1].RoomId, AllocationDate = DateTime.Today.AddMonths(-1), Status = "Active" }
            };
            await context.Allocations.AddRangeAsync(allocations);
            await context.SaveChangesAsync();

            // Seed Payments
            var payments = new Payment[]
            {
                new Payment { StudentId = students[0].StudentId, Amount = 6000, PaymentDate = DateTime.Today.AddDays(-10), PaymentMonth = "October 2026", PaymentStatus = "Paid", PaymentMethod = "UPI" },
                new Payment { StudentId = students[1].StudentId, Amount = 4500, PaymentDate = DateTime.Today.AddDays(-5), PaymentMonth = "October 2026", PaymentStatus = "Pending", PaymentMethod = "Cash" },
                new Payment { StudentId = students[2].StudentId, Amount = 4500, PaymentDate = DateTime.Today.AddDays(-2), PaymentMonth = "October 2026", PaymentStatus = "Pending", PaymentMethod = "Net Banking" }
            };
            await context.Payments.AddRangeAsync(payments);
            await context.SaveChangesAsync();

            // Seed Maintenance Requests
            var maintenanceRequests = new MaintenanceRequest[]
            {
                new MaintenanceRequest { RoomId = rooms[4].RoomId, StudentId = null, IssueTitle = "Ceiling Fan Repair", Description = "Fan regulator is not working and making noise.", RequestDate = DateTime.Today.AddDays(-3), Status = "Pending" },
                new MaintenanceRequest { RoomId = rooms[1].RoomId, StudentId = students[1].StudentId, IssueTitle = "Bathroom Tap Leakage", Description = "Water is slowly leaking from the wash basin tap.", RequestDate = DateTime.Today.AddDays(-1), Status = "Pending" }
            };
            await context.MaintenanceRequests.AddRangeAsync(maintenanceRequests);
            await context.SaveChangesAsync();
        }

        private static async Task EnsureUsersTableAsync(HostelDbContext context)
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

                await context.Database.ExecuteSqlRawAsync(sql);

                // Insert default admin and user directly via SQL if table was just created
                const string seedAdminSql = @"
                    INSERT IGNORE INTO `Users` (`Username`, `Password`, `FullName`, `Email`, `Role`)
                    VALUES 
                    ('yash', 'yash', 'Yash (Admin)', 'yash@hostelcare.com', 'Admin'),
                    ('user', 'user123', 'Rahul Sharma', 'rahul.sharma@example.com', 'User');";

                await context.Database.ExecuteSqlRawAsync(seedAdminSql);
            }
            catch
            {
                // Ignore if already created or unable to run raw sql
            }
        }
    }
}
