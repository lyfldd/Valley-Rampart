using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 箱子管理器（2_12 步骤7C + 步骤11 / D269 统一资源容器 D142 的唯一归属者）。
/// 负责：生成（由触发源调用）、拾取接口、命中重置、过期扫描、单格数量上限（D222）。
/// 生成触发源归属：工人背包满（D221）→ 8 调度层；怪物掉落（D213）→ 2_14；仓库溢出（D222）→ 步骤11；本步只定义接口。
/// 搬运/拾取入背包 → 8 调度层；渲染 → 2_10；存档 → 2_11 步骤8（本步不留 ISaveable，避免与 2_11 重复）。
/// </summary>
public class ChestManager : Singleton<ChestManager>
{
    /// <summary>实例是否已存在（Instance 判空，供外部安全访问）。</summary>
    public static bool HasInstance => Instance != null;

    private readonly List<ChestEntity> _chests = new List<ChestEntity>();

    /// <summary>存活箱子总数（供调试/上限判定）。</summary>
    public int Count => _chests.Count;

    private void Awake()
    {
        base.Awake();   // Singleton：自动创建 + DontDestroyOnLoad
    }

    private void Update()
    {
        // 过期扫描（D148）：bornDay + expireDays &lt; 当前天 → 移除
        if (_chests.Count == 0) return;
        int curDay = Mathf.RoundToInt(TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 1);
        float expire = ChestConfig.Instance != null ? ChestConfig.Instance.expireDays : 3f;
        for (int i = _chests.Count - 1; i >= 0; i--)
        {
            var c = _chests[i];
            if (c == null) { _chests.RemoveAt(i); continue; }
            if (curDay - c.bornDay >= expire) Remove(c);
        }
    }

    /// <summary>
    /// 生成一个箱子落到格上（D245 统一容器）。所有来源（工人背包满/怪物掉落/仓库溢出）经此落箱。
    /// 单格数量上限（D222）：超 chestMaxPerCell 时移除该格最早箱子。
    /// </summary>
    /// <param name="cell">落点格坐标（微格/楼层 0）。</param>
    /// <param name="pack">内容物资源包（D145 容量同工人携带量）。</param>
    /// <param name="faction">来源阵营（任意阵营可拾 D146；记录来源供 2_14 掠夺）。</param>
    /// <returns>创建成功的箱子；内容空/坐标非法返回 null。</returns>
    public ChestEntity SpawnChest(GridCoord cell, ResourcePack pack, Faction faction)
    {
        if (pack.IsZero) return null;
        float born = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 1;

        // 单格上限：该格已满则移除最早一个（D222 防堆积）
        EnforceCellLimit(cell);

        var go = new GameObject("Chest");
        go.transform.position = WorldPosOf(cell);
        var chest = go.AddComponent<ChestEntity>();
        chest.Init(cell, pack, born);
        chest.ownerFaction = faction;
        _chests.Add(chest);
        return chest;
    }

    /// <summary>该格箱子数（供 spawn 前判定上限/调试）。</summary>
    public int CountAt(GridCoord cell)
    {
        int n = 0;
        for (int i = 0; i < _chests.Count; i++)
            if (_chests[i].cell.Equals(cell)) n++;
        return n;
    }

    private void EnforceCellLimit(GridCoord cell)
    {
        int max = ChestConfig.Instance != null ? Mathf.Max(1, ChestConfig.Instance.chestMaxPerCell) : 4;
        int at = CountAt(cell);
        if (at >= max)
        {
            // 移除该格最早的箱子（遍历找该格最早 bornDay 的）
            ChestEntity oldest = null;
            for (int i = 0; i < _chests.Count; i++)
            {
                var c = _chests[i];
                if (c == null || !c.cell.Equals(cell)) continue;
                if (oldest == null || c.bornDay < oldest.bornDay) oldest = c;
            }
            if (oldest != null) Remove(oldest);
        }
    }

    /// <summary>格坐标 → 世界位置（对 Execure 等归一）。用楼阁层 0。</summary>
    private Vector3 WorldPosOf(GridCoord cell)
    {
        if (GridSystem.Instance != null) return GridSystem.Instance.CoordToWorld(cell);
        return new Vector3(cell.x, cell.y, 0f);
    }

    /// <summary>
    /// 拾取箱子（D246 任意阵营可拾 D146）。抽出内容物给发起者背包（背包落库链接由 8 调度/3.5 完成），
    /// 本步仅负责"取走内容 + 移除箱子实体"。返回值=拾取到的资源包（供调用方入背包），空则零值。
    /// </summary>
    public ResourcePack Pickup(ChestEntity chest, Interactor ctx)
    {
        if (chest == null) return ResourcePack.Zero;
        var got = chest.contents;
        Remove(chest);
        return got;
    }

    /// <summary>命中重置（D247：HP=1 一击碎，内容物原地重新落箱可再拾）。由 ChestEntity.Strike 调用。</summary>
    public void ResetDrop(ChestEntity chest)
    {
        if (chest == null) return;
        var pack = chest.contents;
        var cell = chest.cell;
        var faction = chest.ownerFaction;
        Remove(chest);                       // 移除原实体
        SpawnChest(cell, pack, faction);     // 原地重落（内容保留）
    }

    /// <summary>移除箱子（拾取/过期/上限）。</summary>
    public void Remove(ChestEntity chest)
    {
        if (chest == null) return;
        _chests.Remove(chest);
        if (chest.gameObject != null) Destroy(chest.gameObject);
    }

    /// <summary>清空全部（开局/读档重建用）。</summary>
    public void ClearAll()
    {
        for (int i = 0; i < _chests.Count; i++)
            if (_chests[i] != null && _chests[i].gameObject != null)
                Destroy(_chests[i].gameObject);
        _chests.Clear();
    }
}