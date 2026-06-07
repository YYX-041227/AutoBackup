using System;
using Avalonia;
using Avalonia.Fonts.Inter;

namespace AutoBackup
{
    internal sealed class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            if (!App.InitializeSingleInstance(args))
            {
                return 0;
            }

            try
            {
                return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            finally
            {
                App.ReleaseSingleInstance();
            }
        }

        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
        }
    }
}
