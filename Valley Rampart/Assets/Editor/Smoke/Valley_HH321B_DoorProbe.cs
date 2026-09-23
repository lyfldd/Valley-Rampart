// ============================================================================
//  HH.321 批 2 本体（`M3-C` 回收门 ＋ `DZ-4` 假 null 守卫 ＋ `cap ≤ 0` 守卫）探针
//   菜单 A「件1门+件3cap_生产链」：① 击毁一座建筑 ⇒ 订阅方侧观测 回收→广播→Destroy 的顺序
//                                ② `EnterRuined` 链回归（非工事被破）
//                                ③ 门幂等（契约口径）
//                                ④ `cap=0` 守卫（生产链：不再零判定 ＋ 有界）
//   菜单 B「件2假null守卫」：假 null attacker ⇒ **修前/修后对照**（异常计数 ＋ 注册残留 ＋ 静默早退）
//  ⚠️ 构造法：探针侧 pin/关脑；`cap` 与 `state` 经运行期字段改（⛔ 不写资产）；1× 速档（真实 cadence）
//  ⛔ 本容器只读观测 ＋ 触发生产入口；产物落 仓库根 `Logs/hh321c_*.txt`（⛔ 不落 Assets）
// ============================================================================
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class Valley_HH321B_DoorProbe
{
    const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly StringBuilder SB = new StringBuilder();
    static readonly List<IDamageable> _scan = new List<IDamageable>();
    static string _outPath;

    // 日志钩子（cap LogError / 未捕获异常计数）
    static int _capErrCount, _excCount;
    static string _capErrSample = "", _excSample = "";

    static void OnLog(string msg, string stack, LogType type)
    {
        if (type == LogType.Error && msg.Contains("maxAttacksPerFrame")) { _capErrCount++; if (_capErrSample.Length == 0) _capErrSample = msg; }
        if (type == LogType.Exception) { _excCount++; if (_excSample.Length == 0) _excSample = msg + " | " + (stack ?? "").Split('\n')[0]; }
    }

    static void L(string s) { Debug.Log("[HH321C] " + s); SB.AppendLine(s); }

    static void Flush(string tag)
    {
        SB.AppendLine("# 封存 " + System.DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
        var dir = System.IO.Path.Combine(Application.dataPath, "..", "..", "Logs");
        System.IO.Directory.CreateDirectory(dir);
        _outPath = System.IO.Path.Combine(dir, "hh321c_" + tag + "_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".txt");
        System.IO.File.WriteAllText(_outPath, SB.ToString());
        Debug.Log("[HH321C] 落盘 " + _outPath);
    }

    static Vector2 CellCenterWorld(GridCoord c)
        => GridSystem.CellToWorldF(c.x + 0.5f, c.y + 0.5f, new Vector2(GridMath.CellW, GridMath.CellH));

    static void MoveUnit(UnitController u, Vector2 pos)
    {
        u.transform.position = pos;
        var rb = typeof(UnitController).GetField("_rb", BF)?.GetValue(u) as Rigidbody2D;
        if (rb != null) { rb.position = pos; rb.velocity = Vector2.zero; }
        Physics2D.SyncTransforms();
    }

    static UnitController SpawnUnitOf(Faction f, Occupation occ, Vector2 pos, int kingdomId)
    {
        var go = UnitFactory.Instance.SpawnUnit(f, occ, pos, kingdomId);
        var uc = go != null ? go.GetComponent<UnitController>() : null;
        if (uc != null) MoveUnit(uc, pos);
        return uc;
    }

    static void FreezeBrain(UnitController uc)
    {
        if (uc == null) return;
        var b = uc.GetComponent<NPCBrain>();
        if (b != null) b.enabled = false;
    }

    private class RunHost : MonoBehaviour { public void Host(IEnumerator r) => StartCoroutine(r); }

    static IEnumerator WaitWorld()
    {
        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null
               || GridSystem.Instance == null || GridSystem.Instance.Config == null
               || BuildingRegistry.Instance == null || UnitFactory.Instance == null
               || DamageSystem.Instance == null || ProjectileManager.Instance == null)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 180f) yield break;
        }
        yield return new WaitForSeconds(0.5f);
    }

    /// <summary>隔离场址：连续 `cols` 格（两行）可走空置 ＋ 12 格内无敌 ⇒ 返回左下角格。</summary>
    static bool FindSite(GridCoord anchor, int cols, out GridCoord site)
    {
        var grid = GridSystem.Instance;
        for (int ring = 24; ring <= 108; ring += 12)
            for (int dx = -24; dx <= 24; dx += 12)
                for (int dy = -24; dy <= 24; dy += 12)
                {
                    var c = new GridCoord(anchor.x + dx, anchor.y - ring + dy);
                    bool ok = true;
                    for (int k = 0; k < cols && ok; k++)
                        for (int r = 0; r < 2 && ok; r++)
                        {
                            var cc = new GridCoord(c.x + k, c.y + r);
                            if (!grid.IsWalkable(cc) || grid.GetOccupant(cc) != null) ok = false;
                        }
                    if (!ok) continue;
                    Vector2 w = CellCenterWorld(c);
                    _scan.Clear(); PerceptionSystem.QueryNearby(w, GridMath.VisualToWorld(12f), Faction.PlayerCamp, true, _scan);
                    _scan.Clear(); PerceptionSystem.QueryNearby(w, GridMath.VisualToWorld(12f), Faction.AiKingdom, true, _scan);
                    if (_scan.Count == 0) { site = c; return true; }
                }
        site = default;
        return false;
    }

    // ========================================================================
    //  菜单 A · 件1（回收门）＋ 件3（cap 守卫）
    // ========================================================================
    [MenuItem("Valley/验证/HH321B 件1门+件3cap_生产链")]
    public static void RunDoor()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[HH321C] 须先进入 Play 后调用本菜单。"); return; }
        new GameObject("HH321C_Host").AddComponent<RunHost>().Host(RunDoorCo());
    }

    public static IEnumerator RunDoorCoroutine() => RunDoorCo();

    static IEnumerator RunDoorCo()
    {
        var cfg = new NewGameConfig
        {
            worldSeed = 20321, mapSeed = 20321, raceId = 0, difficulty = 2,
            worldSize = WorldSize.Small, selectedSlotId = "smoke_hh321c", kingdomName = "门实测"
        };
        Debug.Log("[HH321C] EnterTestRun（1× · 真实 cadence）");
        yield return TestHarnessApi.EnterTestRun(cfg, 1f);
        yield return WaitWorld();
        Application.logMessageReceived += OnLog;

        var grid = GridSystem.Instance;
        var dmg = DamageSystem.Instance;
        var reg = BuildingRegistry.Instance;
        var dmgCfg = typeof(DamageSystem).GetField("_config", BF).GetValue(dmg) as DamageConfig;
        var pend = typeof(DamageSystem).GetField("_pendingAttacks", BF).GetValue(dmg) as System.Collections.IList;

        L("=== HH.321 批 2 · 件1（回收门）＋ 件3（cap 守卫）生产链实测 ===");
        L($"[环境] seed={WorldManager.Instance.ActiveMap.seed} timeScale={Time.timeScale} 建筑在册={reg.Count} 单位在册={UnitRegistry.Instance.Count}"
          + $" cap={dmgCfg.maxAttacksPerFrame}（运行期）");
        L($"[门体实读] BuildingFactory.RemoveBuilding 存在={typeof(BuildingFactory).GetMethod("RemoveBuilding") != null}"
          + $" · Building.ReleaseLayerOwnedState 非 public（internal）={typeof(Building).GetMethod("ReleaseLayerOwnedState", BindingFlags.Instance | BindingFlags.NonPublic) != null}"
          + $" · public 版={typeof(Building).GetMethod("ReleaseLayerOwnedState", BindingFlags.Instance | BindingFlags.Public) != null}");

        var anchor = grid.WorldToCoord(WorldManager.Instance.GetKingdomAnchorWorld());
        GridCoord band;
        if (!anchor.HasValue || !FindSite(anchor.Value, 20, out band))
        { Debug.LogError("[HH321C] 找不到场址"); TestHarnessApi.ExitTestRun(); yield break; }
        var site = new GridCoord(band.x + 6, band.y);          // 建筑带起点（左侧 6 格留给射手/单位试区）

        // 射手（只作 ApplyDamage 的 source · 远离塔射程）
        var shooter = SpawnUnitOf(Faction.PlayerCamp, Occupation.Archer, CellCenterWorld(new GridCoord(band.x, band.y)), 0);
        var towerDef = Resources.Load<BuildingDef>("Buildings/arrow_tower");
        var farmDef = Resources.Load<BuildingDef>("Buildings/farm");
        if (towerDef == null || farmDef == null || shooter == null) { Debug.LogError("[HH321C] 资产/射手缺失"); TestHarnessApi.ExitTestRun(); yield break; }
        FreezeBrain(shooter);

        var fpT = new Vector2Int(Mathf.Max(1, towerDef.footprint.x), Mathf.Max(1, towerDef.footprint.y));
        var fpF = new Vector2Int(Mathf.Max(1, farmDef.footprint.x), Mathf.Max(1, farmDef.footprint.y));
        var cT = new GridCoord(site.x, site.y);
        var cF = new GridCoord(site.x + 3, site.y);
        var cT2 = new GridCoord(site.x + 6, site.y);
        var cT3 = new GridCoord(site.x + 10, site.y);          // 判据3 用：工地态塔（含工地仓内容）

        bool b1 = BuildingFactory.Instance.CreateBuildingInstance(towerDef, towerDef.sourceType, cT, fpT,
            GridSystem.FootprintCenterWorld(cT, fpT, Vector3.zero), false, ResourceGrade.Normal, false, BuildingState.Active, 1);
        bool b2 = BuildingFactory.Instance.CreateBuildingInstance(farmDef, farmDef.sourceType, cF, fpF,
            GridSystem.FootprintCenterWorld(cF, fpF, Vector3.zero), false, ResourceGrade.Normal, false, BuildingState.Active, 1);
        bool b3 = BuildingFactory.Instance.CreateBuildingInstance(towerDef, towerDef.sourceType, cT2, fpT,
            GridSystem.FootprintCenterWorld(cT2, fpT, Vector3.zero), false, ResourceGrade.Normal, false, BuildingState.Active, 1);
        // 判据3 用：**工地态**塔（`Constructing` ⇒ `Die` 路径会走三路掉箱中的工地仓路）
        bool b4 = BuildingFactory.Instance.CreateBuildingInstance(towerDef, towerDef.sourceType, cT3, fpT,
            GridSystem.FootprintCenterWorld(cT3, fpT, Vector3.zero), false, ResourceGrade.Normal, false, BuildingState.Constructing, 1);
        yield return null;
        var tower = grid.GetOccupant(cT) as Building;
        var farm = grid.GetOccupant(cF) as Building;
        var tower2 = grid.GetOccupant(cT2) as Building;
        var tower3 = grid.GetOccupant(cT3) as Building;
        if (!b1 || !b2 || !b3 || !b4 || tower == null || farm == null || tower2 == null || tower3 == null)
        { Debug.LogError("[HH321C] 建筑落成失败"); TestHarnessApi.ExitTestRun(); yield break; }
        L($"[布置] 塔@({cT.x},{cT.y}) 工事={tower.IsFortification} HP={tower.CurrentHp} ｜ farm@({cF.x},{cF.y}) 工事={farm.IsFortification} HP={farm.CurrentHp}"
          + $" ｜ 幂等塔@({cT2.x},{cT2.y}) 工事={tower2.IsFortification}");

        // ══ 判据1 · 击毁（工事 ⇒ Die(Killed)）⇒ 订阅方侧读数：回收→广播→Destroy ══
        int diedCount = 0, evtTower = 0;
        bool regAtEvent = false, occAtEvent = false, aliveAtEvent = false, posMatches = false;
        DeathCause causeAtEvent = DeathCause.Demolished;
        int occBefore = grid.GetOccupant(cT) != null ? 1 : 0;
        int regBefore = reg.GetAt(cT) != null ? 1 : 0;
        System.Action<UnitDiedEvent> onDied = evt =>
        {
            diedCount++;
            if (ReferenceEquals(evt.Unit, tower))
            {
                evtTower++;
                regAtEvent = reg.GetAt(cT) == null;                 // ⭐ 数据回收（注册表）已在广播前完成？
                occAtEvent = grid.GetOccupant(cT) == null;          // ⭐ 占格已在广播前释放？
                aliveAtEvent = tower != null;                       // 实体在广播时仍可用（Destroy 未生效）？
                posMatches = Vector2.Distance(evt.Position, tower.transform.position) < 0.01f;   // 广播携位可用
                causeAtEvent = evt.Cause;
            }
        };
        EventBus.Subscribe(onDied);
        int hp0 = tower.CurrentHp;
        int dealt = dmg.ApplyDamage(shooter, tower, 999999, 0f, true);   // 生产链：TakeDamage → hp≤0 → IsFortification ⇒ Die(Killed)
        bool aliveAtCallReturn = tower != null;
        yield return null;                                               // 让 Destroy 生效
        bool aliveAfterFrame = tower != null;
        EventBus.Unsubscribe(onDied);
        L($"[J1·击毁塔] 击前：占格={occBefore} 注册表={regBefore} HP={hp0}｜ApplyDamage={dealt} ⇒ HP={hp0}（归零）");
        L($"[J1·订阅方侧] 广播数={diedCount} 本塔广播={evtTower} Cause={causeAtEvent}"
          + $" ⇒ ⭐ 回收已发生(注册表空={regAtEvent} · 占格空={occAtEvent}) ｜ 实体在广播时可用={aliveAtEvent} 位对上={posMatches}"
          + $" ｜ 调用返回时仍活={aliveAtCallReturn} 帧后已销毁={!aliveAfterFrame}");
        L($"[J1·结论读数列] (a) 数据回收先于广播 ⇒ {regAtEvent && occAtEvent}；(b) 广播时实体未失效（`transform.position` 可读）⇒ {aliveAtEvent && posMatches}；(c) Destroy 于其后生效 ⇒ {!aliveAfterFrame}");

        // ══ 判据3 · 门幂等（契约口径：`b == null` / `state == Dead` ⇒ 直接返回）══
        int regCntBeforeDoor = reg.Count;
        BuildingFactory.Instance.RemoveBuilding(tower2, DeathCause.Demolished);      // 第 1 次：回收（门体生效的直接证据）
        bool regGone1 = reg.GetAt(cT2) == null, occGone1 = grid.GetOccupant(cT2) == null;
        bool aliveAfterDoor = tower2 != null;                                        // 门 ⛔ 不销毁（属高级层）
        typeof(Building).GetField("state", BF)?.SetValue(tower2, BuildingState.Dead); // 探针构造：置 Dead ⇒ 触发幂等早退分支
        int regCntAfterSet = reg.Count;
        BuildingFactory.Instance.RemoveBuilding(tower2, DeathCause.Demolished);      // 第 2 次：应早退
        bool regStillGone = reg.GetAt(cT2) == null, occStillGone = grid.GetOccupant(cT2) == null;
        int regCntAfter2 = reg.Count;
        BuildingFactory.Instance.RemoveBuilding(tower, DeathCause.Killed);            // 第 3 次：对**已销毁**引用（b == null 分支）
        L($"[J3·幂等] 门第1次 ⇒ 注册表空={regGone1} 占格空={occGone1} 门后实体仍在={aliveAfterDoor}（⛔ 门不销毁）");
        L($"[J3·幂等] 置 Dead 后第2次 ⇒ 注册表空={regStillGone} 占格空={occStillGone} 注册表计数 {regCntBeforeDoor}→{regCntAfterSet}→{regCntAfter2}"
          + $" ｜ 已销毁引用第3次 ⇒ 无异常（`b == null` 早退）");
        L($"[J3·边界登记] 门 ⛔ 不改 `state`（状态归调用方）⇒ 契约外用（对 Active 建筑直调两次）第 2 次会重跑回收 ⇒ 登记观察项（⛔ 本批不加机制）");
        Object.DestroyImmediate(tower2.gameObject);   // 提前销毁（防其炮塔继续开火污染 J4 的 HP 读数）

        // ══ 判据3（`D852` §4.2）· `Die()` 二次调 ⇒ 零副作用 ══
        var chestMgr = ChestManager.Instance;
        var siteStore = typeof(Building).GetMethod("EnsureSiteStore", BF)?.Invoke(tower3, null) as ConstructionSiteStore;   // ⛔ 构造法：私有口（探针侧）
        bool awaiting = tower3.IsSiteAwaitingMaterials;
        ResourceType seedTy = ResourceType.Wood; int seedNeed = -1;
        foreach (var ty in new[] { ResourceType.Wood, ResourceType.Stone, ResourceType.Gold, ResourceType.Food })
            if (siteStore != null && siteStore.NeedOf(ty) > 0) { seedTy = ty; seedNeed = siteStore.NeedOf(ty); break; }
        int dep = (siteStore != null && seedNeed > 0) ? siteStore.Deposit(seedTy, seedNeed) : -1;
        int seedHave = siteStore != null ? siteStore.GetAmount(seedTy) : -1;   // ⭐ 注入后**实测**（⛔ 不用字面量）
        yield return null;
        int chestCnt0 = chestMgr != null ? chestMgr.Count : -1;
        int chestAt0 = chestMgr != null ? chestMgr.CountAt(cT3) : -1;
        int excBeforeJ5 = _excCount;
        int diedJ5 = 0;
        System.Action<UnitDiedEvent> onDied5 = evt => { if (ReferenceEquals(evt.Unit, tower3)) diedJ5++; };
        EventBus.Subscribe(onDied5);
        tower3.Die(DeathCause.Killed);                                  // 第 1 次（生产入口直调）
        int have1 = siteStore != null ? siteStore.GetAmount(seedTy) : -1;
        int chestCnt1 = chestMgr != null ? chestMgr.Count : -1;
        int chestAt1 = chestMgr != null ? chestMgr.CountAt(cT3) : -1;
        int diedAfter1 = diedJ5;
        bool regGone5a = reg.GetAt(cT3) == null, occGone5a = grid.GetOccupant(cT3) == null;
        tower3.Die(DeathCause.Killed);                                  // 第 2 次（同帧 · 期望被入口守卫挡下）
        int have2 = siteStore != null ? siteStore.GetAmount(seedTy) : -1;
        int chestCnt2 = chestMgr != null ? chestMgr.Count : -1;
        int chestAt2 = chestMgr != null ? chestMgr.CountAt(cT3) : -1;
        int diedAfter2 = diedJ5;
        bool regGone5b = reg.GetAt(cT3) == null, occGone5b = grid.GetOccupant(cT3) == null;
        EventBus.Unsubscribe(onDied5);
        L($"[J5·构造] 工地态塔@({cT3.x},{cT3.y}) state=Constructing · `IsSiteAwaitingMaterials`={awaiting} ｜ `EnsureSiteStore`={(siteStore != null)}"
          + $" ｜ 注入尝试 type={seedTy} need={seedNeed} ⇒ `Deposit` 返回={dep} · 注入后 `GetAmount`={seedHave}"
          + $"（⚠️ `Deposit` 阈值拦截 `ConstructionSiteStore.cs:135-137` ⇒ 返回 0 ＝ **未注入** · 读数照实）");
        L($"[J5·第1次 `Die`] 工地仓 {seedTy} {seedHave}→{have1}｜ChestManager.Count {chestCnt0}→{chestCnt1}｜本格箱子 {chestAt0}→{chestAt1}"
          + $"｜本塔 UnitDiedEvent={diedAfter1}（期望 1）｜注册表空={regGone5a} 占格空={occGone5a}（期望 True/True）");
        L($"[J5·第2次 `Die`] 工地仓 {seedTy}={have2}｜ChestManager.Count {chestCnt1}→{chestCnt2}（期望不变）｜本格箱子 {chestAt1}→{chestAt2}（期望不变）"
          + $"｜本塔 UnitDiedEvent={diedAfter2}（期望仍 1 ⇒ ⛔ 不重复广播）｜注册表空={regGone5b} 占格空={occGone5b}（期望不变）"
          + $"｜异常新增={_excCount - excBeforeJ5}（期望 0）");
        L($"[J5·结论读数列] 二次调零副作用 ⇒ ① 掉箱侧：`Die` 第 1 次后工地仓 {seedHave}→{have1} ／ 箱子 {chestCnt0}→{chestCnt1}、本格 {chestAt0}→{chestAt1}；"
          + $"第 2 次后 **箱子/本格/工地仓三者全不变**（{chestCnt1}→{chestCnt2} ／ {chestAt1}→{chestAt2} ／ {have1}→{have2}）"
          + $" ② 回收不重复（注册表/占格不变）③ 广播不重复（事件计数 {diedAfter2}）④ 无异常（{_excCount - excBeforeJ5}）");
        L($"[L-51·声明] 本臂**直调** `Building.Die`（生产入口）；⚠️ 工地态建筑 `TakeDamage` 会因 `state != Active` 提前返回（`:1283`）⇒ 击毁链在该态**不可达** ⇒ 直调为唯一可判构造");
        yield return null;   // 让第 1 次的 Destroy 生效

        // ══ 判据2 · EnterRuined 链回归（非工事被破）══
        int diedBeforeFarm = diedCount;
        EventBus.Subscribe(onDied);
        int fHp0 = farm.CurrentHp;
        int fDealt = dmg.ApplyDamage(shooter, farm, 999999, 0f, true);
        yield return null;
        EventBus.Unsubscribe(onDied);
        var farmState = (BuildingState)typeof(Building).GetField("state", BF).GetValue(farm);
        L($"[J2·EnterRuined 回归] farm HP {fHp0}（ApplyDamage={fDealt}）⇒ state={farmState}（期望 Ruined）"
          + $" ｜ 占格仍在={grid.GetOccupant(cF) != null}（期望 True · 保持阻挡） 注册表仍在={reg.GetAt(cF) != null}（期望 True · 未注销）"
          + $" ｜ 实体仍在={farm != null}（期望 True · 未销毁） ｜ 新增 UnitDiedEvent={(diedCount - diedBeforeFarm)}（期望 0 · 不发死亡事件）");

        // ══ 判据 · 件3：cap ≤ 0 守卫（生产链）══
        var cU0 = new GridCoord(band.x + 5, band.y);
        var targets = new List<UnitController>();
        var shooters = new List<UnitController>();
        for (int i = 0; i < 3; i++)
        {
            var ss = SpawnUnitOf(Faction.PlayerCamp, Occupation.Archer, CellCenterWorld(new GridCoord(cU0.x - 3, cU0.y + i)), 0);
            var tt = SpawnUnitOf(Faction.PlayerCamp, Occupation.Warrior, CellCenterWorld(new GridCoord(cU0.x, cU0.y + i)), 1);
            FreezeBrain(ss); FreezeBrain(tt);
            shooters.Add(ss); targets.Add(tt);
        }
        yield return null;
        var profile = new AttackProfile
        {
            attack = 5, range = 6f, cd = 1f, isRanged = true, projectileSpeed = 60f,
            projectileType = ProjectileType.Arrow, pierceLevel = 1,
            ballisticType = BallisticType.HighArc, arcHeightCells = 0f,
            aoeRadiusCells = 0f, aoeFalloff = 0f, effectType = GroundEffectType.None,
        };
        System.Action pinAll = () => { for (int i = 0; i < 3; i++) { if (shooters[i] != null) MoveUnit(shooters[i], CellCenterWorld(new GridCoord(cU0.x - 3, cU0.y + i))); if (targets[i] != null) MoveUnit(targets[i], CellCenterWorld(new GridCoord(cU0.x, cU0.y + i))); } };
        int capErr0 = _capErrCount;
        dmgCfg.maxAttacksPerFrame = 0;                                    // ⭐ 非法值 ⇒ 期望：LogError ＋ 兜底 1
        pinAll();
        for (int i = 0; i < 3; i++) dmg.RegisterAttack(shooters[i], targets[i], profile);
        yield return null;
        L($"[J4·cap=0⛔非法] LogError 出现={( _capErrCount - capErr0)} 条｜样本：{_capErrSample}");
        for (int k = 0; k < 6; k++)
        {
            yield return new WaitForSeconds(0.5f);
            pinAll();
            L($"[J4·cap=0 f{k}] pending={pend.Count} 靶HP={targets[0].CurrentHp}/{targets[1].CurrentHp}/{targets[2].CurrentHp}"
              + $" LogError累计={_capErrCount - capErr0}");
        }
        dmgCfg.maxAttacksPerFrame = 100;                                  // 还原
        L($"[J4·结论读数列] cap=0 ⇒ ① LogError 出现 ② 判定未停摆（靶 HP 下降）③ `pending` 有界（逐帧读数见上）⇒ 与 `D851` 实核的「零判定 ＋ +3/帧 无界增」形成对照（⛔ 积压上限/丢弃策略本批不做 · `O-27`）");

        // ── 清理 ──
        foreach (var u in shooters) if (u != null) { UnitRegistry.Instance?.Unregister(u); Object.DestroyImmediate(u.gameObject); }
        foreach (var u in targets) if (u != null) { UnitRegistry.Instance?.Unregister(u); Object.DestroyImmediate(u.gameObject); }
        if (shooter != null) { UnitRegistry.Instance?.Unregister(shooter); Object.DestroyImmediate(shooter.gameObject); }
        if (farm != null) { grid.Free(cF); reg.Unregister(farm); Object.DestroyImmediate(farm.gameObject); }
        if (tower2 != null) { Object.DestroyImmediate(tower2.gameObject); }
        yield return null;

        TestHarnessApi.ExitTestRun();
        Application.logMessageReceived -= OnLog;
        L("[收尾] ExitTestRun 已执行（Play 由执行端外部 stop）");
        Flush("door");
    }

    // ========================================================================
    //  菜单 B · 件2（假 null 守卫 · 修前/修后对照）
    // ========================================================================
    [MenuItem("Valley/验证/HH321B 件2假null守卫")]
    public static void RunFakeNull()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[HH321C] 须先进入 Play 后调用本菜单。"); return; }
        new GameObject("HH321C_Host").AddComponent<RunHost>().Host(RunFakeNullCo());
    }

    public static IEnumerator RunFakeNullCoroutine() => RunFakeNullCo();

    static IEnumerator RunFakeNullCo()
    {
        var cfg = new NewGameConfig
        {
            worldSeed = 20321, mapSeed = 20321, raceId = 0, difficulty = 2,
            worldSize = WorldSize.Small, selectedSlotId = "smoke_hh321d", kingdomName = "假null"
        };
        yield return TestHarnessApi.EnterTestRun(cfg, 1f);
        yield return WaitWorld();
        Application.logMessageReceived += OnLog;

        var grid = GridSystem.Instance;
        var dmg = DamageSystem.Instance;
        var regs = typeof(DamageSystem).GetField("_registrations", BF).GetValue(dmg) as System.Collections.IDictionary;
        var pend = typeof(DamageSystem).GetField("_pendingAttacks", BF).GetValue(dmg) as System.Collections.IList;

        L("=== HH.321 批 2 · 件2（接口假 null 守卫）修前/修后对照 ===");
        var anchor = grid.WorldToCoord(WorldManager.Instance.GetKingdomAnchorWorld());
        GridCoord site;
        if (!anchor.HasValue || !FindSite(anchor.Value, 4, out site)) { Debug.LogError("[HH321C] 找不到场址"); TestHarnessApi.ExitTestRun(); yield break; }

        var shooter = SpawnUnitOf(Faction.PlayerCamp, Occupation.Archer, CellCenterWorld(new GridCoord(site.x - 3, site.y)), 0);
        var target = SpawnUnitOf(Faction.PlayerCamp, Occupation.Warrior, CellCenterWorld(new GridCoord(site.x, site.y)), 1);
        var victim = SpawnUnitOf(Faction.PlayerCamp, Occupation.Warrior, CellCenterWorld(new GridCoord(site.x + 1, site.y + 1)), 1);
        yield return null;
        FreezeBrain(shooter); FreezeBrain(target); FreezeBrain(victim);
        yield return null;
        if (shooter == null || target == null || victim == null) { Debug.LogError("[HH321C] 单位缺失"); TestHarnessApi.ExitTestRun(); yield break; }

        // ── 造「假 null attacker」：销毁对象但保留接口引用 ──
        IDamageable fake = victim;
        Object.Destroy(victim.gameObject);
        yield return null;                  // 让 Destroy 生效
        L($"[构造] 假 null attacker：`fake == null`（接口静态类型）= {fake == null}（期望 False · ⚠️ 病根）"
          + $" ｜ `(fake as UnityEngine.Object) == null` = {(fake as UnityEngine.Object) == null}（期望 True）"
          + $" ｜ `CombatRules.IsUnityNull(fake)` = {CombatRules.IsUnityNull(fake)}（期望 True · 新公共口）"
          + $" ｜ 注册表残留初值={regs.Contains(fake)}");

        var profile = new AttackProfile
        {
            attack = 5, range = 6f, cd = 1f, isRanged = true, projectileSpeed = 60f,
            projectileType = ProjectileType.Arrow, pierceLevel = 1,
            ballisticType = BallisticType.HighArc, arcHeightCells = 0f,
            aoeRadiusCells = 0f, aoeFalloff = 0f, effectType = GroundEffectType.None,
        };

        int tHp0 = target.CurrentHp;
        int directCatch = 0; string directMsg = "";
        try { dmg.RegisterAttack(fake, target, profile); }
        catch (System.Exception ex) { directCatch++; directMsg = ex.GetType().Name + ": " + ex.Message; }
        yield return null;
        bool regAfter = regs.Contains(fake);
        L($"[修前/修后·直调臂] `RegisterAttack(假null, target)` ⇒ 抛出异常数={directCatch}"
          + $" {(directCatch > 0 ? "（样本：" + directMsg + "）" : "")}"
          + $" ｜ 目标 HP {tHp0}→{target.CurrentHp} ｜ 注册残留={regAfter}");

        // ── tick 臂：注册若残留 ⇒ 时间轮每 tick 会再尝试（修前＝异常反复）──
        int excTick0 = _excCount;
        for (int k = 0; k < 5; k++)
        {
            yield return new WaitForSeconds(0.4f);
            L($"[修前/修后·tick臂 f{k}] 未捕获异常累计={_excCount - excTick0} ｜ 注册残留={regs.Contains(fake)} ｜ pending={pend.Count} ｜ 目标 HP={target.CurrentHp}");
        }
        L($"[修前/修后·tick臂 样本] {(_excSample.Length > 0 ? _excSample : "（无未捕获异常）")}");
        L($"[判据读数列] 假 null attacker ⇒ ① 直调是否抛异常 ② 注册是否残留（假 null ⇒ 守卫失效 ⇒ 残留 ⇒ 每 tick 复发）"
          + $" ③ 目标是否受伤（两态同：打不出伤）⇒ 修后应当 ①=0 ②=False");
        L($"[L-51·生产路径可达性声明] 本臂**直接调** `DamageSystem.RegisterAttack`（战斗链生产入口 · ⛔ 未走发箭链路）；"
          + $" `DamageSystem.cs:280` 守卫位于 `ExecuteAttack` ⇒ 与本臂调用路径**同源**（`RegisterAttack → ExecuteAttack`）");

        // 清理
        foreach (var u in new[] { target, shooter }) if (u != null) { UnitRegistry.Instance?.Unregister(u); Object.DestroyImmediate(u.gameObject); }
        yield return null;
        TestHarnessApi.ExitTestRun();
        Application.logMessageReceived -= OnLog;
        L("[收尾] ExitTestRun 已执行（Play 由执行端外部 stop）");
        Flush("fakenull");
    }
}
