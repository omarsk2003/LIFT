namespace LIFT.Models;

public sealed class ClientTrainingDay
{
    public Guid ClientId { get; set; }
    public byte DayOfWeek { get; set; }
}

public sealed class ClientMeasurement
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public decimal WeightKg { get; set; }
    public decimal? BodyFatPercent { get; set; }
    public decimal? MusclePercent { get; set; }
}

public sealed class ClientActivityLog
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public Guid? PlanItemId { get; set; }
    public string ActivityType { get; set; } = "other";
    public DateOnly ActivityDate { get; set; }
    public string? Label { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
}
