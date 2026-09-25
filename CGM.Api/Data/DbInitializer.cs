using CGM.Api.Models.Entities;
using CGM.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace CGM.Api.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(CgmDbContext context, IPasswordHasher passwordHasher)
    {
        try
        {
            // Ensure tables exist using migrations or external schema scripts for MySQL

            var testEmail = "faizanhassan47@gmail.com";
            var existingUser = await context.Users.FirstOrDefaultAsync(u => u.Email == testEmail);

            if (existingUser == null)
            {
                var user = new User
                {
                    FullName = "Faizan Hassan",
                    Email = testEmail,
                    PasswordHash = passwordHasher.HashPassword("Test1234"),
                    AuthProvider = "Email",
                    EmailVerified = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                context.Users.Add(user);
                await context.SaveChangesAsync();

                var profile = new PatientProfileEntity
                {
                    UserId = user.Id,
                    PreferredGlucoseUnit = "mg/dL",
                    Language = "English",
                    Theme = "System",
                    ProfileCompleted = true,
                    CreatedAt = DateTime.UtcNow
                };

                context.PatientProfiles.Add(profile);
                await context.SaveChangesAsync();

                Console.WriteLine($"[DbInitializer] Successfully seeded user: {testEmail}");
            }
            else
            {
                existingUser.PasswordHash = passwordHasher.HashPassword("Test1234");
                existingUser.EmailVerified = true;
                existingUser.IsActive = true;
                existingUser.UpdatedAt = DateTime.UtcNow;
                await context.SaveChangesAsync();

                Console.WriteLine($"[DbInitializer] Successfully updated user password for: {testEmail}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DbInitializer] Error seeding database: {ex.Message}");
        }
    }

}
