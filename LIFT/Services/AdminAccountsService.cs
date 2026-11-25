using LIFT.Data;
using LIFT.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LIFT.Services;

public sealed class AdminAccountsService(LiftDbContext db, IPasswordHasher<LiftUser> hasher,
    IWebHostEnvironment environment, ILogger<AdminAccountsService> logger) : IAdminAccountsService
{
    public async Task<AdminProfileVm?> GetProfileAsync(Guid adminId, CancellationToken ct)
        => await db.Users.AsNoTracking().Where(x => x.Id == adminId && x.Role == "admin" && x.Status == "active")
            .Select(x => new AdminProfileVm(x.FullName, x.Email, x.PhotoUrl)).SingleOrDefaultAsync(ct);

    public async Task<string?> SavePhotoAsync(Guid adminId, IFormFile? photo, CancellationToken ct)
    {
        if (photo is null || photo.Length == 0) return "Choose a photo to upload.";
        if (photo.Length > 2 * 1024 * 1024) return "Photo must be 2 MB or smaller.";
        var extension = Path.GetExtension(photo.FileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp")) return "Use a JPG, PNG, or WebP photo.";
        var header = new byte[12];
        await using (var input = photo.OpenReadStream())
        {
            if (await input.ReadAsync(header.AsMemory(), ct) < 12) return "Photo file is invalid.";
        }
        var jpg = header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff;
        var png = header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var webp = System.Text.Encoding.ASCII.GetString(header, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(header, 8, 4) == "WEBP";
        if (extension is ".jpg" or ".jpeg" ? !jpg : extension == ".png" ? !png : !webp) return "Photo file is invalid.";
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == adminId && x.Role == "admin" && x.Status == "active", ct);
        if (user is null) return "Admin account is unavailable.";
        var root = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        var folder = Path.Combine(root, "uploads", "admin");
        Directory.CreateDirectory(folder);
        var name = $"{adminId:N}-{Guid.NewGuid():N}{extension}";
        var path = Path.Combine(folder, name);
        await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            await photo.CopyToAsync(output, ct);
        user.PhotoUrl = $"/uploads/admin/{name}";
        try { await db.SaveChangesAsync(ct); }
        catch { System.IO.File.Delete(path); throw; }
        logger.LogInformation("Admin {AdminId} updated own photo", adminId);
        return null;
    }

    public async Task<AdminAccountsVm> GetAsync(Guid currentUserId, CancellationToken ct)
    {
        var accounts = await db.Users.AsNoTracking().Where(x => x.Role == "admin")
            .OrderBy(x => x.CreatedAt)
            .Select(x => new AdminAccountVm(x.Id, x.FullName, x.Email, x.Status, x.CreatedAt))
            .ToListAsync(ct);
        return new AdminAccountsVm(accounts, currentUserId);
    }

    public async Task<string?> CreateAsync(CreateAdminForm form, Guid creatorId, CancellationToken ct)
    {
        var email = form.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Email == email, ct)) return "Email already belongs to an account.";
        var user = new LiftUser
        {
            Id = Guid.NewGuid(), FullName = form.FullName.Trim(), Email = email,
            Role = "admin", Status = "active", CreatedAt = DateTimeOffset.UtcNow
        };
        user.PasswordHash = hasher.HashPassword(user, form.Password);
        db.Users.Add(user);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return "This email could not be used."; }
        logger.LogInformation("Admin {CreatorId} created admin {NewAdminId}", creatorId, user.Id);
        return null;
    }

    public async Task<string?> SetStatusAsync(AdminStatusForm form, Guid actorId, CancellationToken ct)
    {
        if (form.UserId == actorId) return "You cannot change your own status.";
        var target = await db.Users.SingleOrDefaultAsync(x => x.Id == form.UserId && x.Role == "admin", ct);
        if (target is null) return "Admin account not found.";
        if (target.Status == form.Status) return null;
        target.Status = form.Status;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Admin {ActorId} changed admin {TargetId} status to {Status}", actorId, target.Id, form.Status);
        return null;
    }

    public async Task<string?> ChangePasswordAsync(AdminPasswordForm form, Guid adminId, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == adminId && x.Role == "admin" && x.Status == "active", ct);
        if (user is null) return "Admin account is unavailable.";
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, form.CurrentPassword) == PasswordVerificationResult.Failed)
            return "Current password is incorrect.";
        user.PasswordHash = hasher.HashPassword(user, form.NewPassword);
        user.AuthVersion++;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Admin {AdminId} changed own password", adminId);
        return null;
    }
}
