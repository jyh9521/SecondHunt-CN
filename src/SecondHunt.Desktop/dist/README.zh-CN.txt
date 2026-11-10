Second Hunt 简体中文版 · Windows x64
=====================================

1. 将整个 ZIP 解压到可写目录，例如 D:\Games。双击 SecondHunt.exe。
2. 第一次运行时选择自己提供的《密特罗德究极：猎人》美版 ROM
   （AMHE，1.0 或 1.1），也可以把 .nds 拖进窗口。安装包不含 ROM。
3. 程序解包后自动装入简体中文补丁。ROM 只读，原版解包资源不覆盖。

数据在程序旁的 data 文件夹：
    data\game           解包资源和简体中文覆盖层
    data\saves          存档
    data\mods           模组
    data\logs、crash    日志和崩溃报告

如原版使用 %LOCALAPPDATA%\Second Hunt，新安装首次打开时会复制
旧解包数据、存档和设置。已有的本地文件不覆盖，旧目录保留作备份。
如果放在 Program Files，只有新建 data 确实需要授权时才请求 UAC，
游戏本体不以管理员权限运行。若已有 data 不可写，请先移到可写位置。

把整个文件夹连同 data 一起搬走即可保留存档。需回到英文时，在
SecondHunt.exe 旁创建 locale.txt，内容为 en；删除该文件恢复中文。
也可临时以 --locale en 启动。中文版不从上游自动覆盖更新。

本版补译设置、导入、模组、回放和联机页面，核对了 332 条扫描记录。
片头六张猎人档案卡、标题和提示贴图的中文只替换原文字，不遮住原画。
厂商商标、署名、玩家输入和第三方模组自带文本保持原样。
美版 1.0 已验收；1.1、真实联机和可选导入游戏未逐一实机验收。

来源和许可见 LICENSE.txt、NOTICE-fork.txt、THIRD_PARTY_NOTICES.txt。
