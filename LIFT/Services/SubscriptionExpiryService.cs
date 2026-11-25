using System.Data;
using LIFT.Data;
using LIFT.Models;
using Microsoft.EntityFrameworkCore;

namespace LIFT.Services;

public sealed class SubscriptionExpiryService(IServiceScopeFactory scopes,
    ILogger<SubscriptionExpiryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ExpireDueAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        while (await timer.WaitForNextTickAsync(stoppingToken)) await ExpireDueAsync(stoppingToken);
    }

    private async Task ExpireDueAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LiftDbContext>();
            var now = DateTimeOffset.UtcNow;
            var due = await db.Subscriptions.AsNoTracking()
                .Where(x => x.Status == "active" && x.EndsAt != null && x.EndsAt <= now)
                .OrderBy(x => x.EndsAt).Select(x => x.Id).Take(100).ToListAsync(ct);
            if (due.Count == 0) return;
            var freeId = await db.SubscriptionPlans.AsNoTracking().Where(x => x.Code == "free")
                .Select(x => x.Id).SingleAsync(ct);
            foreach (var id in due)
            {
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var subscription = await db.Subscriptions.SingleAsync(x => x.Id == id, ct);
                if (subscription.Status == "active" && subscription.EndsAt <= DateTimeOffset.UtcNow)
                {
                    var assignments = await db.SpecialistAssignments.Where(x => x.SubscriptionId == id && x.Status == "active")
                        .ToListAsync(ct);
                    var endedAt = DateTimeOffset.UtcNow;
                    foreach (var item in assignments) { item.Status = "ended"; item.EndedAt = endedAt; }
                    subscription.Status = "expired";
                    await db.SaveChangesAsync(ct);
                    db.Subscriptions.Add(new Subscription { Id = Guid.NewGuid(), ClientId = subscription.ClientId,
                        PlanId = freeId, PriceAtStart = 0, Currency = "USD", Status = "active", StartsAt = endedAt });
                    await db.SaveChangesAsync(ct);
                }
                await tx.CommitAsync(ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) { logger.LogError(ex, "Subscription expiration pass failed"); }
    }
}
