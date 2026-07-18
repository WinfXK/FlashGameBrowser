using System;
using System.IO;
using System.Windows.Forms;
using CefSharp;
using CefSharp.WinForms;

namespace FlashGameBrowser
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 初始化 CEF（必须在创建任何浏览器控件之前）
            try
            {
                InitializeCef();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"CEF 初始化失败: {ex.Message}\n\n请确保:\n" +
                    "1. 已安装 Visual C++ Redistributable (vc_redist.x64.exe)\n" +
                    "2. 网络连接正常（首次运行需下载 CEF 组件）",
                    "初始化错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            // 启动主窗口，退出后清理 CEF
            try
            {
                Application.Run(new MainForm());
            }
            finally
            {
                Cef.Shutdown();
            }
        }

        static void InitializeCef()
        {
            var settings = new CefSettings();

            // === Cookie / 用户数据路径 ===
            string userDataPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "UserData");
            settings.CachePath = userDataPath;

            // 明确指定子进程路径（CEF 渲染进程）
            settings.BrowserSubprocessPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "CefSharp.BrowserSubprocess.exe");

            // === Flash PPAPI 插件 ===
            string flashPath = FindFlashPlugin();
            if (!string.IsNullOrEmpty(flashPath))
            {
                settings.CefCommandLineArgs.Add("ppapi-flash-path", flashPath);
                settings.CefCommandLineArgs.Add("ppapi-flash-version", "34.0.0.330");
                // 确保插件始终运行（不弹"点击运行"）
                settings.CefCommandLineArgs.Add("always-authorize-plugins", "1");
                // 启用 PPAPI 插件（Chromium 85 默认开启，这里显式确保）
                settings.CefCommandLineArgs.Add("enable-plugins", "1");
            }

            // === 其他设置 ===
            settings.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                "AppleWebKit/537.36 (KHTML, like Gecko) " +
                "Chrome/85.0.4183.121 Safari/537.36";

            // 允许运行不安全的上下文（部分老游戏页面是 HTTP）
            settings.CefCommandLineArgs.Add("allow-running-insecure-content", "1");
            settings.CefCommandLineArgs.Add("disable-web-security", "0"); // 保持基本安全

            // 忽略证书错误（部分老网站证书过期）
            settings.IgnoreCertificateErrors = true;

            // 语言设置
            settings.Locale = "zh-CN";
            settings.AcceptLanguageList = "zh-CN,zh;q=0.9,en;q=0.8";

            // 日志（诊断 Flash 问题时启用）
            settings.LogSeverity = LogSeverity.Verbose;
            settings.LogFile = Path.Combine(userDataPath, "cef_debug.log");

            // 初始化
            Cef.Initialize(settings);
        }

        /// <summary>
        /// 查找 pepflashplayer.dll 的路径
        /// </summary>
        static string FindFlashPlugin()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            string[] searchPaths = {
                // 项目自带的 Plugins 目录（开发时）
                Path.Combine(baseDir, "Plugins", "pepflashplayer.dll"),
                // 输出目录根目录
                Path.Combine(baseDir, "pepflashplayer.dll"),
                // 系统目录（可能有人手动安装过）
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "FlashPlayer", "pepflashplayer.dll"),
                // 上一层 Plugins（发布目录结构）
                Path.Combine(
                    Directory.GetParent(baseDir)?.FullName ?? baseDir,
                    "Plugins", "pepflashplayer.dll"),
            };

            foreach (string path in searchPaths)
            {
                if (File.Exists(path))
                    return path;
            }

            return null;
        }
    }
}
