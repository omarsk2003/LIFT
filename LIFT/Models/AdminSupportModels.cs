using System.ComponentModel.DataAnnotations;

namespace LIFT.Models;

public sealed record AdminThreadVm(Guid Id, string Kind, string Subject, string Status,
    string OtherName, DateTimeOffset CreatedAt, DateTimeOffset? LastMessageAt);
public sealed record AdminMessageVm(Guid Id, Guid SenderId, string SenderName, string Body, DateTimeOffset SentAt);
public sealed record AdminSupportVm(IReadOnlyList<AdminThreadVm> Threads,
    AdminThreadVm? Selected, IReadOnlyList<AdminMessageVm> Messages,
    IReadOnlyList<AdminContactVm> Contacts);

public sealed record AdminContactVm(Guid Id, string Name, string Email, string Body,
    string Status, DateTimeOffset CreatedAt);

public sealed class AdminContactStatusForm
{
    [Required] public Guid Id { get; set; }
    [Required, RegularExpression("reviewed|closed")] public string Status { get; set; } = "";
}

public sealed class AdminReplyForm
{
    [Required] public Guid ConversationId { get; set; }
    [Required, StringLength(4000, MinimumLength = 1)] public string Body { get; set; } = "";
}

public sealed class AdminCloseThreadForm
{
    [Required] public Guid ConversationId { get; set; }
}
