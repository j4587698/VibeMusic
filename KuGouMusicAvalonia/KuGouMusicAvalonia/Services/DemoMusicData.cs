using KuGou.Lite;
using System.Collections.Generic;

namespace KuGouMusicAvalonia.Services;

public static class DemoMusicData
{
    public static IReadOnlyList<KugouPlaylist> Playlists { get; } = new[]
    {
        new KugouPlaylist
        {
            Id = 1001,
            Name = "夜色开车歌单",
            Intro = "合成器、城市流行和轻快鼓点。",
            Nickname = "VIBE 编辑部",
            Tags = "流行,电子,夜晚",
            PlayCount = 1452000,
            Count = 36
        },
        new KugouPlaylist
        {
            Id = 1002,
            Name = "午后工作流",
            Intro = "少打扰、节奏稳的专注音乐。",
            Nickname = "Lumina Picks",
            Tags = "轻音乐,独立,专注",
            PlayCount = 876000,
            Count = 42
        },
        new KugouPlaylist
        {
            Id = 1003,
            Name = "高保真人声测试",
            Intro = "适合检查耳机和音箱的人声细节。",
            Nickname = "Audio Lab",
            Tags = "HiFi,人声,现场",
            PlayCount = 1184000,
            Count = 28
        },
        new KugouPlaylist
        {
            Id = 1004,
            Name = "周末慢速巡航",
            Intro = "低速律动、柔和贝斯和温暖旋律。",
            Nickname = "Echo Curator",
            Tags = "放松,R&B,爵士",
            PlayCount = 653000,
            Count = 31
        },
        new KugouPlaylist
        {
            Id = 1005,
            Name = "清晨唤醒计划",
            Intro = "明亮、干净、不刺耳的早晨歌单。",
            Nickname = "Morning Set",
            Tags = "清晨,民谣,流行",
            PlayCount = 734000,
            Count = 24
        },
        new KugouPlaylist
        {
            Id = 1006,
            Name = "电子跃迁",
            Intro = "更适合运动和短途通勤的节拍。",
            Nickname = "Beat Works",
            Tags = "电子,运动,节奏",
            PlayCount = 1590000,
            Count = 48
        }
    };

}