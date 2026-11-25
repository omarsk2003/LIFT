using LIFT.Models;

namespace LIFT.Services;

public interface ICoachService
{
    Task<CoachDashboardVm> GetDashboardAsync(Guid coachId);
    Task<CoachClientDetailVm?> GetClientAsync(Guid coachId, Guid clientId);
    Task<bool> SavePlanAsync(Guid coachId, WorkoutPlanForm form);
    Task<bool> ReviewAiPlanAsync(Guid coachId, CoachAiReviewForm form);
}
