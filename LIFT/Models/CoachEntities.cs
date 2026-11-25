namespace LIFT.Models;

public sealed class WorkoutPlanItem
{
    public Guid Id { get; set; }
    public Guid PlanVersionId { get; set; }
    public byte DayOfWeek { get; set; }
    public short SortOrder { get; set; }
    public string ExerciseName { get; set; } = "";
    public short? Sets { get; set; }
    public string? Reps { get; set; }
    public short? DurationMinutes { get; set; }
    public string? Instructions { get; set; }
}
