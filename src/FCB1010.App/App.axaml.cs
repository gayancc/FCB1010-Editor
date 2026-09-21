using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FCB1010.App.Services;
using FCB1010.App.ViewModels;
using FCB1010.App.Views;

namespace FCB1010.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var args = Environment.GetCommandLineArgs();
            var dialogs = new DesktopDialogService();
            var vm = new MainViewModel(dialogs);
            var window = new MainWindow { DataContext = vm };
            dialogs.Attach(window);
            desktop.MainWindow = window;
            window.Closed += (_, _) => vm.Dispose();

            ApplyCaptureArgs(window, args);
        }
        base.OnFrameworkInitializationCompleted();
    }

    private static void ApplyCaptureArgs(MainWindow window, string[] args)
    {
        var sizeIdx = Array.FindIndex(args, a => a.Equals("--size", StringComparison.OrdinalIgnoreCase));
        if (sizeIdx >= 0 && sizeIdx + 1 < args.Length)
        {
            var parts = args[sizeIdx + 1].Split('x', 'X');
            if (parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h))
            {
                window.Width = w;
                window.Height = h;
            }
        }

        if (args.Any(a => a.Equals("--hits", StringComparison.OrdinalIgnoreCase))
            && window.DataContext is MainViewModel hitsVm)
            hitsVm.ShowHitRegions = true;

        var captureIdx = Array.FindIndex(args, a => a.Equals("--capture", StringComparison.OrdinalIgnoreCase));
        if (captureIdx < 0 || captureIdx + 1 >= args.Length) return;

        var path = args[captureIdx + 1];
        window.Opened += async (_, _) =>
        {
            await Task.Delay(700);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var size = new PixelSize(
                    Math.Max(8, (int)Math.Ceiling(window.Bounds.Width)),
                    Math.Max(8, (int)Math.Ceiling(window.Bounds.Height)));
                using var bitmap = new RenderTargetBitmap(size, new Vector(96, 96));
                bitmap.Render(window);
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                bitmap.Save(path);
            });
            await Task.Delay(150);
            window.Close();
        };
    }
}
