# Second Hunt Windows 简体中文预览版

中文游戏名为《密特罗德究极：猎人》。术语统一为 **Metroid → 密特罗德**、
**Prime → 究极**（如「究极猎人」「究极倍率」）。`Ultimate Power` 是另一个词，
译为「终极力量」。程序名、文件名、游戏代码和联机字段不随翻译改动。

## 使用

- 解压 Windows x64 ZIP 到可写位置，启动 `SecondHunt.exe`，选择自己的美版
  `.nds`（AMHE 1.0 或 1.1）。包内不含 ROM、原版贴图、音视频或解包内容。
- 解包完成后自动应用简体中文包。默认路径是 `SecondHunt.exe` 旁的 `data`，
  解包数据在 `data/game`，存档在 `data/saves`。整个目录一起搬运即可；
  `--data <dir>` 是显式覆盖。
- `%LOCALAPPDATA%\Second Hunt` 如有旧数据，首次启动会复制有效的 `game` 和
  新目录中不存在的存档、设置、模组等。不会覆盖目标或删除源；不迁移临时缓存、
  日志或崩溃报告。重新解包时会保留原有不完整目录作为备份。
- 若装在 `Program Files`，可写时正常启动；若需要新建而权限不足，先显示说明，
  用户同意后才通过 Windows `runas` 请求授权。提升的进程只创建固定的 `data`
  目录，并授予发起用户修改该数据目录的权限。不改变 EXE／DLL 的 ACL，
  不在管理员进程中读 ROM、加载游戏或模组。已有目录不可写时不会改它的权限。
- 在程序旁新建内容为 `en` 的 `locale.txt` 即可切回英语，删除文件恢复中文。
  或使用 `--locale en` 临时启动。上游自动更新停用，以免覆盖中文版。

## 技术和验收边界

Windows 构建基于 2026-10-18 的 MIT 提交
`41452f2d053fcfa679e33c00a73d120a53f59e61`。上游较新的源码另有许可条件，
这里没有混入那些改动。Android 默认流程、ROM 文件格式、存档格式和房间 ID
均未改。新分支没有被推送或发布。

- `locales/zh-CN/ui.json` 是应用的 UI 字符串字典；`rom.json` 是 UTF-8 覆盖包，
  `glyphs.bin` 是 16×16 字形。启动器完成解包后把覆盖包原子写入
  `data/game/localization/zh-CN.json`。`data/game/files/AMHE*` 的原始文件保留；
  运行时按源文本指纹匹配，不匹配的行回退到原文，而不是套错译文。
- 菜单渲染优先用译文，脚本仍读原索引及原字符串。十张 ROM 表保留条目 ID、
  前缀、扫描类别和速度，动态变量如 `%s` 和 `&tab0` 也保留。菜单标题是程序绘制
  的中文替代，不包含原版标题贴图。
- 美版 1.0 已用实际解包数据验证。1.1 保留上游的解包支持，共用匹配规则，但
  没有 1.1 样本验收。历史基线的联机服务兼容性没有验收。
- 这是预览版，不是全流程校对稿。扫描描述有本地机译初稿；游戏少数新增页面、
  贴图与影片中的嵌字、可选导入内容仍未全部本地化。厂商标志和人名署名按原样保留。
  Windows 安全桌面上的真实 UAC 交互需要管理员现场批准或取消；自动检查不操作它。
- 本地构建沿用基线依赖。若计划公开分发，还应单独复核 NuGet 审计结果和联网功能。

## 复现构建

用 Windows PowerShell、Git 和 .NET 9 SDK。从源码根目录运行（无需 ROM）：

```powershell
.\src\SecondHunt.Desktop\Publish-Windows.ps1 -Version 0.3.5-zh.2
```

默认输出至 `out/`。有多个 SDK 时用 `-Dotnet 'C:\path\to\dotnet.exe'`。
测试用本地 ROM 解包目录与一个可写的临时目录：

```powershell
dotnet run --project tests/localization/SecondHunt.LocalizationTests.csproj -c Release -p:MphPublic=true -p:MphJitOnly=true -- 'D:\test\data\game' 'D:\test'
```

## 继续维护译文

用 `tools/localization/catalog.py` 和 Python 3。先用自己的 ROM 生成只留在本机的
`source.json`（其中含原文），再导出已绑定译文，按菜单索引和表名／ID 编辑：

```powershell
py tools/localization/catalog.py export 'D:\own\hunters.nds' 'D:\work\source.json'
py tools/localization/catalog.py unpack 'D:\work\source.json' locales/zh-CN/rom.json 'D:\work\translation.json'
py tools/localization/catalog.py pack 'D:\work\source.json' 'D:\work\translation.json' locales/zh-CN/rom.json --locale zh-CN
```

只纳入译文、源文本的 SHA-256 指纹和字形，不提交 `source.json` 或原始 ROM。
修改译文后重建字形：

```powershell
py -m pip install Pillow==12.2.0 fonttools==4.63.0
py tools/localization/catalog.py glyphs 'D:\fonts\NotoSansCJKsc-Medium.otf' locales/zh-CN/ui.json locales/zh-CN/rom.json locales/zh-CN/glyphs.bin
```

字形来源：[Noto Sans CJK SC Medium, Sans2.004](https://github.com/notofonts/noto-cjk/blob/Sans2.004/Sans/OTF/SimplifiedChinese/NotoSansCJKsc-Medium.otf)，
SHA-256：`ca094f6b0001fb048ca39ddd797a0cdb0179e1e55c6561e111c49c3e6a61d7b7`。
OFL 许可随源码和 Windows 包附带。补丁编译校验占位符。
