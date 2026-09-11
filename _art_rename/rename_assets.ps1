#Requires -Version 7
param([switch]$Execute)

$ErrorActionPreference = 'Stop'
$root = 'C:\Users\trs\Desktop\Valley Rampart\美术资源文件夹'

$raceDir = @{ '人类' = 'human'; '兽人' = 'orc'; '矮人' = 'dwarf'; '精灵' = 'elf' }

$occMap = @{
  '普通士兵' = 'warrior'; '普通战士' = 'warrior'; '战士' = 'warrior'
  '弓箭手'   = 'archer';  '弩手'     = 'crossbowman'
  '法师'     = 'mage';    '治疗师'   = 'healer';      '盾卫' = 'shieldguard'
  '将军'     = 'general'; '骑兵'     = 'knight';      '居民' = 'resident'
  '工人'     = 'worker';  '流浪汉'   = 'vagrant';     '小孩' = 'child'; '儿童' = 'child'
  '狂战士'   = 'berserker'; '狼骑兵' = 'wolfrider';   '火枪手' = 'musqueteer'
  '磐石卫士' = 'bedrock'; '游侠'     = 'ranger';      '风行者' = 'windwalker'; '鹿骑' = 'deerrider'
}

$lvlCn = @{ '零级' = 0; '一级' = 1; '二级' = 2; '三级' = 3; '四级' = 4; '五级' = 5; '六级' = 6 }

$items = [System.Collections.Generic.List[object]]::new()
function Add-Item([string]$src, [string]$dst) {
  $items.Add([pscustomobject]@{ Source = $src; Target = $dst })
}
function Join-Root([string]$rel) { Join-Path $root $rel }

function Get-State([string]$name) {
  if ($name -match '待机') { return 'idle' }
  if ($name -match '走路|行走') { return 'walk' }
  if ($name -match '奔跑') { return 'run' }
  if ($name -match '攻击|射击|战斗|发射|治疗|建造|交互|冲锋') { return 'attack' }
  return $null
}

# ---- (1) 四族人种立绘 -> Portraits/{race}/unit_{race}_{occ}.png
foreach ($race in $raceDir.Keys) {
  $d = Join-Root "$race\${race}人种"
  if (-not (Test-Path $d)) { continue }
  foreach ($f in Get-ChildItem $d -File) {
    $occKey = $f.BaseName -replace "^$race", ''
    $occ = $occMap[$occKey]
    if (-not $occ) { throw "未识别职业（立绘）: $($f.Name)" }
    $r = $raceDir[$race]
    Add-Item $f.FullName (Join-Root "Portraits\$r\unit_${r}_${occ}.png")
  }
}

# ---- (2) 单位动画 -> Units/{race}/{occ}/unit_{race}_{occ}_{state}.png
foreach ($race in $raceDir.Keys) {
  $d = Join-Root "$race\${race}动画"
  if (-not (Test-Path $d)) { continue }
  foreach ($roleDir in Get-ChildItem $d -Directory) {
    $occKey = $roleDir.Name -replace "^$race", ''
    $occ = $occMap[$occKey]
    if (-not $occ) { throw "未识别职业（动画目录）: $($roleDir.Name)" }
    $r = $raceDir[$race]
    foreach ($f in Get-ChildItem $roleDir.FullName -File) {
      $st = Get-State $f.BaseName
      if (-not $st) { throw "未识别状态: $($f.Name)" }
      Add-Item $f.FullName (Join-Root "Units\$r\$occ\unit_${r}_${occ}_$st.png")
    }
  }
}

# ---- (3) 主城 -> Buildings/{race}/castle/castle_{race}_lv{n}.png
foreach ($race in $raceDir.Keys) {
  $d = Join-Root "$race\建筑"
  if (-not (Test-Path $d)) { continue }
  foreach ($f in (Get-ChildItem $d -File | Where-Object { $_.Name -notmatch '特殊建筑' })) {
    $n = $f.BaseName
    $lvl = $null
    foreach ($k in $lvlCn.Keys) { if ($n -match $k) { $lvl = $lvlCn[$k]; break } }
    if ($null -eq $lvl -and $n -match '^(\d)') { $lvl = [int]$Matches[1] }
    if ($null -eq $lvl) { throw "未识别主城等级: $($f.Name)" }
    $r = $raceDir[$race]
    Add-Item $f.FullName (Join-Root "Buildings\$r\castle\castle_${r}_lv$lvl.png")
  }
}

# ---- (4) 族特殊建筑 -> Buildings/{race}/exclusive/building_{race}_{excl}.png
$exclMap = @{ human = 'waracademy'; orc = 'waracademy'; dwarf = 'leyforge'; elf = 'archery' }
foreach ($race in $raceDir.Keys) {
  $f = Join-Root "$race\建筑\${race}特殊建筑.png"
  if (-not (Test-Path $f)) { continue }
  $r = $raceDir[$race]
  Add-Item $f (Join-Root "Buildings\$r\exclusive\building_${r}_$($exclMap[$r]).png")
}

# ---- (5) 战争机器 -> Machines/machine_{race}_{machine}.png
$machMap = @{ '人类' = @('human', 'ballista'); '兽人' = @('orc', 'ram'); '矮人' = @('dwarf', 'mortar'); '精灵' = @('elf', 'vinecatapult') }
foreach ($race in $machMap.Keys) {
  $r = $machMap[$race][0]; $m = $machMap[$race][1]
  $f1 = Join-Root "四族战争机器\${race}战争机器_已抠图.png"
  $f2 = Join-Root "四族战争机器\${race}战争机器动画_已抠图.png"
  if (Test-Path $f1) { Add-Item $f1 (Join-Root "Machines\machine_${r}_$m.png") }
  if (Test-Path $f2) { Add-Item $f2 (Join-Root "Machines\machine_${r}_${m}_strip.png") }
}

# ---- (6) 普通建筑（有级）-> Buildings/neutral/building_{id}_lv{n}.png
$nb = @{
  '仓库' = 'warehouse'; '房屋' = 'house'; '教堂' = 'church'; '练兵场' = 'barracks'
  '牧场' = 'ranch'; '农场' = 'farm'; '市场' = 'market'; '水井' = 'well'
  '铁匠铺' = 'blacksmith'; '医院' = 'hospital'; '战争机器工坊' = 'siegeworkshop'
}
foreach ($k in $nb.Keys) {
  $d = Join-Root "普通建筑\$k"
  if (-not (Test-Path $d)) { continue }
  foreach ($f in Get-ChildItem $d -File) {
    Add-Item $f.FullName (Join-Root "Buildings\neutral\building_$($nb[$k])_lv$($f.BaseName).png")
  }
}

# ---- (6') 矿洞：矿场建筑 3 张 + 矿山锚点 1 张
$dMine = Join-Root '普通建筑\矿洞'
if (Test-Path $dMine) {
  foreach ($f in Get-ChildItem $dMine -File) {
    if ($f.BaseName -match '^\d+$') {
      Add-Item $f.FullName (Join-Root "Buildings\neutral\building_mine_lv$($f.BaseName).png")
    }
    else {
      Add-Item $f.FullName (Join-Root 'Buildings\neutral\features\feat_mine.png')
    }
  }
}

# ---- (6'') 单张普通建筑
Add-Item (Join-Root '普通建筑\城门，桥\城门.png') (Join-Root 'Buildings\neutral\building_gate_closed.png')
Add-Item (Join-Root '普通建筑\城门，桥\桥.png')   (Join-Root 'Buildings\neutral\building_bridge.png')
Add-Item (Join-Root '普通建筑\传送门\传送门.png') (Join-Root 'Buildings\neutral\building_portal.png')
Add-Item (Join-Root '普通建筑\三个塔\箭塔.png')   (Join-Root 'Buildings\neutral\building_arrowtower.png')
Add-Item (Join-Root '普通建筑\三个塔\弩塔.png')   (Join-Root 'Buildings\neutral\building_crossbowtower.png')
Add-Item (Join-Root '普通建筑\三个塔\魔法塔.png') (Join-Root 'Buildings\neutral\building_magictower.png')
Add-Item (Join-Root '普通建筑\脚手架\1乘1.png')   (Join-Root 'Buildings\neutral\building_scaffold_1x1.png')
Add-Item (Join-Root '普通建筑\脚手架\2乘2.png')   (Join-Root 'Buildings\neutral\building_scaffold_2x2.png')
Add-Item (Join-Root '普通建筑\脚手架\3乘3.png')   (Join-Root 'Buildings\neutral\building_scaffold_3x3.png')

# ---- (6''') 城墙段 -> building_wall_seg{01..11}.png
$dWall = Join-Root '普通建筑\十一个完整转弯结构城墙'
if (Test-Path $dWall) {
  $i = 1
  foreach ($f in (Get-ChildItem $dWall -File | Sort-Object Name)) {
    Add-Item $f.FullName ('{0}\Buildings\neutral\building_wall_seg{1:D2}.png' -f $root, $i)
    $i++
  }
}

# ---- (7) 自然/资源 -> Buildings/neutral/features/feat_{id}.png
Add-Item (Join-Root '普通建筑\矿脉石堆枯木\矿脉.png')   (Join-Root 'Buildings\neutral\features\feat_orevein.png')
Add-Item (Join-Root '普通建筑\矿脉石堆枯木\石堆.png')   (Join-Root 'Buildings\neutral\features\feat_stone_pile.png')
Add-Item (Join-Root '普通建筑\矿脉石堆枯木\枯木.png')   (Join-Root 'Buildings\neutral\features\feat_deadwood.png')
Add-Item (Join-Root '普通建筑\流浪汉营地\流浪汉营地.png') (Join-Root 'Buildings\neutral\building_vagrant_camp.png')

$treeMap = @{ '热带' = 'tropical'; '亚热带' = 'subtropical'; '温带' = 'temperate'; '寒带' = 'cold' }
$dTree = Join-Root '普通建筑\树木'
if (Test-Path $dTree) {
  foreach ($f in Get-ChildItem $dTree -File) {
    foreach ($k in $treeMap.Keys) {
      if ($f.BaseName -match "^$k(\d)$") {
        Add-Item $f.FullName (Join-Root "Buildings\neutral\features\feat_tree_$($treeMap[$k])_$($Matches[1]).png")
        break
      }
    }
  }
}

# ---- 校验：覆盖面 + 重名 ----
$all = Get-ChildItem $root -Recurse -File | Where-Object { $_.FullName -notmatch '\\_rename' -and $_.Name -notmatch '^_' }
$srcSet = $items | ForEach-Object { $_.Source }
$unmapped = $all | Where-Object { $_.FullName -notin $srcSet }

Write-Host "==== 映射条数: $($items.Count) / 实盘文件: $($all.Count) ===="
if ($unmapped) { Write-Host "!! 未覆盖文件:"; $unmapped | ForEach-Object { Write-Host "   $($_.FullName)" } }

$dupT = $items | Group-Object Target | Where-Object { $_.Count -gt 1 }
if ($dupT) { Write-Host "!! 目标重名:"; $dupT | ForEach-Object { Write-Host "   $($_.Name) x$($_.Count)" } }

Write-Host "`n==== 源 -> 目标 ===="
foreach ($it in $items) {
  '{0}  ->  {1}' -f $it.Source.Substring($root.Length + 1), $it.Target.Substring($root.Length + 1)
}

if (-not $Execute) {
  Write-Host "`n[DRY-RUN] 未执行任何改动。加 -Execute 执行。"
  return
}

Write-Host "`n==== 执行 ===="
foreach ($it in $items) {
  $dir = Split-Path $it.Target -Parent
  if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
  Move-Item -LiteralPath $it.Source -Destination $it.Target -Force
}

Get-ChildItem $root -Directory | Where-Object { $_.Name -notin @('Portraits', 'Units', 'Buildings', 'Machines', '_rename') } | ForEach-Object {
  if (-not (Get-ChildItem $_.FullName -Recurse -File)) { Remove-Item -LiteralPath $_.FullName -Recurse -Force }
}

Write-Host "执行完成。"
