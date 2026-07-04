# BurgerIAM — Test Harness

## Overview

This document describes the testing infrastructure for the BurgerIAM microservice ordering system. Tests are organized by development phase, with automated unit/integration tests (xUnit) and manual end-to-end tests (ManualTestApp).

---

## Test Projects

| Project | Type | Location | Depends On |
|---|---|---|---|
| `BurgerIAM.Shared.Tests` | Unit | `tests/BurgerIAM.Shared.Tests/` | `BurgerIAM.Shared` |
| `BurgerIAM.EventBus.Tests` | Unit | `tests/BurgerIAM.EventBus.Tests/` | `BurgerIAM.EventBus`, `BurgerIAM.TestUtilities` |
| `IdentityService.Tests` | Unit | `tests/IdentityService.Tests/` | `IdentityService`, `BurgerIAM.TestUtilities` |
| `MenuService.Tests` | Unit | `tests/MenuService.Tests/` | `MenuService`, `BurgerIAM.TestUtilities` |
| `OrderService.Tests` | Unit | `tests/OrderService.Tests/` | `OrderService`, `BurgerIAM.TestUtilities` |
| `PaymentService.Tests` | Unit | `tests/PaymentService.Tests/` | `PaymentService`, `BurgerIAM.TestUtilities` |
| `KitchenService.Tests` | Unit | `tests/KitchenService.Tests/` | `KitchenService`, `BurgerIAM.TestUtilities` |
| `DeliveryService.Tests` | Unit | `tests/DeliveryService.Tests/` | `DeliveryService`, `BurgerIAM.TestUtilities` |
| `FeedbackService.Tests` | Unit | `tests/FeedbackService.Tests/` | `FeedbackService`, `BurgerIAM.TestUtilities` |
| `Integration.Tests` | Integration | `tests/Integration.Tests/` | `BurgerIAM.EventBus`, `BurgerIAM.TestUtilities` |
| `ManualTestApp` | Manual | `tests/ManualTestApp/` | gRPC proto clients |

### Shared Test Utilities

Location: `tests/BurgerIAM.TestUtilities/`

| Utility | Purpose |
|---|---|
| `MockServerCallContext` | Abstract `ServerCallContext` for unit testing gRPC services |
| `InMemoryEventBus` | In-memory `IEventBus` implementation for testing event flows without RabbitMQ |

---

## Running Tests

### Run All Automated Tests

```powershell
# From repository root
dotnet test BurgerIAM.sln --nologo
```

### Run a Specific Test Project

```powershell
dotnet test tests/IdentityService.Tests --nologo
dotnet test tests/Integration.Tests --nologo
```

### Run with Detailed Output

```powershell
dotnet test BurgerIAM.sln --nologo -v n
```

### Run Manual Integration Tests

The manual test app (`tests/ManualTestApp/`) exercises gRPC endpoints against live services. It runs a series of PASS/FAIL scenarios for each configured microservice.

**Step 1: Start the required services**

Each service must be running before you launch the manual tests. Open separate terminals for each:

| # | Service | Port | Start Command |
|---|---------|------|---------------|
| 1 | **IdentityService** | 5041 | `dotnet run --project src/IdentityService` |
| 2 | **MenuService** | 5052 | `dotnet run --project src/MenuService` |
| 3 | **OrderService** | 5063 | `dotnet run --project src/OrderService` |
| 4 | **PaymentService** | 5074 | `dotnet run --project src/PaymentService` |
| 5 | **KitchenService** | 5085 | `dotnet run --project src/KitchenService` |
| 6 | **DeliveryService** | 5096 | `dotnet run --project src/DeliveryService` |

**Step 2: Run the manual test app**

Pass the service URLs in this exact order:
```
<IdentityUrl> <MenuUrl> <OrderUrl> <PaymentUrl> <KitchenUrl> <DeliveryUrl>
```

```powershell
dotnet run --project tests/ManualTestApp -- http://localhost:5041 http://localhost:5052 http://localhost:5063 http://localhost:5074 http://localhost:5085 http://localhost:5096
```

**Step 3: Interpret the results**

Each test prints a `PASS` or `FAIL` status. The final summary shows the pass/fail count. A non-zero failure count exits with code 1.

**Per-service testing (run only specific services):**

If you only want to test certain services, pass only the URLs for the running services:

```powershell
# Identity + Menu only (Phase 2)
dotnet run --project tests/ManualTestApp -- http://localhost:5041 http://localhost:5052

# Identity + Menu + Order + Payment (Phase 3 — full)
dotnet run --project tests/ManualTestApp -- http://localhost:5041 http://localhost:5052 http://localhost:5063 http://localhost:5074

# All six services (Phase 4 — full)
dotnet run --project tests/ManualTestApp -- http://localhost:5041 http://localhost:5052 http://localhost:5063 http://localhost:5074 http://localhost:5085 http://localhost:5096
```

---

## Test Coverage by Phase

### Phase 1 — Foundation & Shared Infrastructure

**Projects:** `BurgerIAM.Shared.Tests`, `BurgerIAM.EventBus.Tests`

**Tests:** 19 (14 Shared + 5 EventBus)

| File | Tests | What It Verifies |
|---|---|---|
| `DtoTests.cs` | 5 | DTO construction: `MenuItemDto`, `OrderItemDto.Subtotal`, `OrderDto`, `PaymentDto`, `UserDto` |
| `EnumTests.cs` | 3 | `OrderStatus`, `PaymentStatus`, `DeliveryStatus` values |
| `IntegrationEventTests.cs` | 6 | Base event behavior (`EventId`, `OccurredOn`, `EventType`), `OrderPlacedEvent`, `PaymentConfirmedEvent`, `OrderCancelledEvent` |
| `EventBusTests.cs` | 5 | Publish/subscribe, multi-subscriber, no-subscriber safety, unsubscribe, type routing |

**How to run:**
```powershell
dotnet test tests/BurgerIAM.Shared.Tests --nologo
dotnet test tests/BurgerIAM.EventBus.Tests --nologo
```

---

### Phase 2 — Identity & Menu Services

**Projects:** `IdentityService.Tests`, `MenuService.Tests`

**Tests:** 11 (6 Identity + 5 Menu)

| File | Tests | What It Verifies |
|---|---|---|
| `IdentityGrpcServiceTests.cs` | 6 | Register new user, duplicate email, login valid/invalid, validate token valid/invalid |
| `MenuGrpcServiceTests.cs` | 5 | Empty database, return all items, get by ID, not found, update availability |

**How to run:**
```powershell
dotnet test tests/IdentityService.Tests --nologo
dotnet test tests/MenuService.Tests --nologo
```

**Manual test — requires IdentityService + MenuService running:**
| Service | Port | Start Command |
|---------|------|---------------|
| IdentityService | 5041 | `dotnet run --project src/IdentityService` |
| MenuService | 5052 | `dotnet run --project src/MenuService` |

```powershell
dotnet run --project tests/ManualTestApp -- http://localhost:5041 http://localhost:5052
```

---

### Phase 3 — Order & Payment Orchestration

**Services Implemented:**
- `src/OrderService/` — gRPC service (port 5063), SQLite, publishes `OrderPlacedEvent`
- `src/PaymentService/` — gRPC service (port 5074), SQLite, consumes `OrderPlacedEvent`, publishes `PaymentConfirmedEvent`

**Projects:** `OrderService.Tests`, `PaymentService.Tests`, `Integration.Tests`

**Tests:** 17 (7 Order + 6 Payment + 4 Integration)

| File | Tests | What It Verifies |
|---|---|---|
| `OrderGrpcServiceTests.cs` | 7 | CreateOrder, GetOrder (exists + not found), GetOrderStatus, CancelOrder (success + not found), GetCustomerOrders |
| `PaymentGrpcServiceTests.cs` | 6 | ProcessPayment (new + duplicate), GetPayment (exists + not found), RefundPayment, HandleOrderPlaced event handler |
| `EventBusFlowsTests.cs` | 4 | OrderPlaced triggers payment, PaymentConfirmed triggers kitchen + receipt, PaymentFailed isolation, full lifecycle chain |

**How to run:**
```powershell
dotnet test tests/OrderService.Tests --nologo
dotnet test tests/PaymentService.Tests --nologo
dotnet test tests/Integration.Tests --nologo
```

**Manual test — requires all four services running:**
| Service | Port | Start Command |
|---------|------|---------------|
| IdentityService | 5041 | `dotnet run --project src/IdentityService` |
| MenuService | 5052 | `dotnet run --project src/MenuService` |
| OrderService | 5063 | `dotnet run --project src/OrderService` |
| PaymentService | 5074 | `dotnet run --project src/PaymentService` |

```powershell
dotnet run --project tests/ManualTestApp -- http://localhost:5041 http://localhost:5052 http://localhost:5063 http://localhost:5074
```

---

### Phase 4 — Kitchen & Delivery ✅

**Services Implemented:**
- `src/KitchenService/` — gRPC service (port 5085), SQLite, consumes `PaymentConfirmedEvent`, publishes `OrderInProgressEvent`/`OrderReadyEvent`
- `src/DeliveryService/` — gRPC service (port 5096), SQLite, consumes `OrderReadyEvent`, publishes `OrderOutForDeliveryEvent`/`OrderDeliveredEvent`

**Projects:** `KitchenService.Tests`, `DeliveryService.Tests`, extended `Integration.Tests`

**Tests:** 18 (9 Kitchen + 9 Delivery + 6 Integration total)

| File | Tests | What It Verifies |
|---|---|---|
| `KitchenGrpcServiceTests.cs` | 9 | GetPendingOrders (empty + filters), StartPreparing (success + not found + precondition), MarkAsReady (success + precondition), HandlePaymentConfirmed (creates + no duplicate) |
| `DeliveryGrpcServiceTests.cs` | 9 | AssignDelivery (no drivers + success + duplicate), UpdateDeliveryStatus (delivered frees driver), GetDeliveryStatus (exists + not found), GetDriverDeliveries, HandleOrderReady (creates + no duplicate) |
| `EventBusFlowsTests.cs` *(extended)* | 6 | Full lifecycle event chain tests |

**Manual test seed RPCs:**

The gRPC services expose two test-only RPCs used by `ManualTestApp` to seed data so that successful flows can be exercised:

| Service | RPC | Purpose |
|---|---|---|
| `KitchenService` | `SeedKitchenOrder(order_id)` | Creates a kitchen order in `Pending` status for a given order ID (idempotent) |
| `DeliveryService` | `SeedDriver(name)` | Creates an available driver for delivery assignment |

**How to run:**
```powershell
dotnet test tests/KitchenService.Tests --nologo
dotnet test tests/DeliveryService.Tests --nologo
```

**Manual test — requires all six services running:**

The `ManualTestApp` seeds data via `SeedKitchenOrder` and `SeedDriver` RPCs at the start of the Kitchen and Delivery sections, then runs successful flows (StartPreparing, MarkAsReady, AssignDelivery, UpdateDeliveryStatus, etc.).

| Service | Port | Start Command |
|---|---|---|
| IdentityService | 5041 | `dotnet run --project src/IdentityService` |
| MenuService | 5052 | `dotnet run --project src/MenuService` |
| OrderService | 5063 | `dotnet run --project src/OrderService` |
| PaymentService | 5074 | `dotnet run --project src/PaymentService` |
| KitchenService | 5085 | `dotnet run --project src/KitchenService` |
| DeliveryService | 5096 | `dotnet run --project src/DeliveryService` |

```powershell
dotnet run --project tests/ManualTestApp -- http://localhost:5041 http://localhost:5052 http://localhost:5063 http://localhost:5074 http://localhost:5085 http://localhost:5096
```

---

### Phase 5 — Feedback, Notification & Receipt Services ✅

**Services Implemented:**

| Service | Type | Port | Database | Description |
|---|---|---|---|---|
| `FeedbackService` | gRPC | 5007 | SQLite | Submit & query customer feedback |
| `NotificationService` | gRPC + EventBus | 5018 | SQLite | In-app notifications, consumes `OrderDeliveredEvent` |
| `ReceiptService` | Web API + EventBus | 5029 | SQLite | HTML receipt generation, consumes `PaymentConfirmedEvent` |

**Projects:** `FeedbackService.Tests`, `NotificationService.Tests`, `ReceiptService.Tests`

**Tests:** 15 (7 Feedback + 6 Notification + 2 Receipt)

| File | Tests | What It Verifies |
|---|---|---|
| `FeedbackGrpcServiceTests.cs` | 7 | SubmitFeedback (valid + duplicate + invalid rating), GetOrderFeedback (exists + not found), GetAverageRating (empty + with ratings) |
| `NotificationGrpcServiceTests.cs` | 6 | GetNotifications (empty + filtered), MarkAsRead (existing + not found), GetUnreadCount, HandleOrderDelivered event handler |
| `ReceiptServiceHandlerTests.cs` | 2 | GetReceipt (exists + not found) |

**How to run:**
```powershell
dotnet test tests/FeedbackService.Tests --nologo
dotnet test tests/NotificationService.Tests --nologo
dotnet test tests/ReceiptService.Tests --nologo
```

**Manual test — requires all nine services running:**

| Service | Port | Start Command |
|---|---|---|
| IdentityService | 5041 | `dotnet run --project src/IdentityService` |
| MenuService | 5052 | `dotnet run --project src/MenuService` |
| OrderService | 5063 | `dotnet run --project src/OrderService` |
| PaymentService | 5074 | `dotnet run --project src/PaymentService` |
| KitchenService | 5085 | `dotnet run --project src/KitchenService` |
| DeliveryService | 5096 | `dotnet run --project src/DeliveryService` |
| FeedbackService | 5007 | `dotnet run --project src/FeedbackService` |
| NotificationService | 5018 | `dotnet run --project src/NotificationService` |
| ReceiptService | 5029 | `dotnet run --project src/ReceiptService` |

```powershell
dotnet run --project tests/ManualTestApp -- http://localhost:5041 http://localhost:5052 http://localhost:5063 http://localhost:5074 http://localhost:5085 http://localhost:5096 http://localhost:5007 http://localhost:5018 http://localhost:5029
```

---

### Phase 6 — API Gateway & Web Frontend ✅

**Projects:** `ApiGateway.Tests`, `WasmFrontend.Tests`

**Tests:** 34 (14 ApiGateway + 20 WasmFrontend)

| File | Tests | What It Verifies |
|---|---|---|
| `GatewayEndpointTests.cs` | 14 | All 25+ endpoint routes are registered, JWT config, service URL ports, gateway port |
| `CartServiceTests.cs` | 15 | Cart add/remove/update/clear, event firing, TotalAmount calculation, empty state |
| `ApiModelsTests.cs` | 5 | Login/Register/CreateOrder/CartItem constructors, MenuItemResponse props, valid status range |

**How to run:**
```powershell
dotnet test tests/ApiGateway.Tests --nologo
dotnet test tests/WasmFrontend.Tests --nologo
```

**Automated tests:**
```powershell
dotnet test tests/ApiGateway.Tests --nologo
dotnet test tests/WasmFrontend.Tests --nologo
```

**Manual test — requires all nine backend services + API Gateway:**

The API Gateway serves the Blazor WASM frontend at `http://localhost:5000`. The first `dotnet run` (or `dotnet build`) of the ApiGateway automatically publishes the WasmFrontend and copies its output to the gateway's `wwwroot/` directory — this adds ~1–2 minutes to the initial build.

| Service | Port | Start Command |
|---------|------|---------------|
| IdentityService | 5041 | `dotnet run --project src/IdentityService` |
| MenuService | 5052 | `dotnet run --project src/MenuService` |
| OrderService | 5063 | `dotnet run --project src/OrderService` |
| PaymentService | 5074 | `dotnet run --project src/PaymentService` |
| KitchenService | 5085 | `dotnet run --project src/KitchenService` |
| DeliveryService | 5096 | `dotnet run --project src/DeliveryService` |
| FeedbackService | 5007 | `dotnet run --project src/FeedbackService` |
| NotificationService | 5018 | `dotnet run --project src/NotificationService` |
| ReceiptService | 5029 | `dotnet run --project src/ReceiptService` |
| **ApiGateway** | **5000** | `dotnet run --project src/ApiGateway` |

**Step 2: Run the manual test app with the gateway URL as the 10th argument**

```powershell
dotnet run --project tests/ManualTestApp -- http://localhost:5041 http://localhost:5052 http://localhost:5063 http://localhost:5074 http://localhost:5085 http://localhost:5096 http://localhost:5007 http://localhost:5018 http://localhost:5029 http://localhost:5000
```

When the gateway URL (10th arg) is provided, the test app adds a **Phase 6 — API Gateway** section that runs REST endpoint tests through the gateway, including:
| Test | Auth | Endpoint |
|------|------|----------|
| Health check | No | `GET /health` |
| Register via gateway | No | `POST /api/auth/register` |
| Login via gateway | No | `POST /api/auth/login` |
| Get menu via gateway | No | `GET /api/menu` |
| Create order via gateway | JWT | `POST /api/orders` |
| Get order via gateway | JWT | `GET /api/orders/{id}` |
| Process payment via gateway | JWT | `POST /api/payments` |
| Kitchen prepare via gateway | JWT | `POST /api/kitchen/{id}/prepare` |
| Submit feedback via gateway | JWT | `POST /api/feedback` |
| Average rating via gateway | No | `GET /api/feedback/rating/average` |
| Get receipt via gateway | JWT | `GET /api/receipts/{id}` |
| Get notifications via gateway | JWT | `GET /api/notifications/{customerId}` |

**Test credentials used:** Same random user created by the gRPC backend tests (shared `userId`/`token`).

Open `http://localhost:5000` in a browser to use the Blazor WebAssembly frontend. The UI provides pages for:
- Home (how-it-works + average rating)
- Register / Login (JWT persisted in localStorage)
- Menu browsing by category with add-to-cart
- Shopping cart with quantity controls
- Checkout with delivery address
- Order status tracking (auto-refreshes every 10s)
- Delivery tracking
- Receipt viewer (iframe + print)
- Feedback submission (1–5 star rating)
- My Orders history with status badges

---

### Phase 7 — Docker & Docker Compose

When implemented, add:
- Docker Compose integration health checks as test steps
- Container startup/shutdown test scripts in `tests/docker/`

---

### Phase 8 — Kubernetes Manifests

When implemented, add:
- Conftest or kubeconform policy tests in `tests/k8s/`
- Deployment verification scripts

---

## Adding New Tests

### Unit Test Pattern

```csharp
using BurgerIAM.TestUtilities;
using Xunit;

namespace ServiceName.Tests;

public class ServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task MethodName_Scenario_ExpectedResult()
    {
        var db = CreateDbContext();
        var service = new ServiceClass(db);

        var response = await service.Method(request, new MockServerCallContext());

        Assert.NotNull(response);
    }
}
```

### Integration Test Pattern (Event Bus)

```csharp
using BurgerIAM.Shared.Events;
using BurgerIAM.TestUtilities;

[Fact]
public async Task Event_Triggers_Handler()
{
    var bus = new InMemoryEventBus();

    await bus.SubscribeAsync<SomeEvent>(async (@event, ct) =>
    {
        // assert or signal
    });

    await bus.PublishAsync(new SomeEvent { ... });
}
```

---

## Test Count Summary

| Phase | Project | Test Count |
|---|---|---|
| 1 | BurgerIAM.Shared.Tests | 14 |
| 1 | BurgerIAM.EventBus.Tests | 5 |
| 2 | IdentityService.Tests | 6 |
| 2 | MenuService.Tests | 5 |
| 3 | OrderService.Tests | 7 |
| 3 | PaymentService.Tests | 6 |
| 3 | Integration.Tests | 6 |
| 4 | KitchenService.Tests | 9 |
| 4 | DeliveryService.Tests | 9 |
| 5 | FeedbackService.Tests | 7 |
| 5 | NotificationService.Tests | 6 |
| 5 | ReceiptService.Tests | 2 |
| 6 | ApiGateway.Tests | 14 |
| 6 | WasmFrontend.Tests | 20 |
| | **Total** | **116** |
