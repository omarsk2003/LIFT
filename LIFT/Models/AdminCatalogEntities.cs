namespace LIFT.Models;

public sealed class PromoCode
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public decimal DiscountPercent { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
}

public sealed class PromoCodePlan
{
    public Guid PromoCodeId { get; set; }
    public int PlanId { get; set; }
}
