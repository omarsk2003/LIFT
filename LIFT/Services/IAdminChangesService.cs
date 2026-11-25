using LIFT.Models;

namespace LIFT.Services;

public interface IAdminChangesService
{
    Task<AdminChangesVm> GetAsync(CancellationToken cancellationToken);
    Task<string?> DecideAsync(AdminChangeDecisionForm form, Guid adminId, CancellationToken cancellationToken);
}
