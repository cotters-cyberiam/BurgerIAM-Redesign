using Grpc.Net.Client;
using ProtoIdentity = BurgerIAM.Protos.Identity;
using ProtoMenu = BurgerIAM.Protos.Menu;
using ProtoCommon = BurgerIAM.Protos.Common;

var identityUrl = args.Length > 0 ? args[0] : "http://localhost:5041";
var menuUrl = args.Length > 1 ? args[1] : "http://localhost:5052";

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("═══════════════════════════════════════════");
Console.WriteLine("  BurgerIAM - Manual Integration Test App");
Console.WriteLine($"  Identity: {identityUrl}");
Console.WriteLine($"  Menu    : {menuUrl}");
Console.WriteLine("═══════════════════════════════════════════");
Console.ResetColor();
Console.WriteLine();

var passed = 0;
var failed = 0;

var identityChannel = GrpcChannel.ForAddress(identityUrl);
var identityClient = new ProtoIdentity.IdentityService.IdentityServiceClient(identityChannel);

var menuChannel = GrpcChannel.ForAddress(menuUrl);
var menuClient = new ProtoMenu.MenuService.MenuServiceClient(menuChannel);

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

await RunTest("GetMenuItems - empty list", async () =>
{
    var response = await menuClient.GetMenuItemsAsync(new ProtoCommon.Empty());
    Console.WriteLine($"  Items count: {response.Items.Count}");
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
