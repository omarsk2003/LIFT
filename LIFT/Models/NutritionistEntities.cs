namespace LIFT.Models;

public sealed class PlanVersion
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public string PlanType { get; set; } = "nutrition";
    public int VersionNumber { get; set; }
    public string Source { get; set; } = "specialist";
    public Guid? CreatedBy { get; set; }
    public Guid? ReviewedBy { get; set; }
    public string Status { get; set; } = "draft";
    public string? Title { get; set; }
    public string? Notes { get; set; }
    public int? Calories { get; set; }
    public decimal? ProteinGrams { get; set; }
    public decimal? CarbsGrams { get; set; }
    public decimal? FatGrams { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}

public sealed class NutritionPlanMeal
{
    public Guid Id { get; set; }
    public Guid PlanVersionId { get; set; }
    public short SortOrder { get; set; }
    public string MealName { get; set; } = "";
    public string? Description { get; set; }
}
