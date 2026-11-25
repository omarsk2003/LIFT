using LIFT.Models;

namespace LIFT.Services;

public interface IClientInteractionService
{
    Task<ClientMessagesVm> GetMessagesAsync(Guid clientId, Guid? selectedId);
    Task<Guid?> OpenSpecialistThreadAsync(Guid clientId, Guid specialistId);
    Task<Guid?> OpenSupportThreadAsync(Guid clientId);
    Task<bool> SendMessageAsync(Guid clientId, ClientMessageForm form);
    Task<List<ClientMeetingVm>> GetMeetingsAsync(Guid clientId);
    Task<bool> DecideMeetingAsync(Guid clientId, ClientMeetingDecisionForm form);
    Task<ClientSubscriptionVm> GetSubscriptionAsync(Guid clientId);
    Task<bool> RequestSpecialistChangeAsync(Guid clientId, ClientChangeRequestForm form);
}
