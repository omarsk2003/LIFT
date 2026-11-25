using System.ComponentModel.DataAnnotations;

namespace LIFT.Models;

public sealed record AdminPlanVm(int Id, string Code, string Name, decimal MonthlyPrice, bool IsActive);
public sealed record AdminPromoVm(Guid Id, string Code, decimal DiscountPercent, bool IsActive,
    int? PlanId, string PlanScope, DateTimeOffset? StartsAt, DateTimeOffset? EndsAt);
public sealed record AdminCatalogVm(IReadOnlyList<AdminPlanVm> Plans, IReadOnlyList<AdminPromoVm> Promos);

public sealed class AdminPriceForm
{
    [Range(1, int.MaxValue)] public int PlanId { get; set; }
    [Range(typeof(decimal), "0.01", "99999999.99")] public decimal MonthlyPrice { get; set; }
}

public sealed class AdminPromoForm
{
    public Guid? Id { get; set; }
    [Required, StringLength(50, MinimumLength = 3), RegularExpression("[A-Za-z0-9_-]+")]
    public string Code { get; set; } = "";
    [Range(typeof(decimal), "0.01", "99.99")] public decimal DiscountPercent { get; set; }
    public int? PlanId { get; set; }
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
}

public sealed class AdminPromoToggleForm
{
    [Required] public Guid Id { get; set; }
}
