using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ScriptHub
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool created;
            using (var mutex = new Mutex(true, "ScriptHub-19715-DesktopAutomation", out created))
            {
                if (!created)
                {
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new ScriptHubContext(args));
            }
        }
    }

    internal sealed class ScriptHubContext : ApplicationContext
    {
        private readonly AppPaths _paths;
        private readonly Logger _logger;
        private readonly HubSettings _settings;
        private readonly QqMusicLyricsModule _qqMusic;
        private readonly NotifyIcon _tray;
        private readonly ToolStripMenuItem _qqMusicItem;
        private readonly ToolStripMenuItem _pauseAllItem;
        private readonly ToolStripMenuItem _autostartItem;
        private CodexQuotaForm _codexQuotaForm;
        private bool _pauseAll;

        public ScriptHubContext(string[] args)
        {
            _paths = AppPaths.Discover();
            Directory.CreateDirectory(_paths.ConfigDir);
            Directory.CreateDirectory(_paths.LogDir);
            Directory.CreateDirectory(_paths.ScriptsDir);
            Directory.CreateDirectory(_paths.ModulesDir);

            _logger = new Logger(Path.Combine(_paths.LogDir, "ScriptHub.log"));
            _settings = HubSettings.Load(_paths.SettingsPath, _logger);
            _qqMusic = new QqMusicLyricsModule(_logger, _settings.QqMusicLyrics);
            _qqMusic.EnabledChanged += (_, __) => SaveSettings();

            _qqMusicItem = new ToolStripMenuItem("QQ音乐歌词自动隐藏");
            _qqMusicItem.Click += (_, __) =>
            {
                _qqMusic.Enabled = !_qqMusic.Enabled;
                UpdateMenu();
                SaveSettings();
            };

            var codexQuotaWindowItem = new ToolStripMenuItem("打开 Codex 额度小窗");
            codexQuotaWindowItem.Click += (_, __) => ShowCodexQuotaWindow();

            _pauseAllItem = new ToolStripMenuItem("暂停全部脚本");
            _pauseAllItem.Click += (_, __) =>
            {
                _pauseAll = !_pauseAll;
                _qqMusic.Suspended = _pauseAll;
                UpdateMenu();
            };

            var reloadItem = new ToolStripMenuItem("重新加载配置");
            reloadItem.Click += (_, __) => ReloadSettings();

            var openLogsItem = new ToolStripMenuItem("打开日志目录");
            openLogsItem.Click += (_, __) => Process.Start("explorer.exe", _paths.LogDir);

            _autostartItem = new ToolStripMenuItem("开机自启");
            _autostartItem.Click += (_, __) => ToggleAutostart();

            var exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += (_, __) => ExitThread();

            var menu = new ContextMenuStrip();
            menu.Items.Add(new ToolStripMenuItem("ScriptHub") { Enabled = false });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_qqMusicItem);
            menu.Items.Add(codexQuotaWindowItem);
            menu.Items.Add(_pauseAllItem);
            menu.Items.Add(reloadItem);
            menu.Items.Add(openLogsItem);
            menu.Items.Add(_autostartItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);

            _tray = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "ScriptHub",
                ContextMenuStrip = menu,
                Visible = true
            };
            _tray.DoubleClick += (_, __) => Process.Start("explorer.exe", _paths.RootDir);

            UpdateMenu();
            _qqMusic.Start();
            _logger.Info("ScriptHub started.");

            if (_settings.CodexQuota.OpenOnStartup ||
                (args != null && args.Any(arg => string.Equals(arg, "--open-codex-quota", StringComparison.OrdinalIgnoreCase))))
            {
                ShowCodexQuotaWindow();
            }
        }

        protected override void ExitThreadCore()
        {
            _logger.Info("ScriptHub exiting.");
            _qqMusic.Dispose();
            if (_codexQuotaForm != null)
            {
                _codexQuotaForm.Close();
                _codexQuotaForm = null;
            }
            _tray.Visible = false;
            _tray.Dispose();
            base.ExitThreadCore();
        }

        private void ReloadSettings()
        {
            var fresh = HubSettings.Load(_paths.SettingsPath, _logger);
            _qqMusic.ApplySettings(fresh.QqMusicLyrics);
            _settings.CodexQuota.ApplyFrom(fresh.CodexQuota);
            _pauseAll = false;
            _qqMusic.Suspended = false;
            UpdateMenu();
            _logger.Info("Settings reloaded.");
        }

        private void SaveSettings()
        {
            _settings.QqMusicLyrics.Enabled = _qqMusic.Enabled;
            _settings.Save(_paths.SettingsPath, _logger);
        }

        private void ShowCodexQuotaWindow()
        {
            if (_codexQuotaForm == null || _codexQuotaForm.IsDisposed)
            {
                _codexQuotaForm = new CodexQuotaForm(_paths, _logger, _settings.CodexQuota);
                _codexQuotaForm.FormClosed += (_, __) =>
                {
                    SaveSettings();
                    _codexQuotaForm = null;
                };
            }

            _codexQuotaForm.Show();
            if (_codexQuotaForm.WindowState == FormWindowState.Minimized)
            {
                _codexQuotaForm.WindowState = FormWindowState.Normal;
            }
            _codexQuotaForm.Activate();
        }

        private void ToggleAutostart()
        {
            try
            {
                if (TaskSchedulerHelper.IsEnabled())
                {
                    TaskSchedulerHelper.Disable();
                }
                else
                {
                    TaskSchedulerHelper.Enable(_paths.ExePath);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Autostart toggle failed: " + ex);
                MessageBox.Show("开机自启设置失败，详情见日志。", "ScriptHub", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            UpdateMenu();
        }

        private void UpdateMenu()
        {
            _qqMusicItem.Checked = _qqMusic.Enabled;
            _pauseAllItem.Checked = _pauseAll;
            _autostartItem.Checked = TaskSchedulerHelper.IsEnabled();
            var status = _pauseAll ? "已暂停" : (_qqMusic.Enabled ? "运行中" : "已关闭");
            _tray.Text = "ScriptHub - " + status;
        }
    }

    internal sealed class QqMusicLyricsModule : IDisposable
    {
        private readonly Logger _logger;
        private readonly System.Threading.Timer _timer;
        private readonly object _lock = new object();
        private QqMusicLyricsSettings _settings;
        private string _lastStatus = "Unknown";
        private bool _disposed;

        public event EventHandler EnabledChanged;

        public QqMusicLyricsModule(Logger logger, QqMusicLyricsSettings settings)
        {
            _logger = logger;
            _settings = settings ?? new QqMusicLyricsSettings();
            _timer = new System.Threading.Timer(_ => Tick(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public bool Enabled
        {
            get { return _settings.Enabled; }
            set
            {
                if (_settings.Enabled == value)
                {
                    return;
                }
                _settings.Enabled = value;
                _logger.Info("QQMusic lyrics module " + (value ? "enabled." : "disabled."));
                if (!value)
                {
                    RestoreLyrics();
                }
                var handler = EnabledChanged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
        }

        public bool Suspended { get; set; }

        public void Start()
        {
            _timer.Change(0, Math.Max(300, _settings.PollIntervalMs));
        }

        public void ApplySettings(QqMusicLyricsSettings settings)
        {
            _settings = settings ?? new QqMusicLyricsSettings();
            _timer.Change(0, Math.Max(300, _settings.PollIntervalMs));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _timer.Dispose();
            RestoreLyrics();
        }

        private void Tick()
        {
            if (_disposed)
            {
                return;
            }

            lock (_lock)
            {
                if (!_settings.Enabled || Suspended)
                {
                    return;
                }

                try
                {
                    var status = MediaStatusReader.GetQqMusicPlaybackStatus();
                    if (!string.Equals(status, _lastStatus, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.Info("QQMusic playback status: " + status);
                        _lastStatus = status;
                    }

                    if (status == "Playing")
                    {
                        SetLyricsVisible(true);
                    }
                    else if (status == "Paused" || status == "Stopped")
                    {
                        SetLyricsVisible(false);
                    }
                    else if (!Process.GetProcessesByName("QQMusic").Any())
                    {
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("QQMusic module tick failed: " + ex.Message);
                }
            }
        }

        private void RestoreLyrics()
        {
            try
            {
                SetLyricsVisible(true, force: true);
            }
            catch (Exception ex)
            {
                _logger.Error("Restore lyrics failed: " + ex.Message);
            }
        }

        private void SetLyricsVisible(bool visible, bool force = false)
        {
            var state = QqMusicWindowFinder.GetDesktopLyricsState();
            if (IsTargetSatisfied(visible, state) && !force)
            {
                return;
            }

            if (!NeedsInternalToggle(visible, state) && !force)
            {
                return;
            }

            ToggleDesktopLyricsAndWait(visible);
            var finalState = QqMusicWindowFinder.GetDesktopLyricsState();
            if (!IsTargetSatisfied(visible, finalState))
            {
                ToggleDesktopLyricsAndWait(visible);
                finalState = QqMusicWindowFinder.GetDesktopLyricsState();
            }

            _logger.Info("Desktop lyrics " + (visible ? "shown" : "hidden") +
                " via QQMusic hotkey. Roots=" + finalState.RootCount +
                " VisibleRoots=" + finalState.VisibleRootCount);
        }

        private static bool IsTargetSatisfied(bool visible, DesktopLyricsState state)
        {
            return visible ? state.VisibleRootCount > 0 : state.RootCount == 0;
        }

        private static bool NeedsInternalToggle(bool visible, DesktopLyricsState state)
        {
            return visible ? state.VisibleRootCount == 0 : state.RootCount > 0;
        }

        private static void ToggleDesktopLyricsAndWait(bool visible)
        {
            HotKeySender.SendCtrlAltW();
            var deadline = DateTime.UtcNow.AddMilliseconds(1200);
            while (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(100);
                var state = QqMusicWindowFinder.GetDesktopLyricsState();
                if (IsTargetSatisfied(visible, state))
                {
                    return;
                }
            }
        }
    }

    internal sealed class CodexQuotaForm : Form
    {
        private readonly AppPaths _paths;
        private readonly Logger _logger;
        private readonly CodexQuotaSettings _settings;
        private readonly WebView2 _webView;
        private readonly System.Windows.Forms.Timer _refreshTimer;
        private bool _initialized;
        private int _quotaNavigateAttempts;

        public CodexQuotaForm(AppPaths paths, Logger logger, CodexQuotaSettings settings)
        {
            _paths = paths;
            _logger = logger;
            _settings = settings ?? new CodexQuotaSettings();

            Text = "Codex 额度小窗";
            Icon = SystemIcons.Application;
            MinimumSize = new Size(420, 320);
            Size = new Size(Math.Max(420, _settings.Width), Math.Max(320, _settings.Height));
            StartPosition = FormStartPosition.CenterScreen;
            if (_settings.X >= 0 && _settings.Y >= 0)
            {
                StartPosition = FormStartPosition.Manual;
                Location = new Point(_settings.X, _settings.Y);
            }

            _webView = new WebView2
            {
                Dock = DockStyle.Fill
            };
            Controls.Add(_webView);

            _refreshTimer = new System.Windows.Forms.Timer();
            _refreshTimer.Interval = Math.Max(1, _settings.RefreshIntervalMinutes) * 60 * 1000;
            _refreshTimer.Tick += (_, __) => RefreshQuotaPage();

            Load += CodexQuotaForm_Load;
            Move += (_, __) => RememberBounds();
            Resize += (_, __) => RememberBounds();
            FormClosing += (_, __) => RememberBounds();
            FormClosed += (_, __) =>
            {
                _refreshTimer.Stop();
                _refreshTimer.Dispose();
            };
        }

        private async void CodexQuotaForm_Load(object sender, EventArgs e)
        {
            if (_initialized)
            {
                return;
            }
            _initialized = true;

            try
            {
                var userDataFolder = Path.Combine(_paths.RootDir, "data", "codex-quota-webview");
                Directory.CreateDirectory(userDataFolder);

                var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await _webView.EnsureCoreWebView2Async(environment);
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                _webView.CoreWebView2.DocumentTitleChanged += (_, __) =>
                {
                    var title = _webView.CoreWebView2.DocumentTitle;
                    Text = string.IsNullOrWhiteSpace(title) ? "Codex 额度小窗" : "Codex 额度小窗";
                };
                _webView.CoreWebView2.NavigationCompleted += async (_, __) =>
                {
                    try
                    {
                        EnsureQuotaPageAfterLogin();
                        await _webView.CoreWebView2.ExecuteScriptAsync(BuildLiteModeScript());
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("Codex quota lite script failed: " + ex.Message);
                    }
                };
                await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(BuildLiteModeScript());
                _webView.CoreWebView2.Navigate(_settings.Url);
                if (_settings.RefreshIntervalMinutes > 0)
                {
                    _refreshTimer.Start();
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Open Codex quota window failed: " + ex);
                MessageBox.Show("Codex 额度小窗打开失败，详情见日志。", "ScriptHub", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RefreshQuotaPage()
        {
            try
            {
                if (_webView == null || _webView.CoreWebView2 == null)
                {
                    return;
                }

                var source = _webView.CoreWebView2.Source ?? string.Empty;
                if (source.IndexOf("/codex/cloud/settings/analytics", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _webView.CoreWebView2.Reload();
                }
                else
                {
                    _webView.CoreWebView2.Navigate(_settings.Url);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Refresh Codex quota page failed: " + ex.Message);
            }
        }

        private void RememberBounds()
        {
            if (WindowState != FormWindowState.Normal)
            {
                return;
            }

            _settings.X = Location.X;
            _settings.Y = Location.Y;
            _settings.Width = Size.Width;
            _settings.Height = Size.Height;
        }

        private void EnsureQuotaPageAfterLogin()
        {
            if (_webView == null || _webView.CoreWebView2 == null)
            {
                return;
            }

            var source = _webView.CoreWebView2.Source ?? string.Empty;
            if (source.IndexOf("/codex/cloud/settings/analytics", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _quotaNavigateAttempts = 0;
                return;
            }

            if (source.IndexOf("chatgpt.com", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return;
            }

            if (source.IndexOf("/api/auth/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                source.IndexOf("auth.openai.com", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return;
            }

            if (_quotaNavigateAttempts >= 3)
            {
                return;
            }

            _quotaNavigateAttempts++;
            BeginInvoke(new Action(() =>
            {
                try
                {
                    _webView.CoreWebView2.Navigate(_settings.Url);
                }
                catch (Exception ex)
                {
                    _logger.Error("Navigate Codex quota page failed: " + ex.Message);
                }
            }));
        }

        private static string BuildLiteModeScript()
        {
            return @"
(function () {
  if (window.__scriptHubCodexQuotaLiteInstalled) {
    return;
  }
  window.__scriptHubCodexQuotaLiteInstalled = true;

  var groups = [
    { title: '5 小时使用限额' },
    { title: '每周使用限额' }
  ];
  var lastData = [];

  function normalizeText(text) {
    return (text || '')
      .replace(/\s+/g, ' ')
      .replace(/剩\s*余/g, '剩余')
      .replace(/重置\s*时间/g, '重置时间')
      .trim();
  }

  function getOriginalText() {
    var clone = document.body.cloneNode(true);
    var lite = clone.querySelector('#__scriptHubCodexQuotaLite');
    if (lite && lite.parentNode) {
      lite.parentNode.removeChild(lite);
    }
    return normalizeText(clone.textContent || clone.innerText || '');
  }

  function findTitleIndex(text, title) {
    var index = text.indexOf(title);
    if (index >= 0) {
      return index;
    }
    if (title === '5 小时使用限额') {
      return text.indexOf('5小时使用限额');
    }
    return -1;
  }

  function getSectionText(text, title) {
    var start = findTitleIndex(text, title);
    if (start < 0) {
      return '';
    }

    var end = text.length;
    for (var i = 0; i < groups.length; i++) {
      var otherTitle = groups[i].title;
      if (otherTitle === title) {
        continue;
      }
      var otherIndex = findTitleIndex(text, otherTitle);
      if (otherIndex > start && otherIndex < end) {
        end = otherIndex;
      }
    }

    return text.substring(start, end);
  }

  function ensureWrapper() {
    var wrapper = document.getElementById('__scriptHubCodexQuotaLite');
    if (!wrapper) {
      wrapper = document.createElement('div');
      wrapper.id = '__scriptHubCodexQuotaLite';
      document.body.appendChild(wrapper);
    }
    wrapper.style.cssText =
      'position:fixed!important;inset:0!important;z-index:2147483647!important;' +
      'box-sizing:border-box!important;display:flex!important;flex-direction:column!important;gap:20px!important;' +
      'align-items:center!important;justify-content:flex-start!important;padding:96px 14px 18px!important;' +
      'background:#ffffff!important;overflow:hidden!important;font-family:system-ui,-apple-system,Segoe UI,sans-serif!important;';
    return wrapper;
  }

  function installPageShield() {
    var style = document.getElementById('__scriptHubCodexQuotaStyle');
    if (!style) {
      style = document.createElement('style');
      style.id = '__scriptHubCodexQuotaStyle';
      (document.head || document.documentElement).appendChild(style);
    }
    style.textContent =
      'html,body{background:#fff!important;margin:0!important;overflow:hidden!important;}' +
      'body>:not(#__scriptHubCodexQuotaLite){position:fixed!important;left:-20000px!important;top:0!important;' +
      'width:1400px!important;min-height:900px!important;opacity:0!important;visibility:hidden!important;' +
      'pointer-events:none!important;transform:none!important;}' +
      '#__scriptHubCodexQuotaLite,#__scriptHubCodexQuotaLite *{box-sizing:border-box!important;}';
  }

  function createClock() {
    var clock = document.createElement('div');
    clock.id = '__scriptHubCodexQuotaClock';
    clock.style.cssText =
      'box-sizing:border-box!important;width:100%!important;text-align:center!important;' +
      'color:#111827!important;line-height:1.15!important;user-select:none!important;';

    var time = document.createElement('div');
    time.setAttribute('data-role', 'time');
    time.style.cssText =
      'font-size:44px!important;font-weight:700!important;letter-spacing:0!important;' +
      'font-variant-numeric:tabular-nums!important;';

    var date = document.createElement('div');
    date.setAttribute('data-role', 'date');
    date.style.cssText =
      'margin-top:8px!important;font-size:18px!important;font-weight:500!important;color:#6b7280!important;';

    clock.appendChild(time);
    clock.appendChild(date);
    return clock;
  }

  function updateClock() {
    var clock = document.getElementById('__scriptHubCodexQuotaClock');
    if (!clock) {
      return;
    }

    var now = new Date();
    var time = clock.querySelector(`[data-role='time']`);
    var date = clock.querySelector(`[data-role='date']`);
    if (time) {
      time.textContent = now.toLocaleTimeString('zh-CN', { hour12: false });
    }
    if (date) {
      var dateText = now.toLocaleDateString('zh-CN', { year: 'numeric', month: 'long', day: 'numeric' });
      var weekText = now.toLocaleDateString('zh-CN', { weekday: 'long' });
      date.textContent = dateText + '  ' + weekText;
    }
  }

  function extractQuota(group, pageText, fallback) {
    var text = getSectionText(pageText, group.title);
    if (!text && fallback) {
      return fallback;
    }

    var percentMatch = text.match(/(\d{1,3})\s*%\s*剩余/) || text.match(/(\d{1,3})\s*%/);
    var resetMatch = text.match(/重置时间[:：]?\s*((?:\d{4}年\d{1,2}月\d{1,2}日\s*)?\d{1,2}:\d{2})/);
    if (!resetMatch) {
      resetMatch = text.match(/重置时间[:：]?\s*([^ ]+)/);
    }
    var reset = resetMatch ? resetMatch[1] : '';
    reset = reset
      .replace(/5\s*小时使用限额.*$/g, '')
      .replace(/每周使用限额.*$/g, '')
      .trim();

    var percent = percentMatch ? Math.max(0, Math.min(100, parseInt(percentMatch[1], 10))) : null;
    return {
      title: group.title,
      percent: percent,
      reset: reset || (fallback ? fallback.reset : ''),
      ok: percent !== null
    };
  }

  function makeQuotaCard(info) {
    var card = document.createElement('div');
    card.style.cssText =
      'width:320px!important;min-height:134px!important;padding:24px 26px!important;' +
      'border:1px solid #e5e7eb!important;border-radius:18px!important;background:#fff!important;' +
      'box-shadow:0 14px 30px rgba(15,23,42,.06)!important;color:#111827!important;';

    var title = document.createElement('div');
    title.textContent = info.title;
    title.style.cssText = 'font-size:14px!important;color:#6b7280!important;margin-bottom:8px!important;';

    var value = document.createElement('div');
    value.style.cssText = 'font-size:26px!important;font-weight:700!important;line-height:1.1!important;margin-bottom:20px!important;';
    var percentText = info.ok ? String(info.percent) + '%' : '--%';
    value.textContent = percentText + ' 剩余';

    var track = document.createElement('div');
    track.style.cssText =
      'height:12px!important;border-radius:999px!important;background:#e5e7eb!important;' +
      'overflow:hidden!important;margin-bottom:18px!important;';

    var bar = document.createElement('div');
    bar.style.cssText =
      'height:100%!important;border-radius:999px!important;background:#22c55e!important;width:' +
      (info.ok ? info.percent : 0) + '%!important;';
    track.appendChild(bar);

    var reset = document.createElement('div');
    reset.textContent = '重置时间: ' + (info.reset || '读取中');
    reset.style.cssText = 'font-size:13px!important;color:#6b7280!important;line-height:1.4!important;';

    card.appendChild(title);
    card.appendChild(value);
    card.appendChild(track);
    card.appendChild(reset);
    return card;
  }

  function createCardRow() {
    var row = document.createElement('div');
    row.id = '__scriptHubCodexQuotaCards';
    row.style.cssText =
      'box-sizing:border-box!important;width:100%!important;display:flex!important;gap:16px!important;' +
      'align-items:center!important;justify-content:center!important;margin-top:30px!important;';
    return row;
  }

  function render() {
    if (!document.body) {
      return;
    }

    var wrapper = ensureWrapper();
    installPageShield();

    var pageText = getOriginalText();
    var data = [];
    for (var i = 0; i < groups.length; i++) {
      data.push(extractQuota(groups[i], pageText, lastData[i] || { title: groups[i].title, percent: null, reset: '', ok: false }));
    }

    if (data.length >= 2 && (data[0].ok || data[1].ok)) {
      lastData = data;
    }

    wrapper.innerHTML = '';
    var clock = createClock();
    wrapper.appendChild(clock);
    updateClock();

    var row = createCardRow();
    for (var j = 0; j < data.length; j++) {
      row.appendChild(makeQuotaCard(data[j]));
    }
    wrapper.appendChild(row);
  }

  render();
  window.setInterval(render, 3000);
  window.setInterval(updateClock, 1000);
})();
";
        }
    }

    internal static class MediaStatusReader
    {
        private static Type _managerType;
        private static MethodInfo _asTaskGeneric;

        public static string GetQqMusicPlaybackStatus()
        {
            var managerType = ManagerType;
            if (managerType == null)
            {
                return "Unavailable";
            }

            var requestAsync = managerType.GetMethod("RequestAsync", BindingFlags.Public | BindingFlags.Static);
            var asyncOperation = requestAsync.Invoke(null, null);
            var task = AsTaskGeneric.MakeGenericMethod(managerType).Invoke(null, new[] { asyncOperation });
            task.GetType().GetMethod("Wait", Type.EmptyTypes).Invoke(task, null);
            var manager = task.GetType().GetProperty("Result").GetValue(task, null);
            var sessions = manager.GetType().GetMethod("GetSessions").Invoke(manager, null);

            foreach (var session in (IEnumerable)sessions)
            {
                var source = Convert.ToString(session.GetType().GetProperty("SourceAppUserModelId").GetValue(session, null));
                if (!IsQqMusicSource(source))
                {
                    continue;
                }

                var info = session.GetType().GetMethod("GetPlaybackInfo").Invoke(session, null);
                var status = info.GetType().GetProperty("PlaybackStatus").GetValue(info, null);
                return Convert.ToString(status);
            }

            return "NoSession";
        }

        private static Type ManagerType
        {
            get
            {
                return _managerType ?? (_managerType = Type.GetType("Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager, Windows.Media.Control, ContentType=WindowsRuntime"));
            }
        }

        private static MethodInfo AsTaskGeneric
        {
            get
            {
                if (_asTaskGeneric != null)
                {
                    return _asTaskGeneric;
                }

                _asTaskGeneric = typeof(System.WindowsRuntimeSystemExtensions).GetMethods()
                    .First(method =>
                        method.Name == "AsTask" &&
                        method.IsGenericMethodDefinition &&
                        method.GetParameters().Length == 1 &&
                        method.GetParameters()[0].ParameterType.Name == "IAsyncOperation`1");
                return _asTaskGeneric;
            }
        }

        private static bool IsQqMusicSource(string source)
        {
            if (string.IsNullOrEmpty(source))
            {
                return false;
            }
            return source.IndexOf("QQMusic", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    internal static class QqMusicWindowFinder
    {
        public static DesktopLyricsState GetDesktopLyricsState()
        {
            var roots = FindDesktopLyricsRootWindows();
            var visibleRoots = roots.Count(hwnd => NativeMethods.IsWindowVisible(hwnd));
            return new DesktopLyricsState
            {
                RootCount = roots.Count,
                VisibleRootCount = visibleRoots
            };
        }

        private static List<IntPtr> FindDesktopLyricsRootWindows()
        {
            var qqMusicIds = new HashSet<int>(Process.GetProcessesByName("QQMusic").Select(p => p.Id));
            var allWindows = new List<IntPtr>();
            var lyricRoots = new HashSet<IntPtr>();
            if (qqMusicIds.Count == 0)
            {
                return allWindows;
            }

            NativeMethods.EnumWindows((hwnd, _) =>
            {
                uint processId;
                NativeMethods.GetWindowThreadProcessId(hwnd, out processId);
                if (!qqMusicIds.Contains((int)processId))
                {
                    return true;
                }

                allWindows.Add(hwnd);

                return true;
            }, IntPtr.Zero);

            foreach (var hwnd in allWindows)
            {
                if (IsDesktopLyricsRoot(hwnd))
                {
                    lyricRoots.Add(hwnd);
                }
            }

            var result = new List<IntPtr>();
            foreach (var hwnd in allWindows)
            {
                if (lyricRoots.Contains(hwnd))
                {
                    result.Add(hwnd);
                }
            }

            return result;
        }

        private static bool IsDesktopLyricsRoot(IntPtr hwnd)
        {
            var title = NativeMethods.GetWindowText(hwnd);
            var className = NativeMethods.GetClassName(hwnd);
            var owner = NativeMethods.GetWindow(hwnd, NativeMethods.GW_OWNER);
            return owner == IntPtr.Zero &&
                string.Equals(className, "TXGuiFoundation", StringComparison.OrdinalIgnoreCase) &&
                title.IndexOf("桌面歌词", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    internal sealed class DesktopLyricsState
    {
        public int RootCount;
        public int VisibleRootCount;
    }

    internal static class HotKeySender
    {
        private const byte VK_CONTROL = 0x11;
        private const byte VK_SHIFT = 0x10;
        private const byte VK_MENU = 0x12;
        private const byte VK_W = 0x57;
        private const byte VK_V = 0x56;
        private const byte VK_RETURN = 0x0D;
        private const byte VK_ESCAPE = 0x1B;
        private const byte VK_DOWN = 0x28;
        private const int KEYEVENTF_KEYUP = 0x0002;

        public static void SendCtrlAltW()
        {
            NativeMethods.keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            NativeMethods.keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
            NativeMethods.keybd_event(VK_W, 0, 0, UIntPtr.Zero);
            NativeMethods.keybd_event(VK_W, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            NativeMethods.keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            NativeMethods.keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public static void SendCtrlV()
        {
            NativeMethods.keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            NativeMethods.keybd_event(VK_V, 0, 0, UIntPtr.Zero);
            NativeMethods.keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            NativeMethods.keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public static void SendEnter()
        {
            NativeMethods.keybd_event(VK_RETURN, 0, 0, UIntPtr.Zero);
            NativeMethods.keybd_event(VK_RETURN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public static void SendEscape()
        {
            NativeMethods.keybd_event(VK_ESCAPE, 0, 0, UIntPtr.Zero);
            NativeMethods.keybd_event(VK_ESCAPE, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public static void SendDown()
        {
            NativeMethods.keybd_event(VK_DOWN, 0, 0, UIntPtr.Zero);
            NativeMethods.keybd_event(VK_DOWN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public static void SendHotkey(string accelerator)
        {
            var parsed = ParseAccelerator(accelerator);
            foreach (var modifier in parsed.Modifiers)
            {
                NativeMethods.keybd_event(modifier, 0, 0, UIntPtr.Zero);
                Thread.Sleep(15);
            }
            NativeMethods.keybd_event(parsed.Key, 0, 0, UIntPtr.Zero);
            Thread.Sleep(30);
            NativeMethods.keybd_event(parsed.Key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            for (var i = parsed.Modifiers.Count - 1; i >= 0; i--)
            {
                NativeMethods.keybd_event(parsed.Modifiers[i], 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                Thread.Sleep(15);
            }
        }

        private static ParsedHotkey ParseAccelerator(string accelerator)
        {
            if (string.IsNullOrWhiteSpace(accelerator))
            {
                throw new ArgumentException("Hotkey is empty.");
            }

            var result = new ParsedHotkey();
            foreach (var rawToken in accelerator.Split('+'))
            {
                var token = rawToken.Trim();
                if (token.Length == 0)
                {
                    continue;
                }

                if (string.Equals(token, "Ctrl", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(token, "Control", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(token, "CmdOrCtrl", StringComparison.OrdinalIgnoreCase))
                {
                    result.Modifiers.Add(VK_CONTROL);
                }
                else if (string.Equals(token, "Alt", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(token, "Option", StringComparison.OrdinalIgnoreCase))
                {
                    result.Modifiers.Add(VK_MENU);
                }
                else if (string.Equals(token, "Shift", StringComparison.OrdinalIgnoreCase))
                {
                    result.Modifiers.Add(VK_SHIFT);
                }
                else
                {
                    result.Key = MapKey(token);
                }
            }

            if (result.Key == 0)
            {
                throw new ArgumentException("Hotkey has no key: " + accelerator);
            }

            return result;
        }

        private static byte MapKey(string token)
        {
            if (token.Length == 1)
            {
                var ch = char.ToUpperInvariant(token[0]);
                if (ch >= 'A' && ch <= 'Z')
                {
                    return (byte)ch;
                }
                if (ch >= '0' && ch <= '9')
                {
                    return (byte)ch;
                }
            }

            if (string.Equals(token, "Enter", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "Return", StringComparison.OrdinalIgnoreCase))
            {
                return VK_RETURN;
            }

            throw new ArgumentException("Unsupported hotkey key: " + token);
        }

        private sealed class ParsedHotkey
        {
            public readonly List<byte> Modifiers = new List<byte>();
            public byte Key;
        }
    }

    internal static class NativeMethods
    {
        public const int SW_HIDE = 0;
        public const int SW_SHOWNOACTIVATE = 4;
        public const uint GW_OWNER = 4;
        public const int VK_LBUTTON = 0x01;
        public const int MOUSEEVENTF_LEFTDOWN = 0x0002;
        public const int MOUSEEVENTF_LEFTUP = 0x0004;

        public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public INPUTUNION union;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct INPUTUNION
        {
            [FieldOffset(0)]
            public KEYBDINPUT keyboard;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, int dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder text, int count);

        public static string GetWindowText(IntPtr hWnd)
        {
            var text = new StringBuilder(512);
            GetWindowText(hWnd, text, text.Capacity);
            return text.ToString();
        }

        public static string GetClassName(IntPtr hWnd)
        {
            var text = new StringBuilder(256);
            GetClassName(hWnd, text, text.Capacity);
            return text.ToString();
        }

        public static INPUT CreateUnicodeInput(char ch, uint flags)
        {
            return new INPUT
            {
                type = 1,
                union = new INPUTUNION
                {
                    keyboard = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = ch,
                        dwFlags = 0x0004 | flags,
                        time = 0,
                        dwExtraInfo = UIntPtr.Zero
                    }
                }
            };
        }
    }

    internal sealed class HubSettings
    {
        public QqMusicLyricsSettings QqMusicLyrics = new QqMusicLyricsSettings();
        public CodexQuotaSettings CodexQuota = new CodexQuotaSettings();

        public static HubSettings Load(string path, Logger logger)
        {
            var settings = new HubSettings();
            if (!File.Exists(path))
            {
                settings.Save(path, logger);
                return settings;
            }

            try
            {
                foreach (var rawLine in File.ReadAllLines(path, Encoding.UTF8))
                {
                    var line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith("#"))
                    {
                        continue;
                    }

                    var parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length != 2)
                    {
                        continue;
                    }

                    var key = parts[0].Trim();
                    var value = parts[1].Trim();
                    if (string.Equals(key, "qqMusicLyrics.enabled", StringComparison.OrdinalIgnoreCase))
                    {
                        settings.QqMusicLyrics.Enabled = ParseBool(value, true);
                    }
                    else if (string.Equals(key, "qqMusicLyrics.pollIntervalMs", StringComparison.OrdinalIgnoreCase))
                    {
                        settings.QqMusicLyrics.PollIntervalMs = ParseInt(value, 800);
                    }
                    else if (string.Equals(key, "qqMusicLyrics.minActionGapMs", StringComparison.OrdinalIgnoreCase))
                    {
                        settings.QqMusicLyrics.MinActionGapMs = ParseInt(value, 1200);
                    }
                    else if (string.Equals(key, "codexQuota.url", StringComparison.OrdinalIgnoreCase))
                    {
                        settings.CodexQuota.Url = value.Length == 0 ? settings.CodexQuota.Url : value;
                    }
                    else if (string.Equals(key, "codexQuota.width", StringComparison.OrdinalIgnoreCase))
                    {
                        settings.CodexQuota.Width = ParseInt(value, 660);
                    }
                    else if (string.Equals(key, "codexQuota.height", StringComparison.OrdinalIgnoreCase))
                    {
                        settings.CodexQuota.Height = ParseInt(value, 360);
                    }
                    else if (string.Equals(key, "codexQuota.x", StringComparison.OrdinalIgnoreCase))
                    {
                        settings.CodexQuota.X = ParseInt(value, -1);
                    }
                    else if (string.Equals(key, "codexQuota.y", StringComparison.OrdinalIgnoreCase))
                    {
                        settings.CodexQuota.Y = ParseInt(value, -1);
                    }
                    else if (string.Equals(key, "codexQuota.openOnStartup", StringComparison.OrdinalIgnoreCase))
                    {
                        settings.CodexQuota.OpenOnStartup = ParseBool(value, false);
                    }
                    else if (string.Equals(key, "codexQuota.refreshIntervalMinutes", StringComparison.OrdinalIgnoreCase))
                    {
                        settings.CodexQuota.RefreshIntervalMinutes = ParseInt(value, 5);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error("Load settings failed: " + ex.Message);
            }

            return settings;
        }

        public void Save(string path, Logger logger)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var lines = new[]
                {
                    "# ScriptHub settings",
                    "qqMusicLyrics.enabled=" + QqMusicLyrics.Enabled.ToString().ToLowerInvariant(),
                    "qqMusicLyrics.pollIntervalMs=" + QqMusicLyrics.PollIntervalMs,
                    "qqMusicLyrics.minActionGapMs=" + QqMusicLyrics.MinActionGapMs,
                    "codexQuota.url=" + CodexQuota.Url,
                    "codexQuota.width=" + CodexQuota.Width,
                    "codexQuota.height=" + CodexQuota.Height,
                    "codexQuota.x=" + CodexQuota.X,
                    "codexQuota.y=" + CodexQuota.Y,
                    "codexQuota.openOnStartup=" + CodexQuota.OpenOnStartup.ToString().ToLowerInvariant(),
                    "codexQuota.refreshIntervalMinutes=" + CodexQuota.RefreshIntervalMinutes
                };
                File.WriteAllLines(path, lines, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                logger.Error("Save settings failed: " + ex.Message);
            }
        }

        private static bool ParseBool(string value, bool fallback)
        {
            bool parsed;
            return bool.TryParse(value, out parsed) ? parsed : fallback;
        }

        private static int ParseInt(string value, int fallback)
        {
            int parsed;
            return int.TryParse(value, out parsed) ? parsed : fallback;
        }
    }

    internal sealed class QqMusicLyricsSettings
    {
        public bool Enabled = true;
        public int PollIntervalMs = 800;
        public int MinActionGapMs = 1200;
    }

    internal sealed class CodexQuotaSettings
    {
        public string Url = "https://chatgpt.com/codex/cloud/settings/analytics#usage";
        public int Width = 660;
        public int Height = 360;
        public int X = -1;
        public int Y = -1;
        public bool OpenOnStartup = false;
        public int RefreshIntervalMinutes = 5;

        public void ApplyFrom(CodexQuotaSettings other)
        {
            if (other == null)
            {
                return;
            }

            Url = other.Url;
            Width = other.Width;
            Height = other.Height;
            X = other.X;
            Y = other.Y;
            OpenOnStartup = other.OpenOnStartup;
            RefreshIntervalMinutes = other.RefreshIntervalMinutes;
        }
    }

    internal sealed class AppPaths
    {
        public string ExePath;
        public string RootDir;
        public string ConfigDir;
        public string LogDir;
        public string ScriptsDir;
        public string ModulesDir;
        public string SettingsPath;

        public static AppPaths Discover()
        {
            var exePath = Assembly.GetExecutingAssembly().Location;
            var root = Path.GetDirectoryName(exePath);
            return new AppPaths
            {
                ExePath = exePath,
                RootDir = root,
                ConfigDir = Path.Combine(root, "config"),
                LogDir = Path.Combine(root, "logs"),
                ScriptsDir = Path.Combine(root, "scripts"),
                ModulesDir = Path.Combine(root, "modules"),
                SettingsPath = Path.Combine(root, "config", "settings.ini")
            };
        }
    }

    internal sealed class Logger
    {
        private readonly string _path;
        private readonly object _lock = new object();

        public Logger(string path)
        {
            _path = path;
        }

        public void Info(string message)
        {
            Write("INFO", message);
        }

        public void Error(string message)
        {
            Write("ERROR", message);
        }

        private void Write(string level, string message)
        {
            try
            {
                lock (_lock)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_path));
                    File.AppendAllText(_path, string.Format("{0:yyyy-MM-dd HH:mm:ss.fff} [{1}] {2}{3}", DateTime.Now, level, message, Environment.NewLine), Encoding.UTF8);
                }
            }
            catch
            {
            }
        }
    }

    internal static class TaskSchedulerHelper
    {
        private const string TaskName = "ScriptHub";
        private const string StartupScriptName = "ScriptHubStartup.vbs";

        public static bool IsEnabled()
        {
            if (File.Exists(StartupScriptPath))
            {
                return true;
            }

            var result = RunSchtasks("/Query /TN \"" + TaskName + "\" /FO LIST");
            return result.ExitCode == 0 && result.Output.IndexOf("Ready", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static void Enable(string exePath)
        {
            var command = "/Create /F /SC ONLOGON /DELAY 0000:10 /TN \"" + TaskName + "\" /TR \"\\\"" + exePath + "\\\"\"";
            var result = RunSchtasks(command);
            if (result.ExitCode != 0)
            {
                WriteStartupScript(exePath);
            }
        }

        public static void Disable()
        {
            var result = RunSchtasks("/Delete /F /TN \"" + TaskName + "\"");
            if (result.ExitCode != 0 && result.Output.IndexOf("cannot find", StringComparison.OrdinalIgnoreCase) < 0 && result.Output.IndexOf("找不到", StringComparison.OrdinalIgnoreCase) < 0)
            {
                // Keep going: this machine may not allow Task Scheduler writes, while the Startup fallback is still removable.
            }

            if (File.Exists(StartupScriptPath))
            {
                File.Delete(StartupScriptPath);
            }
        }

        private static string StartupScriptPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), StartupScriptName);
            }
        }

        private static void WriteStartupScript(string exePath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StartupScriptPath));
            var escapedPath = exePath.Replace("\"", "\"\"");
            var script =
                "Option Explicit\r\n" +
                "Dim shell\r\n" +
                "Set shell = CreateObject(\"WScript.Shell\")\r\n" +
                "WScript.Sleep 10000\r\n" +
                "shell.Run \"\"\"" + escapedPath + "\"\"\", 0, False\r\n";
            File.WriteAllText(StartupScriptPath, script, Encoding.ASCII);
        }

        private static ProcessResult RunSchtasks(string arguments)
        {
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.Default,
                    StandardErrorEncoding = Encoding.Default
                };
                process.Start();
                var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                process.WaitForExit();
                return new ProcessResult { ExitCode = process.ExitCode, Output = output };
            }
        }

        private sealed class ProcessResult
        {
            public int ExitCode;
            public string Output;
        }
    }
}

