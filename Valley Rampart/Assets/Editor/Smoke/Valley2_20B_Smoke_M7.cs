using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using static BuildingFactory;

// ============================================================================
//  2_20 M6+M7 专属建筑/兵种/机器 Play 冒烟（Q10 批3，D490~D497）
//  用法：Play 上下文（先 Play 再点）——菜单「Valley/验证/2_20B_M7种族专属冒烟」。
//  前置：批3 资产已落盘（四专属建筑+7 兵种+3 机器+2 弹药+训练 17 条+RaceDef 回填）。
//
//  探针（正/负双侧，自适应玩家国族 playerRace=GetKingdomRace(0)）：
//   P1 数据域：Occ 尾插 28~37 逐值 + 10 兵种/机器资产可载
//   P2 训练链：17 条结构（重装已除/骑兵 raceId=0/练兵场 Lv2 专属×4/战营×2/射箭场×3）
//   P3 RaceDef 回填：4 资产 exclusiveBuildingDef+exclusiveUnitDefs 值
//   P4 机器成本：SiegeProductionConfig 三新 cost 非零（臼炮最贵梯度）
//   P5 熔炉正负：无熔炉 GetGatherMul(Ore)=mineMul；建 LeyForge → ×1.4（矮人 1.3→1.82）
//   P6 学院正负：无学院 HasExclusiveBuilding=false；建 WarAcademy → true
//   P7 训练过滤（自适应国族）：WarCamp 兽人专有（非兽人空=负）；练兵场 Lv2 矮人专有（非矮人空=负）；
//      共通战士/弓手全族有；Barracks 无重装 + 骑兵 raceId=0
//   P8 机器白名单：ProduceMachine 日志分流（玩家=矮人：Mortar 过白名单/ram 拒）
//   P9 磐石减伤：ApplyDamage(远程)=减伤45%（20→11，defense=0）
//   P10 攻城槌：对单位 0 伤 + 对建筑×2
//   P11 狂战 buff：击杀事件→Frenzy.Stacks==1（正）+非击杀（拆除）不叠（负）
//   P12 兽人战利品：兽人击杀→ChestManager 箱+1
//   P13 限建/门禁：玩家=人类 → WarAcademy 可建第二次拒（限建1）；非人类 → WarAcademy 种族门禁拒（负）
//
//  布局注：全部探针实体用 UnitFactory.SpawnUnit 正规链路（含 TaskScheduler 注册）+ 世界已有（冒烟自建）；
//  探针结束自动回收实体（DestroyImmediate + 注册表清退），防污染下一轮。
// ============================================================================
public static class Valley2_20B_Smoke_M7
{
    [MenuItem("Valley/验证/2_20B_M7种族专属冒烟")]
    public static void Run()
    {
        var wm = Object.FindAnyObjectByType<WorldManager>();
        if (wm == null) { Debug.LogError("[2_20B冒烟] 未找到 WorldManager——请在 Play 上下文执行（先 Play 再点菜单）。"); return; }
        new GameObject("M7_SmokeRunner").AddComponent<M7SmokeHost>().Host(RunCoroutine());
    }

    private static IEnumerator RunCoroutine()
    {
        // ===== 等 Ready（阶段1 完成，第一轮 EnterGame 前；后续轮次阶段1 已完成）=====
        var lm = LoadManager.Instance;
        float readyT0 = Time.realtimeSinceStartup;
        while (lm == null || lm.CurrentPhase == LoadPhase.Booting
               || WorldManager.Instance == null || UnitDataManager.Instance == null
               || UnitFactory.Instance == null || DamageSystem.Instance == null || ChestManager.Instance == null)
        {
            yield return null;
            if (Time.realtimeSinceStartup - readyT0 > 60f)
            {
                Debug.LogError("[2_20B冒烟] 等待 Ready 超时(60s)。"); yield break;
            }
        }
        yield return new WaitForSeconds(0.2f);

        // ===== D520 多轮自动跑：四族固定 seed 各 1 局（4 轮）+ 换 seed 2 轮（周批回归）=====
        // 每轮=SmokeApi.EnterGame（等价用户进局真实链路）→ P1~P13 探针（自适应国族）→ SmokeApi.ResetWorldForNext（同场景清场）
        var rounds = new[] {
            (raceId: RaceIds.Human, seed: 22360, raceName: "人类"),
            (raceId: RaceIds.Elf,   seed: 22360, raceName: "精灵"),
            (raceId: RaceIds.Dwarf, seed: 22360, raceName: "矮人"),
            (raceId: RaceIds.Orc,   seed: 22360, raceName: "兽人"),
            (raceId: RaceIds.Dwarf, seed: 7841,  raceName: "矮人·换seed"),
            (raceId: RaceIds.Orc,   seed: 31337, raceName: "兽人·换seed"),
        };

        MapData lastMap = null;               // 上轮 ActiveMap 引用（跨轮「新实例」断言基准）
        List<Object> prevCleanup = null;      // 上轮探针实体引用（跨轮「无残留」断言基准）

        for (int i = 0; i < rounds.Length; i++)
        {
            var round = rounds[i];
            var sb = new StringBuilder();
            bool allPass = true;
            var cleanup = new List<Object>();

            void Check(bool ok, string name, string detail)
            {
                allPass &= ok;
                sb.Append(ok ? "PASS" : "FAIL").Append(" ").Append(name).Append(" :: ").Append(detail).Append('\n');
                Debug.Log((ok ? "[2_20B][PASS] " : "[2_20B][FAIL] ") + name + " :: " + detail);
            }

            Debug.Log($"[2_20B冒烟] ============ 第 {i + 1}/{rounds.Length} 轮 族={round.raceName}(raceId={round.raceId}) seed={round.seed} ============");

            // 进局（SmokeApi：等价用户进局真实链路 + ActiveMap 幂等守卫）
            SmokeApi.EnterGame(new NewGameConfig
            {
                raceId = round.raceId,
                worldSeed = round.seed,
                mapSeed = round.seed,
                difficulty = 2,
                worldSize = WorldSize.Medium,
                kingdomName = "冒烟王国" + (i + 1),
                selectedSlotId = "smoke_" + (i + 1),
            });

            // 等 ActiveMap 就绪
            float worldT0 = Time.realtimeSinceStartup;
            while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null)
            {
                yield return null;
                if (Time.realtimeSinceStartup - worldT0 > 120f)
                {
                    Debug.LogError("[2_20B冒烟] 等待世界就绪超时(120s)。"); yield break;
                }
            }
            yield return null; yield return null;   // 网格/AI 系统起跑余量

            // ===== 跨轮污染负探针（验收标准4，行为级：ActiveMap 新实例 + 上轮探针实体无残留）=====
            if (lastMap != null)
                Check(WorldManager.Instance.ActiveMap != lastMap, "R" + (i + 1) + " 跨轮 ActiveMap 新实例",
                    "旧引用=" + lastMap + " 新=" + WorldManager.Instance.ActiveMap);
            if (prevCleanup != null)
            {
                bool oldGone = true;
                foreach (var r in prevCleanup)
                    if (r != null) { oldGone = false; break; }
                Check(oldGone, "R" + (i + 1) + " 上轮探针实体无残留", "旧单位/建筑引用 " + (oldGone ? "全已销毁" : "仍有存活"));
            }
            lastMap = WorldManager.Instance.ActiveMap;
            prevCleanup = cleanup;

            int playerRace = KingdomRace.GetKingdomRace(0);
            var uf = UnitFactory.Instance;
            var ds = DamageSystem.Instance;

        // ===== P1 数据域：Occ 尾插 + 资产可载 =====
        Check((int)Occupation.Berserker == 28 && (int)Occupation.WolfRider == 29
            && (int)Occupation.Musqueteer == 30 && (int)Occupation.Bedrock == 31
            && (int)Occupation.Ranger == 32 && (int)Occupation.Windwalker == 33
            && (int)Occupation.DeerRider == 34 && (int)Occupation.Mortar == 35
            && (int)Occupation.VineCatapult == 36 && (int)Occupation.Ram == 37,
            "P1 Occ 尾插 28~37", "枚举值全对（2_20.1 27~36 系设计占位，Monster=27 已占故偏移）");
        string[] profNames = { "Orc_Berserker","Orc_WolfRider","Dwarf_Musqueteer","Dwarf_Bedrock",
            "Elf_Ranger","Elf_Windwalker","Elf_DeerRider","Dwarf_Mortar","Elf_VineCatapult","Orc_Ram" };
        bool allLoad = true;
        foreach (var n in profNames)
            allLoad &= Resources.Load<NpcProfessionDef>("UnitData/" + n) != null;
        Check(allLoad, "P1 十兵种/机器资产可载", "UnitData 全 Load 非 null");

        // ===== P2 训练链结构 =====
        var tc = Resources.Load<TrainingConfig>("Config/TrainingConfig");
        bool hasHeavy = false, cavalryRace0 = false, hasLv2DwarfMus = false, hasWarCampBer = false, hasArcRanger = false;
        foreach (var t in tc.trainings)
        {
            if (t.toOccupation == Occupation.HeavyWarrior) hasHeavy = true;
            if (t.toOccupation == Occupation.Cavalry && t.raceId == RaceIds.Human) cavalryRace0 = true;
            if (t.toOccupation == Occupation.Musqueteer && t.raceId == RaceIds.Dwarf && t.minBuildingLevel == 2 && t.buildingId == "TrainingCamp") hasLv2DwarfMus = true;
            if (t.toOccupation == Occupation.Berserker && t.raceId == RaceIds.Orc && t.buildingId == "WarCamp") hasWarCampBer = true;
            if (t.toOccupation == Occupation.Ranger && t.raceId == RaceIds.Elf && t.buildingId == "ArcheryRange") hasArcRanger = true;
        }
        Check(tc.trainings.Length == 17, "P2 训练 17 条", "重建后共 " + tc.trainings.Length + " 条");
        Check(!hasHeavy, "P2 重装条目已退役", "D492：枚举保留训练条目移除（Barracks 无 HeavyWarrior）");
        Check(cavalryRace0, "P2 骑兵→人类专属", "D490：骑兵 raceId=0 金5/2天占位");
        Check(hasLv2DwarfMus && hasWarCampBer && hasArcRanger, "P2 专属训练唯一入口", "练兵场Lv2矮人火枪+战营狂战+射箭场游侠均在");

        // ===== P3 RaceDef 回填 =====
        var rdH = Resources.Load<RaceDef>("Config/Races/Race_Human");
        var rdE = Resources.Load<RaceDef>("Config/Races/Race_Elf");
        var rdD = Resources.Load<RaceDef>("Config/Races/Race_Dwarf");
        var rdO = Resources.Load<RaceDef>("Config/Races/Race_Orc");
        Check(rdH.exclusiveBuildingDef != null && rdH.exclusiveBuildingDef.id == "WarAcademy" && rdH.exclusiveUnitDefs != null && rdH.exclusiveUnitDefs.Length == 3,
            "P3 人类回填", "WarAcademy+3 专属兵（弩手/盾卫/战马骑士）");
        Check(rdE.exclusiveBuildingDef != null && rdE.exclusiveBuildingDef.id == "ArcheryRange" && rdE.exclusiveUnitDefs != null && rdE.exclusiveUnitDefs.Length == 3,
            "P3 精灵回填", "ArcheryRange+3 专属兵（游侠/风行者/鹿骑）");
        Check(rdD.exclusiveBuildingDef != null && rdD.exclusiveBuildingDef.id == "LeyForge" && rdD.exclusiveUnitDefs != null && rdD.exclusiveUnitDefs.Length == 2,
            "P3 矮人回填", "LeyForge+2 专属兵（火枪/磐石）");
        Check(rdO.exclusiveBuildingDef != null && rdO.exclusiveBuildingDef.id == "WarCamp" && rdO.exclusiveUnitDefs != null && rdO.exclusiveUnitDefs.Length == 2,
            "P3 兽人回填", "WarCamp+2 专属兵（狂战/狼骑）");

        // ===== P4 机器成本 =====
        var sp = Resources.Load<SiegeProductionConfig>("Config/SiegeProductionConfig");
        Check(sp != null && !sp.mortarCost.IsZero && !sp.vineCatapultCost.IsZero && !sp.ramCost.IsZero,
            "P4 机器成本", "臼炮" + sp.mortarCost.gold + "g" + sp.mortarCost.stone + "s/藤蔓" + sp.vineCatapultCost.gold + "g/槌" + sp.ramCost.gold + "g（臼炮最贵梯度）");
        Check(sp.mortarCost.gold > sp.ballistaCost.gold, "P4 臼炮最贵", "梯度矮＞人＞精＞兽（mortar>" + sp.ballistaCost.gold + "g）");

        // ===== P5 熔炉正负 =====
        float baseMine = KingdomRace.GetGatherMul(0, ResourceType.Ore);
        Check(baseMine == (KingdomRace.GetKingdomRaceDef(0) != null ? KingdomRace.GetKingdomRaceDef(0).mineMul : 1f),
            "P5 无熔炉基线", "GetGatherMul(Ore)=" + baseMine);
        var ley = BuildExclusiveBuilding("LeyForge", rdD.exclusiveBuildingDef != null ? rdD.exclusiveBuildingDef : Resources.Load<BuildingDef>("Buildings/LeyForge"), cleanup);
        bool leyBuilt = ley != null;
        float withLey = KingdomRace.GetGatherMul(0, ResourceType.Ore);
        Check(leyBuilt && Mathf.Abs(withLey - baseMine * 1.4f) < 0.001f, "P5 熔炉+40%",
            "建 LeyForge 后 " + withLey + " = 基线 " + baseMine + " ×1.4（乘算口径 1.82 矮人）");
        if (ley != null) DestroyBuildingSafe(ley);

        // ===== P6 学院正负 =====
        Check(!KingdomRace.HasExclusiveBuilding(0, "WarAcademy"), "P6 无学院负", "HasExclusiveBuilding=false");
        var aca = BuildExclusiveBuilding("WarAcademy", rdH.exclusiveBuildingDef != null ? rdH.exclusiveBuildingDef : Resources.Load<BuildingDef>("Buildings/WarAcademy"), cleanup);
        bool acaBuilt = aca != null;
        Check(acaBuilt && KingdomRace.HasExclusiveBuilding(0, "WarAcademy"), "P6 学院正", "建后 HasExclusiveBuilding=true（训练时长×0.75 叠乘点在 TryTrain）");
        if (aca != null) DestroyBuildingSafe(aca);

        // ===== P7 训练过滤（自适应国族）=====
        var ts = TrainingSystem.Instance;
        var camp = BuildExclusiveBuilding("WarCamp", rdO.exclusiveBuildingDef != null ? rdO.exclusiveBuildingDef : Resources.Load<BuildingDef>("Buildings/WarCamp"), cleanup);
        if (camp != null)
        {
            var wcTrain = ts.GetTrainings(camp);
            bool hasBerserker = AnyTo(wcTrain, Occupation.Berserker);
            bool hasWolf = AnyTo(wcTrain, Occupation.WolfRider);
            if (playerRace == RaceIds.Orc)
                Check(hasBerserker && hasWolf, "P7 战营兽人正", "玩家=兽人 → 狂战/狼骑可训（唯一入口）");
            else
                Check(!hasBerserker && !hasWolf, "P7 战营异族负", "玩家=" + playerRace + " ≠ 兽人 → 战营无专属条目（D419 唯一入口跨族不可训）");
            DestroyBuildingSafe(camp);
        }
        else Check(false, "P7 战营", "WarCamp 建造失败");

        var tcamp = BuildTrainingCampLv2(cleanup);
        if (tcamp != null)
        {
            var tTrain = ts.GetTrainings(tcamp);
            bool hasWarrior = AnyTo(tTrain, Occupation.Warrior);   // 共通（raceId=-1）全族有
            bool hasMus = AnyTo(tTrain, Occupation.Musqueteer);
            bool hasBed = AnyTo(tTrain, Occupation.Bedrock);
            bool hasCross = AnyTo(tTrain, Occupation.Crossbowman);
            Check(hasWarrior, "P7 练兵场共通正", "战士(raceId=-1)全族可训");
            if (playerRace == RaceIds.Dwarf)
                Check(hasMus && hasBed && !hasCross, "P7 练兵场Lv2矮人正", "火枪/磐石可训 + 弩手(人类专属)不可训（负）");
            else if (playerRace == RaceIds.Human)
                Check(hasCross && !hasMus, "P7 练兵场Lv2人类正", "弩手可训 + 火枪(矮人专属)不可训（负）");
            else
                Check(!hasMus && !hasBed && !hasCross, "P7 练兵场Lv2异族负", "玩家=" + playerRace + " → 矮/人专属均不可训，仅共通");
            DestroyBuildingSafe(tcamp);
        }
        else Check(false, "P7 练兵场", "TrainingCamp Lv2 建造失败");

        var barracks = BuildBarracks(cleanup);
        if (barracks != null)
        {
            var bTrain = ts.GetTrainings(barracks);
            Check(!AnyTo(bTrain, Occupation.HeavyWarrior), "P7 Barracks 无重装", "共通槽退役（D492）");
            DestroyBuildingSafe(barracks);
        }
        else Check(false, "P7 Barracks", "Barracks 建造失败");

        // ===== P8 机器白名单（反射调私有 IsRaceAllowedMachine）=====
        {
            var m = typeof(SiegeProductionSystem).GetMethod("IsRaceAllowedMachine",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (m != null)
            {
                bool dwarfMortar = (bool)m.Invoke(null, new object[] { RaceIds.Dwarf, Occupation.Mortar });
                bool dwarfRam = (bool)m.Invoke(null, new object[] { RaceIds.Dwarf, Occupation.Ram });
                bool humanBallista = (bool)m.Invoke(null, new object[] { RaceIds.Human, Occupation.Ballista });
                bool anySiege = (bool)m.Invoke(null, new object[] { RaceIds.Human, Occupation.SiegeMachine });
                Check(dwarfMortar && !dwarfRam && humanBallista && !anySiege, "P8 机器白名单",
                    "矮人臼炮✓/矮人槌✗/人类重弩✓/投掷机退役✗（D496/D497 per-race）");
            }
            else Check(false, "P8 机器白名单", "反射 IsRaceAllowedMachine 失败");
        }

        // ===== P9 磐石减伤 =====
        var buc = SpawnUnitDirect(Occupation.Bedrock, new Vector2(20, 6));
        if (buc != null)
        {
            cleanup.Add(buc.gameObject);
            int dmg = ds.ApplyDamage(null, buc, 20, 0f, true);   // 远程单体：减伤45% 生效（ArmorK 防御公式后×0.55 取整，探针断言动态算）
            int baseDmg9 = ds.CalculateDamage(20, buc.Defense);
            int expect9 = Mathf.Max(1, Mathf.RoundToInt(baseDmg9 * 0.55f));
            Check(dmg == expect9, "P9 磐石远程减伤", "装甲公式基础=" + baseDmg9 + " ×0.55=" + dmg + "（rangedDamageReduce=0.45 生效，D494）");
            Object.DestroyImmediate(buc.gameObject);
        }
        else Check(false, "P9 磐石", "直构失败（GetData 空/Initialize 异常）");

        // ===== P10 攻城槌对单位0/对建筑×2 =====
        var ruc = SpawnUnitDirect(Occupation.Ram, new Vector2(22, 6));
        if (ruc != null)
        {
            cleanup.Add(ruc.gameObject);
            var dummy = SpawnUnitDirect(Occupation.Warrior, new Vector2(24, 6));
            if (dummy != null)
            {
                cleanup.Add(dummy.gameObject);
                int unitDmg = ds.ApplyDamage(ruc, dummy, 20);
                Check(unitDmg == 0, "P10 攻城槌对单位0", "unitDamageMul=0 → " + unitDmg + "（纯拆墙数值特性，D497）");
                Object.DestroyImmediate(dummy.gameObject);
            }
            var wall = BuildSimpleBuilding("wall", Resources.Load<BuildingDef>("Buildings/wall"), cleanup);
            if (wall != null)
            {
                int bldDmg = ds.ApplyDamage(ruc, wall, 20);
                int wallBase = ds.CalculateDamage(20, wall.Defense);
                Check(bldDmg == Mathf.Max(1, Mathf.RoundToInt(wallBase * 2f)), "P10 攻城槌对建筑×2", "基础" + wallBase + " ×2=" + bldDmg + "（buildingDamageMul=2，D497）");
                DestroyBuildingSafe(wall);
            }
            else Check(false, "P10 墙", "wall 建造失败");
            Object.DestroyImmediate(ruc.gameObject);
        }
        else Check(false, "P10 攻城槌", "直构失败");

        // ===== P11 狂战 buff（击杀正/拆除负）=====
        var buc2 = SpawnUnitDirect(Occupation.Berserker, new Vector2(26, 6));
        if (buc2 != null)
        {
            cleanup.Add(buc2.gameObject);
            var vuc = SpawnUnitDirect(Occupation.Warrior, new Vector2(28, 6));
            vuc.kingdomId = -1;   // 材料隔离：探针死亡不进玩家幸福桶/王国口径（防 HappinessSystem 首日空桶 KeyNotFound 污染）
            if (vuc != null)
            {
                cleanup.Add(vuc.gameObject);
                // 负探针：拆除死因不叠层
                EventBus.Publish(new UnitDiedEvent(vuc, vuc.GetFaction(), vuc.GetPosition(), buc2, DeathCause.Demolished));
                Check(buc2.Frenzy == null || buc2.Frenzy.Stacks == 0, "P11 拆除不叠层", "Cause=Demolished → Stacks=0（负）");
                // 正探针：被击杀死因 → 叠层1
                EventBus.Publish(new UnitDiedEvent(vuc, vuc.GetFaction(), vuc.GetPosition(), buc2, DeathCause.Killed));
                Check(buc2.Frenzy != null && buc2.Frenzy.Stacks == 1, "P11 击杀叠层", "Stacks=" + (buc2.Frenzy != null ? buc2.Frenzy.Stacks : -1) + "（+20%移速/+30%攻速/层，D490）");
                Object.DestroyImmediate(vuc.gameObject);
            }
            Object.DestroyImmediate(buc2.gameObject);
        }
        else Check(false, "P11 狂战", "直构失败");

        // ===== P12 兽人战利品 =====
        int chestBefore = ChestManager.Instance.Count;
        var okuc = SpawnUnitDirect(Occupation.Berserker, GridSystem.Instance.CoordToWorld(new GridCoord(40, 8)));   // 合法世界坐标（等距反解不越界，WorldToCoord 可落箱）
        if (okuc != null)
        {
            cleanup.Add(okuc.gameObject);
            okuc.raceId = RaceIds.Orc;   // 材料强制兽人（玩家可能非兽人）
            var puc = SpawnUnitDirect(Occupation.Warrior, GridSystem.Instance.CoordToWorld(new GridCoord(41, 8)));
            puc.kingdomId = -1;   // 材料隔离：探针死亡不进玩家幸福桶/王国口径
            if (puc != null)
            {
                cleanup.Add(puc.gameObject);
                EventBus.Publish(new UnitDiedEvent(puc, puc.GetFaction(), puc.GetPosition(), okuc, DeathCause.Killed));
                int chestAfter = ChestManager.Instance.Count;
                Check(chestAfter == chestBefore + 1, "P12 兽人战利品", "兽人击杀 → 箱 " + chestBefore + "→" + chestAfter + "（D493 金0.5~1 占位，谁拾取归谁）");
                Object.DestroyImmediate(puc.gameObject);
            }
            Object.DestroyImmediate(okuc.gameObject);
        }
        else Check(false, "P12 兽人", "直构失败");

        // ===== P13 限建/门禁（自适应国族）=====
        var bc = Object.FindAnyObjectByType<BuildController>();
        var waDef = Resources.Load<BuildingDef>("Buildings/WarAcademy");
        if (bc != null && waDef != null)
        {
            if (playerRace == RaceIds.Human)
            {
                // 限建1 正探针：手动建一栋 WarAcademy（Active）→ TryBuild 同 id 应被 uniquePerKingdom 拒（不真建第二次）
                var existing = BuildExclusiveBuilding("WarAcademy", waDef, cleanup);
                bool rejected = !bc.TryBuild(waDef, FindFreeSpot(), GateOrientation.Horizontal, 0);
                Check(existing != null && rejected, "P13 限建1", "已建 WarAcademy 后 TryBuild 同 id 被拒（uniquePerKingdom，防叠乘失控）");
                if (existing != null) DestroyBuildingSafe(existing);
            }
            else
            {
                bool rejected = !bc.TryBuild(waDef, FindFreeSpot(), GateOrientation.Horizontal, 0);
                Check(rejected, "P13 异族拒建", "玩家=" + playerRace + " ≠ 人类 → WarAcademy 种族门禁拒（负）");
            }
        }
        else Check(false, "P13 建造", "BuildController/WarAcademy 不可用");

        // 清理残留（防污染下一轮）
        foreach (var o in cleanup) if (o != null) Object.DestroyImmediate(o);

        // ===== 本轮汇总 =====
        Debug.Log("[2_20B冒烟] ==== 第 " + (i + 1) + " 轮汇总 " + (allPass ? "ALL PASS" : "有 FAIL") + " ====\n" + sb);

        // ===== 清场（WorldLifecycle 同场景重建编排，无 LoadScene 兜底）=====
        SmokeApi.ResetWorldForNext();

        // 清场后负探针（验收标准4，行为级：世界已清空 + 单位注册表真空）
        Check(WorldManager.Instance.ActiveMap == null, "R" + (i + 1) + " 清场 ActiveMap 空",
            "WorldManager.ResetState 清 _world 生效");
        Check(UnitRegistry.Instance.Count == 0, "R" + (i + 1) + " 清场 UnitRegistry 真空",
            "count=" + UnitRegistry.Instance.Count);

        yield return null;   // 陷阱2：留一帧让 Destroy 落地（GameState=Loading 下安全），再进下一轮
        }

        // ===== 全部轮次完成 =====
        SmokeApi.QuitSmoke();
    }

    // ===== 辅助 =====

    private static bool AnyTo(System.Collections.Generic.IReadOnlyList<TrainingDef> list, Occupation to)
    {
        if (list == null) return false;
        for (int i = 0; i < list.Count; i++)
            if (list[i].toOccupation == to) return true;
        return false;
    }

    private static GridCoord FindFreeSpot()
    {
        var grid = GridSystem.Instance;
        if (grid == null) return new GridCoord(50, 50);
        for (int x = 44; x < 64; x++)
            for (int y = 4; y < 24; y++)
            {
                var c = new GridCoord(x, y);
                if (!grid.IsObstacle(c) && !grid.IsOccupied(c)) return c;
            }
        return new GridCoord(50, 50);
    }

    /// <summary>安全销毁探针建筑（先退注册表防脏条目，再销毁实体）。</summary>
    private static void DestroyBuildingSafe(Building b)
    {
        if (b == null) return;
        if (BuildingRegistry.Instance != null) BuildingRegistry.Instance.Unregister(b);
        Object.DestroyImmediate(b.gameObject);
    }

    /// <summary>
    /// 探针单位直构（2_20 M7：新兵种资产无美术 prefab，UnitFactory.SpawnUnit 拒生成 → 走 UnitController.Initialize 直构）。
    /// 不走 UnitFactory/UnitRegistry 注册（探针实体自回收，不污染单位注册表）。
    /// </summary>
    private static UnitController SpawnUnitDirect(Occupation occ, Vector2 pos)
    {
        var data = UnitDataManager.Instance != null ? UnitDataManager.Instance.GetData(Faction.PlayerCamp, occ) : null;
        if (data == null) return null;
        var go = new GameObject("probe_unit_" + occ);
        go.transform.position = pos;
        var uc = go.AddComponent<UnitController>();
        uc.Initialize(data);
        return uc;
    }

    private static Building BuildExclusiveBuilding(string id, BuildingDef def, List<Object> cleanup)
    {
        if (def == null || BuildingFactory.Instance == null) return null;
        var spot = FindFreeSpot();
        var go = new GameObject("M7Smoke_" + id);
        go.transform.position = GridSystem.Instance != null ? GridSystem.Instance.CoordToWorld(spot) : new Vector3(spot.x, spot.y, 0);
        var b = go.AddComponent<Building>();
        b.def = def;
        b.coord = spot;
        b.kingdomId = 0;
        b.level = 1;
        b.faction = Faction.PlayerCamp;
        b.state = BuildingState.Active;   // 限建1/专属建筑查询依赖 IsActive（2_20 M6）
        BuildingRegistry.Instance.Register(b);
        cleanup.Add(go);
        return b;
    }

    private static Building BuildTrainingCampLv2(List<Object> cleanup)
    {
        var def = Resources.Load<BuildingDef>("Buildings/TrainingCamp");
        if (def == null || BuildingFactory.Instance == null) return null;
        var spot = FindFreeSpot();
        var go = new GameObject("M7Smoke_TrainingCampLv2");
        go.transform.position = GridSystem.Instance != null ? GridSystem.Instance.CoordToWorld(spot) : new Vector3(spot.x, spot.y, 0);
        var b = go.AddComponent<Building>();
        b.def = def;
        b.coord = spot;
        b.kingdomId = 0;
        b.level = 2;   // 练兵场 Lv2（minBuildingLevel 门槛探针）
        b.faction = Faction.PlayerCamp;
        b.state = BuildingState.Active;
        BuildingRegistry.Instance.Register(b);
        cleanup.Add(go);
        return b;
    }

    private static Building BuildBarracks(List<Object> cleanup)
    {
        var def = Resources.Load<BuildingDef>("Buildings/Barracks");
        if (def == null || BuildingFactory.Instance == null) return null;
        var spot = FindFreeSpot();
        var go = new GameObject("M7Smoke_Barracks");
        go.transform.position = GridSystem.Instance != null ? GridSystem.Instance.CoordToWorld(spot) : new Vector3(spot.x, spot.y, 0);
        var b = go.AddComponent<Building>();
        b.def = def;
        b.coord = spot;
        b.kingdomId = 0;
        b.level = 1;
        b.faction = Faction.PlayerCamp;
        b.state = BuildingState.Active;
        BuildingRegistry.Instance.Register(b);
        cleanup.Add(go);
        return b;
    }

    private static Building BuildSimpleBuilding(string id, BuildingDef def, List<Object> cleanup)
    {
        if (def == null || BuildingFactory.Instance == null || GridSystem.Instance == null) return null;
        var spot = FindFreeSpot();
        var worldPos = GridSystem.Instance.CoordToWorld(spot);
        // 正规建造（BuildingFactory.CreateBuildingInstance 内部 Initialize 设 hp/maxHp——探针 ApplyDamage 需 CurrentHp>0）
        bool ok = BuildingFactory.Instance.CreateBuildingInstance(def, BuildingType.None, spot,
            new Vector2Int(1, 1), worldPos, false, ResourceGrade.Normal, false, BuildingState.Active, 0);
        var b = ok && BuildingRegistry.Instance != null ? BuildingRegistry.Instance.GetAt(spot) : null;
        if (b != null) cleanup.Add(b.gameObject);
        return b;
    }
}

/// <summary>批3 冒烟宿主（协程调度 + 结果写盘）。</summary>
public class M7SmokeHost : MonoBehaviour
{
    public void Host(IEnumerator routine) => StartCoroutine(Routine(routine));
    private IEnumerator Routine(IEnumerator inner)
    {
        yield return inner;
        Destroy(gameObject);
    }
}
