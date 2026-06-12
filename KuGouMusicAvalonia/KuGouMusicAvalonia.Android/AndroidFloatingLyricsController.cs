using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Provider;
using Android.Text;
using Android.Views;
using Android.Widget;
using Android.Runtime;
using KuGouMusicAvalonia.Services;
using System;
using System.ComponentModel;
using AndroidUri = global::Android.Net.Uri;

namespace KuGouMusicAvalonia.Android;

internal sealed class AndroidFloatingLyricsController : IFloatingLyricsController, IDisposable
{
    public static AndroidFloatingLyricsController Instance { get; } = new();

    private readonly Handler _mainHandler = new(Looper.MainLooper!);
    private Activity? _activity;
    private Context? _context;
    private IWindowManager? _windowManager;
    private WindowManagerLayoutParams? _layoutParams;
    private LinearLayout? _rootView;
    private TextView? _titleText;
    private TextView? _currentLineText;
    private TextView? _nextLineText;
    private bool _isOpen;
    private bool _isLocked;
    private bool _isCompactMode;
    private bool _pendingOpenAfterPermission;

    private AndroidFloatingLyricsController()
    {
    }

    public bool IsSupported => _context is not null;

    public bool IsOpen => _isOpen;

    public bool SupportsCompactMode => true;

    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            if (_isLocked == value)
            {
                return;
            }

            _isLocked = value;
            UpdateTouchFlags();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsCompactMode
    {
        get => _isCompactMode;
        set
        {
            if (_isCompactMode == value)
            {
                return;
            }

            _isCompactMode = value;
            _mainHandler.Post(() => ApplyDisplayMode(updateLayout: true));
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? StateChanged;

    public void Initialize(Activity activity)
    {
        _activity = activity;
        _context = activity.ApplicationContext;
        _windowManager = activity.GetSystemService(Context.WindowService)?.JavaCast<IWindowManager>();
        FloatingLyricsService.Instance.RegisterController(this);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RefreshOverlayPermission()
    {
        if (_pendingOpenAfterPermission && HasOverlayPermission())
        {
            _pendingOpenAfterPermission = false;
            ShowOrActivate();
            return;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Toggle()
    {
        if (IsOpen)
        {
            Close();
        }
        else
        {
            ShowOrActivate();
        }
    }

    public void ShowOrActivate()
    {
        if (_context is null || _windowManager is null)
        {
            return;
        }

        if (!HasOverlayPermission())
        {
            _pendingOpenAfterPermission = true;
            RequestOverlayPermission();
            return;
        }

        if (_isOpen)
        {
            return;
        }

        _mainHandler.Post(OpenOnMainThread);
    }

    public void Dispose()
    {
        CloseOnMainThread();
        _activity = null;
        _context = null;
        _windowManager = null;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OpenOnMainThread()
    {
        if (_context is null || _windowManager is null || _isOpen)
        {
            return;
        }

        _rootView = CreateOverlayView(_context);
        _layoutParams = CreateLayoutParams();
        ApplyDisplayMode(updateLayout: false);
        UpdateTexts();

        if (!TryAddOverlayView())
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        Subscribe();
        _isOpen = true;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool TryAddOverlayView()
    {
        if (_rootView is null || _layoutParams is null || _windowManager is null)
        {
            return false;
        }

        try
        {
            _windowManager.AddView(_rootView, _layoutParams);
            return true;
        }
        catch (Java.Lang.RuntimeException)
        {
            ClearOverlayReferences();
            _pendingOpenAfterPermission = false;
            return false;
        }
    }

    private void Close()
    {
        _mainHandler.Post(CloseOnMainThread);
    }

    private void CloseOnMainThread()
    {
        Unsubscribe();

        if (_rootView is not null && _windowManager is not null)
        {
            try
            {
                _windowManager.RemoveView(_rootView);
            }
            catch (Java.Lang.IllegalArgumentException)
            {
            }
        }

        ClearOverlayReferences();
        _isOpen = false;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ClearOverlayReferences()
    {
        _rootView?.Dispose();
        _rootView = null;
        _layoutParams = null;
        _titleText = null;
        _currentLineText = null;
        _nextLineText = null;
    }

    private LinearLayout CreateOverlayView(Context context)
    {
        var root = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical
        };
        root.SetGravity(GravityFlags.Center);
        root.SetPadding(Dp(18), Dp(10), Dp(18), Dp(10));
        root.SetMinimumWidth(Dp(280));
        root.Background = CreateBackground(isCompact: false);
        root.SetOnTouchListener(new DragTouchListener(this));

        _titleText = CreateTextView(context, 12, Color.Argb(190, 255, 255, 255), maxLines: 1);
        _currentLineText = CreateTextView(context, 22, Color.Rgb(255, 179, 173), maxLines: 2);
        _currentLineText.SetTypeface(Typeface.DefaultBold, TypefaceStyle.Bold);
        _nextLineText = CreateTextView(context, 14, Color.Argb(205, 255, 255, 255), maxLines: 1);

        root.AddView(_titleText, CreateChildLayoutParams());
        root.AddView(_currentLineText, CreateChildLayoutParams());
        root.AddView(_nextLineText, CreateChildLayoutParams());
        return root;
    }

    private void ApplyDisplayMode(bool updateLayout)
    {
        if (_rootView is null || _titleText is null || _currentLineText is null || _nextLineText is null)
        {
            return;
        }

        if (_isCompactMode)
        {
            _rootView.SetPadding(Dp(14), Dp(5), Dp(14), Dp(5));
            _rootView.SetMinimumWidth(Dp(180));
            _rootView.Background = CreateBackground(isCompact: true);

            _titleText.Visibility = ViewStates.Gone;
            _currentLineText.TextSize = 15;
            _currentLineText.SetMaxLines(1);
            _currentLineText.SetMaxWidth(GetOverlayMaxWidth(maxWidthDp: 420));
            _nextLineText.Visibility = ViewStates.Gone;
        }
        else
        {
            _rootView.SetPadding(Dp(18), Dp(10), Dp(18), Dp(10));
            _rootView.SetMinimumWidth(Dp(280));
            _rootView.Background = CreateBackground(isCompact: false);

            _titleText.Visibility = ViewStates.Visible;
            _currentLineText.TextSize = 22;
            _currentLineText.SetMaxLines(2);
            _currentLineText.SetMaxWidth(GetOverlayMaxWidth(maxWidthDp: 520));
            _nextLineText.SetMaxWidth(GetOverlayMaxWidth(maxWidthDp: 520));
        }

        UpdateTexts();

        if (updateLayout && _windowManager is not null && _rootView is not null && _layoutParams is not null)
        {
            ClampCurrentPosition();
            UpdateOverlayLayout();
        }
    }

    private WindowManagerLayoutParams CreateLayoutParams()
    {
        var flags = GetBaseWindowFlags();
        var layoutParams = new WindowManagerLayoutParams(
            ViewGroup.LayoutParams.WrapContent,
            ViewGroup.LayoutParams.WrapContent,
            GetWindowType(),
            flags,
            Format.Translucent)
        {
            Gravity = GravityFlags.Top | GravityFlags.CenterHorizontal,
            Y = Dp(92)
        };
        return layoutParams;
    }

    private WindowManagerTypes GetWindowType()
    {
        return Build.VERSION.SdkInt >= BuildVersionCodes.O
            ? WindowManagerTypes.ApplicationOverlay
            : WindowManagerTypes.Phone;
    }

    private WindowManagerFlags GetBaseWindowFlags()
    {
        var flags = WindowManagerFlags.NotFocusable | WindowManagerFlags.NotTouchModal | WindowManagerFlags.LayoutNoLimits;
        if (_isLocked)
        {
            flags |= WindowManagerFlags.NotTouchable;
        }

        return flags;
    }

    private void UpdateTouchFlags()
    {
        if (_layoutParams is null || _rootView is null || _windowManager is null)
        {
            return;
        }

        _layoutParams.Flags = GetBaseWindowFlags();
        UpdateOverlayLayout();
    }

    private void Subscribe()
    {
        LyricsService.Instance.PropertyChanged += OnLyricsPropertyChanged;
        PlayerService.Instance.PropertyChanged += OnPlayerPropertyChanged;
    }

    private void Unsubscribe()
    {
        LyricsService.Instance.PropertyChanged -= OnLyricsPropertyChanged;
        PlayerService.Instance.PropertyChanged -= OnPlayerPropertyChanged;
    }

    private void OnLyricsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LyricsService.CurrentLineText)
            or nameof(LyricsService.NextLineText)
            or nameof(LyricsService.IsLoading))
        {
            PostUpdateTexts();
        }
    }

    private void OnPlayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PlayerService.CurrentSong)
            or nameof(PlayerService.CurrentTitle)
            or nameof(PlayerService.CurrentArtist)
            or nameof(PlayerService.HasSong))
        {
            PostUpdateTexts();
        }
    }

    private void PostUpdateTexts()
    {
        _mainHandler.Post(UpdateTexts);
    }

    private void UpdateTexts()
    {
        if (_titleText is null || _currentLineText is null || _nextLineText is null)
        {
            return;
        }

        var player = PlayerService.Instance;
        var lyrics = LyricsService.Instance;
        var title = player.HasSong
            ? string.IsNullOrWhiteSpace(player.CurrentArtist)
                ? player.CurrentTitle
                : $"{player.CurrentTitle} - {player.CurrentArtist}"
            : "暂无播放";

        _titleText.Text = title;
        _titleText.Visibility = _isCompactMode ? ViewStates.Gone : ViewStates.Visible;
        _currentLineText.Text = string.IsNullOrWhiteSpace(lyrics.CurrentLineText) ? "暂无歌词" : lyrics.CurrentLineText;
        _nextLineText.Text = lyrics.NextLineText;
        _nextLineText.Visibility = _isCompactMode || string.IsNullOrWhiteSpace(lyrics.NextLineText)
            ? ViewStates.Gone
            : ViewStates.Visible;
    }

    private bool HasOverlayPermission()
    {
        if (_context is null)
        {
            return false;
        }

        return Build.VERSION.SdkInt < BuildVersionCodes.M || Settings.CanDrawOverlays(_context);
    }

    private void RequestOverlayPermission()
    {
        if (_context is null)
        {
            return;
        }

        var packageUri = AndroidUri.Parse($"package:{_context.PackageName}");
        var intent = new Intent(Settings.ActionManageOverlayPermission, packageUri);
        intent.AddFlags(ActivityFlags.NewTask);
        (_activity ?? _context).StartActivity(intent);
    }

    private static GradientDrawable CreateBackground(bool isCompact)
    {
        var drawable = new GradientDrawable();
        drawable.SetColor(Color.Argb(isCompact ? 178 : 204, 18, 18, 22));
        drawable.SetStroke(1, Color.Argb(55, 255, 255, 255));
        drawable.SetCornerRadius(Dp(isCompact ? 14 : 18));
        return drawable;
    }

    private static TextView CreateTextView(Context context, float textSize, Color color, int maxLines)
    {
        var textView = new TextView(context)
        {
            Gravity = GravityFlags.Center,
            TextSize = textSize,
            Ellipsize = TextUtils.TruncateAt.End
        };
        textView.SetTextColor(color);
        textView.SetMaxLines(maxLines);
        textView.SetIncludeFontPadding(false);
        return textView;
    }

    private static LinearLayout.LayoutParams CreateChildLayoutParams()
    {
        var layoutParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
        layoutParams.SetMargins(0, 2, 0, 2);
        return layoutParams;
    }

    private static int Dp(int value)
    {
        var density = global::Android.App.Application.Context.Resources?.DisplayMetrics?.Density ?? 1f;
        return (int)Math.Round(value * density);
    }

    private static int GetOverlayMaxWidth(int maxWidthDp)
    {
        var displayMetrics = global::Android.App.Application.Context.Resources?.DisplayMetrics;
        if (displayMetrics is null)
        {
            return Dp(maxWidthDp);
        }

        var availableWidth = displayMetrics.WidthPixels - Dp(32);
        return Math.Max(Dp(180), Math.Min(Dp(maxWidthDp), availableWidth));
    }

    private void CaptureCurrentPositionForDrag()
    {
        if (_rootView is null || _layoutParams is null || _windowManager is null)
        {
            return;
        }

        var location = new int[2];
        _rootView.GetLocationOnScreen(location);
        _layoutParams.Gravity = GravityFlags.Top | GravityFlags.Left;
        var position = ClampOverlayPosition(location[0], location[1]);
        _layoutParams.X = position.X;
        _layoutParams.Y = position.Y;
        UpdateOverlayLayout();
    }

    private void MoveOverlay(int x, int y)
    {
        if (_rootView is null || _layoutParams is null || _windowManager is null)
        {
            return;
        }

        var position = ClampOverlayPosition(x, y);
        _layoutParams.X = position.X;
        _layoutParams.Y = position.Y;
        UpdateOverlayLayout();
    }

    private void ClampCurrentPosition()
    {
        if (_layoutParams is null || (_layoutParams.Gravity & GravityFlags.Left) != GravityFlags.Left)
        {
            return;
        }

        var position = ClampOverlayPosition(_layoutParams.X, _layoutParams.Y);
        _layoutParams.X = position.X;
        _layoutParams.Y = position.Y;
    }

    private (int X, int Y) ClampOverlayPosition(int x, int y)
    {
        var displayMetrics = _context?.Resources?.DisplayMetrics ?? Android.App.Application.Context.Resources?.DisplayMetrics;
        if (displayMetrics is null || _rootView is null)
        {
            return (x, y);
        }

        var viewWidth = Math.Max(_rootView.Width, _rootView.MeasuredWidth);
        var viewHeight = Math.Max(_rootView.Height, _rootView.MeasuredHeight);
        var maxX = viewWidth > 0 ? Math.Max(0, displayMetrics.WidthPixels - viewWidth) : displayMetrics.WidthPixels;
        var maxY = viewHeight > 0 ? Math.Max(0, displayMetrics.HeightPixels - viewHeight) : displayMetrics.HeightPixels;

        return (Math.Clamp(x, 0, maxX), Math.Clamp(y, 0, maxY));
    }

    private void UpdateOverlayLayout()
    {
        if (_rootView is null || _layoutParams is null || _windowManager is null)
        {
            return;
        }

        try
        {
            _windowManager.UpdateViewLayout(_rootView, _layoutParams);
        }
        catch (Java.Lang.IllegalArgumentException)
        {
        }
        catch (Java.Lang.RuntimeException)
        {
        }
    }

    private sealed class DragTouchListener(AndroidFloatingLyricsController owner) : Java.Lang.Object, View.IOnTouchListener
    {
        private float _downRawX;
        private float _downRawY;
        private int _startX;
        private int _startY;

        public bool OnTouch(View? v, MotionEvent? e)
        {
            if (e is null || owner._layoutParams is null || owner._isLocked)
            {
                return false;
            }

            switch (e.ActionMasked)
            {
                case MotionEventActions.Down:
                    owner.CaptureCurrentPositionForDrag();
                    _downRawX = e.RawX;
                    _downRawY = e.RawY;
                    _startX = owner._layoutParams.X;
                    _startY = owner._layoutParams.Y;
                    return true;
                case MotionEventActions.Move:
                    var x = _startX + (int)Math.Round(e.RawX - _downRawX);
                    var y = _startY + (int)Math.Round(e.RawY - _downRawY);
                    owner.MoveOverlay(x, y);
                    return true;
                default:
                    return true;
            }
        }
    }
}
