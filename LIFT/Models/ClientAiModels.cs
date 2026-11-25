using System.ComponentModel.DataAnnotations;

namespace LIFT.Models;

public sealed record ClientAiMessageVm(string Role, string Body, DateTimeOffset CreatedAt);
public sealed class ClientAiVm
{
    public bool Entitled { get; set; }
    public bool ProfileReady { get; set; }
    public Guid? ConversationId { get; set; }
    public List<ClientAiMessageVm> Messages { get; set; } = [];
    public List<AiAnalysisRun> Analyses { get; set; } = [];
    public List<AiPlanGeneration> Generations { get; set; } = [];
}

public sealed class ClientAiChatForm
{
    public Guid? ConversationId { get; set; }
    [Required, StringLength(2000, MinimumLength = 2)] public string Message { get; set; } = "";
}

public sealed class ClientAiPlanForm
{
    [Required, RegularExpression("workout|nutrition")] public string PlanType { get; set; } = "";
}

public sealed record ClientAiResult(bool Success, string? Error, Guid? ConversationId = null);
