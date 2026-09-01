#requires -Version 7.0
<#
像素工坊启动器（3.1.4）
用法：
  pwsh start.ps1           # 启动本地服务 + opencode + 打开编辑器
  pwsh start.ps1 -Init      # 首次使用：导入 Assets 单位/地块图到 workspace，然后启动
停止：关闭弹出的两个服务窗口。
#>
param(
    [switch]$Init,
    [int]$Port = 5173,
    [int]$OcPort = 4096
)
$ErrorActionPreference = 'Stop'
$toolRoot = $PSScriptRoot

Write-Host "== 像素工坊 pixel-forge ==" -ForegroundColor Cyan

try { $nv = node --version } catch { throw "未找到 node（需 Node 18+）。请先安装：https://nodejs.org" }
Write-Host "node $nv"

if ($Init) {
    Write-Host "-- 初始化工作区（导入 Assets 图 + 建台账）..."
    node (Join-Path $toolRoot 'server/init.mjs')
}

$serveScript = Join-Path $toolRoot 'server/serve.mjs'
Write-Host "-- 启动本地服务 http://localhost:$Port ..."
Start-Process pwsh -ArgumentList @('-NoExit', '-Command', "node `"$serveScript`" --port $Port")

$healthy = $false
foreach ($i in 1..30) {
    try { $null = Invoke-RestMethod "http://127.0.0.1:$Port/api/health" -TimeoutSec 1; $healthy = $true; break } catch { Start-Sleep -Milliseconds 400 }
}
if ($healthy) { Write-Host "本地服务就绪 :$Port" -ForegroundColor Green }
else { Write-Warning "本地服务 12 秒内未就绪，请查看服务窗口日志" }

$ocOk = $false
try { $ocOk = [bool](Invoke-RestMethod "http://127.0.0.1:$OcPort/global/health" -TimeoutSec 2).healthy } catch {}
if (-not $ocOk) {
    Write-Host "-- opencode serve 未运行，尝试启动（端口 $OcPort）..."
    Start-Process pwsh -ArgumentList @('-NoExit', '-Command', "opencode serve --port $OcPort --hostname 127.0.0.1")
    foreach ($i in 1..30) {
        try {
            $ocOk = [bool](Invoke-RestMethod "http://127.0.0.1:$OcPort/global/health" -TimeoutSec 1).healthy
            if ($ocOk) { break }
        } catch { Start-Sleep -Milliseconds 500 }
    }
}
if ($ocOk) { Write-Host "opencode serve 就绪 :$OcPort" -ForegroundColor Green }
else { Write-Warning "opencode 未就绪：编辑器可手绘/保存，AI 面板不可用。可手动运行：opencode serve --port $OcPort" }

Start-Process "http://localhost:$Port"
Write-Host "== 完成。关闭两个服务窗口即停止。 =="
