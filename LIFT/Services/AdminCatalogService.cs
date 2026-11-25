using LIFT.Data;
using LIFT.Models;
using Microsoft.EntityFrameworkCore;

namespace LIFT.Services;

public sealed class AdminCatalogService(LiftDbContext db, ILogger<AdminCatalogService> logger) : IAdminCatalogService
{
    public async Task<AdminCatalogVm> GetAsync(CancellationToken ct)
    {
        var plans = await db.SubscriptionPlans.AsNoTracking().OrderBy(x => x.Id)
            .Select(x => new AdminPlanVm(x.Id, x.Code, x.Name, x.MonthlyPrice, x.IsActive)).ToListAsync(ct);
        var promos = await (
            from promo in db.PromoCodes.AsNoTracking()
            orderby promo.Code
            select new AdminPromoVm(promo.Id, promo.Code, promo.DiscountPercent, promo.IsActive,
                db.PromoCodePlans.Where(x => x.PromoCodeId == promo.Id).Select(x => (int?)x.PlanId).FirstOrDefault(),
                db.PromoCodePlans.Any(x => x.PromoCodeId == promo.Id) ? "Specific plan" : "All paid plans",
                promo.StartsAt, promo.EndsAt)
        ).ToListAsync(ct);
        return new AdminCatalogVm(plans, promos);
    }

    public async Task<string?> UpdatePriceAsync(AdminPriceForm form, Guid adminId, CancellationToken ct)
    {
        var plan = await db.SubscriptionPlans.SingleOrDefaultAsync(x => x.Id == form.PlanId, ct);
        if (plan is null || plan.Code == "free") return "This plan cannot be repriced.";
        plan.MonthlyPrice = form.MonthlyPrice;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Admin {AdminId} changed plan {PlanId} price", adminId, plan.Id);
        return null;
    }

    public async Task<string?> SavePromoAsync(AdminPromoForm form, Guid adminId, CancellationToken ct)
    {
        var code = form.Code.Trim().ToUpperInvariant();
        if (form.DiscountPercent >= 100) return "A 100% discount cannot be processed through PayPal checkout.";
        if (form.EndsAt.HasValue && form.StartsAt.HasValue && form.EndsAt <= form.StartsAt)
            return "The end time must be after the start time.";
        if (form.PlanId.HasValue && !await db.SubscriptionPlans.AnyAsync(
            x => x.Id == form.PlanId && x.Code != "free" && x.IsActive, ct))
            return "Choose an active paid plan.";
        if (await db.PromoCodes.AnyAsync(x => x.Code == code && x.Id != form.Id, ct))
            return "This promo code already exists.";

        PromoCode promo;
        if (form.Id.HasValue)
        {
            var existingPromo = await db.PromoCodes.SingleOrDefaultAsync(x => x.Id == form.Id, ct);
            if (existingPromo is null) return "The promo code no longer exists.";
            promo = existingPromo;
        }
        else
        {
            promo = new PromoCode { Id = Guid.NewGuid(), IsActive = true };
            db.PromoCodes.Add(promo);
        }
        promo.Code = code;
        promo.DiscountPercent = form.DiscountPercent;
        promo.StartsAt = form.StartsAt;
        promo.EndsAt = form.EndsAt;

        var existing = await db.PromoCodePlans.Where(x => x.PromoCodeId == promo.Id).ToListAsync(ct);
        foreach (var item in existing.Where(x => x.PlanId != form.PlanId)) db.PromoCodePlans.Remove(item);
        if (form.PlanId.HasValue && existing.All(x => x.PlanId != form.PlanId.Value))
            db.PromoCodePlans.Add(new PromoCodePlan { PromoCodeId = promo.Id, PlanId = form.PlanId.Value });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Admin {AdminId} saved promo {PromoId}", adminId, promo.Id);
        return null;
    }

    public async Task<string?> TogglePromoAsync(Guid id, Guid adminId, CancellationToken ct)
    {
        var promo = await db.PromoCodes.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (promo is null) return "Promo code not found.";
        promo.IsActive = !promo.IsActive;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Admin {AdminId} set promo {PromoId} active={Active}", adminId, id, promo.IsActive);
        return null;
    }
}

