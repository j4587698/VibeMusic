using Avalonia.Controls;
using Avalonia.Input;
using KuGouMusicAvalonia.ViewModels;

namespace KuGouMusicAvalonia.Views;

public partial class SearchView : UserControl
{
    public SearchView()
    {
        InitializeComponent();
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is SearchViewModel vm)
        {
            if (vm.SearchCommand.CanExecute(null))
            {
                vm.SearchCommand.Execute(null);
            }
        }
    }

    private void OnRecordButtonPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is SearchViewModel vm)
        {
            if (sender is Control control)
            {
                e.Pointer.Capture(control);
            }
            if (vm.StartRecordingCommand.CanExecute(null))
            {
                vm.StartRecordingCommand.Execute(null);
            }
        }
    }

    private void OnRecordButtonReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is SearchViewModel vm)
        {
            if (sender is Control control)
            {
                e.Pointer.Capture(null);
            }
            if (vm.StopRecordingAndMatchCommand.CanExecute(null))
            {
                vm.StopRecordingAndMatchCommand.Execute(null);
            }
        }
    }
}
