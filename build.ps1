# 打卡助手 · 构建脚本
#
# 用 Windows 自带的 .NET Framework 编译器 csc.exe 编译，
# 不需要安装 .NET SDK、Visual Studio、Node 或 Python。
#
# 用法：双击 build.cmd，或在此目录执行
#       powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$srcDir = Join-Path $root 'src'

# ---- 定位编译器 ----
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) {
    Write-Host '[错误] 找不到 csc.exe（需要 .NET Framework 4.x，Windows 自带）。' -ForegroundColor Red
    exit 1
}

# ---- 源文件清单（显式列出，避免通配符误收）----
$names = @(
    'Model.cs'
    'Stats.cs'
    'Theme.cs'
    'Storage.cs'
    'MoodPicker.cs'
    'HabitListPanel.cs'
    'StatsStrip.cs'
    'HeatmapPanel.cs'
    'Dialogs.cs'
    'SettingsDialog.cs'
    'FolderOpener.cs'
    'MainForm.cs'
    'Program.cs'
    'SelfTest.cs'
)

$sources = @()
foreach ($n in $names) {
    $p = Join-Path $srcDir $n
    if (-not (Test-Path $p)) {
        Write-Host "[错误] 缺少源文件：$p" -ForegroundColor Red
        exit 1
    }
    $sources += $p
}

$refs = @(
    '/r:System.dll'
    '/r:System.Drawing.dll'
    '/r:System.Windows.Forms.dll'
)

# 说明：
#   /codepage:65001  源文件按 UTF-8 解析，保证中文字面量正确
#   /warnaserror+    任何警告都视为失败，避免留下隐患
#   /warnaserror-:... 目前没有需要豁免的告警
$common = @(
    '/nologo'
    '/codepage:65001'
    '/utf8output'
    '/optimize+'
    '/warnaserror+'
    '/platform:anycpu'
)

$appOut = Join-Path $root '打卡助手.exe'
$testOut = Join-Path $root '打卡助手-测试.exe'
$manifest = Join-Path $root 'app.manifest'

# ---- 主程序 ----
Write-Host '正在编译 打卡助手.exe ...' -ForegroundColor Cyan
& $csc @common '/target:winexe' "/out:$appOut" "/win32manifest:$manifest" @refs @sources
if ($LASTEXITCODE -ne 0) {
    Write-Host '[错误] 主程序编译失败。' -ForegroundColor Red
    exit $LASTEXITCODE
}

# ---- 自测程序 ----
Write-Host '正在编译 打卡助手-测试.exe ...' -ForegroundColor Cyan
& $csc @common '/target:exe' '/define:SELFTEST' "/out:$testOut" @refs @sources
if ($LASTEXITCODE -ne 0) {
    Write-Host '[错误] 自测程序编译失败。' -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host ''
Write-Host '构建完成。' -ForegroundColor Green
foreach ($f in @($appOut, $testOut)) {
    $item = Get-Item $f
    Write-Host ('  {0}  ({1:N0} 字节)' -f $item.Name, $item.Length)
}
Write-Host ''
Write-Host '下一步：双击「打卡助手.exe」即可使用。' -ForegroundColor Green
exit 0
