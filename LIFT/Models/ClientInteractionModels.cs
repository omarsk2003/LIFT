using System.ComponentModel.DataAnnotations;

namespace LIFT.Models;

public sealed record ClientThreadVm(Guid Id, string Kind, string OtherName, string Status,
    DateTimeOffset? LastMessageAt);
public sealed record ClientMessageVm(Guid Id, string SenderName, bool IsMine, string Body, DateTimeOffset SentAt);
public sealed class ClientMessagesVm
{
    public List<ClientThreadVm> Threads { get; set; } = [];
    public ClientThreadVm? Selected { get; set; }
    public List<ClientMessageVm> Messages { get; set; } = [];
    public List<ClientAssignedSpecialistVm> Assigned { get; set; } = [];
}

public sealed record ClientAssignedSpecialistVm(Guid AssignmentId, Guid SpecialistId,
    string Name, string Role, string Specialty);
public sealed class ClientMessageForm
{
    [Required] public Guid ConversationId { get; set; }
    [Required, StringLength(4000, MinimumLength = 1)] public string Body { get; set; } = "";
}

public sealed record ClientMeetingVm(Guid Id, string SpecialistName, string Role, string Topic,
    DateTimeOffset StartsAt, short DurationMinutes, string Status, string? MeetingUrl);
public sealed class ClientMeetingDecisionForm
{
    [Required] public Guid MeetingId { get; set; }
    [Required, RegularExpression("accept|decline|cancel")] public string Decision { get; set; } = "";
}

public sealed record ClientAvailableSpecialistVm(Guid Id, string Name, string Role, string Specialty,
    string? Bio, byte? ExperienceYears, int AvailableSlots);
public sealed record ClientPlanVm(int Id, string Code, string Name, decimal MonthlyPrice);
public sealed class ClientSubscriptionVm
{
    public bool CheckoutConfigured { get; set; }
    public string CurrentPlan { get; set; } = "Free";
    public List<ClientPlanVm> Plans { get; set; } = [];
    public List<ClientAssignedSpecialistVm> Assigned { get; set; } = [];
    public List<ClientAvailableSpecialistVm> Available { get; set; } = [];
    public List<SpecialistChangeRequest> ChangeRequests { get; set; } = [];
}
public sealed class ClientChangeRequestForm
{
    [Required] public Guid AssignmentId { get; set; }
    [Required, StringLength(1000, MinimumLength = 10)] public string Reason { get; set; } = "";
}
