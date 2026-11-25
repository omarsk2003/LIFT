using LIFT.Models;

namespace LIFT.Services;

public interface IAdminSupportService
{
    Task<AdminSupportVm> GetAsync(Guid adminId, Guid? selectedId, CancellationToken cancellationToken);
    Task<string?> ReplyAsync(AdminReplyForm form, Guid adminId, CancellationToken cancellationToken);
    Task<string?> CloseAsync(Guid conversationId, Guid adminId, CancellationToken cancellationToken);
    Task<string?> SetContactStatusAsync(AdminContactStatusForm form, Guid adminId, CancellationToken cancellationToken);
}
