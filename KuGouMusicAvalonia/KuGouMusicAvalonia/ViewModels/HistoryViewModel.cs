using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KuGou.Lite;
using KuGouMusicAvalonia.Services;

namespace KuGouMusicAvalonia.ViewModels;

public sealed class HistoryHeaderSection
{
    public static HistoryHeaderSection Instance { get; } = new();
    private HistoryHeaderSection() { }
}

public partial class HistoryViewModel : ViewModelBase
{
    public ObservableCollection<object> PageItems { get; } = new();
    public ObservableCollection<KugouSong> Songs { get; } = new();

    [ObservableProperty]
    private string _statusMessage = string.Empty;
    
    [ObservableProperty]
    private string _songCountText = "0 首歌曲";

    public HistoryViewModel()
    {
        LoadHistory();
    }

    public void LoadHistory()
    {
        try
        {
            var history = LocalMusicStore.Instance.LoadLocalHistory(200);
            Songs.Clear();
            PageItems.Clear();
            PageItems.Add(HistoryHeaderSection.Instance);
            foreach (var song in history)
            {
                Songs.Add(song);
                PageItems.Add(song);
            }
            SongCountText = $"{Songs.Count} 首歌曲";
        }
        catch (Exception)
        {
            StatusMessage = "加载历史记录失败";
        }
    }

    [RelayCommand]
    private async Task PlaySongAsync(KugouSong song)
    {
        if (song is null) return;
        if (PlayerService.IsSameSong(song, PlayerService.Instance.CurrentSong))
        {
            PlayerService.Instance.TogglePlayPause();
            return;
        }
        var index = Songs.IndexOf(song);
        await PlayerService.Instance.PlayQueueAsync(Songs.ToList(), index < 0 ? 0 : index, "历史播放", replaceQueue: true);
    }
    
    [RelayCommand]
    private async Task PlayAllAsync()
    {
        if (Songs.Count > 0)
        {
            await PlayerService.Instance.PlayQueueAsync(Songs.ToList(), 0, "历史播放", replaceQueue: true);
        }
    }
}
