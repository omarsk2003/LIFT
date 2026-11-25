using LIFT.Data;
using LIFT.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LIFT.Services;

public sealed class SpecialistRegistrationService(LiftDbContext db,
    IPasswordHasher<LiftUser> hasher, IWebHostEnvironment environment,
    ILogger<SpecialistRegistrationService> logger) : ISpecialistRegistrationService
{
    public async Task<string?> RegisterAsync(SpecialistRegistrationForm form, string role, CancellationToken ct)
    {
        if (role is not ("coach" or "nutritionist")) throw new ArgumentOutOfRangeException(nameof(role));
        if (form.Certificates.Count is < 1 or > 5) return "Upload between 1 and 5 PDF certificates.";
        foreach (var file in form.Certificates)
        {
            if (file.Length is < 5 or > 10485760 ||
                !string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
                return "Each certificate must be a PDF under 10 MB.";
            await using var stream = file.OpenReadStream();
            var signature = new byte[5];
            if (await stream.ReadAsync(signature, ct) != 5 ||
                signature[0] != 0x25 || signature[1] != 0x50 || signature[2] != 0x44 ||
                signature[3] != 0x46 || signature[4] != 0x2D)
                return "A certificate is not a valid PDF file.";
        }

        var email = form.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Email == email, ct)) return "An account with this email already exists.";

        var user = new LiftUser
        {
            Id = Guid.NewGuid(), FullName = form.FullName.Trim(), Email = email,
            Phone = form.Phone?.Trim(), Role = role, Status = "pending",
            CreatedAt = DateTimeOffset.UtcNow
        };
        user.PasswordHash = hasher.HashPassword(user, form.Password);
        var profile = new SpecialistProfile
        {
            UserId = user.Id, Specialty = form.Specialty.Trim(), Bio = form.Bio.Trim(),
            ExperienceYears = form.ExperienceYears, Capacity = form.Capacity,
            AcceptingNewClients = true
        };

        var folder = Path.Combine(environment.ContentRootPath, "App_Data", "certificates");
        Directory.CreateDirectory(folder);
        var savedPaths = new List<string>();
        try
        {
            foreach (var file in form.Certificates)
            {
                var storageKey = Guid.NewGuid().ToString("D") + ".pdf";
                var path = Path.Combine(folder, storageKey);
                await using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    await file.CopyToAsync(target, ct);
                savedPaths.Add(path);
                db.SpecialistCertificates.Add(new SpecialistCertificate
                {
                    Id = Guid.NewGuid(), SpecialistId = user.Id,
                    OriginalFileName = Path.GetFileName(file.FileName)[..Math.Min(Path.GetFileName(file.FileName).Length, 255)],
                    StorageKey = storageKey, UploadedAt = DateTimeOffset.UtcNow
                });
            }
            db.Users.Add(user);
            db.SpecialistProfiles.Add(profile);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("{Role} application {UserId} submitted", role, user.Id);
            return null;
        }
        catch (DbUpdateException)
        {
            foreach (var path in savedPaths) File.Delete(path);
            return "The application could not be saved. Check whether this email is already used.";
        }
        catch
        {
            foreach (var path in savedPaths) File.Delete(path);
            throw;
        }
    }
}
