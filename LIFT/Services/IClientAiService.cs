using LIFT.Models;

namespace LIFT.Services;

public interface IClientAiService
{
    Task<ClientAiVm> GetAsync(Guid clientId, CancellationToken ct);
    Task<ClientAiResult> ChatAsync(Guid clientId, ClientAiChatForm form, CancellationToken ct);
    Task<ClientAiResult> AnalyzeAsync(Guid clientId, CancellationToken ct);
    Task<ClientAiResult> GeneratePlanAsync(Guid clientId, ClientAiPlanForm form, CancellationToken ct);
}
