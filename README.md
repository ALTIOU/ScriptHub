# ScriptHub

ScriptHub 是一个 Windows 托盘小工具，用来统一管理本机常用的小脚本和小程序。当前主要负责两个功能：QQ 音乐桌面歌词自动隐藏，以及 Codex 额度小窗。

## 当前功能

### QQ 音乐桌面歌词自动隐藏

- QQ 音乐正在播放时：自动显示桌面歌词。
- QQ 音乐暂停或停止时：自动隐藏桌面歌词。
- 退出 ScriptHub 时：恢复桌面歌词显示，避免退出后状态不一致。
- 实现方式使用 QQ 音乐自己的 `Ctrl + Alt + W` 桌面歌词快捷键，不是简单遮挡窗口，也不是外部强行隐藏窗口。

需要保持 QQ 音乐设置里的“自动打开歌词”开启，这样 QQ 音乐才会自己创建桌面歌词窗口。

### Codex 额度小窗

- 在 ScriptHub 内打开官方 Codex 用量页面：`https://chatgpt.com/codex/cloud/settings/analytics#usage`。
- 小窗会显示当前时间、日期、星期、5 小时额度和每周额度。
- 小窗使用 WebView2 登录状态，首次使用时需要在窗口里登录一次。
- `codexQuota.openOnStartup=true` 时，ScriptHub 启动后会自动打开额度小窗。
- `codexQuota.refreshIntervalMinutes=5` 时，每 5 分钟强制刷新一次官方用量页面，让额度信息保持更新。
- ScriptHub 不负责窗口置顶、吸附或摆放位置；窗口位置可以自己拖动，也可以用其他窗口管理工具处理。

## 常用路径

- 程序：`C:\Users\19715\Documents\ScriptHub\ScriptHub.exe`
- 配置：`C:\Users\19715\Documents\ScriptHub\config\settings.ini`
- 配置模板：`C:\Users\19715\Documents\ScriptHub\config\settings.example.ini`
- 日志：`C:\Users\19715\Documents\ScriptHub\logs\ScriptHub.log`
- 开机启动脚本：`C:\Users\19715\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup\ScriptHubStartup.vbs`

## 使用方式

- 右键托盘图标可以打开菜单。
- 菜单里可以启用或暂停 QQ 音乐歌词脚本。
- 菜单里可以打开 Codex 额度小窗。
- 菜单里可以暂停全部脚本、重新加载配置、打开日志目录或退出程序。
- 双击托盘图标会打开 ScriptHub 文件夹。

## 配置说明

本机配置放在 `config\settings.ini`，这个文件不会提交到 GitHub。仓库里只提交 `config\settings.example.ini` 作为模板。

常用配置项：

```ini
qqMusicLyrics.enabled=true
qqMusicLyrics.pollIntervalMs=800
qqMusicLyrics.minActionGapMs=1200

codexQuota.url=https://chatgpt.com/codex/cloud/settings/analytics#usage
codexQuota.width=1094
codexQuota.height=496
codexQuota.openOnStartup=true
codexQuota.refreshIntervalMinutes=5
```

## 构建

在项目根目录运行：

```powershell
.\scripts\build.ps1
```

构建脚本会自动下载 WebView2 WinForms 包到 `modules\webview2`，把运行需要的 DLL 复制到 `ScriptHub.exe` 旁边，然后使用系统自带的 .NET Framework C# 编译器生成程序。如果 ScriptHub 正在运行，构建脚本会自动重启它。

## 开机自启

安装或移除开机启动脚本：

```powershell
.\scripts\install-startup.ps1
.\scripts\uninstall-startup.ps1
```

如果想手动关闭开机自启，也可以删除启动目录里的 `ScriptHubStartup.vbs`。

## Git 管理

仓库只提交源码、脚本、README 和配置模板。下面这些文件不会提交：

- `ScriptHub.exe`
- WebView2 运行 DLL
- `config\settings.ini`
- `data\`
- `logs\`
- `modules\`

这样 GitHub 仓库里不会包含本机登录状态、日志、下载缓存和生成文件。
