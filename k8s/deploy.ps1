<#
.SYNOPSIS
    Deploys all BurgerIAM microservices to a Kubernetes cluster.
.DESCRIPTION
    Applies all Kubernetes manifests in the correct order:
    1. Namespace
    2. Secrets
    3. Persistent Volume Claims
    4. RabbitMQ message broker
    5. All backend microservices
    6. API Gateway
    7. Wasm Frontend (exposed via LoadBalancer)
.PARAMETER KubeConfig
    Path to kubeconfig file (optional, uses default if not specified).
.PARAMETER Namespace
    Override the target namespace (default: burgeriam).
.PARAMETER WaitForReady
    Wait for all pods to be in Ready state before returning.
.PARAMETER Timeout
    Timeout in seconds when waiting for pods to become ready (default: 180).
.EXAMPLE
    .\k8s\deploy.ps1
    .\k8s\deploy.ps1 -WaitForReady
    .\k8s\deploy.ps1 -WaitForReady -Timeout 300
#>

param(
    [string]$KubeConfig = "",
    [string]$Namespace = "burgeriam",
    [switch]$WaitForReady,
    [int]$Timeout = 180
)

$ErrorActionPreference = "Stop"

$k8sDir = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }

$kubectlArgs = @("apply", "-f")
if ($KubeConfig) {
    $kubectlArgs = @("--kubeconfig=$KubeConfig") + $kubectlArgs
}

$manifests = @(
    "00-namespace.yaml",
    "01-secrets.yaml",
    "02-persistent-volumes.yaml",
    "03-rabbitmq.yaml",
    "04-identity-service.yaml",
    "05-menu-service.yaml",
    "06-order-service.yaml",
    "07-payment-service.yaml",
    "08-kitchen-service.yaml",
    "09-delivery-service.yaml",
    "10-feedback-service.yaml",
    "11-notification-service.yaml",
    "12-receipt-service.yaml",
    "13-api-gateway.yaml",
    "14-wasm-frontend.yaml"
)

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Deploying BurgerIAM to Kubernetes" -ForegroundColor Cyan
Write-Host "  Namespace: $Namespace" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

foreach ($manifest in $manifests) {
    $path = Join-Path $k8sDir $manifest
    if (-not (Test-Path $path)) {
        Write-Warning "Manifest not found: $path"
        continue
    }
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Applying $manifest ..." -ForegroundColor Yellow
    $global:LASTEXITCODE = 0
    if ($KubeConfig) {
        & kubectl apply --kubeconfig=$KubeConfig -f $path 2>&1 | ForEach-Object { "$_" }
    } else {
        & kubectl apply -f $path 2>&1 | ForEach-Object { "$_" }
    }
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[FAIL] $manifest failed to apply" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""
Write-Host "[$(Get-Date -Format 'HH:mm:ss')] All manifests applied successfully!" -ForegroundColor Green

if ($WaitForReady) {
    Write-Host ""
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Waiting for all pods to be ready (timeout: ${Timeout}s) ..." -ForegroundColor Yellow

    $endTime = (Get-Date).AddSeconds($Timeout)
    $allReady = $false

    while ((Get-Date) -lt $endTime) {
        $pending = if ($KubeConfig) {
            (& kubectl get pods -n $Namespace --kubeconfig=$KubeConfig -o jsonpath='{range .items[*]}{.metadata.name}{"\t"}{.status.phase}{"\n"}{end}' 2>$null) | Where-Object { $_ -match '\t' -and $_ -notmatch 'Running|Succeeded' }
        } else {
            (& kubectl get pods -n $Namespace -o jsonpath='{range .items[*]}{.metadata.name}{"\t"}{.status.phase}{"\n"}{end}' 2>$null) | Where-Object { $_ -match '\t' -and $_ -notmatch 'Running|Succeeded' }
        }

        if (-not $pending -or $pending.Count -eq 0) {
            $allReady = $true
            break
        }

        $pendingCount = ($pending | Measure-Object).Count
        $totalCount = if ($KubeConfig) {
            [int](& kubectl get pods -n $Namespace --kubeconfig=$KubeConfig --no-headers 2>$null | Measure-Object).Count
        } else {
            [int](& kubectl get pods -n $Namespace --no-headers 2>$null | Measure-Object).Count
        }

        Write-Host "  Waiting... $pendingCount/$totalCount pods not yet Running" -ForegroundColor Gray
        Start-Sleep -Seconds 5
    }

    if ($allReady) {
        Write-Host "[$(Get-Date -Format 'HH:mm:ss')] All pods are ready!" -ForegroundColor Green
    } else {
        Write-Warning "Timeout reached - some pods may not be ready yet"
        if ($KubeConfig) {
            & kubectl get pods -n $Namespace --kubeconfig=$KubeConfig
        } else {
            & kubectl get pods -n $Namespace
        }
    }
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Deployment Summary" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

if ($KubeConfig) {
    & kubectl get pods -n $Namespace --kubeconfig=$KubeConfig
    Write-Host ""
    & kubectl get svc -n $Namespace --kubeconfig=$KubeConfig
} else {
    & kubectl get pods -n $Namespace
    Write-Host ""
    & kubectl get svc -n $Namespace
}

Write-Host ""
Write-Host "Frontend URL:" -ForegroundColor Cyan
if ($KubeConfig) {
    $frontendIp = & kubectl get svc -n $Namespace burgeriam-wasm-frontend --kubeconfig=$KubeConfig -o jsonpath='{.status.loadBalancer.ingress[0].ip}' 2>$null
    $frontendHostname = & kubectl get svc -n $Namespace burgeriam-wasm-frontend --kubeconfig=$KubeConfig -o jsonpath='{.status.loadBalancer.ingress[0].hostname}' 2>$null
} else {
    $frontendIp = & kubectl get svc -n $Namespace burgeriam-wasm-frontend -o jsonpath='{.status.loadBalancer.ingress[0].ip}' 2>$null
    $frontendHostname = & kubectl get svc -n $Namespace burgeriam-wasm-frontend -o jsonpath='{.status.loadBalancer.ingress[0].hostname}' 2>$null
}

if ($frontendIp) {
    Write-Host "  http://$frontendIp" -ForegroundColor Green
} elseif ($frontendHostname) {
    Write-Host "  http://$frontendHostname" -ForegroundColor Green
} else {
    Write-Host "  (pending - run 'kubectl get svc -n $Namespace burgeriam-wasm-frontend' to check)" -ForegroundColor Yellow
}

Write-Host "RabbitMQ Management:" -ForegroundColor Cyan
Write-Host "  http://localhost:15672 (via 'kubectl port-forward -n $Namespace svc/burgeriam-rabbitmq 15672:15672')" -ForegroundColor Green

Write-Host ""
Write-Host "Done! Use .\k8s\remove.ps1 to tear down all resources." -ForegroundColor Cyan
