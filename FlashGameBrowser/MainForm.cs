using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using CefSharp;
using CefSharp.WinForms;

namespace FlashGameBrowser
{
    public partial class MainForm : Form
    {
        // === 控件 ===
        private ChromiumWebBrowser _browser;
        private ToolStrip _toolStrip;
        private ToolStripButton _backBtn, _forwardBtn, _refreshBtn, _homeBtn, _goBtn;
        private ToolStripTextBox _urlBox;
        private ToolStripDropDownButton _favBtn;
        private ToolStripButton _cookieBtn;
        private StatusStrip _statusStrip;
        private ToolStripStatusLabel _statusLabel;
        private ToolStripProgressBar _progressBar;

        // === 状态 ===
        private bool _isFullscreen = false;
        private FormBorderStyle _savedBorderStyle;
        private bool _savedToolStripVisible;
        private bool _savedStatusStripVisible;

        private const string HOME_URL = "http://www.4399.com/flash/32979.htm";
        private const string FLASH_TEST_URL = "https://get.adobe.com/flashplayer/about/";

        public MainForm()
        {
            InitializeComponent();
            InitializeBrowser();
            WireUpEvents();
        }

        // ============================================================
        //  UI 构建
        // ============================================================
        private void InitializeComponent()
        {
            this.SuspendLayout();

            // ---- 窗口 ----
            this.Text = "Flash 游戏浏览器";
            this.Size = new System.Drawing.Size(1280, 800);
            this.MinimumSize = new System.Drawing.Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            // 尝试加载图标
            try
            {
                string iconPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(iconPath))
                    this.Icon = new System.Drawing.Icon(iconPath);
            }
            catch { }

            // ---- 工具栏 ----
            _toolStrip = new ToolStrip
            {
                GripStyle = ToolStripGripStyle.Hidden,
                Dock = DockStyle.Top,
            };

            _backBtn = new ToolStripButton("◀")
            {
                ToolTipText = "后退 (Alt+←)",
                Enabled = false,
            };
            _backBtn.Click += (s, e) => _browser?.Back();

            _forwardBtn = new ToolStripButton("▶")
            {
                ToolTipText = "前进 (Alt+→)",
                Enabled = false,
            };
            _forwardBtn.Click += (s, e) => _browser?.Forward();

            _refreshBtn = new ToolStripButton("⟳")
            {
                ToolTipText = "刷新 (F5)",
            };
            _refreshBtn.Click += (s, e) => _browser?.Reload();

            _homeBtn = new ToolStripButton("⌂")
            {
                ToolTipText = $"主页 - {HOME_URL}",
            };
            _homeBtn.Click += (s, e) => Navigate(HOME_URL);

            _urlBox = new ToolStripTextBox
            {
                Size = new System.Drawing.Size(450, 25),
                AutoCompleteMode = AutoCompleteMode.Suggest,
                AutoCompleteSource = AutoCompleteSource.AllUrl,
            };
            _urlBox.KeyDown += OnUrlKeyDown;

            _goBtn = new ToolStripButton("→")
            {
                ToolTipText = "转到",
            };
            _goBtn.Click += (s, e) => Navigate(_urlBox.Text);

            // 收藏夹
            _favBtn = new ToolStripDropDownButton("★ 收藏")
            {
                ToolTipText = "收藏的网站",
            };
            _favBtn.DropDownItems.Add("🎮 4399 小游戏", null,
                (s, e) => Navigate("https://www.4399.com"));
            _favBtn.DropDownItems.Add("🎯 7k7k 小游戏", null,
                (s, e) => Navigate("https://www.7k7k.com"));
            _favBtn.DropDownItems.Add(new ToolStripSeparator());
            _favBtn.DropDownItems.Add("📌 添加当前页面", null,
                (s, e) => AddCurrentToFavorites());
            _favBtn.DropDownItems.Add("🔧 管理收藏...", null,
                (s, e) => OpenFavoritesFile());

            // Cookie 按钮 (靠右)
            _cookieBtn = new ToolStripButton("🍪 Cookie")
            {
                ToolTipText = "打开 Cookie 文件夹（可拷贝到其他电脑迁移进度）",
                Alignment = ToolStripItemAlignment.Right,
            };
            _cookieBtn.Click += (s, e) => OpenCookieFolder();

            _toolStrip.Items.AddRange(new ToolStripItem[]
            {
                _backBtn, _forwardBtn, _refreshBtn, _homeBtn,
                _urlBox, _goBtn,
                new ToolStripSeparator(),
                _favBtn,
                _cookieBtn,
            });

            // ---- 状态栏 ----
            _statusStrip = new StatusStrip
            {
                Dock = DockStyle.Bottom,
            };
            _statusLabel = new ToolStripStatusLabel("就绪");
            _progressBar = new ToolStripProgressBar
            {
                Style = ProgressBarStyle.Continuous,
                Width = 100,
                Visible = false,
            };
            _statusStrip.Items.Add(_progressBar);
            _statusStrip.Items.Add(_statusLabel);

            // ---- 添加到 Form ----
            this.Controls.Add(_toolStrip);
            this.Controls.Add(_statusStrip);

            // ---- 快捷键 ----
            this.KeyPreview = true;
            this.KeyDown += OnFormKeyDown;

            // ---- 事件 ----
            this.FormClosing += OnFormClosing;
            this.Load += OnFormLoad;
            this.Resize += OnFormResize;

            this.ResumeLayout(false);
            this.PerformLayout();
        }

        // ============================================================
        //  CEF 浏览器初始化
        // ============================================================
        private void InitializeBrowser()
        {
            _browser = new ChromiumWebBrowser(HOME_URL)
            {
                Dock = DockStyle.Fill,
            };

            // 把浏览器插入到工具栏和状态栏之间
            // ToolStrip Dock=Top, StatusStrip Dock=Bottom, Browser Dock=Fill 会自动正确排列
            this.Controls.Add(_browser);
            _browser.BringToFront(); // 确保浏览器在最前面

            // 设置浏览器事件处理器
            _browser.LifeSpanHandler = new BrowserLifeSpanHandler(this);
            _browser.DisplayHandler = new BrowserDisplayHandler(this);
            _browser.MenuHandler = new BrowserMenuHandler();
            _browser.RequestHandler = new BrowserRequestHandler();
        }

        private void WireUpEvents()
        {
            if (_browser == null) return;

            // Flash 检测修复：必须在页面脚本执行前注入
            // 4399 通过 document.write 加载 flashopen_cpp.js 并立即轮询 checkflash()
            // FrameLoadEnd 太晚，必须用 FrameLoadStart 提前覆盖
            string flashPatchJs = LoadEmbeddedScript("FlashGameBrowser.FlashPatch.js");
            if (!string.IsNullOrEmpty(flashPatchJs))
            {
                // 所有 frame（包括游戏 iframe）都注入 Flash 修复脚本
                _browser.FrameLoadStart += (s, e) =>
                {
                    e.Frame.ExecuteJavaScriptAsync(flashPatchJs);
                };
                _browser.FrameLoadEnd += (s, e) =>
                {
                    e.Frame.ExecuteJavaScriptAsync(flashPatchJs);
                };
            }

            // 地址栏同步
            _browser.AddressChanged += (s, e) =>
            {
                this.BeginInvoke(new Action(() =>
                {
                    if (!_urlBox.Focused)
                        _urlBox.Text = e.Address;
                }));
            };

            // 标题同步
            _browser.TitleChanged += (s, e) =>
            {
                this.BeginInvoke(new Action(() =>
                {
                    this.Text = string.IsNullOrEmpty(e.Title)
                        ? "Flash 游戏浏览器"
                        : $"{e.Title} - Flash 游戏浏览器";
                }));
            };

            // 加载状态
            _browser.LoadingStateChanged += (s, e) =>
            {
                this.BeginInvoke(new Action(() =>
                {
                    _refreshBtn.Text = e.IsLoading ? "✕" : "⟳";
                    _refreshBtn.ToolTipText = e.IsLoading ? "停止" : "刷新 (F5)";

                    if (e.IsLoading)
                    {
                        _statusLabel.Text = "正在加载...";
                        _progressBar.Visible = true;
                        _progressBar.Style = ProgressBarStyle.Marquee;
                    }
                    else
                    {
                        _statusLabel.Text = "完成";
                        _progressBar.Visible = false;
                        _progressBar.Style = ProgressBarStyle.Continuous;
                    }

                    UpdateNavButtons();
                }));
            };

            // 加载进度
            _browser.LoadError += (s, e) =>
            {
                this.BeginInvoke(new Action(() =>
                {
                    _statusLabel.Text = $"加载失败: {e.ErrorText}";
                    _progressBar.Visible = false;
                }));
            };
        }

        // ============================================================
        //  导航方法
        // ============================================================
        private void Navigate(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            url = url.Trim();

            // 如果不是 URL，作为搜索词处理
            if (!url.Contains(".") && !url.Contains("://"))
            {
                url = "https://www.baidu.com/s?wd=" + Uri.EscapeDataString(url);
            }
            else if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "https://" + url;
            }

            _urlBox.Text = url;
            _browser?.Load(url);
        }

        private void UpdateNavButtons()
        {
            if (_browser == null) return;

            _backBtn.Enabled = _browser.CanGoBack;
            _forwardBtn.Enabled = _browser.CanGoForward;
        }

        // ============================================================
        //  事件处理
        // ============================================================
        private void OnUrlKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                Navigate(_urlBox.Text);
            }
        }

        private void OnFormKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.F5:
                    _browser?.Reload();
                    e.Handled = true;
                    break;
                case Keys.F6:
                case Keys.L when e.Control:
                    _urlBox.Focus();
                    _urlBox.SelectAll();
                    e.Handled = true;
                    break;
                case Keys.F11:
                    ToggleFullscreen();
                    e.Handled = true;
                    break;
                case Keys.Escape:
                    if (_isFullscreen)
                    {
                        ToggleFullscreen();
                        e.Handled = true;
                    }
                    break;
                case Keys.BrowserBack:
                case Keys.Left when e.Alt:
                    _browser?.Back();
                    e.Handled = true;
                    break;
                case Keys.BrowserForward:
                case Keys.Right when e.Alt:
                    _browser?.Forward();
                    e.Handled = true;
                    break;
                case Keys.Home when e.Alt:
                    Navigate(HOME_URL);
                    e.Handled = true;
                    break;
            }
        }

        private void OnFormLoad(object sender, EventArgs e)
        {
            _urlBox.Text = HOME_URL;
            UpdateNavButtons();
            LoadFavoritesFromFile();

            // 检查 Flash 状态
            this.BeginInvoke(new Action(() =>
            {
                string flashPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "Plugins", "pepflashplayer.dll");
                if (File.Exists(flashPath))
                {
                    _statusLabel.Text = "Flash 插件已就绪 ✅ | 可以玩 Flash 游戏了";
                }
                else
                {
                    _statusLabel.Text = "⚠ Flash 插件未找到（Plugins/pepflashplayer.dll）| Flash 游戏可能无法运行";
                }
            }));
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_browser != null)
            {
                _browser.Dispose();
                _browser = null;
            }
        }

        private void OnFormResize(object sender, EventArgs e)
        {
            // 确保工具栏项正确排列
            _toolStrip?.PerformLayout();
        }

        // ============================================================
        //  全屏切换
        // ============================================================
        public void ToggleFullscreen()
        {
            if (_isFullscreen)
            {
                // 退出全屏
                this.FormBorderStyle = _savedBorderStyle;
                this.WindowState = FormWindowState.Normal;
                _toolStrip.Visible = _savedToolStripVisible;
                _statusStrip.Visible = _savedStatusStripVisible;
                _isFullscreen = false;
            }
            else
            {
                // 进入全屏
                _savedBorderStyle = this.FormBorderStyle;
                _savedToolStripVisible = _toolStrip.Visible;
                _savedStatusStripVisible = _statusStrip.Visible;

                this.FormBorderStyle = FormBorderStyle.None;
                this.WindowState = FormWindowState.Maximized;
                _toolStrip.Visible = false;
                _statusStrip.Visible = false;
                _isFullscreen = true;
            }
        }

        // ============================================================
        //  收藏夹
        // ============================================================
        private void AddCurrentToFavorites()
        {
            string url = _urlBox.Text;
            string title = this.Text.Replace(" - Flash 游戏浏览器", "");

            if (string.IsNullOrWhiteSpace(url) || url == HOME_URL)
                return;

            // 避免重复
            foreach (ToolStripItem item in _favBtn.DropDownItems)
            {
                if (item.Tag != null && item.Tag.ToString() == url)
                {
                    MessageBox.Show("该页面已在收藏夹中。", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }

            // 截取合适的标题长度
            if (title.Length > 30)
                title = title.Substring(0, 30) + "...";

            var newItem = new ToolStripMenuItem($"🎮 {title}")
            {
                Tag = url,
                ToolTipText = url,
            };
            newItem.Click += (s, e) => Navigate(url);

            // 在"管理收藏"之前插入
            int insertPos = _favBtn.DropDownItems.Count - 1; // 在"管理收藏"之前
            _favBtn.DropDownItems.Insert(insertPos >= 0 ? insertPos : 0, newItem);

            // 同时保存到文件
            SaveFavoriteToFile(title, url);

            _statusLabel.Text = $"已添加到收藏: {title}";
        }

        private void SaveFavoriteToFile(string title, string url)
        {
            try
            {
                string favPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "UserData", "favorites.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(favPath)!);
                File.AppendAllText(favPath, $"{title}|{url}{Environment.NewLine}");
            }
            catch { /* 保存失败不影响使用 */ }
        }

        private void LoadFavoritesFromFile()
        {
            try
            {
                string favPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "UserData", "favorites.txt");
                if (!File.Exists(favPath)) return;

                string[] lines = File.ReadAllLines(favPath);
                foreach (string line in lines)
                {
                    string[] parts = line.Split('|');
                    if (parts.Length >= 2)
                    {
                        string title = parts[0];
                        string url = parts[1];

                        var item = new ToolStripMenuItem($"🎮 {title}")
                        {
                            Tag = url,
                            ToolTipText = url,
                        };
                        item.Click += (s, e) => Navigate(url);

                        // 插入到"管理收藏"之前
                        int insertPos = _favBtn.DropDownItems.Count - 1;
                        _favBtn.DropDownItems.Insert(insertPos >= 0 ? insertPos : 0, item);
                    }
                }
            }
            catch { /* 加载失败不影响使用 */ }
        }

        private void OpenFavoritesFile()
        {
            try
            {
                string favPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "UserData", "favorites.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(favPath)!);

                if (!File.Exists(favPath))
                    File.WriteAllText(favPath,
                        "# Flash 游戏浏览器 - 收藏夹" + Environment.NewLine +
                        "# 格式: 标题|URL" + Environment.NewLine +
                        "# 删除某行即可移除收藏" + Environment.NewLine);

                Process.Start("notepad.exe", favPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开收藏文件失败: {ex.Message}",
                    "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ============================================================
        //  Cookie 文件夹
        // ============================================================
        private void OpenCookieFolder()
        {
            try
            {
                string userDataPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "UserData");
                Directory.CreateDirectory(userDataPath);
                Process.Start("explorer.exe", userDataPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开 Cookie 文件夹失败: {ex.Message}",
                    "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ============================================================
        //  公开方法（供 Handler 调用）
        // ============================================================
        public void InvokeNavigate(string url)
        {
            if (this.InvokeRequired)
                this.BeginInvoke(new Action(() => Navigate(url)));
            else
                Navigate(url);
        }

        public void InvokeToggleFullscreen()
        {
            if (this.InvokeRequired)
                this.BeginInvoke(new Action(ToggleFullscreen));
            else
                ToggleFullscreen();
        }

        /// <summary>
        /// 从嵌入资源加载脚本文件
        /// </summary>
        private static string LoadEmbeddedScript(string resourceName)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null) return null;
                    using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
            catch
            {
                return null;
            }
        }
    }

    // ============================================================
    //  Handler: 生命周期（处理弹窗）
    // ============================================================
    public class BrowserLifeSpanHandler : ILifeSpanHandler
    {
        private readonly MainForm _form;

        public BrowserLifeSpanHandler(MainForm form)
        {
            _form = form;
        }

        public bool OnBeforePopup(IWebBrowser chromiumWebBrowser, IBrowser browser,
            IFrame frame, string targetUrl, string targetFrameName,
            WindowOpenDisposition targetDisposition, bool userGesture,
            IPopupFeatures popupFeatures, IWindowInfo windowInfo,
            IBrowserSettings browserSettings, ref bool noJavascriptAccess,
            out IWebBrowser newBrowser)
        {
            // 弹窗在当前窗口打开，不创建新窗口
            _form.InvokeNavigate(targetUrl);
            newBrowser = null;
            return true; // true = 取消默认的弹窗创建
        }

        public void OnAfterCreated(IWebBrowser chromiumWebBrowser, IBrowser browser) { }
        public bool DoClose(IWebBrowser chromiumWebBrowser, IBrowser browser) => false;
        public void OnBeforeClose(IWebBrowser chromiumWebBrowser, IBrowser browser) { }
    }

    // ============================================================
    //  Handler: 显示（全屏处理）
    // ============================================================
    public class BrowserDisplayHandler : IDisplayHandler
    {
        private readonly MainForm _form;

        public BrowserDisplayHandler(MainForm form)
        {
            _form = form;
        }

        public void OnAddressChanged(IWebBrowser chromiumWebBrowser,
            AddressChangedEventArgs addressChangedArgs) { }

        public void OnTitleChanged(IWebBrowser chromiumWebBrowser,
            TitleChangedEventArgs titleChangedArgs) { }

        public void OnFaviconUrlChange(IWebBrowser chromiumWebBrowser,
            IBrowser browser, System.Collections.Generic.IList<string> urls) { }

        public void OnFullscreenModeChange(IWebBrowser chromiumWebBrowser,
            IBrowser browser, bool fullscreen)
        {
            _form.InvokeToggleFullscreen();
        }

        public bool OnTooltipChanged(IWebBrowser chromiumWebBrowser,
            ref string text) => false;

        public bool OnConsoleMessage(IWebBrowser chromiumWebBrowser,
            ConsoleMessageEventArgs e)
        {
            // 捕获网页控制台日志写入文件
            try
            {
                string logPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "UserData", "page_console.log");
                string level = "LOG";
                if ((int)e.Level == 0) level = "ERROR";
                else if ((int)e.Level == 1) level = "WARN";
                else if ((int)e.Level == 2) level = "INFO";
                File.AppendAllText(logPath,
                    $"[{DateTime.Now:HH:mm:ss.fff}][{level}] {e.Source}:{e.Line} {e.Message}{Environment.NewLine}");
            }
            catch { }
            return false;
        }

        public bool OnAutoResize(IWebBrowser chromiumWebBrowser,
            IBrowser browser, CefSharp.Structs.Size newSize) => false;

        public void OnLoadingProgressChange(IWebBrowser chromiumWebBrowser,
            IBrowser browser, double progress) { }

        public void OnStatusMessage(IWebBrowser chromiumWebBrowser,
            StatusMessageEventArgs statusMessageArgs) { }

        public bool OnCursorChange(IWebBrowser chromiumWebBrowser,
            IBrowser browser, IntPtr cursorHandle) => false;
    }

    // ============================================================
    //  Handler: 菜单（禁用右键菜单）
    // ============================================================
    public class BrowserMenuHandler : IContextMenuHandler
    {
        public void OnBeforeContextMenu(IWebBrowser chromiumWebBrowser,
            IBrowser browser, IFrame frame, IContextMenuParams parameters,
            IMenuModel model)
        {
            // 清空右键菜单
            model.Clear();
        }

        public bool OnContextMenuCommand(IWebBrowser chromiumWebBrowser,
            IBrowser browser, IFrame frame, IContextMenuParams parameters,
            CefMenuCommand commandId, CefEventFlags eventFlags) => false;

        public void OnContextMenuDismissed(IWebBrowser chromiumWebBrowser,
            IBrowser browser, IFrame frame) { }

        public bool RunContextMenu(IWebBrowser chromiumWebBrowser,
            IBrowser browser, IFrame frame, IContextMenuParams parameters,
            IMenuModel model, IRunContextMenuCallback callback) => false;
    }

    // ============================================================
    //  Handler: 请求（处理 Flash 相关内容）
    // ============================================================
    public class BrowserRequestHandler : CefSharp.Handler.RequestHandler
    {
        protected override bool OnBeforeBrowse(IWebBrowser chromiumWebBrowser,
            IBrowser browser, IFrame frame, IRequest request,
            bool userGesture, bool isRedirect)
        {
            return false; // 允许所有请求
        }

        protected override bool OnCertificateError(IWebBrowser chromiumWebBrowser,
            IBrowser browser, CefErrorCode errorCode, string requestUrl,
            ISslInfo sslInfo, IRequestCallback callback)
        {
            // 忽略证书错误（老网站常有问题）
            callback.Continue(true);
            return true;
        }
    }
}
