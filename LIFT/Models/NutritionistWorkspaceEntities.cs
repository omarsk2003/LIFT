namespace LIFT.Models;

public sealed class SpecialistGuidanceNote
{
    public Guid Id { get; set; }
    public Guid SpecialistId { get; set; }
    public Guid ClientId { get; set; }
    public string Topic { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class LiftMeeting
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public Guid SpecialistId { get; set; }
    public string Topic { get; set; } = "";
    public DateTimeOffset StartsAt { get; set; }
    public short DurationMinutes { get; set; }
    public string Status { get; set; } = "pending";
    public string? MeetingUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
