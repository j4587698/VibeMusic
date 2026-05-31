# EchoMusic 输入输出与强类型 SDK 方案

结论：EchoMusic 没有官方接口 schema，但它自己维护了一套稳定 UI 输出模型与字段别名 mapper。可以把这套模型迁移到 C#，让常用接口返回 `Song`、`Playlist`、`Album`、`Artist`、`Rank`、`Comment`、`User`、`Video`、`AudioUrl` 等强类型对象；剩余长尾接口继续走 `InvokeRouteAsync()` 返回原始 JSON。

## 1. 请求链路

EchoMusic 的请求不是直接访问外部 HTTP API，而是：

1. `src/renderer/api/*.ts` 定义前端方法和输入参数。
2. `src/renderer/utils/request.ts` 通过 IPC 把请求发给 Electron 主进程。
3. `src/main/server.ts` 动态加载 `server/module/*.js`。
4. `server` 子模块指向 KuGouMusicApi，最终使用 KuGouMusicApi 的 `createRequest()` 请求酷狗上游。

## 2. 通用响应提取规则

EchoMusic 不是按单一 JSON 路径取值，而是兼容多个路径：

| 类型 | 提取候选路径 |
| --- | --- |
| 列表 | `data.special_list`、`data.lists`、`data.list`、`data.info`、`data.song_list`、`data.songlist`、`data.songs`、`songs.list`、`info.list`、`payload.list`、`payload.data`、`lists`、`list`、`songs`、`songlist`、`items`、`data` |
| 单对象 | 上述列表第一项、`data`、`info`、根对象 |
| 总数 | `data.total`、`data.totalCount`、`data.count`、`data.counts`、`total`、`totalCount`、`count`、`counts` |
| 评论热评 | `data.weight_list`、`data.hot_list`、`data.star_cmts.list`、`data.star_comment.list` |

## 3. EchoMusic 稳定输出模型

| 模型 | 主要字段 | 来源 mapper |
| --- | --- | --- |
| `KugouSong` | `Id`、`SongId`、`Title`、`Artist`、`Artists`、`Album`、`AlbumId`、`Duration`、`CoverUrl`、`Hash`、`MvHash`、`MixSongId`、`FileId`、`Privilege`、`PayType`、`OldCpy`、`RelateGoods`、`LyricSnippet` | `mapTopSong`、`mapPlaylistSong`、`mapArtistSong`、`mapAlbumSong`、`mapRankSong`、`mapSearchSong`、`mapHistorySong`、`mapCloudSong` |
| `KugouPlaylist` | `Id`、`Listid`、`GlobalCollectionId`、`ListCreateGid`、`ListCreateUserid`、`Name`、`Pic`、`Intro`、`Nickname`、`UserPic`、`Tags`、`PlayCount`、`Count`、`Source`、`OriginalId` | `mapPlaylistMeta` |
| `KugouAlbum` | `Id`、`Name`、`Pic`、`Intro`、`SingerName`、`SingerId`、`PublishTime`、`SongCount`、`PlayCount`、`Heat`、`Language`、`Type`、`Company` | `mapAlbumMeta`、`mapAlbumDetailMeta` |
| `KugouArtist` | `Id`、`Name`、`Pic`、`Intro`、`SongCount`、`AlbumCount`、`MvCount`、`FansCount`、`Birthday`、`IsFollowed` | `mapArtistMeta`、`mapArtistDetailMeta` |
| `KugouRank` | `Id`、`Name`、`Pic`、`RankType`、`RankTypeName`、`UpdateFrequency`、`Group`、`Type` | `mapRankMeta` |
| `KugouComment` | `Id`、`UserName`、`Avatar`、`Content`、`Time`、`LikeCount`、`ReplyCount`、`IsHot`、`IsStar`、`SpecialId`、`Tid`、`Code`、`MixSongId` | `mapCommentItem` |
| `KugouUser` | `UserId`、`Token`、`Username`、`Nickname`、`Mobile`、`Pic`、`Expires`、`T1`、`VipType`、`PGrade`、`Detail`、`Vip` | `mapUser` |
| `KugouVideo` | `Id`、`Hash`、`Title`、`Description`、`CoverUrl`、`Duration`、`PlayCount`、`PublishTime`、`AlbumAudioId`、`Authors`、`Tags`、`Sources` | `mapVideoMeta` |
| `KugouAudioUrl` | `Url`、`Loudness` | EchoMusic `resolveUrlFromResponse()`、`resolveTrackLoudness()` |

## 4. 关键接口输入输出表

### 搜索

| 路由 | 输入参数 | 输出提取 | 规范化输出 | C# 方法 |
| --- | --- | --- | --- | --- |
| `/search` `type=song` | `keywords`、`page`、`pagesize` | `data.lists/list` | `KugouListResult<KugouSong>` | `SearchSongsTypedAsync()` |
| `/search` `type=special` | `keywords`、`page`、`pagesize` | `data.lists/list` | `KugouListResult<KugouPlaylist>` | `SearchPlaylistsTypedAsync()` |
| `/search` `type=album` | `keywords`、`page`、`pagesize` | `data.lists/list` | `KugouListResult<KugouAlbum>` | `SearchAlbumsTypedAsync()` |
| `/search` `type=author` | `keywords`、`page`、`pagesize` | `data.lists/list` | `KugouListResult<KugouArtist>` | `SearchArtistsTypedAsync()` |
| `/search` `type=mv` | `keywords`、`page`、`pagesize` | `data.lists/list` | `KugouListResult<KugouVideo>` | `SearchMvsTypedAsync()` |
| `/search/hot` | 无 | `data.list` | 热词分类，可后续建 `SearchHotCategory` | raw fallback |
| `/search/default` | 无 | `data.keyword/show_keyword` | 默认搜索词 | raw fallback |
| `/search/suggest` | `keywords` | `data[].RecordDatas[]` | 搜索建议分类 | raw fallback |

### 歌曲、播放、推荐

| 路由 | 输入参数 | 输出提取 | 规范化输出 | C# 方法 |
| --- | --- | --- | --- | --- |
| `/song/url` | `hash`、`quality`、`ppage_id?` | `url/play_url/playUrl`、递归 `data/info` | `KugouTypedResult<KugouAudioUrl>` | `GetSongUrlTypedAsync()` |
| `/privilege/lite` | `hash`、`album_id?` | `data[0].relate_goods` | `KugouListResult<KugouSongRelateGood>` | `GetSongPrivilegeLiteTypedAsync()` |
| `/user/cloud/url` | `hash` | `data.url` | `KugouTypedResult<KugouAudioUrl>` | `GetCloudSongUrlTypedAsync()` |
| `/top/song` | 无 | 通用列表路径 | `KugouListResult<KugouSong>` | `GetNewSongsTypedAsync()` |
| `/everyday/recommend` | 无 | 通用列表路径 | `KugouListResult<KugouSong>` | `GetEverydayRecommendTypedAsync()` |
| `/personal/fm` | `hash?`、`songid?`、`playtime?`、`mode?`、`action?`、`song_pool_id?` | 通用列表路径 | `KugouListResult<KugouSong>` | `GetPersonalFmTypedAsync()` |
| `/search/lyric` | `hash` 或关键词 | 原始歌词候选 | 保留 raw JSON | `SearchLyricAsync()` raw |
| `/lyric` | `id`、`accesskey`、`fmt`、`decode` | 歌词文本/KRC 解码 | raw/decoded text | `GetLyricAsync()` raw |

### 歌单、榜单

| 路由 | 输入参数 | 输出提取 | 规范化输出 | C# 方法 |
| --- | --- | --- | --- | --- |
| `/playlist/detail` | `ids` | 单对象/列表第一项 | `KugouTypedResult<KugouPlaylist>` | `GetPlaylistDetailTypedAsync()` |
| `/playlist/track/all` | `id`、`page`、`pagesize` | 通用列表路径 | `KugouListResult<KugouSong>` | `GetPlaylistTracksTypedAsync()` |
| `/playlist/track/all/new` | `listid`、`page`、`pagesize` | 通用列表路径 | `KugouListResult<KugouSong>` | `GetPlaylistTracksNewTypedAsync()` |
| `/user/playlist` | `page`、`pagesize` | 通用列表路径 | `KugouListResult<KugouPlaylist>` | `GetUserPlaylistsTypedAsync()` |
| `/rank/list` | 无 | 通用列表路径 | `KugouListResult<KugouRank>` | `GetRankListTypedAsync()` |
| `/rank/top` | 无 | 通用列表路径 | `KugouListResult<KugouRank>` | `GetRankTopTypedAsync()` |
| `/rank/audio` | `rankid/rank_id`、`page`、`pagesize` | 通用列表路径 | `KugouListResult<KugouSong>` | `GetRankSongsTypedAsync()` |
| `/playlist/tags`、`/top/playlist`、`/playlist/add`、`/playlist/del`、`/playlist/tracks/add`、`/playlist/tracks/del` | 分类、收藏、增删参数 | 状态型响应 | 可保留 raw/status DTO | raw fallback |

### 专辑、歌手

| 路由 | 输入参数 | 输出提取 | 规范化输出 | C# 方法 |
| --- | --- | --- | --- | --- |
| `/album/detail` | `id` | 单对象/列表第一项 | `KugouTypedResult<KugouAlbum>` | `GetAlbumDetailTypedAsync()` |
| `/album/songs` | `id`、`page`、`pagesize` | 通用列表路径 | `KugouListResult<KugouSong>` | `GetAlbumSongsTypedAsync()` |
| `/artist/detail` | `id` | 单对象/列表第一项 | `KugouTypedResult<KugouArtist>` | `GetArtistDetailTypedAsync()` |
| `/artist/audios` | `id`、`page`、`pagesize`、`sort` | 通用列表路径 | `KugouListResult<KugouSong>` | `GetArtistSongsTypedAsync()` |
| `/artist/albums` | `id`、`page`、`pagesize`、`sort` | 通用列表路径 | `KugouListResult<KugouAlbum>` | `GetArtistAlbumsTypedAsync()` |
| `/artist/videos` | `id`、`page`、`pagesize`、`tag` | `data` 或通用列表路径 | `KugouListResult<KugouVideo>` | `GetArtistVideosTypedAsync()` |
| `/artist/follow`、`/artist/unfollow`、`/artist/lists` | `id` 或筛选条件 | 状态/列表 | 可继续补 DTO | raw fallback |

### 评论

| 路由 | 输入参数 | 输出提取 | 规范化输出 | C# 方法 |
| --- | --- | --- | --- | --- |
| `/comment/music` | `mixsongid`、`page`、`pagesize`、`show_classify`、`show_hotword_list`、`sort` | `data.list/comments`、热评路径 | `KugouListResult<KugouComment>`，`HotItems` 存热评/星评 | `GetMusicCommentsTypedAsync()` |
| `/comment/music/classify` | `mixsongid`、`type_id`、`page`、`pagesize`、`sort` | 同评论列表 | 可复用 `KugouComment` | raw fallback |
| `/comment/music/hotword` | `mixsongid`、`hot_word`、`page`、`pagesize`、`sort` | 同评论列表 | 可复用 `KugouComment` | raw fallback |
| `/comment/playlist` | `id`、`page`、`pagesize`、`show_classify`、`show_hotword_list` | 同评论列表 | `KugouListResult<KugouComment>` | `GetPlaylistCommentsTypedAsync()` |
| `/comment/album` | `id`、`page`、`pagesize`、`show_classify`、`show_hotword_list` | 同评论列表 | `KugouListResult<KugouComment>` | `GetAlbumCommentsTypedAsync()` |
| `/comment/floor` | `special_id`、`tid`、`mixsongid?`、`code?`、`resource_type?`、`page`、`pagesize` | `data.list` | `KugouListResult<KugouComment>` | `GetFloorCommentsTypedAsync()` |
| `/comment/count`、`/favorite/count` | `hash/special_id`、`mixsongids` | 数量字段 | count DTO 可后续补 | raw fallback |

### 用户、登录、视频

| 路由 | 输入参数 | 输出提取 | 规范化输出 | C# 方法 |
| --- | --- | --- | --- | --- |
| `/register/dev` | 无或设备信息 | `dfid/mid` 等 | 已由 CookieStore 消化 | `RegisterDeviceAsync()` raw |
| `/login/cellphone` | `mobile`、`code`、`userid?` | 登录用户字段、token、cookie | raw + CookieStore；可映射 `KugouUser` | `LoginByCellphoneAsync()` raw |
| `/user/detail` | 登录态 | `data/info/userinfo/profile` | `KugouTypedResult<KugouUser>` | `GetUserDetailTypedAsync()` |
| `/user/history` | `bp?` | 通用列表路径 | `KugouListResult<KugouSong>` | `GetUserHistoryTypedAsync()` |
| `/user/cloud` | `page`、`pagesize` | 通用列表路径 | `KugouListResult<KugouSong>` | `GetUserCloudTypedAsync()` |
| `/user/vip/detail`、`/youth/day/vip`、`/youth/day/vip/upgrade`、`/youth/month/vip/record` | 登录态/日期 | 状态/VIP 明细 | raw 或并入 `KugouUser.Vip` | raw fallback |
| `/video/detail` | `id` | `data[0]` | `KugouTypedResult<KugouVideo>` | `GetVideoDetailTypedAsync()` |
| `/video/url` | `hash` | `data[hash].downurl/url/play_url` | `KugouTypedResult<string>` | `GetVideoUrlTypedAsync()` |
| `/video/privilege` | `hash` | `data[].hash/info` | `KugouListResult<KugouVideoSource>` | `GetVideoPrivilegeTypedAsync()` |
| `/kmr/audio/mv` | `album_audio_id`、`fields` | `data[0][]` | `KugouVideo`/`KugouVideoSource`，可继续补专用方法 | raw fallback |

## 5. 已迁移到 C# 的文件

| 文件 | 作用 |
| --- | --- |
| `KugouTypedModels.cs` | EchoMusic 风格 DTO 与 `KugouTypedResult<T>`、`KugouListResult<T>` 包装 |
| `KugouJsonMapper.cs` | 通用路径提取、字段别名兼容、song/playlist/album/artist/rank/comment/user/video/audio URL 映射 |
| `KugouTypedEndpoints.cs` | 对 EchoMusic 高频接口提供强类型方法 |

## 6. 分层策略

1. **EchoMusic 强类型层**：覆盖 EchoMusic 高频接口，返回 `KugouSong`、`KugouPlaylist`、`KugouAlbum` 等稳定 DTO。
2. **全量 AOT DTO 层**：155 个 KuGouMusicApi 目录接口都有独立 `*ResponseDto`，并全部注册到 `KugouJsonSerializerContext`。
3. **AOT 调用入口**：`InvokeRouteDtoAsync()` 会用源生成上下文反序列化为 `KugouRouteDtoResult.Response`，避免业务层只能处理 `BodyText`。
4. **原始响应保留**：高频 typed result 仍带 `Raw`，用于调试；AOT 路径可以只消费 DTO。
5. **后续细化**：目前长尾接口的 `*ResponseDto` 继承通用 envelope，包含 `Status/Code/Message/Data/Info/List/Total/ExtensionData`；有真实样本后可继续把其中的 `Data` 细化成更具体字段。