# Second Hunt-CN｜密特罗德究极：猎人

[![Windows x64](https://img.shields.io/badge/Windows-x64-E66308?style=flat-square)](https://github.com/jyh9521/SecondHunt-CN/releases/tag/0.3.5-zh.8)
[![Release](https://img.shields.io/github/v/release/jyh9521/SecondHunt-CN?label=Release&style=flat-square&color=E66308)](https://github.com/jyh9521/SecondHunt-CN/releases)

[下载 Windows x64 ZIP](https://github.com/jyh9521/SecondHunt-CN/releases/download/0.3.5-zh.8/SecondHunt-CN-0.3.5-zh.8-windows-x64.zip) · [SHA-256 校验文件](https://github.com/jyh9521/SecondHunt-CN/releases/download/0.3.5-zh.8/SecondHunt-CN-0.3.5-zh.8-windows-x64.zip.sha256) · [版本说明](https://github.com/jyh9521/SecondHunt-CN/releases/tag/0.3.5-zh.8) · [原项目](https://github.com/BountyHunterKanden/SecondHunt)

这是 [Second Hunt](https://github.com/BountyHunterKanden/SecondHunt) 的 Windows 简体中文分支。Second Hunt 基于 [MphRead](https://github.com/NoneGiven/MphRead) 重建了 NDS《密特罗德究极：猎人》，不用模拟器就能玩原作剧情和多人模式，也支持宽屏、双摇杆手柄及键鼠。

**当前简中版为 `0.3.5-zh.8`。** Windows 汉化源码及构建文档在 [`zh-cn-windows` 分支](https://github.com/jyh9521/SecondHunt-CN/tree/zh-cn-windows)，默认分支的首页也同步这份说明；下载请使用上方本仓库的发布包。

**发布包不含游戏本体、ROM、解包资源、存档或测试数据。** 汉化在你提供的 ROM 解包后自动应用，不改写 ROM 或原版解包文件。此仓库不发布 Android 汉化包。

## 我想……

| 我想做什么 | 从哪里开始 |
| --- | --- |
| 第一次玩简中版 | 下载上方的 ZIP；[首次启动](#首次启动) |
| 升级旧版、保留存档 | [老用户升级与保留存档](#老用户升级与保留存档) |
| 核对下载、切回英文 | [校验和回退](#校验和回退) |
| 从源码构建、改译文或查术语 | [本地化与构建](https://github.com/jyh9521/SecondHunt-CN/blob/zh-cn-windows/docs/LOCALIZATION.zh-CN.md) · [术语表](https://github.com/jyh9521/SecondHunt-CN/blob/zh-cn-windows/docs/TERMINOLOGY.zh-CN.md) |
| 看贴图和过场是怎么处理的 | [图像与影片汉化](https://github.com/jyh9521/SecondHunt-CN/blob/zh-cn-windows/docs/MEDIA-LOCALIZATION.zh-CN.md) |

## 首次启动

1. 把整个 ZIP 解压到可写目录，例如 `D:\Games\Second Hunt`，运行 `SecondHunt.exe`。包已包含运行时，不要只单独取出 EXE。
2. 选择或拖入自己的美版《密特罗德究极：猎人》`.nds`（AMHE 1.0 或 1.1；已实测 1.0）。
3. **首次导入美版 ROM 后，程序会自动解包，再自动应用简体中文，最后进入中文游戏。** 不需要手动替换汉化文件，也不会改写原 ROM 或原版解包文件。
4. 解包结果保存在 `SecondHunt.exe` 同级的 `data\game`。以后启动会复用这份数据，正常情况下不必再次选择 ROM 或重新解包。

装在 `Program Files` 时，仅在需要创建 `data` 且权限不足时请求 UAC；游戏本体仍以普通权限运行。

**「导入游戏」不是用来启动其他《究极》作品的。** 它从你选择的其他作品文件中导入支持的装甲、角色或竞技场等内容。只玩《猎人》不需要导入任何额外资源。

## 老用户升级与保留存档

**先退出游戏，再备份旧版的整个 `data` 文件夹。** 备份放在旧安装目录之外，确认新版能读取原有进度前，不要删除旧目录或备份。

### 从旧便携版升级

1. 将 `0.3.5-zh.8` 的完整 ZIP 解压到一个新目录，不要混用旧版的 EXE 和 DLL。
2. **首次启动新版之前**，把旧版 `SecondHunt.exe` 旁的整个 `data` 文件夹复制到新版 EXE 旁。最终应为 `新版目录\data\saves`，而不是 `新版目录\data\data\saves`。不要用空的新 `data` 反向覆盖旧数据。
3. 运行新版。已有的有效美版解包数据会直接复用，程序会自动刷新汉化覆盖包；存档、设置、模组等仍从复制过来的 `data` 读取。
4. 检查原有存档和设置，再决定是否移除旧安装目录。要回退时，使用旧程序及升级前的数据备份。

| EXE 旁的路径 | 内容与保留方式 |
| --- | --- |
| `data\saves` | 剧情进度等存档，务必备份和保留 |
| `data\recomp_settings.json`、`data\window.json` | 游戏设置、窗口设置，随整个 `data` 一起复制 |
| `data\game` | 已解包 ROM 与汉化覆盖包，保留后无需重新导入 |
| `data\mods`、`data\hd`、`data\replays` | 已有模组、导入资源和回放，存在时一并保留 |
| `data\movies_seen.json` 等文件 | 影片观看记录等状态，复制整个 `data` 可一并保留 |

### 从旧版 LocalAppData 目录迁移

如果旧版数据位于 `%LOCALAPPDATA%\Second Hunt`，请先备份该目录，再启动新解压的版本。首次启动会将有效解包数据、存档、设置及可迁移资源复制到 EXE 旁的 `data`；**不覆盖目标中已有的对应文件或目录，也不删除旧目录**。若新版已创建同名数据，请先退出游戏并分别备份两份数据，再选择需要保留的一整份，避免混合覆盖。

如果一直使用 `--data <目录>` 指定数据位置，升级后继续指向原目录即可；更新前仍应备份该目录。简中版停用了上游自动更新，请从本仓库 Release 手动升级。

## 这一版改了什么

- 汉化启动器、设置及说明、剧情对话、扫描记录、地图、对战 HUD、扩展导入页和回放等界面。
- 片头猎人档案卡和标题贴图沿原字形处理：恢复字下底图再排中文，保留原画与光效，不用矩形色块盖住画面。
- 改善游戏内 HUD 与扫描文字的可读性，统一系统提示标点；补齐对战侧栏、地图大小和联机诊断等动态文案。
- 修复标题图中被截断的 ®，以及影片库点击后立即跳过的问题；影片缺失时显示中文提示。
- 译名优先使用任天堂官方简中，未找到官方译名的沿用[项目术语](https://github.com/jyh9521/SecondHunt-CN/blob/zh-cn-windows/docs/TERMINOLOGY.zh-CN.md)，例如「黑暗回声」「腐蚀」。厂商标识、署名、玩家输入及模组自己的文字保留原样。

**实测样本是美版 1.0（AMHE0）**：57 项检查通过，遍历 98 个菜单入口及 7 种已填值的对战规则；26 个影片条目可连续解码，首部影片完整播放 82.9 秒后正常返回。此前已跑过正式版开局和本地人机对战，并从检查点验证两个结局的过场与结算；同一条回放的 823 步全部校验一致。1.1、公网联机和需要另备游戏文件的导入没有逐一实测；更多细节见[验收与依赖说明](https://github.com/jyh9521/SecondHunt-CN/blob/zh-cn-windows/docs/LOCALIZATION.zh-CN.md#技术和验收边界)。

## 校验和回退

把 ZIP 和 `.zip.sha256` 放在同一目录，在 PowerShell 中运行下面的命令，确认两行的 SHA-256 一致：

```powershell
Get-FileHash -Algorithm SHA256 -LiteralPath '.\SecondHunt-CN-0.3.5-zh.8-windows-x64.zip'
Get-Content -LiteralPath '.\SecondHunt-CN-0.3.5-zh.8-windows-x64.zip.sha256'
```

要恢复英文，在 `SecondHunt.exe` 旁新建 `locale.txt`，内容写 `en`，然后重启；删除这个文件即恢复中文。切换语言不会清除存档。

## 开发与反馈

切换到 [`zh-cn-windows` 源码分支](https://github.com/jyh9521/SecondHunt-CN/tree/zh-cn-windows) 后，使用 .NET 9 SDK 构建，详见[复现构建](https://github.com/jyh9521/SecondHunt-CN/blob/zh-cn-windows/docs/LOCALIZATION.zh-CN.md#复现构建)。译文、字形与遮罩在该分支的 `locales/zh-CN`，可复用的提取和验证工具在 `tools/localization`。反馈时请附版本、ROM 修订号、出问题的页面或房间、复现步骤和截图或日志，不用附 ROM 或存档。

## 上游与许可

感谢 BountyHunterKanden 的 Second Hunt、NoneGiven 的 MphRead 及原项目贡献者。Windows 简中发行版的 `zh-cn-windows` 源码分支以 [Second Hunt 的 MIT 基线 `41452f2`](https://github.com/BountyHunterKanden/SecondHunt/tree/41452f2d053fcfa679e33c00a73d120a53f59e61) 为基础，未合并上游后续许可版本。该分支的源码及署名见 [LICENSE](https://github.com/jyh9521/SecondHunt-CN/blob/zh-cn-windows/LICENSE) 和 [NOTICE-fork](https://github.com/jyh9521/SecondHunt-CN/blob/zh-cn-windows/NOTICE-fork.md)；第三方组件和字体见 [第三方许可](https://github.com/jyh9521/SecondHunt-CN/blob/zh-cn-windows/THIRD_PARTY_NOTICES.md) 与 [Noto 字体许可](https://github.com/jyh9521/SecondHunt-CN/blob/zh-cn-windows/locales/zh-CN/OFL-Noto-CJK.txt)。本项目与任天堂无隶属关系。

<details>
<summary>原项目致谢（英文原文）</summary>

## Credits

Second Hunt is built on **MphRead** by NoneGiven (MIT, see `LICENSE` and `NOTICE-fork.md`). Third-party libraries and
their licenses are listed in `THIRD_PARTY_NOTICES.md`, which the app also shows under SECOND HUNT SETTINGS > credits.

From MphRead's README:

### Acknowledgements

A significant portion of this project's code was based on the file format information or source code from several other projects.

- **dsgraph** - The original MPH model viewer, on which all other projects are built.
- **[Chemical's model format](https://gitlab.com/ch-mcl/metroid-prime-hunters-file-document/-/blob/master/Model/BinModel.md)** - Documentation of the model format.
- **[McKay42's mph-model-viewer](https://github.com/McKay42/mph-model-viewer)** - COLLADA export method.
- **[McKay42's mph-arc-extractor](https://github.com/McKay42/mph-arc-extractor)** - ARC file format information.
- **[Barubary's dsdecmp](https://github.com/Barubary/dsdecmp)** - LZ10 compression routines.
- **[loveemu's swav2wav](https://github.com/loveemu/loveemu-lab)** - SWAV conversion function.
- **[Gericom's ffmpeg patch](https://lists.ffmpeg.org/pipermail/ffmpeg-devel/2021-March/277774.html)** - ActImagine VX movie file format information.
- **[CharlesVanEeckhout's actimagine decoder](https://github.com/CharlesVanEeckhout/actimagine)** - Further understanding of VX video decoding, based on the above ffmpeg patch.
- **[CyberBotX's NCSF](https://github.com/CyberBotX/NCSF)** - Source code for the NCSF converter and player for Nintendo DS sequenced music.

(Note from Second Hunt: MphRead's VX movie decoder, and the sequencer in the NCSF-based music player, have since been
replaced with clean-room code that gives the same pictures and sound. See [docs/cleanroom](docs/cleanroom).)

### Special Thanks

This project's reverse engineering effort was developed parallel to **[hackyourlife's mph-viewer](https://github.com/hackyourlife/mph-viewer)**, a model viewer implementation in C. Major features such as the transparency rendering implementation were derived from its source code.

(Note from Second Hunt: that transparency rendering code has since been replaced with a clean-room version that draws
the same result. See [docs/cleanroom](docs/cleanroom).)

### Notice
> **AI tools were used in the making of this project.**

</details>
