namespace LIFT.Models;

public sealed class SpecialistProfile
{
    public Guid UserId { get; set; }
    public string Specialty { get; set; } = "";
    public string? Bio { get; set; }
    public byte? ExperienceYears { get; set; }
    public short Capacity { get; set; }
    public bool AcceptingNewClients { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
}

public sealed class SpecialistCertificate
{
    public Guid Id { get; set; }
    public Guid SpecialistId { get; set; }
    public string OriginalFileName { get; set; } = "";
    public string StorageKey { get; set; } = "";
    public DateTimeOffset UploadedAt { get; set; }
}

public sealed class LiftNotification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string? TargetUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}
