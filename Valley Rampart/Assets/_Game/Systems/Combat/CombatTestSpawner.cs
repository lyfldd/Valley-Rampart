using System.Collections;
using UnityEngine;

/// <summary>
/// 战斗系统验证场景生成器（3.4 + 3.0.1）。
/// 挂在 GameScene 的任意 GameObject 上，游戏启动后自动生成测试单位。
///
/// 验证场景：2近战+1远程士兵 vs 3敌人(含1远程) + 1工人旁观
/// 3.4 验证：有 NPC 死亡 + 远程投射物命中 + 受击闪红 + 无报错
/// 3.0.1 验证：NPCBrain 驱动攻击 + 工人受威胁撤退 + 受击->威胁3->撤退闭环
/// </summary>
public class CombatTestSpawner : MonoBehaviour
{
    [Header("生成位置（y=-3 为地面基线）")]
    [SerializeField] private bool _autoSpawn = true;

    private void Start()
    {
        if (_autoSpawn) StartCoroutine(SpawnAfterInit());
    }

    /// <summary>等待 UnitDataManager 初始化后生成测试单位。</summary>
    private IEnumerator SpawnAfterInit()
    {
        // 等待 UnitDataManager 就绪
        while (UnitDataManager.Instance == null || !UnitDataManager.Instance.IsInitialized)
            yield return null;

        // 等待 GridSystem 就绪
        while (GridSystem.Instance == null || GridSystem.Instance.Config == null)
            yield return null;

        yield return new WaitForSeconds(0.5f); // 等一帧让其他系统稳定

        SpawnTestUnits();
    }

    [ContextMenu("生成测试单位")]
    public void SpawnTestUnits()
    {
        Debug.Log("[3.4 验证] 开始生成测试单位...");

        // ===== 玩家方（左侧，x=-4~-7）=====
        // 2 近战士兵
        SpawnUnit(Faction.Human_Player, Occupation.Warrior, new Vector2(-5f, -3f));
        SpawnUnit(Faction.Human_Player, Occupation.Warrior, new Vector2(-4f, -3f));

        // 1 远程士兵
        SpawnUnit(Faction.Human_Player, Occupation.Archer, new Vector2(-6f, -3f));

        // 1 工人旁观（无攻击能力）
        SpawnUnit(Faction.Human_Player, Occupation.Civilian, new Vector2(-7f, -3f));

        // ===== 敌方（右侧，x=4~6）=====
        // 2 近战敌人
        SpawnUnit(Faction.Undead, Occupation.Warrior, new Vector2(5f, -3f));
        SpawnUnit(Faction.Undead, Occupation.Warrior, new Vector2(4f, -3f));

        // 1 远程敌人
        SpawnUnit(Faction.Undead, Occupation.Archer, new Vector2(6f, -3f));

        Debug.Log("[3.4 验证] 测试单位生成完成。2近战+1远程 vs 3敌人(含1远程) + 1工人旁观");
    }

    /// <summary>生成单个单位并注册到 GridSystem。</summary>
    private void SpawnUnit(Faction faction, Occupation occupation, Vector2 position)
    {
        GameObject go = UnitFactory.Instance?.SpawnUnit(faction, occupation, position);
        if (go == null)
        {
            Debug.LogError($"[3.4 验证] 生成失败: {faction}_{occupation}");
            return;
        }

        // 注册到 GridSystem（空间分区查目标/投射物到达检测依赖此注册）
        var controller = go.GetComponent<UnitController>();
        if (controller != null && GridSystem.Instance != null)
        {
            GridCoord coord = GridSystem.Instance.WorldToCoord(position);
            GridSystem.Instance.TryEnter(controller, coord);
        }

        Debug.Log($"[3.4 验证] 生成: {faction}_{occupation} @ {position}");
    }
}
