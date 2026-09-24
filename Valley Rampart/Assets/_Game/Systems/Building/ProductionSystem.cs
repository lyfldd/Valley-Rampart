using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 产能调度系统（3.3.4 批次5）。单例，每秒统一遍历所有建筑的 `ITickable` 组件调 Tick
/// （⭐ `M4-A`／`HH.329`：⛔ 不再点名具体组件类 —— 加带 `Tick` 的组件只实现 `ITickable` ＋ 数据行填键）。
/// 集中调度优势：支持暂停、易存档、性能好（O(n) 遍历无每帧 Update）。
/// 暂停时（Time.timeScale=0）Update 自然停推；额外 SetPaused 供显式控制。
/// </summary>
public class ProductionSystem : Singleton<ProductionSystem>
{
    private float _timer;
    private float _tickInterval = 1f;
    private bool _paused;

    public bool IsPaused => _paused;

    private void Update()
    {
        if (_paused) return;
        _timer += Time.deltaTime;
        if (_timer >= _tickInterval)
        {
            _timer -= _tickInterval;
            TickAll();
        }
    }

    // 【HH.329 件2】`ITickable` 遍历缓冲（成员持有 ⇒ 稳态零分配 · ⛔ 禁每次 new List）
    private readonly List<ITickable> _tickBuf = new List<ITickable>(8);

    private void TickAll()
    {
        if (BuildingRegistry.Instance == null) return;
        var all = BuildingRegistry.Instance.All;
        for (int i = 0; i < all.Count; i++)
        {
            var b = all[i];
            if (b == null) continue;
            // ⭐ `M4-A`：**遍历 `ITickable`**（⛔ 不再点名 4 个具体组件类）——
            //   新增带 `Tick` 的组件 ⇒ 实现 `ITickable` ＋ 数据行填键即可，**不用改本文件**。
            //   ⚠️ 先 `Clear()` 再 `GetComponents`：无论该重载是「清后填」还是「追加」，都只得到一份 ⇒ 不会双 tick。
            _tickBuf.Clear();
            b.GetComponents<ITickable>(_tickBuf);
            for (int j = 0; j < _tickBuf.Count; j++)
            {
                var t = _tickBuf[j];
                if (t == null) continue;
                if (t is UnityEngine.Object uo && uo == null) continue;   // 假 null 兜底（接口静态类型）
                t.Tick();
            }
        }
    }

    /// <summary>显式暂停/恢复产能 tick（游戏暂停时 timeScale=0 已天然停止）。</summary>
    public void SetPaused(bool paused) { _paused = paused; }
}
