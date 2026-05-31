# KuGou Music C#

This directory is the C# source root for the music player and SDK.

It contains the native C# implementation only:

- `KuGouLiteSdk`: direct C# SDK for KuGou upstream APIs.
- `KuGouMusicAvalonia`: Avalonia app, including shared UI plus Desktop, Android, and iOS hosts.

JavaScript reference projects such as `EchoMusic` and `KuGouMusicApi` stay outside this tree. The Avalonia Browser host is also omitted because it brings `wwwroot` JavaScript/HTML/CSS assets and is not part of the local C# product target.

## Build

Desktop validation:

```powershell
dotnet build .\KuGouMusicAvalonia\KuGouMusicAvalonia.Desktop\KuGouMusicAvalonia.Desktop.csproj
```

Full solution metadata is in `KuGouMusic.slnx`. Android and iOS builds require the corresponding local .NET workloads and SDKs.