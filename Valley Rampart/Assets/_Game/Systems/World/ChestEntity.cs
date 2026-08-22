using System;
using UnityEngine;

/// <summary>
/// 掉落箱子实体（2_12 步骤7C + 步骤11 / D269 统一资源容器 D142）。
/// 落地资源统一用箱子承载：生成→落格可拾、限期消失、命中破碎洒内容、任意阵营可拾。
/// 本类为**数据容器 + 交互面**，详见 ChestManager（唯一归属者）。渲染归 2_10（"treasure_box" sprite），
/// 移动/拾取动画归 2_3，搬运调度归 2_8，存档归 2_11（ISaveable）。
/// 继承 MonoBehaviour，不逃入 Building 建筑管线（无造价/无 FSM/不产出的轻量可拾物）。
/// </summary>
public class ChestEntity : MonoBehaviour, IInteractable
{
    /// <summary>挂格坐标（楼层=l0 微格）。</summary>
    public GridCoord cell;

    /// <summary>箱子内容物（四资源包）。D145 容量同工人携带量，由生成方填。</summary>
    public ResourcePack contents;

    /// <summary>生成的绝对天数，过期 = 生成天 + ChestConfig.expireDays（D148）。</summary>
    public float bornDay;

    /// <summary>拾取示例（任意阵营可拾 D146）。由 ChestManager 拾取调用。</summary>
    public Faction ownerFaction = Faction.None;

    /// <summary>HP=1 一击碎（D247），破碎后内容物返回地面可再拾。</summary>
    public int hp = 1;

    private SpriteRenderer _renderer;
    private bool _initialized;

    /// <summary>初始化（幂等，仅首次生效）。cell/contents/bornDay 由 ChestManager.SpawnChest 预先填。</summary>
    public void Init(GridCoord c, ResourcePack pack, float day)
    {
        if (_initialized) return;
        cell = c;
        contents = pack;
        bornDay = day;
        _initialized = true;
    }

    void Awake()
    {
        Render();
    }

    /// <summary>渲染箱子占位（2_10 替换成真实瓦片）。</summary>
    private void Render()
    {
        if (_renderer == null) _renderer = gameObject.AddComponent<SpriteRenderer>();
        _renderer.sprite = PlaceholderSprites.Get("treasure_box");
        _renderer.sortingOrder = 5;
    }

    /// <summary>箱子是否已空/失效（内容空或已销毁）。</summary>
    public bool IsEmpty => contents.IsZero;

    // ===== IInteractable =====
    public InteractionResult Interact(Interactor ctx)
    {
        // 任意阵营可拾取（D146）。交由 ChestManager 统一拾取逻辑（防重复拾取/退订清算）。
        if (ChestManager.HasInstance) ChestManager.Instance.Pickup(this, ctx);
        return InteractionResult.None;
    }

    // ===== 命中（D247）：HP=1 一击碎，内容物返回地面可再拾 =====
    public void Strike()
    {
        if (--hp <= 0)
        {
            // 破碎：内容物原地重新落箱（可再拾取）
            if (ChestManager.HasInstance) ChestManager.Instance.ResetDrop(this);
        }
    }
}