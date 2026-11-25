using LIFT.Data;
using LIFT.Models;
using Microsoft.EntityFrameworkCore;

namespace LIFT.Services;

public sealed class AdminSupportService(LiftDbContext db, ILogger<AdminSupportService> logger) : IAdminSupportService
{
    public async Task<AdminSupportVm> GetAsync(Guid adminId, Guid? selectedId, CancellationToken ct)
    {
        var contacts = await db.ContactMessages.AsNoTracking().OrderByDescending(x => x.CreatedAt)
            .Take(50).Select(x => new AdminContactVm(x.Id, x.FullName, x.Email,
                x.Body, x.Status, x.CreatedAt)).ToListAsync(ct);
        var threads = await (
            from conversation in db.Conversations.AsNoTracking()
            where conversation.Kind == "support" || db.ConversationParticipants.Any(
                p => p.ConversationId == conversation.Id && p.UserId == adminId)
            orderby conversation.CreatedAt descending
            select new AdminThreadVm(conversation.Id, conversation.Kind,
                conversation.Subject ?? "Conversation", conversation.Status,
                (from participant in db.ConversationParticipants
                 join user in db.Users on participant.UserId equals user.Id
                 where participant.ConversationId == conversation.Id && user.Id != adminId
                 select user.FullName).FirstOrDefault() ?? "Unknown user",
                conversation.CreatedAt,
                db.Messages.Where(m => m.ConversationId == conversation.Id)
                    .Max(m => (DateTimeOffset?)m.SentAt))
        ).Take(50).ToListAsync(ct);

        var selected = selectedId.HasValue
            ? threads.FirstOrDefault(x => x.Id == selectedId.Value)
            : threads.FirstOrDefault();
        if (selected is null) return new AdminSupportVm(threads, null, [], contacts);

        var messages = await (
            from message in db.Messages.AsNoTracking()
            join user in db.Users.AsNoTracking() on message.SenderId equals user.Id
            where message.ConversationId == selected.Id
            orderby message.SentAt descending
            select new AdminMessageVm(message.Id, message.SenderId, user.FullName,
                message.Body, message.SentAt)
        ).Take(100).ToListAsync(ct);
        messages.Reverse();
        return new AdminSupportVm(threads, selected, messages, contacts);
    }

    public async Task<string?> SetContactStatusAsync(AdminContactStatusForm form, Guid adminId, CancellationToken ct)
    {
        var contact = await db.ContactMessages.SingleOrDefaultAsync(x => x.Id == form.Id, ct);
        if (contact is null) return "Contact message not found.";
        contact.Status = form.Status;
        contact.ReviewedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Admin {AdminId} changed contact message {ContactId} to {Status}",
            adminId, contact.Id, form.Status);
        return null;
    }

    public async Task<string?> ReplyAsync(AdminReplyForm form, Guid adminId, CancellationToken ct)
    {
        var conversation = await db.Conversations.SingleOrDefaultAsync(x => x.Id == form.ConversationId, ct);
        if (conversation is null || conversation.Status != "open") return "This conversation is unavailable or closed.";
        if (conversation.Kind != "support" && !await db.ConversationParticipants.AnyAsync(
            p => p.ConversationId == conversation.Id && p.UserId == adminId, ct))
            return "You cannot reply to this conversation.";
        if (!await db.ConversationParticipants.AnyAsync(p => p.ConversationId == conversation.Id && p.UserId == adminId, ct))
        {
            db.ConversationParticipants.Add(new ConversationParticipant
            {
                ConversationId = conversation.Id, UserId = adminId
            });
        }
        var body = form.Body.Trim();
        if (body.Length == 0) return "Write a reply before sending.";
        db.Messages.Add(new LiftMessage
        {
            Id = Guid.NewGuid(), ConversationId = conversation.Id, SenderId = adminId,
            Body = body, SentAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Admin {AdminId} replied to conversation {ConversationId}", adminId, conversation.Id);
        return null;
    }

    public async Task<string?> CloseAsync(Guid conversationId, Guid adminId, CancellationToken ct)
    {
        var conversation = await db.Conversations.SingleOrDefaultAsync(x => x.Id == conversationId, ct);
        if (conversation is null || conversation.Kind != "support") return "Support conversation not found.";
        if (conversation.Status == "closed") return null;
        conversation.Status = "closed";
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Admin {AdminId} closed support conversation {ConversationId}", adminId, conversationId);
        return null;
    }
}
