using Avalonia.Controls;
using Avalonia.Media;
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KuGouMusicAvalonia.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private CancellationTokenSource? _qrPollingCts;
    private decimal _lastValidFloatingLyricsFontSize = (decimal)FloatingLyricsService.DefaultFontSize;
    private static readonly TimeSpan ProfileRetryDelay = TimeSpan.FromMilliseconds(1500);

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
    private bool _isCaptchaDialogOpen;

    [ObservableProperty]
    private string _captchaEventId = string.Empty;

    [ObservableProperty]
    private string _captchaVerifyCode = string.Empty;

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
    private string _loginUsername = string.Empty;
    [ObservableProperty]
    private string _loginPassword = string.Empty;
    [ObservableProperty]
    private string _weChatLoginCode = string.Empty;
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
    [NotifyPropertyChangedFor(nameof(HasUserExpireText))]
    private string _userExpireText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUserProfileStatus))]
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
    public bool HasUserExpireText => !string.IsNullOrWhiteSpace(UserExpireText);
    public bool HasUserProfileStatus => !string.IsNullOrWhiteSpace(UserProfileStatus);
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
    }

    public async Task ActivateAsync()
    {
        RefreshLoginState();
        if (!IsLoggedIn)
        {
            ClearUserProfile();
            return;
        }

        RefreshLocalHistory();
        RebuildUserLibraryPreview();
        if (!ApplyUserProfileCache())
        {
            await RefreshUserDataAsync();
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

    [ObservableProperty]
    private string _sendCodeButtonText = "获取验证码";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCodeCommand))]
    private bool _canSendCode = true;

    [RelayCommand(CanExecute = nameof(CanSendCode))]
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
            ExtractKugouResponse(response, out var isSuccess, out var errorMsg, out var errorCode, out var eventId);
            
            if (!isSuccess && errorCode == 20006)
            {
                await StartCaptchaVerificationAsync(eventId, () => SendCodeCommand.Execute(null));
                return;
            }

            if (isSuccess)
            {
                LoginStatus = "验证码已发送";
                _ = StartSendCodeCountdownAsync();
            }
            else
            {
                LoginStatus = $"验证码发送失败：{errorMsg}";
            }
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

    private async Task StartSendCodeCountdownAsync()
    {
        CanSendCode = false;
        for (var i = 60; i > 0; i--)
        {
            SendCodeButtonText = $"{i}s 后重试";
            await Task.Delay(1000);
        }
        SendCodeButtonText = "获取验证码";
        CanSendCode = true;
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
            ExtractKugouResponse(response, out var isSuccess, out var errorMsg, out var errorCode, out var eventId);
            if (errorCode == 20006)
            {
                await StartCaptchaVerificationAsync(eventId, () => LoginByPhoneCommand.Execute(null));
                return;
            }
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
    private async Task LoginByPasswordAsync()
    {
        if (string.IsNullOrWhiteSpace(LoginUsername))
        {
            LoginStatus = "请输入用户名";
            return;
        }

        IsBusy = true;
        try
        {
            var response = await MusicService.Client.LoginByPasswordAsync(LoginUsername.Trim(), LoginPassword);
            ExtractKugouResponse(response, out var isSuccess, out var errorMsg, out var errorCode, out var eventId);
            if (errorCode == 20006)
            {
                await StartCaptchaVerificationAsync(eventId, () => LoginByPasswordCommand.Execute(null));
                return;
            }
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
    private async Task LoginByWeChatAsync()
    {
        if (string.IsNullOrWhiteSpace(WeChatLoginCode))
        {
            LoginStatus = "请输入微信授权码";
            return;
        }

        IsBusy = true;
        try
        {
            var response = await MusicService.Client.LoginByOpenPlatAsync(WeChatLoginCode.Trim());
            ExtractKugouResponse(response, out var isSuccess, out var errorMsg, out _, out _);
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
            LoginStatus = $"微信登录失败：{ex.Message}";
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
            ExtractKugouResponse(response, out var isSuccess, out var errorMsg, out _, out _);
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
    private Task OpenDataDirectoryAsync()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = AppStateStore.AppDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            LoginStatus = $"打开数据目录失败：{ex.Message}";
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private void ClearAllData()
    {
        CancelQrPolling("已清除扫码状态");
        
        // 1. 彻底清理底层数据和配置
        MusicService.ClearAllData();
        VipPrivilegeService.Instance.ResetSessionState();
        
        // 2. 软重启，重建整个 UI 根节点，从而销毁所有旧 ViewModel 并重置状态
        if (Application.Current is App app)
        {
            app.RestartAppUI();
        }
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
            await LoadProfileWithRetryAsync();
            var assetsSynced = await LoadUserAssetsAsync();
            await SyncFavoriteStateAsync();
            var profileSynced = HasProfileContent();
            if (assetsSynced && profileSynced)
            {
                SaveUserProfileCache();
            }

            UserProfileStatus = assetsSynced && profileSynced ? "账号资料已同步" : "部分账号资料同步失败";
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

    private async Task LoadProfileWithRetryAsync()
    {
        await LoadProfileAsync();
        if (HasProfileContent())
        {
            return;
        }

        await Task.Delay(ProfileRetryDelay);
        await LoadProfileAsync();
    }

    private async Task<bool> LoadUserAssetsAsync()
    {
        var synced = true;

        try
        {
            var playlists = await MusicService.Client.GetUserPlaylistsTypedAsync(page: 1, pageSize: 40);
            ApplyProfileFallbackFromPlaylists(playlists.Items);
            var playlistItems = playlists.Items
                .Take(4)
                .Select(playlist => new UserLibraryItem(playlist.Name, $"{playlist.Count} 首 · {playlist.PlayCount} 播放", playlist.Pic, "歌单"))
                .ToList();
            UserPlaylistCountText = CountText(playlists.Total, playlists.Items.Count);
            ReplaceUserLibraryItems(UserPlaylists, playlistItems);
        }
        catch
        {
            synced = false;
            if (UserPlaylists.Count == 0)
            {
                UserPlaylistCountText = "同步失败";
            }
        }

        try
        {
            var cloud = await MusicService.Client.GetUserCloudTypedAsync(page: 1, pageSize: 8);
            var collectionItems = cloud.Items
                .Take(4)
                .Select(song => new UserLibraryItem(song.Title, song.Artist, song.CoverUrl, "云盘/收藏"))
                .ToList();
            UserCollectionCountText = CountText(cloud.Total, cloud.Items.Count);
            ReplaceUserLibraryItems(UserCollections, collectionItems);
        }
        catch
        {
            synced = false;
            if (UserCollections.Count == 0)
            {
                UserCollectionCountText = "同步失败";
            }
        }

        RefreshLocalHistory();
        RebuildUserLibraryPreview();
        return synced;
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
        UserExpireText = string.Empty;
        UserProfileStatus = "登录后同步账号资料";
        UserPlaylistCountText = "0";
        UserCollectionCountText = "0";
        UserHistoryCountText = "0";
        UserPlaylists.Clear();
        UserCollections.Clear();
        UserHistory.Clear();
        UserLibraryPreview.Clear();
    }

    private void ApplyProfileFallbackFromPlaylists(IReadOnlyList<KugouPlaylist> playlists)
    {
        var owner = playlists.FirstOrDefault(playlist =>
            !string.IsNullOrWhiteSpace(playlist.Nickname) ||
            !string.IsNullOrWhiteSpace(playlist.UserPic));
        if (owner is null)
        {
            return;
        }

        var state = MusicService.Client.GetLoginState();
        if (IsFallbackDisplayName(UserDisplayName, state.UserId) && !string.IsNullOrWhiteSpace(owner.Nickname))
        {
            UserDisplayName = owner.Nickname;
        }

        if (string.IsNullOrWhiteSpace(UserAvatarUrl) && !string.IsNullOrWhiteSpace(owner.UserPic))
        {
            UserAvatarUrl = owner.UserPic;
        }
    }

    private bool HasProfileContent()
    {
        var state = MusicService.Client.GetLoginState();
        return !string.IsNullOrWhiteSpace(UserAvatarUrl) || !IsFallbackDisplayName(UserDisplayName, state.UserId);
    }

    private static bool IsFallbackDisplayName(string value, string? userId)
    {
        return string.IsNullOrWhiteSpace(value) ||
            string.Equals(value, "未登录", StringComparison.Ordinal) ||
            string.Equals(value, "酷狗用户", StringComparison.Ordinal) ||
            !string.IsNullOrWhiteSpace(userId) && string.Equals(value, userId, StringComparison.Ordinal);
    }

    private bool ApplyUserProfileCache()
    {
        var cache = LocalMusicStore.Instance.LoadUserProfileCache();
        if (cache is null)
        {
            return false;
        }

        var state = MusicService.Client.GetLoginState();
        UserDisplayName = FirstNonEmpty(cache.DisplayName, "酷狗用户");
        UserAvatarUrl = cache.AvatarUrl;
        UserIdText = FirstNonEmpty(cache.UserIdText, "userid -");
        UserExpireText = FormatLoginExpireText(state, null);
        UserPlaylistCountText = FirstNonEmpty(cache.PlaylistCountText, "0");
        UserCollectionCountText = FirstNonEmpty(cache.CollectionCountText, "0");
        ReplaceUserLibraryItems(UserPlaylists, cache.Playlists.Select(item => ToUserLibraryItem(item, "歌单")));
        ReplaceUserLibraryItems(UserCollections, cache.Collections.Select(item => ToUserLibraryItem(item, "云盘/收藏")));
        RefreshLocalHistory();
        RebuildUserLibraryPreview();
        UserProfileStatus = string.Empty;
        return true;
    }

    private void SaveUserProfileCache()
    {
        LocalMusicStore.Instance.SaveUserProfileCache(new UserProfileCacheSnapshot(
            UserDisplayName,
            UserAvatarUrl,
            UserIdText,
            UserPlaylistCountText,
            UserCollectionCountText,
            UserPlaylists.Select(ToCacheItem).ToList(),
            UserCollections.Select(ToCacheItem).ToList(),
            DateTime.UtcNow));
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

    private static void ReplaceUserLibraryItems(ObservableCollection<UserLibraryItem> target, IEnumerable<UserLibraryItem> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private static UserLibraryItem ToUserLibraryItem(UserLibraryCacheItem item, string category)
    {
        return new UserLibraryItem(item.Title, item.Subtitle, item.CoverUrl, category);
    }

    private static UserLibraryCacheItem ToCacheItem(UserLibraryItem item)
    {
        return new UserLibraryCacheItem(item.Title, item.Subtitle, item.CoverUrl);
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
            return string.Empty;
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
    private void OpenUserHistory()
    {
        ShellNavigationService.Instance.Navigate("NavHistory");
    }

    [RelayCommand]
    private void OpenUserPlaylists()
    {
        ShellNavigationService.Instance.Navigate("NavPlaylists");
    }

    [RelayCommand]
    private void OpenUserCloud()
    {
        ShellNavigationService.Instance.Navigate("NavCloud");
    }

    [RelayCommand]
    private async Task ShowDeviceListAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var response = await MusicService.Client.LoginDeviceListAsync();
            var doc = response.TryParseJson();
            if (doc is not null && doc.RootElement.TryGetProperty("data", out var data))
            {
                var deviceList = new List<string>();
                if (data.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var item in data.EnumerateArray())
                    {
                        var name = item.TryGetProperty("devicename", out var dn) ? dn.GetString() : "";
                        var model = item.TryGetProperty("devicemodel", out var dm) ? dm.GetString() : "";
                        var uuid = item.TryGetProperty("uuid", out var u) ? u.GetString() : "";
                        deviceList.Add($"{name} {model} ({uuid})");
                    }
                }
                LoginStatus = deviceList.Count > 0
                    ? $"已登录设备：{string.Join("；", deviceList.Take(3))}{(deviceList.Count > 3 ? $" 等 {deviceList.Count} 台" : "")}"
                    : "未找到已登录设备";
            }
            else
            {
                LoginStatus = "获取设备列表失败";
            }
        }
        catch (Exception ex)
        {
            LoginStatus = $"设备列表加载失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
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

    private static void ExtractKugouResponse(KugouResponse response, out bool isSuccess, out string errorMessage, out int errorCode, out string eventId)
    {
        isSuccess = !MusicService.TryGetResponseError(response, out errorMessage, out errorCode, out eventId);
    }
    
    private async Task StartCaptchaVerificationAsync(string eventId, Action retryAction)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            LoginStatus = "风控拦截，但未返回 eventid，自动绕过失败";
            return;
        }

        IsBusy = true;
        try
        {
            LoginStatus = "正在全自动绕过风控验证...";
            var response = await MusicService.Client.SubmitVerifyCodeAsync(eventId, string.Empty);
            ExtractKugouResponse(response, out var isSuccess, out var errorMsg, out _, out _);
            if (isSuccess)
            {
                LoginStatus = "自动验证成功，正在继续登录...";
                retryAction();
            }
            else
            {
                LoginStatus = $"自动验证失败：{errorMsg}";
            }
        }
        catch (Exception ex)
        {
            LoginStatus = $"自动验证发生异常：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CloseCaptchaDialog()
    {
        IsCaptchaDialogOpen = false;
    }

    [RelayCommand]
    private Task SubmitCaptchaAsync()
    {
        return Task.CompletedTask;
    }

}

public sealed record UserLibraryItem(string Title, string Subtitle, string CoverUrl, string Category = "");
