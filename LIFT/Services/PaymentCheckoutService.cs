using System.Data;
using LIFT.Data;
using LIFT.Models;
using Microsoft.EntityFrameworkCore;

namespace LIFT.Services;

public sealed class PaymentCheckoutService(LiftDbContext db, IPayPalGateway gateway,
    ILogger<PaymentCheckoutService> logger) : IPaymentCheckoutService
{
    public bool IsConfigured => gateway.IsConfigured;

    private static string[] Roles(string code) => code switch
    {
        "ai_coach" => ["coach"], "ai_nutritionist" => ["nutritionist"],
        "premium" => ["coach", "nutritionist"], _ => []
    };

    public async Task<CheckoutStartResult> StartAsync(Guid clientId, StartCheckoutForm form,
        Uri siteBase, CancellationToken ct)
    {
        if (!IsConfigured) return new(false, "PayPal Sandbox credentials are not configured.", null);
        var plan = await db.SubscriptionPlans.AsNoTracking().SingleOrDefaultAsync(x => x.Id == form.PlanId && x.IsActive, ct);
        if (plan is null || plan.MonthlyPrice <= 0 || Roles(plan.Code).Length == 0)
            return new(false, "Select an available paid plan.", null);
        var now = DateTimeOffset.UtcNow;
        var price = plan.MonthlyPrice;
        if (!string.IsNullOrWhiteSpace(form.PromoCode))
        {
            var code = form.PromoCode.Trim().ToUpperInvariant();
            var promo = await db.PromoCodes.AsNoTracking().SingleOrDefaultAsync(x =>
                x.Code == code && x.IsActive &&
                (x.StartsAt == null || x.StartsAt <= now) &&
                (x.EndsAt == null || x.EndsAt > now), ct);
            if (promo is null || (await db.PromoCodePlans.AnyAsync(x => x.PromoCodeId == promo.Id, ct) &&
                !await db.PromoCodePlans.AnyAsync(x => x.PromoCodeId == promo.Id && x.PlanId == plan.Id, ct)))
                return new(false, "This promo code is unavailable for the selected plan.", null);
            price = decimal.Round(plan.MonthlyPrice * (100 - promo.DiscountPercent) / 100, 2,
                MidpointRounding.AwayFromZero);
            if (price < 0.01m) return new(false, "This discount cannot be processed through PayPal checkout.", null);
        }
        var chosen = new Dictionary<string, Guid>();
        if (form.CoachId.HasValue) chosen["coach"] = form.CoachId.Value;
        if (form.NutritionistId.HasValue) chosen["nutritionist"] = form.NutritionistId.Value;
        var required = Roles(plan.Code);
        if (chosen.Count != required.Length || required.Any(role => !chosen.ContainsKey(role)))
            return new(false, "Select the required available specialists for this plan.", null);
        foreach (var (role, id) in chosen)
        {
            var available = await db.SpecialistProfiles.AsNoTracking().AnyAsync(p => p.UserId == id &&
                p.ApprovedAt != null && p.AcceptingNewClients &&
                db.Users.Any(u => u.Id == id && u.Role == role && u.Status == "active") &&
                db.SpecialistAssignments.Count(a => a.SpecialistId == id && a.Status == "active") < p.Capacity, ct);
            if (!available) return new(false, $"The selected {role} is no longer available.", null);
        }
        var subscription = new Subscription { Id = Guid.NewGuid(), ClientId = clientId, PlanId = plan.Id,
            Status = "pending", PriceAtStart = price, Currency = "USD", StartsAt = now };
        var payment = new LiftPayment { Id = Guid.NewGuid(), SubscriptionId = subscription.Id,
            Provider = "paypal", Amount = price, Currency = "USD", Status = "pending", CreatedAt = now };
        db.Subscriptions.Add(subscription); db.Payments.Add(payment);
        foreach (var (role, id) in chosen) db.SubscriptionSpecialistSelections.Add(
            new SubscriptionSpecialistSelection { SubscriptionId = subscription.Id, SpecialistRole = role,
                SpecialistId = id, SelectedAt = now });
        await db.SaveChangesAsync(ct);
        try
        {
            var returnUrl = new Uri(siteBase, "Client/PaymentReturn");
            var cancelUrl = new Uri(siteBase, "Client/PaymentCancelled");
            var order = await gateway.CreateOrderAsync(payment.Id, payment.Amount, payment.Currency,
                returnUrl, cancelUrl, ct);
            payment.ProviderPaymentId = order.Id;
            subscription.ExternalBillingId = order.Id;
            await db.SaveChangesAsync(ct);
            return new(true, null, order.ApprovalUrl);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or TaskCanceledException)
        {
            logger.LogWarning(ex, "PayPal order creation failed for payment {PaymentId}", payment.Id);
            payment.Status = "failed"; subscription.Status = "cancelled";
            await db.SaveChangesAsync(CancellationToken.None);
            return new(false, "PayPal could not create the order. Please try again.", null);
        }
    }

    public async Task<CheckoutCaptureResult> CompleteAsync(Guid clientId, string orderId, CancellationToken ct)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(orderId)) return new(false, "PayPal is not configured.");
        var payment = await (from p in db.Payments
            join s in db.Subscriptions on p.SubscriptionId equals s.Id
            where s.ClientId == clientId && p.Provider == "paypal" && p.ProviderPaymentId == orderId
            select p).SingleOrDefaultAsync(ct);
        if (payment is null) return new(false, "Payment order not found.");
        if (payment.Status == "succeeded")
        {
            var active = await db.Subscriptions.AnyAsync(x => x.Id == payment.SubscriptionId && x.Status == "active", ct);
            return active ? new(true, "Payment was already confirmed.") :
                new(false, "Payment was confirmed; support must complete the subscription.");
        }
        if (payment.Status != "pending") return new(false, "This payment is no longer pending.");
        PayPalCapture captured;
        try { captured = await gateway.CaptureOrderAsync(orderId, payment.Id, ct); }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or TaskCanceledException or ArgumentException or KeyNotFoundException)
        {
            logger.LogWarning(ex, "PayPal capture verification failed for payment {PaymentId}", payment.Id);
            return new(false, "Payment confirmation is pending. Please retry or contact support.");
        }
        if (captured.Status != "COMPLETED" || captured.OrderId != orderId ||
            captured.Amount != payment.Amount || captured.Currency != payment.Currency ||
            string.IsNullOrWhiteSpace(captured.CaptureId))
            return new(false, "PayPal has not confirmed the expected payment amount.");

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        payment = await db.Payments.SingleAsync(x => x.Id == payment.Id, ct);
        if (payment.Status == "succeeded") return new(false, "Payment was confirmed; support must complete the subscription.");
        if (payment.Status != "pending") return new(false, "This payment is no longer pending.");
        var subscription = await db.Subscriptions.SingleAsync(x => x.Id == payment.SubscriptionId && x.ClientId == clientId, ct);
        if (subscription.Status != "pending") return new(false, "Subscription is no longer pending.");
        var code = await db.SubscriptionPlans.Where(x => x.Id == subscription.PlanId).Select(x => x.Code).SingleAsync(ct);
        var selections = await db.SubscriptionSpecialistSelections.Where(x => x.SubscriptionId == subscription.Id).ToListAsync(ct);
        var roles = Roles(code);
        if (roles.Length != selections.Count || roles.Any(role => !selections.Any(s => s.SpecialistRole == role)))
            return new(false, "Payment confirmed; specialist selection needs support review.");
        var eventId = "capture:" + captured.CaptureId;
        if (!await db.PaymentEvents.AnyAsync(x => x.Provider == "paypal" && x.ProviderEventId == eventId, ct))
            db.PaymentEvents.Add(new LiftPaymentEvent { Id = Guid.NewGuid(), PaymentId = payment.Id,
                Provider = "paypal", ProviderEventId = eventId, EventType = "PAYMENT.CAPTURE.COMPLETED",
                ProcessingStatus = "received", ReceivedAt = DateTimeOffset.UtcNow });
        payment.Status = "succeeded"; payment.CompletedAt = DateTimeOffset.UtcNow;
        // A successful capture remains recorded even if a chosen specialist has filled their capacity.
        var available = true;
        foreach (var choice in selections)
        {
            if (!await db.SpecialistProfiles.AnyAsync(p => p.UserId == choice.SpecialistId &&
                p.ApprovedAt != null && p.AcceptingNewClients &&
                db.Users.Any(u => u.Id == p.UserId && u.Role == choice.SpecialistRole && u.Status == "active") &&
                db.SpecialistAssignments.Count(a => a.SpecialistId == p.UserId && a.Status == "active") < p.Capacity, ct))
                available = false;
        }
        if (available)
        {
            var previous = await db.Subscriptions.Where(x => x.ClientId == clientId && x.Status == "active").ToListAsync(ct);
            var previousIds = previous.Select(x => x.Id).ToArray();
            var assignments = await db.SpecialistAssignments.Where(x => previousIds.Contains(x.SubscriptionId) && x.Status == "active").ToListAsync(ct);
            var now = DateTimeOffset.UtcNow;
            foreach (var item in assignments) { item.Status = "ended"; item.EndedAt = now; }
            foreach (var item in previous) { item.Status = "cancelled"; item.EndsAt = now; }
            await db.SaveChangesAsync(ct);
            subscription.Status = "active"; subscription.StartsAt = now; subscription.EndsAt = now.AddMonths(1);
            foreach (var item in selections) db.SpecialistAssignments.Add(new SpecialistAssignment { Id = Guid.NewGuid(),
                ClientId = clientId, SubscriptionId = subscription.Id, SpecialistId = item.SpecialistId,
                SpecialistRole = item.SpecialistRole, Status = "active", AssignedAt = now });
            db.Notifications.Add(new LiftNotification { Id = Guid.NewGuid(), UserId = clientId,
                Type = "subscription", Title = "Subscription active", Body = "Your paid plan is now active.",
                TargetUrl = "/Client/Subscription", CreatedAt = now });
        }
        var paymentEvent = db.ChangeTracker.Entries<LiftPaymentEvent>().Select(x => x.Entity)
            .FirstOrDefault(x => x.ProviderEventId == eventId);
        if (paymentEvent is not null)
        {
            paymentEvent.ProcessingStatus = available ? "processed" : "failed";
            paymentEvent.ProcessedAt = DateTimeOffset.UtcNow;
            paymentEvent.ErrorCode = available ? null : "specialist_unavailable";
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return available ? new(true, "Payment confirmed and subscription activated.") :
            new(false, "Payment confirmed. Your chosen specialist is unavailable; support will complete your subscription.");
    }

    public async Task CancelAsync(Guid clientId, string orderId, CancellationToken ct)
    {
        var row = await (from p in db.Payments join s in db.Subscriptions on p.SubscriptionId equals s.Id
            where s.ClientId == clientId && p.Provider == "paypal" && p.ProviderPaymentId == orderId &&
                p.Status == "pending" && s.Status == "pending" select new { p, s }).SingleOrDefaultAsync(ct);
        if (row is null) return;
        row.p.Status = "cancelled"; row.s.Status = "cancelled";
        await db.SaveChangesAsync(ct);
    }
}
