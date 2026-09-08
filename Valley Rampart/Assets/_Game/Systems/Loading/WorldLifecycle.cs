using UnityEngine;

/// <summary>
/// 世界生命周期编排门面（D520 冒烟自动化批 / HH.62）。
/// 同场景「清场→重建」编排：不 LoadScene，留在 GameScene 内完成世界销毁与解锁二次建局。
/// 纯编排现有接口：不碰业务逻辑、不改现有系统内部（红线2）；对外暴露走门面方法（接口纪律 D520）。
/// 潜在复用方：2_14 传送门跨地图（跨图前清场）。
/// </summary>
public static class WorldLifecycle
{
    /// <summary>
    /// 清场→重建编排。调用约定（陷阱2）：调用方应在调用后留一帧（yield return null）
    /// 让 Destroy 落地（BuildingFactory.ClearAllBuildings 用 Destroy 延迟帧末销毁），再建新世界，
    /// 避免旧 GameObject 与新世界共存。清场后 GameState=Loading（陷阱1：关只在 Playing 跑的 Update）。
    /// </summary>
    public static void ResetWorldForNext()
    {
        // ① 停输入 + 恢复 timeScale（防销毁中误触 / 暂停/结算 timeScale=0 残留）
        if (InputManager.Instance != null) InputManager.Instance.DisableInput();
        Time.timeScale = 1f;

        // ② 陷阱1：GameStateManager 覆盖——从 Playing 拉出，关只在 Playing 跑的 Update
        //    （ThroneAnchor 轮询窗等；Singleton 无 ResetState，SetState 是唯一公开复位口）
        if (GameStateManager.Instance != null)
            GameStateManager.Instance.SetState(GameState.Loading);

        // ③ 场景级清理（Level 1：销毁全部场景单位 + 清君主引用 + CleanupDestroyedSaveables）
        if (TeardownManager.Instance != null)
            TeardownManager.Instance.TeardownScene();

        // ④ 业务 Manager ResetState 全家（复用 Level 2 ⑤ 序：依赖方先→被依赖方后→会话级最后）
        if (TimeManager.Instance != null) TimeManager.Instance.ResetState();
        if (DifficultyManager.Instance != null) DifficultyManager.Instance.ResetState();
        if (RulerController.Instance != null) RulerController.Instance.ResetState();
        if (KingdomManager.Instance != null) KingdomManager.Instance.ResetState();
        if (PopulationSystem.Instance != null) PopulationSystem.Instance.ResetState();
        if (WaterNetwork.Instance != null) WaterNetwork.Instance.ResetState();   // T13/M10（HH.92）：AI 水桶跨轮残留清偿——策划侦察实锤缺口（正式局换局同样受益）
        if (RanchSystem.Instance != null) RanchSystem.Instance.ResetState();
        if (SiegeProductionSystem.Instance != null) SiegeProductionSystem.Instance.ResetState();

        // ⑤ 散点清场（Level 2 靠 LoadScene 兜底，同场景重建必须显式清）
        if (GridSystem.Instance != null) GridSystem.Instance.ClearAll();
        if (BuildingFactory.Instance != null) BuildingFactory.Instance.ClearAllBuildings();
        if (TrainingSystem.Instance != null) TrainingSystem.Instance.ResetState();   // DZ-075（HH.109 件2 方案B）：ClearAllBuildings 直接 Destroy 不走 Die，_queues 死键+死 unit 引用滞留——跨轮清偿（HH.93 T13 同族编排）
        if (ChestManager.Instance != null) ChestManager.Instance.ClearAll();
        if (KingdomRegistry.Instance != null) KingdomRegistry.Instance.ResetState();
        if (KingdomBrainRegistry.Instance != null) KingdomBrainRegistry.Instance.ResetState();   // T13/M10（HH.92）：AI 决策注册跨轮残留清偿（策划侦察实锤缺口）
        if (OverheadSpeechManager.Instance != null) OverheadSpeechManager.Instance.ResetState(); // T13/M10（HH.92）：表现层气泡残留（低危，同批补齐）
        if (VagrantCampSystem.Instance != null) VagrantCampSystem.Instance.ResetState();   // HH.66 段B#3（D522 挂账）：跨轮营地记录残留清偿
        if (DamageSystem.Instance != null) DamageSystem.Instance.ResetState();   // T13（HH.92）：攻击注册三表跨轮残留清偿（THD R3 MissingReference 4586 次/轮实证：死塔注册+池化 target 存活）
        if (MapRenderService.Instance != null) MapRenderService.Instance.ClearAllTiles();
        if (TerritoryOverlay.Instance != null) TerritoryOverlay.Instance.ClearOverlay();   // 2_10 步骤13：清场同步清染色层（跨轮零残留）
        // AttentionSystem 非全局单例（每 NPCBrain 私有成员 _attention，无 Instance）——
        // 随 TeardownScene 销毁全部单位自动清空（ClearAll/ClearThreats 仅 NPCBrain 内部用），无需显式清。

        // ⑥ 注册表清空 + 世界清空（清 ActiveMap，解锁二次建局）
        if (UnitRegistry.Instance != null) UnitRegistry.Instance.Clear();
        if (WorldManager.Instance != null) WorldManager.Instance.ResetState();

        // ⑦ 会话级状态（槽位/自动存档计数；不清存档文件）
        if (SaveManager.Instance != null) SaveManager.Instance.ResetSessionState();

        Debug.Log("[WorldLifecycle] ResetWorldForNext: 清场完成，可重建新世界");
    }
}
