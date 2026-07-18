# Flash 游戏浏览器 - 编译脚本
# 在 PowerShell 中运行: .\build.ps1

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Flash 游戏浏览器 - 编译脚本" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$ProjectDir = Join-Path $PSScriptRoot "FlashGameBrowser"
$OutputDir = Join-Path $ProjectDir "bin\x64\Release\net48"

# 检查 .NET SDK
Write-Host "[1/3] 检查 .NET SDK..." -ForegroundColor Yellow
try {
    dotnet --version | Out-Null
    Write-Host "  .NET SDK 版本: $(dotnet --version)" -ForegroundColor Green
} catch {
    Write-Host "  错误: 未找到 .NET SDK，请先安装: https://dotnet.microsoft.com/download" -ForegroundColor Red
    exit 1
}

# 编译
Write-Host "[2/3] 编译项目 (Release x64)..." -ForegroundColor Yellow
Push-Location $ProjectDir
try {
    dotnet build -c Release 2>&1 | Select-String -Pattern "error|成功|失败|error CS" | ForEach-Object { Write-Host "  $_" }
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  编译失败！请检查错误信息。" -ForegroundColor Red
        exit 1
    }
    Write-Host "  编译成功！" -ForegroundColor Green
} finally {
    Pop-Location
}

# 检查输出
Write-Host "[3/3] 检查输出..." -ForegroundColor Yellow
$ExePath = Join-Path $OutputDir "FlashGameBrowser.exe"
if (Test-Path $ExePath) {
    Write-Host "  输出目录: $OutputDir" -ForegroundColor Green
    Write-Host "  主程序: $ExePath" -ForegroundColor Green

    # 检查 Flash 插件
    $FlashPath = Join-Path $ProjectDir "Plugins\pepflashplayer.dll"
    $FlashOutputPath = Join-Path $OutputDir "Plugins\pepflashplayer.dll"
    if (Test-Path $FlashPath) {
        Write-Host "  Flash 插件: 已就绪 ✅" -ForegroundColor Green
    } else {
        Write-Host "  Flash 插件: 未找到 ⚠" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "  请将 pepflashplayer.dll 放在以下位置之一:" -ForegroundColor Yellow
        Write-Host "    1. $FlashPath" -ForegroundColor Yellow
        Write-Host "    2. $FlashOutputPath" -ForegroundColor Yellow
        Write-Host "  下载说明: 见 Plugins\README.txt" -ForegroundColor Yellow
    }
} else {
    Write-Host "  错误: 输出文件未找到" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  编译完成！运行:" -ForegroundColor Cyan
Write-Host "  $ExePath" -ForegroundColor White
Write-Host "========================================" -ForegroundColor Cyan
