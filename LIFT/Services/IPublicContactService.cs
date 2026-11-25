using LIFT.Models;

namespace LIFT.Services;

public interface IPublicContactService
{
    Task SaveAsync(ContactForm form, CancellationToken cancellationToken);
}
