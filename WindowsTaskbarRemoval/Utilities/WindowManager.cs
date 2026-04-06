using ManagedShell;
using ManagedShell.AppBar;
using ManagedShell.Common.Logging;
using System;
using System.Collections.Generic;

namespace WindowsTaskbarRemoval
{
    /// <summary>
    /// Creates / destroys TaskbarRemover windows and owns the
    /// <c>HideExplorerTaskbar</c> flag that suppresses the native shell bar.
    ///
    /// Lifecycle
    /// ─────────
    ///   ctor      → hide native taskbar, open ghost bar(s)
    ///   Dispose() → close ghost bar(s), restore native taskbar
    ///
    /// The restore on Dispose is critical: if WindowsTaskbarRemoval exits without
    /// setting HideExplorerTaskbar = false the user's desktop is left with
    /// no visible taskbar at all until Explorer restarts.
    /// </summary>
    public sealed class WindowManager : IDisposable
    {
        private readonly List<TaskbarRemover> _bars   = new();
        private readonly ShellManager       _shell;
        private readonly ExplorerMonitor    _monitor;
        private          bool               _disposed;

        public WindowManager(ShellManager shell, ExplorerMonitor monitor)
        {
            _shell   = shell;
            _monitor = monitor;

            // ── Step 1: Hide the real Explorer taskbar ────────────────────────
            // ManagedShell's ExplorerHelper calls ShowWindow(Shell_TrayWnd, SW_HIDE)
            // (and mirrors it on secondary monitor bars).  Setting the property
            // back to false in Dispose() calls SW_SHOW to restore everything.
            _shell.ExplorerHelper.HideExplorerTaskbar = true;

            // ── Step 2: Open our ghost AppBar ────────────────────────────────
            OpenBars();

            // ── Step 3: Watch for Explorer restarts ──────────────────────────
            // Explorer broadcasts WM_TASKBARCREATED whenever it recreates its
            // taskbar (crash recovery, manual restart via Task Manager, etc.).
            // ExplorerMonitor listens for that message and calls ReopenBars()
            // so we immediately re-suppress the reborn native bar.
            _monitor.Start(this, _shell);
        }

        // ── Called by ExplorerMonitor on TaskbarCreated ───────────────────────
        public void ReopenBars()
        {
            ShellLogger.Debug("WindowManager: Reopening ghost bars");
            CloseBars();
            OpenBars();
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private void OpenBars()
        {
            // Single primary screen only.  Extend to AppBarScreen.FromAllScreens()
            // if you ever want multi-monitor support.
            var screen = AppBarScreen.FromPrimaryScreen();

            var bar = new TaskbarRemover(
                _shell,
                screen,
                AppBarEdge.Bottom,   // sit at the bottom edge, same as default taskbar
                AppBarMode.Normal);

            bar.Show();
            _bars.Add(bar);

            ShellLogger.Debug("WindowManager: Ghost bar opened");
        }

        private void CloseBars()
        {
            foreach (var bar in _bars)
            {
                bar.AllowClose = true;
                bar.Close();
            }
            _bars.Clear();
        }

        // ── IDisposable ───────────────────────────────────────────────────────
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            CloseBars();

            // Restore the real taskbar — MUST happen after our windows are closed
            // so ABM_REMOVE has already freed the screen-edge reservation.
            _shell.ExplorerHelper.HideExplorerTaskbar = false;

            ShellLogger.Debug("WindowManager: Disposed; native taskbar restored");
        }
    }
}
