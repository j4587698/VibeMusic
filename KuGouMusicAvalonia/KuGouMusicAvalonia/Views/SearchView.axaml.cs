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
}
