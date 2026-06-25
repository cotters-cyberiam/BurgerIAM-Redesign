<#
.SYNOPSIS
    Starts all BurgerIAM microservices for manual testing (Phases 2-6).
.DESCRIPTION
    Launches each service in a separate PowerShell terminal window.
    After the startup delay, optionally runs the ManualTestApp.
.PARAMETER RunTests
    After starting all services, runs the ManualTestApp against all 10 service URLs.
.PARAMETER StartupDelaySeconds
    Seconds to wait for services to initialize before running tests (default: 15).
.PARAMETER NoBuild
    Skip dotnet build before starting services (useful if already built).
.EXAMPLE
    .\Start-FullTest.ps1
    .\Start-FullTest.ps1 -RunTests -StartupDelaySeconds 20
#>

param(
    [switch]$RunTests,
    [int]$StartupDelaySeconds = 15,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
$repoRoot = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }

$services = @(
    @{ Name = "IdentityService";     Port = 5041 }
    @{ Name = "MenuService";         Port = 5052 }
    @{ Name = "OrderService";        Port = 5063 }
    @{ Name = "PaymentService";      Port = 5074 }
    @{ Name = "KitchenService";      Port = 5085 }
    @{ Name = "DeliveryService";     Port = 5096 }
    @{ Name = "FeedbackService";     Port = 5007 }
    @{ Name = "NotificationService"; Port = 5018 }
    @{ Name = "ReceiptService";      Port = 5029 }
    @{ Name = "ApiGateway";          Port = 5000 }
)

# Build all projects first (unless skipped)
if (-not $NoBuild) {
    Write-Host "Building all projects..." -ForegroundColor Cyan
    dotnet build --nologo
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed. Aborting." -ForegroundColor Red
        exit 1
    }
    Write-Host ""
}

# Start each service in a new terminal window
Write-Host "Starting all BurgerIAM services..." -ForegroundColor Cyan
Write-Host ""

foreach ($svc in $services) {
    $title = "BurgerIAM - $($svc.Name) :$($svc.Port)"
    $projectDir = Join-Path $repoRoot "src" $svc.Name

    if (-not (Test-Path $projectDir)) {
        Write-Host "  [SKIP] $($svc.Name) — project directory not found" -ForegroundColor DarkYellow
        continue
    }

    $cmd = "dotnet run --project `"$projectDir`""

    Start-Process pwsh.exe -ArgumentList @(
        "-NoExit"
        "-Command"
        "Write-Host '=== $title ===' -ForegroundColor Cyan; dotnet run --project '$projectDir'"
    ) -WindowStyle Normal

    Write-Host "  [STARTED] $($svc.Name) on port $($svc.Port)" -ForegroundColor Green
    Start-Sleep -Milliseconds 500
}

Write-Host ""
Write-Host "All services launched. Waiting $StartupDelaySeconds seconds for startup..." -ForegroundColor Yellow
Write-Host ""

# Health-check loop — poll until all services respond or timeout
$timeout = [datetime]::Now.AddSeconds($StartupDelaySeconds)
$allReady = $false

while (-not $allReady -and [datetime]::Now -lt $timeout) {
    $allReady = $true
    foreach ($svc in $services) {
        try {
            $response = Invoke-WebRequest -Uri "http://localhost:$($svc.Port)/health" -TimeoutSec 2 -ErrorAction Stop
            if ($response.StatusCode -ne 200) { $allReady = $false }
        } catch {
            $allReady = $false
        }
    }
    if (-not $allReady) { Start-Sleep -Seconds 2 }
}

if ($allReady) {
    Write-Host "All services are healthy!" -ForegroundColor Green
} else {
    Write-Host "Some services may still be starting (timeout reached)." -ForegroundColor DarkYellow
    Write-Host "Check the individual terminal windows for errors." -ForegroundColor DarkYellow
}

Write-Host ""

if ($RunTests) {
    Write-Host "Running ManualTestApp..." -ForegroundColor Cyan

    $urls = foreach ($svc in $services) { "http://localhost:$($svc.Port)" }
    $testProject = Join-Path $repoRoot "tests" "ManualTestApp"

    dotnet run --project $testProject -- $urls

    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "All manual tests PASSED!" -ForegroundColor Green
    } else {
        Write-Host ""
        Write-Host "Some manual tests FAILED (exit code $LASTEXITCODE)." -ForegroundColor Red
    }
} else {
    Write-Host "Services are running in separate terminal windows."
    Write-Host ""
    Write-Host "To run manual tests, open another terminal and execute:"
    Write-Host "  dotnet run --project tests/ManualTestApp -- http://localhost:5041 http://localhost:5052 http://localhost:5063 http://localhost:5074 http://localhost:5085 http://localhost:5096 http://localhost:5007 http://localhost:5018 http://localhost:5029 http://localhost:5000"
    Write-Host ""
    Write-Host "Or browse to http://localhost:5000 for the Blazor frontend."
}
