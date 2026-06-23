# Debug Log

This file documents issues encountered during development and their resolutions. Refer here when troubleshooting similar problems.

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
