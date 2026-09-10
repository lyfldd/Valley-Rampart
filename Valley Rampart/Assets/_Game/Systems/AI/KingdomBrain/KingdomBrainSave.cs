using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  王国脑运行时状态存档（2_22 P0 批E / E1，2_11 纪律：双层版本号+模块自治）
//  入档三件（§3.6 八格持久化）：
//    ① 兵种表现统计 = 窗口损毁流水（KingdomBrain._lossLog，TTL 窗口连续性=读档后统计恢复）
//    ② 姿态档位     = MilitaryPostureController.Current + _lastChangeDay（滞回基准恢复）
//    ③ 权重现值     = BattleLearnedWeights 局内环权重（per kingdom per occupation）
//
//  schema 版本化（2_11 §3.3 双层版本）：
//    · 全局 saveVersion 零 bump（M1/HH.109 先例：新模块纯增量，旧档无本条目=SaveManager
//      分发跳过、LoadState 不被调用=零兼容负担，旧档可载不炸硬性验收）。
//    · 模块自治 version=1；纯加字段走 JsonUtility 缺省兼容，无需迁移器。
//
//  读档恢复时序（无硬排序依赖）：
//    · LoadState（Global 段）→ 解析入 pending 缓存 + 静态枢纽即时落（权重/BattleLearnedWeights
//      + 姿态档/PostureHub——两枢纽不依赖脑实例）。
//    · GameLoadedEvent（SaveManager.Load 收尾）→ 补建无脑王国的脑（读档王国由 KingdomRegistry
//      LoadState 恢复但无脑=既有已知边界，E1 修复载体）+ 对全部 AI 王国 ApplyPendingRestore
//      （脑已存在=旧脑/新脑都落，稳健）。
//    · KingdomBrainFactory.Create 钩子兜底（王国诞生路径，pending 有该王国即落）。
//
//  L-17 变体③（D600）：本模块纯数据搬运零副作用；涉清场/读档副作用探针在 E4 侧走
//  快照复原+撤守卫前终检三件套。
// ============================================================================

/// <summary>王国脑存档权重条目（per occupation）。</summary>
[System.Serializable]
public struct KingdomBrainSaveWeightEntry
{
    public int occupationId;
    public float weight;
}

/// <summary>王国脑存档损毁流水条目（TTL 窗口连续性载体）。</summary>
[System.Serializable]
public struct KingdomBrainSaveLossEntry
{
    public int day;
    public bool isBuilding;
    public int occupationId;
}

/// <summary>单王国脑存档条目（全字段平铺，JsonUtility 直序列化）。</summary>
[System.Serializable]
public struct KingdomBrainSaveEntry
{
    public int kingdomId;
    public int posture;                    // (int)MilitaryPosture
    public int postureLastChangeDay;       // 滞回基准（读档后防抖窗口连续）
    public List<KingdomBrainSaveWeightEntry> weights;
    public List<KingdomBrainSaveLossEntry> losses;
}

/// <summary>王国脑存档数据（模块自治 version，独立于全局 saveVersion）。</summary>
[System.Serializable]
public class KingdomBrainSaveData
{
    public int version = 1;
    public List<KingdomBrainSaveEntry> kingdoms = new List<KingdomBrainSaveEntry>();
}

/// <summary>
/// 王国脑运行时状态存档模块（ISaveable，Global 段；Singleton 生命周期）。
/// 经 KingdomBrainFactory.Create 与 CoreBootstrap 确保实例先于 Save/Load 存在。
/// </summary>
public class KingdomBrainSave : Singleton<KingdomBrainSave>, ISaveable
{
    /// <summary>读档暂存（LoadState 落，ApplyPendingRestore 消费清）。</summary>
    private static readonly Dictionary<int, KingdomBrainSaveEntry> _pending =
        new Dictionary<int, KingdomBrainSaveEntry>();

    public string SaveId => "KingdomBrainAI";
    public SaveLoadPhase LoadPhase => SaveLoadPhase.Global;

    protected override void Awake()
    {
        base.Awake();
        if (_instance != this) return;
        if (SaveManager.Instance != null) SaveManager.Instance.RegisterSaveable(this);
        EventBus.Subscribe<GameLoadedEvent>(OnGameLoaded);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        EventBus.Unsubscribe<GameLoadedEvent>(OnGameLoaded);
    }

    public SavePayload SaveState()
    {
        var data = new KingdomBrainSaveData();
        var reg = KingdomBrainRegistry.Instance;
        if (reg != null)
        {
            foreach (var brain in reg.GetAll())
            {
                if (brain == null) continue;
                var e = new KingdomBrainSaveEntry
                {
                    kingdomId = brain.kingdomId,
                    posture = (int)brain.Posture.Current,
                    postureLastChangeDay = brain.Posture.LastChangeDay,
                    weights = new List<KingdomBrainSaveWeightEntry>(),
                    losses = new List<KingdomBrainSaveLossEntry>()
                };
                // ③ 权重现值（只存有记录的兵种；无记录=读档后 1.0 中性起步=语义一致）
                if (BattleLearnedWeights.TrySnapshot(brain.kingdomId, out var wmap))
                {
                    foreach (var kv in wmap)
                        e.weights.Add(new KingdomBrainSaveWeightEntry { occupationId = kv.Key, weight = kv.Value });
                }
                // ① 兵种表现统计=损毁流水（确定性：_lossLog 追加序，存读同序）
                brain.CollectLosses(e.losses);
                data.kingdoms.Add(e);
            }
        }
        return new SavePayload
        {
            typeName = typeof(KingdomBrainSaveData).AssemblyQualifiedName,
            json = JsonUtility.ToJson(data),
            version = data.version
        };
    }

    public void LoadState(SavePayload payload)
    {
        if (payload.typeName != typeof(KingdomBrainSaveData).AssemblyQualifiedName) return;
        _pending.Clear();
        var data = JsonUtility.FromJson<KingdomBrainSaveData>(payload.json);
        if (data == null || data.kingdoms == null) return;

        foreach (var e in data.kingdoms)
        {
            _pending[e.kingdomId] = e;
            // 静态枢纽即时落（不依赖脑实例）：权重 + 姿态档
            if (e.weights != null)
                for (int i = 0; i < e.weights.Count; i++)
                    BattleLearnedWeights.SetWeight(e.kingdomId, e.weights[i].occupationId, e.weights[i].weight);
            PostureHub.Put(e.kingdomId, (MilitaryPosture)e.posture);
        }
        Debug.Log($"[KingdomBrainSave] 读档暂存 {data.kingdoms.Count} 国（权重/姿态档已落静态枢纽）");
    }

    /// <summary>
    /// GameLoadedEvent（SaveManager.Load 收尾）：补建无脑王国脑 + 全部 AI 王国落实例态。
    /// 既有脑（旧脑，同会话连续存读场景）与新脑（读档重建路径）统一走 ApplyPendingRestore。
    /// </summary>
    private void OnGameLoaded(GameLoadedEvent evt)
    {
        if (!evt.IsSuccess) return;
        var reg = KingdomRegistry.Instance;
        if (reg == null) return;
        var brains = KingdomBrainRegistry.Instance;
        int ensured = 0, applied = 0;
        foreach (var k in reg.GetAll())
        {
            if (k == null || k.IsPlayer) continue;
            var brain = brains != null ? brains.Get(k.id) : null;
            if (brain == null)
            {
                brain = KingdomBrainFactory.Create(k.id);   // 读档无脑→补建（E1 载体）
                if (brain != null) ensured++;
            }
            if (brain != null && ApplyPendingRestore(brain)) applied++;
        }
        Debug.Log($"[KingdomBrainSave] 读档恢复收尾：补建脑 {ensured}，落实例态 {applied}（统计/档位/权重）");
    }

    /// <summary>
    /// 王国脑创建钩子（KingdomBrainFactory.Create 调）：pending 有该王国 → 落实例态（滞回基准+损毁流水）。
    /// 返回是否已落实（探针/日志用）。
    /// </summary>
    public static bool ApplyPendingRestore(KingdomBrain brain)
    {
        if (brain == null) return false;
        if (!_pending.TryGetValue(brain.kingdomId, out var e))
        {
            _pending.Remove(brain.kingdomId);   // 防御：清幽灵键
            return false;
        }
        brain.RestoreSavedState((MilitaryPosture)e.posture, e.postureLastChangeDay, e.losses);
        _pending.Remove(brain.kingdomId);
        return true;
    }

    /// <summary>pending 清空（harness 两轮间归零用，对齐 SituationHub.Clear 先例）。</summary>
    public static void ClearPending() => _pending.Clear();
}
