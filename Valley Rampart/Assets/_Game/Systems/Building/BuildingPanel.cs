using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 建筑面板（3.3 第七节）。挂在 SampleScene 的 BuildingPanel GameObject 上（UIDocument）。
/// 实现 IUIPanel，由 InteractionManager 通过 UIManager 打开。
/// 显示：名称/等级/HP/攻防/产能/描述 + 升级/拆除按钮（按字段条件渲染，无则隐藏）。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class BuildingPanel : MonoBehaviour, IUIPanel
{
    private Building _target;
    private bool _buttonsBound;
    private Interactor _lastCtx;       // 上一次打开面板的交互发起者（打开子面板时透传）

    // ===== UI 元素引用 =====
    private Label _nameLabel;
    private Label _levelLabel;
    private VisualElement _hpFill;
    private Label _hpText;
    private VisualElement _descBlock;
    private Label _descLabel;
    private VisualElement _combatRow;
    private Label _attackValue;
    private Label _defenseValue;
    private Label _rangeValue;
    private VisualElement _producerRow;
    private Label _producerRate;
    private Label _producerCap;
    private Label _footprintValue;
    private Label _factionValue;
    private Label _obstacleValue;
    private Button _upgradeButton;
    private Button _demolishButton;
    private Button _closeButton;
    private Button _harvestButton;

    // ===== 功能区（3.5 建筑级功能，按建筑类型动态渲染）=====
    private VisualElement _functionArea;
    private VisualElement _functionRows;

    // ===== IUIPanel =====

    public void Open(Interactor ctx)
    {
        if (!_buttonsBound) BindButtons();
        _lastCtx = ctx;
        Refresh();
        SetVisible(true);
    }

    public void Close()
    {
        SetVisible(false);
        _target = null;
    }

    public void Refresh()
    {
        if (_target == null || _target.def == null) return;

        var def = _target.def;

        // 标题：废弃态显示"废弃城堡"，修复后(Active)显示 def.displayName（主城）
        if (_nameLabel != null)
            _nameLabel.text = _target.state == BuildingState.Abandoned ? "废弃城堡"
                : (_target.state == BuildingState.Ruined ? "废墟" : def.displayName);

        // 废墟重建按钮（Ruined 态，2_12 步骤7 / D156：修复=同建造，成本 D155）
        if (_target.state == BuildingState.Ruined)
        {
            if (_upgradeButton != null)
            {
                _upgradeButton.style.display = DisplayStyle.Flex;
                var rc = _target.GetRepairCost();
                _upgradeButton.text = $"重建废墟 (金{rc.Get(ResourceType.Gold)} 石{rc.Get(ResourceType.Stone)} 木{rc.Get(ResourceType.Wood)} 粮{rc.Get(ResourceType.Food)})";
                _upgradeButton.SetEnabled(WarehouseHelper.CanAfford(rc));
            }
            if (_demolishButton != null) _demolishButton.style.display = DisplayStyle.None;
            return;
        }

        // 修复按钮（Abandoned 态，复用升级按钮；3.3.4 批次7）
        // 3.5 步骤2：修复消耗按 KingdomConfig Lv1 修复档（§2.1 木10/石6/粮6/金2），回退 def.cost
        if (_target.state == BuildingState.Abandoned)
        {
            if (_upgradeButton != null)
            {
                _upgradeButton.style.display = DisplayStyle.Flex;
                var repairCost = GetRepairCost();
                _upgradeButton.text = $"修复 (金{repairCost.Get(ResourceType.Gold)} 石{repairCost.Get(ResourceType.Stone)} 木{repairCost.Get(ResourceType.Wood)} 粮{repairCost.Get(ResourceType.Food)})";
                _upgradeButton.SetEnabled(RulerController.Instance != null && RulerController.Instance.CanAfford(repairCost));
            }
            if (_demolishButton != null) _demolishButton.style.display = DisplayStyle.None;
            return;
        }

        if (_levelLabel != null) _levelLabel.text = $"Lv.{_target.level}";

        // HP
        float hpRatio = _target.maxHp > 0 ? (float)_target.hp / _target.maxHp : 0f;
        if (_hpFill != null) _hpFill.style.width = new StyleLength(new Length(Mathf.Clamp01(hpRatio) * 100, LengthUnit.Percent));
        if (_hpText != null) _hpText.text = $"{_target.hp}/{_target.maxHp}";

        // 描述（条件渲染）
        bool hasDesc = !string.IsNullOrEmpty(def.description);
        if (_descBlock != null) _descBlock.style.display = hasDesc ? DisplayStyle.Flex : DisplayStyle.None;
        if (_descLabel != null && hasDesc) _descLabel.text = def.description;

        // 攻防行（条件渲染：有 combat.attack 或 combat.defense 或 combat.range）
        bool hasCombat = def.combat.attack > 0 || def.combat.defense > 0 || def.combat.range > 0f;
        if (_combatRow != null) _combatRow.style.display = hasCombat ? DisplayStyle.Flex : DisplayStyle.None;
        if (_attackValue != null) _attackValue.text = def.combat.attack > 0 ? def.combat.attack.ToString() : "-";
        if (_defenseValue != null) _defenseValue.text = def.combat.defense > 0 ? def.combat.defense.ToString() : "-";
        if (_rangeValue != null) _rangeValue.text = def.combat.range > 0f ? def.combat.range.ToString("F1") : "-";

        // 产能行（条件渲染）
        bool hasProducer = def.producer.rate > 0f || def.producer.capacity > 0;
        if (_producerRow != null) _producerRow.style.display = hasProducer ? DisplayStyle.Flex : DisplayStyle.None;
        if (_producerRate != null) _producerRate.text = def.producer.rate > 0f ? $"{def.producer.rate:F1}/s" : "-";
        if (_producerCap != null) _producerCap.text = def.producer.capacity > 0 ? def.producer.capacity.ToString() : "-";

        // 基础信息（2_2：footprint w×h）
        if (_footprintValue != null)
            _footprintValue.text = $"{Mathf.Max(1, _target.footprint.x)}×{Mathf.Max(1, _target.footprint.y)}格";
        if (_factionValue != null) _factionValue.text = FactionDisplayName(def.faction);
        if (_obstacleValue != null) _obstacleValue.text = _target.isObstacle ? "是" : "否";

        // 升级按钮（3.5：主城升级走 KingdomManager；普通建筑升级受模块级门控）
        bool isCastle = _target.sourceType == BuildingType.CastleCore;
        bool hasUpgradeSlot = _target.isPlayerBuilt
            && def.levels != null
            && def.levels.Length > 0
            && _target.level - 1 < def.levels.Length;
        bool moduleOk = KingdomManager.Instance == null || KingdomManager.Instance.CanUpgradeBuilding(_target);
        bool canUpgrade = isCastle
            ? (KingdomManager.Instance != null && KingdomManager.Instance.CastleLevel >= 1 && KingdomManager.Instance.CastleLevel < 6)
            : hasUpgradeSlot;
        if (_upgradeButton != null)
        {
            _upgradeButton.style.display = canUpgrade ? DisplayStyle.Flex : DisplayStyle.None;
            if (canUpgrade)
            {
                if (isCastle)
                {
                    var castleCost = KingdomManager.Instance.NextCastleUpgradeCost();
                    _upgradeButton.text = $"升级主城 (金{castleCost.Get(ResourceType.Gold)} 石{castleCost.Get(ResourceType.Stone)} 木{castleCost.Get(ResourceType.Wood)} 粮{castleCost.Get(ResourceType.Food)})";
                    _upgradeButton.SetEnabled(RulerController.Instance != null && RulerController.Instance.CanAfford(castleCost));
                }
                else
                {
                    var lvCost = def.levels[_target.level - 1].upgradeCost;
                    if (!moduleOk)
                    {
                        // 模块等级不足（3.5 §2.1：建筑等级 ≤ 模块等级）：置灰并提示，避免"为什么没有升级按钮"
                        _upgradeButton.text = "升级 (需先升级主城)";
                        _upgradeButton.SetEnabled(false);
                    }
                    else
                    {
                        _upgradeButton.text = $"升级 (金{lvCost.Get(ResourceType.Gold)} 石{lvCost.Get(ResourceType.Stone)} 木{lvCost.Get(ResourceType.Wood)} 粮{lvCost.Get(ResourceType.Food)}{(lvCost.Get(ResourceType.Metal) > 0 ? $" 铁{lvCost.Get(ResourceType.Metal)}" : "")})";   // 2_12 步骤8 D131：工事升级含铁
                        _upgradeButton.SetEnabled(RulerController.Instance != null && RulerController.Instance.CanAfford(lvCost));
                    }
                }
            }
        }

        // 拆除按钮（条件渲染）
        // 【HH.294 片4·4-H】⭐ **「可否拆」不再在 UI 现算**（原 `_target.isPlayerBuilt && def.isDestructible && !def.isResourceNode`
        //   三字段组合写在 UI 里）⇒ 挪到**数据栏**：判据归数据（`Building.CanDemolish`），UI 只读结果。
        bool canDemolish = _target.CanDemolish;
        if (_demolishButton != null)
            _demolishButton.style.display = canDemolish ? DisplayStyle.Flex : DisplayStyle.None;

        // 收取按钮（条件渲染：有 StorageComponent，3.3.5 库存常显 + 搬运中状态）
        var storage = _target.GetComponent<StorageComponent>();
        if (_harvestButton != null)
        {
            bool hasStorage = storage != null;
            _harvestButton.style.display = hasStorage ? DisplayStyle.Flex : DisplayStyle.None;
            if (hasStorage)
            {
                bool transporting = FindObjectOfType<ScheduleCenterStub>()?.IsTransporting(storage) ?? false;
                _harvestButton.text = transporting
                    ? $"搬运中 {storage.UsedSpace}/{storage.capacity}"
                    : $"收取 {storage.UsedSpace}/{storage.capacity}";
                _harvestButton.SetEnabled(storage.IsReadyToHarvest());
            }
        }

        // 功能区：按建筑类型动态渲染（3.5 建筑级功能）
        RefreshFunctionArea();
    }

    // ===== 对外 API（由 Building.Interact → InteractionManager 调用 SetTarget → Open）=====

    /// <summary>设置面板目标建筑。由 InteractionManager 在打开面板前调用。</summary>
    public void SetTarget(Building b)
    {
        _target = b;
    }

    /// <summary>当前目标（用于调试/扩展）。</summary>
    public Building CurrentTarget => _target;

    // ===== Unity 生命周期 =====

    private void OnEnable()
    {
        EventBus.Subscribe<BuildingUpgradedEvent>(OnBuildingUpgraded);
        EventBus.Subscribe<UnitDiedEvent>(OnUnitDied);
        if (!_buttonsBound) BindButtons();
        SetVisible(false);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<BuildingUpgradedEvent>(OnBuildingUpgraded);
        EventBus.Unsubscribe<UnitDiedEvent>(OnUnitDied);
        UnbindButtons();
    }

    private void Start()
    {
        if (!_buttonsBound) BindButtons();
    }

    // ===== 按钮绑定 =====

    private void BindButtons()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return;
        var root = doc.rootVisualElement;

        _nameLabel = root.Q<Label>("building-name");
        _levelLabel = root.Q<Label>("building-level");
        _hpFill = root.Q<VisualElement>("hp-bar-fill");
        _hpText = root.Q<Label>("hp-text");
        _descBlock = root.Q<VisualElement>("desc-block");
        _descLabel = root.Q<Label>("building-desc");
        _combatRow = root.Q<VisualElement>("combat-row");
        _attackValue = root.Q<Label>("attack-value");
        _defenseValue = root.Q<Label>("defense-value");
        _rangeValue = root.Q<Label>("range-value");
        _producerRow = root.Q<VisualElement>("producer-row");
        _producerRate = root.Q<Label>("producer-rate");
        _producerCap = root.Q<Label>("producer-cap");
        _footprintValue = root.Q<Label>("footprint-value");
        _factionValue = root.Q<Label>("faction-value");
        _obstacleValue = root.Q<Label>("obstacle-value");
        _functionArea = root.Q<VisualElement>("function-area");
        _functionRows = root.Q<VisualElement>("function-rows");

        _upgradeButton = root.Q<Button>("upgrade-button");
        _demolishButton = root.Q<Button>("demolish-button");
        _closeButton = root.Q<Button>("close-button");

        if (_upgradeButton != null) _upgradeButton.clicked += OnUpgradeClicked;
        if (_demolishButton != null) _demolishButton.clicked += OnDemolishClicked;
        if (_closeButton != null) _closeButton.clicked += OnCloseClicked;

        // 标题栏拖动（可拖动窗口，不破坏关闭按钮点击）
        var panel = root.Q<VisualElement>("building-panel");
        var handle = root.Q<VisualElement>("drag-handle");
        if (panel != null && handle != null) UIDragHelper.Attach(panel, handle);

        // 动态创建收取按钮（UXML 未预留，3.3.4 批次5）
        if (_harvestButton == null)
        {
            _harvestButton = new Button(OnHarvestClicked) { name = "harvest-button", text = "收取" };
            // 加到拆除按钮的父级（按钮容器），否则回退到 root
            var host = _demolishButton != null ? _demolishButton.parent : root;
            host?.Add(_harvestButton);
            _harvestButton.style.display = DisplayStyle.None;
        }

        _buttonsBound = true;
    }

    private void UnbindButtons()
    {
        if (!_buttonsBound) return;
        if (_upgradeButton != null) _upgradeButton.clicked -= OnUpgradeClicked;
        if (_demolishButton != null) _demolishButton.clicked -= OnDemolishClicked;
        if (_closeButton != null) _closeButton.clicked -= OnCloseClicked;
        if (_harvestButton != null) _harvestButton.clicked -= OnHarvestClicked;
        _buttonsBound = false;
    }

    // ===== 事件回调 =====

    private void OnBuildingUpgraded(BuildingUpgradedEvent evt)
    {
        if (evt.Building == _target) Refresh();
    }

    // 3.4：改订阅 UnitDiedEvent（BuildingDestroyedEvent 退役）。
    // 建筑被击杀/拆除都关面板，不过滤 Cause。
    private void OnUnitDied(UnitDiedEvent evt)
    {
        if (evt.Unit as Building == _target) Close();
    }

    private void OnUpgradeClicked()
    {
        if (_target == null || _target.def == null) return;

        // 废墟重建（2_12 步骤7 / D156：修复=同建造，仓库积满→协作施工，走 WarehouseHelper.TrySettle 多仓库凑单 D51）
        if (_target.state == BuildingState.Ruined)
        {
            var rc = _target.GetRepairCost();
            if (!WarehouseHelper.CanAfford(rc))
            {
                Debug.Log("[BuildingPanel] 仓库资源不足，无法重建废墟");
                return;
            }
            // ⭐ `M1-C` 件1（`09` §16.3-2 修复与建造**同构**）：下单**只扣金**（裁决 2 金-A），
            //   非金改「投料 ⇒ 等时间」⇒ 料齐才推进度（⛔ 不再「扣费即开工」）。
            var rcGold = Building.GoldOnlyOf(rc);
            if (!rcGold.IsZero && !WarehouseHelper.TrySettle(rcGold)) return;
            _target.AddInvested(rcGold.TotalCount);
            _target.StartRebuildFromRuins(rc);
            UIManager.Instance?.Pop();
            return;
        }

        // 修复废弃主城（3.3.4 批次7 / 3.5 步骤2：消耗按 KingdomConfig）
        if (_target.state == BuildingState.Abandoned)
        {
            var repairCost = GetRepairCost();
            if (RulerController.Instance == null || !RulerController.Instance.CanAfford(repairCost))
            {
                Debug.Log("[BuildingPanel] 资源不足，无法修复");
                return;
            }
            // ⭐ `M1-C` 件1：下单**只扣金**（裁决 2 金-A），非金改「投料 ⇒ 等时间」（`09` §16.3-2）
            var repairGold = Building.GoldOnlyOf(repairCost);
            RulerController.Instance.Spend(repairGold);
            _target.AddInvested(repairGold.TotalCount);
            _target.StartConstructing(repairCost);
            // 修复主城即时解锁 Lv1（不等建造动画完成），使重建后立即可升级/建造
            if (_target.sourceType == BuildingType.CastleCore && KingdomManager.Instance != null)
                KingdomManager.Instance.SetCastleLevel(1);
            UIManager.Instance?.Pop();
            return;
        }

        // 主城升级（3.5 步骤1：Lv2+ 走 KingdomManager.TryUpgradeCastle，跨级解锁模块）
        if (_target.sourceType == BuildingType.CastleCore)
        {
            if (KingdomManager.Instance == null || !KingdomManager.Instance.TryUpgradeCastle())
            {
                Debug.Log("[BuildingPanel] 主城升级失败（已达最高级 / 资源不足）");
                return;
            }
            UIManager.Instance?.Pop();
            return;
        }

        if (!_target.isPlayerBuilt || _target.def.levels == null || _target.def.levels.Length == 0) return;
        if (_target.level - 1 >= _target.def.levels.Length) return;

        // 模块级门控（3.5 §2.1：建筑等级 ≤ 模块级）
        if (KingdomManager.Instance != null && !KingdomManager.Instance.CanUpgradeBuilding(_target))
        {
            Debug.Log("[BuildingPanel] 升级被禁：建筑等级超过所属模块等级");
            return;
        }

        var lvCost = _target.def.levels[_target.level - 1].upgradeCost;
        if (RulerController.Instance == null || !RulerController.Instance.CanAfford(lvCost))
        {
            Debug.Log("[BuildingPanel] 资源不足，无法升级");
            return;
        }

        // ⭐ `M1-C` 件1（`09` §16.3-1 升级与建造**同构**）：下单**只扣金**（裁决 2 金-A），
        //   非金改「投料 ⇒ 等时间」⇒ 料齐才提级（⛔ 不再「扣费即升」）。
        var lvGold = Building.GoldOnlyOf(lvCost);
        RulerController.Instance.Spend(lvGold);
        _target.AddInvested(lvGold.TotalCount);
        if (_target.TryUpgrade())
        {
            // 升级走 Constructing 进度（3.3.4 批次3）
            UIManager.Instance?.Pop();
        }
    }

    private void OnDemolishClicked()
    {
        if (_target == null) return;
        if (!_target.CanDemolish) return;   // 【HH.294 片4·4-H】判据归数据栏（同源，禁 UI 复算）

        // ⭐ `M1-C` 件4（`09` §16.3-3）：`Demolish` 只**进入拆除态**（有耗时 ＋ 需工人到场推进）；
        //   旧行为「按 HP 比例返还 ＋ 瞬时 `Die`」已退役（件2／件3）。
        _target.Demolish();
        UIManager.Instance?.Pop();  // 出栈关闭面板
    }

    private void OnHarvestClicked()
    {
        if (_target == null) return;
        var storage = _target.GetComponent<StorageComponent>();
        if (storage == null || !storage.IsReadyToHarvest()) return;
        storage.Harvest();  // 内部调 RulerController.ModifyResource 转入国库
        Refresh();
    }

    // ===== 功能区（3.5 建筑级功能，按 def.id + moduleType 动态渲染）=====

    /// <summary>
    /// 按建筑类型渲染功能区。每个建筑只显示其真正的玩法用途：
    /// 房屋→人口容量；仓储→存储量/容量；市场→贸易入口；训练所→可训练项；
    /// 牧场→动物数/容量；学院/工坊→研究占位。无对应功能则整块隐藏。
    /// </summary>
    private void RefreshFunctionArea()
    {
        if (_functionArea == null || _functionRows == null || _target == null || _target.def == null) return;
        _functionRows.Clear();
        var def = _target.def;
        bool any = false;

        // 房屋：人口容量（HappinessSystem.GetHouseCapacity，Lv1=3/Lv2=5/Lv3=8）
        if (def.id == "House")
        {
            AddFunctionRow("人口容量", $"{HappinessSystem.GetHouseCapacity(_target.level)}");
            any = true;
        }

        // 仓库/粮仓/高级仓储等有 StorageComponent 的：资源种类 + 当前存储 / 容量
        // （用户反馈：点击具体仓库建筑要能看到"存的是什么资源"，而非只有数字）
        // D118（2_13 步骤11B）：此行即「仓库存量」字段（StorageComponent 与 IWarehouse.Query 同数据源）
        var storage = _target.GetComponent<StorageComponent>();
        if (storage != null)
        {
            AddFunctionRow("存储", $"{storage.Contents} {storage.UsedSpace}/{storage.capacity}");
            any = true;
        }

        // D118 信息面板三字段（2_13 步骤11B / D256）：仓库存量（上）+ 守卫状态 + 威胁等级
        // 守卫状态：GuardDeploymentSystem.IsGuarded（2_8 消费端，2_13 批B 已接线右键部署）
        var coord = GridSystem.Instance != null ? GridSystem.Instance.WorldToCoord(_target.transform.position) : null;
        if (coord.HasValue)
        {
            bool guarded = GuardDeploymentSystem.IsGuarded(coord.Value);
            AddFunctionRow("守卫状态", guarded ? "已守卫" : "未守卫");
            any = true;
        }
        // 威胁等级：区块热度（2_7/2_8 热度口径，价值驱使 D56；LODSystem.GetHeatAt）
        // 阈值对齐 AttentionTuningConfig：敌压近 0.3 / 冲锋 0.6 → 低/中/高
        float heat = LODSystem.Instance != null ? LODSystem.Instance.GetHeatAt(_target.transform.position) : 0f;
        AddFunctionRow("威胁等级", HeatLabel(heat));
        any = true;

        // 市场：贸易入口按钮（打开 TradePanel）
        if (def.id == "market")
        {
            AddFunctionButton("贸易（卖出 / 买入）", OnTradeClicked);
            any = true;
        }

        // 训练设施：展示可训练项 + 打开训练面板入口（数据驱动：有训练配置即视为训练设施，避免硬编码 id）
        var trainings = TrainingSystem.Instance != null ? TrainingSystem.Instance.GetTrainings(def.id) : null;
        if (trainings != null && trainings.Count > 0)
        {
            AddFunctionRow("可训练", $"{trainings.Count} 项");
            AddFunctionButton("训练", OnTrainingClicked);
            any = true;
        }

        // 战争机器工坊：生产入口（HH.111 玩家生产入口批；P8 首案玩家侧处置 D558④/D564）
        if (def.id == "SiegeWorkshop")
        {
            AddFunctionButton("生产", OnMachineProduceClicked);
            any = true;
        }

        // 牧场：动物数 / 容量（RanchSystem）
        if (def.id == "Ranch")
        {
            int count = RanchSystem.Instance != null ? RanchSystem.Instance.AnimalCount : 0;
            int cap = RanchSystem.Instance != null ? RanchSystem.Instance.Capacity() : 0;
            AddFunctionRow("牧场", $"{count}/{cap} 头");
            any = true;
        }

        // 一次性资源点采集入口（QQQ.2 T19 / DR-11）—— 【HH.294 片 6-2·6-D】**成死码 ⇒ 已退役**
        //   改前：`if (def.isConsumable)` 行内「资源 ×N / 预计耗时 / 采集按钮」→ `_target.StartGather()`。
        //   本批：① 资源点转纯数据（6-A）⇒ 一次性三型（ore_vein/stone_pile/wood_pile）**不再是 Building**，
        //          本分支在实盘无活对象（`isConsumable=1` 余 `treasure_box`＝待退役死资产·无实例化路径）；
        //        ② 玩家采集入口改走**右键资源格**（D115 `PrioritizeHarvestCommand` → `ConfirmResourceGather`，
        //           `05` 交互层的数据格面板留中批次）。
        //   ⇒ 按钮/耗时行/采集量读口一并退役（`StartGather`／`isBeingGathered` 同批删除）。

        _functionArea.style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>威胁热度 → 等级文案（D118；阈值对齐 AttentionTuningConfig：敌压近 0.3 / 冲锋 0.6）。</summary>
    private static string HeatLabel(float heat)
    {
        if (heat < 0.1f) return "无";
        if (heat < 0.3f) return "低";
        if (heat < 0.6f) return "中";
        return "高";
    }

    /// <summary>功能区加一行「标签：值」统计（复用 info-row/info-cell 样式）。</summary>
    private void AddFunctionRow(string label, string value)
    {
        if (_functionRows == null) return;
        var row = new VisualElement();
        row.AddToClassList("info-row");
        var cell = new VisualElement();
        cell.AddToClassList("info-cell");
        var l = new Label { text = label };
        l.AddToClassList("info-label");
        var v = new Label { text = value };
        v.AddToClassList("info-value");
        cell.Add(l);
        cell.Add(v);
        row.Add(cell);
        _functionRows.Add(row);
    }

    /// <summary>功能区加一个主操作按钮（复用 action-button--primary 样式）。</summary>
    private void AddFunctionButton(string text, System.Action onClick)
    {
        if (_functionRows == null) return;
        var btn = new Button(onClick) { text = text };
        btn.AddToClassList("action-button");
        btn.AddToClassList("action-button--primary");
        _functionRows.Add(btn);
    }

    /// <summary>市场「贸易」按钮：推送 TradePanel 入栈（透传本次交互 ctx）。</summary>
    private void OnTradeClicked()
    {
        if (_target == null) return;
        var tradePanel = FindObjectOfType<TradePanel>();
        if (tradePanel == null)
        {
            Debug.LogWarning("[BuildingPanel] 未找到 TradePanel（场景缺少挂载 TradePanel + UIDocument 的 GameObject）");
            return;
        }
        tradePanel.SetTarget(_target);
        UIManager.Instance?.Push(tradePanel, _lastCtx);
    }

    /// <summary>训练设施「训练」按钮：推送 TrainingPanel 入栈（透传本次交互 ctx）。</summary>
    private void OnTrainingClicked()
    {
        if (_target == null) return;
        var trainingPanel = FindObjectOfType<TrainingPanel>();
        if (trainingPanel == null)
        {
            Debug.LogWarning("[BuildingPanel] 未找到 TrainingPanel（场景缺少挂载 TrainingPanel + UIDocument 的 GameObject）");
            return;
        }
        trainingPanel.SetTarget(_target);
        UIManager.Instance?.Push(trainingPanel, _lastCtx);
    }

    /// <summary>战争机器工坊「生产」按钮：推送 MachinePanel 入栈（HH.111；TrainingPanel 同构，动态构建无需场景挂载）。</summary>
    private void OnMachineProduceClicked()
    {
        if (_target == null) return;
        var machinePanel = MachinePanel.Instance;
        if (machinePanel == null) return;
        machinePanel.SetTarget(_target);
        UIManager.Instance?.Push(machinePanel, _lastCtx);
    }

    // 【HH.294 片 6-2·6-D】原 `OnGatherClicked`（采集按钮 → `_target.StartGather()`）随「一次性资源点不再是
    //   Building ⇒ 本入口成死码」**已退役**（见 `RefreshFunctions` 内退役注）；玩家采集入口改走右键资源格。

    private void OnCloseClicked()
    {
        UIManager.Instance?.CloseCurrent();
    }

    // ===== 辅助 =====

    /// <summary>
    /// 主城修复消耗（3.5 步骤2）。优先 KingdomConfig Lv1 修复档（§2.1 木10/石6/粮6/金2），
    /// 兼容旧城堡 def.cost 回退（KingdomConfig 未加载/未配置时）。
    /// </summary>
    private ResourceList GetRepairCost()
    {
        if (KingdomManager.Instance != null && KingdomManager.Instance.Config != null)
        {
            var c = KingdomManager.Instance.Config.GetCastleUpgradeCost(1);
            if (!c.IsZero) return c;
        }
        return _target != null && _target.def != null ? _target.def.cost : ResourceList.Empty;
    }

    private void SetVisible(bool visible)
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return;
        doc.rootVisualElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private static string FactionDisplayName(Faction f)
    {
        switch (f)
        {
            case Faction.PlayerCamp: return "我方";
            case Faction.Monster: return "亡灵";
            case Faction.None: return "中立";
            default: return f.ToString();
        }
    }
}
