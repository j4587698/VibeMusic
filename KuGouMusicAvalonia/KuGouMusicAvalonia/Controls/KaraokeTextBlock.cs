using System.Collections.Generic;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using KuGouMusicAvalonia.Services;

namespace KuGouMusicAvalonia.Controls;

/// <summary>
/// 逐字（卡拉OK）歌词控件。使用"两层文字裁剪"渲染：
/// 先用未唱颜色绘制整行，再用已唱颜色按当前播放进度裁剪覆盖绘制。
/// 相比 <see cref="LinearGradientBrush"/> 逐字方案，单行只需一次文本布局，
/// 每帧仅做少量裁剪绘制，避免渐变着色器在大号粗体字上的高额开销。
/// </summary>
public sealed class KaraokeTextBlock : Control
{
    public static readonly StyledProperty<IReadOnlyList<LyricWord>?> WordsProperty =
        AvaloniaProperty.Register<KaraokeTextBlock, IReadOnlyList<LyricWord>?>(nameof(Words));

    public static readonly StyledProperty<double> PlaybackPositionProperty =
        AvaloniaProperty.Register<KaraokeTextBlock, double>(nameof(PlaybackPosition));

    public static readonly StyledProperty<IBrush?> SungBrushProperty =
        AvaloniaProperty.Register<KaraokeTextBlock, IBrush?>(nameof(SungBrush));

    public static readonly StyledProperty<IBrush?> UnsungBrushProperty =
        AvaloniaProperty.Register<KaraokeTextBlock, IBrush?>(nameof(UnsungBrush));

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<KaraokeTextBlock, double>(nameof(FontSize), 36);

    public static readonly StyledProperty<FontWeight> FontWeightProperty =
        AvaloniaProperty.Register<KaraokeTextBlock, FontWeight>(nameof(FontWeight), FontWeight.Black);

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        AvaloniaProperty.Register<KaraokeTextBlock, FontFamily>(nameof(FontFamily), FontFamily.Default);

    public static readonly StyledProperty<TextAlignment> TextAlignmentProperty =
        AvaloniaProperty.Register<KaraokeTextBlock, TextAlignment>(nameof(TextAlignment), TextAlignment.Center);

    private TextLayout? _unsung;
    private TextLayout? _sung;
    private double _builtWidth = double.NaN;
    private bool _usesLayoutAlignment;
    private string _text = string.Empty;

    public IReadOnlyList<LyricWord>? Words
    {
        get => GetValue(WordsProperty);
        set => SetValue(WordsProperty, value);
    }

    public double PlaybackPosition
    {
        get => GetValue(PlaybackPositionProperty);
        set => SetValue(PlaybackPositionProperty, value);
    }

    public IBrush? SungBrush
    {
        get => GetValue(SungBrushProperty);
        set => SetValue(SungBrushProperty, value);
    }

    public IBrush? UnsungBrush
    {
        get => GetValue(UnsungBrushProperty);
        set => SetValue(UnsungBrushProperty, value);
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

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PlaybackPositionProperty)
        {
            // 只有当前可见（活动行）的控件才重绘；隐藏的非活动行不该因
            // 播放位置变化而反复失效，避免大量无用的 30fps 重绘。
            if (IsEffectivelyVisible)
            {
                InvalidateVisual();
            }

            return;
        }

        if (change.Property == WordsProperty ||
            change.Property == FontSizeProperty ||
            change.Property == FontWeightProperty ||
            change.Property == FontFamilyProperty ||
            change.Property == TextAlignmentProperty ||
            change.Property == SungBrushProperty ||
            change.Property == UnsungBrushProperty)
        {
            _unsung = null;
            _sung = null;
            _builtWidth = double.NaN;
            InvalidateMeasure();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var wrapWidth = double.IsFinite(availableSize.Width) && availableSize.Width > 0
            ? availableSize.Width
            : double.PositiveInfinity;

        if (_unsung is null || _builtWidth != wrapWidth)
        {
            BuildText(wrapWidth);
            _builtWidth = wrapWidth;
        }

        return _unsung is null ? default : new Size(_unsung.Width, _unsung.Height);
    }

    public override void Render(DrawingContext context)
    {
        if (_unsung is null)
        {
            return;
        }

        // 有明确可用宽度时交给 TextLayout 做逐行对齐，保证换行后的每一行都能居中；
        // 宽度无穷时继续用整体平移，避免无约束测量下布局抖动。
        var offsetX = 0d;
        if (!_usesLayoutAlignment)
        {
            var extra = Bounds.Width - _unsung.Width;
            if (extra > 0)
            {
                offsetX = TextAlignment switch
                {
                    TextAlignment.Center => extra / 2,
                    TextAlignment.Right => extra,
                    _ => 0,
                };
            }
        }

        using (context.PushTransform(Matrix.CreateTranslation(offsetX, 0)))
        {
            _unsung.Draw(context, default);
            DrawSung(context);
        }
    }

    private void BuildText(double maxWidth)
    {
        var words = Words;
        if (words is null || words.Count == 0)
        {
            _text = string.Empty;
            _unsung = null;
            _sung = null;
            return;
        }

        var sb = new StringBuilder();
        foreach (var word in words)
        {
            sb.Append(word.Text);
        }

        _text = sb.ToString();

        var typeface = new Typeface(FontFamily, FontStyle.Normal, FontWeight);
        _usesLayoutAlignment = double.IsFinite(maxWidth) && maxWidth > 0;
        var constraint = _usesLayoutAlignment ? maxWidth : double.PositiveInfinity;
        var alignment = _usesLayoutAlignment ? TextAlignment : TextAlignment.Left;

        _unsung = new TextLayout(_text, typeface, FontSize, UnsungBrush, alignment, TextWrapping.Wrap, maxWidth: constraint);
        _sung = new TextLayout(_text, typeface, FontSize, SungBrush, alignment, TextWrapping.Wrap, maxWidth: constraint);
    }

    private void DrawSung(DrawingContext context)
    {
        if (_sung is null || _unsung is null)
        {
            return;
        }

        var words = Words;
        if (words is null || words.Count == 0)
        {
            return;
        }

        var position = PlaybackPosition;
        var fullChars = 0;
        var activeStart = -1;
        var activeLength = 0;
        var fraction = 0d;

        foreach (var word in words)
        {
            var end = word.StartTime + word.Duration;
            if (position >= end)
            {
                fullChars += word.Text.Length;
                continue;
            }

            if (position < word.StartTime)
            {
                break;
            }

            activeStart = fullChars;
            activeLength = word.Text.Length;
            fraction = word.Duration > 0 ? (position - word.StartTime) / word.Duration : 1;
            break;
        }

        if (fullChars == 0 && activeStart < 0)
        {
            return;
        }

        if (fullChars > 0)
        {
            foreach (var rect in _unsung.HitTestTextRange(0, fullChars))
            {
                using (context.PushClip(rect))
                {
                    _sung.Draw(context, default);
                }
            }
        }

        if (activeStart >= 0 && activeLength > 0 && fraction > 0)
        {
            foreach (var rect in _unsung.HitTestTextRange(activeStart, activeLength))
            {
                var partial = new Rect(rect.X, rect.Y, rect.Width * fraction, rect.Height);
                using (context.PushClip(partial))
                {
                    _sung.Draw(context, default);
                }

                break;
            }
        }
    }
}
