#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// HH.239 美术接入批 · 四族四轮冒烟探针（Editor-only，正门 <see cref="TestHarnessApi"/> 口径）。
///
/// 验收面（任务书 §四 6 条）：
///   ①真图生效（六类：建筑/资源/地皮/单位/弹药/特效）；②缺图回退（负探针，不崩）；
///   ③锚点（importer 读回由 ArtImportPipeline 侧取证）；④两向动画（帧集解析 + flipX 归 SpriteAnimator）；
///   ⑤残留清零；⑥回归零退化。
/// 用法（MCP `unity-mcp-first`）：Play 模式内 <c>Valley_HH239_ArtProbe.RunRound(race)</c>；
/// 收尾 <c>Valley_HH239_ArtProbe.Quit()</c>（ExitTestRun + QuitSmoke 退 Play）。
/// </summary>
public static class Valley_HH239_ArtProbe
{
    static readonly FieldInfo SetField =
        typeof(SpriteAnimator).GetField("_set", BindingFlags.NonPublic | BindingFlags.Instance);
    // HH.264 A 段后 `EnsureSet` 由 internal 提升为 public（探针可访问）⇒ 反射须含 Public（否则取到 null）
    static readonly MethodInfo EnsureSet =
        typeof(SpriteAnimator).GetMethod("EnsureSet", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>单轮：建局（正门链路）→ 探针 → 返回证据文本。race: 0=human 1=elf 2=dwarf 3=orc。</summary>
    public static string RunRound(int race, int worldSeed = 21107)
    {
        var table = ValleyRampart.Rendering.SpriteRefTable.Instance;
        var cfg = new NewGameConfig { raceId = race, worldSeed = worldSeed, difficulty = 2, selectedSlotId = "smoke_hh239" };
        SmokeApi.EnterGame(cfg);
        if (TimeManager.Instance != null) TimeManager.Instance.EnableTestHarness(15f);

        var sb = new StringBuilder();
        sb.AppendLine($"[HH239] ==== ROUND race={race} ({new[] { "human", "elf", "dwarf", "orc" }[race]}) seed={worldSeed} ====");
        sb.AppendLine($"  table: entries={table?.EntryCount ?? -1} frames={table?.FrameEntryCount ?? -1}");

        // ---- ① 建筑真图 + ③ 主城 (race, level) ----
        int bTot = 0, bReal = 0;
        var castles = new List<string>();
        var byDef = new Dictionary<string, string>();
        var reg = BuildingRegistry.Instance;
        if (reg != null)
        {
            foreach (var b in reg.All)
            {
                if (b == null) continue;
                b.RefreshVisual();                      // 强制走一次真实视觉路径（等价下一帧）
                bTot++;
                var sr = b.GetComponent<SpriteRenderer>();
                bool real = sr != null && sr.sprite != null && table != null && table.Contains(sr.sprite);
                if (real) bReal++;
                if (b.sourceType == BuildingType.CastleCore)
                    castles.Add($"k{b.kingdomId}={sr?.sprite?.name ?? "null"}");
                string id = b.def != null ? b.def.id : "?";
                if (!byDef.ContainsKey(id)) byDef[id] = (sr?.sprite?.name ?? "null") + (real ? "*" : "!");
            }
        }
        sb.AppendLine($"  ①BUILDING total={bTot} realArt={bReal}");
        sb.AppendLine($"    castle按族按级: {string.Join(", ", castles.OrderBy(s => s))}");
        sb.AppendLine($"    def→sprite 抽验({Mathf.Min(14, byDef.Count)}/{byDef.Count}): " +
                      string.Join(" | ", byDef.Take(14).Select(kv => kv.Key + "=" + kv.Value)));

        // ---- ① 单位真图 + 不串族 + ④ 帧集 ----
        var units = UnityEngine.Object.FindObjectsOfType<UnitController>();
        int uResolved = 0;
        var perRace = new Dictionary<int, List<string>>();
        foreach (var u in units)
        {
            var anim = u.GetComponent<SpriteAnimator>();
            if (anim != null) EnsureSet.Invoke(anim, null);
            bool ok = anim != null && SetField.GetValue(anim) != null;
            if (ok) uResolved++;
            var sr = u.GetComponent<SpriteRenderer>();
            if (!perRace.ContainsKey(u.raceId)) perRace[u.raceId] = new List<string>();
            if (perRace[u.raceId].Count < 2)
                perRace[u.raceId].Add($"{u.EffectiveOccupation}={(sr?.sprite?.name ?? "null")}{(ok ? "" : "(noSet)")}");
        }
        sb.AppendLine($"  ①UNIT total={units.Length} resolvedFrames={uResolved}");
        foreach (var kv in perRace.OrderBy(k => k.Key))
            sb.AppendLine($"    race{kv.Key} n={kv.Value.Count}: {string.Join(" | ", kv.Value)}");
        var driver = UnityEngine.Object.FindObjectOfType<SpriteAnimatorDriver>();
        sb.AppendLine($"    driver活跃数={driver?.ActiveCount ?? -1}");

        // ---- ① 地皮真图 ----
        var tmGo = GameObject.Find("Tilemap_Ground");
        var tilemap = tmGo != null ? tmGo.GetComponent<UnityEngine.Tilemaps.Tilemap>() : null;
        var used = new HashSet<string>();
        if (tilemap != null)
        {
            var bb = tilemap.cellBounds;
            for (int x = bb.xMin; x < bb.xMax; x += 5)
                for (int y = bb.yMin; y < bb.yMax; y += 5)
                {
                    var sp = tilemap.GetSprite(new Vector3Int(x, y, 0));
                    if (sp != null) used.Add(sp.name);
                }
        }
        sb.AppendLine($"  ①GROUND distinct={used.Count} -> {string.Join(", ", used.OrderBy(s => s))}");

        // ---- G 段回归（HH.264 G1）：特征物层真图（四族轮同源对照；行跨 3 抽样）----
        var ftGo = GameObject.Find("Tilemap_Feature");
        var ftm = ftGo != null ? ftGo.GetComponent<UnityEngine.Tilemaps.Tilemap>() : null;
        int ftTot = 0, ftReal = 0;
        var ftNames = new HashSet<string>();
        if (ftm != null)
        {
            var fbb = ftm.cellBounds;
            for (int x = fbb.xMin; x < fbb.xMax; x += 3)
                for (int y = fbb.yMin; y < fbb.yMax; y += 3)
                {
                    var sp = ftm.GetSprite(new Vector3Int(x, y, 0));
                    if (sp == null) continue;
                    ftTot++;
                    if (!string.IsNullOrEmpty(sp.name))
                    {
                        if (sp.name.StartsWith("feat_")) ftReal++;
                        ftNames.Add(sp.name);
                    }
                }
        }
        sb.AppendLine($"  G段回归 特征物层: sampled={ftTot} real={ftReal} distinctFeat={ftNames.Count} -> {string.Join(", ", ftNames.OrderBy(s => s))}");

        // ---- ② 缺图回退负探针（不崩 + 回退非真图）----
        // D724 件3（HH.239 探针勘正）：删 `ground_lake` 查项——git log -S "ground_lake" 全库历史零命中
        // （全库从未有该键，R5 收敛后水系走 ground_ocean/ground_river），且随本日「湖/冰河删除」彻底作废
        // （撞 L-26 家族：判据本身不可满足）。D724 裁定「须修探针（删该查项）」。
        var fb1 = ValleyRampart.Rendering.PlaceholderSprites.Get("bld_academy");            // 学院无素材
        var fb3 = ValleyRampart.Rendering.PlaceholderSprites.Get("unit_orc_windwalker_idle"); // 兽人无风行者
        var fb4 = ValleyRampart.Rendering.PlaceholderSprites.Get("bld_house", 2);           // 命中 lv2 真图
        sb.AppendLine($"  ②FALLBACK bld_academy→nonNull={fb1 != null}/inTable={fb1 != null && table.Contains(fb1)}" +
                      $" ; orc_windwalker→nonNull={fb3 != null}" +
                      $" ; bld_house_lv2→inTable={fb4 != null && table.Contains(fb4)}");

        return sb.ToString();
    }

    /// <summary>收尾：退考跑 + 清场 + 退 Play（L-32）。</summary>
    public static string Quit()
    {
        TestHarnessApi.ExitTestRun();
        SmokeApi.QuitSmoke();
        return "[HH239] ExitTestRun + QuitSmoke 完成";
    }
}
#endif
