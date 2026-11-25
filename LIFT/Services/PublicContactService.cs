using LIFT.Data;
using LIFT.Models;

namespace LIFT.Services;

public sealed class PublicContactService(LiftDbContext db) : IPublicContactService
{
    public async Task SaveAsync(ContactForm form, CancellationToken ct)
    {
        db.ContactMessages.Add(new ContactMessage
        {
            Id = Guid.NewGuid(), FullName = form.Name.Trim(),
            Email = form.Email.Trim().ToLowerInvariant(),
            Address = form.Address?.Trim(), Phone = form.Phone?.Trim(),
            Body = form.Message.Trim(), Status = "new", CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }
}
