using System.Data;
using LIFT.Data;
using LIFT.Models;
using Microsoft.EntityFrameworkCore;

namespace LIFT.Services;

public sealed class AdminChangesService(LiftDbContext db, ILogger<AdminChangesService> logger) : IAdminChangesService
{
    public async Task<AdminChangesVm> GetAsync(CancellationToken ct)
    {
        var requests = await (
            from request in db.SpecialistChangeRequests.AsNoTracking()
            join assignment in db.SpecialistAssignments.AsNoTracking() on request.AssignmentId equals assignment.Id
            join client in db.Users.AsNoTracking() on request.ClientId equals client.Id
            join specialist in db.Users.AsNoTracking() on assignment.SpecialistId equals specialist.Id
            where request.Status == "pending"
            orderby request.RequestedAt
            select new AdminChangeVm(request.Id, request.ClientId, client.FullName, specialist.Id,
                assignment.SpecialistRole, specialist.FullName, request.Reason, request.RequestedAt)
        ).Take(50).ToListAsync(ct);

        var candidates = await (
            from profile in db.SpecialistProfiles.AsNoTracking()
            join user in db.Users.AsNoTracking() on profile.UserId equals user.Id
            where (user.Role == "coach" || user.Role == "nutritionist") && user.Status == "active"
                && profile.ApprovedAt != null && profile.AcceptingNewClients
            select new { user.Id, user.FullName, user.Role, profile.Specialty, profile.Capacity }
        ).ToListAsync(ct);
        var activeCounts = await db.SpecialistAssignments.AsNoTracking()
            .Where(x => x.Status == "active")
            .GroupBy(x => x.SpecialistId)
            .Select(group => new { SpecialistId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.SpecialistId, x => x.Count, ct);
        var replacements = candidates.Select(x => new AdminReplacementVm(x.Id, x.FullName,
            x.Role, x.Specialty, x.Capacity - activeCounts.GetValueOrDefault(x.Id)))
            .Where(x => x.AvailableSlots > 0).ToList();
        return new AdminChangesVm(requests, replacements);
    }

    public async Task<string?> DecideAsync(AdminChangeDecisionForm form, Guid adminId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var request = await db.SpecialistChangeRequests.SingleOrDefaultAsync(x => x.Id == form.RequestId, ct);
        if (request is null || request.Status != "pending") return "Pending request not found.";
        var assignment = await db.SpecialistAssignments.SingleOrDefaultAsync(x => x.Id == request.AssignmentId, ct);
        if (assignment is null || assignment.ClientId != request.ClientId || assignment.Status != "active")
            return "The current assignment is no longer active.";

        if (form.Decision == "decline")
        {
            request.Status = "declined";
            request.ReviewedBy = adminId;
            request.ReviewedAt = DateTimeOffset.UtcNow;
            request.AdminNote = form.AdminNote?.Trim();
            db.Notifications.Add(Notify(request.ClientId, "Specialist change declined", "Your change request was reviewed."));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            logger.LogInformation("Admin {AdminId} declined change request {RequestId}", adminId, request.Id);
            return null;
        }

        if (form.ReplacementSpecialistId is not Guid replacementId || replacementId == assignment.SpecialistId)
            return "Choose a different replacement specialist.";
        var subscriptionActive = await db.Subscriptions.AnyAsync(x =>
            x.Id == assignment.SubscriptionId && x.ClientId == assignment.ClientId && x.Status == "active", ct);
        if (!subscriptionActive) return "The client subscription is no longer active.";
        var candidate = await (
            from user in db.Users
            join profile in db.SpecialistProfiles on user.Id equals profile.UserId
            where user.Id == replacementId && user.Role == assignment.SpecialistRole && user.Status == "active"
                && profile.ApprovedAt != null && profile.AcceptingNewClients
            select new { profile.Capacity }
        ).SingleOrDefaultAsync(ct);
        if (candidate is null) return "The replacement specialist is unavailable.";
        var activeCount = await db.SpecialistAssignments.CountAsync(x =>
            x.SpecialistId == replacementId && x.Status == "active", ct);
        if (activeCount >= candidate.Capacity) return "The replacement specialist has no free slots.";

        assignment.Status = "ended";
        assignment.EndedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        var replacement = new SpecialistAssignment
        {
            Id = Guid.NewGuid(), ClientId = assignment.ClientId,
            SubscriptionId = assignment.SubscriptionId, SpecialistId = replacementId,
            SpecialistRole = assignment.SpecialistRole, Status = "active",
            AssignedAt = DateTimeOffset.UtcNow
        };
        db.SpecialistAssignments.Add(replacement);
        request.Status = "completed";
        request.ReviewedBy = adminId;
        request.ReviewedAt = DateTimeOffset.UtcNow;
        request.AdminNote = form.AdminNote?.Trim();
        request.ReplacementAssignmentId = replacement.Id;
        db.Notifications.Add(Notify(request.ClientId, "Specialist changed", "Your new specialist is assigned."));
        db.Notifications.Add(Notify(replacementId, "New client assigned", "A client has been assigned to you."));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        logger.LogInformation("Admin {AdminId} completed change request {RequestId}", adminId, request.Id);
        return null;
    }

    private static LiftNotification Notify(Guid userId, string title, string body) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, Type = "specialist_change",
        Title = title, Body = body, CreatedAt = DateTimeOffset.UtcNow
    };
}
