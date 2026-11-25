using LIFT.Models;

namespace LIFT.Services;

public interface ISpecialistRegistrationService
{
    Task<string?> RegisterAsync(SpecialistRegistrationForm form, string role, CancellationToken cancellationToken);
}
