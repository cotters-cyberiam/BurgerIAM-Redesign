<#
.SYNOPSIS
    Stops all BurgerIAM microservices launched by Start-FullTest.ps1.
.DESCRIPTION
    Finds and kills dotnet processes running from the BurgerIAM src/ directory
    and closes any terminal windows titled "BurgerIAM - *".
#>

$servicePaths = @(
    "IdentityService", "MenuService", "OrderService", "PaymentService",
    "KitchenService", "DeliveryService", "FeedbackService",
    "NotificationService", "ReceiptService", "ApiGateway"
)

Write-Host "Stopping BurgerIAM services..." -ForegroundColor Cyan

# Find dotnet processes running from the BurgerIAM repo
$processes = Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" -ErrorAction SilentlyContinue

$killed = 0
foreach ($proc in $processes) {
    $cmdLine = $proc.CommandLine
    foreach ($svc in $servicePaths) {
        if ($cmdLine -match [regex]::Escape($svc)) {
            try {
                Stop-Process -Id $proc.ProcessId -Force -ErrorAction Stop
                Write-Host "  Killed dotnet PID $($proc.ProcessId) ($svc)" -ForegroundColor Green
                $killed++
            } catch {
                Write-Host "  Failed to kill PID $($proc.ProcessId) ($svc): $_" -ForegroundColor Red
            }
            break
        }
    }
}

# Close terminal windows with "BurgerIAM -" in the title
Get-Process pwsh -ErrorAction SilentlyContinue | ForEach-Object {
    try {
        $title = $_.MainWindowTitle
        if ($title -match "BurgerIAM - ") {
            $_.Kill()
            Write-Host "  Closed terminal: $title" -ForegroundColor Green
        }
    } catch {}
}

if ($killed -eq 0) {
    Write-Host "No running BurgerIAM services found." -ForegroundColor Yellow
} else {
    Write-Host "Stopped $killed service(s)." -ForegroundColor Green
}
