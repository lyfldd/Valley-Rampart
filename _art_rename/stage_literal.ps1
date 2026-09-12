# 全字面路径逐个移动（无变量插值、无哈希）
$base = 'C:\Users\trs\Desktop\Valley Rampart\美术资源文件夹'
$stage = "$base\待填充文件夹"

$moves = @(
  @{ s = "$stage\Raider图片以及动画\待机.png";      d = "$base\Units\monster\raider\unit_monster_raider_idle.png" }
  @{ s = "$stage\Raider图片以及动画\移动.png";      d = "$base\Units\monster\raider\unit_monster_raider_walk.png" }
  @{ s = "$stage\Raider图片以及动画\攻击.png";      d = "$base\Units\monster\raider\unit_monster_raider_attack.png" }
  @{ s = "$stage\Raider图片以及动画\掠夺.png";      d = "$base\Units\monster\raider\unit_monster_raider_loot.png" }
  @{ s = "$stage\Raider图片以及动画\精灵图片.png";  d = "$base\Units\monster\raider\unit_monster_raider_icon.png" }
  @{ s = "$stage\Slinger图片以及动画\待机.png";     d = "$base\Units\monster\slinger\unit_monster_slinger_idle.png" }
  @{ s = "$stage\Slinger图片以及动画\移动.png";     d = "$base\Units\monster\slinger\unit_monster_slinger_walk.png" }
  @{ s = "$stage\Slinger图片以及动画\攻击.png";     d = "$base\Units\monster\slinger\unit_monster_slinger_attack.png" }
  @{ s = "$stage\Slinger图片以及动画\掠夺.png";     d = "$base\Units\monster\slinger\unit_monster_slinger_loot.png" }
  @{ s = "$stage\Slinger图片以及动画\精灵图片.png"; d = "$base\Units\monster\slinger\unit_monster_slinger_icon.png" }
  @{ s = "$stage\Brute图片以及动画\待机.png";       d = "$base\Units\monster\brute\unit_monster_brute_idle.png" }
  @{ s = "$stage\Brute图片以及动画\移动.png";       d = "$base\Units\monster\brute\unit_monster_brute_walk.png" }
  @{ s = "$stage\Brute图片以及动画\攻击.png";       d = "$base\Units\monster\brute\unit_monster_brute_attack.png" }
  @{ s = "$stage\Brute图片以及动画\掠夺.png";       d = "$base\Units\monster\brute\unit_monster_brute_loot.png" }
  @{ s = "$stage\Brute图片以及动画\精灵图片.png";   d = "$base\Units\monster\brute\unit_monster_brute_icon.png" }
)
foreach ($mt in $moves) {
    if (Test-Path -LiteralPath $mt.s) {
        $dd = Split-Path -Parent $mt.d
        if (-not (Test-Path -LiteralPath $dd)) { New-Item -ItemType Directory -Force -Path $dd | Out-Null }
        try {
            Move-Item -LiteralPath $mt.s -Destination $mt.d -Force -ErrorAction Stop
            Write-Host ("MOVE: {0} -> {1}" -f (Split-Path -Leaf $mt.s), (Split-Path $mt.d -Leaf))
        } catch { Write-Host ("FAIL: {0} :: {1}" -f $mt.s, $_.Exception.Message) }
    } else { Write-Host ("SRC-MISS: " + $mt.s) }
}
# 清空后处理
foreach ($sub in @('Raider图片以及动画','Slinger图片以及动画','Brute图片以及动画')) {
    $p = "$stage\$sub"
    if (Test-Path -LiteralPath $p) {
        $left = @(Get-ChildItem -LiteralPath $p -Force)
        if ($left.Count -eq 0) { Remove-Item -LiteralPath $p -Force; Write-Host ("RMDIR: " + $sub) }
        else { Write-Host ("NOTEMPTY: " + $sub + " 剩 " + ($left.Name -join ',')) }
    }
}
if (Test-Path -LiteralPath $stage) {
    $left = @(Get-ChildItem -LiteralPath $stage -Force)
    if ($left.Count -eq 0) { Rename-Item -LiteralPath $stage -NewName '_待整理'; Write-Host "RENAME: 待填充文件夹 -> _待整理" }
    else { Write-Host ("STAGE-NOTEMPTY: 剩 " + ($left | ForEach-Object { $_.Name }) -join ',') }
}
Write-Host "=== 入库验证 ==="
Get-ChildItem -LiteralPath "$base\Units\monster" -Recurse -File | ForEach-Object {
    Write-Host ("  " + $_.FullName.Substring($base.Length+1) + "  (" + [math]::Round($_.Length/1KB,1) + "KB)")
}