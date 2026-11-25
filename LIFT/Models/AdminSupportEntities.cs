namespace LIFT.Models;

public sealed class LiftConversation
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = "support";
    public string? Subject { get; set; }
    public string Status { get; set; } = "open";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ConversationParticipant
{
    public Guid ConversationId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset? LastReadAt { get; set; }
}

public sealed class LiftMessage
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public string Body { get; set; } = "";
    public DateTimeOffset SentAt { get; set; }
}
