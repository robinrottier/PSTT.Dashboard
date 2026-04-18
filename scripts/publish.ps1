#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Publish MqttDashboard as a self-contained single-file executable.
.PARAMETER Runtime
    Target runtime (default: win-x64). Options: win-x64, win-arm64, linux-x64, linux-arm64
.PARAMETER Configuration
    Build configuration (default: Release)
.PARAMETER Version
    Override version string (default: derived by MinVer from git tags)
#>
param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"
$ProjectPath = "$PSScriptRoot\..\src\MqttDashboard.WebApp\MqttDashboard.WebApp\MqttDashboard.WebApp.csproj"
$OutputDir = "$PSScriptRoot\..\artifacts\$Runtime"
$ArtifactsDir = "$PSScriptRoot\..\artifacts"

Write-Host "Publishing MqttDashboard for $Runtime..." -ForegroundColor Cyan

# Build publish args
$publishArgs = @(
    "publish", $ProjectPath,
    "-c", $Configuration,
    "-r", $Runtime,
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-o", $OutputDir
)
if ($Version -ne "") {
    $publishArgs += "-p:Version=$Version"
}

dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# Create appsettings sample
$sampleSettings = @'
{
  "MqttSettings": {
    "Broker": "your-mqtt-broker-host",
    "Port": 1883,
    "Username": "",
    "Password": ""
  },
  "DiagramStorage": {
    "DataDirectory": "./data"
  },
  "AllowedPathBase": "",
  "Auth": {
    "AdminPasswordHash": ""
  }
}
'@
Set-Content -Path "$OutputDir\appsettings.sample.json" -Value $sampleSettings

# Also copy existing appsettings
if (Test-Path "$OutputDir\appsettings.json") {
    Write-Host "appsettings.json already present in output" -ForegroundColor Gray
}

# Create zip
$zipName = "mqttdashboard-$Runtime.zip"
$zipPath = "$ArtifactsDir\$zipName"
if (Test-Path $zipPath) { Remove-Item $zipPath }
Compress-Archive -Path "$OutputDir\*" -DestinationPath $zipPath
Write-Host "Created: $zipPath" -ForegroundColor Green
