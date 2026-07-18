# FlashGameBrowser - 项目文档

## 项目简介

基于 **Chromium 85 + Clean Flash PPAPI** 的 Windows 桌面浏览器，专为运行 4399/7k7k 等网页 Flash 小游戏设计。纯净无广告，便携式设计，Cookie 可跨电脑迁移。

## 技术栈

| 组件 | 版本 | 说明 |
|------|------|------|
| .NET | Framework 4.8 (net48) | WinForms 桌面应用 |
| CefSharp | 85.3.130 | Chromium Embedded Framework 封装 |
| Chromium | 85.0.4183 | 最后一个完整支持 PPAPI Flash 的版本 |
| Flash | Clean Flash v34.0.0.330 | 基于 Adobe 中国版但移除了所有广告组件 |

## 目录结构

```
FlashGameBrowser/
├── CLAUDE.md                         # 本文档
├── README.md                         # 使用说明
├── build.ps1                         # 一键编译脚本
├── .gitignore
└── FlashGameBrowser/                 # 主项目
    ├── FlashGameBrowser.csproj       # .NET 4.8 WinForms x64
    ├── Program.cs                    # 入口 + CEF/Flash 初始化
    ├── MainForm.cs                   # 浏览器窗口全部逻辑
    ├── FlashPatch.js                 # Flash 检测修复脚本（嵌入资源）
    ├── app.ico                       # 应用程序图标
    ├── app.manifest                  # 高 DPI 感知清单
    └── Plugins/
        ├── pepflashplayer.dll        # Flash PPAPI 插件 (16MB)
        ├── manifest.json             # Flash 插件元数据
        └── README.txt                # Flash 插件说明
```

## 游戏加载流程

针对 `http://www.4399.com/flash/32979.htm`（洛克王国）的完整流程：

```
1. 4399 页面加载 → flashopen_cpp.js 轮询检测 Flash
   ↓ (通过 <embed> 的 checkflash() 判断)
2. 检测通过 → iframe 加载 17roco.qq.com/default.html
   ↓
3. Tencent 页面 document.write 创建 <embed src="ROCO-Z8.swf">
   ↓
4. Flash 插件渲染 SWF → ExternalInterface 回调 flashInit()
   ↓
5. 游戏运行（需要 4399 账号登录）
```

## 关键修复

### 1. Flash DLL 来源
- 从 Clean Flash Installer (GitLab: `cleanflash/installer`) 提取
- 用 7-Zip 解压 EXE，取 `pp64/pepflashplayer64_34_0_0_330.dll`
- 需配套 `manifest.json` — Chromium 用它识别插件元数据

### 2. 4399 Flash 检测绕过 (`HTMLEmbedElement.prototype.checkflash`)
- 4399 通过 `document.write` 创建 test `<embed>` 并轮询 `checkflash()`
- PPAPI Flash 首次初始化 >1 秒，轮询在 1 秒后放弃
- 修复：在 `HTMLEmbedElement.prototype` 预定义 `checkflash` 方法

### 3. Tencent 游戏页面 (`flashdown` 重定向拦截)
- Tencent 页面等待 SWF 的 `flashInit()` ExternalInterface 回调
- 11 秒后若未收到 → `flashdown()` → 重定向到 `/cgi-bin/login` (404)
- 修复：全局设置 `flashready=1` + 拦截 `confirm` 对话框

### 4. JS 注入时机
- `FrameLoadStart` 注入（页面脚本执行前）+ `FrameLoadEnd` 补注
- 所有 frame（含 iframe）都注入

## CEF 关键配置

```csharp
// Program.cs - InitializeCef()
settings.CachePath = "UserData";           // Cookie 持久化
settings.CefCommandLineArgs["ppapi-flash-path"] = flashPath;
settings.CefCommandLineArgs["ppapi-flash-version"] = "34.0.0.330";
settings.CefCommandLineArgs["enable-plugins"] = "1";
settings.CefCommandLineArgs["always-authorize-plugins"] = "1";
```

## 构建

```powershell
# 需求: .NET SDK 9.0+ (支持 net48 目标框架)
dotnet build -c Release
# 输出: bin\x64\Release\net48\FlashGameBrowser.exe

# 或使用脚本
.\build.ps1
```

## 部署

拷贝整个 `bin\x64\Release\net48\` 目录到目标电脑，运行 `FlashGameBrowser.exe`。

- 目录大小 ~213MB（主要是 libcef.dll 133MB + pepflashplayer.dll 16MB）
- Cookie/进度存储在 `UserData\` 目录，拷贝即迁移

## 已知问题

1. **企业网络 DNS 过滤**：某些公司网络（Cloudflare Family DNS）会拦截 `17roco.qq.com` 等游戏域名。需配置代理（Clash 全局模式或添加 qq.com 规则）。

2. **需要 4399 账户**：洛克王国是网游，需要在 4399 登录账号才能存档。

3. **CefSharp v85 安全漏洞**：旧版 Chromium 有已知 CVE，仅供游戏使用，勿处理敏感信息。

4. **vc_redist 依赖**：目标电脑需安装 Visual C++ 2015+ Redistributable (x64)。

5. **`--always-authorize-plugins` 无效**：这是 Chrome 专有 flag，CEF 不支持。但 CEF 默认不启用 click-to-play，所以无影响。

## 调试

```csharp
// Program.cs - 开启详细日志
settings.LogSeverity = LogSeverity.Verbose;
settings.LogFile = "UserData/cef_debug.log";
```

网页控制台日志会自动输出到 `UserData/page_console.log`。

## 更新 Flash 插件

1. 下载新版 CleanFlashInstaller.exe (GitLab Releases)
2. `7za.exe x CleanFlashInstaller.exe -o./ pp64`
3. 重命名 `pepflashplayer64_x_x_x_x.dll` → `pepflashplayer.dll`
4. 同时更新 `manifest.json`
5. 修改 `Program.cs` 中的 `ppapi-flash-version`

## Git 仓库

- Remote: `https://github.com/WinfXK/FlashGameBrowser`
- Flash DLL (16MB) 在 Git 中，无需额外下载
