#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ValleyRampart.Rendering;

/// <summary>
/// 美术资源导入流水线（HH.239 T4/T5/T6 · D689 计划 §三/§七 · D707）。
///
/// **① 配导入设置 + 切帧**（<see cref="ApplyImportSettings"/>）：
///   - PPU=100（D27）；filter=Bilinear；compression 静态 HQ / Sheet 中；mipmap 关。
///   - pivot = **地基菱形底面中心**（美术规范 §1.1 锚点铁律）：
///       地皮（等轴立方体块）→ 顶面菱形中心（= 最宽行，实测 128px@1:1）；
///       站立物（建筑/自然特征/机器）→ 不透明底边 + footprintH×32px（菱形前角→底面中心修正）；
///       序列帧 → 全帧不透明底边最小值（**逐帧一致防抖**）；弹药/特效/立绘 → 居中。
///   - Sheet 切帧：idle/loot=3×3（**播 8 帧**）、walk/attack/run=4×4（16 帧）、`_strip`=4×4、icon=单张；
///     **显式排除** `unit_human_archer_idle.png`（96×102 异类·D10/D662·归批5 重出）。
/// **② 重建 SpriteRefTable**（<see cref="BuildSpriteRefTable"/>）：artId→Sprite / artId→Sprite[] 全量落表。
///
/// 调用方式：MCP（`unity-mcp-first`）→ <c>ArtImportPipeline.RunAll()</c>；**无 [MenuItem]**（禁手点）。
/// </summary>
public static class ArtImportPipeline
{
    const string ArtRoot = "Assets/_Game/Art";
    const string TablePath = "Assets/Resources/Config/Art/SpriteRefTable.asset";
    const int Ppu = 100;
    const byte AlphaThreshold = 16;

    /// <summary>异类表：显式排除（本批不切帧、不接线，归批5 重出）。</summary>
    static readonly HashSet<string> Excluded = new HashSet<string>
    {
        "Assets/_Game/Art/Units/human/archer/unit_human_archer_idle.png",   // 96×102 非正方（D662/D10）
    };

    // ===================== 入口 =====================

    public static void RunAll()
    {
        ApplyImportSettings();
        BuildSpriteRefTable();
        EnsureAnimatorConfig();
    }

    /// <summary>
    /// A1（HH.264）：落盘 <c>Resources/Config/Art/SpriteAnimatorConfig.asset</c>（出厂值＝规格 §三 表）。
    /// 仅**新建时**写出厂值（已存在则不动，避免覆盖人工调参·承 so-data-driven）。
    /// </summary>
    public static void EnsureAnimatorConfig()
    {
        const string path = "Assets/Resources/Config/Art/SpriteAnimatorConfig.asset";
        EnsureFolder("Assets/Resources/Config/Art");
        var cfg = AssetDatabase.LoadAssetAtPath<SpriteAnimatorConfig>(path);
        if (cfg != null) { Debug.Log("[ArtImportPipeline] SpriteAnimatorConfig 已存在（不覆盖）：" + path); return; }

        cfg = ScriptableObject.CreateInstance<SpriteAnimatorConfig>();
        cfg.defaultFps = 12f;
        cfg.fpsByState = new[] { 12f, 12f, 12f, 12f, 12f, 12f };
        cfg.attackSpeedScale = 1f;
        cfg.runAsWalkSpeedScale = 1.5f;
        cfg.lodFpsScale = new[] { 1.0f, 0.5f, 0f };
        cfg.lodRefreshInterval = 0.5f;
        cfg.stateFallback = new[] { 0, 0, 0, 1, 0, -1 };   // Idle/Walk/Attack→Idle/Run→Walk/Loot→Idle/Death 无回退
        cfg.stateMode = new[] { 0, 0, 1, 0, 2, 2 };         // Loop/Loop/OnceReturn/Loop/OnceHold/OnceHold
        cfg.workAttackLoop = true;
        AssetDatabase.CreateAsset(cfg, path);
        EditorUtility.SetDirty(cfg);
        AssetDatabase.SaveAssets();
        SpriteAnimatorConfig.ClearCache();
        Debug.Log("[ArtImportPipeline] SpriteAnimatorConfig 已创建：" + path);
    }

    // ===================== ① 导入设置 + 切帧 =====================

    public static void ApplyImportSettings()
    {
        string rootAbs = Path.GetFullPath(ArtRoot);
        var pngs = Directory.GetFiles(rootAbs, "*.png", SearchOption.AllDirectories)
                            .Select(p => p.Replace('\\', '/'))
                            .OrderBy(p => p, StringComparer.Ordinal)
                            .ToArray();

        int single = 0, sheet = 0, skipped = 0;
        var cellPivots = new Dictionary<string, float>();   // 供帧表构建复用（sheet pivot）
        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (var abs in pngs)
            {
                string rel = ToAssetPath(abs);
                if (Excluded.Contains(rel)) { skipped++; continue; }
                if (ApplyOne(rel, out bool asSheet, out float py)) { if (asSheet) sheet++; else single++; }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }
        Debug.Log($"[ArtImportPipeline] 导入设置完成：单图 {single} / 切帧 {sheet} / 排除 {skipped}（共 {pngs.Length}）");
    }

    /// <summary>配置单个 png 的导入设置。返回 false = 跳过（无法读取）。</summary>
    static bool ApplyOne(string rel, out bool asSheet, out float pivotY)
    {
        asSheet = false;
        pivotY = 0.5f;
        var ti = AssetImporter.GetAtPath(rel) as TextureImporter;
        if (ti == null) return false;

        string stem = Path.GetFileNameWithoutExtension(rel);
        string dir = Path.GetDirectoryName(rel).Replace('\\', '/');
        bool isGround = dir.EndsWith("/Ground");
        bool isAmmo = dir.EndsWith("/Ammo");
        bool isFx = dir.Contains("/Effects/");
        bool isPortrait = dir.Contains("/Portraits/");
        bool isUnits = dir.Contains("/Units/");

        // ---- 通用 ----
        ti.textureType = TextureImporterType.Sprite;
        ti.spritePixelsPerUnit = Ppu;              // D27
        ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Bilinear;       // §七（2.5D 像素风）
        ti.alphaIsTransparency = true;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.maxTextureSize = 2048;

        // ---- 切帧（仅 Units / Machines._strip）----
        bool gridOk = TryGrid(stem, out int cols, out int rows);
        bool wantSheet = gridOk && !isGround && !isAmmo && !isFx && !isPortrait
                         && (isUnits || stem.EndsWith("_strip"));

        if (wantSheet)
        {
            asSheet = true;
            pivotY = SliceSheet(rel, ti, cols, rows, out int playFrames);
            ti.textureCompression = TextureImporterCompression.Compressed;   // Sheet = 中等（§七）
            return true;
        }

        // ---- 单图 ----
        var tex = LoadPng(rel);
        if (tex == null) return false;
        pivotY = ComputeSinglePivot(tex, rel, isGround);
        DestroySafe(tex);

        var st = new TextureImporterSettings();
        ti.ReadTextureSettings(st);
        st.spriteMode = (int)SpriteImportMode.Single;
        st.spriteAlignment = (int)SpriteAlignment.Custom;
        st.spritePivot = new Vector2(0.5f, pivotY);
        st.spritePixelsPerUnit = Ppu;
        st.spriteMeshType = SpriteMeshType.FullRect;
        ti.SetTextureSettings(st);
        ti.textureCompression = isGround || isAmmo || isFx ? TextureImporterCompression.Compressed
                                                          : TextureImporterCompression.CompressedHQ;
        AssetDatabase.ImportAsset(rel, ImportAssetOptions.ForceUpdate);
        return true;
    }

    /// <summary>状态定网格（§七.1）：idle/loot=3×3（播 8）；walk/attack/run/_strip=4×4（16）。</summary>
    static bool TryGrid(string stem, out int cols, out int rows)
    {
        cols = rows = 0;
        if (stem.EndsWith("_icon")) return false;                 // 单张
        if (stem.EndsWith("_strip")) { cols = 4; rows = 4; return true; }
        if (stem.EndsWith("_idle") || stem.EndsWith("_loot")) { cols = 3; rows = 3; return true; }
        if (stem.EndsWith("_walk") || stem.EndsWith("_attack") || stem.EndsWith("_run")) { cols = 4; rows = 4; return true; }
        return false;
    }

    /// <summary>切帧 + 逐帧同 pivot（底面中心·防抖）。返回 sheet pivot.y（归一）。</summary>
    static float SliceSheet(string rel, TextureImporter ti, int cols, int rows, out int playFrames)
    {
        var tex = LoadPng(rel);
        if (tex == null) { playFrames = 0; return 0f; }
        int W = tex.width, H = tex.height;
        int cw = W / cols, ch = H / rows;
        var px = tex.GetPixels32();

        // 逐格不透明底边（格内归一），取全帧最小值 ⇒ 单 pivot 逐帧一致
        int anchorMin = int.MaxValue;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int x0 = c * cw, x1 = x0 + cw;
                int y0 = H - (r + 1) * ch, y1 = y0 + ch;   // r=0 为最上一行
                int bottom = int.MaxValue;
                for (int y = y0; y < y1; y++)
                {
                    for (int x = x0; x < x1; x++)
                    {
                        if (px[y * W + x].a <= AlphaThreshold) continue;
                        int local = y - y0;
                        if (local < bottom) bottom = local;
                        break;
                    }
                }
                if (bottom != int.MaxValue && bottom < anchorMin) anchorMin = bottom;
            }
        }
        if (anchorMin == int.MaxValue) anchorMin = 0;
        float pivotY = Mathf.Clamp01((float)anchorMin / Mathf.Max(1, ch));
        DestroySafe(tex);

        var metas = new List<SpriteMetaData>(cols * rows);
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                metas.Add(new SpriteMetaData
                {
                    name = $"{Path.GetFileNameWithoutExtension(rel)}_{r}_{c}",
                    rect = new Rect(c * cw, H - (r + 1) * ch, cw, ch),   // rect 原点在左下
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = new Vector2(0.5f, pivotY),                    // 逐帧一致
                });
            }
        }
        ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.spritesheet = metas.ToArray();
        AssetDatabase.ImportAsset(rel, ImportAssetOptions.ForceUpdate);

        // 有效帧：idle/loot 末格空 → 播 8；其余满格
        bool idleLike = Path.GetFileNameWithoutExtension(rel).EndsWith("_idle")
                        || Path.GetFileNameWithoutExtension(rel).EndsWith("_loot");
        playFrames = idleLike ? Mathf.Min(8, cols * rows) : cols * rows;
        return pivotY;
    }

    /// <summary>单图 pivot（美术规范 §1.1 锚点铁律：地基菱形底面中心）。</summary>
    static float ComputeSinglePivot(Texture2D tex, string rel, bool isGround)
    {
        int W = tex.width, H = tex.height;
        var px = tex.GetPixels32();

        if (isGround)
        {
            // 等轴立方体块：顶面菱形中心 = 最宽行（实测 128px 宽 2:1 菱形，与格对齐）。
            // ⚠ 立方体侧壁垂直，最宽宽度会连续多行成立 ⇒ 取**最高**那行（菱形左右角所在行 = 菱形中心行），
            // 取最低行会落到侧壁底部（实测差 33px ≈ 一层高度）。
            int bestW = -1, bestY = H / 2;
            for (int y = 0; y < H; y++)
            {
                int cnt = 0;
                for (int x = 0; x < W; x++) if (px[y * W + x].a > AlphaThreshold) cnt++;
                if (cnt >= bestW) { bestW = cnt; bestY = y; }
            }
            return Mathf.Clamp01((float)bestY / H);
        }

        // 弹药/命中特效/立绘/图标 = 飞行物/UI 图 ⇒ 居中（无地基概念）
        if (rel.Contains("/Ammo/") || rel.Contains("/Effects/") || rel.Contains("/Portraits/")
            || Path.GetFileNameWithoutExtension(rel).EndsWith("_icon"))
            return 0.5f;

        // 站立物：不透明底边 + footprintH×32px（菱形前角 → 底面中心修正）
        int bboxBottom = 0;
        bool found = false;
        for (int y = 0; y < H && !found; y++)
        {
            for (int x = 0; x < W; x++)
            {
                if (px[y * W + x].a <= AlphaThreshold) continue;
                bboxBottom = y; found = true; break;
            }
        }
        int fpY = FootprintOf(rel).y;
        float py = (bboxBottom + fpY * 32f) / H;   // fpY × 32px（cellH/2 = 0.64/2 world = 32px @PPU100）
        return Mathf.Clamp01(py);
    }

    /// <summary>artId/路径 → footprint（小区块）。用于 pivot 修正与验收报告。</summary>
    public static Vector2Int FootprintOf(string rel)
    {
        string stem = Path.GetFileNameWithoutExtension(rel);
        if (rel.Contains("/Ground/")) return new Vector2Int(1, 1);
        if (rel.Contains("/Portraits/") || rel.Contains("/Ammo/") || rel.Contains("/Effects/")) return Vector2Int.one;
        if (stem.StartsWith("castle_")) return new Vector2Int(3, 3);
        if (stem == "feat_mine") return new Vector2Int(2, 2);
        if (stem.StartsWith("feat_")) return Vector2Int.one;
        if (stem == "building_scaffold_3x3") return new Vector2Int(3, 3);
        if (stem == "building_scaffold_2x2") return new Vector2Int(2, 2);
        if (stem == "building_scaffold_1x1") return Vector2Int.one;
        if (stem == "building_gate_closed") return new Vector2Int(2, 1);
        if (stem.StartsWith("building_"))
        {
            switch (stem)
            {
                case "building_arrowtower":
                case "building_crossbowtower":
                case "building_magictower":
                case "building_well":
                case "building_bridge":
                case "building_vagrant_camp":
                    return Vector2Int.one;
            }
            if (stem.StartsWith("building_wall_seg")) return Vector2Int.one;
            return new Vector2Int(2, 2);
        }
        if (stem.StartsWith("machine_")) return Vector2Int.one;   // 战争机器 = 单位侧（1×1 微格）
        return Vector2Int.one;
    }

    // ===================== ② 重建 SpriteRefTable =====================

    public static void BuildSpriteRefTable()
    {
        var entries = new List<SpriteRefTable.Entry>();
        var frameEntries = new List<SpriteRefTable.FrameEntry>();
        var unmapped = new List<string>();

        string rootAbs = Path.GetFullPath(ArtRoot);
        var pngs = Directory.GetFiles(rootAbs, "*.png", SearchOption.AllDirectories)
                            .Select(p => p.Replace('\\', '/'))
                            .OrderBy(p => p, StringComparer.Ordinal)
                            .ToArray();

        foreach (var abs in pngs)
        {
            string rel = ToAssetPath(abs);
            if (Excluded.Contains(rel)) { unmapped.Add(rel + "（显式排除·归批5）"); continue; }
            string artId = ArtIdOf(rel);
            if (artId == null) { unmapped.Add(rel); continue; }

            string stem = Path.GetFileNameWithoutExtension(rel);
            bool sheet = !rel.Contains("/Ground/") && !rel.Contains("/Ammo/") && !rel.Contains("/Effects/")
                         && !rel.Contains("/Portraits/") && (rel.Contains("/Units/") || stem.EndsWith("_strip"))
                         && TryGrid(stem, out _, out _);

            if (sheet)
            {
                var frames = LoadFrames(rel, stem, out int playFrames);
                if (frames != null && frames.Length > 0)
                {
                    frameEntries.Add(new SpriteRefTable.FrameEntry { artId = artId, frames = frames });
                    continue;
                }
                // 切帧失败 → 退化为单图登记
            }

            var s = AssetDatabase.LoadAssetAtPath<Sprite>(rel);
            if (s != null) entries.Add(new SpriteRefTable.Entry { artId = artId, sprite = s });
            else unmapped.Add(rel + "（LoadAssetAtPath 返回 null）");
        }

        // 落资产
        EnsureFolder("Assets/Resources/Config/Art");
        var table = AssetDatabase.LoadAssetAtPath<SpriteRefTable>(TablePath);
        if (table == null)
        {
            table = ScriptableObject.CreateInstance<SpriteRefTable>();
            AssetDatabase.CreateAsset(table, TablePath);
        }
        var so = new SerializedObject(table);
        so.FindProperty("entries").arraySize = entries.Count;
        for (int i = 0; i < entries.Count; i++)
        {
            var p = so.FindProperty("entries").GetArrayElementAtIndex(i);
            p.FindPropertyRelative("artId").stringValue = entries[i].artId;
            p.FindPropertyRelative("sprite").objectReferenceValue = entries[i].sprite;
        }
        so.FindProperty("frameEntries").arraySize = frameEntries.Count;
        for (int i = 0; i < frameEntries.Count; i++)
        {
            var p = so.FindProperty("frameEntries").GetArrayElementAtIndex(i);
            p.FindPropertyRelative("artId").stringValue = frameEntries[i].artId;
            var arr = p.FindPropertyRelative("frames");
            arr.arraySize = frameEntries[i].frames.Length;
            for (int k = 0; k < frameEntries[i].frames.Length; k++)
                arr.GetArrayElementAtIndex(k).objectReferenceValue = frameEntries[i].frames[k];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        SpriteRefTable.ClearCache();

        Debug.Log($"[ArtImportPipeline] SpriteRefTable 重建：单图 {entries.Count} 键 / 帧 {frameEntries.Count} 键 → {TablePath}");
        if (unmapped.Count > 0)
            Debug.LogWarning($"[ArtImportPipeline] 未落表 {unmapped.Count} 条：{string.Join(" | ", unmapped)}");
    }

    /// <summary>按 rect 行优先（上→下、左→右）取子 sprite，截取有效帧数。</summary>
    static Sprite[] LoadFrames(string rel, string stem, out int playFrames)
    {
        playFrames = 0;
        var all = AssetDatabase.LoadAllAssetsAtPath(rel).OfType<Sprite>().ToList();
        if (all.Count == 0) return null;
        all.Sort((a, b) =>
        {
            int c = b.rect.y.CompareTo(a.rect.y);      // 上面（y 大）在前
            return c != 0 ? c : a.rect.x.CompareTo(b.rect.x);
        });
        bool idleLike = stem.EndsWith("_idle") || stem.EndsWith("_loot");
        int n = idleLike ? Mathf.Min(8, all.Count) : all.Count;   // 帧格 ≠ 有效帧（idle 切 9 播 8）
        playFrames = n;
        return all.Take(n).ToArray();
    }

    /// <summary>文件路径 → artId（D37 唯一源；见映射表 §十/§十一）。无法映射返回 null。</summary>
    public static string ArtIdOf(string rel)
    {
        string stem = Path.GetFileNameWithoutExtension(rel);
        string dir = Path.GetDirectoryName(rel).Replace('\\', '/');

        if (dir.EndsWith("/Ground")) return stem;                                  // ground_*
        if (dir.EndsWith("/Ammo")) return stem;                                    // ammo_*
        if (dir.Contains("/Effects/"))
            return stem.StartsWith("fx_hit_") ? "fx_" + stem.Substring(7) : stem;  // fx_hit_fireball → fx_fireball
        if (dir.EndsWith("/Machines") || dir.Contains("/Machines")) return stem;   // machine_*
        if (dir.Contains("/Portraits/"))
        {
            // unit_{race}_{occ} → portrait_{race}_{occ}
            var parts = stem.Split('_');
            return parts.Length >= 3 ? "portrait_" + string.Join("_", parts.Skip(1)) : null;
        }
        if (dir.Contains("/Units/")) return stem;                                   // unit_{race}_{occ}_{state} / unit_monster_*
        if (dir.Contains("/Buildings/"))
        {
            if (dir.EndsWith("/castle"))
            {
                var m = System.Text.RegularExpressions.Regex.Match(stem, @"^castle_([a-z]+)_lv(\d)$");
                return m.Success ? $"bld_castle_{m.Groups[1].Value}_lv{m.Groups[2].Value}" : null;
            }
            if (dir.EndsWith("/exclusive"))
            {
                switch (stem)
                {
                    case "building_human_waracademy": return "bld_waracademy";
                    case "building_orc_waracademy":   return "bld_warcamp";     // 语义=兽人战营（映射表 §四）
                    case "building_dwarf_leyforge":   return "bld_leyforge";
                    case "building_elf_archery":      return "bld_archeryrange";
                    default: return null;
                }
            }
            if (stem.StartsWith("feat_")) return stem;                              // feat_*
            if (stem.StartsWith("building_"))
            {
                string body = stem.Substring("building_".Length);
                if (body.StartsWith("mine_lv")) body = "mine_b_lv" + body.Substring("mine_lv".Length);
                else if (body == "arrowtower") body = "tower";                      // 箭塔（唯一塔素材）
                else if (body == "gate_closed") body = "gate";                      // open 缺图 → 开门态复用 closed（E5）
                return "bld_" + body;
            }
        }
        return null;
    }

    // ===================== 工具 =====================

    static Texture2D LoadPng(string assetPath)
    {
        try
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (t.LoadImage(File.ReadAllBytes(Abs(assetPath)))) return t;
            DestroySafe(t);
        }
        catch (Exception e) { Debug.LogWarning($"[ArtImportPipeline] 读取失败 {assetPath}: {e.Message}"); }
        return null;
    }

    /// <summary>Assets 相对路径 → 绝对路径（不依赖 CWD）。</summary>
    static string Abs(string assetPath) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));

    /// <summary>绝对路径 → Assets 相对路径（AssetDatabase 用）。</summary>
    static string ToAssetPath(string abs)
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
        string f = abs.Replace('\\', '/');
        return f.StartsWith(root) ? f.Substring(root.Length).TrimStart('/') : f;
    }

    static void DestroySafe(UnityEngine.Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(o); else UnityEngine.Object.DestroyImmediate(o);
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        var leaf = Path.GetFileName(folder);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
#endif
