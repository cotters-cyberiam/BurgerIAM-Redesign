# Debug Log

This file documents issues encountered during development and their resolutions. Refer here when troubleshooting similar problems.

---

## 2026-06-25 — Checkout total still shows £0.00 after DTO fix

**Problem**: The checkout success page displayed `£0.00` even though:
- The order was created successfully (valid GUID shown)
- Items had correct prices in the cart summary
- The original DTO fix (2026-06-24) already ensured items were sent to the API

**Root cause**: Two issues compounded:
1. The success page read `Cart.TotalAmount` in the Razor template, which was evaluated *after* `Cart.Clear()` was called in `PlaceOrder()`, so it always returned 0.
2. `order.TotalAmount` from the API response was unreliable due to protobuf JSON serialization details.

**Fix**:
- Captured the total from the items list **before** the API call: `capturedTotal = items.Sum(i => i.Quantity * i.UnitPrice)`.
- Used three-way fallback: `capturedTotal > 0 ? capturedTotal : order.TotalAmount > 0 ? order.TotalAmount : Cart.TotalAmount`.
- Saved the value before `Cart.Clear()`.

**Files**: `src/WasmFrontend/Pages/Checkout.razor`

---

## 2026-06-29 — Menu and order tracking pages don't display (content invisible)

**Problem**: The Menu page and Order Tracking page appeared blank — content was rendered in the DOM but remained invisible. All CSS styles and HTML structure were correct.

**Root cause**: Blazor's `IJSRuntime.InvokeVoidAsync("window.BurgerIAM.xxx")` calls JS methods without object context, so `this` resolves to `window`, not `window.BurgerIAM`. All methods using `this` (e.g., `this.observer`, `this.initScrollAnimations()`) failed silently. Since all page content is wrapped in `.animate-on-scroll` (which starts at `opacity: 0`), the IntersectionObserver never triggered, leaving everything at `opacity: 0`.

**Fix**: Two changes:
1. Rewrote every `window.BurgerIAM` method to capture `var self = window.BurgerIAM` at the top and use `self` instead of `this`.
2. Made scroll animations fail-safe: `.animate-on-scroll` only applies `opacity: 0` when parent has `.js-ready` class (added by JS on load). If JS fails or the IntersectionObserver doesn't fire, content remains visible by default.

**Files**: `src/WasmFrontend/wwwroot/js/app.js`, `src/WasmFrontend/Pages/Menu.razor`, `src/WasmFrontend/wwwroot/css/app.css`

---

## 2026-06-25 — Delivery tracking page shows HTML entity codes instead of emoji

**Problem**: The delivery tracking page displayed literal text `&#127881;` instead of the party popper emoji.

**Root cause**: Blazor HTML-encodes `@(...)` expression output. The HTML entity `&#127881;` was treated as literal text — the `&` was encoded to `&amp;`, producing `&amp;#127881;` which the browser rendered as `&#127881;`.

**Fix**: Replaced `@("&#127881;")` with `@((MarkupString)"🎉")`. Using `MarkupString` bypasses Blazor's HTML encoding and renders the emoji character directly. Also replaced the other HTML entities with actual emoji characters for consistency.

**Files**: `src/WasmFrontend/Pages/DeliveryTracking.razor`
# Debug Log

This file documents issues encountered during development and their resolutions. Refer here when troubleshooting similar problems.

---

## 2026-06-24 — Order stuck at "Order Placed" (status 0) after payment

**Problem**: After placing an order, the order status remained at 0 (Pending) and never advanced to 2 (Paid). The gateway's POST to `/api/internal/orders/{id}/confirm-payment` silently failed.

**Root cause**: All gRPC services had `"Protocols": "Http2"` in Kestrel config (HTTP/2 only). The gateway used `HttpClient` (defaults to HTTP/1.1) to call the REST confirm-payment endpoint. The request was rejected because the server only spoke HTTP/2. The exception was caught by the generic `catch` block, returning "Failed to create order".

**Fix**: Changed the REST call to use `HttpRequestMessage` with `Version = HttpVersion.Version20` and `VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher`. Kept Kestrel at `Http2` for gRPC compatibility.

---

## 2026-06-24 — Changing OrderService to Http1AndHttp2 broke gRPC calls

**Problem**: After changing OrderService Kestrel to `Http1AndHttp2`, gRPC calls from the gateway failed with `HTTP_1_1_REQUIRED (0xd)` — the server told the HTTP/2 client to use HTTP/1.1 instead.

**Root cause**: Kestrel in `Http1AndHttp2` mode can reject HTTP/2 requests for endpoints that don't look like gRPC. The gRPC client negotiates HTTP/2 via h2c upgrade, but after the upgrade, the server may reject actual gRPC requests.

**Fix**: Reverted to `"Protocols": "Http2"` and instead configured the gateway's HttpClient to send HTTP/2 requests when calling the OrderService REST endpoint.

---

## 2026-06-24 — POST /api/orders blocks for ~40s before returning to frontend

**Problem**: Clicking "Place Order" took ~40 seconds (4 stages × 10s delays) before the user saw the success page. By then all order stages were already complete, so the tracking page showed "Delivered" immediately.

**Fix**: Moved the stage progression (StartPreparing → MarkAsReady → AssignDelivery → UpdateDeliveryStatus) into a `BackgroundService` (`OrderProgressService`) using `System.Threading.Channels`. The POST handler now only does the fast path (create order, process payment, confirm payment, seed kitchen, generate receipt) and enqueues the order for background processing.

---

## 2026-06-24 — OrderProgressService not found by minimal API binder

**Problem**: ApiGateway failed at startup with "Failure to infer one or more parameters — progress (UNKNOWN)". The `OrderProgressService` was registered only as `AddHostedService<T>()` which registers it as `IHostedService`, not as the concrete type.

**Fix**: Registered as both singleton and hosted service:
```csharp
builder.Services.AddSingleton<OrderProgressService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<OrderProgressService>());
```

---

## 2026-06-24 — Order status page doesn't auto-refresh

**Problem**: The order tracking page only showed the initial status and never updated. Users had to manually refresh the browser to see stage progression.

**Root cause**: `System.Threading.Timer` callback (`async void`) inside `OnAfterRenderAsync` had threading issues in Blazor WASM — the timer's async callback ran on a thread pool thread, and `InvokeAsync(StateHasChanged)` didn't reliably trigger re-renders.

**Fix**: Replaced `Timer` with a fire-and-forget async loop in `OnInitializedAsync` using `Task.Delay`:
```csharp
if (order is not null && order.Status < 6)
    _ = RefreshAsync();

private async Task RefreshAsync()
{
    while (order is not null && order.Status < 6)
    {
        await Task.Delay(10000);
        await LoadOrder();
        await InvokeAsync(StateHasChanged);
    }
}
```

---

## 2026-06-24 — DeliveryTracking.razor "Element left unclosed" error

**Problem**: Clicking "Track Delivery" showed "A frame of type 'Element' was left unclosed. Do not use try/catch inside rendering logic, because partial output cannot be undone."

**Root cause**: `return;` statements inside `@if` blocks in Razor markup. When `return;` exits the rendering method, any open HTML elements from parent branches remain unclosed, corrupting the render tree.

**Fix**: Same pattern as Checkout.razor — replaced `@if/return` with `@if/else if/else` branching so all HTML elements are properly balanced.

---

## 2026-06-24 — Order total shows £0.00 on checkout confirmation and receipt

**Problem**: After placing an order, both the checkout success page and the receipt showed `£0.00` instead of the actual cart total. Items and prices appeared correctly in the cart before ordering.

**Root cause**: The gateway's `POST /api/orders` handler accepted `ProtoOrder.CreateOrderRequest` as the JSON body parameter. This protobuf-generated type has a getter-only `Items` property (`private readonly RepeatedField<OrderItem> items_`). System.Text.Json cannot populate a getter-only collection property — it creates the instance but the `Items` field remains empty. The OrderService then calculated `totalAmount = 0` from the empty items list.

**Fix**: Created plain C# record DTOs (`CreateOrderItemDto`, `CreateOrderDto`) for the REST endpoint binding, then manually mapped to the protobuf types before calling gRPC:
```csharp
var orderReq = new ProtoOrder.CreateOrderRequest { ... };
orderReq.Items.AddRange(dto.Items.Select(i => new ProtoOrder.OrderItem { ... }));
```

---

## 2026-06-23 — .NET 10 SDK creates .slnx format incompatible with VS 2022

**Problem**: `dotnet new sln` with .NET 10 SDK generates `BurgerIAM.slnx` (XML format), which Visual Studio 2022 cannot open. VS 2022 requires the legacy `.sln` format.

**Fix**: Deleted `.slnx` and manually created `BurgerIAM.sln` in the classic OLE format. Verified both `dotnet build` and VS 2022 open it correctly.

---

## 2026-06-23 — EF Core packages resolve to 10.x which targets net10.0

**Problem**: `dotnet add package Microsoft.EntityFrameworkCore.Sqlite` resolves to version 10.0.9, which only supports `net10.0`. Our projects target `net9.0`.

**Fix**: Pin to version 9.0.0 explicitly: `dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 9.0.0`. Same for `Microsoft.AspNetCore.Authentication.JwtBearer` and `Microsoft.EntityFrameworkCore.InMemory`.

---

## 2026-06-23 — Tests missing in Phase 1 commit

**Problem**: Phase 1 was committed without any test projects, violating the requirement that tests be written and executed after each change.

**Fix**: Created `BurgerIAM.Shared.Tests` (14 tests) and `BurgerIAM.EventBus.Tests` (5 tests) with an `InMemoryEventBus` implementation. Amended the Phase 1 commit.

---

## 2026-06-23 — Proto C# namespace collision with project namespace

**Problem**: Proto files define `option csharp_namespace = "BurgerIAM.Protos.Menu"`, which generates a `MenuService` class — same name as the project namespace `MenuService`. Also `MenuItem` proto type conflicts with `MenuService.Data.MenuItem` entity class.

**Fix**: Used namespace aliases (`ProtoMenu`, `ProtoIdentity`, `ProtoCommon`) instead of direct `using` directives. Renamed entity class to `MenuItemEntity`. Used fully-qualified types in service implementations.

---

## 2026-06-23 — MockServerCallContext fails to compile with newer gRPC API

**Problem**: The `ServerCallContext` abstract class in Grpc.Core.Api 2.80.0 has different abstract members (`CreatePropagationTokenCore` instead of `PropagateCancellationToChildrenCore`, no `HttpContextCore`, no `CompletionTaskFlag`).

**Fix**: Simplified `MockServerCallContext` to only implement the required members: `CreatePropagationTokenCore` and `WriteResponseHeadersAsyncCore`. Removed outdated override methods.

---

## 2026-06-23 — gRPC reflection not enabled — grpcurl fails

**Problem**: `grpcurl` reported "server does not support the reflection API" because gRPC reflection was not registered on the server.

**Fix**: Added `Grpc.AspNetCore.Server.Reflection` NuGet package to both IdentityService and MenuService, then added `builder.Services.AddGrpcReflection()` and `app.MapGrpcReflectionService()` in each `Program.cs`.

---

## 2026-06-23 — SQLite .db files accidentally committed to git

**Problem**: Running the services locally created `identity.db`, `menu.db`, and their WAL/SHM files, which were then tracked by git via `git add -A`.

**Fix**: Added `*.db`, `*.db-shm`, `*.db-wal` to `.gitignore`, removed the files from tracking with `git rm --cached`, and amended the commit.

---

## 2026-06-23 — PowerShell single quotes not passed correctly to grpcurl

**Problem**: `grpcurl -plaintext -d '{"email":"..."}' ...` failed with "invalid character 'e' looking for beginning of object key string" because PowerShell treats single-quoted strings differently when passing to native commands.

**Fix**: Use a JSON variable with escaped double quotes: `$body = "{`"email`":`"..."}"`. Or use the ManualTestApp instead.

---

## 2026-06-23 — ManualTestApp GetPayment used orderId instead of paymentId

**Problem**: The `GetPayment` manual test passed `orderId` (the order's ID) as the `PaymentId` parameter, causing a "not found" error since payments use a different ID.

**Reproduction**: Run all four services then `dotnet run --project tests/ManualTestApp -- http://localhost:5041 http://localhost:5052 http://localhost:5063 http://localhost:5074`. The "GetPayment - existing payment" test fails.

**Fix**: Added a `paymentId` variable to capture the response from `ProcessPayment` and used it in the `GetPayment` call.

---

## 2026-06-23 — PaymentService singleton/scoped DI mismatch

**Problem**: `PaymentGrpcService` was registered as a singleton (`builder.Services.AddSingleton<PaymentGrpcService>()`) but depends on `AppDbContext` which is scoped (DbContext default lifetime). This threw at startup: "Cannot consume scoped service from singleton."

**Reproduction**: `dotnet run --project src/PaymentService` fails with `AggregateException` during service validation.

**Fix**: Removed the explicit singleton registration. `MapGrpcService<PaymentGrpcService>()` registers it with the correct scoped lifetime automatically. The `EventBusHostedService` (which needs to resolve `PaymentGrpcService`) already uses `IServiceScopeFactory` to create a scope, so resolution works correctly.

---

## 2026-06-23 — Protobuf string field rejects null assignment

**Problem**: Setting `Error = null` on a protobuf message field (a `string` type) throws `ArgumentNullException` because protobuf fields don't accept null values.

**Reproduction**: `PaymentService.Tests.PaymentGrpcServiceTests.ProcessPayment_DuplicateOrder_ReturnsExisting` fails with `ArgumentNullException`.

**Fix**: Changed `Error = existing.Status == 2 ? null : "Payment already exists"` to `Error = existing.Status != 2 ? "Payment already exists" : string.Empty`, ensuring an empty string is assigned instead of null.

---

## 2026-06-24 — Missing Home.razor page causes 404 on root route "/"

**Problem**: Navigating to "/" showed Blazor's `<NotFound>` template ("Page not found, Sorry there's nothing at this address") because no page existed with `@page "/"`. The NavMenu brand link and logout action both redirect to "/".

**Fix**: Created `src/WasmFrontend/Pages/Home.razor` with `@page "/"` and `[AllowAnonymous]`. Also created `DeliveryTracking.razor` with `@page "/delivery/{OrderId}"` and added a "Track Delivery" link to `OrderStatus.razor`.

---

## 2026-06-24 — ApiGateway MSBuild target skips WASM copy after first build

**Problem**: `ApiGateway.csproj` had `Condition="!Exists('wwwroot\index.html')"` on its `PublishAndCopyWasmFrontend` target. After the first build created `wwwroot\index.html`, all subsequent builds skipped re-publishing and copying the WasmFrontend output. Changes to Blazor pages never reached the served SPA.

**Reproduction**: Add a new `.razor` page, rebuild, refresh browser — changes don't appear. The old `.wasm` and `.dll` files in `ApiGateway/wwwroot/_framework/` are never updated.

**Fix**: Removed the `Condition` attribute from the MSBuild target so it always runs `dotnet publish` on WasmFrontend and copies the output on every build. Also added an explicit `Remove-Item` of `ApiGateway/wwwroot/` before rebuild to avoid stale files.

---

## 2026-06-24 — Blazor error overlay always visible (missing CSS)

**Problem**: The `<div id="blazor-error-ui">An unhandled error has occurred...</div>` in `index.html` was always visible because `app.css` was missing the `#blazor-error-ui { display: none; }` style rule. The `.gitignore` pattern `wwwroot/` also prevented `app.css` from being tracked by git.

**Fix**: Added `#blazor-error-ui` CSS (with `display: none`, fixed positioning, and dismiss button styles) to `src/WasmFrontend/wwwroot/css/app.css`. Changed the `.gitignore` pattern from `wwwroot/` to `src/ApiGateway/wwwroot/` so the WasmFrontend's custom static assets are tracked.

---

## 2026-06-24 — Unhandled exception on "Place Order" button click crashes Blazor app

**Problem**: Clicking "Place Order" in Checkout.razor showed "An unhandled error has occurred. Reload" because the error path had no exception handling at any layer:

1. `ApiService.CreateOrderAsync()` (line 17-22) had no `try/catch` — unlike every other method in that class
2. `Checkout.razor.PlaceOrder()` (line 115-141) had no `try/catch` around `Api.CreateOrderAsync()`
3. ApiGateway `POST /api/orders` (line 169-173) had no `try/catch` — the only endpoint without error handling
4. `App.razor` had no `<ErrorBoundary>` — so any unhandled exception triggered Blazor's default fatal error overlay

**Reproduction**: Log in, add items to cart, go to /checkout, fill in address, click "Place Order" while the OrderService is not running.

**Fix**:
- Wrapped `ApiService.CreateOrderAsync()`, `ProcessPaymentAsync()`, `SubmitFeedbackAsync()`, and `GetMenuAsync()` in `try/catch` returning `null`/empty on failure (matching the pattern of all other methods).
- Wrapped `Checkout.razor.PlaceOrder()` body in `try/catch` with user-friendly error display.
- Added `try/catch` to ApiGateway `POST /api/orders` endpoint returning `BadRequest` on failure (matching all other endpoints).
