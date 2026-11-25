using System.Data;
using LIFT.Data;
using LIFT.Models;
using Microsoft.EntityFrameworkCore;

namespace LIFT.Services;

public sealed class NutritionistService(LiftDbContext db) : INutritionistService
{
    public async Task<NutritionistDashboardVm> GetDashboardAsync(Guid specialistId)
    {
        var name = await db.Users.Where(x => x.Id == specialistId && x.Role == "nutritionist" && x.Status == "active")
            .Select(x => x.FullName).SingleAsync();
        var assignments = await (from a in db.SpecialistAssignments.AsNoTracking()
            join u in db.Users.AsNoTracking() on a.ClientId equals u.Id
            join p in db.ClientProfiles.AsNoTracking() on a.ClientId equals p.UserId
            where a.SpecialistId == specialistId && a.SpecialistRole == "nutritionist" && a.Status == "active" && u.Status == "active"
                && db.Subscriptions.Any(s => s.Id == a.SubscriptionId && s.Status == "active")
            select new { a.ClientId, u.FullName, u.Email, p.Goal, a.AssignedAt }).ToListAsync();
        var ids = assignments.Select(x => x.ClientId).ToArray();
        var plans = await db.PlanVersions.AsNoTracking()
            .Where(x => ids.Contains(x.ClientId) && x.PlanType == "nutrition")
            .OrderByDescending(x => x.VersionNumber).ToListAsync();
        var clients = assignments.Select(a =>
        {
            var plan = plans.FirstOrDefault(x => x.ClientId == a.ClientId);
            return new NutritionistClientVm(a.ClientId, a.FullName, a.Email, a.Goal,
                plan?.Status ?? "none", plan?.Calories, a.AssignedAt);
        }).OrderBy(x => x.Name).ToList();
        return new NutritionistDashboardVm { Name = name, Clients = clients,
            PublishedPlans = plans.Count(x => x.Status == "approved" && x.PublishedAt != null &&
                plans.Where(p => p.ClientId == x.ClientId && p.Status == "approved" && p.PublishedAt != null)
                    .Max(p => p.VersionNumber) == x.VersionNumber) };
    }

    public async Task<NutritionistClientDetailVm?> GetClientAsync(Guid specialistId, Guid clientId)
    {
        var dashboard = await GetDashboardAsync(specialistId);
        var client = dashboard.Clients.FirstOrDefault(x => x.ClientId == clientId);
        if (client is null) return null;
        var latest = await db.PlanVersions.AsNoTracking()
            .Where(x => x.ClientId == clientId && x.PlanType == "nutrition")
            .OrderByDescending(x => x.VersionNumber).FirstOrDefaultAsync();
        var published = await db.PlanVersions.AsNoTracking()
            .Where(x => x.ClientId == clientId && x.PlanType == "nutrition" && x.Status == "approved" && x.PublishedAt != null)
            .OrderByDescending(x => x.VersionNumber).FirstOrDefaultAsync();
        var pending = await db.PlanVersions.AsNoTracking()
            .Where(x => x.ClientId == clientId && x.PlanType == "nutrition" && x.Source == "ai" && x.Status == "pending_review")
            .OrderByDescending(x => x.VersionNumber)
            .Select(x => new PendingNutritionPlanVm(x.Id, x.VersionNumber, x.Title, x.Calories,
                x.ProteinGrams, x.CarbsGrams, x.FatGrams, x.Notes, x.CreatedAt))
            .Take(20).ToListAsync();
        var pendingIds = pending.Select(x => x.Id).ToArray();
        var pendingMeals = await db.NutritionPlanMeals.AsNoTracking()
            .Where(x => pendingIds.Contains(x.PlanVersionId))
            .OrderBy(x => x.SortOrder).ToListAsync();
        foreach (var item in pending)
            item.Meals = pendingMeals.Where(x => x.PlanVersionId == item.Id)
                .Select(x => new NutritionMealVm(x.MealName, x.Description)).ToList();
        var meals = latest is null ? [] : await db.NutritionPlanMeals.AsNoTracking()
            .Where(x => x.PlanVersionId == latest.Id).OrderBy(x => x.SortOrder).ToListAsync();
        var meal = meals.FirstOrDefault();
        return new NutritionistClientDetailVm { Client = client, PublishedVersion = published?.VersionNumber, PendingAiPlans = pending,
            Form = new NutritionPlanForm { ClientId = clientId, Title = latest?.Title ?? "Nutrition plan",
                Notes = latest?.Notes, Calories = latest?.Calories ?? 2000,
                ProteinGrams = latest?.ProteinGrams ?? 0, CarbsGrams = latest?.CarbsGrams ?? 0,
                FatGrams = latest?.FatGrams ?? 0, MealName = meal?.MealName, MealDescription = meal?.Description,
                MealsText = string.Join("\n", meals.Select(x => x.MealName +
                    (string.IsNullOrWhiteSpace(x.Description) ? "" : ": " + x.Description))) } };
    }

    public async Task<bool> SavePlanAsync(Guid specialistId, NutritionPlanForm form)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var assigned = await db.SpecialistAssignments.AnyAsync(x => x.SpecialistId == specialistId &&
            x.ClientId == form.ClientId && x.SpecialistRole == "nutritionist" && x.Status == "active" &&
            db.Subscriptions.Any(s => s.Id == x.SubscriptionId && s.Status == "active"));
        if (!assigned) return false;
        var lastVersion = await db.PlanVersions.Where(x => x.ClientId == form.ClientId && x.PlanType == "nutrition")
            .MaxAsync(x => (int?)x.VersionNumber) ?? 0;
        var now = DateTimeOffset.UtcNow;
        var plan = new PlanVersion { Id = Guid.NewGuid(), ClientId = form.ClientId,
            VersionNumber = lastVersion + 1, CreatedBy = specialistId,
            ReviewedBy = form.Publish ? specialistId : null,
            Status = form.Publish ? "approved" : "draft", Title = form.Title.Trim(),
            Notes = form.Notes?.Trim(), Calories = form.Calories, ProteinGrams = form.ProteinGrams,
            CarbsGrams = form.CarbsGrams, FatGrams = form.FatGrams, CreatedAt = now,
            ApprovedAt = form.Publish ? now : null, PublishedAt = form.Publish ? now : null };
        db.PlanVersions.Add(plan);
        var lines = (form.MealsText ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length > 0)
        {
            for (short i = 0; i < lines.Length; i++)
            {
                var parts = lines[i].Split(':', 2, StringSplitOptions.TrimEntries);
                db.NutritionPlanMeals.Add(new NutritionPlanMeal { Id = Guid.NewGuid(), PlanVersionId = plan.Id,
                    SortOrder = (short)(i + 1), MealName = parts[0], Description = parts.Length > 1 ? parts[1] : null });
            }
        }
        else if (!string.IsNullOrWhiteSpace(form.MealName)) db.NutritionPlanMeals.Add(new NutritionPlanMeal
        { Id = Guid.NewGuid(), PlanVersionId = plan.Id, SortOrder = 1,
            MealName = form.MealName.Trim(), Description = form.MealDescription?.Trim() });
        if (form.Publish) db.Notifications.Add(new LiftNotification { Id = Guid.NewGuid(),
            UserId = form.ClientId, Type = "nutrition_plan", Title = "Your nutrition plan is ready",
            Body = "Your nutritionist published an updated nutrition plan.",
            TargetUrl = "/Client/Nutrition/nutrition.html", CreatedAt = now });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return true;
    }

    public async Task<bool> ReviewAiPlanAsync(Guid specialistId, NutritionAiReviewForm form)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var plan = await db.PlanVersions.SingleOrDefaultAsync(x => x.Id == form.PlanId &&
            x.PlanType == "nutrition" && x.Source == "ai" && x.Status == "pending_review");
        if (plan is null) return false;
        var assigned = await db.SpecialistAssignments.AnyAsync(x => x.SpecialistId == specialistId &&
            x.ClientId == plan.ClientId && x.SpecialistRole == "nutritionist" && x.Status == "active" &&
            db.Subscriptions.Any(s => s.Id == x.SubscriptionId && s.Status == "active"));
        if (!assigned) return false;
        var now = DateTimeOffset.UtcNow;
        plan.Status = form.Decision == "approve" ? "approved" : "rejected";
        plan.ReviewedBy = specialistId;
        if (form.Decision == "approve")
        {
            plan.ApprovedAt = now; plan.PublishedAt = now;
            db.Notifications.Add(new LiftNotification { Id = Guid.NewGuid(), UserId = plan.ClientId,
                Type = "nutrition_plan", Title = "Nutrition plan approved",
                Body = "Your nutritionist approved a new nutrition plan.",
                TargetUrl = "/Client/Nutrition/nutrition.html", CreatedAt = now });
        }
        await db.SaveChangesAsync(); await tx.CommitAsync(); return true;
    }
}
