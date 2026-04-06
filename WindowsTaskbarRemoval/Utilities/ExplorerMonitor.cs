using ManagedShell;
using ManagedShell.Common.Logging;
using ManagedShell.Interop;
using System;
using System.Windows.Forms;
using System.Windows.Threading;

namespace WindowsTaskbarRemoval
{
    /// <summary>
    /// Listens for the shell-broadcast message "TaskbarCreated".
    ///
    /// Windows Explorer sends this message to every top-level window whenever
    /// it (re)creates the native taskbar — on startup, after a crash, or when
    /// the user manually restarts Explorer via Task Manager.
    ///
    /// Without this monitor, a restarted Explorer would make its taskbar
    /// visible again (overriding WindowsTaskbarRemoval's HideExplorerTaskbar flag).
    /// When we receive TaskbarCreated we simply reopen our ghost bar, which
    /// re-asserts the hidden state.
    ///
    /// Implementation note: we use a raw NativeWindow (a message-only HWND)
    /// rather than a WPF window because we need to be listening before any
    /// WPF window exists, and a plain HWND is lighter weight.
    /// </summary>
    public sealed class ExplorerMonitor : IDisposable
    {
        private MonitorWindow? _window;

        /// <summary>
        /// Start listening.  Safe to call only once.
        /// </summary>
        public void Start(WindowManager manager, ShellManager shell)
        {
            if (_window is not null) return;
            _window = new MonitorWindow(manager, shell);
        }

        public void Dispose() => _window?.Dispose();

        // ── Inner NativeWindow ────────────────────────────────────────────────

        private sealed class MonitorWindow : NativeWindow, IDisposable
        {
            private readonly WindowManager _manager;

            // RegisterWindowMessage returns a unique message ID agreed upon
            // by every process that calls it with the same string — that is
            // how Explorer notifies third-party shell components.
            private static readonly int WM_TASKBARCREATED =
                NativeMethods.RegisterWindowMessage("TaskbarCreated");

            public MonitorWindow(WindowManager manager, ShellManager _)
            {
                _manager = manager;

                // CreateParams with no parent → message-only window (HWND_MESSAGE
                // would be more correct but CreateParams() defaults are fine here).
                CreateHandle(new CreateParams());
                ShellLogger.Debug("ExplorerMonitor: Listening for TaskbarCreated");
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_TASKBARCREATED)
                {
                    ShellLogger.Debug("ExplorerMonitor: TaskbarCreated received — reopening bars");

                    // Marshal back onto the WPF/UI dispatcher so we can safely
                    // create WPF windows (GhostTaskbar is a WPF Window).
                    Dispatcher.CurrentDispatcher.BeginInvoke(() =>
                    {
                        try   { _manager.ReopenBars(); }
                        catch (Exception ex)
                        {
                            ShellLogger.Warning(
                                $"ExplorerMonitor: Error during reopen — {ex.Message}");
                        }
                    });
                }

                base.WndProc(ref m);
            }

            public void Dispose()
            {
                DestroyHandle();
                ShellLogger.Debug("ExplorerMonitor: Disposed");
            }
        }
    }
}
