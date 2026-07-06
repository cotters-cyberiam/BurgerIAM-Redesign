<#
.SYNOPSIS
    Builds Docker images for all BurgerIAM microservices.
.DESCRIPTION
    Builds each service's Docker image individually using its Dockerfile.
    Images are tagged as burgeriam/<service-name>:latest.
.PARAMETER Services
    Specific services to build (default: all). Separate multiple with commas.
    Example: -Services "identity-service,menu-service"
.PARAMETER Tag
    Image tag suffix (default: latest).
    Example: -Tag "v1.0"
.PARAMETER NoCache
    Bypass Docker build cache for a clean build.
.EXAMPLE
    .\Build-Images.ps1
    .\Build-Images.ps1 -Services "api-gateway,order-service"
    .\Build-Images.ps1 -Tag "v1.0" -NoCache
#>

param(
    [string]$Services = "all",
    [string]$Tag = "latest",
    [switch]$NoCache
)

$ErrorActionPreference = "Stop"
$repoRoot = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }

$serviceList = @(
    @{ Name = "identity-service";    Project = "src/IdentityService";    Dockerfile = "src/IdentityService/Dockerfile" }
    @{ Name = "menu-service";        Project = "src/MenuService";        Dockerfile = "src/MenuService/Dockerfile" }
    @{ Name = "order-service";       Project = "src/OrderService";       Dockerfile = "src/OrderService/Dockerfile" }
    @{ Name = "payment-service";     Project = "src/PaymentService";     Dockerfile = "src/PaymentService/Dockerfile" }
    @{ Name = "kitchen-service";     Project = "src/KitchenService";     Dockerfile = "src/KitchenService/Dockerfile" }
    @{ Name = "delivery-service";    Project = "src/DeliveryService";    Dockerfile = "src/DeliveryService/Dockerfile" }
    @{ Name = "feedback-service";    Project = "src/FeedbackService";    Dockerfile = "src/FeedbackService/Dockerfile" }
    @{ Name = "notification-service";Project = "src/NotificationService";Dockerfile = "src/NotificationService/Dockerfile" }
    @{ Name = "receipt-service";     Project = "src/ReceiptService";     Dockerfile = "src/ReceiptService/Dockerfile" }
    @{ Name = "wasm-frontend";       Project = "src/WasmFrontend";       Dockerfile = "src/WasmFrontend/Dockerfile" }
    @{ Name = "api-gateway";         Project = "src/ApiGateway";         Dockerfile = "src/ApiGateway/Dockerfile" }
)

$targets = if ($Services -eq "all") { $serviceList }
           else { $services.Split(',') | ForEach-Object { $name = $_.Trim(); $serviceList | Where-Object { $_.Name -eq $name } } }

$failed = @()

foreach ($svc in $targets) {
    $imageName = "burgeriam/$($svc.Name):$Tag"
    $dockerfile = Join-Path $repoRoot $svc.Dockerfile

    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "Building $imageName ..." -ForegroundColor Cyan
    Write-Host "  Dockerfile: $dockerfile" -ForegroundColor Gray
    Write-Host "========================================" -ForegroundColor Cyan

    $cacheArg = if ($NoCache) { "--no-cache" } else { "" }
    $cmd = "docker build $cacheArg -f `"$dockerfile`" -t $imageName `"$repoRoot`""

    try {
        Invoke-Expression $cmd
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  [OK] $imageName built successfully" -ForegroundColor Green
        } else {
            Write-Host "  [FAIL] $imageName build failed (exit code $LASTEXITCODE)" -ForegroundColor Red
            $failed += $svc.Name
        }
    } catch {
        Write-Host "  [FAIL] $imageName build error: $_" -ForegroundColor Red
        $failed += $svc.Name
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
if ($failed.Count -eq 0) {
    Write-Host "All images built successfully!" -ForegroundColor Green
} else {
    Write-Host "Failed builds: $($failed -join ', ')" -ForegroundColor Red
    exit 1
}
