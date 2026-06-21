using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

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
                _codexQuotaForm = new CodexQuotaForm(_logger, _settings.CodexQuota);
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
        private readonly Logger _logger;
        private readonly CodexQuotaSettings _settings;
        private readonly CodexAppServerQuotaClient _quotaClient;
        private readonly System.Windows.Forms.Timer _refreshTimer;
        private readonly System.Windows.Forms.Timer _clockTimer;
        private readonly Label _clockLabel;
        private readonly Label _dateLabel;
        private readonly Label _statusLabel;
        private readonly QuotaCardControl _primaryCard;
        private readonly QuotaCardControl _secondaryCard;
        private bool _refreshInProgress;
        private DateTime? _lastSuccessfulRefresh;

        public CodexQuotaForm(Logger logger, CodexQuotaSettings settings)
        {
            _logger = logger;
            _settings = settings ?? new CodexQuotaSettings();
            _quotaClient = new CodexAppServerQuotaClient();

            Text = "Codex 额度小窗";
            Icon = SystemIcons.Application;
            BackColor = Color.White;
            ForeColor = Color.FromArgb(17, 24, 39);
            MinimumSize = new Size(640, 420);
            Size = new Size(Math.Max(640, _settings.Width), Math.Max(420, _settings.Height));
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            if (_settings.X >= 0 && _settings.Y >= 0)
            {
                StartPosition = FormStartPosition.Manual;
                Location = new Point(_settings.X, _settings.Y);
            }

            _clockLabel = new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI", 44f, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                TextAlign = ContentAlignment.MiddleCenter,
                UseCompatibleTextRendering = true
            };
            _dateLabel = new Label
            {
                AutoSize = false,
                Font = new Font("Microsoft YaHei UI", 17f, FontStyle.Regular),
                ForeColor = Color.FromArgb(100, 116, 139),
                TextAlign = ContentAlignment.MiddleCenter,
                UseCompatibleTextRendering = true
            };
            _primaryCard = new QuotaCardControl("5 小时使用额度");
            _secondaryCard = new QuotaCardControl("每周使用额度");
            _statusLabel = new Label
            {
                AutoSize = false,
                Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Regular),
                ForeColor = Color.FromArgb(100, 116, 139),
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "正在读取本机 Codex 额度...",
                UseCompatibleTextRendering = true
            };

            Controls.Add(_clockLabel);
            Controls.Add(_dateLabel);
            Controls.Add(_primaryCard);
            Controls.Add(_secondaryCard);
            Controls.Add(_statusLabel);

            var menu = new ContextMenuStrip();
            menu.Items.Add(new ToolStripMenuItem("立即刷新", null, (_, __) => RefreshQuota()));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("关闭窗口", null, (_, __) => Close()));
            ContextMenuStrip = menu;

            _refreshTimer = new System.Windows.Forms.Timer();
            _refreshTimer.Interval = Math.Max(1, _settings.RefreshIntervalMinutes) * 60 * 1000;
            _refreshTimer.Tick += (_, __) => RefreshQuota();
            _clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _clockTimer.Tick += (_, __) => UpdateClock();

            Load += (_, __) =>
            {
                UpdateClock();
                _clockTimer.Start();
                if (_settings.RefreshIntervalMinutes > 0)
                {
                    _refreshTimer.Start();
                }
                RefreshQuota();
            };
            Move += (_, __) => RememberBounds();
            Resize += (_, __) =>
            {
                RememberBounds();
                LayoutQuotaControls();
            };
            Shown += (_, __) => LayoutQuotaControls();
            KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.F5)
                {
                    RefreshQuota();
                    e.Handled = true;
                }
            };
            FormClosing += (_, __) => RememberBounds();
            FormClosed += (_, __) =>
            {
                _refreshTimer.Stop();
                _refreshTimer.Dispose();
                _clockTimer.Stop();
                _clockTimer.Dispose();
            };
        }

        private void LayoutQuotaControls()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
            {
                return;
            }

            _clockLabel.SetBounds(0, 46, ClientSize.Width, 58);
            _dateLabel.SetBounds(0, 109, ClientSize.Width, 30);

            const int cardHeight = 166;
            const int cardGap = 18;
            var cardWidth = Math.Min(320, Math.Max(220, (ClientSize.Width - 80 - cardGap) / 2));
            var totalWidth = cardWidth * 2 + cardGap;
            var left = Math.Max(20, (ClientSize.Width - totalWidth) / 2);
            var top = 194;
            _primaryCard.SetBounds(left, top, cardWidth, cardHeight);
            _secondaryCard.SetBounds(left + cardWidth + cardGap, top, cardWidth, cardHeight);
            _statusLabel.SetBounds(0, top + cardHeight + 17, ClientSize.Width, 24);
        }

        private void UpdateClock()
        {
            var now = DateTime.Now;
            _clockLabel.Text = now.ToString("HH:mm:ss");
            var weekdays = new[] { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" };
            _dateLabel.Text = now.Year + "年" + now.Month + "月" + now.Day + "日 " + weekdays[(int)now.DayOfWeek];
        }

        private async void RefreshQuota()
        {
            if (_refreshInProgress || IsDisposed)
            {
                return;
            }

            _refreshInProgress = true;
            if (!_lastSuccessfulRefresh.HasValue)
            {
                _statusLabel.ForeColor = Color.FromArgb(100, 116, 139);
                _statusLabel.Text = "正在读取本机 Codex 额度...";
            }

            try
            {
                var snapshot = await Task.Run(() => _quotaClient.ReadQuota());
                if (IsDisposed)
                {
                    return;
                }

                _primaryCard.UpdateQuota(snapshot.Primary);
                _secondaryCard.UpdateQuota(snapshot.Secondary);
                _lastSuccessfulRefresh = DateTime.Now;
                _statusLabel.ForeColor = Color.FromArgb(100, 116, 139);
                _statusLabel.Text = "本机 Codex 数据 · 更新于 " + _lastSuccessfulRefresh.Value.ToString("HH:mm:ss");
                _logger.Info("Codex quota refreshed via app-server. Plan=" + (snapshot.PlanType ?? "unknown"));
            }
            catch (Exception ex)
            {
                _logger.Error("Read Codex quota via app-server failed: " + ex);
                _statusLabel.ForeColor = Color.FromArgb(185, 28, 28);
                _statusLabel.Text = _lastSuccessfulRefresh.HasValue
                    ? "读取失败，保留上次数据 · " + _lastSuccessfulRefresh.Value.ToString("HH:mm:ss")
                    : "读取失败，请确认 Codex 桌面端已登录";
            }
            finally
            {
                _refreshInProgress = false;
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
    }

    internal sealed class QuotaCardControl : Control
    {
        private readonly string _title;
        private int? _remainingPercent;
        private DateTime? _resetsAt;

        public QuotaCardControl(string title)
        {
            _title = title;
            BackColor = Color.White;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        public void UpdateQuota(CodexRateLimitWindow window)
        {
            _remainingPercent = window == null ? (int?)null : Math.Max(0, Math.Min(100, 100 - window.UsedPercent));
            _resetsAt = window == null ? (DateTime?)null : window.ResetsAt;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var cardBounds = new Rectangle(1, 1, Math.Max(1, Width - 2), Math.Max(1, Height - 2));
            using (var path = CreateRoundedRectangle(cardBounds, 16))
            using (var border = new Pen(Color.FromArgb(229, 231, 235)))
            using (var fill = new SolidBrush(Color.White))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            }

            const int padding = 26;
            using (var titleFont = new Font("Microsoft YaHei UI", 11f, FontStyle.Regular))
            using (var valueFont = new Font("Segoe UI", 25f, FontStyle.Bold))
            using (var suffixFont = new Font("Microsoft YaHei UI", 15f, FontStyle.Regular))
            using (var resetFont = new Font("Microsoft YaHei UI", 10f, FontStyle.Regular))
            {
                TextRenderer.DrawText(e.Graphics, _title, titleFont,
                    new Rectangle(padding, 24, Width - padding * 2, 22), Color.FromArgb(100, 116, 139),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

                var number = _remainingPercent.HasValue ? _remainingPercent.Value + "%" : "--";
                var numberSize = TextRenderer.MeasureText(e.Graphics, number, valueFont, Size.Empty, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(e.Graphics, number, valueFont,
                    new Rectangle(padding, 49, numberSize.Width + 4, 38), Color.FromArgb(15, 23, 42),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                TextRenderer.DrawText(e.Graphics, "剩余", suffixFont,
                    new Rectangle(padding + numberSize.Width + 8, 57, 62, 25), Color.FromArgb(15, 23, 42),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

                var track = new Rectangle(padding, 102, Math.Max(1, Width - padding * 2), 12);
                using (var trackPath = CreateRoundedRectangle(track, 6))
                using (var trackBrush = new SolidBrush(Color.FromArgb(229, 231, 235)))
                {
                    e.Graphics.FillPath(trackBrush, trackPath);
                }

                if (_remainingPercent.HasValue)
                {
                    var fillWidth = Math.Max(4, (int)Math.Round(track.Width * (_remainingPercent.Value / 100.0)));
                    var fill = new Rectangle(track.X, track.Y, Math.Min(track.Width, fillWidth), track.Height);
                    using (var fillPath = CreateRoundedRectangle(fill, 6))
                    using (var fillBrush = new SolidBrush(ProgressColorFor(_remainingPercent.Value)))
                    {
                        e.Graphics.FillPath(fillBrush, fillPath);
                    }
                }

                var reset = "重置时间: " + FormatResetTime(_resetsAt);
                TextRenderer.DrawText(e.Graphics, reset, resetFont,
                    new Rectangle(padding, 132, Width - padding * 2, 23), Color.FromArgb(100, 116, 139),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            }
        }

        internal static Color ProgressColorFor(int remainingPercent)
        {
            if (remainingPercent >= 60)
            {
                return Color.FromArgb(34, 197, 94);
            }
            if (remainingPercent >= 30)
            {
                return Color.FromArgb(245, 158, 11);
            }
            return Color.FromArgb(239, 68, 68);
        }

        private static string FormatResetTime(DateTime? resetAt)
        {
            if (!resetAt.HasValue)
            {
                return "读取中";
            }

            var now = DateTime.Now;
            return resetAt.Value.Date == now.Date
                ? resetAt.Value.ToString("HH:mm")
                : resetAt.Value.Year + "年" + resetAt.Value.Month + "月" + resetAt.Value.Day + "日 " + resetAt.Value.ToString("HH:mm");
        }

        private static GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            var diameter = Math.Max(1, radius * 2);
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class CodexAppServerQuotaClient
    {
        private const int InitializeTimeoutMs = 10000;
        private const int ReadTimeoutMs = 15000;

        public CodexQuotaSnapshot ReadQuota()
        {
            var executable = FindCodexExecutable();
            if (string.IsNullOrWhiteSpace(executable))
            {
                throw new InvalidOperationException("未找到本机 Codex CLI。请确认 Codex 桌面端已安装。");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = "app-server --listen stdio://",
                WorkingDirectory = Path.GetDirectoryName(executable),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var process = new Process { StartInfo = startInfo })
            {
                process.Start();
                var errorTask = process.StandardError.ReadToEndAsync();
                var serializer = new JavaScriptSerializer();
                try
                {
                    Send(process, serializer, new Dictionary<string, object>
                    {
                        { "jsonrpc", "2.0" },
                        { "id", 1 },
                        { "method", "initialize" },
                        { "params", new Dictionary<string, object>
                            {
                                { "clientInfo", new Dictionary<string, object>
                                    {
                                        { "name", "ScriptHub" },
                                        { "version", "1.0" }
                                    }
                                },
                                { "capabilities", new Dictionary<string, object>() }
                            }
                        }
                    });
                    ReadResponse(process, serializer, 1, InitializeTimeoutMs);

                    Send(process, serializer, new Dictionary<string, object>
                    {
                        { "jsonrpc", "2.0" },
                        { "method", "initialized" }
                    });
                    Send(process, serializer, new Dictionary<string, object>
                    {
                        { "jsonrpc", "2.0" },
                        { "id", 2 },
                        { "method", "account/rateLimits/read" },
                        { "params", null }
                    });

                    var response = ReadResponse(process, serializer, 2, ReadTimeoutMs);
                    return ParseSnapshot(response);
                }
                catch (Exception ex)
                {
                    var stderr = StopProcess(process, errorTask);
                    if (!string.IsNullOrWhiteSpace(stderr))
                    {
                        throw new InvalidOperationException(ex.Message + " " + stderr.Trim(), ex);
                    }
                    throw;
                }
                finally
                {
                    StopProcess(process, errorTask);
                }
            }
        }

        private static void Send(Process process, JavaScriptSerializer serializer, IDictionary<string, object> message)
        {
            process.StandardInput.WriteLine(serializer.Serialize(message));
            process.StandardInput.Flush();
        }

        private static IDictionary<string, object> ReadResponse(Process process, JavaScriptSerializer serializer, int id, int timeoutMs)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                var remaining = Math.Max(1, (int)(deadline - DateTime.UtcNow).TotalMilliseconds);
                var lineTask = process.StandardOutput.ReadLineAsync();
                if (!lineTask.Wait(remaining))
                {
                    break;
                }

                var line = lineTask.Result;
                if (line == null)
                {
                    break;
                }
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var message = serializer.DeserializeObject(line) as IDictionary<string, object>;
                if (message == null || !MatchesId(message, id))
                {
                    continue;
                }

                object error;
                if (message.TryGetValue("error", out error) && error != null)
                {
                    throw new InvalidOperationException("Codex app-server 返回错误: " + ReadErrorMessage(error));
                }
                return message;
            }

            throw new TimeoutException("等待 Codex 额度数据超时。");
        }

        private static CodexQuotaSnapshot ParseSnapshot(IDictionary<string, object> response)
        {
            var result = GetDictionary(response, "result");
            var rateLimits = GetDictionary(result, "rateLimits");
            var byLimitId = GetDictionary(result, "rateLimitsByLimitId");
            if (byLimitId != null)
            {
                var codexBucket = GetDictionary(byLimitId, "codex");
                if (codexBucket != null)
                {
                    rateLimits = codexBucket;
                }
            }
            if (rateLimits == null)
            {
                throw new InvalidOperationException("Codex app-server 未返回额度数据。");
            }

            return new CodexQuotaSnapshot
            {
                PlanType = GetString(rateLimits, "planType"),
                Primary = ParseWindow(GetDictionary(rateLimits, "primary")),
                Secondary = ParseWindow(GetDictionary(rateLimits, "secondary"))
            };
        }

        private static CodexRateLimitWindow ParseWindow(IDictionary<string, object> data)
        {
            if (data == null)
            {
                return null;
            }

            var resetsAt = GetInt64(data, "resetsAt");
            return new CodexRateLimitWindow
            {
                UsedPercent = GetInt32(data, "usedPercent") ?? 0,
                WindowDurationMinutes = GetInt64(data, "windowDurationMins"),
                ResetsAt = resetsAt.HasValue
                    ? DateTimeOffset.FromUnixTimeSeconds(resetsAt.Value).ToLocalTime().DateTime
                    : (DateTime?)null
            };
        }

        private static string FindCodexExecutable()
        {
            var candidates = new List<string>();
            var configured = Environment.GetEnvironmentVariable("SCRIPTHUB_CODEX_EXE");
            if (!string.IsNullOrWhiteSpace(configured))
            {
                candidates.Add(configured);
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appBin = Path.Combine(localAppData, "OpenAI", "Codex", "bin");
            candidates.Add(Path.Combine(appBin, "codex.exe"));
            try
            {
                candidates.AddRange(Directory.GetFiles(appBin, "codex.exe", SearchOption.AllDirectories));
            }
            catch
            {
            }

            var codexHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
            candidates.Add(Path.Combine(codexHome, "plugins", ".plugin-appserver", "codex.exe"));
            candidates.Add(Path.Combine(codexHome, ".sandbox-bin", "codex.exe"));

            return candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
        }

        private static string StopProcess(Process process, Task<string> errorTask)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.StandardInput.Close();
                    if (!process.WaitForExit(1200))
                    {
                        process.Kill();
                        process.WaitForExit();
                    }
                }

                if (errorTask != null && errorTask.Wait(300))
                {
                    return errorTask.Result;
                }
            }
            catch
            {
            }
            return string.Empty;
        }

        private static bool MatchesId(IDictionary<string, object> message, int id)
        {
            object value;
            return message.TryGetValue("id", out value) && value != null && Convert.ToString(value) == id.ToString();
        }

        private static IDictionary<string, object> GetDictionary(IDictionary<string, object> source, string key)
        {
            if (source == null)
            {
                return null;
            }
            object value;
            return source.TryGetValue(key, out value) ? value as IDictionary<string, object> : null;
        }

        private static string GetString(IDictionary<string, object> source, string key)
        {
            if (source == null)
            {
                return null;
            }
            object value;
            return source.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : null;
        }

        private static int? GetInt32(IDictionary<string, object> source, string key)
        {
            var value = GetInt64(source, key);
            return value.HasValue ? (int?)Math.Max(int.MinValue, Math.Min(int.MaxValue, value.Value)) : null;
        }

        private static long? GetInt64(IDictionary<string, object> source, string key)
        {
            if (source == null)
            {
                return null;
            }
            object value;
            if (!source.TryGetValue(key, out value) || value == null)
            {
                return null;
            }

            long parsed;
            return long.TryParse(Convert.ToString(value), out parsed) ? (long?)parsed : null;
        }

        private static string ReadErrorMessage(object error)
        {
            var dictionary = error as IDictionary<string, object>;
            return dictionary == null ? Convert.ToString(error) : GetString(dictionary, "message") ?? Convert.ToString(error);
        }
    }

    internal sealed class CodexQuotaSnapshot
    {
        public string PlanType;
        public CodexRateLimitWindow Primary;
        public CodexRateLimitWindow Secondary;
    }

    internal sealed class CodexRateLimitWindow
    {
        public int UsedPercent;
        public long? WindowDurationMinutes;
        public DateTime? ResetsAt;
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
        public int Width = 1094;
        public int Height = 496;
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

