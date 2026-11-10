# 贴图和影片嵌字

本地化基于 AMHE0 的原版 RGB／RGBA 图像。运行时只改英文笔画、阴影、光晕的遮罩
与新字的 Alpha 范围，不刷整行或矩形底色。源贴图、VX 文件和解码缓冲保持只读。

## 可复现的数据

- `locales/zh-CN/artwork.json` 记录来源 SHA-256、屏幕、帧段、恢复参考帧和中文排版。
- `artwork-source.json` 记录选样和抠字范围。采样闪光帧提取姓名轮廓，比较打字前后
  帧提取说明文字轮廓；小字较早出现的笔画单独标注。输出 `artwork-masks.json`
  仅存 Alpha 遮罩，不含原版图像像素。`artglyphs.bin` 是独立的 48 像素 OFL 字形。
- 影片按解码后的相邻帧恢复人物轮廓，并从当前帧未被文字占据的像素补回亮度和色彩。
  姓名横向光带使用同一像素行的原画作为取样，不冻结或重画光带。中文按原画分阶段出现：
  姓名先亮后收，资料逐字显示，均随各卡退场。
- 原画尚未打出某个英文单词时，其区域继续显示当前帧。韦弗尔资料压过斜向的黄色
  刀刃，字形下方沿刀刃方向取样，以免在刀刃上留下暗色缺口。
- 标题圆徽被原英文字挡住的局部用同一圆徽的可见弧面补回；其余区域仍是原始贴图。
  NST 标志字标、ACTIMAGINE 商标保持原样，只换旁边的文字。ESRB 提示清除原笔画
  再排中文，保留背景。

| 档案 | 屏幕 | 资料起帧 | 姓名起帧 | 结束（不含） |
| --- | --- | ---: | ---: | ---: |
| 赛拉克斯 | 下 | 7 | 17 | 208 |
| 韦弗尔 | 上 | 214 | 225 | 415 |
| 诺克萨斯 | 下 | 422 | 432 | 518 |
| 特雷斯 | 上 | 524 | 535 | 619 |
| 斯派尔 | 下 | 628 | 637 | 813 |
| 坎登 | 上 | 820 | 830 | 1005 |

## 导出和校验

在仓库根目录运行。仅在本机保留 ROM 和导出的原图／对照图；它们不是构建输入。

```powershell
$amhe0 = 'D:\test\data\game\files\AMHE0'
$work = 'D:\work\media'
$project = 'tests/localization/SecondHunt.LocalizationTests.csproj'
dotnet run --project $project -c Release -p:MphJitOnly=true -- --media-inventory $amhe0 "$work\inventory" 12
foreach ($side in 'top','bot') {
  dotnet run --project $project -c Release -p:MphJitOnly=true -- --movie-inventory "$amhe0\movies\01_$side.vx" "$work\frames" 1
}
py tools/localization/artwork.py locales/zh-CN/artwork-source.json locales/zh-CN/artwork.json "$work\frames" "$work\inventory\textures" 'D:\fonts\NotoSansCJKsc-Medium.otf' locales/zh-CN/artwork-masks.json locales/zh-CN/artglyphs.bin
dotnet run --project $project -c Release -p:MphJitOnly=true -- --artwork-check $amhe0 "$work\compare"
```

`artwork.py` 需要 Pillow、NumPy。测试用真实 VX 和贴图对照初现、闪光、稳定、退场四种
状态，逐像素检查人物主体、对侧屏幕和商标等保护区域，以及英语模式的原画回退。
未核对过的版本或哈希不匹配的素材保留其原画。
