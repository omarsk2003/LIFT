namespace LIFT.Models;

public sealed class SpecialistAssignment
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public Guid SubscriptionId { get; set; }
    public Guid SpecialistId { get; set; }
    public string SpecialistRole { get; set; } = "";
    public string Status { get; set; } = "active";
    public DateTimeOffset AssignedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
}

public sealed class SpecialistChangeRequest
{
    public Guid Id { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid ClientId { get; set; }
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "pending";
    public DateTimeOffset RequestedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? AdminNote { get; set; }
    public Guid? ReplacementAssignmentId { get; set; }
}
