namespace LIFT.Models;

public sealed class LiftUser
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public int AuthVersion { get; set; }
    public string Role { get; set; } = "client";
    public string Status { get; set; } = "active";
    public string? Phone { get; set; }
    public string? PhotoUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ClientProfile
{
    public Guid UserId { get; set; }
    public string? Goal { get; set; }
    public string? GoalNote { get; set; }
    public DateOnly? BirthDate { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? StartingWeightKg { get; set; }
    public string? FitnessLevel { get; set; }
    public string? ActivityLevel { get; set; }
    public string? TrainingLocation { get; set; }
    public short? SessionMinutes { get; set; }
    public string? PreferredTrainingTime { get; set; }
    public decimal? SleepHours { get; set; }
    public string? ScheduleNotes { get; set; }
    public string? HealthNotes { get; set; }
    public string? FoodHabits { get; set; }
    public string? DietaryPreferences { get; set; }
    public string? CookingStyle { get; set; }
    public string? Sport { get; set; }
    public DateTimeOffset? ProfileCompletedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class SubscriptionPlan
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal MonthlyPrice { get; set; }
    public bool IsActive { get; set; }
}

public sealed class Subscription
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public int PlanId { get; set; }
    public string Status { get; set; } = "active";
    public decimal PriceAtStart { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public string? ExternalBillingId { get; set; }
}

