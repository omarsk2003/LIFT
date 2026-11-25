using System.ComponentModel.DataAnnotations;

namespace LIFT.Models;

public sealed record CoachClientVm(Guid ClientId, string Name, string Email, string? Goal,
    string PlanStatus, DateTimeOffset AssignedAt);

public sealed class CoachDashboardVm
{
    public string Name { get; set; } = "";
    public List<CoachClientVm> Clients { get; set; } = [];
    public int PublishedPlans { get; set; }
}

public sealed record CoachExerciseVm(byte Day, string Name, short? Sets, string? Reps,
    short? DurationMinutes, string? Instructions);

public sealed record CoachPendingPlanVm(Guid Id, int VersionNumber, string? Title,
    string? Notes, DateTimeOffset CreatedAt, List<CoachExerciseVm> Exercises);

public sealed class WorkoutPlanForm : IValidatableObject
{
    [Required] public Guid ClientId { get; set; }
    [Required, StringLength(200)] public string Title { get; set; } = "";
    [StringLength(2000)] public string? Notes { get; set; }
    [Required, StringLength(12000)] public string ExercisesText { get; set; } = "";
    public bool Publish { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var lines = ExercisesText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length is < 1 or > 30)
            yield return new ValidationResult("Enter between 1 and 30 exercises.", [nameof(ExercisesText)]);
        foreach (var line in lines)
        {
            var p = line.Split('|', StringSplitOptions.TrimEntries);
            if (p.Length != 6 || !byte.TryParse(p[0], out var day) || day is < 1 or > 7 ||
                p[1].Length is < 1 or > 160 ||
                (p[2].Length > 0 && (!short.TryParse(p[2], out var sets) || sets is < 1 or > 100)) ||
                p[3].Length > 50 ||
                (p[4].Length > 0 && (!short.TryParse(p[4], out var minutes) || minutes is < 1 or > 180)) ||
                p[5].Length > 1000)
                yield return new ValidationResult("Use: day 1-7 | exercise | sets | reps | minutes | instructions.", [nameof(ExercisesText)]);
        }
    }
}

public sealed class CoachClientDetailVm
{
    public CoachClientVm Client { get; set; } = null!;
    public string? FitnessLevel { get; set; }
    public string? ActivityLevel { get; set; }
    public string? TrainingLocation { get; set; }
    public short? SessionMinutes { get; set; }
    public string? HealthNotes { get; set; }
    public WorkoutPlanForm Form { get; set; } = new();
    public int? PublishedVersion { get; set; }
    public List<CoachPendingPlanVm> PendingAiPlans { get; set; } = [];
}

public sealed class CoachAiReviewForm
{
    [Required] public Guid PlanId { get; set; }
    [Required, RegularExpression("approve|reject")] public string Decision { get; set; } = "";
}

public sealed class CoachGuidancePageVm
{
    public List<CoachClientVm> Clients { get; set; } = [];
    public List<NutritionGuidanceVm> Notes { get; set; } = [];
}

public sealed class CoachMeetingPageVm
{
    public List<CoachClientVm> Clients { get; set; } = [];
    public List<NutritionMeetingVm> Meetings { get; set; } = [];
}
