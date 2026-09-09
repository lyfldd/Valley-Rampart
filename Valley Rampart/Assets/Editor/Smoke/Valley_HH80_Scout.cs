using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  HH.80 P1 三考 seed 侦察容器（Editor-only；观察清单 §三「侦察跑 3~5 分钟自定+报备」）
//  用法：GameScene Play 后菜单「Valley/验证/HH80_侦察」。单容器轮跑 SEEDS 全部
//  （EnterGame 幂等清场重建——2_20B 六轮连续重建先例；QuitSmoke 只在最后退出 Play）。
//  结构性检查四点（仅结构性缺陷升级报裁，否则自定+报备）：
//  ①出生口袋（国锚点间距离过近/被围）②资源不可达 ③邻国过近 ④AI 模板/族分布。
//  结果写文件 Logs/P1/hh80_scout_result.log（console 大缓冲教训）。侦察不计判定证据（D533 口径）。
// ============================================================================
public static class Valley_HH80_Scout
{
    private static readonly int[] SEEDS = { 48271, 69496, 81203 };   // HH.122 六考正门重跑（D585）新 seed 候选（73311=D45 袭扰段报废；历史 20273/22360/52707/7841/31337/16180/31415/27182/57721/51713/21109/60221/90210 全避开）

    [MenuItem("Valley/验证/HH80_侦察")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[HH80侦察] 须先 GameScene 进 Play。"); return; }
        new GameObject("HH80_ScoutRunner").AddComponent<RunHost>().Host(RunCoroutine());
    }

    private class RunHost : MonoBehaviour
    {
        public void Host(IEnumerator routine) => StartCoroutine(routine);
    }

    private static IEnumerator RunCoroutine()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("===== HH.80 P1 三考 seed 侦察 =====");

        foreach (int seed in SEEDS)
        {
            sb.AppendLine($"---- seed {seed} ----");
            var cfg = new NewGameConfig
            {
                worldSeed = seed, mapSeed = seed, raceId = 0, difficulty = 2,
                worldSize = WorldSize.Medium, selectedSlotId = "smoke_scout80", kingdomName = "侦察局"
            };
            SmokeApi.EnterGame(cfg);

            float t0 = Time.realtimeSinceStartup;
            while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null
                   || KingdomRegistry.Instance == null || KingdomRegistry.Instance.Count < 4)
            {
                yield return null;
                if (Time.realtimeSinceStartup - t0 > 120f) { sb.AppendLine("等世界就绪超时"); break; }
            }
            yield return new WaitForSeconds(1f);

            var reg = KingdomRegistry.Instance;
            if (reg != null)
            {
                var all = reg.GetAll();
                for (int i = 0; i < all.Count; i++)
                {
                    var k = all[i];
                    sb.AppendLine($"k{k.id} [{k.name}] race={k.raceId} 工={k.workerCount} 战={k.warriorCount} 金={k.resources.gold} 粮={k.resources.food} 领土mid={(k.Territory != null ? k.Territory.Count : -1)} 阶段={k.scriptPhase}");
                }
                // 国锚点两两距离（出生口袋/邻国过近检查）——用各国单位平均位置近似
                var us = Object.FindObjectsOfType<UnitController>();
                var posByKid = new Dictionary<int, Vector2>();
                var cntByKid = new Dictionary<int, int>();
                int vag = 0;
                for (int i = 0; i < us.Length; i++)
                {
                    var u = us[i];
                    if (u == null || !u.IsAlive) continue;
                    if (u.EffectiveOccupation == Occupation.Vagrant) vag++;
                    if (u.kingdomId <= 0) continue;
                    Vector2 p = u.transform.position;
                    if (posByKid.ContainsKey(u.kingdomId)) { posByKid[u.kingdomId] += p; cntByKid[u.kingdomId]++; }
                    else { posByKid[u.kingdomId] = p; cntByKid[u.kingdomId] = 1; }
                }
                var kids = new List<int>(posByKid.Keys);
                kids.Sort();
                for (int i = 0; i < kids.Count; i++)
                    for (int j = i + 1; j < kids.Count; j++)
                    {
                        float d = Vector2.Distance(posByKid[kids[i]] / cntByKid[kids[i]], posByKid[kids[j]] / cntByKid[kids[j]]);
                        sb.AppendLine($"  dist k{kids[i]}~k{kids[j]} = {d:F1}");
                    }
                var vc = VagrantCampSystem.Instance;
                int camps = vc != null ? vc.FindCamps().Count : -1;
                sb.AppendLine($"  营地={camps} 流浪={vag} 单位总数={us.Length}");
            }
            yield return new WaitForSeconds(1f);   // 观察窗口（EnterGame 下局幂等清场重建）
        }

        sb.AppendLine("===== 侦察完（全部 seed）=====");
        try
        {
            var dir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Logs/P1");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "hh80_scout_result.log"), sb.ToString());
        }
        catch (System.Exception e) { Debug.LogError("[HH80侦察] 结果写文件失败: " + e.Message); }

        SmokeApi.QuitSmoke();
    }
}
