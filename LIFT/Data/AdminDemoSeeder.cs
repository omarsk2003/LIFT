using System.Data;
using LIFT.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LIFT.Data;

// Explicit, development-only fixture. Never runs during normal application startup.
public static class AdminDemoSeeder
{
    public static async Task RunAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiftDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<LiftUser>>();
        var plan = await db.SubscriptionPlans.AsNoTracking()
            .Where(x => x.IsActive && x.MonthlyPrice > 0)
            .OrderByDescending(x => x.Code == "premium")
            .FirstOrDefaultAsync() ?? throw new InvalidOperationException("Seed the paid subscription plans first.");

        var now = DateTimeOffset.UtcNow;
        var password = Environment.GetEnvironmentVariable("LIFT_DEMO_PASSWORD") ?? "LiftDemo2026!";
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        async Task<LiftUser> User(string email, string name, string role, string status, DateTimeOffset created)
        {
            var existing = await db.Users.SingleOrDefaultAsync(x => x.Email == email);
            if (existing is not null) return existing;
            var user = new LiftUser
            {
                Id = Guid.NewGuid(), Email = email, FullName = name, Role = role,
                Status = status, CreatedAt = created
            };
            user.PasswordHash = hasher.HashPassword(user, password);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return user;
        }

        async Task<ClientProfile> ClientProfile(LiftUser user)
        {
            var profile = await db.ClientProfiles.SingleOrDefaultAsync(x => x.UserId == user.Id);
            if (profile is not null) return profile;
            profile = new ClientProfile { UserId = user.Id, Goal = "Build a consistent routine", UpdatedAt = now };
            db.ClientProfiles.Add(profile);
            await db.SaveChangesAsync();
            return profile;
        }

        async Task<SpecialistProfile> Specialist(string email, string name, string role,
            string status, string specialty)
        {
            var user = await User(email, name, role, status, now.AddDays(-40));
            var profile = await db.SpecialistProfiles.SingleOrDefaultAsync(x => x.UserId == user.Id);
            if (profile is null)
            {
                profile = new SpecialistProfile
                {
                    UserId = user.Id, Specialty = specialty, Bio = "Development sample profile.",
                    Capacity = 8, AcceptingNewClients = status == "active",
                    ApprovedAt = status == "active" ? now.AddDays(-35) : null
                };
                db.SpecialistProfiles.Add(profile);
                await db.SaveChangesAsync();
            }
            return profile;
        }

        await User("admin.demo@lift.local", "Demo Admin", "admin", "active", now.AddMonths(-10));
        var mainClient = await User("client.demo@lift.local", "Maya Saleh (Demo)", "client", "active", now.AddMonths(-5));
        await ClientProfile(mainClient);
        for (var index = 1; index <= 8; index++)
        {
            var month = now.AddMonths(-index);
            var sample = await User($"client{index}.demo@lift.local", $"Sample Client {index}",
                "client", index == 8 ? "suspended" : "active", month);
            await ClientProfile(sample);
        }

        var currentCoach = await Specialist("coach.demo@lift.local", "Omar Haddad (Demo)",
            "coach", "active", "Strength training");
        await Specialist("coach2.demo@lift.local", "Lina Nasser (Demo)",
            "coach", "active", "Functional fitness");
        await Specialist("nutrition.demo@lift.local", "Sara Khalil (Demo)",
            "nutritionist", "active", "Everyday nutrition");
        // This pending application is useful for testing rejection; approval requires a real PDF certificate.
        await Specialist("coach.pending.demo@lift.local", "Tariq Amin (Demo)",
            "coach", "pending", "Beginner training");

        var subscription = await db.Subscriptions.SingleOrDefaultAsync(x =>
            x.ClientId == mainClient.Id && x.Status == "active" && x.PlanId == plan.Id);
        if (subscription is null)
        {
            subscription = new Subscription
            {
                Id = Guid.NewGuid(), ClientId = mainClient.Id, PlanId = plan.Id,
                Status = "active", PriceAtStart = plan.MonthlyPrice, Currency = "USD",
                StartsAt = now.AddDays(-4), EndsAt = now.AddDays(26)
            };
            db.Subscriptions.Add(subscription);
            await db.SaveChangesAsync();
        }
        var assignment = await db.SpecialistAssignments.SingleOrDefaultAsync(x =>
            x.ClientId == mainClient.Id && x.SpecialistRole == "coach" && x.Status == "active");
        if (assignment is null)
        {
            assignment = new SpecialistAssignment
            {
                Id = Guid.NewGuid(), ClientId = mainClient.Id, SubscriptionId = subscription.Id,
                SpecialistId = currentCoach.UserId, SpecialistRole = "coach",
                Status = "active", AssignedAt = now.AddDays(-3)
            };
            db.SpecialistAssignments.Add(assignment);
            await db.SaveChangesAsync();
        }
        if (!await db.SpecialistChangeRequests.AnyAsync(x => x.AssignmentId == assignment.Id))
        {
            db.SpecialistChangeRequests.Add(new SpecialistChangeRequest
            {
                Id = Guid.NewGuid(), AssignmentId = assignment.Id, ClientId = mainClient.Id,
                Reason = "I need a coach with a different weekly schedule.",
                Status = "pending", RequestedAt = now.AddHours(-8)
            });
        }
        if (!await db.ContactMessages.AnyAsync(x => x.Email == "contact.demo@lift.local"))
        {
            db.ContactMessages.Add(new ContactMessage
            {
                Id = Guid.NewGuid(), FullName = "Demo Visitor", Email = "contact.demo@lift.local",
                Body = "Can I adjust my training schedule after signing up?",
                Status = "new", CreatedAt = now.AddHours(-2)
            });
        }
        var support = await db.Conversations.SingleOrDefaultAsync(x =>
            x.Kind == "support" && x.Subject == "Demo: adjusting my schedule");
        if (support is null)
        {
            support = new LiftConversation
            {
                Id = Guid.NewGuid(), Kind = "support", Subject = "Demo: adjusting my schedule",
                Status = "open", CreatedAt = now.AddHours(-3)
            };
            db.Conversations.Add(support);
            db.ConversationParticipants.Add(new ConversationParticipant
                { ConversationId = support.Id, UserId = mainClient.Id });
            db.Messages.Add(new LiftMessage
            {
                Id = Guid.NewGuid(), ConversationId = support.Id, SenderId = mainClient.Id,
                Body = "I need to move my training sessions to evenings. Can my plan adapt?",
                SentAt = now.AddHours(-3)
            });
        }
        var promo = await db.PromoCodes.SingleOrDefaultAsync(x => x.Code == "LIFTDEMO15");
        if (promo is null)
        {
            promo = new PromoCode
            {
                Id = Guid.NewGuid(), Code = "LIFTDEMO15", DiscountPercent = 15,
                IsActive = true, StartsAt = now.AddDays(-1), EndsAt = now.AddMonths(1)
            };
            db.PromoCodes.Add(promo);
            db.PromoCodePlans.Add(new PromoCodePlan { PromoCodeId = promo.Id, PlanId = plan.Id });
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        Console.WriteLine("Admin demo data is ready. Sign in with admin.demo@lift.local and the LIFT_DEMO_PASSWORD value (default: LiftDemo2026!).");
    }
}
