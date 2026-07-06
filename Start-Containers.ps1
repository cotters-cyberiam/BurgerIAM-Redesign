<#
.SYNOPSIS
    Creates and runs all BurgerIAM containers from built Docker images.
.DESCRIPTION
    Creates the Docker network, volumes, and runs all 11 service containers.
    Services use InMemoryEventBus by default (no RabbitMQ required for local
    testing). Optionally starts RabbitMQ with -UseRabbitMQ flag.
.PARAMETER ImageTag
    Image tag suffix (default: v1.0).
.PARAMETER UseRabbitMQ
    Attempt to start RabbitMQ container for event persistence between restarts.
    If RabbitMQ is available, backend services connect to it automatically.
.PARAMETER Detach
    Run containers in background (default: $true).
.PARAMETER Force
    Remove existing containers with the same names before starting.
.EXAMPLE
    .\Start-Containers.ps1
    .\Start-Containers.ps1 -UseRabbitMQ
    .\Start-Containers.ps1 -Force
#>

param(
    [string]$ImageTag = "v1.0",
    [switch]$UseRabbitMQ,
    [switch]$Detach = $true,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# ─── Configuration ───────────────────────────────────────────────────────────

$networkName  = "burgeriam"
$imagePrefix  = "burgeriam"
$rabbitImage  = "rabbitmq:3-management"

$backendEventBusEnv = @()
if ($UseRabbitMQ) {
    $backendEventBusEnv = @(
        "EventBus__ConnectionString=amqp://guest:guest@burgeriam-rabbitmq:5672",
        "EventBus__ExchangeName=burgeriam.exchange"
    )
}

$services = @(
    @{ Name = "identity-service";  HostPort = @(5041);  ContPort = @(5041); Image = "$imagePrefix/identity-service:$ImageTag";  Volumes = @("identity_data:/app/data"); Env = @("ConnectionStrings__DefaultConnection=Data Source=/app/data/identity.db") }
    @{ Name = "menu-service";      HostPort = @(5052);  ContPort = @(5052); Image = "$imagePrefix/menu-service:$ImageTag";      Volumes = @("menu_data:/app/data");     Env = @("ConnectionStrings__DefaultConnection=Data Source=/app/data/menu.db") }
    @{ Name = "order-service";     HostPort = @(5063);  ContPort = @(5063); Image = "$imagePrefix/order-service:$ImageTag";     Volumes = @("order_data:/app/data");    Env = @("ConnectionStrings__DefaultConnection=Data Source=/app/data/order.db") + $backendEventBusEnv }
    @{ Name = "payment-service";   HostPort = @(5074);  ContPort = @(5074); Image = "$imagePrefix/payment-service:$ImageTag";   Volumes = @("payment_data:/app/data");  Env = @("ConnectionStrings__DefaultConnection=Data Source=/app/data/payment.db") + $backendEventBusEnv }
    @{ Name = "kitchen-service";   HostPort = @(5085);  ContPort = @(5085); Image = "$imagePrefix/kitchen-service:$ImageTag";   Volumes = @("kitchen_data:/app/data");  Env = @("ConnectionStrings__DefaultConnection=Data Source=/app/data/kitchen.db") + $backendEventBusEnv }
    @{ Name = "delivery-service";  HostPort = @(5096);  ContPort = @(5096); Image = "$imagePrefix/delivery-service:$ImageTag";  Volumes = @("delivery_data:/app/data"); Env = @("ConnectionStrings__DefaultConnection=Data Source=/app/data/delivery.db") + $backendEventBusEnv }
    @{ Name = "feedback-service";  HostPort = @(5007);  ContPort = @(5007); Image = "$imagePrefix/feedback-service:$ImageTag";  Volumes = @("feedback_data:/app/data"); Env = @("ConnectionStrings__DefaultConnection=Data Source=/app/data/feedback.db") + $backendEventBusEnv }
    @{ Name = "notification-service"; HostPort = @(5018); ContPort = @(5018); Image = "$imagePrefix/notification-service:$ImageTag"; Volumes = @("notification_data:/app/data"); Env = @("ConnectionStrings__DefaultConnection=Data Source=/app/data/notifications.db") + $backendEventBusEnv }
    @{ Name = "receipt-service";   HostPort = @(5029);  ContPort = @(5029); Image = "$imagePrefix/receipt-service:$ImageTag";   Volumes = @("receipt_data:/app/data");  Env = @("ConnectionStrings__DefaultConnection=Data Source=/app/data/receipts.db") }
    @{ Name = "wasm-frontend";     HostPort = @(8080);  ContPort = @(80);   Image = "$imagePrefix/wasm-frontend:$ImageTag";      Volumes = @();                          Env = @() }
    @{ Name = "api-gateway";       HostPort = @(5000);  ContPort = @(5000); Image = "$imagePrefix/api-gateway:$ImageTag";       Volumes = @();                          Env = @(
        "Services__Identity=http://burgeriam-identity-service:5041",
        "Services__Menu=http://burgeriam-menu-service:5052",
        "Services__Order=http://burgeriam-order-service:5063",
        "Services__Payment=http://burgeriam-payment-service:5074",
        "Services__Kitchen=http://burgeriam-kitchen-service:5085",
        "Services__Delivery=http://burgeriam-delivery-service:5096",
        "Services__Feedback=http://burgeriam-feedback-service:5007",
        "Services__Notification=http://burgeriam-notification-service:5018",
        "Services__Receipt=http://burgeriam-receipt-service:5029",
        "Jwt__Key=BurgerIAM-SuperSecret-Key-Min32Chars!"
    ) }
)

# ─── Helpers ─────────────────────────────────────────────────────────────────

function Get-ContainerName($name) { return "burgeriam-$name" }

function Get-ContainerStatus($name) {
    $cn = Get-ContainerName $name
    $result = docker inspect --format '{{.State.Status}}' $cn 2>$null
    return ($result -eq "running" -or $result -eq "created" -or $result -eq "exited") ? $result : $null
}

function Remove-ExistingContainer($name) {
    $cn = Get-ContainerName $name
    $status = Get-ContainerStatus $name
    if ($status) {
        Write-Host "  Removing existing container '$cn' (status: $status)..." -ForegroundColor DarkYellow
        if ($status -eq "running") { docker stop $cn 2>&1 | Out-Null }
        docker rm $cn 2>&1 | Out-Null
        Write-Host "  Removed." -ForegroundColor Green
    }
}

function Get-PortArgs($hostPorts, $contPorts) {
    $args = @()
    for ($i = 0; $i -lt $hostPorts.Length; $i++) {
        $args += "-p"; $args += "$($hostPorts[$i]):$($contPorts[$i])"
    }
    return $args
}

function Get-VolumeArgs($volumes) {
    $args = @()
    foreach ($v in $volumes) {
        $args += "-v"; $args += $v
    }
    return $args
}

function Get-EnvArgs($envars) {
    $args = @()
    foreach ($e in $envars) {
        $args += "-e"; $args += $e
    }
    return $args
}

function Get-ImageExists($image) {
        $result = docker images --format "{{.Repository}}:{{.Tag}}" | Select-String -SimpleMatch $image
    return [bool]$result
}

# ─── Main ────────────────────────────────────────────────────────────────────

Write-Host "╔══════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║    BurgerIAM — Starting Containers          ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════╝" -ForegroundColor Cyan

# 1. Create network
$networkExists = docker network ls --format "{{.Name}}" | Select-String -SimpleMatch $networkName
if (-not $networkExists) {
    Write-Host "`nCreating network '$networkName'..." -ForegroundColor Yellow
    docker network create $networkName 2>&1 | Out-Null
    Write-Host "  Created." -ForegroundColor Green
} else {
    Write-Host "`nNetwork '$networkName' already exists." -ForegroundColor Gray
}

# 2. Create volumes
$volumeNames = $services | Where-Object { $_.Volumes.Count -gt 0 } | ForEach-Object {
    $_.Volumes | ForEach-Object { $_.Split(":")[0] }
} | Select-Object -Unique

foreach ($vol in $volumeNames) {
    $volExists = docker volume ls --format "{{.Name}}" | Select-String -SimpleMatch $vol
    if (-not $volExists) {
        Write-Host "Creating volume '$vol'..." -ForegroundColor Yellow
        docker volume create $vol 2>&1 | Out-Null
    }
}

# 3. Verify images exist
Write-Host "`nVerifying images..." -ForegroundColor Yellow
$missingImages = @()
foreach ($svc in $services) {
    if (-not (Get-ImageExists $svc.Image)) {
        $missingImages += $svc.Image
    }
}
if ($missingImages.Count -gt 0) {
    Write-Host "  Missing images:" -ForegroundColor Red
    foreach ($img in $missingImages) { Write-Host "    - $img" -ForegroundColor Red }
    Write-Host "  Run .\Build-Images.ps1 -Tag $ImageTag first." -ForegroundColor Red
    exit 1
}

# 4. Remove old containers if -Force
if ($Force) {
    Write-Host "`nRemoving existing containers (-Force)..." -ForegroundColor Yellow
    foreach ($svc in $services) { Remove-ExistingContainer $svc.Name }
}

# 5. Optionally start RabbitMQ
if ($UseRabbitMQ) {
    Write-Host "`nStarting RabbitMQ..." -ForegroundColor Yellow
    $rname = Get-ContainerName "rabbitmq"
    $existingStatus = Get-ContainerStatus "rabbitmq"
    if ($existingStatus -eq "running") {
        Write-Host "  RabbitMQ already running." -ForegroundColor Green
    } else {
        if ($existingStatus) { Remove-ExistingContainer "rabbitmq" }
        $detachFlag = if ($Detach) { "-d" } else { "--rm" }
        try {
            & docker run $detachFlag --name $rname --network $networkName -p 5672:5672 -p 15672:15672 -e RABBITMQ_ERLANG_COOKIE=burgeriam-cluster-cookie --user root $rabbitImage 2>&1 | Out-Null
            Write-Host "  RabbitMQ started." -ForegroundColor Green
            Write-Host "  Backend services will use RabbitMQ for event publishing." -ForegroundColor Gray
        } catch {
            Write-Host "  Warning: Could not start RabbitMQ. Services will use InMemoryEventBus." -ForegroundColor DarkYellow
            $script:useRabbitMQ = $false
        }
    }
} else {
    Write-Host "`nSkipping RabbitMQ. Services will use InMemoryEventBus." -ForegroundColor DarkYellow
    Write-Host "  Use -UseRabbitMQ flag to enable RabbitMQ." -ForegroundColor Gray
}

# 6. Run all services
Write-Host "`nStarting services..." -ForegroundColor Yellow
$started = 0
foreach ($svc in $services) {
    $cn = Get-ContainerName $svc.Name
    $existingStatus = Get-ContainerStatus $svc.Name

    if ($existingStatus -eq "running") {
        Write-Host "  [SKIP] $($svc.Name) — already running" -ForegroundColor DarkYellow
        $started++
        continue
    }
    if ($existingStatus) { Remove-ExistingContainer $svc.Name }

    $portArgs = Get-PortArgs $svc.HostPort $svc.ContPort
    $volArgs  = Get-VolumeArgs $svc.Volumes
    $envArgs  = Get-EnvArgs $svc.Env
    $detachFlag = if ($Detach) { "-d" } else { "--rm" }

    Write-Host "  Starting $($svc.Name)..." -ForegroundColor Gray
    & docker run $detachFlag --name $cn --network $networkName $portArgs $volArgs $envArgs $svc.Image 2>&1 | Out-Null
    Write-Host "    [OK] $($svc.Name) started" -ForegroundColor Green
    $started++
    Start-Sleep -Milliseconds 500
}

# 7. Summary
Write-Host "`n╔══════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  Summary                                  ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════╝" -ForegroundColor Cyan

$running = @()
$stopped = @()
foreach ($svc in $services) {
    $status = Get-ContainerStatus $svc.Name
    $cn = Get-ContainerName $svc.Name
    if ($status -eq "running") {
        $running += $cn
    } else {
        $stopped += "$cn ($status)"
    }
}
# Also check rabbitmq if UseRabbitMQ
if ($UseRabbitMQ) {
    $rStatus = Get-ContainerStatus "rabbitmq"
    $rCn = Get-ContainerName "rabbitmq"
    if ($rStatus -eq "running") { $running += $rCn }
    else { $stopped += "$rCn ($rStatus)" }
}

Write-Host "`nRunning containers ($($running.Count)):" -ForegroundColor Green
foreach ($r in $running) { Write-Host "  - $r" }

if ($stopped.Count -gt 0) {
    Write-Host "`nNot running ($($stopped.Count)):" -ForegroundColor Red
    foreach ($s in $stopped) { Write-Host "  - $s" }
}

Write-Host "`nAccess the frontend at: http://localhost:5000" -ForegroundColor Cyan
Write-Host "Standalone frontend:    http://localhost:8080" -ForegroundColor Cyan
if ($UseRabbitMQ) { Write-Host "RabbitMQ management:   http://localhost:15672 (guest/guest)" -ForegroundColor Cyan }
Write-Host "`nTo stop all containers: .\Stop-Containers.ps1" -ForegroundColor Yellow
Write-Host "EventBus: $(if ($UseRabbitMQ) { 'RabbitMQ' } else { 'InMemoryEventBus' })" -ForegroundColor Cyan
