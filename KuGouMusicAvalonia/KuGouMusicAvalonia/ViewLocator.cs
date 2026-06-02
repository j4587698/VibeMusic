using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using KuGouMusicAvalonia.ViewModels;

namespace KuGouMusicAvalonia;

/// <summary>
/// Given a view model, returns the corresponding view if possible.
/// </summary>
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        return param switch
        {
            MainViewModel => new Views.MainView(),
            DiscoverViewModel => new Views.DiscoverView(),
            PlaylistsViewModel => new Views.PlaylistsView(),
            ArtistsViewModel => new Views.ArtistsView(),
            PlaylistDetailViewModel => new Views.PlaylistDetailView(),
            RankingsViewModel => new Views.RankingsView(),
            RankingDetailViewModel => new Views.RankingDetailView(),
            ArtistDetailViewModel => new Views.ArtistDetailView(),
            SearchViewModel => new Views.SearchView(),
            SettingsViewModel => new Views.SettingsView(),
            HistoryViewModel => new Views.HistoryView(),
            NowPlayingViewModel => new Views.NowPlayingView(),
            LyricsViewModel => new Views.LyricsView(),
            _ => new TextBlock { Text = "Not Found: " + param.GetType().Name }
        };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}