namespace LIFT.Models;

public sealed class AiModel
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = "";
    public string Provider { get; set; } = "ollama";
    public string ModelKey { get; set; } = "";
    public string Purpose { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class AiConversation
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public string? Title { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class AiMessage
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public string Role { get; set; } = "user";
    public string Body { get; set; } = "";
    public string? ModelName { get; set; }
    public int? AiModelId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class AiAnalysisRun
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public int? AiModelId { get; set; }
    public string AnalysisType { get; set; } = "lifestyle";
    public string Status { get; set; } = "pending";
    public string? Summary { get; set; }
    public string? ErrorCode { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class AiPlanGeneration
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public Guid? ConversationId { get; set; }
    public Guid? PlanVersionId { get; set; }
    public string PlanType { get; set; } = "workout";
    public string Status { get; set; } = "pending";
    public string? ModelName { get; set; }
    public int? AiModelId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
