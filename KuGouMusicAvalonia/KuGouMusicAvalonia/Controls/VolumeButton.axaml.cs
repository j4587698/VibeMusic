using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Material.Icons;
using System;

namespace KuGouMusicAvalonia.Controls;

public partial class VolumeButton : UserControl
{
    public static readonly StyledProperty<double> VolumeProperty =
        AvaloniaProperty.Register<VolumeButton, double>(nameof(Volume), 70, defaultBindingMode: BindingMode.TwoWay);

    public static readonly DirectProperty<VolumeButton, string> VolumeTextProperty =
        AvaloniaProperty.RegisterDirect<VolumeButton, string>(nameof(VolumeText), control => control.VolumeText);

    public static readonly DirectProperty<VolumeButton, string> ToolTipTextProperty =
        AvaloniaProperty.RegisterDirect<VolumeButton, string>(nameof(ToolTipText), control => control.ToolTipText);

    public static readonly DirectProperty<VolumeButton, string> MuteToolTipProperty =
        AvaloniaProperty.RegisterDirect<VolumeButton, string>(nameof(MuteToolTip), control => control.MuteToolTip);

    public static readonly DirectProperty<VolumeButton, MaterialIconKind> VolumeIconProperty =
        AvaloniaProperty.RegisterDirect<VolumeButton, MaterialIconKind>(nameof(VolumeIcon), control => control.VolumeIcon);

    public static readonly DirectProperty<VolumeButton, double> SelectionHeightProperty =
        AvaloniaProperty.RegisterDirect<VolumeButton, double>(nameof(SelectionHeight), control => control.SelectionHeight);

    public static readonly DirectProperty<VolumeButton, Thickness> ThumbMarginProperty =
        AvaloniaProperty.RegisterDirect<VolumeButton, Thickness>(nameof(ThumbMargin), control => control.ThumbMargin);

    private const double SliderHeight = 106;
    private const double SliderThumbSize = 10;
    private const double SliderTrackRange = SliderHeight - SliderThumbSize;
    private double _lastAudibleVolume = 70;
    private double _selectionHeight = SliderTrackRange * 0.7;
    private Thickness _thumbMargin = new(0, 0, 0, SliderTrackRange * 0.7);
    private string _volumeText = "70";
    private string _toolTipText = "音量 70%";
    private string _muteToolTip = "静音";
    private MaterialIconKind _volumeIcon = MaterialIconKind.VolumeHigh;
    private bool _isDraggingVolume;

    public VolumeButton()
    {
        InitializeComponent();
        UpdateVolumeState(Volume);
    }

    public double Volume
    {
        get => GetValue(VolumeProperty);
        set => SetValue(VolumeProperty, Math.Clamp(value, 0, 100));
    }

    public string VolumeText
    {
        get => _volumeText;
        private set => SetAndRaise(VolumeTextProperty, ref _volumeText, value);
    }

    public string ToolTipText
    {
        get => _toolTipText;
        private set => SetAndRaise(ToolTipTextProperty, ref _toolTipText, value);
    }

    public string MuteToolTip
    {
        get => _muteToolTip;
        private set => SetAndRaise(MuteToolTipProperty, ref _muteToolTip, value);
    }

    public MaterialIconKind VolumeIcon
    {
        get => _volumeIcon;
        private set => SetAndRaise(VolumeIconProperty, ref _volumeIcon, value);
    }

    public double SelectionHeight
    {
        get => _selectionHeight;
        private set => SetAndRaise(SelectionHeightProperty, ref _selectionHeight, value);
    }

    public Thickness ThumbMargin
    {
        get => _thumbMargin;
        private set => SetAndRaise(ThumbMarginProperty, ref _thumbMargin, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == VolumeProperty)
        {
            UpdateVolumeState(change.GetNewValue<double>());
        }
    }

    private static void OpenFlyout_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Flyout: { } flyout } button)
        {
            flyout.ShowAt(button);
        }
    }

    private void ToggleMute_Click(object? sender, RoutedEventArgs e)
    {
        if (Volume <= 0)
        {
            SetCurrentValue(VolumeProperty, _lastAudibleVolume > 0 ? _lastAudibleVolume : 70);
            return;
        }

        _lastAudibleVolume = Volume;
        SetCurrentValue(VolumeProperty, 0);
    }

    private void UpdateVolumeState(double value)
    {
        var volume = Math.Clamp(value, 0, 100);
        if (volume > 0)
        {
            _lastAudibleVolume = volume;
        }

        VolumeText = $"{volume:0}";
        ToolTipText = $"音量 {VolumeText}%";
        MuteToolTip = volume <= 0 ? "恢复音量" : "静音";
        VolumeIcon = ResolveVolumeIcon(volume);
        UpdateSliderVisuals(volume);
    }

    private void VolumeSlider_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        _isDraggingVolume = true;
        e.Pointer.Capture(control);
        SetVolumeFromPoint(e.GetPosition(control));
        e.Handled = true;
    }

    private void VolumeSlider_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDraggingVolume || sender is not Control control)
        {
            return;
        }

        SetVolumeFromPoint(e.GetPosition(control));
        e.Handled = true;
    }

    private void VolumeSlider_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDraggingVolume = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void VolumeSlider_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _isDraggingVolume = false;
    }

    private void SetVolumeFromPoint(Point point)
    {
        var centerY = Math.Clamp(point.Y, SliderThumbSize / 2, SliderHeight - SliderThumbSize / 2);
        var ratio = (SliderHeight - SliderThumbSize / 2 - centerY) / SliderTrackRange;
        SetCurrentValue(VolumeProperty, Math.Clamp(ratio * 100, 0, 100));
    }

    private void UpdateSliderVisuals(double volume)
    {
        var offset = SliderTrackRange * Math.Clamp(volume, 0, 100) / 100;
        SelectionHeight = offset;
        ThumbMargin = new Thickness(0, 0, 0, offset);
    }

    private static MaterialIconKind ResolveVolumeIcon(double volume)
    {
        return volume switch
        {
            <= 0 => MaterialIconKind.VolumeOff,
            < 34 => MaterialIconKind.VolumeLow,
            < 67 => MaterialIconKind.VolumeMedium,
            _ => MaterialIconKind.VolumeHigh
        };
    }
}
