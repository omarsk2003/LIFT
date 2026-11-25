using LIFT.Data;
using LIFT.Models;
using Microsoft.EntityFrameworkCore;

namespace LIFT.Services;

public sealed class ClientService(LiftDbContext db) : IClientService
{
    private async Task<(string? Code, string? Name)> ActivePlan(Guid id)
    {
        var result = await (from sub in db.Subscriptions.AsNoTracking()
            join plan in db.SubscriptionPlans.AsNoTracking() on sub.PlanId equals plan.Id
            where sub.ClientId == id && sub.Status == "active"
            select new { plan.Code, plan.Name }).SingleOrDefaultAsync();
        return (result?.Code, result?.Name);
    }

    public async Task<ClientDashboardVm> GetDashboardAsync(Guid id)
    {
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == id && x.Role == "client");
        var profile = await db.ClientProfiles.AsNoTracking().SingleAsync(x => x.UserId == id);
        var plan = await ActivePlan(id);
        var assignments = await (from a in db.SpecialistAssignments.AsNoTracking()
            join specialist in db.Users.AsNoTracking() on a.SpecialistId equals specialist.Id
            where a.ClientId == id && a.Status == "active" && specialist.Status == "active"
            select new { a.SpecialistRole, specialist.FullName }).ToListAsync();
        return new ClientDashboardVm { Name = user.FullName, PlanName = plan.Name ?? "Free",
            CoachName = assignments.FirstOrDefault(x => x.SpecialistRole == "coach")?.FullName,
            NutritionistName = assignments.FirstOrDefault(x => x.SpecialistRole == "nutritionist")?.FullName,
            ActivityCount = await db.ActivityLogs.CountAsync(x => x.ClientId == id),
            UnreadNotifications = await db.Notifications.CountAsync(x => x.UserId == id && x.ReadAt == null),
            ProfileComplete = profile.ProfileCompletedAt != null };
    }

    public async Task<ClientProfileVm> GetProfileAsync(Guid id)
    {
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == id && x.Role == "client");
        var p = await db.ClientProfiles.AsNoTracking().SingleAsync(x => x.UserId == id);
        var days = await db.ClientTrainingDays.AsNoTracking().Where(x => x.ClientId == id)
            .OrderBy(x => x.DayOfWeek).Select(x => x.DayOfWeek).ToListAsync();
        return new ClientProfileVm { Email = user.Email, Form = new ClientProfileForm {
            FullName = user.FullName, Phone = user.Phone, Goal = p.Goal, GoalNote = p.GoalNote,
            BirthDate = p.BirthDate, HeightCm = p.HeightCm, StartingWeightKg = p.StartingWeightKg,
            FitnessLevel = p.FitnessLevel, ActivityLevel = p.ActivityLevel,
            TrainingLocation = p.TrainingLocation, SessionMinutes = p.SessionMinutes,
            PreferredTrainingTime = p.PreferredTrainingTime, SleepHours = p.SleepHours,
            ScheduleNotes = p.ScheduleNotes, HealthNotes = p.HealthNotes,
            FoodHabits = p.FoodHabits, DietaryPreferences = p.DietaryPreferences,
            CookingStyle = p.CookingStyle, Sport = p.Sport, TrainingDays = days } };
    }

    public async Task SaveProfileAsync(Guid id, ClientProfileForm form)
    {
        var days = form.TrainingDays.Distinct().ToArray();
        if (days.Any(x => x is < 1 or > 7)) throw new ArgumentException("Invalid training day.");
        await using var tx = await db.Database.BeginTransactionAsync();
        var user = await db.Users.SingleAsync(x => x.Id == id && x.Role == "client");
        var p = await db.ClientProfiles.SingleAsync(x => x.UserId == id);
        user.FullName = form.FullName.Trim(); user.Phone = form.Phone?.Trim();
        p.Goal = form.Goal?.Trim(); p.GoalNote = form.GoalNote?.Trim(); p.BirthDate = form.BirthDate;
        p.HeightCm = form.HeightCm; p.StartingWeightKg = form.StartingWeightKg;
        p.FitnessLevel = form.FitnessLevel?.Trim(); p.ActivityLevel = form.ActivityLevel?.Trim();
        p.TrainingLocation = form.TrainingLocation?.Trim(); p.SessionMinutes = form.SessionMinutes;
        p.PreferredTrainingTime = form.PreferredTrainingTime?.Trim(); p.SleepHours = form.SleepHours;
        p.ScheduleNotes = form.ScheduleNotes?.Trim(); p.HealthNotes = form.HealthNotes?.Trim();
        p.FoodHabits = form.FoodHabits?.Trim(); p.DietaryPreferences = form.DietaryPreferences?.Trim();
        p.CookingStyle = form.CookingStyle?.Trim(); p.Sport = form.Sport?.Trim();
        p.UpdatedAt = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(p.Goal) && p.FitnessLevel is not null && p.ActivityLevel is not null)
            p.ProfileCompletedAt ??= DateTimeOffset.UtcNow;
        var oldDays = await db.ClientTrainingDays.Where(x => x.ClientId == id).ToListAsync();
        db.ClientTrainingDays.RemoveRange(oldDays.Where(x => !days.Contains(x.DayOfWeek)));
        db.ClientTrainingDays.AddRange(days.Where(x => oldDays.All(o => o.DayOfWeek != x))
            .Select(x => new ClientTrainingDay { ClientId = id, DayOfWeek = x }));
        await db.SaveChangesAsync(); await tx.CommitAsync();
    }

    public async Task<ClientPlansVm> GetPlansAsync(Guid id)
    {
        var active = await ActivePlan(id);
        var result = new ClientPlansVm { PlanName = active.Name ?? "Free" };
        if (active.Code is null or "free") return result;
        var plans = await db.PlanVersions.AsNoTracking().Where(x => x.ClientId == id &&
            x.Status == "approved" && x.PublishedAt != null)
            .OrderByDescending(x => x.VersionNumber).ToListAsync();
        var workout = plans.FirstOrDefault(x => x.PlanType == "workout");
        var nutrition = plans.FirstOrDefault(x => x.PlanType == "nutrition");
        if (workout is not null)
        {
            result.WorkoutTitle = workout.Title; result.WorkoutVersion = workout.VersionNumber;
            result.WorkoutItems = await db.WorkoutPlanItems.AsNoTracking().Where(x => x.PlanVersionId == workout.Id)
                .OrderBy(x => x.DayOfWeek).ThenBy(x => x.SortOrder)
                .Select(x => new ClientWorkoutItemVm(x.DayOfWeek, x.ExerciseName, x.Sets,
                    x.Reps, x.DurationMinutes, x.Instructions)).ToListAsync();
        }
        if (nutrition is not null)
        {
            result.NutritionTitle = nutrition.Title; result.NutritionVersion = nutrition.VersionNumber;
            result.Calories = nutrition.Calories; result.ProteinGrams = nutrition.ProteinGrams;
            result.CarbsGrams = nutrition.CarbsGrams; result.FatGrams = nutrition.FatGrams;
            result.Meals = await db.NutritionPlanMeals.AsNoTracking().Where(x => x.PlanVersionId == nutrition.Id)
                .OrderBy(x => x.SortOrder).Select(x => new ClientMealVm(x.MealName, x.Description)).ToListAsync();
        }
        return result;
    }

    public async Task<ClientProgressVm> GetProgressAsync(Guid id) => new()
    {
        Activities = await db.ActivityLogs.AsNoTracking().Where(x => x.ClientId == id)
            .OrderByDescending(x => x.ActivityDate).ThenByDescending(x => x.CompletedAt).Take(100).ToListAsync(),
        Measurements = await db.Measurements.AsNoTracking().Where(x => x.ClientId == id)
            .OrderByDescending(x => x.RecordedAt).Take(100).ToListAsync()
    };

    public async Task<bool> AddActivityAsync(Guid id, ClientActivityForm form)
    {
        if (form.ActivityDate > DateOnly.FromDateTime(DateTime.UtcNow)) return false;
        db.ActivityLogs.Add(new ClientActivityLog { Id = Guid.NewGuid(), ClientId = id,
            ActivityType = form.ActivityType, ActivityDate = form.ActivityDate,
            Label = form.Label?.Trim(), CompletedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(); return true;
    }

    public async Task AddMeasurementAsync(Guid id, ClientMeasurementForm form)
    {
        db.Measurements.Add(new ClientMeasurement { Id = Guid.NewGuid(), ClientId = id,
            WeightKg = form.WeightKg, BodyFatPercent = form.BodyFatPercent,
            MusclePercent = form.MusclePercent, RecordedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
    }

    public async Task<ClientNotificationsVm> GetNotificationsAsync(Guid id) => new()
    {
        Items = await db.Notifications.AsNoTracking().Where(x => x.UserId == id)
            .OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync()
    };

    public async Task<bool> MarkNotificationReadAsync(Guid id, Guid notificationId)
    {
        var item = await db.Notifications.SingleOrDefaultAsync(x => x.Id == notificationId && x.UserId == id);
        if (item is null) return false;
        item.ReadAt ??= DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(); return true;
    }
}
