using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System.Windows.Input;

namespace KuGouMusicAvalonia.Controls;

public partial class MusicSongItem : UserControl
{
    public MusicSongItem()
    {
        InitializeComponent();
    }

    public static readonly StyledProperty<ICommand?> PlayCommandProperty =
        AvaloniaProperty.Register<MusicSongItem, ICommand?>(nameof(PlayCommand));

    public ICommand? PlayCommand
    {
        get => GetValue(PlayCommandProperty);
        set => SetValue(PlayCommandProperty, value);
    }

    public static readonly StyledProperty<ICommand?> AddToQueueCommandProperty =
        AvaloniaProperty.Register<MusicSongItem, ICommand?>(nameof(AddToQueueCommand));

    public ICommand? AddToQueueCommand
    {
        get => GetValue(AddToQueueCommandProperty);
        set => SetValue(AddToQueueCommandProperty, value);
    }

    public static readonly StyledProperty<ICommand?> DownloadCommandProperty =
        AvaloniaProperty.Register<MusicSongItem, ICommand?>(nameof(DownloadCommand));

    public ICommand? DownloadCommand
    {
        get => GetValue(DownloadCommandProperty);
        set => SetValue(DownloadCommandProperty, value);
    }

    public static readonly StyledProperty<string> PlayButtonToolTipProperty =
        AvaloniaProperty.Register<MusicSongItem, string>(nameof(PlayButtonToolTip), "播放/暂停");

    public string PlayButtonToolTip
    {
        get => GetValue(PlayButtonToolTipProperty);
        set => SetValue(PlayButtonToolTipProperty, value);
    }

    public static readonly StyledProperty<bool> ShowDownloadButtonProperty =
        AvaloniaProperty.Register<MusicSongItem, bool>(nameof(ShowDownloadButton), true);

    public bool ShowDownloadButton
    {
        get => GetValue(ShowDownloadButtonProperty);
        set => SetValue(ShowDownloadButtonProperty, value);
    }

    public static readonly StyledProperty<IBrush?> ItemBackgroundProperty =
        AvaloniaProperty.Register<MusicSongItem, IBrush?>(nameof(ItemBackground));

    public IBrush? ItemBackground
    {
        get => GetValue(ItemBackgroundProperty);
        set => SetValue(ItemBackgroundProperty, value);
    }

    public static readonly StyledProperty<IBrush?> ItemBorderBrushProperty =
        AvaloniaProperty.Register<MusicSongItem, IBrush?>(nameof(ItemBorderBrush));

    public IBrush? ItemBorderBrush
    {
        get => GetValue(ItemBorderBrushProperty);
        set => SetValue(ItemBorderBrushProperty, value);
    }

    public static readonly StyledProperty<double> CoverWidthProperty =
        AvaloniaProperty.Register<MusicSongItem, double>(nameof(CoverWidth), 46.0);

    public double CoverWidth
    {
        get => GetValue(CoverWidthProperty);
        set => SetValue(CoverWidthProperty, value);
    }

    public static readonly StyledProperty<double> CoverHeightProperty =
        AvaloniaProperty.Register<MusicSongItem, double>(nameof(CoverHeight), 46.0);

    public double CoverHeight
    {
        get => GetValue(CoverHeightProperty);
        set => SetValue(CoverHeightProperty, value);
    }
}
