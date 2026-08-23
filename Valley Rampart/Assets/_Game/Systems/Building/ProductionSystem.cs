using UnityEngine;

/// <summary>
/// 产能调度系统（3.3.4 批次5）。单例，每秒统一遍历所有 ProducerComponent 调 Tick。
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

    private void TickAll()
    {
        if (BuildingRegistry.Instance == null) return;
        var all = BuildingRegistry.Instance.All;
        for (int i = 0; i < all.Count; i++)
        {
            var b = all[i];
            if (b == null) continue;
            var producer = b.GetComponent<ProducerComponent>();
            if (producer != null) producer.Tick();
            // 2_12 步骤8：铁匠铺逐秒加工（石→Metal，D199~D201）
            var blacksmith = b.GetComponent<BlacksmithBuilding>();
            if (blacksmith != null) blacksmith.Tick();
        }
    }

    /// <summary>显式暂停/恢复产能 tick（游戏暂停时 timeScale=0 已天然停止）。</summary>
    public void SetPaused(bool paused) { _paused = paused; }
}
