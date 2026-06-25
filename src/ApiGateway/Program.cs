using System.Text;
using System.Text.Json;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.IdentityModel.Tokens;
using ProtoIdentity = BurgerIAM.Protos.Identity;
using ProtoMenu = BurgerIAM.Protos.Menu;
using ProtoOrder = BurgerIAM.Protos.Order;
using ProtoPayment = BurgerIAM.Protos.Payment;
using ProtoKitchen = BurgerIAM.Protos.Kitchen;
using ProtoDelivery = BurgerIAM.Protos.Delivery;
using ProtoFeedback = BurgerIAM.Protos.Feedback;
using ProtoNotification = BurgerIAM.Protos.Notification;
using ProtoCommon = BurgerIAM.Protos.Common;

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

var jwtKey = builder.Configuration["Jwt:Key"] ?? "BurgerIAM-SuperSecret-Key-Min32Chars!";
var servicesConfig = builder.Configuration.GetSection("Services");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

string GetServiceUrl(string name) => servicesConfig[name] ?? $"http://localhost:5{name}";

builder.Services.AddSingleton(_ =>
{
    var channel = GrpcChannel.ForAddress(GetServiceUrl("Identity"));
    return new ProtoIdentity.IdentityService.IdentityServiceClient(channel);
});

builder.Services.AddSingleton(_ =>
{
    var channel = GrpcChannel.ForAddress(GetServiceUrl("Menu"));
    return new ProtoMenu.MenuService.MenuServiceClient(channel);
});

builder.Services.AddSingleton(_ =>
{
    var channel = GrpcChannel.ForAddress(GetServiceUrl("Order"));
    return new ProtoOrder.OrderService.OrderServiceClient(channel);
});

builder.Services.AddSingleton(_ =>
{
    var channel = GrpcChannel.ForAddress(GetServiceUrl("Payment"));
    return new ProtoPayment.PaymentService.PaymentServiceClient(channel);
});

builder.Services.AddSingleton(_ =>
{
    var channel = GrpcChannel.ForAddress(GetServiceUrl("Kitchen"));
    return new ProtoKitchen.KitchenService.KitchenServiceClient(channel);
});

builder.Services.AddSingleton(_ =>
{
    var channel = GrpcChannel.ForAddress(GetServiceUrl("Delivery"));
    return new ProtoDelivery.DeliveryService.DeliveryServiceClient(channel);
});

builder.Services.AddSingleton(_ =>
{
    var channel = GrpcChannel.ForAddress(GetServiceUrl("Feedback"));
    return new ProtoFeedback.FeedbackService.FeedbackServiceClient(channel);
});

builder.Services.AddSingleton(_ =>
{
    var channel = GrpcChannel.ForAddress(GetServiceUrl("Notification"));
    return new ProtoNotification.NotificationService.NotificationServiceClient(channel);
});

builder.Services.AddHttpClient("Receipt", client =>
{
    client.BaseAddress = new Uri(GetServiceUrl("Receipt"));
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var wasmFrontendPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
if (Directory.Exists(wasmFrontendPath))
{
    builder.Services.AddSpaStaticFiles(config => config.RootPath = "wwwroot");
}

builder.Services.Configure<StaticFileOptions>(options =>
{
    var provider = new FileExtensionContentTypeProvider();
    provider.Mappings[".wasm"] = "application/wasm";
    provider.Mappings[".br"] = "application/brotli";
    provider.Mappings[".dat"] = "application/octet-stream";
    provider.Mappings[".blat"] = "application/octet-stream";
    provider.Mappings[".pdb"] = "application/octet-stream";
    options.ContentTypeProvider = provider;
});

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

if (Directory.Exists(wasmFrontendPath))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.UseSpaStaticFiles();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "ApiGateway" }));

app.MapPost("/api/auth/login", async (ProtoIdentity.LoginRequest request, ProtoIdentity.IdentityService.IdentityServiceClient client) =>
{
    var response = await client.LoginAsync(request);
    if (!string.IsNullOrEmpty(response.Error))
        return Results.BadRequest(new { error = response.Error });
    return Results.Ok(new { token = response.Token, userId = response.UserId, email = response.Email, name = response.Name, role = response.Role });
});

app.MapPost("/api/auth/register", async (ProtoIdentity.RegisterRequest request, ProtoIdentity.IdentityService.IdentityServiceClient client) =>
{
    var response = await client.RegisterAsync(request);
    if (!string.IsNullOrEmpty(response.Error))
        return Results.BadRequest(new { error = response.Error });
    return Results.Ok(new { userId = response.UserId });
});

app.MapGet("/api/menu", async (ProtoMenu.MenuService.MenuServiceClient client) =>
{
    var response = await client.GetMenuItemsAsync(new ProtoCommon.Empty());
    return Results.Ok(response.Items);
});

app.MapGet("/api/menu/{id}", async (string id, ProtoMenu.MenuService.MenuServiceClient client) =>
{
    try
    {
        var item = await client.GetMenuItemAsync(new ProtoMenu.GetMenuItemRequest { Id = id });
        return Results.Ok(item);
    }
    catch (Exception)
    {
        return Results.NotFound(new { error = $"Menu item {id} not found" });
    }
});

app.MapPost("/api/orders", async (ProtoOrder.CreateOrderRequest request,
    ProtoOrder.OrderService.OrderServiceClient orderClient,
    ProtoPayment.PaymentService.PaymentServiceClient paymentClient,
    ProtoKitchen.KitchenService.KitchenServiceClient kitchenClient,
    IHttpClientFactory httpFactory) =>
{
    try
    {
        var order = await orderClient.CreateOrderAsync(request);

        var paymentRequest = new ProtoPayment.ProcessPaymentRequest
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            Amount = order.TotalAmount,
            Method = "CreditCard"
        };
        await paymentClient.ProcessPaymentAsync(paymentRequest);

        var orderHttp = httpFactory.CreateClient();
        await orderHttp.PostAsync($"{GetServiceUrl("Order")}/api/internal/orders/{order.Id}/confirm-payment", null);

        await kitchenClient.SeedKitchenOrderAsync(new ProtoKitchen.SeedKitchenOrderRequest { OrderId = order.Id });

        await orderHttp.PostAsync($"{GetServiceUrl("Receipt")}/receipts?orderId={order.Id}&customerId={order.CustomerId}&amount={order.TotalAmount}", null);

        return Results.Created($"/api/orders/{order.Id}", order);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = $"Failed to create order: {ex.Message}" });
    }
}).RequireAuthorization();

app.MapGet("/api/orders/{id}", async (string id, ProtoOrder.OrderService.OrderServiceClient client) =>
{
    try
    {
        var order = await client.GetOrderAsync(new ProtoOrder.GetOrderRequest { Id = id });
        return Results.Ok(order);
    }
    catch (Exception)
    {
        return Results.NotFound(new { error = $"Order {id} not found" });
    }
}).RequireAuthorization();

app.MapGet("/api/orders/{id}/status", async (string id, ProtoOrder.OrderService.OrderServiceClient client) =>
{
    try
    {
        var status = await client.GetOrderStatusAsync(new ProtoOrder.GetOrderRequest { Id = id });
        return Results.Ok(status);
    }
    catch (Exception)
    {
        return Results.NotFound(new { error = $"Order {id} not found" });
    }
}).RequireAuthorization();

app.MapPost("/api/orders/{id}/cancel", async (string id, ProtoOrder.CancelOrderRequest request, ProtoOrder.OrderService.OrderServiceClient client) =>
{
    request.Id = id;
    try
    {
        var order = await client.CancelOrderAsync(request);
        return Results.Ok(order);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireAuthorization();

app.MapGet("/api/orders/my/{customerId}", async (string customerId, ProtoOrder.OrderService.OrderServiceClient client) =>
{
    var response = await client.GetCustomerOrdersAsync(new ProtoOrder.GetCustomerOrdersRequest { CustomerId = customerId });
    return Results.Ok(response.Orders);
}).RequireAuthorization();

app.MapPost("/api/payments", async (ProtoPayment.ProcessPaymentRequest request, ProtoPayment.PaymentService.PaymentServiceClient client) =>
{
    var response = await client.ProcessPaymentAsync(request);
    if (!string.IsNullOrEmpty(response.Error))
        return Results.BadRequest(new { error = response.Error });
    return Results.Created($"/api/payments/{response.PaymentId}", response);
}).RequireAuthorization();

app.MapGet("/api/payments/{id}", async (string id, ProtoPayment.PaymentService.PaymentServiceClient client) =>
{
    try
    {
        var payment = await client.GetPaymentAsync(new ProtoPayment.GetPaymentRequest { PaymentId = id });
        return Results.Ok(payment);
    }
    catch (Exception)
    {
        return Results.NotFound(new { error = $"Payment {id} not found" });
    }
}).RequireAuthorization();

app.MapPost("/api/payments/{id}/refund", async (string id, ProtoPayment.RefundPaymentRequest request, ProtoPayment.PaymentService.PaymentServiceClient client) =>
{
    request.PaymentId = id;
    var response = await client.RefundPaymentAsync(request);
    if (!string.IsNullOrEmpty(response.Error))
        return Results.BadRequest(new { error = response.Error });
    return Results.Ok(response);
}).RequireAuthorization();

app.MapGet("/api/kitchen/pending", async (ProtoKitchen.KitchenService.KitchenServiceClient client) =>
{
    var response = await client.GetPendingOrdersAsync(new ProtoCommon.Empty());
    return Results.Ok(response.Orders);
}).RequireAuthorization();

app.MapPost("/api/kitchen/{orderId}/prepare", async (string orderId, ProtoKitchen.StartPreparingRequest request, ProtoKitchen.KitchenService.KitchenServiceClient client) =>
{
    request.OrderId = orderId;
    try
    {
        var result = await client.StartPreparingAsync(request);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireAuthorization();

app.MapPost("/api/kitchen/{orderId}/ready", async (string orderId, ProtoKitchen.KitchenService.KitchenServiceClient client) =>
{
    try
    {
        var result = await client.MarkAsReadyAsync(new ProtoKitchen.MarkAsReadyRequest { OrderId = orderId });
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireAuthorization();

app.MapGet("/api/delivery/{orderId}", async (string orderId, ProtoDelivery.DeliveryService.DeliveryServiceClient client) =>
{
    try
    {
        var delivery = await client.GetDeliveryStatusAsync(new ProtoDelivery.GetDeliveryRequest { OrderId = orderId });
        return Results.Ok(delivery);
    }
    catch (Exception)
    {
        return Results.NotFound(new { error = $"Delivery for order {orderId} not found" });
    }
}).RequireAuthorization();

app.MapGet("/api/delivery/driver/{driverId}", async (string driverId, ProtoDelivery.DeliveryService.DeliveryServiceClient client) =>
{
    var response = await client.GetDriverDeliveriesAsync(new ProtoDelivery.GetDriverDeliveriesRequest { DriverId = driverId });
    return Results.Ok(response.Deliveries);
}).RequireAuthorization();

app.MapPost("/api/feedback", async (ProtoFeedback.SubmitFeedbackRequest request, ProtoFeedback.FeedbackService.FeedbackServiceClient client) =>
{
    var response = await client.SubmitFeedbackAsync(request);
    if (!string.IsNullOrEmpty(response.Error))
        return Results.BadRequest(new { error = response.Error });
    return Results.Created($"/api/feedback/{request.OrderId}", response);
}).RequireAuthorization();

app.MapGet("/api/feedback/{orderId}", async (string orderId, ProtoFeedback.FeedbackService.FeedbackServiceClient client) =>
{
    try
    {
        var feedback = await client.GetOrderFeedbackAsync(new ProtoFeedback.GetOrderFeedbackRequest { OrderId = orderId });
        return Results.Ok(feedback);
    }
    catch (Exception)
    {
        return Results.NotFound(new { error = $"Feedback for order {orderId} not found" });
    }
}).RequireAuthorization();

app.MapGet("/api/feedback/rating/average", async (ProtoFeedback.FeedbackService.FeedbackServiceClient client) =>
{
    var rating = await client.GetAverageRatingAsync(new ProtoFeedback.GetAverageRatingRequest());
    return Results.Ok(rating);
});

app.MapGet("/api/receipts/{orderId}", async (string orderId, IHttpClientFactory httpFactory) =>
{
    var httpClient = httpFactory.CreateClient("Receipt");
    try
    {
        var response = await httpClient.GetAsync($"/receipts/{orderId}");
        if (!response.IsSuccessStatusCode)
            return Results.NotFound(new { error = $"Receipt for order {orderId} not found" });
        var html = await response.Content.ReadAsStringAsync();
        return Results.Content(html, "text/html");
    }
    catch
    {
        return Results.NotFound(new { error = $"Receipt for order {orderId} not found" });
    }
}).RequireAuthorization();

app.MapGet("/api/notifications/{customerId}", async (string customerId, ProtoNotification.NotificationService.NotificationServiceClient client) =>
{
    var response = await client.GetNotificationsAsync(new ProtoNotification.GetNotificationsRequest { CustomerId = customerId });
    return Results.Ok(response.Notifications);
}).RequireAuthorization();

app.MapPost("/api/notifications/{id}/read", async (string id, ProtoNotification.NotificationService.NotificationServiceClient client) =>
{
    await client.MarkAsReadAsync(new ProtoNotification.MarkAsReadRequest { NotificationId = id });
    return Results.Ok();
}).RequireAuthorization();

app.MapGet("/api/notifications/{customerId}/unread-count", async (string customerId, ProtoNotification.NotificationService.NotificationServiceClient client) =>
{
    var response = await client.GetUnreadCountAsync(new ProtoNotification.GetUnreadCountRequest { CustomerId = customerId });
    return Results.Ok(response);
}).RequireAuthorization();

app.MapGet("/api/menu/{id}/availability", async (string id, bool isAvailable, ProtoMenu.MenuService.MenuServiceClient client) =>
{
    try
    {
        var item = await client.UpdateAvailabilityAsync(new ProtoMenu.UpdateAvailabilityRequest { Id = id, IsAvailable = isAvailable });
        return Results.Ok(item);
    }
    catch (Exception)
    {
        return Results.NotFound(new { error = $"Menu item {id} not found" });
    }
}).RequireAuthorization();

if (Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")))
{
    app.MapFallbackToFile("index.html");
}

app.Run();
