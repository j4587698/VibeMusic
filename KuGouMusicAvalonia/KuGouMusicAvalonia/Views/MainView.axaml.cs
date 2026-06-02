using System;
using Avalonia;
using Avalonia.Controls;
using KuGouMusicAvalonia.ViewModels;

namespace KuGouMusicAvalonia.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
        {
            UpdateLayoutMode();
        }
    }

    private MainViewModel? _viewModel;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        UpdateLayoutMode();

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _viewModel = DataContext as MainViewModel;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsQueueOpen))
        {
            if (_viewModel?.IsQueueOpen == true && _viewModel.Player.CurrentSong != null)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    QueueListBox.ScrollIntoView(_viewModel.Player.CurrentSong);
                }, Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }
    }

    private void UpdateLayoutMode()
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.IsCompactLayout = Bounds.Width > 0 && Bounds.Width < 760;
            if (Bounds.Width > 0)
            {
                viewModel.QueuePopupWidth = Math.Clamp(Bounds.Width - 32, 320, 420);
            }
        }
    }
}