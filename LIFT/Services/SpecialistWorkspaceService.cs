using System.Data;
using LIFT.Data;
using LIFT.Models;
using Microsoft.EntityFrameworkCore;

namespace LIFT.Services;

public abstract class SpecialistWorkspaceService(LiftDbContext db) : ISpecialistWorkspaceService
{
    protected abstract string SpecialistRole { get; }
    private Task<bool> Assigned(Guid specialistId, Guid clientId) => db.SpecialistAssignments.AnyAsync(a =>
        a.SpecialistId == specialistId && a.ClientId == clientId && a.SpecialistRole == SpecialistRole &&
        a.Status == "active" && db.Subscriptions.Any(s => s.Id == a.SubscriptionId && s.Status == "active"));

    public async Task<NutritionistProfileVm> GetProfileAsync(Guid id)
    {
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == id);
        var profile = await db.SpecialistProfiles.AsNoTracking().SingleAsync(x => x.UserId == id);
        var count = await db.SpecialistAssignments.CountAsync(x => x.SpecialistId == id && x.Status == "active");
        return new NutritionistProfileVm { Name = user.FullName, Email = user.Email, Phone = user.Phone,
            Specialty = profile.Specialty, Bio = profile.Bio, ExperienceYears = profile.ExperienceYears,
            Capacity = profile.Capacity, AcceptingNewClients = profile.AcceptingNewClients, ActiveClients = count };
    }

    public async Task UpdateProfileAsync(Guid id, NutritionistProfileForm form)
    {
        var user = await db.Users.SingleAsync(x => x.Id == id && x.Role == SpecialistRole);
        var profile = await db.SpecialistProfiles.SingleAsync(x => x.UserId == id);
        user.FullName = form.Name.Trim(); user.Phone = form.Phone?.Trim();
        profile.Specialty = form.Specialty.Trim(); profile.Bio = form.Bio?.Trim();
        profile.ExperienceYears = form.ExperienceYears; profile.Capacity = form.Capacity;
        profile.AcceptingNewClients = form.AcceptingNewClients;
        await db.SaveChangesAsync();
    }

    public async Task<List<NutritionGuidanceVm>> GetGuidanceAsync(Guid id) => await (
        from note in db.SpecialistGuidanceNotes.AsNoTracking()
        join user in db.Users.AsNoTracking() on note.ClientId equals user.Id
        where note.SpecialistId == id && db.SpecialistAssignments.Any(a => a.SpecialistId == id &&
            a.ClientId == note.ClientId && a.SpecialistRole == SpecialistRole && a.Status == "active")
        orderby note.CreatedAt descending
        select new NutritionGuidanceVm(note.Id, note.ClientId, user.FullName,
            note.Topic, note.Title, note.Body, note.CreatedAt)).Take(100).ToListAsync();

    public async Task<bool> AddGuidanceAsync(Guid id, NutritionGuidanceForm form)
    {
        if (!await Assigned(id, form.ClientId)) return false;
        db.SpecialistGuidanceNotes.Add(new SpecialistGuidanceNote { Id = Guid.NewGuid(), SpecialistId = id,
            ClientId = form.ClientId, Topic = form.Topic.Trim(), Title = form.Title.Trim(),
            Body = form.Body.Trim(), CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(); return true;
    }

    public async Task<bool> DeleteGuidanceAsync(Guid id, Guid noteId)
    {
        var note = await db.SpecialistGuidanceNotes.SingleOrDefaultAsync(x => x.Id == noteId && x.SpecialistId == id);
        if (note is null || !await Assigned(id, note.ClientId)) return false;
        db.SpecialistGuidanceNotes.Remove(note); await db.SaveChangesAsync(); return true;
    }

    public async Task<NutritionMessagesVm> GetMessagesAsync(Guid id, Guid? selectedId)
    {
        var participations = await db.ConversationParticipants.AsNoTracking().Where(x => x.UserId == id)
            .Select(x => new { x.ConversationId, x.LastReadAt }).ToListAsync();
        var ids = participations.Select(x => x.ConversationId).ToArray();
        var conversations = await db.Conversations.AsNoTracking().Where(x => ids.Contains(x.Id) &&
            (x.Kind == SpecialistRole || x.Kind == "support")).OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync();
        var threadIds = conversations.Select(x => x.Id).ToArray();
        var otherPeople = await (from p in db.ConversationParticipants.AsNoTracking()
            join u in db.Users.AsNoTracking() on p.UserId equals u.Id
            where threadIds.Contains(p.ConversationId) && p.UserId != id
            select new { p.ConversationId, u.Id, u.FullName, u.Role }).ToListAsync();
        var activeClients = await db.SpecialistAssignments.AsNoTracking().Where(a => a.SpecialistId == id &&
            a.SpecialistRole == SpecialistRole && a.Status == "active").Select(a => a.ClientId).ToListAsync();
        var valid = conversations.Where(c =>
        {
            var others = otherPeople.Where(x => x.ConversationId == c.Id).ToList();
            return others.Count == 1 && (c.Kind == "support" ? others[0].Role == "admin" :
                others[0].Role == "client" && activeClients.Contains(others[0].Id));
        }).ToList();
        var validIds = valid.Select(x => x.Id).ToArray();
        var latest = await db.Messages.AsNoTracking().Where(x => validIds.Contains(x.ConversationId))
            .GroupBy(x => x.ConversationId).Select(g => new { Id = g.Key, At = g.Max(x => x.SentAt) }).ToListAsync();
        var threads = valid.Select(c =>
        {
            var other = otherPeople.First(x => x.ConversationId == c.Id &&
                (c.Kind == "support" ? x.Role == "admin" : x.Role == "client"));
            return new NutritionThreadVm(c.Id, c.Kind == SpecialistRole ? other.Id : null,
                other.FullName, c.Kind, c.Status, latest.FirstOrDefault(x => x.Id == c.Id)?.At, 0);
        }).OrderByDescending(x => x.LastMessageAt).ToList();
        var selected = selectedId.HasValue ? threads.FirstOrDefault(x => x.Id == selectedId.Value) : threads.FirstOrDefault();
        if (selected is null) return new NutritionMessagesVm { Threads = threads };
        var rows = await (from message in db.Messages.AsNoTracking()
            join sender in db.Users.AsNoTracking() on message.SenderId equals sender.Id
            where message.ConversationId == selected.Id
            orderby message.SentAt descending
            select new { message.Id, message.SenderId, sender.FullName, message.Body, message.SentAt }).Take(100).ToListAsync();
        var participant = await db.ConversationParticipants.SingleAsync(x => x.ConversationId == selected.Id && x.UserId == id);
        participant.LastReadAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        rows.Reverse();
        return new NutritionMessagesVm { Threads = threads, Selected = selected,
            Messages = rows.Select(x => new NutritionMessageVm(x.Id, x.FullName, x.SenderId == id, x.Body, x.SentAt)).ToList() };
    }

    public async Task<Guid?> OpenClientThreadAsync(Guid id, Guid clientId)
    {
        if (!await Assigned(id, clientId)) return null;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var existing = await db.Conversations.Where(c => c.Kind == SpecialistRole && c.Status == "open" &&
            db.ConversationParticipants.Any(p => p.ConversationId == c.Id && p.UserId == id) &&
            db.ConversationParticipants.Any(p => p.ConversationId == c.Id && p.UserId == clientId))
            .Select(c => (Guid?)c.Id).FirstOrDefaultAsync();
        if (existing.HasValue) return existing;
        var conversation = new LiftConversation { Id = Guid.NewGuid(), Kind = SpecialistRole,
            Subject = SpecialistRole == "coach" ? "Workout guidance" : "Nutrition guidance", Status = "open", CreatedAt = DateTimeOffset.UtcNow };
        db.Conversations.Add(conversation);
        db.ConversationParticipants.AddRange(new ConversationParticipant { ConversationId = conversation.Id, UserId = id },
            new ConversationParticipant { ConversationId = conversation.Id, UserId = clientId });
        await db.SaveChangesAsync(); await tx.CommitAsync(); return conversation.Id;
    }

    public async Task<Guid?> OpenAdminThreadAsync(Guid id)
    {
        var existing = await db.Conversations.Where(c => c.Kind == "support" && c.Status == "open" &&
            db.ConversationParticipants.Any(p => p.ConversationId == c.Id && p.UserId == id))
            .Select(c => (Guid?)c.Id).FirstOrDefaultAsync();
        if (existing.HasValue) return existing;
        var admin = await db.Users.AsNoTracking().Where(x => x.Role == "admin" && x.Status == "active")
            .OrderBy(x => x.CreatedAt).Select(x => (Guid?)x.Id).FirstOrDefaultAsync();
        if (!admin.HasValue) return null;
        var conversation = new LiftConversation { Id = Guid.NewGuid(), Kind = "support",
            Subject = SpecialistRole == "coach" ? "Coach support" : "Nutritionist support", Status = "open", CreatedAt = DateTimeOffset.UtcNow };
        db.Conversations.Add(conversation);
        db.ConversationParticipants.AddRange(new ConversationParticipant { ConversationId = conversation.Id, UserId = id },
            new ConversationParticipant { ConversationId = conversation.Id, UserId = admin.Value });
        await db.SaveChangesAsync(); return conversation.Id;
    }

    public async Task<bool> SendMessageAsync(Guid id, NutritionMessageForm form)
    {
        var conversation = await db.Conversations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == form.ConversationId && x.Status == "open");
        if (conversation is null || !await db.ConversationParticipants.AnyAsync(x => x.ConversationId == conversation.Id && x.UserId == id)) return false;
        var others = await (from p in db.ConversationParticipants.AsNoTracking()
            join u in db.Users.AsNoTracking() on p.UserId equals u.Id
            where p.ConversationId == conversation.Id && p.UserId != id
            select new { u.Id, u.Role }).ToListAsync();
        var allowed = others.Count == 1 && conversation.Kind == "support" && others[0].Role == "admin";
        if (others.Count == 1 && conversation.Kind == SpecialistRole)
        {
            foreach (var other in others.Where(x => x.Role == "client"))
                if (await Assigned(id, other.Id)) { allowed = true; break; }
        }
        if (!allowed || string.IsNullOrWhiteSpace(form.Body)) return false;
        var now = DateTimeOffset.UtcNow;
        db.Messages.Add(new LiftMessage { Id = Guid.NewGuid(), ConversationId = conversation.Id,
            SenderId = id, Body = form.Body.Trim(), SentAt = now });
        foreach (var other in others) db.Notifications.Add(new LiftNotification { Id = Guid.NewGuid(), UserId = other.Id,
            Type = "message", Title = "New message", Body = "You have a new message in LIFT.",
            TargetUrl = other.Role == "admin" ? "/Admin/Support" : "/Client/Messages/messages.html", CreatedAt = now });
        await db.SaveChangesAsync(); return true;
    }

    public async Task<List<NutritionMeetingVm>> GetMeetingsAsync(Guid id) => await (
        from meeting in db.Meetings.AsNoTracking()
        join client in db.Users.AsNoTracking() on meeting.ClientId equals client.Id
        where meeting.SpecialistId == id
        orderby meeting.StartsAt descending
        select new NutritionMeetingVm(meeting.Id, client.FullName, meeting.Topic, meeting.StartsAt,
            meeting.DurationMinutes, meeting.Status, meeting.MeetingUrl)).Take(100).ToListAsync();

    public async Task<string?> CreateMeetingAsync(Guid id, NutritionMeetingForm form)
    {
        if (!await Assigned(id, form.ClientId)) return "Client is not currently assigned to you.";
        if (form.StartsAt <= DateTimeOffset.UtcNow.AddMinutes(5)) return "Choose a future time.";
        if (form.MeetingUrl is { Length: > 0 } && (!Uri.TryCreate(form.MeetingUrl, UriKind.Absolute, out var url) || url.Scheme != "https"))
            return "Use an HTTPS meeting link.";
        var end = form.StartsAt.AddMinutes(form.DurationMinutes);
        var possible = await db.Meetings.AsNoTracking().Where(x => x.SpecialistId == id &&
            x.Status == "accepted" && x.StartsAt < end)
            .Select(x => new { x.StartsAt, x.DurationMinutes }).ToListAsync();
        var overlaps = possible.Any(x => x.StartsAt.AddMinutes(x.DurationMinutes) > form.StartsAt);
        if (overlaps) return "This time overlaps an accepted meeting.";
        db.Meetings.Add(new LiftMeeting { Id = Guid.NewGuid(), ClientId = form.ClientId, SpecialistId = id,
            Topic = form.Topic.Trim(), StartsAt = form.StartsAt, DurationMinutes = form.DurationMinutes,
            MeetingUrl = form.MeetingUrl?.Trim(), Status = "pending", CreatedAt = DateTimeOffset.UtcNow });
        db.Notifications.Add(new LiftNotification { Id = Guid.NewGuid(), UserId = form.ClientId,
            Type = "meeting", Title = "Meeting proposed", Body = $"Your {SpecialistRole} proposed a meeting time.",
            TargetUrl = "/Client/Meetings/meetings.html", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(); return null;
    }

    public async Task<bool> CancelMeetingAsync(Guid id, Guid meetingId)
    {
        var meeting = await db.Meetings.SingleOrDefaultAsync(x => x.Id == meetingId && x.SpecialistId == id &&
            (x.Status == "pending" || x.Status == "accepted") && x.StartsAt > DateTimeOffset.UtcNow);
        if (meeting is null) return false;
        meeting.Status = "cancelled";
        db.Notifications.Add(new LiftNotification { Id = Guid.NewGuid(), UserId = meeting.ClientId,
            Type = "meeting", Title = "Meeting cancelled", Body = $"Your {SpecialistRole} cancelled a meeting.",
            TargetUrl = "/Client/Meetings/meetings.html", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(); return true;
    }
}
