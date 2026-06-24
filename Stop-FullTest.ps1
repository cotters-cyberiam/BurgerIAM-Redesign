<#
.SYNOPSIS
    Stops all BurgerIAM microservices launched by Start-FullTest.ps1.
.DESCRIPTION
    Kills dotnet processes listening on the BurgerIAM service ports.
    Also closes any terminal windows titled "BurgerIAM - *".
#>

$ports = @(5041, 5052, 5063, 5074, 5085, 5096, 5007, 5018, 5029, 5000)

Write-Host "Stopping BurgerIAM services..." -ForegroundColor Cyan

# Kill by port — find processes listening on any BurgerIAM port
$tcpConnections = Get-NetTCPConnection -State Established,Listen,Bound -ErrorAction SilentlyContinue |
    Where-Object { $_.LocalPort -in $ports }

$pids = $tcpConnections | ForEach-Object { $_.OwningProcess } | Select-Object -Unique

foreach ($procId in $pids) {
    try {
        $proc = Get-Process -Id $procId -ErrorAction Stop
        if ($proc.ProcessName -match "dotnet") {
            $proc.Kill()
            Write-Host "  Killed dotnet process $procId (listening on BurgerIAM port)" -ForegroundColor Green
        }
    } catch {
        # Process may have already exited
    }
}

# Also kill any terminal windows with "BurgerIAM -" in the title
Get-Process pwsh -ErrorAction SilentlyContinue | ForEach-Object {
    try {
        $title = $_.MainWindowTitle
        if ($title -match "BurgerIAM - ") {
            $_.Kill()
            Write-Host "  Closed terminal: $title" -ForegroundColor Green
        }
    } catch {}
}

Start-Sleep -Seconds 1

# Verify no services are still running
$remaining = Get-NetTCPConnection -ErrorAction SilentlyContinue |
    Where-Object { $_.LocalPort -in $ports -and $_.State -ne "TimeWait" }

if ($remaining) {
    Write-Host "Some services may still be running. Check Task Manager." -ForegroundColor Yellow
} else {
    Write-Host "All services stopped." -ForegroundColor Green
}
