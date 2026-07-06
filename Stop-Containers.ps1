<#
.SYNOPSIS
    Stops and removes all BurgerIAM containers and optionally the Docker network.
.DESCRIPTION
    Stops and removes all containers created by Start-Containers.ps1.
    Can optionally remove the Docker network and named volumes.
.PARAMETER RemoveVolumes
    Also remove named volumes (destroys SQLite data).
.PARAMETER RemoveNetwork
    Also remove the burgeriam Docker network.
.PARAMETER SkipContainers
    Skip stopping containers (useful with -RemoveVolumes to clean up data
    while services keep running via restart).
.EXAMPLE
    .\Stop-Containers.ps1
    .\Stop-Containers.ps1 -RemoveVolumes
    .\Stop-Containers.ps1 -RemoveVolumes -RemoveNetwork
#>

param(
    [switch]$RemoveVolumes,
    [switch]$RemoveNetwork,
    [switch]$SkipContainers
)

$ErrorActionPreference = "Stop"

$networkName = "burgeriam"
$containerNames = @(
    "burgeriam-rabbitmq",
    "burgeriam-identity-service",
    "burgeriam-menu-service",
    "burgeriam-order-service",
    "burgeriam-payment-service",
    "burgeriam-kitchen-service",
    "burgeriam-delivery-service",
    "burgeriam-feedback-service",
    "burgeriam-notification-service",
    "burgeriam-receipt-service",
    "burgeriam-wasm-frontend",
    "burgeriam-api-gateway"
)

$volumeNames = @(
    "rabbitmq_data",
    "identity_data",
    "menu_data",
    "order_data",
    "payment_data",
    "kitchen_data",
    "delivery_data",
    "feedback_data",
    "notification_data",
    "receipt_data"
)

Write-Host "╔══════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║    BurgerIAM — Stopping Containers         ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════╝" -ForegroundColor Cyan

# 1. Stop and remove containers
if (-not $SkipContainers) {
    Write-Host "`nStopping containers..." -ForegroundColor Yellow

    foreach ($cn in $containerNames) {
        $exists = docker inspect $cn 2>$null
        if ($exists) {
            $status = docker inspect --format '{{.State.Status}}' $cn 2>$null
            if ($status -eq "running") {
                Write-Host "  Stopping $cn..." -ForegroundColor Gray
                docker stop $cn --time 10 2>&1 | Out-Null
            }
            Write-Host "  Removing $cn..." -ForegroundColor Gray
            docker rm $cn 2>&1 | Out-Null
            Write-Host "    Removed $cn" -ForegroundColor Green
        } else {
            Write-Host "  [SKIP] $cn — not found" -ForegroundColor DarkYellow
        }
    }
} else {
    Write-Host "`nSkipping container removal (-SkipContainers)." -ForegroundColor DarkYellow
}

# 2. Remove volumes
if ($RemoveVolumes) {
    Write-Host "`nRemoving volumes..." -ForegroundColor Yellow
    foreach ($vol in $volumeNames) {
        $exists = docker volume ls --format "{{.Name}}" | Select-String -SimpleMatch $vol
        if ($exists) {
            docker volume rm $vol 2>&1 | Out-Null
            Write-Host "  Removed volume: $vol" -ForegroundColor Green
        } else {
            Write-Host "  [SKIP] $vol — not found" -ForegroundColor DarkYellow
        }
    }
} else {
    Write-Host "`nVolumes preserved (use -RemoveVolumes to delete SQLite data)." -ForegroundColor Gray
}

# 3. Remove network
if ($RemoveNetwork) {
    $netExists = docker network ls --format "{{.Name}}" | Select-String -SimpleMatch $networkName
    if ($netExists) {
        Write-Host "Removing network '$networkName'..." -ForegroundColor Yellow
        docker network rm $networkName 2>&1 | Out-Null
        Write-Host "  Removed." -ForegroundColor Green
    } else {
        Write-Host "[SKIP] Network '$networkName' not found." -ForegroundColor DarkYellow
    }
} else {
    Write-Host "`nNetwork '$networkName' preserved (use -RemoveNetwork to remove)." -ForegroundColor Gray
}

Write-Host "`nDone." -ForegroundColor Green
