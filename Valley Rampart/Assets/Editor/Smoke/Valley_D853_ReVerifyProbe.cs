// ============================================================================
//  `D853` 独立复验探针（策划端独立复验实例 · 独立路径）
//    ⛔ 不写生产码、⛔ 不改资产；本文件仅 Editor-only 读取/构造，产物落 仓库根 Logs/
//    覆盖：R1 判据1（经门 · 新判据侧）／R2 判据2（门幂等 ＋ ⭐反向鉴别 ＋ ⭐边界臂）／
//          R3 判据3（`Die` 二次调 · 空仓臂 ＋ ⭐批外构造「材料阶段」非空掉箱臂）／
//          R4 判据4（`EnterRuined` 回归）／R5 回归（`cap=0`）
//    ⚠️ 独立性声明：本档为**另开档**（⛔ 不复用 `Valley_HH321B_DoorProbe.cs` 的构造与埋点）；
//       载体仍为**生产链**（击毁入口 `DamageSystem.ApplyDamage → Building.TakeDamage → Die`）。
//    ⚠️ `L-89`③：「预期反转」类对照的**预期在测量前写死**（见 RunCo 开头「预期·事先写死」行）。
// ============================================================================
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class Valley_D853_ReVerifyProbe
{
    const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly StringBuilder SB = new StringBuilder();
    static readonly List<IDamageable> _scan = new List<IDamageable>();
    static int _capErrCount, _excCount;
    static string _capErrSample = "", _excSample = "";

    static void OnLog(string msg, string stack, LogType type)
    {
        if (type == LogType.Error && msg.Contains("maxAttacksPerFrame")) { _capErrCount++; if (_capErrSample.Length == 0) _capErrSample = msg; }
        if (type == LogType.Exception) { _excCount++; if (_excSample.Length == 0) _excSample = msg; }
    }

    static void L(string s) { Debug.Log("[D853V] " + s); SB.AppendLine(s); }

    static void Flush()
    {
        SB.AppendLine("# 封存 " + System.DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
        var dir = System.IO.Path.Combine(Application.dataPath, "..", "..", "Logs");
        System.IO.Directory.CreateDirectory(dir);
        var path = System.IO.Path.Combine(dir, "d853_verify_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".txt");
        System.IO.File.WriteAllText(path, SB.ToString());
        Debug.Log("[D853V] 落盘 " + path);
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

    [MenuItem("Valley/验证/D853 复验 件1独立路径")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[D853V] 须先进入 Play 后调用本菜单。"); return; }
        new GameObject("D853V_Host").AddComponent<RunHost>().Host(RunCo());
    }

    public static IEnumerator RunCoroutine() => RunCo();

    static IEnumerator RunCo()
    {
        var cfg = new NewGameConfig
        {
            worldSeed = 20321, mapSeed = 20321, raceId = 0, difficulty = 2,
            worldSize = WorldSize.Small, selectedSlotId = "smoke_d853v", kingdomName = "复验"
        };
        yield return TestHarnessApi.EnterTestRun(cfg, 1f);
        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null
               || GridSystem.Instance == null || GridSystem.Instance.Config == null
               || BuildingRegistry.Instance == null || UnitFactory.Instance == null
               || DamageSystem.Instance == null || ChestManager.Instance == null)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 180f) { Debug.LogError("[D853V] 等世界就绪超时。"); yield break; }
        }
        yield return new WaitForSeconds(0.5f);
        Application.logMessageReceived += OnLog;

        var grid = GridSystem.Instance;
        var reg = BuildingRegistry.Instance;
        var dmg = DamageSystem.Instance;
        var dmgCfg = typeof(DamageSystem).GetField("_config", BF).GetValue(dmg) as DamageConfig;
        var pend = typeof(DamageSystem).GetField("_pendingAttacks", BF).GetValue(dmg) as System.Collections.IList;

        L("=== `D853` 独立复验（策划端独立实例 · 另开档 · 生产链载体）===");
        L($"[环境] seed={WorldManager.Instance.ActiveMap.seed} timeScale={Time.timeScale} 建筑在册={reg.Count} 单位在册={UnitRegistry.Instance.Count} cap={dmgCfg.maxAttacksPerFrame}");

        // ⭐ 预期事先写死（`L-89`③ · 在测量之前落盘）
        L("[预期·事先写死] R1 判据1：事件内 注册表空=True／占格空=True／实体可用=True／位对上=True／帧后已销毁=True；旧判据判别式(state==Dead)=True");
        L("[预期·事先写死] R2 判据2：门首调⇒双空 True/True 且实体仍在 True；双空复调⇒判据实参两 True＋五面零变化＋无异常；已销毁引用⇒无异常早退；边界臂(后注册者占同格)⇒判据 False⇒门执行");
        L("[预期·事先写死] R3 判据3：Die×2⇒事件计数 1；空仓臂 工地仓 0→0；⭐材料阶段臂 工地仓 8→0(第1次) 且第2次零变化＋箱子侧仅第1次变");
        L("[预期·事先写死] R4 判据4：farm 击破⇒state=Ruined／占格仍在 True／注册表仍在 True／实体仍在 True／新增 UnitDiedEvent=0");
        L("[预期·事先写死] R5 回归：cap=0⇒LogError 1 条／pending 有界／靶 HP 逐帧下降");
        L("[预期·事先写死] R2 边界臂（新增鉴别臂）：同格后注册者 nb ⇒ 判据实参 `GetAt(A2.coord)==null`=False ⇒ 门**执行**；`FreeFootprint` 无归属判定（GridSystem.cs:445-450）⇒ 预期 **nb 占格被清空（False）**、nb 注册表项保持（BuildingRegistry.Unregister:39 有归属判定 ⇒ True）");

        var anchor = grid.WorldToCoord(WorldManager.Instance.GetKingdomAnchorWorld());
        GridCoord band;
        if (!anchor.HasValue || !FindSite(anchor.Value, 24, out band))
        { Debug.LogError("[D853V] 找不到场址"); TestHarnessApi.ExitTestRun(); yield break; }
        L($"[场址] band=({band.x},{band.y}) 24×2 格空置且 12 格内无敌");

        var towerDef = Resources.Load<BuildingDef>("Buildings/arrow_tower");
        var farmDef = Resources.Load<BuildingDef>("Buildings/farm");
        if (towerDef == null || farmDef == null) { Debug.LogError("[D853V] 资产缺失"); TestHarnessApi.ExitTestRun(); yield break; }
        var fpT = new Vector2Int(Mathf.Max(1, towerDef.footprint.x), Mathf.Max(1, towerDef.footprint.y));
        var fpF = new Vector2Int(Mathf.Max(1, farmDef.footprint.x), Mathf.Max(1, farmDef.footprint.y));

        var shooter = SpawnUnitOf(Faction.PlayerCamp, Occupation.Archer, CellCenterWorld(new GridCoord(band.x, band.y)), 0);
        FreezeBrain(shooter);
        var cT = new GridCoord(band.x + 6, band.y);      // R1 塔
        var cA = new GridCoord(band.x + 10, band.y);     // R2 幂等塔
        var cE = new GridCoord(band.x + 14, band.y);     // R3 空仓臂（工地态）
        var cM = new GridCoord(band.x + 18, band.y);     // R3 ⭐材料阶段臂
        var cF = new GridCoord(band.x + 21, band.y);     // R4 farm

        bool b1 = BuildingFactory.Instance.CreateBuildingInstance(towerDef, towerDef.sourceType, cT, fpT, GridSystem.FootprintCenterWorld(cT, fpT, Vector3.zero), false, ResourceGrade.Normal, false, BuildingState.Active, 1);
        bool b2 = BuildingFactory.Instance.CreateBuildingInstance(towerDef, towerDef.sourceType, cA, fpT, GridSystem.FootprintCenterWorld(cA, fpT, Vector3.zero), false, ResourceGrade.Normal, false, BuildingState.Active, 1);
        bool b3 = BuildingFactory.Instance.CreateBuildingInstance(towerDef, towerDef.sourceType, cE, fpT, GridSystem.FootprintCenterWorld(cE, fpT, Vector3.zero), false, ResourceGrade.Normal, false, BuildingState.Constructing, 1);
        bool b4 = BuildingFactory.Instance.CreateBuildingInstance(towerDef, towerDef.sourceType, cM, fpT, GridSystem.FootprintCenterWorld(cM, fpT, Vector3.zero), false, ResourceGrade.Normal, false, BuildingState.Constructing, 1);
        bool b5 = BuildingFactory.Instance.CreateBuildingInstance(farmDef, farmDef.sourceType, cF, fpF, GridSystem.FootprintCenterWorld(cF, fpF, Vector3.zero), false, ResourceGrade.Normal, false, BuildingState.Active, 1);
        yield return null;
        var tR1 = grid.GetOccupant(cT) as Building;
        var tR2 = grid.GetOccupant(cA) as Building;
        var tE = grid.GetOccupant(cE) as Building;
        var tM = grid.GetOccupant(cM) as Building;
        var farm = grid.GetOccupant(cF) as Building;
        if (!b1 || !b2 || !b3 || !b4 || !b5 || tR1 == null || tR2 == null || tE == null || tM == null || farm == null)
        { Debug.LogError("[D853V] 建筑落成失败"); TestHarnessApi.ExitTestRun(); yield break; }
        L($"[布置] R1塔@({cT.x},{cT.y}) 工事={tR1.IsFortification} HP={tR1.CurrentHp}｜R2塔@({cA.x},{cA.y})｜R3空仓塔@({cE.x},{cE.y}) 工事={tE.IsFortification}｜R3材料塔@({cM.x},{cM.y})｜R4 farm@({cF.x},{cF.y}) 工事={farm.IsFortification}");

        // ══════ R1 · 判据 1（经门 · 新判据侧 · 生产链） ══════
        int regBefore = reg.GetAt(cT) != null ? 1 : 0;
        int occBefore = grid.GetOccupant(cT) != null ? 1 : 0;
        var stateBefore = tR1.state;
        int diedT = 0;
        bool regAtEvent = false, occAtEvent = false, aliveAtEvent = false, posMatch = false;
        bool oldPredAtEvent = false; DeathCause causeAtEvent = DeathCause.Demolished;
        System.Action<UnitDiedEvent> onDied = evt =>
        {
            if (!ReferenceEquals(evt.Unit, tR1)) return;
            diedT++;
            regAtEvent = reg.GetAt(cT) == null;
            occAtEvent = grid.GetOccupant(cT) == null;
            aliveAtEvent = tR1 != null;
            posMatch = Vector2.Distance(evt.Position, tR1.transform.position) < 0.01f;
            oldPredAtEvent = tR1.state == BuildingState.Dead;      // ⭐ 旧判据表达式（判别式 · 码面）
            causeAtEvent = evt.Cause;
        };
        EventBus.Subscribe(onDied);
        int dealt = dmg.ApplyDamage(shooter, tR1, 999999, 0f, true);   // 生产链：TakeDamage → hp≤0 → 工事 ⇒ Die(Killed)
        yield return null;
        bool aliveAfter = tR1 != null;
        EventBus.Unsubscribe(onDied);
        L($"[R1] 击前 注册表={regBefore} 占格={occBefore} state={stateBefore}｜ApplyDamage 返回={dealt}");
        L($"[R1·新判据侧·生产链实测] 事件内：注册表空={regAtEvent} 占格空={occAtEvent}（预期 True/True）｜实体可用={aliveAtEvent} 位对上={posMatch}（预期 True/True）｜帧后已销毁={!aliveAfter}（预期 True）｜事件数={diedT} Cause={causeAtEvent}");
        L($"[R1·旧判据判别式（⛔ 码面·不入能力列）] 事件内 `tR1.state == BuildingState.Dead` = {oldPredAtEvent} ⇒ 旧码在该点会命中 `:229` 分支（跳过回收）⇒ 旧判据侧的 False/False 来自**上一批同构造产物**，非本 HEAD 可执行路径");

        // ══════ R2 · 判据 2（门幂等 ＋ 反向鉴别 ＋ 边界臂） ══════
        BuildingFactory.Instance.RemoveBuilding(tR2, DeathCause.Demolished);              // 第 1 次（直调）
        bool r2a = reg.GetAt(cA) == null, r2b = grid.GetOccupant(cA) == null, r2c = tR2 != null;
        // 复调（判据实参即时读）
        bool arg1 = reg.GetAt(cA) == null, arg2 = grid.GetOccupant(cA) == null;
        int regCnt0 = reg.Count;
        int exc0 = _excCount;
        BuildingFactory.Instance.RemoveBuilding(tR2, DeathCause.Demolished);              // 第 2 次（双空）
        int regCnt1 = reg.Count;
        bool r2d = reg.GetAt(cA) == null, r2e = grid.GetOccupant(cA) == null, r2f = tR2 != null;
        int exc1 = _excCount;
        // 已销毁引用臂
        int exc2 = _excCount;
        BuildingFactory.Instance.RemoveBuilding(tR1, DeathCause.Killed);
        int exc3 = _excCount;
        L($"[R2·门第1次] 注册表空={r2a} 占格空={r2b}（预期 True/True）｜门后实体仍在={r2c}（预期 True · 门不销毁）");
        L($"[R2·复调·判据实参即时读] `reg.GetAt(b.coord)==null`={arg1} ∧ `grid.GetOccupant(b.coord)==null`={arg2}（预期 True/True）⇒ 盘上路径为 `:233 return`（⛔ 码面）"
          + $"｜调用后：注册表空={r2d} 占格空={r2e} 实体仍在={r2f}｜注册表计数 {regCnt0}→{regCnt1}｜异常新增={exc1 - exc0}");
        L($"[R2·鉴伪] ⚠️「跳过」分支**无外部可观测指纹**（跳过 vs 于空状态执行：`FreeFootprint`(空格无害)／`Unregister`(归属判定命中不到)／`TaskScheduler.Unregister`(已注销)）⇒ 本节仅能记**判据实参实测＋码面路径**，⛔ 不构成能力列读数");
        L($"[R2·已销毁引用臂] `RemoveBuilding(已销毁 tR1, Killed)` ⇒ 异常新增={exc3 - exc2}（预期 0）");
        // ⭐ 边界臂：后注册者占同格 ⇒ 判据 `GetAt(coord)==null` 为 False ⇒ 门执行 ⇒ 观测对后注册者的占格影响
        bool bn = BuildingFactory.Instance.CreateBuildingInstance(towerDef, towerDef.sourceType, cA, fpT, GridSystem.FootprintCenterWorld(cA, fpT, Vector3.zero), false, ResourceGrade.Normal, false, BuildingState.Active, 1);
        yield return null;
        var nb = grid.GetOccupant(cA) as Building;
        bool nbReg0 = nb != null && ReferenceEquals(reg.GetAt(cA), nb);
        bool nbOcc0 = grid.GetOccupant(cA) != null;
        bool predArgAfterNew = reg.GetAt(cA) == null;
        BuildingFactory.Instance.RemoveBuilding(tR2, DeathCause.Demolished);              // 双空判据已被"后注册者"破坏 ⇒ 应执行
        bool nbOcc1 = grid.GetOccupant(cA) != null;
        bool nbReg1 = ReferenceEquals(reg.GetAt(cA), nb);
        bool nbAlive = nb != null;
        L($"[R2·边界臂（`F-15` 单槽）] 同格后注册者 nb 落成={bn}｜判据实参 `GetAt(A2.coord)==null`={predArgAfterNew}（预期 False ⇒ 门**执行**）"
          + $"｜执行前：nb 占格={nbOcc0}（预期 True）注册表项={nbReg0}（预期 True）"
          + $"｜执行后：nb 占格仍在={nbOcc1}（**事先预期 False · 被清空**）｜nb 注册表项仍在={nbReg1}（事先预期 True · 归属判定）｜nb 实体仍在={nbAlive}（预期 True）");

        // ══════ R3 · 判据 3（`Die` 二次调 · 空仓臂 ＋ ⭐材料阶段臂） ══════
        var chestMgr = ChestManager.Instance;
        int evtCountE = 0, evtCountM = 0;
        System.Action<UnitDiedEvent> onDiedR3 = evt =>
        {
            if (ReferenceEquals(evt.Unit, tE)) evtCountE++;
            if (ReferenceEquals(evt.Unit, tM)) evtCountM++;
        };
        EventBus.Subscribe(onDiedR3);
        // 空仓臂
        int ce0 = chestMgr.Count, ce0at = chestMgr.CountAt(cE);
        int excE0 = _excCount;
        tE.Die(DeathCause.Killed);
        int ce1 = chestMgr.Count, ce1at = chestMgr.CountAt(cE);
        tE.Die(DeathCause.Killed);
        int ce2 = chestMgr.Count, ce2at = chestMgr.CountAt(cE);
        L($"[R3·空仓臂] Die#1：箱子 {ce0}→{ce1}（本格 {ce0at}→{ce1at}）事件={evtCountE}｜Die#2：箱子 {ce1}→{ce2}（本格 {ce1at}→{ce2at}）事件={evtCountE}｜异常={_excCount - excE0}");
        // ⭐ 材料阶段臂（批外构造 · 反射私有口 · ⛔ 不改生产码）
        var mBegin = typeof(Building).GetMethod("BeginMaterialPhase", BF);
        var mStore = typeof(Building).GetMethod("EnsureSiteStore", BF);
        var need = ResourceList.Empty.Set(ResourceType.Wood, 8).Set(ResourceType.Stone, 8);
        L($"[R3·材料臂·构造] `SiteNeedOf(need).IsZero`={Building.SiteNeedOf(need).IsZero}（需为 False 才能建仓）");
        mBegin?.Invoke(tM, new object[] { need });
        var mStoreObj = mStore?.Invoke(tM, null) as ConstructionSiteStore;
        int mDep = mStoreObj != null ? mStoreObj.Deposit(ResourceType.Wood, 8) : -1;   // 只投 Wood（Stone 留缺 ⇒ 不触发"料齐开工 ⇒ Clear"）
        bool mAwait = tM.IsSiteAwaitingMaterials;
        int mHave = mStoreObj != null ? mStoreObj.GetAmount(ResourceType.Wood) : -1;
        int cm0 = chestMgr.Count, cm0at = chestMgr.CountAt(cM);
        int excM0 = _excCount;
        int regM0 = reg.Count;
        tM.Die(DeathCause.Killed);                                    // Die#1（生产入口直调 · ⛔ L-51 声明见报告）
        int mHave1 = mStoreObj != null ? mStoreObj.GetAmount(ResourceType.Wood) : -1;
        int cm1 = chestMgr.Count, cm1at = chestMgr.CountAt(cM);
        bool mRegGone = reg.GetAt(cM) == null, mOccGone = grid.GetOccupant(cM) == null;
        tM.Die(DeathCause.Killed);                                    // Die#2
        int mHave2 = mStoreObj != null ? mStoreObj.GetAmount(ResourceType.Wood) : -1;
        int cm2 = chestMgr.Count, cm2at = chestMgr.CountAt(cM);
        EventBus.Unsubscribe(onDiedR3);
        L($"[R3·材料臂] `IsSiteAwaitingMaterials`={mAwait}（预期 True）｜`Deposit(Wood,8)` 返回={mDep}（预期 8）｜工地仓 Wood={mHave}（预期 8）｜（投料与 Die 同帧 ⇒ 无工人搬运窗口）");
        L($"[R3·材料臂·Die#1] 工地仓 Wood {mHave}→{mHave1}（预期 0）｜箱子 {cm0}→{cm1} 本格 {cm0at}→{cm1at}（预期出现掉箱箱）｜注册表空={mRegGone} 占格空={mOccGone}（预期 True/True）｜事件={evtCountM}（预期 1）");
        L($"[R3·材料臂·Die#2] 工地仓 Wood={mHave2}｜箱子 {cm1}→{cm2}（预期不变）本格 {cm1at}→{cm2at}（预期不变）｜事件={evtCountM}（预期仍 1）｜注册表计数 {regM0}→{reg.Count}｜异常={_excCount - excM0}");

        // ══════ R4 · 判据 4（`EnterRuined` 回归） ══════
        int diedFarm = 0;
        System.Action<UnitDiedEvent> onDiedFarm = evt => { if (ReferenceEquals(evt.Unit, farm)) diedFarm++; };
        EventBus.Subscribe(onDiedFarm);
        int fHp0 = farm.CurrentHp;
        int fDealt = dmg.ApplyDamage(shooter, farm, 999999, 0f, true);
        yield return null;
        EventBus.Unsubscribe(onDiedFarm);
        L($"[R4] farm HP {fHp0}（ApplyDamage={fDealt}）⇒ state={farm.state}（预期 Ruined）｜占格仍在={grid.GetOccupant(cF) != null}（预期 True）"
          + $"｜注册表仍在={reg.GetAt(cF) != null}（预期 True）｜实体仍在={farm != null}（预期 True）｜新增 UnitDiedEvent={diedFarm}（预期 0）");

        // ══════ R5 · 回归（cap=0） ══════
        var cU = new GridCoord(band.x + 2, band.y);
        var targets = new List<UnitController>();
        var shooters = new List<UnitController>();
        for (int i = 0; i < 3; i++)
        {
            var ss = SpawnUnitOf(Faction.PlayerCamp, Occupation.Archer, CellCenterWorld(new GridCoord(cU.x - 3, cU.y + i)), 0);
            var tt = SpawnUnitOf(Faction.PlayerCamp, Occupation.Warrior, CellCenterWorld(new GridCoord(cU.x, cU.y + i)), 1);
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
        System.Action pinAll = () =>
        {
            for (int i = 0; i < 3; i++)
            {
                if (shooters[i] != null) MoveUnit(shooters[i], CellCenterWorld(new GridCoord(cU.x - 3, cU.y + i)));
                if (targets[i] != null) MoveUnit(targets[i], CellCenterWorld(new GridCoord(cU.x, cU.y + i)));
            }
        };
        int capErr0 = _capErrCount;
        dmgCfg.maxAttacksPerFrame = 0;
        pinAll();
        for (int i = 0; i < 3; i++) dmg.RegisterAttack(shooters[i], targets[i], profile);
        yield return null;
        L($"[R5·cap=0] LogError 样本：{(_capErrSample.Length > 0 ? _capErrSample : "（暂无）")}");
        for (int k = 0; k < 6; k++)
        {
            yield return new WaitForSeconds(0.5f);
            pinAll();
            L($"[R5·cap=0 f{k}] LogError累计={_capErrCount - capErr0}｜pending={pend.Count}｜靶HP={targets[0].CurrentHp}/{targets[1].CurrentHp}/{targets[2].CurrentHp}");
        }
        dmgCfg.maxAttacksPerFrame = 100;
        L($"[R5·结论读数列] LogError 累计={_capErrCount - capErr0}（预期 1）｜pending 峰值有界（逐帧见上）｜靶 HP 逐帧下降（未停摆）");

        // 清理
        foreach (var u in shooters) if (u != null) { UnitRegistry.Instance?.Unregister(u); Object.DestroyImmediate(u.gameObject); }
        foreach (var u in targets) if (u != null) { UnitRegistry.Instance?.Unregister(u); Object.DestroyImmediate(u.gameObject); }
        if (shooter != null) { UnitRegistry.Instance?.Unregister(shooter); Object.DestroyImmediate(shooter.gameObject); }
        if (nb != null) { grid.Free(cA); reg.Unregister(nb); Object.DestroyImmediate(nb.gameObject); }
        if (tR2 != null) Object.DestroyImmediate(tR2.gameObject);
        if (farm != null) { grid.Free(cF); reg.Unregister(farm); Object.DestroyImmediate(farm.gameObject); }
        yield return null;

        TestHarnessApi.ExitTestRun();
        Application.logMessageReceived -= OnLog;
        L($"[收尾] ExitTestRun 已执行｜日志未捕获异常总数={_excCount}｜样本：{(_excSample.Length > 0 ? _excSample : "（无）")}");
        Flush();
    }
}
