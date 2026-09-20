// 【HH.294 片 6-2·B-4⑤】原 `using System.Collections.Generic;`（供 `_pool` 的 Dictionary/Stack）
//   随对象池退役一并移除 —— 删后本文件零泛型容器使用。
using UnityEngine;

/// <summary>
/// 建筑工厂（3.3 批次0 + 3.5 实施计划 P0 步骤3）。
/// 职责拆为两块：
///   1) 地图预置建筑实例化（BuildingPlaceholder → Building，WorldManager.GenerateWorld 调）。
///   2) 存档重建（ISaveableSpawner，前缀 "Building_"，读档时由 SaveManager 调 SpawnFromSave）。
///
/// 3.5 步骤3：static class → Singleton<BuildingFactory>，并实现 ISaveableSpawner。
/// 调用方统一走 BuildingFactory.Instance.X（WorldManager / BuildController 已同步）。
/// </summary>
public class BuildingFactory : Singleton<BuildingFactory>, ISaveableSpawner
{
    public string SaveIdPrefix => "Building_";

    private static BuildingMappingTable _mappingTable;
    private static BuildingDef[] _allDefsCache;

    protected override void Awake()
    {
        base.Awake();
        if (_instance != this) return;
        _mappingTable = Resources.Load<BuildingMappingTable>("Buildings/BuildingMappingTable");
    }

    static BuildingMappingTable GetMappingTable()
    {
        if (_mappingTable == null)
            _mappingTable = Resources.Load<BuildingMappingTable>("Buildings/BuildingMappingTable");
        return _mappingTable;
    }

    /// <summary>按 id 查找 BuildingDef（Resources/Buildings 下全部资产，缓存）。存档重建用。</summary>
    public static BuildingDef FindDefById(string defId)
    {
        if (string.IsNullOrEmpty(defId)) return null;
        if (_allDefsCache == null)
            _allDefsCache = Resources.LoadAll<BuildingDef>("Buildings");
        if (_allDefsCache == null) return null;
        for (int i = 0; i < _allDefsCache.Length; i++)
            if (_allDefsCache[i] != null && _allDefsCache[i].id == defId)
                return _allDefsCache[i];
        return null;
    }

    // ===== 地图预置建筑实例化（【HH.294 片 6-2 收尾】目标态：只剩玩家出生点主城）=====

    /// <summary>
    /// 把 MapData 的自然建筑占位（2_1 naturalBuildings）转为 Building 实例（2_2 接管）。
    /// 【HH.294 片 6-2 收尾·旧路径清场】目标态：`naturalBuildings` 恒空（资源点＝格表 ⇒ 不派生实体）——
    /// 树/矿/一次性三型不再实例化；只保留「玩家出生点放主城（CastleCore）」保建造解锁链路（主城锚点归 2_12）。
    /// </summary>
    public int InstantiateFromMap(MapData map)
    {
        if (map == null) return 0;
        var table = GetMappingTable();
        if (table == null)
        {
            Debug.LogWarning("[BuildingFactory] BuildingMappingTable 未加载，跳过地图预置建筑实例化");
            return 0;
        }

        int count = 0;

        // 玩家出生点放主城（2_2 过渡桥：保建造解锁链路；主城=王座/旗帜锚点归 2_12 重做）
        // 沿用 1D 流程：Abandoned 废墟态放置，玩家经 BuildingPanel 修复 -> CastleLevel=1 解锁建造
        var castleDef = table.Get(BuildingType.CastleCore);
        if (castleDef != null && map.kingdomSpawns != null && map.kingdomSpawns.Count > 0)
        {
            var spawn = map.kingdomSpawns[0];
            var coord = new GridCoord(spawn.x, spawn.y);
            var fp = new Vector2Int(
                castleDef.footprint.x > 0 ? castleDef.footprint.x : 1,
                castleDef.footprint.y > 0 ? castleDef.footprint.y : 1);
            if (CreateBuildingInstance(castleDef, BuildingType.CastleCore, coord, fp,
                    GridSystem.FootprintCenterWorld(coord, fp, Vector3.zero),
                    isPlayerBuilt: false, grade: ResourceGrade.Normal,
                    isConsumable: false, initialState: BuildingState.Abandoned))
                count++;
        }

        Debug.Log($"[BuildingFactory] 2D 地图预置建筑实例化完成：{count} 个（主城·自然建筑自 HH.294 片 6-2 收尾起不再派生）");
        return count;
    }

    /// <summary>按占用/注册/挂件/发事件创建 Building 实例。供地图与玩家放置共用逻辑（BuildController 保留自身放置路径）。</summary>
    public bool CreateBuildingInstance(BuildingDef def, BuildingType sourceType, GridCoord coord, Vector2Int footprint,
                                       Vector3 worldPos, bool isPlayerBuilt, ResourceGrade grade, bool isConsumable,
                                       BuildingState initialState, int kingdomId = 0)
    {
        if (def == null) return false;
        var fp = new Vector2Int(
            footprint.x > 0 ? footprint.x : 1,
            footprint.y > 0 ? footprint.y : 1);

        GameObject go;
        if (def.prefab != null)
        {
            go = Object.Instantiate(def.prefab, worldPos, Quaternion.identity);
        }
        else
        {
            go = new GameObject($"Building_{def.id}_{coord.x}_{coord.y}");
            go.transform.position = worldPos;
            BuildingVisual.ApplyPlaceholder(go, sourceType, def.role, def.id, 0, -1, coord);
        }

        var b = go.GetComponent<Building>();
        if (b == null)
        {
            b = go.AddComponent<Building>();
            if (b == null)
            {
                Debug.LogError($"[BuildingFactory] 添加 Building 组件失败！id={def.id}");
                Object.DestroyImmediate(go);
                return false;
            }
        }

        // 内联初始化
        try
        {
            b.def = def;
            b.coord = coord;
            b.isPlayerBuilt = isPlayerBuilt;
            b.sourceType = sourceType;
            b.grade = grade;
            b.footprint = fp;
            b.level = 1;
            b.isObstacle = def.isObstacle;
            b.kingdomId = kingdomId;   // 2_16 步骤2：王国归属（默认 0=玩家）
            // HH.86/DZ-040 件2a：faction 按 kingdomId 派生（照抄 UnitFactory.SpawnUnit 先例，仅 >0 覆写；
            // 须在 kingdomId 赋值后，旧 L204 `b.faction = def.faction` 在归属写入前=AI 国仍挂 def.faction）
            b.faction = kingdomId > 0 ? Faction.AiKingdom : def.faction;

            // HP：统一入口 = def.maxHp（3.5.1 E-S10）× gradeScale
            // 2_20 M5/D420：×buildingHpMul（双路同乘之二——内联路径不经 ApplyDef；kingdomId 上方 L206 已赋值先于 HP 计算）
            var raceDef = KingdomRace.GetKingdomRaceDef(kingdomId);
            float hpMul = raceDef != null ? raceDef.buildingHpMul : 1f;
            int baseHp = def.maxHp > 0 ? def.maxHp : 100;
            try
            {
                float scale = def.GetGradeScale(grade);
                baseHp = Mathf.Max(1, Mathf.RoundToInt(baseHp * Mathf.Max(0.1f, scale)));
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[BuildingFactory] HP计算降级为 def.maxHp 无缩放（def={def.id}, grade={grade}）: {ex.Message}");
            }
            baseHp = Mathf.Max(1, Mathf.RoundToInt(baseHp * hpMul));
            b.maxHp = baseHp;
            b.hp = baseHp;
            b.state = initialState;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[BuildingFactory] Building字段初始化失败：id={def.id}, err={ex}");
            Object.DestroyImmediate(go);
            return false;
        }

        // 【HH.294 片 6-1·6-C】原此处无条件挂 BoxCollider2D（地图预置/重生/双写三路共用）⇒
        //   拾取已改道 MapGate.PickAt（`03` §8.6·不走物理）后 Collider 无消费者（全库 Physics2D 拾取 0·OnTrigger/OnCollision 0），
        //   按批口径**删除挂载**；将来若确有物理需求再按需挂。

        try { if (GridSystem.Instance != null) GridSystem.Instance.MarkOccupiedFootprint(coord, fp.x, fp.y, b); }
        catch (System.Exception ex) { Debug.LogWarning("[BuildingFactory] MarkOccupiedFootprint 失败: " + ex.Message); }

        // 桥：置 Bridge 位（2_2 §3.5）
        if (def.isBridge && GridSystem.Instance != null)
        {
            try { GridSystem.Instance.SetBridge(coord, fp.x, fp.y, true); }
            catch (System.Exception ex) { Debug.LogWarning("[BuildingFactory] SetBridge 失败: " + ex.Message); }
        }

        try { if (BuildingRegistry.Instance != null) BuildingRegistry.Instance.Register(b); }
        catch (System.Exception ex) { Debug.LogWarning("[BuildingFactory] Registry.Register 失败: " + ex.Message); }

        try { AttachComponents(b, def); }
        catch (System.Exception ex) { Debug.LogWarning("[BuildingFactory] AttachComponents 失败: " + ex.Message); }

        // 城门：挂 GateController（2_2 §3.4）
        if (def.isGate && go.GetComponent<GateController>() == null)
            go.AddComponent<GateController>();

        // QQQ.2 T17：直接以 Active 态创建的建筑（地图预置/读档）注册到任务调度器
        if (initialState == BuildingState.Active && TaskScheduler.HasInstance)
        {
            try { TaskScheduler.Instance.Register(b); }
            catch (System.Exception ex) { Debug.LogWarning("[BuildingFactory] Register TaskScheduler 失败: " + ex.Message); }
        }

        // 仅玩家建造的 Publish（HH.4 裁决：发布侧剔除，地图自然预置建筑不 Publish——无订阅者时避免全图丢弃刷屏）。
        // DZ-077：再加 HasSubscribers 守卫（无订阅者不广播）。
        if (isPlayerBuilt && EventBus.HasSubscribers<BuildingPlacedEvent>())
        {
            try { EventBus.Publish(new BuildingPlacedEvent(b)); }
            catch (System.Exception ex) { Debug.LogWarning("[BuildingFactory] Publish BuildingPlacedEvent 失败: " + ex.Message); }
        }

        return true;
    }

    /// <summary>按 BuildingDef 配置挂行为组件（Producer/Storage/Combat/Pickup/Rift/CastleCore）。供 BuildingFactory 和 BuildController 共用。</summary>
    public void AttachComponents(Building b, BuildingDef def)
    {
        if (b == null || def == null) return;
        // HH.86/DZ-041 件3a①：空壳仓储补挂——rate==0 但有容量且 role==Economy（Warehouse/Granary 类）照挂
        // StorageComponent 入 WarehouseRegistry（旧=不挂→采集/搬运无落点→连轴建 79 座空壳）。
        // DZ-078：排除 outputResource==Gold——market（rate=0+cap=100+role=Economy）曾误挂 Gold 死仓
        //（金=货币直通不占仓，该仓无任何消费面）；金直通由 RulerController 承担。
        bool econStorageOnly = def.producer.rate <= 0f && def.producer.capacity > 0
            && def.role == BuildingRole.Economy
            && def.outputResource != ResourceType.Gold;
        if (econStorageOnly)
        {
            b.gameObject.AddComponent<StorageComponent>()?.Init(b);
        }
        if (def.producer.rate > 0f && def.producer.kind == ProduceKind.Resource && !def.isResourceNode)
        {
            // 投掷机厂（2_12 步骤9 D207~D212 / HH.19 A×4）：挂专属组件产出弹药（厂级 3 子仓），替代通用 ProducerComponent。
            // 弹性逻辑：仍需本地无 StorageComponent（厂仓 3 子弹药不附建筑本体），故跳过通用 StorageComponent 挂载。
            if (def.isSiegeWorkshop)
            {
                b.gameObject.AddComponent<SiegeWorkshopBuilding>()?.Init(b);
            }
            else
            {
                b.gameObject.AddComponent<StorageComponent>()?.Init(b);
                // 铁匠铺（2_12 步骤8 D200）：挂 BlacksmithBuilding 替代通用 ProducerComponent（矿石→Metal 就地加工 D609）
                if (def.isBlacksmith)
                    b.gameObject.AddComponent<BlacksmithBuilding>()?.Init(b);
                else
                    b.gameObject.AddComponent<ProducerComponent>()?.Init(b);
            }
        }
        // 矿洞副产（DZ-072a，D562 / HH.107 件1）：mine 双身份=A2 变体——isResourceNode 采集点身份不动
        //（上面产能分支被 isResourceNode 排除，mine 本体无 StorageComponent/ProducerComponent），
        // 另挂专属副产组件恒产水晶/火油入自管子仓（仿 isSiegeWorkshop 先例）。
        if (def.isMineByproduct)
        {
            b.gameObject.AddComponent<MineByproductComponent>()?.Init(b);
        }
        if (def.combat.attack > 0)
            b.gameObject.AddComponent<CombatComponent>()?.Init(b);
        if (def.isConsumable)
            b.gameObject.AddComponent<PickupComponent>()?.Init(b);
        if (b.sourceType == BuildingType.Rift)
            b.gameObject.AddComponent<RiftComponent>()?.Init(b);
        if (b.sourceType == BuildingType.CastleCore)
            b.gameObject.AddComponent<CastleCoreComponent>()?.Init(b);
    }

    // ===== ISaveableSpawner（3.5 步骤3：读档重建）=====

    /// <summary>读档重建单栋建筑：按 defId 重建 + 恢复 level/hp/storedAmount + 网格占用。</summary>
    public void SpawnFromSave(ModuleSaveEntry entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.json)) return;
        BuildingSaveData data;
        try { data = JsonUtility.FromJson<BuildingSaveData>(entry.json); }
        catch (System.Exception ex) { Debug.LogError($"[BuildingFactory] BuildingSaveData 反序列化失败: {ex}"); return; }

        var def = FindDefById(data.defId);
        if (def == null)
        {
            Debug.LogWarning($"[BuildingFactory] 读档重建失败：未找到 BuildingDef id={data.defId}，跳过。");
            return;
        }

        // 2D 坐标/占地恢复（2_2）：旧档缺字段 -> 兜底 def.footprint
        var coord = new GridCoord(data.coordX, data.coordY);
        int fw = data.footprintW > 0 ? data.footprintW : (def.footprint.x > 0 ? def.footprint.x : 1);
        int fh = data.footprintH > 0 ? data.footprintH : (def.footprint.y > 0 ? def.footprint.y : 1);
        var fp = new Vector2Int(Mathf.Max(1, fw), Mathf.Max(1, fh));
        // 【HH.294 片3-A】中心点换算统一走 `GridSystem` 唯一内核（原先此处「就地展开」同一公式）；
        //   无网格兜底沿用改前常量 2.26/1.13（旧档重放路径，不属内核适用域）。
        var gs = GridSystem.Instance;
        Vector3 worldPos;
        if (gs != null && gs.Config != null)
        {
            worldPos = GridSystem.FootprintCenterWorld(coord, fp, gs.Config.cellSize);
        }
        else
        {
            worldPos = new Vector3(coord.x * 2.26f + (fp.x - 1) * 0.5f * 2.26f,
                                   coord.y * 1.13f + (fp.y - 1) * 0.5f * 1.13f, 0);
        }

        BuildingState state = (BuildingState)data.state;
        if (def.sourceType == BuildingType.CastleCore && state == BuildingState.Abandoned)
            state = BuildingState.Active;   // 主城修复后读档不应回到废墟（castoeLevel≥1）

        // 响亮断言（读档建筑双份修复，替代"网格已有 occupant 则保留"方案）：
        // 若目标格已有 Building 占用，说明 A(InstantiateFromMap) 与 B(SpawnFromSave) 双路径在此双份——
        // 且该格上通常是 A 的新随机 GUID + 默认 kingdomId，保留它会静默数据腐坏（归属错 + 传送门排除集污染）。
        // 此处不跳过、不吞，仅响亮报错把"存→读→再存→再读"的复合腐坏链暴露出来。
        // 范围只查 Building，Portal/Chest 同为 IGridOccupant 但不在此列（防误报）。
        if (GridSystem.Instance != null)
        {
            var occupied = GridSystem.Instance.GetOccupant(coord) as Building;
            if (occupied != null)
            {
                Debug.LogError($"[BuildingFactory] SpawnFromSave 冲突：coord=({coord.x},{coord.y}) 已有 Building " +
                               $"saveId={occupied.SaveId}（疑似路径 A 新随机 GUID+默认 kingdomId），" +
                               $"存档侧 saveId={entry.saveId}，defId={data.defId}。双路径双份/复合腐坏风险——请核查读档建筑重建路径。");
            }
        }

        bool ok = CreateBuildingInstance(def, (BuildingType)data.sourceType, coord, fp, worldPos,
                                         isPlayerBuilt: true, (ResourceGrade)data.grade, false, state,
                                         kingdomId: ReadArchiveKingdomId((BuildingType)data.sourceType, data.kingdomId));
        if (!ok) return;

        var b = GridSystem.Instance != null ? GridSystem.Instance.GetOccupant(coord) as Building : null;
        if (b == null)
        {
            Debug.LogWarning($"[BuildingFactory] 读档重建后未取到 Building（coord=({coord.x},{coord.y})），跳过状态恢复。");
            return;
        }

        // 覆盖 SaveId（否则 SaveManager 找不到该 saveId 分发 LoadState）
        b.OverrideSaveId(entry.saveId);

        // QQQ.3 B8-5 / LC-B2：grade 恢复后按新等级重算属性（修复读档后产能永久降贫瘠档 rate×0.7）
        b.grade = (ResourceGrade)data.grade;
        b.ApplyDef();

        // 恢复核心状态（level/hp/maxHp/仓内容）
        b.level = Mathf.Max(1, data.level);
        b.maxHp = Mathf.Max(1, data.maxHp);
        b.hp = Mathf.Clamp(data.hp, 0, b.maxHp);
        var storage = b.GetComponent<StorageComponent>();
        if (storage != null)
        {
            // ⭐ M1-B 件4：先按恢复后的等级重算容量，再灌存量（否则高等级仓按 Lv1 容量 clamp：实测 Lv3 粮仓 180→60 净损 120）
            storage.RefreshCapacity();
            storage.RestoreContents(data.storageContents);
        }
        // ⭐ M1-A：国库容器内容（仅主城有；非主城 ≡ 空列表）
        var vault = b.GetComponent<TreasureVault>();
        if (vault != null) vault.RestoreContents(data.treasuryContents);

        // 2_12 步骤7 / D155：累计投入恢复（D155 修复成本基数 / D162 拆除返还基数）。旧档缺字段 → 兜底按 def.cost。
        b.totalInvested = data.totalInvested > 0
            ? data.totalInvested
            : (b.def != null ? Building.SumCostOf(b.def.cost, includeMetal: false) : 0);
    }

    /// <summary>读档王国归属：自然建筑（OreVein/WoodPile/StonePile 一次性资源点）一律强制 -1（哨兵配套，
    /// 旧档缺 kingdomId 默认 0，不强制则自然建筑全变"玩家王国"污染排除集）；其余取存栏 kingdomId。</summary>
    private static int ReadArchiveKingdomId(BuildingType sourceType, int archivedKingdomId)
    {
        if (sourceType == BuildingType.OreVein || sourceType == BuildingType.WoodPile || sourceType == BuildingType.StonePile)
            return -1;
        return archivedKingdomId;
    }

    /// <summary>清空所有地图建筑（跨岛切换时由 WorldManager 调）。</summary>
    public void ClearAllBuildings()
    {
        if (BuildingRegistry.Instance == null) return;
        var all = BuildingRegistry.Instance.All;
        for (int i = all.Count - 1; i >= 0; i--)
        {
            if (all[i] != null && all[i].gameObject != null)
            {
                if (Application.isPlaying) Object.Destroy(all[i].gameObject);
                else Object.DestroyImmediate(all[i].gameObject);
            }
        }
        BuildingRegistry.Instance.Clear();
    }

    // ===== 对象池回收 —— 【HH.294 片 6-2·B-4⑤】随实体退役 **已删** =====
    //   改前：`Building.OnGatherCompleted:926` 调 `ReturnBuildingToPool`（一次性资源点采集后按 def.id 入池复用）。
    //   本批：三型不再派生实体（6-A）⇒ 采集完成无实体可回收 ⇒ 本方法与 `_pool` 字段一并退役。
    //   grep 证据（改后）：`ReturnBuildingToPool` 全库 0 命中（改前唯一调用点＝`Building.cs:926`，随采集面退役同删）；
    //   `_pool` 在本文件 0 命中。T6 若将来重新引入「有实体的一次性资源」，需一并恢复本池或另立回收口。
}