# Issues & TODO List

> Auto-generated from codebase audit. Treat this as the master TODO list.

---

## Critical

### 1. Missing Kubernetes manifests
- **File**: `k8s/` (does not exist)
- **Problem**: AGENTS.md requires Kubernetes manifests (deployments, services, Gateway API ingress) for all services. PLAN.md Phase 8 describes the full structure. Nothing exists.
- **Required**: Create `k8s/` with namespace, Gateway API resources, deployments + services for all 10 services, ConfigMaps, Secrets, PVCs, RabbitMQ.

### 2. Missing docker-compose.yml
- **File**: `docker-compose.yml` (does not exist)
- **Problem**: PLAN.md Phase 7 requires a root `docker-compose.yml` for local development with all services + RabbitMQ + SQLite volumes.
- **Required**: Create `docker-compose.yml` with all 10 services and RabbitMQ.

### 3. WasmFrontend Dockerfile is broken
- **File**: `src/WasmFrontend/Dockerfile:13`
- **Problem**: `ENTRYPOINT ["dotnet", "WasmFrontend.dll"]` — Blazor WASM produces static web assets, not a runnable assembly. The file even has a comment acknowledging this (`# Use dotnet serve or nginx`).
- **Required**: Replace with nginx-based serving or use `dotnet serve` tool. Alternatively, remove the standalone Dockerfile since WasmFrontend is published into ApiGateway's `wwwroot/`.

### 4. Port mismatch: Dockerfiles vs appsettings.json
- **Files**: All `src/*/Dockerfile` EXPOSE lines vs `src/ApiGateway/appsettings.json:15-23`
- **Problem**: Every service Dockerfile exposes a different port than what ApiGateway's appsettings.json configures for the service URL:
  | Service | Dockerfile EXPOSE | appsettings URL |
  |---------|------------------|-----------------|
  | Identity | 5001 | 5041 |
  | Menu | 5002 | 5052 |
  | Order | 5003 | 5063 |
  | Payment | 5004 | 5074 |
  | Kitchen | 5005 | 5085 |
  | Delivery | 5006 | 5096 |
  | Notification | 5018 | 5018 |
  | Feedback | 5007 | 5007 |
  | Receipt | 5029 | 5029 |
- **Required**: Align Dockerfile ports with appsettings.json URLs (or vice versa). The appsettings values match test harness ports and should be canonical.

### 5. `GetServiceUrl` fallback generates invalid URLs
- **File**: `src/ApiGateway/Program.cs:41`
- **Problem**: `string GetServiceUrl(string name) => servicesConfig[name] ?? $"http://localhost:5{name}";` — when a config key is missing, this produces garbage like `http://localhost:5Identity`.
- **Required**: Remove the fallback or make it return a sensible default.

### 6. Receipt generated twice (dual invocation)
- **Files**: `src/ApiGateway/Program.cs:218` + `src/ReceiptService/EventBusHostedService.cs`
- **Problem**: ApiGateway's `POST /api/orders` calls ReceiptService directly via HTTP to create a receipt. Simultaneously, the PaymentService publishes `PaymentConfirmedEvent`, and ReceiptService's `EventBusHostedService` also handles it by creating the same receipt. This produces duplicate receipts.
- **Required**: Remove the direct HTTP call from ApiGateway (let the event-driven flow handle it), or remove the EventBus handler.

### 7. FeedbackService missing EventBusHostedService
- **File**: `src/FeedbackService/` (no `EventBusHostedService.cs`)
- **Problem**: PLAN.md states FeedbackService should consume `OrderDeliveredEvent` to prompt customer feedback. No hosted service or event subscription exists. `Program.cs` doesn't register one either.
- **Required**: Create `EventBusHostedService` that subscribes to `OrderDeliveredEvent` and creates a feedback prompt/notification.

### 8. `InMemoryEventBus.PublishAsync` doesn't await handlers

---

## Completed UX Polish

### 18. Delivery tracking page text hard to read (dark theme contrast)
- **Status**: ✅ Fixed
- **Commits**: `2949d96`, `8670e5b`, `ae81d4b`, `1cef55b`, `f0719f6`
- **Problem**: Text on dark cards was nearly invisible — card backgrounds at 4% opacity barely differentiated from page background. Timeline labels and descriptions used 40% white (`text-muted`). Raw ISO datetime strings displayed for estimated delivery times.
- **Fix**: 
  - Card background 4% → 7%, borders 8% → 12% for visible card containers
  - Added `.card-section-title` class (amber accent `var(--accent)` with bottom border) for all card section headings across OrderStatus, Checkout, Cart, Feedback, DeliveryTracking
  - Added `.detail-label` (70% white, weight 600) and `.detail-value` (white, weight 500) pattern for label/value pairs
  - Formatted `EstimatedDeliveryTime` with date formatter (was raw ISO string)
  - Bumped all `text-muted` (45%) content references to `text-secondary` (70%): auto-refresh text, empty states, quantities, metadata
  - Completed timeline titles changed from text-muted to green (#2ecc71)

---

### ~~17. Menu/page content invisible after UX redesign~~ ✅ FIXED
- **Status**: ✅ Fully fixed — all pages patched
- **Commit**: `497c91d` (partial), `<current>` (full)
- **Root cause**: `OnAfterRenderAsync` guarded `observeNewElements()` behind `if (firstRender)`. On first render, only skeleton loaders exist (no `.animate-on-scroll`). When real data loads and items render on subsequent passes, `firstRender` is `false`, so elements never get observed by the IntersectionObserver and remain at `opacity: 0`.
- **Fix**: Removed the `if (firstRender)` guard from all remaining pages: `MyOrders.razor`, `Cart.razor`, `DeliveryTracking.razor`, `Feedback.razor`, `Login.razor`, `Receipt.razor`, `Register.razor` (previously fixed: `Menu.razor`, `Home.razor`, `Checkout.razor`, `OrderStatus.razor`). The JS `observeNewElements()` already deduplicates with `:not(.animate-visible)`.
- **File**: `src/BurgerIAM.EventBus/InMemoryEventBus.cs:18`
- **Problem**: `handler(@event, cancellationToken)` returns a `Task` that is never `await`ed. Handlers run fire-and-forget. Exceptions in handlers are silently swallowed.
- **Required**: `await handler(@event, cancellationToken)` or capture all tasks and `await Task.WhenAll(...)`.

---

## Major

### 9. OrderService has duplicate payment-confirmation paths
- **Files**: `src/ApiGateway/Program.cs:207-213` + `src/OrderService/Services/EventBusHostedService.cs:21-33`
- **Problem**: ApiGateway calls `POST /api/internal/orders/{id}/confirm-payment` synchronously to update order status to "Paid". But `OrderService.EventBusHostedService` also listens for `PaymentConfirmedEvent` and applies the same status update. This creates a race condition and redundant work.
- **Required**: Pick one path: either keep the synchronous HTTP call (and remove the EventBus subscription) or keep the event-driven path (and remove the HTTP call).

### 10. NotificationService creates notifications with empty CustomerId
- **File**: `src/NotificationService/Services/NotificationGrpcService.cs:62`
- **Problem**: `HandleOrderDelivered` sets `CustomerId = string.Empty`. The notification is stored but cannot be retrieved by any customer since all queries filter by `CustomerId`.
- **Required**: The `OrderDeliveredEvent` does not carry `CustomerId`. Either add `CustomerId` to the event, or look it up from another source.

### 11. No `/health/ready` readiness endpoints
- **Files**: All `src/*/Program.cs`
- **Problem**: PLAN.md states each service exposes both `/health` (liveness) and `/health/ready` (readiness) with health checks for SQLite and RabbitMQ. Only `/health` exists on all services.
- **Required**: Add `/health/ready` endpoints with proper health checks (SQLite connectivity, RabbitMQ connection where applicable).

### 12. ApiGateway Dockerfile copies entire repo
- **File**: `src/ApiGateway/Dockerfile:3` (same pattern in all Dockerfiles)
- **Problem**: `COPY . .` copies all source code into every Docker image. Each image ends up containing all 10+ services' code, the tests directory, etc., making images unnecessarily large.
- **Required**: Use `.dockerignore` to exclude unnecessary directories (`tests/`, `*.db`, `bin/`, `obj/`) or restructure the build context.

---

## Minor

### 13. DEBUG.md has duplicate content
- **File**: `DEBUG.md`
- **Problem**: Multiple `# Debug Log` headings. Content at lines 50-91 is duplicate/out-of-place (a copy of the file header and "Running Services for Testing" section appears in the middle of the file).
- **Required**: Clean up the duplicate content and ensure each entry has a single date-stamped heading.

### 14. SQLite .db files appear in service directories
- **Files**: `src/IdentityService/identity.db`, `src/OrderService/order.db`, `src/PaymentService/payment.db`, `src/KitchenService/kitchen.db`, `src/DeliveryService/delivery.db`, `src/FeedbackService/feedback.db`, `src/NotificationService/notifications.db`, `src/ReceiptService/receipts.db`
- **Problem**: These look like they were generated during local development. The `.gitignore` has `*.db` but if they were ever staged or committed, they're being tracked.
- **Required**: Verify with `git rm --cached` if tracked, add to `.gitignore` if needed, and delete the files.

### 15. `POST /api/feedback` returns wrong location path
- **File**: `src/ApiGateway/Program.cs:363`
- **Problem**: `Results.Created($"/api/feedback/{request.OrderId}", response)` uses `request.OrderId` in the URL path instead of the returned `response.FeedbackId`.
- **Required**: Change to `$"/api/feedback/{response.FeedbackId}"`.

### 16. Phase 7 and Phase 8 from PLAN.md are unimplemented
- **File**: `PLAN.md`
- **Problem**: PLAN.md lists Phase 7 (Docker Compose) and Phase 8 (Kubernetes Manifests) as development phases, but neither has been implemented. These are requirements from AGENTS.md.
- **Required**: Implement Phase 7 (docker-compose.yml) and Phase 8 (k8s/ manifests).
