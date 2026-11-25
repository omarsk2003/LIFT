using System.ComponentModel.DataAnnotations;

namespace LIFT.Models;

public sealed record NutritionistClientVm(Guid ClientId, string Name, string Email, string? Goal,
    string PlanStatus, int? Calories, DateTimeOffset AssignedAt);

public sealed class NutritionistDashboardVm
{
    public string Name { get; set; } = "";
    public List<NutritionistClientVm> Clients { get; set; } = [];
    public int PublishedPlans { get; set; }
}

public sealed class NutritionPlanForm : IValidatableObject
{
    public Guid ClientId { get; set; }
    [Required, StringLength(200)] public string Title { get; set; } = "";
    [StringLength(2000)] public string? Notes { get; set; }
    [Range(500, 10000)] public int Calories { get; set; }
    [Range(0, 1000)] public decimal ProteinGrams { get; set; }
    [Range(0, 1000)] public decimal CarbsGrams { get; set; }
    [Range(0, 1000)] public decimal FatGrams { get; set; }
    [StringLength(100)] public string? MealName { get; set; }
    [StringLength(1000)] public string? MealDescription { get; set; }
    [StringLength(4000)] public string? MealsText { get; set; }
    public bool Publish { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var lines = (MealsText ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length > 10) yield return new ValidationResult("Use at most 10 meals.", [nameof(MealsText)]);
        foreach (var line in lines)
        {
            var parts = line.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts[0].Length is < 1 or > 100 || (parts.Length > 1 && parts[1].Length > 1000))
                yield return new ValidationResult("Each meal needs a name up to 100 characters and details up to 1000.", [nameof(MealsText)]);
        }
    }
}

public sealed class NutritionistClientDetailVm
{
    public NutritionistClientVm Client { get; set; } = null!;
    public NutritionPlanForm Form { get; set; } = new();
    public int? PublishedVersion { get; set; }
    public List<PendingNutritionPlanVm> PendingAiPlans { get; set; } = [];
}

public sealed record PendingNutritionPlanVm(Guid Id, int VersionNumber, string? Title,
    int? Calories, decimal? ProteinGrams, decimal? CarbsGrams, decimal? FatGrams,
    string? Notes, DateTimeOffset CreatedAt)
{
    public List<NutritionMealVm> Meals { get; set; } = [];
}

public sealed record NutritionMealVm(string Name, string? Description);

public sealed class NutritionAiReviewForm
{
    [Required] public Guid PlanId { get; set; }
    [Required, RegularExpression("approve|reject")] public string Decision { get; set; } = "";
}
