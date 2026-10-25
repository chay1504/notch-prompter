using System;
using System.Threading;
using System.Windows;

namespace NotchPrompter
{
    internal sealed class Options
    {
        public bool Expand, Play, Edit, CaptureVisible, AutoStart, Quit;

        public static Options Parse(string[] args)
        {
            var o = new Options();
            foreach (var raw in args)
            {
                switch (raw.Trim().ToLowerInvariant())
                {
                    case "--expand": o.Expand = true; break;
                    case "--play": o.Play = true; break;
                    case "--edit": o.Edit = true; break;
                    case "--capture-visible": o.CaptureVisible = true; break;   // testing: show up in screenshots
                    case "--autostart": o.AutoStart = true; break;               // launched at sign-in
                    case "--quit": o.Quit = true; break;                         // ask the running copy to exit
                }
            }
            return o;
        }
    }

    internal static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            var opts = Options.Parse(args);
            using var instance = new Mutex(true, @"Local\NotchPrompter.Instance", out bool firstInstance);
            if (!firstInstance)
            {
                Signal(opts.Quit ? AppController.QuitSignal : AppController.ShowSignal);
                return 0;
            }
            if (opts.Quit) return 0;

            System.Windows.Forms.Application.EnableVisualStyles();
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.DispatcherUnhandledException += (s, e) =>
            {
                Log.Write("unhandled: " + e.Exception);
                e.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Log.Write("fatal: " + e.ExceptionObject);

            var controller = new AppController(opts);
            app.Startup += (s, e) => controller.Start();
            app.SessionEnding += (s, e) => controller.Exit();
            app.Run();
            GC.KeepAlive(instance);
            return 0;
        }

        static void Signal(string name)
        {
            try
            {
                using var ev = EventWaitHandle.OpenExisting(name);
                ev.Set();
            }
            catch (Exception ex)
            {
                Log.Write("could not signal running instance: " + ex.Message);
            }
        }
    }
}
