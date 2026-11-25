using LIFT.Models;

namespace LIFT.Services;

public interface IAdminAccountsService
{
    Task<AdminAccountsVm> GetAsync(Guid currentUserId, CancellationToken cancellationToken);
    Task<string?> CreateAsync(CreateAdminForm form, Guid creatorId, CancellationToken cancellationToken);
    Task<string?> SetStatusAsync(AdminStatusForm form, Guid actorId, CancellationToken cancellationToken);
    Task<string?> ChangePasswordAsync(AdminPasswordForm form, Guid adminId, CancellationToken cancellationToken);
    Task<AdminProfileVm?> GetProfileAsync(Guid adminId, CancellationToken cancellationToken);
    Task<string?> SavePhotoAsync(Guid adminId, IFormFile? photo, CancellationToken cancellationToken);
}
