using System.ComponentModel.DataAnnotations;

namespace LIFT.Models;

public sealed record AdminAccountVm(Guid Id, string FullName, string Email, string Status, DateTimeOffset CreatedAt);
public sealed record AdminAccountsVm(IReadOnlyList<AdminAccountVm> Accounts, Guid CurrentUserId);
public sealed record AdminProfileVm(string FullName, string Email, string? PhotoUrl);

public sealed class CreateAdminForm
{
    [Required, StringLength(120, MinimumLength = 2)] public string FullName { get; set; } = "";
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = "";
    [Required, MinLength(12)] public string Password { get; set; } = "";
}

public sealed class AdminStatusForm
{
    [Required] public Guid UserId { get; set; }
    [Required, RegularExpression("active|suspended|blocked")] public string Status { get; set; } = "";
}

public sealed class AdminPasswordForm
{
    [Required] public string CurrentPassword { get; set; } = "";
    [Required, MinLength(12)] public string NewPassword { get; set; } = "";
    [Required, Compare(nameof(NewPassword))] public string ConfirmPassword { get; set; } = "";
}
