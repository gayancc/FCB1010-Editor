using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace FCB1010.App.Services;

public interface IAppDialogs
{
    Task AlertAsync(string title, string message);
    Task<bool> ConfirmAsync(string title, string message);
    Task<string?> OpenFileAsync(string title, params FilePickerFileType[] types);
    Task<string?> SaveFileAsync(string title, string? suggestedName, params FilePickerFileType[] types);
}

public sealed class DesktopDialogService : IAppDialogs
{
    private Window? _window;
    public void Attach(Window window) => _window = window;

    public async Task AlertAsync(string title, string message)
    {
        if (_window is null) return;
        var dialog = new Window
        {
            Title = title,
            Width = 560,
            MinHeight = 220,
            MaxHeight = 560,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = BuildMessage(message, "OK", null, out var ok),
        };
        ok.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(_window);
    }

    public async Task<bool> ConfirmAsync(string title, string message)
    {
        if (_window is null) return false;
        var result = false;
        var dialog = new Window
        {
            Title = title,
            Width = 560,
            MinHeight = 240,
            MaxHeight = 560,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        dialog.Content = BuildMessage(message, "Confirm", "Cancel", out var ok, out var cancel);
        ok.Click += (_, _) => { result = true; dialog.Close(); };
        cancel!.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(_window);
        return result;
    }

    public async Task<string?> OpenFileAsync(string title, params FilePickerFileType[] types)
    {
        if (_window is null) return null;
        var files = await _window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = types.Length == 0 ? null : types,
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> SaveFileAsync(string title, string? suggestedName, params FilePickerFileType[] types)
    {
        if (_window is null) return null;
        var file = await _window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            FileTypeChoices = types.Length == 0 ? null : types,
        });
        return file?.TryGetLocalPath();
    }

    private static Control BuildMessage(string message, string okText, string? cancelText, out Button ok)
        => BuildMessage(message, okText, cancelText, out ok, out _);

    private static Control BuildMessage(string message, string okText, string? cancelText, out Button ok, out Button? cancel)
    {
        ok = new Button { Content = okText, MinWidth = 88 };
        cancel = cancelText is null ? null : new Button { Content = cancelText, MinWidth = 88 };
        ok.SetValue(AutomationProperties.AutomationIdProperty, "DialogPrimary");
        ok.SetValue(AutomationProperties.NameProperty, okText);
        cancel?.SetValue(AutomationProperties.AutomationIdProperty, "DialogCancel");
        if (cancel is not null) cancel.SetValue(AutomationProperties.NameProperty, cancelText);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        if (cancel is not null) buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        var root = new DockPanel { Margin = new Avalonia.Thickness(20) };
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(new ScrollViewer
        {
            MaxHeight = 420,
            Margin = new Avalonia.Thickness(0, 0, 0, 18),
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
        });
        return root;
    }
}

public static class FileTypes
{
    public static FilePickerFileType FcbFiles { get; } = new("FCB1010 files") { Patterns = ["*.syx", "*.fcbproject"] };
    public static FilePickerFileType SysEx { get; } = new("SysEx") { Patterns = ["*.syx"] };
    public static FilePickerFileType Project { get; } = new("FCB project") { Patterns = ["*.fcbproject"] };
    public static FilePickerFileType Json { get; } = new("JSON") { Patterns = ["*.json"] };
}
