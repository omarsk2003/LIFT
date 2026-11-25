using System.Data;
using LIFT.Data;
using LIFT.Models;
using Microsoft.EntityFrameworkCore;

namespace LIFT.Services;

public sealed class ClientInteractionService(LiftDbContext db, IPaymentCheckoutService checkout) : IClientInteractionService
{
    private Task<bool> Assigned(Guid clientId, Guid specialistId, string role) =>
        db.SpecialistAssignments.AnyAsync(a => a.ClientId == clientId && a.SpecialistId == specialistId &&
            a.SpecialistRole == role && a.Status == "active" &&
            db.Subscriptions.Any(s => s.Id == a.SubscriptionId && s.Status == "active") &&
            db.Users.Any(u => u.Id == specialistId && u.Role == role && u.Status == "active"));

    private async Task<List<ClientAssignedSpecialistVm>> GetAssigned(Guid clientId) => await (
        from a in db.SpecialistAssignments.AsNoTracking()
        join u in db.Users.AsNoTracking() on a.SpecialistId equals u.Id
        join p in db.SpecialistProfiles.AsNoTracking() on a.SpecialistId equals p.UserId
        where a.ClientId == clientId && a.Status == "active" && u.Status == "active" &&
            db.Subscriptions.Any(s => s.Id == a.SubscriptionId && s.Status == "active")
        select new ClientAssignedSpecialistVm(a.Id, a.SpecialistId, u.FullName,
            a.SpecialistRole, p.Specialty)).ToListAsync();

    public async Task<ClientMessagesVm> GetMessagesAsync(Guid clientId, Guid? selectedId)
    {
        var assigned = await GetAssigned(clientId);
        var ids = await db.ConversationParticipants.AsNoTracking().Where(x => x.UserId == clientId)
            .Select(x => x.ConversationId).ToArrayAsync();
        var conversations = await db.Conversations.AsNoTracking().Where(x => ids.Contains(x.Id))
            .OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync();
        var conversationIds = conversations.Select(x => x.Id).ToArray();
        var others = await (from p in db.ConversationParticipants.AsNoTracking()
            join u in db.Users.AsNoTracking() on p.UserId equals u.Id
            where conversationIds.Contains(p.ConversationId) && p.UserId != clientId
            select new { p.ConversationId, u.Id, u.FullName, u.Role, u.Status }).ToListAsync();
        var valid = conversations.Where(c =>
        {
            var people = others.Where(x => x.ConversationId == c.Id).ToList();
            if (people.Count != 1 || people[0].Status != "active") return false;
            return c.Kind == "support" ? people[0].Role == "admin" :
                people[0].Role == c.Kind && assigned.Any(a => a.SpecialistId == people[0].Id && a.Role == c.Kind);
        }).ToList();
        var validIds = valid.Select(x => x.Id).ToArray();
        var latest = await db.Messages.AsNoTracking().Where(x => validIds.Contains(x.ConversationId))
            .GroupBy(x => x.ConversationId).Select(g => new { Id = g.Key, At = g.Max(x => x.SentAt) }).ToListAsync();
        var threads = valid.Select(c => new ClientThreadVm(c.Id, c.Kind,
            others.Single(x => x.ConversationId == c.Id).FullName, c.Status,
            latest.FirstOrDefault(x => x.Id == c.Id)?.At)).OrderByDescending(x => x.LastMessageAt).ToList();
        var selected = selectedId.HasValue ? threads.FirstOrDefault(x => x.Id == selectedId.Value) : threads.FirstOrDefault();
        if (selected is null) return new ClientMessagesVm { Threads = threads, Assigned = assigned };
        var rows = await (from m in db.Messages.AsNoTracking()
            join u in db.Users.AsNoTracking() on m.SenderId equals u.Id
            where m.ConversationId == selected.Id orderby m.SentAt descending
            select new { m.Id, m.SenderId, u.FullName, m.Body, m.SentAt }).Take(100).ToListAsync();
        var participant = await db.ConversationParticipants.SingleAsync(x => x.ConversationId == selected.Id && x.UserId == clientId);
        participant.LastReadAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync();
        rows.Reverse();
        return new ClientMessagesVm { Threads = threads, Selected = selected, Assigned = assigned,
            Messages = rows.Select(x => new ClientMessageVm(x.Id, x.FullName, x.SenderId == clientId, x.Body, x.SentAt)).ToList() };
    }

    public async Task<Guid?> OpenSpecialistThreadAsync(Guid clientId, Guid specialistId)
    {
        var specialist = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == specialistId &&
            (x.Role == "coach" || x.Role == "nutritionist") && x.Status == "active");
        if (specialist is null || !await Assigned(clientId, specialistId, specialist.Role)) return null;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var existing = await db.Conversations.Where(c => c.Kind == specialist.Role && c.Status == "open" &&
            db.ConversationParticipants.Any(p => p.ConversationId == c.Id && p.UserId == clientId) &&
            db.ConversationParticipants.Any(p => p.ConversationId == c.Id && p.UserId == specialistId))
            .Select(c => (Guid?)c.Id).FirstOrDefaultAsync();
        if (existing.HasValue) return existing;
        var thread = new LiftConversation { Id = Guid.NewGuid(), Kind = specialist.Role,
            Subject = specialist.Role == "coach" ? "Workout guidance" : "Nutrition guidance",
            Status = "open", CreatedAt = DateTimeOffset.UtcNow };
        db.Conversations.Add(thread);
        db.ConversationParticipants.AddRange(new ConversationParticipant { ConversationId = thread.Id, UserId = clientId },
            new ConversationParticipant { ConversationId = thread.Id, UserId = specialistId });
        await db.SaveChangesAsync(); await tx.CommitAsync(); return thread.Id;
    }

    public async Task<Guid?> OpenSupportThreadAsync(Guid clientId)
    {
        var existing = await db.Conversations.Where(c => c.Kind == "support" && c.Status == "open" &&
            db.ConversationParticipants.Any(p => p.ConversationId == c.Id && p.UserId == clientId))
            .Select(c => (Guid?)c.Id).FirstOrDefaultAsync();
        if (existing.HasValue) return existing;
        var admin = await db.Users.AsNoTracking().Where(x => x.Role == "admin" && x.Status == "active")
            .OrderBy(x => x.CreatedAt).Select(x => (Guid?)x.Id).FirstOrDefaultAsync();
        if (!admin.HasValue) return null;
        var thread = new LiftConversation { Id = Guid.NewGuid(), Kind = "support",
            Subject = "Client support", Status = "open", CreatedAt = DateTimeOffset.UtcNow };
        db.Conversations.Add(thread);
        db.ConversationParticipants.AddRange(new ConversationParticipant { ConversationId = thread.Id, UserId = clientId },
            new ConversationParticipant { ConversationId = thread.Id, UserId = admin.Value });
        await db.SaveChangesAsync(); return thread.Id;
    }

    public async Task<bool> SendMessageAsync(Guid clientId, ClientMessageForm form)
    {
        var thread = await db.Conversations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == form.ConversationId && x.Status == "open");
        if (thread is null || !await db.ConversationParticipants.AnyAsync(x => x.ConversationId == thread.Id && x.UserId == clientId)) return false;
        var others = await (from p in db.ConversationParticipants.AsNoTracking()
            join u in db.Users.AsNoTracking() on p.UserId equals u.Id
            where p.ConversationId == thread.Id && p.UserId != clientId
            select new { u.Id, u.Role, u.Status }).ToListAsync();
        if (others.Count != 1 || others[0].Status != "active" || string.IsNullOrWhiteSpace(form.Body)) return false;
        var other = others[0];
        var allowed = thread.Kind == "support" ? other.Role == "admin" :
            other.Role == thread.Kind && await Assigned(clientId, other.Id, other.Role);
        if (!allowed) return false;
        var now = DateTimeOffset.UtcNow;
        db.Messages.Add(new LiftMessage { Id = Guid.NewGuid(), ConversationId = thread.Id,
            SenderId = clientId, Body = form.Body.Trim(), SentAt = now });
        db.Notifications.Add(new LiftNotification { Id = Guid.NewGuid(), UserId = other.Id,
            Type = "message", Title = "New message", Body = "You have a new message in LIFT.",
            TargetUrl = other.Role == "admin" ? "/Admin/Support" : other.Role == "coach" ? "/Coach/Messages" : "/Nutritionist/Messages",
            CreatedAt = now });
        await db.SaveChangesAsync(); return true;
    }

    public async Task<List<ClientMeetingVm>> GetMeetingsAsync(Guid clientId) => await (
        from m in db.Meetings.AsNoTracking()
        join u in db.Users.AsNoTracking() on m.SpecialistId equals u.Id
        where m.ClientId == clientId orderby m.StartsAt descending
        select new ClientMeetingVm(m.Id, u.FullName, u.Role, m.Topic, m.StartsAt,
            m.DurationMinutes, m.Status, m.MeetingUrl)).Take(100).ToListAsync();

    public async Task<bool> DecideMeetingAsync(Guid clientId, ClientMeetingDecisionForm form)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var meeting = await db.Meetings.SingleOrDefaultAsync(x => x.Id == form.MeetingId && x.ClientId == clientId);
        if (meeting is null || meeting.StartsAt <= DateTimeOffset.UtcNow) return false;
        if (form.Decision is "accept" or "decline")
        {
            if (meeting.Status != "pending") return false;
            if (form.Decision == "accept")
            {
                var role = await db.Users.Where(x => x.Id == meeting.SpecialistId).Select(x => x.Role).SingleAsync();
                if (!await Assigned(clientId, meeting.SpecialistId, role)) return false;
                var end = meeting.StartsAt.AddMinutes(meeting.DurationMinutes);
                var possible = await db.Meetings.AsNoTracking().Where(x => x.Id != meeting.Id && x.Status == "accepted" &&
                    (x.ClientId == clientId || x.SpecialistId == meeting.SpecialistId) && x.StartsAt < end)
                    .Select(x => new { x.StartsAt, x.DurationMinutes }).ToListAsync();
                if (possible.Any(x => x.StartsAt.AddMinutes(x.DurationMinutes) > meeting.StartsAt)) return false;
            }
            meeting.Status = form.Decision == "accept" ? "accepted" : "declined";
        }
        else
        {
            if (meeting.Status is not ("pending" or "accepted")) return false;
            meeting.Status = "cancelled";
        }
        db.Notifications.Add(new LiftNotification { Id = Guid.NewGuid(), UserId = meeting.SpecialistId,
            Type = "meeting", Title = "Meeting updated", Body = $"A client {meeting.Status} a meeting.",
            TargetUrl = await db.Users.Where(x => x.Id == meeting.SpecialistId).Select(x => x.Role).SingleAsync() == "coach"
                ? "/Coach/Meetings" : "/Nutritionist/Meetings", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(); await tx.CommitAsync(); return true;
    }

    public async Task<ClientSubscriptionVm> GetSubscriptionAsync(Guid clientId)
    {
        var plan = await (from s in db.Subscriptions.AsNoTracking()
            join p in db.SubscriptionPlans.AsNoTracking() on s.PlanId equals p.Id
            where s.ClientId == clientId && s.Status == "active"
            select p.Name).SingleOrDefaultAsync() ?? "Free";
        var candidates = await (from p in db.SpecialistProfiles.AsNoTracking()
            join u in db.Users.AsNoTracking() on p.UserId equals u.Id
            where (u.Role == "coach" || u.Role == "nutritionist") && u.Status == "active" &&
                p.ApprovedAt != null && p.AcceptingNewClients
            select new { u.Id, u.FullName, u.Role, p.Specialty, p.Bio, p.ExperienceYears, p.Capacity }).ToListAsync();
        var activeCounts = await db.SpecialistAssignments.AsNoTracking().Where(x => x.Status == "active")
            .GroupBy(x => x.SpecialistId).Select(x => new { Id = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count);
        return new ClientSubscriptionVm { CurrentPlan = plan, CheckoutConfigured = checkout.IsConfigured,
            Plans = await db.SubscriptionPlans.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Id)
                .Select(x => new ClientPlanVm(x.Id, x.Code, x.Name, x.MonthlyPrice)).ToListAsync(),
            Assigned = await GetAssigned(clientId),
            Available = candidates.Select(x => new ClientAvailableSpecialistVm(x.Id, x.FullName, x.Role,
                x.Specialty, x.Bio, x.ExperienceYears, x.Capacity - activeCounts.GetValueOrDefault(x.Id)))
                .Where(x => x.AvailableSlots > 0).ToList(),
            ChangeRequests = await db.SpecialistChangeRequests.AsNoTracking().Where(x => x.ClientId == clientId)
                .OrderByDescending(x => x.RequestedAt).Take(20).ToListAsync() };
    }

    public async Task<bool> RequestSpecialistChangeAsync(Guid clientId, ClientChangeRequestForm form)
    {
        if (form.Reason.Trim().Length < 10) return false;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var assignment = await db.SpecialistAssignments.SingleOrDefaultAsync(x => x.Id == form.AssignmentId &&
            x.ClientId == clientId && x.Status == "active");
        if (assignment is null || !await db.Subscriptions.AnyAsync(x => x.Id == assignment.SubscriptionId && x.Status == "active") ||
            await db.SpecialistChangeRequests.AnyAsync(x => x.AssignmentId == assignment.Id && x.Status == "pending")) return false;
        db.SpecialistChangeRequests.Add(new SpecialistChangeRequest { Id = Guid.NewGuid(),
            AssignmentId = assignment.Id, ClientId = clientId, Reason = form.Reason.Trim(),
            Status = "pending", RequestedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(); await tx.CommitAsync(); return true;
    }
}
