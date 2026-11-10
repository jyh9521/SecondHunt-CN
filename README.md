# Second Hunt-CN｜密特罗德究极：猎人

[![Windows x64](https://img.shields.io/badge/Windows-x64-E66308?style=flat-square)](https://github.com/jyh9521/SecondHunt-CN/releases/tag/0.3.5-zh.5)
[![Release](https://img.shields.io/github/v/release/jyh9521/SecondHunt-CN?label=Release&style=flat-square&color=E66308)](https://github.com/jyh9521/SecondHunt-CN/releases)

[下载 Windows x64 ZIP](https://github.com/jyh9521/SecondHunt-CN/releases/download/0.3.5-zh.5/SecondHunt-CN-0.3.5-zh.5-windows-x64.zip) · [SHA-256 校验文件](https://github.com/jyh9521/SecondHunt-CN/releases/download/0.3.5-zh.5/SecondHunt-CN-0.3.5-zh.5-windows-x64.zip.sha256) · [版本说明](https://github.com/jyh9521/SecondHunt-CN/releases/tag/0.3.5-zh.5) · [原项目](https://github.com/BountyHunterKanden/SecondHunt)

这是 [Second Hunt](https://github.com/BountyHunterKanden/SecondHunt) 的 Windows 简体中文分支。Second Hunt 基于 [MphRead](https://github.com/NoneGiven/MphRead) 重建了 NDS《密特罗德究极：猎人》，不用模拟器就能玩原作剧情和多人模式，也支持宽屏、双摇杆手柄及键鼠。

**发布包不含游戏本体、ROM、解包资源、存档或测试数据。** 汉化在你提供的 ROM 解包后自动应用，不改写 ROM 或原版解包文件。此仓库不发布 Android 汉化包。

## 我想……

| 我想做什么 | 从哪里开始 |
| --- | --- |
| 玩简中版 | 下载上方的 ZIP；[首次启动和迁移](#首次启动和迁移) |
| 核对下载、切回英文 | [校验和回退](#校验和回退) |
| 从源码构建、改译文或查术语 | [本地化与构建](docs/LOCALIZATION.zh-CN.md) · [术语表](docs/TERMINOLOGY.zh-CN.md) |
| 看贴图和过场是怎么处理的 | [图像与影片汉化](docs/MEDIA-LOCALIZATION.zh-CN.md) |

## 首次启动和迁移

1. 把整个 ZIP 解压到可写目录，例如 `D:\Games`，运行 `SecondHunt.exe`。包已包含运行时。
2. 选择或拖入自己的美版《密特罗德究极：猎人》`.nds`（AMHE 1.0 或 1.1）。等解包完成，程序会自动加载简体中文。
3. 解包文件、存档、设置和模组保存在 EXE 同级的 `data`。更新旧便携版时，先保留备份，再把旧版的 `data` 整个复制到新目录。旧上游版若使用 `%LOCALAPPDATA%\Second Hunt`，首次启动会复制可迁移的数据，不覆盖新目录里已有文件，也不删除旧目录。

装在 `Program Files` 时，仅在需要创建 `data` 且权限不足时请求 UAC；游戏本体仍以普通权限运行。

**「导入游戏」不是用来启动其他《究极》作品的。** 它从你选择的其他作品文件中导入支持的装甲、角色或竞技场等内容。只玩《猎人》不需要导入任何额外资源。

## 这一版改了什么

- 汉化启动器、设置及说明、剧情对话、扫描记录、地图、对战 HUD、扩展导入页和回放等界面。
- 片头猎人档案卡和标题贴图沿原字形处理：恢复字下底图再排中文，保留原画与光效，不用矩形色块盖住画面。
- 译名优先使用任天堂官方简中，未找到官方译名的沿用[项目术语](docs/TERMINOLOGY.zh-CN.md)，例如「黑暗回声」「腐蚀」。厂商标识、署名、玩家输入及模组自己的文字保留原样。

**实测样本是美版 1.0（AMHE0）**：54 项检查通过，遍历 98 个菜单入口，跑过正式版开局和本地人机对战，并从检查点验证了两个结局的过场与结算。同一条回放的 823 步全部校验一致。1.1、公网联机和需要另备游戏文件的导入没有逐一实测；更多细节见[验收与依赖说明](docs/LOCALIZATION.zh-CN.md#技术和验收边界)。

## 校验和回退

把 ZIP 和 `.zip.sha256` 放在同一目录，在 PowerShell 中运行下面的命令，确认两行的 SHA-256 一致：

```powershell
Get-FileHash -Algorithm SHA256 -LiteralPath '.\SecondHunt-CN-0.3.5-zh.5-windows-x64.zip'
Get-Content -LiteralPath '.\SecondHunt-CN-0.3.5-zh.5-windows-x64.zip.sha256'
```

要恢复英文，在 `SecondHunt.exe` 旁新建 `locale.txt`，内容写 `en`，然后重启；删除这个文件即恢复中文。切换语言不会清除存档。

## 开发与反馈

Windows 构建使用 .NET 9 SDK，详见[复现构建](docs/LOCALIZATION.zh-CN.md#复现构建)。译文、字形与遮罩在 `locales/zh-CN`，可复用的提取和验证工具在 `tools/localization`。反馈时请附版本、ROM 修订号、出问题的页面或房间、复现步骤和截图或日志，不用附 ROM 或存档。

## 上游与许可

感谢 BountyHunterKanden 的 Second Hunt、NoneGiven 的 MphRead 及原项目贡献者。本分支以 [Second Hunt 的 MIT 基线 `41452f2`](https://github.com/BountyHunterKanden/SecondHunt/tree/41452f2d053fcfa679e33c00a73d120a53f59e61) 为基础，未合并上游后续许可版本。源码及署名见 [LICENSE](LICENSE) 和 [NOTICE-fork](NOTICE-fork.md)；第三方组件和字体见 [第三方许可](THIRD_PARTY_NOTICES.md) 与 [Noto 字体许可](locales/zh-CN/OFL-Noto-CJK.txt)。本项目与任天堂无隶属关系。

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
