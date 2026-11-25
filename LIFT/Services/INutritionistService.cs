using LIFT.Models;

namespace LIFT.Services;

public interface INutritionistService
{
    Task<NutritionistDashboardVm> GetDashboardAsync(Guid specialistId);
    Task<NutritionistClientDetailVm?> GetClientAsync(Guid specialistId, Guid clientId);
    Task<bool> SavePlanAsync(Guid specialistId, NutritionPlanForm form);
    Task<bool> ReviewAiPlanAsync(Guid specialistId, NutritionAiReviewForm form);
}
