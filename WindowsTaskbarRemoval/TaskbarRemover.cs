using ManagedShell;
using ManagedShell.AppBar;
using System;

namespace WindowsTaskbarRemoval
{
    /// <summary>
    /// A near-invisible AppBar window that occupies the bottom screen edge.
    /// Built entirely in code so we avoid the XAML root-element restriction
    /// that prevents AppBarWindow from being used as a XAML root tag.
    ///
    /// Height strategy: 2 logical pixels, fully transparent background.
    /// Invisible to the eye but satisfies SHAppBarMessage geometry requirements.
    /// </summary>
    public class TaskbarRemover : AppBarWindow
    {
        private const double GhostSize = 0.0;

        public TaskbarRemover(
            ShellManager shellManager,
            AppBarScreen screen,
            AppBarEdge edge,
            AppBarMode mode)
            : base(
                shellManager.AppBarManager,
                shellManager.ExplorerHelper,
                shellManager.FullScreenHelper,
                screen,
                edge,
                mode,
                GhostSize)
        {
            DesiredHeight = GhostSize;
            DesiredWidth  = GhostSize;

            WindowStyle      = System.Windows.WindowStyle.None;
            ResizeMode       = System.Windows.ResizeMode.NoResize;
            ShowInTaskbar    = false;
            AllowsTransparency = true;
            Background       = System.Windows.Media.Brushes.Transparent;

            Content = new System.Windows.Controls.Grid();
        }

        protected override void OnSourceInitialized(object sender, EventArgs e)
        {
            base.OnSourceInitialized(sender, e);
        }

        protected override void CustomClosing() { }

        protected override void SetScreenProperties(ScreenSetupReason reason)
        {
            base.SetScreenProperties(reason);
        }
    }
}
