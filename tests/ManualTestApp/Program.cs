using Grpc.Net.Client;
using ProtoIdentity = BurgerIAM.Protos.Identity;
using ProtoMenu = BurgerIAM.Protos.Menu;
using ProtoOrder = BurgerIAM.Protos.Order;
using ProtoPayment = BurgerIAM.Protos.Payment;
using ProtoKitchen = BurgerIAM.Protos.Kitchen;
using ProtoDelivery = BurgerIAM.Protos.Delivery;
using ProtoCommon = BurgerIAM.Protos.Common;

var identityUrl = args.Length > 0 ? args[0] : "http://localhost:5041";
var menuUrl = args.Length > 1 ? args[1] : "http://localhost:5052";
var orderUrl = args.Length > 2 ? args[2] : "http://localhost:5063";
var paymentUrl = args.Length > 3 ? args[3] : "http://localhost:5074";
var kitchenUrl = args.Length > 4 ? args[4] : "http://localhost:5085";
var deliveryUrl = args.Length > 5 ? args[5] : "http://localhost:5096";

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("═══════════════════════════════════════════");
Console.WriteLine("  BurgerIAM - Manual Integration Test App");
Console.WriteLine($"  Identity: {identityUrl}");
Console.WriteLine($"  Menu    : {menuUrl}");
Console.WriteLine($"  Order   : {orderUrl}");
Console.WriteLine($"  Payment : {paymentUrl}");
Console.WriteLine($"  Kitchen : {kitchenUrl}");
Console.WriteLine($"  Delivery: {deliveryUrl}");
Console.WriteLine("═══════════════════════════════════════════");
Console.ResetColor();
Console.WriteLine();

var passed = 0;
var failed = 0;

var identityChannel = GrpcChannel.ForAddress(identityUrl);
var identityClient = new ProtoIdentity.IdentityService.IdentityServiceClient(identityChannel);

var menuChannel = GrpcChannel.ForAddress(menuUrl);
var menuClient = new ProtoMenu.MenuService.MenuServiceClient(menuChannel);

var orderChannel = GrpcChannel.ForAddress(orderUrl);
var orderClient = new ProtoOrder.OrderService.OrderServiceClient(orderChannel);

var paymentChannel = GrpcChannel.ForAddress(paymentUrl);
var paymentClient = new ProtoPayment.PaymentService.PaymentServiceClient(paymentChannel);

var kitchenChannel = GrpcChannel.ForAddress(kitchenUrl);
var kitchenClient = new ProtoKitchen.KitchenService.KitchenServiceClient(kitchenChannel);

var deliveryChannel = GrpcChannel.ForAddress(deliveryUrl);
var deliveryClient = new ProtoDelivery.DeliveryService.DeliveryServiceClient(deliveryChannel);

var testId = Guid.NewGuid().ToString("N")[..8];
var testEmail = $"manual-{testId}@test.com";
var testPassword = "password123";

Console.WriteLine($"  Test user: {testEmail}");
Console.WriteLine();

Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("─── Identity Service ───");
Console.ResetColor();

await RunTest("Register - new user", async () =>
{
    var response = await identityClient.RegisterAsync(new ProtoIdentity.RegisterRequest
    {
        Email = testEmail,
        Password = testPassword,
        Name = "Manual Tester"
    });
    if (string.IsNullOrWhiteSpace(response.UserId))
        throw new Exception($"Expected UserId, got empty. Error: {response.Error}");
    Console.WriteLine($"  UserId: {response.UserId}");
});

await RunTest("Register - duplicate email", async () =>
{
    var response = await identityClient.RegisterAsync(new ProtoIdentity.RegisterRequest
    {
        Email = testEmail,
        Password = testPassword,
        Name = "Manual Tester"
    });
    if (string.IsNullOrWhiteSpace(response.Error))
        throw new Exception("Expected error for duplicate email");
    Console.WriteLine($"  Error: {response.Error}");
});

string? token = null;
string? userId = null;
await RunTest("Login - valid credentials", async () =>
{
    var response = await identityClient.LoginAsync(new ProtoIdentity.LoginRequest
    {
        Email = testEmail,
        Password = testPassword
    });
    if (string.IsNullOrWhiteSpace(response.Token))
        throw new Exception($"Expected token. Error: {response.Error}");
    token = response.Token;
    userId = response.UserId;
    Console.WriteLine($"  Token: {response.Token[..Math.Min(50, response.Token.Length)]}...");
    Console.WriteLine($"  Role : {response.Role}");
});

await RunTest("Login - invalid password", async () =>
{
    var response = await identityClient.LoginAsync(new ProtoIdentity.LoginRequest
    {
        Email = testEmail,
        Password = "wrongpassword"
    });
    if (string.IsNullOrWhiteSpace(response.Error))
        throw new Exception("Expected error for invalid password");
    Console.WriteLine($"  Error: {response.Error}");
});

await RunTest("ValidateToken - valid token", async () =>
{
    if (token is null) throw new Exception("No token to validate");
    var response = await identityClient.ValidateTokenAsync(new ProtoIdentity.ValidateTokenRequest
    {
        Token = token
    });
    if (!response.IsValid)
        throw new Exception("Expected valid token");
    Console.WriteLine($"  UserId: {response.UserId}");
    Console.WriteLine($"  Role  : {response.Role}");
});

await RunTest("ValidateToken - invalid token", async () =>
{
    var response = await identityClient.ValidateTokenAsync(new ProtoIdentity.ValidateTokenRequest
    {
        Token = "bogus-token-here"
    });
    if (response.IsValid)
        throw new Exception("Expected invalid token");
    Console.WriteLine("  Token correctly rejected");
});

Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine();
Console.WriteLine("─── Menu Service ───");
Console.ResetColor();

string? firstItemId = null;
await RunTest("GetMenuItems - returns items", async () =>
{
    var response = await menuClient.GetMenuItemsAsync(new ProtoCommon.Empty());
    if (response.Items.Count == 0)
        throw new Exception("Expected menu items, got 0");
    firstItemId = response.Items[0].Id;
    Console.WriteLine($"  Items count: {response.Items.Count}");
    Console.WriteLine($"  First item : {response.Items[0].Name} ({response.Items[0].Id})");
});

await RunTest("GetMenuItem - existing item", async () =>
{
    if (firstItemId is null) throw new Exception("No item ID available");

    var item = await menuClient.GetMenuItemAsync(new ProtoMenu.GetMenuItemRequest { Id = firstItemId });

    if (string.IsNullOrWhiteSpace(item.Name))
        throw new Exception("Expected item data");
    Console.WriteLine($"  Name    : {item.Name}");
    Console.WriteLine($"  Price   : {item.Price}");
    Console.WriteLine($"  Cat     : {item.Category}");
});

await RunTest("GetMenuItem - not found", async () =>
{
    try
    {
        await menuClient.GetMenuItemAsync(new ProtoMenu.GetMenuItemRequest { Id = "nonexistent" });
        throw new Exception("Expected RpcException");
    }
    catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
    {
        Console.WriteLine($"  Expected error: {ex.Status.Detail}");
    }
});

Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine();
Console.WriteLine("─── Order Service ───");
Console.ResetColor();

string? orderId = null;
await RunTest("CreateOrder - new order", async () =>
{
    if (userId is null) throw new Exception("No user ID available");

    var items = new List<ProtoOrder.OrderItem>
    {
        new()
        {
            MenuItemId = "item-1",
            ItemName = "Cheeseburger",
            Quantity = 2,
            UnitPrice = 5.99
        }
    };

    var response = await orderClient.CreateOrderAsync(new ProtoOrder.CreateOrderRequest
    {
        CustomerId = userId,
        CustomerEmail = testEmail,
        DeliveryAddress = "123 Main St",
        Items = { items }
    });

    if (string.IsNullOrWhiteSpace(response.Id))
        throw new Exception("Expected order ID");
    orderId = response.Id;
    Console.WriteLine($"  OrderId: {response.Id}");
    Console.WriteLine($"  Status : {response.Status}");
    Console.WriteLine($"  Total  : {response.TotalAmount:C}");
});

await RunTest("GetOrder - existing order", async () =>
{
    if (orderId is null) throw new Exception("No order ID available");

    var response = await orderClient.GetOrderAsync(new ProtoOrder.GetOrderRequest
    {
        Id = orderId
    });

    if (string.IsNullOrWhiteSpace(response.Id))
        throw new Exception("Expected order data");
    Console.WriteLine($"  OrderId: {response.Id}");
    Console.WriteLine($"  Status : {response.Status}");
    Console.WriteLine($"  Items  : {response.Items.Count}");
});

await RunTest("GetOrderStatus - existing order", async () =>
{
    if (orderId is null) throw new Exception("No order ID available");

    var response = await orderClient.GetOrderStatusAsync(new ProtoOrder.GetOrderRequest
    {
        Id = orderId
    });

    Console.WriteLine($"  Status: {response.Status}");
});

await RunTest("GetCustomerOrders - returns orders", async () =>
{
    if (userId is null) throw new Exception("No user ID available");

    var response = await orderClient.GetCustomerOrdersAsync(new ProtoOrder.GetCustomerOrdersRequest
    {
        CustomerId = userId
    });

    if (response.Orders.Count == 0)
        throw new Exception("Expected at least one order");
    var first = response.Orders[0];
    Console.WriteLine($"  Orders : {response.Orders.Count}");
    Console.WriteLine($"  First  : {first.Id} (status {first.Status})");
});

await RunTest("GetOrder - not found", async () =>
{
    try
    {
        await orderClient.GetOrderAsync(new ProtoOrder.GetOrderRequest { Id = "nonexistent" });
        throw new Exception("Expected RpcException");
    }
    catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
    {
        Console.WriteLine($"  Expected error: {ex.Status.Detail}");
    }
});

Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine();
Console.WriteLine("─── Payment Service ───");
Console.ResetColor();

string? paymentId = null;
await RunTest("ProcessPayment - valid payment", async () =>
{
    if (orderId is null) throw new Exception("No order ID available");

    var response = await paymentClient.ProcessPaymentAsync(new ProtoPayment.ProcessPaymentRequest
    {
        OrderId = orderId,
        CustomerId = userId ?? "",
        Amount = 11.98,
        Method = "CreditCard"
    });

    if (string.IsNullOrWhiteSpace(response.PaymentId) && !string.IsNullOrWhiteSpace(response.Error))
        throw new Exception($"Payment failed: {response.Error}");
    paymentId = response.PaymentId;
    Console.WriteLine($"  PaymentId: {paymentId}");
    Console.WriteLine($"  Status   : {response.Status}");
});

await RunTest("GetPayment - existing payment", async () =>
{
    if (paymentId is null) throw new Exception("No payment ID available — ProcessPayment may have failed");

    var response = await paymentClient.GetPaymentAsync(new ProtoPayment.GetPaymentRequest
    {
        PaymentId = paymentId
    });

    Console.WriteLine($"  PaymentId: {response.Id}");
    Console.WriteLine($"  Status   : {response.Status}");
});

await RunTest("CancelOrder - existing order", async () =>
{
    if (orderId is null) throw new Exception("No order ID available");

    var response = await orderClient.CancelOrderAsync(new ProtoOrder.CancelOrderRequest
    {
        Id = orderId,
        Reason = "Manual test cancellation"
    });

    Console.WriteLine($"  Status: {response.Status}");
});

Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine();
Console.WriteLine("─── Kitchen Service ───");
Console.ResetColor();

await RunTest("GetPendingOrders - empty or has orders", async () =>
{
    var response = await kitchenClient.GetPendingOrdersAsync(new ProtoCommon.Empty());
    Console.WriteLine($"  Pending orders: {response.Orders.Count}");
});

await RunTest("GetKitchenOrder - existing order", async () =>
{
    if (orderId is null) throw new Exception("No order ID available");
    try
    {
        var response = await kitchenClient.GetKitchenOrderAsync(new ProtoKitchen.GetKitchenOrderRequest { OrderId = orderId });
        Console.WriteLine($"  Status: {response.Status}");
    }
    catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
    {
        Console.WriteLine($"  Kitchen order not yet created (OK): {ex.Status.Detail}");
    }
});

Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine();
Console.WriteLine("─── Delivery Service ───");
Console.ResetColor();

string? driverId = null;
await RunTest("AssignDelivery - assigns driver", async () =>
{
    if (orderId is null) throw new Exception("No order ID available");
    try
    {
        var response = await deliveryClient.AssignDeliveryAsync(new ProtoDelivery.AssignDeliveryRequest
        {
            OrderId = orderId,
            DeliveryAddress = "123 Main St"
        });
        driverId = response.DriverId;
        Console.WriteLine($"  DeliveryId: {response.Id}");
        Console.WriteLine($"  Driver    : {response.DriverName}");
    }
    catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.ResourceExhausted)
    {
        Console.WriteLine($"  No drivers available (OK): {ex.Status.Detail}");
    }
});

await RunTest("GetDeliveryStatus - check status", async () =>
{
    if (orderId is null) throw new Exception("No order ID available");
    try
    {
        var response = await deliveryClient.GetDeliveryStatusAsync(new ProtoDelivery.GetDeliveryRequest { OrderId = orderId });
        Console.WriteLine($"  Status: {response.Status}");
        Console.WriteLine($"  Driver: {response.DriverName}");
    }
    catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
    {
        Console.WriteLine($"  Delivery not yet created (OK): {ex.Status.Detail}");
    }
});

Console.WriteLine();
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"────────────────────────────────────────");
Console.WriteLine($"  Results: {passed} passed, {failed} failed");
Console.WriteLine($"────────────────────────────────────────");
Console.ResetColor();

if (failed > 0)
    Environment.Exit(1);

async Task RunTest(string name, Func<Task> test)
{
    Console.Write($"  [{name.PadRight(42)}] ");
    try
    {
        await test();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  PASS");
        Console.ResetColor();
        passed++;
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("  FAIL");
        Console.ResetColor();
        Console.WriteLine($"    {ex.GetType().Name}: {ex.Message}");
        failed++;
    }
}
