using LIFT.Models;

namespace LIFT.Services;

public interface IPayPalGateway
{
    bool IsConfigured { get; }
    Task<PayPalOrder> CreateOrderAsync(Guid paymentId, decimal amount, string currency,
        Uri returnUrl, Uri cancelUrl, CancellationToken ct);
    Task<PayPalCapture> CaptureOrderAsync(string orderId, Guid paymentId, CancellationToken ct);
}
