using LIFT.Models;

namespace LIFT.Services;

public interface IAdminService
{
    Task<AdminDashboardVm> GetDashboardAsync(string? search, int specialistPage, int clientPage,
        int subscriptionPage, CancellationToken cancellationToken);
    Task<AdminReviewVm?> GetReviewAsync(Guid specialistId, CancellationToken cancellationToken);
    Task<CertificateDownload?> GetCertificateAsync(Guid certificateId, CancellationToken cancellationToken);
    Task<string?> ReviewSpecialistAsync(AdminSpecialistDecisionForm form, Guid reviewerId, CancellationToken cancellationToken);
    Task<string?> ChangeAccountStatusAsync(AdminAccountStatusForm form, Guid adminId, CancellationToken cancellationToken);
    Task<string?> EditClientAsync(AdminClientEditForm form, Guid adminId, CancellationToken cancellationToken);
}
