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

---

## 2026-06-30 — Menu page blank (no items visible) after UX redesign

**Problem**: After the UX redesign, the Menu page appeared blank — the page loaded but no menu items were visible. The data was being fetched correctly from the API but content was not displayed.

**Root cause**: The scroll-animation system uses `IntersectionObserver` to add `.animate-visible` class to `.animate-on-scroll` elements (which start at `opacity: 0`). The `OnAfterRenderAsync` in each page called `window.BurgerIAM.observeNewElements()` only on first render (`if (firstRender)`). However, on first render, skeleton loaders are shown (no `.animate-on-scroll`). When the API data loads and actual items re-render, `firstRender` is `false`, so `observeNewElements` is never called. The items stay at `opacity: 0` permanently.

**Fix**: Removed the `if (firstRender)` guard from `OnAfterRenderAsync` in all pages so `observeNewElements()` runs on every render. The JS method already deduplicates by using the `:not(.animate-visible)` selector.

**Files**: `src/WasmFrontend/Pages/Menu.razor`, `Home.razor`, `Checkout.razor`, `OrderStatus.razor`, `MyOrders.razor`, `Cart.razor`, `DeliveryTracking.razor`, `Feedback.razor`, `Login.razor`, `Receipt.razor`, `Register.razor`

---

## 2026-06-30 — Delivery tracking text unreadable (dark theme contrast)

**Problem**: Several readability issues across the delivery tracking, order status, checkout, cart, and feedback pages:
- Card backgrounds at 4% white were nearly invisible against the dark page background
- Timeline titles for completed steps and all timeline descriptions used `text-muted` (40% white)
- Section headings were plain white with no visual hierarchy
- Detail labels and values had no consistent styling pattern
- Estimated delivery times displayed as raw ISO datetime strings (`2026-06-29T13:05:41.1753143`)
- Empty state text and auto-refresh indicators used dim 40% white text

**Fix** (multi-commit):
1. Card background 4% → 7%, borders 8% → 12% for visible containers
2. Added `.card-section-title` class: amber accent (`var(--accent)`), weight 700, bottom border — applied to all card section headings across DeliveryTracking, OrderStatus, Checkout, Cart, Feedback
3. Added `.detail-label` (70% white, weight 600, min-width 90px) and `.detail-value` (white, weight 500) for consistent label/value pairs
4. Formatted `EstimatedDeliveryTime` using `DeliveryDate()`/`OrderDate()` — converts ISO string to `"MMM dd, yyyy HH:mm"` format
5. Bumped all `var(--text-muted)` content references to `var(--text-secondary)` (45% → 70% white): auto-refresh text, empty states, order metadata, quantity labels
6. Changed timeline completed step titles from `text-muted` to green `#2ecc71`
7. Added `?v=2` cache-buster to CSS link to bypass aggressive Blazor WASM caching
8. Removed stale `.br`/`.gz` compressed CSS files (also deleted stale copies that reappear)

**Files**:
- `src/WasmFrontend/wwwroot/css/app.css` — card colors, `.card-section-title`, `.detail-label`, `.detail-value`, timeline colors
- `src/WasmFrontend/wwwroot/index.html` — cache-busting CSS version
- `src/WasmFrontend/Pages/DeliveryTracking.razor` — card-section-title, detail-label/value, DeliveryDate formatting
- `src/WasmFrontend/Pages/OrderStatus.razor` — card-section-title, detail-label/value, OrderDate formatting
- `src/WasmFrontend/Pages/Checkout.razor` — card-section-title, bumped text colors
- `src/WasmFrontend/Pages/Cart.razor` — card-section-title, bumped text colors
- `src/WasmFrontend/Pages/Feedback.razor` — card-section-title

---

## 2026-06-30 — Receipt page white background clashes with dark theme

**Problem**: The receipt page displayed a stark white iframe on the dark-themed site. The `BuildReceiptHtml` method in ReceiptService generated light-themed HTML (white background, `#333` text, `#eee` borders, `#666` muted text), and the `receipt-frame` CSS explicitly set `background: white`.

**Fix** (commit `ea60ebd`):
- Rewrote `BuildReceiptHtml` with full dark theme matching the BurgerIAM design system:
  - Page background `#0d0d1a` with `Plus Jakarta Sans` font
  - Gradient header (`#e63946` → `#f4a261`) matching the brand logo style
  - Card container using the same pattern as site cards: `rgba(255,255,255,0.04)` background, `rgba(255,255,255,0.08)` border, `16px` border-radius
  - Muted labels at 50% white (`rgba(255,255,255,0.5)`), values in full white with weight 600
  - Total amount in large red (`#e63946`) 32px bold
  - Footer gradient brand name with muted thank-you text
  - Date format changed from `"yyyy-MM-dd HH:mm"` to `"MMM dd, yyyy HH:mm"` for readability
- Updated `.receipt-frame` CSS: removed `background: white`, set to `transparent`
- Bumped empty state heading/text on Receipt.razor from `text-muted` to `text-secondary`

**Files**:
- `src/ReceiptService/Program.cs` — `BuildReceiptHtml` dark theme redesign
- `src/WasmFrontend/wwwroot/css/app.css` — `.receipt-frame` background fix
- `src/WasmFrontend/Pages/Receipt.razor` — empty state text color

---

## 2026-06-30 — Sign-in fails silently with generic "Invalid email or password"

**Problem**: Two compounding issues prevented sign-in from working or giving useful feedback:
1. `ApiGateway` login/register endpoints used protobuf generated types (`ProtoIdentity.LoginRequest`, `ProtoIdentity.RegisterRequest`) as Minimal API body parameters. The same pattern broke `CreateOrderRequest` before (the `RepeatedField<T>` collection was getter-only). For simple string fields the risk is lower but still fragile.
2. No exception handling around the gRPC call in `/api/auth/login`. If `IdentityService` was down, the Gateway returned a raw 500. The frontend then discarded the error body and showed "Invalid email or password" for every failure — network errors, wrong credentials, service down, all looked the same.

**Root cause**:  
- Protobuf message types have non-standard property setters (`pb::ProtoPreconditions.CheckNotNull`) and the `IMessage` interface adds overhead that `System.Text.Json` wasn't designed to handle. The earlier `CreateOrderRequest` workaround (2026-06-24) proved that plain DTOs are safer for REST binding.
- The login endpoint was the only high-traffic endpoint with zero exception handling — every other `RequireAuthorization()` gRPC endpoint had try-catch, but `/api/auth/login` and `/api/auth/register` did not.
- `AuthService.Login()` didn't read the response body on error — it just returned `null`.

**Fix**:
- Created `LoginRequestDto` and `RegisterRequestDto` plain records in `ApiGateway/Program.cs`.
- Replaced protobuf parameter binding with DTOs, then manually mapped to protobuf types before the gRPC call (same pattern as `CreateOrderDto`).
- Wrapped the gRPC calls in `try-catch` so connectivity errors return `400 BadRequest { error: "..." }` instead of a raw 500.
- `AuthService.Login()` now reads the response body's `error` field on non-2xx and returns it to the UI. The `Login` method signature changed from `Task<AuthResponse?>` to `Task<(AuthResponse? Result, string? Error)>`.
- `Login.razor` displays the actual server error message instead of the hardcoded string.

**Files**: `src/ApiGateway/Program.cs`, `src/WasmFrontend/Services/AuthService.cs`, `src/WasmFrontend/Pages/Login.razor`

---

## 2026-07-03 — Unauthenticated users can add items to cart (ghost cart)

**Problem**: The Menu page (`[AllowAnonymous]`) allowed any unauthenticated user to click "Add to Cart". The `CartService` accepted items regardless of auth state. Since `Cart.razor` and `Checkout.razor` require `[Authorize]`, the items accumulated invisibly. When the user finally logged in, the cart was full of stale items from before authentication — or items added during a previous anonymous session were still there.

**Root cause**: Two compounding issues:
1. `Menu.razor.AddToCart()` had no auth check — it called `Cart.AddItem()` unconditionally
2. `Login.razor` never cleared the cart on successful login — old items persisted across sessions

**Fix**:
1. `Menu.razor.AddToCart()` now checks `Auth.IsLoggedIn` first. If not logged in, it redirects to `/login` without adding the item.
2. `Login.razor.HandleLogin()` calls `Cart.Clear()` immediately after successful authentication, ensuring every new session starts with an empty cart.

**Files**: `src/WasmFrontend/Pages/Menu.razor`, `src/WasmFrontend/Pages/Login.razor`

---

## 2026-07-03 — Receipt shows no items and no prices (race with event-driven creation)

**Problem**: After placing an order, the receipt page displayed an empty items list and zero/empty prices even though the order had items with correct prices.

**Root cause**: Dual receipt creation paths (issue #6 in issues.md). The `PaymentConfirmedEvent` published during `ProcessPaymentAsync` triggered `ReceiptService.EventBusHostedService`, which created a receipt with `ItemsJson = "[]"` and no customer info. Since the in-memory event bus ran handlers synchronously during the publish call, this happened **before** the ApiGateway's HTTP `POST /receipts` call (which had full item data). The HTTP call found an existing receipt and skipped.

**Fix**: Removed the event-driven receipt creation path entirely:
- Deleted `ReceiptService/EventBusHostedService.cs`
- Removed `IEventBus` registration and `EventBusHostedService` from `ReceiptService/Program.cs`
- Removed unused `IEventBus` dependency and `HandlePaymentConfirmed` from `ReceiptServiceHandler`
- Removed the two `HandlePaymentConfirmed` tests (they tested removed functionality)
- Receipt creation now happens exclusively via the ApiGateway's HTTP `POST /receipts` call, which passes order items, customer info, and total amount correctly

**Files**: `src/ReceiptService/EventBusHostedService.cs` (deleted), `src/ReceiptService/Program.cs`, `src/ReceiptService/Services/ReceiptServiceHandler.cs`, `tests/ReceiptService.Tests/ReceiptServiceHandlerTests.cs`

---

## 2026-07-03 — Receipt items show as "0"/£0.00 due to JSON case sensitivity

**Problem**: The receipt items table displayed default values (item name "0", quantity 0, price £0.00) even though the total amount was correct. The `ItemsJson` field in the receipt had correct data, but the frontend couldn't parse it.

**Root cause**: The ApiGateway serializes receipt items into JSON using camelCase property names (`menuItemId`, `itemName`, `quantity`, `unitPrice`). The `Receipt.razor` page uses `JsonSerializer.Deserialize<List<ReceiptItem>>()` to parse this JSON back. However, the default `JsonSerializerOptions` uses **case-sensitive** property matching, while the `ReceiptItem` positional record has PascalCase constructor parameters (`MenuItemId`, `ItemName`). The case mismatch caused all properties to default to `null`/`0`.

**Fix**: Added `PropertyNameCaseInsensitive = true` to the `JsonSerializerOptions` used in `Receipt.razor`'s item deserialization. This correctly maps camelCase JSON properties to the PascalCase record parameters.

**Files**: `src/WasmFrontend/Pages/Receipt.razor`

---

## 2026-07-04 — Star rating always selects all 5 stars in Feedback.razor

**Problem**: Clicking any star (1-5) in the feedback form always lit up all 5 stars. Users couldn't change their selection — clicking star 3 after star 4 had no effect.

**Root cause**: Classic C# closure bug with `for` loop variables. The `@for (int i = 1; i <= 5; i++)` loop declared `i` once, and all `@onclick="() => rating = i"` lambdas captured the same `i` reference. By the time any button was clicked, the loop had finished and `i` was 6, so `rating = 6` was set. The active-star CSS condition `i <= rating` then evaluated `1-5 <= 6` as true for all stars.

**Fix**: Captured a local `var star = i;` inside the loop body, and used `@onclick="() => rating = star"`, so each lambda captures its own per-iteration value.

**Files**: `src/WasmFrontend/Pages/Feedback.razor`

---

## 2026-07-06 — RabbitMQ container fails with ".erlang.cookie: eacces"

**Problem**: `Start-Containers.ps1` failed to start RabbitMQ with `"Error when reading /var/lib/rabbitmq/.erlang.cookie: eacces"`. RabbitMQ could not read its Erlang cluster cookie file due to filesystem permission issues on Docker Desktop for Windows.

**Root cause**: Two compounding issues:
1. The RabbitMQ startup code in `Start-Containers.ps1` defined env vars and volumes in the `$services` hash but **never passed them to `docker run`** — only `$portArgs` was used. The `RABBITMQ_ERLANG_COOKIE` env var was configured but not applied.
2. The `rabbitmq:3-management` image expects `/var/lib/rabbitmq/.erlang.cookie` to have restricted permissions (owned by `rabbitmq` user). Docker Desktop for Windows overlay filesystem misapplies POSIX permissions on this file. Even without a named volume, Docker creates an anonymous volume from the image's `VOLUME /var/lib/rabbitmq` directive, which can carry stale permission errors.

**Fix**:
- Look up the RabbitMQ service definition from `$services` and pass `$envArgs` and `$volArgs` to `docker run` (same as all other services)
- Added `docker volume rm -f rabbitmq_data` before starting RabbitMQ to purge any stale volume data with wrong permissions
- Set `RABBITMQ_ERLANG_COOKIE=burgeriam-cluster-cookie` environment variable
- Added `--user root` to RabbitMQ's `docker run` — Docker Desktop on Windows creates `/var/lib/rabbitmq` with root ownership that the `rabbitmq` user can't read. Running as root bypasses the permission issue entirely for local testing.

**Files**: `Start-Containers.ps1`

---

## 2026-07-07 — Start-Containers.ps1 default tag mismatch with Build-Images.ps1

**Problem**: `Start-Containers.ps1` failed to find Docker images because `Build-Images.ps1` defaulted to tag `latest` while `Start-Containers.ps1` defaulted to tag `v1.0`. This caused either "Missing images" errors or running stale containers from a previous deployment.

**Root cause**: The two scripts used incompatible default tags:
- `Build-Images.ps1`: `[string]$Tag = "latest"`
- `Start-Containers.ps1`: `[string]$ImageTag = "v1.0"`

**Fix**: Changed `Start-Containers.ps1` default `$ImageTag` from `"v1.0"` to `"latest"`.

**Files**: `Start-Containers.ps1` line 24

---

## 2026-07-06 — Docker DNS resolves container names, not short names

**Problem**: API Gateway gRPC calls to backend services failed with `Grpc.Core.RpcException: Name or service not known`. The API gateway resolved `menu-service` as `NXDOMAIN` even though both containers were on the same Docker `burgeriam` network.

**Root cause**: Docker DNS on user-defined bridges resolves **container names** (`burgeriam-menu-service`), not the short names used in environment variables (`menu-service`). The `Start-Containers.ps1` set `Services__Menu=http://menu-service:5052` but the container's actual DNS name was `burgeriam-menu-service`.

**Fix**: Updated all `Services__*` env vars in `Start-Containers.ps1` (and nginx `proxy_pass` in `nginx.conf`) to use the full container names (`burgeriam-<name>`) instead of short names.

**Files**: `Start-Containers.ps1`, `src/WasmFrontend/nginx.conf`

---

## 2026-07-06 — nginx fails at startup with "host not found in upstream"

**Problem**: nginx container exited immediately with `[emerg] host not found in upstream "api-gateway"`. The variable `proxy_pass http://api-gateway:5000` requires DNS resolution at configuration load time, but the `api-gateway` container hadn't started yet or wasn't on the same network.

**Root cause**: When nginx's `proxy_pass` uses a literal hostname (not a variable), it resolves the DNS name **at startup** during config parsing. If the upstream container isn't running or DNS is unavailable, nginx fails to start.

**Fix**: Used nginx variables for the proxy target:
```
set $gateway_api "http://burgeriam-api-gateway:5000";
proxy_pass $gateway_api;
```
With a variable, nginx defers DNS resolution to **runtime** using the `resolver 127.0.0.11 ipv6=off valid=10s;` directive (Docker DNS). Also fixed the hostname to `burgeriam-api-gateway`.

**Files**: `src/WasmFrontend/nginx.conf`

---

## 2026-07-06 — WasmFrontend Dockerfile copies wrong directory

**Problem**: The Blazor WASM frontend served nginx's default welcome page (896 bytes) instead of the Blazor app. The `index.html` in the nginx html root was the nginx default, while the actual Blazor app was nested at `/usr/share/nginx/html/wwwroot/index.html`.

**Root cause**: `dotnet publish -o /app/wwwroot` places the Blazor WASM output at `/app/wwwroot/wwwroot/` (nested). The Dockerfile had `COPY --from=build /app/wwwroot .` which copied the parent directory, placing the Blazor output into a nested `wwwroot/` subdirectory.

**Fix**: Changed to `COPY --from=build /app/wwwroot/wwwroot .` so the Blazor WASM static files land directly in nginx's root.

**Files**: `src/WasmFrontend/Dockerfile`

---

## 2026-07-06 — RabbitMQ made optional; InMemoryEventBus as default

**Problem**: RabbitMQ container would not start on Docker Desktop for Windows due to `.erlang.cookie` POSIX permission issues. The entire `Start-Containers.ps1` failed because RabbitMQ was a hard dependency.

**Resolution**: Refactored `Start-Containers.ps1`:
- Removed RabbitMQ from the `$services` array (no longer a required service)
- Added `-UseRabbitMQ` switch to optionally start RabbitMQ (attempt, skip on failure)
- Backend services no longer have `EventBus__ConnectionString` env vars by default — they fall back to `InMemoryEventBus` when the connection string is empty
- Added `$backendEventBusEnv` helper to conditionally inject RabbitMQ connection string only when `-UseRabbitMQ` is specified

**Files**: `Start-Containers.ps1`
