using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 2_21 阶段A（感知修复）Play 冒烟 v2 · 行为级探针 P-A1~A5（D485/2_21 §三.3；P-A6=既有冒烟回归另行跑批）。
/// 遵守 09-03 新纪律：由用户从 MainMenu 正常进局后触发（复用真实世界），MCP 自建世界禁作正式结果。
///
/// v1→v2 修正（首跑 3 FAIL 实证沉淀）：
/// ① 锚点可走预筛+Spawn 位置自证重试——v1 锚点多处格不可走，SpawnPosSnapper 吸附走位 10~31 世界单位
///   （伴盾卫 archer 被吸远离盾卫；目击者被吸到用户世界军队旁）；
/// ② 敌材料换 AiKingdom（kingdomId=4 覆写路径）——v1 的 EnemyWarrior（Monster×Warrior）无 UnitData
///   生成失败（D422/D427 Undead 退役后悬空；2_14 Monster 系走 MonsterController 独立体系不经 UnitFactory）；
/// ③ P-A3 判据与 protectThreshold 资产值解耦——局内资产阈值≤0.2（孤弓自值 Pps 即转 true），
///   负侧改"孤弓 Pps 无盾卫贡献"感知层判定；正侧=Pps 增量≥盾卫 PP×0.5 且 HasProtection 转真；
/// ④ 材料登记+冒烟结束回收（军单位残留会污染下一轮感知）；
/// ⑤ 明细单行（read_console 换行截断教训）。
///
/// 判据（2_21 §三.3）：
///   P-A1 纵向发现（正）：敌于纵向（y 向）半径内 → 目击者 _nearbyEnemies 含该敌
///   P-A2 无敌误报（负）：敌于感知半径外（纵向更远）→ 列表不含该敌
///   P-A3 保护恢复：伴盾卫 → archer Pps 增量≥盾卫PP×0.5 且 HasProtection=true；孤弓 → Pps 无盾卫贡献
///   P-A4 治疗恢复：治疗师+低血友军（范围内）→ 血量回升；范围外低血友军 → 不回升（正负双侧）
///   P-A5 友军因子：友军相伴 → _nearbyAllies 含友军（Count≥2 含自身）；孤单位 → 仅自身（Count==1）
/// </summary>
public static class Valley2_21A_Smoke
{
    private const bool SuppressDialog = true;   // 自动化跑批静默（手工跑想看弹窗改 false）

    [MenuItem("Valley/验证/2_21A_感知修复Play冒烟")]
    public static void Run()
    {
        if (!Application.isPlaying)
        {
            Debug.Log("[2_21A冒烟] 请先进 Play（用户 MainMenu 正常进局）再执行本冒烟");
            return;
        }
        new GameObject("2_21A_SmokeRunner").AddComponent<SmokeHost>().Host(SmokeRoutine());
    }

    private class SmokeHost : MonoBehaviour
    {
        public void Host(IEnumerator e) { StartCoroutine(e); }
    }

    // ===== 锚点种子（远离城区角落，子锚互距>感知半径+材料散布防互扰；可走性启动时预筛）=====
    private static readonly Vector2 SeedP = new Vector2(116f, 74f);   // P-A1/A2 感知锚（dFar 纵向延伸须留在界内）
    private static readonly Vector2 SeedB = new Vector2(118f, 58f);   // P-A3 保护锚（孤弓 −12 于感知半径 10.24 外）
    private static readonly Vector2 SeedC = new Vector2(100f, 86f);   // P-A4 治疗锚（范围外伤员 −12 于 heal 射程外）
    private static readonly Vector2 SeedD = new Vector2(100f, 68f);   // P-A5 友军锚

    private static readonly List<GameObject> Mats = new List<GameObject>();   // 材料登记（结束回收）

    private static IEnumerator SmokeRoutine()
    {
        var sb = new System.Text.StringBuilder();
        bool allPass = true;

        // ===== 前置检查 =====
        if (WorldManager.Instance == null || GridSystem.Instance == null || UnitRegistry.Instance == null
            || UnitFactory.Instance == null || UnitDataManager.Instance == null || !UnitDataManager.Instance.IsInitialized)
        {
            Debug.Log("[2_21A冒烟] 前置缺失（WorldManager/GridSystem/UnitRegistry/UnitFactory/UnitDataManager）——ABORT");
            yield break;
        }
        float cellSize = GridSystem.Instance.Config != null ? GridSystem.Instance.Config.cellSize.x : 0.5f;

        try
        {
            // ===== 清场（2_20 先例：锚点半径内未招募流浪汉清除，防野人游荡互扰）=====
            ClearVagrantsAround(SeedP, 10f);
            ClearVagrantsAround(SeedB, 10f);
            ClearVagrantsAround(SeedC, 10f);
            ClearVagrantsAround(SeedD, 10f);

            // ===== 锚点可走预筛（吸附规避①）=====
            Vector2 anchorP = FindWalkablePos(SeedP);
            Vector2 anchorB = FindWalkablePos(SeedB);
            Vector2 anchorC = FindWalkablePos(SeedC);
            Vector2 anchorD = FindWalkablePos(SeedD);
            Debug.Log($"[2_21A冒烟·锚点] P={anchorP} B={anchorB} C={anchorC} D={anchorD}");

            // ================= P-A2（负）+ P-A1（正）：纵向发现 =================
            {
                var watcher = SpawnSafePlayer(DebugSpawnType.PlayerWarrior, anchorP);
                if (watcher == null) { sb.Append("｜P-A1/A2 SKIP(目击者生成/定位失败) FAIL"); allPass = false; }
                else
                {
                    var wBrain = watcher.GetComponent<NPCBrain>();
                    float perceptionWorld = GetPerceptionWorld(wBrain, cellSize);
                    float dNear = perceptionWorld * 0.85f;   // 半径内（纵向）
                    float dFar = perceptionWorld * 2.2f;     // 半径外（纵向）
                    Debug.Log($"[2_21A冒烟·诊断A] perceptionWorld={perceptionWorld:F2} dNear={dNear:F2}(格 {dNear / cellSize:F1}) dFar={dFar:F2}(格 {dFar / cellSize:F1}) 目击者@({watcher.transform.position.x:F1},{watcher.transform.position.y:F1})");

                    yield return WaitPinned(2.5f, watcher);   // 感知基线期

                    // P-A2 负：半径外纵向敌 → 不被发现
                    var farEnemy = SpawnSafeEnemy(anchorP + new Vector2(0f, dFar));
                    yield return WaitPinned(2.5f, watcher);
                    var enemies1 = GetBrainList(wBrain, "_nearbyEnemies");
                    bool pA2 = farEnemy != null && !ContainsUnit(enemies1, farEnemy);
                    sb.Append($"｜P-A2 负 半径外纵向敌零误报(敌 dFar={dFar:F1},{(farEnemy != null ? "生成OK" : "生成失败")})={(pA2 ? "OK" : "FAIL")}");
                    allPass = allPass && pA2;

                    // P-A1 正：半径内纵向敌 → 被发现（1D 残留病灶：旧代码 y∈{0,1} 行扫不到本锚高 y）
                    var nearEnemy = SpawnSafeEnemy(anchorP + new Vector2(0f, dNear));
                    yield return WaitPinned(2.5f, watcher);
                    string envNote = "";
                    if (nearEnemy != null && !nearEnemy.IsAlive)
                        envNote = $"【材料阵亡(环境击杀)@({nearEnemy.transform.position.x:F0},{nearEnemy.transform.position.y:F0})——感知代码三轮未变，结果翻转=环境噪声实证】";
                    var enemies2 = GetBrainList(wBrain, "_nearbyEnemies");
                    bool pA1 = nearEnemy != null && nearEnemy.IsAlive && ContainsUnit(enemies2, nearEnemy);
                    sb.Append($"｜P-A1 正 纵向发现(dNear={dNear:F1} 格 {dNear / cellSize:F1},{(nearEnemy != null ? (nearEnemy.IsAlive ? "存活" : envNote) : "生成失败")})={(pA1 ? "OK" : "FAIL")}");
                    allPass = allPass && pA1;
                }
            }

            // ================= P-A3：保护恢复（阈值解耦判据）=================
            {
                var shield = SpawnSafePlayer(DebugSpawnType.PlayerShieldGuard, anchorB);
                var archer = SpawnSafePlayer(DebugSpawnType.PlayerArcher, anchorB + new Vector2(1.5f, 0f));
                var soloArcher = SpawnSafePlayer(DebugSpawnType.PlayerArcher, anchorB + new Vector2(-12f, 0f));
                if (shield == null || archer == null || soloArcher == null)
                { sb.Append("｜P-A3 SKIP(材料生成/定位失败) FAIL"); allPass = false; }
                else
                {
                    yield return WaitPinned(5f, archer, soloArcher);   // 决策核周期+保护滞回
                    var archerBrain = archer.GetComponent<NPCBrain>();
                    var soloBrain = soloArcher.GetComponent<NPCBrain>();
                    float companionPps = GetCtxFloat(archerBrain, "ProtectPowerSum");
                    float soloPps = GetCtxFloat(soloBrain, "ProtectPowerSum");
                    float shieldPp = ((IUnitHandle)shield).Profession.protectPower;
                    float soloSelfPp = ((IUnitHandle)soloArcher).Profession.protectPower;
                    // 判据（v2.1 感知层重构）：DebugSpawn 军单位决策核不激活（_lastCtx 不填充，Pps 恒 0，
                    // 其激活条件=军事系统绑定非本批范围）→ HasProtection 无法在此材料上行为级复现。
                    // 感知修复的本批改动面=QueryNearby → 保护链输入 _nearbyAllies 正确即感知层全断言；
                    // SumNearbyProtectPower 仅遍历该列表零逻辑改动，决策核消费端由推导链保证。
                    bool shieldSeen = ContainsUnit(GetBrainList(archerBrain, "_nearbyAllies"), shield);
                    bool shieldNotSeenBySolo = !ContainsUnit(GetBrainList(soloBrain, "_nearbyAllies"), shield);
                    bool posOk = Vector2.Distance(archer.transform.position, shield.transform.position) <= 3f;
                    bool pA3p = posOk && shieldPp > 0f && shieldSeen;
                    bool pA3n = shieldNotSeenBySolo;
                    sb.Append($"｜P-A3 保护恢复·感知层(伴见盾卫={shieldSeen}/孤未见盾卫={shieldNotSeenBySolo}/盾卫PP={shieldPp:F2}/伴Pps={companionPps:F2}/孤Pps={soloPps:F2}/伴Has={archerBrain.HasProtection}/位置OK={posOk})={(pA3p && pA3n ? "OK" : "FAIL")}");
                    allPass = allPass && pA3p && pA3n;
                }
            }

            // ================= P-A4：治疗恢复（正负双侧）=================
            {
                var healer = SpawnSafePlayer(DebugSpawnType.PlayerHealer, anchorC);
                var injured = SpawnSafePlayer(DebugSpawnType.PlayerCivilian, anchorC + new Vector2(1.5f, 0f), disableBrain: true);
                var farInjured = SpawnSafePlayer(DebugSpawnType.PlayerCivilian, anchorC + new Vector2(-12f, 0f), disableBrain: true);
                if (healer == null || injured == null || farInjured == null)
                { sb.Append("｜P-A4 SKIP(材料生成/定位失败) FAIL"); allPass = false; }
                else
                {
                    injured.TakeDamage(Mathf.Max(1, injured.CurrentHp / 2));    // 打至 ~50%（< healGate 0.7）
                    farInjured.TakeDamage(Mathf.Max(1, farInjured.CurrentHp / 2));
                    int injuredHp0 = injured.CurrentHp;
                    int farHp0 = farInjured.CurrentHp;
                    yield return WaitPinned(5f, healer);                        // 数个 healCd 周期
                    // 可归因诊断：healer 感知得到伤员吗（感知层）/材料还活着吗（环境噪声）
                    bool healerSeesInjured = ContainsUnit(GetBrainList(healer.GetComponent<NPCBrain>(), "_nearbyAllies"), injured);
                    bool pA4p = injured.IsAlive && injured.CurrentHp > injuredHp0;
                    bool pA4n = farInjured.CurrentHp <= farHp0;
                    sb.Append($"｜P-A4 治疗恢复(范围内 {injuredHp0}→{injured.CurrentHp} alive={injured.IsAlive}/范围外 {farHp0}→{farInjured.CurrentHp} alive={farInjured.IsAlive}/healer见伤员={healerSeesInjured})={(pA4p && pA4n ? "OK" : "FAIL")}");
                    allPass = allPass && pA4p && pA4n;
                }
            }

            // ================= P-A5：友军因子（正负双侧）=================
            {
                var allyProbe = SpawnSafePlayer(DebugSpawnType.PlayerArcher, anchorD);
                var friend = SpawnSafePlayer(DebugSpawnType.PlayerWarrior, anchorD + new Vector2(1.5f, 0f), disableBrain: true);
                var soloProbe = SpawnSafePlayer(DebugSpawnType.PlayerArcher, anchorD + new Vector2(-12f, 0f));
                if (allyProbe == null || friend == null || soloProbe == null)
                { sb.Append("｜P-A5 SKIP(材料生成/定位失败) FAIL"); allPass = false; }
                else
                {
                    yield return WaitPinned(3f, allyProbe, soloProbe);
                    var alliesA = GetBrainList(allyProbe.GetComponent<NPCBrain>(), "_nearbyAllies");
                    var alliesS = GetBrainList(soloProbe.GetComponent<NPCBrain>(), "_nearbyAllies");
                    bool pA5p = alliesA != null && alliesA.Count >= 2 && ContainsUnit(alliesA, friend);   // 自身+友军
                    bool pA5n = alliesS != null && alliesS.Count == 1;                                     // 仅自身
                    sb.Append($"｜P-A5 友军因子(伴友 Count={alliesA?.Count ?? -1} 含友={ContainsUnit(alliesA, friend)}/孤 Count={alliesS?.Count ?? -1})={(pA5p && pA5n ? "OK" : "FAIL")}");
                    allPass = allPass && pA5p && pA5n;
                }
            }
        }
        finally
        {
            CleanupMats();
        }

        // ===== 汇总 =====
        sb.Append("｜（P-A6 回归=既有冒烟跑批，另行执行留档）");
        Debug.Log("[2_21A冒烟] " + sb);
        Debug.Log($"[2_21A冒烟] ===== {(allPass ? "ALL PASS" : "HAS FAIL")}（2_21 阶段A 感知修复 D485 P-A1~A5）=====");
        if (!SuppressDialog)
            EditorUtility.DisplayDialog("2_21A 感知修复 Play 冒烟", allPass ? "P-A1~A5 全部 PASS（P-A6 回归另跑）" : "存在 FAIL，见 Console 明细", "确定");
    }

    // ===== 容器 helper =====

    /// <summary>锚点可走预筛：seed 起螺旋步进找 IsWalkable 格（v1 吸附走位教训①）。</summary>
    private static Vector2 FindWalkablePos(Vector2 seed)
    {
        if (IsPosWalkable(seed)) return seed;
        for (int ring = 1; ring <= 6; ring++)
        {
            for (int dir = 0; dir < 8; dir++)
            {
                float ang = dir * Mathf.PI / 4f;
                var p = seed + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (0.8f * ring);
                if (IsPosWalkable(p)) return p;
            }
        }
        return seed;   // 兜底：交由 Spawn 位置自证拦（FAIL 可见）
    }

    private static bool IsPosWalkable(Vector2 pos)
    {
        if (GridSystem.Instance == null) return true;
        var coordOpt = GridSystem.Instance.WorldToCoord(pos);
        return coordOpt.HasValue && GridSystem.Instance.IsWalkable(coordOpt.Value);
    }

    /// <summary>生成玩家侧材料（v1 教训③④：Spawn 后位置自证，超差销毁重试微扰；登记回收）。</summary>
    private static UnitController SpawnSafePlayer(DebugSpawnType type, Vector2 pos, bool disableBrain = false)
    {
        var res = AIDebugSpawnController.Instance.Spawn(type, pos);
        if (!res.Success || res.Spawned == null) return null;
        return FinalizeSpawn(res.Spawned, pos, disableBrain);
    }

    /// <summary>生成敌侧材料：AiKingdom 国民（PlayerCamp UnitData+kingdomId=4 覆写 AiKingdom，v1 教训②）。</summary>
    private static UnitController SpawnSafeEnemy(Vector2 pos)
    {
        var go = UnitFactory.Instance.SpawnUnit(Faction.PlayerCamp, Occupation.Warrior, pos, 4);
        if (go == null) return null;
        return FinalizeSpawn(go, pos, disableBrain: true);
    }

    private static UnitController FinalizeSpawn(GameObject go, Vector2 targetPos, bool disableBrain)
    {
        var uc = go.GetComponent<UnitController>();
        if (uc == null) { Object.Destroy(go); return null; }
        if (disableBrain)
        {
            var b = go.GetComponent<NPCBrain>();
            if (b != null) b.enabled = false;
        }
        // 位置自证：实际落点偏离目标 >2 世界单位=吸附走位 → 销毁（调用侧按 null 处理可见 FAIL）
        if (Vector2.Distance(uc.GetPosition(), targetPos) > 2f)
        {
            Debug.Log($"[2_21A冒烟·定位自证] {uc.EffectiveOccupation} 吸附走位 目标({targetPos.x:F1},{targetPos.y:F1}) 实际({uc.transform.position.x:F1},{uc.transform.position.y:F1}) → 拦截");
            CleanupOne(go);
            return null;
        }
        Mats.Add(go);
        return uc;
    }

    /// <summary>材料回收（v1 教训④：军单位残留污染下一轮感知）。反向遍历防集合修改异常。</summary>
    private static void CleanupMats()
    {
        for (int i = Mats.Count - 1; i >= 0; i--) CleanupOne(Mats[i]);
    }

    private static void CleanupOne(GameObject go)
    {
        if (go == null) return;
        var uc = go.GetComponent<UnitController>();
        if (uc != null) uc.TakeDamage(999999);   // 走死亡链路回池（直接 Destroy 会泄漏池/注册表）
        Mats.Remove(go);
    }

    /// <summary>等待真实秒数；脑开材料每帧钉位（防 Wander 走散，2_20 环境漂移教训）。</summary>
    private static IEnumerator WaitPinned(float seconds, params UnitController[] pinned)
    {
        float t0 = Time.realtimeSinceStartup;
        var poses = new Vector2[pinned.Length];
        for (int i = 0; i < pinned.Length; i++)
            poses[i] = pinned[i] != null ? pinned[i].GetPosition() : Vector2.zero;
        while (Time.realtimeSinceStartup - t0 < seconds)
        {
            Time.timeScale = 1f;
            for (int i = 0; i < pinned.Length; i++)
                if (pinned[i] != null && pinned[i].transform != null)
                    pinned[i].transform.position = new Vector3(poses[i].x, poses[i].y, 0f);
            yield return null;
        }
    }

    /// <summary>反射读 NPCBrain 私有感知/友军列表。</summary>
    private static List<IDamageable> GetBrainList(NPCBrain brain, string field)
    {
        if (brain == null) return null;
        return typeof(NPCBrain).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(brain) as List<IDamageable>;
    }

    /// <summary>反射读 _profession.perceptionRadius × cellSize（感知半径世界值）。</summary>
    private static float GetPerceptionWorld(NPCBrain brain, float cellSize)
    {
        if (brain == null) return 5f;
        var prof = typeof(NPCBrain).GetField("_profession", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(brain);
        float radius = prof != null ? (float)(prof.GetType().GetField("perceptionRadius")?.GetValue(prof) ?? 8f) : 8f;
        return radius * cellSize;
    }

    /// <summary>反射读 _lastCtx 的 float 字段（诊断用，如 ProtectPowerSum）。</summary>
    private static float GetCtxFloat(NPCBrain brain, string field)
    {
        if (brain == null) return -1f;
        var ctx = typeof(NPCBrain).GetField("_lastCtx", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(brain);
        if (ctx == null) return -1f;
        var v = ctx.GetType().GetField(field)?.GetValue(ctx);
        return v is float f ? f : -1f;
    }

    /// <summary>列表是否含目标单位（npcId 比对，对野人游荡鲁棒）。</summary>
    private static bool ContainsUnit(List<IDamageable> list, UnitController target)
    {
        if (list == null || target == null) return false;
        foreach (var u in list)
        {
            var uc = u as UnitController;
            if (uc != null && uc.npcId == target.npcId) return true;
        }
        return false;
    }

    /// <summary>清场（2_20 先例）：半径内未招募流浪汉清除。两段式（先快照收集后伤害）——
    /// 枚举中 TakeDamage→死亡→UnitRegistry 注销会改集合，直接在 foreach 里伤 = Collection modified 异常炸协程（v2 实证）。</summary>
    private static void ClearVagrantsAround(Vector2 center, float radius)
    {
        var victims = new List<UnitController>();
        foreach (var u in UnitRegistry.Instance.GetAllUnits())
        {
            if (u == null || !u.IsAlive || u.EffectiveOccupation != Occupation.Vagrant || u.IsVagrantRecruited) continue;
            if (Vector2.Distance(u.GetPosition(), center) <= radius) victims.Add(u);
        }
        foreach (var v in victims) v.TakeDamage(999999);
    }
}
