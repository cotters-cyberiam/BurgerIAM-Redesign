using System.Net.Http.Json;
using WasmFrontend.Models;

namespace WasmFrontend.Services;

public sealed class ApiService
{
    private readonly HttpClient _http;

    public ApiService(HttpClient http) => _http = http;

    public async Task<List<MenuItemResponse>> GetMenuAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<List<MenuItemResponse>>("/api/menu") ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<OrderResponse?> CreateOrderAsync(CreateOrderRequest request)
    {
        var response = await _http.PostAsJsonAsync("/api/orders", request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(body);
        }
        return await response.Content.ReadFromJsonAsync<OrderResponse>();
    }

    public async Task<OrderResponse?> GetOrderAsync(string orderId)
    {
        try
        {
            return await _http.GetFromJsonAsync<OrderResponse>($"/api/orders/{orderId}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<OrderStatusResponse?> GetOrderStatusAsync(string orderId)
    {
        try
        {
            return await _http.GetFromJsonAsync<OrderStatusResponse>($"/api/orders/{orderId}/status");
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<OrderResponse>> GetMyOrdersAsync(string customerId)
    {
        try
        {
            return await _http.GetFromJsonAsync<List<OrderResponse>>($"/api/orders/my/{customerId}") ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<PaymentResponse?> ProcessPaymentAsync(string orderId, string customerId, double amount)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("/api/payments",
                new ProcessPaymentRequest(orderId, customerId, amount, "Card"));
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<PaymentResponse>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<DeliveryResponse?> GetDeliveryAsync(string orderId)
    {
        try
        {
            return await _http.GetFromJsonAsync<DeliveryResponse>($"/api/delivery/{orderId}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> SubmitFeedbackAsync(string orderId, string customerId, int rating, string comment)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("/api/feedback",
                new SubmitFeedbackRequest(orderId, customerId, rating, comment));
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
                return error?.GetValueOrDefault("error", "Submission failed");
            }
            return null;
        }
        catch
        {
            return "Submission failed";
        }
    }

    public async Task<FeedbackDetail?> GetFeedbackAsync(string orderId)
    {
        try
        {
            return await _http.GetFromJsonAsync<FeedbackDetail>($"/api/feedback/{orderId}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<AverageRatingResponse?> GetAverageRatingAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<AverageRatingResponse>("/api/feedback/rating/average");
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<FeedbackDetail>> GetAllFeedbackAsync(int limit = 50)
    {
        try
        {
            return await _http.GetFromJsonAsync<List<FeedbackDetail>>($"/api/feedback/all?limit={limit}") ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<ReceiptDetail?> GetReceiptAsync(string orderId)
    {
        try
        {
            return await _http.GetFromJsonAsync<ReceiptDetail>($"/api/receipts/{orderId}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<NotificationResponse>> GetNotificationsAsync(string customerId)
    {
        try
        {
            return await _http.GetFromJsonAsync<List<NotificationResponse>>($"/api/notifications/{customerId}") ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<UnreadCountResponse?> GetUnreadCountAsync(string customerId)
    {
        try
        {
            return await _http.GetFromJsonAsync<UnreadCountResponse>($"/api/notifications/{customerId}/unread-count");
        }
        catch
        {
            return null;
        }
    }
}
