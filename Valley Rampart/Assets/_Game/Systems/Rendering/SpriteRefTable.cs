using System;
using System.Collections.Generic;
using UnityEngine;

namespace ValleyRampart.Rendering
{
/// <summary>
/// artId → Sprite 真图查询表（HH.239 T6 · D689 计划 §三 · D707 R4 旁挂裁）。
///
/// 单一注入点：<see cref="PlaceholderSprites.Get(string,int)"/> 先查本表 → 命中真图；未命中回退占位。
/// 弹药/命中特效走本表 <c>ammo_*</c>/<c>fx_*</c> 旁挂键（**不动** AmmoDef/GroundEffectDef —— 二者字段会拉平进
/// ProfessionSnapshot，位处 Systems/AI.Core/Config ⇒ 加表现字段必然改快照 = 撞 H3「AI.Core 禁触」红线）。
///
/// 资产：<c>Resources/Config/Art/SpriteRefTable.asset</c>（由 <c>Editor/ArtImportPipeline</c> 生成，禁手改）。
/// 缺图回退：本表 miss 返回 false → 调用方回退 <see cref="PlaceholderSprites"/> 生成占位（不崩不空白）。
/// </summary>
public class SpriteRefTable : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string artId;
        public Sprite sprite;
    }

    [Serializable]
    public class FrameEntry
    {
        public string artId;
        public Sprite[] frames;
    }

    [SerializeField] private Entry[] entries = Array.Empty<Entry>();
    [SerializeField] private FrameEntry[] frameEntries = Array.Empty<FrameEntry>();

    /// <summary>Resources 相对路径（无扩展名）。</summary>
    public const string ResourcePath = "Config/Art/SpriteRefTable";

    private static SpriteRefTable _instance;
    private static bool _loadAttempted;

    private Dictionary<string, Sprite> _map;
    private Dictionary<string, Sprite[]> _frames;

    /// <summary>单例（Resources 懒加载；资产缺失返回 null = 全量走占位回退）。</summary>
    public static SpriteRefTable Instance
    {
        get
        {
            if (!_loadAttempted)
            {
                _loadAttempted = true;
                _instance = Resources.Load<SpriteRefTable>(ResourcePath);
                if (_instance == null)
                    Debug.LogWarning($"[SpriteRefTable] 未找到 Resources/{ResourcePath}.asset ⇒ 全量回退占位（缺图回退链生效）");
            }
            return _instance;
        }
    }

    /// <summary>清静态缓存（编辑器重建资产后调用；运行时勿用）。</summary>
    public static void ClearCache()
    {
        _loadAttempted = false;
        _instance = null;
    }

    public int EntryCount => entries != null ? entries.Length : 0;
    public int FrameEntryCount => frameEntries != null ? frameEntries.Length : 0;

    private void EnsureIndex()
    {
        if (_map != null) return;
        _map = new Dictionary<string, Sprite>();
        if (entries != null)
        {
            foreach (var e in entries)
            {
                if (e == null || string.IsNullOrEmpty(e.artId) || e.sprite == null) continue;
                _map[e.artId] = e.sprite;
            }
        }
        _frames = new Dictionary<string, Sprite[]>();
        if (frameEntries != null)
        {
            foreach (var f in frameEntries)
            {
                if (f == null || string.IsNullOrEmpty(f.artId) || f.frames == null || f.frames.Length == 0) continue;
                _frames[f.artId] = f.frames;
            }
        }
    }

    /// <summary>精确 artId 查询（单图）。</summary>
    public bool TryGet(string artId, out Sprite sprite)
    {
        sprite = null;
        if (string.IsNullOrEmpty(artId)) return false;
        EnsureIndex();
        return _map.TryGetValue(artId, out sprite) && sprite != null;
    }

    /// <summary>取单图（miss 返回 null）。</summary>
    public Sprite Get(string artId) => TryGet(artId, out var s) ? s : null;

    /// <summary>该 sprite 是否来自真图表（单图或序列帧任一；供调用方区分「真图 / 占位回退」）。</summary>
    public bool Contains(Sprite sprite)
    {
        if (sprite == null) return false;
        EnsureIndex();
        foreach (var kv in _map)
            if (ReferenceEquals(kv.Value, sprite)) return true;
        foreach (var kv in _frames)
            if (kv.Value != null)
                for (int i = 0; i < kv.Value.Length; i++)
                    if (ReferenceEquals(kv.Value[i], sprite)) return true;
        return false;
    }

    /// <summary>精确 artId 查询（序列帧，切帧后的 sprite 数组）。</summary>
    public bool TryGetFrames(string artId, out Sprite[] frames)
    {
        frames = null;
        if (string.IsNullOrEmpty(artId)) return false;
        EnsureIndex();
        return _frames.TryGetValue(artId, out frames) && frames != null && frames.Length > 0;
    }

    /// <summary>
    /// 分级建筑查询：<c>{baseArtId}_lv{level}</c> → <c>{baseArtId}</c> → <c>{baseArtId}_lv1</c>。
    /// level&lt;=0 时只试无后缀键。全 miss 返回 false（调用方回退占位）。
    /// </summary>
    public bool TryGetLeveled(string baseArtId, int level, out Sprite sprite)
    {
        sprite = null;
        if (string.IsNullOrEmpty(baseArtId)) return false;
        if (level > 0 && TryGet($"{baseArtId}_lv{level}", out sprite)) return true;
        if (TryGet(baseArtId, out sprite)) return true;
        if (level > 1 && TryGet($"{baseArtId}_lv1", out sprite)) return true;
        sprite = null;
        return false;
    }

    /// <summary>全部已登记 artId（验收取证用：键空间覆盖实证）。</summary>
    public string[] GetAllArtIds()
    {
        EnsureIndex();
        var list = new List<string>(_map.Keys);
        list.Sort(StringComparer.Ordinal);
        return list.ToArray();
    }

    /// <summary>全部帧键（验收取证用）。</summary>
    public string[] GetAllFrameArtIds()
    {
        EnsureIndex();
        var list = new List<string>(_frames.Keys);
        list.Sort(StringComparer.Ordinal);
        return list.ToArray();
    }
}
}
