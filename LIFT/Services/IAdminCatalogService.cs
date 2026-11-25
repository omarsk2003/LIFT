using LIFT.Models;

namespace LIFT.Services;

public interface IAdminCatalogService
{
    Task<AdminCatalogVm> GetAsync(CancellationToken cancellationToken);
    Task<string?> UpdatePriceAsync(AdminPriceForm form, Guid adminId, CancellationToken cancellationToken);
    Task<string?> SavePromoAsync(AdminPromoForm form, Guid adminId, CancellationToken cancellationToken);
    Task<string?> TogglePromoAsync(Guid id, Guid adminId, CancellationToken cancellationToken);
}
