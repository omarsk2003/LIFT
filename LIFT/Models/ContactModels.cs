using System.ComponentModel.DataAnnotations;

namespace LIFT.Models;

public sealed class ContactForm
{
    [Required, StringLength(100, MinimumLength = 2)] public string Name { get; set; } = "";
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = "";
    [StringLength(180)] public string? Address { get; set; }
    [StringLength(40)] public string? Phone { get; set; }
    [Required, StringLength(2000, MinimumLength = 5)] public string Message { get; set; } = "";
}

public sealed class ContactMessage
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string Body { get; set; } = "";
    public string Status { get; set; } = "new";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
}
