namespace LIFT.Models;

public sealed class LiftPayment
{
    public Guid Id { get; set; }
    public Guid SubscriptionId { get; set; }
    public string Provider { get; set; } = "paypal";
    public string? ProviderPaymentId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = "pending";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class LiftPaymentEvent
{
    public Guid Id { get; set; }
    public Guid? PaymentId { get; set; }
    public string Provider { get; set; } = "paypal";
    public string ProviderEventId { get; set; } = "";
    public string EventType { get; set; } = "";
    public string ProcessingStatus { get; set; } = "processed";
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? ErrorCode { get; set; }
}

public sealed class SubscriptionSpecialistSelection
{
    public Guid SubscriptionId { get; set; }
    public string SpecialistRole { get; set; } = "";
    public Guid SpecialistId { get; set; }
    public DateTimeOffset SelectedAt { get; set; }
}
