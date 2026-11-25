using System.ComponentModel.DataAnnotations;

namespace LIFT.Models;

public sealed class NutritionistProfileVm
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string Specialty { get; set; } = "";
    public string? Bio { get; set; }
    public byte? ExperienceYears { get; set; }
    public short Capacity { get; set; }
    public bool AcceptingNewClients { get; set; }
    public int ActiveClients { get; set; }
}

public sealed class NutritionistProfileForm
{
    [Required, StringLength(120, MinimumLength = 2)] public string Name { get; set; } = "";
    [StringLength(30)] public string? Phone { get; set; }
    [Required, StringLength(120)] public string Specialty { get; set; } = "";
    [StringLength(1000)] public string? Bio { get; set; }
    [Range(0, 60)] public byte? ExperienceYears { get; set; }
    [Range(1, 8)] public short Capacity { get; set; }
    public bool AcceptingNewClients { get; set; }
}

public sealed record NutritionGuidanceVm(Guid Id, Guid ClientId, string ClientName, string Topic,
    string Title, string Body, DateTimeOffset CreatedAt);

public sealed class NutritionGuidanceForm
{
    [Required] public Guid ClientId { get; set; }
    [Required, StringLength(80)] public string Topic { get; set; } = "";
    [Required, StringLength(160)] public string Title { get; set; } = "";
    [Required, StringLength(2000)] public string Body { get; set; } = "";
}

public sealed class NutritionGuidancePageVm
{
    public List<NutritionistClientVm> Clients { get; set; } = [];
    public List<NutritionGuidanceVm> Notes { get; set; } = [];
}

public sealed record NutritionThreadVm(Guid Id, Guid? ClientId, string OtherName, string Kind,
    string Status, DateTimeOffset? LastMessageAt, int UnreadCount);
public sealed record NutritionMessageVm(Guid Id, string SenderName, bool IsMine, string Body, DateTimeOffset SentAt);
public sealed class NutritionMessagesVm
{
    public List<NutritionThreadVm> Threads { get; set; } = [];
    public NutritionThreadVm? Selected { get; set; }
    public List<NutritionMessageVm> Messages { get; set; } = [];
}

public sealed class NutritionMessageForm
{
    [Required] public Guid ConversationId { get; set; }
    [Required, StringLength(4000, MinimumLength = 1)] public string Body { get; set; } = "";
}

public sealed record NutritionMeetingVm(Guid Id, string ClientName, string Topic,
    DateTimeOffset StartsAt, short DurationMinutes, string Status, string? MeetingUrl);

public sealed class NutritionMeetingForm
{
    [Required] public Guid ClientId { get; set; }
    [Required, StringLength(200)] public string Topic { get; set; } = "";
    [Required] public DateTimeOffset StartsAt { get; set; }
    [Range(15, 180)] public short DurationMinutes { get; set; }
    [StringLength(1000), Url] public string? MeetingUrl { get; set; }
}

public sealed class NutritionMeetingPageVm
{
    public List<NutritionistClientVm> Clients { get; set; } = [];
    public List<NutritionMeetingVm> Meetings { get; set; } = [];
}
