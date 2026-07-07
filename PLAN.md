# BurgerIAM - Development Plan

> **Reminder**: Always commit changes after every code change cycle — build, test, stage, commit.

## 1. Architecture Overview

Microservice-based fast food ordering system with **.NET 9**, communicating via **gRPC** (sync) and **RabbitMQ** (async/events). Each service owns its **SQLite** database. Frontend is **Blazor WebAssembly** hosted by the API Gateway. Deployment via **Docker** + **Kubernetes** with **Gateway API** ingress.

### High-Level Data Flow

```
User → Web Frontend → API Gateway → [Order Service] → (events) → [Kitchen Service] → (events) → [Delivery Service]
                                        ↕ gRPC                              ↕ gRPC
                                [Menu Service]  [Payment Service]   [Notification Service]
                                        ↓                                      ↓
                                [Receipt Service]                      [Feedback Service]
```

### Event Bus Flow (RabbitMQ)

```
Order Placed       → Kitchen, Payment, Notification, Receipt
Payment Confirmed  → Kitchen, Notification
Order Ready        → Delivery, Notification
Delivered          → Notification, Feedback
Feedback Submitted → Notification
```

---

## 2. Microservices Breakdown

### 2.1 API Gateway
- **Role**: Single entry point, authentication gateway, REST-to-gRPC proxy
- **Tech**: ASP.NET Core Minimal API + gRPC client stubs + JWT auth
- **Database**: None (routes requests to backend gRPC services)
- **Endpoints**:
  - `POST /api/auth/login` → Identity Service (gRPC)
  - `POST /api/auth/register` → Identity Service (gRPC)
  - `GET /api/menu` → Menu Service (gRPC)
  - `GET /api/menu/{id}` → Menu Service (gRPC)
  - `GET /api/menu/{id}/availability?isAvailable=` → Menu Service (gRPC)
  - `POST /api/orders` → Order Service (gRPC) [Auth]
  - `GET /api/orders/{id}` → Order Service (gRPC) [Auth]
  - `GET /api/orders/{id}/status` → Order Service (gRPC) [Auth]
  - `POST /api/orders/{id}/cancel` → Order Service (gRPC) [Auth]
  - `GET /api/orders/my/{customerId}` → Order Service (gRPC) [Auth]
  - `POST /api/payments` → Payment Service (gRPC) [Auth]
  - `GET /api/payments/{id}` → Payment Service (gRPC) [Auth]
  - `POST /api/payments/{id}/refund` → Payment Service (gRPC) [Auth]
  - `GET /api/kitchen/pending` → Kitchen Service (gRPC) [Auth]
  - `POST /api/kitchen/{orderId}/prepare` → Kitchen Service (gRPC) [Auth]
  - `POST /api/kitchen/{orderId}/ready` → Kitchen Service (gRPC) [Auth]
  - `GET /api/delivery/{orderId}` → Delivery Service (gRPC) [Auth]
  - `GET /api/delivery/driver/{driverId}` → Delivery Service (gRPC) [Auth]
  - `POST /api/feedback` → Feedback Service (gRPC) [Auth]
  - `GET /api/feedback/{orderId}` → Feedback Service (gRPC) [Auth]
  - `GET /api/feedback/rating/average` → Feedback Service (gRPC)
  - `GET /api/receipts/{orderId}` → Receipt Service (HTTP/plain) [Auth]
  - `GET /api/notifications/{customerId}` → Notification Service (gRPC) [Auth]
  - `POST /api/notifications/{id}/read` → Notification Service (gRPC) [Auth]
  - `GET /api/notifications/{customerId}/unread-count` → Notification Service (gRPC) [Auth]
- **Frontend (Web)**: Serves Blazor WASM static files; Blazor calls Gateway REST endpoints
- **Mobile**: All REST endpoints designed to be consumed by .NET MAUI Android app as well (same API surface, no special mobile-only endpoints needed)

### 2.2 Identity Service
- **Role**: User registration, login, JWT token generation
- **Tech**: ASP.NET Core gRPC + SQLite (users table)
- **gRPC Service**: `IdentityService` (Login, Register, ValidateToken)
- **Database Tables**: `Users`, `Roles`

### 2.3 Menu Service
- **Role**: Manage menu items, categories, prices, availability
- **Tech**: ASP.NET Core gRPC + SQLite
- **gRPC Service**: `MenuService` (GetMenuItems, GetItem, UpdateAvailability)
- **Events Published**: `MenuItemUpdated` (price/availability change)
- **Database Tables**: `Categories`, `MenuItems`

### 2.4 Order Service
- **Role**: Orchestrate order lifecycle; create, update, track orders
- **Tech**: ASP.NET Core gRPC + SQLite
- **gRPC Service**: `OrderService` (CreateOrder, GetOrder, GetOrderStatus, CancelOrder)
- **Events Published**: `OrderPlaced`, `OrderCancelled`, `OrderStatusChanged`
- **Events Consumed**: `PaymentConfirmed`, `OrderReady`, `OrderDelivered`
- **Database Tables**: `Orders`, `OrderItems`, `OrderStatusHistory`

### 2.5 Payment Service
- **Role**: Process payments, handle refunds
- **Tech**: ASP.NET Core gRPC + SQLite
- **gRPC Service**: `PaymentService` (ProcessPayment, RefundPayment, GetPaymentStatus)
- **Events Published**: `PaymentConfirmed`, `PaymentFailed`
- **Events Consumed**: `OrderPlaced` (triggers payment processing)
- **Database Tables**: `Payments`

### 2.6 Kitchen Service
- **Role**: Manage order preparation, cooking workflow, status updates
- **Tech**: ASP.NET Core gRPC + SQLite
- **gRPC Service**: `KitchenService` (GetPendingOrders, UpdateOrderStatus, AssignStation)
- **Events Published**: `OrderInProgress`, `OrderReady`
- **Events Consumed**: `OrderPlaced`, `PaymentConfirmed`
- **Database Tables**: `KitchenOrders`, `CookingStations`

### 2.7 Delivery Service
- **Role**: Assign drivers, track delivery status
- **Tech**: ASP.NET Core gRPC + SQLite
- **gRPC Service**: `DeliveryService` (AssignDelivery, UpdateDeliveryStatus, GetDeliveryStatus)
- **Events Published**: `OrderOutForDelivery`, `OrderDelivered`
- **Events Consumed**: `OrderReady`
- **Database Tables**: `Deliveries`, `Drivers`

### 2.8 Notification Service
- **Role**: Send email/SMS/push notifications for order events
- **Tech**: ASP.NET Core (background worker) + RabbitMQ consumer
- **Events Consumed**: `OrderPlaced`, `PaymentConfirmed`, `OrderInProgress`, `OrderReady`, `OrderOutForDelivery`, `OrderDelivered`, `FeedbackSubmitted`
- **Database Tables**: `Notifications`, `NotificationTemplates`
- **Note**: Simulated — logs notifications to console/database

### 2.9 Receipt Service
- **Role**: Generate and store receipts (HTML/PDF)
- **Tech**: ASP.NET Core Web API + SQLite (store receipt metadata)
- **Endpoints**: `GET /api/receipts/{orderId}` (returns receipt data)
- **Events Consumed**: `PaymentConfirmed` (triggers receipt generation)
- **Database Tables**: `Receipts`

### 2.10 Feedback Service
- **Role**: Collect and manage customer feedback
- **Tech**: ASP.NET Core gRPC + SQLite
- **gRPC Service**: `FeedbackService` (SubmitFeedback, GetFeedback, GetAverageRating)
- **Events Published**: `FeedbackSubmitted`
- **Events Consumed**: `OrderDelivered` (prompts feedback request)
- **Database Tables**: `Feedbacks`

---

## 3. Shared Infrastructure

### 3.1 Shared NuGet Packages / Class Libraries
- `BurgerIAM.Shared` — Common DTOs, enums (OrderStatus, PaymentStatus), event message contracts
- `BurgerIAM.EventBus` — RabbitMQ abstraction (publish/subscribe interfaces, connection management)
- `BurgerIAM.Protos` — Shared `.proto` files for gRPC contracts

### 3.2 Shared Proto Definitions (gRPC)
Standard `.proto` files define all service contracts. Each service references the shared proto package.

### 3.3 RabbitMQ Event Contracts
Standard event message classes in `BurgerIAM.Shared.Events`:
- `OrderPlacedEvent` — OrderId, CustomerId, Items[], TotalAmount, Timestamp
- `PaymentConfirmedEvent` — OrderId, PaymentId, Amount, Timestamp
- `PaymentFailedEvent` — OrderId, PaymentId, Reason, Timestamp
- `OrderInProgressEvent` — OrderId, EstimatedReadyTime, Timestamp
- `OrderReadyEvent` — OrderId, Timestamp
- `OrderOutForDeliveryEvent` — OrderId, DriverId, EstimatedDeliveryTime, Timestamp
- `OrderDeliveredEvent` — OrderId, Timestamp
- `OrderCancelledEvent` — OrderId, Reason, Timestamp
- `FeedbackSubmittedEvent` — OrderId, Rating, Timestamp

---

## 4. Development Phases

### Phase 1: Foundation & Shared Infrastructure
1. Create solution structure with `dotnet new sln`
2. Create `BurgerIAM.Shared` class library — enums, DTOs, event contracts
3. Create `BurgerIAM.EventBus` class library — RabbitMQ publish/subscribe abstractions
4. Create `BurgerIAM.Protos` — shared `.proto` files and code generation
5. Verify solution builds and all tests pass
6. Commit: `git add . && git commit -m "Phase 1: Foundation & shared infrastructure"`

### Phase 2: Identity & Menu Services ✅
1. Implement **Identity Service** (gRPC + SQLite)
   - User registration, login, JWT generation
   - Unit tests
2. Implement **Menu Service** (gRPC + SQLite)
   - Menu CRUD, categories, availability
   - Unit tests
3. Dockerfile for each service
4. Commit

### Phase 3: Order & Payment Orchestration ✅
1. ✅ Implement **Order Service** (gRPC + SQLite + EventBus publisher)
   - Order creation, status tracking
   - Publishes `OrderPlacedEvent`
   - Unit tests (7 tests)
2. ✅ Implement **Payment Service** (gRPC + SQLite + EventBus pub/sub)
   - Payment processing, publishes `PaymentConfirmed`/`PaymentFailed`
   - Consumes `OrderPlacedEvent` (via `EventBusHostedService`)
   - Unit tests (6 tests)
3. ✅ Wire RabbitMQ between Order and Payment (dev fallback: `InMemoryEventBus`)
4. ✅ Integration test: place order → payment processed (4 event flow tests)
5. ✅ Dockerfiles for both services
6. ✅ Commit

### Phase 4: Kitchen & Delivery ✅
1. Implement **Kitchen Service** (gRPC + SQLite + EventBus pub/sub)
   - Order preparation workflow
   - Consumes `PaymentConfirmedEvent`, publishes `OrderReadyEvent`
   - Unit tests
2. Implement **Delivery Service** (gRPC + SQLite + EventBus pub/sub)
   - Driver assignment, delivery tracking
   - Consumes `OrderReadyEvent`, publishes `OrderDeliveredEvent`
   - Unit tests
3. Dockerfiles
4. Commit

### Phase 5: Notification, Receipt & Feedback ✅
1. Implement **Notification Service** (background worker + EventBus consumer)
   - Consumes all order lifecycle events, logs/simulates notifications
   - Unit tests
2. Implement **Receipt Service** (Web API + EventBus consumer)
   - Generates receipt on `PaymentConfirmedEvent`
   - Unit tests
3. Implement **Feedback Service** (gRPC + SQLite + EventBus pub/sub)
   - Customer feedback submission
   - Prompts on `OrderDeliveredEvent`
   - Unit tests
4. Dockerfiles
5. Commit

### Phase 6: API Gateway & Web Frontend ✅
1. ✅ Implement **API Gateway** (ASP.NET Core Minimal API + gRPC client stubs)
   - 25+ REST endpoints proxying to backend gRPC services
   - JWT auth middleware (validates tokens for protected endpoints)
   - CORS support for Blazor WASM
   - gRPC client stubs for all 9 backend services
   - Unit tests (14 endpoint registration/config tests)
2. ✅ Implement **Blazor WebAssembly Frontend** (10 pages)
   - Home page with how-it-works and average rating
   - Login/Register pages with localStorage JWT persistence
   - Menu browsing by category with add-to-cart
   - Shopping cart with quantity controls and event-based updates
   - Checkout with delivery address and order summary
   - Order status tracking with auto-refreshing timeline (10s polling)
   - Delivery tracking display
   - Receipt viewer (iframe with print support)
   - Feedback submission (1-5 star rating + comment)
   - My Orders history with status badges
   - Custom AuthenticationStateProvider for Blazor auth
3. ✅ Unit tests: 34 (14 ApiGateway + 20 WasmFrontend)
4. ✅ Dockerfile for API Gateway (multi-stage build, port 5000)
5. ✅ Commit

### Phase 7: Docker & Container Orchestration ✅
1. ✅ `.dockerignore` created for each service
2. ✅ `Dockerfile` created for each service (multi-stage build)
3. ✅ PowerShell scripts for local container management (used instead of docker-compose):
   - `Build-Images.ps1` — builds all 11 service images
   - `Start-Containers.ps1` — starts all containers with optional RabbitMQ support
   - `Stop-Containers.ps1` — stops all containers
   - `Start-FullTest.ps1` — runs all services locally for development
   - `Stop-FullTest.ps1` — stops local development services
4. ✅ Full end-to-end testing verified with both InMemoryEventBus and RabbitMQ
5. ✅ All images pushed to Docker Hub as `cotters07/burgeriam-*:latest`
6. ✅ Commit

### Phase 8: Kubernetes Manifests ✅
1. ✅ Created `k8s/` directory with all manifests (17 files)
2. ✅ **Namespace**: `burgeriam` (`00-namespace.yaml`)
3. ✅ **Secrets**: JWT key + RabbitMQ credentials (`01-secrets.yaml`)
4. ✅ **PersistentVolumeClaims**: 9 PVCs (1Gi each) for SQLite databases (`02-persistent-volumes.yaml`)
5. ✅ **RabbitMQ**: Deployment + ClusterIP service with health probes (`03-rabbitmq.yaml`)
6. ✅ **All 9 backend services**: Deployment + ClusterIP Service per service:
   - Identity, Menu, Order, Payment, Kitchen, Delivery, Feedback, Notification, Receipt
   - Resource requests/limits, liveness + readiness probes
   - PVC mounts for SQLite persistence
   - RabbitMQ event bus enabled via env vars (`EventBus__ConnectionString`)
7. ✅ **API Gateway**: Deployment + ClusterIP service with all backend URLs configured
8. ✅ **Wasm Frontend**: Deployment + **LoadBalancer** Service (HTTP port 80)
   - Kubernetes-compatible nginx ConfigMap (kube-dns resolver instead of Docker DNS)
9. ✅ **Deploy script** (`deploy.ps1`): applies manifests in dependency order, optional `-WaitForReady`
10. ✅ **Remove script** (`remove.ps1`): namespace deletion with confirmation prompt, optional `-DeletePVCs`
11. ✅ All images pushed to Docker Hub (`cotters07/burgeriam-*:latest`) and referenced in manifests
12. ✅ Branch `Kubernetes` created locally and remotely on GitHub
13. ✅ Committed

### Phase 10: .NET MAUI Android App *(deferred — on request)*
> **Note**: This phase will only be started when explicitly instructed. All design decisions in earlier phases (API surface, auth flow, response shapes) must keep mobile consumption in mind.

1. Create `src/MauiApp/` — .NET MAUI project targeting Android
2. Implement shared HTTP client layer consuming same Gateway REST endpoints
3. Implement mobile views:
   - Login/Register
   - Menu browsing + cart
   - Order placement + payment
   - Order status tracking
   - Delivery tracking
   - Receipt view
   - Feedback submission
4. Platform-specific considerations: push notifications (via Notification Service), biometric auth, offline menu caching
5. Dockerfile (if deploying mobile backend services) or publish to store
6. Commit

### Phase 9: Testing & Documentation
1. Write comprehensive unit tests for all services (xUnit + Moq)
2. Write integration tests for gRPC endpoints
3. Write integration tests for event bus message flows
4. End-to-end test: registration → login → browse menu → place order → payment → kitchen → delivery → receipt → feedback
5. Commit

---

## 5. Project Structure

```
E:\Repos\BurgerIAM\
├── AGENTS.md
├── PLAN.md
├── BurgerIAM.sln
├── .gitignore
├── docker-compose.yml
│
├── src/
│   ├── ApiGateway/
│   │   ├── Dockerfile
│   │   └── ...
│   ├── IdentityService/
│   │   ├── Dockerfile
│   │   └── ...
│   ├── MenuService/
│   │   ├── Dockerfile
│   │   └── ...
│   ├── OrderService/
│   │   ├── Dockerfile
│   │   └── ...
│   ├── PaymentService/
│   │   ├── Dockerfile
│   │   └── ...
│   ├── KitchenService/
│   │   ├── Dockerfile
│   │   └── ...
│   ├── DeliveryService/
│   │   ├── Dockerfile
│   │   └── ...
│   ├── NotificationService/
│   │   ├── Dockerfile
│   │   └── ...
│   ├── ReceiptService/
│   │   ├── Dockerfile
│   │   └── ...
│   ├── FeedbackService/
│   │   ├── Dockerfile
│   │   └── ...
│   ├── WebFrontend/            (Blazor WASM)
│   │   ├── Dockerfile
│   │   └── ...
│   ├── MauiApp/                (.NET MAUI Android — Phase 10, on request)
│   │   └── ...
│   │
│   ├── BurgerIAM.Shared/       (classlib)
│   │   └── ...
│   ├── BurgerIAM.EventBus/     (classlib)
│   │   └── ...
│   └── BurgerIAM.Protos/       (proto files)
│       └── ...
│
├── tests/
│   ├── IdentityService.Tests/
│   ├── MenuService.Tests/
│   ├── OrderService.Tests/
│   ├── PaymentService.Tests/
│   ├── KitchenService.Tests/
│   ├── DeliveryService.Tests/
│   ├── NotificationService.Tests/
│   ├── ReceiptService.Tests/
│   ├── FeedbackService.Tests/
│   └── Integration.Tests/
│
└── k8s/
    ├── 00-namespace.yaml            # burgeriam namespace
    ├── 01-secrets.yaml              # JWT key + RabbitMQ credentials
    ├── 02-persistent-volumes.yaml   # 9 PVCs (1Gi each)
    ├── 03-rabbitmq.yaml            # RabbitMQ + ClusterIP service
    ├── 04-identity-service.yaml     # Deployment + ClusterIP
    ├── 05-menu-service.yaml         # Deployment + ClusterIP
    ├── 06-order-service.yaml        # Deployment + ClusterIP
    ├── 07-payment-service.yaml      # Deployment + ClusterIP
    ├── 08-kitchen-service.yaml      # Deployment + ClusterIP
    ├── 09-delivery-service.yaml     # Deployment + ClusterIP
    ├── 10-feedback-service.yaml     # Deployment + ClusterIP
    ├── 11-notification-service.yaml # Deployment + ClusterIP
    ├── 12-receipt-service.yaml      # Deployment + ClusterIP
    ├── 13-api-gateway.yaml          # Deployment + ClusterIP
    ├── 14-wasm-frontend.yaml        # Deployment + LoadBalancer + nginx ConfigMap
    ├── deploy.ps1                   # Deploy all manifests
    └── remove.ps1                   # Teardown all resources
```

---

## 6. Technology Stack Summary

| Component | Technology |
|-----------|-----------|
| Runtime | .NET 9 |
| Database | SQLite (each service) |
| ORM | Entity Framework Core |
| Sync Communication | gRPC |
| Async Communication | RabbitMQ |
| Frontend (Web) | Blazor WebAssembly |
| Frontend (Mobile) | .NET MAUI (Android) — developed last, on request |
| API Gateway | ASP.NET Core Minimal API + gRPC client stubs |
| Auth | JWT (Identity Service) |
| Containerization | Docker (multi-stage builds) |
| Orchestration | Kubernetes (Gateway API) |
| Testing | xUnit + Moq + FluentAssertions |
| Proto Generation | protobuf-net.Grpc / Grpc.AspNetCore |

---

## 7. Service Communication Matrix

```
Service A → Service B   | Protocol   | Method
-------------------------|------------|-------------------------
Gateway → Identity       | gRPC       | Login, Register, Validate
Gateway → Menu           | gRPC       | GetMenuItems, GetItem
Gateway → Order          | gRPC       | CreateOrder, GetOrder, etc.
Gateway → Payment        | gRPC       | ProcessPayment
Gateway → Feedback       | gRPC       | SubmitFeedback
Gateway → Receipt        | HTTP/REST  | GetReceipt
Order → (EventBus) → Payment   | RabbitMQ | OrderPlaced → triggers payment
Payment → (EventBus) → Kitchen  | RabbitMQ | PaymentConfirmed → start cooking
Kitchen → (EventBus) → Delivery | RabbitMQ | OrderReady → assign driver
Delivery → (EventBus) → Notif   | RabbitMQ | OrderDelivered → notify user
Delivery → (EventBus) → Feedback| RabbitMQ | OrderDelivered → prompt feedback
Payment → (EventBus) → Receipt  | RabbitMQ | PaymentConfirmed → generate receipt
All → (EventBus) → Notification | RabbitMQ | Status updates → notify user
```

---

## 8. Key Design Decisions

1. **SQLite per service** — lightweight, zero-config, each service independently scalable
2. **gRPC for sync** — strongly-typed contracts, high performance, native .NET support
3. **RabbitMQ for async** — reliable, durable, supports pub/sub and competing consumers
4. **BI-directional event flow** — services react to events without direct coupling
5. **Minimal API Gateway** — ASP.NET Core Minimal API with direct gRPC client stubs, no reverse proxy needed
6. **JWT auth at Gateway** — centralized authentication, downstream services trust Gateway-issued tokens
7. **Blazor WASM** — .NET-based frontend, shared models with backend, single language stack
8. **Multi-stage Docker builds** — smaller images, faster deployments
9. **Gateway API** — modern Kubernetes ingress, richer routing than Ingress v1
10. **PowerShell orchestration** — custom PowerShell scripts replace docker-compose for local container management
11. **Docker Hub registry** — all images published to `cotters07/burgeriam-*` for K8s pull access
12. **Kubernetes-first event bus** — manifests configured for RabbitMQ by default (not InMemoryEventBus)

---

## 9. Health Checks & Observability

- Each service exposes `/health` (liveness) and `/health/ready` (readiness) endpoints
- Services register health checks for SQLite connectivity, RabbitMQ connection (where applicable)
- Kubernetes probes configured in each Deployment manifest
- Logging: structured logging with Serilog to stdout (container-friendly)
- OpenTelemetry tracing planned for future enhancement

---

## 10. Configuration Management

- All services use `appsettings.json` with environment variable overrides
- Connection strings, RabbitMQ hosts, JWT secrets via environment variables
- Kubernetes ConfigMaps for non-sensitive config, Secrets for sensitive values
- Default development settings committed; production overrides via env vars
