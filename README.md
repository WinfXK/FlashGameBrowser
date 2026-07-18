# Flash 游戏浏览器 - FlashGameBrowser

基于 Chromium 85 内核 + Flash PPAPI 插件的 Windows 桌面浏览器，专门用于玩 4399、7k7k 等 Flash 网页小游戏。

## ✨ 特性

- ✅ **内置 Flash 支持** — 通过 PPAPI Flash 插件运行 Flash 游戏
- ✅ **无广告无捆绑** — 纯净开源代码，0 "狠活"
- ✅ **便携式 Cookie** — Cookie/游戏进度存储在 `UserData/` 目录，可直接拷贝到其他电脑
- ✅ **收藏夹功能** — 自带 4399/7k7k 入口，支持自定义收藏
- ✅ **全屏支持** — F11 切换全屏，Flash 游戏内全屏也能正常工作
- ✅ **键盘快捷键** — 支持 F5 刷新、Ctrl+L 地址栏、Alt+←→ 前进后退等

## 📋 系统要求

- Windows 10/11 (64位)
- .NET Framework 4.8（Win10/11 已内置）
- Visual C++ Redistributable 2015+ (x64)

## 🚀 快速开始

### 1. 编译项目

```powershell
# 使用 .NET SDK 编译
dotnet build -c Release
```

### 2. 安装 Flash 插件

下载 `pepflashplayer.dll` (PPAPI版本，v34.0.0.267) 放入 `Plugins\` 目录。

详见：[Plugins/README.txt](FlashGameBrowser/Plugins/README.txt)

### 3. 运行

```
bin\x64\Release\net48\FlashGameBrowser.exe
```

## 📂 目录结构

```
FlashGameBrowser/
├── FlashGameBrowser.exe         # 主程序
├── Plugins/
│   └── pepflashplayer.dll       # Flash PPAPI 插件（需手动下载）
├── UserData/                    # 用户数据（运行时自动创建）
│   ├── Cookies                  # Cookie 数据库
│   ├── Local Storage/           # 网页本地存储
│   ├── Cache/                   # 浏览器缓存
│   └── favorites.txt            # 收藏夹
└── *.dll                        # Chromium 运行库
```

### 🍪 Cookie 迁移

直接将 `UserData/` 目录复制到其他电脑的相同位置，游戏进度即可同步。

## ⌨️ 快捷键

| 快捷键 | 功能 |
|--------|------|
| F5 | 刷新页面 |
| F11 | 全屏切换 |
| Esc | 退出全屏 |
| Ctrl+L / F6 | 聚焦地址栏 |
| Alt+← | 后退 |
| Alt+→ | 前进 |
| Alt+Home | 打开主页 (4399) |

## 🔧 技术栈

- CefSharp v85.3.130 (Chromium 85 + Flash PPAPI 支持)
- .NET Framework 4.8 / C# WinForms
- Windows 10/11 x64

## ⚠ 安全说明

**本浏览器仅供玩网页小游戏使用。** 由于使用旧版 Chromium (v85)，存在已知安全漏洞。请勿用于：
- 访问银行/支付网站
- 登录重要账号
- 处理敏感信息

## 📝 许可

本项目代码自由使用。CefSharp 和 Chromium 遵循各自的 BSD 许可。
