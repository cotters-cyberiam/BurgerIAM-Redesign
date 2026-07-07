<#
.SYNOPSIS
    Removes all BurgerIAM resources from the Kubernetes cluster.
.DESCRIPTION
    Deletes the entire burgeriam namespace which cascades to remove all
    deployments, services, PVCs, secrets, and configmaps within it.
.PARAMETER KubeConfig
    Path to kubeconfig file (optional, uses default if not specified).
.PARAMETER Namespace
    Override the target namespace (default: burgeriam).
.PARAMETER SkipPrompt
    Skip the confirmation prompt (automated usage).
.PARAMETER DeletePVCs
    Also delete PersistentVolumeClaims (data will be lost).
.EXAMPLE
    .\k8s\remove.ps1
    .\k8s\remove.ps1 -SkipPrompt
    .\k8s\remove.ps1 -DeletePVCs -SkipPrompt
#>

param(
    [string]$KubeConfig = "",
    [string]$Namespace = "burgeriam",
    [switch]$SkipPrompt,
    [switch]$DeletePVCs
)

$ErrorActionPreference = "Stop"

if (-not $SkipPrompt) {
    Write-Host "============================================" -ForegroundColor Red
    Write-Host "  WARNING: This will remove ALL BurgerIAM" -ForegroundColor Red
    Write-Host "  resources from namespace '$Namespace'." -ForegroundColor Red
    Write-Host "============================================" -ForegroundColor Red
    Write-Host ""

    $confirmation = Read-Host "Are you sure you want to proceed? (y/N) "
    if ($confirmation -notin @("y", "Y", "yes", "YES")) {
        Write-Host "Operation cancelled." -ForegroundColor Yellow
        exit 0
    }
}

$kubectlCmd = if ($KubeConfig) { "kubectl --kubeconfig=$KubeConfig" } else { "kubectl" }

Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Removing all BurgerIAM resources ..." -ForegroundColor Yellow

if ($DeletePVCs) {
    Write-Host "  Deleting PersistentVolumeClaims (data will be lost) ..." -ForegroundColor Red
    $pvcManifests = @(
        "identity-service-data",
        "menu-service-data",
        "order-service-data",
        "payment-service-data",
        "kitchen-service-data",
        "delivery-service-data",
        "feedback-service-data",
        "notification-service-data",
        "receipt-service-data"
    )
    foreach ($pvc in $pvcManifests) {
        if ($KubeConfig) {
            & kubectl --kubeconfig=$KubeConfig delete pvc -n $Namespace $pvc --ignore-not-found=true 2>&1 | Out-Null
        } else {
            & kubectl delete pvc -n $Namespace $pvc --ignore-not-found=true 2>&1 | Out-Null
        }
    }
}

if ($KubeConfig) {
    & kubectl --kubeconfig=$KubeConfig delete namespace $Namespace --ignore-not-found=true 2>&1 | ForEach-Object { "$_" }
} else {
    & kubectl delete namespace $Namespace --ignore-not-found=true 2>&1 | ForEach-Object { "$_" }
}

if ($LASTEXITCODE -eq 0) {
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] All resources removed successfully." -ForegroundColor Green
} else {
    Write-Warning "Some resources may not have been removed cleanly."
}

Write-Host ""
Write-Host "To verify: kubectl get all -n $Namespace" -ForegroundColor Cyan
Write-Host "  (should return 'No resources found' once deletion is complete)" -ForegroundColor Gray
