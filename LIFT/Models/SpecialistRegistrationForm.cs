using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace LIFT.Models;

public sealed class SpecialistRegistrationForm
{
    [Required, StringLength(120, MinimumLength = 2)] public string FullName { get; set; } = "";
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = "";
    [Required, MinLength(8)] public string Password { get; set; } = "";
    [Required, Compare(nameof(Password))] public string ConfirmPassword { get; set; } = "";
    [StringLength(30)] public string? Phone { get; set; }
    [Required, StringLength(120)] public string Specialty { get; set; } = "";
    [Required, StringLength(1000, MinimumLength = 10)] public string Bio { get; set; } = "";
    [Range(0, 60)] public byte ExperienceYears { get; set; }
    [Range(1, 8)] public short Capacity { get; set; } = 8;
    public List<IFormFile> Certificates { get; set; } = [];
}
