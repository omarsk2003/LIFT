using System.ComponentModel.DataAnnotations;

namespace LIFT.Models;

public sealed record AdminChangeVm(Guid Id, Guid ClientId, string ClientName, Guid CurrentSpecialistId, string SpecialistRole,
    string CurrentSpecialistName, string Reason, DateTimeOffset RequestedAt);
public sealed record AdminReplacementVm(Guid Id, string Name, string Role, string Specialty, int AvailableSlots);
public sealed record AdminChangesVm(IReadOnlyList<AdminChangeVm> Requests,
    IReadOnlyList<AdminReplacementVm> Replacements);

public sealed class AdminChangeDecisionForm
{
    [Required] public Guid RequestId { get; set; }
    [Required, RegularExpression("complete|decline")] public string Decision { get; set; } = "";
    public Guid? ReplacementSpecialistId { get; set; }
    [StringLength(1000)] public string? AdminNote { get; set; }
}
