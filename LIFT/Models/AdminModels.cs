using System.ComponentModel.DataAnnotations;

namespace LIFT.Models;

public sealed record AdminDashboardVm(
    int ActiveClients, int ActiveCoaches, int ActiveNutritionists,
    int PendingSpecialists, int ActiveSubscriptions, int PendingChanges,
    IReadOnlyList<AdminSpecialistVm> Pending,
    IReadOnlyList<AdminSpecialistVm> Active,
    IReadOnlyList<AdminClientVm> Clients,
    IReadOnlyList<AdminSubscriptionVm> Subscriptions,
    IReadOnlyList<AdminMonthlyGrowthVm> MonthlyGrowth,
    IReadOnlyList<AdminMonthlyRevenueVm> MonthlyRevenue,
    string Search, int SpecialistPage, int SpecialistPages,
    int ClientPage, int ClientPages,
    int SubscriptionPage, int SubscriptionPages);

public sealed record AdminMonthlyGrowthVm(int Year, int Month, int NewUsers, int TotalUsers);
public sealed record AdminMonthlyRevenueVm(int Year, int Month, decimal Total, string Currency, IReadOnlyList<decimal> Daily);

public sealed record AdminSpecialistVm(Guid Id, string Name, string Email, string Role,
    string Status, string Specialty, int Capacity, bool AcceptingNewClients,
    int CertificateCount, DateTimeOffset CreatedAt);

public sealed record AdminClientVm(Guid Id, string Name, string Email, string Status,
    string? Plan, DateTimeOffset CreatedAt)
{
    public ClientProfileForm Profile { get; init; } = new();
}

public sealed class AdminClientEditForm
{
    [Required] public Guid UserId { get; set; }
    [Required, StringLength(120, MinimumLength = 2)] public string FullName { get; set; } = "";
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = "";
    [StringLength(30)] public string? Phone { get; set; }
    public DateOnly? BirthDate { get; set; }
    [Required, RegularExpression("active|suspended|blocked")] public string Status { get; set; } = "active";
}

public sealed record AdminSubscriptionVm(Guid Id, string ClientName, string PlanName,
    string Status, decimal PriceAtStart, string Currency, DateTimeOffset StartsAt);

public sealed class AdminSpecialistDecisionForm
{
    [Required] public Guid SpecialistId { get; set; }
    [Required, RegularExpression("approve|reject")] public string Decision { get; set; } = "";
}

public sealed class AdminAccountStatusForm
{
    [Required] public Guid UserId { get; set; }
    [Required, RegularExpression("active|suspended|blocked")] public string Status { get; set; } = "";
}
