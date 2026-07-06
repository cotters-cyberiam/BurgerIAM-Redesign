<#
.SYNOPSIS
    Creates and runs all BurgerIAM containers from built Docker images.
.DESCRIPTION
    Creates the Docker network, volumes, and runs all 11 service containers
    plus RabbitMQ. Services start in dependency order with correct port
    mappings, volume mounts, and environment variables for service discovery.
.PARAMETER ImageTag
    Image tag suffix (default: v1.0).
.PARAMETER Detach
    Run containers in background (default: $true).
.PARAMETER Force
    Remove existing containers with the same names before starting.
.EXAMPLE
    .\Start-Containers.ps1
    .\Start-Containers.ps1 -ImageTag "latest"
    .\Start-Containers.ps1 -Force
#>

param(
    [string]$ImageTag = "v1.0",
    [switch]$Detach = $true,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# ─── Configuration ───────────────────────────────────────────────────────────

$networkName  = "burgeriam"
$imagePrefix  = "burgeriam"
$rabbitImage  = "rabbitmq:3-management"

$services = @(
    @{ Name = "rabbitmq";         HostPort = @(5672, 15672);  ContPort = @(5672, 15672);  Image = $rabbitImage;  Volumes = @();          Env = @();                                               Depends = $null }
    @{ Name = "identity-service";  HostPort = @(5041);         ContPort = @(5041);         Image = "$imagePrefix/identity-service:$ImageTag";  Volumes = @("identity_data:/app/data");                    Env = @("ConnectionStrings__DefaultConnection=Data Source=/app/data/identity.db");                                    Depends = "rabbitmq" }
    @{ Name = "menu-service";      HostPort = @(5052);         ContPort = @(5052);         Image = "$imagePrefix/menu-service:$ImageTag";      Volumes = @("menu_data:/app/data");                        Env = @("ConnectionStrings__DefaultConnection=Data Source=/app/data/menu.db");                                        Depends = "rabbitmq" }
    @{ Name = "order-service";     HostPort = @(5063);         ContPort = @(5063);         Image = "$imagePrefix/order-service:$ImageTag";     Volumes = @("order_data:/app/data");                       Env = @("ConnectionStrings__DefaultConnection=Data Source=/app/data/order.db","EventBus__ConnectionString=amqp://guest:guest@rabbitmq:5672","EventBus__ExchangeName=burgeriam.exchange");  Depends = "rabbitmq" }
    @{ Name = "payment-service";   HostPort = @(5074);         ContPort = @(5074);         Image = "$imagePrefix/payment-service:$ImageTag";   Volumes = @("payment_data:/app/data");                     Env = @("ConnectionStrings__DefaultConnection=Data Source=/app/data/payment.db","EventBus__ConnectionString=amqp://guest:guest@rabbitmq:5672","EventBus__ExchangeName=burgeriam.exchange");  Depends = "rabbitmq" }
    @{ Name = "kitchen-service";   HostPort = @(5085);         ContPort = @(5085);         Image = "$imagePrefix/kitchen-service:$ImageTag";   Volumes = @("kitchen_data:/app/data");                     Env = @("ConnectionStrings__DefaultConnection=Data Source=/app/data/kitchen.db","EventBus__ConnectionString=amqp://guest:guest@rabbitmq:5672","EventBus__ExchangeName=burgeriam.exchange");  Depends = "rabbitmq" }
    @{ Name = "delivery-service";  HostPort = @(5096);         ContPort = @(5096);         Image = "$imagePrefix/delivery-service:$ImageTag";  Volumes = @("delivery_data:/app/data");                    Env = @("ConnectionStrings__DefaultConnection=Data Source=/app/data/delivery.db","EventBus__ConnectionString=amqp://guest:guest@rabbitmq:5672","EventBus__ExchangeName=burgeriam.exchange");  Depends = "rabbitmq" }
    @{ Name = "feedback-service";  HostPort = @(5007);         ContPort = @(5007);         Image = "$imagePrefix/feedback-service:$ImageTag";  Volumes = @("feedback_data:/app/data");                    Env = @("ConnectionStrings__DefaultConnection=Data Source=/app/data/feedback.db","EventBus__ConnectionString=amqp://guest:guest@rabbitmq:5672","EventBus__ExchangeName=burgeriam.exchange");  Depends = "rabbitmq" }
    @{ Name = "notification-service"; HostPort = @(5018);     ContPort = @(5018);         Image = "$imagePrefix/notification-service:$ImageTag"; Volumes = @("notification_data:/app/data");               Env = @("ConnectionStrings__DefaultConnection=Data Source=/app/data/notifications.db","EventBus__ConnectionString=amqp://guest:guest@rabbitmq:5672","EventBus__ExchangeName=burgeriam.exchange");  Depends = "rabbitmq" }
    @{ Name = "receipt-service";   HostPort = @(5029);         ContPort = @(5029);         Image = "$imagePrefix/receipt-service:$ImageTag";   Volumes = @("receipt_data:/app/data");                     Env = @("ConnectionStrings__DefaultConnection=Data Source=/app/data/receipts.db");                                    Depends = "rabbitmq" }
    @{ Name = "wasm-frontend";     HostPort = @(8080);         ContPort = @(80);           Image = "$imagePrefix/wasm-frontend:$ImageTag";    Volumes = @();                                            Env = @();                                               Depends = $null }
    @{ Name = "api-gateway";       HostPort = @(5000);         ContPort = @(5000);         Image = "$imagePrefix/api-gateway:$ImageTag";      Volumes = @();                                            Env = @(
        "Services__Identity=http://identity-service:5041",
        "Services__Menu=http://menu-service:5052",
        "Services__Order=http://order-service:5063",
        "Services__Payment=http://payment-service:5074",
        "Services__Kitchen=http://kitchen-service:5085",
        "Services__Delivery=http://delivery-service:5096",
        "Services__Feedback=http://feedback-service:5007",
        "Services__Notification=http://notification-service:5018",
        "Services__Receipt=http://receipt-service:5029",
        "Jwt__Key=BurgerIAM-SuperSecret-Key-Min32Chars!"
    ); Depends = @("identity-service","menu-service","order-service","payment-service","kitchen-service","delivery-service","feedback-service","notification-service","receipt-service") }
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
    if ($svc.Image -ne $rabbitImage -and -not (Get-ImageExists $svc.Image)) {
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

# 5. Run RabbitMQ first
Write-Host "`nStarting RabbitMQ..." -ForegroundColor Yellow
$rname = Get-ContainerName "rabbitmq"
$existingStatus = Get-ContainerStatus "rabbitmq"
if ($existingStatus -eq "running") {
    Write-Host "  RabbitMQ already running." -ForegroundColor Green
} else {
    if ($existingStatus) { Remove-ExistingContainer "rabbitmq" }
    $portArgs = Get-PortArgs @(5672,15672) @(5672,15672)
    $detachFlag = if ($Detach) { "-d" } else { "--rm" }
    & docker run $detachFlag --name $rname --network $networkName $portArgs $rabbitImage 2>&1 | Out-Null
    Write-Host "  Waiting for RabbitMQ to become healthy..." -ForegroundColor Gray
    $timeout = [datetime]::Now.AddSeconds(60)
    $ready = $false
    while (-not $ready -and [datetime]::Now -lt $timeout) {
        try {
            $health = docker exec $rname rabbitmqctl status 2>&1 | Out-Null
            if ($LASTEXITCODE -eq 0) { $ready = $true }
        } catch {}
        if (-not $ready) { Start-Sleep 2 }
    }
    if ($ready) { Write-Host "  RabbitMQ is ready." -ForegroundColor Green }
    else { Write-Host "  Warning: RabbitMQ may not be fully ready yet." -ForegroundColor DarkYellow }
}

# 6. Run backend services
Write-Host "`nStarting backend services..." -ForegroundColor Yellow
$started = 0
foreach ($svc in $services) {
    if ($svc.Name -eq "rabbitmq") { continue }

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

Write-Host "`nRunning containers ($($running.Count)):" -ForegroundColor Green
foreach ($r in $running) { Write-Host "  - $r" }

if ($stopped.Count -gt 0) {
    Write-Host "`nNot running ($($stopped.Count)):" -ForegroundColor Red
    foreach ($s in $stopped) { Write-Host "  - $s" }
}

Write-Host "`nAccess the frontend at: http://localhost:5000" -ForegroundColor Cyan
Write-Host "Standalone frontend:    http://localhost:8080" -ForegroundColor Cyan
Write-Host "RabbitMQ management:   http://localhost:15672 (guest/guest)" -ForegroundColor Cyan
Write-Host "`nTo stop all containers: .\Stop-Containers.ps1" -ForegroundColor Yellow
