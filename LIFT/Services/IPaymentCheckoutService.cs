using LIFT.Models;

namespace LIFT.Services;

public interface IPaymentCheckoutService
{
    bool IsConfigured { get; }
    Task<CheckoutStartResult> StartAsync(Guid clientId, StartCheckoutForm form, Uri siteBase, CancellationToken ct);
    Task<CheckoutCaptureResult> CompleteAsync(Guid clientId, string orderId, CancellationToken ct);
    Task CancelAsync(Guid clientId, string orderId, CancellationToken ct);
}
