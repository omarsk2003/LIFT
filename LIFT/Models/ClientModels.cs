using System.ComponentModel.DataAnnotations;

namespace LIFT.Models;

public sealed class ClientDashboardVm
{
    public string Name { get; set; } = "";
    public string PlanName { get; set; } = "Free";
    public string? CoachName { get; set; }
    public string? NutritionistName { get; set; }
    public int ActivityCount { get; set; }
    public int UnreadNotifications { get; set; }
    public bool ProfileComplete { get; set; }
}

public sealed class ClientProfileForm
{
    [Required, StringLength(120, MinimumLength = 2)] public string FullName { get; set; } = "";
    [StringLength(30)] public string? Phone { get; set; }
    [StringLength(100)] public string? Goal { get; set; }
    [StringLength(500)] public string? GoalNote { get; set; }
    public DateOnly? BirthDate { get; set; }
    [Range(80, 250)] public decimal? HeightCm { get; set; }
    [Range(20, 500)] public decimal? StartingWeightKg { get; set; }
    [StringLength(40)] public string? FitnessLevel { get; set; }
    [StringLength(40)] public string? ActivityLevel { get; set; }
    [StringLength(40)] public string? TrainingLocation { get; set; }
    [Range(5, 300)] public short? SessionMinutes { get; set; }
    [StringLength(40)] public string? PreferredTrainingTime { get; set; }
    [Range(0, 24)] public decimal? SleepHours { get; set; }
    [StringLength(500)] public string? ScheduleNotes { get; set; }
    [StringLength(1000)] public string? HealthNotes { get; set; }
    [StringLength(500)] public string? FoodHabits { get; set; }
    [StringLength(500)] public string? DietaryPreferences { get; set; }
    [StringLength(40)] public string? CookingStyle { get; set; }
    [StringLength(80)] public string? Sport { get; set; }
    public List<byte> TrainingDays { get; set; } = [];
}

public sealed class ClientProfileVm
{
    public string Email { get; set; } = "";
    public ClientProfileForm Form { get; set; } = new();
}

public sealed record ClientWorkoutItemVm(byte Day, string Name, short? Sets, string? Reps,
    short? DurationMinutes, string? Instructions);
public sealed record ClientMealVm(string Name, string? Description);
public sealed class ClientPlansVm
{
    public string PlanName { get; set; } = "";
    public string? WorkoutTitle { get; set; }
    public int? WorkoutVersion { get; set; }
    public List<ClientWorkoutItemVm> WorkoutItems { get; set; } = [];
    public string? NutritionTitle { get; set; }
    public int? NutritionVersion { get; set; }
    public int? Calories { get; set; }
    public decimal? ProteinGrams { get; set; }
    public decimal? CarbsGrams { get; set; }
    public decimal? FatGrams { get; set; }
    public List<ClientMealVm> Meals { get; set; } = [];
}

public sealed class ClientActivityForm
{
    [Required, RegularExpression("workout|meal|other")] public string ActivityType { get; set; } = "";
    [Required] public DateOnly ActivityDate { get; set; }
    [StringLength(200)] public string? Label { get; set; }
}

public sealed class ClientMeasurementForm
{
    [Range(typeof(decimal), "20", "500")] public decimal WeightKg { get; set; }
    [Range(typeof(decimal), "0", "100")] public decimal? BodyFatPercent { get; set; }
    [Range(typeof(decimal), "0", "100")] public decimal? MusclePercent { get; set; }
}

public sealed class ClientProgressVm
{
    public List<ClientActivityLog> Activities { get; set; } = [];
    public List<ClientMeasurement> Measurements { get; set; } = [];
}

public sealed class ClientNotificationsVm
{
    public List<LiftNotification> Items { get; set; } = [];
}
