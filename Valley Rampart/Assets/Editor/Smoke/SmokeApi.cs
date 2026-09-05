using UnityEngine;
using UnityEditor;

/// <summary>
/// 冒烟自动化门面（D520 / HH.62，Editor-only）。
/// 封装「进局→探针→清场→重建」外部调用面：EnterGame 复用 InitializeNewGame 真实链路，
/// ResetWorldForNext 走 WorldLifecycle 同场景清场，QuitSmoke 收尾退出 Play。
/// 不进入主流程代码/构建（红线5）。
/// </summary>
public static class SmokeApi
{
    /// <summary>
    /// 新建进局（等价用户进局真实链路 = GameBootstrap.StartNewGame 全链）。
    /// 幂等守卫：若 ActiveMap 已存在（真机进局/前轮残留）先 ResetWorldForNext 再建，
    /// 防「ActiveMap 未清时二次 InitializeNewGame 卡死」。调用方应在 EnterGame 后留帧等 ActiveMap。
    /// </summary>
    public static void EnterGame(NewGameConfig config)
    {
        if (config == null)
        {
            Debug.LogError("[SmokeApi] EnterGame: config 为空。");
            return;
        }

        if (WorldManager.Instance != null && WorldManager.Instance.ActiveMap != null)
            WorldLifecycle.ResetWorldForNext();

        GameSceneEntrance.SetNewGame(config);
        LoadManager.Instance.InitializeNewGame(config);
        SaveManager.Instance.ResetAutoSaveCounter();
        PopulationSystem.Instance.SpawnInitialEntities();
        VagrantCampSystem.Instance.OnNewGameMapReady();

        string slotId = config.selectedSlotId;
        if (!string.IsNullOrEmpty(slotId) && !SaveManager.Instance.HasSave(slotId))
            SaveManager.Instance.Save(slotId);

        Debug.Log($"[SmokeApi] EnterGame: 族={config.raceId} seed={config.worldSeed} 难度={config.difficulty} 世界已建");
    }

    /// <summary>同场景清场→重建（冒烟轮次间调用，无 LoadScene 兜底）。</summary>
    public static void ResetWorldForNext() => WorldLifecycle.ResetWorldForNext();

    /// <summary>收尾：清场 + 冒烟槽位存档自愈清理 + 退出 Play 模式（点一次菜单全程自动化的闭环出口）。</summary>
    public static void QuitSmoke()
    {
        WorldLifecycle.ResetWorldForNext();
        // HH.66 段B#2（HH.65 §六.4 挂账清偿）：smoke_ 前缀槽位自动清（防堆积复发）；
        // 走 SaveManager 单一口径（D520 接口纪律：门面方法暴露能力，Editor 侧禁散落拼路径）
        if (SaveManager.Instance != null)
        {
            int n = SaveManager.Instance.DeleteSlotsWithPrefix("smoke_");
            if (n > 0) Debug.Log($"[SmokeApi] QuitSmoke: 清理冒烟槽位存档 smoke_*.json ×{n}（防堆积自愈）");
        }
        Debug.Log("[SmokeApi] QuitSmoke: 冒烟全部完成，退出 Play 模式。");
        EditorApplication.ExitPlaymode();
    }
}
