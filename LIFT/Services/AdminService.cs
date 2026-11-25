using LIFT.Data;
using LIFT.Models;
using Microsoft.EntityFrameworkCore;

namespace LIFT.Services;

public sealed class AdminService(LiftDbContext db, ILogger<AdminService> logger, IWebHostEnvironment environment) : IAdminService
{
    public async Task<AdminDashboardVm> GetDashboardAsync(string? search, int specialistPage,
        int clientPage, int subscriptionPage, CancellationToken ct)
    {
        const int pageSize = 12;
        search = (search ?? "").Trim();
        if (search.Length > 100) search = search[..100];
        specialistPage = Math.Max(1, specialistPage);
        clientPage = Math.Max(1, clientPage);
        subscriptionPage = Math.Max(1, subscriptionPage);
        var activeClients = await db.Users.CountAsync(x => x.Role == "client" && x.Status == "active", ct);
        var activeCoaches = await db.Users.CountAsync(x => x.Role == "coach" && x.Status == "active", ct);
        var activeNutritionists = await db.Users.CountAsync(x => x.Role == "nutritionist" && x.Status == "active", ct);
        var pendingSpecialists = await db.Users.CountAsync(x =>
            (x.Role == "coach" || x.Role == "nutritionist") && x.Status == "pending", ct);
        var activeSubscriptions = await db.Subscriptions.CountAsync(x => x.Status == "active", ct);
        var pendingChanges = await db.SpecialistChangeRequests.CountAsync(x => x.Status == "pending", ct);

        // Use actual client registration dates. Keep the first eleven months complete;
        // the current month is explicitly marked as month-to-date in the view.
        var now = DateTimeOffset.UtcNow;
        var thisMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var firstMonth = thisMonth.AddMonths(-11);
        var clientsBeforeWindow = await db.Users.AsNoTracking().CountAsync(
            x => x.Role == "client" && x.CreatedAt < firstMonth, ct);
        var recentRegistrations = await db.Users.AsNoTracking()
            .Where(x => x.Role == "client" && x.CreatedAt >= firstMonth && x.CreatedAt <= now)
            .Select(x => x.CreatedAt).ToListAsync(ct);
        var monthlyCounts = recentRegistrations.GroupBy(date => (date.Year, date.Month))
            .ToDictionary(group => group.Key, group => group.Count());
        var monthlyGrowth = new List<AdminMonthlyGrowthVm>(12);
        var runningTotal = clientsBeforeWindow;
        for (var offset = 0; offset < 12; offset++)
        {
            var month = firstMonth.AddMonths(offset);
            var newUsers = monthlyCounts.GetValueOrDefault((month.Year, month.Month));
            runningTotal += newUsers;
            monthlyGrowth.Add(new AdminMonthlyGrowthVm(month.Year, month.Month, newUsers, runningTotal));
        }
        var paidPayments = await db.Payments.AsNoTracking()
            .Where(x => x.Status == "succeeded" && x.CompletedAt >= firstMonth && x.CompletedAt <= now)
            .Select(x => new { x.Amount, x.Currency, x.CompletedAt }).ToListAsync(ct);
        var monthlyRevenue = new List<AdminMonthlyRevenueVm>(12);
        for (var offset = 0; offset < 12; offset++)
        {
            var month = firstMonth.AddMonths(offset);
            var daily = Enumerable.Range(1, DateTime.DaysInMonth(month.Year, month.Month))
                .Select(day => paidPayments.Where(x => x.CompletedAt!.Value.Year == month.Year && x.CompletedAt.Value.Month == month.Month && x.CompletedAt.Value.Day == day && x.Currency == "USD").Sum(x => x.Amount)).ToList();
            monthlyRevenue.Add(new AdminMonthlyRevenueVm(month.Year, month.Month, daily.Sum(), "USD", daily));
        }

        var specialistsQuery =
            from user in db.Users.AsNoTracking()
            join profile in db.SpecialistProfiles.AsNoTracking() on user.Id equals profile.UserId
            where user.Role == "coach" || user.Role == "nutritionist"
            select new { user, profile };
        var pending = await specialistsQuery.Where(x => x.user.Status == "pending")
            .OrderBy(x => x.user.CreatedAt)
            .Select(x => new AdminSpecialistVm(x.user.Id, x.user.FullName, x.user.Email, x.user.Role,
                x.user.Status, x.profile.Specialty, x.profile.Capacity, x.profile.AcceptingNewClients,
                db.SpecialistCertificates.Count(c => c.SpecialistId == x.user.Id), x.user.CreatedAt))
            .ToListAsync(ct);
        var activeQuery = specialistsQuery.Where(x => x.user.Status == "active" || x.user.Status == "suspended" || x.user.Status == "blocked");
        if (search.Length > 0) activeQuery = activeQuery.Where(x => x.user.FullName.Contains(search) || x.user.Email.Contains(search));
        var specialistCount = await activeQuery.CountAsync(ct);
        var specialistPages = Math.Max(1, (specialistCount + pageSize - 1) / pageSize);
        specialistPage = Math.Min(specialistPage, specialistPages);
        var specialists = await activeQuery.OrderByDescending(x => x.user.CreatedAt).ThenBy(x => x.user.Id)
            .Skip((specialistPage - 1) * pageSize).Take(pageSize)
            .Select(x => new AdminSpecialistVm(x.user.Id, x.user.FullName, x.user.Email, x.user.Role,
                x.user.Status, x.profile.Specialty, x.profile.Capacity, x.profile.AcceptingNewClients,
                db.SpecialistCertificates.Count(c => c.SpecialistId == x.user.Id), x.user.CreatedAt))
            .ToListAsync(ct);

        var clientsQuery = db.Users.AsNoTracking().Where(user => user.Role == "client");
        if (search.Length > 0) clientsQuery = clientsQuery.Where(x => x.FullName.Contains(search) || x.Email.Contains(search));
        var clientCount = await clientsQuery.CountAsync(ct);
        var clientPages = Math.Max(1, (clientCount + pageSize - 1) / pageSize);
        clientPage = Math.Min(clientPage, clientPages);
        var clients = await clientsQuery.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((clientPage - 1) * pageSize).Take(pageSize)
            .Select(user => new AdminClientVm(user.Id, user.FullName, user.Email, user.Status,
                (from sub in db.Subscriptions
                 join plan in db.SubscriptionPlans on sub.PlanId equals plan.Id
                 where sub.ClientId == user.Id && sub.Status == "active"
                 select plan.Name).FirstOrDefault(), user.CreatedAt))
            .ToListAsync(ct);
        var clientIds = clients.Select(x => x.Id).ToList();
        var profiles = await db.ClientProfiles.AsNoTracking().Where(x => clientIds.Contains(x.UserId)).ToDictionaryAsync(x => x.UserId, ct);
        var phones = await db.Users.AsNoTracking().Where(x => clientIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Phone, ct);
        var days = await db.ClientTrainingDays.AsNoTracking().Where(x => clientIds.Contains(x.ClientId)).ToListAsync(ct);
        clients = clients.Select(client =>
        {
            profiles.TryGetValue(client.Id, out var profile);
            return client with { Profile = new ClientProfileForm {
                FullName = client.Name, Phone = phones.GetValueOrDefault(client.Id), Goal = profile?.Goal,
                GoalNote = profile?.GoalNote, BirthDate = profile?.BirthDate, HeightCm = profile?.HeightCm,
                StartingWeightKg = profile?.StartingWeightKg, FitnessLevel = profile?.FitnessLevel,
                ActivityLevel = profile?.ActivityLevel, TrainingLocation = profile?.TrainingLocation,
                SessionMinutes = profile?.SessionMinutes, PreferredTrainingTime = profile?.PreferredTrainingTime,
                SleepHours = profile?.SleepHours, ScheduleNotes = profile?.ScheduleNotes, HealthNotes = profile?.HealthNotes,
                FoodHabits = profile?.FoodHabits, DietaryPreferences = profile?.DietaryPreferences,
                CookingStyle = profile?.CookingStyle, Sport = profile?.Sport,
                TrainingDays = days.Where(x => x.ClientId == client.Id).Select(x => x.DayOfWeek).ToList()
            } };
        }).ToList();

        var subscriptionsQuery =
            from sub in db.Subscriptions.AsNoTracking()
            join user in db.Users.AsNoTracking() on sub.ClientId equals user.Id
            join plan in db.SubscriptionPlans.AsNoTracking() on sub.PlanId equals plan.Id
            select new { sub, user, plan };
        if (search.Length > 0) subscriptionsQuery = subscriptionsQuery.Where(x => x.user.FullName.Contains(search));
        var subscriptionCount = await subscriptionsQuery.CountAsync(ct);
        var subscriptionPages = Math.Max(1, (subscriptionCount + pageSize - 1) / pageSize);
        subscriptionPage = Math.Min(subscriptionPage, subscriptionPages);
        var subscriptions = await subscriptionsQuery.OrderByDescending(x => x.sub.StartsAt).ThenBy(x => x.sub.Id)
            .Skip((subscriptionPage - 1) * pageSize).Take(pageSize)
            .Select(x => new AdminSubscriptionVm(x.sub.Id, x.user.FullName, x.plan.Name, x.sub.Status,
                x.sub.PriceAtStart, x.sub.Currency, x.sub.StartsAt))
            .ToListAsync(ct);

        return new AdminDashboardVm(activeClients, activeCoaches, activeNutritionists,
            pendingSpecialists, activeSubscriptions, pendingChanges,
            pending, specialists, clients, subscriptions, monthlyGrowth, monthlyRevenue,
            search, specialistPage, specialistPages, clientPage, clientPages,
            subscriptionPage, subscriptionPages);
    }

    public async Task<AdminReviewVm?> GetReviewAsync(Guid specialistId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == specialistId, ct);
        var profile = await db.SpecialistProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == specialistId, ct);
        if (user is null || profile is null || (user.Role != "coach" && user.Role != "nutritionist")) return null;
        var certificates = await db.SpecialistCertificates.AsNoTracking()
            .Where(x => x.SpecialistId == specialistId).OrderBy(x => x.UploadedAt)
            .Select(x => new AdminCertificateVm(x.Id, x.OriginalFileName, x.UploadedAt))
            .ToListAsync(ct);
        return new AdminReviewVm(user.Id, user.FullName, user.Email, user.Role, user.Status,
            profile.Specialty, profile.Bio, profile.ExperienceYears, profile.Capacity, certificates);
    }

    public async Task<CertificateDownload?> GetCertificateAsync(Guid certificateId, CancellationToken ct)
    {
        var certificate = await db.SpecialistCertificates.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == certificateId, ct);
        if (certificate is null) return null;
        var path = CertificatePath(certificate.StorageKey);
        if (path is null || !File.Exists(path)) return null;
        var file = new FileInfo(path);
        if (file.Length > 10 * 1024 * 1024) return null;
        return new CertificateDownload(await File.ReadAllBytesAsync(path, ct), certificate.OriginalFileName);
    }

    private string? CertificatePath(string storageKey)
    {
        if (Path.GetFileName(storageKey) != storageKey ||
            !string.Equals(Path.GetExtension(storageKey), ".pdf", StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParse(Path.GetFileNameWithoutExtension(storageKey), out _)) return null;
        return Path.Combine(environment.ContentRootPath, "App_Data", "certificates", storageKey);
    }
    public async Task<string?> ReviewSpecialistAsync(AdminSpecialistDecisionForm form, Guid reviewerId, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == form.SpecialistId, ct);
        var profile = await db.SpecialistProfiles.SingleOrDefaultAsync(x => x.UserId == form.SpecialistId, ct);
        if (user is null || profile is null || user.Status != "pending" ||
            (user.Role != "coach" && user.Role != "nutritionist"))
            return "The pending specialist was not found.";
        if (form.Decision == "approve")
        {
            var certificates = await db.SpecialistCertificates.AsNoTracking()
                .Where(x => x.SpecialistId == user.Id).Select(x => x.StorageKey).ToListAsync(ct);
            if (certificates.Count == 0 || certificates.Any(key => CertificatePath(key) is not { } path || !File.Exists(path)))
                return "Uploaded certificate files must be available before approval.";
        }

        user.Status = form.Decision == "approve" ? "active" : "rejected";
        profile.ReviewedBy = reviewerId;
        profile.ApprovedAt = form.Decision == "approve" ? DateTimeOffset.UtcNow : null;
        db.Notifications.Add(new LiftNotification
        {
            Id = Guid.NewGuid(), UserId = user.Id, Type = "specialist_review",
            Title = form.Decision == "approve" ? "Application approved" : "Application reviewed",
            Body = form.Decision == "approve"
                ? "Your specialist account is now active."
                : "Your specialist application was not approved.",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Admin {AdminId} {Decision} specialist {SpecialistId}", reviewerId, form.Decision, user.Id);
        return null;
    }

    public async Task<string?> ChangeAccountStatusAsync(AdminAccountStatusForm form, Guid adminId, CancellationToken ct)
    {
        if (form.UserId == adminId) return "You cannot change your own account status.";
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == form.UserId, ct);
        if (user is null || user.Role == "admin") return "The account was not found or cannot be changed here.";
        if (user.Status == "pending" || user.Status == "rejected")
            return "Pending and rejected applications must use the review process.";
        if (user.Status == form.Status) return null;
        user.Status = form.Status;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Admin {AdminId} changed account {UserId} to {Status}", adminId, user.Id, form.Status);
        return null;
    }

    public async Task<string?> EditClientAsync(AdminClientEditForm form, Guid adminId, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == form.UserId && x.Role == "client", ct);
        if (user is null) return "Client account was not found.";
        var email = form.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Id != user.Id && x.Email == email, ct)) return "Email is already in use.";
        user.FullName = form.FullName.Trim();
        user.Email = email;
        user.Phone = form.Phone?.Trim();
        user.Status = form.Status;
        var profile = await db.ClientProfiles.SingleOrDefaultAsync(x => x.UserId == user.Id, ct);
        if (profile is null) { profile = new ClientProfile { UserId = user.Id }; db.ClientProfiles.Add(profile); }
        profile.BirthDate = form.BirthDate;
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Admin {AdminId} edited client {ClientId}", adminId, user.Id);
        return null;
    }
}
