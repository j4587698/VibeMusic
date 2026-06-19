using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KuGou.Lite;
using KuGouMusicAvalonia.Services;
using QRCoder;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KuGouMusicAvalonia.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private CancellationTokenSource? _qrPollingCts;
    private decimal _lastValidFloatingLyricsFontSize = (decimal)FloatingLyricsService.DefaultFontSize;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isQrPolling;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoginPromptVisible))]
    private bool _isLoginDialogOpen;

    [ObservableProperty]
    private int _loginTabIndex;

    [ObservableProperty]
    private double _loginDialogWidth = 420;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasQrCode))]
    private Bitmap? _qrCodeImage;
    [ObservableProperty]
    private string _qrUrl = string.Empty;
    [ObservableProperty]
    private string _qrStatus = "未生成二维码";
    [ObservableProperty]
    private string _phoneNumber = string.Empty;
    [ObservableProperty]
    private string _verifyCode = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoggedIn))]
    [NotifyPropertyChangedFor(nameof(IsLoggedInPanelVisible))]
    [NotifyPropertyChangedFor(nameof(IsLoginPromptVisible))]
    private string _loginStatus = "正在检查登录态";

    [ObservableProperty]
    private bool _isProfileBusy;

    [ObservableProperty]
    private string _userDisplayName = "未登录";

    [ObservableProperty]
    private string _userAvatarUrl = string.Empty;

    [ObservableProperty]
    private string _userIdText = "userid -";

    [ObservableProperty]
    private string _userExpireText = "登录到期：未同步";

    [ObservableProperty]
    private string _userProfileStatus = "登录后同步账号资料";

    [ObservableProperty]
    private string _userPlaylistCountText = "0";

    [ObservableProperty]
    private string _userCollectionCountText = "0";

    [ObservableProperty]
    private string _userHistoryCountText = "0";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWideLayout))]
    private bool _isCompactLayout;

    [ObservableProperty]
    private bool _autoReceiveVipBeforePlayback;

    [ObservableProperty]
    private bool _isVipBusy;

    [ObservableProperty]
    private string _vipStatus = "VIP状态：未刷新";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasVipDetail))]
    private string _vipDetail = string.Empty;

    [ObservableProperty]
    private string _themeMode = "深色";

    [ObservableProperty]
    private bool _streamWhileDownloading;

    [ObservableProperty]
    private string _downloadDirectory = string.Empty;

    [ObservableProperty]
    private string _defaultPlaybackQuality = "无损 FLAC";

    [ObservableProperty]
    private bool _minimizeToTrayOnClose;

    [ObservableProperty]
    private bool _preferKrc;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FloatingLyricsFontSizeText))]
    private decimal? _floatingLyricsFontSize = (decimal)FloatingLyricsService.DefaultFontSize;

    public IReadOnlyList<string> ThemeModeOptions { get; } = new[] { "跟随系统", "浅色", "深色" };

    public IReadOnlyList<string> PlaybackQualityOptions { get; } = new[] { "标准 128k", "高品 320k", "无损 FLAC", "高解析 High" };

    public ObservableCollection<UserLibraryItem> UserPlaylists { get; } = new();

    public ObservableCollection<UserLibraryItem> UserCollections { get; } = new();

    public ObservableCollection<KugouSong> UserHistory { get; } = new();

    public ObservableCollection<UserLibraryItem> UserLibraryPreview { get; } = new();

    public string SdkStatus => "KuGouLiteSdk typed endpoints";
    public string AotStatus => "Compiled bindings + source-generated SDK DTO";
    public bool HasQrCode => QrCodeImage is not null;
    public bool IsLoggedIn => MusicService.Client.GetLoginState().IsLoggedIn;
    public bool IsLoggedInPanelVisible => IsLoggedIn;
    public bool IsLoginPromptVisible => !IsLoggedIn;
    public bool HasVipDetail => !string.IsNullOrWhiteSpace(VipDetail);
    public bool IsWideLayout => !IsCompactLayout;
    public bool IsDesktopSettingVisible => Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime;
    public bool IsFloatingLyricsSupported => FloatingLyricsService.Instance.IsSupported;
    public bool IsFloatingLyricsCompactModeSupported => FloatingLyricsService.Instance.SupportsCompactMode;
    public string FloatingLyricsStatusText => IsFloatingLyricsSupported
        ? (IsFloatingLyricsOpen ? "已开启" : "已关闭")
        : "当前平台不支持";
    public decimal FloatingLyricsMinFontSize => (decimal)FloatingLyricsService.MinFontSize;
    public decimal FloatingLyricsMaxFontSize => (decimal)FloatingLyricsService.MaxFontSize;
    public string FloatingLyricsFontSizeText => $"{FloatingLyricsFontSize ?? _lastValidFloatingLyricsFontSize:0}";

    public bool IsFloatingLyricsOpen
    {
        get => MusicService.FloatingLyricsOpen;
        set
        {
            if (MusicService.FloatingLyricsOpen == value &&
                (!value || FloatingLyricsService.Instance.IsOpen))
            {
                return;
            }

            MusicService.FloatingLyricsOpen = value;
            if (value)
            {
                FloatingLyricsService.Instance.ShowOrActivate();
            }
            else if (FloatingLyricsService.Instance.IsOpen)
            {
                FloatingLyricsService.Instance.Toggle();
            }

            NotifyFloatingLyricsStateChanged();
        }
    }

    public bool IsFloatingLyricsLocked
    {
        get => FloatingLyricsService.Instance.IsLocked;
        set
        {
            if (FloatingLyricsService.Instance.IsLocked == value)
            {
                return;
            }

            FloatingLyricsService.Instance.IsLocked = value;
            NotifyFloatingLyricsStateChanged();
        }
    }

    public bool IsFloatingLyricsCompactMode
    {
        get => FloatingLyricsService.Instance.IsCompactMode;
        set
        {
            if (FloatingLyricsService.Instance.IsCompactMode == value)
            {
                return;
            }

            FloatingLyricsService.Instance.IsCompactMode = value;
            NotifyFloatingLyricsStateChanged();
        }
    }

    public SettingsViewModel()
    {
        AutoReceiveVipBeforePlayback = VipPrivilegeService.Instance.AutoReceiveBeforePlayback;
        ThemeMode = MusicService.ThemeMode;
        StreamWhileDownloading = MusicService.StreamWhileDownloading;
        MinimizeToTrayOnClose = MusicService.MinimizeToTrayOnClose;
        DownloadDirectory = MusicService.DownloadDirectory;
        DefaultPlaybackQuality = MusicService.DefaultPlaybackQuality;
        PreferKrc = MusicService.PreferKrc;
        _lastValidFloatingLyricsFontSize = (decimal)FloatingLyricsService.Instance.FontSize;
        FloatingLyricsFontSize = _lastValidFloatingLyricsFontSize;
        FloatingLyricsService.Instance.StateChanged += OnFloatingLyricsStateChanged;
        ApplyThemeMode(ThemeMode);
        RefreshLoginState();
        if (IsLoggedIn)
        {
            _ = RefreshUserDataAsync();
            _ = RefreshVipStateAsync();
        }
    }

    partial void OnAutoReceiveVipBeforePlaybackChanged(bool value)
    {
        VipPrivilegeService.Instance.AutoReceiveBeforePlayback = value;
    }

    partial void OnThemeModeChanged(string value)
    {
        MusicService.ThemeMode = value;
        ApplyThemeMode(value);
    }

    partial void OnStreamWhileDownloadingChanged(bool value)
    {
        MusicService.StreamWhileDownloading = value;
    }

    partial void OnMinimizeToTrayOnCloseChanged(bool value)
    {
        MusicService.MinimizeToTrayOnClose = value;
    }

    partial void OnPreferKrcChanged(bool value)
    {
        MusicService.PreferKrc = value;
    }

    partial void OnFloatingLyricsFontSizeChanged(decimal? value)
    {
        if (value is null || decimal.Truncate(value.Value) != value.Value)
        {
            RestoreFloatingLyricsFontSize();
            return;
        }

        var normalized = Math.Clamp(value.Value, FloatingLyricsMinFontSize, FloatingLyricsMaxFontSize);
        if (normalized != value.Value)
        {
            FloatingLyricsFontSize = normalized;
            return;
        }

        _lastValidFloatingLyricsFontSize = normalized;
        FloatingLyricsService.Instance.FontSize = (double)normalized;
    }

    partial void OnDownloadDirectoryChanged(string value)
    {
        MusicService.DownloadDirectory = value;
    }

    [RelayCommand]
    private async Task PickDownloadDirectoryAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            LoginStatus = "当前平台不支持目录选择器，请手动输入下载目录";
            return;
        }

        try
        {
            var picked = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "选择下载目录",
                AllowMultiple = false
            });

            var folder = picked.FirstOrDefault();
            if (folder is null)
            {
                return;
            }

            var path = folder.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                LoginStatus = "选择的目录不可映射到本地路径，请手动输入";
                return;
            }

            DownloadDirectory = path;
            LoginStatus = $"下载目录已更新：{path}";
        }
        catch (Exception ex)
        {
            LoginStatus = $"选择下载目录失败：{ex.Message}";
        }
    }

    partial void OnDefaultPlaybackQualityChanged(string value)
    {
        MusicService.DefaultPlaybackQuality = value;
    }

    [RelayCommand]
    private void RefreshLoginState()
    {
        var state = MusicService.Client.GetLoginState();
        LoginStatus = state.Message;
        NotifyLoginStateChanged();
        if (IsLoggedIn)
        {
            IsLoginDialogOpen = false;
        }
    }

    [RelayCommand]
    private void StartLogin()
    {
        LoginTabIndex = 0;
        IsLoginDialogOpen = true;
        if (!HasQrCode && !IsQrPolling)
        {
            _ = StartQrLoginAsync();
        }
    }

    [RelayCommand]
    private void CloseLoginDialog()
    {
        IsLoginDialogOpen = false;
        CancelQrPolling("已取消扫码");
    }

    [RelayCommand]
    private async Task StartQrLoginAsync()
    {
        if (IsBusy) return;
        CancelQrPolling("正在重新生成二维码");
        IsBusy = true;

        try
        {
            var session = await MusicService.Client.CreateLoginQrSessionAsync();
            QrUrl = session.Url;
            QrCodeImage = CreateQrBitmap(session.Url);
            QrStatus = "等待扫码确认";
            _qrPollingCts = new CancellationTokenSource();
            await PollQrLoginAsync(session.Key, _qrPollingCts.Token);
        }
        catch (OperationCanceledException)
        {
            QrStatus = "已取消扫码";
        }
        catch (Exception ex)
        {
            QrStatus = $"扫码登录失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
            IsQrPolling = false;
        }
    }

    [RelayCommand]
    private void CancelQrLogin()
    {
        CancelQrPolling("已取消扫码");
    }

    [RelayCommand]
    private async Task SendCodeAsync()
    {
        if (string.IsNullOrWhiteSpace(PhoneNumber))
        {
            LoginStatus = "请输入手机号";
            return;
        }

        IsBusy = true;
        try
        {
            var response = await MusicService.Client.SendCaptchaAsync(PhoneNumber.Trim());
            ExtractKugouResponse(response, out var isSuccess, out var errorMsg);
            LoginStatus = isSuccess ? "验证码已发送" : $"验证码发送失败：{errorMsg}";
        }
        catch (Exception ex)
        {
            LoginStatus = $"验证码发送失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoginByPhoneAsync()
    {
        if (string.IsNullOrWhiteSpace(PhoneNumber) || string.IsNullOrWhiteSpace(VerifyCode))
        {
            LoginStatus = "请输入手机号和验证码";
            return;
        }

        IsBusy = true;
        try
        {
            var response = await MusicService.Client.LoginByCellphoneAsync(PhoneNumber.Trim(), VerifyCode.Trim());
            ExtractKugouResponse(response, out var isSuccess, out var errorMsg);
            if (isSuccess)
            {
                MusicService.SaveSession();
                LoginStatus = "已登录，登录态已保存";
                VipPrivilegeService.Instance.ResetSessionState();
                RefreshLoginState();
                if (IsLoggedIn)
                {
                    IsLoginDialogOpen = false;
                    await RefreshUserDataAsync();
                    await RefreshVipStateAsync();
                }
            }
            else
            {
                LoginStatus = $"登录失败：{errorMsg}";
            }
        }
        catch (Exception ex)
        {
            LoginStatus = $"登录失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshTokenAsync()
    {
        IsBusy = true;
        try
        {
            var response = await MusicService.Client.RefreshTokenAsync();
            ExtractKugouResponse(response, out var isSuccess, out var errorMsg);
            if (isSuccess)
            {
                MusicService.SaveSession();
                LoginStatus = "登录态已刷新";
                RefreshLoginState();
                if (IsLoggedIn)
                {
                    await RefreshUserDataAsync();
                    await RefreshVipStateAsync();
                }
            }
            else
            {
                LoginStatus = $"刷新失败：{errorMsg}";
            }
        }
        catch (Exception ex)
        {
            LoginStatus = $"刷新失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ClearLogin()
    {
        CancelQrPolling("已清除扫码状态");
        MusicService.ClearSession();
        VipPrivilegeService.Instance.ResetSessionState();
        QrCodeImage = null;
        QrUrl = string.Empty;
        QrStatus = "未生成二维码";
        LoginStatus = "已清除登录态";
        VipStatus = "VIP状态：未登录";
        VipDetail = string.Empty;
        IsLoginDialogOpen = false;
        ClearUserProfile();
        NotifyLoginStateChanged();
    }

    [RelayCommand]
    private async Task RefreshVipStateAsync()
    {
        if (IsVipBusy) return;
        IsVipBusy = true;
        VipStatus = "VIP状态：正在刷新";

        try
        {
            var status = await VipPrivilegeService.Instance.RefreshAsync();
            ApplyVipStatus(status);
            RefreshLoginState();
        }
        catch (Exception ex)
        {
            VipStatus = $"VIP状态：刷新失败：{ex.Message}";
            VipDetail = ex.ToString();
        }
        finally
        {
            IsVipBusy = false;
        }
    }

    [RelayCommand]
    private async Task ClaimVipAsync()
    {
        if (IsVipBusy) return;
        IsVipBusy = true;
        VipStatus = "VIP状态：正在领取今日畅听/VIP权益";

        try
        {
            var status = await VipPrivilegeService.Instance.EnsureManuallyAsync();
            ApplyVipStatus(status);
            RefreshLoginState();
        }
        catch (Exception ex)
        {
            VipStatus = $"VIP状态：领取失败：{ex.Message}";
            VipDetail = ex.ToString();
        }
        finally
        {
            IsVipBusy = false;
        }
    }

    private async Task PollQrLoginAsync(string key, CancellationToken cancellationToken)
    {
        IsQrPolling = true;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await MusicService.Client.CheckLoginQrAsync(key, cancellationToken);
            var status = KugouLiteClient.GetLoginQrStatus(response);
            QrStatus = status switch
            {
                KugouLoginQrStatus.WaitingScan => "等待扫码",
                KugouLoginQrStatus.WaitingConfirm => "等待手机确认",
                KugouLoginQrStatus.Success => "扫码登录成功",
                KugouLoginQrStatus.Expired => "二维码已过期",
                _ => "等待扫码状态"
            };

            if (status == KugouLoginQrStatus.Success)
            {
                MusicService.SaveSession();
                VipPrivilegeService.Instance.ResetSessionState();
                RefreshLoginState();
                IsLoginDialogOpen = false;
                await RefreshUserDataAsync();
                await RefreshVipStateAsync();
                return;
            }

            if (status == KugouLoginQrStatus.Expired)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        }

        QrStatus = "扫码等待超时";
    }

    private void CancelQrPolling(string status)
    {
        _qrPollingCts?.Cancel();
        _qrPollingCts?.Dispose();
        _qrPollingCts = null;
        IsQrPolling = false;
        QrStatus = status;
    }

    private void ApplyVipStatus(VipPrivilegeStatus status)
    {
        VipStatus = status.Message;
        VipDetail = status.Detail;
    }

    [RelayCommand]
    private async Task RefreshUserDataAsync()
    {
        if (IsProfileBusy) return;
        if (!IsLoggedIn)
        {
            ClearUserProfile();
            return;
        }

        IsProfileBusy = true;
        UserProfileStatus = "正在同步账号资料";

        try
        {
            await VipPrivilegeService.Instance.EnsureLoginFreshAsync();
            RefreshLoginState();
            await LoadProfileAsync();
            await LoadUserAssetsAsync();
            await SyncFavoriteStateAsync();
            UserProfileStatus = "账号资料已同步";
        }
        catch (Exception ex)
        {
            UserProfileStatus = $"账号资料同步失败：{ex.Message}";
        }
        finally
        {
            IsProfileBusy = false;
        }
    }

    private async Task LoadProfileAsync()
    {
        var state = MusicService.Client.GetLoginState();
        var result = await MusicService.Client.GetUserDetailTypedAsync();
        var user = result.Data;
        UserDisplayName = FirstNonEmpty(user?.Nickname, user?.Username, state.UserId, "酷狗用户");
        UserAvatarUrl = user?.Pic ?? string.Empty;
        UserIdText = $"userid {FirstNonEmpty(user?.UserId > 0 ? user.UserId.ToString() : null, state.UserId, "-")}";
        UserExpireText = FormatLoginExpireText(state, user?.Expires);
        MusicService.SaveSession();
    }

    private async Task LoadUserAssetsAsync()
    {
        UserPlaylists.Clear();
        UserCollections.Clear();
        UserHistory.Clear();

        try
        {
            var playlists = await MusicService.Client.GetUserPlaylistsTypedAsync(page: 1, pageSize: 40);
            UserPlaylistCountText = CountText(playlists.Total, playlists.Items.Count);
            foreach (var playlist in playlists.Items.Take(4))
            {
                UserPlaylists.Add(new UserLibraryItem(playlist.Name, $"{playlist.Count} 首 · {playlist.PlayCount} 播放", playlist.Pic, "歌单"));
            }
        }
        catch (Exception ex)
        {
            UserPlaylistCountText = "同步失败";
            UserPlaylists.Add(new UserLibraryItem("歌单同步失败", ex.Message, string.Empty, "歌单"));
        }

        try
        {
            var cloud = await MusicService.Client.GetUserCloudTypedAsync(page: 1, pageSize: 8);
            UserCollectionCountText = CountText(cloud.Total, cloud.Items.Count);
            foreach (var song in cloud.Items.Take(4))
            {
                UserCollections.Add(new UserLibraryItem(song.Title, song.Artist, song.CoverUrl, "云盘/收藏"));
            }
        }
        catch (Exception ex)
        {
            UserCollectionCountText = "同步失败";
            UserCollections.Add(new UserLibraryItem("云盘/收藏同步失败", ex.Message, string.Empty, "云盘/收藏"));
        }

        RefreshLocalHistory();

        RebuildUserLibraryPreview();
    }

    private async Task SyncFavoriteStateAsync()
    {
        try
        {
            await FavoriteSongService.Instance.RefreshFromCloudAsync();
            PlayerService.Instance.RefreshFavoriteState();
        }
        catch
        {
        }
    }

    private void ClearUserProfile()
    {
        UserDisplayName = "未登录";
        UserAvatarUrl = string.Empty;
        UserIdText = "userid -";
        UserExpireText = "登录到期：未同步";
        UserProfileStatus = "登录后同步账号资料";
        UserPlaylistCountText = "0";
        UserCollectionCountText = "0";
        UserHistoryCountText = "0";
        UserPlaylists.Clear();
        UserCollections.Clear();
        UserHistory.Clear();
        UserLibraryPreview.Clear();
    }

    private void RebuildUserLibraryPreview()
    {
        UserLibraryPreview.Clear();
        foreach (var item in UserPlaylists.Take(2).Concat(UserCollections.Take(2)))
        {
            UserLibraryPreview.Add(item);
        }
        foreach (var song in UserHistory.Take(2))
        {
            UserLibraryPreview.Add(new UserLibraryItem(song.Title, song.Artist, song.CoverUrl, "最近播放"));
        }
    }

    private void NotifyLoginStateChanged()
    {
        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(IsLoggedInPanelVisible));
        OnPropertyChanged(nameof(IsLoginPromptVisible));
    }

    private void OnFloatingLyricsStateChanged(object? sender, EventArgs e)
    {
        var fontSize = (decimal)FloatingLyricsService.Instance.FontSize;
        if (FloatingLyricsFontSize != fontSize)
        {
            _lastValidFloatingLyricsFontSize = fontSize;
            FloatingLyricsFontSize = fontSize;
        }

        NotifyFloatingLyricsStateChanged();
    }

    private void NotifyFloatingLyricsStateChanged()
    {
        OnPropertyChanged(nameof(IsFloatingLyricsSupported));
        OnPropertyChanged(nameof(IsFloatingLyricsOpen));
        OnPropertyChanged(nameof(IsFloatingLyricsLocked));
        OnPropertyChanged(nameof(IsFloatingLyricsCompactMode));
        OnPropertyChanged(nameof(IsFloatingLyricsCompactModeSupported));
        OnPropertyChanged(nameof(FloatingLyricsStatusText));
        OnPropertyChanged(nameof(FloatingLyricsFontSizeText));
    }

    [RelayCommand]
    private void ResetFloatingLyricsFontSize()
    {
        FloatingLyricsFontSize = (decimal)FloatingLyricsService.DefaultFontSize;
    }

    private void RestoreFloatingLyricsFontSize()
    {
        if (FloatingLyricsFontSize != _lastValidFloatingLyricsFontSize)
        {
            FloatingLyricsFontSize = _lastValidFloatingLyricsFontSize;
        }
    }

    private static string CountText(int total, int fallback) => (total > 0 ? total : fallback).ToString();

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string FormatLoginExpireText(KugouLoginState state, int? userExpires)
    {
        var expiresAt = state.TokenExpiresAt ?? ConvertUserExpires(userExpires);
        if (expiresAt is null)
        {
            return "登录到期：服务端未返回，已完成资料同步";
        }

        return $"登录到期：{expiresAt.Value.ToLocalTime():yyyy-MM-dd HH:mm}";
    }

    private static DateTimeOffset? ConvertUserExpires(int? expires)
    {
        if (expires is null || expires <= 0)
        {
            return null;
        }

        var value = expires.Value;
        return value > 1_600_000_000
            ? DateTimeOffset.FromUnixTimeSeconds(value)
            : DateTimeOffset.Now.AddSeconds(value);
    }

    private static void ApplyThemeMode(string mode)
    {
        if (Application.Current is null)
        {
            return;
        }

        Application.Current.RequestedThemeVariant = mode switch
        {
            "浅色" => ThemeVariant.Light,
            "跟随系统" => ThemeVariant.Default,
            _ => ThemeVariant.Dark
        };
    }

    private static Bitmap CreateQrBitmap(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(8);
        return new Bitmap(new MemoryStream(png));
    }
    [RelayCommand]
    private async Task PlayHistorySongAsync(KugouSong song)
    {
        if (song is null) return;
        if (PlayerService.IsSameSong(song, PlayerService.Instance.CurrentSong))
        {
            PlayerService.Instance.TogglePlayPause();
            return;
        }
        var index = UserHistory.IndexOf(song);
        await PlayerService.Instance.PlayQueueAsync(UserHistory.ToList(), index < 0 ? 0 : index, "最近播放", replaceQueue: true);
    }

    [RelayCommand]
    private void ViewAllHistory()
    {
        ShellNavigationService.Instance.Navigate("NavHistory");
    }

    [RelayCommand]
    private void ViewAllPlaylists()
    {
        ShellNavigationService.Instance.Navigate("NavPlaylists");
    }

    public void RefreshLocalHistory()
    {
        var history = LocalMusicStore.Instance.LoadLocalHistory(100);
        UserHistoryCountText = $"{history.Count} 首";
        UserHistory.Clear();
        foreach (var song in history.Take(4))
        {
            UserHistory.Add(song);
        }
    }

    private static void ExtractKugouResponse(KugouResponse response, out bool isSuccess, out string errorMessage)
    {
        isSuccess = response.IsSuccessStatusCode;
        errorMessage = string.Empty;
        if (!isSuccess)
        {
            errorMessage = $"HTTP {(int)response.StatusCode}";
            return;
        }

        var doc = response.TryParseJson();
        if (doc != null)
        {
            var root = doc.RootElement;
            int errorCode = 0;
            int status = 1;

            if (root.TryGetProperty("error_code", out var ec) && ec.ValueKind == System.Text.Json.JsonValueKind.Number)
            {
                errorCode = ec.GetInt32();
            }
            else if (root.TryGetProperty("errcode", out var err) && err.ValueKind == System.Text.Json.JsonValueKind.Number)
            {
                errorCode = err.GetInt32();
            }

            if (root.TryGetProperty("status", out var st) && st.ValueKind == System.Text.Json.JsonValueKind.Number)
            {
                status = st.GetInt32();
            }

            // In some KUGOU apis, status=0 is failure, or error_code != 0 is failure.
            if (errorCode != 0 || status == 0)
            {
                isSuccess = false;
                var msg = root.TryGetProperty("msg", out var m) && m.ValueKind == System.Text.Json.JsonValueKind.String ? m.GetString() :
                          root.TryGetProperty("message", out var m2) && m2.ValueKind == System.Text.Json.JsonValueKind.String ? m2.GetString() :
                          root.TryGetProperty("error", out var m3) && m3.ValueKind == System.Text.Json.JsonValueKind.String ? m3.GetString() : null;

                errorMessage = string.IsNullOrWhiteSpace(msg) ? $"API Error Code: {errorCode}" : msg;
            }
        }
    }
}

public sealed record UserLibraryItem(string Title, string Subtitle, string CoverUrl, string Category = "");
