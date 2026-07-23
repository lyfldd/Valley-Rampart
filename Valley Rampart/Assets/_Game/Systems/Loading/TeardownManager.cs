using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 销毁阶段。标记当前销毁进度，与 LoadPhase 对称。
/// </summary>
public enum TeardownPhase
{
    Idle,              // 未执行销毁
    InputStopped,      // 输入已停
    UnitsDestroyed,    // 场景单位已销毁
    ManagerReset,      // 业务 Manager 状态已重置
    RegistriesCleared, // 注册表已清空
    Done               // 销毁完成
}

/// <summary>
/// 销毁总管。与 LoadManager 对称：LoadManager 管加载时序，TeardownManager 管销毁时序。
///
/// 核心原则：
///   - 统一销毁出口：消除 PausePanel / GameOverPanel / GameBootstrap 各自写清理逻辑的不一致。
///   - 反拓扑序销毁：依赖方先销毁，被依赖方后销毁，避免销毁过程中访问已销毁对象。
///   - 主动优于被动：显式反注册 + 销毁，不靠 LoadScene 被动销毁 + 事后扫描。
///   - 不接管业务逻辑：不调 Save、不 SetState，只管"按顺序反注册 + 销毁 + 清空注册表"。
///
/// 三级 API：
///   Level 1 TeardownScene          — 场景级清理（读档前 / 新游戏前）
///   Level 2 TeardownForReturnToMenu — 返回主菜单（场景级 + 业务重置 + LoadScene）
///   Level 3 TeardownForQuit         — 退出游戏（轻量清理，Unity 自动回收）
/// </summary>
public class TeardownManager : Singleton<TeardownManager>
{
    private const string MainMenuSceneName = "MainMenuScene";

    public TeardownPhase CurrentPhase { get; private set; } = TeardownPhase.Idle;

    // ===== Level 1：场景级清理（读档前 / 新游戏前）=====

    /// <summary>
    /// 清理场景内运行时对象（单位），不动 Core Manager，不动业务 Manager 订阅。
    /// 顺序：先反注册（纯字典操作，立即生效）→ 后 Destroy（延迟到帧末）→ 清引用 → 兜底扫描。
    /// 调用方：GameBootstrap.ContinueFromSave（替代 RulerController.DestroyAllSceneUnits）。
    /// </summary>
    public void TeardownScene()
    {
        Debug.Log("[TeardownManager] Level 1：场景级清理...");

        // ① 遍历场景内所有单位：反注册 + 销毁
        var allUnits = FindObjectsOfType<UnitController>();
        int destroyed = 0;

        foreach (var unit in allUnits)
        {
            if (unit == null || unit.gameObject == null) continue;

            SaveManager.Instance.UnregisterSaveable(unit);
            UnitRegistry.Instance.Unregister(unit);
            Destroy(unit.gameObject);
            destroyed++;
        }

        if (destroyed > 0)
        {
            Debug.Log($"[TeardownManager] 销毁 {destroyed} 个场景单位");
        }

        // ② 清除君主引用（单位已在 ① 中销毁，此处只置 null 防残留）
        if (RulerController.Instance != null)
        {
            RulerController.Instance.ClearMonarchReference();
        }

        // ③ 兜底清理已销毁对象的 ISaveable 残留
        SaveManager.Instance.CleanupDestroyedSaveables();

        CurrentPhase = TeardownPhase.UnitsDestroyed;
    }

    // ===== Level 2：返回主菜单（场景级 + 业务重置）=====

    /// <summary>
    /// 返回主菜单：停输入 → 可选保存 → 场景清理 → 业务重置 → 清注册表 → LoadScene。
    /// 业务 Manager 只重置运行时状态，不反订阅、不反注册 ISaveable（Manager 保留，订阅和注册继续用）。
    /// 调用方：PausePanel.OnQuitClicked（save=true）、GameOverPanel.OnBackToMenuClicked（save=false）。
    /// </summary>
    public void TeardownForReturnToMenu(bool saveBeforeTeardown)
    {
        Debug.Log($"[TeardownManager] Level 2：返回主菜单（save={saveBeforeTeardown}）...");

        // ① 停输入，防销毁中误触事件
        InputManager.Instance.DisableInput();
        CurrentPhase = TeardownPhase.InputStopped;

        // ② 恢复时间缩放（暂停/结算时 timeScale=0，不恢复则主菜单冻住）
        Time.timeScale = 1f;

        // ③ 保存（Pause 要存，GameOver 不存）
        if (saveBeforeTeardown)
        {
            string slotId = SaveManager.Instance.CurrentSlotId ?? "slot_1";
            SaveManager.Instance.Save(slotId);
        }

        // ④ 场景级清理（复用 Level 1：反注册 + 销毁所有单位 + 清君主引用）
        TeardownScene();

        // ⑤ 业务 Manager 重置运行时状态（不反订阅、不反注册 ISaveable）
        if (TimeManager.Instance != null)
            TimeManager.Instance.ResetState();
        if (DifficultyManager.Instance != null)
            DifficultyManager.Instance.ResetState();
        if (RulerController.Instance != null)
            RulerController.Instance.ResetState();
        CurrentPhase = TeardownPhase.ManagerReset;

        // ⑥ 清空单位注册表
        UnitRegistry.Instance.Clear();

        // ⑦ 清理已销毁单位的 ISaveable 残留
        SaveManager.Instance.CleanupDestroyedSaveables();
        CurrentPhase = TeardownPhase.RegistriesCleared;

        // ⑧ 加载主菜单场景（UI Panel 等场景对象随 LoadScene 自动销毁）
        Debug.Log("[TeardownManager] Level 2 完成，加载主菜单场景");
        SceneManager.LoadScene(MainMenuSceneName);
        CurrentPhase = TeardownPhase.Done;
    }

    // ===== Level 3：退出游戏（轻量清理）=====

    /// <summary>
    /// 退出游戏。进程退出时 Unity 自动回收内存，各 Manager 的 OnDestroy 由 Unity 自动调用。
    /// EventBus.Clear() 在 OnDestroy 前后都安全（Unsubscribe 对已清空字典是 no-op）。
    /// </summary>
    public void TeardownForQuit()
    {
        Debug.Log("[TeardownManager] Level 3：退出游戏...");

        if (InputManager.Instance != null)
            InputManager.Instance.DisableInput();

        TeardownScene();

        EventBus.Clear();

        CurrentPhase = TeardownPhase.Done;
    }

    protected override void OnApplicationQuit()
    {
        base.OnApplicationQuit();
        TeardownForQuit();
    }
}
