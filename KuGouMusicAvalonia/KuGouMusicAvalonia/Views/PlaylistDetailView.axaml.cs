using Avalonia.Controls;

namespace KuGouMusicAvalonia.Views;

public partial class PlaylistDetailView : UserControl
{
    public PlaylistDetailView()
    {
        InitializeComponent();
    }

    private void OnSentinelAttached(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        if (DataContext is ViewModels.PlaylistDetailViewModel vm)
        {
            if (vm.LoadNextPageCommand.CanExecute(null))
            {
                vm.LoadNextPageCommand.Execute(null);
            }
        }
    }
}