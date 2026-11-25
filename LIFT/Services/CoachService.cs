using System.Data;
using LIFT.Data;
using LIFT.Models;
using Microsoft.EntityFrameworkCore;

namespace LIFT.Services;

public sealed class CoachService(LiftDbContext db) : ICoachService
{
    private Task<bool> Assigned(Guid coachId, Guid clientId) => db.SpecialistAssignments.AnyAsync(a =>
        a.SpecialistId == coachId && a.ClientId == clientId && a.SpecialistRole == "coach" && a.Status == "active" &&
        db.Subscriptions.Any(s => s.Id == a.SubscriptionId && s.Status == "active"));

    public async Task<CoachDashboardVm> GetDashboardAsync(Guid coachId)
    {
        var name = await db.Users.AsNoTracking().Where(x => x.Id == coachId && x.Role == "coach" && x.Status == "active")
            .Select(x => x.FullName).SingleAsync();
        var assignments = await (from a in db.SpecialistAssignments.AsNoTracking()
            join user in db.Users.AsNoTracking() on a.ClientId equals user.Id
            join profile in db.ClientProfiles.AsNoTracking() on a.ClientId equals profile.UserId
            where a.SpecialistId == coachId && a.SpecialistRole == "coach" && a.Status == "active" &&
                user.Status == "active" && db.Subscriptions.Any(s => s.Id == a.SubscriptionId && s.Status == "active")
            select new { a.ClientId, user.FullName, user.Email, profile.Goal, a.AssignedAt }).ToListAsync();
        var ids = assignments.Select(x => x.ClientId).ToArray();
        var plans = await db.PlanVersions.AsNoTracking().Where(x => ids.Contains(x.ClientId) && x.PlanType == "workout")
            .OrderByDescending(x => x.VersionNumber).ToListAsync();
        var clients = assignments.Select(a => new CoachClientVm(a.ClientId, a.FullName, a.Email, a.Goal,
            plans.FirstOrDefault(x => x.ClientId == a.ClientId)?.Status ?? "none", a.AssignedAt))
            .OrderBy(x => x.Name).ToList();
        return new CoachDashboardVm { Name = name, Clients = clients,
            PublishedPlans = plans.Where(x => x.Status == "approved" && x.PublishedAt != null)
                .Select(x => x.ClientId).Distinct().Count() };
    }

    public async Task<CoachClientDetailVm?> GetClientAsync(Guid coachId, Guid clientId)
    {
        var dashboard = await GetDashboardAsync(coachId);
        var client = dashboard.Clients.FirstOrDefault(x => x.ClientId == clientId);
        if (client is null) return null;
        var profile = await db.ClientProfiles.AsNoTracking().SingleAsync(x => x.UserId == clientId);
        var latest = await db.PlanVersions.AsNoTracking().Where(x => x.ClientId == clientId && x.PlanType == "workout")
            .OrderByDescending(x => x.VersionNumber).FirstOrDefaultAsync();
        var publishedVersion = await db.PlanVersions.AsNoTracking().Where(x => x.ClientId == clientId &&
            x.PlanType == "workout" && x.Status == "approved" && x.PublishedAt != null)
            .MaxAsync(x => (int?)x.VersionNumber);
        var latestItems = latest is null ? new List<WorkoutPlanItem>() : await db.WorkoutPlanItems.AsNoTracking()
            .Where(x => x.PlanVersionId == latest.Id).OrderBy(x => x.SortOrder).ToListAsync();
        var pending = await db.PlanVersions.AsNoTracking().Where(x => x.ClientId == clientId &&
            x.PlanType == "workout" && x.Source == "ai" && x.Status == "pending_review")
            .OrderByDescending(x => x.VersionNumber).Take(20).ToListAsync();
        var pendingIds = pending.Select(x => x.Id).ToArray();
        var pendingItems = await db.WorkoutPlanItems.AsNoTracking().Where(x => pendingIds.Contains(x.PlanVersionId))
            .OrderBy(x => x.SortOrder).ToListAsync();
        return new CoachClientDetailVm { Client = client, FitnessLevel = profile.FitnessLevel,
            ActivityLevel = profile.ActivityLevel, TrainingLocation = profile.TrainingLocation,
            SessionMinutes = profile.SessionMinutes, HealthNotes = profile.HealthNotes,
            PublishedVersion = publishedVersion,
            Form = new WorkoutPlanForm { ClientId = clientId, Title = latest?.Title ?? "Workout plan",
                Notes = latest?.Notes,
                ExercisesText = string.Join("\n", latestItems.Select(FormatItem)) },
            PendingAiPlans = pending.Select(p => new CoachPendingPlanVm(p.Id, p.VersionNumber, p.Title,
                p.Notes, p.CreatedAt, pendingItems.Where(i => i.PlanVersionId == p.Id)
                    .Select(i => new CoachExerciseVm(i.DayOfWeek, i.ExerciseName, i.Sets,
                        i.Reps, i.DurationMinutes, i.Instructions)).ToList())).ToList() };
    }

    private static string FormatItem(WorkoutPlanItem item) => string.Join(" | ",
        item.DayOfWeek, item.ExerciseName, item.Sets?.ToString() ?? "", item.Reps ?? "",
        item.DurationMinutes?.ToString() ?? "", item.Instructions ?? "");

    public async Task<bool> SavePlanAsync(Guid coachId, WorkoutPlanForm form)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (!await Assigned(coachId, form.ClientId)) return false;
        var version = (await db.PlanVersions.Where(x => x.ClientId == form.ClientId && x.PlanType == "workout")
            .MaxAsync(x => (int?)x.VersionNumber) ?? 0) + 1;
        var now = DateTimeOffset.UtcNow;
        var plan = new PlanVersion { Id = Guid.NewGuid(), ClientId = form.ClientId, PlanType = "workout",
            Source = "specialist", VersionNumber = version, CreatedBy = coachId,
            ReviewedBy = form.Publish ? coachId : null, Status = form.Publish ? "approved" : "draft",
            Title = form.Title.Trim(), Notes = form.Notes?.Trim(), CreatedAt = now,
            ApprovedAt = form.Publish ? now : null, PublishedAt = form.Publish ? now : null };
        db.PlanVersions.Add(plan);
        short order = 0;
        foreach (var line in form.ExercisesText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var p = line.Split('|', StringSplitOptions.TrimEntries);
            db.WorkoutPlanItems.Add(new WorkoutPlanItem { Id = Guid.NewGuid(), PlanVersionId = plan.Id,
                DayOfWeek = byte.Parse(p[0]), SortOrder = ++order, ExerciseName = p[1],
                Sets = p[2].Length == 0 ? null : short.Parse(p[2]), Reps = p[3].Length == 0 ? null : p[3],
                DurationMinutes = p[4].Length == 0 ? null : short.Parse(p[4]),
                Instructions = p[5].Length == 0 ? null : p[5] });
        }
        if (form.Publish) db.Notifications.Add(new LiftNotification { Id = Guid.NewGuid(),
            UserId = form.ClientId, Type = "workout_plan", Title = "Your workout plan is ready",
            Body = "Your coach published an updated workout plan.", TargetUrl = "/Client/Workout/workout.html", CreatedAt = now });
        await db.SaveChangesAsync(); await tx.CommitAsync(); return true;
    }

    public async Task<bool> ReviewAiPlanAsync(Guid coachId, CoachAiReviewForm form)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var plan = await db.PlanVersions.SingleOrDefaultAsync(x => x.Id == form.PlanId &&
            x.PlanType == "workout" && x.Source == "ai" && x.Status == "pending_review");
        if (plan is null || !await Assigned(coachId, plan.ClientId)) return false;
        var now = DateTimeOffset.UtcNow;
        plan.Status = form.Decision == "approve" ? "approved" : "rejected";
        plan.ReviewedBy = coachId;
        if (form.Decision == "approve")
        {
            plan.ApprovedAt = now; plan.PublishedAt = now;
            db.Notifications.Add(new LiftNotification { Id = Guid.NewGuid(), UserId = plan.ClientId,
                Type = "workout_plan", Title = "Workout plan approved",
                Body = "Your coach approved a new workout plan.", TargetUrl = "/Client/Workout/workout.html", CreatedAt = now });
        }
        await db.SaveChangesAsync(); await tx.CommitAsync(); return true;
    }
}
