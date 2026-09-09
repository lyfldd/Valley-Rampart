using UnityEngine;

/// <summary>
/// 战争机器放置模式（HH.111 件2 生产接线；BuildModeEntry 虚拟栈条目先例同构）。
/// 无面板，代表「机器放置模式」占栈位：入栈激活放置器，出栈退出并重开生产面板（返回刷新余量）。
///
/// 放置合法性（件2.2 与建筑放置规则对齐，API 全同源 GridSystem/BuildingRegistry——与
/// PlacementValidator 同一套格子语义，1 格无 footprint）：界内+非水（WalkFlags.Water）+
/// 可走（IsWalkable）+非障碍（IsObstacle）+无建筑占格（BuildingRegistry.GetAt）。
/// 点选合法格 → SiegeProductionSystem.ProduceMachine(type, pos)（玩家 overload：扣费/白名单/上限
/// 校验全在函数内，UI 只传 type+pos——M3：真校验以函数返回为准，UI 侧预检仅 toast 文案对齐）。
///
/// 输入模式复用 InputMode.Build（禁交互点击，防误选单位/建筑）；鼠标轮询走 legacy Input
/// （BuildController 同款——Build 模式下 InputManager 不发布左/右键事件）。
/// </summary>
public class MachinePlacementEntry : IUIStackEntry
{
    private readonly Occupation _type;
    private readonly IUIPanel _returnTo;   // 放置结束后返回的面板（MachinePanel，余量刷新）

    public MachinePlacementEntry(Occupation type, IUIPanel returnTo = null)
    {
        _type = type;
        _returnTo = returnTo;
    }

    public void Open(Interactor ctx)
    {
        MachinePlacer.Instance?.Begin(_type);
    }

    public void Close()
    {
        MachinePlacer.Instance?.End();
        // 退出放置后重开生产面板（BuildModeEntry 先例：ESC/完成均回菜单）
        if (_returnTo != null && UIManager.Instance != null)
            UIManager.Instance.Push(_returnTo, new Interactor(Faction.PlayerCamp, Vector3.zero));
    }
}

/// <summary>
/// 机器放置器（HH.111 件2）：ghost 跟随鼠标+微格吸附+绿/红反馈（BuildController.Update 先例同构）；
/// 左键放置（合法格 → ProduceMachine → 成功/失败均 Pop 回面板），右键退出。
/// </summary>
public class MachinePlacer : Singleton<MachinePlacer>
{
    private Occupation _type;
    private bool _placing;
    private GameObject _ghost;
    private SpriteRenderer _ghostRenderer;

    public void Begin(Occupation type)
    {
        if (_placing) End();
        _type = type;
        _placing = true;
        InputManager.Instance?.SetMode(InputMode.Build);   // 复用 Build 模式：禁交互点击（防误选）
        CreateGhost();
        Debug.Log($"[MachinePlacer] 进入机器放置模式: {type}");
    }

    public void End()
    {
        if (!_placing) return;
        _placing = false;
        if (_ghost != null) Destroy(_ghost);
        _ghost = null;
        _ghostRenderer = null;
        InputManager.Instance?.SetMode(InputMode.Normal);
        Debug.Log("[MachinePlacer] 退出机器放置模式");
    }

    void Update()
    {
        if (!_placing || Camera.main == null || GridSystem.Instance == null) return;

        // ghost 跟随鼠标 + 微格吸附 + 绿/红反馈（BuildController.Update 先例同构）
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        var subOpt = GridSystem.Instance.WorldToSubCoord(mouseWorld);
        if (subOpt.HasValue)
        {
            GridCoord sub = subOpt.Value;
            var cell = GridSystem.Instance.SubToCell(sub);
            if (_ghost != null) _ghost.transform.position = GridSystem.Instance.CoordToWorld(cell);
            var cfg = PlacementValidator.BuildConfig;
            bool ok = CanPlaceCell(cell);
            if (_ghostRenderer != null)
                _ghostRenderer.color = ok
                    ? (cfg != null ? cfg.previewColorOk : new Color(0, 1, 0, 0.5f))
                    : (cfg != null ? cfg.previewColorBad : new Color(1, 0, 0, 0.5f));
        }

        // 左键放置（Build 模式下 InputManager 不发事件，legacy Input 轮询=BuildController 同款）
        if (Input.GetMouseButtonDown(0))
        {
            TrySpawn();
        }

        // 右键退出（ESC 由 UIManager 栈统一处理，关栈顶 MachinePlacementEntry）
        if (Input.GetMouseButtonDown(1))
        {
            UIManager.Instance?.Pop();
        }
    }

    /// <summary>
    /// 单格放置合法性（件2.2：与建筑放置规则对齐，不可放水/占格；API 与 PlacementValidator 同源）。
    /// static 供冒烟探针直调（P1/P5 行为级锚）。
    /// </summary>
    public static bool CanPlaceCell(GridCoord cell)
    {
        var grid = GridSystem.Instance;
        if (grid == null) return false;
        if (!grid.IsInBounds(cell)) return false;
        var flags = grid.GetWalkFlags(cell);
        if ((flags & WalkFlags.Water) != 0) return false;   // 不可放水（桥特例不适用机器）
        if (!grid.IsWalkable(cell)) return false;
        if (grid.IsObstacle(cell)) return false;
        if (BuildingRegistry.Instance != null && BuildingRegistry.Instance.GetAt(cell) != null) return false;   // 无建筑占格
        return true;
    }

    void TrySpawn()
    {
        if (Camera.main == null || GridSystem.Instance == null) return;

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        var subOpt = GridSystem.Instance.WorldToSubCoord(mouseWorld);
        if (!subOpt.HasValue) return;
        var cell = GridSystem.Instance.SubToCell(subOpt.Value);

        if (!CanPlaceCell(cell))
        {
            ToastManager.Instance?.Show("此处不可放置（水域/障碍/占格）", 3f);
            Debug.Log("[MachinePlacer] 放置点非法（水/障碍/占格），拒绝。");
            return;
        }

        // UI 侧预检仅决定 toast 文案（M3：真校验以 ProduceMachine 返回为准）
        var sys = SiegeProductionSystem.Instance;
        if (sys != null)
        {
            if (sys.GetPlacedMachineCount() >= sys.GetMachineLimit())
            {
                ToastManager.Instance?.Show($"机器已达上限 {sys.GetMachineLimit()}（需升级战争机器工坊）", 3f);
                Debug.Log("[MachinePlacer] 上限满，拒绝。");
                UIManager.Instance?.Pop();
                return;
            }
            var cost = sys.PeekMachineCost(_type);
            if (RulerController.Instance != null && !RulerController.Instance.CanAfford(cost))
            {
                ToastManager.Instance?.Show("资源不足，无法生产机器", 3f);
                Debug.Log("[MachinePlacer] 资源不足，拒绝。");
                UIManager.Instance?.Pop();
                return;
            }
        }

        Vector2 spawnPos = GridSystem.Instance.CoordToWorld(cell);
        bool ok = SiegeProductionSystem.Instance != null
            && SiegeProductionSystem.Instance.ProduceMachine(_type, spawnPos);
        if (ok)
        {
            ToastManager.Instance?.Show($"「{TrainingPanel.OccName(_type)}」已开工生产", 3f);
        }
        else
        {
            // 兜底（白名单/资源/上限三分支已在函数内日志；正常流程预检后到不了这里）
            ToastManager.Instance?.Show("生产失败（详见日志）", 3f);
        }
        // 成功/失败均退出放置回面板（余量/置灰态刷新；上限低无连放需求，Shift 连放不实现=最小面）
        UIManager.Instance?.Pop();
    }

    /// <summary>ghost 占位视觉（1 格色块；BuildController 占位先例同构，机器无 prefab/BuildingRole 域）。</summary>
    void CreateGhost()
    {
        _ghost = new GameObject("MachineGhost");
        _ghostRenderer = _ghost.AddComponent<SpriteRenderer>();
        var tex = Texture2D.whiteTexture;
        _ghostRenderer.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), tex.width);   // 1 world unit
        float cellW = GridSystem.Instance != null && GridSystem.Instance.Config != null
            ? GridSystem.Instance.Config.cellSize.x : 1.28f;
        float cellH = GridSystem.Instance != null && GridSystem.Instance.Config != null
            ? GridSystem.Instance.Config.cellSize.y : 0.64f;
        _ghost.transform.localScale = new Vector3(cellW, cellH, 1f);
        var cfg = PlacementValidator.BuildConfig;
        _ghostRenderer.color = cfg != null ? cfg.previewColorOk : new Color(0, 1, 0, 0.5f);
    }
}
