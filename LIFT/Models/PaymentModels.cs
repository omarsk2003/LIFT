using System.ComponentModel.DataAnnotations;

namespace LIFT.Models;

public sealed class StartCheckoutForm
{
    [Range(1, int.MaxValue)] public int PlanId { get; set; }
    public Guid? CoachId { get; set; }
    public Guid? NutritionistId { get; set; }
    [StringLength(50)] public string? PromoCode { get; set; }
}

public sealed record CheckoutStartResult(bool Success, string? Error, string? ApprovalUrl);
public sealed record CheckoutCaptureResult(bool Success, string Message);
public sealed record PayPalOrder(string Id, string ApprovalUrl);
public sealed record PayPalCapture(string OrderId, string CaptureId, string Status, decimal Amount, string Currency);
