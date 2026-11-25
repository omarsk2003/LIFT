using LIFT.Models;

namespace LIFT.Services;

public interface ISpecialistWorkspaceService
{
    Task<NutritionistProfileVm> GetProfileAsync(Guid specialistId);
    Task UpdateProfileAsync(Guid specialistId, NutritionistProfileForm form);
    Task<List<NutritionGuidanceVm>> GetGuidanceAsync(Guid specialistId);
    Task<bool> AddGuidanceAsync(Guid specialistId, NutritionGuidanceForm form);
    Task<bool> DeleteGuidanceAsync(Guid specialistId, Guid noteId);
    Task<NutritionMessagesVm> GetMessagesAsync(Guid specialistId, Guid? selectedId);
    Task<Guid?> OpenClientThreadAsync(Guid specialistId, Guid clientId);
    Task<Guid?> OpenAdminThreadAsync(Guid specialistId);
    Task<bool> SendMessageAsync(Guid specialistId, NutritionMessageForm form);
    Task<List<NutritionMeetingVm>> GetMeetingsAsync(Guid specialistId);
    Task<string?> CreateMeetingAsync(Guid specialistId, NutritionMeetingForm form);
    Task<bool> CancelMeetingAsync(Guid specialistId, Guid meetingId);
}
