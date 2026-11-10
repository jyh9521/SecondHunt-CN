# Second Hunt Windows 简体中文版

中文游戏名为《密特罗德究极：猎人》。术语统一为 **Metroid → 密特罗德**、
**Prime → 究极**（如「究极猎人」「究极倍率」）。`Ultimate Power` 是另一个词，
译为「终极力量」。项目采用「萨姆斯･亚兰」（半角中点）、「赛拉克斯」和「舰艇」。
任天堂简中证据和未核实项目的处理见[术语核对](TERMINOLOGY.zh-CN.md)。
程序名、文件名、游戏代码和联机字段不随翻译改动。

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
均未改。汉化源码在 `zh-cn-windows` 分支；Windows 下载见 [Releases](https://github.com/jyh9521/SecondHunt-CN/releases)。

- `locales/zh-CN/ui.json` 是应用的 UI 字符串字典；`settings.json` 按稳定 ID
  保存选项提示、键位和选项值，`terms.json` 保存导入卡片的标题、来源和内置资源名称。
  `extras.json` 保存扩展页面动态句式，`runtime.json` 保存地图控件、对战、联机和回放的运行时提示，
  `names.json` 通过原有房间键、角色和模式枚举映射显示名称。`rom.json` 是 UTF-8 覆盖包，
  `glyphs.bin` 是 32×32 灰阶字形（SHG2；加载器仍识别旧的 16×16 SHG1）。
  游戏内 HUD 保留原字号、字距和换行；汉字直接用灰阶覆盖率绘制，
  字色沿用当前 HUD 的色调并提亮，描边只贴着字形轮廓，不改面板、贴图或过场。
  启动器完成解包后把覆盖包原子写入
  `data/game/localization/zh-CN.json`。`data/game/files/AMHE*` 的原始文件保留；
  运行时按源文本指纹匹配，不匹配的行回退到原文，而不是套错译文。
- 菜单渲染优先用译文，脚本仍读原索引及原字符串。十张 ROM 表保留条目 ID、
  前缀、扫描类别和速度，动态变量如 `%s` 和 `&tab0` 也保留。332 条扫描记录对照
  原文逐条核对；设置说明、模组、回放及联机页面含中文界面文案。
- 地图逐字标题在切分字符串前解析译名，操作提示先翻译、后量宽。对战和回放的显示名与
  诊断字符串分开；原有房间查找键、玩家自填名称、消息预设编号和回放校验不做翻译。
  Windows 在线规则用相同的资源加载流程，第三方许可保留原文。
- 原版贴图和片头嵌字通过[轮廓遮罩与原画恢复](MEDIA-LOCALIZATION.zh-CN.md)
  本地化。覆盖包只带遮罩、字形和版式；原版圆徽、人物、厂商商标和音频保持使用用户的 ROM。
- 美版 1.0 已用实际解包数据验证。1.1 保留上游的解包支持，共用匹配规则，但
  没有 1.1 样本验收。历史基线的联机服务兼容性没有验收。
- 验收涵盖 Windows 启动器、菜单、扩展页面、开场影片、正式剧情开局、四地与终盘检查点、
  两个结局的过场及结算、本地人机对战、实际录制的回放和有数据的回放详情页。
  剧情中后期用开发版检查点定位，不是逐任务通关，也不代替公网联机或可选导入游戏的实机验收。
  厂商标志、人名署名、玩家输入
  及第三方模组自带文本按原样显示。Windows 安全桌面上的真实 UAC 交互需要现场批准或取消。
- `0.3.5-zh.5` 发布的是此前验收的原包，仍沿用基线依赖。NuGet 审计列出 ImageSharp 3.1.12 的已知问题（例如 [GHSA-v76p-62qx-wwq2](https://github.com/advisories/GHSA-v76p-62qx-wwq2)）；请只加载信任的模组、图像及导入资源。

## 复现构建

用 Windows PowerShell、Git 和 .NET 9 SDK。从源码根目录运行（无需 ROM）：

```powershell
.\src\SecondHunt.Desktop\Publish-Windows.ps1 -Version 0.3.5-zh.8
```

默认输出至 `out/`。有多个 SDK 时用 `-Dotnet 'C:\path\to\dotnet.exe'`。
测试用本地 ROM 解包目录与一个可写的临时目录：

```powershell
dotnet run --project tests/localization/SecondHunt.LocalizationTests.csproj -c Release -p:MphPublic=true -p:MphJitOnly=true -- 'D:\test\data\game' 'D:\test'
```

用美版 1.0 测试能量标签及「门」扫描的状态、标题、正文和确认按钮：

```powershell
dotnet run --project tests/localization/SecondHunt.LocalizationTests.csproj -c Release -p:MphPublic=true -p:MphJitOnly=true -- --hud-readability 'D:\test\data\game' 'D:\test\scan.png'
```

省略截图路径时只检查模拟器提交给渲染器的字形。提供路径则额外用 Windows
的游戏渲染器导出实际画面；截图和 ROM 测试数据只保存在本机。

检查扩展页面时可在 `--ui-inventory <game> <report.json>` 后再传入含实际回放的测试 `data` 目录。
工具会记录真正送到字体渲染器的文字，遍历七种已填值的对战规则、在线断连和协议不符等
分支。传入回放时会额外打开第一条记录的详情。联机预览使用本地样本，不改协议、也不连接服务器。

## 简体中文标点

- 独立的单句系统提示（如状态、操作提示、设置说明）中，如果正文没有括号以外的
  标点，句尾省略句号。例如「暂无回放」「未能创建可写目录（退出码 {0}）」。
  仅用于换行的行分隔和格式占位符不是标点。问号、叹号按语气保留。
- 按实际显示语境断句。同一句里的条件、结果或紧密衔接的分句用逗号，例如
  「暂停地图使用原版房间模型，在地图中按 Y 切换。」不同步骤、确认问句、
  图鉴叙述及剧情的独立句保留原有断句。拼接的模板按完整显示结果判断。
- 标点修订只编辑译文值。保留目录键、ROM 源指纹、条目元数据、格式占位符、
  明确的换行及原画遮罩。路径、版本号、IP 地址和人名里的点保持原样。

## 继续维护译文

用 `tools/localization/catalog.py` 和 Python 3。先用自己的 ROM 生成只留在本机的
`source.json`（其中含原文），再导出已绑定译文，按菜单索引和表名／ID 编辑：

```powershell
py tools/localization/catalog.py export 'D:\own\hunters.nds' 'D:\work\source.json'
py tools/localization/catalog.py unpack 'D:\work\source.json' locales/zh-CN/rom.json 'D:\work\translation.json'
py tools/localization/catalog.py pack 'D:\work\source.json' 'D:\work\translation.json' locales/zh-CN/rom.json --locale zh-CN
py tools/localization/catalog.py audit 'D:\work\source.json' locales/zh-CN/rom.json locales/zh-CN/rom-review.amhe0.json 'D:\work\coverage.json'
```

只纳入译文、源文本的 SHA-256 指纹和字形，不提交 `source.json` 或原始 ROM。
审计覆盖菜单及十张表的每个非空字段；审核例外绑定索引或表项、字段和源指纹，
只包括原文署名、绘制时处理的电脑别名、界面会覆盖／隐藏的初值、数字符号和房间内部键。
新增未覆盖文本、改变已审核的原文或保留过期例外会使审计失败。1.1 如要单独出具验收，
应使用自己的 1.1 样本建立对应审核文件，而不是套用 1.0 的结论。
修改译文后重建字形：

```powershell
py -m pip install Pillow==12.2.0 fonttools==4.63.0
py tools/localization/catalog.py glyphs 'D:\fonts\NotoSansCJKsc-Medium.otf' locales/zh-CN/ui.json locales/zh-CN/rom.json locales/zh-CN/glyphs.bin --catalog locales/zh-CN/settings.json --catalog locales/zh-CN/terms.json --catalog locales/zh-CN/extras.json --catalog locales/zh-CN/runtime.json --catalog locales/zh-CN/names.json
```

字形来源：[Noto Sans CJK SC Medium, Sans2.004](https://github.com/notofonts/noto-cjk/blob/Sans2.004/Sans/OTF/SimplifiedChinese/NotoSansCJKsc-Medium.otf)，
SHA-256：`ca094f6b0001fb048ca39ddd797a0cdb0179e1e55c6561e111c49c3e6a61d7b7`。
OFL 许可随源码和 Windows 包附带。补丁编译校验占位符。
