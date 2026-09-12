# _待整理 入库：地块5 + 飞行物/命中特效 13（法师弹幕暂留）
$base = 'C:\Users\trs\Desktop\Valley Rampart\美术资源文件夹'
$stage = "$base\_待整理"

$moves = @(
  # 地块 → Ground/
  @{ s = "$stage\地块\寒带地块.png";       d = "$base\Ground\ground_cold.png" }
  @{ s = "$stage\地块\河流地块.png";       d = "$base\Ground\ground_river.png" }
  @{ s = "$stage\地块\热带地块.png";       d = "$base\Ground\ground_tropical.png" }
  @{ s = "$stage\地块\温带地块.png";       d = "$base\Ground\ground_temperate.png" }
  @{ s = "$stage\地块\亚热带地块.png";     d = "$base\Ground\ground_subtropical.png" }
  # 飞行物 → Ammo/
  @{ s = "$stage\飞行物以及动画\弓箭手弩手通用箭矢.png";    d = "$base\Ammo\ammo_arrow.png" }
  @{ s = "$stage\飞行物以及动画\人类战争机器专用箭矢.png";  d = "$base\Ammo\ammo_heavybolt.png" }
  @{ s = "$stage\飞行物以及动画\石弹.png";                  d = "$base\Ammo\ammo_stone.png" }
  @{ s = "$stage\飞行物以及动画\火焰弹.png";                d = "$base\Ammo\ammo_fireball.png" }
  @{ s = "$stage\飞行物以及动画\魔法蛋.png";                d = "$base\Ammo\ammo_magic.png" }
  # 命中特效 → Effects/hit/
  @{ s = "$stage\飞行物以及动画\石弹命中动画.png";          d = "$base\Effects\hit\fx_hit_stone.png" }
  @{ s = "$stage\飞行物以及动画\火焰弹命中动画.png";        d = "$base\Effects\hit\fx_hit_fireball.png" }
  @{ s = "$stage\飞行物以及动画\魔法蛋命中动画.png";        d = "$base\Effects\hit\fx_hit_magic.png" }
)
foreach ($mt in $moves) {
    if (Test-Path -LiteralPath $mt.s) {
        $dd = Split-Path -Parent $mt.d
        if (-not (Test-Path -LiteralPath $dd)) { New-Item -ItemType Directory -Force -Path $dd | Out-Null }
        try {
            Move-Item -LiteralPath $mt.s -Destination $mt.d -Force -ErrorAction Stop
            Write-Host ("MOVE: {0} -> {1}" -f (Split-Path -Leaf $mt.s), $mt.d.Substring($base.Length+1))
        } catch { Write-Host ("FAIL: {0} :: {1}" -f $mt.s, $_.Exception.Message) }
    } else { Write-Host ("SRC-MISS: " + $mt.s) }
}
Write-Host "=== _待整理 剩余 ==="
Get-ChildItem -LiteralPath $stage -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object { Write-Host ("  " + $_.FullName.Substring($stage.Length+1)) }
Write-Host "=== 入库验证 ==="
foreach ($d in @('Ground','Ammo','Effects')) {
    $p = "$base\$d"
    Write-Host "  [$d]"
    Get-ChildItem -LiteralPath $p -Recurse -File | ForEach-Object { Write-Host ("    " + $_.FullName.Substring($base.Length+1) + "  (" + [math]::Round($_.Length/1KB,1) + "KB)") }
}