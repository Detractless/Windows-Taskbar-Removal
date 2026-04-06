using ManagedShell;
using ManagedShell.Common.Helpers;
using ManagedShell.Interop;
using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace WindowsTaskbarRemoval
{
    /// <summary>
    /// Application lifecycle: boots ManagedShell, creates the WindowManager,
    /// and provides a system-tray icon so the user can exit cleanly.
    /// </summary>
    public partial class App : Application
    {
        private WindowManager? _windowManager;
        private ExplorerMonitor? _explorerMonitor;
        private NotifyIcon? _trayIcon;
        private readonly ShellManager _shellManager;

        // ── Construction ──────────────────────────────────────────────────────
        public App()
        {
            // ManagedShell needs to know whether we are THE shell (explorer replacement)
            // or just an overlay.  We are an overlay, so this will almost always be false.
            EnvironmentHelper.IsAppRunningAsShell = NativeMethods.GetShellWindow() == IntPtr.Zero;

            _shellManager = new ShellManager(ShellManager.DefaultShellConfig);
        }

        // ── Startup ───────────────────────────────────────────────────────────
        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            BuildTrayIcon();

            _explorerMonitor = new ExplorerMonitor();
            _windowManager   = new WindowManager(_shellManager, _explorerMonitor);
        }

        // ── Graceful exit (called from tray menu or OS session end) ───────────
        public void ExitGracefully()
        {
            // Tell ManagedShell's AppBarManager we are shutting down so it can
            // process pending ABM_REMOVE messages before we destroy windows.
            _shellManager.AppBarManager.SignalGracefulShutdown();
            Current.Shutdown();
        }

        private void App_OnExit(object sender, ExitEventArgs e)
        {
            _trayIcon?.Dispose();

            _windowManager?.Dispose();   // restores HideExplorerTaskbar = false
            _explorerMonitor?.Dispose();
            _shellManager.Dispose();
        }

        // ── System-tray icon ──────────────────────────────────────────────────
        private void BuildTrayIcon()
        {
            // Use the built-in application icon — no embedded resource needed.
            var menu = new ContextMenuStrip();
            menu.Items.Add("Exit WindowsTaskbarRemoval", null, (_, _) => ExitGracefully());

            _trayIcon = new NotifyIcon
            {
                Icon    = SystemIcons.Application,
                Text    = "WindowsTaskbarRemoval  --  right-click to exit",
                Visible = true,
                ContextMenuStrip = menu
            };

            // Also allow double-click to exit
            _trayIcon.DoubleClick += (_, _) => ExitGracefully();
        }
    }
}
