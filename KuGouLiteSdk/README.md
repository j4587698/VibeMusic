# KuGouLiteSdk

这是把 `KuGouMusicApi` 的“概念版”酷狗请求链路抽出来做成的 C# DLL。它不启动 Express，不暴露对外 URL，只负责在 .NET 内部直接请求酷狗上游接口。

## 当前封装范围

- 固定概念版参数：`appid=3116`、`clientver=11440`、概念版 Android 签名盐、概念版 RSA 公钥。
- 核心请求：`KugouLiteClient.SendAsync(KugouRequest)`，可直接构造任意酷狗上游请求。
- 全量入口：`KugouApiCatalog.All` 已完整登记原 Node.js API 中 150+ 个路由（包括最新版曲谱接口），可通过 `InvokeRouteAsync(route, parameters)` 直接调用。
- 参数元数据：`KugouApiParameterCatalog.All` 抽取了路由的输入参数限制。
- EchoMusic 强类型输出层：按 EchoMusic 的 `models` + `mappers` 增加 `KugouSong`、`KugouPlaylist`、`KugouAlbum`、`KugouArtist`、`KugouRank`、`KugouComment`、`KugouUser`、`KugouVideo`、`KugouAudioUrl` 等 DTO；typed result 会保留 `Raw` 原始响应。
- AOT DTO 层：155 个目录接口都有独立的 `*ResponseDto`，并全部加入 `KugouJsonSerializerContext` 源生成上下文；可通过 `InvokeRouteDtoAsync()` 返回 AOT 友好的 `KugouRouteDtoResult`，不必只处理 `BodyText`。
- Cookie/设备态：自动生成并维护 `KUGOU_API_GUID`、`KUGOU_API_MID`、`KUGOU_API_DEV`、`KUGOU_API_MAC`、`token`、`userid`、`dfid` 等。
- 签名/加密：Android/Web/Register 签名、AES-CBC、RSA Raw、RSA PKCS#1、歌单/云盘 AES、KRC 歌词解码。
- 强类型高阶方法：
  - `SendCaptchaAsync`
  - `LoginByCellphoneAsync`
  - `CreateLoginQrSessionAsync`
  - `GetLoginQrKeyAsync`
  - `CreateLoginQrCodeAsync`
  - `CheckLoginQrAsync`
  - `WaitForLoginQrAsync`
  - `RefreshTokenAsync`
  - `RegisterDeviceAsync`
  - `GetUserDetailAsync`
  - `SearchAsync`
  - `SearchPublicSongsAsync`
  - `SearchMixedAsync`
  - `SearchLyricAsync`
  - `GetLyricAsync`
  - `GetSongUrlAsync`
  - `GetSongUrlNewAsync`
  - `GetAlbumDetailAsync`
  - `GetAlbumSongsAsync`
  - `GetPlaylistTracksAsync`
  - `GetRankAudioAsync`
  - `GetServerNowAsync`
- EchoMusic 风格强类型方法：
  - `SearchSongsTypedAsync` / `SearchPlaylistsTypedAsync` / `SearchAlbumsTypedAsync` / `SearchArtistsTypedAsync` / `SearchMvsTypedAsync`
  - `GetPlaylistDetailTypedAsync` / `GetPlaylistTracksTypedAsync` / `GetPlaylistTracksNewTypedAsync` / `GetUserPlaylistsTypedAsync`
  - `GetRankListTypedAsync` / `GetRankTopTypedAsync` / `GetRankSongsTypedAsync`
  - `GetAlbumDetailTypedAsync` / `GetAlbumSongsTypedAsync`
  - `GetArtistDetailTypedAsync` / `GetArtistSongsTypedAsync` / `GetArtistAlbumsTypedAsync` / `GetArtistVideosTypedAsync`
  - `GetMusicCommentsTypedAsync` / `GetPlaylistCommentsTypedAsync` / `GetAlbumCommentsTypedAsync` / `GetFloorCommentsTypedAsync`
  - `GetUserDetailTypedAsync` / `GetUserHistoryTypedAsync` / `GetUserCloudTypedAsync`
  - `GetSongUrlTypedAsync` / `GetSongPrivilegeLiteTypedAsync` / `GetCloudSongUrlTypedAsync`
  - `GetVideoDetailTypedAsync` / `GetVideoUrlTypedAsync` / `GetVideoPrivilegeTypedAsync`

> 所有接口都已先以“目录 + 通用调用器”迁入 DLL；上面的强类型方法是对常用/复杂接口再做的一层便捷封装。`SearchSongsTypedAsync()` 会优先尝试概念版签名搜索，如果上游返回 `Parameter Error`，自动回退到可用的 `SearchPublicSongsAsync()`，避免界面搜索恒为 0 条。`GetSongUrlAsync()` / `GetSongUrlNewAsync()` / `GetSongUrlTypedAsync()` 在没有有效 `dfid` 时会先调用 `/register/dev` 自动注册设备；随后依次尝试概念版 `v5/url`、`ppage_id` 回退、新版 `priv_url` 和旧移动端 `playInfo`。如果歌曲需要付费/VIP/验证，上游仍可能不返回下载 URL。EchoMusic 输入输出分析见 [docs/echomusic-typed-output-analysis.md](docs/echomusic-typed-output-analysis.md)。

## 构建

```powershell
dotnet build
```

输出 DLL：

```text
bin/Debug/net8.0/KuGouLiteSdk.dll
```

Release：

```powershell
dotnet build -c Release
```

## 使用示例

```csharp
using KuGou.Lite;

using var client = new KugouLiteClient();

// 1. 原项目 README 要求播放 URL 前先通过 /register/dev 拿 dfid；SDK 取播放 URL 时会自动执行，
//    也可以显式注册，便于提前持久化设备态。
var device = await client.RegisterDeviceAsync();
Console.WriteLine(device.BodyText);

// 2. 搜索。
var search = await client.SearchAsync("海阔天空");
Console.WriteLine(search.BodyText);

// 3. 获取歌曲 URL。
var url = await client.GetSongUrlAsync("歌曲 hash", albumAudioId: 0, quality: "128");
Console.WriteLine(url.BodyText);

// 3.1 EchoMusic 风格强类型搜索。
var typedSearch = await client.SearchSongsTypedAsync("海阔天空");
foreach (var song in typedSearch.Items)
{
  Console.WriteLine($"{song.Title} - {song.Artist} - {song.Hash}");
}

// 3.2 强类型播放地址，同时保留 Raw 原始响应。
var typedUrl = await client.GetSongUrlTypedAsync("歌曲 hash", quality: "128");
Console.WriteLine(typedUrl.Data?.Url);

// 4. 调用任意已迁入目录的接口。参数会按目录中的 GET/POST/Body/Query 规则进入酷狗上游请求。
var rank = await client.InvokeRouteAsync("/rank/list", new Dictionary<string, object?>
{
  ["withsong"] = 1
});
Console.WriteLine(rank.BodyText);

// 4.1 AOT DTO 调用：155 个目录接口都有对应 ResponseDto 元数据。
var rankDto = await client.InvokeRouteDtoAsync(
  "/rank/list",
  KugouRouteRequestDto.FromParameters(("withsong", 1)));
Console.WriteLine(rankDto.Response.Data?.GetRawText());

// 查看原文档抽取出来的输入参数说明。
var meta = KugouApiParameterCatalog.Find("/song/url");
Console.WriteLine(string.Join(", ", meta?.Required.Select(p => p.Name) ?? []));

// 5. 登录流程：先发验证码，再传验证码登录。
await client.SendCaptchaAsync("手机号");
var login = await client.LoginByCellphoneAsync("手机号", "验证码");
Console.WriteLine(login.BodyText);

// 登录成功后 CookieStore 会自动保存 token/userid/vip_token。
var user = await client.GetUserDetailAsync();
Console.WriteLine(user.BodyText);

// 6. 酷狗概念版扫码登录。
var qr = await client.CreateLoginQrSessionAsync();
Console.WriteLine(qr.Url); // 把这个 URL 渲染成二维码，用酷狗概念版 App 扫码。

// 轮询直到成功或过期；成功后 CookieStore 会自动保存 token/userid。
var qrLogin = await client.WaitForLoginQrAsync(qr.Key);
Console.WriteLine(KugouLiteClient.GetLoginQrStatus(qrLogin));
Console.WriteLine(qrLogin.BodyText);
```

扫码登录说明：SDK 内部对齐原 `/login/qr/key` 与 `/login/qr/check`，使用 `Web` 签名请求 `login-user.kugou.com`；二维码内容是 `https://h5.kugou.com/apps/loginQRCode/html/index.html?qrcode=...`，需要调用方自行用 UI 或二维码库渲染成图片后，用酷狗概念版 App 扫码确认。

## 迁移更多接口

对照原项目 `module/*.js`：

1. 把模块里的 `dataMap`/`paramsMap` 翻成 `Dictionary<string, object?>`。
2. 设置 `BaseUri`、`Path`、`Method`。
3. 设置 `Headers` 中的 `x-router`、`kg-tid` 等。
4. 保持 `EncryptType=Android`；需要特殊签名时设置 `Web` 或 `Register`。
5. 需要歌曲播放 key 时设置 `EncryptKey=true`。
6. 不需要默认参数时设置 `ClearDefaultParams=true`。
7. 不需要签名时设置 `NotSignature=true`。
8. 需要登录态的接口直接使用同一个 `KugouLiteClient` 实例。

更完整流程见 [docs/csharp-dll-flow.md](docs/csharp-dll-flow.md)。
