# build.ps1 - Build script for WindowsTaskbarRemoval
# Usage:
#   .\build.ps1          # debug build
#   .\build.ps1 Release  # release build
#   .\build.ps1 Release publish  # self-contained single-file exe

param(
    [string]$Configuration = "Debug",
    [string]$Action = "build"
)

$ErrorActionPreference = "Stop"

$ProjectDir = Join-Path $PSScriptRoot "WindowsTaskbarRemoval"
$ProjectFile = Join-Path $ProjectDir "WindowsTaskbarRemoval.csproj"

if (-not (Test-Path $ProjectFile)) {
    Write-Error "Could not find project file at: $ProjectFile"
    exit 1
}

# Check dotnet is available
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "dotnet SDK not found. Install it from https://dotnet.microsoft.com/download/dotnet/8.0"
    exit 1
}

Write-Host "Configuration : $Configuration"
Write-Host "Action        : $Action"
Write-Host "Project       : $ProjectFile"
Write-Host ""

if ($Action -eq "publish") {
    Write-Host "Publishing self-contained single-file executable..."
    dotnet publish $ProjectFile `
        --configuration $Configuration `
        --runtime win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true
} else {
    Write-Host "Building..."
    dotnet build $ProjectFile --configuration $Configuration
}

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "Build succeeded."

if ($Action -eq "publish") {
    $OutDir = Join-Path $ProjectDir "bin\$Configuration\net8.0-windows\win-x64\publish"
    Write-Host "Output: $OutDir"
} else {
    $OutDir = Join-Path $ProjectDir "bin\$Configuration\net8.0-windows"
    Write-Host "Output: $OutDir"
}
