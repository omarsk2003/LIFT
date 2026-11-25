using LIFT.Models;

namespace LIFT.Services;

public interface IClientService
{
    Task<ClientDashboardVm> GetDashboardAsync(Guid clientId);
    Task<ClientProfileVm> GetProfileAsync(Guid clientId);
    Task SaveProfileAsync(Guid clientId, ClientProfileForm form);
    Task<ClientPlansVm> GetPlansAsync(Guid clientId);
    Task<ClientProgressVm> GetProgressAsync(Guid clientId);
    Task<bool> AddActivityAsync(Guid clientId, ClientActivityForm form);
    Task AddMeasurementAsync(Guid clientId, ClientMeasurementForm form);
    Task<ClientNotificationsVm> GetNotificationsAsync(Guid clientId);
    Task<bool> MarkNotificationReadAsync(Guid clientId, Guid notificationId);
}
