using Avalonia.Controls;
using KuGouMusicAvalonia.ViewModels;

namespace KuGouMusicAvalonia.Views;

public partial class HistoryView : UserControl
{
    public HistoryView()
    {
        InitializeComponent();
    }
    
    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is HistoryViewModel vm)
        {
            vm.LoadHistory();
        }
    }
}
