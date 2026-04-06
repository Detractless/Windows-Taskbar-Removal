using System;

namespace WindowsTaskbarRemoval
{
    /// <summary>
    /// Explicit entry point so we can mark [STAThread] unambiguously.
    /// WPF requires the UI thread to be Single-Threaded Apartment.
    /// </summary>
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            App app = new App();
            app.InitializeComponent();
            app.Run();
        }
    }
}
