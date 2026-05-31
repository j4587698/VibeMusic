# 酷狗 API 参数参考

说明：本表从原项目 docs/README.md 自动抽取。原文主要提供输入参数，没有稳定的输出字段表；输出仍以酷狗上游 JSON 为准。

- 原源码模块数：155
- 原文档有参数说明的接口地址数：144
- 源码存在但原文档未写参数表/未写接口地址的路由数：15

## 源码存在但文档未覆盖的路由

- `/album/shop`
- `/artist/honour`
- `/everyday/friend`
- `/ip/dateil`
- `/login/device`
- `/login/device/kick`
- `/recommend/songs`
- `/scene/lists/v2`
- `/scene/music`
- `/search/mixed`
- `/sheet/collection/detail`
- `/singer/list`
- `/top/card/youth`
- `/youth/dynamic`
- `/youth/listen/song`

## 输入参数表

| API | 标题 | 必选参数 | 可选参数 |
|---|---|---|---|
| `/login/cellphone` | 1.手机登录 | `mobile`：手机号码<br>`code`：验证码，使用 [`/captcha/sent`](#发送验证码)接口传入手机号获取验证码,调用此接口传入验证码,可使用验证码登录 | `userid`：用户 id,当用户存在多个账户是时，必须加上需要登录的用户 id |
| `/login` | 2. 用户名登录(该登录可能需要验证，不推荐使用) | `username`：用户名<br>`password`：密码 | — |
| `/login/openplat` | 3. 开放接口登录(目前仅支持微信登录) | `code`：由微信扫码成功后生成 | — |
| `/login/qr/key` | 1.二维码 key 生成接口 | — | — |
| `/login/qr/create` | 2.二维码生成接口 | `key`：,由第一个接口生成 | `qrimg`：传入后会额外返回二维码图片 base64 编码 |
| `/login/qr/check` | 2.二维码检测扫码状态接口 | `key`：,由第一个接口生成 | — |
| `/login/wx/create` | 1. 二维码生成接口 | — | — |
| `/login/wx/check` | 2.二维码检测扫码状态接口 | `uuid`：由第一个接口生成 | `timestamp`：建议传递，否则由于缓存会导致延迟 |
| `/login/token` | 刷新登录 | — | `token`：登录后获取的 token<br>`userid`：用户 id |
| `/captcha/sent` | 发送验证码 | `mobile`：手机号码 | — |
| `/register/dev` | dfid 获取 | — | — |
| `/user/detail` | 获取用户额外信息 | — | — |
| `/user/vip/detail` | 获取用户 vip 信息 | — | — |
| `/user/playlist` | 获取用户歌单 | — | `page`：：页数<br>`pagesize`：每页页数, 默认为 30 |
| `/user/follow` | 获取用户关注歌手 | — | — |
| `/user/follow/message` | 获取关注歌手消息 | — | — |
| `/user/cloud` | 获取用户云盘 | — | `page`：页数<br>`pagesize`：每页页数, 默认为 30 |
| `/user/cloud/url` | 获取用户云盘音乐 URL | `hash`：音乐 hash | `album_id`：专辑 id<br>`name`：云盘音乐名称<br>`album_audio_id`：：专辑音频 id |
| `/user/video/collect` | 获取用户收藏的视频 | — | `page`：页数<br>`pagesize`：每页页数, 默认为 30 |
| `/user/video/love` | 获取用户喜欢的视频 | — | `pagesize`：每页页数, 默认为 30 |
| `/user/listen` | 获取用户听歌历史排行 | — | `type`：：0 为获取最近一周前 120 首歌曲，1：获取全部累计前 120 首歌曲 |
| `/user/history` | 获取用户最近听歌历史 | — | `bp`：可以更加上一次返回值传入 |
| `/lastest/songs/listen` | 获取继续播放信息（对应手机版首页显示继续播放入口） | — | `pagesize`：每页页数, 默认为 30 |
| `/playlist/add` | 收藏歌单/新建歌单 | `name`：歌单名称<br>`list_create_userid`：歌单 list_create_userid<br>`list_create_listid`：歌单 list_create_listid | `is_pri`：是否设为隐私，0：公开，1：隐私，仅支持创建歌单时传入<br>`type`：1：为收藏歌单，0：创建歌单, 默认为 0<br>`list_create_gid`：：歌单 list_create_gid |
| `/playlist/del` | 取消收藏歌单/删除歌单 | `listid`：用户歌单 listid | — |
| `/playlist/del?listid=xxx` | 取消收藏歌单/删除歌单 | `listid`：用户歌单 listid | — |
| `/playlist/tracks/add` | 对歌单添加歌曲 | `listid`：用户歌单 listid<br>`data`：歌曲数据, 格式为 歌曲名称\|歌曲 hash\|专辑 id\|(mixsongid/album_audio_id)，最少需要 歌曲名称以及歌曲 hash(若返回错误则需要全部参数)， 支持多个，每 | — |
| `/playlist/tracks/del` | 对歌单删除歌曲 | `listid`：用户歌单 listid<br>`fileids`：歌单中歌曲的 fileid，可多个,用逗号隔开 | — |
| `/top/album` | 新碟上架 | — | `type`：1：华语；2：欧美；3：日本；4：韩国；推荐为空，默认为空<br>`page`：页数<br>`pagesize`：每页页数, 默认为 30 |
| `/album` | 专辑信息 | `album_id`：专辑 id,可以传多个，以逗号分割 | `fields`：需要返回的信息，可以传多个，以逗号分割，支持的值有 `trans_param` `special_tag` `authors` `album_name` `publish_date` `cover` `intro`<br>`publish_company`：`type` `album_id` `language_id` `is_publish` `heat` `grade` `quality` `exclusive` `grade_count` `author_name` `sizable_cover`<br>`language`：`category` |
| `/album/detail` | 专辑详情 | `id`：专辑 id | — |
| `/album/songs` | 专辑音乐列表 | `id`：专辑 id | `page`：页数<br>`pagesize`：每页页数, 默认为 30 |
| `/song/url` | 获取音乐 URL | `hash`：音乐 hash | `album_id`：专辑 id<br>`free_part`：是否返回试听部分（仅部分歌曲）<br>`album_audio_id`：：专辑音频 id<br>`quality`：：获取不同音质的 url |
| `/song/url/new` | 获取音乐 URL（新版） | `hash`：音乐 hash | `album_audio_id`：：专辑音频 id<br>`free_part`：是否返回试听部分（仅部分歌曲）<br>`album_audio_id`：：专辑音频 id |
| `/song/climax` | 获取歌曲高潮部分 | `hash`：音乐 hash, 可以传多个，以逗号分割 | — |
| `/search` | 搜索 | `keywords`：关键词 | `page`：页数<br>`pagesize`：每页页数, 默认为 30<br>`type`：搜索类型；默认为单曲，special：歌单，lyric：歌词，song：单曲，album：专辑，author：歌手，mv：mv |
| `/search/default` | 默认搜索关键词 | — | — |
| `/search/complex` | 综合搜索 | `keywords`：关键词 | `page`：页数<br>`pagesize`：每页页数, 默认为 30 |
| `/search/hot` | 热搜列表 | — | — |
| `/search/suggest` | 搜索建议 | — | `albumTipCount`：专辑返回数量<br>`correctTipCount`：目前未知，可能是歌单<br>`mvTipCount`：MV 返回数量<br>`musicTipCount`：音乐返回数量 |
| `/search/lyric` | 歌词搜索 | `keywords`：关键词，与 hash 二选一<br>`hash`：歌曲 hash，与 keyword 二选一 | `album_audio_id`：专辑音乐 id,<br>`man`：是否返回多个歌词，`yes`：返回多个， `no`：返回一个。 默认为`no` |
| `/lyric` | 获取歌词 | `id`：歌词 id, 可以从 [`/search/lyric`](#歌词搜搜) 接口中获取<br>`accesskey`：歌词 accesskey, 可以从 [`/search/lyric`](#歌词搜搜) 接口中获取 | `fmt`：歌词类型，lrc 为普通歌词，krc 为逐字歌词<br>`decode`：是否解码，传入该参数这返回解码后的歌词 |
| `/playlist/tags` | 歌单分类 | — | — |
| `/top/playlist` | 歌单 | `category_id`：tag，0：推荐，11292：HI-RES，其他可以从 [`/playlist/tags`](#歌单分类) 接口中获取（接口下的 `tag_id` 为 `category_id`的值） | `withsong`：是否返回歌曲列表（不全），0：不返回，1：返回<br>`withtag`：是否返回歌单分类，0：不返回，1：返回 |
| `/theme/playlist` | 主题歌单 | — | — |
| `/playlist/effect` | 音效歌单 | — | `page`：页数<br>`pagesize`：每页页数, 默认为 30 |
| `/playlist/detail` | 获取歌单详情 | `ids`：歌单中的 `global_collection_id`，可以传多个，用逗号分隔 | — |
| `/playlist/track/all` | 获取歌单所有歌曲 | `id`：歌单中的 `global_collection_id` | `page`：页数<br>`pagesize`：每页页数, 默认为 30 |
| `/playlist/track/all/new` | 获取歌单所有歌曲(新版) | `lisdid`：歌单中的 `listid` | `page`：页数<br>`pagesize`：每页页数, 默认为 30 |
| `/playlist/similar` | 相似歌单 | `ids`：：歌单 global_collection_id，支持多个，每个以逗号分隔 | — |
| `/theme/playlist/track` | 获取主题歌单所有歌曲 | `theme_id`：主题歌单 id | — |
| `/theme/music` | 获取主题音乐 | — | — |
| `/theme/music/detail` | 获取主题音乐详情 | `id`：主题音乐 id | — |
| `/top/card` | 歌曲推荐 | `card_id`：1：对应安卓 精选好歌随心听 \|\| 私人专属好歌，2：对应安卓 经典怀旧金曲，3：对应安卓 热门好歌精选，4：对应安卓 小众宝藏佳作，5：未知，6：对应 | — |
| `/top/card` | 歌曲推荐（概念版） | `card_id`：3006: VIP 专属推荐，3001: 私人专属好歌，3004: 小众宝藏佳作，3014: 喜欢这首歌的 TA 也喜欢，3101: 概念 er 新推，3005: 潮流尝鲜 | `pagesize`：每页页数, 默认为 30 |
| `/images` | 获取歌手和专辑图片 | `hash`：歌曲 hash, 可以传多个，每个以逗号分开 | `album_id`：专辑 id, 可以传多个，每个以逗号分开<br>`album_audio_id`：专辑音乐 id, 可以传多个，每个以逗号分开<br>`count`：最多返回多少张图片，默认为 5 |
| `/images/audio` | 获取歌手图片 | `hash`：歌曲 hash, 可以传多个，每个以逗号分开 | `audio_id`：音乐 id, 可以传多个，每个以逗号分开<br>`album_audio_id`：专辑音乐 id, 可以传多个，每个以逗号分开<br>`filename`：音乐文件名称, 可以传多个，每个以逗号分开<br>`count`：最多返回多少张图片，默认为 5 |
| `/audio` | 获取音乐相关信息 | `hash`：歌曲 hash, 可以传多个，每个以逗号分开 | — |
| `/audio/related` | 获取更多音乐版本 | `album_audio_id`：：音乐的 mixsongid/album_audio_id | `page`：： 页码<br>`pagesize`：每页页数, 默认为 30<br>`show_type`：：是否返回分类<br>`sort`：：排序，支持 `all`，`hot`，`new`<br>`type`：分类<br>`show_detail`：：是否返回详情，否则只返回总数，0：只返回总数，不传或者其他都返回详情 |
| `/audio/accompany/matching` | 获取音乐伴奏信息 | `hash`：：音乐 hash<br>`fileName`：音乐 fileName<br>`mixid`：音乐的 mixsongid/album_audio_id | — |
| `/audio/ktv/total` | 获取音乐 K 歌数量 | `songId`：：音乐 songid, 该字段需要请求 [获取音乐伴奏信息](#获取音乐伴奏信息) 获取<br>`singerName`：：歌手名称，多个以 `、` 隔开，也可以到 [获取音乐伴奏信息](#获取音乐伴奏信息) 中获取<br>`songHash`：：音乐 hash, 该字段需要请求 [获取音乐伴奏信息](#获取音乐伴奏信息) 获取 | — |
| `/privilege/lite` | 获取音乐详情 | `hash`：歌曲 hash, 可以传多个，每个以逗号分开 | — |
| `/krm/audio` | 获取音乐专辑/歌手信息 | `album_audio_id`：专辑音乐 id (album_audio_id/MixSongID 均可以), 可以传多个，每个以逗号分开 | `fields`：可以传 `album_info` `authors.base` `base` `audio_info`, `authors.ip`, `extra`, `tags`, `tagmap` 每个 field 以逗号分开 |
| `/personal/fm` | 私人 FM(对应手机和 pc 端的猜你喜欢) | — | `hash`：音乐 hash, 建议<br>`songid`：音乐 songid, 建议<br>`playtime`：已播放时间, 建议<br>`mode`：获取模式，默认为 normal, normal：发现，small： 小众，peak：30s<br>`action`：默认为 play, garbage: 为不喜欢<br>`song_pool_id`：： 手机版的 AI，0：Alpha 根据口味推荐相似歌曲, 1：Beta 根据风格推荐相似歌曲, 2：Gamma<br>`is_overplay`：是否已播放完成<br>`remain_songcnt`：剩余未播放歌曲数, 默认为 0，大于 4 不返回推荐歌曲，建议 |
| `/pc/diantai` | banner | — | — |
| `/yueku/banner` | 乐库 banner | — | — |
| `/yueku/fm` | 乐库电台 | — | — |
| `/yueku` | 乐库 | — | — |
| `/fm/class` | 电台 | — | — |
| `/fm/recommend` | 电台 - 推荐 | — | — |
| `/fm/image` | 电台 - 图片 | `fmid`：fmid，可以传多个，以逗号分割 | — |
| `/fm/songs` | 电台 - 音乐列表 | `fmid`：fmid，可以传多个，以逗号分割 | `fmtype`：fmtype, 可以传多个，以逗号分割<br>`fmoffset`：歌曲偏移，可以传多个，以逗号分割<br>`fmsize`：歌曲列表大小，可以传多个，以逗号分割 |
| `/top/ip` | 编辑精选 | — | — |
| `/ip` | 编辑精选数据 | `id`：ip id | `type`：数据类型，audios: 音乐, albums: 专辑, videos: 视频, author_list: 歌手<br>`page`：： 页码<br>`pagesize`：每页页数, 默认为 30 |
| `/ip/playlist` | 编辑精选歌单 | `id`：ip id | `page`：： 页码<br>`pagesize`：每页页数, 默认为 30 |
| `/ip/zone` | 编辑精选专区 | — | — |
| `/ip/zone/home` | 编辑精选专区详情 | `id`：ip id | — |
| `/youth/vip` | 领取 VIP（需要登陆，该接口为测试接口,仅限概念版使用，该接口目前不可使用） | — | — |
| `/youth/day/vip` | 领取一天 VIP（需要登陆，该接口为测试接口,仅限概念版使用） | `receive_day`：领取 VIP 日期，格式为：2026-01-30 | — |
| `/youth/day/vip/upgrade` | 升级概念版 VIP（需要登录，需要先领取一天 VIP，该接口为测试接口,仅限概念版使用） | — | — |
| `/youth/month/vip/record` | 获取当月已领取 VIP 天数（需要登陆，该接口为测试接口,仅限概念版使用） | — | — |
| `/youth/union/vip` | 获取已领取 VIP 状态（需要登陆，该接口为测试接口,仅限概念版使用） | — | — |
| `/artist/lists` | 获取歌手列表 | — | `sextypes`：：性别类型，0：全部，1：男，2：女，3：组合<br>`type`：：类型，0：全部，1：华语，2：欧美，3：日韩，4：其他，5：日本，6：韩国<br>`musician`：：音乐人，3：为音乐人,0：默认<br>`hotsize`：：返回热门数量，默认 30 |
| `/artist/detail` | 获取歌手详情 | `id`：： 歌手 id | — |
| `/artist/albums` | 获取歌手专辑 | `id`：： 歌手 id | `page`：： 页码<br>`pagesize`：每页页数, 默认为 30<br>`sort`：排序，hot : 热门, new: 最新 |
| `/artist/audios` | 获取歌手单曲 | `id`：： 歌手 id | `page`：： 页码<br>`pagesize`：每页页数, 默认为 30<br>`sort`：排序，hot : 热门, new: 最新 |
| `/artist/videos` | 获取歌手 MV | `id`：： 歌手 id | `page`：： 页码<br>`pagesize`：每页页数, 默认为 30<br>`tag`：official: 官方版本，live：现场版本，fan：饭制版本，artist: 歌手发布, all: 获取全部，默认为获取全部 |
| `/artist/follow` | 关注歌手 | `id`：歌手 id | — |
| `/artist/unfollow` | 取消关注歌手 | `id`：歌手 id | — |
| `/artist/follow/newsongs` | 获取关注歌手新歌 | — | `last_album_id`：最后专辑 id<br>`pagesize`：每页页数, 默认为 30,<br>`opt_sort`：排序，1：时间，2：亲密度，默认为 1(时间) |
| `/video/url` | 获取视频 url | `hash`：视频 hash | — |
| `/kmr/audio/mv` | 获取歌曲 MV | `album_audio_id`：专辑音乐 id (album_audio_id/MixSongID 均可以), 可以传多个，每个以逗号分开, | `fields`：支持多个，每个以逗号分隔，支持的值有：mkv,tags,h264,h265,authors |
| `/video/privilege` | 获取视频相关信息 | `hash`：视频 hash，可以传多个，以逗号隔开 | — |
| `/video/detail` | 获取视频详情 | `id`：视频 id/video id | — |
| `/top/song` | 新歌速递 | — | — |
| `/scene/lists` | 场景音乐列表 | — | — |
| `/scene/module` | 场景音乐详情 | `id`：场景音乐 scene_id | — |
| `/scene/list/v2` | 获取场景音乐讨论区 | `id`：场景音乐 scene_id | `page`：： 页码<br>`pagesize`：每页页数, 默认为 30<br>`sort`：排序，rec: 推荐，hot: 热门，new: 最新, 默认为推荐 |
| `/scene/module/info` | 获取场景音乐模块 Tag | `id`：场景音乐 scene_id<br>`module_id`：场景音乐 module_id | — |
| `/scene/collection/list` | 获取场景音乐歌单列表 | `tag_id`：场景音乐 tag_id | `page`：： 页码<br>`pagesize`：每页页数, 默认为 30 |
| `/scene/video/list` | 获取场景音乐视频列表 | `tag_id`：场景音乐视频 tag_id | `page`：： 页码<br>`pagesize`：每页页数, 默认为 30 |
| `/scene/audio/list` | 获取场景音乐音乐列表 | `id`：场景音乐 scene_id<br>`module_id`：场景音乐 module_id<br>`tag`：场景音乐 tag_id | `page`：： 页码<br>`pagesize`：每页页数, 默认为 30 |
| `/everyday/recommend` | 每日推荐 | — | `platform`：：设备类型，默认为 ios,支持 android 和 ios |
| `/everyday/history` | 历史推荐 | `platform`：：设备类型，默认为 ios,支持 android 和 ios | `mode`：：当 mode 为 list 时，则返回历史推荐列表，当 mode 为 song 时则返回当前歌曲列表，支持参数为：list 和 song, |
| `/everyday/style/recommend` | 风格推荐 | — | `platform`：：设备类型，默认为 ios,支持 android 和 ios<br>`tagids`：：支持多个，每个以逗号分隔，该接口下可获取 tag 信息 |
| `/rank/list` | 排行列表 | — | `withsong`：：是否返回歌曲（部分） |
| `/rank/top` | 排行榜推荐列表 | — | — |
| `/rank/vol` | 排行榜往期列表 | `rankid`：：排行榜 id | `rank_cid`：：排行榜 cid |
| `/rank/info` | 排行榜信息 | `rankid`：：排行榜 id | `rank_cid`：：排行榜 cid<br>`album_img`：：是否返回专辑图片，1：返回，0：不返回，默认返回<br>`zone`：：排行榜 zone |
| `/rank/audio` | 排行榜歌曲列表 | `rankid`：：排行榜 id | `rank_cid`：：若需要返回往期歌曲列表，则该参数为必填，否则默认返回最新一期，[`/rank/vol`](#排行榜往期列表) 返回值中，`volid` 则为该参数<br>`page`：： 页码<br>`pagesize`：每页页数, 默认为 30 |
| `/favorite/count` | 歌曲收藏数 | `mixsongids`：：音乐 mixsongid，多个以逗号分隔 | — |
| `/comment/count` | 歌曲评论数 | `hash`：：音乐 hash<br>`special_id`：：为 评论下的 special_child_id 字段 | — |
| `/comment/music` | 歌曲评论 | `mixsongid`：：音乐 mixsongid | `page`：： 页码<br>`pagesize`：每页页数, 默认为 30<br>`show_classify`：： 是否返回分类列表，0 为不返回，1 为返回<br>`show_hotword_list`：：是否返回热词，0 为不返回，1 为返回 |
| `/comment/music/classify` | 歌曲评论-根据分类返回 | `mixsongid`：：音乐 mixsongid<br>`type_id`：：分类 id | `page`：： 页码<br>`pagesize`：每页页数, 默认为 30<br>`sort`：：排序，1 为正序，2 为倒序 |
| `/comment/music/hotword` | 歌曲评论-根据热词返回 | `mixsongid`：：音乐 mixsongid<br>`hot_word`：：热词 | `page`：： 页码<br>`pagesize`：每页页数, 默认为 30 |
| `/comment/floor` | 楼层评论 | `special_id`：：为 评论下的 special_child_id 字段<br>`mixsongid`：：为 歌曲的 mixsongid<br>`tid`：：评论 id | `page`：： 页码<br>`pagesize`：每页页数, 默认为 30 |
| `/comment/playlist` | 歌单评论 | `id`：：歌单 global_collection_id | `page`：： 页码<br>`pagesize`：每页页数, 默认为 30<br>`show_classify`：： 是否返回分类列表，0 为不返回，1 为返回<br>`show_hotword_list`：：是否返回热词，0 为不返回，1 为返回 |
| `/comment/album` | 专辑评论 | — | `page`：： 页码<br>`pagesize`：每页页数, 默认为 30<br>`show_classify`：： 是否返回分类列表，0 为不返回，1 为返回<br>`show_hotword_list`：：是否返回热词，0 为不返回，1 为返回 |
| `/sheet/list` | 歌曲曲谱 | `album_audio_id`：：音乐的 mixsongid/album_audio_id | `opern_type`：：曲谱类型，0：全部，1：钢琴，2：吉他，3：鼓，98：简谱，99：其他<br>`page`：： 页码<br>`pagesize`：每页页数, 默认为 30 |
| `/sheet/detail` | 曲谱详情 | `id`：：曲谱 id,<br>`source`：：曲谱 source, | — |
| `/sheet/hot` | 推荐曲谱 | — | `opern_type`：：曲谱类型，1：钢琴，2：吉他，3：鼓，98：简谱，99：其他 |
| `/sheet/collection` | 曲谱合集 | — | `position`：：2：精选谱单，3：音乐教材，4：古典钢琴 |
| `/sheet/collection` | 曲谱合集详情 | — | `collection_id`：：合集 id<br>`page`：： 页码 |
| `/playhistory/upload` | 提交听歌历史 | `mxid`：： 专辑音乐 id (album_audio_id/MixSongID 均可以) | `ot`：：当前时间戳, 秒级，不要传入毫秒级，否者会返回错误，或者从 [`获取服务器时间`](#获取服务器时间) 中获取<br>`pc`：当前播放次数，更新播放次数，当服务器的值大于传入值时，将维持服务最大值，否则更新 |
| `/server/now` | 获取服务器时间 | — | — |
| `/brush` | 刷刷 | — | — |
| `/ai/recommend` | AI 推荐 | `album_audio_id`：： 专辑音乐 id (album_audio_id/MixSongID 均可以), 可以传多个，每个以逗号分开, | — |
| `/youth/channel/all` | 频道 - 获取用户所有频道 | — | `page`：：页数<br>`pagesize`：每页页数, 默认为 30 |
| `/youth/channel/detail` | 频道 - 详情 | `global_collection_id`：：频道 id (global_collection_id / channel_id 均可以), 可以传多个，每个以逗号分开, | — |
| `/youth/channel/amway` | 频道 - 频道安利 | `global_collection_id`：：频道 id (global_collection_id / channel_id 均可以) | — |
| `/youth/channel/similar` | 频道 - 相似频道 | `channel_id`：：频道 id (global_collection_id / channel_id 均可以) | — |
| `/youth/channel/sub` | 频道 - 订阅 | `global_collection_id`：：频道 id (global_collection_id / channel_id 均可以) | `t`：：1 为订阅，0 为取消订阅，不传默认为订阅 |
| `/youth/channel/song` | 频道 - 音乐故事 | `global_collection_id`：：频道 id (global_collection_id / channel_id 均可以) | `page`：：页数<br>`pagesize`：每页页数, 默认为 30 |
| `/youth/channel/song/detail` | 频道 - 音乐故事详情 | `global_collection_id`：：频道 id (global_collection_id / channel_id 均可以)<br>`fileid`：音乐故事 fileid | — |
| `/youth/dynamic/recent` | 动态 - 最常访问 | — | — |
| `/youth/user/song` | 获取用户公开的音乐 | `userid`：：用户 id | `page`：：页数<br>`pagesize`：每页页数, 默认为 30 |
| `/longaudio/daily/recommend` | 听书 - 每日推荐 | — | `page`：：页数<br>`pagesize`：每页页数, 默认为 30 |
| `/longaudio/rank/recommend` | 听书 - 排行榜推荐 | — | — |
| `/longaudio/vip/recommend` | 听书 - VIP 推荐 | — | — |
| `/longaudio/week/recommend` | 听书 - 每周推荐 | — | — |
| `/longaudio/album/detail` | 听书 - 专辑详情 | `album_id`：专辑 id 可以传多个，每个以逗号分开, | — |
| `/longaudio/album/audios` | 听书 - 专辑音乐列表 | `album_id`：专辑 id 可以传多个 | — |
| `/song/ranking` | 歌曲详情 - 歌曲成绩单 | `album_audio_id`：： 专辑音乐 id (album_audio_id/MixSongID 均可以), | — |
| `/song/ranking/filter` | 歌曲详情 - 歌曲成绩单详情 | `album_audio_id`：： 专辑音乐 id (album_audio_id/MixSongID 均可以), | `page`：：页数<br>`pagesize`：每页页数, 默认为 30 |
