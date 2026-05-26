# ScriptHub

ScriptHub 是一个 Windows 托盘小工具，用来集中管理轻量级桌面自动化脚本。

当前内置两个模块：

- QQ 音乐桌面歌词自动显示 / 隐藏
- Codex 额度小窗

项目目标是把零散的小脚本做成一个安静、可开机自启、可托盘管理的本地程序，避免多个脚本窗口常驻桌面。

## 功能

### QQ 音乐桌面歌词

- 播放时自动显示 QQ 音乐桌面歌词。
- 暂停或停止时自动隐藏 QQ 音乐桌面歌词。
- 退出 ScriptHub 时恢复桌面歌词显示，避免状态残留。
- 使用 QQ 音乐自己的 `Ctrl + Alt + W` 桌面歌词快捷键切换，不通过遮挡窗口模拟隐藏。

使用前需要确认 QQ 音乐里“显示/隐藏桌面歌词”的快捷键是 `Ctrl + Alt + W`。“自动打开歌词”不是硬性依赖；如果希望完全由 ScriptHub 控制播放时显示、暂停时隐藏，可以关闭 QQ 音乐自己的自动打开歌词。

### Codex 额度小窗

- 在内置 WebView2 窗口中打开 Codex 官方用量页面。
- 抽取页面里的 5 小时额度和每周额度，渲染成简洁的小窗。
- 显示当前时间、日期和星期。
- 支持启动时自动打开。
- 支持按配置间隔刷新页面。

首次使用需要在 WebView2 窗口里登录 ChatGPT / Codex 账号。登录状态保存在本地 WebView2 用户数据目录中，不会提交到仓库。

## 环境要求

- Windows
- PowerShell
- .NET Framework C# 编译器 `csc.exe`
- Microsoft Edge WebView2 Runtime

大多数 Windows 机器已经自带 .NET Framework 和 WebView2 Runtime。如果缺少 WebView2 Runtime，可以从 Microsoft 官方渠道安装。

## 快速开始

克隆仓库后，在项目根目录运行：

```powershell
Copy-Item .\config\settings.example.ini .\config\settings.ini
.\scripts\build.ps1
.\ScriptHub.exe
```

构建完成后，ScriptHub 会出现在系统托盘中。

## 使用方式

- 右键托盘图标打开菜单。
- 在菜单中启用或暂停 QQ 音乐歌词模块。
- 在菜单中打开 Codex 额度小窗。
- 可以从菜单暂停全部脚本、重新加载配置、打开日志目录或退出程序。
- 双击托盘图标会打开项目目录。

## 配置说明

本地配置文件是 `config/settings.ini`。仓库只提交 `config/settings.example.ini` 作为模板。

常用配置项：

```ini
qqMusicLyrics.enabled=true
qqMusicLyrics.pollIntervalMs=800
qqMusicLyrics.minActionGapMs=1200

codexQuota.url=https://chatgpt.com/codex/cloud/settings/analytics#usage
codexQuota.width=1094
codexQuota.height=496
codexQuota.x=-1
codexQuota.y=-1
codexQuota.openOnStartup=true
codexQuota.refreshIntervalMinutes=5
```

说明：

- `qqMusicLyrics.enabled`：是否启用 QQ 音乐桌面歌词自动隐藏。
- `qqMusicLyrics.pollIntervalMs`：检测播放状态的间隔。
- `qqMusicLyrics.minActionGapMs`：两次切换歌词状态之间的最小间隔。
- `codexQuota.url`：Codex 用量页面地址。
- `codexQuota.width` / `codexQuota.height`：额度小窗大小。
- `codexQuota.x` / `codexQuota.y`：额度小窗位置，设为 `-1` 时使用系统默认位置。
- `codexQuota.openOnStartup`：ScriptHub 启动时是否自动打开额度小窗。
- `codexQuota.refreshIntervalMinutes`：额度页面强制刷新间隔，设为 `0` 可关闭定时刷新。

## 构建

在项目根目录运行：

```powershell
.\scripts\build.ps1
```

构建脚本会：

- 下载 WebView2 WinForms NuGet 包到 `modules/webview2`。
- 复制运行需要的 WebView2 DLL 到项目根目录。
- 使用系统自带的 .NET Framework C# 编译器生成 `ScriptHub.exe`。
- 如果 ScriptHub 已经在运行，构建后会自动重启它。

## 开机自启

安装或移除开机启动脚本：

```powershell
.\scripts\install-startup.ps1
.\scripts\uninstall-startup.ps1
```

安装脚本会在当前用户的 Windows 启动目录中创建一个 VBS 启动器，用于静默启动 `ScriptHub.exe`。

## 目录结构

```text
ScriptHub/
  config/
    settings.example.ini
  scripts/
    build.ps1
    install-startup.ps1
    uninstall-startup.ps1
  src/
    Program.cs
  README.md
```

## Git 管理

仓库只提交源码、脚本、README 和配置模板。以下内容会被忽略：

- `ScriptHub.exe`
- WebView2 运行 DLL
- `config/settings.ini`
- `data/`
- `logs/`
- `modules/`

这样可以避免把本地登录状态、日志、下载缓存和生成文件提交到仓库。
