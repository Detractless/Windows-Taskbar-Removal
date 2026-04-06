# WindowsTaskbarRemoval

A lightweight Windows utility that suppresses the native Explorer taskbar using the Windows AppBar API, with automatic recovery when Explorer restarts and a clean system-tray exit path.

---

## Purpose

WindowsTaskbarRemoval is designed for scenarios where the Windows taskbar must be permanently hidden without relying on auto-hide, group policy, or shell replacement. Common use cases include kiosk deployments, custom launcher environments, presentation machines, and any setup where an alternative shell component occupies the bottom screen edge and the native taskbar must not reappear.

The application hides the taskbar cleanly using the same shell interop mechanisms that Microsoft documents for third-party shell components. It survives Explorer crashes and restarts, restores the taskbar on exit, and requires no user interaction beyond the initial install.

---

## How It Works

### The Core Mechanism

Windows exposes a flag through its shell interop layer (`HideExplorerTaskbar`) that instructs Explorer to hide its own taskbar window via `ShowWindow(Shell_TrayWnd, SW_HIDE)`. This is the same mechanism used by alternative shell applications. Setting it to `true` causes the native taskbar to disappear; setting it back to `false` restores it.

Hiding the taskbar alone is not sufficient. The Windows shell reserves a strip of screen real estate at the bottom of the display (the "AppBar deskband") for the taskbar. If nothing else claims that space, other maximized windows will not use it correctly. WindowsTaskbarRemoval solves this by registering a ghost AppBar window at the bottom edge — a fully transparent, zero-height WPF window that holds the screen reservation, preventing layout gaps without being visible.

### Explorer Restart Recovery

Explorer broadcasts the registered window message `TaskbarCreated` to every top-level window whenever it recreates its taskbar. This happens after an Explorer crash, after a manual restart via Task Manager, or during certain system updates. Without handling this message, a restarted Explorer would make its taskbar visible again, overriding the hidden state.

`ExplorerMonitor` maintains a lightweight native window (a raw `NativeWindow` message sink rather than a WPF window) that registers for `TaskbarCreated` via `RegisterWindowMessage`. When the message arrives, it marshals back onto the WPF dispatcher and instructs `WindowManager` to close and reopen the ghost AppBar, immediately re-suppressing the reborn native taskbar.

### Graceful Shutdown

When the application exits — whether through the tray icon menu or a Windows session end event — `WindowManager.Dispose()` closes the ghost AppBar windows first (so the AppBar API can release the screen edge reservation cleanly via `ABM_REMOVE`), then sets `HideExplorerTaskbar = false` to restore the native taskbar. This ensures the desktop is always left in a usable state.

---

## Architecture

```
App (WPF Application)
  |
  |-- ShellManager          ManagedShell host; owns AppBarManager,
  |                         ExplorerHelper, and FullScreenHelper
  |
  |-- ExplorerMonitor       NativeWindow message sink
  |     |                   Registers for WM_TASKBARCREATED
  |     |                   Calls WindowManager.ReopenBars() on receipt
  |
  |-- WindowManager         Lifecycle controller
  |     |                   Sets HideExplorerTaskbar = true on startup
  |     |                   Sets HideExplorerTaskbar = false on Dispose
  |     |
  |     |-- TaskbarRemover  Ghost AppBar window (one per screen)
  |                         Zero-height, fully transparent
  |                         Holds bottom-edge screen reservation
  |
  |-- NotifyIcon            System-tray icon (WinForms)
                            Right-click menu and double-click to exit
```

### Component Breakdown

**`Program.cs`** — Explicit entry point marked `[STAThread]`. WPF requires the UI thread to run as a Single-Threaded Apartment; declaring a manual entry point makes this unambiguous rather than relying on the XAML-generated entry point.

**`App.xaml / App.xaml.cs`** — Application lifecycle host. Initializes `ShellManager` at construction time, creates `ExplorerMonitor` and `WindowManager` on startup, tears everything down on exit, and owns the system-tray icon.

**`TaskbarRemover.cs`** — Subclass of ManagedShell's `AppBarWindow`. Registers as a zero-height, transparent WPF window at the bottom screen edge. The window is invisible to the user but satisfies the Win32 `SHAppBarMessage` geometry requirements that hold the screen reservation.

**`WindowManager.cs`** — Owns the list of active `TaskbarRemover` instances and the `HideExplorerTaskbar` flag. Responsible for opening bars on startup, reopening them when Explorer restarts, and restoring the native taskbar on disposal.

**`ExplorerMonitor.cs`** — Contains an inner `NativeWindow` that listens for the shell broadcast message `TaskbarCreated`. Uses a raw Win32 message sink rather than a WPF window to stay lightweight and to remain active before any WPF window has been created.

**`install.ps1 / install.bat`** — Installer and uninstaller. Copies the build output to `%ProgramFiles%\WindowsTaskbarRemoval`, registers a Windows Task Scheduler task under `Detractless\WindowsTaskbarRemoval` that launches the application at logon for all Administrators, and optionally removes everything cleanly.

**`build.ps1 / build.bat`** — Build helpers wrapping `dotnet build` and `dotnet publish`. Support Debug and Release configurations, and optionally produce a self-contained single-file executable via `PublishSingleFile`.

### Dependencies

| Dependency | Purpose |
|---|---|
| .NET 8 (net8.0-windows) | Runtime framework |
| WPF | Window and application model |
| Windows Forms | System-tray NotifyIcon |
| ManagedShell (v0.0.330) | AppBar API, ExplorerHelper (HideExplorerTaskbar), ShellManager |

---

## Requirements

- Windows 10 or Windows 11
- .NET 8 SDK (build) or .NET 8 Runtime (run)
- Administrator privileges (install/uninstall only)

---

## Building

A .bat wrapper is provided so PowerShell execution policy is handled automatically.

**Debug build (default)**
```bat
build.bat
```

**Release build**
```bat
build.bat Release
```

**Release self-contained single-file executable**
```bat
build.bat Release publish
```

Output locations:

| Mode | Output path |
|---|---|
| Debug | `WindowsTaskbarRemoval\bin\Debug\net8.0-windows\` |
| Release | `WindowsTaskbarRemoval\bin\Release\net8.0-windows\` |
| Publish | `WindowsTaskbarRemoval\bin\Release\net8.0-windows\win-x64\publish\` |

---

## Installing

The installer must run as Administrator. Double-clicking `install.bat` triggers an automatic UAC elevation prompt.

```bat
install.bat
```

The script presents two options:

- **A — Install**: Copies the build output to `%ProgramFiles%\WindowsTaskbarRemoval` and registers a Task Scheduler task at `Detractless\WindowsTaskbarRemoval` that runs the application at logon for all users (standard accounts and administrators alike). The executable is also launched immediately for the current user without waiting for the next logon.

- **B — Remove**: Terminates any running instance, restarts Explorer to ensure the native taskbar is restored, removes the scheduled task, and deletes the install directory.

The installer searches the following paths for the executable, in order:

1. The script's own directory
2. `WindowsTaskbarRemoval\bin\Release\net8.0-windows`
3. `WindowsTaskbarRemoval\bin\Debug\net8.0-windows`
4. `bin\Release\net8.0-windows`
5. `bin\Debug\net8.0-windows`

Build the project before running the installer.

---

## Running Without Installing

The executable can be run directly without the installer. Launch `WindowsTaskbarRemoval.exe` from the build output directory. The taskbar will be hidden immediately. Exit via the system-tray icon (right-click, then "Exit WindowsTaskbarRemoval", or double-click) to restore the taskbar and terminate cleanly.

Do not terminate the process via Task Manager's "End task" without first exiting through the tray icon. Killing the process without a graceful shutdown will leave the native taskbar hidden until Explorer restarts.

---

## Uninstalling Manually

If the installer is unavailable, the following steps restore the system to its original state:

1. Right-click the tray icon and select "Exit WindowsTaskbarRemoval", or end the process through Task Manager and then restart Explorer (`explorer.exe`).
2. Remove the scheduled task: open Task Scheduler, navigate to `Detractless\WindowsTaskbarRemoval`, and delete it.
3. Delete `%ProgramFiles%\WindowsTaskbarRemoval`.

---

## Limitations

- Multi-monitor support is not currently implemented. The ghost AppBar is registered on the primary screen only. Secondary monitors retain their default taskbar behavior.
- The application runs as the logged-on user (manifest level `asInvoker`). No elevated privileges are required at runtime.
- The scheduled task principal is `BUILTIN\Users`, so the application runs at logon for all local user accounts, including standard (non-administrator) accounts.
