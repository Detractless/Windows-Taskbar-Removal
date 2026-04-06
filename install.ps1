# install.ps1 - Installer for WindowsTaskbarRemoval
# Must be run as Administrator (use install.bat to auto-elevate).

$ErrorActionPreference = "Stop"

# ---- Configuration ----------------------------------------------------------

$AppName    = "WindowsTaskbarRemoval"
$ExeName    = "WindowsTaskbarRemoval.exe"
$TaskName   = "Detractless\WindowsTaskbarRemoval"
$InstallDir = Join-Path $env:ProgramFiles $AppName

# ---- Require Administrator --------------------------------------------------

$currentPrincipal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
$isAdmin = $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Error "This script must be run as Administrator. Use install.bat to launch it correctly."
    exit 1
}

# ---- Menu -------------------------------------------------------------------

Write-Host ""
Write-Host "  WindowsTaskbarRemoval Installer"
Write-Host "  --------------------------------"
Write-Host ""
Write-Host "  1. Install"
Write-Host "  2. Remove"
Write-Host ""

$choice = $null
while ($choice -notin @("1", "2")) {
    $choice = (Read-Host "  Select an option (1/2)").Trim()
    if ($choice -notin @("1", "2")) {
        Write-Host "  Invalid option. Please enter 1 or 2."
    }
}

Write-Host ""

# ---- Remove -----------------------------------------------------------------

if ($choice -eq "2") {
    Write-Host "Removing $AppName..."

    # Stop the app twice without -Force so it can cleanly undo its effects.
    $running = Get-Process -Name $AppName -ErrorAction SilentlyContinue
    if ($running) {
        Write-Host "Stopping running instance (attempt 1)..."
        $running | Stop-Process
        Start-Sleep -Seconds 2
        Write-Host "Restarting Explorer to undo taskbar effects..."
        taskkill /IM explorer.exe /F 2>&1 | Out-Null
        Start-Process explorer.exe
        Start-Sleep -Seconds 2
    }

    try { schtasks.exe /Query /TN $TaskName 2>&1 | Out-Null } catch {}
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Removing scheduled task..."
        try { schtasks.exe /Delete /TN $TaskName /F 2>&1 | Out-Null } catch {}
    }

    if (Test-Path $InstallDir) {
        Write-Host "Removing install directory: $InstallDir"
        Remove-Item -Path $InstallDir -Recurse -Force
    }

    Write-Host ""
    Write-Host "Removal complete."
    exit 0
}

# ---- Install ----------------------------------------------------------------

$SearchPaths = @(
    $PSScriptRoot,
    (Join-Path $PSScriptRoot "WindowsTaskbarRemoval\bin\Release\net8.0-windows"),
    (Join-Path $PSScriptRoot "WindowsTaskbarRemoval\bin\Debug\net8.0-windows"),
    (Join-Path $PSScriptRoot "bin\Release\net8.0-windows"),
    (Join-Path $PSScriptRoot "bin\Debug\net8.0-windows")
)

$SourceExe = $null
foreach ($path in $SearchPaths) {
    $candidate = Join-Path $path $ExeName
    if (Test-Path $candidate) {
        $SourceExe = $candidate
        break
    }
}

if (-not $SourceExe) {
    Write-Error "Could not find $ExeName. Build the project first, then re-run this installer."
    exit 1
}

$SourceDir = Split-Path $SourceExe -Parent
Write-Host "Source      : $SourceDir"
Write-Host "Install dir : $InstallDir"
Write-Host ""

Write-Host "Copying files..."

if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir | Out-Null
}

Copy-Item -Path (Join-Path $SourceDir "*") -Destination $InstallDir -Recurse -Force

$InstalledExe = Join-Path $InstallDir $ExeName

if (-not (Test-Path $InstalledExe)) {
    Write-Error "Copy succeeded but $ExeName was not found in $InstallDir. Something went wrong."
    exit 1
}

Write-Host "Files copied."
Write-Host "Registering scheduled task..."

if (Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue) {
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
}

$Action = New-ScheduledTaskAction -Execute $InstalledExe

# -AtLogOn with no -User means the task fires for any user that logs on.
$Trigger = New-ScheduledTaskTrigger -AtLogOn

$Settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -ExecutionTimeLimit (New-TimeSpan -Seconds 0) `
    -MultipleInstances IgnoreNew

# GroupId instead of UserId+LogonType Interactive avoids a silent registration
# failure that occurs when the script is launched from a non-interactive
# elevated process (i.e. via install.bat / Start-Process -Verb RunAs).
#
# BUILTIN\Users covers every local account (standard and administrator alike).
# RunLevel Limited is correct — the application runs as the logged-on user
# without elevation.  The app manifest already declares asInvoker, and
# ManagedShell's HideExplorerTaskbar does not require elevated privileges.
$Principal = New-ScheduledTaskPrincipal `
    -GroupId  "BUILTIN\Users" `
    -RunLevel Limited

$Task = New-ScheduledTask `
    -Action      $Action `
    -Trigger     $Trigger `
    -Settings    $Settings `
    -Principal   $Principal `
    -Description "Hides the Windows taskbar at logon using WindowsTaskbarRemoval (all users)."

Register-ScheduledTask -TaskName $TaskName -InputObject $Task -Force | Out-Null

Write-Host "Scheduled task registered."

Write-Host "Starting $AppName for the current user..."
Start-Process -FilePath $InstalledExe

Write-Host ""
Write-Host "Installation complete."
Write-Host "  Installed to : $InstallDir"
Write-Host "  Task name    : $TaskName"
Write-Host "  Runs at      : Logon (all users)"
