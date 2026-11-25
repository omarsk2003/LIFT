using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LIFT.Models;

namespace LIFT.Services;

public sealed class PayPalGateway(HttpClient http, IConfiguration config) : IPayPalGateway
{
    private string? ClientId => config["PayPal:ClientId"];
    private string? ClientSecret => config["PayPal:ClientSecret"];
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

    private async Task<string> TokenAsync(CancellationToken ct)
    {
        if (!IsConfigured) throw new InvalidOperationException("PayPal credentials are not configured.");
        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/oauth2/token");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{ClientId}:{ClientSecret}")));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" });
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return json.RootElement.GetProperty("access_token").GetString() ?? throw new InvalidDataException("PayPal token missing.");
    }

    private async Task<JsonDocument> SendAsync(HttpMethod method, string path, object? body,
        string? requestId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync(ct));
        if (requestId is not null) request.Headers.TryAddWithoutValidation("PayPal-Request-Id", requestId);
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(ct);
        if (content.Length > 100_000) throw new InvalidDataException("PayPal response too large.");
        return JsonDocument.Parse(content);
    }

    public async Task<PayPalOrder> CreateOrderAsync(Guid paymentId, decimal amount, string currency,
        Uri returnUrl, Uri cancelUrl, CancellationToken ct)
    {
        var body = new
        {
            intent = "CAPTURE",
            purchase_units = new[] { new { reference_id = paymentId.ToString("N"),
                amount = new { currency_code = currency, value = amount.ToString("0.00", CultureInfo.InvariantCulture) } } },
            payment_source = new { paypal = new { experience_context = new {
                return_url = returnUrl.ToString(), cancel_url = cancelUrl.ToString(),
                shipping_preference = "NO_SHIPPING", user_action = "PAY_NOW" } } }
        };
        using var json = await SendAsync(HttpMethod.Post, "v2/checkout/orders", body,
            $"lift-create-{paymentId:N}", ct);
        var root = json.RootElement;
        var id = root.GetProperty("id").GetString();
        var url = root.GetProperty("links").EnumerateArray()
            .Where(x => x.GetProperty("rel").GetString() is "payer-action" or "approve")
            .Select(x => x.GetProperty("href").GetString()).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var parsed) || parsed.Scheme != Uri.UriSchemeHttps ||
            !(parsed.Host.Equals("paypal.com", StringComparison.OrdinalIgnoreCase) ||
              parsed.Host.EndsWith(".paypal.com", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("PayPal order or approval URL missing.");
        return new PayPalOrder(id, url);
    }

    public async Task<PayPalCapture> CaptureOrderAsync(string orderId, Guid paymentId, CancellationToken ct)
    {
        if (orderId.Length is < 1 or > 200 || !orderId.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
            throw new ArgumentException("Invalid order ID.", nameof(orderId));
        JsonDocument json;
        try
        {
            json = await SendAsync(HttpMethod.Post, $"v2/checkout/orders/{orderId}/capture", new { },
                $"lift-capture-{paymentId:N}", ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            json = await SendAsync(HttpMethod.Get, $"v2/checkout/orders/{orderId}", null, null, ct);
        }
        using (json)
        {
            var root = json.RootElement;
            if (root.GetProperty("id").GetString() != orderId) throw new InvalidDataException("PayPal order mismatch.");
            var capture = root.GetProperty("purchase_units")[0].GetProperty("payments")
                .GetProperty("captures")[0];
            var amount = capture.GetProperty("amount");
            return new PayPalCapture(orderId, capture.GetProperty("id").GetString() ?? "",
                capture.GetProperty("status").GetString() ?? "",
                decimal.Parse(amount.GetProperty("value").GetString() ?? "", CultureInfo.InvariantCulture),
                amount.GetProperty("currency_code").GetString() ?? "");
        }
    }
}
