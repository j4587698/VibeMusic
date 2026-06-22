using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace KuGouMusicAvalonia.Controls;

public sealed class OutlinedTextBlock : Control
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<OutlinedTextBlock, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<OutlinedTextBlock, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<OutlinedTextBlock, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<OutlinedTextBlock, double>(nameof(StrokeThickness), 0);

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<OutlinedTextBlock, double>(nameof(FontSize), 14);

    public static readonly StyledProperty<FontWeight> FontWeightProperty =
        AvaloniaProperty.Register<OutlinedTextBlock, FontWeight>(nameof(FontWeight), FontWeight.Normal);

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        AvaloniaProperty.Register<OutlinedTextBlock, FontFamily>(nameof(FontFamily), FontFamily.Default);

    public static readonly StyledProperty<TextAlignment> TextAlignmentProperty =
        AvaloniaProperty.Register<OutlinedTextBlock, TextAlignment>(nameof(TextAlignment), TextAlignment.Left);

    public static readonly StyledProperty<TextWrapping> TextWrappingProperty =
        AvaloniaProperty.Register<OutlinedTextBlock, TextWrapping>(nameof(TextWrapping), TextWrapping.NoWrap);

    public static readonly StyledProperty<int> MaxLinesProperty =
        AvaloniaProperty.Register<OutlinedTextBlock, int>(nameof(MaxLines), 0);

    private TextLayout? _textLayout;
    private Geometry? _textGeometry;
    private double _builtWidth = double.NaN;

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public FontWeight FontWeight
    {
        get => GetValue(FontWeightProperty);
        set => SetValue(FontWeightProperty, value);
    }

    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public TextAlignment TextAlignment
    {
        get => GetValue(TextAlignmentProperty);
        set => SetValue(TextAlignmentProperty, value);
    }

    public TextWrapping TextWrapping
    {
        get => GetValue(TextWrappingProperty);
        set => SetValue(TextWrappingProperty, value);
    }

    public int MaxLines
    {
        get => GetValue(MaxLinesProperty);
        set => SetValue(MaxLinesProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty ||
            change.Property == FontSizeProperty ||
            change.Property == FontWeightProperty ||
            change.Property == FontFamilyProperty ||
            change.Property == TextAlignmentProperty ||
            change.Property == TextWrappingProperty ||
            change.Property == MaxLinesProperty ||
            change.Property == ForegroundProperty ||
            change.Property == StrokeProperty ||
            change.Property == StrokeThicknessProperty)
        {
            _textLayout = null;
            _textGeometry = null;
            _builtWidth = double.NaN;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var wrapWidth = double.IsFinite(availableSize.Width) && availableSize.Width > 0
            ? availableSize.Width
            : double.PositiveInfinity;

        if (_textLayout is null || _builtWidth != wrapWidth)
        {
            BuildText(wrapWidth);
            _builtWidth = wrapWidth;
        }

        return _textLayout is null ? default : new Size(_textLayout.Width, _textLayout.Height);
    }

    public override void Render(DrawingContext context)
    {
        if (_textLayout is null) return;

        var offsetX = 0d;
        var extra = Bounds.Width - _textLayout.Width;
        if (extra > 0 && TextWrapping != TextWrapping.Wrap)
        {
            offsetX = TextAlignment switch
            {
                TextAlignment.Center => extra / 2,
                TextAlignment.Right => extra,
                _ => 0,
            };
        }

        using (context.PushTransform(Matrix.CreateTranslation(offsetX, 0)))
        {
            if (_textGeometry != null && StrokeThickness > 0 && Stroke != null)
            {
                var pen = new Pen(Stroke, StrokeThickness) { LineJoin = PenLineJoin.Round };
                context.DrawGeometry(null, pen, _textGeometry);
            }

            _textLayout.Draw(context, default);
        }
    }

    private void BuildText(double maxWidth)
    {
        var text = Text;
        if (string.IsNullOrEmpty(text))
        {
            _textLayout = null;
            _textGeometry = null;
            return;
        }

        var typeface = new Typeface(FontFamily, FontStyle.Normal, FontWeight);
        var constraint = maxWidth;

        _textLayout = new TextLayout(text, typeface, FontSize, Foreground, TextAlignment, TextWrapping, maxWidth: constraint, maxLines: MaxLines);

        if (StrokeThickness > 0 && Stroke != null)
        {
            var ft = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                FontSize,
                Foreground)
            {
                TextAlignment = TextAlignment
            };
            if (double.IsFinite(constraint) && constraint > 0 && TextWrapping == TextWrapping.Wrap)
            {
                ft.MaxTextWidth = constraint;
            }

            _textGeometry = ft.BuildGeometry(default);
        }
        else
        {
            _textGeometry = null;
        }
    }
}
