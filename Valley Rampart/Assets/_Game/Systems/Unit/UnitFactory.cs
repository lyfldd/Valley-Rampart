using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单位工厂。按 UnitData（SO 直接引用）实例化，不依赖 Resources/UnitPrefabs 命名匹配（2_3 步骤0）。
/// Prefab 由 UnitData.prefab 提供；实例对象池按 UnitData 分桶。
/// </summary>
public class UnitFactory : Singleton<UnitFactory>, ISaveableSpawner
{
    public string SaveIdPrefix => "Unit_";

    // 3.0.1 §7.4 对象池：实例层（按 UnitData 分桶），门面挂在 UnitFactory 现有生成路径
    private readonly UnitInstancePool _instancePool = new UnitInstancePool();

    /// <summary>实例池（供外部统计/调试）。</summary>
    public UnitInstancePool InstancePool => _instancePool;

    /// <summary>
    /// 预加载（2_3 步骤0：不再扫描 Resources/UnitPrefabs，prefab 引用随 UnitData SO 自带）。
    /// 幂等：由 LoadManager 阶段1 显式调用。
    /// </summary>
    public void PreloadAll()
    {
        if (_isPreloaded)
        {
            Debug.Log("[UnitFactory] 已预加载过，跳过。");
            return;
        }

        Debug.Log("[UnitFactory] 预加载单位数据（prefab 引用随 UnitData SO 自带）...");
        _isPreloaded = true;
    }

    private bool _isPreloaded = false;

    /// <summary>
    /// 根据 UnitData 创建单位实例（2_3 步骤0：用 data.prefab，无命名回退）。
    /// kingdomId 参数（2_16 步骤2，默认 0=玩家）：Entity 归属标注，AI/动态王国传入 Registry id。
    /// </summary>
    public GameObject SpawnUnit(UnitData data, Vector2 position, int kingdomId = 0)
    {
        if (data == null)
        {
            Debug.LogError("[UnitFactory] UnitData 为空，无法创建单位。");
            return null;
        }

        if (data.prefab == null)
        {
            Debug.LogError($"[UnitFactory] UnitData '{data.name}' 未挂 prefab 引用，无法生成。");
            return null;   // 彻底解耦：无命名回退
        }

        // 3.0.1 §7.4 对象池：优先取桶（池空才 Instantiate——战斗尖峰零分配）
        GameObject instance = _instancePool.Get(data);
        if (instance == null)
        {
            instance = Instantiate(data.prefab, position, Quaternion.identity);
            instance.name = data.name;
        }
        else
        {
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.identity;
            instance.SetActive(true);
        }

        // 绑定数据到控制器
        var controller = instance.GetComponent<UnitController>();
        if (controller != null)
        {
            controller.Initialize(data);
            controller.kingdomId = kingdomId;   // 2_16 步骤2：Entity 王国归属（默认 0=玩家）
        }

        // 3.0.1: 如果有 NPCBrain 且 data 是 NpcProfessionDef，初始化 AI 大脑
        var brain = instance.GetComponent<NPCBrain>();
        if (brain != null && data is NpcProfessionDef npcDef)
        {
            brain.Init(npcDef);
        }

        return instance;
    }

    /// <summary>
    /// 3.0.1 §7.4 单位死亡回池（由 UnitController.Die 调用）。
    /// 立即 SetActive(false) 回桶（P0 简化：死亡动画停留表现 P2 再叠加延迟）。
    /// 出池时 SpawnUnit 会重新 Initialize + brain.Init，状态天然全新，无需手动 Reset。
    /// </summary>
    public void ReturnUnitToPool(UnitController unit)
    {
        if (unit == null) return;
        if (unit.Data == null) return;
        _instancePool.Return(unit.Data, unit.gameObject);
    }

    /// <summary>
    /// 3.0.1 §7.4 预热（战斗尖峰零 Instantiate）。按 UnitData × 数量预实例化入桶。
    /// 数量为 0/缺 prefab 自动跳过。幂等可重复调（重复预热同 data 会继续叠加）。
    /// </summary>
    public void Prewarm(UnitData data, int count)
    {
        if (data == null || count <= 0) return;
        _instancePool.Prewarm(data, count, transform);
    }

    /// <summary>
    /// 按 Faction + Occupation 直接创建单位。kingdomId 默认 0=玩家（2_16 步骤2 门面 D329）。
    /// 2_17 步骤10 Faction 收编：新建 AI 王国单位（kingdomId>0）生成后覆写阵营为 AiKingdom，
    /// 不再以 Human_Player 冒充（读档路径走 SpawnUnit(UnitData) 不经过本门面，存量旧档
    /// Human_Player+kingdomId>0 过渡兼容保留，由各处 kingdomId 双条件守卫兜底）。
    /// </summary>
    public GameObject SpawnUnit(Faction faction, Occupation occupation, Vector2 position, int kingdomId = 0)
    {
        UnitData data = UnitDataManager.Instance.GetData(faction, occupation);
        GameObject go = SpawnUnit(data, position, kingdomId);
        if (go != null && kingdomId > 0)
        {
            var uc = go.GetComponent<UnitController>();
            if (uc != null) uc.SetFaction(Faction.AiKingdom);   // 2_17 步骤10
        }
        return go;
    }

    // ===== ISaveableSpawner 实现 =====

    public void SpawnFromSave(ModuleSaveEntry entry)
    {
        if (entry.typeName != typeof(UnitSaveData).AssemblyQualifiedName) return;

        // R3: 去重检查——如果该 SaveId 已存在（可能是上次读档残留），跳过创建
        if (SaveManager.Instance.HasSaveable(entry.saveId))
        {
            Debug.LogWarning($"[UnitFactory] SaveId '{entry.saveId}' 已存在，跳过重复创建。");
            return;
        }

        var data = JsonUtility.FromJson<UnitSaveData>(entry.json);
        var faction = (Faction)data.faction;
        var occupation = (Occupation)data.occupation;

        // HH.17 裁决（决策2/3）：上帝视角君主实体已退役。旧档 occupation=Ruler 单位读档过滤，
        // 不重建君主（新建档无君主，此分支仅旧档触发——2_14 步骤14 迁移待办在此落地）。
        if (faction == Faction.Human_Player && occupation == Occupation.Ruler)
        {
            Debug.Log($"[UnitFactory] 旧档君主 occupation=Ruler 已退役（HH.17 上帝视角），读档过滤不重建（saveId={entry.saveId}）。");
            return;
        }

        UnitData config = UnitDataManager.Instance.GetData(faction, occupation);
        if (config == null)
        {
            Debug.LogError($"[UnitFactory] 找不到配置: {faction}_{occupation}，跳过。");
            return;
        }

        Vector2 pos = new Vector2(data.posX, data.posY);
        GameObject go = SpawnUnit(config, pos, data.kingdomId);  // 触发 Initialize → 注册 ISaveable（新 GUID）；kingdomId 归属

        if (go != null)
        {
            var controller = go.GetComponent<UnitController>();
            controller.OverrideSaveId(entry.saveId);  // 覆盖为存档里的 SaveId
        }
    }
}
