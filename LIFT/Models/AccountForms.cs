using System.ComponentModel.DataAnnotations;

namespace LIFT.Models;

public sealed class RegisterForm
{
    [Required, StringLength(120, MinimumLength = 2)] public string FullName { get; set; } = "";
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = "";
    [Required, MinLength(8)] public string Password { get; set; } = "";
    [Required, Compare(nameof(Password))] public string ConfirmPassword { get; set; } = "";
    [Range(typeof(bool), "true", "true")] public bool Terms { get; set; }
}

public sealed class LoginForm
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required] public string Password { get; set; } = "";
}
