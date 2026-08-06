using Avalonia;
using System;

namespace DiskGeek.App;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args)
    {
        // Headless "scheduled scan" mode: --scan <folder> plus one or more of --export-csv,
        // --export-html, --snapshot-save, --snapshot-compare. No window is created - this is meant
        // to be invoked by Windows Task Scheduler on a timer. See CommandLineScanRunner for why
        // this is the honest way to deliver "scheduled scans" rather than a custom in-app scheduler.
        if (CommandLineScanRunner.TryGetScanPath(args, out _))
            return CommandLineScanRunner.Run(args);

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
