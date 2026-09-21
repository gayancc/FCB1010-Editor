using Avalonia.Controls;
using Avalonia.Input;
using FCB1010.App.Controls;
using FCB1010.App.ViewModels;

namespace FCB1010.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        // Don't steal keys from text / scrub-value editors.
        if (e.Source is TextBox or ScrubValueField) return;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            switch (e.Key)
            {
                case Key.S:
                    if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) _ = vm.WriteDeviceCommand.ExecuteAsync(null);
                    else _ = vm.SaveCommand.ExecuteAsync(null);
                    e.Handled = true;
                    break;
                case Key.Z:
                    if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) vm.RedoCommand.Execute(null);
                    else vm.UndoCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.Y:
                    vm.RedoCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.O:
                    _ = vm.OpenCommand.ExecuteAsync(null);
                    e.Handled = true;
                    break;
                case Key.C:
                    vm.CopyPresetCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.V:
                    vm.PastePresetCommand.Execute(null);
                    e.Handled = true;
                    break;
            }
            return;
        }

        if (e.Key is >= Key.D1 and <= Key.D9) { vm.SelectFootswitchCommand.Execute((int)e.Key - (int)Key.D0); e.Handled = true; }
        else if (e.Key == Key.D0) { vm.SelectFootswitchCommand.Execute(10); e.Handled = true; }
        else if (e.Key == Key.PageUp) { vm.BankUpCommand.Execute(null); e.Handled = true; }
        else if (e.Key == Key.PageDown) { vm.BankDownCommand.Execute(null); e.Handled = true; }
    }
}
