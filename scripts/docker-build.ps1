param(
    [string]$Tag = "local",
    [switch]$Push
)
$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path $PSScriptRoot -Parent
$Image = "ghcr.io/robinrottier/mqttdashboard"

Set-Location $RepoRoot
Write-Host "Building $Image:$Tag..." -ForegroundColor Cyan
docker build -t "${Image}:${Tag}" -f MqttDashboard.WebApp\MqttDashboard.WebApp\Dockerfile .
if ($Push) {
    Write-Host "Pushing ${Image}:${Tag}..." -ForegroundColor Cyan
    docker push "${Image}:${Tag}"
}
Write-Host "Done." -ForegroundColor Green
