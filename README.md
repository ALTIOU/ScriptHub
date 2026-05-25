# ScriptHub

Windows tray-based manager for small desktop automations.

## Current modules

- QQMusic lyrics auto-hide
  - Playing: show desktop lyrics.
  - Paused or stopped: hide desktop lyrics.
  - Exit ScriptHub: restore desktop lyrics.
  - Uses QQMusic's own `Ctrl + Alt + W` desktop-lyrics shortcut instead of externally hiding windows.
- Codex quota window
  - Opens the official Codex usage page inside ScriptHub.
  - Renders a small local view with the current time, 5-hour quota, and weekly quota.
  - ScriptHub does not pin, move, or keep this window on top; place it manually or use another window tool if needed.
  - `codexQuota.openOnStartup=true` opens this window automatically when ScriptHub starts.
  - `codexQuota.refreshIntervalMinutes=5` reloads the official usage page every 5 minutes.

Keep QQMusic's built-in "自动打开歌词" enabled so QQMusic creates the desktop lyric window.

## Paths

- App: `C:\Users\19715\Documents\ScriptHub\ScriptHub.exe`
- Settings: `C:\Users\19715\Documents\ScriptHub\config\settings.ini`
- Logs: `C:\Users\19715\Documents\ScriptHub\logs\ScriptHub.log`
- Startup fallback: `C:\Users\19715\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup\ScriptHubStartup.vbs`

## Usage

- Use the tray icon menu to toggle QQ Music lyrics, open the Codex quota window, pause scripts, open logs, or exit.
- Double-click the tray icon to open the ScriptHub folder.
- To disable autostart manually, delete `ScriptHubStartup.vbs` from the Startup folder above.

## Build

Run this from the project root:

```powershell
.\scripts\build.ps1
```

The build script downloads the WebView2 WinForms package into `modules\webview2`, copies the runtime DLLs next to `ScriptHub.exe`, compiles with the built-in .NET Framework C# compiler, and restarts ScriptHub if it was already running.

This project intentionally does not commit generated binaries, logs, downloaded packages, or WebView2 browser profile data. Local settings live in `config\settings.ini`; use `config\settings.example.ini` as the committed template.

## Startup

Install or remove the Startup-folder launcher:

```powershell
.\scripts\install-startup.ps1
.\scripts\uninstall-startup.ps1
```
